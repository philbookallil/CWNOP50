using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames.Import;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Web.Factories;
using Nop.Web.Models.Customer;

namespace Nop.Plugin.CW.Core.Factories;

public class PackNameModelFactory : IPackNameModelFactory
{
    #region Fields

    protected readonly IPackNameService _packNameService;
    protected readonly IWorkContext _workContext;
    protected readonly ISettingService _settingService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly IStoreContext _storeContext;
    protected readonly ILocalizationService _localizationService;
    protected readonly IBranchService _branchService;
    protected readonly CustomerSettings _customerSettings;
    protected readonly IAddressModelFactory _addressModelFactory;
    protected readonly AddressSettings _addressSettings;
    protected readonly ICountryService _countryService;
    protected readonly IStateProvinceService _stateProvinceService;
    protected readonly ICustomerModelFactory _customerModelFactory;
    protected readonly ISubsidyOrderingService _subsidyOrderingService;
    protected readonly ISubsidyService _subsidyService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public PackNameModelFactory(IPackNameService packNameService,
        IWorkContext workContext,
        ISettingService settingService,
        IMultiStoreService multiStoreService,
        IStoreContext storeContext,
        ILocalizationService localizationService,
        IBranchService branchService,
        CustomerSettings customerSettings,
        IAddressModelFactory addressModelFactory,
        AddressSettings addressSettings,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        ICustomerModelFactory customerModelFactory,
        ISubsidyOrderingService subsidyOrderingService,
        ISubsidyService subsidyService,
        ICWFeatureService cwFeatureService)
    {
        _packNameService = packNameService;
        _workContext = workContext;
        _settingService = settingService;
        _multiStoreService = multiStoreService;
        _storeContext = storeContext;
        _localizationService = localizationService;
        _branchService = branchService;
        _customerSettings = customerSettings;
        _addressModelFactory = addressModelFactory;
        _addressSettings = addressSettings;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _customerModelFactory = customerModelFactory;
        _subsidyOrderingService = subsidyOrderingService;
        _subsidyService = subsidyService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Offers the store's active subsidies as checkboxes, ticked where the model holds the subsidy's id. Nothing is
    /// offered when the store does not let managers pick subsidies or has subsidies switched off.
    /// </summary>
    /// <param name="model">The packname form model</param>
    /// <param name="storeId">The store the form belongs to</param>
    /// <param name="storeSettings">That store's settings</param>
    protected virtual async Task PrepareAvailableSubsidiesAsync(PackNameRegisterModel model, int storeId, MultiStoreSettings storeSettings)
    {
        model.SubsidySelectionEnabled = storeSettings.AllowSubsidySelection
            && await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, storeId);

        model.AvailableSubsidies.Clear();

        if (!model.SubsidySelectionEnabled)
            return;

        var selectedIds = (model.SubsidyIds ?? new List<int>()).ToHashSet();

        foreach (var subsidy in await _subsidyService.GetActiveSubsidiesByStoreIdAsync(storeId))
        {
            model.AvailableSubsidies.Add(new SelectListItem
            {
                Text = subsidy.Name,
                Value = subsidy.Id.ToString(),
                Selected = selectedIds.Contains(subsidy.Id)
            });
        }
    }

    /// <summary>
    /// The packnames in this roster that cannot order, and the reason the selector shows on them.
    /// </summary>
    protected virtual async Task<(List<int> blockedPackNameIds, string reason)> GetBlockOrderingPackNameIdsAsync(List<PackNameDTO> packNames)
    {
        var blockedPackNameIds = await _subsidyOrderingService.GetOrderingBlockedPackNamesAsync(packNames.Select(packName => packName.Id).ToList());

        if (blockedPackNameIds.Count == 0)
            return (blockedPackNameIds, string.Empty);

        return (blockedPackNameIds, await _localizationService.GetResourceAsync("Plugins.CW.Core.PackName.BlockOrderingReason"));
    }

    /// <remarks>
    /// EVERY STOREFRONT PICKER IS AN ASSIGNMENT PICKER - there is no search filter out here, so unlike
    /// the admin helpers these filter unconditionally and take only the keep. Active means "no new
    /// assignments"; keepId is what the packname is already on, and dropping it
    /// would do more than hide an option. PreparePackNameEditModelAsync reads
    /// AvailableBranches.FirstOrDefault(...) ?? throw new NopException, so a missing assigned branch is
    /// a 500 on the edit page for every employee under a branch that was just deactivated.
    /// </remarks>
    protected async Task PrepareManagerAvailableBranchesAsync(IList<SelectListItem> listItems, int managerId, int storeId, MultiStoreSettings storeSettings, int? keepId = null)
    {
        listItems.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.SelectBranch"), Value = "0" });

        var allBranches = await _branchService.GetAllBranchesByStoreIdAsync(storeId);

        var branches = storeSettings.LimitAssignedBranches
            ? await _multiStoreService.GetVisibleBranchesForManagerAsync(managerId)
            : allBranches;

        //The breadcrumbs are built from the FILTERED list, so the index pairing below stays correct.
        //allBranches stays unfiltered: it is the tree the breadcrumb walks up, and an inactive
        //ancestor must still resolve or the trail breaks at it.
        branches = branches
            .Where(b => b.Active || b.Id == keepId)
            .ToList();

        //the branch the packname is already on is always offered: a packname the manager reaches through a
        //department may sit in a branch outside the manager's own
        if (keepId.HasValue && branches.All(b => b.Id != keepId) && allBranches.FirstOrDefault(b => b.Id == keepId) is { } keptBranch)
            branches.Add(keptBranch);

        var breadcrumbs = new List<string>();

        foreach (var branch in branches)
            breadcrumbs.Add(await _branchService.GetFormattedBreadCrumbAsync(storeId, branch, allBranches));

        for (var i = 0; i < branches.Count; i++)
        {
            listItems.Add(new SelectListItem
            {
                Text = breadcrumbs[i],
                Value = branches[i].Id.ToString()
            });
        }
    }

    /// <remarks>See PrepareManagerAvailableBranchesAsync: assignment picker, so Active is enforced and only the current value is kept.</remarks>
    protected async Task PrepareAvailableDepartmentsAsync(IList<SelectListItem> listItems, int storeId, int? keepId = null)
    {
        listItems.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Department.DefaultText"), Value = "0" });

        foreach (var department in await _multiStoreService.GetDepartmentsByStoreIdAsync(storeId))
        {
            if (!department.Active && department.Id != keepId)
                continue;

            listItems.Add(new SelectListItem
            {
                Text = department.Name,
                Value = department.Id.ToString(),
            });
        }
    }

    /// <remarks>See PrepareManagerAvailableBranchesAsync: assignment picker, so Active is enforced and only the current value is kept.</remarks>
    protected async Task PrepareAvailableCostCentersAsync(IList<SelectListItem> listItems, int storeId, int? keepId = null)
    {
        listItems.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.CostCenter.DefaultText"), Value = "0" });

        foreach (var (Id, Name, Active) in await _multiStoreService.GetCostCenterSelectItemsAsync(storeId))
        {
            if (!Active && Id != keepId)
                continue;

            listItems.Add(new SelectListItem
            {
                Text = Name,
                Value = Id.ToString(),
            });
        }
    }

    protected async Task PrepareAvailableGendersAsync(IList<SelectListItem> listItems)
    {
        listItems.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Mixed"),
            Value = MultiStoreServicesDefaults.GenderMixed,
            Selected = true
        });

        listItems.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Male"),
            Value = MultiStoreServicesDefaults.GenderMale,
        });

        listItems.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Female"),
            Value = MultiStoreServicesDefaults.GenderFemale,
        });

        if (_customerSettings.NeutralGenderEnabled)
        {
            listItems.Add(new SelectListItem
            {
                Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Neutral"),
                Value = MultiStoreServicesDefaults.GenderNeutral,
            });
        }
    }

    protected async Task PrepareCustomerModelAsync(CustomerInfoModel model, MultiStoreSettings storeSettings, string overrideCustomCustomerAttributesXml)
    {
        model.GenderEnabled = !storeSettings.GenderEnabled && _customerSettings.GenderEnabled;
        model.UsernamesEnabled = _customerSettings.UsernamesEnabled;

        model.FirstNameEnabled = _customerSettings.FirstNameEnabled;
        model.LastNameEnabled = _customerSettings.LastNameEnabled;
        model.FirstNameRequired = _customerSettings.FirstNameRequired;
        model.LastNameRequired = _customerSettings.LastNameRequired;

        model.DateOfBirthEnabled = _customerSettings.DateOfBirthEnabled;
        model.DateOfBirthRequired = _customerSettings.DateOfBirthRequired;
        model.CountryEnabled = _customerSettings.CountryEnabled;
        model.CountryRequired = _customerSettings.CountryRequired;
        model.StateProvinceEnabled = _customerSettings.StateProvinceEnabled;
        model.StateProvinceRequired = _customerSettings.StateProvinceRequired;
        model.CountyEnabled = _customerSettings.CountyEnabled;
        model.CountyRequired = _customerSettings.CountyRequired;
        model.CityEnabled = _customerSettings.CityEnabled;
        model.CityRequired = _customerSettings.CityRequired;
        model.StreetAddressEnabled = _customerSettings.StreetAddressEnabled;
        model.StreetAddressRequired = _customerSettings.StreetAddressRequired;
        model.StreetAddress2Enabled = _customerSettings.StreetAddress2Enabled;
        model.StreetAddress2Required = _customerSettings.StreetAddress2Required;
        model.ZipPostalCodeEnabled = _customerSettings.ZipPostalCodeEnabled;
        model.ZipPostalCodeRequired = _customerSettings.ZipPostalCodeRequired;
        model.PhoneEnabled = _customerSettings.PhoneEnabled;
        model.PhoneRequired = _customerSettings.PhoneRequired;
        model.FaxEnabled = _customerSettings.FaxEnabled;
        model.FaxRequired = _customerSettings.FaxRequired;

        //countries and states
        if (_customerSettings.CountryEnabled)
        {
            model.AvailableCountries.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Address.SelectCountry"), Value = "0" });
            model.CountryId = _customerSettings.DefaultCountryId ?? 0;
            var currentLanguage = await _workContext.GetWorkingLanguageAsync();
            foreach (var c in await _countryService.GetAllCountriesAsync(currentLanguage.Id))
            {
                model.AvailableCountries.Add(new SelectListItem
                {
                    Text = await _localizationService.GetLocalizedAsync(c, x => x.Name),
                    Value = c.Id.ToString(),
                    Selected = c.Id == model.CountryId
                });
            }

            if (_customerSettings.StateProvinceEnabled)
            {
                //states
                var states = (await _stateProvinceService.GetStateProvincesByCountryIdAsync(model.CountryId, currentLanguage.Id)).ToList();
                if (states.Any())
                {
                    model.AvailableStates.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Address.SelectState"), Value = "0" });

                    foreach (var s in states)
                    {
                        model.AvailableStates.Add(new SelectListItem { Text = await _localizationService.GetLocalizedAsync(s, x => x.Name), Value = s.Id.ToString(), Selected = (s.Id == model.StateProvinceId) });
                    }
                }
                else
                {
                    var anyCountrySelected = model.AvailableCountries.Any(x => x.Selected);

                    model.AvailableStates.Add(new SelectListItem
                    {
                        Text = await _localizationService.GetResourceAsync(anyCountrySelected ? "Address.Other" : "Address.SelectState"),
                        Value = "0"
                    });
                }

            }
        }

        //custom customer attributes
        var customAttributes = await _customerModelFactory.PrepareCustomCustomerAttributesAsync(new Customer(), overrideCustomCustomerAttributesXml);
        foreach (var attribute in customAttributes)
            model.CustomerAttributes.Add(attribute);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepares one page of the packnames the current manager may order for in an entry of the storefront selector
    /// </summary>
    /// <remarks>
    /// The roster follows the manager's scope: every active packname of a list the manager owns; in a list reached only
    /// through the manager's departments (when the store shows department packnames by branch), the packnames of those
    /// departments; in a virtual department entry (a negative list id), the department's packnames in any list; and in the
    /// manager's own list, the manager's own packname. The page is picked by <see cref="PackNameSelectorHelper.SelectPage"/>,
    /// and which of its packnames cannot order or may be edited is worked out for the page only.
    /// </remarks>
    /// <param name="listId">The identifier of the pack name list to retrieve. If negative, represents a virtual list for a department, where
    /// the absolute value is the department ID.</param>
    /// <param name="term">What the manager typed in the selector; empty for the first page</param>
    /// <param name="selectedPackNameId">The packname in context, kept on the first page; 0 for none</param>
    /// <returns>The page; empty when the manager has no packnames to see in the entry</returns>
    public virtual async Task<PackNameSelectorPageModel> PreparePackNamesModelForManagerAsync(int listId, string term, int selectedPackNameId)
    {
        var model = new PackNameSelectorPageModel();

        if (listId == 0) //Can be negative for departments
            return model;

        var manager = await _packNameService.GetPackNameByCustomerIdAsync((await _workContext.GetCurrentCustomerAsync()).Id);

        if (manager == null)
            return model;

        var packNameList = await _packNameService.GetPackNameListByIdAsync(manager.PackNameListId);

        if (packNameList == null)
            return model;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNameList.StoreId);
        var departmentIds = (await _multiStoreService.GetDepartmentsByManagerIdAsync(manager.Id)).Select(x => x.Id).ToList();

        //the service's lists are cached and shared, so the roster is a list of its own
        var packNames = new List<PackNameDTO>();

        //If this is a virtual list for a department, the id is negative and the value is the id for the department
        if (listId < 0)
        {
            var departmentId = Math.Abs(listId);

            //If the store is configured to show the packnames from the departments in its own branches, then we shouldn't have a virtual list for the department
            if (storeSettings.ShowDepartmentPackNamesByBranch || !departmentIds.Contains(departmentId))
                return model;

            //Get the packnames for the department in any branch
            packNames.AddRange(await _packNameService.GetActivePackNamesByListIdForDepartmentsAsync(0, new List<int> { departmentId }));
        }
        else
        {
            var ownedPackNameListIds = await _multiStoreService.GetPackNameListIdsByManagerIdAsync(manager.Id);

            //If the manager is not an owner for the list
            if (!ownedPackNameListIds.Any(id => id == listId))
            {
                //Validates if the manager has departments and the store is configure to show the packNames in their own branch
                if (departmentIds.Count != 0 && storeSettings.ShowDepartmentPackNamesByBranch)
                {
                    //Check if there are packnames for the departments managed by this manager
                    packNames.AddRange(await _packNameService.GetActivePackNamesByListIdForDepartmentsAsync(listId, departmentIds));
                }

                //Check if it is the list for the manager, in that case, we need to add his packName
                //The manager should be able to order for himself like employees can order for themselves
                if (manager.PackNameListId == listId && !packNames.Any(pn => pn.Id == manager.Id))
                {
                    var packName = await _packNameService.GetActivePackNameByManagerIdAsync(manager.Id);

                    if (packName != null)
                        packNames.Add(packName);
                }
            }
            else //Is not a virtual list and the manager owns this list, we can add all the active packnames for the list
            {
                packNames.AddRange(await _packNameService.GetActivePackNamesByListIdAsync(listId));
            }
        }

        var cartCounts = await _packNameService.GetCartCountsByCustomerAsync();
        var (page, hasMore) = PackNameSelectorHelper.SelectPage(packNames, term, selectedPackNameId, cartCounts, storeSettings.EmployeeNumberEnabled);

        var (blockedPacNameIds, blockedReason) = await GetBlockOrderingPackNameIdsAsync(page.ToList());

        //If the managers are limited to the branches they owned, then we can't let them edit a packname for a different branch
        var editablePackNameIds = new List<int>();
        if (storeSettings.LimitAssignedBranches)
        {
            editablePackNameIds = await _multiStoreService.GetPackNameIdsByManagerId(manager.Id);
        }

        model.HasMore = hasMore;
        model.PackNames = page.Select(packName => new PackNameModel
        {
            Id = packName.Id,
            Name = packName.Name,
            Group = packName.Group,
            Colour = packName.Colour,
            IsCustomer = packName.IsCustomer,
            EmployeeNumber = storeSettings.EmployeeNumberEnabled ? packName.EmployeeNumber ?? string.Empty : string.Empty,
            InCart = cartCounts.GetValueOrDefault(packName.Id, 0),
            BlockOrdering = blockedPacNameIds.Contains(packName.Id),
            BlockOrderingReason = blockedReason,
            Editable = !storeSettings.LimitAssignedBranches || editablePackNameIds.Contains(packName.Id)
        }).ToList();

        return model;
    }

    /// <summary>
    /// Prepare the packName register model
    /// </summary>
    /// <param name="model">PackName register model</param>
    /// <param name="manager">Manager</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="branchId">Branch identifier</param>
    /// <param name="overrideAddressAttributesXml">Overridden address attributes in XML format</param>
    /// <param name="overrideCustomCustomerAttributesXml">Overridden customer attributes in XML format</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName register model
    /// </returns>
    public virtual async Task<PackNameRegisterModel> PreparePackNameRegisterModelAsync(PackNameRegisterModel model, PackName manager, int storeId, int branchId, string overrideAddressAttributesXml = "", string overrideCustomCustomerAttributesXml = "")
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(manager);

        model.StoreId = storeId;

        if (model.BranchId == 0)
            model.BranchId = branchId;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        //form fields
        model.EmployeeNumberEnabled = storeSettings.EmployeeNumberEnabled;
        model.POEntryEnabled = storeSettings.POEntryEnabled;
        model.GenderEnabled = storeSettings.GenderEnabled;
        model.PrintNameEnabled = storeSettings.PrintNameEnabled;
        model.CommentsEnabled = storeSettings.CommentsEnabled;
        model.CanAddAddress = storeSettings.AllowHomeDelivery;

        //customer
        model.CanAddCustomer = storeSettings.AllowToAddCustomerToPackName;

        if (model.CanAddCustomer)
            await PrepareCustomerModelAsync(model.Customer, storeSettings, overrideCustomCustomerAttributesXml);

        //Branches
        await PrepareManagerAvailableBranchesAsync(model.AvailableBranches, manager.Id, storeId, storeSettings);
        //Departments
        await PrepareAvailableDepartmentsAsync(model.AvailableDepartments, storeId);
        //Cost Centers
        await PrepareAvailableCostCentersAsync(model.AvailableCostCenters, storeId);
        //Genders
        await PrepareAvailableGendersAsync(model.AvailableGenders);
        //Address
        await _addressModelFactory.PrepareAddressModelAsync(model.Address, null, excludeProperties: false, addressSettings: _addressSettings,
            loadCountries: async () => await _countryService.GetAllCountriesAsync((await _workContext.GetWorkingLanguageAsync()).Id), overrideAttributesXml: overrideAddressAttributesXml);

        //Subsidies
        await PrepareAvailableSubsidiesAsync(model, storeId, storeSettings);

        //Avoid to show the company field since we are going to populate it with the selected branch name
        model.Address.CompanyEnabled = false;

        return model;
    }

    /// <summary>
    /// Prepare the employee edit model
    /// </summary>
    /// <param name="model">Employee register model</param>
    /// <param name="packName">PackNamer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the employee register model
    /// </returns>
    public virtual async Task<PackNameRegisterModel> PreparePackNameEditModelAsync(PackNameRegisterModel model, PackName packName)
    {
        ArgumentNullException.ThrowIfNull(packName);

        var manager = await _packNameService.GetPackNameByCustomerIdAsync((await _workContext.GetCurrentCustomerAsync()).Id);
        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

        if (manager == null)
            throw new InvalidOperationException("Manager not found.");

        //the packname's store is its list's, whichever store the manager is browsing: the branches, departments, cost
        //centres and subsidies offered are that store's
        var storeId = packNameList?.StoreId
            ?? throw new NopException("The packname's list couldn't be found");
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        //fill in model values from the entity
        var posted = model != null;

        if (model == null)
        {
            model = new PackNameRegisterModel
            {
                Id = packName.Id,
                BranchId = packNameList?.BranchId ?? 0,
                Colour = packName.Colour,
                Comments = packName.Comments,
                EmployeeNumber = packName.EmployeeNumber,
                CostCenterId = packName.CostCenterId,
                DepartmentId = packName.DepartmentId,
                Gender = packName.Gender,
                Group = packName.Group,
                IsCustomer = packName.IsCustomer,
                Name = packName.Name,
                POEntry = packName.POEntry,
                PrintName = packName.PrintName,
            };
        }

        model.StoreId = storeId;

        //form fields
        model.EmployeeNumberEnabled = storeSettings.EmployeeNumberEnabled;
        model.POEntryEnabled = storeSettings.POEntryEnabled;
        model.GenderEnabled = storeSettings.GenderEnabled;
        model.PrintNameEnabled = storeSettings.PrintNameEnabled;
        model.CommentsEnabled = storeSettings.CommentsEnabled;
        model.CanAddAddress = false;
        model.CanAddCustomer = false;
        model.CanDelete = manager?.Id != packName.Id;

        //The keeps come from the ENTITY and its list, never from model.* - model may be the posted
        //model on a redisplay, and a posted id is a request value. packNameList
        //is the packname's CURRENT list, so its BranchId is the branch it is actually on.
        //Branches
        await PrepareManagerAvailableBranchesAsync(model.AvailableBranches, manager.Id, storeId, storeSettings, keepId: packNameList.BranchId);
        //Departments
        await PrepareAvailableDepartmentsAsync(model.AvailableDepartments, storeId, keepId: packName.DepartmentId);
        //Cost Centers
        await PrepareAvailableCostCentersAsync(model.AvailableCostCenters, storeId, keepId: packName.CostCenterId);
        //Genders
        await PrepareAvailableGendersAsync(model.AvailableGenders);

        //Subsidies: on a first render the ticks come from the packname's live assignments; on a redisplay they are
        //whatever the manager just posted
        if (!posted)
            model.SubsidyIds = (await _subsidyService.GetAllSubsidyPackNamesByPackNameIdAsync(packName.Id))
                .Select(mapping => mapping.SubsidyId)
                .ToList();

        await PrepareAvailableSubsidiesAsync(model, storeId, storeSettings);

        //THE NAME COMES FROM THE ENTITY'S BRANCH, NEVER FROM model.BranchId.
        //On a validation redisplay model is the POSTED model, so model.BranchId is whatever the manager
        //just tried to save - and a branch that was refused is deliberately absent from
        //AvailableBranches (the picker offers active branches plus the one the packname is already on).
        //Looking the posted value up here threw NopException, so every refused save became a 500 and the
        //validation message was never seen - including the "branch is no longer active" refusal added in
        //the same round - a refusal must leave a usable screen.
        //packNameList is the packname's CURRENT list, so its BranchId is both the branch the employee is
        //really on and the keepId passed above, which the branch picker always offers.
        var ownedBranchId = packNameList.BranchId
            ?? throw new NopException("The Branch couldn't be found for the manager");

        model.BranchName = model.AvailableBranches.FirstOrDefault(item => item.Value == $"{ownedBranchId}")?.Text
            ?? throw new NopException("The Branch couldn't be found for the manager");

        //The posted branch was refused and is not on offer, so the form must not keep showing it: a
        //<select> whose value is absent silently falls back to its first option, which would leave the
        //manager looking at "Select branch" with an error about a branch they can no longer see.
        model.BranchId = ownedBranchId;

        return model;
    }

    /// <summary>
    /// Prepare the manager's Excel import page model from the layout of the manager's file
    /// </summary>
    /// <param name="layout">The import layout</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the import page model
    /// </returns>
    public virtual Task<PackNameImportModel> PreparePackNameImportModelAsync(PackNameImportLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var model = new PackNameImportModel
        {
            Columns = layout.Columns.Select(column => column.Header).ToList(),
            MaxRows = PackNameImportDefaults.MaxRows,
            DateFormat = PackNameImportDefaults.DateFormat.ToLowerInvariant()
        };

        return Task.FromResult(model);
    }

    #endregion
}