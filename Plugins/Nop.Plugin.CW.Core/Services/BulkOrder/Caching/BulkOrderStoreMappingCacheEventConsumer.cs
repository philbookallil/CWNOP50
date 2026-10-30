using Nop.Core.Domain.Stores;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

/// <summary>
/// The visible-category tree is filtered by store mapping inside ApplyVisibilityAsync, but the cache
/// key embeds only the storeId - so limiting a category or product to a different set of stores leaves
/// the key identical and the cached answer wrong. Five consumers already clear this prefix for lesser
/// changes; there was no consumer for StoreMapping at all (H-3).
/// </summary>
public partial class BulkOrderStoreMappingCacheEventConsumer : CacheEventConsumer<StoreMapping>
{
    protected override async Task ClearCacheAsync(StoreMapping entity)
    {
        await RemoveByPrefixAsync(BulkOrderServicesDefaults.VisibleCategoryIdsPrefix);

        await base.ClearCacheAsync(entity);
    }
}