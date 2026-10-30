using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Services.Messages;

public interface IExchangeWorkflowMessageService
{
    /// <summary>
    /// Sends the custom Return Request Receipt message template
    /// </summary>
    /// <param name="returnRequest"></param>
    /// <param name="orderItem"></param>
    /// <param name="languageId"></param>
    /// <returns></returns>
    Task<IList<int>> SendReturnRequestReceivedNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, int languageId);

    /// <summary>
    /// Sends a notification when a Return is approved and no extra money is needed
    /// </summary>
    /// <param name="returnRequest"></param>
    /// <param name="orderItem"></param>
    /// <param name="languageId"></param>
    /// <returns></returns>
    Task<IList<int>> SendReturnRequestApprovedExceptedCustomerNotificationAsync(ReturnRequest returnRequest, Order paidOrder, OrderItem orderItem);

    /// <summary>
    /// Sends a notification when a Return Request is approved but extra money is needed
    /// </summary>
    /// <param name="returnRequestItem">
    /// The return request item as the caller holds it. Passing it keeps the email's itemised fees on the
    /// same instance the workflow priced against, rather than whatever a second read returns.
    /// </param>
    Task<IList<int>> SendReturnRequestApprovedPaymentCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, decimal pendingAmount, ReturnRequestItem returnRequestItem = null);

    Task<int> SendPendingReturnRequestNotificationAsync(ReturnRequest returnRequest);
}
