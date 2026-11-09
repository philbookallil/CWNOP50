using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Services.Messages;

public interface IExchangeWorkflowMessageService
{
    /// <summary>
    /// Sends the custom Return Request Receipt message template
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="languageId">The language identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestReceivedNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, int languageId);

    /// <summary>
    /// Sends a notification when a Return is approved and no extra money is needed
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="paidOrder">The order placed for the replacement</param>
    /// <param name="orderItem">The returned order item</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestApprovedExceptedCustomerNotificationAsync(ReturnRequest returnRequest, Order paidOrder, OrderItem orderItem);

    /// <summary>
    /// Sends a notification when a Return Request is approved but extra money is needed
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="pendingAmount">What the customer is asked to pay</param>
    /// <param name="returnRequestItem">
    /// The return request item as the caller holds it. Passing it keeps the email's itemised fees on the
    /// same instance the workflow priced against, rather than whatever a second read returns.
    /// </param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestApprovedPaymentCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, decimal pendingAmount, ReturnRequestItem returnRequestItem = null);
}