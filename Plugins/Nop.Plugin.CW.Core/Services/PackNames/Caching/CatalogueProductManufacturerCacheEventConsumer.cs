using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.PackNames.Caching;

/// <summary>
/// The categories and manufacturers a restricted packname may see are derived from its visible products through
/// a product's manufacturers; a change here changes the answer without changing the products, so it clears them.
/// </summary>
public partial class CatalogueProductManufacturerCacheEventConsumer : CacheEventConsumer<ProductManufacturer>
{
    protected override async Task ClearCacheAsync(ProductManufacturer entity)
    {
        await RemoveByPrefixAsync(PackNameServicesDefaults.CatalogueVisiblePrefix);

        await base.ClearCacheAsync(entity);
    }
}