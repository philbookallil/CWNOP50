namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// Sort options offered on the bulk ordering page. Deliberately separate from nop's
/// ProductSortingEnum, which has no SKU option.
/// </summary>
public enum BulkOrderSorting
{
    /// <summary>Display order, then id. On Best Sellers this preserves best-seller order.</summary>
    Position = 0,
    NameAsc = 5,
    NameDesc = 6,
    SkuAsc = 7,
    SkuDesc = 8,
    PriceAsc = 10,
    PriceDesc = 11,
    CreatedOn = 15
}