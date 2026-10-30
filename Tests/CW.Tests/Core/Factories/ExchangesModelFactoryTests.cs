using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Web.Models.Order;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The wizard's model factory: the exchange fees of the store's shopping cart only, and a step that can be shown again
/// when a chosen replacement has since gone
/// </summary>
[TestFixture]
public class ExchangesModelFactoryTests
{
    private static readonly Store Store = new() { Id = 1 };
    private static readonly Customer Customer = new() { Id = 5 };
    private static readonly Order Order = new() { Id = 10, CustomerId = 5 };

    private ExchangesModelFactory _factory = null!;
    private Mock<IShoppingCartService> _shoppingCartService = null!;

    [SetUp]
    public void SetUp()
    {
        _shoppingCartService = new Mock<IShoppingCartService>();
        _shoppingCartService.SetReturnsDefault<Task<IList<ShoppingCartItem>>>(Task.FromResult<IList<ShoppingCartItem>>(new List<ShoppingCartItem>()));

        var exchangeService = new Mock<IExchangeService>();
        exchangeService.SetReturnsDefault<Task<IList<ExchangeReturnMode>>>(Task.FromResult<IList<ExchangeReturnMode>>(new List<ExchangeReturnMode> { ExchangeReturnMode.Exchange }));
        exchangeService.SetReturnsDefault<Task<IList<FeeRule>>>(Task.FromResult<IList<FeeRule>>(new List<FeeRule>()));

        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderItemsAsync(Order.Id, false, null, 0))
            .ReturnsAsync(new List<OrderItem> { new() { Id = 100, OrderId = Order.Id, ProductId = 50, Quantity = 2 } });

        var returnRequestModelFactory = new Mock<IReturnRequestOverrideModelFactory>();
        returnRequestModelFactory.Setup(factory => factory.PrepareSubmitReturnRequestOrderItemModelAsync(It.IsAny<Order>(), It.IsAny<OrderItem>(), It.IsAny<int>()))
            .ReturnsAsync((Order _, OrderItem item, int quantity) => new SubmitReturnRequestModel.OrderItemModel { Id = item.Id, ProductId = item.ProductId, Quantity = quantity });

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(Store);
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(Customer);

        //the replacement chosen before (product 60) has since been deleted
        var productService = new Mock<IProductService>();

        _factory = TestObject.Create<ExchangesModelFactory>();
        TestObject.Set(_factory, "_shoppingCartService", _shoppingCartService.Object);
        TestObject.Set(_factory, "_exchangeService", exchangeService.Object);
        TestObject.Set(_factory, "_orderService", orderService.Object);
        TestObject.Set(_factory, "_returnRequestModelFactory", returnRequestModelFactory.Object);
        TestObject.Set(_factory, "_storeContext", storeContext.Object);
        TestObject.Set(_factory, "_workContext", workContext.Object);
        TestObject.Set(_factory, "_productService", productService.Object);
        TestObject.Set(_factory, "_returnRequestService", new Mock<IReturnRequestService>().Object);
        TestObject.Set(_factory, "_localizationService", new Mock<ILocalizationService>().Object);
        TestObject.Set(_factory, "_priceFormatter", new Mock<IPriceFormatter>().Object);
        TestObject.Set(_factory, "_exchangesLogic", new Mock<IExchangesLogic>().Object);
        TestObject.Set(_factory, "_exchangesDisplayService", new Mock<IExchangesDisplayService>().Object);
    }

    [Test]
    public async Task The_cart_fees_come_from_the_stores_shopping_cart_only()
    {
        await _factory.PrepareExchangesFeesModelFromCartAsync(Customer, Store);

        _shoppingCartService.Verify(service => service.GetShoppingCartAsync(Customer, ShoppingCartType.ShoppingCart, Store.Id, null, null, null, null), Times.Once,
            "a wishlist or another store's cart has no exchange fee to show");
        _shoppingCartService.Verify(service => service.GetShoppingCartAsync(Customer, It.IsAny<List<int>>(), It.IsAny<int>(), null, null, null, null), Times.Never);
    }

    [Test]
    public async Task The_items_step_shows_again_when_a_chosen_replacement_has_gone()
    {
        var submission = new ReturnWizardSubmission
        {
            Mode = ExchangeReturnMode.Exchange,
            Lines = [new ReturnWizardLine { OrderItemId = 100, Quantity = 1, ExchangeType = ExchangeType.DifferentStyle, ReplacementProductId = 60 }]
        };

        var model = await _factory.PrepareGatherInfoModelAsync(Order, new SubmitReturnRequestModel(), submission, Guid.NewGuid(), 0);

        model.ReturnedItems.Should().ContainSingle().Which.ReplacementProduct.Should().BeNull("the checks then ask for the replacement again");
    }

    [Test]
    public async Task A_credit_note_carries_no_replacement()
    {
        var submission = new ReturnWizardSubmission
        {
            Mode = ExchangeReturnMode.CreditNote,
            Lines = [new ReturnWizardLine { OrderItemId = 100, Quantity = 1, ExchangeType = ExchangeType.SameStyle, ReplacementProductId = 60 }]
        };

        var model = await _factory.PrepareGatherInfoModelAsync(Order, new SubmitReturnRequestModel(), submission, Guid.NewGuid(), 0);

        var item = model.ReturnedItems.Should().ContainSingle().Subject;
        item.ReplacementProduct.Should().BeNull();
        item.ExchangeTypeId.Should().Be((int)ExchangeType.Exact);
    }
}
