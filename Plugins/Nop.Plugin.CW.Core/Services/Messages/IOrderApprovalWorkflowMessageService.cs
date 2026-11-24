using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Services.Messages;

/// <summary>
/// The e-mails of the order approvals: the requests and reminders to the approvers, and what the ordering customer is
/// told
/// </summary>
public interface IOrderApprovalWorkflowMessageService
{
    /// <summary>
    /// Sends an approver the link to review an order
    /// </summary>
    /// <param name="order">The held order</param>
    /// <param name="approval">The order's approval</param>
    /// <param name="request">The request the link belongs to</param>
    /// <param name="token">The link's token, which only the e-mail carries</param>
    /// <param name="reminder">Whether this is a reminder rather than the first request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the queued e-mail identifiers
    /// </returns>
    Task<IList<int>> SendApproverRequestAsync(Order order, OrderApproval approval, OrderApprovalRequest request, string token, bool reminder);

    /// <summary>
    /// Tells the customer who placed the order where its approval stands
    /// </summary>
    /// <param name="order">The order</param>
    /// <param name="approval">The order's approval</param>
    /// <param name="messageTemplateName">One of the customer templates of the order approvals</param>
    /// <param name="approverNames">The approvers asked, for the awaiting e-mail</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the queued e-mail identifiers
    /// </returns>
    Task<IList<int>> SendCustomerNotificationAsync(Order order, OrderApproval approval, string messageTemplateName, string approverNames);
}
