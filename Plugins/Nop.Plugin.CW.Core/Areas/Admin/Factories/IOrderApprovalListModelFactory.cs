using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Prepares the admin lists of the order approvals: the orders, and the approvers
/// </summary>
public interface IOrderApprovalListModelFactory
{
    /// <summary>
    /// Prepares the search of the orders; waiting ones by default
    /// </summary>
    /// <param name="searchModel">The search</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the search
    /// </returns>
    Task<OrderApprovalSearchModel> PrepareOrderApprovalSearchModelAsync(OrderApprovalSearchModel searchModel);

    /// <summary>
    /// Prepares a page of the orders
    /// </summary>
    /// <param name="searchModel">The search</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the page
    /// </returns>
    Task<OrderApprovalListModel> PrepareOrderApprovalListModelAsync(OrderApprovalSearchModel searchModel);

    /// <summary>
    /// Prepares the search of the approvers
    /// </summary>
    /// <param name="searchModel">The search</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the search
    /// </returns>
    Task<ApproverSearchModel> PrepareApproverSearchModelAsync(ApproverSearchModel searchModel);

    /// <summary>
    /// Prepares a page of the approvers
    /// </summary>
    /// <param name="searchModel">The search</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the page
    /// </returns>
    Task<ApproverListModel> PrepareApproverListModelAsync(ApproverSearchModel searchModel);
}
