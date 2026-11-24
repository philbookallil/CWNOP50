using System.Reflection;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Orders;

/// <summary>
/// A packname order is checked on the lines it orders. A cart may also hold a line of no packname (added while the
/// PackName feature was off, or with no packname selected): it is not ordered, so it is neither checked nor counted
/// towards nop's minimum order amounts. A payment method that places its own order is the exception: it charges
/// for the whole cart, so such a line refuses it.
/// </summary>
/// <remarks>
/// The service is built without its constructor and prepares the order for real. The customer has no address, so the
/// preparation stops at the billing address, the step after the cart: getting that far means the cart was accepted.
/// </remarks>
[TestFixture]
public class PackNamePlacementCartTests
{
    private const int CustomerId = 5;
    private const int StoreId = 1;
    private const int PlainLineId = 10;
    private const int AnnLineId = 11;
    private const int BobLineId = 12;
    private const decimal LinePrice = 10M;

    private Customer _customer = null!;
    private List<ShoppingCartItem> _cart = null!;
    private List<PackNameShoppingCartItem> _packNameCart = null!;
    private Dictionary<int, string> _lineWarnings = null!;
    private List<int> _checkedLineIds = null!;
    private List<int> _checkedCartIds = null!;
    private List<string> _cartWarnings = null!;
    private bool _checkoutAttributesChecked;
    private OrderSettings _orderSettings = null!;
    private OrderProcessingOverrideService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _customer = new Customer { Id = CustomerId };
        _cart = [Line(PlainLineId), Line(AnnLineId), Line(BobLineId)];
        _packNameCart = [PackNameLine(AnnLineId, 71), PackNameLine(BobLineId, 72)];
        _lineWarnings = [];
        _checkedLineIds = [];
        _checkedCartIds = [];
        _cartWarnings = [];
        _checkoutAttributesChecked = false;
        _orderSettings = new OrderSettings();

        var shoppingCartService = new Mock<IShoppingCartOverrideService>();
        shoppingCartService.Setup(service => service.GetShoppingCartAsync(_customer, ShoppingCartType.ShoppingCart, StoreId,
                It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>()))
            .ReturnsAsync(() => _cart);
        shoppingCartService.Setup(service => service.GetPackNameShoppingCartItemsAsync(_customer, 0, StoreId))
            .ReturnsAsync(() => _packNameCart);
        shoppingCartService.Setup(service => service.GetShoppingCartWarningsAsync(It.IsAny<IList<ShoppingCartItem>>(), "<attributes/>", It.IsAny<bool>()))
            .ReturnsAsync((IList<ShoppingCartItem> cart, string _, bool validateCheckoutAttributes) =>
            {
                _checkedCartIds.AddRange(cart.Select(item => item.Id));
                _checkoutAttributesChecked = validateCheckoutAttributes;
                return _cartWarnings;
            });
        shoppingCartService.Setup(service => service.GetShoppingCartItemWarningsAsync(_customer, ShoppingCartType.ShoppingCart,
                It.IsAny<Product>(), StoreId, It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(new InvocationFunc(invocation =>
            {
                //a line is checked as it is in the cart, whichever cart it was read from
                var arguments = invocation.Arguments;
                var line = _cart.Single(item => item.Id == (int)arguments[10]);
                ((Product)arguments[2]).Id.Should().Be(line.ProductId);
                arguments[4].Should().Be(line.AttributesXml);
                arguments[5].Should().Be(line.CustomerEnteredPrice);
                arguments[6].Should().Be(line.RentalStartDateUtc);
                arguments[7].Should().Be(line.RentalEndDateUtc);
                arguments[8].Should().Be(line.Quantity);
                arguments[9].Should().Be(false, "placing an order never adds lines to the cart");

                _checkedLineIds.Add(line.Id);

                IList<string> warnings = _lineWarnings.TryGetValue(line.Id, out var warning) ? [warning] : [];
                return Task.FromResult(warnings);
            }));

        var productService = new Mock<IProductService>();
        productService.Setup(service => service.GetProductByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => new Product { Id = id });

        var genericAttributeService = new Mock<IGenericAttributeService>();
        genericAttributeService.Setup(service => service.GetAttributeAsync<string>(_customer, NopCustomerDefaults.CheckoutAttributes, StoreId, It.IsAny<string>()))
            .ReturnsAsync("<attributes/>");

        var checkoutAttributeFormatter = new Mock<ICheckoutAttributeFormatter>();
        checkoutAttributeFormatter.Setup(formatter => formatter.FormatAttributesAsync("<attributes/>", _customer, It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync("Gift wrap: yes");

        var totals = new Mock<IOrderTotalCalculationOverrideService>();
        totals.Setup(service => service.GetShoppingCartSubTotalAsync(It.IsAny<IList<ShoppingCartItem>>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<ShoppingCartItem> cart, bool _) => (0M, new List<Discount>(), cart.Count * LinePrice, cart.Count * LinePrice, new SortedDictionary<decimal, decimal>()));
        totals.Setup(service => service.GetShoppingCartTotalAsync(It.IsAny<IList<ShoppingCartItem>>(), It.IsAny<bool?>(), It.IsAny<bool>()))
            .ReturnsAsync((IList<ShoppingCartItem> cart, bool? _, bool _) => (cart.Count * LinePrice, 0M, new List<Discount>(), new List<AppliedGiftCard>(), 0, 0M));

        var customerService = new Mock<ICustomerOverrideService>();
        customerService.Setup(service => service.GetCustomerByIdAsync(CustomerId)).ReturnsAsync(_customer);
        customerService.Setup(service => service.GetAddressesByCustomerIdAsync(CustomerId)).ReturnsAsync(new List<Address>());

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingCurrencyAsync()).ReturnsAsync(new Currency { Rate = 1M });
        workContext.Setup(context => context.GetWorkingLanguageAsync()).ReturnsAsync(new Language());

        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(service => service.GetCurrencyByIdAsync(It.IsAny<int>())).ReturnsAsync(new Currency { Rate = 1M });
        currencyService.Setup(service => service.ConvertFromPrimaryStoreCurrencyAsync(It.IsAny<decimal>(), It.IsAny<Currency>()))
            .ReturnsAsync((decimal amount, Currency _) => amount);

        var priceFormatter = new Mock<IPriceFormatter>();
        priceFormatter.Setup(formatter => formatter.FormatPriceAsync(It.IsAny<decimal>(), true, false))
            .ReturnsAsync((decimal price, bool _, bool _) => $"${price:0}");

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);
        localizationService.Setup(service => service.GetResourceAsync("Checkout.MinOrderSubtotalAmount")).ReturnsAsync("Minimum subtotal {0}");
        localizationService.Setup(service => service.GetResourceAsync("Checkout.MinOrderTotalAmount")).ReturnsAsync("Minimum total {0}");

        _service = TestObject.Create<OrderProcessingOverrideService>();
        SetEverywhere("_shoppingCartService", shoppingCartService.Object);
        SetEverywhere("_orderTotalCalculationService", totals.Object);
        SetEverywhere("_customerService", customerService.Object);
        SetEverywhere("_productService", productService.Object);
        SetEverywhere("_genericAttributeService", genericAttributeService.Object);
        SetEverywhere("_checkoutAttributeFormatter", checkoutAttributeFormatter.Object);
        SetEverywhere("_workContext", workContext.Object);
        SetEverywhere("_currencyService", currencyService.Object);
        SetEverywhere("_languageService", new Mock<ILanguageService>().Object);
        SetEverywhere("_storeMappingService", new Mock<IStoreMappingService>().Object);
        SetEverywhere("_priceFormatter", priceFormatter.Object);
        SetEverywhere("_localizationService", localizationService.Object);
        SetEverywhere("_orderSettings", _orderSettings);
        SetEverywhere("_currencySettings", new CurrencySettings());
    }

    private static ShoppingCartItem Line(int id) => Fill(new ShoppingCartItem(), id);

    private static PackNameShoppingCartItem PackNameLine(int id, int packNameId) => Fill(new PackNameShoppingCartItem { PackNameId = packNameId }, id);

    private static T Fill<T>(T line, int id) where T : ShoppingCartItem
    {
        line.Id = id;
        line.CustomerId = CustomerId;
        line.StoreId = StoreId;
        line.ShoppingCartType = ShoppingCartType.ShoppingCart;
        line.ProductId = 100 + id;
        line.AttributesXml = $"<line id=\"{id}\"/>";
        line.CustomerEnteredPrice = id;
        line.RentalStartDateUtc = new DateTime(2026, 11, id);
        line.RentalEndDateUtc = new DateTime(2026, 12, id);
        line.Quantity = id;
        return line;
    }

    /// <summary>
    /// Set a field on the service and on nop's service beneath it: CW declares some again under its own type
    /// </summary>
    private void SetEverywhere(string name, object value)
    {
        var found = false;

        for (var type = _service.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null)
                continue;

            field.SetValue(_service, value);
            found = true;
        }

        if (!found)
            throw new MissingFieldException(name);
    }

    private static ProcessPaymentRequest Request => new() { StoreId = StoreId, CustomerId = CustomerId, OrderGuid = Guid.NewGuid() };

    private Func<Task> PreparingThePackNameOrder => () => TestObject.InvokeAsync(_service, "PreparePlacePackNameOrderDetailsAsync", Request);

    private async Task CartIsAcceptedAsync() =>
        await PreparingThePackNameOrder.Should().ThrowAsync<NopException>().WithMessage("Billing address is not provided");

    /// <summary>
    /// A container of one of the service's protected types, for the test's customer
    /// </summary>
    private object Container(string typeName)
    {
        for (var type = typeof(OrderProcessingOverrideService); type != null; type = type.BaseType)
        {
            if (type.GetNestedType(typeName, BindingFlags.NonPublic) is not { } containerType)
                continue;

            var container = Activator.CreateInstance(containerType)!;
            containerType.GetProperty("Customer")!.SetValue(container, _customer);
            return container;
        }

        throw new MissingMemberException(nameof(OrderProcessingOverrideService), typeName);
    }

    private static object? Read(object container, string property) => container.GetType().GetProperty(property)!.GetValue(container);

    [Test]
    public async Task A_line_of_no_packname_that_could_not_be_bought_does_not_refuse_the_packname_order()
    {
        _lineWarnings[PlainLineId] = "Product is not published";

        await CartIsAcceptedAsync();

        _checkedLineIds.Should().Equal(AnnLineId, BobLineId);
    }

    [Test]
    public async Task The_whole_cart_checks_look_at_the_ordered_lines_only()
    {
        await CartIsAcceptedAsync();

        _checkedCartIds.Should().Equal(AnnLineId, BobLineId);
        _checkoutAttributesChecked.Should().BeTrue();
    }

    [Test]
    public async Task A_warning_of_the_whole_cart_checks_refuses_the_order()
    {
        _cartWarnings.Add("Select a gift wrap");

        await PreparingThePackNameOrder.Should().ThrowAsync<NopException>().WithMessage("Select a gift wrap;");
    }

    [Test]
    public async Task The_order_is_made_of_the_packname_lines_and_the_checkout_attributes()
    {
        var details = Container("PlacePackNameOrderContainer");

        await TestObject.InvokeAsync(_service, "PrepareAndValidatePackNameCartAndCheckoutAttributesAsync", details, Request, new Currency());

        ((IList<ShoppingCartItem>)Read(details, "Cart")!).Should().Equal(_packNameCart);
        ((IList<PackNameShoppingCartItem>)Read(details, "PackNamesCart")!).Should().Equal(_packNameCart);
        Read(details, "CheckoutAttributesXml").Should().Be("<attributes/>");
        Read(details, "CheckoutAttributeDescription").Should().Be("Gift wrap: yes");
    }

    [Test]
    public async Task A_packname_line_that_cannot_be_bought_still_refuses_the_order()
    {
        _lineWarnings[BobLineId] = "Product is not published";

        await PreparingThePackNameOrder.Should().ThrowAsync<NopException>().WithMessage("Product is not published;");
    }

    [Test]
    public async Task A_line_of_no_packname_does_not_help_reach_the_minimum_subtotal()
    {
        _orderSettings.MinOrderSubtotalAmount = 3 * LinePrice;

        await PreparingThePackNameOrder.Should().ThrowAsync<NopException>().WithMessage("Minimum subtotal $30");
    }

    [Test]
    public async Task A_line_of_no_packname_does_not_help_reach_the_minimum_total()
    {
        _orderSettings.MinOrderTotalAmount = 3 * LinePrice;

        await PreparingThePackNameOrder.Should().ThrowAsync<NopException>().WithMessage("Minimum total $30");
    }

    [Test]
    public async Task Packname_lines_that_reach_the_minimums_on_their_own_are_accepted()
    {
        _orderSettings.MinOrderSubtotalAmount = 2 * LinePrice;
        _orderSettings.MinOrderTotalAmount = 2 * LinePrice;

        await CartIsAcceptedAsync();
    }

    [Test]
    public async Task A_cart_holding_only_a_line_of_no_packname_is_empty_for_a_packname_order()
    {
        _cart = [Line(PlainLineId)];
        _packNameCart = [];

        await PreparingThePackNameOrder.Should().ThrowAsync<NopException>().WithMessage("Cart is empty");
    }

    [Test]
    public async Task A_plain_order_is_still_checked_on_every_line_of_the_cart()
    {
        _lineWarnings[BobLineId] = "Product is not published";

        var preparing = () => TestObject.InvokeAsync(_service, "PrepareAndValidateShoppingCartAndCheckoutAttributesAsync",
            Container("PlaceOrderContainer"), Request, new Currency());

        await preparing.Should().ThrowAsync<NopException>().WithMessage("Product is not published;");
        _checkedCartIds.Should().Equal(PlainLineId, AnnLineId, BobLineId);
        _checkedLineIds.Should().Equal(PlainLineId, AnnLineId, BobLineId);
    }

    [Test]
    public async Task A_payment_method_placing_its_own_order_is_refused_over_a_line_of_no_packname()
    {
        var result = await TestObject.InvokeAsync<PlaceOrderResult>(_service, "PlaceDelegatedPackNameOrderAsync", Request, _customer);

        result.Errors.Should().Equal(["Plugins.CW.Core.Order.PackNameCartHasOtherItems"], "it charges for the whole cart");
        _checkedLineIds.Should().BeEmpty("the order is refused before it is prepared");
    }

    [Test]
    public async Task A_payment_method_placing_its_own_order_places_a_cart_of_packname_lines()
    {
        _cart = [Line(AnnLineId), Line(BobLineId)];

        var placing = () => TestObject.InvokeAsync<PlaceOrderResult>(_service, "PlaceDelegatedPackNameOrderAsync", Request, _customer);

        //it goes on to prepare the packname order, whose cart is accepted
        await placing.Should().ThrowAsync<NopException>().WithMessage("Billing address is not provided");
        _checkedLineIds.Should().Equal(AnnLineId, BobLineId);
    }
}
