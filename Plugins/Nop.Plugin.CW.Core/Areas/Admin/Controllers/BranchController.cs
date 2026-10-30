using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Plugin.CW.Core.Services.Shipping;
using Nop.Services.Attributes;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

public class BranchController : BaseAdminController
{
    #region Fields

    protected readonly IBranchModelFactory _branchModelFactory;
    protected readonly IStoreService _storeService;
    protected readonly IBranchService _branchService;
    protected readonly IAddressService _addressService;
    protected readonly IAttributeParser<AddressAttribute, AddressAttributeValue> _addressAttributeParser;
    protected readonly INotificationService _notificationService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly IMultiStoreModelFactory _multiStoreModelFactory;
    protected readonly ICWAddressModelFactory _cwAddressModelFactory;
    protected readonly IPackNameProvisioningService _packNameProvisioningService;
    protected readonly IPackNameShippingMethodService _packNameShippingMethodService;

    #endregion

    #region Ctor

    public BranchController(IBranchModelFactory branchModelFactory,
        IStoreService storeService,
        IBranchService branchService,
        IAddressService addressService,
        IAttributeParser<AddressAttribute, AddressAttributeValue> addressAttributeParser,
        INotificationService notificationService,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        ICustomerActivityService customerActivityService,
        IMultiStoreModelFactory multiStoreModelFactory,
        ICWAddressModelFactory cwAddressModelFactory,
        IPackNameProvisioningService packNameProvisioningService,
        IPackNameShippingMethodService packNameShippingMethodService)
    {
        _branchModelFactory = branchModelFactory;
        _storeService = storeService;
        _branchService = branchService;
        _addressService = addressService;
        _addressAttributeParser = addressAttributeParser;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _customerActivityService = customerActivityService;
        _multiStoreModelFactory = multiStoreModelFactory;
        _cwAddressModelFactory = cwAddressModelFactory;
        _packNameProvisioningService = packNameProvisioningService;
        _packNameShippingMethodService = packNameShippingMethodService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Adds a model error when the parent is not a live branch of the store, or is the branch itself or one of its sub-branches
    /// </summary>
    /// <param name="branchId">The branch identifier; 0 for a new branch</param>
    /// <param name="parentBranchId">The posted parent branch identifier; null or 0 for a root branch</param>
    /// <param name="storeId">The store the branch belongs to</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task ValidateParentBranchAsync(int branchId, int? parentBranchId, int storeId)
    {
        if (!parentBranchId.HasValue || parentBranchId.Value == 0)
            return;

        var parentBranch = await _branchService.GetBranchByIdAsync(parentBranchId.Value);

        if (parentBranch == null || parentBranch.Deleted || parentBranch.StoreId != storeId)
        {
            ModelState.AddModelError(nameof(BranchModel.ParentBranchId),
                await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranch.WrongStore"));
            return;
        }

        if (branchId == 0)
            return;

        //walk up from the new parent: reaching the branch means the parent is the branch or one of its sub-branches
        var visited = new HashSet<int>();
        var current = parentBranch;

        while (current != null && visited.Add(current.Id))
        {
            if (current.Id == branchId)
            {
                ModelState.AddModelError(nameof(BranchModel.ParentBranchId),
                    await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranch.Cycle"));
                return;
            }

            current = current.ParentBranchId.HasValue
                ? await _branchService.GetBranchByIdAsync(current.ParentBranchId.Value)
                : null;
        }
    }

    #endregion

    #region Methods

    #region Branches 

    public virtual IActionResult Index()
    {
        return RedirectToAction("List");
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> List()
    {
        //prepare model
        var model = await _multiStoreModelFactory.PrepareMultiStoreSearchModelAsync(new MultiStoreSearchModel());

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> SearchBranches(int storeId, string term, int page = 1)
    {
        const int pageSize = 20;

        // Filtra tu entidad según el término buscado
        var branches = await _branchService.GetBranchesByStoreIdAsync(
            storeId: storeId,
            branchName: term,
            pageIndex: page - 1,
            pageSize: pageSize
        );

        var results = branches.Select(branch => new
        {
            id = branch.Id,
            text = branch.Name
        });

        // Select2 waits: { results: [...], pagination: { more: bool } }
        return Json(new
        {
            results,
            pagination = new { more = (page * pageSize) < branches.TotalCount }
        });
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> BranchList(BranchSearchModel searchModel)
    {
        //try to get a store with the specified id
        var store = await _storeService.GetStoreByIdAsync(searchModel.StoreId)
                         ?? throw new ArgumentException("No store found with the specified id");

        //prepare model
        var model = await _branchModelFactory.PrepareBranchListModelAsync(searchModel, store);

        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> Create()
    {
        //prepare model
        var model = await _branchModelFactory.PrepareBranchModelAsync(new BranchModel(), null);

        return View(model);
    }

    [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
    [FormValueRequired("save", "save-continue")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> Create(BranchModel model, bool continueEditing, IFormCollection form)
    {
        await ValidateParentBranchAsync(0, model.ParentBranchId, model.StoreId);

        if (ModelState.IsValid)
        {
            //only the names the freight plugin offers in the store are kept
            model.ShippingMethod = await _packNameShippingMethodService.KeepOfferedShippingMethodsAsync(model.BranchShippingMethods, model.StoreId);

            //fill entity from model
            var branch = model.ToEntity<Branch>();

            if (branch.ParentBranchId == 0)
                branch.ParentBranchId = null;

            branch.CreatedOnUtc = DateTime.UtcNow;
            branch.UpdatedOnUtc = DateTime.UtcNow;

            await _branchService.InsertBranchAsync(branch);

            //the store may ask for a default packname list on every new branch
            await _packNameProvisioningService.ProvisionNewBranchAsync(branch);

            //activity log
            await _customerActivityService.InsertActivityAsync("AddNewBranch",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewBranch"), branch.Id), branch);
            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Added"));

            if (!continueEditing)
                return RedirectToAction("List");

            return RedirectToAction("Edit", new { id = branch.Id });
        }

        //prepare model
        model = await _branchModelFactory.PrepareBranchModelAsync(model, null);

        //if we got this far, something failed, redisplay form
        return View("~/Plugins/CW.Core/Areas/Admin/Views/Branch/Create.cshtml", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> Edit(int id)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(id);
        if (branch == null || branch.Deleted)
            return RedirectToAction("List");

        //prepare model
        var model = await _branchModelFactory.PrepareBranchModelAsync(null, branch);

        return View(model);
    }

    [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
    [FormValueRequired("save", "save-continue")]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> Edit(BranchModel model, bool continueEditing, IFormCollection form)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.Id);
        if (branch == null || branch.Deleted)
            return RedirectToAction("List");

        //the branch keeps its store: the read-only store field posts it from a hidden input
        await ValidateParentBranchAsync(branch.Id, model.ParentBranchId, branch.StoreId);

        if (ModelState.IsValid)
        {
            try
            {
                branch.ParentBranchId = model.ParentBranchId;
                branch.BranchCode = model.BranchCode;
                branch.Name = model.Name;
                branch.Active = model.Active;
                branch.BranchNumber = model.BranchNumber;
                branch.CardCode = model.CardCode;
                branch.PaymentTerms = model.PaymentTerms;
                branch.ShippingMethod = await _packNameShippingMethodService.KeepOfferedShippingMethodsAsync(model.BranchShippingMethods, branch.StoreId);
                branch.UpdatedOnUtc = DateTime.UtcNow;

                if (branch.ParentBranchId == 0)
                    branch.ParentBranchId = null;

                await _branchService.UpdateBranchAsync(branch);

                //activity log
                await _customerActivityService.InsertActivityAsync("EditBranch",
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditBranch"), branch.Id), branch);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Updated"));

                if (!continueEditing)
                {
                    return RedirectToAction("List");
                }

                return RedirectToAction("Edit", new { id = branch.Id });
            }
            catch (Exception exc)
            {
                _notificationService.ErrorNotification(exc.Message);
            }
        }

        //prepare model
        model = await _branchModelFactory.PrepareBranchModelAsync(model, branch);

        //if we got this far, something failed, redisplay form
        return View("~/Plugins/CW.Core/Areas/Admin/Views/Branch/Edit.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> Delete(int id)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(id);
        if (branch == null || branch.Deleted)
            return RedirectToAction("List");

        //the Manage Store page's rule: a branch with sub-branches, packname lists or cost centres is kept
        if (await _branchService.BranchHasChildrenAsync(branch.Id))
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Delete.HasChildren"));
            return RedirectToAction("Edit", new { id = branch.Id });
        }

        try
        {
            //delete
            await _branchService.DeleteBranchAsync(branch);

            //activity log
            await _customerActivityService.InsertActivityAsync("DeleteBranch",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteBranch"), branch.Id), branch);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Deleted"));

            return RedirectToAction("List");
        }
        catch (Exception exc)
        {
            _notificationService.ErrorNotification(exc.Message);
            return RedirectToAction("Edit", new { id = branch.Id });
        }
    }

    #endregion

    #region Addresses

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> AddressesSelect(CWAddressSearchModel searchModel)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(searchModel.EntityId)
            ?? throw new ArgumentException("No branch found with the specified id");

        //prepare model
        var model = await _branchModelFactory.PrepareBranchAddressListModelAsync(searchModel, branch);

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> AddressDelete(int id, int branchId)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(branchId)
            ?? throw new ArgumentException("No branch found with the specified id", nameof(branchId));

        //try to get an address with the specified id
        var address = await _branchService.GetBranchAddressAsync(branch.Id, id);

        if (address == null)
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        await _branchService.RemoveBranchAddressAsync(branch, address);

        //now delete the address record
        await _addressService.DeleteAddressAsync(address);

        return new NullJsonResult();
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> AddressCreate(int branchId)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(branchId);
        if (branch == null)
            return RedirectToAction("List");

        //prepare model
        var model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(new CWAddressModel(), branch, null);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Branch/AddressCreate.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> AddressCreate(CWAddressModel model, IFormCollection form)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.EntityId);
        if (branch == null)
            return RedirectToAction("List");

        //custom address attributes
        var customAttributes = await _addressAttributeParser.ParseCustomAttributesAsync(form, NopCommonDefaults.AddressAttributeControlName);
        var customAttributeWarnings = await _addressAttributeParser.GetAttributeWarningsAsync(customAttributes);
        foreach (var error in customAttributeWarnings)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (ModelState.IsValid)
        {
            var address = model.Address.ToEntity<Address>();
            address.CustomAttributes = customAttributes;
            address.CreatedOnUtc = DateTime.UtcNow;

            //some validation
            if (address.CountryId == 0)
                address.CountryId = null;
            if (address.StateProvinceId == 0)
                address.StateProvinceId = null;

            await _addressService.InsertAddressAsync(address);

            await _branchService.InsertBranchAddressAsync(branch, address);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Addresses.Added"));

            return RedirectToAction("AddressEdit", new { addressId = address.Id, branchId = model.EntityId });
        }

        //prepare model
        model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(model, branch, null, true);

        //if we got this far, something failed, redisplay form
        return View("~/Plugins/CW.Core/Areas/Admin/Views/Branch/AddressCreate.cshtml", model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> AddressEdit(int addressId, int branchId)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(branchId);
        if (branch == null)
            return RedirectToAction("List");

        //try to get an address with the specified id
        var address = await _addressService.GetAddressByIdAsync(addressId);
        if (address == null)
            return RedirectToAction("Edit", new { id = branch.Id });

        //prepare model
        var model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(null, branch, address);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Branch/AddressEdit.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> AddressEdit(CWAddressModel model, IFormCollection form)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.EntityId);
        if (branch == null)
            return RedirectToAction("List");

        //try to get an address with the specified id
        var address = await _addressService.GetAddressByIdAsync(model.Address.Id);
        if (address == null)
            return RedirectToAction("Edit", new { id = branch.Id });

        //custom address attributes
        var customAttributes = await _addressAttributeParser.ParseCustomAttributesAsync(form, NopCommonDefaults.AddressAttributeControlName);
        var customAttributeWarnings = await _addressAttributeParser.GetAttributeWarningsAsync(customAttributes);
        foreach (var error in customAttributeWarnings)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (ModelState.IsValid)
        {
            address = model.Address.ToEntity(address);
            address.CustomAttributes = customAttributes;
            await _addressService.UpdateAddressAsync(address);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Addresses.Updated"));

            return RedirectToAction("AddressEdit", new { addressId = model.Address.Id, branchId = model.EntityId });
        }

        //prepare model
        model = await _cwAddressModelFactory.PrepareCWAddressModelAsync(model, branch, address, true);

        //if we got this far, something failed, redisplay form
        return View("~/Plugins/CW.Core/Areas/Admin/Views/Branch/AddressEdit.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_BRANCH)]
    public virtual async Task<IActionResult> AddressSetDefault(int branchId, int addressId)
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

    #endregion
}