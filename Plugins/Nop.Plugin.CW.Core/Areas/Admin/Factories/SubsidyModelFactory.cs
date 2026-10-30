using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class SubsidyModelFactory : ISubsidyModelFactory
{
    #region Fields

    protected readonly ISubsidyService _subsidyService;
    protected readonly IUrlRecordService _urlRecordService;
    protected readonly IPictureService _pictureService;
    protected readonly IBaseAdminModelFactory _baseAdminModelFactory;
    protected readonly IProductService _productService;
    protected readonly ICWSettingModelFactory _cwSettingModelFactory;
    protected readonly ICurrencyService _currencyService;
    protected readonly CurrencySettings _currencySettings;
    protected readonly IWorkContext _workContext;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IPackNameModelFactory _packNameModelFactory;
    protected readonly ISettingService _settingService;
    protected readonly IPackNameService _packNameService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ISubsidyReportingService _subsidyReportingService;

    #endregion

    #region Ctor

    public SubsidyModelFactory(ISubsidyService subsidyService,
        IUrlRecordService urlRecordService,
        IPictureService pictureService,
        IBaseAdminModelFactory baseAdminModelFactory,
        IProductService productService,
        ICWSettingModelFactory cwSettingModelFactory,
        ICurrencyService currencyService,
        CurrencySettings currencySettings,
        IWorkContext workContext,
        ILocalizationService localizationService,
        IPriceFormatter priceFormatter,
        IPackNameModelFactory packNameModelFactory,
        ISettingService settingService,
        IPackNameService packNameService,
        IDateTimeHelper dateTimeHelper,
        ISubsidyReportingService subsidyReportingService)
    {
        _subsidyService = subsidyService;
        _urlRecordService = urlRecordService;
        _pictureService = pictureService;
        _baseAdminModelFactory = baseAdminModelFactory;
        _productService = productService;
        _cwSettingModelFactory = cwSettingModelFactory;
        _currencyService = currencyService;
        _currencySettings = currencySettings;
        _workContext = workContext;
        _localizationService = localizationService;
        _priceFormatter = priceFormatter;
        _packNameModelFactory = packNameModelFactory;
        _settingService = settingService;
        _packNameService = packNameService;
        _dateTimeHelper = dateTimeHelper;
        _subsidyReportingService = subsidyReportingService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Formats how much of an allocation has been used, as a percentage
    /// </summary>
    /// <param name="totalCredit">Credit available (allocation plus top-ups)</param>
    /// <param name="usedCredit">Credit used</param>
    /// <param name="totalQuantity">Units available (allocation plus top-ups)</param>
    /// <param name="usedQuantity">Units used</param>
    /// <returns>
    /// The credit percentage when there is credit, the unit percentage when there are only units, and a
    /// dash when there is nothing to measure against
    /// </returns>
    protected virtual string FormatUsedPercent(decimal totalCredit, decimal usedCredit, int totalQuantity, int usedQuantity)
    {
        if (totalCredit > 0)
            return string.Format("{0:P0}", usedCredit / totalCredit);

        if (totalQuantity > 0)
            return string.Format("{0:P0}", (decimal)usedQuantity / totalQuantity);

        return "\u2014";
    }

    /// <summary>
    /// Picks the colour a product group starts with, so a group never saves without one
    /// </summary>
    /// <returns>A random colour in #RRGGBB form</returns>
    protected virtual string NewProductGroupColour()
    {
        return $"#{Random.Shared.Next(0x1000000):X6}";
    }

    /// <summary>
    /// Prepare subsidy product group search model
    /// </summary>
    /// <param name="searchModel">Subsidy product group search model</param>
    /// <param name="productGroupId">Product group identifier</param>
    /// <returns>Product group product search model</returns>
    protected virtual ProductGroupProductSearchModel PrepareProductGroupProductSearchModel(ProductGroupProductSearchModel searchModel, int productGroupId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.ProductGroupId = productGroupId;

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare available icons for product groups
    /// </summary>
    /// <param name="items">Items</param>
    /// <returns>Product group product icons</returns>
    protected virtual async Task<IList<SelectListItem>> PrepareProductGroupIconsAsync(IList<SelectListItem> items)
    {
        var icons = await _subsidyService.GetProductGroupIconsAsync();

        foreach (var icon in icons)
        {
            items.Add(new SelectListItem
            {
                Text = icon,
                Value = icon
            });
        }

        items.Insert(0, new SelectListItem
        {
            Text = string.Empty,
            Value = string.Empty
        });

        return items;
    }

    /// <summary>
    /// Prepare subsidy packname search model
    /// </summary>
    /// <param name="searchModel">Subsidy search model</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>Subsidy packname search model</returns>
    protected virtual SubsidyPackNameSearchModel PrepareSubsidyPackNameSearchModel(SubsidyPackNameSearchModel searchModel, int subsidyId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.SubsidyId = subsidyId;

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare subsidy product group search model
    /// </summary>
    /// <param name="searchModel">Subsidy search model</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>Subsidy product group search model</returns>
    protected virtual SubsidyProductGroupSearchModel PrepareSubsidyProductGroupSearchModel(SubsidyProductGroupSearchModel searchModel, int subsidyId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.SubsidyId = subsidyId;

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare subsidy models
    /// </summary>
    /// <param name="model">PackName model</param>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>Subsidy models</returns>
    /// <remarks>
    /// Every live assignment is listed, in period or not: an admin sets a season up before it opens and
    /// reviews it after it closes. The period figures of an assignment outside its period are zero and
    /// its top-up buttons are disabled by the view.
    /// </remarks>
    protected virtual async Task<PackNameSubsidiesModel> PreparePackNameSubsidiesModelAsync(PackNameSubsidiesModel model, int packNameId)
    {
        ArgumentNullException.ThrowIfNull(model);

        var packNameSubsidies = await _subsidyReportingService.GetSubsidyDetailsByPackNameIdAsync(packNameId, inOrderingPeriodOnly: false);
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        model.Subsidies = await packNameSubsidies.SelectAwait(async packNameSubsidy =>
        {
            var credit = packNameSubsidy.Credit;

            var totalCredit = credit + packNameSubsidy.TopupCredit;
            var usedCredit = packNameSubsidy.UsedCredit + packNameSubsidy.UsedCreditTopup;

            //units live on the product groups; the money row above does not see them
            var productGroups = await _subsidyReportingService.GetSubsidyPackNameProductGroupDetailsAsync(packNameSubsidy.Id, packNameSubsidy.SubsidyId);
            var totalQuantity = productGroups.Sum(productGroup => productGroup.Quantity + productGroup.TopupQuantity);
            var usedQuantity = productGroups.Sum(productGroup => productGroup.UsedQuantity + productGroup.UsedQuantityTopup);

            //fill in model values from the entity
            var subsidyModel = new PackNameSubsidyModel
            {
                Id = packNameSubsidy.Id,
                PackNameId = packNameId,
                SubsidyId = packNameSubsidy.SubsidyId,
                Name = packNameSubsidy.Subsidy,
                Description = packNameSubsidy.Description,
                StartDate = packNameSubsidy.DurationStart.ToShortDateString(),
                EndDate = packNameSubsidy.DurationEnd.ToShortDateString(),
                AnnualRecurrence = packNameSubsidy.AnnualRecurrence,
                OrderOutsidePeriod = packNameSubsidy.OrderOutsidePeriod,
                OnlyAllocatedProducts = packNameSubsidy.OnlyAllocatedProducts,
                IncludeManagers = packNameSubsidy.IncludeManagers,
                RestrictToAllocatedQty = packNameSubsidy.RestrictToAllocatedQty,
                Active = packNameSubsidy.Active,
                Credit = credit,
                CreditFormatted = await _priceFormatter.FormatPriceAsync(credit, true, currency, languageId, true, false),
                TopupCredit = packNameSubsidy.TopupCredit,
                TopupCreditFormatted = await _priceFormatter.FormatPriceAsync(packNameSubsidy.TopupCredit, true, currency, languageId, true, false),
                TotalCredit = totalCredit,
                TotalCreditFormatted = await _priceFormatter.FormatPriceAsync(totalCredit, true, currency, languageId, true, false),
                UsedCredit = usedCredit,
                UsedCreditFormatted = await _priceFormatter.FormatPriceAsync(usedCredit, true, currency, languageId, true, false),
                RemainingCredit = totalCredit - usedCredit,
                RemainingCreditFormatted = await _priceFormatter.FormatPriceAsync(totalCredit - usedCredit, true, currency, languageId, true, false),
                HasQuantity = totalQuantity > 0,
                TotalQuantity = totalQuantity,
                UsedQuantity = usedQuantity,
                RemainingQuantity = totalQuantity - usedQuantity,
                ProductGroupsCount = packNameSubsidy.ProductGroupsCount,
                //outside its ordering period an assignment has no usage to measure, so the panel reads like the grids
                UsedPercent = packNameSubsidy.InOrderingPeriod
                    ? FormatUsedPercent(totalCredit, usedCredit, totalQuantity, usedQuantity)
                    : FormatUsedPercent(0, 0, 0, 0),
                InOrderingPeriod = packNameSubsidy.InOrderingPeriod,
                PackNameSubsidyProductGroupSearchModel = new PackNameSubsidyProductGroupSearchModel { SubsidyPackNameId = packNameSubsidy.Id }
            };

            return subsidyModel;
        }).ToListAsync();

        return model;
    }

    #endregion

    #region Methods

    #region Product Groups

    /// <summary>
    /// Prepare product group search model
    /// </summary>
    /// <param name="searchModel">Product group search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>Product group search model</returns>
    public virtual ProductGroupSearchModel PrepareProductGroupSearchModel(ProductGroupSearchModel searchModel, int storeId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.StoreId = storeId;

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged product group list model
    /// </summary>
    /// <param name="searchModel">Product group search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store product group list model
    /// </returns>
    public virtual async Task<ProductGroupListModel> PrepareProductGroupListModelAsync(ProductGroupSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get store product groups
        var productGroups = await _subsidyService.GetProductGroupsByStoreIdAsync(searchModel.StoreId, name: searchModel.SearchProductGroupName,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = new ProductGroupListModel().PrepareToGrid(searchModel, productGroups, () =>
        {
            return productGroups.Select(productGroup =>
            {
                //fill in model values from the entity
                var productGroupModel = new ProductGroupModel
                {
                    Id = productGroup.Id,
                    StoreId = searchModel.StoreId,
                    Name = productGroup.Name,
                    Description = productGroup.Description,
                    Icon = productGroup.Icon,
                    Colour = productGroup.Colour,
                    DisplayOrder = productGroup.DisplayOrder,
                    ProductCount = productGroup.ProductCount,
                    Active = productGroup.Active
                };

                return productGroupModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare product group model
    /// </summary>
    /// <param name="model">Product group model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="productGroupId">Product group identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group model
    /// </returns>
    public virtual async Task<ProductGroupModel> PrepareProductGroupModelAsync(ProductGroupModel model, int storeId, int? productGroupId)
    {
        var productGroup = productGroupId.HasValue ? await _subsidyService.GetProductGroupByIdAsync(productGroupId.Value) : null;

        if (model == null)
        {
            model = productGroup != null
                ? productGroup.ToModel(new ProductGroupModel { IsNew = false })
                : new ProductGroupModel { Active = true, IsNew = true };
        }

        model.StoreId = storeId;

        //a group always carries a colour: the storefront paints its icon with it
        if (string.IsNullOrEmpty(model.Colour))
            model.Colour = NewProductGroupColour();

        if (productGroup != null)
        {
            PrepareProductGroupProductSearchModel(model.ProductGroupProductSearchModel, productGroup?.Id ?? 0);
        }

        await PrepareProductGroupIconsAsync(model.AvailableIcons);

        return model;
    }

    #endregion

    #region Product Group - Products

    /// <summary>
    /// Prepare paged product list model
    /// </summary>
    /// <param name="searchModel">Product search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product list model
    /// </returns>
    public virtual async Task<ProductListModel> PrepareProductListModelAsync(ProductGroupProductSearchModel searchModel)
    {
        if (searchModel == null)
            throw new ArgumentNullException(nameof(searchModel));

        var products = await _subsidyService.GetProductsByProductGroupIdAsync(searchModel.ProductGroupId, pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare list model
        var model = await new ProductListModel().PrepareToGridAsync(searchModel, products, () =>
        {
            return products.SelectAwait(async product =>
            {
                //fill in model values from the entity
                var productModel = product.ToModel<ProductModel>();

                //little performance optimization: ensure that "FullDescription" is not returned
                productModel.FullDescription = string.Empty;

                //fill in additional values (not existing in the entity)
                productModel.SeName = await _urlRecordService.GetSeNameAsync(product, 0, true, false);
                var defaultProductPicture = (await _pictureService.GetPicturesByProductIdAsync(product.Id, 1)).FirstOrDefault();
                (productModel.PictureThumbnailUrl, _) = await _pictureService.GetPictureUrlAsync(defaultProductPicture, 75);

                return productModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare product search model to add to the product group
    /// </summary>
    /// <param name="searchModel">Product search model to add to the product group</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product search model to add to the product group
    /// </returns>
    public virtual async Task<AddProductToProductGroupSearchModel> PrepareAddProductToProductGroupSearchModelAsync(AddProductToProductGroupSearchModel searchModel, int productGroupId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var productGroup = await _subsidyService.GetProductGroupByIdAsync(productGroupId);

        searchModel.ProductGroupId = productGroupId;
        searchModel.ProductGroup = productGroup?.Name ?? string.Empty;

        //prepare available categories
        await _baseAdminModelFactory.PrepareCategoriesAsync(searchModel.AvailableCategories);

        //prepare available manufacturers
        await _baseAdminModelFactory.PrepareManufacturersAsync(searchModel.AvailableManufacturers);

        //prepare available product types
        await _baseAdminModelFactory.PrepareProductTypesAsync(searchModel.AvailableProductTypes);

        //prepare page parameters
        searchModel.SetPopupGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged product list model to add to the product group
    /// </summary>
    /// <param name="searchModel">Product search model to add to the product group</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product list model to add to the product group
    /// </returns>
    public virtual async Task<ProductListModel> PrepareAddProductListModelAsync(AddProductToProductGroupSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //STORE-SCOPED. This listed every store's products for a picker that attaches products to ONE
        //store's product group, so an admin could pick a product that store cannot sell and the POST
        //accepted it. The group's store is the authority; the POST re-checks the store mapping AND
        //Published.
        //
        //overridePublished: true is NOT redundant with showHidden: true - it is the published filter.
        //nop applies Where(p => p.Published) only when !showHidden, and
        //Where(p => p.Published == overridePublished.Value) only when overridePublished has a value
        //(ProductService:840-843). With showHidden: true and no overridePublished there is NO published
        //predicate, which is how dropping this line put unpublished products into the picker. The
        //caller already refuses a missing product group (ProductGroupProductPopupList:218), so
        //storeId is never 0 here.
        var productGroup = await _subsidyService.GetProductGroupByIdAsync(searchModel.ProductGroupId);

        //get products
        var products = await _productService.SearchProductsAsync(showHidden: true,
            overridePublished: true,
            storeId: productGroup?.StoreId ?? 0,
            categoryIds: new List<int> { searchModel.SearchCategoryId },
            manufacturerIds: new List<int> { searchModel.SearchManufacturerId },
            productType: searchModel.SearchProductTypeId > 0 ? (ProductType?)searchModel.SearchProductTypeId : null,
            keywords: searchModel.SearchProductName,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = await new ProductListModel().PrepareToGridAsync(searchModel, products, () =>
        {
            return products.SelectAwait(async product =>
            {
                var productModel = product.ToModel<ProductModel>();

                productModel.SeName = await _urlRecordService.GetSeNameAsync(product, 0, true, false);
                var defaultProductPicture = (await _pictureService.GetPicturesByProductIdAsync(product.Id, 1)).FirstOrDefault();
                (productModel.PictureThumbnailUrl, _) = await _pictureService.GetPictureUrlAsync(defaultProductPicture, 75);

                return productModel;
            });
        });

        return model;
    }

    #endregion

    #region Subsidies

    /// <summary>
    /// Prepare subsidy search model
    /// </summary>
    /// <param name="searchModel">Subsidy search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>Subsidy search model</returns>
    public virtual SubsidySearchModel PrepareSubsidySearchModel(SubsidySearchModel searchModel, int storeId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.StoreId = storeId;

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged subsidy list model
    /// </summary>
    /// <param name="searchModel">Subsidy search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store subsidy list model
    /// </returns>
    /// <remarks>
    /// <c>TotalCredit</c> is the allocation - credit times assigned packnames - which is what an admin
    /// budgets against. <c>UsedPercent</c> is measured over the in-period assignments only, since an
    /// assignment outside its period has nothing to spend; a subsidy whose season has not opened reads
    /// a dash.
    /// </remarks>
    public virtual async Task<SubsidyListModel> PrepareSubsidyListModelAsync(SubsidySearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get store subsidies
        var subsidies = await _subsidyService.GetSubsidiesByStoreIdAsync(searchModel.StoreId, searchModel.SearchSubsidyName,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        var usageBySubsidy = (await _subsidyReportingService.GetSubsidyUsageBySubsidyIdsAsync(subsidies.Select(subsidy => subsidy.Id).ToList()))
            .GroupBy(usage => usage.SubsidyId)
            .ToDictionary(group => group.Key, group => new
            {
                TotalCredit = group.Sum(usage => usage.TotalCredit),
                UsedCredit = group.Sum(usage => usage.UsedCredit),
                TotalQuantity = group.Sum(usage => usage.TotalQuantity),
                UsedQuantity = group.Sum(usage => usage.UsedQuantity)
            });

        //prepare grid model
        var model = new SubsidyListModel().PrepareToGrid(searchModel, subsidies, () =>
        {
            return subsidies.Select(subsidy =>
            {
                var usage = usageBySubsidy.GetValueOrDefault(subsidy.Id);

                //fill in model values from the entity
                var subsidyModel = new SubsidyModel
                {
                    Id = subsidy.Id,
                    StoreId = searchModel.StoreId,
                    Name = subsidy.Name,
                    StartDate = subsidy.DurationStart.ToShortDateString(),
                    EndDate = subsidy.DurationEnd.ToShortDateString(),
                    AllocatedStaff = subsidy.PackNameCount,
                    Active = subsidy.Active,
                    Credit = subsidy.Credit,
                    TotalCredit = subsidy.Credit * subsidy.PackNameCount,
                    UsedPercent = usage == null
                        ? FormatUsedPercent(0, 0, 0, 0)
                        : FormatUsedPercent(usage.TotalCredit, usage.UsedCredit, usage.TotalQuantity, usage.UsedQuantity)
                };

                return subsidyModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare subsidy model
    /// </summary>
    /// <param name="model">Subsidy model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy model
    /// </returns>
    public virtual async Task<SubsidyModel> PrepareSubsidyModelAsync(SubsidyModel model, int storeId, int? subsidyId)
    {
        var subsidy = subsidyId.HasValue ? await _subsidyService.GetSubsidyByIdAsync(subsidyId.Value) : null;

        if (model == null)
        {
            model = subsidy != null
               ? subsidy.ToModel(new SubsidyModel { IsNew = false })
               : new SubsidyModel { Active = true, IsNew = true };
        }

        model.StoreId = storeId;

        if (subsidy != null)
        {
            PrepareSubsidyPackNameSearchModel(model.SubsidyPackNameSearchModel, subsidy?.Id ?? 0);
            PrepareSubsidyProductGroupSearchModel(model.SubsidyProductGroupSearchModel, subsidy?.Id ?? 0);
            await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(model.MultiStoreSettingsModel, storeId);
        }

        model.PrimaryStoreCurrencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId)).CurrencyCode;

        return model;
    }

    #endregion

    #region Subsidy - PackNames

    /// <summary>
    /// Prepare paged packName list model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName list model
    /// </returns>
    /// <remarks>
    /// The variation column lists only what the store lets a variation own. A credit override on a
    /// store where packnames share the global value, or dates on a store where they share the season,
    /// is stored but never applied, and showing it would tell the admin it applies.
    /// </remarks>
    public virtual async Task<SubsidyPackNameListModel> PrepareSubsidyPackNameListModelAsync(SubsidyPackNameSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var subsidy = await _subsidyService.GetSubsidyByIdAsync(searchModel.SubsidyId)
            ?? throw new InvalidOperationException($"Subsidy not found (id: {searchModel.SubsidyId}).");

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(subsidy.StoreId);

        var packNames = await _subsidyService.GetPackNamesBySubsidyIdAsync(searchModel.SubsidyId, pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        var usageByAssignment = (await _subsidyReportingService.GetSubsidyUsageBySubsidyIdsAsync(new List<int> { searchModel.SubsidyId }))
            .ToDictionary(usage => usage.SubsidyPackNameId);

        var currency = await _workContext.GetWorkingCurrencyAsync();
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        var creditTitle = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Credit");
        var durationStartTitle = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationStart");
        var durationEndTitle = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationEnd");

        //prepare grid model
        var model = await new SubsidyPackNameListModel().PrepareToGridAsync(searchModel, packNames, () =>
        {
            return packNames.SelectAwait(async packName =>
            {
                var usage = usageByAssignment.GetValueOrDefault(packName.Id);

                var packNameModel = new SubsidyPackNameModel
                {
                    Id = packName.Id,
                    SubsidyId = packName.SubsidyId,
                    PackNameId = packName.PackNameId,
                    Name = packName.PackName,
                    PrintName = packName.PrintName,
                    Branch = packName.Branch,
                    Department = packName.Department,
                    CostCenter = packName.CostCenter,
                    Active = packName.Active,
                    Gender = packName.Gender,
                    IsCustomer = packName.IsCustomer,
                    EmployeeNumber = packName.EmployeeNumber,
                    POEntry = packName.POEntry,
                    UsedPercent = usage == null
                        ? FormatUsedPercent(0, 0, 0, 0)
                        : FormatUsedPercent(usage.TotalCredit, usage.UsedCredit, usage.TotalQuantity, usage.UsedQuantity)
                };

                var variations = new List<string>();

                if (storeSettings.PackNamesOwnGlobalValue && packName.VariationCredit.HasValue)
                    variations.Add($"{creditTitle}: {await _priceFormatter.FormatPriceAsync(packName.VariationCredit.Value, true, currency, languageId, true, false)}");

                if (storeSettings.PackNamesOwnStartAndFinishDates && packName.VariationDurationStart.HasValue)
                    variations.Add($"{durationStartTitle}: {packName.VariationDurationStart.Value:d}");

                if (storeSettings.PackNamesOwnStartAndFinishDates && packName.VariationDurationEnd.HasValue)
                    variations.Add($"{durationEndTitle}: {packName.VariationDurationEnd.Value:d}");

                packNameModel.SubsidyVariation = string.Join("<br />", variations);

                return packNameModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare packName search model to add to the subsidy
    /// </summary>
    /// <param name="searchModel">PackName search model to add to the subsidy</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName search model to add to the subsidy
    /// </returns>
    public virtual async Task<AddPackNameToSubsidySearchModel> PrepareAddPackNameToSubsidySearchModelAsync(AddPackNameToSubsidySearchModel searchModel, int subsidyId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyId);

        searchModel.SubsidyId = subsidyId;
        searchModel.Subsidy = subsidy?.Name ?? string.Empty;

        //NO activeOnly: a search filter, not an assignment picker. See PackNameModelFactory:377.
        await _packNameModelFactory.PrepareAvailableDepartmentsAsync(searchModel.AvailableDepartments, subsidy?.StoreId ?? 0);
        await _packNameModelFactory.PrepareAvailableCostCentersAsync(searchModel.AvailableCostCenters, subsidy?.StoreId ?? 0);

        await _packNameModelFactory.PrepareAvailableGendersAsync(searchModel.AvailableGenders, null, true);

        await _packNameModelFactory.PrepareAvailablePackNameTypesAsync(searchModel.AvailablePackNameTypes);
        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(searchModel.MultiStoreSettingsModel, subsidy?.StoreId ?? 0);

        //prepare page parameters
        searchModel.SetPopupGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged packName list model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <param name="subsidy">Subsidy</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName list model
    /// </returns>
    public virtual async Task<PackNamesListModel> PreparePackNameListModelAsync(AddPackNameToSubsidySearchModel searchModel, Domain.Subsidies.Subsidy subsidy)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var packNames = await _subsidyService.GetPackNamesAsync(subsidy.StoreId, searchModel.SearchBranchName, searchModel.SearchPackName, searchModel.SearchEmail, searchModel.SearchPackNameType, searchModel.SearchGender, searchModel.WithoutSubsidy, searchModel.SearchDepartmentId, searchModel.SearchCostCenterId, pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = await new PackNamesListModel().PrepareToGridAsync(searchModel, packNames, () =>
        {
            return packNames.SelectAwait(async packName =>
            {
                var packNameModel = new PackNameModel
                {
                    Id = packName.Id,
                    Name = packName.Name,
                    PrintName = packName.PrintName,
                    Active = packName.Active,
                    Gender = packName.Gender,
                    IsCustomer = packName.CustomerId.HasValue,
                    Branch = packName.Branch,
                    Manager = packName.Manager,
                    EmployeeNumber = packName.EmployeeNumber,
                    POEntry = packName.POEntry,
                    Department = packName.Department,
                    CostCenter = packName.CostCenter
                };

                return packNameModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare subsidy packname variation model
    /// </summary>
    /// <param name="model">Subsidy packname variation model</param>
    /// <param name="variation">Variation</param>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packname variation
    /// </returns>
    public virtual async Task<SubsidyVariationModel> PrepareSubsidyVariationModelAsync(SubsidyVariationModel model, SubsidyVariation variation, int subsidyPackNameId)
    {
        if (variation != null)
        {
            //fill in model values from the entity
            if (model == null)
            {
                model = new SubsidyVariationModel
                {
                    Id = variation.Id,
                    Credit = variation.Credit,
                    DurationStart = variation.DurationStart,
                    DurationEnd = variation.DurationEnd,
                };
            }
        }

        model ??= new SubsidyVariationModel();

        var subsidyPackName = await _subsidyReportingService.GetSubsidyPackNameDetailsByIdAsync(subsidyPackNameId)
            ?? throw new InvalidOperationException($"Subsidy-PackName not found (id: {subsidyPackNameId}).");

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(subsidyPackName.StoreId);

        //set default values for the new model
        if (variation == null)
        {
            model.Credit = subsidyPackName.Credit;
            model.DurationStart = subsidyPackName.DurationStart;
            model.DurationEnd = subsidyPackName.DurationEnd;
            model.Reason = string.Empty;
        }

        model.SubsidyPackNameId = subsidyPackNameId;
        model.Subsidy = subsidyPackName.Subsidy;
        model.PackName = subsidyPackName.PackName;
        model.PackNamesOwnGlobalValue = storeSettings.PackNamesOwnGlobalValue;
        model.PackNamesOwnStartAndFinishDates = storeSettings.PackNamesOwnStartAndFinishDates;

        return model;
    }

    #endregion

    #region Subsidy - Product Groups

    /// <summary>
    /// Prepare paged subsidy product group list model
    /// </summary>
    /// <param name="searchModel">Product group search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list model
    /// </returns>
    public virtual async Task<SubsidyProductGroupListModel> PrepareSubsidyProductGroupListModelAsync(SubsidyProductGroupSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var productGroups = await _subsidyService.GetSubsidyProductGroupsAsync(searchModel.SubsidyId, pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = new SubsidyProductGroupListModel().PrepareToGrid(searchModel, productGroups, () =>
        {
            return productGroups.Select(productGroup =>
            {
                var productGroupModel = new SubsidyProductGroupModel
                {
                    Id = productGroup.Id,
                    SubsidyId = productGroup.SubsidyId,
                    ProductGroupId = productGroup.ProductGroupId,
                    Name = productGroup.Name,
                    Description = productGroup.Description,
                    Quantity = productGroup.Quantity,
                    Credit = productGroup.Credit,
                    Icon = productGroup.Icon,
                    Colour = productGroup.Colour,
                    DisplayOrder = productGroup.DisplayOrder
                };

                return productGroupModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare product group search model to add to the subsidy
    /// </summary>
    /// <param name="searchModel">Product group search model to add to the subsidy</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group search model to add to the subsidy
    /// </returns>
    public virtual async Task<AddProductGroupToSubsidySearchModel> PrepareAddProductGroupToSubsidySearchModelAsync(AddProductGroupToSubsidySearchModel searchModel, int subsidyId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyId);

        searchModel.SubsidyId = subsidyId;
        searchModel.Subsidy = subsidy?.Name ?? string.Empty;

        //prepare page parameters
        searchModel.SetPopupGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged product group list model
    /// </summary>
    /// <param name="searchModel">Product group search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list model
    /// </returns>
    public virtual async Task<ProductGroupListModel> PrepareProductGroupListModelAsync(AddProductGroupToSubsidySearchModel searchModel, Domain.Subsidies.Subsidy subsidy)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get store product groups
        var productGroups = await _subsidyService.GetProductGroupsByStoreIdAsync(subsidy.StoreId, subsidyId: subsidy.Id, name: searchModel.SearchName,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = new ProductGroupListModel().PrepareToGrid(searchModel, productGroups, () =>
        {
            return productGroups.Select(productGroup =>
            {
                var productGroupModel = new ProductGroupModel
                {
                    Id = productGroup.Id,
                    StoreId = productGroup.StoreId,
                    Name = productGroup.Name,
                    Description = productGroup.Description,
                    ProductCount = productGroup.ProductCount,
                    Active = productGroup.Active,
                    Icon = productGroup.Icon,
                    Colour = productGroup.Colour,
                    DisplayOrder = productGroup.DisplayOrder
                };

                return productGroupModel;
            });
        });

        return model;
    }

    #endregion

    #region PackName - Subsidies

    /// <summary>
    /// Prepare packaging name details model
    /// </summary>
    /// <param name="model">Packaging name model</param>
    /// <param name="packName">Packaging name</param>
    /// <param name="prefix">Prefix used to ensure unique ids for elements in the view</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name model
    /// </returns>
    public virtual async Task<PackNameSubsidiesModel> PreparePackNameSubsidiesModelAsync(PackNameSubsidiesModel model, PackName packName, string prefix)
    {
        if (packName == null)
            return new PackNameSubsidiesModel();

        var storeId = (await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId))?.StoreId ?? 0;

        //fill in model values from the entity
        if (model == null)
        {
            model = new PackNameSubsidiesModel
            {
                PackNameId = packName.Id,
            };
        }

        model.StoreId = storeId;
        model.Prefix = prefix ?? string.Empty;

        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(model.MultiStoreSettingsModel, storeId);

        await PreparePackNameSubsidiesModelAsync(model, packName.Id);

        return model;
    }

    /// <summary>
    /// Prepare subsidy topup model
    /// </summary>
    /// <param name="model">Subsidy topup model</param>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy topup model
    /// </returns>
    /// <remarks>
    /// No <c>topup</c> argument — see
    /// <see cref="PrepareProductGroupTopupModelAsync"/>. The <c>ToModel</c> call went with it: it
    /// rebuilt the model from the entity and then overwrote <c>Credit</c> and <c>Reason</c>, the only
    /// two fields the form uses, so the single value it carried through was <c>Id</c>, which no save
    /// path reads.
    /// </remarks>
    public virtual async Task<SubsidyTopupModel> PrepareSubsidyTopupModelAsync(SubsidyTopupModel model, int subsidyPackNameId)
    {
        ArgumentNullException.ThrowIfNull(model);

        var subsidyPackName = await _subsidyReportingService.GetSubsidyPackNameBasicInformationByIdAsync(subsidyPackNameId)
            ?? throw new InvalidOperationException($"Subsidy-PackName not found (id: {subsidyPackNameId}).");

        model.SubsidyPackNameId = subsidyPackNameId;
        model.Subsidy = subsidyPackName.Subsidy;
        model.PackName = subsidyPackName.PackName;
        model.Reason = string.Empty;
        model.Credit = 0;

        return model;
    }

    /// <summary>
    /// Prepare product group topup model
    /// </summary>
    /// <param name="model">Product group topup model</param>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <param name="productGroupId">Product group identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group topup model
    /// </returns>
    /// <remarks>
    /// No <c>topup</c> argument. It was used only to set <c>model.Id</c> from the
    /// period's first row, and no save path reads <c>model.Id</c> — the form submits an amount to ADD,
    /// which is why <c>Quantity</c> and <c>Credit</c> are reset to 0 below rather than bound from an
    /// entity. Since top-ups became insert-only there is no single "period row" to point at anyway.
    /// </remarks>
    public virtual async Task<ProductGroupTopupModel> PrepareProductGroupTopupModelAsync(ProductGroupTopupModel model, int subsidyPackNameId, int productGroupId)
    {
        ArgumentNullException.ThrowIfNull(model);

        var subsidyPackName = await _subsidyReportingService.GetSubsidyPackNameBasicInformationByIdAsync(subsidyPackNameId)
            ?? throw new InvalidOperationException($"Subsidy-PackName not found (id: {subsidyPackNameId}).");

        var productGroup = await _subsidyService.GetProductGroupByIdAsync(productGroupId);

        model.SubsidyPackNameId = subsidyPackNameId;
        model.ProductGroupId = productGroupId;
        model.ProductGroup = productGroup?.Name;
        model.PackName = subsidyPackName.PackName;
        model.Reason = string.Empty;
        model.Quantity = 0;
        model.Credit = 0;

        return model;
    }

    /// <summary>
    /// Prepare paged packname subsidy product group list model
    /// </summary>
    /// <param name="searchModel">Subsidy packaging name product group search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list model
    /// </returns>
    /// <remarks>
    /// Reads the same per-group figures the panel's totals are summed from, so the grid and the row
    /// above it can never disagree. The list is short (a subsidy's product groups), so it is paged in
    /// memory.
    /// </remarks>
    public virtual async Task<PackNameSubsidyProductGroupListModel> PreparePackNameSubsidyProductGroupListModelAsync(PackNameSubsidyProductGroupSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var subsidyPackName = await _subsidyService.GetSubsidyPackNameByIdAsync(searchModel.SubsidyPackNameId) ?? throw new InvalidOperationException($"Subsidy-PackName not found (id: {searchModel.SubsidyPackNameId}).");
        var details = await _subsidyReportingService.GetSubsidyPackNameProductGroupDetailsAsync(searchModel.SubsidyPackNameId, subsidyPackName.SubsidyId);
        var productGroups = new PagedList<SubsidyProductGroupDTO>(details, searchModel.Page - 1, searchModel.PageSize);

        //prepare grid model
        var model = new PackNameSubsidyProductGroupListModel().PrepareToGrid(searchModel, productGroups, () =>
        {
            return productGroups.Select(productGroup =>
            {
                var productGroupModel = new PackNameSubsidyProductGroupModel
                {
                    Id = productGroup.Id,
                    SubsidyId = productGroup.SubsidyId,
                    SubsidyPackNameId = searchModel.SubsidyPackNameId,
                    ProductGroupId = productGroup.ProductGroupId,
                    Name = productGroup.Name,
                    Description = productGroup.Description,
                    Quantity = productGroup.Quantity,
                    Credit = productGroup.Credit,
                    TopupQuantity = productGroup.TopupQuantity,
                    TopupCredit = productGroup.TopupCredit,
                    UsedQuantity = productGroup.UsedQuantity + productGroup.UsedQuantityTopup,
                    UsedCredit = productGroup.UsedCredit + productGroup.UsedCreditTopup,
                    Icon = productGroup.Icon,
                    Colour = productGroup.Colour,
                    DisplayOrder = productGroup.DisplayOrder,
                };

                return productGroupModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare paged packname subsidy history list model
    /// </summary>
    /// <param name="searchModel">Subsidy packaging name history search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the history list model
    /// </returns>
    public virtual async Task<PackNameSubsidyHistoryListModel> PreparePackNameSubsidyHistoryListModelAsync(PackNameSubsidyProductGroupSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var subsidyPackName = await _subsidyService.GetSubsidyPackNameByIdAsync(searchModel.SubsidyPackNameId) ?? throw new InvalidOperationException($"Subsidy-PackName not found (id: {searchModel.SubsidyPackNameId}).");

        var history = await _subsidyReportingService.GetSubsidyPackNameHistoryAsync(subsidyPackName.PackNameId, subsidyPackName.SubsidyId, pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        var currency = await _workContext.GetWorkingCurrencyAsync();
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        //prepare grid model
        var model = await new PackNameSubsidyHistoryListModel().PrepareToGridAsync(searchModel, history, () =>
        {
            return history.SelectAwait(async item =>
            {
                var historyModel = new PackNameSubsidyHistoryModel
                {
                    OrderId = item.OrderId,
                    OrderIdText = item.OrderId > 0 ? item.OrderId.ToString() : string.Empty,
                    HistoryTypeId = item.HistoryTypeId,
                    AssignmentRemovedText = item.AssignmentRemoved
                        ? await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.AssignmentRemoved")
                        : string.Empty,
                    ProductGroup = item.ProductGroup,
                    Quantity = item.Quantity,
                    QuantityTopup = item.QuantityTopup,
                    CreatedOnFormatted = (await _dateTimeHelper.ConvertToUserTimeAsync(item.CreatedOn, DateTimeKind.Utc)).ToString("dd/MM/yyyy h:mm tt"),
                    CreditFormatted = await _priceFormatter.FormatPriceAsync(item.Credit, true, currency, languageId, true, false),
                    CreditTopupFormatted = await _priceFormatter.FormatPriceAsync(item.CreditTopup, true, currency, languageId, true, false),
                    ProductGroupCreditFormatted = await _priceFormatter.FormatPriceAsync(item.ProductGroupCredit, true, currency, languageId, true, false),
                    ProductGroupCreditTopupFormatted = await _priceFormatter.FormatPriceAsync(item.ProductGroupCreditTopup, true, currency, languageId, true, false),
                    CreatorName = item.CreatorName,
                };

                switch (item.HistoryType)
                {
                    case HistoryType.Subsidy:
                        historyModel.Reason = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.Subsidy");
                        break;
                    case HistoryType.ProductGroup:
                        historyModel.Reason = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.ProductGroup");
                        break;
                    case HistoryType.SubsidyUsed:
                        historyModel.Reason = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.SubsidyUsed");
                        break;
                    case HistoryType.ProductGroupUsed:
                        historyModel.Reason = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.ProductGroupUsed");
                        break;
                    case HistoryType.SubsidyUsedForShipping:
                        historyModel.Reason = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.SubsidyUsedForShipping");
                        break;
                    case HistoryType.Refund:
                        historyModel.Reason = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackName.Subsidies.Subsidy.History.Field.Reason.Refund"), item.OrderId);
                        break;
                    default:
                        historyModel.Reason = item.Reason;
                        break;
                }

                return historyModel;
            });
        });

        return model;
    }

    #endregion

    #endregion
}