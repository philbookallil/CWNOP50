using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

/// <summary>
/// A combination added, edited or deleted changes the skeleton's index and its signature set -
/// which is what decides whether a cell is a box or a dash on a restricted product.
/// </summary>
public partial class BulkOrderProductAttributeCombinationCacheEventConsumer : CacheEventConsumer<ProductAttributeCombination>
{
    protected override async Task ClearCacheAsync(ProductAttributeCombination entity)
    {
        await RemoveByPrefixAsync(string.Format(BulkOrderServicesDefaults.SkeletonByProductPrefix, entity.ProductId));

        await base.ClearCacheAsync(entity);
    }
}