using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

/// <summary>
/// The admin's actions on a held order, posted by the approval card of the order page
/// </summary>
/// <remarks>
/// The card sits inside nop's order form, so its buttons post here by AJAX rather than with forms of their own. Each
/// action answers <c>{ success, message }</c> and the page reloads on success. Not gated on the Approvals switch: an
/// order already waiting is finished whatever the switch says now.
/// </remarks>
public class OrderApprovalAdminController : BaseAdminController
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderApprovalAdminController(ILocalizationService localizationService,
        IOrderApprovalService orderApprovalService,
        IWorkContext workContext)
    {
        _localizationService = localizationService;
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

    #endregion

    #region Methods

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
    public virtual async Task<IActionResult> ChangeApprover(int orderId, string name, string email)
    {
        var admin = await _workContext.GetCurrentCustomerAsync();

        return await AnswerAsync(await _orderApprovalService.ChangeApproverAsync(orderId, admin, name, email));
    }

    #endregion
}
