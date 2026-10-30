using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.PackNames.Caching;

public partial class PackNameAddressMappingCacheEventConsumer : CacheEventConsumer<PackNameAddressMapping>
{
    /// <summary>
    /// Clear cache by entity event type
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="entityEventType">Entity event type</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(PackNameAddressMapping entity, EntityEventType entityEventType)
    {
        await RemoveAsync(PackNameServicesDefaults.PackNameAddressesCacheKey, entity.PackNameId);

        if (entityEventType == EntityEventType.Delete)
            await RemoveAsync(PackNameServicesDefaults.PackNameAddressCacheKey, entity.PackNameId, entity.AddressId);

        await base.ClearCacheAsync(entity, entityEventType);
    }
}
