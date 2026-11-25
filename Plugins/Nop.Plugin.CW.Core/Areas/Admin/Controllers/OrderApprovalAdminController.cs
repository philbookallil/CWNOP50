using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

/// <summary>
/// The lists of the orders that need approval and of the approvers, and the admin's actions on a held order, posted by
/// the approval card of the order page and by the list
/// </summary>
/// <remarks>
/// The card sits inside nop's order form, so its buttons post here by AJAX rather than with forms of their own. Each
/// action answers <c>{ success, message }</c>. Not gated on the Approvals switch: an order already waiting is finished
/// whatever the switch says now.
/// </remarks>
public class OrderApprovalAdminController : BaseAdminController
{
    #region Fields

    protected readonly IApproverService _approverService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderApprovalListModelFactory _orderApprovalListModelFactory;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderApprovalAdminController(IApproverService approverService,
        ILocalizationService localizationService,
        IOrderApprovalListModelFactory orderApprovalListModelFactory,
        IOrderApprovalService orderApprovalService,
        IWorkContext workContext)
    {
        _approverService = approverService;
        _localizationService = localizationService;
        _orderApprovalListModelFactory = orderApprovalListModelFactory;
        _orderApprovalService = orderApprovalService;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The answer the card reads: success, and the message to show
    /// </summary>
    protected virtual async Task<IActionResult> AnswerAsync(OrderApprovalResult result)
    {
        return Json(new
        {
            success = result == OrderApprovalResult.Done,
            message = await _localizationService.GetResourceAsync($"Plugins.CW.Core.Admin.OrderApproval.Result.{result}")
        });
    }

    /// <summary>
    /// The answer the list of approvers reads: success, and the message to show
    /// </summary>
    protected virtual async Task<IActionResult> AnswerAsync(ApproverChangeResult result)
    {
        return Json(new
        {
            success = result == ApproverChangeResult.Done,
            message = await _localizationService.GetResourceAsync($"Plugins.CW.Core.Admin.Approvers.Result.{result}")
        });
    }

    #endregion

    #region Orders

    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> List()
    {
        return View(await _orderApprovalListModelFactory.PrepareOrderApprovalSearchModelAsync(new OrderApprovalSearchModel()));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> List(OrderApprovalSearchModel searchModel)
    {
        return Json(await _orderApprovalListModelFactory.PrepareOrderApprovalListModelAsync(searchModel));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> Approve(int orderId)
    {
        var admin = await _workContext.GetCurrentCustomerAsync();

        return await AnswerAsync(await _orderApprovalService.AcceptByAdminAsync(orderId, admin));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> Decline(int orderId, string reason)
    {
        var admin = await _workContext.GetCurrentCustomerAsync();

        return await AnswerAsync(await _orderApprovalService.DeclineByAdminAsync(orderId, admin, reason));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> Resend(int orderId)
    {
        var admin = await _workContext.GetCurrentCustomerAsync();

        return await AnswerAsync(await _orderApprovalService.ResendAsync(orderId, admin));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> ChangeApprovers(int orderId, string emails, string startedFrom)
    {
        var admin = await _workContext.GetCurrentCustomerAsync();

        return await AnswerAsync(await _orderApprovalService.ChangeApproversAsync(orderId, admin, emails, startedFrom));
    }

    #endregion

    #region Approvers

    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> Approvers()
    {
        return View(await _orderApprovalListModelFactory.PrepareApproverSearchModelAsync(new ApproverSearchModel()));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> ApproverList(ApproverSearchModel searchModel)
    {
        return Json(await _orderApprovalListModelFactory.PrepareApproverListModelAsync(searchModel));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> UpdateApprover(int id, string name, string email)
    {
        return await AnswerAsync(await _approverService.UpdateApproverAsync(id, name, email));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> ReplaceApprover(int id, string name, string email)
    {
        return await AnswerAsync(await _approverService.ReplaceApproverAsync(id, email, name));
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS)]
    public virtual async Task<IActionResult> DeleteApprover(int id)
    {
        return await AnswerAsync(await _approverService.DeleteApproverAsync(id));
    }

    #endregion
}
