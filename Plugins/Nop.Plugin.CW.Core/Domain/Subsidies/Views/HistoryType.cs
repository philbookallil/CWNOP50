namespace Nop.Plugin.CW.Core.Domain.Subsidies.Views;

/// <summary>
/// The kind of row the subsidy history procedure emits. The values are the procedure's own
/// <c>HistoryTypeId</c> constants; change one side and change the other.
/// </summary>
public enum HistoryType
{
    Subsidy = 1,
    SubsidyTopUp = 2,
    ProductGroup = 3,
    ProductGroupTopup = 4,
    SubsidyUsed = 5,
    ProductGroupUsed = 6,
    SubsidyVariation = 7,
    SubsidyUsedForShipping = 10,
    /// <summary>
    /// Subsidy given back by an approved return, restored to the bucket it came from.
    /// Amounts are positive — the row records what was restored, not a negative consumption.
    /// </summary>
    Refund = 12
}