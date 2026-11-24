using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Refuses nop's admin actions that would move an order held for approval on, or change what the approver is
/// approving: setting its status by hand, adding a shipment, and editing its lines, totals or shipping method
/// </summary>
/// <remarks>
/// <para>
/// Payment, shipping and completion move an order through nop's <c>SetOrderStatusAsync</c>, and
/// <c>OrderApprovalEventConsumer</c> puts a held order back there. <c>ChangeOrderStatus</c> sets any status the admin
/// picks, Cancelled included, without nop's cancel (the stock would not come back); a shipment would send goods nobody
/// has approved; an edited line or total would change the order after the approver was shown it. The Cancel and Delete
/// buttons stay open: either ends the approval. Matched on the method name, as <see cref="PackNameOrderAdminGuardFilter"/>
/// explains.
/// </para>
/// <para>
/// A global filter is built for every request, so it reads the approval table itself rather than through the approval
/// service and its dependencies, and only once the action is one it guards. No feature-flag check: a held order is
/// protected whatever the switch says now. Fails closed: a guarded action whose order cannot be identified is refused.
/// </para>
/// </remarks>
public class OrderApprovalAdminGuardFilter : CoreActionFilter, IAsyncActionFilter
{
    #region Fields

    /// <summary>
    /// nop OrderController METHOD names that would move a held order on or change it
    /// </summary>
    private static readonly string[] BlockedMethods =
    [
        "ChangeOrderStatus",
        "AddShipment",
        "EditOrderItem",
        "DeleteOrderItem",
        "AddProductToOrder",
        "AddProductToOrderDetails",
        "EditOrderTotals",
        "EditShippingMethod"
    ];

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IRepository<OrderApproval> _orderApprovalRepository;

    #endregion

    #region Ctor

    public OrderApprovalAdminGuardFilter(ILocalizationService localizationService,
        INotificationService notificationService,
        IRepository<OrderApproval> orderApprovalRepository)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _orderApprovalRepository = orderApprovalRepository;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The order id, from whichever argument the action carries it in
    /// </summary>
    private static int ResolveOrderId(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("model", out var model) && model is ShipmentModel shipmentModel)
            return shipmentModel.OrderId;

        if (context.ActionArguments.TryGetValue("searchModel", out var searchModel) && searchModel is AddProductToOrderSearchModel productSearchModel)
            return productSearchModel.OrderId;

        if (context.ActionArguments.TryGetValue("orderId", out var orderId) && orderId is int orderIdValue)
            return orderIdValue;

        //ChangeOrderStatus, the AddShipment GET, EditOrderItem, DeleteOrderItem, EditOrderTotals and EditShippingMethod
        //bind the order id as the route's "id"
        if (context.ActionArguments.TryGetValue("id", out var id) && id is int idValue)
            return idValue;

        return 0;
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor
            || context.Controller is not OrderController
            || !BlockedMethods.Contains(descriptor.MethodInfo.Name))
        {
            await next();
            return;
        }

        var orderId = ResolveOrderId(context);

        if (orderId > 0 && !await _orderApprovalRepository.Table
                .AnyAsync(approval => approval.OrderId == orderId && approval.StatusId == (int)OrderApprovalStatus.Pending))
        {
            await next();
            return;
        }

        //the product search grid behind "Add product" reads JSON: a redirect would render as nothing having happened
        var isAjax = string.Equals(context.HttpContext.Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (orderId > 0 && !isAjax)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.OrderApproval.OrderHeld"));
            context.Result = CreateRedirect("Edit", "Order", new RouteValueDictionary { ["id"] = orderId }, AreaNames.ADMIN);
            return;
        }

        context.Result = IsGet(context.HttpContext) && !isAjax
            ? CreateRedirect("List", "Order", null, AreaNames.ADMIN)
            : new StatusCodeResult(StatusCodes.Status403Forbidden);
    }

    #endregion
}
