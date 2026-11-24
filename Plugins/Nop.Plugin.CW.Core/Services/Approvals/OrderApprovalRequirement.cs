namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Whether an order needs approval, and from whom
/// </summary>
public record OrderApprovalRequirement
{
    /// <summary>
    /// An order that needs no approval
    /// </summary>
    public static OrderApprovalRequirement NotRequired { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the order must wait for an approver
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Gets the amount the thresholds were compared with: the order total, what is left to pay after subsidy, store
    /// credit, discounts and gift cards
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Gets the approvers to ask, each once
    /// </summary>
    public IReadOnlyList<int> ApproverIds { get; init; } = [];

    /// <summary>
    /// Gets the rules that asked for approval
    /// </summary>
    public IReadOnlyList<int> RuleIds { get; init; } = [];
}
