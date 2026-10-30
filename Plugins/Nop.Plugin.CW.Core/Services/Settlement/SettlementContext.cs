using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;

namespace Nop.Plugin.CW.Core.Services.Settlement;

/// <summary>
/// Everything a settlement source needs in order to quote or apply against one cart or order.
/// An input record owned by the settlement layer — never a view model
/// </summary>
public record SettlementContext
{
    /// <summary>The customer whose pools are being consumed.</summary>
    public required Customer Customer { get; init; }

    /// <summary>The store the cart or order belongs to.</summary>
    public required int StoreId { get; init; }

    /// <summary>The packname cart being settled. Empty when the customer has no packname.</summary>
    public IList<PackNameShoppingCartItem> PackNameCart { get; init; } = [];

    /// <summary>
    /// Per-address shipping prices for <see cref="PackNameCart"/>, already computed by the caller.
    /// Passed in rather than resolved here so that no settlement source has to depend on
    /// IOrderTotalCalculationOverrideService, which depends on the pipeline.
    /// </summary>
    public IList<PackNameShippingPrice> ShippingPrices { get; init; } = [];

    /// <summary>
    /// The order being settled. Null on the quote (display) path; required on the apply path.
    /// </summary>
    public Order? Order { get; init; }

    /// <summary>
    /// Maps ShoppingCartItemId to the OrderItem it became. Populated on the apply path only —
    /// SubsidyOrderItem rows are per order item, so the order items must exist before subsidy applies.
    /// </summary>
    public IReadOnlyDictionary<int, int> OrderItemIdByCartItemId { get; init; } = new Dictionary<int, int>();
}