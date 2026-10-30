namespace Nop.Plugin.CW.Core.Services.Settlement;

/// <summary>
/// Walks every enabled <see cref="ISettlementSource"/> in Priority order, handing each the amount that
/// is still unsettled. One implementation serves both display (Quote) and placement (Apply).
/// </summary>
public interface ISettlementPipeline
{
    /// <summary>Walks the sources without persisting anything.</summary>
    Task<SettlementPlan> QuoteAsync(SettlementContext context, decimal settleableAmount);

    /// <summary>Walks the sources, persisting each consumption as it goes.</summary>
    Task<SettlementPlan> ApplyAsync(SettlementContext context, decimal settleableAmount);
}