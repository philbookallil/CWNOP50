using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.PackNames.Caching;

public partial class PackNameListManagerCacheEvenConsumer : CacheEventConsumer<PackNameListManagerMapping>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    /// <remarks>
    /// <para>
    /// <c>entity.PackNameId</c> is the MANAGER's packname id — this mapping is list → manager.
    /// </para>
    /// <para>
    /// Clearing the ownership cache is a KEYED remove: the set is stored per manager under
    /// <c>PackNameOwnedIdsByManagerCacheKey</c>, a one-parameter key, and this consumer is the one
    /// place that knows WHICH manager changed. The consumers for branch, packname, packname-list and
    /// department clear every manager's entry by prefix, because those changes can alter who owns what
    /// across managers; department → manager, like list → manager, cannot.
    /// </para>
    /// <para>
    /// An earlier version of the first line here was
    /// <c>RemoveAsync(PackNameOwnedByManagerCacheKey, entity.PackNameId)</c> on a key patterned
    /// <c>"{manager}-{packname}"</c>. One argument to a two-parameter key reaches
    /// <c>CacheKey.Create</c> → <c>string.Format</c> and throws <c>FormatException</c>;
    /// <c>EventPublisher</c> swallows and logs it, so the three lines below it never ran and a
    /// manager's scope stayed cached with its old value until the static cache expired.
    /// Two rules follow, and the second matters more: clear a composite key by prefix, and do
    /// not let one key's failure cancel the others. (The first rule is why the key was reduced to one
    /// parameter rather than cleared by prefix forever.)
    /// </para>
    /// </remarks>
    protected override async Task ClearCacheAsync(PackNameListManagerMapping entity)
    {
        await RemoveAsync(PackNameServicesDefaults.PackNameOwnedIdsByManagerCacheKey, entity.PackNameId);
        await RemoveAsync(PackNameServicesDefaults.PackNamesByManagerCacheKey, entity.PackNameId);
        await RemoveAsync(MultiStoreServicesDefaults.PackNameListsByManagerCacheKey, entity.PackNameId);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKeyAllPrefix);

        await base.ClearCacheAsync(entity);
    }
}