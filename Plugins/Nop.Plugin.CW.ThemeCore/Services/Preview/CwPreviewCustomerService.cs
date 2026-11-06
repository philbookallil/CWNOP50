using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.ThemeCore.Services.Preview;

/// <summary>
/// <see cref="ICwPreviewCustomerService"/> implementation. One hidden system-account customer per store, seeded
/// with a dummy address + a handful of the store's visible products so live Cart/Checkout render populated.
/// </summary>
public class CwPreviewCustomerService : ICwPreviewCustomerService
{
    #region Constants

    private const int DummyCartProductCount = 3;

    //first use per store is serialised (see GetOrCreatePreviewCustomerAsync). ILocker does not wait for a held lock,
    //so a second request retries for up to LockAttempts × LockRetryDelay — long enough for a first seed to finish.
    private const int LockAttempts = 40;
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan LockExpiration = TimeSpan.FromSeconds(30);

    /// <summary>Per-store system name — how the account is found again. (The Guests role is what keeps it out of
    /// the admin customer list's default view.)</summary>
    private static string PreviewSystemName(int storeId) => $"CwThemePreview-{storeId}";

    private static string LockKey(int storeId) => $"CW.ThemeCore.PreviewCustomer.{storeId}";

    #endregion

    #region Fields

    protected readonly IAddressService _addressService;
    protected readonly ICountryService _countryService;
    protected readonly ICustomerService _customerService;
    protected readonly ILocker _locker;
    protected readonly IProductAttributeParser _productAttributeParser;
    protected readonly IProductAttributeService _productAttributeService;
    protected readonly IProductService _productService;
    protected readonly IShoppingCartService _shoppingCartService;

    #endregion

    #region Ctor

    public CwPreviewCustomerService(
        IAddressService addressService,
        ICountryService countryService,
        ICustomerService customerService,
        ILocker locker,
        IProductAttributeParser productAttributeParser,
        IProductAttributeService productAttributeService,
        IProductService productService,
        IShoppingCartService shoppingCartService)
    {
        _addressService = addressService;
        _countryService = countryService;
        _customerService = customerService;
        _locker = locker;
        _productAttributeParser = productAttributeParser;
        _productAttributeService = productAttributeService;
        _productService = productService;
        _shoppingCartService = shoppingCartService;
    }

    #endregion

    #region Utilities

    private async Task<Customer> CreatePreviewCustomerAsync(int storeId)
    {
        var now = DateTime.UtcNow;

        var customer = new Customer
        {
            CustomerGuid = Guid.NewGuid(),
            IsSystemAccount = true,                       // hidden from the human customer list (§6)
            SystemName = PreviewSystemName(storeId),
            Email = $"preview-{storeId}@cw.preview",
            Username = $"cw-preview-{storeId}",
            Active = true,
            CreatedOnUtc = now,
            LastActivityDateUtc = now,
            RegisteredInStoreId = storeId
        };
        await _customerService.InsertCustomerAsync(customer);

        //Guests role — same as nop's own system accounts (SearchEngine/BackgroundTask), so it stays out of the
        //admin customer list's default (Registered) view WITHOUT any customer-list change. A guest can be bounced
        //by checkout's guest gate (`IsGuest && !AnonymousCheckoutAllowed`); CwPreviewCheckoutFilter renders the
        //checkout anyway for preview requests, so the preview does not depend on that setting.
        var guests = await _customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.GuestsRoleName);
        if (guests != null)
            await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping { CustomerId = customer.Id, CustomerRoleId = guests.Id });

        await EnsureDummyAddressAsync(customer);

        return customer;
    }

    private async Task EnsureDummyAddressAsync(Customer customer)
    {
        if ((await _customerService.GetAddressesByCustomerIdAsync(customer.Id)).Any())
            return;

        //a country makes the checkout address steps render complete (shipping/billing); first published one is fine
        var country = (await _countryService.GetAllCountriesAsync()).FirstOrDefault();

        var address = new Address
        {
            FirstName = "Preview",
            LastName = "Customer",
            Email = customer.Email,
            Company = "CW Preview",
            CountryId = country?.Id,
            Address1 = "1 Preview Street",
            City = "Preview City",
            ZipPostalCode = "0000",
            PhoneNumber = "0000000000",
            CreatedOnUtc = DateTime.UtcNow
        };
        await _addressService.InsertAddressAsync(address);
        await _customerService.InsertCustomerAddressAsync(customer, address);

        customer.BillingAddressId = address.Id;
        customer.ShippingAddressId = address.Id;
        await _customerService.UpdateCustomerAsync(customer);
    }

    private async Task EnsureDummyCartAsync(Customer customer, int storeId)
    {
        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, storeId);
        if (cart.Any())
        {
            if (await IsCartStillValidAsync(customer, cart))
                return; // already seeded and still sound

            //a seeded product has since been unpublished, deleted, sold out or lost its attribute value — the
            //preview would show nop's cart warnings forever, so start over with products that add cleanly today
            await _shoppingCartService.ClearShoppingCartAsync(customer, storeId);
        }

        //Pull a wider set of candidates and keep only the ones that add WITHOUT warnings. A plain add (no
        //attributesXml) fails for products that require attributes (colour/size), are call-for-price, have the
        //buy button disabled, are out of stock, etc. — skip those so we reliably end up with a populated cart.
        var candidates = await _productService.SearchProductsAsync(pageIndex: 0, pageSize: 40,
            storeId: storeId, visibleIndividuallyOnly: true);

        var added = 0;
        foreach (var product in candidates)
        {
            if (added >= DummyCartProductCount)
                break;

            //this store's products require attributes (colour/size), so supply default values or the add no-ops
            var attributesXml = await BuildDefaultAttributesXmlAsync(product);
            var warnings = await _shoppingCartService.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, storeId, attributesXml: attributesXml, quantity: 1);
            if (warnings.Count == 0)
                added++;
        }
    }

    //True when every seeded line would still be accepted — the same per-item warnings AddToCartAsync applied
    //when the cart was seeded, re-run against today's catalogue.
    private async Task<bool> IsCartStillValidAsync(Customer customer, IList<ShoppingCartItem> cart)
    {
        var products = (await _productService.GetProductsByIdsAsync(cart.Select(item => item.ProductId).Distinct().ToArray()))
            .ToDictionary(product => product.Id);

        foreach (var item in cart)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                return false;

            var warnings = await _shoppingCartService.GetShoppingCartItemWarningsAsync(customer, item.ShoppingCartType,
                product, item.StoreId, item.AttributesXml, item.CustomerEnteredPrice,
                item.RentalStartDateUtc, item.RentalEndDateUtc, item.Quantity,
                addRequiredProducts: false, shoppingCartItemId: item.Id);
            if (warnings.Any())
                return false;
        }

        return true;
    }

    private async Task<Customer> GetOrCreateAndSeedAsync(int storeId)
    {
        var customer = await _customerService.GetCustomerBySystemNameAsync(PreviewSystemName(storeId))
                       ?? await CreatePreviewCustomerAsync(storeId);

        await EnsureDummyCartAsync(customer, storeId);

        return customer;
    }

    //Build an attributesXml that satisfies a product's value-based attributes with their pre-selected (or first)
    //value — enough for colour/size dropdowns/radios. Products whose required attribute has no values (free text,
    //datepicker) will still warn on add and get skipped by the caller.
    private async Task<string> BuildDefaultAttributesXmlAsync(Product product)
    {
        var xml = string.Empty;

        foreach (var mapping in await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id))
        {
            var values = await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id);
            if (!values.Any())
                continue;

            var chosen = values.FirstOrDefault(v => v.IsPreSelected) ?? values.First();
            xml = _productAttributeParser.AddProductAttribute(xml, mapping, chosen.Id.ToString());
        }

        return xml;
    }

    #endregion

    #region Methods

    public async Task<Customer> GetOrCreatePreviewCustomerAsync(int storeId)
    {
        //Serialise per store: "find, else create" and "check cart, else seed" are not atomic, so two concurrent
        //first requests (two admins, or a quick Cart → Checkout switch during a slow first seed) would each create
        //a CwThemePreview-{id} account and each seed the cart.
        Customer customer = null;
        for (var attempt = 0; attempt < LockAttempts; attempt++)
        {
            if (await _locker.PerformActionWithLockAsync(LockKey(storeId), LockExpiration,
                    async () => customer = await GetOrCreateAndSeedAsync(storeId)))
                return customer;

            await Task.Delay(LockRetryDelay);
        }

        //the lock was never released within the wait (it expires on its own) — serve the preview unlocked rather
        //than fail it; the worst case is the rare duplicate this lock exists to prevent
        return await GetOrCreateAndSeedAsync(storeId);
    }

    #endregion
}
