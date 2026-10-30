using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Plugin.CW.Core.Domain.Orders.Views;

namespace Nop.Plugin.CW.Core.Services.Orders;

public interface ICWOrderService
{
    /// <summary>
    /// Gets a list of PackNameFullOrderItem by order id
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    Task<IList<PackNameFullOrderItem>> GetPackNameFullOrderItemsAsync(int orderId);

    /// <summary>
    /// Gets a PackNameOrderItem by order item id
    /// </summary>
    /// <param name="orderItemId"></param>
    /// <returns></returns>
    Task<PackNameOrderItem> GetPackNameOrderItemAsync(int orderItemId);

    /// <summary>
    /// Inserts a packname order item
    /// </summary>
    /// <param name="orderItem">Order item</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertPackNameOrderItemAsync(PackNameOrderItem orderItem);

    Task<IList<OrderShipping>> GetOrderShippingAsync(int orderId);

    /// <summary>
    /// Inserts an order shipping
    /// </summary>
    /// <param name="orderShipping">Order shipping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertOrderShippingAsync(OrderShipping orderShipping);

    /// <summary>
    /// Updates an order shipping
    /// </summary>
    /// <param name="orderShipping">Order shipping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task UpdateOrderShippingAsync(OrderShipping orderShipping);

    /// <summary>
    /// Gets what the settlement pipeline did for an order
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the settlement, or null when nothing was covered or the order was placed
    /// before settlements were recorded
    /// </returns>
    Task<OrderSettlement> GetOrderSettlementAsync(int orderId);

    /// <summary>
    /// Inserts an order settlement
    /// </summary>
    /// <param name="orderSettlement">Order settlement</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertOrderSettlementAsync(OrderSettlement orderSettlement);

    /// <summary>
    /// Whether any item on this order is a packname item.
    /// </summary>
    /// <remarks>
    /// One definition of the question. <c>GetOrderShippingDestinationsAsync</c> answers a DIFFERENT one
    /// ("how many destinations"), and reading <c>destinations.Count > 1</c> as "is a packname order" is
    /// wrong for the common single-destination case.
    /// </remarks>
    Task<bool> IsPackNameOrderAsync(int orderId);

    /// <summary>
    /// Whether this address belongs to a packname order in ANY role - one of its per-packname
    /// destinations, or the order's own billing, shipping or pickup address.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked by the admin order guard, because nop's <c>AddressEdit</c> POST resolves the address from
    /// <c>model.Address.Id</c> and never checks it belongs to <c>model.OrderId</c>
    /// (<c>OrderController:1739</c>) - so the posted order id is not the thing being mutated.
    /// </para>
    /// <para>
    /// <b>Both halves are needed.</b> <c>OrderShipping</c> rows are written only for packname orders, so
    /// that table answers the destination question on its own - but a packname order's BILLING address
    /// is on the <c>Order</c> row and appears in no <c>OrderShipping</c> row. Checking the destinations
    /// alone left it editable by posting a non-packname order's id with this address id: neither
    /// identifier flagged and nop edited the address it was handed. Confirmed live against order 131.
    /// </para>
    /// </remarks>
    Task<bool> IsPackNameOrderAddressAsync(int addressId);

    /// <summary>
    /// Returns order items grouped by packname, with shipping, address, and pre-formatted
    /// address display fields loaded. Returns an empty list when the order has no packname items.
    /// </summary>
    Task<IList<PackNameOrderGroupDTO>> GetPackNameOrderGroupsAsync(int orderId);

    /// <summary>
    /// The distinct shipping destinations on a packname order — one entry per (address, pickup, method),
    /// carrying every packname and order item that shares it. Empty for a non-packname order.
    /// </summary>
    Task<IList<OrderShippingDestination>> GetOrderShippingDestinationsAsync(int orderId);

    /// <summary>
    /// The shipping destination a saved shipment's items belong to, or null when the order is not a
    /// packname order and nop's single-address assumption holds.
    /// </summary>
    /// <remarks>
    /// The one place this is worked out. The packing slip, the customer's shipment page and the shipment
    /// emails all ask the same question, and answering it three times is how they end up disagreeing.
    /// </remarks>
    Task<OrderShippingDestination> GetShipmentDestinationAsync(int shipmentId, int orderId);
}