using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

/// <summary>
/// Renaming an attribute changes the localized name every skeleton stores for it, and an
/// attribute is shared across products - so this clears the WHOLE prefix.
/// </summary>
/// <remarks>
/// Deliberately coarse. There is no index from ProductAttribute to the products using it that does not cost
/// a query, and renaming an attribute is a rare admin action; the cost of getting it wrong is a screen
/// showing the old name until the entry expires.
/// </remarks>
public partial class BulkOrderProductAttributeCacheEventConsumer : CacheEventConsumer<ProductAttribute>
{
    protected override async Task ClearCacheAsync(ProductAttribute entity)
    {
        await RemoveByPrefixAsync(BulkOrderServicesDefaults.SkeletonPrefix);

        await base.ClearCacheAsync(entity);
    }
}