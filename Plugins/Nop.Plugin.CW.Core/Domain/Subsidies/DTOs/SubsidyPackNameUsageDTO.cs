namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// What one assignment has in the current period and how much of it is spent, money and units together.
/// Assignments outside their ordering period have no row at all — the credit views only carry the
/// current period — so a reader that joins on this treats "no row" as "nothing to report".
/// </summary>
public class SubsidyPackNameUsageDTO
{
    public int SubsidyPackNameId { get; set; }

    public int SubsidyId { get; set; }

    /// <summary>Allocation plus topups.</summary>
    public decimal TotalCredit { get; set; }

    /// <summary>Consumed from the allocation and the topups.</summary>
    public decimal UsedCredit { get; set; }

    /// <summary>Product-group units allocated plus topped up, summed over the subsidy's groups.</summary>
    public int TotalQuantity { get; set; }

    /// <summary>Product-group units consumed, summed over the subsidy's groups.</summary>
    public int UsedQuantity { get; set; }
}