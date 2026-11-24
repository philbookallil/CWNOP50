namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// What an approval rule is attached to
/// </summary>
public enum ApprovalScopeType
{
    /// <summary>
    /// A branch: covers the orders its customers place
    /// </summary>
    Branch = 10,

    /// <summary>
    /// A department: covers the orders placed by the customers whose own packname is in it
    /// </summary>
    Department = 30
}
