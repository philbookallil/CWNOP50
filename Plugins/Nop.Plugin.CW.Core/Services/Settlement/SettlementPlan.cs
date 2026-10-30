namespace Nop.Plugin.CW.Core.Services.Settlement;

/// <summary>
/// The result of walking the settlement pipeline once. The shape is identical whether the pipeline was
/// quoted or applied — that identity is the point of the pipeline.
/// </summary>
public record SettlementPlan
{
    /// <summary>The amount offered to the pipeline, incl tax.</summary>
    public decimal SettleableAmount { get; init; }

    /// <summary>One line per source that covered something, in the order they ran.</summary>
    public IReadOnlyList<SettlementLine> Lines { get; init; } = [];

    /// <summary>Total covered across all sources, incl tax.</summary>
    public decimal TotalSettled => Lines.Sum(x => x.Amount);

    /// <summary>What the customer still owes after settlement, incl tax. Never negative.</summary>
    public decimal BalanceDue => Math.Max(decimal.Zero, SettleableAmount - TotalSettled);

    /// <summary>Amount covered by one named source. Zero when that source did not contribute.</summary>
    public decimal AmountFor(string sourceName) =>
        Lines.Where(x => x.SourceName == sourceName).Sum(x => x.Amount);

    /// <summary>The shipping part of one named source's contribution.</summary>
    public decimal ShippingAmountFor(string sourceName) =>
        Lines.Where(x => x.SourceName == sourceName).Sum(x => x.ShippingAmount);

    /// <summary>What was still unsettled when the named source ran — i.e. its budget.</summary>
    public decimal BudgetFor(string sourceName)
    {
        var line = Lines.FirstOrDefault(x => x.SourceName == sourceName);
        var priority = line?.Priority;

        return priority.HasValue
            ? SettleableAmount - Lines.Where(x => x.Priority < priority.Value).Sum(x => x.Amount)
            : SettleableAmount - TotalSettled;
    }
}