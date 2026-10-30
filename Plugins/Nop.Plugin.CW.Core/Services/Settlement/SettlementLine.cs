namespace Nop.Plugin.CW.Core.Services.Settlement;

/// <summary>
/// One source's contribution to a settlement. Amounts are incl tax, in the primary store currency.
/// </summary>
public record SettlementLine
{
    /// <summary>The contributing source, from <see cref="SettlementSourceNames"/>.</summary>
    public required string SourceName { get; init; }

    /// <summary>The source's priority, copied so a plan can be read without the sources.</summary>
    public required int Priority { get; init; }

    /// <summary>Total amount this source covered.</summary>
    public decimal Amount { get; init; }

    /// <summary>The part of <see cref="Amount"/> that paid for shipping rather than goods.</summary>
    public decimal ShippingAmount { get; init; }
}