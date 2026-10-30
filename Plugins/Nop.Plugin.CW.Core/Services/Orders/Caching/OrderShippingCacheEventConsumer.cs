using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Orders.Caching;

public partial class OrderShippingCacheEventConsumer : CacheEventConsumer<OrderShipping>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(OrderShipping entity)
    {
        await RemoveAsync(OrderServicesDefaults.OrderShippingCacheKey, entity.OrderId);
        await RemoveAsync(OrderServicesDefaults.PackNameOrderGroupsCacheKey, entity.OrderId);

        await base.ClearCacheAsync(entity);
    }
}
