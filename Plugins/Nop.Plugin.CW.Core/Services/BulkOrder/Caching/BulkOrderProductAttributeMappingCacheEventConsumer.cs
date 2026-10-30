using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

/// <summary>
/// A mapping added, removed, reordered, made required, or given a condition changes the axes,
/// the pins and the condition set.
/// </summary>
public partial class BulkOrderProductAttributeMappingCacheEventConsumer : CacheEventConsumer<ProductAttributeMapping>
{
    protected override async Task ClearCacheAsync(ProductAttributeMapping entity)
    {
        await RemoveByPrefixAsync(string.Format(BulkOrderServicesDefaults.SkeletonByProductPrefix, entity.ProductId));

        await base.ClearCacheAsync(entity);
    }
}