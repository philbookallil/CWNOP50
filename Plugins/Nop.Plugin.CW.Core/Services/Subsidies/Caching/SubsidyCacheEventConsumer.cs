using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Subsidies.Caching;

public partial class SubsidyCacheEventConsumer : CacheEventConsumer<Domain.Subsidies.Subsidy>
{
    /// <summary>
    /// entity
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(Domain.Subsidies.Subsidy entity)
    {
        await RemoveAsync(SubsidyServicesDefaults.SubsidyPackNamesCacheKey, entity.Id);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.SubsidiesByPackNamePrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.ActiveSubsidiesByStorePrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.InOrderingSubsidiesByPackNamePrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.OrderingBlockedPackNamePrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameOrderingProductsPrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameHasActiveProductGroupsAllPrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameNextSubsidyStatusChangePrefix);

        await base.ClearCacheAsync(entity);
    }
}
