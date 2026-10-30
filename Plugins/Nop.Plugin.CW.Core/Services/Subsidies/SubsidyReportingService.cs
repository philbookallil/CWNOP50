using LinqToDB.Data;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

public class SubsidyReportingService : ISubsidyReportingService
{
    #region Fields

    protected readonly IRepository<SubsidyPackNameCredit> _subsidyPackNameCreditRepository;
    protected readonly IRepository<Subsidy> _subsidyRepository;
    protected readonly IRepository<PackName> _packNameRepository;
    protected readonly IRepository<SubsidyVariation> _subsidyVariationRepository;
    protected readonly IRepository<SubsidyPackName> _subsidyPackNameRepository;
    protected readonly IRepository<SubsidyProductGroup> _subsidyProductGroupRepository;
    protected readonly IRepository<SubsidyInOrderingPeriod> _subsidyInOrderingPeriodRepository;
    protected readonly IRepository<SubsidyVariationInOrderingPeriod> _subsidyVariationInOrderingPeriodRepository;
    protected readonly IRepository<ProductGroupPackNameCredit> _productGroupPackNameCreditRepository;
    protected readonly IRepository<ProductGroup> _productGroupRepository;
    protected readonly IRepository<StoreSubsidyVariationSetting> _storeSubsidyVariationSettingRepository;
    protected readonly INopDataProvider _dataProvider;

    #endregion

    #region Ctor

    public SubsidyReportingService(IRepository<SubsidyPackNameCredit> subsidyPackNameCreditRepository,
        IRepository<Subsidy> subsidyRepository,
        IRepository<PackName> packNameRepository,
        IRepository<SubsidyVariation> subsidyVariationRepository,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        IRepository<SubsidyProductGroup> subsidyProductGroupRepository,
        IRepository<SubsidyInOrderingPeriod> subsidyInOrderingPeriodRepository,
        IRepository<SubsidyVariationInOrderingPeriod> subsidyVariationInOrderingPeriodRepository,
        IRepository<ProductGroupPackNameCredit> productGroupPackNameCreditRepository,
        IRepository<ProductGroup> productGroupRepository,
        IRepository<StoreSubsidyVariationSetting> storeSubsidyVariationSettingRepository,
        INopDataProvider dataProvider)
    {
        _subsidyPackNameCreditRepository = subsidyPackNameCreditRepository;
        _subsidyRepository = subsidyRepository;
        _packNameRepository = packNameRepository;
        _subsidyVariationRepository = subsidyVariationRepository;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _subsidyProductGroupRepository = subsidyProductGroupRepository;
        _subsidyInOrderingPeriodRepository = subsidyInOrderingPeriodRepository;
        _subsidyVariationInOrderingPeriodRepository = subsidyVariationInOrderingPeriodRepository;
        _productGroupPackNameCreditRepository = productGroupPackNameCreditRepository;
        _productGroupRepository = productGroupRepository;
        _storeSubsidyVariationSettingRepository = storeSubsidyVariationSettingRepository;
        _dataProvider = dataProvider;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Every live assignment, projected the way the screens read it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Starts from the ASSIGNMENT and left-joins the credit view. The view keeps only active assignments
    /// inside their ordering period, so reading it directly hid a season that had not opened yet, an
    /// inactive subsidy, and everything the admin needs to set up before the first order - and the
    /// variation popup dereferenced the missing row. Here an out-of-period assignment is a row with zero
    /// period figures and <c>InOrderingPeriod = false</c>; the caller decides what to do with it.
    /// </para>
    /// <para>
    /// The effective credit and dates are computed here with the same rule the view applies - the
    /// variation's value when the store lets packnames own it and the variation sets one, else the
    /// subsidy's - so an out-of-period row shows the same figures it will show once its season opens.
    /// Only non-deleted variations count; the view filters them the same way.
    /// </para>
    /// <para>
    /// The subsidy's own <c>OnlyAllocatedProducts</c> / <c>RestrictToAllocatedQty</c> flags are reported,
    /// not the view's manager-adjusted ones: these rows describe the assignment as configured, and the
    /// panel shows <c>IncludeManagers</c> beside them so the reader can apply the rule themselves.
    /// </para>
    /// </remarks>
    /// <returns>The query, unfiltered and unordered</returns>
    protected virtual IQueryable<SubsidyPackNameDTO> AssignmentDetailsQuery()
    {
        var subsidyProductGroupsQuery = from spg in _subsidyProductGroupRepository.Table
                                        where !spg.Deleted
                                        select spg;

        var subsidyVariationsQuery = from sv in _subsidyVariationRepository.Table
                                     where !sv.Deleted
                                     select sv;

        return from sp in _subsidyPackNameRepository.Table
               join s in _subsidyRepository.Table on sp.SubsidyId equals s.Id
               join pn in _packNameRepository.Table on sp.PackNameId equals pn.Id
               join vset in _storeSubsidyVariationSettingRepository.Table on s.StoreId equals vset.StoreId
               join sc in _subsidyPackNameCreditRepository.Table on sp.Id equals sc.Id into Gsc
               from sc in Gsc.DefaultIfEmpty()
               join sv in subsidyVariationsQuery on sp.Id equals sv.SubsidyPackNameId into Gsv
               from sv in Gsv.DefaultIfEmpty()
               join inordering in _subsidyInOrderingPeriodRepository.Table on sp.SubsidyId equals inordering.Id into Ginordering
               from inordering in Ginordering.DefaultIfEmpty()
               join varordering in _subsidyVariationInOrderingPeriodRepository.Table on sp.Id equals varordering.Id into Gvarordering
               from varordering in Gvarordering.DefaultIfEmpty()
               join spg in subsidyProductGroupsQuery on sp.SubsidyId equals spg.SubsidyId into Gspg
               where !sp.Deleted && !s.Deleted && !pn.Deleted
               select new SubsidyPackNameDTO
               {
                   Id = sp.Id,
                   StoreId = s.StoreId,
                   PackNameId = sp.PackNameId,
                   PackName = pn.Name,
                   SubsidyId = sp.SubsidyId,
                   Subsidy = s.Name,
                   Description = s.Description,
                   Active = s.Active,
                   AssignmentActive = sp.Active,
                   IncludeManagers = s.IncludeManagers,
                   AnnualRecurrence = s.AnnualRecurrence,
                   OrderOutsidePeriod = s.OrderOutsidePeriod,
                   OnlyAllocatedProducts = s.OnlyAllocatedProducts,
                   RestrictToAllocatedQty = s.RestrictToAllocatedQty,
                   Credit = vset.PackNamesOwnGlobalValue && sv != null && sv.Credit.HasValue ? sv.Credit.Value : s.Credit,
                   DurationStart = vset.PackNamesOwnStartAndFinishDates && sv != null && sv.DurationStart.HasValue ? sv.DurationStart.Value : s.DurationStart,
                   DurationEnd = vset.PackNamesOwnStartAndFinishDates && sv != null && sv.DurationEnd.HasValue ? sv.DurationEnd.Value : s.DurationEnd,
                   TopupCredit = sc != null ? sc.CreditTopup : 0,
                   UsedCredit = sc != null ? sc.UsedCredit : 0,
                   UsedCreditTopup = sc != null ? sc.UsedCreditTopup : 0,
                   InOrderingPeriod = varordering != null ? varordering.InOrderingPeriod : (inordering != null ? inordering.InOrderingPeriod : false),
                   ProductGroupsCount = Gspg.Count(),
                   VariationCredit = sv != null ? sv.Credit : null,
                   VariationDurationStart = sv != null ? sv.DurationStart : null,
                   VariationDurationEnd = sv != null ? sv.DurationEnd : null,
               };
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets one assignment with its effective credit and dates and its current-period figures
    /// </summary>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packname details, or null when the assignment does not exist
    /// </returns>
    public virtual async Task<SubsidyPackNameDTO> GetSubsidyPackNameDetailsByIdAsync(int subsidyPackNameId)
    {
        return await AssignmentDetailsQuery()
            .Where(dto => dto.Id == subsidyPackNameId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets subsidy and packName basic information by identifier
    /// </summary>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packname basic information
    /// </returns>
    public virtual async Task<SubsidyPackNameDTO> GetSubsidyPackNameBasicInformationByIdAsync(int subsidyPackNameId)
    {
        var query = from sp in _subsidyPackNameRepository.Table
                    join s in _subsidyRepository.Table on sp.SubsidyId equals s.Id
                    join pn in _packNameRepository.Table on sp.PackNameId equals pn.Id
                    where sp.Id == subsidyPackNameId
                    select new SubsidyPackNameDTO
                    {
                        Id = sp.Id,
                        StoreId = s.StoreId,
                        PackNameId = sp.PackNameId,
                        PackName = pn.Name,
                        SubsidyId = sp.SubsidyId,
                        Subsidy = s.Name,
                        DurationStart = s.DurationStart,
                        DurationEnd = s.DurationEnd,
                    };

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets every live assignment of a packname with its effective credit and dates and its current-period
    /// figures
    /// </summary>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <param name="inOrderingPeriodOnly">True to return only assignments whose ordering period is open today</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the assignments, ordered by subsidy name
    /// </returns>
    public virtual async Task<IList<SubsidyPackNameDTO>> GetSubsidyDetailsByPackNameIdAsync(int packNameId, bool inOrderingPeriodOnly)
    {
        var query = AssignmentDetailsQuery().Where(dto => dto.PackNameId == packNameId);

        if (inOrderingPeriodOnly)
            query = query.Where(dto => dto.InOrderingPeriod);

        return await query.OrderBy(dto => dto.Subsidy).ToListAsync();
    }

    /// <summary>
    /// Gets the product groups a subsidy allocates, with this assignment's current-period topups and usage
    /// per group (zero when the assignment is out of period)
    /// </summary>
    /// <remarks>
    /// Starts from the ALLOCATION and left-joins the product-group credit view, for the same reason
    /// <see cref="AssignmentDetailsQuery"/> starts from the assignment: an inner join to a view that only
    /// carries the current period showed an out-of-period packname no groups at all.
    /// </remarks>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product groups, in display order
    /// </returns>
    public virtual async Task<IList<SubsidyProductGroupDTO>> GetSubsidyPackNameProductGroupDetailsAsync(int subsidyPackNameId, int subsidyId, bool activeOnly = false)
    {
        var creditsQuery = from pgc in _productGroupPackNameCreditRepository.Table
                           where pgc.SubsidyId == subsidyId && pgc.Id == subsidyPackNameId
                           select pgc;

        var query = from spg in _subsidyProductGroupRepository.Table
                    join pg in _productGroupRepository.Table on spg.ProductGroupId equals pg.Id
                    join pgc in creditsQuery on pg.Id equals pgc.ProductGroupId into Gpgc
                    from pgc in Gpgc.DefaultIfEmpty()
                    where spg.SubsidyId == subsidyId && !spg.Deleted && !pg.Deleted && (!activeOnly || pg.Active)
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
                        Credit = spg.Credit,
                        TopupQuantity = pgc != null ? pgc.QuantityTopup : 0,
                        TopupCredit = pgc != null ? pgc.CreditTopup : 0,
                        UsedQuantity = pgc != null ? pgc.UsedQuantity : 0,
                        UsedQuantityTopup = pgc != null ? pgc.UsedQuantityTopup : 0,
                        UsedCredit = pgc != null ? pgc.UsedCredit : 0,
                        UsedCreditTopup = pgc != null ? pgc.UsedCreditTopup : 0
                    };

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets, for every in-period assignment of the given subsidies, what it has and what it has used —
    /// money and units together. Assignments out of period have no row.
    /// </summary>
    /// <remarks>
    /// One query for a whole grid page: the two admin grids used to render a hard-coded "0%" because the
    /// aggregate was never written, while the panel computed a real figure, so the same packname read
    /// 37% on one screen and 0% on another. Units are summed over the subsidy's product groups, because a
    /// quantity-only subsidy has no credit to divide by and would otherwise show nothing.
    /// </remarks>
    /// <param name="subsidyIds">Subsidy identifiers</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains one row per in-period assignment
    /// </returns>
    public virtual async Task<IList<SubsidyPackNameUsageDTO>> GetSubsidyUsageBySubsidyIdsAsync(IList<int> subsidyIds)
    {
        if (subsidyIds == null || !subsidyIds.Any())
            return new List<SubsidyPackNameUsageDTO>();

        var unitsQuery = from pgc in _productGroupPackNameCreditRepository.Table
                         where subsidyIds.Contains(pgc.SubsidyId)
                         group pgc by pgc.Id into g
                         select new
                         {
                             SubsidyPackNameId = g.Key,
                             TotalQuantity = g.Sum(pgc => pgc.Quantity + pgc.QuantityTopup),
                             UsedQuantity = g.Sum(pgc => pgc.UsedQuantity + pgc.UsedQuantityTopup)
                         };

        var query = from sc in _subsidyPackNameCreditRepository.Table
                    join units in unitsQuery on sc.Id equals units.SubsidyPackNameId into Gunits
                    from units in Gunits.DefaultIfEmpty()
                    where subsidyIds.Contains(sc.SubsidyId)
                    select new SubsidyPackNameUsageDTO
                    {
                        SubsidyPackNameId = sc.Id,
                        SubsidyId = sc.SubsidyId,
                        TotalCredit = sc.Credit + sc.CreditTopup,
                        UsedCredit = sc.UsedCredit + sc.UsedCreditTopup,
                        TotalQuantity = units != null ? units.TotalQuantity : 0,
                        UsedQuantity = units != null ? units.UsedQuantity : 0
                    };

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets subsidy history
    /// </summary>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the history rows, newest first
    /// </returns>
    public virtual async Task<IPagedList<SubsidyHistory>> GetSubsidyPackNameHistoryAsync(int packNameId, int subsidyId,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var results = await _dataProvider.QueryAsync<SubsidyHistory>(
            $"EXEC [dbo].[{CWCoreDefaults.GetSubsidyHistorySPName}] @PackNameId, @SubsidyId, @PageIndex, @PageSize",
            new DataParameter("@PackNameId", packNameId),
            new DataParameter("@SubsidyId", subsidyId),
            new DataParameter("@PageIndex", pageIndex),
            new DataParameter("@PageSize", pageSize));

        var rows = results.ToList();

        var totalCount = rows.FirstOrDefault()?.TotalCount ?? 0;

        return new PagedList<SubsidyHistory>(rows, pageIndex, pageSize, totalCount);
    }

    #endregion
}