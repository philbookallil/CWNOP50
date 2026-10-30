using Nop.Core;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Services.Orders
{
    /// <summary>
    /// Interface contract to define custom methods to deal with NOP Return Requests
    /// </summary>
    public interface ICWReturnRequestService
    {
        Task<IPagedList<ReturnRequest>> SearchReturnRequestsAsync(int storeId = 0, int customerId = 0, string firstname = "", string lastname = "",
                    int orderItemId = 0, string customNumber = "", ReturnRequestStatus? rs = null, DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
                    int pageIndex = 0, int pageSize = int.MaxValue, bool getOnlyTotalCount = false);

    }
}
