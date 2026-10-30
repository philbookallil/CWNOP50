using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.PackNameCheckout;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Web.Models.ShoppingCart;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The packname cart's order totals: each packname's shipping and the shipping total are shown in the customer's
/// currency, and a cart whose shipping is not priced yet says it is calculated during checkout. A packname line whose
/// packname is gone still renders.
/// </summary>
[TestFixture]
public class ShoppingCartOverrideModelFactoryTests
{
    private static readonly Currency WorkingCurrency = new() { Id = 2, CurrencyCode = "EUR" };

    private ShoppingCartOverrideModelFactory _factory = null!;
    private Mock<IOrderTotalCalculationOverrideService> _orderTotalCalculationService = null!;
    private Mock<IPackNameService> _packNameService = null!;
    private IList<PackNameShippingPrice> _shippingPrices = null!;

    [SetUp]
    public void SetUp()
    {
        _shippingPrices = new List<PackNameShippingPrice>();

        var shoppingCartService = new Mock<IShoppingCartOverrideService>();
        shoppingCartService.Setup(service => service.ShoppingCartRequiresShippingAsync(It.IsAny<IList<ShoppingCartItem>>())).ReturnsAsync(true);
        _orderTotalCalculationService = new Mock<IOrderTotalCalculationOverrideService>();
        _orderTotalCalculationService.Setup(service => service.GetShoppingCartShippingTotalAsync(It.IsAny<IList<PackNameShoppingCartItem>>()))
            .ReturnsAsync(() => _shippingPrices);
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 5 });
        workContext.Setup(context => context.GetWorkingCurrencyAsync()).ReturnsAsync(WorkingCurrency);
        var storeContext = new Mock<IStoreContext>();
        //the working currency is worth twice the store's
        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(service => service.ConvertFromPrimaryStoreCurrencyAsync(It.IsAny<decimal>(), WorkingCurrency))
            .ReturnsAsync((decimal amount, Currency _) => amount * 2);
        var priceFormatter = new Mock<IPriceFormatter>();
        priceFormatter.Setup(formatter => formatter.FormatShippingPriceAsync(It.IsAny<decimal>(), true))
            .ReturnsAsync((decimal amount, bool _) => $"€{amount:0.00}");
        _packNameService = new Mock<IPackNameService>();
        _packNameService.Setup(service => service.GetPackNameByIdAsync(1)).ReturnsAsync(new PackName { Id = 1, Name = "Ann" });
        _packNameService.Setup(service => service.GetPackNameByIdAsync(2)).ReturnsAsync(new PackName { Id = 2, Name = "Bob" });
        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(It.IsAny<int>()))
            .ReturnsAsync(new MultiStoreSettings { POEntryEnabled = true });
        var exchangeService = new Mock<IExchangeService>();
        exchangeService.Setup(service => service.GetReturnRequestItemsForCartItemsAsync(It.IsAny<int[]>()))
            .ReturnsAsync(new List<ReturnRequestItem>());

        _factory = (ShoppingCartOverrideModelFactory)RuntimeHelpers.GetUninitializedObject(typeof(ShoppingCartOverrideModelFactory));
        SetField("_shoppingCartService", shoppingCartService.Object);
        SetField("_orderTotalCalculationService", _orderTotalCalculationService.Object);
        SetField("_workContext", workContext.Object);
        SetField("_storeContext", storeContext.Object);
        SetField("_currencyService", currencyService.Object);
        SetField("_priceFormatter", priceFormatter.Object);
        SetField("_packNameService", _packNameService.Object);
        SetField("_settingService", settingService.Object);
        SetField("_exchangeService", exchangeService.Object);
        SetField("_shippingSettings", new ShippingSettings());
    }

    /// <summary>
    /// Set a field declared on CW's factory or on nop's factory beneath it; CW's own (a hiding <c>new</c> field) first
    /// </summary>
    private void SetField(string name, object value)
    {
        for (var type = _factory.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null)
                continue;

            field.SetValue(_factory, value);
            return;
        }

        throw new MissingFieldException(name);
    }

    private Task InvokeAsync(string method, params object[] arguments) =>
        (Task)typeof(ShoppingCartOverrideModelFactory).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_factory, arguments)!;

    private async Task<PackNameOrderTotalsModel> ShippingAsync()
    {
        var model = new PackNameOrderTotalsModel();
        await InvokeAsync("PreparePackNameShipping", model, new List<ShoppingCartItem>(), new List<PackNameShoppingCartItem>());

        return model;
    }

    [Test]
    public async Task Each_packnames_shipping_and_the_total_are_in_the_customers_currency()
    {
        _shippingPrices.Add(new PackNameShippingPrice { PackNameIds = [1], Price = 5m, ShippingOption = new ShippingOption { Name = "Ground" } });
        _shippingPrices.Add(new PackNameShippingPrice { PackNameIds = [2], Price = 7.5m, ShippingOption = new ShippingOption { Name = "Air" } });

        var model = await ShippingAsync();

        model.ShippingTotals.Select(total => (total.EntityName, total.Shipping, total.SelectedShippingMethod))
            .Should().Equal(("Ann", "€10.00", "Ground"), ("Bob", "€15.00", "Air"));
        model.Shipping.Should().Be("€25.00", "the total is converted like each packname's line");
    }

    [Test]
    public async Task One_shipment_for_several_packnames_names_them_all()
    {
        _shippingPrices.Add(new PackNameShippingPrice { PackNameIds = [1, 2], Price = 4m });

        var model = await ShippingAsync();

        model.ShippingTotals.Single().EntityName.Should().Be("Ann, Bob");
    }

    [Test]
    public async Task A_cart_whose_shipping_is_not_priced_yet_leaves_the_total_to_checkout()
    {
        var model = await ShippingAsync();

        model.Shipping.Should().BeNullOrEmpty("the totals view then shows \"calculated during checkout\", not a free shipping");
    }

    [Test]
    public async Task One_unpriced_packname_leaves_the_total_to_checkout()
    {
        _shippingPrices.Add(new PackNameShippingPrice { PackNameIds = [1], Price = 5m });
        _shippingPrices.Add(new PackNameShippingPrice { PackNameIds = [2], Price = null });

        var model = await ShippingAsync();

        model.Shipping.Should().BeNullOrEmpty();
        model.ShippingTotals.Should().ContainSingle();
    }

    [Test]
    public async Task A_line_whose_packname_is_gone_still_renders_with_no_PO()
    {
        var model = new PackNameShoppingCartModel();
        model.Items.Add(new PackNameShoppingCartModel.PackNameShoppingCartItemModel { Id = 11 });
        var line = new PackNameShoppingCartItem { Id = 11, PackNameId = 99, Name = "Gone", Colour = "#1F3A93", StoreId = 1 };

        await InvokeAsync("InjectPackNameShoppingCartDataAsync", model, new List<PackNameShoppingCartItem> { line });

        var packName = model.Items.Single().PackNameModel;
        packName.Name.Should().Be("Gone");
        packName.POEntry.Should().BeEmpty();
        packName.ForeColor.Should().Be("#FFF");
    }
}
