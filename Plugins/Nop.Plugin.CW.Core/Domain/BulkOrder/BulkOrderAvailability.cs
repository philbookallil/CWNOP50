namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// Every stock figure one product's grid needs, resolved in one go. One object rather than a
/// per-cell call because the point of the seam is that a stock integration answers for the whole product at
/// once — asking a stock system per combination was the dominant cost of the previous implementation.
/// </summary>
/// <param name="ProductStockQuantity">
/// GetTotalStockQuantityAsync, used when ManageInventoryMethod is ManageStock. Zero otherwise; nothing reads
/// it in the other two methods.
/// </param>
/// <param name="ByCombinationId">
/// Per-combination stock, used when ManageInventoryMethod is ManageStockByAttributes. A combination missing
/// from this dictionary is one the provider had nothing to say about.
/// </param>
public record BulkOrderAvailability(
    int ProductStockQuantity,
    IReadOnlyDictionary<int, BulkOrderCombinationStock> ByCombinationId)
{
    public static BulkOrderAvailability Empty { get; } =
        new(0, new Dictionary<int, BulkOrderCombinationStock>());
}

/// <param name="Quantity">Units available for this combination.</param>
/// <param name="AllowOutOfStockOrders">When true the cell is orderable whatever Quantity says.</param>
public readonly record struct BulkOrderCombinationStock(int Quantity, bool AllowOutOfStockOrders);


/// <summary>
/// What a bundle value's ASSOCIATED product allows. nop validates that product's own stock for the line
/// (ShoppingCartService.cs:977-1000, quantity x value.Quantity), so a cell carrying the value is capped by it.
/// </summary>
/// <param name="Units">The most parent units the cell can take; 0 means the cell is blocked.</param>
/// <param name="Message">The associated product's own stock wording when Units is 0; empty otherwise.</param>
public readonly record struct BulkOrderBundleLimit(int Units, string Message);