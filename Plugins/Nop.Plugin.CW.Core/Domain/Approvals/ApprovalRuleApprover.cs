using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// An approver of an approval rule
/// </summary>
public class ApprovalRuleApprover : BaseEntity
{
    /// <summary>
    /// Gets or sets the approval rule identifier
    /// </summary>
    public int ApprovalRuleId { get; set; }

    /// <summary>
    /// Gets or sets the approver identifier
    /// </summary>
    public int ApproverId { get; set; }
}
