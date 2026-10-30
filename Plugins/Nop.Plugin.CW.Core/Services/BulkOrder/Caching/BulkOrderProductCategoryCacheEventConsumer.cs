using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

public partial class BulkOrderProductCategoryCacheEventConsumer : CacheEventConsumer<ProductCategory>
{
    protected override async Task ClearCacheAsync(ProductCategory entity)
    {
        await RemoveByPrefixAsync(BulkOrderServicesDefaults.VisibleCategoryIdsPrefix);

        await base.ClearCacheAsync(entity);
    }
}