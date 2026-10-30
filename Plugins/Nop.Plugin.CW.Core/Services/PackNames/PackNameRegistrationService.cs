using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using System.Transactions;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Creates packnames the way a manager does, with the customer account, the delivery address, the subsidies and the
/// welcome e-mail that go with one
/// </summary>
/// <remarks>
/// One registration is one transaction (<see cref="PackNameTransaction"/>), joined to the import's when it runs inside
/// one. The customer account is registered first, so a refused registration (an e-mail already in use, say) rolls
/// back and leaves nothing behind. Nobody is told the account's password: it is random and the welcome e-mail carries
/// nop's link to set one. The account gets an address to bill: a copy of the packname's delivery address when one was
/// given, otherwise one built from the profile fields when they make a valid address. A copy, not the same row: nop's
/// own pages delete an account's address outright, and the packname's must not go with it. The branch's name is the
/// Company of the account and of the addresses, whatever the form or the file holds. The e-mail goes last, once the
/// packname and its subsidies exist to be named in it.
/// </remarks>
public partial class PackNameRegistrationService : IPackNameRegistrationService
{
    #region Fields

    protected readonly IPackNameService _packNameService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly ICustomerOverrideService _customerService;
    protected readonly ICustomerRegistrationService _customerRegistrationService;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly IAddressService _addressService;
    protected readonly ISubsidyService _subsidyService;
    protected readonly ISettingService _settingService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly IEventPublisher _eventPublisher;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPackNameWorkflowMessageService _packNameWorkflowMessageService;
    protected readonly CustomerSettings _customerSettings;
    protected readonly AddressSettings _addressSettings;

    #endregion

    #region Ctor

    public PackNameRegistrationService(IPackNameService packNameService,
        IMultiStoreService multiStoreService,
        ICustomerOverrideService customerService,
        ICustomerRegistrationService customerRegistrationService,
        ICustomerActivityService customerActivityService,
        IAddressService addressService,
        ISubsidyService subsidyService,
        ISettingService settingService,
        ICWFeatureService cwFeatureService,
        IDateTimeHelper dateTimeHelper,
        IEventPublisher eventPublisher,
        ILocalizationService localizationService,
        IPackNameWorkflowMessageService packNameWorkflowMessageService,
        CustomerSettings customerSettings,
        AddressSettings addressSettings)
    {
        _packNameService = packNameService;
        _multiStoreService = multiStoreService;
        _customerService = customerService;
        _customerRegistrationService = customerRegistrationService;
        _customerActivityService = customerActivityService;
        _addressService = addressService;
        _subsidyService = subsidyService;
        _settingService = settingService;
        _cwFeatureService = cwFeatureService;
        _dateTimeHelper = dateTimeHelper;
        _eventPublisher = eventPublisher;
        _localizationService = localizationService;
        _packNameWorkflowMessageService = packNameWorkflowMessageService;
        _customerSettings = customerSettings;
        _addressSettings = addressSettings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Registers the customer account of a packname: nop's registration with a random password, the profile fields,
    /// the custom attributes, the store's default packname roles and the link to the branch
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer, or null with the reasons in <paramref name="errors"/>
    /// </returns>
    protected virtual async Task<Customer> RegisterCustomerAsync(PackNameRegistration registration, IList<string> errors)
    {
        var data = registration.Customer;
        var timezone = _dateTimeHelper.DefaultStoreTimeZone;

        var customer = new Customer
        {
            RegisteredInStoreId = registration.StoreId,
            CreatedOnUtc = DateTime.UtcNow,
            LastActivityDateUtc = DateTime.UtcNow,
            Company = registration.Branch.Name,
            Active = true,
            TimeZoneId = timezone.Id,
            CustomerGuid = Guid.NewGuid()
        };

        await _customerService.InsertCustomerAsync(customer);

        //nobody is told this password: the welcome e-mail carries a link to set one
        var password = Guid.NewGuid().ToString("N");

        var registrationRequest = new CustomerRegistrationRequest(customer,
            data.Email,
            _customerSettings.UsernamesEnabled ? data.Username : data.Email,
            password,
            _customerSettings.DefaultPasswordFormat,
            registration.StoreId,
            true);

        var registrationResult = await _customerRegistrationService.RegisterCustomerAsync(registrationRequest);

        if (!registrationResult.Success)
        {
            if (registrationResult.Errors?.Count > 0)
                foreach (var error in registrationResult.Errors)
                    errors.Add(error);
            else
                errors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.AddPackName.RegistrationFailed"));

            //the customer row inserted above goes with the transaction, which is not completed
            return null;
        }

        if (_customerSettings.GenderEnabled)
            customer.Gender = data.Gender;

        if (_customerSettings.FirstNameEnabled)
            customer.FirstName = data.FirstName;

        if (_customerSettings.LastNameEnabled)
            customer.LastName = data.LastName;

        if (_customerSettings.DateOfBirthEnabled)
            customer.DateOfBirth = data.DateOfBirth;

        if (_customerSettings.StreetAddressEnabled)
            customer.StreetAddress = data.StreetAddress;

        if (_customerSettings.StreetAddress2Enabled)
            customer.StreetAddress2 = data.StreetAddress2;

        if (_customerSettings.ZipPostalCodeEnabled)
            customer.ZipPostalCode = data.ZipPostalCode;

        if (_customerSettings.CityEnabled)
            customer.City = data.City;

        if (_customerSettings.CountyEnabled)
            customer.County = data.County;

        if (_customerSettings.CountryEnabled)
            customer.CountryId = data.CountryId;

        if (_customerSettings.CountryEnabled && _customerSettings.StateProvinceEnabled)
            customer.StateProvinceId = data.StateProvinceId;

        if (_customerSettings.PhoneEnabled)
            customer.Phone = data.Phone;

        if (_customerSettings.FaxEnabled)
            customer.Fax = data.Fax;

        customer.CustomCustomerAttributesXML = data.CustomAttributesXml;
        await _customerService.UpdateCustomerAsync(customer);

        await AddDefaultPackNameRolesAsync(customer, registration.StoreId);

        await _eventPublisher.PublishAsync(new CustomerRegisteredEvent(customer));
        await _eventPublisher.PublishAsync(new CustomerActivatedEvent(customer));

        await _customerService.InsertCustomerBranchAsync(new CustomerBranch
        {
            Id = customer.Id,
            BranchId = registration.Branch.Id,
            LastModifiedDateUtc = DateTime.UtcNow,
        });

        return customer;
    }

    /// <summary>
    /// Gives a new account its address book entry and makes it the billing and shipping address: a copy of the
    /// packname's delivery address when there is one, otherwise an address built from the profile fields when they make
    /// a valid address. Without one, the account's first checkout has no address to bill
    /// </summary>
    /// <param name="customer">The account just registered</param>
    /// <param name="packNameAddress">The packname's delivery address, already inserted; null when none was given</param>
    /// <param name="company">The Company an address carries</param>
    protected virtual async Task SetCustomerAddressAsync(Customer customer, Address packNameAddress, string company)
    {
        Address address;

        if (packNameAddress != null)
        {
            //the account's own row: deleting it from the account's address book leaves the packname's in place
            address = _addressService.CloneAddress(packNameAddress);
            await _addressService.InsertAddressAsync(address);
        }
        else
        {
            address = new Address
            {
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Company = company,
                CountryId = customer.CountryId > 0 ? customer.CountryId : null,
                StateProvinceId = customer.StateProvinceId > 0 ? customer.StateProvinceId : null,
                County = customer.County,
                City = customer.City,
                Address1 = customer.StreetAddress,
                Address2 = customer.StreetAddress2,
                ZipPostalCode = customer.ZipPostalCode,
                PhoneNumber = customer.Phone,
                FaxNumber = customer.Fax,
                CreatedOnUtc = customer.CreatedOnUtc
            };

            if (!await _addressService.IsAddressValidAsync(address))
                return;

            await _addressService.InsertAddressAsync(address);
        }

        await _customerService.InsertCustomerAddressAsync(customer, address);

        customer.BillingAddressId = address.Id;
        customer.ShippingAddressId = address.Id;
        await _customerService.UpdateCustomerAsync(customer);
    }

    /// <summary>
    /// Adds the customer roles the store gives a packname customer, beyond nop's Registered. A configured role
    /// that no longer exists is skipped
    /// </summary>
    protected virtual async Task AddDefaultPackNameRolesAsync(Customer customer, int storeId)
    {
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);
        var roleIds = storeSettings.DefaultPackNameCustomerRoleIds ?? new List<int>();

        if (roleIds.Count == 0)
            return;

        var roles = await _customerService.GetAllCustomerRolesAsync(showHidden: true);

        //a role the account already has (nop's Registered, if the setting names it) is not mapped twice
        var currentRoleIds = await _customerService.GetSignedInCustomerRoleIdsAsync(customer, showHidden: true);

        foreach (var customerRole in roles.Where(customerRole => roleIds.Contains(customerRole.Id) && !currentRoleIds.Contains(customerRole.Id)))
            await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping { CustomerId = customer.Id, CustomerRoleId = customerRole.Id });
    }

    /// <summary>
    /// Assigns the subsidies to the packname. Only the store's active subsidies are assigned, and only when the store
    /// has the feature on and lets managers pick subsidies (an administrator's registration needs only the feature); a
    /// subsidy's own credit or period becomes a variation when the store allows packnames to carry them
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the names of the subsidies assigned
    /// </returns>
    protected virtual async Task<IList<string>> AssignSubsidiesAsync(PackName packName, PackNameRegistration registration, MultiStoreSettings storeSettings)
    {
        var assigned = new List<string>();

        if (registration.Subsidies.Count == 0)
            return assigned;

        if ((!registration.ByAdministrator && !storeSettings.AllowSubsidySelection) || !await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, registration.StoreId))
            return assigned;

        var activeSubsidies = await _subsidyService.GetActiveSubsidiesByStoreIdAsync(registration.StoreId);
        var done = new HashSet<int>();

        foreach (var assignment in registration.Subsidies)
        {
            var subsidy = activeSubsidies.FirstOrDefault(candidate => candidate.Id == assignment.SubsidyId);

            if (subsidy == null || !done.Add(subsidy.Id))
                continue;

            var subsidyPackName = new SubsidyPackName
            {
                SubsidyId = subsidy.Id,
                PackNameId = packName.Id,
                CreatorId = registration.CreatedByCustomerId,
                CreatedOnUtc = DateTime.UtcNow,
                Active = true
            };

            await _subsidyService.InsertSubsidyPackNameAsync(subsidyPackName);

            await _customerActivityService.InsertActivityAsync("AddSubsidyPackName",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddSubsidyPackName"), subsidy.Id, 1));

            assigned.Add(subsidy.Name);

            var credit = storeSettings.PackNamesOwnGlobalValue ? assignment.Credit : null;
            var durationStart = storeSettings.PackNamesOwnStartAndFinishDates ? assignment.DurationStart : null;
            var durationEnd = storeSettings.PackNamesOwnStartAndFinishDates ? assignment.DurationEnd : null;

            if (!credit.HasValue && !durationStart.HasValue)
                continue;

            var variation = new SubsidyVariation
            {
                SubsidyPackNameId = subsidyPackName.Id,
                Credit = credit,
                DurationStart = durationStart,
                DurationEnd = durationEnd,
                CreatedOnUtc = DateTime.UtcNow
            };

            await _subsidyService.InsertSubsidyVariationAsync(variation);

            await _subsidyService.InsertSubsidyVariationHistoryAsync(new SubsidyVariationHistory
            {
                SubsidyVariationId = variation.Id,
                Credit = variation.Credit,
                DurationStart = variation.DurationStart,
                DurationEnd = variation.DurationEnd,
                Reason = await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Subsidies.VariationOnCreate"),
                CreatorId = registration.CreatedByCustomerId,
                CreatedOnUtc = DateTime.UtcNow
            });

            await _customerActivityService.InsertActivityAsync("AddSubsidyVariation",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddSubsidyVariation"), variation.Id), variation);
        }

        return assigned;
    }

    #endregion

    #region Methods

    public virtual async Task<PackNameList> ResolveTargetPackNameListAsync(PackName manager, Branch branch, MultiStoreSettings storeSettings)
    {
        if (manager == null || branch == null)
            return null;

        //the branch must belong to the manager's own store whatever LimitAssignedBranches says; inside the
        //store the setting decides
        var managerBranch = await _packNameService.GetBranchByPackNameIdAsync(manager.Id);

        if (managerBranch == null || managerBranch.StoreId != branch.StoreId)
            return null;

        if (!storeSettings.LimitAssignedBranches)
            return await ResolveTargetPackNameListAsync(branch);

        var managerLists = (await _multiStoreService.GetPackNameListsByManagerIdAsync(manager.Id))
            .Where(pnl => pnl.BranchId.HasValue && pnl.BranchId == branch.Id)
            .ToList();

        return managerLists.FirstOrDefault(pnl => pnl.IsDefault) ?? managerLists.FirstOrDefault();
    }

    public virtual async Task<PackNameList> ResolveTargetPackNameListAsync(Branch branch)
    {
        if (branch == null)
            return null;

        //a list that is switched off takes no new packnames: nobody would see them
        var branchLists = (await _packNameService.GetPackNameListsByBranchIdAsync(branch.Id)).Where(pnl => pnl.Active).ToList();

        return branchLists.FirstOrDefault(pnl => pnl.IsDefault) ?? branchLists.FirstOrDefault();
    }

    public virtual async Task<PackNameRegistrationResult> RegisterAsync(PackNameRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(registration.Branch);
        ArgumentNullException.ThrowIfNull(registration.PackNameList);

        using var scope = PackNameTransaction.Begin(TransactionManager.DefaultTimeout);

        //checked again inside the transaction: the import checks every row before it writes the first one, so a name
        //added in the meantime (the add form, another import) would otherwise go into the list a second time. A list
        //being written by another transaction is read once that transaction ends
        if (await _packNameService.PackNameNameExistsAsync(registration.PackNameList.Id, registration.Name))
        {
            return new PackNameRegistrationResult
            {
                NameTaken = true,
                Errors = new List<string>
                {
                    string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Name.Exists"), registration.Name, registration.Branch.Name)
                }
            };
        }

        var errors = new List<string>();
        Customer customer = null;

        if (registration.Customer != null)
        {
            customer = await RegisterCustomerAsync(registration, errors);

            if (customer == null)
                return new PackNameRegistrationResult { Errors = errors };
        }

        //an empty or unusable colour gets a random one
        var colour = PackNameColourHelper.Normalize(registration.Colour) ?? $"#{Random.Shared.Next(0x1000000):X6}";

        var packName = new PackName
        {
            PackNameListId = registration.PackNameList.Id,
            Name = registration.Name,
            PrintName = registration.PrintName,
            Group = registration.Group,
            Active = true,
            //a packname without a gender of its own is mixed, as the import and provisioning make it
            Gender = string.IsNullOrEmpty(registration.Gender) ? MultiStoreServicesDefaults.GenderMixed : registration.Gender,
            Colour = colour,
            Comments = registration.Comments,
            EmployeeNumber = registration.EmployeeNumber,
            POEntry = registration.POEntry,
            DepartmentId = registration.DepartmentId.GetValueOrDefault() > 0 ? registration.DepartmentId : null,
            CostCenterId = registration.CostCenterId.GetValueOrDefault() > 0 ? registration.CostCenterId : null,
            CustomerId = customer?.Id
        };

        await _packNameService.InsertPackNameAsync(packName);

        await _customerActivityService.InsertActivityAsync("AddNewPackName",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddNewPackName"), packName.Name), packName);

        Address packNameAddress = null;
        var addressCompany = _addressSettings.CompanyEnabled ? registration.Branch.Name : null;

        if (registration.Address != null)
        {
            packNameAddress = registration.Address;
            packNameAddress.Company = addressCompany;
            packNameAddress.CreatedOnUtc = DateTime.UtcNow;

            if (packNameAddress.CountryId == 0)
                packNameAddress.CountryId = null;
            if (packNameAddress.StateProvinceId == 0)
                packNameAddress.StateProvinceId = null;

            await _addressService.InsertAddressAsync(packNameAddress);
            await _packNameService.InsertPackNameAddressAsync(packName, packNameAddress);
        }

        if (customer != null)
            await SetCustomerAddressAsync(customer, packNameAddress, addressCompany);

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(registration.StoreId);
        var subsidyNames = await AssignSubsidiesAsync(packName, registration, storeSettings);

        var welcomeEmailQueued = false;

        if (customer != null)
        {
            var queuedEmailIds = await _packNameWorkflowMessageService.SendCustomerRegisteredByManagerMessageAsync(customer, packName, registration.Branch.Name,
                subsidyNames, registration.StoreId, registration.LanguageId);

            welcomeEmailQueued = queuedEmailIds?.Count > 0;
        }

        scope.Complete();

        return new PackNameRegistrationResult
        {
            PackName = packName,
            Customer = customer,
            WelcomeEmailQueued = welcomeEmailQueued
        };
    }

    #endregion
}