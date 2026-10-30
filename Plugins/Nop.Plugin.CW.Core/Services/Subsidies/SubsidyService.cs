using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Plugins;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

public partial class SubsidyService : ISubsidyService
{
    #region Fields

    protected readonly IRepository<Subsidy> _subsidyRepository;
    protected readonly IRepository<PackName> _packNameRepository;
    protected readonly IRepository<SubsidyPackName> _subsidyPackNameRepository;
    protected readonly IRepository<PackNameList> _packNameListRepository;
    protected readonly IRepository<Branch> _branchRepository;
    protected readonly IRepository<ProductGroup> _productGroupRepository;
    protected readonly IRepository<ProductGroupProduct> _productGroupProductRepository;
    protected readonly IRepository<SubsidyProductGroup> _subsidyProductGroupRepository;
    protected readonly IRepository<Product> _productRepository;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IRepository<SubsidyTopup> _subsidyTopupRepository;
    protected readonly IRepository<ProductGroupTopup> _productGroupTopupRepository;
    protected readonly INopFileProvider _fileProvider;
    protected readonly IRepository<SubsidyTopupHistory> _subsidyTopupHistoryRepository;
    protected readonly IRepository<ProductGroupTopupHistory> _productGroupTopupHistoryRepository;
    protected readonly IRepository<SubsidyVariation> _subsidyVariationRepository;
    protected readonly IRepository<SubsidyVariationHistory> _subsidyVariationHistoryRepository;
    protected readonly IRepository<Department> _departmentRepository;
    protected readonly IRepository<CostCenter> _costCenterRepository;
    protected readonly IRepository<Customer> _customerRepository;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ISettingService _settingService;

    #endregion

    #region Ctor

    public SubsidyService(IRepository<Subsidy> subsidyRepository,
        IRepository<PackName> packNameRepository,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        IRepository<PackNameList> packNameListRepository,
        IRepository<Branch> branchRepository,
        IRepository<ProductGroup> productGroupRepository,
        IRepository<ProductGroupProduct> productGroupProductRepository,
        IRepository<Product> productRepository,
        IStaticCacheManager staticCacheManager,
        IRepository<SubsidyProductGroup> subsidyProductGroupRepository,
        IRepository<SubsidyTopup> subsidyTopupRepository,
        IRepository<ProductGroupTopup> productGroupTopupRepository,
        INopFileProvider fileProvider,
        IRepository<SubsidyTopupHistory> subsidyTopupHistoryRepository,
        IRepository<ProductGroupTopupHistory> productGroupTopupHistoryRepository,
        IRepository<SubsidyVariation> subsidyVariationRepository,
        IRepository<SubsidyVariationHistory> subsidyVariationHistoryRepository,
        IRepository<Department> departmentRepository,
        IRepository<CostCenter> costCenterRepository,
        IRepository<Customer> customerRepository,
        IDateTimeHelper dateTimeHelper,
        ISettingService settingService)
    {
        _subsidyRepository = subsidyRepository;
        _packNameRepository = packNameRepository;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _packNameListRepository = packNameListRepository;
        _branchRepository = branchRepository;
        _productGroupRepository = productGroupRepository;
        _productGroupProductRepository = productGroupProductRepository;
        _productRepository = productRepository;
        _staticCacheManager = staticCacheManager;
        _subsidyProductGroupRepository = subsidyProductGroupRepository;
        _subsidyTopupRepository = subsidyTopupRepository;
        _productGroupTopupRepository = productGroupTopupRepository;
        _fileProvider = fileProvider;
        _subsidyTopupHistoryRepository = subsidyTopupHistoryRepository;
        _productGroupTopupHistoryRepository = productGroupTopupHistoryRepository;
        _subsidyVariationRepository = subsidyVariationRepository;
        _subsidyVariationHistoryRepository = subsidyVariationHistoryRepository;
        _departmentRepository = departmentRepository;
        _costCenterRepository = costCenterRepository;
        _customerRepository = customerRepository;
        _dateTimeHelper = dateTimeHelper;
        _settingService = settingService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Returns the exclusive lower bound and inclusive upper bound for CreatedOnUtc values that belong to
    /// the ordering period current for this assignment. Either bound may be null, which means unbounded
    /// on that end.
    /// </summary>
    /// <remarks>
    /// The arithmetic lives in <see cref="SubsidyPeriodHelper"/> so it is unit-tested and so it stays the
    /// same rule as <c>[dbo].[subsidy_topup_period_end]</c>. Two things were wrong before: the
    /// bound for a non-annual period was MIDNIGHT on the closing day, which excluded that day's own topups,
    /// and the whole calculation was in UTC when a period is the STORE'S local time.
    ///
    /// The end is the assignment's EFFECTIVE end - the variation's where the store lets a packname own
    /// its dates, else the subsidy's - which is what the credit views bucket the same rows on. The store
    /// setting is read for the SUBSIDY'S store, and the variation is the cached per-assignment read.
    ///
    /// The zone comes from nop's <c>DateTimeSettings</c> for the CURRENT store, which is the same setting
    /// <c>vw_CORE_SUB_StoreSubsidyVariationSetting</c> reads per store - so the two agree for any work done
    /// inside one store. They can differ only for a cross-store admin action, and only if a store is ever
    /// configured in a different zone from the instance default. The SQL falls back to UTC where nop's C#
    /// falls back to the web server's zone; that gap is reachable only on an install with no store time
    /// zone configured at all, which <c>Subsidy_P1_Precheck.sql</c> check 6 detects.
    ///
    /// <b>An absent bound is null, and every caller must omit the comparison rather than substitute a
    /// sentinel.</b> The topup <c>CreatedOnUtc</c> columns were <c>datetimeoffset</c> until the
    /// CWTimestampsDateTime2 migration, and on that type a bound was coerced per parameter and
    /// <see cref="DateTime.MinValue"/> threw outright on a server ahead of UTC. They are <c>datetime2</c>
    /// holding UTC now; the rule stays, because a sentinel would still be a real date to the query.
    /// </remarks>
    protected async Task<(DateTime? windowStart, DateTime? windowEnd)> GetTopupSearchWindowAsync(DateTime now, int subsidyPackNameId, Subsidy subsidy)
    {
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(subsidy.StoreId);

        var variation = storeSettings.PackNamesOwnStartAndFinishDates
            ? await GetSubsidyVariationBySubsidyPacknameIdAsync(subsidyPackNameId)
            : null;

        var durationEnd = SubsidyPeriodHelper.EffectiveDurationEnd(subsidy.DurationEnd, variation?.DurationEnd, storeSettings.PackNamesOwnStartAndFinishDates);

        return SubsidyPeriodHelper.GetTopupSearchWindow(now, subsidy.AnnualRecurrence, durationEnd, _dateTimeHelper.DefaultStoreTimeZone);
    }

    #endregion

    #region Methods

    #region Subsidy

    /// <summary>
    /// Gets a subsidy by indentifier
    /// </summary>
    /// <param name="subsidyId">Subsidy indentifier</param>
    /// <returns>Subsidy</returns>
    public async Task<Subsidy> GetSubsidyByIdAsync(int subsidyId)
    {
        return await _subsidyRepository.GetByIdAsync(subsidyId, cache => default);
    }

    /// <summary>
    /// Gets a subsidy by subsidy packName mapping identifier
    /// </summary>
    /// <param name="subsidyPackNameId"></param>
    /// <returns></returns>
    public async Task<Subsidy> GetSubsidyBySubsidyPackNameIdAsync(int subsidyPackNameId)
    {
        var query = from s in _subsidyRepository.Table
                    join sp in _subsidyPackNameRepository.Table on s.Id equals sp.SubsidyId
                    where sp.Id == subsidyPackNameId && !s.Deleted && !sp.Deleted
                    select s;

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets subsidies by a list of identifiers
    /// </summary>
    /// <param name="subsidyIds"></param>
    /// <returns></returns>
    public async Task<IList<Subsidy>> GetSubsidiesByIdsAsync(IList<int> subsidyIds)
    {
        return await _subsidyRepository.Table
            .Where(s => subsidyIds.Contains(s.Id))
            .ToListAsync();
    }

    /// <summary>
    /// Gets subsidy store mapping collection
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="name">Subsidy name</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidies store mapping collection
    /// </returns>
    public virtual async Task<IPagedList<SubsidyDTO>> GetSubsidiesByStoreIdAsync(int storeId, string? name = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        if (storeId == 0)
            return new PagedList<SubsidyDTO>(new List<SubsidyDTO>(), pageIndex, pageSize);

        var packNamesQuery = from sp in _subsidyPackNameRepository.Table
                             join pn in _packNameRepository.Table on sp.PackNameId equals pn.Id
                             where !pn.Deleted && !sp.Deleted
                             select sp;

        var query = from s in _subsidyRepository.Table
                    join sp in packNamesQuery on s.Id equals sp.SubsidyId into gap
                    where s.StoreId == storeId && !s.Deleted && (string.IsNullOrEmpty(name) || s.Name.Contains(name))
                    orderby s.Name
                    select new SubsidyDTO
                    {
                        Id = s.Id,
                        Name = s.Name,
                        StoreId = s.StoreId,
                        Credit = s.Credit,
                        DurationStart = s.DurationStart,
                        DurationEnd = s.DurationEnd,
                        Active = s.Active,
                        PackNameCount = gap.Count(),
                    };

        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets active subsidies by store
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidies store mapping collection
    /// </returns>
    public virtual async Task<IList<Subsidy>> GetActiveSubsidiesByStoreIdAsync(int storeId)
    {
        if (storeId == 0)
            return new List<Subsidy>();

        var key = _staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.ActiveSubsidiesByStoreCacheKey, storeId);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            var query = from s in _subsidyRepository.Table
                        where s.StoreId == storeId && !s.Deleted && s.Active
                        orderby s.Name
                        select s;

            return await query.ToListAsync();
        });
    }

    /// <summary>
    /// Asynchronously saves the specified subsidy to the data store, creating a new record or updating an existing
    /// one as appropriate.
    /// </summary>
    /// <param name="subsidy">The subsidy entity to save. Cannot be null. If the subsidy has an ID of 0, a new record is created;
    /// otherwise, the existing record is updated.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SaveSubsidyAsync(Subsidy subsidy)
    {
        ArgumentNullException.ThrowIfNull(subsidy);

        if (!subsidy.OnlyAllocatedProducts || subsidy.OrderOutsidePeriod || subsidy.Credit > 0)
            subsidy.RestrictToAllocatedQty = false;

        if (!subsidy.OnlyAllocatedProducts)
            subsidy.IncludeManagers = false;

        if (subsidy.Id == 0)
        {
            subsidy.CreatedOnUtc = DateTime.UtcNow;
            await InsertSubsidyAsync(subsidy);
        }
        else
            await UpdateSubsidyAsync(subsidy);
    }

    /// <summary>
    /// Inserts a subsidy
    /// </summary>
    /// <param name="Subsidy">subsidy</param>
    public async Task InsertSubsidyAsync(Subsidy subsidy)
    {
        ArgumentNullException.ThrowIfNull(subsidy);

        await _subsidyRepository.InsertAsync(subsidy);
    }

    /// <summary>
    /// Updates a subsidy
    /// </summary>
    /// <param name="subsidy">Subsidy</param>
    public async Task UpdateSubsidyAsync(Subsidy subsidy)
    {
        ArgumentNullException.ThrowIfNull(subsidy);

        await _subsidyRepository.UpdateAsync(subsidy);
    }

    /// <summary>
    /// Deletes a subsidy
    /// </summary>
    /// <param name="subsidy">Subsidy</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DeleteSubsidyAsync(Subsidy subsidy)
    {
        await _subsidyRepository.DeleteAsync(subsidy);
    }

    #endregion

    #region Subsidy PackName Mapping

    /// <summary>
    /// Gets subsidy packNames by subsidy identifier
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<IList<SubsidyPackName>> GetSubsidyPackNamesBySubsidyIdAsync(int subsidyId)
    {
        var query = from sp in _subsidyPackNameRepository.Table
                    join pn in _packNameRepository.Table on sp.PackNameId equals pn.Id
                    where !pn.Deleted && sp.SubsidyId == subsidyId && !sp.Deleted
                    select sp;

        var subsidyPackNames = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.SubsidyPackNamesCacheKey, subsidyId), async () => await query.ToListAsync());

        return subsidyPackNames;
    }

    /// <summary>
    /// Gets subsidy packNames by subsidy identifier
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<IList<SubsidyPackName>> GetAllSubsidyPackNamesBySubsidyIdAsync(int subsidyId)
    {
        var query = from sp in _subsidyPackNameRepository.Table
                    where sp.SubsidyId == subsidyId && !sp.Deleted
                    select sp;

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets subsidy packName mappings by packName identifier
    /// </summary>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packName mappings
    /// </returns>
    public async Task<IList<SubsidyPackName>> GetAllSubsidyPackNamesByPackNameIdAsync(int packNameId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.SubsidiesByPackNameCacheKey, packNameId);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            var query = from sp in _subsidyPackNameRepository.Table
                        where sp.PackNameId == packNameId && !sp.Deleted
                        select sp;

            return await query.ToListAsync();
        });
    }


    /// <summary>
    /// Gets packNames
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<IPagedList<SubsidyPackNameDTO>> GetPackNamesBySubsidyIdAsync(int subsidyId,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var subsidyVariationsQuery = from sv in _subsidyVariationRepository.Table
                                     where !sv.Deleted
                                     select sv;

        var query = from sp in _subsidyPackNameRepository.Table
                    join pn in _packNameRepository.Table on sp.PackNameId equals pn.Id
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    join b in _branchRepository.Table on pnl.BranchId equals b.Id into gb
                    from b in gb.DefaultIfEmpty()
                    join dep in _departmentRepository.Table on pn.DepartmentId equals dep.Id into gdep
                    from dep in gdep.DefaultIfEmpty()
                    join cos in _costCenterRepository.Table on pn.CostCenterId equals cos.Id into gcos
                    from cos in gcos.DefaultIfEmpty()
                    join sv in subsidyVariationsQuery on sp.Id equals sv.SubsidyPackNameId into gsv
                    from sv in gsv.DefaultIfEmpty()
                    where !pn.Deleted && !pnl.Deleted && sp.SubsidyId == subsidyId && !sp.Deleted
                    orderby pn.Name
                    select new SubsidyPackNameDTO
                    {
                        Id = sp.Id,
                        Active = sp.Active && pn.Active,
                        SubsidyId = sp.SubsidyId,
                        PackNameId = pn.Id,
                        PackName = pn.Name,
                        PrintName = pn.PrintName,
                        EmployeeNumber = pn.EmployeeNumber,
                        POEntry = pn.POEntry,
                        Branch = b != null ? b.Name : string.Empty,
                        Department = dep != null ? dep.Name : string.Empty,
                        CostCenter = cos != null ? cos.Name : string.Empty,
                        Gender = pn.Gender,
                        IsCustomer = pn.CustomerId > 0,
                        VariationCredit = sv != null ? sv.Credit : null,
                        VariationDurationStart = sv != null ? sv.DurationStart : null,
                        VariationDurationEnd = sv != null ? sv.DurationEnd : null,
                    };

        //paging
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets packNames
    /// </summary>
    /// <param name="storeId"></param>
    /// <param name="branchName"></param>
    /// <param name="packName"></param>
    /// <param name="email"></param>
    /// <param name="packNameType"></param>
    /// <param name="gender"></param>
    /// <param name="withoutSubsidy"></param>
    /// <param name="departmentId"></param>
    /// <param name="costCenterId"></param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<IPagedList<PackNameDTO>> GetPackNamesAsync(int storeId, string branchName, string packName, string email, string packNameType, string gender, bool withoutSubsidy, int departmentId, int costCenterId,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = from pn in _packNameRepository.Table
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    join b in _branchRepository.Table on pnl.BranchId equals b.Id into gb
                    from b in gb.DefaultIfEmpty()
                    join c in _customerRepository.Table on pn.CustomerId equals c.Id into gc
                    from c in gc.DefaultIfEmpty()
                    join dep in _departmentRepository.Table on pn.DepartmentId equals dep.Id into gdep
                    from dep in gdep.DefaultIfEmpty()
                    join cos in _costCenterRepository.Table on pn.CostCenterId equals cos.Id into gcos
                    from cos in gcos.DefaultIfEmpty()
                    where !pnl.Deleted && !pn.Deleted && pnl.StoreId == storeId &&
                           (string.IsNullOrEmpty(branchName) || b != null && b.Name.Contains(branchName))
                    select new PackNameDTO
                    {
                        Id = pn.Id,
                        CustomerId = pn.CustomerId,
                        Name = pn.Name,
                        PrintName = pn.PrintName ?? string.Empty,
                        EmployeeNumber = pn.EmployeeNumber ?? string.Empty,
                        Gender = pn.Gender,
                        Manager = pn.Manager,
                        Active = pn.Active,
                        Branch = b != null ? b.Name : string.Empty,
                        Email = c != null ? c.Email : string.Empty,
                        CustomerName = c != null ? c.FirstName + " " + c.LastName : string.Empty,
                        DepartmentId = pn.DepartmentId,
                        Department = dep != null ? dep.Name : string.Empty,
                        CostCenterId = pn.CostCenterId,
                        CostCenter = cos != null ? cos.Name : string.Empty,
                    };

        if (withoutSubsidy)
        {
            var mappingsQuery = from s in _subsidyRepository.Table
                                join sp in _subsidyPackNameRepository.Table on s.Id equals sp.SubsidyId
                                where !s.Deleted && !sp.Deleted
                                select sp;

            query = from pn in query
                    join sp in mappingsQuery on pn.Id equals sp.PackNameId into gsp
                    from sp in gsp.DefaultIfEmpty()
                    where sp == null
                    select pn;
        }

        if (!string.IsNullOrEmpty(packName) || !string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(packNameType) || !string.IsNullOrEmpty(gender) || departmentId > 0 || costCenterId > 0)
        {
            query = from pn in query
                    where (string.IsNullOrEmpty(packName) ||
                            (pn.Name ?? string.Empty).Contains(packName) || (pn.PrintName ?? string.Empty).Contains(packName) || (pn.CustomerName ?? string.Empty).Contains(packName) || (pn.EmployeeNumber ?? string.Empty).Contains(packName))
                            &&
                            (string.IsNullOrEmpty(email) || (pn.Email ?? string.Empty).Contains(email))
                            &&
                            (packNameType != MultiStoreServicesDefaults.PackNameTypeCustomer || pn.CustomerId.HasValue)
                            &&
                            (packNameType != MultiStoreServicesDefaults.PackNameTypePackname || !pn.CustomerId.HasValue)
                            &&
                            (string.IsNullOrEmpty(gender) || pn.Gender == gender)
                            &&
                            (departmentId == 0 || pn.DepartmentId == departmentId)
                            &&
                            (costCenterId == 0 || pn.CostCenterId == costCenterId)
                    select pn;
        }

        query = from pn in query
                orderby pn.Name
                select pn;

        //paging
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets subsidy packName mapping by identifier
    /// </summary>
    /// <param name="mappingId">Subsidy packName mapping identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mapping
    /// </returns>
    public async Task<SubsidyPackName> GetSubsidyPackNameByIdAsync(int mappingId)
    {
        return await _subsidyPackNameRepository.GetByIdAsync(mappingId);
    }

    /// <summary>
    /// Inserts a subsidy packname mapping
    /// </summary>
    /// <param name="subsidyPackName">Subsidy packname mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertSubsidyPackNameAsync(SubsidyPackName subsidyPackName)
    {
        ArgumentNullException.ThrowIfNull(subsidyPackName);

        await _subsidyPackNameRepository.InsertAsync(subsidyPackName);
    }

    /// <summary>
    /// Deletes a subsidy packName mapping
    /// </summary>
    /// <param name="subsidyPackName">Subsidy PackName mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task DeleteSubsidyPackNameAsync(SubsidyPackName subsidyPackName)
    {
        await _subsidyPackNameRepository.DeleteAsync(subsidyPackName);
    }

    #endregion

    #region Subsidy PackName Variation

    /// <summary>
    /// Gets a subsidy variation by indentifier
    /// </summary>
    /// <param name="id">Subsidy variation indentifier</param>
    /// <returns>Subsidy variation</returns>
    public async Task<SubsidyVariation> GetSubsidyVariationByIdAsync(int id)
    {
        return await _subsidyVariationRepository.GetByIdAsync(id, cache => default);
    }

    /// <summary>
    /// Gets the variation for a subsidy packName mapping
    /// </summary>
    /// <param name="subsidyPackNameId">Subsidy - Packaging name mapping identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the variation
    /// </returns>
    public async Task<SubsidyVariation> GetSubsidyVariationBySubsidyPacknameIdAsync(int subsidyPackNameId)
    {
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.SubsidyVariationBySubsidyPackNameCacheKey, subsidyPackNameId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var query = from sv in _subsidyVariationRepository.Table
                        where sv.SubsidyPackNameId == subsidyPackNameId && !sv.Deleted
                        select sv;

            return await query.FirstOrDefaultAsync();
        });
    }

    /// <summary>
    /// Asynchronously retrieves all subsidy variations associated with the specified subsidy pack name identifiers.
    /// </summary>
    /// <param name="subsidyPackNameIds">A list of subsidy pack name identifiers for which to retrieve subsidy variations. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of subsidy variations
    /// matching the specified subsidy pack name identifiers. The list is empty if no matching subsidy variations
    /// are found.</returns>
    public async Task<IList<SubsidyVariation>> GetSubsidyVariationsBySubsidyPackNameIdsAsync(IList<int> subsidyPackNameIds)
    {
        return await _subsidyVariationRepository.Table
            .Where(v => !v.Deleted && subsidyPackNameIds.Contains(v.SubsidyPackNameId))
            .ToListAsync();
    }

    /// <summary>
    /// Inserts a subsidy variation
    /// </summary>
    /// <param name="subsidyVariation">Subsidy Variation</param>
    public async Task InsertSubsidyVariationAsync(SubsidyVariation subsidyVariation)
    {
        ArgumentNullException.ThrowIfNull(subsidyVariation);

        await _subsidyVariationRepository.InsertAsync(subsidyVariation);
    }

    /// <summary>
    /// Inserts a subsidy variation history
    /// </summary>
    /// <param name="subsidyVariationHistory">Subsidy Variation History</param>
    public async Task InsertSubsidyVariationHistoryAsync(SubsidyVariationHistory subsidyVariationHistory)
    {
        ArgumentNullException.ThrowIfNull(subsidyVariationHistory);

        await _subsidyVariationHistoryRepository.InsertAsync(subsidyVariationHistory);
    }

    /// <summary>
    /// Updates a subsidy variation
    /// </summary>
    /// <param name="subsidyVariation">Subsidy Variation</param>
    public async Task UpdateSubsidyVariationAsync(SubsidyVariation subsidyVariation)
    {
        ArgumentNullException.ThrowIfNull(subsidyVariation);

        await _subsidyVariationRepository.UpdateAsync(subsidyVariation);
    }

    #endregion

    #region Product Group

    /// <summary>
    /// Gets a product group by indentifier
    /// </summary>
    /// <param name="productGroupId">Product group indentifier</param>
    /// <returns>Product Group</returns>
    public async Task<ProductGroup> GetProductGroupByIdAsync(int productGroupId)
    {
        return await _productGroupRepository.GetByIdAsync(productGroupId, cache => default);
    }

    /// <summary>
    /// Gets a list of product groups by their identifiers
    /// </summary>
    /// <param name="productGroupIds">List of product group identifiers</param>
    /// <returns>List of Product Groups</returns>
    public async Task<IList<ProductGroup>> GetProductGroupsByIdsAsync(IList<int> productGroupIds)
    {
        return await _productGroupRepository.GetByIdsAsync(productGroupIds, cache => default, includeDeleted: false);
    }

    /// <summary>
    /// Gets product group store mapping collection
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <param name="name">Name</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidies store mapping collection
    /// </returns>
    public virtual async Task<IPagedList<ProductGroupDTO>> GetProductGroupsByStoreIdAsync(int storeId, int subsidyId = 0, string? name = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        if (storeId == 0)
            return new PagedList<ProductGroupDTO>(new List<ProductGroupDTO>(), pageIndex, pageSize);

        var query = from pg in _productGroupRepository.Table
                    join pgp in _productGroupProductRepository.Table on pg.Id equals pgp.ProductGroupId into Gpgp
                    where pg.StoreId == storeId && !pg.Deleted && (string.IsNullOrEmpty(name) || pg.Name.Contains(name))
                    orderby pg.DisplayOrder, pg.Name
                    select new ProductGroupDTO
                    {
                        Id = pg.Id,
                        Name = pg.Name,
                        StoreId = pg.StoreId,
                        Description = pg.Description,
                        Icon = pg.Icon,
                        Colour = pg.Colour,
                        DisplayOrder = pg.DisplayOrder,
                        Active = pg.Active,
                        ProductCount = Gpgp.Count(),
                    };

        if (subsidyId > 0)
        {
            var productGroupsQuery = from spg in _subsidyProductGroupRepository.Table
                                     where spg.SubsidyId == subsidyId && !spg.Deleted
                                     select spg.ProductGroupId;

            query = from pg in query
                    where !productGroupsQuery.Contains(pg.Id)
                    select pg;
        }

        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Inserts a Product Group
    /// </summary>
    /// <param name="productGroup">Product group</param>
    public async Task InsertProductGroupAsync(ProductGroup productGroup)
    {
        ArgumentNullException.ThrowIfNull(productGroup);

        await _productGroupRepository.InsertAsync(productGroup);
    }

    /// <summary>
    /// Updates a Product Group
    /// </summary>
    /// <param name="productGroup">Product group</param>
    public async Task UpdateProductGroupAsync(ProductGroup productGroup)
    {
        ArgumentNullException.ThrowIfNull(productGroup);

        await _productGroupRepository.UpdateAsync(productGroup);
    }

    /// <summary>
    /// Deletes a Product Group
    /// </summary>
    /// <param name="productGroup">Product group</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DeleteProductGroupAsync(ProductGroup productGroup)
    {
        await _productGroupRepository.DeleteAsync(productGroup);
    }

    /// <summary>
    /// Gets the list of icons for product groups
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<List<string>> GetProductGroupIconsAsync()
    {
        return await _staticCacheManager.GetAsync(SubsidyServicesDefaults.SubsidyProductGroupIconsCacheKey, async () =>
        {
            var fileName = _fileProvider.MapPath($"{NopPluginDefaults.Path}/{CWCoreDefaults.SystemName}/Content/icons/pg-icons.svg");

            if (!_fileProvider.FileExists(fileName))
                return new List<string>();

            var text = await _fileProvider.ReadAllTextAsync(fileName, Encoding.UTF8);

            // Extract all symbol IDs that start with "pg-icon-"
            var matches = Regex.Matches(text, @"<symbol[^>]+id=""(pg-icon-[^""]+)""");

            return matches
        .Select(m => m.Groups[1].Value)
        .OrderBy(x => x)
        .ToList();
        });
    }

    #endregion

    #region Product Group Product Mapping

    /// <summary>
    /// Gets all products by product group identifier
    /// </summary>
    /// <param name="productGroupId">Product Group identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product list
    /// </returns>
    public async Task<IList<ProductGroupProduct>> GetProductsByProductGroupIdAsync(int productGroupId)
    {
        var query = from pgp in _productGroupProductRepository.Table
                    join p in _productRepository.Table on pgp.ProductId equals p.Id
                    where pgp.ProductGroupId == productGroupId && !p.Deleted
                    select pgp;

        var products = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.ProductsByProductGroupCacheKey, productGroupId), async () => await query.ToListAsync());

        return products;
    }

    /// <summary>
    /// Gets  products by product group identifier
    /// </summary>
    /// <param name="productGroupId">Product Group identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product list
    /// </returns>
    public async Task<IPagedList<Product>> GetProductsByProductGroupIdAsync(int productGroupId,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        //!p.Deleted, as the IList overload above already has it. This is the admin grid's query
        //and it is NOT cached, so a deleted product stayed listed in its
        //product group until the mapping row was removed by hand - no cache clear could help
        //(two methods with one name, and only one of them fixed).
        //ProductGroupProduct itself is a hard-delete mapping with no Deleted column, so the product's
        //own flag is the only thing to filter on.
        var query = from pgp in _productGroupProductRepository.Table
                    join p in _productRepository.Table on pgp.ProductId equals p.Id
                    where pgp.ProductGroupId == productGroupId && !p.Deleted
                    orderby p.Name
                    select p;

        //paging
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets product group product mapping
    /// </summary>
    /// <param name="productGroupId">Product group identifier</param>
    /// <param name="productId">Product identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mapping
    /// </returns>
    public async Task<ProductGroupProduct> GetProductGroupProductByIdAsync(int productGroupId, int productId)
    {
        if (productGroupId == 0 || productId == 0)
            return null;

        var query = from pgp in _productGroupProductRepository.Table
                    where pgp.ProductGroupId == productGroupId && pgp.ProductId == productId
                    select pgp;

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Inserts a product group product mapping
    /// </summary>
    /// <param name="productGroupProduct">Product group product mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertProductGroupProductAsync(ProductGroupProduct productGroupProduct)
    {
        await _productGroupProductRepository.InsertAsync(productGroupProduct);
    }

    /// <summary>
    /// Deletes a product group product mapping
    /// </summary>
    /// <param name="productGroupProduct">Product group product mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task DeleteProductGroupProductAsync(ProductGroupProduct productGroupProduct)
    {
        await _productGroupProductRepository.DeleteAsync(productGroupProduct);
    }

    #endregion

    #region Subsidy Product Group Mapping

    /// <summary>
    /// Gets product groups by subsidy identifier
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    public async Task<IList<SubsidyProductGroup>> GetSubsidyProductGroupsBySubsidyIdAsync(int subsidyId)
    {
        var query = from spg in _subsidyProductGroupRepository.Table
                    where spg.SubsidyId == subsidyId && !spg.Deleted
                    select spg;

        var subsidyProductGroups = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(SubsidyServicesDefaults.SubsidyProductGroupsCacheKey, subsidyId), async () => await query.ToListAsync());

        return subsidyProductGroups;
    }

    /// <summary>
    /// Gets subsidy product groups by product group identifier
    /// </summary>
    /// <param name="productGroupId">Product Group identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    public async Task<IList<SubsidyProductGroup>> GetSubsidyProductGroupsByProductGroupIdAsync(int productGroupId)
    {
        var query = from spg in _subsidyProductGroupRepository.Table
                    where spg.ProductGroupId == productGroupId && !spg.Deleted
                    select spg;

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets product groups
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    public async Task<IPagedList<SubsidyProductGroupDTO>> GetSubsidyProductGroupsAsync(int subsidyId,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = from spg in _subsidyProductGroupRepository.Table
                    join pg in _productGroupRepository.Table on spg.ProductGroupId equals pg.Id
                    where spg.SubsidyId == subsidyId && !spg.Deleted && !pg.Deleted
                    orderby spg.DisplayOrder, pg.Name
                    select new SubsidyProductGroupDTO
                    {
                        Id = spg.Id,
                        SubsidyId = spg.SubsidyId,
                        ProductGroupId = pg.Id,
                        Name = pg.Name,
                        Description = pg.Description,
                        Icon = pg.Icon,
                        Colour = pg.Colour,
                        DisplayOrder = spg.DisplayOrder,
                        Quantity = spg.Quantity,
                        Credit = spg.Credit
                    };

        //paging
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets subsidy product group mapping by identifier
    /// </summary>
    /// <param name="mappingId">Subsidy product group mapping identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mapping
    /// </returns>
    public async Task<SubsidyProductGroup> GetSubsidyProductGroupByIdAsync(int mappingId)
    {
        return await _subsidyProductGroupRepository.GetByIdAsync(mappingId);
    }

    /// <summary>
    /// Updates a subsidy product group mapping
    /// </summary>
    /// <param name="subsidyProductGroup">Subsidy product group mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task UpdateSubsidyProductGroupAsync(SubsidyProductGroup subsidyProductGroup)
    {
        await _subsidyProductGroupRepository.UpdateAsync(subsidyProductGroup);
    }

    /// <summary>
    /// Inserts a subsidy product group mapping
    /// </summary>
    /// <param name="subsidyProductGroup">Subsidy product group mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertSubsidyProductGroupAsync(SubsidyProductGroup subsidyProductGroup)
    {
        ArgumentNullException.ThrowIfNull(subsidyProductGroup);

        await _subsidyProductGroupRepository.InsertAsync(subsidyProductGroup);
    }

    /// <summary>
    /// Deletes a subsidy product group mapping
    /// </summary>
    /// <param name="subsidyProductGroup">Subsidy product group mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task DeleteSubsidyProductGroupAsync(SubsidyProductGroup subsidyProductGroup)
    {
        await _subsidyProductGroupRepository.DeleteAsync(subsidyProductGroup);
    }

    #endregion

    #region Subsidy Topup

    /// <summary>
    /// Whether a topup posted now for this assignment would land inside its current ordering period.
    /// </summary>
    /// <remarks>
    /// The same window the two <c>Save*ForPeriodAsync</c> methods enforce by throwing; asked first so a
    /// screen can refuse with a message. For an annual subsidy some occurrence is always open, so this
    /// only ever answers false for a non-annual season that has closed.
    /// </remarks>
    /// <param name="subsidyPackNameId">Subsidy - packaging name mapping identifier</param>
    /// <param name="subsidy">The assignment's subsidy</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result is true when a topup can be recorded now
    /// </returns>
    public async Task<bool> IsTopupPeriodOpenAsync(int subsidyPackNameId, Subsidy subsidy)
    {
        var now = DateTime.UtcNow;

        var (windowStart, windowEnd) = await GetTopupSearchWindowAsync(now, subsidyPackNameId, subsidy);

        return (windowStart == null || now > windowStart.Value)
            && (windowEnd == null || now <= windowEnd.Value);
    }

    /// <summary>
    /// Records an addition to a packname's subsidy credit for the current ordering period.
    /// </summary>
    /// <remarks>
    /// Insert-only, for the reasons in <see cref="SaveProductGroupTopupForPeriodAsync"/>: this was a
    /// read-modify-write with no lock while inserting a history row every time, so two interleaved
    /// saves left the history summing to more than the stored balance, permanently.
    /// <c>vw_CORE_SUB_SubsidyCredit</c> already SUMs every row in the period
    /// and nothing in C# reads a single row as the period total.
    /// </remarks>
    public async Task<SubsidyTopup> SaveSubsidyTopupForPeriodAsync(int subsidyPackNameId, decimal creditToAdd, Subsidy subsidy, int creatorId, string reason)
    {
        var now = DateTime.UtcNow;

        //Not read back, but the period the row lands in is still decided by the assignment's occurrence.
        var (windowStart, windowEnd) = await GetTopupSearchWindowAsync(now, subsidyPackNameId, subsidy);

        if (windowStart != null && now <= windowStart.Value)
            throw new InvalidOperationException($"Top-up timestamp {now:O} is before the current ordering period for subsidy {subsidy.Id}.");

        if (windowEnd != null && now > windowEnd.Value)
            throw new InvalidOperationException($"Top-up timestamp {now:O} is after the current ordering period for subsidy {subsidy.Id}.");

        var subsidyTopup = new SubsidyTopup
        {
            SubsidyPackNameId = subsidyPackNameId,
            Credit = creditToAdd,
            CreatedOnUtc = now
        };

        //the credit and its history row are saved together or not at all: a top-up without its history row is credit
        //nobody can account for
        using var scope = PackNameTransaction.Begin(TransactionManager.DefaultTimeout);

        await _subsidyTopupRepository.InsertAsync(subsidyTopup);

        await _subsidyTopupHistoryRepository.InsertAsync(new SubsidyTopupHistory
        {
            SubsidyTopupId = subsidyTopup.Id,
            Credit = creditToAdd,
            Reason = reason,
            CreatorId = creatorId,
            CreatedOnUtc = now
        });

        scope.Complete();

        return subsidyTopup;
    }

    #endregion

    #region Product Group Topup

    /// <summary>
    /// Records an addition to a packname's product-group allowance for the current ordering period.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Insert-only, on purpose.</b> This was a read-modify-write — find the period's row,
    /// <c>existing.Credit += creditToAdd</c>, full-row update — with no transaction, no lock and no
    /// concurrency column, while inserting a history row every time. Two interleaved requests both read
    /// 100, both write 150, and both file their history: the history then sums to 200 against a stored
    /// 150, silently and permanently, and neither popup disables its Save button so a double-click is
    /// enough.
    /// </para>
    /// <para>
    /// The fix is to delete the read, not to lock it. <c>vw_CORE_SUB_ProductGroupCredit</c> already SUMs
    /// every row in the period, so two inserts of 50 sum to 100
    /// with no lock at all, and nothing in C# treats one row as the period total — the two
    /// "current period row" getters had one reader each, the admin GET popups, whose only use of the
    /// result was a model Id no save path reads, so they were deleted with this change. It also
    /// removes the unchecked
    /// <c>existing.Quantity += quantityToAdd</c> on an <c>int</c>, which wrapped silently to a negative
    /// quantity: <c>CheckForOverflowUnderflow</c> is set nowhere in this solution.
    /// </para>
    /// <para>
    /// <c>subsidy</c> is still taken, and still required: <c>GetTopupSearchWindow</c> reads its
    /// recurrence and dates to stamp the row inside the current occurrence. The parameter names keep
    /// "ToAdd" because that is what they are — an amount added to the period, not a new total.
    /// </para>
    /// </remarks>
    public async Task<ProductGroupTopup> SaveProductGroupTopupForPeriodAsync(int subsidyPackNameId, int productGroupId, int quantityToAdd, decimal creditToAdd, Subsidy subsidy, int creatorId, string reason)
    {
        var now = DateTime.UtcNow;

        //Not read back, but the period the row lands in is still decided by the assignment's occurrence -
        //an out-of-window CreatedOnUtc would be credit in a period nobody is ordering in.
        var (windowStart, windowEnd) = await GetTopupSearchWindowAsync(now, subsidyPackNameId, subsidy);

        if (windowStart != null && now <= windowStart.Value)
            throw new InvalidOperationException($"Top-up timestamp {now:O} is before the current ordering period for subsidy {subsidy.Id}.");

        if (windowEnd != null && now > windowEnd.Value)
            throw new InvalidOperationException($"Top-up timestamp {now:O} is after the current ordering period for subsidy {subsidy.Id}.");

        var productGroupTopup = new ProductGroupTopup
        {
            SubsidyPackNameId = subsidyPackNameId,
            ProductGroupId = productGroupId,
            Credit = creditToAdd,
            Quantity = quantityToAdd,
            CreatedOnUtc = now
        };

        //the allowance and its history row are saved together or not at all, as in SaveSubsidyTopupForPeriodAsync
        using var scope = PackNameTransaction.Begin(TransactionManager.DefaultTimeout);

        await _productGroupTopupRepository.InsertAsync(productGroupTopup);

        await _productGroupTopupHistoryRepository.InsertAsync(new ProductGroupTopupHistory
        {
            ProductGroupTopupId = productGroupTopup.Id,
            Credit = creditToAdd,
            Quantity = quantityToAdd,
            Reason = reason,
            CreatorId = creatorId,
            CreatedOnUtc = now
        });

        scope.Complete();

        return productGroupTopup;
    }

    #endregion

    #endregion
}
