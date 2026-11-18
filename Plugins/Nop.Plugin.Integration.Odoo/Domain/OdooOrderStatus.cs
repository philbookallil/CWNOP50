namespace Nop.Plugin.Integration.Odoo.Domain;

/// <summary>
/// Where one Odoo sale order made from a nop order stands.
/// </summary>
public enum OdooOrderStatus
{
    /// <summary>Created in Odoo, not yet confirmed: the next send confirms it.</summary>
    Created = 1,

    /// <summary>Created and confirmed in Odoo. Never sent again.</summary>
    Confirmed = 2,

    /// <summary>Not sent: something the order needs is missing in nop or in the plugin's settings. The message says what.</summary>
    Held = 3,

    /// <summary>Odoo refused a call. The message is Odoo's; the next send tries again.</summary>
    Failed = 4,

    /// <summary>Cancelled in Odoo after the nop order was cancelled or deleted.</summary>
    Cancelled = 5,

    /// <summary>Recorded before the send reached it: a run that stops part-way leaves the destination listed to send.</summary>
    Pending = 6
}
