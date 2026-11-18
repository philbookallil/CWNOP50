using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// The sync-state filter of the Orders page.
/// </summary>
public enum OdooOrderSyncFilter
{
    /// <summary>Every order.</summary>
    All = 0,

    /// <summary>Never sent: no Odoo order recorded.</summary>
    NotSent = 1,

    /// <summary>Every Odoo order of it confirmed.</summary>
    Sent = 2,

    /// <summary>Held, failed or part-way: the next send would do something.</summary>
    NeedsAttention = 3,

    /// <summary>Cancelled or deleted in nop while an Odoo order of it is still open.</summary>
    ToCancel = 4
}

/// <summary>
/// Sends nop orders to Odoo as sale orders, one per destination, and cancels them there.
/// </summary>
public interface IOrderSyncService
{
    /// <summary>
    /// One page of the Orders page's list, newest first: every order, synced or not, plus deleted orders whose
    /// Odoo orders are still open.
    /// </summary>
    /// <param name="paymentStatusId">0 all, otherwise a nop payment status.</param>
    Task<IPagedList<Order>> SearchOrdersAsync(int storeId = 0, DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
        OdooOrderSyncFilter syncFilter = OdooOrderSyncFilter.All, int paymentStatusId = 0,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// The ids of the orders the list shows with these filters that a send would act on: not cancelled or deleted,
    /// and not every Odoo order of it already confirmed.
    /// </summary>
    Task<IList<int>> GetSendableOrderIdsAsync(int storeId = 0, DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
        OdooOrderSyncFilter syncFilter = OdooOrderSyncFilter.All, int paymentStatusId = 0);

    /// <summary>
    /// The mapping rows of the given orders.
    /// </summary>
    Task<IList<OdooOrderMapping>> GetMappingsAsync(IList<int> orderIds);

    /// <summary>
    /// Sends the given orders: creates and confirms what is not yet confirmed, holds what cannot be sent.
    /// </summary>
    Task<OrderSyncResult> SendOrdersAsync(int logEventId, IReadOnlyList<int> orderIds);

    /// <summary>
    /// Cancels in Odoo the open sale orders of the given orders, which must be cancelled or deleted in nop.
    /// </summary>
    Task<OrderSyncResult> CancelOrdersAsync(int logEventId, IReadOnlyList<int> orderIds);
}

/// <summary>
/// The result of an order run, per nop order: Created when an Odoo order was created, Updated when an existing one
/// was confirmed or cancelled, Skipped otherwise (held, failed, nothing to do).
/// </summary>
public record OrderSyncResult(int Created, int Updated, int Skipped)
{
    public int Processed => Created + Updated + Skipped;
}
