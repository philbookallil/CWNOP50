using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Orders;

/// <summary>
/// nop's return request service, with the units of a rejected return available to return again
/// </summary>
public class ReturnRequestOverrideService : ReturnRequestService
{
    #region Ctor

    public ReturnRequestOverrideService(IRepository<ReturnRequest> returnRequestRepository,
        IRepository<ReturnRequestAction> returnRequestActionRepository,
        IRepository<ReturnRequestReason> returnRequestReasonRepository,
        IRepository<OrderItem> orderItemRepository,
        IRepository<Product> productRepository,
        ReturnRequestSettings returnRequestSettings) : base(returnRequestRepository,
            returnRequestActionRepository,
            returnRequestReasonRepository,
            orderItemRepository,
            productRepository,
            returnRequestSettings)
    {
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the return request availability
    /// </summary>
    /// <remarks>
    /// nop works out which items can be returned (no non-returnable product and, unless allowed, no download); this
    /// recounts what is still asked for on each of them without the rejected returns, so a unit whose return was
    /// rejected can be returned again. The quantity never goes above what was ordered.
    /// </remarks>
    /// <param name="orderId">The order identifier</param>
    /// <returns>The <see cref="Task"/> containing the <see cref="ReturnRequestAvailability"/></returns>
    public override async Task<ReturnRequestAvailability> GetReturnRequestAvailabilityAsync(int orderId)
    {
        var availability = await base.GetReturnRequestAvailabilityAsync(orderId);

        if (availability?.ReturnableOrderItems == null || availability.ReturnableOrderItems.Count == 0)
            return availability;

        var orderItemIds = availability.ReturnableOrderItems.Select(item => item.OrderItem.Id).ToList();

        //nop gives back only a cancelled return's units; a rejected return's are given back too
        var cancelledStatusId = (int)ReturnRequestStatus.Cancelled;
        var rejectedStatusId = (int)ReturnRequestStatus.RequestRejected;

        var requested = await _returnRequestRepository.Table
            .Where(returnRequest => orderItemIds.Contains(returnRequest.OrderItemId)
                && returnRequest.ReturnRequestStatusId != cancelledStatusId
                && returnRequest.ReturnRequestStatusId != rejectedStatusId)
            .GroupBy(returnRequest => returnRequest.OrderItemId)
            .Select(group => new { OrderItemId = group.Key, Quantity = group.Sum(returnRequest => returnRequest.Quantity) })
            .ToDictionaryAsync(row => row.OrderItemId, row => row.Quantity);

        foreach (var returnable in availability.ReturnableOrderItems)
            returnable.AvailableQuantityForReturn = Math.Max(returnable.OrderItem.Quantity - requested.GetValueOrDefault(returnable.OrderItem.Id), 0);

        return availability;
    }

    #endregion
}