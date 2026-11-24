using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Prepares the approval card of the admin order page
/// </summary>
public interface IOrderApprovalAdminModelFactory
{
    /// <summary>
    /// Prepares the approval card of an order
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the model; null when the order never needed approval
    /// </returns>
    Task<OrderApprovalAdminModel> PrepareOrderApprovalAdminModelAsync(int orderId);
}
