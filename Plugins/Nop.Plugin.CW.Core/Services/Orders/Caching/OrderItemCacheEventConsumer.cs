using Nop.Core.Domain.Orders;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Orders.Caching;

public partial class OrderItemCacheEventConsumer : CacheEventConsumer<OrderItem>
{
    /// <summary>
    /// entity
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(OrderItem entity)
    {
        await RemoveAsync(OrderServicesDefaults.PackNameOrderGroupsCacheKey, entity.OrderId);

        await base.ClearCacheAsync(entity);
    }
}
