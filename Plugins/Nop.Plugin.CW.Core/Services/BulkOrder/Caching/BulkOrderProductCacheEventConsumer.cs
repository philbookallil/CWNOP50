using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

public partial class BulkOrderProductCacheEventConsumer : CacheEventConsumer<Product>
{
    protected override async Task ClearCacheAsync(Product entity)
    {
        await RemoveByPrefixAsync(BulkOrderServicesDefaults.VisibleCategoryIdsPrefix);

        //nothing in the skeleton derives from a Product column any more - the two inventory
        //flags are read from the Product per request - so this is belt and braces. The consumer already
        //fires on every Product change and the cost of a spurious clear is one rebuild.
        await RemoveByPrefixAsync(string.Format(BulkOrderServicesDefaults.SkeletonByProductPrefix, entity.Id));

        await base.ClearCacheAsync(entity);
    }
}