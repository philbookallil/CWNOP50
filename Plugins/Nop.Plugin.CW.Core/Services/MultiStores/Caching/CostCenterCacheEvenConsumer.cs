using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.MultiStores.Caching;

public partial class CostCenterCacheEvenConsumer : CacheEventConsumer<CostCenter>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(CostCenter entity, EntityEventType entityEventType)
    {
        //THE DROPDOWN CLEAR IS NOT DELETE-ONLY. CostCenterSelectItemsByStoreCacheKey feeds the
        //cost-centre selector; inside the Delete branch, a newly inserted or renamed cost centre never
        //reached it. Cleared by prefix because the entity's StoreId may itself have just changed, in
        //which case RemoveAsync(key, entity.StoreId) evicts the NEW store's entry and leaves the old
        //store still listing it.
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.CostCenterSelectItemsByStorePrefix);

        if (entityEventType == EntityEventType.Delete)
        {
            //Addresses belong to the cost centre and do not change when its own fields do, so this
            //one stays delete-only.
            await RemoveAsync(MultiStoreServicesDefaults.CostCenterAddressesCacheKey, entity);
        }

        await base.ClearCacheAsync(entity, entityEventType);
    }
}
