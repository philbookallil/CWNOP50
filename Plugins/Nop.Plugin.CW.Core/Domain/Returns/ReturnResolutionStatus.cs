namespace Nop.Plugin.CW.Core.Domain.Returns;

/// <summary>
/// Tracks whether the financial outcome of a resolved return has been settled.
/// </summary>
public enum ReturnResolutionStatus
{
    /// <summary>
    /// Resolved, but a balance is still owed by the customer.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Fully settled — nothing further is owed.
    /// </summary>
    Settled = 2,

    /// <summary>
    /// Reversed by an administrator undoing the approval.
    /// </summary>
    Cancelled = 3
}