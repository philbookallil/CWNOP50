namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// Represents the product filter applied on the bulk ordering page
/// </summary>
public enum BulkOrderFilterType
{
    /// <summary>Every product visible to the customer</summary>
    AllProducts = 0,

    /// <summary>
    /// Products the customer ordered recently, or the products of one specific past order
    /// when an order is selected
    /// </summary>
    LatestOrdered = 1,

    /// <summary>Best selling products in the store, most sold first</summary>
    BestSellers = 2
}

/// <summary>
/// Extensions for <see cref="BulkOrderFilterType"/>
/// </summary>
public static class BulkOrderFilterTypeExtensions
{
    /// <summary>
    /// Parses a filter mode as it arrived on the request. An unrecognised or missing value falls back to
    /// AllProducts rather than producing an empty list - see spec trap 3.
    /// </summary>
    public static BulkOrderFilterType ToBulkOrderFilterType(this string value)
        => Enum.TryParse<BulkOrderFilterType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : BulkOrderFilterType.AllProducts;
}