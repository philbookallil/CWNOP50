namespace Nop.Plugin.CW.Core.Services.Settlement;

/// <summary>
/// The ordered settlement pipeline. Sources arrive as IEnumerable<ISettlementSource> and Priority
/// decides the order, so this class stays indifferent to how many there are or what they do.
/// The set itself is composed explicitly in NopStartup rather than gathered by the container —
/// see the comment there for why. Adding a third source is a registration, a Priority value, and
/// a line in that composition.
/// </summary>
public class SettlementPipeline : ISettlementPipeline
{
    #region Fields

    protected readonly IEnumerable<ISettlementSource> _sources;

    #endregion

    #region Ctor

    public SettlementPipeline(IEnumerable<ISettlementSource> sources)
    {
        _sources = sources;
    }

    #endregion

    #region Utilities

    protected virtual async Task<SettlementPlan> WalkAsync(SettlementContext context, decimal settleableAmount, bool persist)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lines = new List<SettlementLine>();

        if (settleableAmount <= decimal.Zero)
            return new SettlementPlan { SettleableAmount = decimal.Zero, Lines = lines };

        var remaining = settleableAmount;

        foreach (var source in _sources.OrderBy(x => x.Priority))
        {
            if (remaining <= decimal.Zero)
                break;

            if (!await source.IsEnabledAsync(context))
                continue;

            var line = persist
                ? await source.ApplyAsync(context, remaining)
                : await source.QuoteAsync(context, remaining);

            if (line is null || line.Amount <= decimal.Zero)
                continue;

            lines.Add(line);
            remaining -= line.Amount;
        }

        return new SettlementPlan { SettleableAmount = settleableAmount, Lines = lines };
    }

    #endregion

    #region Methods

    public virtual Task<SettlementPlan> QuoteAsync(SettlementContext context, decimal settleableAmount)
        => WalkAsync(context, settleableAmount, persist: false);

    public virtual Task<SettlementPlan> ApplyAsync(SettlementContext context, decimal settleableAmount)
        => WalkAsync(context, settleableAmount, persist: true);

    #endregion
}