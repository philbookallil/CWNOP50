using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Shipping;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.PackNameCheckout;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Shipping;
using Nop.Services.Tax;
using Nop.Web.Factories;
using Nop.Web.Models.Checkout;
using Nop.Web.Models.Common;
using Nop.Web.Validators.Checkout;

namespace Nop.Plugin.CW.Core.Factories;

public class PackNameCheckoutModelFactory : IPackNameCheckoutModelFactory
{
    #region Fields

    protected readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly IBranchService _branchService;
    protected readonly IWorkContext _workContext;
    protected readonly IAddressService _addressService;
    protected readonly IAddressModelFactory _addressModelFactory;
    protected readonly AddressSettings _addressSettings;
    protected readonly ICountryService _countryService;
    protected readonly IPackNameService _packNameService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ITaxService _taxService;
    protected readonly ICurrencyService _currencyService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly ICustomerService _customerService;
    protected readonly ISettingService _settingService;
    protected readonly IShippingService _shippingService;
    protected readonly CaptchaSettings _captchaSettings;
    protected readonly OrderSettings _orderSettings;
    protected readonly IShoppingCartOverrideModelFactory _shoppingCartModelFactory;
    protected readonly MediaSettings _mediaSettings;
    protected readonly IPackNameShippingAssignmentService _shippingAssignmentService;
    protected readonly IRepository<Address> _addressRepository;
    protected readonly CheckoutBillingAddressValidator _billingAddressValidator;
    protected readonly IOrderTotalCalculationOverrideService _orderTotalCalculationOverrideService;
    protected readonly ISubsidyService _subsidyService;

    #endregion

    #region Ctor

    public PackNameCheckoutModelFactory(IShoppingCartOverrideService shoppingCartService,
        IBranchService branchService,
        IWorkContext workContext,
        IAddressService addressService,
        IAddressModelFactory addressModelFactory,
        AddressSettings addressSettings,
        ICountryService countryService,
        IPackNameService packNameService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ITaxService taxService,
        ICurrencyService currencyService,
        IPriceFormatter priceFormatter,
        ICustomerService customerService,
        ISettingService settingService,
        IShippingService shippingService,
        CaptchaSettings captchaSettings,
        OrderSettings orderSettings,
        IShoppingCartOverrideModelFactory shoppingCartModelFactory,
        ILocalizationService localizationService,
        IStateProvinceService stateProvinceService,
        CustomerSettings customerSettings,
        MediaSettings mediaSettings,
        IPackNameShippingAssignmentService shippingAssignmentService,
        IRepository<Address> addressRepository,
        IOrderTotalCalculationOverrideService orderTotalCalculationOverrideService,
        ISubsidyService subsidyService)
    {
        _shoppingCartService = shoppingCartService;
        _branchService = branchService;
        _workContext = workContext;
        _addressService = addressService;
        _addressModelFactory = addressModelFactory;
        _addressSettings = addressSettings;
        _countryService = countryService;
        _packNameService = packNameService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _taxService = taxService;
        _currencyService = currencyService;
        _priceFormatter = priceFormatter;
        _customerService = customerService;
        _settingService = settingService;
        _shippingService = shippingService;
        _captchaSettings = captchaSettings;
        _orderSettings = orderSettings;
        _shoppingCartModelFactory = shoppingCartModelFactory;
        _mediaSettings = mediaSettings;
        _shippingAssignmentService = shippingAssignmentService;
        _addressRepository = addressRepository;
        _billingAddressValidator = new CheckoutBillingAddressValidator(localizationService, stateProvinceService, addressSettings, customerSettings);
        _orderTotalCalculationOverrideService = orderTotalCalculationOverrideService;
        _subsidyService = subsidyService;
    }

    #endregion

    #region Utilities

    protected async Task<(int?, bool)> ResolvePackNameAddressIdAsync(Customer customer, int packNameId, int branchId, int storeId, MultiStoreSettings storeSettings, PackNameShippingAssignments assignments)
    {
        var addressWasDefaulted = false;
        var originalAddressId = assignments.GetAddressId(packNameId);
        var addressId = originalAddressId;

        var isValid = addressId is > 0 && await _shoppingCartService.IsAddressValidForCheckoutAsync(branchId, packNameId, addressId.Value, customer, storeId, storeSettings);

        if (!isValid)
        {
            addressId = null;

            if (!storeSettings.DoNotPreSelectShippingAddress)
            {
                var defaultAddress = await _branchService.GetDefaultShipAddressByBranchIdAsync(branchId);
                addressId = defaultAddress?.Id;
            }

            if (addressId != originalAddressId)
            {
                assignments.SetAddressId(packNameId, addressId);
                addressWasDefaulted = true;
            }
        }

        return (addressId, addressWasDefaulted);
    }

    protected async Task<PackNameShippingModel> PrepareShippingModelAsync(int packNameId, int branchId, int addressId, IList<ShoppingCartItem> cart, IList<PackNameShoppingCartItem> packNameCart, MultiStoreSettings storeSettings, Customer customer, int storeId, PackNameShippingAssignments assignments, Dictionary<int, Address> addressMap)
    {
        var shippingModel = new PackNameShippingModel()
        {
            BranchId = branchId,
            PackNameId = packNameId,
            RequiresShipping = true,
        };

        addressMap.TryGetValue(addressId, out var address);
        address ??= await _addressService.GetAddressByIdAsync(addressId);

        var shippingAddressModel = await PrepareShipAddressModelAsync(address);

        if ((shippingAddressModel?.Id ?? 0) == 0 && !storeSettings.DoNotPreSelectShippingAddress)
        {
            shippingModel.ShippingMethod.Warnings.Add("No address allocated");
            return shippingModel;
        }

        shippingModel.Address = shippingAddressModel;

        var packName = await _packNameService.GetPackNameByIdAsync(packNameId);

        if (packName == null)
        {
            shippingModel.ShippingMethod.Warnings.Add("PackName not found");
            return shippingModel;
        }

        var selectedOption = assignments.GetSelectedOption(packNameId);
        var offeredOptions = assignments.GetOfferedOptions(packNameId);

        if (selectedOption == null)
        {
            shippingModel.ShippingMethod.Warnings.Add(
                $"The default shipping methods allocated for packName {packName.Name} are not available");
            return shippingModel;
        }

        if (!offeredOptions.Any())
        {
            shippingModel.ShippingMethod.Warnings.Add(
                $"The default shipping methods allocated for packName {packName.Name} are not available");
            return shippingModel;
        }

        shippingModel.SelectedShippingMethod = selectedOption.Name;

        //every option is priced for this packname's own lines, as if it shipped alone
        foreach (var shippingOption in offeredOptions)
        {
            var soModel = new CheckoutShippingMethodModel.ShippingMethodModel
            {
                Name = shippingOption.Name,
                Description = shippingOption.Description,
                DisplayOrder = shippingOption.DisplayOrder ?? 0,
                ShippingRateComputationMethodSystemName = shippingOption.ShippingRateComputationMethodSystemName,
                ShippingOption = shippingOption,
            };

            var (shippingTotal, _) = await _orderTotalCalculationService.AdjustShippingRateAsync(shippingOption.Rate, cart, shippingOption.IsPickupInStore);
            var (rateBase, _) = await _taxService.GetShippingPriceAsync(shippingTotal, customer);
            var rate = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(rateBase, await _workContext.GetWorkingCurrencyAsync());
            soModel.Fee = await _priceFormatter.FormatShippingPriceAsync(rate, true);
            soModel.Rate = rate;

            shippingModel.ShippingMethod.ShippingMethods.Add(soModel);
        }

        //the other packnames whose parcel goes to the same address by the same method: shipping to that destination is
        //rated on all their lines together and charged once, which is what the order total does
        var sharedPackNameIds = packNameCart
            .Select(item => item.PackNameId)
            .Distinct()
            .Where(id => id != packNameId
                && assignments.GetAddressId(id) == addressId
                && assignments.GetSelectedOption(id)?.Name == selectedOption.Name)
            .ToList();

        if (sharedPackNameIds.Count == 0)
            return shippingModel;

        foreach (var sharedPackNameId in sharedPackNameIds)
        {
            var sharedPackName = await _packNameService.GetPackNameByIdAsync(sharedPackNameId);

            if (sharedPackName != null)
                shippingModel.SharedWithPackNames.Add(sharedPackName.Name);
        }

        var destinationCart = packNameCart
            .Where(item => item.PackNameId == packNameId || sharedPackNameIds.Contains(item.PackNameId))
            .Cast<ShoppingCartItem>()
            .ToList();

        var (destinationTotal, _) = await _orderTotalCalculationService.AdjustShippingRateAsync(selectedOption.Rate, destinationCart, selectedOption.IsPickupInStore);
        var (destinationRateBase, _) = await _taxService.GetShippingPriceAsync(destinationTotal, customer);
        var destinationRate = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(destinationRateBase, await _workContext.GetWorkingCurrencyAsync());
        shippingModel.SharedDestinationFee = await _priceFormatter.FormatShippingPriceAsync(destinationRate, true);

        return shippingModel;
    }

    protected async Task PrepareAvailableAddressesAsync(PackNameCheckoutModel model, IList<PackNameShoppingCartItem> packNameCart, MultiStoreSettings storeSettings)
    {
        var branchIds = packNameCart.Select(p => p.BranchId).Distinct().ToList();

        foreach (var branchId in branchIds)
        {
            var addressesModel = await PrepareBranchAvailableAddresses(branchId, storeSettings);

            foreach (var item in model.ShoppingCart.Items.Where(x => x.PackNameModel.BranchId == branchId))
                item.PackNameModel.AvailableAddresses.AddRange(addressesModel);
        }

        //If packName addresses are enabled for selection
        if (storeSettings.AllowHomeDelivery)
        {
            var packNameIds = packNameCart.Select(p => p.PackNameId).Distinct().ToList();

            foreach (var packNameId in packNameIds)
            {
                var customerId = packNameCart.FirstOrDefault(x => x.PackNameId == packNameId)?.PackNameCustomerId ?? 0;

                var addressesModel = await PrepareBranchPackNameAvailableAddresses(packNameId, customerId, storeSettings);

                foreach (var item in model.ShoppingCart.Items.Where(x => x.PackNameModel.Id == packNameId))
                    item.PackNameModel.AvailableAddresses.AddRange(addressesModel);
            }
        }
    }

    protected async Task<List<AddressModel>> PrepareBranchAvailableAddresses(int branchId, MultiStoreSettings storeSettings)
    {
        var availableAddresses = new List<AddressModel>();

        var shipAddress = await _branchService.GetDefaultShipAddressByBranchIdAsync(branchId);
        var branchAddress = await PrepareShipAddressModelAsync(shipAddress?.Id ?? 0);

        if (branchAddress.Id > 0)
            availableAddresses.Add(branchAddress);

        var addresses = await _branchService.GetAddressesByBranchIdAsync(branchId);
        if (branchAddress.Id > 0)
            addresses = addresses.Where(a => a.Id != branchAddress.Id).ToList();

        addresses = addresses.OrderBy(a => a.Email).ToList();//TODO: order by a better field

        var addressesModel = await addresses.SelectAwait(async shipAddress =>
        {
            return await PrepareShipAddressModelAsync(shipAddress.Id);
        }).ToListAsync();

        availableAddresses.AddRange(addressesModel);

        return availableAddresses;
    }

    protected async Task<List<AddressModel>> PrepareBranchPackNameAvailableAddresses(int packNameId, int customerId, MultiStoreSettings storeSettings)
    {
        if (!storeSettings.AllowHomeDelivery)
            return new List<AddressModel>();

        //Get packName addresses; a copy, since the request's cache holds the list itself and the customer's are added below
        var addresses = (await _packNameService.GetAddressesByPackNameIdAsync(packNameId)).ToList();

        //Addresses from the customer, less those that repeat one of the packname's: an account registered with its
        //packname holds its own copy of the packname's address
        if (customerId > 0)
        {
            var packNameAddresses = addresses.ToList();
            var customerAddresses = await _customerService.GetAddressesByCustomerIdAsync(customerId);

            foreach (var customerAddress in customerAddresses)
            {
                if (_addressService.FindAddress(packNameAddresses, customerAddress.FirstName, customerAddress.LastName, customerAddress.PhoneNumber,
                        customerAddress.Email, customerAddress.FaxNumber, customerAddress.Company, customerAddress.Address1, customerAddress.Address2,
                        customerAddress.City, customerAddress.County, customerAddress.StateProvinceId, customerAddress.ZipPostalCode,
                        customerAddress.CountryId, customerAddress.CustomAttributes) != null)
                    continue;

                addresses.Add(customerAddress);
            }
        }

        var addressesModel = await addresses.SelectAwait(async address =>
        {
            var addressModel = new AddressModel();
            await _addressModelFactory.PrepareAddressModelAsync(addressModel,
                address: address,
                excludeProperties: false,
                addressSettings: _addressSettings,
                loadCountries: async () => await _countryService.GetAllCountriesAsync((await _workContext.GetWorkingLanguageAsync()).Id));
            return addressModel;
        }).ToListAsync();

        return addressesModel;
    }

    /// <summary>
    /// Prepares the shipping data for the specified packName shopping cart items and updates the provided checkout
    /// model.
    /// </summary>
    /// <remarks>This method determines the shipping options and address selection capabilities based
    /// on company settings and updates the checkout model accordingly. It also prepares the list of available
    /// addresses for the customer.</remarks>
    /// <param name="packNameCart">A collection of <see cref="PackNameShoppingCartItem"/> objects representing the items in the shopping cart.</param>
    /// <param name="model">The <see cref="PackNameCheckoutModel"/> to be updated with shipping data, including available addresses and
    /// shipping options.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected async Task PreparePackNameShippingDataModels(IList<PackNameShoppingCartItem> packNameCart, PackNameCheckoutModel model, Customer customer)
    {
        if (!packNameCart.Any())
            return;

        var storeId = packNameCart.First().StoreId;
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        model.CanAddAddress = _shoppingCartService.PackNameCanAddAddress(storeSettings);

        //We need to add the shipping option for each packName since they can have different addresses and shipping methods.
        var packNameIds = packNameCart.Select(item => item.PackNameId).Distinct().ToList();

        var assignments = await _shippingAssignmentService.LoadAsync(customer, storeId);

        var selectedAddressIds = packNameIds
            .Select(assignments.GetAddressId)
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var addressMap = selectedAddressIds.Count > 0
            ? (await _addressRepository.Table
                .Where(a => selectedAddressIds.Contains(a.Id))
                .ToListAsync())
                .ToDictionary(a => a.Id)
            : new Dictionary<int, Address>();

        var assignmentsModified = false;

        foreach (var packNameId in packNameIds)
        {
            var (shippingModel, wasModified) = await PreparePackNameShippingModel(packNameId, customer, storeId, packNameCart, storeSettings, assignments, addressMap);
            assignmentsModified |= wasModified;
            model.ShippingOptions.Add(shippingModel);
        }

        if (assignmentsModified)
            await _shippingAssignmentService.SaveAsync(customer, storeId, assignments);

        await PrepareAvailableAddressesAsync(model, packNameCart, storeSettings);
    }

    protected async Task<(PackNameShippingModel shippingModel, bool assignmentsModified)> PreparePackNameShippingModel(int packNameId, Customer customer, int storeId, IList<PackNameShoppingCartItem> packNameCart, MultiStoreSettings storeSettings, PackNameShippingAssignments assignments, Dictionary<int, Address> addressMap)
    {
        var assignmentsModified = false;

        //Only the items for the packName
        var packNameItems = packNameCart.Where(item => item.PackNameId == packNameId).ToList();
        var cart = packNameItems.Cast<ShoppingCartItem>().ToList();
        var branchId = packNameItems.FirstOrDefault()?.BranchId ?? 0;

        if (!await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart))
        {
            return (new PackNameShippingModel
            {
                BranchId = branchId,
                PackNameId = packNameId,
                RequiresShipping = false,
            }, assignmentsModified);
        }

        var (addressId, addressWasDefaulted) = await ResolvePackNameAddressIdAsync(customer, packNameId, branchId, storeId, storeSettings, assignments);

        if (addressWasDefaulted)
            assignmentsModified = true;

        return (await PrepareShippingModelAsync(packNameId, branchId, addressId ?? 0, cart, packNameCart, storeSettings, customer, storeId, assignments, addressMap), assignmentsModified);
    }

    protected async Task PreparePackNameBillingAddressModel(PackNameCheckoutModel model, Customer customer)
    {
        var defaultAddress = await GetCustomerDefaultAddress(customer);

        if (defaultAddress != null)
        {
            var billingAddressModel = new AddressModel();
            await _addressModelFactory.PrepareAddressModelAsync(billingAddressModel,
                address: defaultAddress,
                excludeProperties: false,
                addressSettings: _addressSettings,
                loadCountries: async () => await _countryService.GetAllCountriesAsync((await _workContext.GetWorkingLanguageAsync()).Id));

            model.BillingAddress = billingAddressModel;
        }

        if (model.BillingAddress.Id == 0)
        {
            model.Warnings.Add("No billing address allocated");
        }
    }

    protected async Task<Address> GetCustomerDefaultAddress(Customer customer)
    {
        var addresses = (await _customerService.GetAddressesByCustomerIdAsync(customer.Id))
            .OrderByDescending(address => address.CreatedOnUtc)
            .ThenByDescending(address => address.Id)
            .ToList();

        //The customer's billing address of record wins whenever it is still one of their addresses.
        //That is the address PlaceOrderAsync will actually bill, so resolving anything else here means
        //the checkout screen and the placed order disagree — silently, because neither side warns.
        //Matching by email and name is the fallback for a customer who has no address of record yet.
        var defaultBillingAddress = customer.BillingAddressId.HasValue
            ? addresses.FirstOrDefault(address => address.Id == customer.BillingAddressId.Value)
            : null;

        defaultBillingAddress ??= await MatchAddressToCustomer(addresses, customer);

        if (defaultBillingAddress == null)
        {
            if (addresses.Count > 0)
            {
                var address = addresses.FirstOrDefault();
                defaultBillingAddress = address;
            }
            else
            {
                defaultBillingAddress = customer.ShippingAddressId.HasValue ? await _addressService.GetAddressByIdAsync(customer.ShippingAddressId.Value) : null;
            }
        }

        return defaultBillingAddress;
    }

    protected async Task<Address> MatchAddressToCustomer(IList<Address> addresses, Customer customer)
    {
        Address customerAddress = null;

        var matchedEmail = addresses.Where(a => string.Equals(a.Email, customer.Email, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matchedEmail.Count == 0)
            matchedEmail = addresses.ToList();
        else if (matchedEmail.Count == 1)
            customerAddress = matchedEmail.First();

        if (customerAddress == null)
        {
            var matchedFirstName = matchedEmail.Where(a => string.Equals(a.FirstName, customer.FirstName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchedFirstName.Count == 0)
                matchedFirstName = matchedEmail;
            else if (matchedFirstName.Count == 1)
                customerAddress = matchedFirstName.First();

            if (customerAddress == null)
            {
                var matchedLastName = matchedFirstName.Where(a => string.Equals(a.LastName, customer.LastName, StringComparison.OrdinalIgnoreCase)).ToList();

                if (matchedLastName.Count == 0)
                    matchedLastName = matchedFirstName;
                else if (matchedLastName.Count == 1)
                    customerAddress = matchedLastName.First();

                if (customerAddress == null)
                {
                    var matchedValid = new List<Address>();
                    foreach (var address in matchedLastName)
                    {
                        if (await IsValidAddressAsync(address))
                            matchedValid.Add(address);
                    }

                    if (matchedValid.Count == 0)
                        matchedValid = matchedLastName;
                    else if (matchedValid.Count == 1)
                        customerAddress = matchedValid.First();

                    customerAddress ??= matchedValid.OrderByDescending(address => address.CreatedOnUtc).ThenByDescending(address => address.Id).FirstOrDefault();
                }
            }
        }

        return customerAddress;
    }

    protected async Task<bool> IsValidAddressAsync(Address address)
    {
        CheckoutBillingAddressModel model = new();
        var billingAddressModel = model.BillingNewAddress;

        billingAddressModel.Id = address.Id;
        billingAddressModel.FirstName = address.FirstName;
        billingAddressModel.LastName = address.LastName;
        billingAddressModel.Email = address.Email;
        billingAddressModel.Company = address.Company;
        billingAddressModel.CountryId = address.CountryId;
        billingAddressModel.StateProvinceId = address.StateProvinceId;
        billingAddressModel.County = address.County;
        billingAddressModel.City = address.City;
        billingAddressModel.Address1 = address.Address1;
        billingAddressModel.Address2 = address.Address2;
        billingAddressModel.ZipPostalCode = address.ZipPostalCode;
        billingAddressModel.PhoneNumber = address.PhoneNumber;
        billingAddressModel.FaxNumber = address.FaxNumber;

        //nop 5.00's address rules are async (state / province), so the validator must be run async
        return (await _billingAddressValidator.ValidateAsync(model)).IsValid;
    }

    /// <summary>
    /// Prepares an address model from a pre-loaded <see cref="Address"/> entity.
    /// Avoids a DB round trip when the entity is already available in memory.
    /// </summary>
    protected async Task<AddressModel> PrepareShipAddressModelAsync(Address address)
    {
        var addressModel = new AddressModel();

        if (address == null)
            return addressModel;

        await _addressModelFactory.PrepareAddressModelAsync(addressModel,
            address: address,
            excludeProperties: false,
            addressSettings: _addressSettings,
            loadCountries: async () =>
                await _countryService.GetAllCountriesAsync((await _workContext.GetWorkingLanguageAsync()).Id));

        return addressModel;
    }

    /// <summary>
    /// Runs the subsidy preview and populates SubsidyBreakdown on every item model,
    /// and ShippingSubsidyDisplay on every box model.
    /// </summary>
    protected async Task PopulateSubsidyBreakdownAsync(IList<PackNameShoppingCartItem> packNameCart, PackNameCheckoutModel model)
    {
        var preview = await _orderTotalCalculationOverrideService.GetCartSubsidyBreakdownAsync(packNameCart);

        if (!preview.Items.Any() && !preview.ShippingCredits.Any())
            return;

        // Batch-fetch product groups for icons/colours (only non-null PG IDs)
        var pgIds = preview.Items
            .Where(x => x.ProductGroupId.HasValue)
            .Select(x => x.ProductGroupId!.Value)
            .Distinct()
            .ToList();

        var productGroups = pgIds.Any()
            ? await _subsidyService.GetProductGroupsByIdsAsync(pgIds)
            : [];

        var pgLookup = productGroups.ToDictionary(pg => pg.Id);

        // Map ShoppingCartItemId → list of breakdown rows
        // Note: FormatPriceAsync requires await, so use nested foreach instead of LINQ lambdas
        var itemBreakdowns = new Dictionary<int, List<SubsidyItemBreakdownModel>>();

        foreach (var cartItemGroup in preview.Items.GroupBy(x => x.Id)) // Id == ShoppingCartItemId
        {
            var rows = new List<SubsidyItemBreakdownModel>();

            foreach (var pgGroup in cartItemGroup.GroupBy(x => x.ProductGroupId))
            {
                var pgId = pgGroup.Key;
                var qtyUsed = pgGroup.Sum(x => x.Quantity + x.QuantityTopup);
                var creditUsed = pgId.HasValue
                    ? pgGroup.Sum(x => x.ProductGroupCredit + x.ProductGroupCreditTopup)
                    : pgGroup.Sum(x => x.Credit + x.CreditTopup);

                if (qtyUsed == 0 && creditUsed == 0)
                    continue;

                string? pgName = null, pgIcon = null, pgColour = null;
                if (pgId.HasValue && pgLookup.TryGetValue(pgId.Value, out var pg))
                {
                    pgName = pg.Name;
                    pgIcon = pg.Icon;
                    pgColour = pg.Colour;
                }

                var creditDisplay = creditUsed > 0
                    ? await _priceFormatter.FormatPriceAsync(-creditUsed, true, false)
                    : null;

                rows.Add(new SubsidyItemBreakdownModel
                {
                    ProductGroupId = pgId,
                    ProductGroupName = pgName,
                    ProductGroupIcon = pgIcon,
                    ProductGroupColour = pgColour,
                    QuantityUsed = qtyUsed,
                    CreditUsed = creditUsed,
                    CreditDisplay = creditDisplay,
                });
            }

            if (rows.Count > 0)
                itemBreakdowns[cartItemGroup.Key] = rows;
        }

        // Map PackNameId → shipping subsidy display
        // Note: FormatPriceAsync requires await, so use a foreach instead of ToDictionary
        var shippingByPackName = new Dictionary<int, string?>();
        foreach (var g in preview.ShippingCredits.GroupBy(x => x.PackNameId))
        {
            var total = g.Sum(x => x.Credit + x.CreditTopup);
            shippingByPackName[g.Key] = total > 0
                ? await _priceFormatter.FormatPriceAsync(-total, true, false)
                : null;
        }

        // Push breakdown into item view models
        foreach (var group in model.ShoppingCart.GroupedItems)
        {
            foreach (var row in group.Items)
            {
                if (itemBreakdowns.TryGetValue(row.Item.Id, out var breakdown))
                    row.Item.SubsidyBreakdown = breakdown;
            }
        }

        // Push shipping subsidy into box models (via boxCache deduplicated by PackNameId)
        foreach (var group in model.ShoppingCart.GroupedItems)
        {
            foreach (var row in group.Items)
            {
                var packNameId = row.Item.PackNameModel.Id;
                if (row.Box != null
                    && shippingByPackName.TryGetValue(packNameId, out var display)
                    && display != null
                    && row.Box.ShippingSubsidyDisplay == null) // avoid overwriting if already set
                {
                    row.Box.ShippingSubsidyDisplay = display;
                }
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepare packaging name checkout model
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name checkout model
    /// </returns>
    public virtual async Task<PackNameCheckoutModel> PreparePackNameCheckoutModelAsync(IList<PackNameShoppingCartItem> packNameCart, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(packNameCart);
        ArgumentNullException.ThrowIfNull(customer);

        var model = new PackNameCheckoutModel
        {
            DisplayCaptcha = customer != null && await _customerService.IsGuestAsync(customer) && _captchaSettings.Enabled && _captchaSettings.ShowOnCheckoutPageForGuests,
            IsReCaptchaV3 = _captchaSettings.CaptchaType == CaptchaType.ReCaptchaV3,
            ReCaptchaPublicKey = _captchaSettings.ReCaptchaPublicKey,
            IsCheckoutEnabled = !_orderSettings.CheckoutDisabled,
        };

        if (packNameCart.Count == 0)
            return model;

        model.ShoppingCart = await _shoppingCartModelFactory.PreparePackNameShoppingCartModelAsync(new PackNameShoppingCartModel(), packNameCart, loadItems: false);

        await PreparePackNameShippingDataModels(packNameCart, model, customer);
        await PreparePackNameBillingAddressModel(model, customer);

        model.HasShippingWarnings = model.ShippingOptions.Any(so => so.ShippingMethod != null && so.ShippingMethod.Warnings.Count > 0);

        return model;
    }

    /// <summary>
    /// Prepare packaging name checkout model
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name checkout model
    /// </returns>
    public virtual async Task<PackNameCheckoutModel> PreparePackNameCheckoutModelItemsAsync(IList<PackNameShoppingCartItem> packNameCart, Customer customer, string viewMode = "group")
    {
        ArgumentNullException.ThrowIfNull(packNameCart);
        ArgumentNullException.ThrowIfNull(customer);

        if (packNameCart.Count == 0)
            return new PackNameCheckoutModel();

        var model = new PackNameCheckoutModel
        {
            ShoppingCart = await _shoppingCartModelFactory.PreparePackNameShoppingCartModelAsync(new PackNameShoppingCartModel(), packNameCart, loadItems: true),
        };

        await PreparePackNameShippingDataModels(packNameCart, model, customer);
        var storeId = packNameCart.First().StoreId;
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        // Cache box models by packNameId — each packname's box is computed once
        // regardless of how many products it appears against in the by-product view.
        var boxCache = new Dictionary<int, PackNameBoxModel>();

        async Task<PackNameBoxModel> GetBoxAsync(PackNameShoppingCartItem cartItem)
        {
            if (!boxCache.TryGetValue(cartItem.PackNameId, out var box))
            {
                var shippingModel = model.ShippingOptions.FirstOrDefault(so => so.PackNameId == cartItem.PackNameId);
                box = await PreparePackNameBoxModelAsync(cartItem, packNameCart, storeSettings, shippingModel);
                boxCache[cartItem.PackNameId] = box;
            }
            return box;
        }

        var pictureSize = _mediaSettings.CartThumbPictureSize > 0
            ? _mediaSettings.CartThumbPictureSize + 20 : 150;

        // Group the view model items, keyed either by packNameId or productId
        var groups = viewMode == "list"
            ? model.ShoppingCart.Items.GroupBy(i => i.ProductId)
            : model.ShoppingCart.Items.GroupBy(i => i.PackNameModel.Id);

        foreach (var group in groups)
        {
            var groupModel = new PackNameGroupModel
            {
                Header = group.First(),
                CartThumbPictureSize = pictureSize,
            };

            foreach (var viewItem in group)
            {
                var cartItem = packNameCart.First(ci => ci.Id == viewItem.Id);
                groupModel.Items.Add(new PackNameCartItemWithBox
                {
                    Item = viewItem,
                    Box = await GetBoxAsync(cartItem),
                });
            }

            model.ShoppingCart.GroupedItems.Add(groupModel);
        }

        // Populate per-item subsidy breakdown (non-persisted dry run)
        await PopulateSubsidyBreakdownAsync(packNameCart, model);

        return model;
    }

    /// <summary>
    /// Prepare packaging name box model
    /// </summary>
    /// <param name="item">A cart line of the packname</param>
    /// <param name="packNameCart">Cart</param>
    /// <param name="storeSettings">Store settings</param>
    /// <param name="shippingModel">The packname's shipping, prepared for the whole cart at once</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name box model
    /// </returns>
    public virtual async Task<PackNameBoxModel> PreparePackNameBoxModelAsync(PackNameShoppingCartItem item, IList<PackNameShoppingCartItem> packNameCart, MultiStoreSettings storeSettings, PackNameShippingModel shippingModel)
    {
        ArgumentNullException.ThrowIfNull(packNameCart);
        ArgumentNullException.ThrowIfNull(storeSettings);

        //black or white text, whichever reads on the packname's colour
        var foreColor = PackNameColourHelper.ForegroundFor(item.Colour);

        var packName = await _packNameService.GetPackNameByIdAsync(item.PackNameId);

        var model = new PackNameBoxModel
        {
            Item = new PackNameShoppingCartModel.PackNameShoppingCartItemModel
            {
                Id = item.Id,
                PackNameModel = new PackNameModel
                {
                    Id = item.PackNameId,
                    Name = item.Name,
                    Branch = item.Branch ?? string.Empty,
                    BranchId = item.BranchId,
                    Group = item.Group,
                    IsCustomer = item.PackNameCustomerId.HasValue && item.PackNameCustomerId.Value > 0,
                    Colour = item.Colour,
                    ForeColor = foreColor,
                    POEntry = storeSettings.POEntryEnabled ? packName.POEntry : string.Empty,
                }
            },
            CanAddAddress = _shoppingCartService.PackNameCanAddAddress(storeSettings),
        };

        model.ShippingData = shippingModel ??
            new PackNameShippingModel { ShippingMethod = new CheckoutShippingMethodModel { Warnings = ["No shipping method available"] } };

        var shipAddress = await _branchService.GetDefaultShipAddressByBranchIdAsync(item.BranchId);
        model.BranchDefaultAddressId = shipAddress?.Id ?? 0;

        //Add branch addresses including the default one
        model.AvailableAddresses.AddRange(await PrepareBranchAvailableAddresses(item.BranchId, storeSettings));

        //Add packName addresses
        if (storeSettings.AllowHomeDelivery)
            model.AvailableAddresses.AddRange(await PrepareBranchPackNameAvailableAddresses(item.PackNameId, item.PackNameCustomerId ?? 0, storeSettings));

        return model;
    }

    /// <summary>
    /// Prepare packName shipping address model
    /// </summary>
    /// <param name="model">PackName shipping address model</param>
    /// <param name="packName">PackName</param>
    /// <param name="overrideAddressAttributesXml">Overridden address attributes in XML format</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName address model
    /// </returns>
    public virtual async Task<PackNameShippingAddressModel> PreparePackNameShippingAddressModelAsync(PackNameShippingAddressModel model, PackName packName, string overrideAddressAttributesXml = "")
    {
        ArgumentNullException.ThrowIfNull(packName);

        ArgumentNullException.ThrowIfNull(model);

        model.PackNameId = packName.Id;
        model.Group = packName.Group;
        model.Colour = packName.Colour;
        model.IsCustomer = packName.IsCustomer;

        //Address
        await _addressModelFactory.PrepareAddressModelAsync(model.Address, null, excludeProperties: false, addressSettings: _addressSettings,
            loadCountries: async () => await _countryService.GetAllCountriesAsync((await _workContext.GetWorkingLanguageAsync()).Id), overrideAttributesXml: overrideAddressAttributesXml);

        //Avoid to show the company field since we are going to populate it with the selected branch name
        model.Address.CompanyEnabled = false;

        return model;
    }

    /// <summary>
    /// Sets the shipping options for each pack name in the provided shopping cart, based on the available shipping methods,
    /// shipping addresses, and company settings. This method loads shipping plugins, determines valid shipping options for
    /// each pack name, caches the available and selected shipping options, and ensures that shipping rates are consistent
    /// for pack names sharing the same address and shipping method. It also handles error logging and performance optimizations
    /// by caching results for later use.
    /// </summary>
    /// <param name="customer">The customer for whom shipping options are being set.</param>
    /// <param name="storeId">The identifier of the current store.</param>
    /// <param name="packNamesCart">A list of <see cref="PackNameShoppingCartItem"/> representing the items in the cart, grouped by pack name.</param>
    /// <param name="ratePackNameId">
    /// The one packname whose lines, address or method just changed: only it is rated again, and every other packname
    /// keeps the options it was rated with. Null rates every packname.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetShippingOptionsByPackNamesAsync(Customer customer, int storeId, IList<PackNameShoppingCartItem> packNamesCart, int? ratePackNameId = null)
    {
        if (!packNamesCart.Any())
            return;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        var assignments = await _shippingAssignmentService.LoadAsync(customer, storeId);

        //We need to get the packNames individual shipping information so we can group them later by address and shipping method
        var packNamesShippingInfo = new List<PackNameShippingInformation>();
        var branchIds = packNamesCart.Select(c => c.BranchId).Distinct().ToList();

        //We need to get the shipping methods for each branch and validate the shipping method availability for each packName with only their own items (without the items of other packNames sharing the shipping address and method)
        //There could be rules that could make some shipping methods unavailable depending on the items in the cart for that packName.
        foreach (var branchId in branchIds)
        {
            var packNameIds = packNamesCart.Where(c => c.BranchId == branchId).Select(c => c.PackNameId).Distinct().ToList();
            var branchShippingMethods = await _branchService.GetShippingMethodsByBranchId(branchId); //If empty, all shipping methods are selectable

            foreach (var packNameId in packNameIds)
            {
                var packName = await _packNameService.GetPackNameByIdAsync(packNameId);

                if (packName == null)
                {
                    assignments.SetShippingOptions(packNameId, null, null);
                    continue;
                }

                var (packNameAddressId, _) = await ResolvePackNameAddressIdAsync(customer, packNameId, branchId, storeId, storeSettings, assignments);
                var packNameShippingAddress = await _addressService.GetAddressByIdAsync(packNameAddressId ?? 0);

                //What this packname's options are rated for: its address and its lines. Options rated for anything
                //else (an address that was reset, a line added in another tab) are rated again.
                var ratedFor = $"{packNameAddressId}|" + string.Join(",", packNamesCart
                    .Where(item => item.PackNameId == packNameId)
                    .OrderBy(item => item.Id)
                    .Select(item => $"{item.Id}x{item.Quantity}"));

                //A packname nothing changed for keeps the options it was rated with. Its selected option goes back
                //to the rate it has on its own; the shared-address pass below charges it with the group again.
                //A change to the shipping methods themselves, or to a line's attributes, is not seen here; the full
                //rating on every checkout load sees it. Placing the order charges the selected rates as they are stored.
                var keptOptions = assignments.GetOfferedOptions(packNameId);
                var keptSelectedName = assignments.GetSelectedOption(packNameId)?.Name;

                if (ratePackNameId.HasValue && ratePackNameId.Value != packNameId && keptOptions.Count > 0 && !string.IsNullOrEmpty(keptSelectedName)
                    && assignments.GetRatedFor(packNameId) == ratedFor)
                {
                    var keptSelected = keptOptions.FirstOrDefault(so => so.Name == keptSelectedName);

                    if (keptSelected != null)
                    {
                        assignments.SetSelectedOption(packNameId, keptSelected);

                        if (packNameShippingAddress != null)
                        {
                            packNamesShippingInfo.Add(new PackNameShippingInformation
                            {
                                PackNameId = packNameId,
                                Address = packNameShippingAddress,
                                ShippingOption = keptSelected,
                            });
                        }

                        continue;
                    }
                }

                //Only the items for the packName
                var packNameCart = packNamesCart.Where(item => item.PackNameId == packNameId).Cast<ShoppingCartItem>().ToList();

                var shippingOptionResponse = await _shippingService.GetShippingOptionsAsync(packNameCart, packNameShippingAddress, customer, storeId: storeId, allowedShippingRateComputationMethodSystemName: CWCoreDefaults.FreightPluginSystemName);
                var packNameShippingOptions = shippingOptionResponse.ShippingOptions ?? new List<ShippingOption>();

                //We need to filter the shipping options offered to only those allocated for the branch (if any).
                if (branchShippingMethods.Count != 0)
                {
                    packNameShippingOptions = packNameShippingOptions
                        .Where(shippingOption => branchShippingMethods.Any(shippingMethod => shippingMethod == shippingOption.Name))
                        .ToList();
                }

                //No shipping options loaded
                if (!packNameShippingOptions.Any())
                {
                    assignments.SetShippingOptions(packNameId, null, null);
                    continue;
                }

                // Re-use the previously selected option if it is still available; otherwise pick the first branch default
                var previouslySelectedName = assignments.GetSelectedOption(packNameId)?.Name;
                ShippingOption selectedOption = null;

                if (!string.IsNullOrEmpty(previouslySelectedName))
                {
                    selectedOption = packNameShippingOptions.FirstOrDefault(so => so.Name == previouslySelectedName);
                    if (selectedOption != null)
                        selectedOption.Rate = packNameShippingOptions.First(so => so.Name == selectedOption.Name).Rate;
                }

                if (selectedOption == null)
                {
                    if (branchShippingMethods.Count != 0)
                    {
                        foreach (var defaultMethod in branchShippingMethods)
                        {
                            selectedOption = packNameShippingOptions.FirstOrDefault(so => so.Name == defaultMethod);
                            if (selectedOption != null)
                                break;
                        }
                    }
                    else
                    {
                        selectedOption = packNameShippingOptions.FirstOrDefault();
                    }
                }

                // Update in-memory — no DB write yet
                assignments.SetShippingOptions(packNameId, packNameShippingOptions.ToList(), selectedOption, ratedFor);

                if (packNameShippingAddress != null && selectedOption != null)
                {
                    packNamesShippingInfo.Add(new PackNameShippingInformation
                    {
                        PackNameId = packNameId,
                        Address = packNameShippingAddress,
                        ShippingOption = selectedOption,
                    });
                }
            }
        }

        // Recalculate rates for packnames that share the same address + method (combined shipping cost)
        var sharedAddressGroups = packNamesShippingInfo
            .GroupBy(info => new
            {
                info.Address.Id,
                info.ShippingOption.Name,
                info.ShippingOption.ShippingRateComputationMethodSystemName,
            })
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in sharedAddressGroups)
        {
            var packNameIds = group.Select(x => x.PackNameId).ToList();
            var groupCart = packNamesCart
                .Where(item => packNameIds.Contains(item.PackNameId))
                .Cast<ShoppingCartItem>()
                .ToList();

            var response = await _shippingService.GetShippingOptionsAsync(
                groupCart, group.First().Address, customer,
                storeId: storeId,
                allowedShippingRateComputationMethodSystemName: CWCoreDefaults.FreightPluginSystemName);

            var groupOption = response.ShippingOptions
                ?.FirstOrDefault(so => so.Name == group.First().ShippingOption.Name);

            if (groupOption == null)
                continue;

            foreach (var packNameId in packNameIds)
                assignments.SetSelectedOption(packNameId, groupOption);
        }

        await _shippingAssignmentService.SaveAsync(customer, storeId, assignments);
    }

    /// <summary>
    /// Prepare shipping address model
    /// </summary>
    /// <param name="addressId">Address identifier</param>
    /// <returns></returns>
    public virtual async Task<AddressModel> PrepareShipAddressModelAsync(int addressId)
    {
        var address = await _addressService.GetAddressByIdAsync(addressId);

        var addressModel = new AddressModel();

        if (address == null)
            return addressModel;

        await _addressModelFactory.PrepareAddressModelAsync(addressModel,
            address: address,
            excludeProperties: false,
            addressSettings: _addressSettings,
            loadCountries: async () => await _countryService.GetAllCountriesAsync((await _workContext.GetWorkingLanguageAsync()).Id));

        return addressModel;
    }

    #endregion
}