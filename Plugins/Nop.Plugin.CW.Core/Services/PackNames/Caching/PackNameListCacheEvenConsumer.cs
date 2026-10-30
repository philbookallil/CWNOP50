using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.PackNames.Caching;

public partial class PackNameListCacheEvenConsumer : CacheEventConsumer<PackNameList>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(PackNameList entity)
    {
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNamesByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNameOwnedIdsByManagerCacheKeyAllPrefix);
        await RemoveAsync(PackNameServicesDefaults.ActivePackNamesByListIdCacheKey, entity);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNameListsByDepartmentIdsCacheKeyAllPrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.ActivePackNamesByListIdForDepartmentsCacheKeyAllPrefix);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(BranchServicesDefaults.ManagerStoreIdCacheKeyAllPrefix);

        await base.ClearCacheAsync(entity);
    }
}
