using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames.Import;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Framework.Models.Extensions;
using PackNameImportModel = Nop.Plugin.CW.Core.Models.PackNames.PackNameImportModel;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class PackNameModelFactory : IPackNameModelFactory
{
    #region Fields

    protected readonly ICWSettingModelFactory _cwSettingModelFactory;
    protected readonly IPackNameService _packNameService;
    protected readonly IBranchService _branchService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly ICustomerOverrideService _customerService;
    protected readonly ILocalizationService _localizationService;
    protected readonly CustomerSettings _customerSettings;
    protected readonly ISettingService _settingService;
    protected readonly ICWAddressModelFactory _cwAddressModelFactory;
    protected readonly IStoreService _storeService;
    protected readonly IAddressService _addressService;

    #endregion

    #region Ctor

    public PackNameModelFactory(ICWSettingModelFactory cwSettingModelFactory,
        IPackNameService packNameService,
        IBranchService branchService,
        IMultiStoreService multiStoreService,
        ICustomerOverrideService customerService,
        ILocalizationService localizationService,
        CustomerSettings customerSettings,
        ISettingService settingService,
        ICWAddressModelFactory cwAddressModelFactory,
        IStoreService storeService,
        IAddressService addressService)
    {
        _cwSettingModelFactory = cwSettingModelFactory;
        _packNameService = packNameService;
        _branchService = branchService;
        _multiStoreService = multiStoreService;
        _customerService = customerService;
        _localizationService = localizationService;
        _customerSettings = customerSettings;
        _settingService = settingService;
        _cwAddressModelFactory = cwAddressModelFactory;
        _storeService = storeService;
        _addressService = addressService;
    }

    #endregion

    #region Utilities

    protected virtual async Task PrepareModelCustomerAsync(PackNameModel model)
    {
        if (model.CustomerId.HasValue && model.CustomerId.Value > 0)
        {
            var customer = await _customerService.GetCustomerByIdAsync(model.CustomerId.Value);
            model.CustomerName = await _customerService.GetCustomerFullNameAsync(customer);
        }
    }

    protected async Task PrepareAvailableManagerOptions(IList<SelectListItem> listItems)
    {
        //prepare "manager" filter (0 - all; 1 - managers only; 2 - non-managers only)
        listItems.Add(new SelectListItem
        {
            Value = "0",
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.SearchManager.All")
        });
        listItems.Add(new SelectListItem
        {
            Value = "1",
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.SearchManager.ManagersOnly")
        });
        listItems.Add(new SelectListItem
        {
            Value = "2",
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.SearchManager.NonManagersOnly")
        });
    }

    /// <summary>
    /// The lists of a branch a packname may go into: its active lists, and the list given (the packname's own, which
    /// it keeps even when switched off). With neither, the only option is "none", for the branch's default list to be
    /// created on save
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list selected (0 for "none"), which the picker shows only as the model's value
    /// </returns>
    protected async Task<int> PrepareAvailablePackNameListAsync(IList<SelectListItem> listItems, int branchId, int listId)
    {
        if (branchId == 0)
            return listId;

        var packNameLists = (await _packNameService.GetPackNameListsByBranchAsync(branchId))
            .Where(packNameList => packNameList.Active || packNameList.Id == listId)
            .ToList();
        var activeLists = packNameLists.Where(packNameList => packNameList.Active).ToList();
        var selectedId = packNameLists.Any(packNameList => packNameList.Id == listId)
            ? listId
            : (activeLists.FirstOrDefault(packNameList => packNameList.IsDefault) ?? activeLists.FirstOrDefault())?.Id ?? 0;

        //nothing to keep and nothing active: "none", for the branch's default list to be created on save
        if (packNameLists.Count == 0)
        {
            listItems.Add(new SelectListItem
            {
                Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList.DoesNotExist"),
                Value = "0",
                Selected = selectedId == 0
            });
        }

        foreach (var packNameList in packNameLists)
        {
            listItems.Add(new SelectListItem
            {
                Text = packNameList.Name,
                Value = $"{packNameList.Id}",
                Selected = packNameList.Id == selectedId
            });
        }

        return selectedId;
    }

    /// <summary>
    /// Runs the packname search of the admin grids: the store, the list and every filter of the search model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packnames of the page, ordered by name
    /// </returns>
    protected virtual async Task<IPagedList<PackNameDTO>> SearchPackNamesAsync(PackNameSearchModel searchModel, int pageIndex, int pageSize)
    {
        return await _packNameService.GetPackNamesAsync(searchModel.StoreId, searchModel.PackNameListId, searchModel.SearchPackName,
            searchModel.SearchEmail, searchModel.SearchPackNameType, searchModel.SearchGender, searchModel.SearchDepartmentId,
            searchModel.SearchCostCenterId, searchModel.SearchManager == 0 ? null : searchModel.SearchManager == 1, searchModel.SearchBranchName,
            pageIndex: pageIndex, pageSize: pageSize);
    }

    #endregion

    #region Methods

    #region PackName Lists

    /// <summary>
    /// Prepare packaging name list model
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <param name="listId">Packaging name list identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name list model
    /// </returns>
    public virtual async Task<PackNameListModel> PreparePackNameListModelAsync(int branchId, int? listId)
    {
        var branch = await _branchService.GetBranchByIdAsync(branchId);

        // New list form
        var model = new PackNameListModel
        {
            StoreId = branch?.StoreId,
            BranchId = branchId,
            Branch = branch?.Name,
            Active = true,
            IsNew = true,
        };

        if (listId.HasValue)
        {
            var list = await _packNameService.GetPackNameListByIdAsync(listId.Value);

            model.Id = list.Id;
            model.Name = list.Name;
            model.AllManagers = list.AllManagers;
            model.ParentManagers = list.ParentManagers;
            model.IsDefault = list.IsDefault;
            model.Active = list.Active;
            model.IsNew = false;

            //prepare manager search model
            PrepareManagerSearchModel(model.ManagerSearchModel, list);
        }

        //THE MANAGERS GRID READS THIS AND NOTHING WAS FILLING IT. _Branches.PackNameListDetail
        //passes its own Model straight to _Branches.PackNameListDetail.Managers, which gates four
        //columns on MultiStoreSettingsModel (EmployeeNumberEnabled, PrintNameEnabled, GenderEnabled,
        //POEntryEnabled). The only code that ever populated it was the (model, entity) overload of
        //this method, which had no callers — so the record's constructor default was what the view
        //saw, every flag false, and those four columns were hidden whatever the store had configured.
        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(model.MultiStoreSettingsModel, branch?.StoreId ?? 0);

        return model;
    }

    /// <summary>
    /// Prepare manager search model
    /// </summary>
    /// <param name="searchModel">Manager search model</param>
    /// <returns>Manager search model</returns>
    public ManagerSearchModel PrepareManagerSearchModel(ManagerSearchModel searchModel, PackNameList packNameList)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.PackNameListId = packNameList?.Id ?? 0;

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged manager list model to add to the packNameList
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the packNameList</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager list model to add to the packNameList
    /// </returns>
    public virtual async Task<PackNamesListModel> PrepareManagerListModelAsync(ManagerSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get managers
        var managers = await _packNameService.GetManagersByPackNameListIdAsync(searchModel.PackNameListId,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = new PackNamesListModel().PrepareToGrid(searchModel, managers, () =>
        {
            return managers.Select(manager =>
            {
                var packNameModel = new PackNameModel
                {
                    Id = manager.Id,
                    Name = manager.Name,
                    PrintName = manager.PrintName,
                    EmployeeNumber = manager.EmployeeNumber,
                    POEntry = manager.POEntry,
                    Active = manager.Active,
                    Gender = manager.Gender,
                    Group = manager.Group,
                    IsCustomer = manager.CustomerId.HasValue,
                    CustomerName = manager.Email,
                    Branch = manager.Branch,
                };

                return packNameModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare manager search model to add to the packNameList
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the packNameList</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager search model to add to the packNameList
    /// </returns>
    public async Task<AddPackNameListManagerSearchModel> PrepareAddManagerSearchModelAsync(AddPackNameListManagerSearchModel searchModel, PackNameList packNameList)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.PackNameListId = packNameList.Id;
        searchModel.PackNameList = packNameList.Name;

        //prepare page parameters
        searchModel.SetPopupGridPageSize();

        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(searchModel.MultiStoreSettingsModel, packNameList?.StoreId ?? 0);

        return searchModel;
    }

    /// <summary>
    /// Prepare paged manager list model to add to the packNameList
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the packNameList</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager list model to add to the packNameList
    /// </returns>
    public virtual async Task<PackNamesListModel> PrepareAddManagerListModelAsync(AddPackNameListManagerSearchModel searchModel, PackNameList packNameList)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get managers
        var managers = await _multiStoreService.GetPackNameManagersByStoreAsync(packNameList?.StoreId ?? 0, packNameList?.BranchId ?? 0, searchModel.PackNameListId, searchModel.SearchManagerName, searchModel.SearchOtherBranches,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = new PackNamesListModel().PrepareToGrid(searchModel, managers, () =>
        {
            return managers.Select(manager =>
            {
                var packNameModel = new PackNameModel
                {
                    Id = manager.Id,
                    Name = manager.Name,
                    PrintName = manager.PrintName,
                    EmployeeNumber = manager.EmployeeNumber,
                    POEntry = manager.POEntry,
                    Active = manager.Active,
                    Gender = manager.Gender,
                    Group = manager.Group,
                    IsCustomer = manager.CustomerId.HasValue,
                    CustomerName = manager.Email,
                    Branch = manager.Branch,
                };

                return packNameModel;
            });
        });

        return model;
    }

    #endregion

    #region PackNames

    /// <summary>
    /// Prepare packName search model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <returns>PackName search model</returns>
    public virtual async Task<PackNameSearchModel> PreparePackNameSearchModelAsync(PackNameSearchModel searchModel, PackNameList packNameList)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        ArgumentNullException.ThrowIfNull(packNameList);

        searchModel.StoreId = packNameList.StoreId;
        searchModel.PackNameListId = packNameList.Id;
        searchModel.ListName = packNameList.Name;
        searchModel.IsDefault = packNameList.IsDefault;

        //prepare page parameters
        searchModel.SetGridPageSize();

        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(searchModel.MultiStoreSettingsModel, packNameList?.StoreId ?? 0);

        return searchModel;
    }

    /// <summary>
    /// Prepare packName search model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>PackName search model</returns>
    public virtual async Task<PackNameSearchModel> PreparePackNameSearchModelAsync(PackNameSearchModel searchModel, int storeId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.StoreId = storeId;

        //NO activeOnly: this is the SEARCH filter. Active means "no new assignments", not "hidden" -
        //a deactivated department keeps its packnames, and dropping it here makes them unfindable.
        await PrepareAvailableDepartmentsAsync(searchModel.AvailableDepartments, storeId);
        await PrepareAvailableCostCentersAsync(searchModel.AvailableCostCenters, storeId);
        await PrepareAvailableManagerOptions(searchModel.AvailableManagerOptions);

        await PrepareAvailableGendersAsync(searchModel.AvailableGenders, null, true);
        await PrepareAvailablePackNameTypesAsync(searchModel.AvailablePackNameTypes);

        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(searchModel.MultiStoreSettingsModel, storeId);

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged packName list model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName list model
    /// </returns>
    public virtual async Task<PackNamesListModel> PreparePackNamesListModelAsync(PackNameSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var packNames = await SearchPackNamesAsync(searchModel, searchModel.Page - 1, searchModel.PageSize);

        //prepare grid model
        var model = await new PackNamesListModel().PrepareToGridAsync(searchModel, packNames, () =>
        {
            return packNames.SelectAwait(async packName =>
            {
                var packNameModel = new PackNameModel
                {
                    Id = packName.Id,
                    PackNameListId = packName.PackNameListId,
                    Name = packName.Name,
                    PrintName = packName.PrintName,
                    EmployeeNumber = packName.EmployeeNumber,
                    POEntry = packName.POEntry,
                    Active = packName.Active,
                    Gender = packName.Gender,
                    Group = packName.Group,
                    IsCustomer = packName.IsCustomer,
                    Manager = packName.Manager,
                    Department = packName.Department,
                    CostCenter = packName.CostCenter,
                    Branch = packName.Branch,
                };

                return packNameModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Gets the packnames the grid's search matches, on every page, for the Excel export
    /// </summary>
    /// <param name="searchModel">PackName search model, with the store, the list and the filters the grid posts</param>
    /// <param name="selectedIds">The packnames ticked in the grid; empty for every packname the search matches</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packnames, ordered by name
    /// </returns>
    public virtual async Task<IList<PackNameDTO>> PreparePackNamesForExportAsync(PackNameSearchModel searchModel, ICollection<int> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //a ticked packname is always one the search matched: the grid clears its selection when the search changes, so
        //filtering the search keeps the export inside the store and the list
        var packNames = await SearchPackNamesAsync(searchModel, 0, int.MaxValue);

        if (selectedIds == null || selectedIds.Count == 0)
            return packNames.ToList();

        var selected = selectedIds.ToHashSet();

        return packNames.Where(packName => selected.Contains(packName.Id)).ToList();
    }

    /// <summary>
    /// Prepare packaging name model
    /// </summary>
    /// <param name="model">Packaging name model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="branchId">Branch identifier</param>
    /// <param name="listId">Packaging name list identifier</param>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name list model
    /// </returns>
    public virtual async Task<PackNameModel> PreparePackNameModelAsync(PackNameModel model, int? storeId, int? branchId, int? listId, int? packNameId)
    {
        var packName = packNameId.HasValue ? await _packNameService.GetPackNameByIdAsync(packNameId.Value) : null;
        var packNameList = packName?.PackNameListId > 0 ? await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId) : (listId.HasValue ? await _packNameService.GetPackNameListByIdAsync(listId.Value) : null);
        var branch = packNameList?.BranchId > 0 ? await _branchService.GetBranchByIdAsync(packNameList.BranchId ?? 0) : (branchId.HasValue ? await _branchService.GetBranchByIdAsync(branchId.Value) : null);

        if (model == null)
        {
            // New packName form
            model = new PackNameModel
            {
                Active = true,
                IsNew = true,
            };

            if (packName != null)
            {
                model = packName.ToModel(model);
                model.IsNew = false;
            }
        }

        model.StoreId = storeId ?? packNameList?.StoreId ?? 0;
        model.BranchId = branch?.Id ?? 0;
        model.Branch = branch?.Name;
        model.PackNameListId = packName?.PackNameListId ?? listId ?? 0;
        model.PackNameList = packNameList?.Name;

        if (packName != null)
        {
            await _cwAddressModelFactory.PrepareCWAddressSearchModelAsync(model.CWAddressSearchModel, packName.Id);
        }

        //prepare model customers
        await PrepareModelCustomerAsync(model);
        await PrepareAvailableGendersAsync(model.AvailableGenders, packName?.Gender, false);
        //activeOnly with the CURRENT value kept: this is an assignment picker, and packName is null on
        //an insert so keepId is null there. The keep comes from the ENTITY, never from the model - a
        //posted DepartmentId is a request value and this phase's rule 3 says the store, and everything
        //hanging off it, comes from the entity.
        await PrepareAvailableDepartmentsAsync(model.AvailableDepartments, storeId ?? packNameList?.StoreId ?? 0, activeOnly: true, keepId: packName?.DepartmentId);
        await PrepareAvailableCostCentersAsync(model.AvailableCostCenters, storeId ?? packNameList?.StoreId ?? 0, activeOnly: true, keepId: packName?.CostCenterId);
        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(model.MultiStoreSettingsModel, storeId ?? packNameList?.StoreId ?? 0);

        if (model.IsNew)
        {
            if (branch != null)
            {
                model.AvailableBranches.Add(new SelectListItem
                {
                    Text = branch.Name,
                    Value = $"{branch.Id}",
                    Selected = true
                });
            }
        }

        if (branch != null)
            model.PackNameListId = await PrepareAvailablePackNameListAsync(model.AvailableLists, branch.Id, packNameList?.Id ?? 0);

        return model;
    }

    /// <summary>
    /// Prepare paged packName address list model
    /// </summary>
    /// <param name="searchModel">PackName address search model</param>
    /// <param name="packName">PackName</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName address list model
    /// </returns>
    public virtual async Task<CWAddressListModel> PreparePackNameAddressListModelAsync(CWAddressSearchModel searchModel, PackName packName)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        ArgumentNullException.ThrowIfNull(packName);

        var addressesModel = new List<CWAddressModel>();

        //get packName addresses
        var addresses = await _packNameService.GetAddressesByPackNameIdAsync(packName.Id);

        foreach (var address in addresses)
            addressesModel.Add(await _cwAddressModelFactory.PrepareCWAddressModelAsync(address));

        //If the packName is a customer, then we need to show the customer addresses
        if (packName.IsCustomer)
        {
            var customerAddresses = await _customerService.GetAddressesByCustomerIdAsync(packName.CustomerId ?? 0);

            //an account registered with its packname holds its own copy of the packname's address: the same address
            var packNameAddresses = addresses.ToList();

            foreach (var address in customerAddresses)
            {
                if (_addressService.FindAddress(packNameAddresses, address.FirstName, address.LastName, address.PhoneNumber, address.Email,
                        address.FaxNumber, address.Company, address.Address1, address.Address2, address.City, address.County,
                        address.StateProvinceId, address.ZipPostalCode, address.CountryId, address.CustomAttributes) != null)
                    continue;

                var addressModel = await _cwAddressModelFactory.PrepareCWAddressModelAsync(address);

                addressModel.IsEditable = false;
                addressModel.BranchCode = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackNameAddress.FromCustomer");

                addressesModel.Add(addressModel);
            }
        }

        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

        //get settings for the store
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNameList.StoreId);

        //Show ship addresses in the addresses list for the packName without edit or delete options
        var branch = await _branchService.GetBranchByIdAsync(packNameList?.BranchId ?? 0);
        var branchAddresses = await _branchService.GetAddressesByBranchIdAsync(packNameList?.BranchId ?? 0);

        foreach (var address in branchAddresses)
        {
            if (addressesModel.Select(x => x.AddressId).Contains(address.Id))
                continue;

            var addressModel = await _cwAddressModelFactory.PrepareCWAddressModelAsync(address);

            addressModel.IsEditable = false;
            addressModel.BranchCode = branch?.BranchCode ?? string.Empty;

            addressesModel.Add(addressModel);
        }

        var pagedAddressesModel = addressesModel.OrderByDescending(address => address.Address?.Id ?? 0).ToList().ToPagedList(searchModel);

        var model = await new CWAddressListModel().PrepareToGridAsync(searchModel, pagedAddressesModel, () =>
        {
            return pagedAddressesModel.SelectAwait(async logItem => logItem);
        });

        return model;
    }

    /// <summary>
    /// Populates the specified list with available gender options for selection in a user interface.
    /// </summary>
    /// <param name="listItems">The list to which gender options will be added. Must not be null.</param>
    /// <param name="selectedGender">The gender value that should be marked as selected in the resulting list. Can be null or empty if no selection
    /// is required.</param>
    /// <param name="addAllOption">A value indicating whether to include an 'All' option at the beginning of the list. Set to <see
    /// langword="true"/> to add the option; otherwise, <see langword="false"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task PrepareAvailableGendersAsync(IList<SelectListItem> listItems, string selectedGender, bool addAllOption)
    {
        if (addAllOption)
        {
            listItems.Add(new SelectListItem
            {
                Text = await _localizationService.GetResourceAsync("Admin.Common.All"),
                Value = "",
                Selected = true
            });
        }

        listItems.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Mixed"),
            Value = MultiStoreServicesDefaults.GenderMixed,
            Selected = !addAllOption && (selectedGender == MultiStoreServicesDefaults.GenderMixed || string.IsNullOrEmpty(selectedGender))
        });

        listItems.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Male"),
            Value = MultiStoreServicesDefaults.GenderMale,
            Selected = !addAllOption && selectedGender == MultiStoreServicesDefaults.GenderMale
        });

        listItems.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Female"),
            Value = MultiStoreServicesDefaults.GenderFemale,
            Selected = !addAllOption && selectedGender == MultiStoreServicesDefaults.GenderFemale
        });

        if (_customerSettings.NeutralGenderEnabled)
        {
            listItems.Add(new SelectListItem
            {
                Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Neutral"),
                Value = MultiStoreServicesDefaults.GenderNeutral,
                Selected = !addAllOption && selectedGender == MultiStoreServicesDefaults.GenderNeutral
            });
        }
    }

    /// <summary>
    /// Populates the specified list with available department options for selection in a user interface based on the provided store identifier.
    /// </summary>
    /// <param name="listItems">The list to which department options will be added. Must not be null.</param>
    /// <param name="storeId">The identifier of the store for which department options should be retrieved.</param>
    /// <param name="activeOnly">
    /// True for a picker that ASSIGNS a department, false for a search filter.
    /// Active means "no new assignments", so an inactive department leaves the assignment pickers and
    /// stays in the search filters - otherwise the packnames already on it become unfindable.
    /// </param>
    /// <param name="keepId">
    /// The department the entity being edited is ALREADY on. Always offered even when inactive and
    /// even when <paramref name="activeOnly"/> is true, so that re-saving the form cannot silently
    /// move the entity off it. Null on an insert.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task PrepareAvailableDepartmentsAsync(IList<SelectListItem> listItems, int storeId, bool activeOnly = false, int? keepId = null)
    {
        listItems.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Admin.Common.All"), Value = "0" });

        var departments = await _multiStoreService.GetDepartmentsByStoreIdAsync(storeId);

        foreach (var department in departments)
        {
            if (activeOnly && !department.Active && department.Id != keepId)
                continue;

            listItems.Add(new SelectListItem
            {
                Text = department.Name,
                Value = $"{department.Id}",
            });
        }
    }

    /// <summary>
    /// Populates the specified list with available cost center options for selection in a user interface based on the provided store identifier.
    /// </summary>
    /// <param name="listItems">The list to which cost center options will be added. Must not be null.</param>
    /// <param name="storeId">The identifier of the store for which cost center options should be retrieved.</param>
    /// <param name="activeOnly">True for a picker that ASSIGNS a cost centre, false for a search filter. See the department overload.</param>
    /// <param name="keepId">The cost centre the entity being edited is already on; always offered. Null on an insert.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task PrepareAvailableCostCentersAsync(IList<SelectListItem> listItems, int storeId, bool activeOnly = false, int? keepId = null)
    {
        listItems.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Admin.Common.All"), Value = "0" });

        var costCenters = await _multiStoreService.GetCostCenterSelectItemsAsync(storeId);

        foreach (var (Id, Name, Active) in costCenters)
        {
            if (activeOnly && !Active && Id != keepId)
                continue;

            listItems.Add(new SelectListItem
            {
                Text = Name,
                Value = $"{Id}",
            });
        }
    }

    /// <summary>
    /// Populates the specified search model with available pack name type options for selection in the UI.
    /// </summary>
    /// <remarks>The method adds options for both, customer, and packname types to the model's available pack
    /// name types collection. The first option is selected by default. This method is typically used to prepare data
    /// for dropdown lists in views.</remarks>
    /// <param name="listItems">The list to populate with available pack name type options. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task PrepareAvailablePackNameTypesAsync(IList<SelectListItem> listItems)
    {
        listItems.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Type.Both"),
            Value = "",
            Selected = true
        });

        listItems.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Type.Customer"),
            Value = MultiStoreServicesDefaults.PackNameTypeCustomer
        });

        listItems.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.Type.Packname"),
            Value = MultiStoreServicesDefaults.PackNameTypePackname
        });
    }

    /// <summary>
    /// Prepare the model of an administrator's Excel import page for a store
    /// </summary>
    /// <param name="layout">The layout of the administrator's file</param>
    /// <param name="store">The store the packnames are imported into</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the import page model
    /// </returns>
    public virtual Task<PackNameImportModel> PreparePackNameImportModelAsync(PackNameImportLayout layout, Store store)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(store);

        var model = new PackNameImportModel
        {
            Columns = layout.Columns.Select(column => column.Header).ToList(),
            MaxRows = PackNameImportDefaults.MaxRows,
            DateFormat = PackNameImportDefaults.DateFormat.ToLowerInvariant(),
            StoreId = store.Id,
            StoreName = store.Name
        };

        return Task.FromResult(model);
    }

    #endregion

    #region Customers - PackNames

    /// <summary>
    /// Prepare packaging name model
    /// </summary>
    /// <param name="model">Packaging name model</param>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name model
    /// </returns>
    public virtual async Task<PackNameModel> PrepareCustomerPackNameModelAsync(PackNameModel model, int customerId)
    {
        var customer = await _customerService.GetCustomerByIdAsync(customerId);
        var customerBranch = await _customerService.GetCustomerBranchByIdAsync(customerId);

        if (customerBranch == null || !customerBranch.BranchId.HasValue || customerBranch.BranchId == 0)
            return null;

        var branch = await _branchService.GetBranchByIdAsync(customerBranch.BranchId.Value);

        if (branch == null)
            return null;

        var store = await _storeService.GetStoreByIdAsync(branch.StoreId);

        if (store == null)
            return null;

        model.StoreId = store.Id;
        model.BranchId = branch.Id;

        //the branch's active lists, the default selected; with none, "none", and the save creates the branch's default
        //list. Opening the popup creates nothing. A new packname has no list of its own to keep: a list posted back that
        //is no longer active is not offered again
        var postedListId = model.PackNameListId;
        model.PackNameListId = await PrepareAvailablePackNameListAsync(model.AvailableLists, branch.Id, 0);

        if (postedListId > 0 && model.AvailableLists.Any(item => item.Value == postedListId.ToString()))
            model.PackNameListId = postedListId;

        //A create popup: an assignment picker with nothing yet assigned, so no keepId.
        await PrepareAvailableDepartmentsAsync(model.AvailableDepartments, store.Id, activeOnly: true);
        await PrepareAvailableCostCentersAsync(model.AvailableCostCenters, store.Id, activeOnly: true);

        //prepare model customers
        model.CustomerId = customerId;
        await PrepareModelCustomerAsync(model);

        await PrepareAvailableGendersAsync(model.AvailableGenders, customer.Gender, false);

        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(model.MultiStoreSettingsModel, store.Id);

        return model;
    }

    /// <summary>
    /// Prepare paged customer model with no packname associated
    /// </summary>
    /// <param name="searchModel">Customer model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer model
    /// </returns>
    public virtual async Task<ExtendedCustomerListModel> PrepareCustomersWithNoPackNameListModelAsync(PackNameSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var customers = await _multiStoreService.GetCustomersWithoutPackNameByStoreAsync(searchModel.StoreId, email: searchModel.SearchEmail, name: searchModel.SearchPackName,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = new ExtendedCustomerListModel().PrepareToGrid(searchModel, customers, () =>
        {
            //fill in model values from the entity
            return customers.Select(customer =>
            {
                var customerModel = new ExtendedCustomerModel
                {
                    Id = customer.Id,
                    Branch = customer.Branch,
                    Email = customer.Email,
                    FullName = $"{customer.FirstName} {customer.LastName}",
                    Active = customer.Active,
                };

                return customerModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare customer model to associate to the packname
    /// </summary>
    /// <param name="searchModel">Customer model to associate to the packname</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer model to associate to the packname
    /// </returns>
    public virtual AssociateCustomerToPackNameSearchModel PrepareAssociateCustomerToPackNameSearchModel(
        AssociateCustomerToPackNameSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.UsernamesEnabled = _customerSettings.UsernamesEnabled;
        searchModel.FirstNameEnabled = _customerSettings.FirstNameEnabled;
        searchModel.LastNameEnabled = _customerSettings.LastNameEnabled;

        //prepare page parameters
        searchModel.SetPopupGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged customer model to associate to the packname
    /// </summary>
    /// <param name="searchModel">Customer model to associate to the packname</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer model to associate to the packname
    /// </returns>
    public virtual async Task<ExtendedCustomerListModel> PrepareAssociateCustomerToPackNameListModelAsync(
        AssociateCustomerToPackNameSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var branch = await _branchService.GetBranchByIdAsync(searchModel.BranchId) ?? throw new ArgumentException("No branch found with the specified id");
        var storeId = branch.StoreId;

        var customers = await _multiStoreService.GetCustomersByStoreAsync(storeId, branch.Id,
            email: searchModel.SearchEmail, username: searchModel.SearchUsername, firstName: searchModel.SearchFirstName, lastName: searchModel.SearchLastName,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = new ExtendedCustomerListModel().PrepareToGrid(searchModel, customers, () =>
        {
            //fill in model values from the entity
            return customers.Select(customer =>
            {
                var customerModel = new ExtendedCustomerModel
                {
                    Id = customer.Id,
                    Email = customer.Email,
                    FullName = $"{customer.FirstName} {customer.LastName}",
                    Active = customer.Active,
                    Branch = customer.Branch,
                    HasPackName = (customer.PackName?.Id ?? 0) > 0 && customer.Id != searchModel.CurrentCustomerId,
                };

                return customerModel;
            });
        });

        return model;
    }

    #endregion

    #endregion
}
