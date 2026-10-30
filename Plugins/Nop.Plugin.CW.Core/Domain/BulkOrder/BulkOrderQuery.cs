namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// Input record for a bulk ordering product search. Owned by the service layer.
/// </summary>
public record BulkOrderQuery
{
    public int StoreId { get; init; }

    public BulkOrderFilterType FilterType { get; init; } = BulkOrderFilterType.AllProducts;

    /// <summary>
    /// When > 0 and FilterType is LatestOrdered, restricts to the products of that one order
    /// instead of the customer's recent orders.
    /// </summary>
    public int OrderId { get; init; }

    public int CategoryId { get; init; }

    public BulkOrderSorting OrderBy { get; init; } = BulkOrderSorting.Position;

    public int PageIndex { get; init; }

    public int PageSize { get; init; } = 15;
}