using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Branches.Caching;

public partial class BranchAddressMappingCacheEventConsumer : CacheEventConsumer<BranchAddressMapping>
{
    /// <summary>
    /// Clear cache by entity event type
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="entityEventType">Entity event type</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(BranchAddressMapping entity, EntityEventType entityEventType)
    {
        await RemoveAsync(BranchServicesDefaults.BranchAddressesCacheKey, entity.BranchId);

        if (entityEventType == EntityEventType.Delete)
            await RemoveAsync(BranchServicesDefaults.BranchAddressCacheKey, entity.BranchId, entity.AddressId);

        await base.ClearCacheAsync(entity, entityEventType);
    }
}
