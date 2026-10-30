using Nop.Core.Caching;
using Nop.Core.Configuration;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Services.Configuration;
using Nop.Services.Helpers;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

public class SubsidyOrderingService : ISubsidyOrderingService
{
    #region Constants

    /// <summary>
    /// The shortest time an ordering result is cached when a subsidy of the packname is about to open or close
    /// </summary>
    protected const int MinimumOrderingCacheMinutes = 3;

    #endregion

    #region Fields

    protected readonly ISubsidyService _subsidyService;
    protected readonly IRepository<SubsidyPackNameCanOrder> _subsidyPackNameCanOrderRepository;
    protected readonly IRepository<SubsidyInOrderingPeriod> _subsidyInOrderingPeriodRepository;
    protected readonly IRepository<SubsidyVariationInOrderingPeriod> _subsidyVariationInOrderingPeriodRepository;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly AppSettings _appSettings;
    protected readonly IRepository<SubsidyPackName> _subsidyPackNameRepository;
    protected readonly IRepository<SubsidyProductGroup> _subsidyProductGroupRepository;
    protected readonly IRepository<ProductGroup> _productGroupRepository;
    protected readonly IRepository<ProductGroupProduct> _productGroupProductRepository;
    protected readonly IRepository<PackName> _packNameRepository;
    protected readonly ISettingService _settingService;
    protected readonly IDateTimeHelper _dateTimeHelper;

    #endregion

    #region Ctor

    public SubsidyOrderingService(ISubsidyService subsidyService,
        IRepository<SubsidyPackNameCanOrder> subsidyPackNameCanOrderRepository,
        IRepository<SubsidyInOrderingPeriod> subsidyInOrderingPeriodRepository,
        IRepository<SubsidyVariationInOrderingPeriod> subsidyVariationInOrderingPeriodRepository,
        IStaticCacheManager staticCacheManager,
        AppSettings appSettings,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        IRepository<SubsidyProductGroup> subsidyProductGroupRepository,
        IRepository<ProductGroup> productGroupRepository,
        IRepository<ProductGroupProduct> productGroupProductRepository,
        IRepository<PackName> packNameRepository,
        ISettingService settingService,
        IDateTimeHelper dateTimeHelper)
    {
        _subsidyService = subsidyService;
        _subsidyPackNameCanOrderRepository = subsidyPackNameCanOrderRepository;
        _subsidyInOrderingPeriodRepository = subsidyInOrderingPeriodRepository;
        _subsidyVariationInOrderingPeriodRepository = subsidyVariationInOrderingPeriodRepository;
        _staticCacheManager = staticCacheManager;
        _appSettings = appSettings;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _subsidyProductGroupRepository = subsidyProductGroupRepository;
        _productGroupRepository = productGroupRepository;
        _productGroupProductRepository = productGroupProductRepository;
        _packNameRepository = packNameRepository;
        _settingService = settingService;
        _dateTimeHelper = dateTimeHelper;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets subsidy packName mappings for subsidies in ordering period (or can order outside) by packName identifier
    /// </summary>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packName mappings
    /// </returns>
    public async Task<IList<SubsidyPackName>> GetInOrderingSubsidyPackNamesByPackNameIdAsync(int packNameId)
    {
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.InOrderingSubsidiesByPackNameCacheKey, packNameId);
        //Set the cache time to match the next status change for the subsidies allocated to the packname
        cacheKey.CacheTime = await GetPackNameCacheTimeInMinutesAsync(packNameId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var query = from sp in _subsidyPackNameRepository.Table
                        join ordering in _subsidyInOrderingPeriodRepository.Table on sp.SubsidyId equals ordering.Id
                        join varordering in _subsidyVariationInOrderingPeriodRepository.Table on sp.Id equals varordering.Id into gvarordering
                        from varordering in gvarordering.DefaultIfEmpty()
                        where sp.PackNameId == packNameId && !sp.Deleted && sp.Active &&
                        (
                            (varordering != null && (varordering.InOrderingPeriod || varordering.OrderOutsidePeriod)) ||
                            (varordering == null && (ordering.InOrderingPeriod || ordering.OrderOutsidePeriod))
                        )
                        select sp;

            return await query.ToListAsync();
        });
    }

    /// <summary>
    /// Gets the packname with subsidies allocated that can not order because they are not inside ordering period and can't order outside period
    /// </summary>
    /// <param name="packNameIds">The packnames to validate</param>
    /// <returns></returns>
    public virtual async Task<List<int>> GetOrderingBlockedPackNamesAsync(List<int> packNameIds)
    {
        if (packNameIds == null || packNameIds.Count == 0)
            return new List<int>();

        var canOrderMap = await _subsidyPackNameCanOrderRepository.Table
            .Where(x => packNameIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.CanOrder);

        return packNameIds.Where(id => canOrderMap.TryGetValue(id, out var canOrder) && !canOrder).ToList();
    }

    /// <summary>
    /// Gets the ordering flag information for a packName
    /// </summary>
    /// <param name="packNameId">The packname identifier</param>
    /// <returns></returns>
    public virtual async Task<SubsidyPackNameCanOrder> GetSubsidyPackNameCanOrderAsync(int packNameId)
    {
        if (packNameId == 0)
            return null;

        //Cache by individual packname so we can reuse it in other views
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.OrderingBlockedPackNameCacheKey, packNameId);
        //Set the cache time to match the next status change for the subsidies allocated to the packname
        cacheKey.CacheTime = await GetPackNameCacheTimeInMinutesAsync(packNameId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            return await _subsidyPackNameCanOrderRepository.GetByIdAsync(packNameId);
        });
    }

    /// <summary>
    /// Gets whether the packname can not order because he is not inside ordering period and can't order outside period
    /// </summary>
    /// <param name="packNameId">The packname identifier</param>
    /// <returns></returns>
    public virtual async Task<bool> PackNameCanOrderAsync(int packNameId)
    {
        if (packNameId == 0)
            return false;

        return (await GetSubsidyPackNameCanOrderAsync(packNameId))?.CanOrder ?? true;
    }

    /// <summary>
    /// Gets products from product groups for in ordering subsidies by packname
    /// </summary>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    public async Task<IList<int>> GetSubsidyProductsByPackNameIdAsync(int packNameId)
    {
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.PackNameOrderingProductsCacheKey, packNameId);
        cacheKey.CacheTime = await GetPackNameCacheTimeInMinutesAsync(packNameId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var query = from sp in _subsidyPackNameRepository.Table
                        join ordering in _subsidyInOrderingPeriodRepository.Table on sp.SubsidyId equals ordering.Id
                        join varordering in _subsidyVariationInOrderingPeriodRepository.Table on sp.Id equals varordering.Id into gvarordering
                        from varordering in gvarordering.DefaultIfEmpty()
                        join spg in _subsidyProductGroupRepository.Table on sp.SubsidyId equals spg.SubsidyId
                        join pg in _productGroupRepository.Table on spg.ProductGroupId equals pg.Id
                        join p in _productGroupProductRepository.Table on spg.ProductGroupId equals p.ProductGroupId
                        where sp.PackNameId == packNameId && !sp.Deleted && sp.Active && !spg.Deleted && !pg.Deleted && pg.Active &&
                            (
                                (varordering != null && (varordering.InOrderingPeriod || varordering.OrderOutsidePeriod)) ||
                                (varordering == null && (ordering.InOrderingPeriod || ordering.OrderOutsidePeriod))
                            )
                        select p.ProductId;

            return await query.ToListAsync();
        });
    }

    /// <summary>
    /// Returns the flat union of product IDs allocated to this PackName across all
    /// currently active subsidies (in-period or OrderOutsidePeriod), but only when ALL
    /// of those active subsidies restrict this packname to its allocated products.
    /// Returns null if no restriction applies: no active subsidies, or at least one
    /// active subsidy that does not restrict this packname.
    /// Returns an empty list if restriction applies but no product groups are assigned.
    /// </summary>
    /// <remarks>
    /// A subsidy restricts a manager only when it includes managers: the rule the SQL views apply as
    /// <c>OnlyAllocatedProducts = 1 AND (IncludeManagers = 1 OR Manager = 0)</c>.
    /// </remarks>
    public async Task<IList<int>?> GetOnlyAllocatedProductIdsAsync(int packNameId)
    {
        // GetInOrderingSubsidyPackNamesByPackNameIdAsync returns only subsidies that are
        // currently active for ordering (in period OR OrderOutsidePeriod = true).
        var activeSubsidyPackNames = await GetInOrderingSubsidyPackNamesByPackNameIdAsync(packNameId);

        if (!activeSubsidyPackNames.Any())
            return null; // no active subsidies → no restriction

        var packName = await _packNameRepository.GetByIdAsync(packNameId, cache => default);
        var isManager = packName?.Manager ?? false;

        // Load the Subsidy entities to read the OnlyAllocatedProducts and IncludeManagers flags
        var subsidyIds = activeSubsidyPackNames.Select(sp => sp.SubsidyId).Distinct().ToList();
        var subsidies = await _subsidyService.GetSubsidiesByIdsAsync(subsidyIds);

        // If any active subsidy does not restrict this packname, the PackName is unrestricted
        if (subsidies.Any(s => !s.OnlyAllocatedProducts || (isManager && !s.IncludeManagers)))
            return null;

        // All active subsidies restrict — return allocated product IDs (already smart-cached)
        return await GetSubsidyProductsByPackNameIdAsync(packNameId);
    }

    /// <summary>
    /// Gets whether a packName has at least one product group in his active subsidies (active means in ordering period or can order outside period)
    /// </summary>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    public async Task<bool> PackNameHasActiveProductGroupsAsync(int packNameId)
    {
        if (packNameId == 0)
            return false;

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.PackNameHasActiveProductGroupsCacheKey, packNameId);
        //Set the cache time to match the next status change for the subsidies allocated to the packname
        cacheKey.CacheTime = await GetPackNameCacheTimeInMinutesAsync(packNameId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var query = from sp in _subsidyPackNameRepository.Table
                        join ordering in _subsidyInOrderingPeriodRepository.Table on sp.SubsidyId equals ordering.Id
                        join varordering in _subsidyVariationInOrderingPeriodRepository.Table on sp.Id equals varordering.Id into gvarordering
                        from varordering in gvarordering.DefaultIfEmpty()
                        join spg in _subsidyProductGroupRepository.Table on sp.SubsidyId equals spg.SubsidyId
                        where sp.PackNameId == packNameId && !sp.Deleted && sp.Active && !spg.Deleted &&
                        (
                            (varordering != null && (varordering.InOrderingPeriod || varordering.OrderOutsidePeriod)) ||
                            (varordering == null && (ordering.InOrderingPeriod || ordering.OrderOutsidePeriod))
                        )
                        select sp;

            return await query.AnyAsync();
        });
    }

    /// <summary>
    /// Gets the next moment any subsidy allocated to a packname changes its ordering status (its period
    /// opens or closes), as a UTC instant. Null when none will change again.
    /// </summary>
    /// <remarks>
    /// The dates are the packname's EFFECTIVE dates: a variation's only where the store lets packnames own
    /// their dates, as in the views whose results are cached until this moment. The day is the store's, as
    /// the views' LocalToday is.
    /// </remarks>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the UTC instant of the next change</returns>
    public async Task<DateTime?> GetPackNameNextSubsidyStatusChangeAsync(int packNameId)
    {
        var subsidyPackNames = await _subsidyService.GetAllSubsidyPackNamesByPackNameIdAsync(packNameId);

        if (subsidyPackNames.Count == 0)
            return null;

        var subsidyIds = subsidyPackNames.Select(sp => sp.SubsidyId).Distinct().ToList();
        var subsidies = (await _subsidyService.GetSubsidiesByIdsAsync(subsidyIds))
            .ToDictionary(s => s.Id);

        var subsidyPackNameIds = subsidyPackNames.Select(sp => sp.Id).ToList();
        var variations = (await _subsidyService.GetSubsidyVariationsBySubsidyPackNameIdsAsync(subsidyPackNameIds))
            .ToDictionary(v => v.SubsidyPackNameId);

        DateTime? nextChange = null;

        foreach (var subsidyPackName in subsidyPackNames)
        {
            if (!subsidies.TryGetValue(subsidyPackName.SubsidyId, out var subsidy))
                continue;

            //The subsidy's own store's zone, as the views read it: the answer is cached for every store and for
            //admin requests, so the zone of the request that works it out would be the wrong one.
            var storeTimeZone = await GetStoreTimeZoneAsync(subsidy.StoreId);
            var today = SubsidyPeriodHelper.ToStoreLocal(DateTime.UtcNow, storeTimeZone).Date;

            var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(subsidy.StoreId);
            variations.TryGetValue(subsidyPackName.Id, out var variation);

            var durationStart = SubsidyPeriodHelper.EffectiveDurationStart(subsidy.DurationStart, variation?.DurationStart, storeSettings.PackNamesOwnStartAndFinishDates);
            var durationEnd = SubsidyPeriodHelper.EffectiveDurationEnd(subsidy.DurationEnd, variation?.DurationEnd, storeSettings.PackNamesOwnStartAndFinishDates);

            var change = SubsidyPeriodHelper.NextOrderingStatusChange(durationStart, durationEnd, subsidy.AnnualRecurrence, today);

            if (!change.HasValue)
                continue;

            var changeUtc = SubsidyPeriodHelper.ToUtc(change.Value, storeTimeZone);

            if (!nextChange.HasValue || changeUtc < nextChange.Value)
                nextChange = changeUtc;
        }

        return nextChange;
    }

    /// <summary>
    /// Gets the time zone of a store: its own default store time zone setting, else the one nop uses for the
    /// current request
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the time zone</returns>
    protected virtual async Task<TimeZoneInfo> GetStoreTimeZoneAsync(int storeId)
    {
        var dateTimeSettings = await _settingService.LoadSettingAsync<DateTimeSettings>(storeId);

        if (!string.IsNullOrEmpty(dateTimeSettings.DefaultStoreTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(dateTimeSettings.DefaultStoreTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                //an id this server does not know: nop's own fallback below
            }
            catch (InvalidTimeZoneException)
            {
                //the same
            }
        }

        return _dateTimeHelper.DefaultStoreTimeZone;
    }

    /// <summary>
    /// Gets the total minutes between now and the next moment the ordering status changes for the packname
    /// (based on its subsidies), within the minimum and the default cache time.
    /// </summary>
    /// <remarks>
    /// Every cached ordering read asks this first, so the moment itself is cached: it changes only when one of
    /// the packname's subsidies, assignments or variations changes, or the dates setting does, and their cache
    /// consumers clear it. Once the moment has passed it is worked out again.
    /// </remarks>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the minutes</returns>
    public async Task<int> GetPackNameCacheTimeInMinutesAsync(int packNameId)
    {
        var defaultCacheTime = _appSettings.Get<CacheConfig>().DefaultCacheTime;
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.PackNameNextSubsidyStatusChangeCacheKey, packNameId);

        //Ticks, with MaxValue for "never changes again": the static cache does not keep a null
        async Task<long> getNextChangeTicksAsync() =>
            (await GetPackNameNextSubsidyStatusChangeAsync(packNameId))?.Ticks ?? DateTime.MaxValue.Ticks;

        var nextChangeTicks = await _staticCacheManager.GetAsync(cacheKey, getNextChangeTicksAsync);

        if (nextChangeTicks <= DateTime.UtcNow.Ticks)
        {
            await _staticCacheManager.RemoveAsync(SubsidyServicesDefaults.PackNameNextSubsidyStatusChangeCacheKey, packNameId);
            nextChangeTicks = await _staticCacheManager.GetAsync(cacheKey, getNextChangeTicksAsync);
        }

        //It is not going to change (or the packname has no subsidies), so the default cache term applies
        if (nextChangeTicks == DateTime.MaxValue.Ticks)
            return defaultCacheTime;

        var diffTime = new DateTime(nextChangeTicks, DateTimeKind.Utc) - DateTime.UtcNow;

        //The minutes until then, kept between the minimum and the default cache time
        return Math.Min(Math.Max((int)Math.Floor(diffTime.TotalMinutes), MinimumOrderingCacheMinutes), defaultCacheTime);
    }

    #endregion
}