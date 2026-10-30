using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.BusinessLogic;

public interface IReturnSubmissionLogic
{
    Task<bool> RegisterReturnRequestDataAsync(ReturnRequestGatherInfoModel returnRequestInfo, ExchangeReturnMode exchangeMode, List<int> registeredReturnRequestsIds);

    /// <summary>
    /// Finds the customer's open draft case for this order, or creates one, and refreshes it with the
    /// reason, action and comments currently chosen. Best-effort: on failure the wizard continues on
    /// the legacy path and the return is unaffected.
    /// </summary>
    Task<ReturnCase> EnsureDraftReturnCaseAsync(Order order, SubmitReturnRequestModel returnRequestModel, int storeId, int customerId);

    /// <summary>
    /// Checks a wizard submission against the order and the store's rules, whatever the browser sent: each item and
    /// quantity can still be returned, the action is offered for the reason, and, once a mode is chosen, the mode is
    /// offered for them and each exchange's replacement fits its type
    /// </summary>
    /// <param name="order">The customer's order</param>
    /// <param name="returnReasonId">Return reason identifier</param>
    /// <param name="returnActionId">Return action identifier</param>
    /// <param name="submission">The parsed post</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the messages to show; empty when the submission may go on
    /// </returns>
    Task<IList<string>> CheckSubmissionAsync(Order order, int returnReasonId, int returnActionId, ReturnWizardSubmission submission);
}