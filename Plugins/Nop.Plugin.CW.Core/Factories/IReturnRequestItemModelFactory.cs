using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Factories;

public interface IReturnRequestItemModelFactory
{
    /// <summary>
    /// Prepares the exchange detail for one return request.
    /// </summary>
    /// <param name="includeReceiptMessage">
    /// Only the confirmation panel displays the receipt message, and building it is expensive
    /// (message template + tokens). Leave false for list views.
    /// </param>
    Task<CustomerExchangeRequestModel> PrepareCustomerExchangeRequestModelAsync(CustomerReturnRequestsModel.ReturnRequestModel returnRequestModel, ReturnRequest returnRequest = null, OrderItem orderItem = null, bool includeReceiptMessage = false);

    Task<ReturnExchangesConfirmationModel> PrepareReturnExchangesConfirmationModelAsync(int[] returnRequestIds);
}