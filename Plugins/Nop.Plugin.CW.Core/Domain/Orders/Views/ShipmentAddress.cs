using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Orders.Views;

/// <summary>
/// The address a shipment actually went to, one row per shipment. Backed by
/// <c>vw_CORE_PN_ShipmentAddress</c> — see CWDatabaseService. Query-only.
/// </summary>
/// <remarks>
/// nop assumes one address per order, so its shipment-list location filters test
/// <c>Order.ShippingAddressId</c> — a placeholder on a packname order. This view resolves the real one:
/// the destination's address where the shipment's items belong to a packname that has an
/// <c>OrderShipping</c> row, and the order's own address otherwise. A non-packname order therefore
/// resolves to exactly what nop would have used, which is what keeps the filters unchanged for ordinary
/// orders.
/// <para>
/// <c>AddressId</c> is null when nothing resolves — an order with no address recorded. Such a shipment
/// simply matches no location filter, which is the same as today.
/// </para>
/// </remarks>
public class ShipmentAddress : BaseEntity
{
    /// <summary>Gets or sets the shipment identifier. Same value as <see cref="BaseEntity.Id"/>.</summary>
    public int ShipmentId { get; set; }

    public int OrderId { get; set; }

    /// <summary>Gets or sets the address this shipment went to. Null when none resolves.</summary>
    public int? AddressId { get; set; }
}