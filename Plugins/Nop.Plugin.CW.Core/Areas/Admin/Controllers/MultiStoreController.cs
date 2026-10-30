using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Stores;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Plugin.CW.Core.Background;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Extensions;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames.Bulk;
using Nop.Plugin.CW.Core.Services.PackNames.Import;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Shipping;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Attributes;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

public class MultiStoreController : BaseAdminController
{
    #region Fields

    protected readonly IStoreService _storeService;
    protected readonly IMultiStoreModelFactory _multiStoreModelFactory;
    protected readonly IBranchModelFactory _branchModelFactory;
    protected readonly IBranchService _branchService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ICustomerOverrideService _customerService;
    protected readonly IPackNameService _packNameService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IAddressService _addressService;
    protected readonly IAttributeParser<AddressAttribute, AddressAttributeValue> _addressAttributeParser;
    protected readonly ISettingService _settingService;
    protected readonly IBackgroundTaskQueue _taskQueue;
    protected readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly ILogger _logger;
    protected readonly IEventPublisher _eventPublisher;
    protected readonly INotificationService _notificationService;
    protected readonly IPackNameModelFactory _packNameModelFactory;
    protected readonly ICWSettingModelFactory _cwSettingModelFactory;
    protected readonly ICWAddressModelFactory _cwAddressModelFactory;
    protected readonly ISubsidyService _subsidyService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly ICWFeatureGuard _cwFeatureGuard;
    protected readonly IPackNameProvisioningService _packNameProvisioningService;
    protected readonly IPackNameShippingMethodService _packNameShippingMethodService;
    protected readonly IPackNameImportService _packNameImportService;
    protected readonly IPackNameBulkService _packNameBulkService;
    protected readonly ILanguageService _languageService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public MultiStoreController(IStoreService storeService,
        IMultiStoreModelFactory multiStoreModelFactory,
        IBranchModelFactory branchModelFactory,
        IBranchService branchService,
        IMultiStoreService multiStoreService,
        ICustomerActivityService customerActivityService,
        ILocalizationService localizationService,
        ICustomerOverrideService customerService,
        IPackNameService packNameService,
        IGenericAttributeService genericAttributeService,
        IAddressService addressService,
        IAttributeParser<AddressAttribute, AddressAttributeValue> addressAttributeParser,
        ISettingService settingService,
        IBackgroundTaskQueue taskQueue,
        IShoppingCartOverrideService shoppingCartService,
        ILogger logger,
        IEventPublisher eventPublisher,
        INotificationService notificationService,
        IPackNameModelFactory packNameModelFactory,
        ICWSettingModelFactory cwSettingModelFactory,
        ICWAddressModelFactory cwAddressModelFactory,
        ISubsidyService subsidyService,
        ICWFeatureService cwFeatureService,
        ICWFeatureGuard cwFeatureGuard,
        IPackNameProvisioningService packNameProvisioningService,
        IPackNameShippingMethodService packNameShippingMethodService,
        IPackNameImportService packNameImportService,
        IPackNameBulkService packNameBulkService,
        ILanguageService languageService,
        IWorkContext workContext)
    {
        _storeService = storeService;
        _multiStoreModelFactory = multiStoreModelFactory;
        _branchModelFactory = branchModelFactory;
        _branchService = branchService;
        _multiStoreService = multiStoreService;
        _customerActivityService = customerActivityService;
        _localizationService = localizationService;
        _customerService = customerService;
        _packNameService = packNameService;
        _genericAttributeService = genericAttributeService;
        _addressService = addressService;
        _addressAttributeParser = addressAttributeParser;
        _settingService = settingService;
        _taskQueue = taskQueue;
        _shoppingCartService = shoppingCartService;
        _logger = logger;
        _eventPublisher = eventPublisher;
        _notificationService = notificationService;
        _packNameModelFactory = packNameModelFactory;
        _cwSettingModelFactory = cwSettingModelFactory;
        _cwAddressModelFactory = cwAddressModelFactory;
        _subsidyService = subsidyService;
        _cwFeatureService = cwFeatureService;
        _cwFeatureGuard = cwFeatureGuard;
        _packNameProvisioningService = packNameProvisioningService;
        _packNameShippingMethodService = packNameShippingMethodService;
        _packNameImportService = packNameImportService;
        _packNameBulkService = packNameBulkService;
        _languageService = languageService;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The store a packname save belongs to, derived from the entity or from its posted parents.
    /// </summary>
    /// <remarks>
    /// <c>PackName</c> has no <c>StoreId</c>: it is packname → list → branch → store. On an edit the
    /// authority is the list the packname is in TODAY; on an insert it is the posted list, or the
    /// posted branch when the list is still to be created. Never <c>model.StoreId</c> — that is a
    /// hidden form field, and it was both the feature gate's input and the value written to the
    /// entity's parents.
    /// </remarks>
    protected virtual async Task<int?> ResolvePackNameStoreIdAsync(PackNameModel model, PackName packName)
    {
        if (packName != null)
            return (await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId))?.StoreId;

        if (model.PackNameListId > 0)
            return (await _packNameService.GetPackNameListByIdAsync(model.PackNameListId))?.StoreId;

        if (model.BranchId > 0)
            return (await _branchService.GetBranchByIdAsync(model.BranchId))?.StoreId;

        return null;
    }

    /// <summary>
    /// Server-side validation of a packname save's posted parents: list, customer, department, cost centre.
    /// </summary>
    /// <remarks>
    /// All four were enforced in JavaScript only: the branch picker filtered by
    /// store client-side, the list picker was repopulated by an AJAX call, and "this customer already
    /// has a packname" was an <c>alert()</c> in the popup. A hand-built post therefore attached a
    /// second packname to one customer - after which <c>GetPackNameByCustomerIdAsync</c> returns
    /// whichever the query orders first and that customer's storefront identity is nondeterministic -
    /// or put a packname in another store's list. <c>DepartmentId</c> additionally GRANTS ownership
    /// (the owned set matches <c>pn.DepartmentId</c> against the manager's departments), which is why
    /// it is checked here and not only in the storefront.
    /// </remarks>
    protected virtual async Task ValidatePackNameTargetAsync(PackNameModel model, PackName packName, int storeId)
    {
        if (model.PackNameListId > 0)
        {
            var list = await _packNameService.GetPackNameListByIdAsync(model.PackNameListId);

            if (list == null || list.Deleted || list.StoreId != storeId)
                ModelState.AddModelError(nameof(model.PackNameListId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.WrongStore"));
            else if (model.BranchId > 0 && list.BranchId != model.BranchId)
                ModelState.AddModelError(nameof(model.PackNameListId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.WrongBranch"));

            //a switched-off list takes no new packnames, as an inactive department takes no new assignments; a
            //packname already in it still saves
            else if (!list.Active && list.Id != packName?.PackNameListId)
                ModelState.AddModelError(nameof(model.PackNameListId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Inactive"));
        }

        //one packname per name in a list, for a new packname, a new name or a move: a packname saved again with its name
        //keeps it, even when another of the list has the same one (provisioning names packnames after customers
        //without the rule). A post without a list keeps the list the packname is in
        var targetListId = model.PackNameListId > 0 ? model.PackNameListId : packName?.PackNameListId ?? 0;
        var nameOrListChanged = packName == null || targetListId != packName.PackNameListId
            || !string.Equals(model.Name?.Trim(), packName.Name?.Trim(), StringComparison.OrdinalIgnoreCase);

        if (nameOrListChanged && await _packNameService.PackNameNameExistsAsync(targetListId, model.Name, packName?.Id ?? 0))
            ModelState.AddModelError(nameof(model.Name), string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Name.Exists"), model.Name));

        //a new packname, or one moved to another list, goes only to a branch that is active and not deleted, as on the
        //storefront form and in the import; a packname that stays where it is still saves
        if (packName == null || targetListId != packName.PackNameListId)
        {
            var targetBranchId = model.PackNameListId > 0
                ? (await _packNameService.GetPackNameListByIdAsync(model.PackNameListId))?.BranchId ?? 0
                : model.BranchId;
            var targetBranch = targetBranchId > 0 ? await _branchService.GetBranchByIdAsync(targetBranchId) : null;

            if (targetBranch != null && (!targetBranch.Active || targetBranch.Deleted))
                ModelState.AddModelError(model.PackNameListId > 0 ? nameof(model.PackNameListId) : nameof(model.BranchId),
                    await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Branch.Inactive"));
        }

        if (model.CustomerId.GetValueOrDefault() > 0)
        {
            var linked = await _packNameService.GetPackNameByCustomerIdAsync(model.CustomerId.Value);

            //THE ENTITY'S ID, not model.Id. model.Id is a hidden input, so comparing against it let an
            //INSERT post the id of the packname that customer already owns, match itself, and skip the
            //check entirely - two packnames on one customer, after which GetPackNameByCustomerIdAsync
            //returns whichever the query orders first. packName is null on an insert, which is why the
            //null-coalesce is 0 and not model.Id.
            if (linked != null && linked.Id != (packName?.Id ?? 0))
                ModelState.AddModelError(nameof(model.CustomerId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer.AlreadyHasPackName"));

            //A customer with a branch must be in this store's branch tree. A customer with no branch
            //row - or a row whose BranchId is null - is allowed: that is the ordinary case for a
            //customer created outside the branch flow. CustomerBranch.BranchId is int?, so the row
            //existing is not the same as a branch being assigned.
            var customerBranch = await _customerService.GetCustomerBranchByIdAsync(model.CustomerId.Value);

            if (customerBranch?.BranchId > 0)
            {
                var branch = await _branchService.GetBranchByIdAsync(customerBranch.BranchId.Value);

                if (branch != null && branch.StoreId != storeId)
                    ModelState.AddModelError(nameof(model.CustomerId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer.WrongStore"));
            }
        }

        if (model.DepartmentId.GetValueOrDefault() > 0)
        {
            var department = await _multiStoreService.GetDepartmentByIdAsync(model.DepartmentId.Value);

            if (department == null || department.Deleted || department.StoreId != storeId)
                ModelState.AddModelError(nameof(model.DepartmentId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Department.WrongStore"));

            //INACTIVE IS REFUSED ONLY WHEN IT IS A CHANGE. Active means "no new assignments", so a
            //packname already on a deactivated department must still save - otherwise deactivating a
            //department freezes every packname on it and even renaming an employee fails. packName is
            //null on an insert, so every insert onto an inactive department is a new assignment.
            else if (!department.Active && department.Id != packName?.DepartmentId)
                ModelState.AddModelError(nameof(model.DepartmentId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Department.Inactive"));
        }

        if (model.CostCenterId.GetValueOrDefault() > 0)
        {
            var costCenter = await _multiStoreService.GetCostCenterByIdAsync(model.CostCenterId.Value);

            if (costCenter == null || costCenter.Deleted || costCenter.StoreId != storeId)
                ModelState.AddModelError(nameof(model.CostCenterId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.CostCenter.WrongStore"));

            //Same rule as the department above.
            else if (!costCenter.Active && costCenter.Id != packName?.CostCenterId)
                ModelState.AddModelError(nameof(model.CostCenterId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.CostCenter.Inactive"));
        }
    }

    protected async Task ValidateManagersMapping(PackNameList packNameList)
    {
        if (packNameList.AllManagers)
        {
            var currentManagers = await _packNameService.GetPackNameListManagersAsync(packNameList.Id);

            foreach (var manager in currentManagers)
            {
                await _packNameService.DeletePackNameListManagerAsync(manager);
            }

            return;
        }
    }

    protected void ClearSelectedShippingAddresses(int storeId)
    {
        _taskQueue.QueueBackgroundWorkItem(async token =>
        {
            try
            {
                await _shoppingCartService.ClearSelectedShippingAddressesAsync(storeId);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error on clearing selected shipping addresses", ex);
            }
        });
    }

    protected async Task GeneratePackNamesBackgroundAsync(int storeId, PackNameListNameFrom nameFrom)
    {
        var branches = await _branchService.GetAllBranchesByStoreIdAsync(storeId);

        foreach (var branch in branches)
        {
            var packNameList = await _packNameProvisioningService.EnsureDefaultPackNameListAsync(branch, nameFrom);

            await _packNameProvisioningService.CreatePackNamesForCustomersWithoutOneAsync(packNameList);
        }
    }

    protected async Task GeneratePackNameListsBackgroundAsync(int storeId, PackNameListNameFrom nameFrom)
    {
        var branches = await _branchService.GetAllBranchesByStoreIdAsync(storeId);

        foreach (var branch in branches)
            await _packNameProvisioningService.EnsureDefaultPackNameListAsync(branch, nameFrom);
    }

    protected static List<BranchNodeModel> BuildTree(IList<Branch> allBranches, int? parentId, int depth)
    {
        return allBranches
            .Where(b => b.ParentBranchId == parentId)
            .OrderBy(b => b.Name)
            .Select(b => new BranchNodeModel
            {
                Id = b.Id,
                Name = b.Name,
                Code = b.BranchCode,
                ParentId = b.ParentBranchId,
                Depth = depth,
                Active = b.Active,
                HasChildren = allBranches.Any(x => x.ParentBranchId == b.Id),
                Children = BuildTree(allBranches, b.Id, depth + 1)
            })
            .ToList();
    }

    /// <summary>
    /// The language of the welcome e-mails an administrator's import queues: the store's default language when it has
    /// a published one, otherwise the administrator's own
    /// </summary>
    /// <param name="store">The store the packnames are imported into</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the language identifier
    /// </returns>
    protected virtual async Task<int> ResolveImportLanguageIdAsync(Store store)
    {
        var language = store.DefaultLanguageId > 0 ? await _languageService.GetLanguageByIdAsync(store.DefaultLanguageId) : null;

        if (language?.Published == true)
            return language.Id;

        return (await _workContext.GetWorkingLanguageAsync()).Id;
    }

    #endregion

    #region Methods

    #region Stores

    public virtual IActionResult Index()
    {
        return RedirectToAction("List");
    }

    [CheckPermission([CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE, CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE_SETTINGS])]
    public virtual async Task<IActionResult> List()
    {
        //prepare model
        var model = await _multiStoreModelFactory.PrepareMultiStoreSearchModelAsync(new MultiStoreSearchModel());

        return View(model);
    }

    [HttpPost]
    [CheckPermission([CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE, CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE_SETTINGS, CWPermissionConfigManager.ADMIN_ACCESS_BRANCH])]
    public virtual async Task<IActionResult> StoreList(MultiStoreSearchModel searchModel)
    {
        //prepare model
        var model = await _multiStoreModelFactory.PrepareStoreListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission([CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE, CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE_SETTINGS])]
    public virtual async Task<IActionResult> Edit(int id)
    {
        //try to get a store with the specified id
        var store = await _storeService.GetStoreByIdAsync(id);
        if (store == null)
            return RedirectToAction("Index", "Admin", new { area = AreaNames.ADMIN });

        //prepare model
        var model = await _multiStoreModelFactory.PrepareMultiStoreModelAsync(null, store);

        return View(model);
    }

    #endregion

    #region Breadcrum

    [CheckPermission([CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE, CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE_SETTINGS])]
    //PackName is the base gate: CWFeatureSet.From makes Subsidy depend on PackNameEnable, so a store
    //with subsidies always has PackName on. The attribute also refuses storeId = 0 for free
    //(CheckCWFeatureFilter.IsAllowedAsync), which this action has no meaning for.
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<IActionResult> BreadcrumbData(
        int storeId,
        [FromQuery] int[] branchIds,
        string listId = null,
        string packNameId = null,
        string costCenterId = null,
        string departmentId = null,
        string productGroupId = null,
        string subsidyId = null)
    {
        var items = new List<BreadcrumbItem>();

        //EVERY ID IS RESOLVED WITH ITS CHAIN. Before this, each id was resolved with a
        //bare GetByIdAsync and its name returned, so one GET enumerated every branch, list, packname,
        //cost centre, department, product group and subsidy in EVERY store. Scope does not cross - the
        //names leak, not the data - so this is hygiene, but it is a complete enumeration oracle and it
        //costs one comparison per id to close.
        //The rule: an entity is named only if it belongs to the requested store, and a packname only
        //if its list does. Anything that fails is OMITTED, not reported - telling the caller "that id
        //exists but is not yours" is the same oracle one step removed.
        var store = await _storeService.GetStoreByIdAsync(storeId);
        if (store == null)
            return NotFound();

        items.Add(new BreadcrumbItem
        {
            Type = "store",
            Id = store.Id.ToString(),
            Name = store.Name
        });

        //Branches, in order, and each one must be in the store AND be the child of the one before it.
        //The store check alone would still let a caller assemble a fake ancestry out of unrelated
        //branches; the parent check is what makes the trail a real path.
        int? expectedParentId = null;

        foreach (var branchId in branchIds ?? [])
        {
            var branch = await _branchService.GetBranchByIdAsync(branchId);

            if (branch == null || branch.Deleted || branch.StoreId != storeId)
                break;

            if (expectedParentId.HasValue && branch.ParentBranchId != expectedParentId)
                break;

            items.Add(new BreadcrumbItem
            {
                Type = "branch",
                Id = branch.Id.ToString(),
                Name = branch.Name
            });

            expectedParentId = branch.Id;
        }

        //Local helpers: parse, and resolve-if-in-store. "0" means "new <thing>" on every type and is
        //not an id, so it never reaches a lookup.
        static int? ParseId(string raw)
            => !string.IsNullOrEmpty(raw) && int.TryParse(raw, out var parsed) ? parsed : null;

        void AddNew(string type, string name)
            => items.Add(new BreadcrumbItem { Type = type, Id = "0", Name = name });

        void AddResolved(string type, int id, string name)
            => items.Add(new BreadcrumbItem { Type = type, Id = id.ToString(), Name = name });

        //PackName list
        var listIdValue = ParseId(listId);
        PackNameList resolvedList = null;

        if (listIdValue.HasValue && listIdValue.Value != 0)
        {
            var list = await _packNameService.GetPackNameListByIdAsync(listIdValue.Value);

            if (list != null && !list.Deleted && list.StoreId == storeId)
            {
                resolvedList = list;
                AddResolved("list", list.Id, list.Name);
            }
        }

        //PackName - through its LIST, which is where its store lives. This is the one type with no
        //StoreId of its own, and the only reason resolvedList is held above.
        var packNameIdValue = ParseId(packNameId);

        if (packNameIdValue == 0)
        {
            AddNew("pn", await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Breadcrumb.NewPackName"));
        }
        else if (packNameIdValue.HasValue)
        {
            var packName = await _packNameService.GetPackNameByIdAsync(packNameIdValue.Value);

            if (packName != null && !packName.Deleted)
            {
                var owningList = resolvedList != null && resolvedList.Id == packName.PackNameListId
                    ? resolvedList
                    : await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

                if (owningList != null && !owningList.Deleted && owningList.StoreId == storeId)
                    AddResolved("pn", packName.Id, packName.Name);
            }
        }

        //Cost centre
        var costCenterIdValue = ParseId(costCenterId);

        if (costCenterIdValue == 0)
        {
            AddNew("cc", await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Breadcrumb.NewCostCenter"));
        }
        else if (costCenterIdValue.HasValue)
        {
            var costCenter = await _multiStoreService.GetCostCenterByIdAsync(costCenterIdValue.Value);

            if (costCenter != null && !costCenter.Deleted && costCenter.StoreId == storeId)
                AddResolved("cc", costCenter.Id, costCenter.Name);
        }

        //Department
        var departmentIdValue = ParseId(departmentId);

        if (departmentIdValue == 0)
        {
            AddNew("dep", await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Breadcrumb.NewDepartment"));
        }
        else if (departmentIdValue.HasValue)
        {
            var department = await _multiStoreService.GetDepartmentByIdAsync(departmentIdValue.Value);

            if (department != null && !department.Deleted && department.StoreId == storeId)
                AddResolved("dep", department.Id, department.Name);
        }

        //Product group
        var productGroupIdValue = ParseId(productGroupId);

        if (productGroupIdValue == 0)
        {
            AddNew("pg", await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Breadcrumb.NewProductGroup"));
        }
        else if (productGroupIdValue.HasValue)
        {
            var productGroup = await _subsidyService.GetProductGroupByIdAsync(productGroupIdValue.Value);

            if (productGroup != null && !productGroup.Deleted && productGroup.StoreId == storeId)
                AddResolved("pg", productGroup.Id, productGroup.Name);
        }

        //Subsidy
        var subsidyIdValue = ParseId(subsidyId);

        if (subsidyIdValue == 0)
        {
            AddNew("sub", await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Breadcrumb.NewSubsidy"));
        }
        else if (subsidyIdValue.HasValue)
        {
            var subsidy = await _subsidyService.GetSubsidyByIdAsync(subsidyIdValue.Value);

            if (subsidy != null && !subsidy.Deleted && subsidy.StoreId == storeId)
                AddResolved("sub", subsidy.Id, subsidy.Name);
        }

        return Json(items);
    }

    #endregion

    #region Branches

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> BranchTree(int storeId, string selectedIds = "")
    {
        var allBranches = await _branchService.GetAllBranchesByStoreIdAsync(storeId);
        var selectedIdSet = (selectedIds ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        var model = new BranchTreeModel
        {
            StoreId = storeId,
            Branches = BuildTree(allBranches, parentId: null, depth: 0),
            SelectedIds = selectedIdSet
        };

        return PartialView("_Branches.BranchTree", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> BranchDetail(int storeId, int? branchId, int? parentBranchId)
    {
        var model = await _branchModelFactory.PrepareBranchModelAsync(storeId, branchId, parentBranchId);
        return PartialView("_Branches.BranchDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> SaveBranch(BranchModel model)
    {
        //THE PARENT MUST BE IN THE SAME STORE. ToEntity takes StoreId and ParentBranchId straight from
        //the posted model, so nothing tied them together: a store-2 branch parented to a store-1 branch
        //saves, and then appears in neither store's tree because each tree walks its own store - an
        //orphan only SQL can find. Scope does not cross it (the hierarchy CTE's store predicate holds,
        //verified live), so this is defence in depth and data hygiene rather than a boundary.
        //Keyed to ParentBranchId, which the validation summary renders - it is a hidden input
        //with no span of its own.
        if (model.ParentBranchId.HasValue && model.ParentBranchId.Value > 0)
        {
            var parentBranch = await _branchService.GetBranchByIdAsync(model.ParentBranchId.Value);

            if (parentBranch == null || parentBranch.Deleted || parentBranch.StoreId != model.StoreId)
                ModelState.AddModelError(nameof(model.ParentBranchId),
                    await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranch.WrongStore"));
        }

        if (ModelState.IsValid)
        {
            //both paths below take the branch's shipping methods from here
            model.ShippingMethod = await _packNameShippingMethodService.KeepOfferedShippingMethodsAsync(model.BranchShippingMethods, model.StoreId);

            // Valid — save and return JSON so JS can update the tree
            if (model.IsNew)
            {
                //fill entity from model
                var branch = model.ToEntity<Branch>();

                branch.CreatedOnUtc = DateTime.UtcNow;
                branch.UpdatedOnUtc = DateTime.UtcNow;

                await _branchService.InsertBranchAsync(branch);

                //the store may ask for a default packname list on every new branch
                await _packNameProvisioningService.ProvisionNewBranchAsync(branch);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewBranch",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewBranch"), branch.Id), branch);

                return Json(new
                {
                    success = true,
                    isNew = true,
                    id = branch.Id,
                    name = branch.Name,
                    branchCode = branch.BranchCode,
                    active = branch.Active,
                    parentId = branch.ParentBranchId?.ToString() ?? ""
                });
            }
            else
            {
                var branch = await _branchService.GetBranchByIdAsync(model.Id);

                //A branch deleted in another tab was an unhandled NullReferenceException here - a 500
                //on an AJAX call whose only feedback is "A server error occurred". DeleteBranch next
                //door has this check; this path did not.
                if (branch == null || branch.Deleted)
                    return Json(new { success = false, error = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.NotFound") });

                branch.BranchCode = model.BranchCode;
                branch.Name = model.Name;
                branch.Active = model.Active;
                branch.BranchNumber = model.BranchNumber;
                branch.CardCode = model.CardCode;
                branch.PaymentTerms = model.PaymentTerms;
                branch.ShippingMethod = model.ShippingMethod;
                branch.UpdatedOnUtc = DateTime.UtcNow;

                await _branchService.UpdateBranchAsync(branch);

                //activity log
                await _customerActivityService.InsertActivityAsync("EditBranch",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditBranch"), branch.Id), branch);

                return Json(new
                {
                    success = true,
                    isNew = false,
                    id = branch.Id,
                    name = branch.Name,
                    branchCode = branch.BranchCode,
                    active = branch.Active
                });
            }
        }

        // Re-populate display-only fields lost in the POST
        if (model.ParentBranchId.HasValue)
        {
            var parent = await _branchService
                .GetBranchByIdAsync(model.ParentBranchId.Value);
            model.ParentBranchName = parent?.Name;
        }

        foreach (var shippingMethodName in await _packNameShippingMethodService.GetShippingMethodNamesAsync(model.StoreId))
            model.AvailableShippingMethods.Add(new SelectListItem { Value = shippingMethodName, Text = shippingMethodName });

        model.IsNew = model.Id == 0;

        return PartialView("_Branches.BranchDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> DeleteBranch(int id)
    {
        var hasChildren = await _branchService.BranchHasChildrenAsync(id);
        if (hasChildren)
            return Json(new
            {
                success = false,
                error = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Delete.HasChildren")
            });

        var branch = await _branchService.GetBranchByIdAsync(id);

        //Deleted too, not just null. GetBranchByIdAsync is a bare GetByIdAsync with no Deleted filter,
        //so an already-removed branch resolved, was soft-deleted a second time and reported success -
        //which is how a stale tree in another tab silently "succeeds" at nothing.
        if (branch == null || branch.Deleted)
            return Json(new { success = false, error = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.NotFound") });

        await _branchService.DeleteBranchAsync(branch);

        await _customerActivityService.InsertActivityAsync("DeleteBranch", string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteBranch"), branch.Id), branch);

        return Json(new { success = true });
    }

    #endregion

    #region Branch Addresses

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> BranchAddresses(int branchId)
    {
        //SERVER-RENDERED ROWS, like the branch's other two collections. NO PAGING: a branch holds a
        //handful of addresses, and the collection reads as a list rather than a table now.
        var branch = await _branchService.GetBranchByIdAsync(branchId)
            ?? throw new ArgumentException("No branch found with the specified id", nameof(branchId));

        var model = await _branchModelFactory.PrepareBranchAddressesModelAsync(branch);

        return PartialView("_Branches.Addresses", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> BranchAddressPopup(int branchId, int? addressId)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(branchId);

        if (branch == null)
            return AccessDeniedView();

        var address = addressId.HasValue ? await _addressService.GetAddressByIdAsync(addressId.Value) : null;

        //prepare model
        var model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(new CWAddressModel(), branch, address);

        return PartialView(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> BranchAddressPopup(CWAddressModel model, IFormCollection form)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.EntityId);
        if (branch == null)
            return AccessDeniedView();

        //THROUGH THE OWNER, never by id alone. GetBranchAddressAsync scopes the lookup to this
        //branch; by id alone any row in the shared Address table would load.
        var address = model.Address.Id > 0
            ? await _branchService.GetBranchAddressAsync(branch.Id, model.Address.Id)
            : null;

        if (model.Address.Id > 0 && address == null)
            return AccessDeniedView();

        //custom address attributes
        var customAttributes = await _addressAttributeParser.ParseCustomAttributesAsync(form, NopCommonDefaults.AddressAttributeControlName);
        var customAttributeWarnings = await _addressAttributeParser.GetAttributeWarningsAsync(customAttributes);
        foreach (var error in customAttributeWarnings)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (ModelState.IsValid)
        {
            if (address != null)
            {
                address = model.Address.ToEntity(address);
                address.CustomAttributes = customAttributes;

                //some validation
                if (address.CountryId == 0)
                    address.CountryId = null;
                if (address.StateProvinceId == 0)
                    address.StateProvinceId = null;

                await _addressService.UpdateAddressAsync(address);
            }
            else
            {
                address = model.Address.ToEntity<Address>();
                address.CustomAttributes = customAttributes;
                address.CreatedOnUtc = DateTime.UtcNow;

                //some validation
                if (address.CountryId == 0)
                    address.CountryId = null;
                if (address.StateProvinceId == 0)
                    address.StateProvinceId = null;

                await _addressService.InsertAddressAsync(address);

                await _branchService.InsertBranchAddressAsync(branch, address);
            }

            ViewBag.RefreshPage = true;

            return PartialView(new CWAddressModel());
        }


        //if we got this far, something failed, redisplay form
        model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(model, branch, address, true);

        return PartialView(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> BranchAddressDelete(int id, int entityId)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(entityId)
            ?? throw new ArgumentException("No branch found with the specified id", nameof(entityId));

        //try to get an address with the specified id
        var address = await _branchService.GetBranchAddressAsync(branch.Id, id);

        if (address == null)
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _branchService.RemoveBranchAddressAsync(branch, address))
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

        return new NullJsonResult();
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> BranchAddressSetDefault(int branchId, int addressId)
    {
        var branch = await _branchService.GetBranchByIdAsync(branchId)
            ?? throw new ArgumentException("No branch found with the specified id", nameof(branchId));

        var address = await _branchService.GetBranchAddressAsync(branchId, addressId);
        if (address == null)
            return Json(new { success = false, error = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound") });

        await _branchService.SetDefaultBranchAddressAsync(branch, address);

        return Json(new { success = true });
    }

    #endregion

    #region PackName Lists

    [HttpGet]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Branch)]
    public async Task<IActionResult> PackNameLists(int branchId)
    {
        var lists = await _packNameService.GetPackNameListDetailsByBranchAsync(branchId);

        //See the note on PackNameListsModel.StoreId: the inline add row posts it to get past
        //PackNameListValidator, which runs before SavePackNameList resolves the store itself.
        var branch = await _branchService.GetBranchByIdAsync(branchId);

        var model = new PackNameListsModel
        {
            BranchId = branchId,
            StoreId = branch?.StoreId ?? 0,
            Lists = lists.Select(l => new PackNameListRowModel
            {
                Id = l.Id,
                Name = l.Name,
                IsDefault = l.IsDefault,
                AllManagers = l.AllManagers,
                ParentManagers = l.ParentManagers,
                PackNameCount = l.PackNamesCount,
                ManagerCount = l.ManagersCount,
            }).ToList()
        };

        return PartialView("_Branches.PackNameLists", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Branch)]
    public async Task<IActionResult> PackNameListDetail(int branchId, int? listId)
    {
        var model = await _packNameModelFactory.PreparePackNameListModelAsync(branchId, listId);
        return PartialView("_Branches.PackNameListDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> SavePackNameList(PackNameListModel model)
    {
        //THE STORE AND THE BRANCH COME FROM THE ENTITY on an edit, and from the posted BRANCH on an
        //insert - never from model.StoreId. ToEntity maps both columns (the mapper ignores only
        //CreatedOnUtc), so a post could move a list, its packnames and its manager mappings into
        //another store, and the [CheckCWFeature(Parameter)] gate was satisfied by the same post.
        var existingList = model.IsNew ? null : await _packNameService.GetPackNameListByIdAsync(model.Id);

        if (!model.IsNew && (existingList == null || existingList.Deleted))
            return new ForbidResult();

        var targetBranch = model.BranchId.HasValue && model.BranchId.Value > 0
            ? await _branchService.GetBranchByIdAsync(model.BranchId.Value)
            : null;

        //ON AN INSERT THE POSTED BRANCH DECIDES THE STORE, so it must exist - and this has to be
        //answered BEFORE the store resolution, not after. Placed after it, the branch-required message
        //was unreachable: no branch on an insert means no store, and the refusal below fired first, so
        //the admin got a bare 403 with nothing to read.
        if (model.IsNew && targetBranch == null)
        {
            //Keyed to Branch, not BranchId - see E1a. D3 fixed the transport (the partial, not JSON);
            //this fixes the message, and both are needed for the admin to see anything at all.
            ModelState.AddModelError(nameof(model.Branch), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Branch.Required"));

            //THE PARTIAL, NOT JSON. bindFocusForm decides success-vs-validation-failure purely by
            //Content-Type (admin.store.navigation.js:586-596): text/html swaps the form back in with its
            //validation summary; anything else is treated as a saved record, so a JSON body here closed
            //the editor as though the list had been created and set _state.listId to "undefined".
            //model.Id is 0 on this path, so there is no manager search model to prepare.
            model.IsNew = true;

            return PartialView("_Branches.PackNameListDetail", model);
        }

        var storeId = existingList?.StoreId ?? targetBranch?.StoreId;

        if (storeId == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, storeId.Value, Request) is IActionResult blocked)
            return blocked;

        model.StoreId = storeId.Value;

        //On an edit a posted branch may only be one in the SAME store.
        if (!model.IsNew && targetBranch != null && targetBranch.StoreId != storeId.Value)
            //Keyed to Branch, not BranchId: _Branches.PackNameListDetail.cshtml renders a span for
            //Branch (:67) and has none for BranchId, which is a bare hidden input (:11) - and the view
            //has no validation summary either, so an error keyed BranchId is stored and displayed
            //nowhere. Both properties carry the same display resource.
            ModelState.AddModelError(nameof(model.Branch), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.WrongStore"));

        //AllManagers and ParentManagers are mutually exclusive: AllManagers already covers the whole
        //store, so ParentManagers adds nothing and the two flags together leave the list's reach
        //ambiguous in the UI. 4.6 forced this; 4.9 dropped it.
        if (model.AllManagers)
            model.ParentManagers = false;

        if (ModelState.IsValid)
        {
            // Valid — save and return JSON so JS can update the tree
            if (model.IsNew)
            {
                //fill entity from model
                var packNameList = model.ToEntity<PackNameList>();

                packNameList.StoreId = targetBranch.StoreId;
                packNameList.BranchId = targetBranch.Id;
                packNameList.IsDefault = !await _packNameService.ExistDefaultPackNameListAsync(packNameList.BranchId ?? 0);
                packNameList.CreatedOnUtc = DateTime.UtcNow;

                await _packNameService.InsertPackNameListAsync(packNameList);

                await ValidateManagersMapping(packNameList);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewPackNameList",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewPackNameList"), packNameList.Name), packNameList);


                return Json(new
                {
                    success = true,
                    id = packNameList.Id,
                    name = packNameList.Name,
                });
            }
            else
            {
                var packNameList = existingList;

                var isDefault = packNameList.IsDefault;
                var storeIdBeforeMapping = packNameList.StoreId;
                var branchIdBeforeMapping = packNameList.BranchId;

                packNameList = model.ToEntity(packNameList);

                packNameList.IsDefault = isDefault;
                packNameList.StoreId = storeIdBeforeMapping;
                packNameList.BranchId = targetBranch?.Id ?? branchIdBeforeMapping;

                await _packNameService.UpdatePackNameListAsync(packNameList);

                await ValidateManagersMapping(packNameList);

                //activity log
                await _customerActivityService.InsertActivityAsync("EditPackNameList",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditPackNameList"), packNameList.Name), packNameList);

                return Json(new
                {
                    success = true,
                    isNew = false,
                    id = packNameList.Id,
                    name = packNameList.Name,
                });
            }
        }

        // Re-populate display-only fields lost in the POST
        if (model.BranchId.HasValue)
        {
            var branch = await _branchService.GetBranchByIdAsync(model.BranchId.Value);
            model.Branch = branch?.Name;
        }

        model.IsNew = model.Id == 0;

        if (!model.IsNew)
        {
            //prepare manager search model
            _packNameModelFactory.PrepareManagerSearchModel(model.ManagerSearchModel, await _packNameService.GetPackNameListByIdAsync(model.Id));
        }

        return PartialView("_Branches.PackNameListDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Branch)]
    public virtual async Task<IActionResult> SetDefaultPackNameList(int listId, int branchId)
    {
        var packNameLists = await _packNameService.GetPackNameListsByBranchAsync(branchId);

        //The feature attribute gates on branchId; listId was never tied to it. An out-of-branch listId
        //matches nothing in the loop, so every list in branchId is set IsDefault = false and the branch
        //is left with no default - which the generator and SavePackNameList then resolve arbitrarily.
        //JSON, not ForbidResult: this action's caller is a $.post with no .fail handler
        //(_Branches.PackNameLists.cshtml:211), so a 403 skips the callback entirely and leaves the
        //button disabled with no message. Every sibling refusal in this file answers in JSON.
        if (!packNameLists.Any(list => list.Id == listId))
            return Json(new { success = false, error = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNameLists.PackNameList.WrongBranch") });

        foreach (var packNameList in packNameLists)
        {
            packNameList.IsDefault = packNameList.Id == listId;

            await _packNameService.UpdatePackNameListAsync(packNameList);

            //activity log
            if (packNameList.IsDefault)
                await _customerActivityService.InsertActivityAsync("EditPackNameListDefault",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditPackNameListDefault"), packNameList.Name), packNameList);
        }

        return Json(new { success = true });
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> DeletePackNameList(int id)
    {
        //try to get a packaging name list with the specified id
        var packNameList = await _packNameService.GetPackNameListByIdAsync(id);
        if (packNameList == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, packNameList.StoreId, Request) is IActionResult blocked)
            return blocked;

        await _packNameService.DeletePackNameListAsync(packNameList);

        //activity log
        await _customerActivityService.InsertActivityAsync("DeletePackNameList",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeletePackNameList"), packNameList.Name), packNameList);

        return Json(new { success = true });
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Branch)]
    public virtual async Task<IActionResult> GetListsByBranchId(int branchId)
    {
        if (branchId == 0)
            return Json(new List<object>());

        //a switched-off list takes no new packnames, so it is not offered
        var lists = (await _packNameService.GetPackNameListsByBranchAsync(branchId)).Where(list => list.Active).ToList();
        var selectedId = (lists.FirstOrDefault(list => list.IsDefault) ?? lists.FirstOrDefault())?.Id;

        var result = lists.Any() ? (from l in lists
                                    select new { id = l.Id, name = l.Name, selected = l.Id == selectedId }).ToList() :
                      new[] { new { id = 0, name = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.DoesNotExist"), selected = true } }.ToList();

        return Json(result);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.PackNameList, ParameterName = "PackNameListId")]
    public virtual async Task<IActionResult> ManagerList(ManagerSearchModel searchModel)
    {
        //prepare model
        var model = await _packNameModelFactory.PrepareManagerListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.PackNameList, ParameterName = "packNameListId")]
    public virtual async Task<IActionResult> ManagerAddPopup(int packNameListId)
    {
        //try to get a packaging name list with the specified id
        var packNameList = await _packNameService.GetPackNameListByIdAsync(packNameListId);

        if (packNameList == null)
            return AccessDeniedView();

        //prepare model
        var model = await _packNameModelFactory.PrepareAddManagerSearchModelAsync(new AddPackNameListManagerSearchModel(), packNameList);

        return View(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.PackNameList, ParameterName = "PackNameListId")]
    public virtual async Task<IActionResult> ManagerAddPopupList(AddPackNameListManagerSearchModel searchModel)
    {
        //try to get a packaging name list with the specified id
        var packNameList = await _packNameService.GetPackNameListByIdAsync(searchModel.PackNameListId);

        if (packNameList == null)
            return new ForbidResult();

        //prepare model
        var model = await _packNameModelFactory.PrepareAddManagerListModelAsync(searchModel, packNameList);

        return Json(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.PackNameList, ParameterName = "PackNameListId")]
    public virtual async Task<IActionResult> ManagerAddPopup(AddPackNameListManagerModel model)
    {
        var packNameList = await _packNameService.GetPackNameListByIdAsync(model.PackNameListId);

        if (packNameList == null || packNameList.Deleted)
            return AccessDeniedView();

        var addedCount = 0;

        if (model.SelectedManagerIds.Any())
        {
            //RE-RUN THE PICKER'S OWN QUERY AND INTERSECT. GetPackNameManagersByStoreAsync scopes to the
            //store and requires pn.Manager; this POST used to insert whatever ids arrived. The mapping
            //IS the grant - it is path 4 of packname_get_listids_by_manager, the one path with no store
            //predicate - so a forged id made a manager of another store an owner of this list, and the
            //storefront then honoured it.
            var selectable = await _multiStoreService.GetPackNameManagersByStoreAsync(packNameList.StoreId, branchId: 0);
            var selectableIds = selectable.Select(m => m.Id).ToHashSet();

            var existingManagers = await _packNameService.GetPackNameListManagersAsync(model.PackNameListId);
            foreach (var managerId in model.SelectedManagerIds)
            {
                if (!selectableIds.Contains(managerId))
                    continue;

                if (existingManagers.Any(m => m.PackNameId == managerId))
                    continue;

                await _packNameService.InsertPackNameListManagerAsync(new PackNameListManagerMapping
                {
                    PackNameListId = model.PackNameListId,
                    PackNameId = managerId,
                });

                addedCount++;
            }
        }

        ViewBag.RefreshPage = true;

        //The count is what was INSERTED, not what was posted. The loop above skips ids the picker
        //would not have offered and ids already mapped, so logging model.SelectedManagerIds.Count
        //would claim grants that never happened - the same class of lie as reporting success on a
        //path that saved nothing.
        await _customerActivityService.InsertActivityAsync("AddPackNameListManager",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddPackNameListManager"), model.PackNameListId, addedCount));

        return View(new AddPackNameListManagerSearchModel());
    }

    [HttpPost]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.PackNameList, ParameterName = "packNameListId")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> ManagerDelete(int id, int packNameListId)
    {
        //try to get a mapping
        var packNameListManager = await _packNameService.GetPackNameListManagerByIdAsync(packNameListId, id)
            ?? throw new ArgumentException("No manager found with the specified id");

        await _packNameService.DeletePackNameListManagerAsync(packNameListManager);

        await _customerActivityService.InsertActivityAsync("DeletePackNameListManager", string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeletePackNameListManager"), id, packNameListId));

        return new NullJsonResult();
    }

    #endregion

    #region PackNames

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> PackNames(int storeId)
    {
        //prepare model
        var model = await _packNameModelFactory.PreparePackNameSearchModelAsync(new PackNameSearchModel(), storeId: storeId);

        return PartialView("_PackNames", model);
    }

    [HttpGet]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.PackNameList)]
    public async Task<IActionResult> PackNamesInline(int listId)
    {
        //try to get a packaging name list with the specified id
        var packNameList = await _packNameService.GetPackNameListByIdAsync(listId);

        if (packNameList == null)
            return AccessDeniedView();

        //prepare searchModel
        var searchModel = await _packNameModelFactory.PreparePackNameSearchModelAsync(new PackNameSearchModel(), packNameList);

        return PartialView("_Branches.PackNamesInline", searchModel);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> PackNamesList(PackNameSearchModel searchModel)
    {
        //prepare model
        var model = await _packNameModelFactory.PreparePackNamesListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.PackNameList)]
    public async Task<IActionResult> BranchPackNameDetail(int listId, int? packNameId)
    {
        var model = await _packNameModelFactory.PreparePackNameModelAsync(null, storeId: null, branchId: null, listId, packNameId);
        return PartialView("_Branches.PackNameDetail", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<IActionResult> PackNameDetail(int storeId, int? packNameId)
    {
        var model = await _packNameModelFactory.PreparePackNameModelAsync(null, storeId: storeId, branchId: null, listId: null, packNameId);
        return PartialView("_PackNames.PackNameDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> SaveBranchPackName(PackNameModel model)
    {
        var existingPackName = model.IsNew ? null : await _packNameService.GetPackNameByIdAsync(model.Id);

        if (!model.IsNew && (existingPackName == null || existingPackName.Deleted))
            return new ForbidResult();

        //THE STORE COMES FROM THE ENTITY, never from model.StoreId, which is a hidden form field.
        var storeId = await ResolvePackNameStoreIdAsync(model, existingPackName);

        if (storeId == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, storeId.Value, Request) is IActionResult blocked)
            return blocked;

        model.StoreId = storeId.Value;

        //If there is no packname list selected, we need to return an error message
        if (model.PackNameListId == 0)
        {
            ModelState.AddModelError("PackNameList", await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Required"));
        }

        await ValidatePackNameTargetAsync(model, existingPackName, storeId.Value);

        if (ModelState.IsValid)
        {
            if (model.IsNew)
            {
                //fill entity from model
                var packName = model.ToEntity<PackName>();

                packName.PackNameListId = model.PackNameListId;

                if (packName.CustomerId.HasValue && packName.CustomerId.Value == 0)
                    packName.CustomerId = null;

                if (packName.CustomerId is null)
                    packName.Manager = false;

                if (packName.DepartmentId.HasValue && packName.DepartmentId.Value == 0)
                    packName.DepartmentId = null;

                if (packName.CostCenterId.HasValue && packName.CostCenterId.Value == 0)
                    packName.CostCenterId = null;

                await _packNameService.InsertPackNameAsync(packName);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewPackName",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewPackName"), packName.Name), packName);

                return Json(new
                {
                    success = true,
                    id = packName.Id,
                    name = packName.Name,
                });
            }
            else
            {
                var packName = existingPackName;

                //To clear context if the packName gets removed from the customer
                var previousCustomerId = packName.CustomerId;

                //Written HERE and only when > 0: the mapper no longer maps PackNameListId and
                //there is no validator on this model, so a post that omitted the field used to map 0
                //onto the entity and orphan the packname. A posted list has already been confirmed to
                //be in this store; a missing one keeps the list the packname is in today.
                var packNameListIdBeforeMapping = packName.PackNameListId;

                packName = model.ToEntity(packName);

                packName.PackNameListId = model.PackNameListId > 0
                    ? model.PackNameListId
                    : packNameListIdBeforeMapping;

                if (packName.CustomerId.HasValue && packName.CustomerId.Value == 0)
                    packName.CustomerId = null;

                if (packName.CustomerId is null)
                    packName.Manager = false;

                if (packName.DepartmentId.HasValue && packName.DepartmentId.Value == 0)
                    packName.DepartmentId = null;

                if (packName.CostCenterId.HasValue && packName.CostCenterId.Value == 0)
                    packName.CostCenterId = null;

                await _packNameService.UpdatePackNameAsync(packName);

                //Clear packName context if the packName's customer changed
                if (previousCustomerId.HasValue && previousCustomerId.Value != packName.CustomerId)
                {
                    var previousCustomer = await _customerService.GetCustomerByIdAsync(previousCustomerId.Value);

                    if (previousCustomer != null)
                    {
                        //clear packname context
                        await _genericAttributeService.SaveAttributeAsync<int?>(previousCustomer, PackNameDefaults.SelectedPackNameIdAttribute, null);
                        await _genericAttributeService.SaveAttributeAsync<int?>(previousCustomer, PackNameDefaults.SelectedPackNameListIdAttribute, null);
                    }
                }

                //activity log
                await _customerActivityService.InsertActivityAsync("EditPackName",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditPackName"), packName.Name), packName);

                return Json(new
                {
                    success = true,
                    isNew = false,
                    id = packName.Id,
                    name = packName.Name,
                });
            }
        }

        // Re-populate display-only fields lost in the POST
        model = await _packNameModelFactory.PreparePackNameModelAsync(model, storeId: null, branchId: null, model.PackNameListId, model.Id);

        return PartialView("_Branches.PackNameDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> SavePackName(PackNameModel model)
    {
        var existingPackName = model.IsNew ? null : await _packNameService.GetPackNameByIdAsync(model.Id);

        if (!model.IsNew && (existingPackName == null || existingPackName.Deleted))
            return new ForbidResult();

        //THE STORE COMES FROM THE ENTITY. On an edit that is the list the packname is in today; on an
        //insert it is the posted list, or the posted branch when the list is still to be created. The
        //[CheckCWFeature(Parameter)] attribute that used to sit here resolved the store from
        //model.StoreId, a hidden form field - so the gate could pass on a store that has PackName
        //enabled while the write landed in one that does not, and AutoMapper then rewrote the
        //packname's parents from the same post.
        var storeId = await ResolvePackNameStoreIdAsync(model, existingPackName);

        if (storeId == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, storeId.Value, Request) is IActionResult blocked)
            return blocked;

        model.StoreId = storeId.Value;

        //no list posted: only a branch without an active list gets its default one created below
        if (model.BranchId > 0 && model.PackNameListId == 0 && (await _packNameService.GetPackNameListsByBranchIdAsync(model.BranchId)).Any(list => list.Active))
        {
            ModelState.AddModelError("PackNameListId", await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Required"));
        }

        await ValidatePackNameTargetAsync(model, existingPackName, storeId.Value);

        if (ModelState.IsValid)
        {
            if (model.IsNew)
            {
                //fill entity from model
                var packName = model.ToEntity<PackName>();

                //Explicit: the mapper no longer maps PackNameListId. A posted list has already been
                //confirmed to be in this store; 0 means "the branch's default list", handled just below.
                packName.PackNameListId = model.PackNameListId;

                //a branch without an active list gets its default one, to create the packname in
                if (packName.PackNameListId == 0)
                {
                    var branch = await _branchService.GetBranchByIdAsync(model.BranchId) ?? throw new ArgumentException("No branch found with the specified id");
                    var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId.Value);

                    var packNameList = await _packNameProvisioningService.EnsureDefaultPackNameListAsync(branch, (PackNameListNameFrom)storeSettings.PackNameListNameFrom);

                    packName.PackNameListId = packNameList.Id;
                }

                if (packName.CustomerId.HasValue && packName.CustomerId.Value == 0)
                    packName.CustomerId = null;

                if (packName.CustomerId is null)
                    packName.Manager = false;

                if (packName.DepartmentId.HasValue && packName.DepartmentId.Value == 0)
                    packName.DepartmentId = null;

                if (packName.CostCenterId.HasValue && packName.CostCenterId.Value == 0)
                    packName.CostCenterId = null;

                await _packNameService.InsertPackNameAsync(packName);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewPackName",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewPackName"), packName.Name), packName);

                return Json(new
                {
                    success = true,
                    id = packName.Id,
                    name = packName.Name,
                });
            }
            else
            {
                var packName = existingPackName;

                //To clear context if the packName gets removed from the customer
                var previousCustomerId = packName.CustomerId;

                //The list is a real form field - moving a packname to another list in the same store is
                //a legitimate edit - but it is written HERE and only when > 0, because the mapper no
                //longer maps it and there is no validator on this model: a post that omitted the
                //field used to map 0 onto the entity and orphan the packname. A posted list has already
                //been confirmed to be in this store by ValidatePackNameTargetAsync; a missing one keeps
                //the list the packname is in today.
                var packNameListIdBeforeMapping = packName.PackNameListId;

                packName = model.ToEntity(packName);

                packName.PackNameListId = model.PackNameListId > 0
                    ? model.PackNameListId
                    : packNameListIdBeforeMapping;

                if (packName.CustomerId.HasValue && packName.CustomerId.Value == 0)
                    packName.CustomerId = null;

                if (packName.CustomerId is null)
                    packName.Manager = false;

                if (packName.DepartmentId.HasValue && packName.DepartmentId.Value == 0)
                    packName.DepartmentId = null;

                if (packName.CostCenterId.HasValue && packName.CostCenterId.Value == 0)
                    packName.CostCenterId = null;

                await _packNameService.UpdatePackNameAsync(packName);

                //Clear packName context if the packName's customer changed
                if (previousCustomerId.HasValue && previousCustomerId.Value != packName.CustomerId)
                {
                    var previousCustomer = await _customerService.GetCustomerByIdAsync(previousCustomerId.Value);

                    if (previousCustomer != null)
                    {
                        //clear packname context
                        await _genericAttributeService.SaveAttributeAsync<int?>(previousCustomer, PackNameDefaults.SelectedPackNameIdAttribute, null);
                        await _genericAttributeService.SaveAttributeAsync<int?>(previousCustomer, PackNameDefaults.SelectedPackNameListIdAttribute, null);
                    }
                }

                //activity log
                await _customerActivityService.InsertActivityAsync("EditPackName",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditPackName"), packName.Name), packName);

                return Json(new
                {
                    success = true,
                    isNew = false,
                    id = packName.Id,
                    name = packName.Name,
                });
            }
        }

        // Re-populate display-only fields lost in the POST
        model = await _packNameModelFactory.PreparePackNameModelAsync(model, storeId: model.StoreId, branchId: model.BranchId, model.PackNameListId, model.Id);

        return PartialView("_PackNames.PackNameDetail", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Branch)]
    public virtual async Task<IActionResult> AssociateCustomerToPackNamePopup(int branchId, int currentCustomerId)
    {
        //prepare model
        var model = _packNameModelFactory.PrepareAssociateCustomerToPackNameSearchModel(new AssociateCustomerToPackNameSearchModel());
        model.BranchId = branchId;
        model.CurrentCustomerId = currentCustomerId;

        return View(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Branch)]
    public virtual async Task<IActionResult> AssociateCustomerToPackNamePopupList(AssociateCustomerToPackNameSearchModel searchModel)
    {
        //prepare model
        var model = await _packNameModelFactory.PrepareAssociateCustomerToPackNameListModelAsync(searchModel);

        return Json(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Branch)]
    public virtual async Task<IActionResult> AssociateCustomerToPackNamePopup([Bind(Prefix = nameof(AssociateCustomerToPackNameModel))] AssociateCustomerToPackNameModel model)
    {
        //try to get a product with the specified id
        var associatedCustomer = await _customerService.GetCustomerByIdAsync(model.AssociatedToCustomerId);
        if (associatedCustomer == null)
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Customers.Customer.NotFound"));

        ViewBag.RefreshPage = true;
        ViewBag.customerId = associatedCustomer.Id;
        ViewBag.customerName = $"{associatedCustomer.FirstName} {associatedCustomer.LastName}";

        return View(new AssociateCustomerToPackNameSearchModel());
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> DeletePackName(int id)
    {
        //try to get a packaging name with the specified id
        var packName = await _packNameService.GetPackNameByIdAsync(id);
        if (packName == null)
            return new ForbidResult();

        var list = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);
        if (list == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, list.StoreId, Request) is IActionResult blocked)
            return blocked;

        await _packNameService.DeletePackNameAsync(packName);

        //activity log
        await _customerActivityService.InsertActivityAsync("DeletePackName",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeletePackName"), packName.Name), packName);

        return Json(new { success = true });
    }

    /// <summary>
    /// Activates, deactivates, deletes or moves the packnames ticked in a packname grid
    /// </summary>
    /// <param name="model">The store, the action, the ticked packnames and, for a move, the list</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result is JSON: success, changed, unchanged, skipped (name, reason), notProcessed and stoppedMessage; or
    /// success false with a message when nothing was done
    /// </returns>
    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> BulkPackNames(PackNameBulkModel model)
    {
        if (await _storeService.GetStoreByIdAsync(model.StoreId) == null)
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Import.StoreNotFound") });

        //one action name exactly: Enum.TryParse would also take a number, or several names joined by commas
        var actionName = Enum.GetNames<PackNameBulkAction>()
            .FirstOrDefault(name => string.Equals(name, model.BulkAction?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (actionName == null)
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Bulk.UnknownAction") });

        var action = Enum.Parse<PackNameBulkAction>(actionName);

        var result = await _packNameBulkService.ApplyAsync(new PackNameBulkRequest(model.StoreId, action,
            PackNameBulkHelper.ParseIds(model.PackNameIds), model.TargetPackNameListId));

        if (result.RefusedMessage != null)
            return Json(new { success = false, message = result.RefusedMessage });

        return Json(new
        {
            success = true,
            changed = result.Changed,
            unchanged = result.Unchanged,
            notProcessed = result.NotProcessed,
            stoppedMessage = result.StoppedMessage,
            skipped = result.Skipped.Select(skip => new { name = skip.Name, reason = skip.Reason }).ToList()
        });
    }

    /// <summary>
    /// The lists of a store the ticked packnames can be moved to
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result is JSON: the lists as id and text, ordered by text
    /// </returns>
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> PackNameBulkMoveTargets(int storeId)
    {
        var targets = await _packNameBulkService.GetMoveTargetsAsync(storeId);

        return Json(targets.Select(target => new { id = target.PackNameListId, text = target.Text }).ToList());
    }

    /// <summary>
    /// Downloads, as an Excel file, the packnames a packname grid's search matches, or the ticked ones
    /// </summary>
    /// <param name="searchModel">The store, the list and the filters of the grid</param>
    /// <param name="selectedIds">The ticked packnames, comma separated; empty for every packname the search matches</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result is the .xlsx file
    /// </returns>
    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> ExportPackNames(PackNameSearchModel searchModel, string selectedIds)
    {
        var store = await _storeService.GetStoreByIdAsync(searchModel.StoreId);

        if (store == null)
            return RedirectToAction("List");

        var packNames = await _packNameModelFactory.PreparePackNamesForExportAsync(searchModel, PackNameBulkHelper.ParseIds(selectedIds));
        var bytes = await _packNameBulkService.ExportToXlsxAsync(store.Id, packNames);

        return File(bytes, MimeTypes.TextXlsx, $"packnames_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.xlsx");
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> CustomersWithNoPackNamePopup(int storeId)
    {
        //prepare model
        var model = new PackNameSearchModel
        {
            StoreId = storeId
        };

        return View(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> CustomersWithNoPackNamePopupList(PackNameSearchModel searchModel)
    {
        //prepare model
        var model = await _packNameModelFactory.PrepareCustomersWithNoPackNameListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> CustomerPackNameCreatePopup(int customerId)
    {
        var customer = await _customerService.GetCustomerByIdAsync(customerId);

        if (customer == null)
            return AccessDeniedView();

        //GATE ON THE STORE THE PACKNAME WILL BE CREATED IN, which is the customer's BRANCH store - not
        //RegisteredInStoreId. The factory derives the target store from the branch, so gating on the
        //registration store checked the wrong store's feature switch.
        var customerBranch = await _customerService.GetCustomerBranchByIdAsync(customer.Id);
        var branch = customerBranch?.BranchId > 0
            ? await _branchService.GetBranchByIdAsync(customerBranch.BranchId.Value)
            : null;

        if (branch == null)
            return View("~/Plugins/CW.Core/Areas/Admin/Views/MultiStore/CustomerPackNameCannotCreatePopup.cshtml");

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, branch.StoreId, Request) is IActionResult blocked)
            return blocked;

        //prepare model
        var model = await _packNameModelFactory.PrepareCustomerPackNameModelAsync(new PackNameModel(), customerId);

        if (model == null)
            return View("~/Plugins/CW.Core/Areas/Admin/Views/MultiStore/CustomerPackNameCannotCreatePopup.cshtml");

        return View("~/Plugins/CW.Core/Areas/Admin/Views/MultiStore/CustomerPackNameCreatePopup.cshtml", model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> CustomerPackNameCreatePopup(PackNameModel model)
    {
        //THE STORE COMES FROM THE TARGET LIST, not from model.StoreId. The [CheckCWFeature(Parameter)]
        //attribute this method used to carry resolved the store from the posted model, so the gate
        //could be satisfied on one store while the row landed in another.
        var storeId = await ResolvePackNameStoreIdAsync(model, packName: null);

        if (storeId == null)
            return AccessDeniedView();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, storeId.Value, Request) is IActionResult blocked)
            return blocked;

        model.StoreId = storeId.Value;

        //no list posted ("New PackName List"): allowed only when the customer's branch has no active list, which the
        //save then creates. The branch is the customer's, read here, never the posted one
        Branch newListBranch = null;

        if (model.PackNameListId == 0)
        {
            var customerBranch = await _customerService.GetCustomerBranchByIdAsync(model.CustomerId.GetValueOrDefault());

            newListBranch = customerBranch?.BranchId > 0 ? await _branchService.GetBranchByIdAsync(customerBranch.BranchId.Value) : null;

            //the checks below read the branch the list would be created on
            model.BranchId = newListBranch?.Id ?? 0;

            if (newListBranch == null || newListBranch.StoreId != storeId.Value
                || (await _packNameService.GetPackNameListsByBranchIdAsync(newListBranch.Id)).Any(list => list.Active))
                ModelState.AddModelError(nameof(model.PackNameListId), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.Required"));
        }

        await ValidatePackNameTargetAsync(model, packName: null, storeId.Value);

        if (ModelState.IsValid)
        {
            var packName = model.ToEntity<PackName>();

            //Explicit: the mapper no longer maps PackNameListId, and it is validated above.
            packName.PackNameListId = model.PackNameListId;

            //the customer's branch has no active list: its default one is created to hold the packname
            if (packName.PackNameListId == 0)
            {
                var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId.Value);
                var packNameList = await _packNameProvisioningService.EnsureDefaultPackNameListAsync(newListBranch, (PackNameListNameFrom)storeSettings.PackNameListNameFrom);

                packName.PackNameListId = packNameList.Id;
            }

            //The posted parents are validated above; the store is not a column on PackName.
            if (packName.CustomerId.HasValue && packName.CustomerId.Value == 0)
                packName.CustomerId = null;

            if (packName.CustomerId is null)
                packName.Manager = false;

            if (packName.DepartmentId.HasValue && packName.DepartmentId.Value == 0)
                packName.DepartmentId = null;

            if (packName.CostCenterId.HasValue && packName.CostCenterId.Value == 0)
                packName.CostCenterId = null;

            await _packNameService.InsertPackNameAsync(packName);

            //activity log
            await _customerActivityService.InsertActivityAsync("AddNewPackName",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewPackName"), packName.Name), packName);

            ViewBag.RefreshPage = true;

            return View(new PackNameModel());
        }

        //prepare model. CustomerId can be null here - that is one of the reasons we are on the failure
        //path - and .Value used to throw on it.
        model = await _packNameModelFactory.PrepareCustomerPackNameModelAsync(model, model.CustomerId.GetValueOrDefault());

        //The factory returns null when the customer has no branch row, no BranchId, or a branch whose
        //store is gone - the same three cases the GET guards. Without this the view dereferences a
        //null model and the popup is a 500 instead of a message.
        if (model == null)
            return View("~/Plugins/CW.Core/Areas/Admin/Views/MultiStore/CustomerPackNameCannotCreatePopup.cshtml");

        //if we got this far, something failed, redisplay form
        return View("~/Plugins/CW.Core/Areas/Admin/Views/MultiStore/CustomerPackNameCreatePopup.cshtml", model);
    }

    #endregion

    #region PackName Import

    /// <summary>
    /// An administrator's Excel import page for the store's packnames
    /// </summary>
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> ImportPackNames(int storeId)
    {
        var store = await _storeService.GetStoreByIdAsync(storeId);

        if (store == null)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Import.StoreNotFound"));
            return RedirectToAction(nameof(List));
        }

        var layout = await _packNameImportService.BuildAdministratorLayoutAsync(store.Id, await _workContext.GetCurrentCustomerAsync());
        var model = await _packNameModelFactory.PreparePackNameImportModelAsync(layout, store);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/MultiStore/ImportPackNames.cshtml", model);
    }

    /// <summary>
    /// The empty template of an administrator's import file; its columns follow the store's settings
    /// </summary>
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> ImportPackNamesTemplate(int storeId)
    {
        var store = await _storeService.GetStoreByIdAsync(storeId);

        if (store == null)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Import.StoreNotFound"));
            return RedirectToAction(nameof(List));
        }

        var layout = await _packNameImportService.BuildAdministratorLayoutAsync(store.Id, await _workContext.GetCurrentCustomerAsync());
        var template = await _packNameImportService.GenerateTemplateAsync(layout);

        return File(template, MimeTypes.TextXlsx, PackNameImportDefaults.TemplateFileName);
    }

    /// <summary>
    /// Imports an uploaded file into the store: every row is checked, and the packnames are created only when all of
    /// them pass. Answers the page's script with the summary or the errors
    /// </summary>
    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> ImportPackNames(int storeId, IFormFile importFile)
    {
        var store = await _storeService.GetStoreByIdAsync(storeId);

        if (store == null)
            return Json(new { message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Import.StoreNotFound") });

        var fileError = await _packNameImportService.CheckFileAsync(importFile?.FileName, importFile?.Length ?? 0);

        if (fileError != null)
            return Json(new { message = fileError });

        var layout = await _packNameImportService.BuildAdministratorLayoutAsync(store.Id, await _workContext.GetCurrentCustomerAsync());
        var languageId = await ResolveImportLanguageIdAsync(store);

        await using var stream = importFile.OpenReadStream();
        var result = await _packNameImportService.ImportAsync(layout, stream, languageId);

        return Json(result.ToPageAnswer());
    }

    /// <summary>
    /// A copy of the uploaded file with an Errors column that names each row's problems; the same file the page just
    /// refused, posted again by its download button
    /// </summary>
    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> ImportPackNamesErrorReport(int storeId, IFormFile importFile)
    {
        var store = await _storeService.GetStoreByIdAsync(storeId);

        if (store == null)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Import.StoreNotFound"));
            return RedirectToAction(nameof(List));
        }

        var fileError = await _packNameImportService.CheckFileAsync(importFile?.FileName, importFile?.Length ?? 0);

        if (fileError != null)
        {
            _notificationService.ErrorNotification(fileError);
            return RedirectToAction(nameof(ImportPackNames), new { storeId = store.Id });
        }

        var layout = await _packNameImportService.BuildAdministratorLayoutAsync(store.Id, await _workContext.GetCurrentCustomerAsync());

        await using var stream = importFile.OpenReadStream();
        var report = await _packNameImportService.BuildErrorReportAsync(layout, stream);

        //the page reloads without its result: the notice says why there is no report
        if (report == null)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Import.ReportFailed"));
            return RedirectToAction(nameof(ImportPackNames), new { storeId = store.Id });
        }

        return File(report, MimeTypes.TextXlsx, $"{Path.GetFileNameWithoutExtension(importFile.FileName)}-errors.xlsx");
    }

    #endregion

    #region PackName Addresses

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> PackNameAddresses(CWAddressSearchModel searchModel)
    {
        //try to get a packName with the specified id
        var packName = await _packNameService.GetPackNameByIdAsync(searchModel.EntityId);
        if (packName == null)
            return new ForbidResult();

        var list = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);
        if (list == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, list.StoreId, Request) is IActionResult blocked)
            return blocked;

        //prepare model
        var model = await _packNameModelFactory.PreparePackNameAddressListModelAsync(searchModel, packName);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.PackName)]
    public virtual async Task<IActionResult> PackNameAddressPopup(int packNameId, int? addressId)
    {
        //try to get a packaging name with the specified id
        var packName = await _packNameService.GetPackNameByIdAsync(packNameId);

        if (packName == null)
            return AccessDeniedView();

        //THROUGH THE OWNER: by id alone this rendered any Address row in the
        //database into the popup, including customers' and other stores'.
        var address = addressId.HasValue
            ? await _packNameService.GetPackNameAddressAsync(packName.Id, addressId.Value)
            : null;

        if (addressId.HasValue && address == null)
            return AccessDeniedView();

        //prepare model
        var model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(new CWAddressModel(), packName, address);

        return PartialView(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> PackNameAddressPopup(CWAddressModel model, IFormCollection form)
    {
        //try to get a packName with the specified id
        var packName = await _packNameService.GetPackNameByIdAsync(model.EntityId);
        if (packName == null)
            return AccessDeniedView();

        var list = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);
        if (list == null)
            return AccessDeniedView();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, list.StoreId, Request) is IActionResult blocked)
            return blocked;

        //THROUGH THE OWNER, never by id alone. GetPackNameAddressAsync is what the
        //delete action at PackNameAddressDelete already uses.
        var address = model.Address.Id > 0
            ? await _packNameService.GetPackNameAddressAsync(packName.Id, model.Address.Id)
            : null;

        if (model.Address.Id > 0 && address == null)
            return AccessDeniedView();

        //custom address attributes
        var customAttributes = await _addressAttributeParser.ParseCustomAttributesAsync(form, NopCommonDefaults.AddressAttributeControlName);
        var customAttributeWarnings = await _addressAttributeParser.GetAttributeWarningsAsync(customAttributes);
        foreach (var error in customAttributeWarnings)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (ModelState.IsValid)
        {
            if (address != null)
            {
                address = model.Address.ToEntity(address);
                address.CustomAttributes = customAttributes;

                //some validation
                if (address.CountryId == 0)
                    address.CountryId = null;
                if (address.StateProvinceId == 0)
                    address.StateProvinceId = null;

                await _addressService.UpdateAddressAsync(address);
            }
            else
            {
                address = model.Address.ToEntity<Address>();
                address.CustomAttributes = customAttributes;
                address.CreatedOnUtc = DateTime.UtcNow;

                //some validation
                if (address.CountryId == 0)
                    address.CountryId = null;
                if (address.StateProvinceId == 0)
                    address.StateProvinceId = null;

                await _addressService.InsertAddressAsync(address);

                await _packNameService.InsertPackNameAddressAsync(packName, address);
            }

            ViewBag.RefreshPage = true;

            return PartialView(new CWAddressModel());
        }


        //if we got this far, something failed, redisplay form
        model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(model, packName, address, true);

        return PartialView(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> PackNameAddressDelete(int id, int entityId)
    {
        //try to get a packName with the specified id
        var packName = await _packNameService.GetPackNameByIdAsync(entityId);

        if (packName == null)
            return new ForbidResult();

        var list = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);
        if (list == null)
            return new ForbidResult();

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.PackName, list.StoreId, Request) is IActionResult blocked)
            return blocked;

        //try to get an address with the specified id
        var address = await _packNameService.GetPackNameAddressAsync(packName.Id, id);

        if (address == null)
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _packNameService.RemovePackNameAddressAsync(packName, address))
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

        return new NullJsonResult();
    }

    #endregion

    #region Cost Centers

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> CostCenters(int storeId)
    {
        //prepare model
        var model = _multiStoreModelFactory.PrepareCostCenterSearchModel(new CostCenterSearchModel(), storeId: storeId, branchId: null);

        return PartialView("_CostCenters", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> BranchCostCenters(int branchId)
    {
        //SERVER-RENDERED ROWS, NOT A GRID. This collection used to be a DataTable while its two
        //siblings on the same branch page were not, so three collections read three different ways.
        //Rendering here also makes the header count honest without a draw callback.
        //NO PAGING: a branch's cost centres are a short list, and GetCostCentersAsync already orders
        //them by name. If a branch ever carries enough of them to need paging, it needs a name
        //filter rather than a page control.
        var branch = await _branchService.GetBranchByIdAsync(branchId);

        //searchInBranches is forced true inside the service whenever branchId is supplied - without
        //it the join's !searchInBranches && b == null arm excludes every row that has a branch.
        var costCenters = await _multiStoreService.GetCostCentersAsync(storeId: null, branchId: branchId);

        var model = new CostCentersModel
        {
            BranchId = branchId,

            //STOREID IS FOR THE INLINE ADD, not for the query. CostCenterValidator has
            //RuleFor(x => x.StoreId).NotEqual(0) and FluentValidation runs at model binding, before
            //SaveCostCenter can derive the store from the branch, so the post has to carry it.
            StoreId = branch?.StoreId ?? 0,

            CostCenters = costCenters.Select(cc => new CostCenterRowModel
            {
                Id = cc.Id,
                Name = cc.Name,
                Active = cc.Active
            }).ToList()
        };

        return PartialView("_Branches.CostCenters", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> CostCenterList(CostCenterSearchModel searchModel)
    {
        //prepare model
        var model = await _multiStoreModelFactory.PrepareCostCenterListModelAsync(searchModel, searchModel.StoreId, searchModel.BranchId);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> BranchCostCenterDetail(int branchId, int? costCenterId)
    {
        var model = await _multiStoreModelFactory.PrepareCostCenterModelAsync(null, storeId: null, branchId: branchId, costCenterId);
        return PartialView("_Branches.CostCenterDetail", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> CostCenterDetail(int storeId, int? costCenterId)
    {
        var model = await _multiStoreModelFactory.PrepareCostCenterModelAsync(null, storeId: storeId, branchId: null, costCenterId);
        return PartialView("_CostCenters.CostCenterDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> SaveCostCenter(CostCenterModel model, string returnView)
    {
        //THE STORE COMES FROM THE ENTITY on an edit and FROM THE POSTED BRANCH on an insert - never
        //from model.StoreId, which is a hidden field and therefore whatever the post says. This is
        //the same store-resolution hardening SavePackNameList carries (:932), applied to the
        //method that was missed. Before it, a post of StoreId=1 carrying a branch that belongs to
        //store 2 created a cost centre whose branch lives in another store, and nothing checked that
        //the branch existed or that it was still active.
        var existingCostCenter = model.IsNew ? null : await _multiStoreService.GetCostCenterByIdAsync(model.Id);

        if (!model.IsNew && existingCostCenter == null)
            return new ForbidResult();

        //A COST CENTRE MAY LEGITIMATELY HAVE NO BRANCH. The store-level Cost Centres tab creates
        //them with BranchId null (PrepareCostCenterModelAsync(..., branchId: null, ...)), so a
        //missing branch is a valid shape here and only a branch that was POSTED has to resolve.
        var targetBranch = model.BranchId.HasValue && model.BranchId.Value > 0
            ? await _branchService.GetBranchByIdAsync(model.BranchId.Value)
            : null;

        if (model.IsNew && model.BranchId.HasValue && model.BranchId.Value > 0 && targetBranch == null)
            ModelState.AddModelError(nameof(model.Branch), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Branch.NotFound"));

        var storeId = existingCostCenter?.StoreId ?? targetBranch?.StoreId ?? model.StoreId;

        //With no branch to take the store from, the posted one still has to exist. This plugin has
        //no per-admin store scoping anywhere, so existence is the bar the rest of the controller
        //applies and the bar applied here.
        if (await _storeService.GetStoreByIdAsync(storeId) == null)
            return new ForbidResult();

        //Keyed to Branch, not BranchId: _Branches.CostCenterDetail.cshtml renders a span for Branch
        //(:67) and has none for BranchId, which is a bare hidden input - an error keyed BranchId is
        //stored and displayed nowhere. Both properties carry the same display resource.
        if (targetBranch != null && targetBranch.StoreId != storeId)
            ModelState.AddModelError(nameof(model.Branch), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Branch.WrongStore"));

        //THE ACTIVE RULE (changes 99-112) on the write path it never covered. An inactive branch
        //keeps what it already has and takes nothing new, so the refusal is on the insert only: the
        //update branch below copies Name and Active and never moves a cost centre between branches.
        if (model.IsNew && targetBranch != null && !targetBranch.Active)
            ModelState.AddModelError(nameof(model.Branch), await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Branch.Inactive"));

        model.StoreId = storeId;

        if (ModelState.IsValid)
        {
            if (model.IsNew)
            {
                var costCenter = new CostCenter
                {
                    StoreId = storeId,
                    BranchId = targetBranch?.Id,
                    Name = model.Name,
                    Active = model.Active,
                };

                await _multiStoreService.InsertCostCenterAsync(costCenter);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewCostCenter",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewCostCenter"), costCenter.Id), costCenter);

                return Json(new
                {
                    success = true,
                    id = costCenter.Id,
                    name = costCenter.Name,
                });
            }
            else
            {
                var costCenter = existingCostCenter;

                costCenter.Name = model.Name;
                costCenter.Active = model.Active;

                await _multiStoreService.UpdateCostCenterAsync(costCenter);

                //activity log
                await _customerActivityService.InsertActivityAsync("EditCostCenter",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditCostCenter"), costCenter.Id), costCenter);

                return Json(new
                {
                    success = true,
                    isNew = false,
                    id = costCenter.Id,
                    name = costCenter.Name,
                });
            }
        }

        // Re-populate display-only fields lost in the POST
        model = await _multiStoreModelFactory.PrepareCostCenterModelAsync(model, model.StoreId, model.BranchId, model.Id);

        //THE PARTIAL MUST MATCH THE FORM THAT POSTED. Both cost-centre editors post to this action,
        //and this line returned the branch one unconditionally: a validation failure on the
        //store-level tab injected a form carrying cc-detail-form / btn-focus-save into
        //#costcenter-container, whose handler binds costcenter-detail-form / btn-costcenter-save.
        //The admin was left with a panel whose Save and Back did nothing. A post with no ReturnView
        //keeps the old behaviour.
        return PartialView(returnView == "tab"
            ? "_CostCenters.CostCenterDetail"
            : "_Branches.CostCenterDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> DeleteCostCenter(int id)
    {
        //try to get a cost center with the specified id
        var costCenter = await _multiStoreService.GetCostCenterByIdAsync(id);
        if (costCenter == null)
            return new ForbidResult();

        await _multiStoreService.DeleteCostCenterAsync(costCenter);

        //activity log
        await _customerActivityService.InsertActivityAsync("DeleteCostCenter",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteCostCenter"), costCenter.Name), costCenter);

        return Json(new { success = true });
    }

    #endregion

    #region Cost Center Addresses

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> CostCenterAddresses(CWAddressSearchModel searchModel)
    {
        //try to get a cost center with the specified id
        var costCenter = await _multiStoreService.GetCostCenterByIdAsync(searchModel.EntityId)
            ?? throw new ArgumentException("No cost centre found with the specified id");

        //prepare model
        var model = await _multiStoreModelFactory.PrepareCostCenterAddressListModelAsync(searchModel, costCenter);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> CostCenterAddressPopup(int costCenterId, int? addressId)
    {
        //try to get a packaging name with the specified id
        var costCenter = await _multiStoreService.GetCostCenterByIdAsync(costCenterId);

        if (costCenter == null)
            return AccessDeniedView();

        var address = addressId.HasValue ? await _addressService.GetAddressByIdAsync(addressId.Value) : null;

        //prepare model
        var model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(new CWAddressModel(), costCenter, address);

        return PartialView(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> CostCenterAddressPopup(CWAddressModel model, IFormCollection form)
    {
        //try to get a cost center with the specified id
        var costCenter = await _multiStoreService.GetCostCenterByIdAsync(model.EntityId);
        if (costCenter == null)
            return AccessDeniedView();

        //THROUGH THE OWNER, never by id alone. GetAddressByIdAsync would load any row in the shared
        //Address table - another store's branch address, a cost-centre address, any customer's or
        //order's address - and the code below then overwrites it. The delete sibling already resolves
        //it this way; that asymmetry was the defect.
        var address = model.Address.Id > 0
            ? await _multiStoreService.GetCostCenterAddressAsync(costCenter.Id, model.Address.Id)
            : null;

        //An id that was supplied but does not belong to this owner is a forged request, not a
        //create: falling through to the insert branch would silently make a second address.
        if (model.Address.Id > 0 && address == null)
            return AccessDeniedView();

        //custom address attributes
        var customAttributes = await _addressAttributeParser.ParseCustomAttributesAsync(form, NopCommonDefaults.AddressAttributeControlName);
        var customAttributeWarnings = await _addressAttributeParser.GetAttributeWarningsAsync(customAttributes);
        foreach (var error in customAttributeWarnings)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (ModelState.IsValid)
        {
            if (address != null)
            {
                address = model.Address.ToEntity(address);
                address.CustomAttributes = customAttributes;

                //some validation
                if (address.CountryId == 0)
                    address.CountryId = null;
                if (address.StateProvinceId == 0)
                    address.StateProvinceId = null;

                await _addressService.UpdateAddressAsync(address);
            }
            else
            {
                address = model.Address.ToEntity<Address>();
                address.CustomAttributes = customAttributes;
                address.CreatedOnUtc = DateTime.UtcNow;

                //some validation
                if (address.CountryId == 0)
                    address.CountryId = null;
                if (address.StateProvinceId == 0)
                    address.StateProvinceId = null;

                await _addressService.InsertAddressAsync(address);

                await _multiStoreService.InsertCostCenterAddressAsync(costCenter, address);
            }

            ViewBag.RefreshPage = true;

            return PartialView(new CWAddressModel());
        }


        //if we got this far, something failed, redisplay form
        model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(model, costCenter, address, true);

        return PartialView(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> CostCenterAddressDelete(int id, int entityId)
    {
        //try to get a cost center with the specified id
        var costCenter = await _multiStoreService.GetCostCenterByIdAsync(entityId)
            ?? throw new ArgumentException("No cost centre found with the specified id", nameof(entityId));

        //try to get an address with the specified id
        var address = await _multiStoreService.GetCostCenterAddressAsync(costCenter.Id, id);

        if (address == null)
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _multiStoreService.RemoveCostCenterAddressAsync(costCenter, address))
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

        return new NullJsonResult();
    }

    #endregion

    #region Departments

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> Departments(int storeId)
    {
        //prepare model
        var model = await _multiStoreModelFactory.PrepareDepartmentSearchModelAsync(new DepartmentSearchModel(), storeId: storeId);

        return PartialView("_Departments", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> DepartmentList(DepartmentSearchModel searchModel)
    {
        //try to get a store with the specified id
        var store = await _storeService.GetStoreByIdAsync(searchModel.StoreId)
                         ?? throw new ArgumentException("No store found with the specified id");

        //prepare model
        var model = await _multiStoreModelFactory.PrepareDepartmentListModelAsync(searchModel, store);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> DepartmentDetail(int storeId, int? departmentId)
    {
        if (departmentId.HasValue && departmentId.Value == 0)
            departmentId = null;

        var model = await _multiStoreModelFactory.PrepareDepartmentModelAsync(null, storeId, departmentId);
        return PartialView("_Departments.DepartmentDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> SaveDepartment(DepartmentModel model)
    {
        //THE POSTED STORE HAS TO EXIST. StoreId is a hidden field, so on an insert it is whatever
        //the post says. DepartmentList (:2221) already refuses an unknown store; this save did not,
        //and an id matching nothing created a department attached to no store at all. A department
        //has no parent entity to take the store from - unlike a cost centre, which takes it from its
        //branch - so verifying it exists is as far as this can go, and it is the same bar the rest
        //of the controller applies.
        var existingDepartment = model.IsNew ? null : await _multiStoreService.GetDepartmentByIdAsync(model.Id);

        //ForbidResult, not the ArgumentException this used to throw on the edit path: a record
        //deleted in another tab is a refusal the admin can read, not a 500. Same shape as
        //DeleteDepartment (:2302).
        if (!model.IsNew && existingDepartment == null)
            return new ForbidResult();

        //ON AN EDIT THE STORE COMES FROM THE ENTITY, so a post cannot move a department between
        //stores. The update branch below already only copied Name and Active, so this makes
        //explicit what that code relied on.
        var storeId = existingDepartment?.StoreId ?? model.StoreId;

        if (await _storeService.GetStoreByIdAsync(storeId) == null)
            return new ForbidResult();

        model.StoreId = storeId;

        if (ModelState.IsValid)
        {
            if (model.IsNew)
            {
                var department = new Department
                {
                    StoreId = storeId,
                    Name = model.Name,
                    Active = model.Active,
                };

                await _customerService.SaveDepartmentCustomerRole(department);
                await _multiStoreService.InsertDepartmentAsync(department);

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewDepartment",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewDepartment"), department.Id), department);

                return Json(new
                {
                    success = true,
                    id = department.Id,
                    name = department.Name,
                });
            }
            else
            {
                var department = existingDepartment;

                department.Name = model.Name;
                department.Active = model.Active;

                await _customerService.SaveDepartmentCustomerRole(department);
                await _multiStoreService.UpdateDepartmentAsync(department);

                //activity log
                await _customerActivityService.InsertActivityAsync("EditDepartment",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditDepartment"), department.Id), department);

                return Json(new
                {
                    success = true,
                    isNew = false,
                    id = department.Id,
                    name = department.Name,
                });
            }
        }

        // Re-populate display-only fields lost in the POST
        model = await _multiStoreModelFactory.PrepareDepartmentModelAsync(model, model.StoreId, model.Id);

        return PartialView("_Departments.DepartmentDetail", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public virtual async Task<IActionResult> DeleteDepartment(int id)
    {
        //try to get a cost center with the specified id
        var department = await _multiStoreService.GetDepartmentByIdAsync(id);
        if (department == null)
            return new ForbidResult();

        var customerRole = await _customerService.GetCustomerRoleByIdAsync(department.CustomerRoleId ?? 0);

        department.CustomerRoleId = null;
        await _multiStoreService.DeleteDepartmentAsync(department);

        if (customerRole != null)
        {
            //Delete customer role of the department
            await _customerService.DeleteCustomerRoleAsync(customerRole);
        }

        //activity log
        await _customerActivityService.InsertActivityAsync("DeleteDepartment",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteDepartment"), department.Name), department);

        return Json(new { success = true });
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Department, ParameterName = "DepartmentId")]
    public virtual async Task<IActionResult> DepartmentManagerList(DepartmentManagerSearchModel searchModel)
    {
        //try to get a department with the specified id
        var department = await _multiStoreService.GetDepartmentByIdAsync(searchModel.DepartmentId)
                         ?? throw new ArgumentException("No department found with the specified id");

        //prepare model
        var model = await _multiStoreModelFactory.PrepareDepartmentManagerListModelAsync(searchModel);

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Department, ParameterName = "DepartmentId")]
    public virtual async Task<IActionResult> DepartmentManagerAddPopupList(AddDepartmentManagerSearchModel searchModel)
    {
        //try to get a department with the specified id
        var department = await _multiStoreService.GetDepartmentByIdAsync(searchModel.DepartmentId)
                         ?? throw new ArgumentException("No department found with the specified id");

        //prepare model
        var model = await _multiStoreModelFactory.PrepareAddDepartmentManagerListModelAsync(searchModel, department);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Department)]
    public virtual async Task<IActionResult> DepartmentManagerAddPopup(int departmentId)
    {
        //try to get a department with the specified id
        var department = await _multiStoreService.GetDepartmentByIdAsync(departmentId)
                         ?? throw new ArgumentException("No department found with the specified id");

        //prepare model
        var model = await _multiStoreModelFactory.PrepareAddDepartmentManagerSearchModelAsync(new AddDepartmentManagerSearchModel(), department);

        return View(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Department)]
    public virtual async Task<IActionResult> DepartmentManagerAddPopup(AddDepartmentManagerModel model)
    {
        var department = await _multiStoreService.GetDepartmentByIdAsync(model.DepartmentId);

        if (department == null || department.Deleted)
            return AccessDeniedView();

        var addedCount = 0;

        if (model.SelectedManagerIds.Any())
        {
            //RE-RUN THE PICKER'S OWN QUERY AND INTERSECT. The grid is store-scoped and requires
            //Manager (GetManagersNotInDepartmentAsync); the POST used to insert whatever ids arrived,
            //and DepartmentManagerMapping is a grant: GetDepartmentsByManagerIdAsync reads it, so a
            //forged id gave a manager of another store ownership of this department's packnames.
            //Anything the picker would not have offered is dropped.
            var selectable = await _multiStoreService.GetManagersNotInDepartmentAsync(department.StoreId, department.Id);
            var selectableIds = selectable.Select(m => m.Id).ToHashSet();

            var existingManagers = await _multiStoreService.GetDepartmentManagersAsync(model.DepartmentId);
            foreach (var managerId in model.SelectedManagerIds)
            {
                if (!selectableIds.Contains(managerId))
                    continue;

                if (existingManagers.Any(m => m.PackNameId == managerId))
                    continue;

                await _multiStoreService.InsertDepartmentManagerAsync(new DepartmentManagerMapping
                {
                    DepartmentId = model.DepartmentId,
                    PackNameId = managerId,
                });

                addedCount++;
            }
        }

        ViewBag.RefreshPage = true;

        //Inserted, not posted - see ManagerAddPopup above.
        await _customerActivityService.InsertActivityAsync("AddDepartmentManager",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddDepartmentManager"), model.DepartmentId, addedCount));

        return View(new AddDepartmentManagerSearchModel());
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Department)]
    public virtual async Task<IActionResult> DepartmentManagerDelete(int id, int departmentId)
    {
        //try to get a mapping
        var departmentManager = await _multiStoreService.GetDepartmentManagerByIdAsync(departmentId, id)
            ?? throw new ArgumentException("No manager found with the specified id");

        await _multiStoreService.DeleteDepartmentManagerAsync(departmentManager);

        await _customerActivityService.InsertActivityAsync("DeleteDepartmentManager", string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteDepartmentManager"), id, departmentId));

        return new NullJsonResult();
    }

    #endregion

    #region Store Settings

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
    public async Task<IActionResult> StoreSettings(int storeId)
    {
        var model = await _cwSettingModelFactory.PrepareMultiStoreSettingsModelAsync(null, storeId);

        return PartialView("_StoreSettings", model);
    }


    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE_SETTINGS)]
    public virtual async Task<IActionResult> StoreSettings(MultiStoreSettingsModel model, IFormCollection form)
    {
        var allStoreSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(0);

        var storeId = model.StoreId;

        //StoreId = 0 is the ALL-STORES DEFAULT scope, and SaveSettingOverridablePerStoreAsync writes
        //the global row for it. That scope belongs to the Settings action, which is guarded by
        //ADMIN_ACCESS_DEFAULT_STORE_SETTINGS - so accepting 0 here lets a holder of
        //ADMIN_ACCESS_MULTISTORE_SETTINGS rewrite every store's defaults, feature switches included,
        //by editing one hidden field.
        if (storeId == 0)
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStore.Settings.DefaultScopeNotAllowed") });

        if (ModelState.IsValid)
        {
            //load settings for the store
            var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

            //If we are enabling the "Do not pre-select shipping address" and the setting was previously disabled, we need to clear the selected shipping address for packNames
            var overrideEnabled = model.DoNotPreSelectShippingAddress_OverrideForStore;
            var clearPreSelectedShippingAddress = (overrideEnabled && model.DoNotPreSelectShippingAddress) || (!overrideEnabled && allStoreSettings.DoNotPreSelectShippingAddress);
            clearPreSelectedShippingAddress = !storeSettings.DoNotPreSelectShippingAddress && clearPreSelectedShippingAddress;

            model.BulkOrderAxisAttributeIds = (model.BulkOrderAxisAttributeIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();

            model.ShippingMethod = await _packNameShippingMethodService.KeepOfferedShippingMethodsAsync(model.SelectedShippingMethods, storeId) ?? string.Empty;

            storeSettings = model.ToSettings(storeSettings);

            //we do not clear cache after each setting update.
            //this behavior can increase performance because cached settings will not be cleared 
            //and loaded from database after each update
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.UseStaffAsPackName, model.UseStaffAsPackName_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.UseBranchesAsPackNameList, model.UseBranchesAsPackNameList_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNameListNameFrom, model.PackNameListNameFrom_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.EmployeeNumberEnabled, model.EmployeeNumberEnabled_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PrintNameEnabled, model.PrintNameEnabled_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PrintNameProductAttributeId, model.PrintNameProductAttributeId_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.POEntryEnabled, model.POEntryEnabled_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.CommentsEnabled, model.CommentsEnabled_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.GenderEnabled, model.GenderEnabled_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ShowDepartmentPackNamesByBranch, model.ShowDepartmentPackNamesByBranch_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowManagersToAddPackNames, model.AllowManagersToAddPackNames_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowToAddCustomerToPackName, model.AllowToAddCustomerToPackName_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowSubsidySelection, model.AllowSubsidySelection_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowEditSubsidyTopup, model.AllowEditSubsidyTopup_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowToAddByExcel, model.AllowToAddByExcel_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowSubsidyCountByExcel, model.AllowSubsidyCountByExcel_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.LimitAssignedBranches, model.LimitAssignedBranches_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNamesOwnGlobalValue, model.PackNamesOwnGlobalValue_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNamesOwnStartAndFinishDates, model.PackNamesOwnStartAndFinishDates_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ShippingMethod, model.ShippingMethod_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowHomeDelivery, model.AllowHomeDelivery_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowCustomersToAddAddress, model.AllowCustomersToAddAddress_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.DoNotPreSelectShippingAddress, model.DoNotPreSelectShippingAddress_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.IncludeShippingOnSubsidy, model.IncludeShippingOnSubsidy_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNameEnable, model.PackNameEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.CreditNotesEnable, model.CreditNotesEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ExchangesEnable, model.ExchangesEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowCloseWithoutPrintingReturnNote, model.AllowCloseWithoutPrintingReturnNote_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.SubsidyEnable, model.SubsidyEnable_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderEnable, model.BulkOrderEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderAxisAttributeIds, model.BulkOrderAxisAttributeIds_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderMaxCellsPerProduct, model.BulkOrderMaxCellsPerProduct_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderDefaultViewMode, model.BulkOrderDefaultViewMode_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderShowProductImages, model.BulkOrderShowProductImages_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderProductImageSize, model.BulkOrderProductImageSize_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderShowAttributeImages, model.BulkOrderShowAttributeImages_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderAttributeImageSize, model.BulkOrderAttributeImageSize_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderAttributeImageSquaresSize, model.BulkOrderAttributeImageSquaresSize_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderHideQtyArrows, model.BulkOrderHideQtyArrows_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderShowAttributePrices, model.BulkOrderShowAttributePrices_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderShowProductStock, model.BulkOrderShowProductStock_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderDefaultPageSize, model.BulkOrderDefaultPageSize_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderPageSizeOptions, model.BulkOrderPageSizeOptions_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderSendUserAfterLogin, model.BulkOrderSendUserAfterLogin_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderEnableOnProductPage, model.BulkOrderEnableOnProductPage_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.SubsidySendUserAfterLogin, model.SubsidySendUserAfterLogin_OverrideForStore, storeId, false);

            model.DefaultPackNameCustomerRoleIds ??= [];

            storeSettings.DefaultPackNameCustomerRoleIds = model.DefaultPackNameCustomerRoleIds.ToList();
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.DefaultPackNameCustomerRoleIds, model.DefaultPackNameCustomerRoleIds_OverrideForStore, storeId, false);

            if (clearPreSelectedShippingAddress)
            {
                ClearSelectedShippingAddresses(storeId);
            }

            //publish event
            await _eventPublisher.PublishAsync(new MultiStoreSettingsChangedEvent());
            _cwFeatureService.ResetRequestCache();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Configuration.Updated"));

            await _customerActivityService.InsertActivityAsync("EditStoreSettings", string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditStoreSettings"), storeId));

            return Json(new
            {
                success = true,
            });
        }

        //prepare model
        model = await _cwSettingModelFactory.PrepareMultiStoreSettingsModelAsync(model, storeId);

        //if we got this far, something failed, redisplay form
        return Json(new
        {
            success = false,
            settingssectionhtml = await RenderPartialViewToStringAsync("_StoreSettings", model)
        });
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE_SETTINGS)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> GeneratePackNames(MultiStoreSettingsModel model)
    {
        var storeId = model.StoreId;

        //SAME REFUSAL AS StoreSettings (:2434). StoreId = 0 is the all-stores default scope, and the
        //three SaveSettingOverridablePerStoreAsync calls below write the global row for it - so the
        //posted id must never be 0 here.
        //DEFENCE IN DEPTH, not a closed hole - and the earlier comment here said the opposite. The
        //[CheckCWFeature(StoreSource = Parameter)] above already fails closed on 0:
        //CheckCWFeatureFilter.IsAllowedAsync ends with
        //  if (storeId is null || (storeId == 0 && !_allowDefaultStore)) return LogUnresolvedStoreAsync(...)
        //so a request with StoreId = 0 is refused by the gate before this body runs ("This feature is
        //not enabled for this store."). This guard is what protects the
        //writes if that attribute is ever removed or given AllowDefaultStore = true. StoreSettings has
        //no [CheckCWFeature] at all, which is why the crossing was real there and only there.
        if (storeId == 0)
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStore.Settings.DefaultScopeNotAllowed") });

        //This action WRITES three settings before queueing the job, so an invalid model must not reach
        //them. The caller's JS reads {success:false, message}, so a failure must come back as JSON.
        if (!ModelState.IsValid)
            return Json(new
            {
                success = false,
                message = string.Join(" ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m)))
            });

        //CHECKED CAST. The posted int goes into a background task as PackNameListNameFrom; an
        //out-of-range value produced an undefined enum there, and the only thing catching it was a
        //catch(Exception) writing a log row nobody reads.
        if (!Enum.IsDefined(typeof(PackNameListNameFrom), model.PackNameListNameFrom))
            return Json(new
            {
                success = false,
                message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStore.Settings.PackNameListNameFrom.Invalid")
            });

        //store settings
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        storeSettings.UseStaffAsPackName = model.UseStaffAsPackName;
        storeSettings.UseBranchesAsPackNameList = model.UseBranchesAsPackNameList;
        storeSettings.PackNameListNameFrom = model.PackNameListNameFrom;

        await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.UseStaffAsPackName, model.UseStaffAsPackName_OverrideForStore, storeId);
        await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.UseBranchesAsPackNameList, model.UseBranchesAsPackNameList_OverrideForStore, storeId);
        await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNameListNameFrom, model.PackNameListNameFrom_OverrideForStore, storeId);

        //Logged BEFORE queueing, and logged at all: this button rewrites three per-store settings and
        //mass-creates packname rows, and left no trace of who pressed it or for which store.
        //The row records the request, not the outcome - the work happens on a
        //background thread and its failures go to the error log.
        await _customerActivityService.InsertActivityAsync("GeneratePackNames",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.GeneratePackNames"), storeId));

        //The rule the store has after the saves above. A rule whose override is unticked is disabled in the
        //form, is not posted and binds 0, so the posted value is not the store's rule.
        var packNameListNameFrom = (PackNameListNameFrom)(await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId)).PackNameListNameFrom;

        _taskQueue.QueueBackgroundWorkItem(async token =>
        {
            try
            {
                await GeneratePackNamesBackgroundAsync(storeId, packNameListNameFrom);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error on generating the packNames for the store identifier {storeId}", ex);
            }
        });

        return new NullJsonResult();
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE_SETTINGS)]
    [CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> GeneratePackNameLists(MultiStoreSettingsModel model)
    {
        var storeId = model.StoreId;

        //Same refusal as GeneratePackNames above and StoreSettings (:2434) - see either for why.
        if (storeId == 0)
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStore.Settings.DefaultScopeNotAllowed") });

        //Same three guards as GeneratePackNames - see there for why each one.
        if (!ModelState.IsValid)
            return Json(new
            {
                success = false,
                message = string.Join(" ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m)))
            });

        if (!Enum.IsDefined(typeof(PackNameListNameFrom), model.PackNameListNameFrom))
            return Json(new
            {
                success = false,
                message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStore.Settings.PackNameListNameFrom.Invalid")
            });

        //store settings
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        storeSettings.UseStaffAsPackName = model.UseStaffAsPackName;
        storeSettings.UseBranchesAsPackNameList = model.UseBranchesAsPackNameList;
        storeSettings.PackNameListNameFrom = model.PackNameListNameFrom;

        await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.UseStaffAsPackName, model.UseStaffAsPackName_OverrideForStore, storeId);
        await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.UseBranchesAsPackNameList, model.UseBranchesAsPackNameList_OverrideForStore, storeId);
        await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNameListNameFrom, model.PackNameListNameFrom_OverrideForStore, storeId);

        //See GeneratePackNames above.
        await _customerActivityService.InsertActivityAsync("GeneratePackNameLists",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.GeneratePackNameLists"), storeId));

        //The rule the store has after the saves above. A rule whose override is unticked is disabled in the
        //form, is not posted and binds 0, so the posted value is not the store's rule.
        var packNameListNameFrom = (PackNameListNameFrom)(await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId)).PackNameListNameFrom;

        _taskQueue.QueueBackgroundWorkItem(async token =>
        {
            try
            {
                await GeneratePackNameListsBackgroundAsync(storeId, packNameListNameFrom);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error on generating the packName lists for the store identifier {storeId}", ex);
            }
        });

        return new NullJsonResult();
    }

    #endregion

    #region Default Stores Settings

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_DEFAULT_STORE_SETTINGS)]
    public virtual async Task<IActionResult> Settings()
    {
        //load default settings for stores
        var storeId = 0;

        //prepare model
        var model = await _cwSettingModelFactory.PrepareMultiStoreSettingsModelAsync(null, storeId);

        return View(model);
    }

    [HttpPost]
    [FormValueRequired("save")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_DEFAULT_STORE_SETTINGS)]
    public virtual async Task<IActionResult> Settings(MultiStoreSettingsModel model, IFormCollection form)
    {
        if (ModelState.IsValid)
        {
            //load default settings for stores
            var storeId = 0;

            //store information settings
            var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

            //If we are enabling the "Do not pre-select shipping address" and the setting was previously disabled, we need to clear the selected shipping address for packNames
            var clearPreSelectedShippingAddress = model.DoNotPreSelectShippingAddress && !storeSettings.DoNotPreSelectShippingAddress;

            model.BulkOrderAxisAttributeIds = (model.BulkOrderAxisAttributeIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();

            model.ShippingMethod = await _packNameShippingMethodService.KeepOfferedShippingMethodsAsync(model.SelectedShippingMethods, storeId) ?? string.Empty;

            storeSettings = model.ToSettings(storeSettings);

            //we do not clear cache after each setting update.
            //this behavior can increase performance because cached settings will not be cleared 
            //and loaded from database after each update
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.UseStaffAsPackName, model.UseStaffAsPackName_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.UseBranchesAsPackNameList, model.UseBranchesAsPackNameList_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNameListNameFrom, model.PackNameListNameFrom_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.EmployeeNumberEnabled, model.EmployeeNumberEnabled_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PrintNameEnabled, model.PrintNameEnabled_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PrintNameProductAttributeId, model.PrintNameProductAttributeId_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.POEntryEnabled, model.POEntryEnabled_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.CommentsEnabled, model.CommentsEnabled_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.GenderEnabled, model.GenderEnabled_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ShowDepartmentPackNamesByBranch, model.ShowDepartmentPackNamesByBranch_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowManagersToAddPackNames, model.AllowManagersToAddPackNames_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowToAddCustomerToPackName, model.AllowToAddCustomerToPackName_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowSubsidySelection, model.AllowSubsidySelection_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowEditSubsidyTopup, model.AllowEditSubsidyTopup_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowToAddByExcel, model.AllowToAddByExcel_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowSubsidyCountByExcel, model.AllowSubsidyCountByExcel_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.LimitAssignedBranches, model.LimitAssignedBranches_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNamesOwnGlobalValue, model.PackNamesOwnGlobalValue_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNamesOwnStartAndFinishDates, model.PackNamesOwnStartAndFinishDates_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ShippingMethod, model.ShippingMethod_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowHomeDelivery, model.AllowHomeDelivery_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowCustomersToAddAddress, model.AllowCustomersToAddAddress_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.DoNotPreSelectShippingAddress, model.DoNotPreSelectShippingAddress_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.IncludeShippingOnSubsidy, model.IncludeShippingOnSubsidy_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PackNameEnable, model.PackNameEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.CreditNotesEnable, model.CreditNotesEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.ExchangesEnable, model.ExchangesEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowCloseWithoutPrintingReturnNote, model.AllowCloseWithoutPrintingReturnNote_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.SubsidyEnable, model.SubsidyEnable_OverrideForStore, storeId, false);

            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderEnable, model.BulkOrderEnable_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderAxisAttributeIds, model.BulkOrderAxisAttributeIds_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderMaxCellsPerProduct, model.BulkOrderMaxCellsPerProduct_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderDefaultViewMode, model.BulkOrderDefaultViewMode_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderShowProductImages, model.BulkOrderShowProductImages_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderProductImageSize, model.BulkOrderProductImageSize_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderShowAttributeImages, model.BulkOrderShowAttributeImages_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderAttributeImageSize, model.BulkOrderAttributeImageSize_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderAttributeImageSquaresSize, model.BulkOrderAttributeImageSquaresSize_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderHideQtyArrows, model.BulkOrderHideQtyArrows_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderShowAttributePrices, model.BulkOrderShowAttributePrices_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderShowProductStock, model.BulkOrderShowProductStock_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderDefaultPageSize, model.BulkOrderDefaultPageSize_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderPageSizeOptions, model.BulkOrderPageSizeOptions_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderSendUserAfterLogin, model.BulkOrderSendUserAfterLogin_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.BulkOrderEnableOnProductPage, model.BulkOrderEnableOnProductPage_OverrideForStore, storeId, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.SubsidySendUserAfterLogin, model.SubsidySendUserAfterLogin_OverrideForStore, storeId, false);

            if (clearPreSelectedShippingAddress)
            {
                ClearSelectedShippingAddresses(storeId);
            }

            //EVERYTHING THAT REPORTS SUCCESS BELONGS INSIDE THE if. These four statements used to run
            //on both paths, so a validation failure saved nothing, said "updated successfully",
            //redirected, and discarded the admin's edits with no indication of which field was wrong.
            //StoreSettings (:2434) has had the correct shape all along - it re-prepares
            //the model and returns it - and this is the same thing for a full-page action.
            //publish event
            await _eventPublisher.PublishAsync(new MultiStoreSettingsChangedEvent());
            _cwFeatureService.ResetRequestCache();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Configuration.Updated"));

            await _customerActivityService.InsertActivityAsync("EditDefaultStoreSettings", await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditDefaultStoreSettings"));

            return RedirectToAction("Settings");
        }

        //Validation failed. Re-prepare the display-only fields the POST does not carry (available
        //product attributes, customer roles, the store list) and redisplay the form with the errors
        //and the admin's own values still in it.
        model = await _cwSettingModelFactory.PrepareMultiStoreSettingsModelAsync(model, 0);

        return View(model);
    }

    #endregion

    #endregion
}
