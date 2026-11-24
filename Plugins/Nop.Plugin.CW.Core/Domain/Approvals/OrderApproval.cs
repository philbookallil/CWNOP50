using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// The approval of one held order: one row per order, written when the order is placed and needs approval
/// </summary>
/// <remarks>
/// While <see cref="Status"/> is Pending the order is held: it stays nop-Pending, it cannot be shipped or moved on
/// in admin, and its subsidy and store credit stay spent (they come back when the order is cancelled).
/// </remarks>
public class OrderApproval : BaseEntity
{
    /// <summary>
    /// Gets or sets the order identifier
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Gets or sets the status identifier
    /// </summary>
    public int StatusId { get; set; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public OrderApprovalStatus Status
    {
        get => (OrderApprovalStatus)StatusId;
        set => StatusId = (int)value;
    }

    /// <summary>
    /// Gets or sets the amount the thresholds were compared with at placement: the order total
    /// </summary>
    public decimal MeasuredAmount { get; set; }

    /// <summary>
    /// Gets or sets when the current requests were sent (UTC): at placement, and again on a resend or a change of
    /// approver. Reminders and expiry count from it.
    /// </summary>
    public DateTime RequestedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of the last reminder sent since <see cref="RequestedOnUtc"/> (0 to 2)
    /// </summary>
    public int LastReminderNumber { get; set; }

    /// <summary>
    /// Gets or sets who decided: the approver's name and e-mail, the admin's e-mail, or the system for an expiry
    /// </summary>
    public string DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets the approver who decided, when an approver did
    /// </summary>
    public int? DecidedByApproverId { get; set; }

    /// <summary>
    /// Gets or sets the admin who decided on the approver's behalf, when one did
    /// </summary>
    public int? DecidedByCustomerId { get; set; }

    /// <summary>
    /// Gets or sets when the decision was made (UTC)
    /// </summary>
    public DateTime? DecidedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason of a decline or an expiry
    /// </summary>
    public string DecisionComment { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC)
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }
}
