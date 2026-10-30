using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Subsidies.Caching;

public partial class SubsidyVariationCacheEventConsumer : CacheEventConsumer<SubsidyVariation>
{
    /// <summary>
    /// entity
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(SubsidyVariation entity)
    {
        await RemoveByPrefixAsync(SubsidyServicesDefaults.InOrderingSubsidiesByPackNamePrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameHasActiveProductGroupsAllPrefix);
        //Same as ProductGroupCacheEventConsumer: the ordering product list depends on the variation's
        //date window, so editing the dates must clear it (H-2).
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameOrderingProductsPrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.OrderingBlockedPackNamePrefix);
        await RemoveAsync(SubsidyServicesDefaults.SubsidyVariationBySubsidyPackNameCacheKey, entity.SubsidyPackNameId);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameNextSubsidyStatusChangePrefix);

        await base.ClearCacheAsync(entity);
    }
}
