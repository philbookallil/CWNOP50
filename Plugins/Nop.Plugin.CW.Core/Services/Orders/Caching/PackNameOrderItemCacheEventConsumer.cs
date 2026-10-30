using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Orders.Caching;

public partial class PackNameOrderItemCacheEventConsumer : CacheEventConsumer<PackNameOrderItem>
{
    /// <summary>
    /// entity
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(PackNameOrderItem entity)
    {
        //By prefix, because this entity has OrderItemId and not OrderId - it cannot build the key
        //CW.order.packname.groups.{orderId}. Without this the admin order screen's packname groups
        //miss an item added after the groups were first read (H-5).
        await RemoveByPrefixAsync(OrderServicesDefaults.PackNameOrderGroupsAllPrefix);

        await base.ClearCacheAsync(entity);
    }
}
