using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Branches.Caching;

public partial class BranchCacheEventConsumer : CacheEventConsumer<Branch>
{
    /// <summary>
    /// Clear cache by entity event type
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="entityEventType">Entity event type</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(Branch entity, EntityEventType entityEventType)
    {
        if (entityEventType == EntityEventType.Delete)
        {
            await RemoveAsync(BranchServicesDefaults.BranchAddressesCacheKey, entity);
            await RemoveByPrefixAsync(BranchServicesDefaults.BranchAddressesByBranchPrefix, entity);
        }

        await base.ClearCacheAsync(entity, entityEventType);
    }

    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(Branch entity)
    {
        await RemoveByPrefixAsync(BranchServicesDefaults.BranchesByStorePrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNamesByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNameOwnedIdsByManagerCacheKeyAllPrefix); 
        await RemoveByPrefixAsync(PackNameServicesDefaults.DepartmentsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(BranchServicesDefaults.BranchBreadcrumbPrefix);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(BranchServicesDefaults.ManagerStoreIdCacheKeyAllPrefix);
        await RemoveAsync(BranchServicesDefaults.DefaultShippingMethodsByBranchCacheKey, entity.Id);
        await RemoveByPrefixAsync(BranchServicesDefaults.BranchByCustomerIdPrefix);
    }
}
