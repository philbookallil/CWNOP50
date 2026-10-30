using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Security;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

/// <summary>
/// Puts the E and R badges on the admin order lists — <i>Sales → Orders</i> and the <i>Orders</i> tab of a
/// customer.
/// </summary>
/// <remarks>
/// nop's order grids are built by its own factory and view, which offer no seam for a column; overriding them
/// would copy a 43-service constructor and a 600-line view for two flags. This widget instead renders a config
/// div and registers a script that, after each draw of either grid, asks <c>CWOrder/OrderBadges</c> for the flags
/// of the orders on the page and appends the badges to their order-number cells. One request per page.
/// </remarks>
[ViewComponent(Name = "OrderListBadges")]
public class OrderListBadgesViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IPermissionService _permissionService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderListBadgesViewComponent(IPermissionService permissionService,
        IWorkContext workContext)
    {
        _permissionService = permissionService;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        //a vendor sees no order status and no returns; a user who cannot view orders gets nothing to ask for
        if (await _workContext.GetCurrentVendorAsync() != null
            || !await _permissionService.AuthorizeAsync(StandardPermission.Orders.ORDERS_VIEW))
            return Content(string.Empty);

        return View();
    }

    #endregion
}