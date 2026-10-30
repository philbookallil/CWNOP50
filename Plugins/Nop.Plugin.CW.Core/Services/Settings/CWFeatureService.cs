using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Stores;
using System.Collections.Concurrent;

namespace Nop.Plugin.CW.Core.Services.Settings;

public class CWFeatureService : ICWFeatureService
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IStoreService _storeService;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly ILogger _logger;

    private readonly ConcurrentDictionary<int, CWFeatureSet> _cache = new();

    #endregion

    #region Ctor

    public CWFeatureService(ISettingService settingService,
        IStoreContext storeContext,
        IStoreService storeService,
        IStaticCacheManager staticCacheManager,
        ILogger logger)
    {
        _settingService = settingService;
        _storeContext = storeContext;
        _storeService = storeService;
        _staticCacheManager = staticCacheManager;
        _logger = logger;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Reads one MultiStoreSettings flag for a store without materialising the whole settings object.
    /// LoadSettingAsync walks every property on the class and scans the settings list per property, so
    /// calling it inside a per-store loop costs orders of magnitude more than the four values needed here.
    /// </summary>
    protected virtual async Task<bool> GetFlagForStoreAsync(string settingKey, int storeId)
        => await _settingService.GetSettingByKeyAsync(settingKey, defaultValue: false, storeId: storeId, loadSharedValueIfNotFound: true);

    #endregion

    #region Methods

    public async Task<CWFeatureSet> GetFeaturesAsync(int? storeId = null)
    {
        var id = storeId ?? (await _storeContext.GetCurrentStoreAsync()).Id;

        if (_cache.TryGetValue(id, out var cached))
            return cached;

        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(id);
        var set = CWFeatureSet.From(settings);
        _cache[id] = set;

        return set;
    }

    public async Task<bool> IsEnabledAsync(CWFeature feature, int? storeId = null)
        => (await GetFeaturesAsync(storeId))[feature];

    public async Task<bool> IsEnabledForOrderAsync(CWFeature feature, Order order)
    {
        if (order is null)
        {
            //fail closed, but a null order here is a caller bug - surface it rather than
            //silently blocking in-flight work
            await _logger.WarningAsync($"{nameof(CWFeatureService)}: {nameof(IsEnabledForOrderAsync)} called with a null order for feature '{feature}'.");
            return false;
        }

        return await IsEnabledAsync(feature, order.StoreId);
    }

    public async Task<CWFeatureSet> GetFeaturesForAnyStoreAsync()
    {
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(CWFeatureDefaults.FeaturesForAnyStoreCacheKey);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            //resolve the setting keys once, from the properties themselves, so a rename cannot
            //silently break this the way a hardcoded key string would
            var probe = new MultiStoreSettings();
            var packNameKey = _settingService.GetSettingKey(probe, x => x.PackNameEnable);
            var subsidyKey = _settingService.GetSettingKey(probe, x => x.SubsidyEnable);
            var exchangesKey = _settingService.GetSettingKey(probe, x => x.ExchangesEnable);
            var creditNotesKey = _settingService.GetSettingKey(probe, x => x.CreditNotesEnable);
            var bulkOrderKey = _settingService.GetSettingKey(probe, x => x.BulkOrderEnable);

            var result = new CWFeatureSet(false, false, false, false, false);

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                var storeSettings = new MultiStoreSettings
                {
                    PackNameEnable = await GetFlagForStoreAsync(packNameKey, store.Id),
                    SubsidyEnable = await GetFlagForStoreAsync(subsidyKey, store.Id),
                    ExchangesEnable = await GetFlagForStoreAsync(exchangesKey, store.Id),
                    CreditNotesEnable = await GetFlagForStoreAsync(creditNotesKey, store.Id),
                    BulkOrderEnable = await GetFlagForStoreAsync(bulkOrderKey, store.Id)
                };

                //feature implications live only in CWFeatureSet.From - never re-derive them here
                var features = CWFeatureSet.From(storeSettings);

                result = new CWFeatureSet(
                    result.PackName || features.PackName,
                    result.Subsidy || features.Subsidy,
                    result.Exchanges || features.Exchanges,
                    result.CreditNotes || features.CreditNotes,
                    result.BulkOrder || features.BulkOrder);

                //every feature is already enabled somewhere - the remaining stores cannot change the answer
                if (result.PackName && result.Subsidy && result.Exchanges && result.CreditNotes && result.BulkOrder)
                    break;
            }

            return result;
        });
    }

    public async Task<bool> IsEnabledForAnyStoreAsync(CWFeature feature)
        => (await GetFeaturesForAnyStoreAsync())[feature];

    public void ResetRequestCache()
    {
        _cache.Clear();
    }

    #endregion
}
