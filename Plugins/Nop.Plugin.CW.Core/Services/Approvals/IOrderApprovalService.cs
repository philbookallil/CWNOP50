using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The approvals of held orders: holding an order, the approvers' and admins' decisions, the reminders and the expiry
/// </summary>
public interface IOrderApprovalService
{
    #region Queries

    /// <summary>
    /// Gets the approval of an order
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the approval; null when the order never needed one
    /// </returns>
    Task<OrderApproval> GetOrderApprovalByOrderIdAsync(int orderId);

    /// <summary>
    /// Gets a value indicating whether an order is held for approval
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true while the order's approval is pending
    /// </returns>
    Task<bool> IsOrderHeldAsync(int orderId);

    /// <summary>
    /// Gets the links sent for an approval, newest first
    /// </summary>
    /// <param name="orderApprovalId">Order approval identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the requests
    /// </returns>
    Task<IList<OrderApprovalRequest>> GetRequestsAsync(int orderApprovalId);

    /// <summary>
    /// Gets the approval and the request an approval link belongs to
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the approval and the request; both null for an unknown token
    /// </returns>
    Task<(OrderApproval approval, OrderApprovalRequest request)> GetByTokenAsync(string token);

    #endregion

    #region Placement

    /// <summary>
    /// Holds an order that needs approval: records its approval, which is what keeps it Pending. The e-mails are
    /// <see cref="NotifyHoldAsync"/>'s.
    /// </summary>
    /// <param name="order">The order just placed</param>
    /// <param name="requirement">What the rules asked</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true when the order is now held; false when it needs no approval or already has one
    /// </returns>
    Task<bool> HoldOrderAsync(Order order, OrderApprovalRequirement requirement);

    /// <summary>
    /// Sends a held order's first e-mails: a link to each approver, and the customer that the order waits
    /// </summary>
    /// <param name="order">The held order, settled</param>
    /// <param name="requirement">What the rules asked</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task NotifyHoldAsync(Order order, OrderApprovalRequirement requirement);

    /// <summary>
    /// Ends a pending approval whose order was cancelled or deleted another way, and revokes its links
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task MarkCancelledAsync(int orderId);

    #endregion

    #region Decisions

    /// <summary>
    /// Accepts an order from an approver's link: the order is released and the customer told
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> AcceptByTokenAsync(string token);

    /// <summary>
    /// Declines an order from an approver's link: the order is cancelled, its lines go back to the customer's cart
    /// and the customer is told why
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <param name="reason">Why; required</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> DeclineByTokenAsync(string token, string reason);

    /// <summary>
    /// Accepts an order on the approvers' behalf
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="admin">The admin deciding</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> AcceptByAdminAsync(int orderId, Customer admin);

    /// <summary>
    /// Declines an order on the approvers' behalf
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="admin">The admin deciding</param>
    /// <param name="reason">Why; required</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> DeclineByAdminAsync(int orderId, Customer admin, string reason);

    /// <summary>
    /// Sends the order's approvers new links and revokes the old ones; reminders and expiry count again from now. An
    /// order whose first links were never sent asks the approvers its rules name now.
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="admin">The admin asking</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> ResendAsync(int orderId, Customer admin);

    /// <summary>
    /// Replaces the order's approvers with one approver, found by e-mail address in the order's store or created,
    /// and sends them a link; the old links are revoked and reminders and expiry count again from now
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="admin">The admin asking</param>
    /// <param name="name">The approver's name</param>
    /// <param name="email">The approver's e-mail address</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what happened
    /// </returns>
    Task<OrderApprovalResult> ChangeApproverAsync(int orderId, Customer admin, string name, string email);

    #endregion

    #region Task

    /// <summary>
    /// Sends the reminders due and expires the approvals not answered in time, in every store; an approval whose order
    /// was deleted or cancelled meanwhile is ended
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ProcessPendingApprovalsAsync();

    #endregion
}
