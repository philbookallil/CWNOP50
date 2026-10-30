using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Shipping;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class BranchModelFactory : IBranchModelFactory
{
    #region Fields

    protected readonly IBranchService _branchService;
    protected readonly IStoreContext _storeContext;
    protected readonly IBaseAdminModelFactory _baseAdminModelFactory;
    protected readonly ICWAddressModelFactory _cwAddressModelFactory;
    protected readonly IStoreService _storeService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPackNameShippingMethodService _packNameShippingMethodService;

    #endregion

    #region Ctor

    public BranchModelFactory(IBranchService branchService,
        IStoreContext storeContext,
        IBaseAdminModelFactory baseAdminModelFactory,
        ICWAddressModelFactory cwAddressModelFactory,
        IStoreService storeService,
        ILocalizationService localizationService,
        IPackNameShippingMethodService packNameShippingMethodService)
    {
        _branchService = branchService;
        _storeContext = storeContext;
        _baseAdminModelFactory = baseAdminModelFactory;
        _cwAddressModelFactory = cwAddressModelFactory;
        _storeService = storeService;
        _localizationService = localizationService;
        _packNameShippingMethodService = packNameShippingMethodService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Prepare available branches
    /// </summary>
    /// <param name="storeId">The store identifier</param>
    /// <param name="items">Branch items</param>
    /// <param name="withSpecialDefaultItem">Whether to insert the first special item for the default value</param>
    /// <param name="defaultItemText">Default item text; pass null to use default value of the default item text</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task PrepareBranchesAsync(int storeId, IList<SelectListItem> items, bool withSpecialDefaultItem = true, string defaultItemText = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        //prepare available branches
        var availableBranchItems = await GetBranchListAsync(storeId);
        foreach (var branchItem in availableBranchItems)
        {
            items.Add(branchItem);
        }

        //whether to insert the first special item for the default value
        if (!withSpecialDefaultItem)
            return;

        //prepare item text
        defaultItemText ??= await _localizationService.GetResourceAsync("Admin.Common.All");

        //insert this default item at first
        items.Insert(0, new SelectListItem { Text = defaultItemText, Value = "0" });
    }


    /// <summary>
    /// Get branch list
    /// </summary>
    /// <param name="storeId">The store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch list
    /// </returns>
    protected virtual async Task<List<SelectListItem>> GetBranchListAsync(int storeId)
    {
        var branches = await _branchService.GetAllBranchesByStoreIdAsync(storeId);

        var result = new List<SelectListItem>();
        foreach (var branch in branches)
        {
            result.Add(new SelectListItem
            {
                Text = await _branchService.GetFormattedBreadCrumbAsync(storeId, branch, branches),
                Value = branch.Id.ToString()
            });
        }

        return result;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepare paged branch list model
    /// </summary>
    /// <param name="searchModel">Branch search model</param>
    /// <param name="store">Store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch list model
    /// </returns>
    public virtual async Task<BranchListModel> PrepareBranchListModelAsync(BranchSearchModel searchModel, Store store)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        ArgumentNullException.ThrowIfNull(store);

        //get forums
        var branches = await _branchService.GetBranchesByStoreIdAsync(storeId: store.Id,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        var allBranches = await _branchService.GetAllBranchesByStoreIdAsync(store.Id);

        //prepare grid model
        var model = await new BranchListModel().PrepareToGridAsync(searchModel, branches, () =>
        {
            return branches.SelectAwait(async branch =>
            {
                //fill in model values from the entity
                return new BranchModel
                {
                    Id = branch.Id,
                    Name = branch.Name,
                    BranchCode = branch.BranchCode,
                    ParentBranchId = branch.ParentBranchId,
                    StaffCount = await _branchService.GetStaffCountAsync(branch.Id),
                    Breadcrumb = await _branchService.GetFormattedBreadCrumbAsync(store.Id, branch, allBranches)
                };
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare branch model
    /// </summary>
    /// <param name="model">Branch model</param>
    /// <param name="branch">Branch</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch model
    /// </returns>
    public virtual async Task<BranchModel> PrepareBranchModelAsync(BranchModel model, Branch branch)
    {
        Store store = null;

        if (branch != null)
        {
            store = await _storeService.GetStoreByIdAsync(branch.StoreId);

            //fill in model values from the entity
            if (model == null)
            {
                model = new BranchModel
                {
                    Id = branch.Id,
                    StoreId = branch.StoreId,
                    ParentBranchId = branch.ParentBranchId,
                    BranchCode = branch.BranchCode,
                    Name = branch.Name,
                    CardCode = branch.CardCode,
                    BranchNumber = branch.BranchNumber,
                    Active = branch.Active,
                    PaymentTerms = branch.PaymentTerms,
                    ShippingMethod = branch.ShippingMethod
                };
            }

            model.StoreName = store.Name;

            //prepare nested search models
            await _cwAddressModelFactory.PrepareCWAddressSearchModelAsync(model.CWAddressSearchModel, branch.Id);
        }
        else
        {
            //a new form takes the current store; a refused post keeps the store it chose
            var isNewForm = model.StoreId == 0;

            store = (isNewForm ? null : await _storeService.GetStoreByIdAsync(model.StoreId))
                ?? await _storeContext.GetCurrentStoreAsync();

            model.StoreId = store.Id;
            model.StoreName = store.Name;

            if (isNewForm)
                model.Active = true;

            //prepare available stores
            await _baseAdminModelFactory.PrepareStoresAsync(model.AvailableStores, false);
        }

        //prepare available parent branches
        await PrepareBranchesAsync(store.Id, model.AvailableBranches,
            defaultItemText: await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranchId.None"));

        //the methods the freight plugin offers in the store, for the branch's own selection
        foreach (var shippingMethodName in await _packNameShippingMethodService.GetShippingMethodNamesAsync(store.Id))
            model.AvailableShippingMethods.Add(new SelectListItem { Value = shippingMethodName, Text = shippingMethodName });

        return model;
    }

    public virtual async Task<BranchModel> PrepareBranchModelAsync(int storeId, int? branchId, int? parentBranchId)
    {
        BranchModel model;

        if (branchId.HasValue)
        {
            var branch = await _branchService.GetBranchByIdAsync(branchId.Value);

            string parentName = null;
            if (branch.ParentBranchId.HasValue)
            {
                var parent = await _branchService
                    .GetBranchByIdAsync(branch.ParentBranchId.Value);
                parentName = parent?.Name;
            }

            model = new BranchModel
            {
                Id = branch.Id,
                Name = branch.Name,
                BranchCode = branch.BranchCode,
                CardCode = branch.CardCode,
                BranchNumber = branch.BranchNumber,
                StoreId = storeId,
                Active = branch.Active,
                PaymentTerms = branch.PaymentTerms,
                ShippingMethod = branch.ShippingMethod,
                ParentBranchId = branch.ParentBranchId,
                ParentBranchName = parentName,
                IsNew = false,

                //THIS IS THE OVERLOAD THE BRANCH DETAIL PANEL USES. MultiStoreController.BranchDetail
                //calls this one, not PrepareBranchModelAsync(model, branch) - the two build
                //their models independently, and only the list path ever filled StaffCount, so the
                //detail header reported 0 for every branch. Cached per branch by GetStaffCountAsync,
                //and both CustomerBranchCacheEventConsumer and CustomerCacheEventConsumer clear that
                //prefix, so it cannot go stale.
                StaffCount = await _branchService.GetStaffCountAsync(branch.Id),
            };
        }
        else
        {
            // New branch form
            string newParentName = null;
            if (parentBranchId.HasValue)
            {
                var parent = await _branchService
                    .GetBranchByIdAsync(parentBranchId.Value);
                newParentName = parent?.Name;
            }

            model = new BranchModel
            {
                StoreId = storeId,
                ParentBranchId = parentBranchId,
                ParentBranchName = newParentName,
                Active = true,
                IsNew = true,
            };
        }

        //the methods the freight plugin offers in the store, for the branch's own selection
        foreach (var shippingMethodName in await _packNameShippingMethodService.GetShippingMethodNamesAsync(storeId))
            model.AvailableShippingMethods.Add(new SelectListItem { Value = shippingMethodName, Text = shippingMethodName });

        return model;
    }

    /// <summary>
    /// Prepare paged branch address list model
    /// </summary>
    /// <param name="searchModel">Branch address search model</param>
    /// <param name="branch">Branch</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch address list model
    /// </returns>
    public virtual async Task<CWAddressListModel> PrepareBranchAddressListModelAsync(CWAddressSearchModel searchModel, Branch branch)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        ArgumentNullException.ThrowIfNull(branch);

        var addressesModel = new List<CWAddressModel>();

        //get branch addresses
        var addresses = await _branchService.GetAddressesByBranchIdAsync(branch.Id);

        foreach (var address in addresses)
        {
            var addressModel = await _cwAddressModelFactory.PrepareCWAddressModelAsync(address);
            addressModel.IsDefault = address.Id == branch.DefaultAddressId;
            addressesModel.Add(addressModel);
        }

        var pagedAddressesModel = addressesModel.ToList().ToPagedList(searchModel);

        var model = await new CWAddressListModel().PrepareToGridAsync(searchModel, pagedAddressesModel, () =>
        {
            return pagedAddressesModel.SelectAwait(async addressModel => addressModel);
        });

        return model;
    }

    /// <summary>
    /// Prepare the branch's server-rendered address collection
    /// </summary>
    /// <param name="branch">Branch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task<BranchAddressesModel> PrepareBranchAddressesModelAsync(Branch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        //SAME PREPARATION AS THE GRID, WITHOUT THE PAGING. The branch page renders its collections
        //server-side now; PrepareBranchAddressListModelAsync stays because BranchController still
        //has a real grid (BranchController.cs:290).
        var model = new BranchAddressesModel { BranchId = branch.Id };

        var addresses = await _branchService.GetAddressesByBranchIdAsync(branch.Id);

        foreach (var address in addresses)
        {
            var addressModel = await _cwAddressModelFactory.PrepareCWAddressModelAsync(address);
            addressModel.IsDefault = address.Id == branch.DefaultAddressId;
            model.Addresses.Add(addressModel);
        }

        return model;
    }

    #endregion
}
