using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Stores.Caching;

public partial class StoreCacheEventConsumer : CacheEventConsumer<Store>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="entityEventType">Entity event type</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(Store entity, EntityEventType entityEventType)
    {
        if (entityEventType == EntityEventType.Insert || entityEventType == EntityEventType.Delete)
        {
            await RemoveByPrefixAsync(CWFeatureDefaults.FeaturePrefix);
        }

        await base.ClearCacheAsync(entity, entityEventType);
    }
}
