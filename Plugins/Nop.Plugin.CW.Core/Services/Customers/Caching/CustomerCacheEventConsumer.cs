using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Customers.Caching;

public partial class CustomerCacheEventConsumer : CacheEventConsumer<Customer>
{
    #region Methods

    /// <summary>
    /// Clear cache by entity event type
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="entityEventType">Entity event type</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(Customer entity, EntityEventType entityEventType)
    {
        if (entityEventType == EntityEventType.Delete)
        {
            await RemoveByPrefixAsync(BranchServicesDefaults.BranchStaffCountPrefix);
            await RemoveAsync(BranchServicesDefaults.BranchByCustomerIdCacheKey, entity.Id);
        }

        await base.ClearCacheAsync(entity, entityEventType);
    }

    #endregion
}
