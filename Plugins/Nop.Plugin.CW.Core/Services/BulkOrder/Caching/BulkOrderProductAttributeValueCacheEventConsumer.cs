using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;
using Nop.Services.Catalog;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

/// <summary>
/// A value's name, order, colour, image or pre-selected flag is all in the skeleton.
/// </summary>
/// <remarks>
/// ProductAttributeValue carries a MAPPING id, not a product id, so the product has to be looked up - which
/// is exactly what nop's own ProductAttributeValueCacheEventConsumer does. A delete arrives after the row is
/// gone but the mapping is still there, so this resolves on the mapping and not on the value.
/// </remarks>
public partial class BulkOrderProductAttributeValueCacheEventConsumer : CacheEventConsumer<ProductAttributeValue>
{
    protected readonly IProductAttributeService _productAttributeService;

    public BulkOrderProductAttributeValueCacheEventConsumer(IProductAttributeService productAttributeService)
    {
        _productAttributeService = productAttributeService;
    }

    protected override async Task ClearCacheAsync(ProductAttributeValue entity)
    {
        var mapping = await _productAttributeService.GetProductAttributeMappingByIdAsync(entity.ProductAttributeMappingId);

        if (mapping != null)
            await RemoveByPrefixAsync(string.Format(BulkOrderServicesDefaults.SkeletonByProductPrefix, mapping.ProductId));

        await base.ClearCacheAsync(entity);
    }
}