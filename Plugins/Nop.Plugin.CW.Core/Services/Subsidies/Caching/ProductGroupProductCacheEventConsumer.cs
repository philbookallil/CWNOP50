using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Subsidies.Caching;

public partial class ProductGroupProductCacheEventConsumer : CacheEventConsumer<ProductGroupProduct>
{
    /// <summary>
    /// entity
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(ProductGroupProduct entity)
    {
        await RemoveAsync(SubsidyServicesDefaults.ProductsByProductGroupCacheKey, entity.ProductGroupId);
        await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameOrderingProductsPrefix);

        await base.ClearCacheAsync(entity);
    }
}
