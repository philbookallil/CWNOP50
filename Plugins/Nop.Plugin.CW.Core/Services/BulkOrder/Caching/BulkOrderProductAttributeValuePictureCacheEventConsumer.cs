using Nop.Core.Domain.Catalog;
using Nop.Services.Caching;
using Nop.Services.Catalog;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

/// <summary>
/// The value picture ids are resolved into the skeleton when BulkOrderShowAttributeImages is
/// on, so changing a value's picture must rebuild it.
/// </summary>
public partial class BulkOrderProductAttributeValuePictureCacheEventConsumer : CacheEventConsumer<ProductAttributeValuePicture>
{
    protected readonly IProductAttributeService _productAttributeService;

    public BulkOrderProductAttributeValuePictureCacheEventConsumer(IProductAttributeService productAttributeService)
    {
        _productAttributeService = productAttributeService;
    }

    protected override async Task ClearCacheAsync(ProductAttributeValuePicture entity)
    {
        var value = await _productAttributeService.GetProductAttributeValueByIdAsync(entity.ProductAttributeValueId);

        var mapping = value == null
            ? null
            : await _productAttributeService.GetProductAttributeMappingByIdAsync(value.ProductAttributeMappingId);

        if (mapping != null)
            await RemoveByPrefixAsync(string.Format(BulkOrderServicesDefaults.SkeletonByProductPrefix, mapping.ProductId));

        await base.ClearCacheAsync(entity);
    }
}