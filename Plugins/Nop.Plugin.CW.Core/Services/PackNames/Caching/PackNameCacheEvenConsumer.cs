using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.PackNames.Caching;

public partial class PackNameCacheEvenConsumer : CacheEventConsumer<PackName>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(PackName entity, EntityEventType entityEventType)
    {
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNameByCustomerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNamesByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNameOwnedIdsByManagerCacheKeyAllPrefix);
        //Prefix, not keyed: entity.PackNameListId is the NEW list on a move, so a keyed clear leaves
        //the old list's roster showing a packname that is no longer in it. And the department roster is
        //cached under list id 0 ("any list"), which a prefix formatted with a real list id can never
        //match - so that one was invalidated by nothing.
        await RemoveByPrefixAsync(PackNameServicesDefaults.ActivePackNamesByListIdCacheKeyAllPrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.ActivePackNamesByListIdForDepartmentsCacheKeyAllPrefix);
        await RemoveAsync(PackNameServicesDefaults.ActivePackNameByManagerIdCacheKey, entity);
        await RemoveAsync(PackNameServicesDefaults.DepartmentsByManagerCacheKey, entity.Id);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNameListsByDepartmentIdsCacheKeyAllPrefix);
        //PREFIX, not keyed: the key now carries the requireActiveList flag as a second parameter,
        //and a keyed remove with one argument would throw FormatException inside the
        //consumer and silently kill every clear below it.
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKeyAllPrefix);
        await RemoveAsync(MultiStoreServicesDefaults.PackNameListsByManagerCacheKey, entity);
        await RemoveAsync(BranchServicesDefaults.ManagerStoreIdCacheKey, entity);

        await RemoveByPrefixAsync(SubsidyServicesDefaults.SubsidyPackNamesPrefix);
        await RemoveAsync(SubsidyServicesDefaults.SubsidiesByPackNameCacheKey, entity.Id);
        await RemoveAsync(SubsidyServicesDefaults.InOrderingSubsidiesByPackNameCacheKey, entity.Id);
        await RemoveAsync(SubsidyServicesDefaults.OrderingBlockedPackNameCacheKey, entity.Id);
        await RemoveAsync(SubsidyServicesDefaults.PackNameOrderingProductsCacheKey, entity.Id);
        await RemoveAsync(SubsidyServicesDefaults.PackNameHasActiveProductGroupsCacheKey, entity.Id);
        await RemoveAsync(SubsidyServicesDefaults.PackNameNextSubsidyStatusChangeCacheKey, entity.Id);

        if (entityEventType == EntityEventType.Delete)
        {
            await RemoveAsync(PackNameServicesDefaults.PackNameAddressesCacheKey, entity);
        }

        await base.ClearCacheAsync(entity, entityEventType);
    }
}
