namespace Nop.Plugin.Integration.Odoo.Domain;

/// <summary>
/// Identifies the type of synchronization event.
/// </summary>
public enum SyncEventType
{
    Product = 0,
    Branch = 1,
    Store = 2,
    Department = 3,

    /// <summary>
    /// Sends nop orders to Odoo as sale orders.
    /// </summary>
    OrderSend = 4,

    /// <summary>
    /// Cancels in Odoo the sale orders of nop orders cancelled after they were sent.
    /// </summary>
    OrderCancel = 5
}