using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.PackNames.Caching;

/// <summary>
/// A restricted packname's visible products are widened to their grouped-product relatives, and its categories and
/// manufacturers derived from them; a product's parent or deletion changes that answer without changing the
/// allocation, so a product change clears them.
/// </summary>
public partial class CatalogueProductCacheEventConsumer : CacheEventConsumer<Product>
{
    protected override async Task ClearCacheAsync(Product entity)
    {
        await RemoveByPrefixAsync(PackNameServicesDefaults.CatalogueVisiblePrefix);

        await base.ClearCacheAsync(entity);
    }
}