using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Subsidies.Caching;

public partial class SubsidyProductGroupCacheEventConsumer : CacheEventConsumer<SubsidyProductGroup>
{
    /// <summary>
    /// entity
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(SubsidyProductGroup entity)
    {
        await RemoveAsync(SubsidyServicesDefaults.SubsidyProductGroupsCacheKey, entity.SubsidyId);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameOrderingProductsPrefix);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameHasActiveProductGroupsAllPrefix);

        await base.ClearCacheAsync(entity);
    }
}
