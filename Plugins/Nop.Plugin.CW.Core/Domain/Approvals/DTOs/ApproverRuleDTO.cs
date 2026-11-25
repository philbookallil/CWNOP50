namespace Nop.Plugin.CW.Core.Domain.Approvals.DTOs;

/// <summary>
/// A rule that names an approver
/// </summary>
public class ApproverRuleDTO
{
    public int ApproverId { get; set; }

    public ApprovalRule Rule { get; set; }
}
