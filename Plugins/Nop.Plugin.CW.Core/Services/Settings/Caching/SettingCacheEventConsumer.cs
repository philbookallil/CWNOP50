using Nop.Core.Domain.Configuration;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Settings.Caching;

/// <summary>
/// CW.feature.* is derived from MultiStoreSettings rows, and nothing watched the Setting entity: only
/// StoreCacheEventConsumer cleared FeaturePrefix, and only on store insert or update. A flag changed by
/// any path other than the two CW settings actions - nop's own All settings screen, an import, a
/// migration - stayed live until the entry expired (H-4).
/// CWFeatureService is registered AddScoped, so its own ConcurrentDictionary dies with the request;
/// clearing the shared cache here is sufficient.
/// </summary>
/// <remarks>
/// The subsidy ordering results are cleared with it. The in-ordering views read the subsidy and packname
/// switches and the packname-dates setting, so a packname's subsidies, its ordering block, its allocated
/// products and the next moment its ordering status changes all change when one of them does, and without
/// this they kept their old values until they expired.
/// </remarks>
public partial class SettingCacheEventConsumer : CacheEventConsumer<Setting>
{
    protected override async Task ClearCacheAsync(Setting entity)
    {
        //Only the settings the feature set is built from, so unrelated setting writes - and nop writes
        //a great many - do not flush this on every save. The store time zone is one of them: the views read
        //"today" in it, and the next status change is an instant worked out in it.
        if (entity.Name?.StartsWith("multistoresettings.", StringComparison.InvariantCultureIgnoreCase) == true
            || string.Equals(entity.Name, "datetimesettings.defaultstoretimezoneid", StringComparison.InvariantCultureIgnoreCase))
        {
            await RemoveByPrefixAsync(CWFeatureDefaults.FeaturePrefix);

            await RemoveByPrefixAsync(SubsidyServicesDefaults.SubsidiesByPackNamePrefix);
            await RemoveByPrefixAsync(SubsidyServicesDefaults.ActiveSubsidiesByStorePrefix);
            await RemoveByPrefixAsync(SubsidyServicesDefaults.InOrderingSubsidiesByPackNamePrefix);
            await RemoveByPrefixAsync(SubsidyServicesDefaults.OrderingBlockedPackNamePrefix);
            await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameOrderingProductsPrefix);
            await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameHasActiveProductGroupsAllPrefix);
            await RemoveByPrefixAsync(SubsidyServicesDefaults.PackNameNextSubsidyStatusChangePrefix);
        }

        await base.ClearCacheAsync(entity);
    }
}