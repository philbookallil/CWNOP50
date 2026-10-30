using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Models.Orders;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Keeps a shipment on a packname order inside one shipping destination.
/// </summary>
/// <remarks>
/// nop's Shipment is order-scoped and holds no address, so nothing in the core stops staff putting
/// items bound for two different addresses under one tracking number. On a packname order that is not
/// an edge case — several destinations is the normal shape. This narrows the Add Shipment screen to the
/// destination that was chosen, and rejects a post that spans more than one.
/// </remarks>
public class AddShipmentDestinationFilter : CoreActionFilter, IAsyncActionFilter
{
    #region Constants

    /// <summary>Querystring/form key carrying the chosen destination.</summary>
    public const string DESTINATION_KEY = "cwDestination";

    /// <summary>ViewData key under which the resolved destination is handed to the banner component.</summary>
    public const string DESTINATION_VIEWDATA_KEY = "CWShipmentDestination";

    #endregion

    #region Fields

    protected readonly ICWOrderService _cwOrderService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;

    #endregion

    #region Ctor

    public AddShipmentDestinationFilter(ICWOrderService cwOrderService,
        ILocalizationService localizationService,
        INotificationService notificationService)
    {
        _cwOrderService = cwOrderService;
        _localizationService = localizationService;
        _notificationService = notificationService;
    }

    #endregion

    #region Utilities

    private bool IsAddShipment(object controller, ControllerActionDescriptor actionDescriptor)
    {
        return actionDescriptor != null
            && IsController<OrderController>(controller)
            && IsActionName("AddShipment", actionDescriptor);
    }

    private static int? ReadDestinationKey(HttpContext httpContext)
    {
        var raw = httpContext.Request.Query[DESTINATION_KEY].ToString();

        if (string.IsNullOrEmpty(raw) && httpContext.Request.HasFormContentType)
            raw = httpContext.Request.Form[DESTINATION_KEY].ToString();

        return int.TryParse(raw, out var key) ? key : null;
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor ||
            !IsAddShipment(context.Controller, descriptor))
        {
            await next();
            return;
        }

        var httpContext = context.HttpContext;

        //---------- POST: refuse a shipment that spans destinations ----------
        if (IsPost(httpContext))
        {
            var orderId = context.ActionArguments.TryGetValue("model", out var raw) && raw is ShipmentModel posted
                ? posted.OrderId
                : 0;

            var destinations = orderId > 0
                ? await _cwOrderService.GetOrderShippingDestinationsAsync(orderId)
                : [];

            //Not a packname order — nop's one-address assumption holds, leave it alone.
            if (destinations.Count > 1)
            {
                var destinationByOrderItem = destinations
                    .SelectMany(d => d.OrderItemIds.Select(id => (OrderItemId: id, d.Key)))
                    .ToDictionary(x => x.OrderItemId, x => x.Key);

                var selected = new HashSet<int>();

                foreach (var formKey in httpContext.Request.Form.Keys)
                {
                    if (!formKey.StartsWith("qtyToAdd", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    if (!int.TryParse(formKey["qtyToAdd".Length..], out var orderItemId))
                        continue;

                    if (!int.TryParse(httpContext.Request.Form[formKey], out var qty) || qty <= 0)
                        continue;

                    if (destinationByOrderItem.TryGetValue(orderItemId, out var destinationKey))
                        selected.Add(destinationKey);
                }

                if (selected.Count > 1)
                {
                    _notificationService.ErrorNotification(
                        await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Orders.Shipments.OneDestinationOnly"));

                    context.Result = CreateRedirect("Edit", "Order",
                        new Microsoft.AspNetCore.Routing.RouteValueDictionary { ["id"] = orderId },
                        Nop.Web.Framework.AreaNames.ADMIN);

                    return;
                }
            }

            var postResult = await next();

            //nop's "no products selected" path does RedirectToAction("AddShipment", model), which loses
            //the destination — the form posts to a bare URL, so the querystring is already gone. Without
            //this, a mis-click drops the user back onto the full, unnarrowed item list.
            if (postResult.Result is RedirectToActionResult redirect &&
                string.Equals(redirect.ActionName, "AddShipment", StringComparison.OrdinalIgnoreCase))
            {
                var postedKey = ReadDestinationKey(httpContext);

                if (postedKey != null)
                {
                    redirect.RouteValues ??= new Microsoft.AspNetCore.Routing.RouteValueDictionary();
                    redirect.RouteValues[DESTINATION_KEY] = postedKey.Value;
                }
            }

            return;
        }

        //---------- GET: narrow the item list to the chosen destination ----------
        var resultContext = await next();

        var key = ReadDestinationKey(httpContext);

        if (key == null || resultContext.Controller is not Controller mvcController)
            return;

        if (mvcController.ViewData.Model is not ShipmentModel model)
            return;

        var orderDestinations = await _cwOrderService.GetOrderShippingDestinationsAsync(model.OrderId);
        var destination = orderDestinations.FirstOrDefault(d => d.Key == key.Value);

        if (destination == null)
            return;

        var allowed = destination.OrderItemIds.ToHashSet();

        model.Items = model.Items.Where(i => allowed.Contains(i.OrderItemId)).ToList();

        //handed whole to the banner component — OrderShippingDestination already carries both the label
        //it renders and the Key it echoes back into the form, so there is nothing to flatten
        mvcController.ViewData[DESTINATION_VIEWDATA_KEY] = destination;
    }

    #endregion
}