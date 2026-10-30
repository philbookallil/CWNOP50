using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Subsidies.Caching;

public partial class SubsidyPackNameCacheEventConsumer : CacheEventConsumer<SubsidyPackName>
{
    /// <summary>
    /// entity
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(SubsidyPackName entity)
    {
        await RemoveAsync(SubsidyServicesDefaults.SubsidyPackNamesCacheKey, entity.SubsidyId);
        await RemoveAsync(SubsidyServicesDefaults.SubsidiesByPackNameCacheKey, entity.PackNameId);
        await RemoveAsync(SubsidyServicesDefaults.InOrderingSubsidiesByPackNameCacheKey, entity.PackNameId);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.OrderingBlockedPackNamePrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameOrderingProductsPrefix);
        await RemoveAsync(SubsidyServicesDefaults.PackNameHasActiveProductGroupsCacheKey, entity.PackNameId);
        await RemoveAsync(SubsidyServicesDefaults.PackNameNextSubsidyStatusChangeCacheKey, entity.PackNameId);

        await base.ClearCacheAsync(entity);
    }
}
