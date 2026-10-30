using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.BulkOrder;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Shipping.Date;
using Nop.Services.Tax;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

/// <summary>
/// Mirrors ShoppingCartService.GetUnitPriceAsync (:1440-1531) exactly, but resolves each
/// DISTINCT price input once for the whole product instead of once per cell.
/// </summary>
public class BulkOrderPricingService : IBulkOrderPricingService
{
    #region Fields

    protected readonly IBulkOrderAvailabilityProvider _availabilityProvider;
    protected readonly IDateRangeService _dateRangeService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPriceCalculationService _priceCalculationService;
    protected readonly IProductAttributeService _productAttributeService;
    protected readonly IProductService _productService;
    protected readonly IShoppingCartService _shoppingCartService;
    protected readonly ITaxService _taxService;
    protected readonly IWorkContext _workContext;
    protected readonly OrderSettings _orderSettings;
    protected readonly ShoppingCartSettings _shoppingCartSettings;

    #endregion

    #region Ctor

    public BulkOrderPricingService(IBulkOrderAvailabilityProvider availabilityProvider,
        IDateRangeService dateRangeService,
        ILocalizationService localizationService,
        IPriceCalculationService priceCalculationService,
        IProductAttributeService productAttributeService,
        IProductService productService,
        IShoppingCartService shoppingCartService,
        ITaxService taxService,
        IWorkContext workContext,
        OrderSettings orderSettings,
        ShoppingCartSettings shoppingCartSettings)
    {
        _availabilityProvider = availabilityProvider;
        _dateRangeService = dateRangeService;
        _localizationService = localizationService;
        _priceCalculationService = priceCalculationService;
        _productAttributeService = productAttributeService;
        _productService = productService;
        _shoppingCartService = shoppingCartService;
        _taxService = taxService;
        _workContext = workContext;
        _orderSettings = orderSettings;
        _shoppingCartSettings = shoppingCartSettings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// nop's stock wording, mirrored rather than called. ProductService.FormatStockMessageAsync (:1537) takes
    /// an attributes-XML string and resolves the combination through FindProductAttributeCombinationAsync,
    /// which parses EVERY combination's XML (:233) - per cell that is prohibitive. We already
    /// know the combination, so only the wording is borrowed (:226-300, :329-384).
    /// </summary>
    protected virtual async Task<string> BuildStockMessageAsync(Product product, int quantity, bool allowOutOfStock)
    {
        if (!product.DisplayStockAvailability)
            return string.Empty;

        if (quantity > 0)
        {
            var lowStock = product.MinStockQuantity >= quantity && product.LowStockActivity == LowStockActivity.Nothing;

            return product.DisplayStockQuantity
                ? string.Format(await _localizationService.GetResourceAsync(lowStock
                    ? "Products.Availability.LowStockWithQuantity"
                    : "Products.Availability.InStockWithQuantity"), quantity)
                : await _localizationService.GetResourceAsync(lowStock
                    ? "Products.Availability.LowStock"
                    : "Products.Availability.InStock");
        }

        //out of stock. The attribute path reads combination.AllowOutOfStockOrders (:263); the product path
        //reads BackorderMode (:363). Both collapse to "orders are allowed anyway", so one flag serves both.
        if (allowOutOfStock)
            return await _localizationService.GetResourceAsync("Products.Availability.InStock");

        var range = await _dateRangeService.GetProductAvailabilityRangeByIdAsync(product.ProductAvailabilityRangeId);

        if (product.ManageInventoryMethod == ManageInventoryMethod.ManageStock
            && product.BackorderMode == BackorderMode.AllowQtyBelow0AndNotifyCustomer)
            return range == null
                ? await _localizationService.GetResourceAsync("Products.Availability.Backordering")
                : string.Format(await _localizationService.GetResourceAsync("Products.Availability.BackorderingWithDate"),
                    await _localizationService.GetLocalizedAsync(range, x => x.Name));

        return range == null
            ? await _localizationService.GetResourceAsync("Products.Availability.OutOfStock")
            : string.Format(await _localizationService.GetResourceAsync("Products.Availability.AvailabilityRange"),
                await _localizationService.GetLocalizedAsync(range, x => x.Name));
    }

    /// <summary>
    /// What a bundle value's ASSOCIATED product allows, resolved once per value. nop validates that
    /// product's own rules for the line - quantity x value.Quantity against ITS stock
    /// (ShoppingCartService.cs:977-1000, through GetShoppingCartItemWarningsAsync) - and the parent's stock
    /// says nothing about it.
    /// </summary>
    /// <returns>Null when the associated product imposes no limit; otherwise the cap, 0 meaning blocked.</returns>
    protected virtual async Task<BulkOrderBundleLimit?> GetBundleLimitAsync(ProductAttributeValue value)
    {
        var associated = await _productService.GetProductByIdAsync(value.AssociatedProductId);

        //GetStandardWarningsAsync on the associated product: gone, unpublished, not simple or buy-disabled
        //refuses the line (:340-368), whatever the parent says
        if (associated == null || associated.Deleted || !associated.Published
            || associated.ProductType != ProductType.SimpleProduct || associated.DisableBuyButton)
            return new BulkOrderBundleLimit(0, await _localizationService.GetResourceAsync("Plugins.CW.Core.BulkOrder.Grid.Unavailable"));

        //only ManageStock with no backorders caps a quantity (:430-435); everything else takes any
        if (associated.ManageInventoryMethod != ManageInventoryMethod.ManageStock
            || associated.BackorderMode != BackorderMode.NoBackorders)
            return null;

        var stock = (await _availabilityProvider.GetAsync(associated, [])).ProductStockQuantity;
        var units = Math.Max(stock, 0) / Math.Max(value.Quantity, 1);

        return new BulkOrderBundleLimit(units, units > 0 ? string.Empty : await BuildStockMessageAsync(associated, 0, false));
    }

    #endregion

    #region Methods

    /// <summary>
    /// Resolves every price and stock figure the grid needs, in as few service calls as the data allows.
    /// </summary>
    /// <param name="tierQuantity">
    /// The quantity nop would price this product at (ShoppingCartService.cs:1490-1497): when
    /// GroupTierPricesForDistinctShoppingCartItems is on it is how many of THIS PRODUCT the cart holds across
    /// all its lines, else 1. Passed in rather than read here because the caller is looping products and this
    /// is one query each.
    /// NOT BulkOrderModelFactory.GetCartQuantitiesAsync's number. That one is packname-scoped, because the
    /// "N in cart" badge should show the packname the customer is looking at. nop's tier lookup is not
    /// packname-aware and counts the whole cart, so reusing the badge's number would price some carts at the
    /// wrong tier - cheaper or dearer than the checkout will actually charge.
    /// </param>
    public virtual async Task<BulkOrderPricingContext> PrepareAsync(Product product,
        BulkOrderGrid grid,
        Customer customer,
        Store store,
        bool displayPrices,
        int tierQuantity)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(grid);

        var combinations = await _productAttributeService.GetAllProductAttributeCombinationsAsync(product.Id);
        var availability = await _availabilityProvider.GetAsync(product, combinations);

        var overriddenPrices = combinations
            .Where(combination => combination.OverriddenPrice.HasValue)
            .ToDictionary(combination => combination.Id, combination => combination.OverriddenPrice.Value);

        //the product-wide clause of the refusal rule, evaluated once. Only !Published is left here:
        //Grouped and DisableBuyButton are unsupported-product refusals (UnsupportedReasonFor), so a
        //grid reaching this method is on a product nop would sell.
        var productDisabled = !product.Published;

        //ShoppingCartService.cs:380-384, the same test BulkOrderModelFactory.PreparePriceAsync applies to
        //the product line: an impersonating admin may buy a call-for-price product when the store allows it,
        //and a card whose header prices the product while every cell refuses it is two answers to one question
        var callForPrice = product.CallForPrice
                           && (!_orderSettings.AllowAdminsToBuyCallForPriceProducts
                               || _workContext.OriginalCustomerIfImpersonated == null);

        //the combinable mappings and their values, read once for the prices AND the bundle limits. Both
        //accessors are IStaticCacheManager-backed, so this is a cache read either way.
        var valuesByMapping = new List<(ProductAttributeMapping Mapping, IList<ProductAttributeValue> Values)>();

        foreach (var mapping in await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id))
        {
            if (mapping.IsNonCombinable())
                continue;

            valuesByMapping.Add((mapping, await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id)));
        }

        //---- prices ------------------------------------------------------------------------------------
        var valueAdjustments = new Dictionary<int, decimal>();
        var prices = new Dictionary<(decimal? Overridden, decimal Charge), decimal>();

        var showPrices = displayPrices && !callForPrice && !productDisabled && !product.CustomerEntersPrice;

        if (showPrices)
        {
            //ONE adjustment per VALUE. GetProductAttributeValuePriceAdjustmentAsync depends on the value, the
            //product price and the quantity - never on the other selected values, so a cell's
            //charge is the SUM of its values' adjustments and needs no call of its own.
            //nop passes the LINE quantity here, not the tier-grouped one (ShoppingCartService.cs:1477 against
            //:1494). A grid cell has no typed quantity, so the line quantity is 1.
            foreach (var (_, values) in valuesByMapping)
                foreach (var value in values)
                    valueAdjustments[value.Id] = await _priceCalculationService
                        .GetProductAttributeValuePriceAdjustmentAsync(product, value, customer, store, null, 1);

            //every distinct price input the grid actually uses, and nothing else
            var inputs = new HashSet<(decimal? Overridden, decimal Charge)>();

            foreach (var row in grid.Rows)
                foreach (var cell in row.Cells)
                {
                    if (!cell.Exists)
                        continue;

                    var overridden = overriddenPrices.TryGetValue(cell.ProductAttributeCombinationId, out var value)
                        ? value
                        : (decimal?)null;

                    //MUTUALLY EXCLUSIVE (ShoppingCartService.cs:1460-1467): an overridden price ignores every
                    //value adjustment. Computing the charge anyway and passing both would silently double-count.
                    var charge = decimal.Zero;

                    if (!overridden.HasValue)
                        foreach (var part in cell.Selection)
                            foreach (var valueId in part.ValueIds)
                                if (valueAdjustments.TryGetValue(valueId, out var adjustment))
                                    charge += adjustment;

                    inputs.Add((overridden, charge));
                }

            //a product with no axes has one cell and no rows - price it at the product price
            if (grid.Rows.Count == 0)
                inputs.Add((null, decimal.Zero));

            foreach (var (overridden, charge) in inputs)
            {
                var (_, finalPrice, _, _) = await _priceCalculationService.GetFinalPriceAsync(product,
                    customer, store, overridden, charge, true, tierQuantity, null, null);

                //rounding LAST, as nop does (ShoppingCartService.cs:1526-1528)
                if (_shoppingCartSettings.RoundPricesDuringCalculation)
                    finalPrice = await _priceCalculationService.RoundPriceAsync(finalPrice);

                var (taxed, _) = await _taxService.GetProductPriceAsync(product, finalPrice, customer);

                prices[(overridden, charge)] = taxed;
            }
        }

        //---- bundles -------------------------------------------------------------------------------------
        //Resolved for every AssociatedToProduct value the product has, once each. Zero on the measured
        //catalogue, two on the dev database, and a cell-gating rule either way.
        var bundleLimits = new Dictionary<int, BulkOrderBundleLimit>();

        foreach (var (_, values) in valuesByMapping)
            foreach (var value in values)
            {
                if (value.AttributeValueType != AttributeValueType.AssociatedToProduct)
                    continue;

                var limit = await GetBundleLimitAsync(value);

                if (limit.HasValue)
                    bundleLimits[value.Id] = limit.Value;
            }

        //---- stock messages ----------------------------------------------------------------------------
        //One message per DISTINCT stock situation, not per cell. A 400-cell product usually has two or three.
        var stockMessages = new Dictionary<(int Quantity, bool AllowOutOfStock), string>();
        var situations = new HashSet<(int Quantity, bool AllowOutOfStock)>();

        if (product.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes)
            foreach (var stock in availability.ByCombinationId.Values)
                situations.Add((stock.Quantity, stock.AllowOutOfStockOrders));
        else if (product.ManageInventoryMethod == ManageInventoryMethod.ManageStock)
            situations.Add((availability.ProductStockQuantity, product.BackorderMode != BackorderMode.NoBackorders));

        foreach (var situation in situations)
            stockMessages[situation] = await BuildStockMessageAsync(product, situation.Quantity, situation.AllowOutOfStock);

        return new BulkOrderPricingContext(valueAdjustments,
            prices,
            availability.ByCombinationId,
            stockMessages,
            bundleLimits,
            product.ManageInventoryMethod,
            availability.ProductStockQuantity,
            product.BackorderMode != BackorderMode.NoBackorders,
            productDisabled,
            callForPrice,
            showPrices,
            product.OrderMaximumQuantity)
        {
            _overriddenPrices = overriddenPrices
        };
    }

    /// <summary>
    /// The quantity nop would price each product at, for the WHOLE cart in one read.
    /// ShoppingCartService.cs:1490-1497: when GroupTierPricesForDistinctShoppingCartItems is on, the tier is
    /// chosen by how many of THAT PRODUCT are in the cart across all its lines, falling back to 1 when the
    /// cart holds none - so a product absent from the returned map is a product priced at 1.
    /// </summary>
    /// <remarks>
    /// Batched because the caller loops products: the per-product form was one cart query per
    /// card, fifteen on a fifteen-card page, each to sum one number. Empty when the setting is off, which is
    /// nop's default, and then no cart is read at all.
    ///
    /// NOT BulkOrderModelFactory.GetCartQuantitiesAsync. That one is packname-scoped, because the "N in cart"
    /// badge should show the packname the customer is looking at. nop's tier lookup is not packname-aware and
    /// counts the whole cart, so reusing the badge's number would price some carts at the wrong tier - cheaper
    /// or dearer than the checkout will actually charge. Two different questions; two different reads.
    /// </remarks>
    public virtual async Task<IDictionary<int, int>> GetTierQuantitiesAsync(Customer customer)
    {
        if (!_shoppingCartSettings.GroupTierPricesForDistinctShoppingCartItems)
            return new Dictionary<int, int>();

        var cart = await _shoppingCartService.GetShoppingCartAsync(customer,
            shoppingCartType: ShoppingCartType.ShoppingCart);

        return cart
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
    }

    #endregion
}