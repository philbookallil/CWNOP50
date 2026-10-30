using Nop.Core.Domain.Localization;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

/// <summary>
/// The skeleton holds LOCALIZED names, prompts and value names, and the localized text lives in
/// LocalizedProperty rather than on the entity - so editing a translation changes the skeleton without
/// touching any of the entities the other consumers watch.
/// </summary>
/// <remarks>
/// Filtered to the three key groups that reach the skeleton, because LocalizedProperty fires for every
/// localized entity in the store - categories, topics, products - and clearing every skeleton on a topic
/// translation would make this cache worthless on a translating store.
/// </remarks>
public partial class BulkOrderLocalizedPropertyCacheEventConsumer : CacheEventConsumer<LocalizedProperty>
{
    protected override async Task ClearCacheAsync(LocalizedProperty entity)
    {
        if (entity.LocaleKeyGroup is nameof(Nop.Core.Domain.Catalog.ProductAttribute)
            or nameof(Nop.Core.Domain.Catalog.ProductAttributeValue)
            or nameof(Nop.Core.Domain.Catalog.ProductAttributeMapping))
        {
            await RemoveByPrefixAsync(BulkOrderServicesDefaults.SkeletonPrefix);
        }

        await base.ClearCacheAsync(entity);
    }
}