using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

/// <summary>
/// Shows which of a packname order's shipping destinations a shipment belongs to.
/// </summary>
/// <remarks>
/// Two screens, two ways of knowing. On **Add Shipment** the destination was chosen before the page
/// loaded, and AddShipmentDestinationFilter has already put it in ViewData and narrowed the item list.
/// On **Shipment Details** nothing has been chosen — the shipment exists, so its destination is derived
/// from the order items it contains. nop shows no address on either screen, so on a multi-destination
/// order this is the only thing that answers "where did this one go".
/// </remarks>
[ViewComponent(Name = "CWShipmentDestination")]
public class CWShipmentDestinationViewComponent : NopViewComponent
{
    #region Fields

    protected readonly ICWOrderService _cwOrderService;

    #endregion

    #region Ctor

    public CWShipmentDestinationViewComponent(ICWOrderService cwOrderService)
    {
        _cwOrderService = cwOrderService;
    }

    #endregion

    #region Utilities

    protected async Task<string> ResolveLabelAsync(int shipmentId, int orderId)
    {
        var destination = await _cwOrderService.GetShipmentDestinationAsync(shipmentId, orderId);

        return destination?.Label;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        //--- Add Shipment: the filter already resolved it, and the form needs the key echoed back ---
        if (ViewData[AddShipmentDestinationFilter.DESTINATION_VIEWDATA_KEY] is OrderShippingDestination chosen)
        {
            return View(new CWShipmentDestinationModel
            {
                Label = chosen.Label,
                DestinationKey = chosen.Key
            });
        }

        //--- Shipment Details: derive it from the shipment's own items ---
        //Id > 0 also keeps this off the Add Shipment screen, where additionalData is an unsaved
        //ShipmentModel with Id 0 and no items to resolve from.
        if (additionalData is ShipmentModel shipment && shipment.Id > 0 && shipment.OrderId > 0)
        {
            var label = await ResolveLabelAsync(shipment.Id, shipment.OrderId);

            if (!string.IsNullOrEmpty(label))
                return View(new CWShipmentDestinationModel { Label = label });
        }

        return Content(string.Empty);
    }

    #endregion
}