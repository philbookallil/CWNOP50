using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Controllers;

[AutoValidateAntiforgeryToken]
[CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.CurrentStore)]
public class SubsidyCatalogController : BasePublicController
{
    #region Fields

    protected readonly IPackNameContext _packNameContext;
    protected readonly ISubsidyCatalogModelFactory _catalogModelFactory;
    protected readonly IWorkContext _workContext;
    protected readonly IStoreContext _storeContext;
    protected readonly ISubsidyService _subsidyService;
    protected readonly IPackNameService _packNameService;
    protected readonly ISettingService _settingService;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IMultiStoreService _multiStoreService;

    #endregion

    #region Ctor

    public SubsidyCatalogController(IPackNameContext packNameContext,
        ISubsidyCatalogModelFactory catalogModelFactory,
        IWorkContext workContext,
        IStoreContext storeContext,
        ISubsidyService subsidyService,
        IPackNameService packNameService,
        ISettingService settingService,
        ICustomerActivityService customerActivityService,
        ILocalizationService localizationService,
        IMultiStoreService multiStoreService)
    {
        _packNameContext = packNameContext;
        _catalogModelFactory = catalogModelFactory;
        _workContext = workContext;
        _storeContext = storeContext;
        _subsidyService = subsidyService;
        _packNameService = packNameService;
        _settingService = settingService;
        _customerActivityService = customerActivityService;
        _localizationService = localizationService;
        _multiStoreService = multiStoreService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The PACKNAME-level topup guard: a context packname exists, the current customer is a manager
    /// who controls it, and this store permits managers to edit topups.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two screens are packname-level rather than assignment-level — <c>ChangeTopup</c> loads the
    /// whole modal for the context packname and <c>GetTopups</c> returns its figures — so they have
    /// no <c>subsidyPackNameId</c> to validate and cannot use the overload below.
    /// </para>
    /// <para>
    /// <b>The ownership test matters here.</b> Without it this method would check only "is a
    /// manager" plus the settings, so a manager holding a context packname they do not control gets
    /// its topup panel and its balances. That is the same hole the assignment-level overload closes,
    /// and its checks do not close this one because these two actions never reach them.
    /// </para>
    /// </remarks>
    protected async Task<(PackName packName, bool authorized)> ValidateTopupAccessAsync()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return (null, false);

        // The current customer must themselves be a Manager PackName
        var managerPackName = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);
        if (!(managerPackName?.Manager ?? false))
            return (packName, false);

        // Settings gate
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

        //No feature test here: the class-level [CheckCWFeature] already blocks every action on this
        //controller when Subsidy is off for the current store.
        if (!settings.AllowEditSubsidyTopup || !settings.AllowManagersToAddPackNames)
            return (packName, false);

        // ...and the context packname must be one this manager actually controls.
        if (!await _multiStoreService.IsPackNameOwnedByManagerId(managerPackName.Id, packName.Id))
            return (packName, false);

        return (packName, true);
    }

    /// <summary>
    /// The ASSIGNMENT-level topup guard: everything the packname-level overload checks, plus that
    /// <paramref name="subsidyPackNameId"/> resolves to an assignment belonging to the context
    /// packname, whose subsidy exists and belongs to the current store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The posted <c>SubsidyPackNameId</c> is the whole attack surface here. This method once took
    /// no parameters and answered only "is a manager" plus the store settings, so any manager could
    /// add credit to any packname of any store by posting its id — and the activity log recorded it
    /// as an ordinary topup under their own name.
    /// </para>
    /// <para>
    /// The ownership tests are both needed and neither subsumes the other. The context comparison is
    /// what the SCREEN means: top up the employee being looked at. <c>IsPackNameOwnedByManagerId</c>,
    /// which the overload applies to the context packname, is what the MANAGER RELATIONSHIP means. A
    /// manager can hold a context packname they do not control, and a manager who
    /// controls a packname can still post a different one's id — so each test closes a hole the other
    /// leaves.
    /// </para>
    /// <para>
    /// Resolved objects come back from this call rather than being re-loaded by the caller: a guard
    /// that validates one row and hands the caller an id to load again is a guard the caller can
    /// accidentally step around.
    /// </para>
    /// </remarks>
    protected async Task<(SubsidyPackName mapping, Subsidy subsidy, PackName packName, bool authorized)> ValidateTopupAccessAsync(int subsidyPackNameId)
    {
        var (packName, authorized) = await ValidateTopupAccessAsync();

        if (!authorized)
            return (null, null, packName, false);

        // The posted assignment must exist...
        var mapping = await _subsidyService.GetSubsidyPackNameByIdAsync(subsidyPackNameId);

        if (mapping == null || mapping.Deleted || !mapping.Active)
            return (null, null, packName, false);

        // ...and belong to the packname this screen is showing.
        if (mapping.PackNameId != packName.Id)
            return (null, null, packName, false);

        // Its subsidy must exist and be this store's. The subsidy is loaded here rather than by the
        // caller because SaveSubsidyTopupForPeriodAsync dereferences it (AnnualRecurrence,
        // DurationEnd) and a deleted subsidy would otherwise reach it as null and throw.
        var store = await _storeContext.GetCurrentStoreAsync();
        var subsidy = await _subsidyService.GetSubsidyByIdAsync(mapping.SubsidyId);

        if (subsidy == null || subsidy.Deleted || !subsidy.Active || subsidy.StoreId != store.Id)
            return (mapping, null, packName, false);

        return (mapping, subsidy, packName, true);
    }

    /// <summary>
    /// Whether <paramref name="productGroupId"/> is one of the product groups allocated by
    /// <paramref name="subsidyId"/>.
    /// </summary>
    /// <remarks>
    /// The product-group topup carries a SECOND posted id, and nothing checked it.
    /// <c>SaveProductGroupTopupForPeriodAsync</c> writes a <c>ProductGroupTopup</c> row for whatever
    /// group id arrives, so a manager could create topup rows against groups the subsidy does not
    /// allocate. Those rows are orphans rather than free credit — the credit view joins
    /// <c>SubsidyProductGroup</c>, so an unmapped group surfaces nothing — but they are junk in a
    /// money ledger, and they become live the moment someone adds that group to the subsidy.
    /// </remarks>
    protected async Task<bool> IsProductGroupInSubsidyAsync(int subsidyId, int productGroupId)
    {
        if (productGroupId == 0)
            return false;

        var groups = await _subsidyService.GetSubsidyProductGroupsBySubsidyIdAsync(subsidyId);

        return groups.Any(spg => spg.ProductGroupId == productGroupId);
    }

    #endregion

    #region Methods

    // GET /SubsidyCatalog  (route: "Plugin.CW.Core.SubsidyCatalog")
    public async Task<IActionResult> Index(string viewMode = "grid")
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return RedirectToRoute("Homepage");

        //Validate whether the packname has at least one subsidy allocated
        var subsidies = await _subsidyService.GetAllSubsidyPackNamesByPackNameIdAsync(packName.Id);

        if (!subsidies.Any())
            return RedirectToRoute("Homepage");

        var model = await _catalogModelFactory.PrepareSubsidyCatalogModelAsync(packName, viewMode);

        return View(model);
    }

    // POST /SubsidyCatalog/Items  (AJAX — reloads product groups partial)
    public async Task<IActionResult> Items(string viewMode = "grid")
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareSubsidyCatalogModelAsync(packName, viewMode);

        return PartialView("_ProductGroups", model);
    }

    // GET /SubsidyCatalog/Bar  (AJAX — reloads subsidy bar)
    public async Task<IActionResult> Bar()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareSubsidyBarModelAsync(packName, flyout: false);
        return PartialView("~/Plugins/CW.Core/Views/Shared/Components/SubsidyBar/Default.cshtml", model);
    }

    // GET /SubsidyCatalog/BarFlyout
    public async Task<IActionResult> BarFlyout()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareSubsidyBarModelAsync(packName, flyout: true);
        return PartialView("~/Plugins/CW.Core/Views/Shared/Components/SubsidyBar/Flyout.cshtml", model);
    }

    // GET /SubsidyCatalog/ProductGroupHeaders  (AJAX — returns JSON dict: productGroupId → header HTML)
    public async Task<IActionResult> ProductGroupHeaders()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return Json(new Dictionary<int, string>());

        var barModel = await _catalogModelFactory.PrepareSubsidyBarModelAsync(packName, flyout: false);
        var result = new Dictionary<int, string>();

        foreach (var pg in barModel.ProductGroups)
        {
            var html = await RenderPartialViewToStringAsync("_ProductGroupHeader", pg);
            result[pg.Id] = html;
        }

        return Json(result);
    }

    /// <summary>
    /// Loads the Change Topup panel into the Bootstrap modal body via $.load().
    /// No packNameId parameter — resolved from PackName context.
    /// </summary>
    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> ChangeTopup()
    {
        var (packName, authorized) = await ValidateTopupAccessAsync();

        if (!authorized)
            return Content(string.Empty); // modal body stays blank; button was only shown to managers

        var model = await _catalogModelFactory.PreparePackNameTopupModelAsync(packName);

        return PartialView(model);
    }

    /// <summary>
    /// Returns lightweight topup values as JSON for in-modal DOM refresh.
    /// Called by loadTopUps() in ChangeTopup.cshtml after each successful save.
    /// </summary>
    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> GetTopups()
    {
        var (packName, authorized) = await ValidateTopupAccessAsync();

        if (!authorized)
            return Json(new { topups = Array.Empty<object>() });

        var topups = await _catalogModelFactory.GetPackNameTopupsAsync(packName.Id);

        return Json(new { topups });
    }

    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> ChangeTopupSubsidy(int subsidyPackNameId)
    {
        var (_, _, _, authorized) = await ValidateTopupAccessAsync(subsidyPackNameId);

        if (!authorized)
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareSubsidyTopupInputModelAsync(new SubsidyTopupInputModel(), subsidyPackNameId);

        return PartialView(model);
    }

    [HttpPost]
    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> ChangeTopupSubsidy(SubsidyTopupInputModel model)
    {
        var (mapping, subsidy, _, authorized) = await ValidateTopupAccessAsync(model.SubsidyPackNameId);

        if (!authorized)
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.ChangeTopup.AccessDenied") });

        //A top-up outside the assignment's window would be stored and never counted.
        if (!await _subsidyService.IsTopupPeriodOpenAsync(mapping.Id, subsidy))
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.ChangeTopup.PeriodClosed") });

        if (ModelState.IsValid)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();

            var subsidyTopup = await _subsidyService.SaveSubsidyTopupForPeriodAsync(subsidyPackNameId: mapping.Id, creditToAdd: model.Credit, subsidy: subsidy, creatorId: customer.Id, reason: model.Reason ?? string.Empty);

            await _customerActivityService.InsertActivityAsync("EditSubsidyTopup",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditSubsidyTopup"), subsidyTopup.SubsidyPackNameId, model.Credit), subsidyTopup);

            return Json(new { success = true });
        }

        // Re-prepare display fields on validation failure
        model = await _catalogModelFactory.PrepareSubsidyTopupInputModelAsync(model, model.SubsidyPackNameId);

        return Json(new
        {
            success = false,
            html = await RenderPartialViewToStringAsync("~/Plugins/CW.Core/Views/SubsidyCatalog/ChangeTopupSubsidy.cshtml", model)
        });
    }

    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> ChangeTopupProductGroup(int subsidyPackNameId, int productGroupId)
    {
        var (_, subsidy, _, authorized) = await ValidateTopupAccessAsync(subsidyPackNameId);

        if (!authorized)
            return Content(string.Empty);

        //The second posted id needs its own check - the group must be one this subsidy allocates.
        if (!await IsProductGroupInSubsidyAsync(subsidy.Id, productGroupId))
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareProductGroupTopupInputModelAsync(new ProductGroupTopupInputModel(), subsidyPackNameId, productGroupId);

        return PartialView(model);
    }

    [HttpPost]
    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> ChangeTopupProductGroup(ProductGroupTopupInputModel model)
    {
        var (mapping, subsidy, _, authorized) = await ValidateTopupAccessAsync(model.SubsidyPackNameId);

        if (!authorized)
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.ChangeTopup.AccessDenied") });

        if (!await IsProductGroupInSubsidyAsync(subsidy.Id, model.ProductGroupId))
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.ChangeTopup.AccessDenied") });

        //A top-up outside the assignment's window would be stored and never counted.
        if (!await _subsidyService.IsTopupPeriodOpenAsync(mapping.Id, subsidy))
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.ChangeTopup.PeriodClosed") });

        if (ModelState.IsValid)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();

            var productGroupTopup = await _subsidyService.SaveProductGroupTopupForPeriodAsync(subsidyPackNameId: mapping.Id, productGroupId: model.ProductGroupId, quantityToAdd: model.Quantity, creditToAdd: model.Credit, subsidy: subsidy, creatorId: customer.Id, reason: model.Reason ?? string.Empty);

            //THE SAME FOUR ARGUMENTS AS THE ADMIN CALLER, and in the same order: the mapping id, the
            //group, the quantity submitted and the credit submitted. One keyword and one resource
            //covered two id spaces - admin logged the topup row id, this logged it too while admin's
            //sibling logged the mapping - so the log could not be read. The resource has
            //four placeholders; passing fewer throws FormatException, which is a 500
            //inside this action with the topup row already inserted.
            await _customerActivityService.InsertActivityAsync("EditProductGroupTopup",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditProductGroupTopup"),
                    productGroupTopup.SubsidyPackNameId, productGroupTopup.ProductGroupId, model.Quantity, model.Credit), productGroupTopup);

            return Json(new { success = true });
        }

        model = await _catalogModelFactory.PrepareProductGroupTopupInputModelAsync(model, model.SubsidyPackNameId, model.ProductGroupId);

        return Json(new
        {
            success = false,
            html = await RenderPartialViewToStringAsync("~/Plugins/CW.Core/Views/SubsidyCatalog/ChangeTopupProductGroup.cshtml", model)
        });
    }

    #endregion
}