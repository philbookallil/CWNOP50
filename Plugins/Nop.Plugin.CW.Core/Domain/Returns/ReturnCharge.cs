using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Domain.Returns;

/// <summary>
/// A fee raised against a resolved return, priced once at resolution. Later changes to a store's fee
/// settings do not affect returns already resolved.
/// </summary>
public class ReturnCharge : BaseEntity
{
    /// <summary>
    /// Gets or sets the resolution this charge belongs to
    /// </summary>
    public int ReturnResolutionId { get; set; }

    /// <summary>
    /// Gets or sets the case line this charge was priced over and is collected with. Always set for a
    /// charge raised against a case, and it means exactly what it says: every fee, redelivery included,
    /// is priced over one line's own quantity, so a charge never stands in for a sibling.
    /// <see cref="Quantity"/> records the quantity it was priced over, which is the staff-typed one
    /// wherever the administrator overrode it.
    /// </summary>
    public int? ReturnCaseLineId { get; set; }

    /// <summary>
    /// Gets or sets the fee type identifier
    /// </summary>
    public int FeeTypeId { get; set; }

    /// <summary>
    /// Gets or sets the fee type
    /// </summary>
    public FeeType FeeType
    {
        get => (FeeType)FeeTypeId;
        set => FeeTypeId = (int)value;
    }

    /// <summary>
    /// Gets or sets the charged amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the quantity the amount was priced over — always this line's own, redelivery
    /// included. Staff overrides
    /// (<see cref="ReturnRequestItem.StaffShipQty"/> /
    /// <c>StaffRepackQty</c>) are already applied, so this is the number the administrator typed wherever
    /// they overrode it.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the fee rule the amount came from, for traceability. Null for ad-hoc staff fees.
    /// </summary>
    public int? SourceFeeRuleId { get; set; }

    /// <summary>
    /// Gets or sets the description, used for ad-hoc staff fees and shown on the order
    /// </summary>
    public string Description { get; set; }
}