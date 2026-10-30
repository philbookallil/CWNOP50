using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Gdpr;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Tax;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Affiliates;
using Nop.Services.Attributes;
using Nop.Services.Authentication.External;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Gdpr;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.PriceLists;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Areas.Admin.Models.Customers;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class CustomerOverrideModelFactory : CustomerModelFactory, ICustomerOverrideModelFactory
{
    #region Fields

    protected new readonly ICustomerOverrideService _customerService;
    protected readonly IBranchService _branchService;
    protected readonly IPackNameService _packNameService;
    protected readonly ICWSettingModelFactory _cwSettingModelFactory;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly IPackNameModelFactory _packNameModelFactory;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly ICreditNotesService _creditNotesService;

    #endregion

    #region Ctor

    public CustomerOverrideModelFactory(AddressSettings addressSettings,
        CustomerSettings customerSettings,
        DateTimeSettings dateTimeSettings,
        GdprSettings gdprSettings,
        IAddressModelFactory addressModelFactory,
        IAddressService addressService,
        IAffiliateService affiliateService,
        IAttributeFormatter<AddressAttribute, AddressAttributeValue> addressAttributeFormatter,
        IAttributeParser<CustomerAttribute, CustomerAttributeValue> customerAttributeParser,
        IAttributeService<CustomerAttribute, CustomerAttributeValue> customerAttributeService,
        IAuthenticationPluginManager authenticationPluginManager,
        IBackInStockSubscriptionService backInStockSubscriptionService,
        IBaseAdminModelFactory baseAdminModelFactory,
        ICountryService countryService,
        ICustomerActivityService customerActivityService,
        ICustomerOverrideService customerService,
        ICustomWishlistService customWishlistService,
        IDateTimeHelper dateTimeHelper,
        IExternalAuthenticationService externalAuthenticationService,
        IGdprService gdprService,
        IGenericAttributeService genericAttributeService,
        IGeoLookupService geoLookupService,
        ILocalizationService localizationService,
        IOrderService orderService,
        IPictureService pictureService,
        IPriceFormatter priceFormatter,
        IPriceListService priceListService,
        IProductAttributeFormatter productAttributeFormatter,
        IProductService productService,
        IRewardPointService rewardPointService,
        IShoppingCartService shoppingCartService,
        IStateProvinceService stateProvinceService,
        IStoreContext storeContext,
        IStoreService storeService,
        ITaxService taxService,
        IWorkContext workContext,
        MediaSettings mediaSettings,
        PrivateMessageSettings privateMessageSettings,
        RewardPointsSettings rewardPointsSettings,
        TaxSettings taxSettings,
        IBranchService branchService,
        IPackNameService packNameService,
        ICWSettingModelFactory cwSettingModelFactory,
        IMultiStoreService multiStoreService,
        IPackNameModelFactory packNameModelFactory,
        ICWFeatureService cwFeatureService,
        ICreditNotesService creditNotesService) : base(addressSettings,
                                        customerSettings,
                                        dateTimeSettings,
                                        gdprSettings,
                                        addressModelFactory,
                                        addressService,
                                        affiliateService,
                                        addressAttributeFormatter,
                                        customerAttributeParser,
                                        customerAttributeService,
                                        authenticationPluginManager,
                                        backInStockSubscriptionService,
                                        baseAdminModelFactory,
                                        countryService,
                                        customerActivityService,
                                        customerService,
                                        customWishlistService,
                                        dateTimeHelper,
                                        externalAuthenticationService,
                                        gdprService,
                                        genericAttributeService,
                                        geoLookupService,
                                        localizationService,
                                        orderService,
                                        pictureService,
                                        priceFormatter,
                                        priceListService,
                                        productAttributeFormatter,
                                        productService,
                                        rewardPointService,
                                        shoppingCartService,
                                        stateProvinceService,
                                        storeContext,
                                        storeService,
                                        taxService,
                                        workContext,
                                        mediaSettings,
                                        privateMessageSettings,
                                        rewardPointsSettings,
                                        taxSettings)
    {
        _customerService = customerService;
        _branchService = branchService;
        _packNameService = packNameService;
        _cwSettingModelFactory = cwSettingModelFactory;
        _multiStoreService = multiStoreService;
        _packNameModelFactory = packNameModelFactory;
        _cwFeatureService = cwFeatureService;
        _creditNotesService = creditNotesService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Prepare available branches
    /// </summary>
    /// <param name="items">Branch items</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="withSpecialDefaultItem">Whether to insert the first special item for the default value</param>
    /// <param name="defaultItemText">Default item text; pass null to use default value of the default item text</param>
    /// <param name="keepId">
    /// The branch this customer is ALREADY on. Always offered even when inactive, so that saving the
    /// customer cannot silently move them off a branch that was deactivated after they were placed on
    /// it. Null when the customer has no branch.
    /// </param>
    /// <returns>A task that represents the asynchronous operation</returns>
    /// <remarks>
    /// Inactive branches are dropped: assigning a customer to a branch is a NEW assignment, which is
    /// what Active forbids. The branch TREE is not filtered - it is structure,
    /// not an assignment, and it already renders inactive branches greyed.
    /// </remarks>
    protected virtual async Task PrepareAvailableBranchesAsync(IList<SelectListItem> items, int storeId, bool withSpecialDefaultItem = true, string defaultItemText = null, int? keepId = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        //prepare available branches
        var availableBranches = await _branchService.GetAllBranchesByStoreIdAsync(storeId);
        foreach (var branch in availableBranches)
        {
            if (!branch.Active && branch.Id != keepId)
                continue;

            items.Add(new SelectListItem { Value = branch.Id.ToString(), Text = branch.Name });
        }

        //insert special item for the default value. The fallback is the BRANCH prompt: this used to
        //fall back to Admin.Address.SelectCountry, which no caller reached but which would have put
        //"Select country" above a list of branches the moment one did.
        await PrepareDefaultItemAsync(items, withSpecialDefaultItem, defaultItemText ?? await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.SelectBranch"));
    }

    /// <summary>
    /// Prepare default item
    /// </summary>
    /// <param name="items">Available items</param>
    /// <param name="withSpecialDefaultItem">Whether to insert the first special item for the default value</param>
    /// <param name="defaultItemText">Default item text; pass null to use "All" text</param>
    /// <param name="defaultItemValue">Default item value; defaults 0</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task PrepareDefaultItemAsync(IList<SelectListItem> items, bool withSpecialDefaultItem, string defaultItemText = null, string defaultItemValue = "0")
    {
        ArgumentNullException.ThrowIfNull(items);

        //whether to insert the first special item for the default value
        if (!withSpecialDefaultItem)
            return;

        //prepare item text
        defaultItemText ??= await _localizationService.GetResourceAsync("Admin.Common.All");

        //insert this default item at first
        items.Insert(0, new SelectListItem { Text = defaultItemText, Value = defaultItemValue });
    }

    protected virtual async Task PreparePackNameModelAsync(PackNameModel model, int storeId, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var packName = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);

        if (packName != null)
        {
            model.Id = packName.Id;

            //THE CARD'S LINK NEEDS THE STORE. storeId arrives as a parameter and was used only for
            //the settings model below, so PackNameModel.StoreId stayed 0 - harmless while every
            //field on this card was disabled text, wrong the moment the card's "Open in Manage Store"
            //link started building /Admin/MultiStore/Edit/{StoreId} out of it.
            model.StoreId = storeId;

            model.PackNameListId = packName.PackNameListId;
            model.Name = packName.Name;
            model.PrintName = packName.PrintName;
            model.EmployeeNumber = packName.EmployeeNumber;
            model.POEntry = packName.POEntry;
            model.Comments = packName.Comments;
            model.Active = packName.Active;
            model.Colour = packName.Colour;
            model.Gender = packName.Gender;
            model.Group = packName.Group;
            model.Manager = packName.Manager;
            model.CustomerId = packName.CustomerId;
            model.Department = packName.DepartmentId.HasValue && packName.DepartmentId.Value > 0 ? (await _multiStoreService.GetDepartmentByIdAsync(packName.DepartmentId.Value))?.Name ?? string.Empty : string.Empty;
            model.CostCenter = packName.CostCenterId.HasValue && packName.CostCenterId.Value > 0 ? (await _multiStoreService.GetCostCenterByIdAsync(packName.CostCenterId.Value))?.Name ?? string.Empty : string.Empty;
            await _packNameModelFactory.PrepareAvailableGendersAsync(model.AvailableGenders, packName.Gender, false);

            await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(model.MultiStoreSettingsModel, storeId);
        }
        else
        {
            model.CustomerId = customer.Id;
        }
    }

    /// <summary>
    /// Sets the credit notes visibility flags. Credit notes are a liability: the balance survives the
    /// feature being switched off, so the card stays visible and turns read-only rather than
    /// disappearing. PackName and subsidy are capabilities and are gated in the view instead.
    /// </summary>
    /// <param name="model">Customer model</param>
    /// <param name="customer">Customer</param>
    /// <param name="storeId">The customer's store identifier</param>
    protected virtual async Task PrepareCreditNotesVisibilityAsync(ExtendedCustomerModel model, Customer customer, int storeId)
    {
        model.CreditNotesEnabled = await _cwFeatureService.IsEnabledAsync(CWFeature.CreditNotes, storeId);

        model.DisplayCreditNotes = model.CreditNotesEnabled || await _creditNotesService.CustomerHasCreditNotesAsync(customer.Id);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepare customer search model
    /// </summary>
    /// <param name="searchModel">Customer search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer search model
    /// </returns>
    public virtual async Task<ExtendedCustomerSearchModel> PrepareCustomerSearchModelAsync(ExtendedCustomerSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //nop's preparation fills this same model (the registered role selected by default); a mapped copy would lose
        //the selected roles, which nop's mapping rules leave out of every ACL-supported model
        await PrepareCustomerSearchModelAsync((CustomerSearchModel)searchModel);

        //prepare available stores
        await _baseAdminModelFactory.PrepareStoresAsync(searchModel.AvailableStores);

        return searchModel;
    }

    /// <summary>
    /// Prepare paged customer list model
    /// </summary>
    /// <param name="searchModel">Customer search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer list model
    /// </returns>
    public virtual async Task<ExtendedCustomerListModel> PrepareCustomerListModelAsync(ExtendedCustomerSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get parameters to filter customers
        _ = int.TryParse(searchModel.SearchDayOfBirth, out var dayOfBirth);
        _ = int.TryParse(searchModel.SearchMonthOfBirth, out var monthOfBirth);
        var createdFromUtc = !searchModel.SearchRegistrationDateFrom.HasValue ? null
            : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.SearchRegistrationDateFrom.Value, await _dateTimeHelper.GetCurrentTimeZoneAsync());
        var createdToUtc = !searchModel.SearchRegistrationDateTo.HasValue ? null
            : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.SearchRegistrationDateTo.Value, await _dateTimeHelper.GetCurrentTimeZoneAsync()).AddDays(1);
        var lastActivityFromUtc = !searchModel.SearchLastActivityFrom.HasValue ? null
            : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.SearchLastActivityFrom.Value, await _dateTimeHelper.GetCurrentTimeZoneAsync());
        var lastActivityToUtc = !searchModel.SearchLastActivityTo.HasValue ? null
            : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.SearchLastActivityTo.Value, await _dateTimeHelper.GetCurrentTimeZoneAsync()).AddDays(1);

        //exclude guests from the result when filter "by registration date" is used
        if (createdFromUtc.HasValue || createdToUtc.HasValue)
        {
            if (!searchModel.SelectedCustomerRoleIds.Any())
            {
                var customerRoles = await _customerService.GetAllCustomerRolesAsync(showHidden: true);
                searchModel.SelectedCustomerRoleIds = customerRoles
                    .Where(cr => cr.SystemName != NopCustomerDefaults.GuestsRoleName).Select(cr => cr.Id).ToList();
            }
            else
            {
                var guestRole = await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.GuestsRoleName);
                if (guestRole != null)
                    searchModel.SelectedCustomerRoleIds.Remove(guestRole.Id);
            }
        }

        //get customers
        var customersData = await _customerService.GetAllCustomersDataAsync(customerRoleIds: searchModel.SelectedCustomerRoleIds.ToArray(),
            email: searchModel.SearchEmail,
            username: searchModel.SearchUsername,
            firstName: searchModel.SearchFirstName,
            lastName: searchModel.SearchLastName,
            dayOfBirth: dayOfBirth,
            monthOfBirth: monthOfBirth,
            company: searchModel.SearchCompany,
            createdFromUtc: createdFromUtc,
            createdToUtc: createdToUtc,
            lastActivityFromUtc: lastActivityFromUtc,
            lastActivityToUtc: lastActivityToUtc,
            phone: searchModel.SearchPhone,
            zipPostalCode: searchModel.SearchZipPostalCode,
            ipAddress: searchModel.SearchIpAddress,
            isActive: searchModel.SearchIsActive,
            storeId: searchModel.SearchStoreId,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare list model
        var model = await new ExtendedCustomerListModel().PrepareToGridAsync(searchModel, customersData, () =>
        {
            return customersData.SelectAwait(async customerData =>
            {
                var customer = customerData.Customer;

                //fill in model values from the entity
                var customerModel = customer.ToModel<ExtendedCustomerModel>();

                //convert dates to the user time
                customerModel.Email = (await _customerService.IsRegisteredAsync(customer))
                    ? customer.Email
                    : await _localizationService.GetResourceAsync("Admin.Customers.Guest");
                customerModel.FullName = await _customerService.GetCustomerFullNameAsync(customer);
                customerModel.Company = customer.Company;
                customerModel.Phone = customer.Phone;
                customerModel.PhoneSmsVerified = customer.PhoneSmsVerified;
                customerModel.ZipPostalCode = customer.ZipPostalCode;

                customerModel.CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(customer.CreatedOnUtc, DateTimeKind.Utc);
                customerModel.LastActivityDate = await _dateTimeHelper.ConvertToUserTimeAsync(customer.LastActivityDateUtc, DateTimeKind.Utc);

                //fill in additional values (not existing in the entity)
                customerModel.CustomerRoleNames = string.Join(", ",
                    (await _customerService.GetCustomerRolesAsync(customer)).Select(role => role.Name));
                if (_customerSettings.AllowCustomersToUploadAvatars)
                {
                    var avatarPictureId = await _genericAttributeService.GetAttributeAsync<int>(customer, NopCustomerDefaults.AvatarPictureIdAttribute);
                    customerModel.AvatarUrl = await _pictureService
                        .GetPictureUrlAsync(avatarPictureId, _mediaSettings.AvatarPictureSize, _customerSettings.DefaultAvatarEnabled, defaultPictureType: PictureType.Avatar);
                }

                customerModel.RegisteredInStore = (await _storeService.GetAllStoresAsync())
                    .FirstOrDefault(store => store.Id == customer.RegisteredInStoreId)?.Name ?? string.Empty;

                customerModel.RegisteredInBranch = customerData.BranchName ?? string.Empty;

                return customerModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare customer model
    /// </summary>
    /// <param name="model">Customer model</param>
    /// <param name="customer">Customer</param>
    /// <param name="excludeProperties">Whether to exclude populating of some properties of model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer model
    /// </returns>
    public virtual async Task<ExtendedCustomerModel> PrepareExtendedCustomerModelAsync(ExtendedCustomerModel model, Customer customer, bool excludeProperties = false)
    {
        var storeId = customer?.RegisteredInStoreId ?? (await _storeContext.GetCurrentStoreAsync()).Id;

        if (customer != null)
        {
            //fill in model values from the entity
            model ??= new ExtendedCustomerModel();

            model.Id = customer.Id;
            model.DisplayVatNumber = _taxSettings.EuVatEnabled;
            model.AllowSendingOfPrivateMessage = await _customerService.IsRegisteredAsync(customer) &&
                                                 _privateMessageSettings.AllowPrivateMessages;
            model.AllowSendingOfWelcomeMessage = await _customerService.IsRegisteredAsync(customer) &&
                                                 _customerSettings.UserRegistrationType == UserRegistrationType.AdminApproval;
            model.AllowReSendingOfActivationMessage = await _customerService.IsRegisteredAsync(customer) && !customer.Active &&
                                                      _customerSettings.UserRegistrationType == UserRegistrationType.EmailValidation;
            model.GdprEnabled = _gdprSettings.GdprEnabled;

            model.MultiFactorAuthenticationProvider = await _genericAttributeService
                .GetAttributeAsync<string>(customer, NopCustomerDefaults.SelectedMultiFactorAuthenticationProviderAttribute);

            //whether to fill in some of properties
            if (!excludeProperties)
            {
                model.Email = customer.Email;
                model.Username = customer.Username;
                model.VendorId = customer.VendorId;
                model.AdminComment = customer.AdminComment;
                model.IsTaxExempt = customer.IsTaxExempt;
                model.Active = customer.Active;
                model.FirstName = customer.FirstName;
                model.LastName = customer.LastName;
                model.Gender = customer.Gender;
                model.DateOfBirth = customer.DateOfBirth;
                model.Company = customer.Company;
                model.StreetAddress = customer.StreetAddress;
                model.StreetAddress2 = customer.StreetAddress2;
                model.ZipPostalCode = customer.ZipPostalCode;
                model.City = customer.City;
                model.County = customer.County;
                model.CountryId = customer.CountryId;
                model.StateProvinceId = customer.StateProvinceId;
                model.Phone = customer.Phone;
                model.PhoneSmsVerified = customer.PhoneSmsVerified;
                model.Fax = customer.Fax;
                model.TimeZoneId = customer.TimeZoneId;
                model.VatNumber = customer.VatNumber;
                model.VatNumberStatusNote = await _localizationService.GetLocalizedEnumAsync(customer.VatNumberStatus);
                model.SelectedCustomerRoleIds = (await _customerService.GetCustomerRoleIdsAsync(customer)).ToList();
                model.SelectedPriceListIds = (await _priceListService.GetPriceListsByCustomerAsync(customer)).Select(pl => pl.Id).ToList();
                model.MustChangePassword = customer.MustChangePassword;

                //prepare model affiliate
                var affiliate = await _affiliateService.GetAffiliateByIdAsync(customer.AffiliateId);
                if (affiliate != null)
                {
                    model.AffiliateId = affiliate.Id;
                    model.AffiliateName = await _affiliateService.GetAffiliateFullNameAsync(affiliate);
                }
            }

            var allStores = await _storeService.GetAllStoresAsync();
            model.RegisteredInStore = allStores.FirstOrDefault(store => store.Id == customer.RegisteredInStoreId)?.Name ?? string.Empty;
            model.DisplayRegisteredInStore = model.Id > 0
                                             && !string.IsNullOrEmpty(model.RegisteredInStore)
                                             && allStores.Select(x => x.Id).Count() > 1;

            model.LastVisitedPage = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.LastVisitedPageAttribute);
            model.CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(customer.CreatedOnUtc, DateTimeKind.Utc);
            model.LastActivityDate = await _dateTimeHelper.ConvertToUserTimeAsync(customer.LastActivityDateUtc, DateTimeKind.Utc);
            model.LastIpAddress = customer.LastIpAddress;

            //prepare reward points model
            model.DisplayRewardPointsHistory = _rewardPointsSettings.Enabled;
            if (model.DisplayRewardPointsHistory)
                await PrepareAddRewardPointsToCustomerModelAsync(model.AddRewardPoints);

            //prepare nested search models
            PrepareRewardPointsSearchModel(model.CustomerRewardPointsSearchModel, customer);
            PrepareCustomerAddressSearchModel(model.CustomerAddressSearchModel, customer);
            PrepareCustomerOrderSearchModel(model.CustomerOrderSearchModel, customer);
            await PrepareCustomerShoppingCartSearchModelAsync(model.CustomerShoppingCartSearchModel, customer);
            PrepareCustomerActivityLogSearchModel(model.CustomerActivityLogSearchModel, customer);
            PrepareCustomerBackInStockSubscriptionSearchModel(model.CustomerBackInStockSubscriptionSearchModel, customer);
            await PrepareCustomerAssociatedExternalAuthRecordsSearchModelAsync(model.CustomerAssociatedExternalAuthRecordsSearchModel, customer);
            await PreparePackNameModelAsync(model.PackNameModel, storeId, customer);

            //credit notes are a liability — keep them visible after the feature is switched off
            await PrepareCreditNotesVisibilityAsync(model, customer, storeId);

            //Customer Branch entity
            var customerBranch = await _customerService.GetCustomerBranchByIdAsync(customer.Id);

            model.BranchId = customerBranch?.BranchId ?? 0;
        }
        else
        {
            //whether to fill in some of properties
            if (!excludeProperties)
            {
                //precheck Registered Role as a default role while creating a new customer through admin
                var registeredRole = await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.RegisteredRoleName);
                if (registeredRole != null)
                    model.SelectedCustomerRoleIds.Add(registeredRole.Id);
            }
        }

        model.UsernamesEnabled = _customerSettings.UsernamesEnabled;
        model.AllowCustomersToSetTimeZone = _dateTimeSettings.AllowCustomersToSetTimeZone;
        model.FirstNameEnabled = _customerSettings.FirstNameEnabled;
        model.LastNameEnabled = _customerSettings.LastNameEnabled;
        model.GenderEnabled = _customerSettings.GenderEnabled;
        model.NeutralGenderEnabled = _customerSettings.NeutralGenderEnabled;
        model.DateOfBirthEnabled = _customerSettings.DateOfBirthEnabled;
        model.CompanyEnabled = _customerSettings.CompanyEnabled;
        model.StreetAddressEnabled = _customerSettings.StreetAddressEnabled;
        model.StreetAddress2Enabled = _customerSettings.StreetAddress2Enabled;
        model.ZipPostalCodeEnabled = _customerSettings.ZipPostalCodeEnabled;
        model.CityEnabled = _customerSettings.CityEnabled;
        model.CountyEnabled = _customerSettings.CountyEnabled;
        model.CountryEnabled = _customerSettings.CountryEnabled;
        model.StateProvinceEnabled = _customerSettings.StateProvinceEnabled;
        model.PhoneEnabled = _customerSettings.PhoneEnabled;
        model.FaxEnabled = _customerSettings.FaxEnabled;

        //set default values for the new model
        if (customer == null)
        {
            model.Active = true;
            model.DisplayVatNumber = false;
        }

        //prepare available vendors
        await _baseAdminModelFactory.PrepareVendorsAsync(model.AvailableVendors,
            defaultItemText: await _localizationService.GetResourceAsync("Admin.Customers.Customers.Fields.Vendor.None"));

        //prepare model customer attributes
        await PrepareCustomerAttributeModelsAsync(model.CustomerAttributes, customer);

        //prepare available customer roles
        var availableRoles = await _customerService.GetAllCustomerRolesAsync(showHidden: true);
        model.AvailableCustomerRoles = availableRoles.Select(role => new SelectListItem
        {
            Text = role.Name,
            Value = role.Id.ToString(),
            Selected = model.SelectedCustomerRoleIds.Contains(role.Id)
        }).ToList();

        //prepare available price lists
        var availablePriceLists = await _priceListService.GetAllPriceListsAsync();
        model.AvailablePriceLists = availablePriceLists.Select(priceList => new SelectListItem
        {
            Text = priceList.Name,
            Value = priceList.Id.ToString(),
            Selected = model.SelectedPriceListIds.Contains(priceList.Id)
        }).ToList();

        //prepare available time zones
        await _baseAdminModelFactory.PrepareTimeZonesAsync(model.AvailableTimeZones, false);

        //prepare available countries and states
        if (_customerSettings.CountryEnabled)
        {
            await _baseAdminModelFactory.PrepareCountriesAsync(model.AvailableCountries);
            if (_customerSettings.StateProvinceEnabled)
                await _baseAdminModelFactory.PrepareStatesAndProvincesAsync(model.AvailableStates, model.CountryId == 0 ? null : (int?)model.CountryId);
        }

        await PrepareAvailableBranchesAsync(model.AvailableBranches, storeId,
            defaultItemText: await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.SelectBranch"),
            keepId: model.BranchId > 0 ? model.BranchId : null);

        model.StoreId = storeId;

        return model;
    }

    #endregion
}
