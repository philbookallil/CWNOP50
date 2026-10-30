using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Orders.Caching;

public partial class OrderSettlementCacheEventConsumer : CacheEventConsumer<OrderSettlement>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(OrderSettlement entity)
    {
        await RemoveAsync(OrderServicesDefaults.OrderSettlementCacheKey, entity.OrderId);

        await base.ClearCacheAsync(entity);
    }
}