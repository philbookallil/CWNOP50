using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Date;
using Nop.Services.Stores;
using System.Net;

namespace Nop.Plugin.CW.Core.Services.Orders;

public class ShoppingCartOverrideService : ShoppingCartService, IShoppingCartOverrideService
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly IPackNameService _packNameService;
    protected readonly IRepository<GenericAttribute> _genericAttributeRepository;
    protected readonly IPackNameContext _packNameContext;
    protected readonly IRepository<PackNameCartItem> _packNameCartItemRepository;
    protected readonly IBranchService _branchService;
    protected readonly IRepository<PackNameShoppingCartItem> _packNameShoppingCartItemRepository;
    protected readonly IPackNameShippingAssignmentService _shippingAssignmentService;
    protected readonly ISubsidyCreditService _subsidyCreditService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IRepository<ReturnRequestItem> _returnRequestItemRepository;
    protected readonly IRepository<ReturnRequest> _returnRequestRepository;
    protected readonly ISubsidyOrderingService _subsidyOrderingService;
    protected readonly Lazy<ISubsidyAllocationService> _subsidyAllocationService;
    protected readonly Lazy<IExchangeService> _exchangeService;

    #endregion

    #region Ctor

    public ShoppingCartOverrideService(CatalogSettings catalogSettings,
    IAclService aclService,
    IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeParser,
    IAttributeService<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeService,
    ICurrencyService currencyService,
    ICustomerService customerService,
    IDateRangeService dateRangeService,
    IDateTimeHelper dateTimeHelper,
    IEventPublisher eventPublisher,
    IGenericAttributeService genericAttributeService,
    IGiftCardService giftCardService,
    IHttpContextAccessor httpContextAccessor,
    ILocalizationService localizationService,
    IPermissionService permissionService,
    IPriceCalculationService priceCalculationService,
    IPriceFormatter priceFormatter,
    IProductAttributeParser productAttributeParser,
    IProductAttributeService productAttributeService,
    IProductService productService,
    IRepository<ShoppingCartItem> sciRepository,
    IShippingService shippingService,
    IShortTermCacheManager shortTermCacheManager,
    IStaticCacheManager staticCacheManager,
    IStoreContext storeContext,
    IStoreService storeService,
    IStoreMappingService storeMappingService,
    IUrlRecordService urlRecordService,
    IWorkContext workContext,
    LinkGenerator linkGenerator,
    OrderSettings orderSettings,
    ShoppingCartSettings shoppingCartSettings,
    ISettingService settingService,
    IPackNameService packNameService,
    IRepository<GenericAttribute> genericAttributeRepository,
    IPackNameContext packNameContext,
    IRepository<PackNameCartItem> packNameCartItemRepository,
    IBranchService branchService,
    IRepository<PackNameShoppingCartItem> packShoppingCartItemRepository,
    IPackNameShippingAssignmentService shippingAssignmentService,
    ISubsidyCreditService subsidyCreditService,
    ICWFeatureService cwFeatureService,
    IRepository<ReturnRequestItem> returnRequestItemRepository,
    IRepository<ReturnRequest> returnRequestRepository,
    ISubsidyOrderingService subsidyOrderingService,
    Lazy<ISubsidyAllocationService> subsidyAllocationService,
    Lazy<IExchangeService> exchangeService) : base(catalogSettings,
        aclService,
        checkoutAttributeParser,
        checkoutAttributeService,
        currencyService,
        customerService,
        dateRangeService,
        dateTimeHelper,
        eventPublisher,
        genericAttributeService,
        giftCardService,
        httpContextAccessor,
        localizationService,
        permissionService,
        priceCalculationService,
        priceFormatter,
        productAttributeParser,
        productAttributeService,
        productService,
        sciRepository,
        shippingService,
        shortTermCacheManager,
        staticCacheManager,
        storeContext,
        storeService,
        storeMappingService,
        urlRecordService,
        workContext,
        linkGenerator,
        orderSettings,
        shoppingCartSettings)
    {
        _settingService = settingService;
        _packNameService = packNameService;
        _genericAttributeRepository = genericAttributeRepository;
        _packNameContext = packNameContext;
        _packNameCartItemRepository = packNameCartItemRepository;
        _branchService = branchService;
        _packNameShoppingCartItemRepository = packShoppingCartItemRepository;
        _shippingAssignmentService = shippingAssignmentService;
        _subsidyCreditService = subsidyCreditService;
        _cwFeatureService = cwFeatureService;
        _returnRequestItemRepository = returnRequestItemRepository;
        _returnRequestRepository = returnRequestRepository;
        _subsidyOrderingService = subsidyOrderingService;
        _subsidyAllocationService = subsidyAllocationService;
        _exchangeService = exchangeService;
    }

    #endregion

    #region Utilities

    protected async Task ClearSelectedShippingAddressesByStoreId(int storeId)
    {
        // Get all the customers associated with the store, only those can have addresses to clear
        var customers = await _packNameService.GetCustomersFromStoreIdAsync(storeId);

        foreach (var customer in customers)
        {
            var packNameIdsInCart = await GetPackNameIdsInShoppingCartAsync(customer.Id);
            var assignments = await _shippingAssignmentService.LoadAsync(customer, storeId);

            if (assignments.IsEmpty)
                continue;

            var packNamesToRemove = assignments.All.Keys
                .Where(id => !packNameIdsInCart.Contains(id))
                .ToList();

            if (!packNamesToRemove.Any())
                continue;

            foreach (var packNameId in packNamesToRemove)
                assignments.ClearPackName(packNameId);

            await _shippingAssignmentService.SaveAsync(customer, storeId, assignments);
        }
    }

    protected async Task<List<int>> GetPackNameIdsInShoppingCartAsync(int customerId)
    {
        var query = from pnci in _packNameCartItemRepository.Table
                    join sci in _sciRepository.Table on pnci.ShoppingCartItemId equals sci.Id
                    where sci.CustomerId == customerId && sci.ShoppingCartTypeId == (int)ShoppingCartType.ShoppingCart
                    select pnci.PackNameId;

        return await query.Distinct().ToListAsync();
    }

    protected async Task<IList<GenericAttribute>> GetGenericAttributesForEntityStartsWithAsync(int entityId, string keyGroup, string keyPrefix)
    {
        var query = from ga in _genericAttributeRepository.Table
                    where ga.EntityId == entityId &&
                          ga.KeyGroup == keyGroup &&
                          ga.Key.StartsWith(keyPrefix)
                    select ga;

        return await query.ToListAsync();
    }

    /// <summary>
    /// The quantity a cart item may not go below, or null when it is not part of an approved return.
    /// </summary>
    /// <remarks>
    /// A returned item is the cart-side half of an approved exchange: it has a floor but no ceiling. It may
    /// be increased — buying more of the same product is ordinary shopping, and mixed carts are expressly
    /// allowed — but never reduced, because the return was approved for an exact
    /// quantity and its fee was priced over that.
    /// <para>
    /// This lives in the service because there are two carts. CW's packname cart guards itself in
    /// PackNameCheckoutController, but nop's own ShoppingCartController.UpdateCart calls straight through
    /// to here, and its view draws both the remove control and the quantity box for a returned item —
    /// nop's model carries DisableRemoval and no nop view reads it.
    /// </para>
    /// </remarks>
    protected virtual async Task<int?> GetReturnedCartItemFloorAsync(int shoppingCartItemId)
    {
        if (shoppingCartItemId <= 0)
            return null;

        var query = from returnedItem in _returnRequestItemRepository.Table
                    join request in _returnRequestRepository.Table on returnedItem.ReturnRequestId equals request.Id
                    where returnedItem.ShoppingCartItemId == shoppingCartItemId
                    select request.Quantity;

        var quantities = await query.ToListAsync();

        //Null means "not a returned item" and zero means "a returned item whose floor we could not read".
        //They are not the same and must not collapse: the second has to fail closed.
        return quantities.Count == 0 ? null : quantities.Sum();
    }

    /// <summary>
    /// The packname a saved cart line belongs to, whose catalogue window the line is validated in.
    /// </summary>
    /// <remarks>
    /// A cart holds lines for several packnames, and each is validated again on the cart page and when the
    /// order is placed - in admin too, for an exchange. Its product has to be checked against what ITS packname
    /// may see, not the one selected right now (see PackNameCatalogScope). A line not yet saved is being added
    /// for a packname, and AddToCartAsync opens that packname's window itself.
    /// <para>
    /// The id alone is not proof of a cart row: nop validates detached items too, and an order being edited
    /// in admin is restored as items carrying the ORDER ITEM ids, which can equal some other customer's cart
    /// row. So the row must also be this customer's and hold this product.
    /// </para>
    /// </remarks>
    /// <param name="customer">Customer the line is validated for</param>
    /// <param name="product">Product on the line</param>
    /// <param name="shoppingCartItemId">Shopping cart item identifier; 0 for a line not yet saved</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packname; null when the line has none
    /// </returns>
    protected virtual async Task<PackName> GetCartLinePackNameAsync(Customer customer, Product product, int shoppingCartItemId, int storeId)
    {
        if (customer == null || product == null || shoppingCartItemId <= 0)
            return null;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, storeId))
            return null;

        //The packnames of the whole cart are read once per request: this runs for every line, several times
        //over, each time nop validates the cart. Adding a packname line drops the map.
        var cartLinePackNames = await _shortTermCacheManager.GetAsync(async () =>
            (await (from pnci in _packNameCartItemRepository.Table
                    join sci in _sciRepository.Table on pnci.ShoppingCartItemId equals sci.Id
                    where sci.CustomerId == customer.Id
                    select new { sci.Id, sci.ProductId, pnci.PackNameId })
                .ToListAsync())
            .ToDictionary(line => line.Id, line => (line.ProductId, line.PackNameId)),
            OrderServicesDefaults.CartLinePackNamesCacheKey, customer.Id);

        if (!cartLinePackNames.TryGetValue(shoppingCartItemId, out var line) || line.ProductId != product.Id)
            return null;

        return line.PackNameId > 0 ? await _packNameService.GetPackNameByIdAsync(line.PackNameId) : null;
    }

    /// <summary>
    /// The exchange type when the line being validated is an exchange replacement, or null for a purchase.
    /// </summary>
    /// <remarks>
    /// A replacement is judged differently in the line's catalogue window: the same product is always allowed,
    /// another one only if allocated, and a closed ordering period never blocks it - or a replacement approved after
    /// the period closed could never be checked out.
    /// </remarks>
    /// <param name="customerId">Identifier of the customer whose cart holds the line</param>
    /// <param name="shoppingCartItemId">Shopping cart item identifier; 0 for a line not yet saved</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the exchange type; null when the line is not a replacement
    /// </returns>
    protected virtual async Task<ExchangeType?> GetCartLineReplacementAsync(int customerId, int shoppingCartItemId, int storeId)
    {
        if (ExchangeReplacementScope.IsActive)
            return ExchangeReplacementScope.Current.ExchangeType;

        if (shoppingCartItemId <= 0 || !await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, storeId))
            return null;

        var cartReturnRequestItems = await _exchangeService.Value.GetCartReturnRequestItemsAsync(customerId);

        return cartReturnRequestItems.TryGetValue(shoppingCartItemId, out var returnRequestItem) ? returnRequestItem.ExchangeType : null;
    }

    /// <summary>
    /// Whether the row being validated is an exchange replacement — either one being written right now, or
    /// one already sitting in the cart with its link recorded.
    /// </summary>
    /// <remarks>
    /// Ordered so ordinary shopping pays almost nothing: the scope is a field read, the id test rules out
    /// every not-yet-saved row, and the feature flag is cached per request. Only a saved row on a store
    /// with exchanges switched on reaches the cart's return request items, which are read once per request.
    /// </remarks>
    protected virtual async Task<bool> IsExchangeReplacementAsync(int customerId, int shoppingCartItemId, int storeId)
    {
        if (ExchangeReplacementScope.IsActive)
            return true;

        if (shoppingCartItemId <= 0)
            return false;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, storeId))
            return false;

        return (await _exchangeService.Value.GetCartReturnRequestItemsAsync(customerId)).ContainsKey(shoppingCartItemId);
    }

    /// <summary>
    /// Ids of the customer's cart lines that belong to a packname which cannot be ordered right now.
    /// </summary>
    /// <remarks>
    /// The packname cart view defines an orderable line: it leaves out lines whose packname, list, branch or
    /// store is deleted, and lines whose packname cannot order. So a line mapped to a packname but missing
    /// from the view is hidden. Unmapped lines are nop's own and stay. Exchange replacement lines are never
    /// hidden: the exchange has to be able to find its line whatever the packname's period.
    /// </remarks>
    protected virtual async Task<IList<int>> GetHiddenPackNameCartItemIdsAsync(Customer customer)
    {
        return await _shortTermCacheManager.GetAsync(async () =>
        {
            var query = from pnci in _packNameCartItemRepository.Table
                        join sci in _sciRepository.Table on pnci.ShoppingCartItemId equals sci.Id
                        where sci.CustomerId == customer.Id
                              && sci.ShoppingCartTypeId == (int)ShoppingCartType.ShoppingCart
                              && !_packNameShoppingCartItemRepository.Table.Any(view => view.Id == sci.Id)
                              && !_returnRequestItemRepository.Table.Any(item => item.ShoppingCartItemId == sci.Id)
                        select sci.Id;

            return await query.ToListAsync();
        }, OrderServicesDefaults.HiddenPackNameCartItemIdsCacheKey, customer);
    }

    /// <summary>
    /// The units of a product a packname's cart may still take under its allocated-quantity cap, or null when
    /// no cap applies.
    /// </summary>
    /// <remarks>
    /// What the rest of the cart already takes from the product's groups is read from the subsidy allocation
    /// of the packname's cart — the same allocation the subsidy bar and order placement use — so a product
    /// that sits in several groups is counted where the allocation actually places it. It runs without a
    /// money budget, because the cap counts units, and it runs only when the rule says a cap applies at all.
    /// </remarks>
    /// <param name="customer">Customer</param>
    /// <param name="packNameId">Packname identifier</param>
    /// <param name="productId">Product identifier</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="excludedShoppingCartItemId">A line left out of the cart because its quantity is being replaced; 0 for none</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the units left, or null when no cap applies</returns>
    protected virtual async Task<int?> GetSubsidyQuantityLeftAsync(Customer customer, int packNameId, int productId, int storeId, int excludedShoppingCartItemId = 0)
    {
        //The cart's warnings ask this once per line: the credit of every packname in the cart is read together the
        //first time, so the lines after it are answered from what this request has read.
        var cartPackNameIds = (await GetPackNameShoppingCartItemsAsync(customer, 0, storeId))
            .Select(item => item.PackNameId)
            .Append(packNameId)
            .Distinct()
            .ToList();

        await _subsidyCreditService.LoadPackNameCreditsAsync(cartPackNameIds);

        var packNameCredit = await _subsidyCreditService.GetPackNameSubsidyCreditAsync(packNameId);

        //Asked first with nothing in the cart: when no cap applies there is no allocation to run.
        if (SubsidyQuantityCapHelper.GetQuantityLeft(packNameCredit, productId, new Dictionary<(int, int), int>()) == null)
            return null;

        var packNameCart = (await GetPackNameShoppingCartItemsAsync(customer, packNameId, storeId))
            .Where(item => item.Id != excludedShoppingCartItemId)
            .ToList();

        var allocation = await _subsidyAllocationService.Value.AllocateAsync(new SubsidyAllocationRequest
        {
            Customer = customer,
            StoreId = storeId,
            Cart = packNameCart,
        });

        var cartUnits = allocation.Items
            .Where(item => item.ProductGroupId.HasValue)
            .GroupBy(item => (item.SubsidyId, item.ProductGroupId.Value))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity + item.QuantityTopup));

        return SubsidyQuantityCapHelper.GetQuantityLeft(packNameCredit, productId, cartUnits);
    }

    #endregion

    #region Overrides

    /// <summary>
    /// The customer's cart as every screen should see it: nop's cart without the lines of packnames that
    /// cannot be ordered right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One definition of the cart. The header count, the flyout, the payment-method refreshes, nop's own
    /// validation at placement and every other nop caller read this method, while the packname checkout
    /// reads the packname cart view. Hiding here exactly the lines the view leaves out makes them agree.
    /// </para>
    /// <para>
    /// The rows returned are real ShoppingCartItem entities, never view rows, because nop updates and
    /// deletes what this method returns. A hidden line can still be removed: nop's update and delete load
    /// the row by id.
    /// </para>
    /// <para>
    /// nop's single-type overload forwards here, so every caller passes through this one. A null type list
    /// asks for every type (the cart, the wishlist and the stash); only shopping-cart lines are ever mapped
    /// to a packname, so a list without the shopping cart has nothing to hide. nop's vendor carts re-type lines
    /// between the cart and the stash through here too, so a hidden line is never re-typed; CW runs without
    /// vendor carts.
    /// </para>
    /// </remarks>
    public override async Task<IList<ShoppingCartItem>> GetShoppingCartAsync(Customer customer, List<int> shoppingCartTypes = null,
        int storeId = 0, int? productId = null, DateTime? createdFromUtc = null, DateTime? createdToUtc = null, int? customWishlistId = null)
    {
        var cart = await base.GetShoppingCartAsync(customer, shoppingCartTypes, storeId, productId, createdFromUtc, createdToUtc, customWishlistId);

        //Only shopping-cart lines are mapped to a packname, and an empty cart has nothing to hide.
        if (shoppingCartTypes?.Contains((int)ShoppingCartType.ShoppingCart) == false || cart.Count == 0)
            return cart;

        //With packnames switched off for the store every line is an ordinary nop line, checked out by nop, so
        //nothing packname-related may hide one.
        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, storeId > 0 ? storeId : null))
            return cart;

        var hiddenIds = await GetHiddenPackNameCartItemIdsAsync(customer);

        if (hiddenIds.Count == 0)
            return cart;

        return cart.Where(item => !hiddenIds.Contains(item.Id)).ToList();
    }

    /// <summary>
    /// nop's whole-cart checks, with the line limit counted per packname.
    /// </summary>
    /// <remarks>
    /// A packname cart is ordered as one basket per packname, and AddToCartAsync already applies
    /// MaximumShoppingCartItems to each packname's lines. nop counts the whole cart here, so a cart the add
    /// had accepted could not be checked out. Only that one message is re-asked; every other check is nop's.
    /// Lines with no packname count as a basket of their own; a cart with no packname lines keeps nop's rule.
    /// </remarks>
    public override async Task<IList<string>> GetShoppingCartWarningsAsync(IList<ShoppingCartItem> shoppingCart,
        string checkoutAttributesXml, bool validateCheckoutAttributes)
    {
        var warnings = await base.GetShoppingCartWarningsAsync(shoppingCart, checkoutAttributesXml, validateCheckoutAttributes);

        if (shoppingCart.Count <= _shoppingCartSettings.MaximumShoppingCartItems)
            return warnings;

        var itemIds = shoppingCart.Select(item => item.Id).Where(id => id > 0).ToList();

        var packNameIdByItemId = await _packNameCartItemRepository.Table
            .Where(pnci => itemIds.Contains(pnci.ShoppingCartItemId))
            .ToDictionaryAsync(pnci => pnci.ShoppingCartItemId, pnci => pnci.PackNameId);

        if (packNameIdByItemId.Count == 0)
            return warnings;

        var largestBasket = shoppingCart
            .GroupBy(item => packNameIdByItemId.TryGetValue(item.Id, out var packNameId) ? packNameId : 0)
            .Max(basket => basket.Count());

        if (largestBasket <= _shoppingCartSettings.MaximumShoppingCartItems)
        {
            var wholeCartMessage = string.Format(await _localizationService.GetResourceAsync("ShoppingCart.MaximumShoppingCartItems"),
                _shoppingCartSettings.MaximumShoppingCartItems);

            warnings.Remove(wholeCartMessage);
        }

        return warnings;
    }

    /// <summary>
    /// Add a product to shopping cart
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <param name="product">Product</param>
    /// <param name="shoppingCartType">Shopping cart type</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="attributesXml">Attributes in XML format</param>
    /// <param name="customerEnteredPrice">The price enter by a customer</param>
    /// <param name="rentalStartDate">Rental start date</param>
    /// <param name="rentalEndDate">Rental end date</param>
    /// <param name="quantity">Quantity</param>
    /// <param name="addRequiredProducts">Whether to add required products</param>
    /// <param name="wishlistId">Wishlist identifier; pass null if it's default wishlist</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the warnings
    /// </returns>
    public override async Task<IList<string>> AddToCartAsync(Customer customer, Product product,
        ShoppingCartType shoppingCartType, int storeId, string attributesXml = null,
        decimal customerEnteredPrice = decimal.Zero,
        DateTime? rentalStartDate = null, DateTime? rentalEndDate = null,
        int quantity = 1, bool addRequiredProducts = true, int? wishlistId = null)
    {
        ArgumentNullException.ThrowIfNull(customer);

        ArgumentNullException.ThrowIfNull(product);

        var currentPackName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (shoppingCartType != ShoppingCartType.ShoppingCart || currentPackName == null || currentPackName.Id == 0)
            return await base.AddToCartAsync(customer, product, shoppingCartType, storeId, attributesXml, customerEnteredPrice, rentalStartDate, rentalEndDate, quantity, addRequiredProducts, wishlistId);

        return await AddToCartAsync(customer, product, shoppingCartType, storeId, packNameId: currentPackName.Id, attributesXml, customerEnteredPrice, rentalStartDate, rentalEndDate, quantity, addRequiredProducts, wishlistId);
    }

    /// <summary>
    /// Standard warnings, with the catalogue's selling quantities not applied to an exchange replacement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A minimum order quantity, a maximum, or a fixed set such as 2, 4, 6 govern what a customer may buy.
    /// A replacement is not a purchase — the customer already owns the item and may be swapping one of a
    /// pack of two — so those three checks have to stand aside.
    /// </para>
    /// <para>
    /// The exemption belongs to the ROW, not to the moment it was written. Exempting only while the
    /// approval runs lets the authorisation succeed and then strands the customer: nop revalidates every
    /// line when the order is placed (OrderProcessingService.PrepareAndValidateShoppingCartAndCheckoutAttributesAsync,
    /// which the packname checkout inherits) and throws on the same rule, with the returned goods already
    /// gone. So the scope covers creation, when there is no row id yet, and the recorded link covers every
    /// moment after.
    /// </para>
    /// <para>
    /// Every other check still runs, and runs at a quantity that is never LOWER than the real one. That
    /// matters more than it looks: nop skips its out-of-stock validation entirely once a min/max warning
    /// has fired, so simply deleting the three messages afterwards would let a replacement through with no
    /// stock check at all. Validating at the rule-satisfying quantity instead keeps stock, call-for-price,
    /// rental and the rest honest, and can only ever be stricter than the row being written.
    /// </para>
    /// The row itself keeps the REAL quantity — this changes what is validated, never what is stored.
    /// </remarks>
    protected override async Task<IList<string>> GetStandardWarningsAsync(Customer customer, ShoppingCartType shoppingCartType, Product product,
        string attributesXml, decimal customerEnteredPrice, int quantity, int shoppingCartItemId, int storeId)
    {
        //A saved line is checked against what its own packname may see.
        //Opened here, in the method whose callees must see it - not inside an awaited helper: what an async
        //method sets on an AsyncLocal is undone when it returns.
        var linePackName = await GetCartLinePackNameAsync(customer, product, shoppingCartItemId, storeId);
        var lineReplacement = linePackName == null ? null : await GetCartLineReplacementAsync(customer.Id, shoppingCartItemId, storeId);
        using var catalogScope = linePackName == null ? null : PackNameCatalogScope.Begin(linePackName, lineReplacement);

        if (product == null || quantity <= 0 || customer == null || !await IsExchangeReplacementAsync(customer.Id, shoppingCartItemId, storeId))
            return await base.GetStandardWarningsAsync(customer, shoppingCartType, product, attributesXml,
                customerEnteredPrice, quantity, shoppingCartItemId, storeId);

        var effectiveQuantity = quantity;

        //Lift to the minimum, then snap up to the nearest allowed value at or above it. Snapping UP rather
        //than to the nearest keeps the guarantee that nothing is validated more leniently than it is added.
        if (effectiveQuantity < product.OrderMinimumQuantity)
            effectiveQuantity = product.OrderMinimumQuantity;

        var allowedQuantities = _productService.ParseAllowedQuantities(product);

        if (allowedQuantities.Length > 0 && !allowedQuantities.Contains(effectiveQuantity))
        {
            var snapped = allowedQuantities.Where(allowed => allowed >= effectiveQuantity)
                .DefaultIfEmpty(allowedQuantities.Max())
                .Min();

            effectiveQuantity = snapped;
        }

        //A maximum below the replacement quantity would still warn, and should: it is the one quantity rule
        //that cannot be satisfied by asking for more.
        if (effectiveQuantity > product.OrderMaximumQuantity)
            effectiveQuantity = product.OrderMaximumQuantity;

        return await base.GetStandardWarningsAsync(customer, shoppingCartType, product, attributesXml,
            customerEnteredPrice, effectiveQuantity, shoppingCartItemId, storeId);
    }

    /// <summary>
    /// Delete shopping cart item
    /// </summary>
    /// <param name="shoppingCartItem">Shopping cart item</param>
    /// <param name="resetCheckoutData">A value indicating whether to reset checkout data</param>
    /// <param name="ensureOnlyActiveCheckoutAttributes">A value indicating whether to ensure that only active checkout attributes are attached to the current customer</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task DeleteShoppingCartItemAsync(ShoppingCartItem shoppingCartItem, bool resetCheckoutData = true,
        bool ensureOnlyActiveCheckoutAttributes = false)
    {
        ArgumentNullException.ThrowIfNull(shoppingCartItem);

        //Validate if there is a packnameCartItem and publish the event because it doesn't get fired since is removed in the DB because it is a foreign key
        var query = from item in _packNameCartItemRepository.Table
                    where item.ShoppingCartItemId == shoppingCartItem.Id
                    select item;
        var packNameCartItem = await query.FirstOrDefaultAsync();

        await base.DeleteShoppingCartItemAsync(shoppingCartItem, resetCheckoutData, ensureOnlyActiveCheckoutAttributes);

        if (packNameCartItem != null)
            await _eventPublisher.EntityDeletedAsync(packNameCartItem);
    }

    public override async Task<IList<string>> UpdateShoppingCartItemAsync(Customer customer,
        int shoppingCartItemId, string attributesXml,
        decimal customerEnteredPrice,
        DateTime? rentalStartDate = null, DateTime? rentalEndDate = null,
        int quantity = 1, bool resetCheckoutData = true)
    {
        //Returned items first. A removal is an update to zero, so this catches that too — which is the
        //whole reason the check cannot live only where the remove button is drawn.
        var returnedItemFloor = await GetReturnedCartItemFloorAsync(shoppingCartItemId);

        if (returnedItemFloor.HasValue && (returnedItemFloor.Value <= 0 || quantity < returnedItemFloor.Value))
        {
            return new List<string>
            {
                string.Format(
                    await _localizationService.GetResourceAsync("Plugins.CW.Core.ShoppingCart.CannotChangeReturnedItem"),
                    returnedItemFloor.Value)
            };
        }

        // For packname cart items, enforce the subsidy allocated-quantity restriction
        // before delegating to the base NOP update logic.
        // Note: quantity here is the FINAL desired quantity, not an increment, so the line itself is left out
        // of what the cart already takes and the whole new quantity is compared with what is left. Only a
        // raise is refused: a line already over what is left can still be brought down.
        if (await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy) && quantity > 0)
        {
            var packNameCartItem = await _packNameCartItemRepository.Table
                .Where(x => x.ShoppingCartItemId == shoppingCartItemId)
                .FirstOrDefaultAsync();

            if (packNameCartItem != null)
            {
                var sciItem = await _sciRepository.GetByIdAsync(shoppingCartItemId, cache => default);
                if (sciItem != null && sciItem.ShoppingCartType == ShoppingCartType.ShoppingCart)
                {
                    var quantityLeft = await GetSubsidyQuantityLeftAsync(customer, packNameCartItem.PackNameId, sciItem.ProductId, sciItem.StoreId, sciItem.Id);

                    if (quantityLeft.HasValue && quantity > sciItem.Quantity && quantity > quantityLeft.Value)
                    {
                        return new List<string>
                        {
                            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Subsidy.ShoppingCart.SubsidyQtyExceeded"), quantityLeft.Value)
                        };
                    }
                }
            }
        }

        return await base.UpdateShoppingCartItemAsync(customer, shoppingCartItemId, attributesXml,
            customerEnteredPrice, rentalStartDate, rentalEndDate, quantity, resetCheckoutData);
    }
    #endregion

    #region Methods

    /// <summary>
    /// Gets the shopping cart items for a specific packName and store by customer. 
    /// </summary>
    /// <param name="customer">The customer for whom to retrieve shopping cart items.</param>
    /// <param name="packNameId">The ID of the packName for which to retrieve shopping cart items.</param>
    /// <param name="storeId">The ID of the store for which to retrieve shopping cart items.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IList<ShoppingCartItem>> GetPackNameCartAsync(Customer customer, int packNameId, int storeId = 0)
    {
        return await _shortTermCacheManager.GetAsync(async () =>
        {
            var query =
                from pnci in _packNameCartItemRepository.Table
                join sci in _sciRepository.Table on pnci.ShoppingCartItemId equals sci.Id
                where sci.CustomerId == customer.Id
                      && pnci.PackNameId == packNameId
                      && sci.ShoppingCartTypeId == (int)ShoppingCartType.ShoppingCart
                      && (storeId == 0 || sci.StoreId == storeId)
                select sci;

            return await query.ToListAsync();
        }, OrderServicesDefaults.PackNameCartCacheKey, customer, storeId, packNameId);
    }

    /// <summary>
    /// Gets the shopping cart items for a specific packName and store by customer. 
    /// </summary>
    /// <param name="customer">The customer for whom to retrieve shopping cart items.</param>
    /// <param name="packNameId">The ID of the packName for which to retrieve shopping cart items.</param>
    /// <param name="storeId">The ID of the store for which to retrieve shopping cart items.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IList<PackNameShoppingCartItem>> GetPackNameShoppingCartItemsAsync(Customer customer, int packNameId = 0, int storeId = 0)
    {
        if (customer == null)
            return new List<PackNameShoppingCartItem>();

        return await _shortTermCacheManager.GetAsync(async () =>
        {
            var query =
                from pnci in _packNameShoppingCartItemRepository.Table
                where pnci.CustomerId == customer.Id
                      && (packNameId == 0 || pnci.PackNameId == packNameId)
                      && pnci.ShoppingCartTypeId == (int)ShoppingCartType.ShoppingCart
                      && (storeId == 0 || pnci.StoreId == storeId)
                select pnci;

            return await query.ToListAsync();
        }, OrderServicesDefaults.PackNameShoppingCartItemsCacheKey, customer, storeId, packNameId);
    }

    /// <summary>
    /// Add a product to shopping cart for a packName
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <param name="product">Product</param>
    /// <param name="shoppingCartType">Shopping cart type</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="packNameId">PackName identifier</param>
    /// <param name="attributesXml">Attributes in XML format</param>
    /// <param name="customerEnteredPrice">The price enter by a customer</param>
    /// <param name="rentalStartDate">Rental start date</param>
    /// <param name="rentalEndDate">Rental end date</param>
    /// <param name="quantity">Quantity</param>
    /// <param name="addRequiredProducts">Whether to add required products</param>
    /// <param name="wishlistId">Wishlist identifier; pass null if it's default wishlist</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the warnings
    /// </returns>
    public virtual async Task<IList<string>> AddToCartAsync(Customer customer, Product product,
        ShoppingCartType shoppingCartType, int storeId, int packNameId, string attributesXml = null,
        decimal customerEnteredPrice = decimal.Zero,
        DateTime? rentalStartDate = null, DateTime? rentalEndDate = null,
        int quantity = 1, bool addRequiredProducts = true, int? wishlistId = null)
    {
        ArgumentNullException.ThrowIfNull(customer);

        ArgumentNullException.ThrowIfNull(product);

        var warnings = new List<string>();

        var packName = await _packNameService.GetPackNameByIdAsync(packNameId);

        //Never an empty list for a line that was not added: every caller reads "no warnings" as success.
        if (packName == null || !packName.Active || packName.Deleted)
        {
            warnings.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackName.NotAvailableForOrdering"));
            return warnings;
        }

        //The line is for this packname, which need not be the one selected - a manager re-ordering for someone
        //else, an exchange approved in admin - so its product is checked against what this packname may see.
        using var catalogScope = PackNameCatalogScope.Begin(packName, ExchangeReplacementScope.Current?.ExchangeType);

        //A packname whose subsidy ordering period has closed cannot order. An exchange replacement is not an
        //order - the customer already owns the goods - so an exchange approved after the period closed still
        //writes its line.
        if (shoppingCartType == ShoppingCartType.ShoppingCart
            && !ExchangeReplacementScope.IsActive
            && !await _subsidyOrderingService.PackNameCanOrderAsync(packName.Id))
        {
            //cart warnings are shown as markup, so the packname's name (typed by the customer) is encoded
            warnings.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackName.CannotOrder"), WebUtility.HtmlEncode(packName.Name)));
            return warnings;
        }

        //The cart view needs the packname's list to sit on a live branch, so a line for a packname without one
        //would be written and then hidden everywhere - a "success" that adds nothing the customer can see.
        if (shoppingCartType == ShoppingCartType.ShoppingCart
            && !ExchangeReplacementScope.IsActive
            && await _packNameService.GetBranchByPackNameIdAsync(packName.Id) == null)
        {
            warnings.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackName.NotAvailableForOrdering"));
            return warnings;
        }

        if (shoppingCartType == ShoppingCartType.ShoppingCart && !await _permissionService.AuthorizeAsync(StandardPermission.PublicStore.ENABLE_SHOPPING_CART, customer))
        {
            warnings.Add("Shopping cart is disabled");
            return warnings;
        }

        if (shoppingCartType == ShoppingCartType.Wishlist && !await _permissionService.AuthorizeAsync(StandardPermission.PublicStore.ENABLE_WISHLIST, customer))
        {
            warnings.Add("Wishlist is disabled");
            return warnings;
        }

        if (customer.IsSearchEngineAccount())
        {
            warnings.Add("Search engine can't add to cart");
            return warnings;
        }

        if (quantity <= 0)
        {
            warnings.Add(await _localizationService.GetResourceAsync("ShoppingCart.QuantityShouldPositive"));
            return warnings;
        }

        //reset checkout info
        await ResetCheckoutDataAsync(customer, storeId);
        if (_shoppingCartSettings.VendorEnabled)
            await SetShoppingCartVendorAsync(customer, null, storeId);

        //Only items for the packName being added/updated should be considered when checking if the cart already has the product, and when calculating required products, warnings, etc.
        var cart = await GetPackNameCartAsync(customer, packNameId, storeId);

        var shoppingCartItem = await FindShoppingCartItemInTheCartAsync(cart, shoppingCartType, product, attributesXml, customerEnteredPrice, rentalStartDate, rentalEndDate);

        //The allocated-quantity cap. The units already in this packname's cart, this product's line included,
        //are counted in what is left, so only the quantity being added is compared with it. An exchange
        //replacement is not a purchase: the returned units were given back to the allocation, and refusing
        //the replacement would roll the whole authorisation back.
        if (shoppingCartType == ShoppingCartType.ShoppingCart
            && !ExchangeReplacementScope.IsActive
            && await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, storeId))
        {
            var quantityLeft = await GetSubsidyQuantityLeftAsync(customer, packNameId, product.Id, storeId);

            if (quantityLeft.HasValue && quantity > quantityLeft.Value)
            {
                warnings.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Subsidy.ShoppingCart.SubsidyQtyExceeded"), quantityLeft.Value));
                return warnings;
            }
        }

        if (shoppingCartItem != null)
        {
            //update existing shopping cart item
            var newQuantity = shoppingCartItem.Quantity + quantity;

            await addRequiredProductsToCartAsync(newQuantity, wishlistId);

            if (warnings.Any())
                return warnings;

            warnings.AddRange(await GetShoppingCartItemWarningsAsync(customer, shoppingCartType, product,
                storeId, attributesXml,
                customerEnteredPrice, rentalStartDate, rentalEndDate,
                newQuantity, addRequiredProducts, shoppingCartItem.Id));

            if (warnings.Any())
                return warnings;

            shoppingCartItem.AttributesXml = attributesXml;
            shoppingCartItem.Quantity = newQuantity;
            shoppingCartItem.UpdatedOnUtc = DateTime.UtcNow;

            await _sciRepository.UpdateAsync(shoppingCartItem);

            return warnings;
        }

        //A new line for this packname. This overload writes it itself, so the row and its packname link are
        //written together. nop's AddToCartAsync would search the customer's WHOLE cart and could merge the
        //quantity into another packname's line, and the new row then had to be guessed by timestamp.
        warnings.AddRange(await GetShoppingCartItemWarningsAsync(customer, shoppingCartType, product,
            storeId, attributesXml, customerEnteredPrice,
            rentalStartDate, rentalEndDate,
            quantity, addRequiredProducts));

        if (warnings.Any())
            return warnings;

        await addRequiredProductsToCartAsync(wishlistId: wishlistId);

        if (warnings.Any())
            return warnings;

        //The line limit applies per packname: each packname's cart is ordered as its own basket.
        if (cart.Count >= _shoppingCartSettings.MaximumShoppingCartItems)
        {
            warnings.Add(string.Format(await _localizationService.GetResourceAsync("ShoppingCart.MaximumShoppingCartItems"), _shoppingCartSettings.MaximumShoppingCartItems));
            return warnings;
        }

        var now = DateTime.UtcNow;

        var newItem = new ShoppingCartItem
        {
            ShoppingCartType = shoppingCartType,
            StoreId = storeId,
            ProductId = product.Id,
            AttributesXml = attributesXml,
            CustomerEnteredPrice = customerEnteredPrice,
            Quantity = quantity,
            RentalStartDateUtc = rentalStartDate,
            RentalEndDateUtc = rentalEndDate,
            CreatedOnUtc = now,
            UpdatedOnUtc = now,
            CustomerId = customer.Id
        };

        await _sciRepository.InsertAsync(newItem);

        await _packNameCartItemRepository.InsertAsync(new PackNameCartItem
        {
            ShoppingCartItemId = newItem.Id,
            PackNameId = packName.Id
        });
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.CartLinePackNamesPrefix);

        if (!customer.HasShoppingCartItems)
        {
            customer.HasShoppingCartItems = true;
            await _customerService.UpdateCustomerAsync(customer);
        }

        return warnings;

        async Task addRequiredProductsToCartAsync(int qty = 0, int? wishlistId = null)
        {
            if (!product.RequireOtherProducts)
                return;

            //get these required products
            var requiredProducts = await _productService.GetProductsByIdsAsync(_productService.ParseRequiredProductIds(product));
            if (!requiredProducts.Any())
                return;

            var finalRequiredProducts = requiredProducts.GroupBy(p => p.Id)
                .Select(g => new { Product = g.First(), Count = g.Count() });

            foreach (var requiredProduct in finalRequiredProducts)
            {
                var productsRequiringRequiredProduct = await GetProductsRequiringProductAsync(cart, requiredProduct.Product);

                //get the required quantity of the required product
                var requiredProductRequiredQuantity = (qty > 0 ? qty : quantity) +
                                                      cart.Where(ci => productsRequiringRequiredProduct.Any(p => p.Id == ci.ProductId))
                                                          .Where(item => item.Id != (shoppingCartItem?.Id ?? 0))
                                                          .Sum(item => item.Quantity);

                //whether required product is already in the cart in the required quantity
                var quantityToAdd = requiredProductRequiredQuantity * requiredProduct.Count - (cart.FirstOrDefault(item => item.ProductId == requiredProduct.Product.Id)?.Quantity ?? 0);
                if (quantityToAdd <= 0)
                    continue;

                if (addRequiredProducts && product.AutomaticallyAddRequiredProducts)
                {
                    //Into the SAME packname: without packNameId this went to whichever packname was the
                    //context, which is not the target when a manager re-orders for someone else.
                    var addToCartWarnings = await AddToCartAsync(customer, requiredProduct.Product, shoppingCartType, storeId,
                        packNameId: packNameId,
                        quantity: quantityToAdd, addRequiredProducts: requiredProduct.Product.AutomaticallyAddRequiredProducts, wishlistId: wishlistId);

                    if (addToCartWarnings.Any())
                    {
                        warnings.AddRange(addToCartWarnings);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Clears the selected shipping addresses for the specified store.
    /// </summary>
    /// <remarks>This method removes all selected shipping addresses associated with the specified store. 
    /// If the storeId is zero, it clears selected shipping addresses for all stores that are not overriding the settings. 
    /// This is useful when you want to reset the selected shipping addresses for a specific store or for all stores in the case of a global setting change. 
    /// Note that this method does not delete the shipping addresses themselves, but rather disassociates them from being pre-selected for checkout for the specified store or stores.
    /// store.</remarks>
    /// <param name="storeId">The unique identifier of the store.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task ClearSelectedShippingAddressesAsync(int storeId)
    {
        //If the storeId is zero, we need to clear selected addresses for all stores that are not overridding the settings
        if (storeId == 0)
        {
            var stores = await _storeService.GetAllStoresAsync();

            foreach (var store in stores)
            {
                var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

                if (!await _settingService.SettingExistsAsync(storeSettings, x => x.DoNotPreSelectShippingAddress, store.Id))
                {
                    await ClearSelectedShippingAddressesByStoreId(store.Id);
                }
            }
        }
        else
        {
            await ClearSelectedShippingAddressesByStoreId(storeId);
        }
    }

    /// <summary>
    /// Determines whether the specified address is valid for checkout based on the provided cardCode and packName
    /// </summary>
    /// <remarks>Read-only: nothing is cleared or saved here. A caller that gets <see langword="false"/> either
    /// refuses the address or picks another one.</remarks>
    /// <param name="branchId">The identifier of the branch associated with the address.</param>
    /// <param name="packNameId">The identifier of the pack name associated with the address.</param>
    /// <param name="addressId">The identifier of the address to validate.</param>
    /// <param name="customer">The customer for whom the address is being validated.</param>
    /// <param name="storeId">The identifier of the store in which the validation is being performed.</param>
    /// <param name="storeSettings">The store settings that define the rules for address selection.</param>
    /// <returns><see langword="true"/> if the address is valid for checkout; otherwise, <see langword="false"/>.</returns>
    public virtual async Task<bool> IsAddressValidForCheckoutAsync(int branchId, int packNameId, int addressId, Customer customer, int storeId, MultiStoreSettings storeSettings)
    {
        var validAddress = true;

        // Check if the address is a shipping address from the branch
        var branchAddress = await _branchService.GetBranchAddressAsync(branchId, addressId);
        validAddress = branchAddress != null;

        // If not a valid branch shipping address and branch addresses are not enforced, check packname addresses
        if (!validAddress && storeSettings.AllowHomeDelivery)
        {
            var packNameAddress = await _packNameService.GetCustomerPackNameAddressAsync(packNameId, addressId);
            validAddress = packNameAddress != null;
        }

        return validAddress;
    }

    /// <summary>
    /// Determines whether a packName can add a new address based on the specified store settings.
    /// </summary>
    /// <param name="storeSettings">The store settings that define the rules for adding a new address.</param>
    /// <returns><see langword="true"/> if address addition is allowed based on the store settings;  otherwise, <see
    /// langword="false"/>.</returns>
    public virtual bool PackNameCanAddAddress(MultiStoreSettings storeSettings)
    {
        return storeSettings.AllowHomeDelivery && storeSettings.AllowCustomersToAddAddress;
    }

    /// <summary>
    /// Determines whether a packname has at least one address its orders can be shipped to.
    /// </summary>
    /// <remarks>
    /// The same sources the packname checkout offers and IsAddressValidForCheckoutAsync accepts: the addresses
    /// of the packname's branch and, with home delivery on, the packname's own addresses and its customer's.
    /// When there is none, the checkout has nothing to select and placing the order is refused.
    /// </remarks>
    /// <param name="branchId">The identifier of the branch of the packname's list.</param>
    /// <param name="packNameId">The identifier of the packname.</param>
    /// <param name="packNameCustomerId">The identifier of the packname's customer, or 0 when it has none.</param>
    /// <param name="storeSettings">The store settings that define the rules for address selection.</param>
    /// <returns><see langword="true"/> if at least one address can be used; otherwise, <see langword="false"/>.</returns>
    public virtual async Task<bool> PackNameHasShippingAddressAsync(int branchId, int packNameId, int packNameCustomerId, MultiStoreSettings storeSettings)
    {
        ArgumentNullException.ThrowIfNull(storeSettings);

        if (branchId > 0 && (await _branchService.GetAddressesByBranchIdAsync(branchId)).Any())
            return true;

        if (!storeSettings.AllowHomeDelivery)
            return false;

        if ((await _packNameService.GetAddressesByPackNameIdAsync(packNameId)).Any())
            return true;

        return packNameCustomerId > 0 && (await _customerService.GetAddressesByCustomerIdAsync(packNameCustomerId)).Any();
    }

    /// <summary>
    /// Removes every cart line that belongs to a packname, for every customer, and clears that packname's
    /// shipping assignment for each of them.
    /// </summary>
    /// <remarks>
    /// Called when a packname is deleted. Packnames are soft-deleted, so nothing in the database removes
    /// their lines, and the lines would stay in the cart with no screen able to show or place them.
    /// Exchange replacement lines are left alone: removing one would orphan its return request, and the
    /// exchange owns that line.
    /// </remarks>
    /// <param name="packNameId">Packname identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task DeletePackNameCartItemsAsync(int packNameId)
    {
        if (packNameId <= 0)
            return;

        var query = from pnci in _packNameCartItemRepository.Table
                    join sci in _sciRepository.Table on pnci.ShoppingCartItemId equals sci.Id
                    where pnci.PackNameId == packNameId
                          && !_returnRequestItemRepository.Table.Any(item => item.ShoppingCartItemId == sci.Id)
                    select sci;

        var cartItems = await query.ToListAsync();

        foreach (var customerLines in cartItems.GroupBy(sci => sci.CustomerId))
        {
            foreach (var cartItem in customerLines)
                await DeleteShoppingCartItemAsync(cartItem, resetCheckoutData: false);

            var customer = await _customerService.GetCustomerByIdAsync(customerLines.Key);

            if (customer == null)
                continue;

            foreach (var storeId in customerLines.Select(sci => sci.StoreId).Distinct())
            {
                var assignments = await _shippingAssignmentService.LoadAsync(customer, storeId);

                if (assignments.IsEmpty)
                    continue;

                assignments.ClearPackName(packNameId);
                await _shippingAssignmentService.SaveAsync(customer, storeId, assignments);
            }
        }
    }

    public virtual async Task<IList<string>> GetSubsidyQuantityCapWarningsAsync(Customer customer, IList<PackNameShoppingCartItem> packNameCart)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(packNameCart);

        var warnings = new List<string>();

        if (packNameCart.Count == 0 || !await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, packNameCart[0].StoreId))
            return warnings;

        foreach (var item in packNameCart)
        {
            //An exchange replacement is not a purchase: the returned units went back to the allocation.
            if (await IsExchangeReplacementAsync(customer.Id, item.Id, item.StoreId))
                continue;

            //The line's own units are left out of the cart that is counted, so "left" is what this line may take.
            var quantityLeft = await GetSubsidyQuantityLeftAsync(customer, item.PackNameId, item.ProductId, item.StoreId, item.Id);

            if (!quantityLeft.HasValue || item.Quantity <= quantityLeft.Value)
                continue;

            var product = await _productService.GetProductByIdAsync(item.ProductId);

            warnings.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Order.SubsidyQtyExceeded"),
                product?.Name ?? item.ProductId.ToString(), quantityLeft.Value));
        }

        return warnings;
    }

    #endregion
}