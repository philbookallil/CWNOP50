using System.Reflection;
using AwesomeAssertions;
using Moq;
using Moq.Protected;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Settlement;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Orders;

/// <summary>
/// The cart's subsidy breakdown the subsidy bar shows: with shipping rated it follows the settlement's subsidy budget;
/// before the packname checkout has assigned shipping, it allocates the goods' subsidy without that budget
/// </summary>
[TestFixture]
public class CartSubsidyBreakdownTests
{
    private Mock<OrderTotalCalculationOverrideService> _service = null!;
    private readonly IList<PackNameShoppingCartItem> _cart = new List<PackNameShoppingCartItem> { new() { Id = 2003, StoreId = 1, PackNameId = 13 } };
    private readonly SubsidyAllocation _allocation = new();

    [SetUp]
    public void SetUp()
    {
        //the service without its other services: the breakdown reads the cart's customer, the settleable amount, the
        //settlement plan and the allocation, which the test stands in for
        var arguments = new object?[typeof(OrderTotalCalculationOverrideService).GetConstructors().Single().GetParameters().Length];
        _service = new Mock<OrderTotalCalculationOverrideService>(MockBehavior.Loose, arguments) { CallBase = true };

        var customerService = new Mock<ICustomerOverrideService>();
        customerService.Setup(service => service.GetShoppingCartCustomerAsync(It.IsAny<IList<ShoppingCartItem>>())).ReturnsAsync(new Customer { Id = 4 });
        typeof(OrderTotalCalculationOverrideService).GetField("_customerService", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_service.Object, customerService.Object);

        _service.Setup(service => service.AllocateCartSubsidyAsync(It.IsAny<IList<PackNameShoppingCartItem>>(), It.IsAny<Customer>(), It.IsAny<int>(), It.IsAny<decimal?>()))
            .ReturnsAsync(_allocation);
    }

    private void SettleableAmount(decimal? amount) =>
        _service.Protected().Setup<Task<decimal?>>("GetSettleableAmountAsync", ItExpr.IsAny<IList<ShoppingCartItem>>()).ReturnsAsync(amount);

    [Test]
    public async Task Before_shipping_can_be_rated_the_goods_subsidy_is_allocated_without_a_budget()
    {
        SettleableAmount(null);

        var breakdown = await _service.Object.GetCartSubsidyBreakdownAsync(_cart);

        breakdown.Should().BeSameAs(_allocation);
        _service.Verify(service => service.AllocateCartSubsidyAsync(_cart, It.IsAny<Customer>(), 1, null), Times.Once);
        _service.Verify(service => service.QuoteCartSettlementAsync(It.IsAny<IList<ShoppingCartItem>>(), It.IsAny<decimal?>()), Times.Never);
    }

    [Test]
    public async Task With_shipping_rated_the_settlements_subsidy_budget_applies()
    {
        SettleableAmount(97.50m);
        _service.Setup(service => service.QuoteCartSettlementAsync(It.IsAny<IList<ShoppingCartItem>>(), 97.50m))
            .ReturnsAsync(new SettlementPlan { SettleableAmount = 97.50m });

        await _service.Object.GetCartSubsidyBreakdownAsync(_cart);

        _service.Verify(service => service.AllocateCartSubsidyAsync(_cart, It.IsAny<Customer>(), 1, 97.50m), Times.Once);
    }
}
