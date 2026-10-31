using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Gdpr;
using Nop.Core.Domain.PriceLists;
using Nop.Core.Domain.Tax;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Attributes;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Gdpr;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.PriceLists;
using Nop.Services.Security;
using Nop.Services.Tax;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Areas.Admin.Models.Customers;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using PhoneNumbers;
using System.Transactions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

public partial class ExtendedCustomerController : BaseAdminController
{
    #region Fields

    protected readonly CustomerSettings _customerSettings;
    protected readonly DateTimeSettings _dateTimeSettings;
    protected readonly GdprSettings _gdprSettings;
    protected readonly IAttributeParser<CustomerAttribute, CustomerAttributeValue> _customerAttributeParser;
    protected readonly IAttributeService<CustomerAttribute, CustomerAttributeValue> _customerAttributeService;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly ICustomerOverrideModelFactory _customerModelFactory;
    protected readonly ICustomerRegistrationService _customerRegistrationService;
    protected readonly ICustomerOverrideService _customerService;
    protected readonly IEventPublisher _eventPublisher;
    protected readonly IGdprService _gdprService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly IPriceListService _priceListService;
    protected readonly IStoreContext _storeContext;
    protected readonly ITaxService _taxService;
    protected readonly IWorkContext _workContext;
    protected readonly IWorkflowMessageService _workflowMessageService;
    protected readonly OtpSettings _otpSettings;
    protected readonly TaxSettings _taxSettings;
    protected readonly IPackNameLandingService _packNameLandingService;
    protected readonly IPackNameProvisioningService _packNameProvisioningService;
    private static readonly char[] _separator = [','];

    #endregion

    #region Ctor

    public ExtendedCustomerController(CustomerSettings customerSettings,
        DateTimeSettings dateTimeSettings,
        GdprSettings gdprSettings,
        IAttributeParser<CustomerAttribute, CustomerAttributeValue> customerAttributeParser,
        IAttributeService<CustomerAttribute, CustomerAttributeValue> customerAttributeService,
        ICustomerActivityService customerActivityService,
        ICustomerOverrideModelFactory customerModelFactory,
        ICustomerRegistrationService customerRegistrationService,
        ICustomerOverrideService customerService,
        IEventPublisher eventPublisher,
        IGdprService gdprService,
        IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        INewsLetterSubscriptionService newsLetterSubscriptionService,
        INotificationService notificationService,
        IPermissionService permissionService,
        IPriceListService priceListService,
        IStoreContext storeContext,
        ITaxService taxService,
        IWorkContext workContext,
        IWorkflowMessageService workflowMessageService,
        OtpSettings otpSettings,
        TaxSettings taxSettings,
        IPackNameLandingService packNameLandingService,
        IPackNameProvisioningService packNameProvisioningService)
    {
        _customerSettings = customerSettings;
        _dateTimeSettings = dateTimeSettings;
        _gdprSettings = gdprSettings;
        _customerAttributeParser = customerAttributeParser;
        _customerAttributeService = customerAttributeService;
        _customerActivityService = customerActivityService;
        _customerModelFactory = customerModelFactory;
        _customerRegistrationService = customerRegistrationService;
        _customerService = customerService;
        _eventPublisher = eventPublisher;
        _gdprService = gdprService;
        _genericAttributeService = genericAttributeService;
        _localizationService = localizationService;
        _newsLetterSubscriptionService = newsLetterSubscriptionService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _priceListService = priceListService;
        _storeContext = storeContext;
        _taxService = taxService;
        _workContext = workContext;
        _workflowMessageService = workflowMessageService;
        _otpSettings = otpSettings;
        _taxSettings = taxSettings;
        _packNameLandingService = packNameLandingService;
        _packNameProvisioningService = packNameProvisioningService;
    }

    #endregion

    #region Utilities

    protected virtual async Task<string> ValidateCustomerRolesAsync(IList<CustomerRole> customerRoles, IList<CustomerRole> existingCustomerRoles)
    {
        ArgumentNullException.ThrowIfNull(customerRoles);

        ArgumentNullException.ThrowIfNull(existingCustomerRoles);

        //check ACL permission to manage customer roles
        var rolesToAdd = customerRoles.Except(existingCustomerRoles, new CustomerRoleComparerByName());
        var rolesToDelete = existingCustomerRoles.Except(customerRoles, new CustomerRoleComparerByName());
        if (rolesToAdd.Any(role => role.SystemName != NopCustomerDefaults.RegisteredRoleName) || rolesToDelete.Any())
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_ACL))
                return await _localizationService.GetResourceAsync("Admin.Customers.Customers.CustomerRolesManagingError");
        }

        //ensure a customer is not added to both 'Guests' and 'Registered' customer roles
        //ensure that a customer is in at least one required role ('Guests' and 'Registered')
        var isInGuestsRole = customerRoles.FirstOrDefault(cr => cr.SystemName == NopCustomerDefaults.GuestsRoleName) != null;
        var isInRegisteredRole = customerRoles.FirstOrDefault(cr => cr.SystemName == NopCustomerDefaults.RegisteredRoleName) != null;
        if (isInGuestsRole && isInRegisteredRole)
            return await _localizationService.GetResourceAsync("Admin.Customers.Customers.GuestsAndRegisteredRolesError");
        if (!isInGuestsRole && !isInRegisteredRole)
            return await _localizationService.GetResourceAsync("Admin.Customers.Customers.AddCustomerToGuestsOrRegisteredRoleError");

        //no errors
        return string.Empty;
    }

    protected virtual async Task<string> ParseCustomCustomerAttributesAsync(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var attributesXml = string.Empty;
        var customerAttributes = await _customerAttributeService.GetAllAttributesAsync();
        foreach (var attribute in customerAttributes)
        {
            var controlId = $"{NopCustomerServicesDefaults.CustomerAttributePrefix}{attribute.Id}";
            StringValues ctrlAttributes;

            switch (attribute.AttributeControlType)
            {
                case AttributeControlType.DropdownList:
                case AttributeControlType.RadioList:
                    ctrlAttributes = form[controlId];
                    if (!StringValues.IsNullOrEmpty(ctrlAttributes))
                    {
                        var selectedAttributeId = int.Parse(ctrlAttributes);
                        if (selectedAttributeId > 0)
                        {
                            attributesXml = _customerAttributeParser.AddAttribute(attributesXml,
                                attribute, selectedAttributeId.ToString());
                        }
                    }

                    break;
                case AttributeControlType.Checkboxes:
                    var cblAttributes = form[controlId];
                    if (!StringValues.IsNullOrEmpty(cblAttributes))
                    {
                        foreach (var item in cblAttributes.ToString()
                                     .Split(_separator, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var selectedAttributeId = int.Parse(item);
                            if (selectedAttributeId > 0)
                            {
                                attributesXml = _customerAttributeParser.AddAttribute(attributesXml,
                                    attribute, selectedAttributeId.ToString());
                            }
                        }
                    }

                    break;
                case AttributeControlType.ReadonlyCheckboxes:
                    //load read-only (already server-side selected) values
                    var attributeValues = await _customerAttributeService.GetAttributeValuesAsync(attribute.Id);
                    foreach (var selectedAttributeId in attributeValues
                                 .Where(v => v.IsPreSelected)
                                 .Select(v => v.Id)
                                 .ToList())
                    {
                        attributesXml = _customerAttributeParser.AddAttribute(attributesXml,
                            attribute, selectedAttributeId.ToString());
                    }

                    break;
                case AttributeControlType.TextBox:
                case AttributeControlType.MultilineTextbox:
                    ctrlAttributes = form[controlId];
                    if (!StringValues.IsNullOrEmpty(ctrlAttributes))
                    {
                        var enteredText = ctrlAttributes.ToString().Trim();
                        attributesXml = _customerAttributeParser.AddAttribute(attributesXml,
                            attribute, enteredText);
                    }

                    break;
                case AttributeControlType.Datepicker:
                case AttributeControlType.ColorSquares:
                case AttributeControlType.ImageSquares:
                case AttributeControlType.FileUpload:
                //not supported customer attributes
                default:
                    break;
            }
        }

        return attributesXml;
    }

    protected virtual async Task<bool> SecondAdminAccountExistsAsync(Customer customer)
    {
        var customers = await _customerService.GetAllCustomersAsync(customerRoleIds: [(await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.AdministratorsRoleName)).Id]);

        return customers.Any(c => c.Active && c.Id != customer.Id);
    }

    protected virtual string FormatPhoneNumber(string phone)
    {
        if (string.IsNullOrEmpty(phone))
            return phone;

        try
        {
            var phoneNumberUtil = PhoneNumberUtil.GetInstance();
            var regionCode = phoneNumberUtil.GetRegionCodeForNumber(phoneNumberUtil.Parse(phone, null));

            var phoneNumber = phoneNumberUtil.Parse(phone, regionCode);
            return phoneNumberUtil.Format(phoneNumber, PhoneNumberFormat.E164);
        }
        catch
        {
            return string.Empty;
        }
    }

    protected virtual async Task<bool> ImpersonateAsync(Customer customer)
    {
        if (!customer.Active)
        {
            _notificationService.WarningNotification(
                await _localizationService.GetResourceAsync("Admin.Customers.Customers.Impersonate.Inactive"));
            return false;
        }

        //ensure that a non-admin user cannot impersonate as an administrator
        //otherwise, that user can simply impersonate as an administrator and gain additional administrative privileges
        var currentCustomer = await _workContext.GetCurrentCustomerAsync();
        if (!await _customerService.IsAdminAsync(currentCustomer) && await _customerService.IsAdminAsync(customer))
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.NonAdminNotImpersonateAsAdminError"));
            return false;
        }

        //activity log
        await _customerActivityService.InsertActivityAsync("Impersonation.Started",
            string.Format(await _localizationService.GetResourceAsync("ActivityLog.Impersonation.Started.StoreOwner"), customer.Email, customer.Id), customer);
        await _customerActivityService.InsertActivityAsync(customer, "Impersonation.Started",
            string.Format(await _localizationService.GetResourceAsync("ActivityLog.Impersonation.Started.Customer"), currentCustomer.Email, currentCustomer.Id), currentCustomer);

        //ensure login is not required
        customer.RequireReLogin = false;
        await _customerService.UpdateCustomerAsync(customer);
        await _genericAttributeService.SaveAttributeAsync<int?>(currentCustomer, NopCustomerDefaults.ImpersonatedCustomerIdAttribute, customer.Id);

        return true;
    }

    #endregion

    #region Customers

    public virtual IActionResult Index()
    {
        return RedirectToAction("List", "ExtendedCustomer");
    }

    [CheckPermission(StandardPermission.Customers.CUSTOMERS_VIEW)]
    public virtual async Task<IActionResult> List()
    {
        //prepare model
        var model = await _customerModelFactory.PrepareCustomerSearchModelAsync(new ExtendedCustomerSearchModel());

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Customer/List.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_VIEW)]
    public virtual async Task<IActionResult> CustomerList(ExtendedCustomerSearchModel searchModel)
    {
        //prepare model
        var model = await _customerModelFactory.PrepareCustomerListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> Create()
    {
        //prepare model
        var model = await _customerModelFactory.PrepareExtendedCustomerModelAsync(new ExtendedCustomerModel(), null);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Customer/Create.cshtml", model);
    }

    [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
    [FormValueRequired("save", "save-continue")]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> Create(ExtendedCustomerModel model, bool continueEditing, IFormCollection form)
    {
        if (!string.IsNullOrWhiteSpace(model.Email) && await _customerService.GetCustomerByEmailAsync(model.Email) != null)
            ModelState.AddModelError(string.Empty, "Email is already registered");

        if (!string.IsNullOrWhiteSpace(model.Username) && _customerSettings.UsernamesEnabled &&
            await _customerService.GetCustomerByUsernameAsync(model.Username) != null)
        {
            ModelState.AddModelError(string.Empty, "Username is already registered");
        }

        //validate customer roles
        var allCustomerRoles = await _customerService.GetAllCustomerRolesAsync(true);
        var newCustomerRoles = new List<CustomerRole>();
        foreach (var customerRole in allCustomerRoles)
        {
            if (model.SelectedCustomerRoleIds.Contains(customerRole.Id))
                newCustomerRoles.Add(customerRole);
        }

        var customerRolesError = await ValidateCustomerRolesAsync(newCustomerRoles, new List<CustomerRole>());
        if (!string.IsNullOrEmpty(customerRolesError))
        {
            ModelState.AddModelError(string.Empty, customerRolesError);
            _notificationService.ErrorNotification(customerRolesError);
        }

        // Ensure that valid email address is entered if Registered role is checked to avoid registered customers with empty email address
        if (newCustomerRoles.Any() && newCustomerRoles.FirstOrDefault(c => c.SystemName == NopCustomerDefaults.RegisteredRoleName) != null &&
            !CommonHelper.IsValidEmail(model.Email))
        {
            ModelState.AddModelError(string.Empty, await _localizationService.GetResourceAsync("Admin.Customers.Customers.ValidEmailRequiredRegisteredRole"));

            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.ValidEmailRequiredRegisteredRole"));
        }

        //check is verified phone number
        var phoneNumber = FormatPhoneNumber(model.Phone);
        if (_otpSettings.LoginByPhoneEnabled && !string.IsNullOrEmpty(phoneNumber))
        {
            if (await _customerService.IsAlreadyExistsVerifiedPhoneNumberAsync(null, phoneNumber))
            {
                ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.IsAlreadyExistsVerifiedPhoneNumber"));
            }
        }

        //custom customer attributes
        var customerAttributesXml = await ParseCustomCustomerAttributesAsync(form);
        if (newCustomerRoles.Any() && newCustomerRoles.FirstOrDefault(c => c.SystemName == NopCustomerDefaults.RegisteredRoleName) != null)
        {
            var customerAttributeWarnings = await _customerAttributeParser.GetAttributeWarningsAsync(customerAttributesXml);
            foreach (var error in customerAttributeWarnings)
                ModelState.AddModelError(string.Empty, error);
        }

        //validate customer price lists
        var allCustomerPriceLists = await _priceListService.GetAllPriceListsAsync();
        var newCustomerPriceLists = new List<PriceList>();
        foreach (var customerPriceList in allCustomerPriceLists)
        {
            if (model.SelectedPriceListIds.Contains(customerPriceList.Id))
                newCustomerPriceLists.Add(customerPriceList);
        }

        if (ModelState.IsValid)
        {
            try
            {
                //fill entity from model
                var customer = model.ToEntity<Customer>();
                var currentStore = await _storeContext.GetCurrentStoreAsync();

                customer.CustomerGuid = Guid.NewGuid();
                customer.CreatedOnUtc = DateTime.UtcNow;
                customer.LastActivityDateUtc = DateTime.UtcNow;
                customer.RegisteredInStoreId = currentStore.Id;

                //form fields
                if (_dateTimeSettings.AllowCustomersToSetTimeZone)
                    customer.TimeZoneId = model.TimeZoneId;
                if (_customerSettings.GenderEnabled)
                    customer.Gender = model.Gender;
                if (_customerSettings.FirstNameEnabled)
                    customer.FirstName = model.FirstName;
                if (_customerSettings.LastNameEnabled)
                    customer.LastName = model.LastName;
                if (_customerSettings.DateOfBirthEnabled)
                    customer.DateOfBirth = model.DateOfBirth;
                if (_customerSettings.CompanyEnabled)
                    customer.Company = model.Company;
                if (_customerSettings.StreetAddressEnabled)
                    customer.StreetAddress = model.StreetAddress;
                if (_customerSettings.StreetAddress2Enabled)
                    customer.StreetAddress2 = model.StreetAddress2;
                if (_customerSettings.ZipPostalCodeEnabled)
                    customer.ZipPostalCode = model.ZipPostalCode;
                if (_customerSettings.CityEnabled)
                    customer.City = model.City;
                if (_customerSettings.CountyEnabled)
                    customer.County = model.County;
                if (_customerSettings.CountryEnabled)
                    customer.CountryId = model.CountryId;
                if (_customerSettings.CountryEnabled && _customerSettings.StateProvinceEnabled)
                    customer.StateProvinceId = model.StateProvinceId;
                if (_customerSettings.PhoneEnabled)
                {
                    customer.Phone = model.Phone;
                    customer.PhoneSmsVerified = model.PhoneSmsVerified;
                }

                if (_customerSettings.FaxEnabled)
                    customer.Fax = model.Fax;
                customer.CustomCustomerAttributesXML = customerAttributesXml;

                await _customerService.InsertCustomerAsync(customer);

                //customer branch
                var customerBranch = new CustomerBranch
                {
                    Id = customer.Id,
                    BranchId = model.BranchId == 0 ? null : model.BranchId,
                    LastModifiedDateUtc = DateTime.UtcNow,
                };

                await _customerService.InsertCustomerBranchAsync(customerBranch);

                //the store may ask for a packname for every customer linked to one of its branches
                if (customerBranch.BranchId.HasValue)
                {
                    var packNameWarning = await _packNameProvisioningService.ProvisionCustomerBranchAsync(customer, customerBranch.BranchId.Value);

                    if (!string.IsNullOrEmpty(packNameWarning))
                        _notificationService.WarningNotification(packNameWarning);
                }

                //password
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    var changePassRequest = new ChangePasswordRequest(model.Email, false,
                        _customerSettings.DefaultPasswordFormat, model.Password);
                    var changePassResult = await _customerRegistrationService.ChangePasswordAsync(changePassRequest);
                    if (!changePassResult.Success)
                    {
                        foreach (var changePassError in changePassResult.Errors)
                            _notificationService.ErrorNotification(changePassError);
                    }
                }

                //customer roles
                foreach (var customerRole in newCustomerRoles)
                {
                    //ensure that the current customer cannot add to "Administrators" system role if he's not an admin himself
                    if (customerRole.SystemName == NopCustomerDefaults.AdministratorsRoleName &&
                        !await _customerService.IsAdminAsync(await _workContext.GetCurrentCustomerAsync()))
                        continue;

                    await _customerService.AddCustomerRoleMappingAsync(
                        new CustomerCustomerRoleMapping { CustomerId = customer.Id, CustomerRoleId = customerRole.Id });
                }

                await _customerService.UpdateCustomerAsync(customer);

                //customer price lists
                foreach (var customerPriceList in newCustomerPriceLists)
                {
                    await _priceListService.InsertPriceListCustomerAsync(
                        new PriceListCustomer { CustomerId = customer.Id, PriceListId = customerPriceList.Id });
                }

                //ensure that a customer with a vendor associated is not in "Administrators" role
                //otherwise, he won't have access to other functionality in admin area
                if (await _customerService.IsAdminAsync(customer) && customer.VendorId > 0)
                {
                    customer.VendorId = 0;
                    await _customerService.UpdateCustomerAsync(customer);

                    _notificationService.ErrorNotification(
                        await _localizationService.GetResourceAsync("Admin.Customers.Customers.AdminCouldNotbeVendor"));
                }

                //ensure that a customer in the Vendors role has a vendor account associated.
                //otherwise, he will have access to ALL products
                if (await _customerService.IsVendorAsync(customer) && customer.VendorId == 0)
                {
                    var vendorRole =
                        await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.VendorsRoleName);
                    await _customerService.RemoveCustomerRoleMappingAsync(customer, vendorRole);

                    _notificationService.ErrorNotification(
                        await _localizationService.GetResourceAsync(
                            "Admin.Customers.Customers.CannotBeInVendoRoleWithoutVendorAssociated"));
                }

                //activity log
                await _customerActivityService.InsertActivityAsync("AddNewCustomer",
                    string.Format(await _localizationService.GetResourceAsync("ActivityLog.AddNewCustomer"),
                        customer.Id), customer);
                _notificationService.SuccessNotification(
                    await _localizationService.GetResourceAsync("Admin.Customers.Customers.Added"));

                if (!continueEditing)
                    return RedirectToAction("List");

                return RedirectToAction("Edit", new { id = customer.Id });
            }
            catch (Exception exc)
            {
                await _notificationService.ErrorNotificationAsync(exc);
            }
        }

        //prepare model
        model = await _customerModelFactory.PrepareExtendedCustomerModelAsync(model, null, true);

        //if we got this far, something failed, redisplay form
        return View("~/Plugins/CW.Core/Areas/Admin/Views/Customer/Create.cshtml", model);
    }

    [CheckPermission(StandardPermission.Customers.CUSTOMERS_VIEW)]
    public virtual async Task<IActionResult> Edit(int id)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null || customer.Deleted)
            return RedirectToAction("List");

        //prepare model
        var model = await _customerModelFactory.PrepareExtendedCustomerModelAsync(null, customer);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Customer/Edit.cshtml", model);
    }

    [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
    [FormValueRequired("save", "save-continue")]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> Edit(ExtendedCustomerModel model, bool continueEditing, IFormCollection form)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(model.Id);
        if (customer == null || customer.Deleted)
            return RedirectToAction("List");

        //validate customer roles
        var allCustomerRoles = await _customerService.GetAllCustomerRolesAsync(true);
        var newCustomerRoles = new List<CustomerRole>();
        foreach (var customerRole in allCustomerRoles)
        {
            if (model.SelectedCustomerRoleIds.Contains(customerRole.Id))
                newCustomerRoles.Add(customerRole);
        }

        var customerRolesError = await ValidateCustomerRolesAsync(newCustomerRoles, await _customerService.GetCustomerRolesAsync(customer));

        if (!string.IsNullOrEmpty(customerRolesError))
        {
            ModelState.AddModelError(string.Empty, customerRolesError);
            _notificationService.ErrorNotification(customerRolesError);
        }

        // Ensure that valid email address is entered if Registered role is checked to avoid registered customers with empty email address
        if (newCustomerRoles.Any() && newCustomerRoles.FirstOrDefault(c => c.SystemName == NopCustomerDefaults.RegisteredRoleName) != null &&
            !CommonHelper.IsValidEmail(model.Email))
        {
            ModelState.AddModelError(string.Empty, await _localizationService.GetResourceAsync("Admin.Customers.Customers.ValidEmailRequiredRegisteredRole"));
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.ValidEmailRequiredRegisteredRole"));
        }

        //check is verified phone number
        var phoneNumber = FormatPhoneNumber(model.Phone);
        if (_otpSettings.LoginByPhoneEnabled && !string.IsNullOrEmpty(phoneNumber))
        {
            if (await _customerService.IsAlreadyExistsVerifiedPhoneNumberAsync(customer, phoneNumber))
            {
                ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.IsAlreadyExistsVerifiedPhoneNumber"));
            }
        }

        //custom customer attributes
        var customerAttributesXml = await ParseCustomCustomerAttributesAsync(form);
        if (newCustomerRoles.Any() && newCustomerRoles.FirstOrDefault(c => c.SystemName == NopCustomerDefaults.RegisteredRoleName) != null)
        {
            var customerAttributeWarnings = await _customerAttributeParser.GetAttributeWarningsAsync(customerAttributesXml);
            foreach (var error in customerAttributeWarnings)
                ModelState.AddModelError(string.Empty, error);
        }

        //a customer's packname follows them to another branch: a change it cannot follow is refused
        if (model.BranchId != ((await _customerService.GetCustomerBranchByIdAsync(customer.Id))?.BranchId ?? 0))
        {
            var branchChangeError = await _packNameProvisioningService.CheckCustomerBranchChangeAsync(customer, model.BranchId);

            if (!string.IsNullOrEmpty(branchChangeError))
                ModelState.AddModelError(nameof(model.BranchId), branchChangeError);
        }

        if (ModelState.IsValid)
        {
            try
            {
                customer.AdminComment = model.AdminComment;
                customer.IsTaxExempt = model.IsTaxExempt;
                customer.MustChangePassword = model.MustChangePassword;

                //prevent deactivation of the last active administrator
                if (!await _customerService.IsAdminAsync(customer) || model.Active || await SecondAdminAccountExistsAsync(customer))
                    customer.Active = model.Active;
                else
                    _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.AdminAccountShouldExists.Deactivate"));

                //email
                if (!string.IsNullOrWhiteSpace(model.Email))
                    await _customerRegistrationService.SetEmailAsync(customer, model.Email, false);
                else
                    customer.Email = model.Email;

                //username
                if (_customerSettings.UsernamesEnabled)
                {
                    if (!string.IsNullOrWhiteSpace(model.Username))
                        await _customerRegistrationService.SetUsernameAsync(customer, model.Username);
                    else
                        customer.Username = model.Username;
                }

                //VAT number
                if (_taxSettings.EuVatEnabled)
                {
                    var prevVatNumber = customer.VatNumber;

                    customer.VatNumber = model.VatNumber;
                    //set VAT number status
                    if (!string.IsNullOrEmpty(model.VatNumber))
                    {
                        if (!model.VatNumber.Equals(prevVatNumber, StringComparison.InvariantCultureIgnoreCase))
                            customer.VatNumberStatusId = (int)(await _taxService.GetVatNumberStatusAsync(model.VatNumber)).vatNumberStatus;
                    }
                    else
                        customer.VatNumberStatusId = (int)VatNumberStatus.Empty;
                }

                //vendor
                customer.VendorId = model.VendorId;

                //form fields
                if (_dateTimeSettings.AllowCustomersToSetTimeZone)
                    customer.TimeZoneId = model.TimeZoneId;
                if (_customerSettings.GenderEnabled)
                    customer.Gender = model.Gender;
                if (_customerSettings.FirstNameEnabled)
                    customer.FirstName = model.FirstName;
                if (_customerSettings.LastNameEnabled)
                    customer.LastName = model.LastName;
                if (_customerSettings.DateOfBirthEnabled)
                    customer.DateOfBirth = model.DateOfBirth;
                if (_customerSettings.CompanyEnabled)
                    customer.Company = model.Company;
                if (_customerSettings.StreetAddressEnabled)
                    customer.StreetAddress = model.StreetAddress;
                if (_customerSettings.StreetAddress2Enabled)
                    customer.StreetAddress2 = model.StreetAddress2;
                if (_customerSettings.ZipPostalCodeEnabled)
                    customer.ZipPostalCode = model.ZipPostalCode;
                if (_customerSettings.CityEnabled)
                    customer.City = model.City;
                if (_customerSettings.CountyEnabled)
                    customer.County = model.County;
                if (_customerSettings.CountryEnabled)
                    customer.CountryId = model.CountryId;
                if (_customerSettings.CountryEnabled && _customerSettings.StateProvinceEnabled)
                    customer.StateProvinceId = model.StateProvinceId;
                if (_customerSettings.PhoneEnabled)
                {
                    customer.Phone = model.Phone;
                    customer.PhoneSmsVerified = model.PhoneSmsVerified;
                }
                if (_customerSettings.FaxEnabled)
                    customer.Fax = model.Fax;

                //custom customer attributes
                customer.CustomCustomerAttributesXML = customerAttributesXml;

                var currentCustomerRoleIds = await _customerService.GetCustomerRoleIdsAsync(customer, true);

                //customer roles
                foreach (var customerRole in allCustomerRoles)
                {
                    //ensure that the current customer cannot add/remove to/from "Administrators" system role
                    //if he's not an admin himself
                    if (customerRole.SystemName == NopCustomerDefaults.AdministratorsRoleName &&
                        !await _customerService.IsAdminAsync(await _workContext.GetCurrentCustomerAsync()))
                    {
                        continue;
                    }

                    if (model.SelectedCustomerRoleIds.Contains(customerRole.Id))
                    {
                        //new role
                        if (currentCustomerRoleIds.All(roleId => roleId != customerRole.Id))
                            await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping { CustomerId = customer.Id, CustomerRoleId = customerRole.Id });
                    }
                    else
                    {
                        //prevent attempts to delete the administrator role from the user, if the user is the last active administrator
                        if (customerRole.SystemName == NopCustomerDefaults.AdministratorsRoleName && !await SecondAdminAccountExistsAsync(customer))
                        {
                            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.AdminAccountShouldExists.DeleteRole"));
                            continue;
                        }

                        //remove role
                        if (currentCustomerRoleIds.Any(roleId => roleId == customerRole.Id))
                            await _customerService.RemoveCustomerRoleMappingAsync(customer, customerRole);
                    }
                }

                await _customerService.UpdateCustomerAsync(customer);

                //customer branch, saved together with the packname that follows the customer into it
                using (var scope = PackNameTransaction.Begin(TransactionManager.DefaultTimeout))
                {
                    var customerBranch = await _customerService.GetCustomerBranchByIdAsync(customer.Id);
                    customerBranch ??= new();

                    var previousBranchId = customerBranch.BranchId;

                    customerBranch.BranchId = model.BranchId == 0 ? null : model.BranchId;
                    customerBranch.LastModifiedDateUtc = DateTime.UtcNow;

                    if (customerBranch.Id == 0)
                    {
                        customerBranch.Id = customer.Id;
                        await _customerService.InsertCustomerBranchAsync(customerBranch);
                    }
                    else
                        await _customerService.UpdateCustomerBranchAsync(customerBranch);

                    //a new branch takes the customer's packname along, or gives the customer one when the store gives
                    //every branch customer a packname
                    if (customerBranch.BranchId.HasValue && customerBranch.BranchId != previousBranchId)
                    {
                        var packNameWarning = await _packNameProvisioningService.ApplyCustomerBranchAsync(customer, customerBranch.BranchId.Value);

                        if (!string.IsNullOrEmpty(packNameWarning))
                            _notificationService.WarningNotification(packNameWarning);
                    }

                    scope.Complete();
                }

                //customer price lists
                var allPriceLists = await _priceListService.GetAllPriceListsAsync();
                var allCustomerPriceLists = await _priceListService.GetPriceListsByCustomerAsync(customer);
                var currentCustomerPriceListIds = allCustomerPriceLists.Select(priceList => priceList.Id).ToList();

                //customer price lists
                foreach (var customerPriceList in allPriceLists)
                {
                    if (model.SelectedPriceListIds.Contains(customerPriceList.Id))
                    {
                        //new price list
                        if (currentCustomerPriceListIds.All(priceListId => priceListId != customerPriceList.Id))
                            await _priceListService.InsertPriceListCustomerAsync(new PriceListCustomer { PriceListId = customerPriceList.Id, CustomerId = customer.Id });
                    }
                    else
                    {
                        //remove price list
                        if (currentCustomerPriceListIds.Any(priceListId => priceListId == customerPriceList.Id))
                            await _priceListService.RemoveCustomerPriceListMappingAsync(customer, customerPriceList);
                    }
                }

                //ensure that a customer with a vendor associated is not in "Administrators" role
                //otherwise, he won't have access to the other functionality in admin area
                if (await _customerService.IsAdminAsync(customer) && customer.VendorId > 0)
                {
                    customer.VendorId = 0;
                    await _customerService.UpdateCustomerAsync(customer);
                    _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.AdminCouldNotbeVendor"));
                }

                //ensure that a customer in the Vendors role has a vendor account associated.
                //otherwise, he will have access to ALL products
                if (await _customerService.IsVendorAsync(customer) && customer.VendorId == 0)
                {
                    var vendorRole = await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.VendorsRoleName);
                    await _customerService.RemoveCustomerRoleMappingAsync(customer, vendorRole);

                    _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.CannotBeInVendoRoleWithoutVendorAssociated"));
                }

                //activity log
                await _customerActivityService.InsertActivityAsync("EditCustomer",
                    string.Format(await _localizationService.GetResourceAsync("ActivityLog.EditCustomer"), customer.Id), customer);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.Updated"));

                if (!continueEditing)
                    return RedirectToAction("List");

                return RedirectToAction("Edit", new { id = customer.Id });
            }
            catch (Exception exc)
            {
                _notificationService.ErrorNotification(exc.Message);
            }
        }

        //prepare model
        model = await _customerModelFactory.PrepareExtendedCustomerModelAsync(model, customer, true);

        //if we got this far, something failed, redisplay form
        return View("~/Plugins/CW.Core/Areas/Admin/Views/Customer/Edit.cshtml", model);
    }

    [HttpPost, ActionName("Edit")]
    [FormValueRequired("changepassword")]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> ChangePassword(CustomerModel model)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(model.Id);
        if (customer == null)
            return RedirectToAction("List");

        //ensure that the current customer cannot change passwords of "Administrators" if he's not an admin himself
        if (await _customerService.IsAdminAsync(customer) && !await _customerService.IsAdminAsync(await _workContext.GetCurrentCustomerAsync()))
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.OnlyAdminCanChangePassword"));
            return RedirectToAction("Edit", new { id = customer.Id });
        }

        try
        {
            var changePassRequest = new ChangePasswordRequest(customer.Email,
                false, _customerSettings.DefaultPasswordFormat, model.Password);
            var changePassResult = await _customerRegistrationService.ChangePasswordAsync(changePassRequest);
            if (changePassResult.Success)
            {
                _notificationService.SuccessNotification(
                    await _localizationService.GetResourceAsync("Admin.Customers.Customers.PasswordChanged"));
            }
            else
            {
                foreach (var error in changePassResult.Errors)
                    _notificationService.ErrorNotification(error);
            }
        }
        catch (Exception exc)
        {
            await _notificationService.ErrorNotificationAsync(exc);
        }

        return RedirectToAction("Edit", new { id = customer.Id });
    }

    [HttpPost, ActionName("Edit")]
    [FormValueRequired("markVatNumberAsValid")]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> MarkVatNumberAsValid(CustomerModel model)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(model.Id);
        if (customer == null)
            return RedirectToAction("List");

        customer.VatNumberStatusId = (int)VatNumberStatus.Valid;
        await _customerService.UpdateCustomerAsync(customer);

        return RedirectToAction("Edit", new { id = customer.Id });
    }

    [HttpPost, ActionName("Edit")]
    [FormValueRequired("markVatNumberAsInvalid")]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> MarkVatNumberAsInvalid(CustomerModel model)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(model.Id);
        if (customer == null)
            return RedirectToAction("List");

        customer.VatNumberStatusId = (int)VatNumberStatus.Invalid;
        await _customerService.UpdateCustomerAsync(customer);

        return RedirectToAction("Edit", new { id = customer.Id });
    }

    [HttpPost, ActionName("Edit")]
    [FormValueRequired("remove-affiliate")]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> RemoveAffiliate(CustomerModel model)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(model.Id);
        if (customer == null)
            return RedirectToAction("List");

        customer.AffiliateId = 0;
        await _customerService.UpdateCustomerAsync(customer);

        return RedirectToAction("Edit", new { id = customer.Id });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> RemoveBindMFA(int id)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null)
            return RedirectToAction("List");

        await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.SelectedMultiFactorAuthenticationProviderAttribute, string.Empty);

        //raise event       
        await _eventPublisher.PublishAsync(new CustomerChangeMultiFactorAuthenticationProviderEvent(customer));

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.UnbindMFAProvider"));

        return RedirectToAction("Edit", new { id = customer.Id });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> Delete(int id)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null)
            return RedirectToAction("List");

        try
        {
            //prevent attempts to delete the user, if it is the last active administrator
            if (await _customerService.IsAdminAsync(customer) && !await SecondAdminAccountExistsAsync(customer))
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.AdminAccountShouldExists.DeleteAdministrator"));
                return RedirectToAction("Edit", new { id = customer.Id });
            }

            //ensure that the current customer cannot delete "Administrators" if he's not an admin himself
            if (await _customerService.IsAdminAsync(customer) && !await _customerService.IsAdminAsync(await _workContext.GetCurrentCustomerAsync()))
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.OnlyAdminCanDeleteAdmin"));
                return RedirectToAction("Edit", new { id = customer.Id });
            }

            //get customer email before deleting customer entity to avoid problems with the changed email after deleting see CustomerSettings.SuffixDeletedCustomers settings
            var customerEmail = customer.Email;

            //delete
            await _customerService.DeleteCustomerAsync(customer);

            //remove newsletter subscriptions (if exist)
            var subscriptions = await _newsLetterSubscriptionService.GetNewsLetterSubscriptionsByEmailAsync(customerEmail);
            foreach (var subscription in subscriptions)
                await _newsLetterSubscriptionService.DeleteNewsLetterSubscriptionAsync(subscription);

            //activity log
            await _customerActivityService.InsertActivityAsync("DeleteCustomer",
                string.Format(await _localizationService.GetResourceAsync("ActivityLog.DeleteCustomer"), customer.Id), customer);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.Deleted"));

            return RedirectToAction("List");
        }
        catch (Exception exc)
        {
            _notificationService.ErrorNotification(exc.Message);
            return RedirectToAction("Edit", new { id = customer.Id });
        }
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    [CheckPermission(StandardPermission.Customers.GDPR_MANAGE)]
    public virtual async Task<IActionResult> GdprDelete(int id)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null)
            return RedirectToAction("List");

        if (!_gdprSettings.GdprEnabled)
            return RedirectToAction("List");

        try
        {
            //prevent attempts to delete the user, if it is the last active administrator
            if (await _customerService.IsAdminAsync(customer) && !await SecondAdminAccountExistsAsync(customer))
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.AdminAccountShouldExists.DeleteAdministrator"));
                return RedirectToAction("Edit", new { id = customer.Id });
            }

            //ensure that the current customer cannot delete "Administrators" if he's not an admin himself
            if (await _customerService.IsAdminAsync(customer) && !await _customerService.IsAdminAsync(await _workContext.GetCurrentCustomerAsync()))
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.OnlyAdminCanDeleteAdmin"));
                return RedirectToAction("Edit", new { id = customer.Id });
            }

            //delete
            await _gdprService.PermanentDeleteCustomerAsync(customer);

            //activity log
            await _customerActivityService.InsertActivityAsync("DeleteCustomer",
                string.Format(await _localizationService.GetResourceAsync("ActivityLog.DeleteCustomer"), customer.Id), customer);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.Deleted"));

            return RedirectToAction("List");
        }
        catch (Exception exc)
        {
            _notificationService.ErrorNotification(exc.Message);
            return RedirectToAction("Edit", new { id = customer.Id });
        }
    }

    [HttpPost, ActionName("Edit")]
    [FormValueRequired("impersonate")]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_IMPERSONATION)]
    public virtual async Task<IActionResult> Impersonate(int id)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null)
            return RedirectToAction("List");

        if (!await ImpersonateAsync(customer))
            return RedirectToAction("Edit", customer.Id);

        return await RedirectAfterImpersonationAsync(customer);
    }

    /// <summary>
    /// Where an impersonated session lands: the subsidy catalogue or the bulk ordering screen when the store
    /// asks for it and this customer qualifies, otherwise nop's homepage as before.
    /// </summary>
    /// <remarks>
    /// The customer passed here is the one being IMPERSONATED, and that is what the gate is asked about —
    /// not the admin. The admin's own permissions got them this far; whose session it is now is a different
    /// question, and a store where the admin can bulk-order but this customer cannot must land on the
    /// homepage.
    ///
    /// `area = string.Empty` is not decoration: this runs inside the Admin area, so `area` is an ambient
    /// route value and route generation would otherwise carry it into a URL for a public route that has no
    /// area at all.
    ///
    /// The impersonation itself has already been written (ImpersonateAsync saved the GenericAttribute) and
    /// takes effect on the NEXT request, which is the one this redirect causes. Nothing here depends on the
    /// work context having changed, which it has not.
    /// </remarks>
    protected virtual async Task<IActionResult> RedirectAfterImpersonationAsync(Customer customer)
    {
        var route = await _packNameLandingService.ResolveSignInLandingRouteAsync(customer);

        return route != null
            ? RedirectToRoute(route, new { area = string.Empty })
            : RedirectToAction("Index", "Home", new { area = string.Empty });
    }

    [CheckPermission(StandardPermission.Customers.CUSTOMERS_IMPERSONATION)]
    public virtual async Task<IActionResult> PlaceOrder(int id)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null)
            return RedirectToAction("List");

        if (!await ImpersonateAsync(customer))
            return RedirectToAction("Edit", customer.Id);

        //This action exists to START ORDERING as this customer, so of the two impersonation entry
        //points it is the one the homepage suits least.
        return await RedirectAfterImpersonationAsync(customer);
    }

    [HttpPost, ActionName("Edit")]
    [FormValueRequired("send-welcome-message")]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> SendWelcomeMessage(CustomerModel model)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(model.Id);
        if (customer == null)
            return RedirectToAction("List");

        await _workflowMessageService.SendCustomerWelcomeMessageAsync(customer, (await _workContext.GetWorkingLanguageAsync()).Id);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.SendWelcomeMessage.Success"));

        return RedirectToAction("Edit", new { id = customer.Id });
    }

    [HttpPost, ActionName("Edit")]
    [FormValueRequired("resend-activation-message")]
    [CheckPermission(StandardPermission.Customers.CUSTOMERS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> ReSendActivationMessage(CustomerModel model)
    {
        //try to get a customer with the specified id
        var customer = await _customerService.GetCustomerByIdAsync(model.Id);
        if (customer == null)
            return RedirectToAction("List");

        //email validation message
        await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.AccountActivationTokenAttribute, Guid.NewGuid().ToString());
        await _workflowMessageService.SendCustomerEmailValidationMessageAsync(customer, (await _workContext.GetWorkingLanguageAsync()).Id);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Customers.Customers.ReSendActivationMessage.Success"));

        return RedirectToAction("Edit", new { id = customer.Id });
    }

    #endregion
}