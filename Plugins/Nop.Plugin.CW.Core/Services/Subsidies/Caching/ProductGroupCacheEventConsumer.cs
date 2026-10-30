using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Subsidies.Caching;

public partial class ProductGroupCacheEventConsumer : CacheEventConsumer<ProductGroup>
{
    /// <summary>
    /// entity
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(ProductGroup entity)
    {
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameHasActiveProductGroupsAllPrefix);
        //The ordering product list is filtered by the group's Active flag, so unticking Active must
        //clear it. ProductGroupProductCacheEventConsumer clears this on a membership change; editing
        //the GROUP itself did not, so a deactivated group kept offering its products (H-2).
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameOrderingProductsPrefix);

        await base.ClearCacheAsync(entity);
    }
}
