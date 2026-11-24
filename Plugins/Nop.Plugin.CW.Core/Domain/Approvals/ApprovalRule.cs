using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// One rule of the order approvals: the orders its scope covers, over its threshold, wait for its approvers
/// </summary>
public class ApprovalRule : BaseEntity
{
    /// <summary>
    /// Gets or sets the store identifier
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets the scope type identifier
    /// </summary>
    public int ScopeTypeId { get; set; }

    /// <summary>
    /// Gets or sets the scope type
    /// </summary>
    public ApprovalScopeType ScopeType
    {
        get => (ApprovalScopeType)ScopeTypeId;
        set => ScopeTypeId = (int)value;
    }

    /// <summary>
    /// Gets or sets the identifier of the branch or department the rule is attached to
    /// </summary>
    public int ScopeId { get; set; }

    /// <summary>
    /// Gets or sets the order total an order must be over to need approval; null or 0 means every order
    /// </summary>
    public decimal? Threshold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rule applies
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC)
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the last change (UTC)
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }
}
