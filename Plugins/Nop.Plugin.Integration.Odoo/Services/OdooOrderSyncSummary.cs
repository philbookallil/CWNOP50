using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Where a nop order stands in Odoo, as one row of the Orders page shows it, and what may be done with it from there.
/// Pure: built from the order's state and its mapping rows, so it is unit-tested.
/// </summary>
/// <param name="Status">The one-word state shown in the status column.</param>
/// <param name="CanSend">Whether the row offers Sync: the order is live in nop and not every Odoo order of it is confirmed.</param>
/// <param name="CanCancel">Whether the row offers Cancel in Odoo: the order is cancelled or deleted in nop while an Odoo
/// order of it is still open.</param>
public record OdooOrderSyncSummary(string Status, bool CanSend, bool CanCancel)
{
    public const string NotSent = "Not sent";
    public const string Sent = "Sent";
    public const string Held = "Held";
    public const string Failed = "Failed";
    public const string InProgress = "In progress";
    public const string ToCancel = "To cancel in Odoo";
    public const string CancelledInOdoo = "Cancelled in Odoo";
    public const string NotSentCancelled = "Not sent (cancelled)";

    /// <summary>
    /// The summary of one order.
    /// </summary>
    /// <param name="cancelledOrDeleted">Whether the order is cancelled or deleted in nop.</param>
    /// <param name="mappings">Its mapping rows, one per destination already tried.</param>
    public static OdooOrderSyncSummary For(bool cancelledOrDeleted, IReadOnlyCollection<OdooOrderMapping> mappings)
    {
        var open = mappings.Any(mapping => mapping.OdooOrderId != null
            && mapping.Status is OdooOrderStatus.Created or OdooOrderStatus.Confirmed or OdooOrderStatus.Failed);

        if (cancelledOrDeleted)
        {
            if (open)
                return new(ToCancel, CanSend: false, CanCancel: true);

            return mappings.Any(mapping => mapping.Status == OdooOrderStatus.Cancelled)
                ? new(CancelledInOdoo, false, false)
                : new(NotSentCancelled, false, false);
        }

        if (mappings.Count == 0)
            return new(NotSent, CanSend: true, CanCancel: false);

        if (mappings.All(mapping => mapping.Status is OdooOrderStatus.Confirmed or OdooOrderStatus.Cancelled))
            return new(Sent, false, false);

        if (mappings.Any(mapping => mapping.Status == OdooOrderStatus.Failed))
            return new(Failed, true, false);

        if (mappings.Any(mapping => mapping.Status == OdooOrderStatus.Held))
            return new(Held, true, false);

        return new(InProgress, true, false);
    }
}
