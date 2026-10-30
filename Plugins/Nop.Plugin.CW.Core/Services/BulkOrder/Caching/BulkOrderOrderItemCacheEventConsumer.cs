using Nop.Core.Domain.Orders;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

public partial class BulkOrderOrderItemCacheEventConsumer : CacheEventConsumer<OrderItem>
{
    protected override async Task ClearCacheAsync(OrderItem entity)
    {
        await RemoveByPrefixAsync(BulkOrderServicesDefaults.VisibleCategoryIdsPrefix);

        await base.ClearCacheAsync(entity);
    }
}