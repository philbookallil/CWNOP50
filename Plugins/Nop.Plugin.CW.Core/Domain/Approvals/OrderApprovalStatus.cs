namespace Nop.Plugin.CW.Core.Domain.Approvals;

/// <summary>
/// Where an order's approval stands
/// </summary>
public enum OrderApprovalStatus
{
    /// <summary>
    /// Waiting for an approver: the order is held
    /// </summary>
    Pending = 10,

    /// <summary>
    /// Accepted: the order is released
    /// </summary>
    Approved = 20,

    /// <summary>
    /// Declined: the order was cancelled and its lines went back to the cart
    /// </summary>
    Declined = 30,

    /// <summary>
    /// Not answered in time: the order was cancelled and its lines went back to the cart
    /// </summary>
    Expired = 40,

    /// <summary>
    /// The order was cancelled or deleted another way while it waited
    /// </summary>
    Cancelled = 50
}
