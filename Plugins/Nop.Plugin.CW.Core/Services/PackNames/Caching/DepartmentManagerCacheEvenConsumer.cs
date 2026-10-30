using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.PackNames.Caching;

public partial class DepartmentManagerCacheEvenConsumer : CacheEventConsumer<DepartmentManagerMapping>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    /// <remarks>
    /// <c>entity.PackNameId</c> is the MANAGER's packname id — this mapping is department → manager,
    /// so every key here is keyed on that manager and nothing else needs clearing.
    /// <para>
    /// The ownership set is the one that matters: <c>GetOwnedPackNameIdsByManagerIdAsync</c> resolves
    /// it from the manager's DEPARTMENTS as well as their lists, so a mapping that is not cleared here
    /// leaves a revoked manager able to edit and top up that department's packnames until the entry
    /// expires. Six entities feed that query — packname, list, list→manager
    /// mapping, branch, department→manager mapping and department — and each has a consumer that
    /// clears it.
    /// </para>
    /// </remarks>
    protected override async Task ClearCacheAsync(DepartmentManagerMapping entity)
    {
        await RemoveAsync(PackNameServicesDefaults.DepartmentsByManagerCacheKey, entity.PackNameId);
        await RemoveAsync(PackNameServicesDefaults.PackNameOwnedIdsByManagerCacheKey, entity.PackNameId);
        await RemoveAsync(PackNameServicesDefaults.PackNamesByManagerCacheKey, entity.PackNameId);

        await base.ClearCacheAsync(entity);
    }
}