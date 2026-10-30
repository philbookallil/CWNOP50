using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges.DTOs;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

public class CWOrderController : BaseAdminController
{
    #region Fields

    protected readonly IOrderModelFactory _orderModelFactory;
    protected readonly IOrderService _orderService;
    protected readonly IShipmentService _shipmentService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IWorkContext _workContext;
    protected readonly IExchangeService _exchangeService;

    #endregion

    #region Ctor

    public CWOrderController(IOrderModelFactory orderModelFactory,
        IOrderService orderService,
        IShipmentService shipmentService,
        ICWOrderService cwOrderService,
        IWorkContext workContext,
        IExchangeService exchangeService)
    {
        _orderModelFactory = orderModelFactory;
        _orderService = orderService;
        _shipmentService = shipmentService;
        _cwOrderService = cwOrderService;
        _workContext = workContext;
        _exchangeService = exchangeService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// nop's shipments grid for an order, plus a Destination column. Same data, same paging — the only
    /// addition is which of the packname order's destinations each shipment went to.
    /// </summary>
    [HttpPost]
    [CheckPermission(StandardPermission.Orders.ORDERS_VIEW)]
    public virtual async Task<IActionResult> ShipmentsByOrder(OrderShipmentSearchModel searchModel)
    {
        var order = await _orderService.GetOrderByIdAsync(searchModel.OrderId)
            ?? throw new ArgumentException("No order found with the specified id");

        var currentVendor = await _workContext.GetCurrentVendorAsync();

        if (currentVendor != null && !(await _orderService.GetOrderItemsAsync(order.Id, vendorId: currentVendor.Id)).Any())
            return Content(string.Empty);

        var baseModel = await _orderModelFactory.PrepareOrderShipmentListModelAsync(searchModel, order);

        var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(order.Id);

        //AddressSummary rather than Label: a grid cell has no room for the method and the packname list,
        //and the address is the part that distinguishes one destination from another.
        var destinationByOrderItem = destinations
            .SelectMany(d => d.OrderItemIds.Select(id => (OrderItemId: id, Text: d.AddressSummary)))
            .ToDictionary(x => x.OrderItemId, x => x.Text);

        var rows = new List<CWShipmentModel>();

        foreach (var shipment in baseModel.Data)
        {
            var items = await _shipmentService.GetShipmentItemsByShipmentIdAsync(shipment.Id);

            //Distinct because a shipment holds several items; normally they all resolve to one
            //destination, and after the guard in §7 they always will. Legacy rows may not, and a row
            //that lists two destinations is exactly what someone needs to see.
            var summaries = items
                .Select(i => destinationByOrderItem.TryGetValue(i.OrderItemId, out var label) ? label : null)
                .Where(label => !string.IsNullOrEmpty(label))
                .Distinct()
                .ToList();

            rows.Add(new CWShipmentModel(shipment)
            {
                Destination = summaries.Count switch
                {
                    0 => string.Empty,
                    1 => summaries[0],
                    _ => string.Join(" + ", summaries)
                }
            });
        }

        return Json(new CWOrderShipmentListModel
        {
            Draw = baseModel.Draw,
            RecordsTotal = baseModel.RecordsTotal,
            RecordsFiltered = baseModel.RecordsFiltered,
            Data = rows
        });
    }

    /// <summary>
    /// The E and R badges of the orders on one page of an admin order list.
    /// </summary>
    /// <param name="orderIds">Identifiers of the orders on the page</param>
    [HttpPost]
    [CheckPermission(StandardPermission.Orders.ORDERS_VIEW)]
    public virtual async Task<IActionResult> OrderBadges(int[] orderIds)
    {
        //one page of a grid; a request for more is not one
        const int maxOrders = 500;

        //a vendor's list has no order status column and no business with returns
        if (orderIds == null || orderIds.Length == 0 || await _workContext.GetCurrentVendorAsync() != null)
            return Json(Array.Empty<OrderReturnFlags>());

        return Json(await _exchangeService.GetOrderReturnFlagsAsync(orderIds.Take(maxOrders).ToArray()));
    }

    #endregion
}