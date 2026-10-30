using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.MultiStores.Caching;

public partial class CostCenterAddressMappingCacheEventConsumer : CacheEventConsumer<CostCenterAddressMapping>
{
    /// <summary>
    /// Clear cache by entity event type
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="entityEventType">Entity event type</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(CostCenterAddressMapping entity, EntityEventType entityEventType)
    {
        await RemoveAsync(MultiStoreServicesDefaults.CostCenterAddressesCacheKey, entity.CostCenterId);

        if (entityEventType == EntityEventType.Delete)
            await RemoveAsync(MultiStoreServicesDefaults.CostCenterAddressCacheKey, entity.CostCenterId, entity.AddressId);

        await base.ClearCacheAsync(entity, entityEventType);
    }
}
