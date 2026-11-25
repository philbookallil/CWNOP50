using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals.DTOs;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Reads the approvals of the orders for the admin list
/// </summary>
public interface IOrderApprovalReportService
{
    /// <summary>
    /// Whether any order ever needed approval
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true when one did
    /// </returns>
    Task<bool> HasApprovalsAsync();

    /// <summary>
    /// Searches the approvals, newest first; those of a deleted order are left out
    /// </summary>
    /// <param name="status">The status; null for every one</param>
    /// <param name="storeId">Store identifier; 0 for every store</param>
    /// <param name="orderNumber">The order's number; empty for all</param>
    /// <param name="customer">Part of the customer's name or e-mail address; empty for all</param>
    /// <param name="approverId">An approver asked (while it waits: with a link that still works); 0 for all</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the approvals with their orders
    /// </returns>
    Task<IPagedList<OrderApprovalLineDTO>> SearchApprovalsAsync(OrderApprovalStatus? status = null, int storeId = 0, string orderNumber = null,
        string customer = null, int approverId = 0, int pageIndex = 0, int pageSize = int.MaxValue);
}
