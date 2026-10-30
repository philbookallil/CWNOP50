using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

public class SubsidyController : BaseAdminController
{
    #region Fields

    protected readonly ISubsidyService _subsidyService;
    protected readonly ISubsidyModelFactory _subsidyModelFactory;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IProductService _productService;
    protected readonly IWorkContext _workContext;
    protected readonly IPackNameService _packNameService;
    protected readonly ISettingService _settingService;
    protected readonly ICWFeatureGuard _cwFeatureGuard;
    protected readonly IStoreMappingService _storeMappingService;

    #endregion

    #region Ctor

    public SubsidyController(ISubsidyService subsidyService,
        ISubsidyModelFactory subsidyModelFactory,
        ICustomerActivityService customerActivityService,
        ILocalizationService localizationService,
        IProductService productService,
        IWorkContext workContext,
        IPackNameService packNameService,
        ISettingService settingService,
        ICWFeatureGuard cwFeatureGuard,
        IStoreMappingService storeMappingService)
    {
        _subsidyService = subsidyService;
        _subsidyModelFactory = subsidyModelFactory;
        _customerActivityService = customerActivityService;
        _localizationService = localizationService;
        _productService = productService;
        _workContext = workContext;
        _packNameService = packNameService;
        _settingService = settingService;
        _cwFeatureGuard = cwFeatureGuard;
        _storeMappingService = storeMappingService;
    }

    #endregion

    #region Methods

    #region Product Groups

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> ProductGroups(int storeId)
    {
        //prepare model
        var model = _subsidyModelFactory.PrepareProductGroupSearchModel(new ProductGroupSearchModel(), storeId: storeId);

        return PartialView("_ProductGroups", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> ProductGroupList(ProductGroupSearchModel searchModel)
    {
        //prepare model
        var model = await _subsidyModelFactory.PrepareProductGroupListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<IActionResult> ProductGroupDetail(int storeId, int? productGroupId)
    {
        var model = await _subsidyModelFactory.PrepareProductGroupModelAsync(null, storeId, productGroupId);
        return PartialView("_ProductGroups.ProductGroupDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<IActionResult> SaveProductGroup(ProductGroupModel model)
    {
        if (ModelState.IsValid)
        {
            //The ENTITY'S ID decides, not the posted IsNew flag. Here a forged id creates a
            //spurious row rather than relocating one, because nop's identity insert discards it, but
            //the two save actions should not differ on this and neither should rely on that.
            if (model.Id == 0)
            {
                var productGroup = model.ToEntity<ProductGroup>();
                productGroup.Id = 0;
                productGroup.CreatedOnUtc = DateTime.UtcNow;

                //Explicit: the mapper no longer maps StoreId.
                productGroup.StoreId = model.StoreId;

                await _subsidyService.InsertProductGroupAsync(productGroup);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewProductGroup",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewProductGroup"), productGroup.Id), productGroup);

                return Json(new
                {
                    success = true,
                    id = productGroup.Id,
                    name = productGroup.Name,
                });
            }
            else
            {
                var productGroup = await _subsidyService.GetProductGroupByIdAsync(model.Id)
                    ?? throw new ArgumentException("No product group found with the specified id");

                //The entity's store wins; a mismatch is refused, not reconciled.
                if (productGroup.StoreId != model.StoreId)
                    return new ForbidResult();

                productGroup = model.ToEntity(productGroup);

                await _subsidyService.UpdateProductGroupAsync(productGroup);

                //activity log
                await _customerActivityService.InsertActivityAsync("EditProductGroup",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditProductGroup"), productGroup.Id), productGroup);

                return Json(new
                {
                    success = true,
                    isNew = false,
                    id = productGroup.Id,
                    name = productGroup.Name,
                });
            }
        }

        // Re-populate display-only fields lost in the POST
        model = await _subsidyModelFactory.PrepareProductGroupModelAsync(model, model.StoreId, model.Id);

        return PartialView("_ProductGroups.ProductGroupDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> DeleteProductGroup(int id)
    {
        //try to get a product group with the specified id
        var productGroup = await _subsidyService.GetProductGroupByIdAsync(id);
        if (productGroup == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.Subsidy, productGroup.StoreId, Request) is IActionResult blocked)
            return blocked;

        await _subsidyService.DeleteProductGroupAsync(productGroup);

        //activity log
        await _customerActivityService.InsertActivityAsync("DeleteProductGroup",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteProductGroup"), productGroup.Id), productGroup);

        return Json(new { success = true });
    }

    #endregion

    #region Product Group - Products

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.ProductGroup, ParameterName = "ProductGroupId")]
    public virtual async Task<IActionResult> ProductGroupProducts(ProductGroupProductSearchModel searchModel)
    {
        //prepare model
        var model = await _subsidyModelFactory.PrepareProductListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.ProductGroup)]
    public virtual async Task<IActionResult> ProductGroupProductPopup(int productGroupId)
    {
        //prepare model
        var model = await _subsidyModelFactory.PrepareAddProductToProductGroupSearchModelAsync(new AddProductToProductGroupSearchModel(), productGroupId);

        return View(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.ProductGroup, ParameterName = "ProductGroupId")]
    public virtual async Task<IActionResult> ProductGroupProductPopupList(AddProductToProductGroupSearchModel searchModel)
    {
        //try to get a product group with the specified id
        var productGroup = await _subsidyService.GetProductGroupByIdAsync(searchModel.ProductGroupId);
        if (productGroup == null)
            return new ForbidResult();

        //prepare model
        var model = await _subsidyModelFactory.PrepareAddProductListModelAsync(searchModel);

        return Json(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.ProductGroup, ParameterName = "ProductGroupId")]
    public virtual async Task<IActionResult> ProductGroupProductPopup(AddProductGroupProductModel model)
    {
        var productGroup = await _subsidyService.GetProductGroupByIdAsync(model.ProductGroupId)
            ?? throw new ArgumentException("No product group found with the specified id");

        var addedCount = 0;

        var selectedProducts = await _productService.GetProductsByIdsAsync(model.SelectedProductIds.ToArray());
        if (selectedProducts.Any())
        {
            var existingProducts = await _subsidyService.GetProductsByProductGroupIdAsync(model.ProductGroupId);

            foreach (var product in selectedProducts)
            {
                if (existingProducts.Where(p => p.ProductId == product.Id).Any())
                    continue;

                //PUBLISHED IS A SEPARATE TEST. AuthorizeAsync checks the store MAPPING and nothing else
                //- not Published, not Deleted - and it returns true outright when
                //CatalogSettings.IgnoreStoreLimitations is on or the product is not LimitedToStores,
                //which is the default for most catalogues. The picker has always filtered on Published
                //so without this line the POST accepts what the screen would never offer. Deleted is
                //already excluded by GetProductsByIdsAsync.
                if (!product.Published)
                    continue;

                if (!await _storeMappingService.AuthorizeAsync(product, productGroup.StoreId))
                    continue;

                await _subsidyService.InsertProductGroupProductAsync(new ProductGroupProduct
                {
                    ProductGroupId = model.ProductGroupId,
                    ProductId = product.Id
                });

                addedCount++;
            }
        }

        ViewBag.RefreshPage = true;

        //Inserted, not posted - the loop skips already-mapped, unpublished and store-unauthorised
        //products, so the posted count would overstate what was added.
        await _customerActivityService.InsertActivityAsync("AddProductGroupProduct",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddProductGroupProduct"), model.ProductGroupId, addedCount));

        return View(new AddProductToProductGroupSearchModel());
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.ProductGroup)]
    public virtual async Task<IActionResult> ProductGroupProductDelete(int id, int productGroupId)
    {
        //try to get a mapping
        var productGroupProduct = await _subsidyService.GetProductGroupProductByIdAsync(productGroupId, id);

        if (productGroupProduct == null)
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Product.NotFound"));

        await _subsidyService.DeleteProductGroupProductAsync(productGroupProduct);

        await _customerActivityService.InsertActivityAsync("DeleteProductGroupProduct",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteProductGroupProduct"), productGroupId, id));

        return new NullJsonResult();
    }

    #endregion

    #region Subsidies

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> Subsidies(int storeId)
    {
        //prepare model
        var model = _subsidyModelFactory.PrepareSubsidySearchModel(new SubsidySearchModel(), storeId: storeId);

        return PartialView("_Subsidies", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> SubsidyList(SubsidySearchModel searchModel)
    {
        //prepare model
        var model = await _subsidyModelFactory.PrepareSubsidyListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<IActionResult> SubsidyDetail(int storeId, int? subsidyId)
    {
        var model = await _subsidyModelFactory.PrepareSubsidyModelAsync(null, storeId, subsidyId);
        return PartialView("_Subsidies.SubsidyDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<IActionResult> SaveSubsidy(SubsidyModel model)
    {
        if (ModelState.IsValid)
        {
            //INSERT VS UPDATE IS DECIDED BY THE ENTITY'S ID, never by the posted IsNew flag.
            //SaveSubsidyAsync branches on subsidy.Id (SubsidyService:259) and the mapper copies Id from
            //the model, so posting IsNew = true with another store's subsidy id took the insert branch,
            //wrote the posted StoreId, and then UPDATED that subsidy - relocating it, with its packname
            //assignments left behind in the old store. The store guard on the edit branch never ran.
            if (model.Id == 0)
            {
                var subsidy = model.ToEntity<Subsidy>();

                //Belt and braces: the mapper copies Id, and an insert must not carry one.
                subsidy.Id = 0;

                //Explicit: the mapper no longer maps StoreId. On an insert the posted store
                //is the one the feature gate just validated, so it is self-consistent.
                subsidy.StoreId = model.StoreId;

                await _subsidyService.SaveSubsidyAsync(subsidy);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewSubsidy",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewSubsidy"), subsidy.Id), subsidy);
                return Json(new
                {
                    success = true,
                    id = subsidy.Id,
                    name = subsidy.Name,
                });
            }
            else
            {
                var subsidy = await _subsidyService.GetSubsidyByIdAsync(model.Id)
                    ?? throw new ArgumentException("No subsidy found with the specified id");

                //THE ENTITY'S STORE WINS, and a mismatch is refused rather than reconciled. The gate is
                //StoreSource.Parameter, i.e. the posted model, and the entity was fetched by id with no
                //scope - so posting a store-A subsidy's id with store B's StoreId passed the gate on B
                //and moved the subsidy there, carrying its packname assignments, which still belong to
                //A.
                if (subsidy.StoreId != model.StoreId)
                    return new ForbidResult();

                subsidy = model.ToEntity(subsidy);

                await _subsidyService.SaveSubsidyAsync(subsidy);

                //activity log
                await _customerActivityService.InsertActivityAsync("EditSubsidy",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditSubsidy"), subsidy.Id), subsidy);
                return Json(new
                {
                    success = true,
                    isNew = false,
                    id = subsidy.Id,
                    name = subsidy.Name,
                });
            }
        }

        // Re-populate display-only fields lost in the POST
        model = await _subsidyModelFactory.PrepareSubsidyModelAsync(model, model.StoreId, model.Id);

        return PartialView("_Subsidies.SubsidyDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> DeleteSubsidy(int id)
    {
        //try to get a subsidy with the specified id
        var subsidy = await _subsidyService.GetSubsidyByIdAsync(id)
            ?? throw new ArgumentException("No subsidy found with the specified id", nameof(id));

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.Subsidy, subsidy.StoreId, Request) is IActionResult blocked)
            return blocked;

        await _subsidyService.DeleteSubsidyAsync(subsidy);

        //activity log
        await _customerActivityService.InsertActivityAsync("DeleteSubsidy",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteSubsidy"), subsidy.Id), subsidy);
        return Json(new { success = true });
    }

    #endregion

    #region Subsidy - PackNames

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Subsidy, ParameterName = "SubsidyId")]
    public virtual async Task<IActionResult> SubsidyPackNames(SubsidyPackNameSearchModel searchModel)
    {
        //prepare model
        var model = await _subsidyModelFactory.PrepareSubsidyPackNameListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Subsidy)]
    public virtual async Task<IActionResult> SubsidyPackNamePopup(int subsidyId)
    {
        //prepare model
        var model = await _subsidyModelFactory.PrepareAddPackNameToSubsidySearchModelAsync(new AddPackNameToSubsidySearchModel(), subsidyId);

        return View(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Subsidy, ParameterName = "SubsidyId")]
    public virtual async Task<IActionResult> SubsidyPackNamePopupList(AddPackNameToSubsidySearchModel searchModel)
    {
        //try to get a subsidy with the specified id
        var subsidy = await _subsidyService.GetSubsidyByIdAsync(searchModel.SubsidyId);
        if (subsidy == null)
            return new ForbidResult();

        //prepare model
        var model = await _subsidyModelFactory.PreparePackNameListModelAsync(searchModel, subsidy);
        return Json(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Subsidy, ParameterName = "SubsidyId")]
    public virtual async Task<IActionResult> SubsidyPackNamePopup(AddPackNameToSubsidyModel model)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        var subsidy = await _subsidyService.GetSubsidyByIdAsync(model.SubsidyId)
            ?? throw new ArgumentException("No subsidy found with the specified id");

        //THE PICKER'S OWN QUERY, literally - _subsidyService.GetPackNamesAsync is what
        //PreparePackNameListModelAsync calls (SubsidyModelFactory:656). Re-deriving the set with
        //_packNameService's same-named method looked equivalent and is not: it adds !b.Deleted over a
        //LEFT-joined branch, so a list whose branch is missing or soft-deleted is shown by the picker
        //and silently dropped here, and the multi-select half-succeeds with no message.
        var selectablePackNames = await _subsidyService.GetPackNamesAsync(subsidy.StoreId, null, null, null, null, null, false, 0, 0);
        var selectableIds = selectablePackNames.Select(p => p.Id).ToHashSet();

        //get selected packNames
        var addedCount = 0;

        var selectedPackNames = await _packNameService.GetPackNamesByIdsAsync(model.SelectedPackNameIds.ToArray());
        if (selectedPackNames.Any())
        {
            var existingPackNames = await _subsidyService.GetSubsidyPackNamesBySubsidyIdAsync(model.SubsidyId);
            var existingIds = existingPackNames.Select(p => p.PackNameId).ToHashSet();

            foreach (var packName in selectedPackNames)
            {
                if (!selectableIds.Contains(packName.Id))
                    continue;

                //whether packname already exists
                if (!existingIds.Add(packName.Id)) // Add returns false if already present
                    continue;

                //insert the new packname subsidy mapping
                await _subsidyService.InsertSubsidyPackNameAsync(new SubsidyPackName
                {
                    SubsidyId = model.SubsidyId,
                    PackNameId = packName.Id,
                    CreatorId = customer.Id,
                    CreatedOnUtc = DateTime.UtcNow,
                    Active = true
                });

                addedCount++;
            }
        }

        ViewBag.RefreshPage = true;

        //Inserted, not posted - the loop drops ids outside the picker's own set and ones already
        //assigned. A subsidy assignment is money, so the count must be the real one.
        await _customerActivityService.InsertActivityAsync("AddSubsidyPackName",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddSubsidyPackName"), model.SubsidyId, addedCount));

        return View(new AddPackNameToSubsidySearchModel());
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> SubsidyPackNameDelete(int id)
    {
        //try to get a mapping
        var subsidyPackName = await _subsidyService.GetSubsidyPackNameByIdAsync(id);

        if (subsidyPackName == null)
            return new ForbidResult();

        var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyPackName.SubsidyId);

        if (subsidy == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.Subsidy, subsidy.StoreId, Request) is IActionResult blocked)
            return blocked;

        await _subsidyService.DeleteSubsidyPackNameAsync(subsidyPackName);

        await _customerActivityService.InsertActivityAsync("DeleteSubsidyPackName",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteSubsidyPackName"), subsidyPackName.SubsidyId, subsidyPackName.PackNameId));

        return new NullJsonResult();
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.SubsidyPackName)]
    public virtual async Task<IActionResult> SubsidyVariationEditPopup(int subsidyPackNameId)
    {
        //try to get a variation with the specified id
        var subsidyVariation = await _subsidyService.GetSubsidyVariationBySubsidyPacknameIdAsync(subsidyPackNameId);

        //prepare model
        var model = await _subsidyModelFactory.PrepareSubsidyVariationModelAsync(null, subsidyVariation, subsidyPackNameId);

        if (!model.PackNamesOwnStartAndFinishDates && !model.PackNamesOwnGlobalValue)
            return AccessDeniedView();

        return View(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.SubsidyPackName, ParameterName = "SubsidyPackNameId")]
    public virtual async Task<IActionResult> SubsidyVariationEditPopup(SubsidyVariationModel model)
    {
        //THE SETTINGS COME FROM THE ENTITY'S STORE, never from model.StoreId - which is a posted hidden
        //field while the feature gate validates the store behind SubsidyPackNameId. With the two free to
        //disagree, a store whose PackNamesOwnGlobalValue is OFF could still have a per-packname credit
        //override written for it, by posting the id of a store where it is ON; the row is inert until
        //that store enables the flag, and then it is live credit with nothing explaining it.
        //The GET of this action already reads the entity's store.
        var subsidyPackName = await _subsidyService.GetSubsidyPackNameByIdAsync(model.SubsidyPackNameId)
            ?? throw new ArgumentException("No subsidy packname mapping found with the specified id");

        var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyPackName.SubsidyId)
            ?? throw new ArgumentException("No subsidy found with the specified id");

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(subsidy.StoreId);

        if (!storeSettings.PackNamesOwnGlobalValue && !storeSettings.PackNamesOwnStartAndFinishDates)
            return AccessDeniedView();

        SubsidyVariation variation = null;

        if (ModelState.IsValid)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();

            if (model.Id > 0)
            {
                variation = await _subsidyService.GetSubsidyVariationByIdAsync(model.Id);

                //AND IT MUST BELONG TO THE MAPPING WE WERE GATED ON. GetSubsidyVariationByIdAsync is a
                //bare GetByIdAsync, so a posted Id let this action rewrite any packname's credit and
                //ordering dates, in any store, and file the history row against it.
                if (variation != null && variation.SubsidyPackNameId != model.SubsidyPackNameId)
                    return new ForbidResult();
            }

            if (variation == null)
            {
                variation = model.ToEntity<SubsidyVariation>();
                variation.CreatedOnUtc = DateTime.UtcNow;
                variation.SubsidyPackNameId = model.SubsidyPackNameId;

                if (!storeSettings.PackNamesOwnGlobalValue)
                    variation.Credit = null;

                if (!storeSettings.PackNamesOwnStartAndFinishDates)
                {
                    variation.DurationStart = null;
                    variation.DurationEnd = null;
                }

                await _subsidyService.InsertSubsidyVariationAsync(variation);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddSubsidyVariation",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddSubsidyVariation"), variation.Id), variation);
            }
            else
            {
                variation.Credit = storeSettings.PackNamesOwnGlobalValue ? model.Credit : null;
                variation.DurationStart = storeSettings.PackNamesOwnStartAndFinishDates ? model.DurationStart : null;
                variation.DurationEnd = storeSettings.PackNamesOwnStartAndFinishDates ? model.DurationEnd : null;

                await _subsidyService.UpdateSubsidyVariationAsync(variation);

                //activity log
                await _customerActivityService.InsertActivityAsync("EditSubsidyVariation",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditSubsidyVariation"), variation.Id), variation);
            }

            var subsidyVariationHistory = new SubsidyVariationHistory
            {
                SubsidyVariationId = variation.Id,
                DurationStart = variation.DurationStart,
                DurationEnd = variation.DurationEnd,
                Credit = variation.Credit,
                Reason = model.Reason,
                CreatorId = customer.Id,
                CreatedOnUtc = DateTime.UtcNow
            };

            await _subsidyService.InsertSubsidyVariationHistoryAsync(subsidyVariationHistory);

            ViewBag.RefreshPage = true;

            return View(new SubsidyVariationModel());
        }

        //prepare model
        model = await _subsidyModelFactory.PrepareSubsidyVariationModelAsync(model, variation, model.SubsidyPackNameId);

        //if we got this far, something failed, redisplay form
        return View(model);
    }

    #endregion

    #region Subsidy - Product Groups

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Subsidy, ParameterName = "SubsidyId")]
    public virtual async Task<IActionResult> SubsidyProductGroups(SubsidyProductGroupSearchModel searchModel)
    {
        //prepare model
        var model = await _subsidyModelFactory.PrepareSubsidyProductGroupListModelAsync(searchModel);

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> SubsidyProductGroupUpdate(SubsidyProductGroupModel model)
    {
        if (!ModelState.IsValid)
            return ErrorJson(ModelState.SerializeErrors());

        var subsidyProductGroup = await _subsidyService.GetSubsidyProductGroupByIdAsync(model.Id);
        if (subsidyProductGroup == null)
            return new ForbidResult();

        var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyProductGroup.SubsidyId);
        if (subsidy == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.Subsidy, subsidy.StoreId, Request) is IActionResult blocked)
            return blocked;

        subsidyProductGroup.Quantity = model.Quantity;
        subsidyProductGroup.Credit = model.Credit;
        subsidyProductGroup.DisplayOrder = model.DisplayOrder;
        await _subsidyService.UpdateSubsidyProductGroupAsync(subsidyProductGroup);

        //activity log
        await _customerActivityService.InsertActivityAsync("EditSubsidyProductGroup",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditSubsidyProductGroup"), subsidyProductGroup.Id), subsidyProductGroup);

        return new NullJsonResult();
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> SubsidyProductGroupDelete(int id)
    {
        //try to get a product group with the specified id
        var subsidyProductGroup = await _subsidyService.GetSubsidyProductGroupByIdAsync(id);
        if (subsidyProductGroup == null)
            return new ForbidResult();

        var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyProductGroup.SubsidyId);
        if (subsidy == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.Subsidy, subsidy.StoreId, Request) is IActionResult blocked)
            return blocked;

        await _subsidyService.DeleteSubsidyProductGroupAsync(subsidyProductGroup);

        //activity log
        await _customerActivityService.InsertActivityAsync("DeleteSubsidyProductGroup",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteSubsidyProductGroup"), subsidyProductGroup.Id), subsidyProductGroup);
        return new NullJsonResult();
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Subsidy)]
    public virtual async Task<IActionResult> SubsidyProductGroupPopup(int subsidyId)
    {
        //prepare model
        var model = await _subsidyModelFactory.PrepareAddProductGroupToSubsidySearchModelAsync(new AddProductGroupToSubsidySearchModel(), subsidyId);

        return View(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Subsidy, ParameterName = "SubsidyId")]
    public virtual async Task<IActionResult> SubsidyProductGroupPopupList(AddProductGroupToSubsidySearchModel searchModel)
    {
        //try to get a subsidy with the specified id
        var subsidy = await _subsidyService.GetSubsidyByIdAsync(searchModel.SubsidyId);
        if (subsidy == null)
            return new ForbidResult();

        //prepare model
        var model = await _subsidyModelFactory.PrepareProductGroupListModelAsync(searchModel, subsidy);
        return Json(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Subsidy, ParameterName = "SubsidyId")]
    public virtual async Task<IActionResult> SubsidyProductGroupPopup(AddProductGroupToSubsidyModel model)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        var subsidy = await _subsidyService.GetSubsidyByIdAsync(model.SubsidyId)
            ?? throw new ArgumentException("No subsidy found with the specified id");

        if (model.SelectedProductGroupIds.Any())
        {
            var existingProductGroups = await _subsidyService.GetSubsidyProductGroupsBySubsidyIdAsync(model.SubsidyId);
            foreach (var productGroupId in model.SelectedProductGroupIds)
            {
                //whether product group already exists
                if (existingProductGroups.Any(c => c.ProductGroupId == productGroupId))
                    continue;

                var productGroup = await _subsidyService.GetProductGroupByIdAsync(productGroupId);

                //EXISTENCE IS NOT SCOPE. The picker list is store-scoped (GetProductGroupsByStoreIdAsync
                //with subsidy.StoreId); this POST checked only that the id resolved, so a group from
                //another store could be allocated to this subsidy and its products then drew this
                //store's credit.
                if (productGroup == null || productGroup.Deleted || productGroup.StoreId != subsidy.StoreId)
                    continue;

                //insert the new product group subsidy mapping
                var subsidyProductGroup = new SubsidyProductGroup
                {
                    SubsidyId = model.SubsidyId,
                    ProductGroupId = productGroupId,
                    Quantity = 0,
                    Credit = 0,
                    DisplayOrder = productGroup.DisplayOrder,
                    CreatorId = customer.Id,
                    CreatedOnUtc = DateTime.UtcNow
                };

                await _subsidyService.InsertSubsidyProductGroupAsync(subsidyProductGroup);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewSubsidyProductGroup",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewSubsidyProductGroup"), subsidyProductGroup.Id), subsidyProductGroup);
            }
        }

        ViewBag.RefreshPage = true;

        return View(new AddProductGroupToSubsidySearchModel());
    }

    #endregion

    #region PackName - Subsidies

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.PackName)]
    public virtual async Task<IActionResult> PackNameSubsidies(int packNameId, string prefix)
    {
        //try to get a packname with the specified id
        var packName = await _packNameService.GetPackNameByIdAsync(packNameId);

        //prepare model
        var model = await _subsidyModelFactory.PreparePackNameSubsidiesModelAsync(null, packName, prefix);

        return PartialView("_PackNameSubsidies", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.SubsidyPackName)]
    public virtual async Task<IActionResult> SubsidyTopupEditPopup(int subsidyPackNameId)
    {
        //Kept as the existence check even though the result is no longer read: an unknown
        //subsidyPackNameId must not render a top-up form.
        _ = await _subsidyService.GetSubsidyBySubsidyPackNameIdAsync(subsidyPackNameId) ?? throw new ArgumentException("Subsidy not found");

        //No "current topup" is read - see the product-group GET.
        var model = await _subsidyModelFactory.PrepareSubsidyTopupModelAsync(new SubsidyTopupModel(), subsidyPackNameId);

        return View(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.SubsidyPackName, ParameterName = "SubsidyPackNameId")]
    public virtual async Task<IActionResult> SubsidyTopupEditPopup(SubsidyTopupModel model)
    {
        SubsidyTopup subsidyTopup = null;

        var subsidy = await _subsidyService.GetSubsidyBySubsidyPackNameIdAsync(model.SubsidyPackNameId) ?? throw new ArgumentException("Subsidy not found");

        //A top-up outside the assignment's window would be stored and never counted: the credit
        //views only sum top-ups dated inside the period they report on.
        if (!await _subsidyService.IsTopupPeriodOpenAsync(model.SubsidyPackNameId, subsidy))
            ModelState.AddModelError(string.Empty, await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.PeriodClosed"));

        if (ModelState.IsValid)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();

            subsidyTopup = await _subsidyService.SaveSubsidyTopupForPeriodAsync(model.SubsidyPackNameId, model.Credit, subsidy, customer.Id, model.Reason);

            //activity log. The MAPPING id, not the topup row id: the storefront logged the mapping and
            //admin logged the row under one keyword and one resource, so the log could not be read
            //at all. And the amount SUBMITTED, not the balance after the add - which the old
            //second argument was.
            await _customerActivityService.InsertActivityAsync("EditSubsidyTopup",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditSubsidyTopup"), subsidyTopup.SubsidyPackNameId, model.Credit), subsidyTopup);
            ViewBag.RefreshPage = true;

            return View(new SubsidyTopupModel());
        }

        //prepare model
        model = await _subsidyModelFactory.PrepareSubsidyTopupModelAsync(model, model.SubsidyPackNameId);

        //if we got this far, something failed, redisplay form
        return View(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.SubsidyPackName)]
    public virtual async Task<IActionResult> ProductGroupTopupEditPopup(int subsidyPackNameId, int productGroupId)
    {
        var subsidy = await _subsidyService.GetSubsidyBySubsidyPackNameIdAsync(subsidyPackNameId) ?? throw new ArgumentException("Subsidy not found");

        //Allocated to THIS subsidy, or there is nothing to top up - see the POST.
        var allocated = await _subsidyService.GetSubsidyProductGroupsBySubsidyIdAsync(subsidy.Id);

        if (!allocated.Any(spg => spg.ProductGroupId == productGroupId))
            return new ForbidResult();

        //No "current topup" is read: the form submits an amount to ADD, top-ups are insert-only, and
        //the model's only use for the entity was an Id no save path reads.
        var model = await _subsidyModelFactory.PrepareProductGroupTopupModelAsync(new ProductGroupTopupModel(), subsidyPackNameId, productGroupId);

        return View(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.SubsidyPackName, ParameterName = "SubsidyPackNameId")]
    public virtual async Task<IActionResult> ProductGroupTopupEditPopup(ProductGroupTopupModel model)
    {
        ProductGroupTopup productGroupTopup = null;

        var subsidy = await _subsidyService.GetSubsidyBySubsidyPackNameIdAsync(model.SubsidyPackNameId) ?? throw new ArgumentException("Subsidy not found");

        //THE PRODUCT GROUP MUST BE ALLOCATED TO THIS SUBSIDY. The feature gate validates
        //SubsidyPackNameId and says nothing about ProductGroupId, which is a posted hidden field - so
        //a topup could be written for any group, in any store. It grants nothing while the group is
        //unallocated and then activates the moment someone allocates it, with no history an admin
        //would look at.
        var allocated = await _subsidyService.GetSubsidyProductGroupsBySubsidyIdAsync(subsidy.Id);

        if (!allocated.Any(spg => spg.ProductGroupId == model.ProductGroupId))
            return new ForbidResult();

        //Same window rule as the subsidy top-up above.
        if (!await _subsidyService.IsTopupPeriodOpenAsync(model.SubsidyPackNameId, subsidy))
            ModelState.AddModelError(string.Empty, await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.PeriodClosed"));

        if (ModelState.IsValid)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();

            productGroupTopup = await _subsidyService.SaveProductGroupTopupForPeriodAsync(model.SubsidyPackNameId, model.ProductGroupId, model.Quantity, model.Credit, subsidy, customer.Id, model.Reason);

            //activity log. The MAPPING id, the group, and the amounts SUBMITTED - four arguments that
            //once went nowhere, because the resource had one placeholder; it now has four. The
            //storefront's call passes the same four, so one keyword no longer covers two id spaces.
            await _customerActivityService.InsertActivityAsync("EditProductGroupTopup",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditProductGroupTopup"),
                    productGroupTopup.SubsidyPackNameId, productGroupTopup.ProductGroupId, model.Quantity, model.Credit), productGroupTopup);

            ViewBag.RefreshPage = true;

            return View(new ProductGroupTopupModel());
        }

        //prepare model
        model = await _subsidyModelFactory.PrepareProductGroupTopupModelAsync(model, model.SubsidyPackNameId, model.ProductGroupId);
        //if we got this far, something failed, redisplay form
        return View(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.SubsidyPackName, ParameterName = "SubsidyPackNameId")]
    public virtual async Task<IActionResult> PackNameSubsidyProductGroupList(PackNameSubsidyProductGroupSearchModel searchModel)
    {
        //prepare model
        var model = await _subsidyModelFactory.PreparePackNameSubsidyProductGroupListModelAsync(searchModel);

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.SubsidyPackName, ParameterName = "SubsidyPackNameId")]
    public virtual async Task<IActionResult> PackNameSubsidyHistoryList(PackNameSubsidyProductGroupSearchModel searchModel)
    {
        //prepare model
        var model = await _subsidyModelFactory.PreparePackNameSubsidyHistoryListModelAsync(searchModel);

        return Json(model);
    }

    #endregion

    #endregion
}