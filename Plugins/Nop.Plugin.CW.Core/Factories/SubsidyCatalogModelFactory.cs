using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Web.Factories;

namespace Nop.Plugin.CW.Core.Factories;

public class SubsidyCatalogModelFactory : ISubsidyCatalogModelFactory
{
    #region Fields

    protected readonly ISubsidyCreditService _subsidyCreditService;
    protected readonly ISubsidyOrderingService _subsidyOrderingService;
    protected readonly ISubsidyService _subsidyService;
    protected readonly ISubsidyCatalogCartService _subsidyCatalogCartService;
    protected readonly IStoreContext _storeContext;
    protected readonly IProductModelFactory _productModelFactory;
    protected readonly IWorkContext _workContext;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly CatalogSettings _catalogSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPackNameService _packNameService;
    protected readonly ISettingService _settingService;
    protected readonly IBranchService _branchService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly ISubsidyReportingService _subsidyReportingService;

    #endregion

    #region Ctor

    public SubsidyCatalogModelFactory(ISubsidyCreditService subsidyCreditService,
        ISubsidyOrderingService subsidyOrderingService,
        ISubsidyService subsidyService,
        ISubsidyCatalogCartService subsidyCatalogCartService,
        IStoreContext storeContext,
        IProductModelFactory productModelFactory,
        IWorkContext workContext,
        IPriceFormatter priceFormatter,
        CatalogSettings catalogSettings,
        ILocalizationService localizationService,
        IPackNameService packNameService,
        ISettingService settingService,
        IBranchService branchService,
        IMultiStoreService multiStoreService,
        ISubsidyReportingService subsidyReportingService)
    {
        _subsidyCreditService = subsidyCreditService;
        _subsidyOrderingService = subsidyOrderingService;
        _subsidyService = subsidyService;
        _subsidyCatalogCartService = subsidyCatalogCartService;
        _storeContext = storeContext;
        _productModelFactory = productModelFactory;
        _workContext = workContext;
        _priceFormatter = priceFormatter;
        _catalogSettings = catalogSettings;
        _localizationService = localizationService;
        _packNameService = packNameService;
        _settingService = settingService;
        _branchService = branchService;
        _multiStoreService = multiStoreService;
        _subsidyReportingService = subsidyReportingService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Prepare view modes
    /// </summary>
    /// <param name="model">Catalog products model</param>
    /// <param name="command">Model to get the catalog products</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected async Task PrepareViewModesAsync(SubsidyCatalogModel model, string viewMode)
    {
        model.AllowProductViewModeChanging = _catalogSettings.AllowProductViewModeChanging;

        viewMode = !string.IsNullOrEmpty(viewMode)
            ? viewMode
            : _catalogSettings.DefaultViewMode;
        model.ViewMode = viewMode;

        if (model.AllowProductViewModeChanging)
        {
            //grid
            model.AvailableViewModes.Add(new SelectListItem
            {
                Text = await _localizationService.GetResourceAsync("Catalog.ViewMode.Grid"),
                Value = "grid",
                Selected = viewMode == "grid"
            });
            //list
            model.AvailableViewModes.Add(new SelectListItem
            {
                Text = await _localizationService.GetResourceAsync("Catalog.ViewMode.List"),
                Value = "list",
                Selected = viewMode == "list"
            });
        }
    }

    /// <summary>
    /// Builds a new product-group model from a credit entry.
    /// Does not set Products or ViewMode — the catalog method handles those.
    /// </summary>
    /// <summary>
    /// The packname's global credit left once the cart's use of it is taken off
    /// </summary>
    /// <param name="packNameCredit">The packname's credit in the current period</param>
    /// <param name="cartUsage">What the cart uses of it</param>
    /// <returns>The global credit left; zero or less when the cart uses it all</returns>
    protected static decimal GetRemainGlobalCredit(PackNameCredit packNameCredit, IList<SubsidyCartUsageItem> cartUsage)
    {
        return packNameCredit.SubsidyCredits.Sum(credit => credit.AvailableCredit + credit.AvailableCreditTopup)
            - cartUsage.Where(item => item.ProductGroupId == null).Sum(item => item.Credit + item.CreditTopup);
    }

    protected static SubsidyCatalogProductGroupModel BuildProductGroupModel(ProductGroupCredit pgCredit, ProductGroup productGroup, IList<SubsidyCartUsageItem> cartUsage, decimal remainGlobalCredit)
    {
        var usedPg = cartUsage.Where(x => x.ProductGroupId == pgCredit.ProductGroupId).ToList();

        var pgModel = new SubsidyCatalogProductGroupModel
        {
            Id = pgCredit.ProductGroupId,
            Name = productGroup.Name,
            Icon = productGroup.Icon,
            Colour = productGroup.Colour,
            DisplayOrder = pgCredit.DisplayOrder,
            RemainGlobalCredit = remainGlobalCredit,
            TotalQuantity = pgCredit.Quantity + pgCredit.QuantityTopup,
            AvailableQuantity = pgCredit.AvailableQuantity + pgCredit.AvailableQuantityTopup,
            AvailableCredit = pgCredit.AvailableCredit + pgCredit.AvailableCreditTopup,
            TotalCredit = pgCredit.Credit + pgCredit.CreditTopup,
            ShoppingCartQuantity = usedPg.Sum(x => x.Quantity + x.QuantityTopup),
            ShoppingCartCredit = usedPg.Sum(x => x.ProductGroupCredit + x.ProductGroupCreditTopup),
        };

        pgModel.RemainCredit = pgModel.AvailableCredit - pgModel.ShoppingCartCredit;

        return pgModel;
    }

    /// <summary>
    /// Folds a second (or later) subsidy's product-group credit into an already-built model entry.
    /// </summary>
    protected static void AggregateProductGroupCredit(SubsidyCatalogProductGroupModel existing, ProductGroupCredit pgCredit)
    {
        existing.TotalQuantity += pgCredit.Quantity + pgCredit.QuantityTopup;
        existing.AvailableQuantity += pgCredit.AvailableQuantity + pgCredit.AvailableQuantityTopup;
        existing.AvailableCredit += pgCredit.AvailableCredit + pgCredit.AvailableCreditTopup;
        existing.TotalCredit += pgCredit.Credit + pgCredit.CreditTopup;
        existing.RemainCredit = existing.AvailableCredit - existing.ShoppingCartCredit;
    }

    #endregion

    #region Methods

    public async Task<SubsidyCatalogModel> PrepareSubsidyCatalogModelAsync(PackName packName, string viewMode)
    {
        ArgumentNullException.ThrowIfNull(packName);

        var packNameCredit = await _subsidyCreditService.GetPackNameSubsidyCreditAsync(packName.Id);

        packNameCredit ??= new PackNameCredit();

        var store = await _storeContext.GetCurrentStoreAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        var allSubsidyPackNames = await _subsidyOrderingService.GetInOrderingSubsidyPackNamesByPackNameIdAsync(packName.Id);
        var creditSubsidyIds = packNameCredit.SubsidyCredits.Select(pnc => pnc.SubsidyId).ToList();

        var allPgIds = packNameCredit.SubsidyCredits.SelectMany(sc => sc.ProductGroupCredits).Select(pgc => pgc.ProductGroupId).Distinct().ToList();
        var productGroups = (await _subsidyService.GetProductGroupsByIdsAsync(allPgIds)).ToDictionary(pg => pg.Id);

        var model = new SubsidyCatalogModel();

        //view mode
        await PrepareViewModesAsync(model, viewMode);

        foreach (var subsidyPackName in allSubsidyPackNames.Where(sp => !creditSubsidyIds.Contains(sp.SubsidyId)))
        {
            var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyPackName.SubsidyId);

            if (subsidy.OrderOutsidePeriod && subsidy.Active)
            {
                var subsidyProductGroups = await _subsidyService.GetSubsidyProductGroupsBySubsidyIdAsync(subsidy.Id);

                if (subsidyProductGroups.Any())
                {
                    // Build a zero-credit SubsidyCredit entry so these product groups still appear in the catalog
                    var subsidyCredit = new SubsidyCredit
                    {
                        SubsidyId = subsidy.Id,
                        AvailableCredit = 0,
                        AvailableCreditTopup = 0,
                        Credit = 0,
                        CreditTopup = 0,
                        OnlyAllocatedProducts = subsidy.OnlyAllocatedProducts && (subsidy.IncludeManagers || !packName.Manager),
                        RestrictToAllocatedQty = subsidy.RestrictToAllocatedQty && (subsidy.IncludeManagers || !packName.Manager),
                    };

                    foreach (var subsidyProductGroup in subsidyProductGroups)
                    {
                        if (!productGroups.TryGetValue(subsidyProductGroup.ProductGroupId, out var productGroup))
                        {
                            productGroup = await _subsidyService.GetProductGroupByIdAsync(subsidyProductGroup.ProductGroupId);
                            if (productGroup is null)
                                continue;
                            productGroups[productGroup.Id] = productGroup;
                        }
                        var productIds = (await _subsidyService.GetProductsByProductGroupIdAsync(productGroup.Id)).Select(x => x.ProductId).ToList();

                        subsidyCredit.ProductGroupCredits.Add(new ProductGroupCredit
                        {
                            ProductGroupId = productGroup.Id,
                            Name = productGroup.Name,
                            DisplayOrder = subsidyProductGroup.DisplayOrder,
                            AvailableCredit = 0,
                            AvailableCreditTopup = 0,
                            AvailableQuantity = 0,
                            AvailableQuantityTopup = 0,
                            Credit = 0,
                            CreditTopup = 0,
                            Quantity = 0,
                            QuantityTopup = 0,
                            ProductIds = productIds
                        });

                    }

                    packNameCredit.SubsidyCredits.Add(subsidyCredit);
                }
            }
        }

        if (packNameCredit.SubsidyCredits.Count == 0)
            return model;

        var cartUsage = await _subsidyCatalogCartService.GetPackNameSubsidyCartUsageAsync(packName.Id, store.Id);

        var remainGlobalCredit = GetRemainGlobalCredit(packNameCredit, cartUsage);

        var pgDict = new Dictionary<int, SubsidyCatalogProductGroupModel>();

        foreach (var subsidyCredit in packNameCredit.SubsidyCredits)
        {
            foreach (var productGroupCredit in subsidyCredit.ProductGroupCredits)
            {
                if (productGroupCredit.ProductIds.Count == 0)
                    continue;

                if (pgDict.TryGetValue(productGroupCredit.ProductGroupId, out var existing))
                {
                    AggregateProductGroupCredit(existing, productGroupCredit);
                    continue;
                }

                if (!productGroups.TryGetValue(productGroupCredit.ProductGroupId, out var productGroup))
                {
                    productGroup = await _subsidyService.GetProductGroupByIdAsync(productGroupCredit.ProductGroupId);
                    if (productGroup is null)
                        continue;
                    productGroups[productGroup.Id] = productGroup;
                }

                var products = await _subsidyCatalogCartService.SearchProductsAsync(storeId: store.Id, productIds: productGroupCredit.ProductIds, visibleIndividuallyOnly: true);

                if (!products.Any())
                    continue;

                var pgModel = BuildProductGroupModel(productGroupCredit, productGroup, cartUsage, remainGlobalCredit);

                pgModel.ViewMode = model.ViewMode;
                pgModel.Products = (await _productModelFactory.PrepareProductOverviewModelsAsync(products)).ToList();

                if (pgModel.ShoppingCartCredit > 0)
                    pgModel.ShoppingCartCreditFormatted = await _priceFormatter.FormatPriceAsync(pgModel.ShoppingCartCredit, true, currency, languageId, true, false);

                pgDict[pgModel.Id] = pgModel;
            }
        }

        //a units-only group has no money figure to show
        foreach (var pgModel in pgDict.Values.Where(group => group.TotalCredit > decimal.Zero))
            pgModel.RemainCreditFormatted = await _priceFormatter.FormatPriceAsync(pgModel.RemainCredit, true, currency, languageId, true, false);

        model.ProductGroups = pgDict.Values.OrderBy(x => x.DisplayOrder).ToList();

        return model;
    }

    public async Task<SubsidyBarModel> PrepareSubsidyBarModelAsync(PackName packName, bool flyout)
    {
        ArgumentNullException.ThrowIfNull(packName);

        var currency = await _workContext.GetWorkingCurrencyAsync();
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        var store = await _storeContext.GetCurrentStoreAsync();
        var customer = await _workContext.GetCurrentCustomerAsync();
        var manager = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);
        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);
        var branch = await _branchService.GetBranchByIdAsync(packNameList.BranchId ?? 0);
        var department = await _multiStoreService.GetDepartmentByIdAsync(packName.DepartmentId ?? 0);
        
        var costCenter = await _multiStoreService.GetCostCenterByIdAsync(packName.CostCenterId ?? 0);

        var model = new SubsidyBarModel
        {
            PackNameName = packName.Name,
            PackNameColour = packName.Colour,
            PackNameBranch = branch?.BranchCode,
            PackNameDepartment = department?.Name,
            PackNameCostCenter = costCenter?.Name,
            PackNamePOEntry = packName.POEntry,
            PackNameIsGroup = packName.Group,
            PackNameIsManager = packName.Manager,
            PackNameIsCustomer = packName.IsCustomer,
            IsFlyout = flyout
        };

        //the same test the top-up pages apply (SubsidyCatalogController.ValidateTopupAccessAsync), so the button never
        //opens a dialog that refuses to load
        model.CanEditSubsidyTopup = !flyout
                                    && settings.AllowEditSubsidyTopup
                                    && settings.AllowManagersToAddPackNames
                                    && (manager?.Manager ?? false)
                                    && await _multiStoreService.IsPackNameOwnedByManagerId(manager.Id, packName.Id);

        var packNameCredit = await _subsidyCreditService.GetPackNameSubsidyCreditAsync(packName.Id);
        if (packNameCredit == null || packNameCredit.SubsidyCredits.Count == 0)
        {
            model.RemainCreditFormatted = await _priceFormatter.FormatPriceAsync(0, true, currency, languageId, true, false);
            return model;
        }

        var allPgIds = packNameCredit.SubsidyCredits.SelectMany(sc => sc.ProductGroupCredits).Select(pgc => pgc.ProductGroupId).Distinct().ToList();
        var productGroups = (await _subsidyService.GetProductGroupsByIdsAsync(allPgIds)).ToDictionary(pg => pg.Id);

        var cartUsage = await _subsidyCatalogCartService.GetPackNameSubsidyCartUsageAsync(packName.Id, store.Id);

        model.TotalCredit = packNameCredit.SubsidyCredits.Sum(x => x.Credit + x.CreditTopup);
        model.ShoppingCartCredit = cartUsage.Where(x => x.ProductGroupId == null).Sum(x => x.Credit + x.CreditTopup);

        if (model.ShoppingCartCredit > 0)
        {
            model.ShoppingCartCreditFormatted = await _priceFormatter.FormatPriceAsync(model.ShoppingCartCredit, true, currency, languageId, true, false);
        }

        var pgDict = new Dictionary<int, SubsidyCatalogProductGroupModel>();

        foreach (var subsidyCredit in packNameCredit.SubsidyCredits)
        {
            model.AvailableCredit += subsidyCredit.AvailableCredit + subsidyCredit.AvailableCreditTopup;

            foreach (var productGroupCredit in subsidyCredit.ProductGroupCredits.Where(pgc => pgc.ProductIds.Count > 0))
            {
                if (pgDict.TryGetValue(productGroupCredit.ProductGroupId, out var existing))
                {
                    AggregateProductGroupCredit(existing, productGroupCredit);
                    continue;
                }

                if (!productGroups.TryGetValue(productGroupCredit.ProductGroupId, out var productGroup))
                {
                    productGroup = await _subsidyService.GetProductGroupByIdAsync(productGroupCredit.ProductGroupId);
                    if (productGroup is null)
                        continue;
                    productGroups[productGroup.Id] = productGroup;
                }

                //the global credit left, as the catalogue's first render gives it, so a refreshed header marks the same
                //groups exhausted
                var pgModel = BuildProductGroupModel(productGroupCredit, productGroup, cartUsage, GetRemainGlobalCredit(packNameCredit, cartUsage));

                if (pgModel.ShoppingCartCredit > 0)
                    pgModel.ShoppingCartCreditFormatted = await _priceFormatter.FormatPriceAsync(pgModel.ShoppingCartCredit, true, currency, languageId, true, false);

                pgDict[pgModel.Id] = pgModel;
            }
        }

        model.RemainCredit = model.AvailableCredit - model.ShoppingCartCredit;
        model.RemainCreditFormatted = await _priceFormatter.FormatPriceAsync(model.RemainCredit, true, currency, languageId, true, false);

        //a units-only group has no money figure to show
        foreach (var pgModel in pgDict.Values.Where(group => group.TotalCredit > decimal.Zero))
            pgModel.RemainCreditFormatted = await _priceFormatter.FormatPriceAsync(pgModel.RemainCredit, true, currency, languageId, true, false);

        model.ProductGroups = pgDict.Values.OrderBy(x => x.DisplayOrder).ToList();

        return model;
    }

    /// <summary>
    /// Prepare the full Change Topup panel model for the given PackName.
    /// </summary>
    public async Task<PackNameTopupModel> PreparePackNameTopupModelAsync(PackName packName)
    {
        ArgumentNullException.ThrowIfNull(packName);

        var currency = await _workContext.GetWorkingCurrencyAsync();
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        var model = new PackNameTopupModel
        {
            PackNameId = packName.Id,
            PackNameName = packName.Name,
        };

        // The manager tops up the season being ordered in, so only assignments whose ordering period
        // is open today are listed. Credit and dates come back already effective - the variation
        // applied where the store lets it apply.
        var subsidyPackNames = await _subsidyReportingService.GetSubsidyDetailsByPackNameIdAsync(packName.Id, inOrderingPeriodOnly: true);

        foreach (var subsidyPackName in subsidyPackNames)
        {
            var subsidyModel = new PackNameTopupSubsidyModel
            {
                Id = subsidyPackName.Id,           // SubsidyPackNameId
                SubsidyId = subsidyPackName.SubsidyId,
                Name = subsidyPackName.Subsidy,
                Description = subsidyPackName.Description ?? string.Empty,
                // dto.Credit is ALREADY the effective credit (variation applied if setting ON).
                Credit = subsidyPackName.Credit,
                CreditFormatted = await _priceFormatter.FormatPriceAsync(subsidyPackName.Credit, true, currency, languageId, true, false),
                // dto.TopupCredit is ALREADY period-windowed by the SQL view.
                TopupCredit = subsidyPackName.TopupCredit,
                TopupCreditFormatted = await _priceFormatter.FormatPriceAsync(subsidyPackName.TopupCredit, true, currency, languageId, true, false),
            };

            // Product groups, with this assignment's period-windowed TopupQuantity and TopupCredit.
            // Product groups have no variation on credit; SubsidyProductGroup.Credit is always the
            // effective value.
            var subsidyProductGroups = await _subsidyReportingService.GetSubsidyPackNameProductGroupDetailsAsync(subsidyPackName.Id, subsidyPackName.SubsidyId);

            foreach (var subsidyProductGroup in subsidyProductGroups.OrderBy(p => p.DisplayOrder))
            {
                subsidyModel.ProductGroups.Add(new PackNameTopupProductGroupModel
                {
                    Id = subsidyProductGroup.ProductGroupId,
                    SubsidyPackNameId = subsidyPackName.Id,
                    Name = subsidyProductGroup.Name,
                    Icon = subsidyProductGroup.Icon,
                    Colour = subsidyProductGroup.Colour,
                    Quantity = subsidyProductGroup.Quantity,
                    TopupQuantity = subsidyProductGroup.TopupQuantity,
                    Credit = subsidyProductGroup.Credit,
                    CreditFormatted = await _priceFormatter.FormatPriceAsync(subsidyProductGroup.Credit, true, currency, languageId, true, false),
                    TopupCredit = subsidyProductGroup.TopupCredit,
                    TopupCreditFormatted = await _priceFormatter.FormatPriceAsync(subsidyProductGroup.TopupCredit, true, currency, languageId, true, false),
                    DisplayOrder = subsidyProductGroup.DisplayOrder,
                });
            }

            model.Subsidies.Add(subsidyModel);
        }

        return model;
    }

    /// <summary>
    /// Return lightweight topup values for AJAX refresh after a save.
    /// </summary>
    public async Task<IList<TopupRefreshModel>> GetPackNameTopupsAsync(int packNameId)
    {
        var result = new List<TopupRefreshModel>();
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        // Same source as PreparePackNameTopupModelAsync: the live season only.
        var subsidyPackNames = await _subsidyReportingService.GetSubsidyDetailsByPackNameIdAsync(packNameId, inOrderingPeriodOnly: true);

        foreach (var subsidyPackName in subsidyPackNames)
        {
            var item = new TopupRefreshModel
            {
                Id = subsidyPackName.Id,
                Credit = await _priceFormatter.FormatPriceAsync(subsidyPackName.TopupCredit, true, currency, languageId, true, false),
            };

            var subsidyProductGroups = await _subsidyReportingService.GetSubsidyPackNameProductGroupDetailsAsync(subsidyPackName.Id, subsidyPackName.SubsidyId);

            foreach (var subsidyProductGroup in subsidyProductGroups)
            {
                item.ProductGroups.Add(new TopupProductGroupRefreshModel
                {
                    Id = subsidyProductGroup.ProductGroupId,
                    Credit = await _priceFormatter.FormatPriceAsync(subsidyProductGroup.TopupCredit, true, currency, languageId, true, false),
                    Qty = subsidyProductGroup.TopupQuantity,
                });
            }

            result.Add(item);
        }

        return result;
    }

    /// <summary>Prepare the subsidy-level topup input form model.</summary>
    public async Task<SubsidyTopupInputModel> PrepareSubsidyTopupInputModelAsync(SubsidyTopupInputModel model, int subsidyPackNameId)
    {
        ArgumentNullException.ThrowIfNull(model);

        var subsidyPackName = await _subsidyService.GetSubsidyPackNameByIdAsync(subsidyPackNameId);

        if (subsidyPackName != null)
        {
            var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyPackName.SubsidyId);
            var packName = await _packNameService.GetPackNameByIdAsync(subsidyPackName.PackNameId);

            model.SubsidyPackNameId = subsidyPackNameId;
            model.SubsidyName = subsidy?.Name ?? string.Empty;
            model.PackNameName = packName?.Name ?? string.Empty;
        }

        return model;
    }

    /// <summary>Prepare the product-group topup input form model.</summary>
    public async Task<ProductGroupTopupInputModel> PrepareProductGroupTopupInputModelAsync(ProductGroupTopupInputModel model, int subsidyPackNameId, int productGroupId)
    {
        ArgumentNullException.ThrowIfNull(model);

        var subsidyPackName = await _subsidyService.GetSubsidyPackNameByIdAsync(subsidyPackNameId);
        var productGroup = await _subsidyService.GetProductGroupByIdAsync(productGroupId);
        var packName = subsidyPackName != null ? await _packNameService.GetPackNameByIdAsync(subsidyPackName.PackNameId) : null;

        model.SubsidyPackNameId = subsidyPackNameId;
        model.ProductGroupId = productGroupId;
        model.ProductGroupName = productGroup?.Name ?? string.Empty;
        model.PackNameName = packName?.Name ?? string.Empty;

        return model;
    }

    #endregion
}
