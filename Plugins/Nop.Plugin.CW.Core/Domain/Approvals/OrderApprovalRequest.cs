using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// One approval link sent to one approver: the request at placement, a reminder, a resend
/// </summary>
/// <remarks>
/// Only the SHA-256 hash of the link's token is stored, so a copy of the database cannot act on an order. A link
/// works while its order's approval is Pending and the request is not revoked; a resend or a change of approver
/// revokes every earlier link of the order.
/// </remarks>
public class OrderApprovalRequest : BaseEntity
{
    /// <summary>
    /// Gets or sets the order approval identifier
    /// </summary>
    public int OrderApprovalId { get; set; }

    /// <summary>
    /// Gets or sets the approver identifier
    /// </summary>
    public int ApproverId { get; set; }

    /// <summary>
    /// Gets or sets the approver's name when the request was sent
    /// </summary>
    public string ApproverName { get; set; }

    /// <summary>
    /// Gets or sets the approver's e-mail address when the request was sent
    /// </summary>
    public string ApproverEmail { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the link's token, as 64 lowercase hex characters
    /// </summary>
    public string TokenHash { get; set; }

    /// <summary>
    /// Gets or sets when the request was sent (UTC)
    /// </summary>
    public DateTime SentOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when the link stopped working (UTC); null while it works
    /// </summary>
    public DateTime? RevokedOnUtc { get; set; }
}
