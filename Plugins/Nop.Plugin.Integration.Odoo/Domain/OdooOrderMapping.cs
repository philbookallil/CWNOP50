using Nop.Core;

namespace Nop.Plugin.Integration.Odoo.Domain;

/// <summary>
/// One Odoo sale order made from a nop order: one row per destination of the order (a plain order has one,
/// destination key 0). The row is the nop-side guard against sending an order twice; the Odoo order's
/// <c>origin</c> is the Odoo-side one.
/// </summary>
public class OdooOrderMapping : BaseEntity
{
    /// <summary>
    /// Gets or sets the nop order id.
    /// </summary>
    public int NopOrderId { get; set; }

    /// <summary>
    /// Gets or sets the destination: CW.Core's destination key (the lowest packname id sharing the address and
    /// method) on a packname order, 0 on a plain order.
    /// </summary>
    public int DestinationKey { get; set; }

    /// <summary>
    /// Gets or sets the Odoo sale.order id, once created.
    /// </summary>
    public int? OdooOrderId { get; set; }

    /// <summary>
    /// Gets or sets the Odoo order reference (e.g. S00042), once read back.
    /// </summary>
    public string OdooOrderName { get; set; }

    /// <summary>
    /// Gets or sets the status identifier.
    /// </summary>
    public int StatusId { get; set; }

    /// <summary>
    /// Gets or sets why the order is held or failed, or the warnings of the send that confirmed it.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC).
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the last update (UTC).
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public OdooOrderStatus Status
    {
        get => (OdooOrderStatus)StatusId;
        set => StatusId = (int)value;
    }
}
