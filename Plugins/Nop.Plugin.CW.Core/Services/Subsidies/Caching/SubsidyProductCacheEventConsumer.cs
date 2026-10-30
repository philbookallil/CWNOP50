using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Subsidies.Caching;

/// <summary>
/// A deleted or unpublished product stayed in its subsidy product groups: ProductGroupProduct's
/// consumer covers membership changes, but nothing covered the product itself. By prefix because a
/// Product does not know which groups contain it (H-6).
/// A second CacheEventConsumer&lt;Product&gt; alongside BulkOrderProductCacheEventConsumer is normal -
/// this solution already has three consumers each for Order and OrderItem.
/// </summary>
public partial class SubsidyProductCacheEventConsumer : CacheEventConsumer<Product>
{
    protected override async Task ClearCacheAsync(Product entity)
    {
        await RemoveByPrefixAsync(SubsidyServicesDefaults.ProductsByProductGroupPrefix);

        await base.ClearCacheAsync(entity);
    }
}