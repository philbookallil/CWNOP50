using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Orders;

/// <summary>
/// An exchange replacement is not a sale: a cart or destination holding only replacements has no delivery charge of its
/// own, and a replacement is not held to the products it requires
/// </summary>
[TestFixture]
public class ReplacementOnlyCartTests
{
    private const int CustomerId = 5;

    private OrderTotalCalculationOverrideService _totals = null!;

    [SetUp]
    public void SetUp()
    {
        var exchangeService = new Mock<IExchangeService>();
        exchangeService.Setup(service => service.GetCartReturnRequestItemsAsync(CustomerId))
            .ReturnsAsync(new Dictionary<int, ReturnRequestItem> { [1] = new() { Id = 7 }, [2] = new() { Id = 8 } });

        _totals = TestObject.Create<OrderTotalCalculationOverrideService>();
        TestObject.Set(_totals, "_exchangeService", exchangeService.Object);
    }

    private static ShoppingCartItem Line(int id) => new() { Id = id, CustomerId = CustomerId, ProductId = 50, Quantity = 1 };

    private Task<bool> IsReplacementOnlyAsync(params ShoppingCartItem[] cart) =>
        TestObject.InvokeAsync<bool>(_totals, "IsReplacementOnlyCartAsync", cart.ToList());

    [Test]
    public async Task A_cart_of_replacements_only_is_replacement_only()
    {
        (await IsReplacementOnlyAsync(Line(1), Line(2))).Should().BeTrue();
    }

    [Test]
    public async Task A_replacement_beside_other_goods_is_not()
    {
        (await IsReplacementOnlyAsync(Line(1), Line(3))).Should().BeFalse("the other goods ship as they always do");
    }

    [Test]
    public async Task A_line_not_saved_yet_is_never_counted_as_a_replacement()
    {
        (await IsReplacementOnlyAsync(Line(1), Line(0))).Should().BeFalse();
    }

    [Test]
    public async Task An_empty_cart_is_not()
    {
        (await IsReplacementOnlyAsync()).Should().BeFalse();
    }

    [Test]
    public async Task A_replacement_being_added_is_not_held_to_its_required_products()
    {
        var cart = TestObject.Create<ShoppingCartOverrideService>();
        var product = new Product { Id = 50, RequireOtherProducts = true, RequiredProductIds = "51", AutomaticallyAddRequiredProducts = true };

        using var scope = ExchangeReplacementScope.Begin(new ReturnRequestItem { Id = 7, ReturnedOrderItemId = 100 });

        var warnings = await TestObject.InvokeAsync<IList<string>>(cart, "GetRequiredProductWarningsAsync",
            new Customer { Id = CustomerId }, ShoppingCartType.ShoppingCart, product, 1, 1, true, 0);

        warnings.Should().BeEmpty();
    }
}
