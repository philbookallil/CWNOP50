using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.MultiStores.Caching;

public partial class DepartmentCacheEvenConsumer : CacheEventConsumer<Department>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    /// <remarks>
    /// PREFIX clears, not keyed ones: one department is mapped to many managers, so a delete or a
    /// StoreId change invalidates every manager's department list and therefore every manager's
    /// ownership set. Soft-delete reaches here too — the repository publishes EntityDeleted after
    /// setting Deleted, and GetDepartmentsByManagerIdAsync filters !Deleted — so without this a
    /// deleted department still granted access to its packnames until the entry expired.
    /// </remarks>
    protected override async Task ClearCacheAsync(Department entity)
    {
        await RemoveAsync(ExtendedCustomerServiceDefaults.DepartmentCustomerRoleIdsCacheKey);
        await RemoveByPrefixAsync(PackNameServicesDefaults.DepartmentsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNameOwnedIdsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNamesByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNameListsByDepartmentIdsCacheKeyAllPrefix);

        await base.ClearCacheAsync(entity);
    }
}