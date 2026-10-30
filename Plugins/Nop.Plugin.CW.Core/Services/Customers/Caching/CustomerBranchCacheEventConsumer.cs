using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Customers.Caching;

public partial class CustomerBranchCacheEventConsumer : CacheEventConsumer<CustomerBranch>
{
    #region Methods

    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(CustomerBranch entity)
    {
        await RemoveByPrefixAsync(BranchServicesDefaults.BranchStaffCountPrefix);
        await RemoveAsync(BranchServicesDefaults.BranchByCustomerIdCacheKey, entity.Id);

        await base.ClearCacheAsync(entity);
    }

    #endregion
}
