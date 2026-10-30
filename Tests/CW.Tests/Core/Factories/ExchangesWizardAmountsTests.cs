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
/// The wizard's items step in the customer's language and currency: the returned item's price, the fees that may apply
/// and the exchange types
/// </summary>
[TestFixture]
public class ExchangesWizardAmountsTests
{
    private static readonly Order Order = new() { Id = 10, CustomerId = 5 };

    private ExchangesModelFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        var exchangeService = new Mock<IExchangeService>();
        exchangeService.SetReturnsDefault<Task<IList<ExchangeReturnMode>>>(Task.FromResult<IList<ExchangeReturnMode>>(new List<ExchangeReturnMode> { ExchangeReturnMode.Exchange }));
        exchangeService.Setup(service => service.GetFeeRulesForActionAndReasonAsync(It.IsAny<int>(), It.IsAny<int>(), 1)).ReturnsAsync(new List<FeeRule>
        {
            new() { FeeType = FeeType.ReShippingFee, Operator = FeeRuleOperator.FixedPrice, Amount = 5m },
            new() { FeeType = FeeType.RepackagingFee, Operator = FeeRuleOperator.PerUnitsQuantity, UnitsQuantity = 2, Amount = 3m },
            new() { FeeType = FeeType.RepackagingFee, Operator = FeeRuleOperator.PerUnitsQuantity, UnitsQuantity = 1, Amount = 1m }
        });

        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderItemsAsync(Order.Id, false, null, 0))
            .ReturnsAsync(new List<OrderItem> { new() { Id = 100, OrderId = Order.Id, ProductId = 50, Quantity = 2, UnitPriceInclTax = 20m } });

        var returnRequestModelFactory = new Mock<IReturnRequestOverrideModelFactory>();
        returnRequestModelFactory.Setup(factory => factory.PrepareSubmitReturnRequestOrderItemModelAsync(It.IsAny<Order>(), It.IsAny<OrderItem>(), It.IsAny<int>()))
            .ReturnsAsync((Order _, OrderItem item, int quantity) => new SubmitReturnRequestModel.OrderItemModel { Id = item.Id, Quantity = quantity });

        //amounts at twice the store's currency, names and texts in French
        var displayService = new Mock<IExchangesDisplayService>();
        displayService.Setup(service => service.FormatPriceAsync(It.IsAny<decimal>())).ReturnsAsync((decimal amount) => $"€{amount * 2:0.00}");
        displayService.Setup(service => service.GetFeeTypeNameAsync(FeeType.ReShippingFee)).ReturnsAsync("Réexpédition");
        displayService.Setup(service => service.GetFeeTypeNameAsync(FeeType.RepackagingFee)).ReturnsAsync("Reconditionnement");
        displayService.Setup(service => service.GetExchangeTypeNameAsync(It.IsAny<ExchangeType>())).ReturnsAsync((ExchangeType type) => $"type {(int)type}");

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Fees.PerItem")).ReturnsAsync("{0} par article");
        localizationService.Setup(service => service.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Fees.PerItems")).ReturnsAsync("{0} par {1} articles");
        localizationService.Setup(service => service.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.SelectPlaceholder")).ReturnsAsync("- Choisir -");

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 1 });
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 5 });

        _factory = TestObject.Create<ExchangesModelFactory>();
        TestObject.Set(_factory, "_exchangeService", exchangeService.Object);
        TestObject.Set(_factory, "_orderService", orderService.Object);
        TestObject.Set(_factory, "_returnRequestModelFactory", returnRequestModelFactory.Object);
        TestObject.Set(_factory, "_exchangesDisplayService", displayService.Object);
        TestObject.Set(_factory, "_localizationService", localizationService.Object);
        TestObject.Set(_factory, "_storeContext", storeContext.Object);
        TestObject.Set(_factory, "_workContext", workContext.Object);
        TestObject.Set(_factory, "_returnRequestService", new Mock<IReturnRequestService>().Object);
        TestObject.Set(_factory, "_productService", new Mock<IProductService>().Object);
        TestObject.Set(_factory, "_exchangesLogic", new Mock<IExchangesLogic>().Object);
    }

    private Task<ReturnRequestGatherInfoModel> StepAsync() =>
        _factory.PrepareGatherInfoModelAsync(Order, new SubmitReturnRequestModel(),
            new ReturnWizardSubmission { Lines = [new ReturnWizardLine { OrderItemId = 100, Quantity = 1 }] }, Guid.NewGuid(), 0);

    [Test]
    public async Task The_fees_that_may_apply_are_in_the_customers_language_and_currency()
    {
        var step = await StepAsync();

        step.EstimatedFees.Select(fee => (fee.Concept, fee.AmountDisplay)).Should().Equal(
            ("Réexpédition", "€10.00"),
            ("Reconditionnement", "€6.00 par 2 articles"),
            ("Reconditionnement", "€2.00 par article"));
    }

    [Test]
    public async Task The_returned_items_price_is_in_the_customers_currency()
    {
        var step = await StepAsync();

        step.ReturnedItems.Single().UnitPriceDisplay.Should().Be("€40.00");
    }

    [Test]
    public async Task The_exchange_types_are_in_the_customers_language()
    {
        var step = await StepAsync();

        step.ReturnedItems.Single().AvailableExchangeTypes.Select(item => item.Text)
            .Should().Equal("- Choisir -", "type 1", "type 2", "type 3");
    }
}
