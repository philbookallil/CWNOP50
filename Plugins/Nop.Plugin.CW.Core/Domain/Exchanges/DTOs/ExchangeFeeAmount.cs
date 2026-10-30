namespace Nop.Plugin.CW.Core.Domain.Exchanges.DTOs;

/// <summary>
/// One priced fee: the amount, what it was priced over, where it came from, and the case line it is
/// collected with.
/// </summary>
/// <remarks>
/// This is the shape fees are computed in, and the shape they are stored and read back in. The
/// per-fee-type breakdown the UI shows is a fold of these, never the other way round — a summed
/// dictionary has already thrown away the line, the quantity, the rule and the staff-typed name.
/// </remarks>
public class ExchangeFeeAmount
{
    /// <summary>
    /// Gets or sets the case line this fee was priced over and is collected with. Every fee belongs to
    /// exactly one line, redelivery included — no charge stands in for a sibling, and no line covers
    /// quantities other than its own.
    /// Null only for a return raised before the case model existed.
    /// </summary>
    public int? ReturnCaseLineId { get; set; }

    /// <summary>
    /// Gets or sets the fee type
    /// </summary>
    public FeeType FeeType { get; set; }

    /// <summary>
    /// Gets or sets the amount charged
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the quantity the amount was priced over — always this line's own, redelivery included
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the name shown to the customer: the staff-entered name for an ad-hoc fee, the fee
    /// type's display name otherwise
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the fee rule the amount came from, for traceability. Null for ad-hoc staff fees.
    /// </summary>
    public int? SourceFeeRuleId { get; set; }
}