using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

/// <summary>
/// The approval card of the admin order page: where the order's approval stands, the links sent, and the admin's
/// decide / resend / change approver buttons while it waits
/// </summary>
/// <remarks>
/// Rendered by CW.Core's copy of nop's order page, as the first card. Nothing for an order that never needed approval,
/// and nothing for a vendor. Not gated on the Approvals switch: an approval that exists is shown.
/// </remarks>
[ViewComponent(Name = "OrderApprovalAdmin")]
public class OrderApprovalAdminViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IOrderApprovalAdminModelFactory _orderApprovalAdminModelFactory;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderApprovalAdminViewComponent(IOrderApprovalAdminModelFactory orderApprovalAdminModelFactory,
        IWorkContext workContext)
    {
        _orderApprovalAdminModelFactory = orderApprovalAdminModelFactory;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(int orderId)
    {
        if (await _workContext.GetCurrentVendorAsync() != null)
            return Content(string.Empty);

        var model = await _orderApprovalAdminModelFactory.PrepareOrderApprovalAdminModelAsync(orderId);

        if (model == null)
            return Content(string.Empty);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Shared/Components/OrderApprovalAdmin/Default.cshtml", model);
    }

    #endregion
}
