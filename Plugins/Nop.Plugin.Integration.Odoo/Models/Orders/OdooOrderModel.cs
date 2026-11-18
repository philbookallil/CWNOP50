using Nop.Web.Framework.Models;

namespace Nop.Plugin.Integration.Odoo.Models;

/// <summary>
/// A row of the Orders page: a nop order and where it stands in Odoo. Id is the nop order id.
/// </summary>
public partial record OdooOrderModel : BaseNopEntityModel
{
    public string CustomOrderNumber { get; set; }

    public string StoreName { get; set; }

    public DateTime CreatedOn { get; set; }

    public string OrderTotal { get; set; }

    /// <summary>
    /// nop's order status, localised, or "Deleted".
    /// </summary>
    public string OrderStatus { get; set; }

    /// <summary>
    /// nop's payment status of the order, localised.
    /// </summary>
    public string PaymentStatus { get; set; }

    /// <summary>
    /// Where the order stands in Odoo (<c>OdooOrderSyncSummary</c>), e.g. "Not sent", "Sent", "Failed".
    /// </summary>
    public string OdooStatus { get; set; }

    /// <summary>
    /// Whether the row offers Sync.
    /// </summary>
    public bool CanSend { get; set; }

    /// <summary>
    /// Whether the row offers Cancel in Odoo.
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// The Odoo order references.
    /// </summary>
    public string OdooOrders { get; set; }

    /// <summary>
    /// Why it is held or failed, or the warnings of the send.
    /// </summary>
    public string Message { get; set; }
}
