using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.BusinessLogic.Requests;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Plugin.CW.Core.BusinessLogic;

/// <summary>
/// Composes each line's AttributesXml and hands it to CW.Core's AddToCartAsync override, which
/// validates internally and attaches the packname ambiently.
/// </summary>
public class BulkOrderLogic : IBulkOrderLogic
{
    #region Fields

    protected readonly IBulkOrderService _bulkOrderService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IProductAttributeParser _productAttributeParser;
    protected readonly IProductAttributeService _productAttributeService;
    protected readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;
    protected readonly ICustomerActivityService _customerActivityService;

    #endregion

    #region Ctor

    public BulkOrderLogic(IBulkOrderService bulkOrderService,
        ILocalizationService localizationService,
        IProductAttributeParser productAttributeParser,
        IProductAttributeService productAttributeService,
        IShoppingCartOverrideService shoppingCartService,
        IStoreContext storeContext,
        IWorkContext workContext,
        ICustomerActivityService customerActivityService)
    {
        _bulkOrderService = bulkOrderService;
        _localizationService = localizationService;
        _productAttributeParser = productAttributeParser;
        _productAttributeService = productAttributeService;
        _shoppingCartService = shoppingCartService;
        _storeContext = storeContext;
        _workContext = workContext;
        _customerActivityService = customerActivityService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Builds one line's AttributesXml from its selections, using nop's own writer so the shape is
    /// byte-for-byte what nop produces, then strips unmet-condition attributes exactly as
    /// nop's own form path does (ProductAttributeParser.ParseProductAttributes:309-317).
    /// </summary>
    /// <remarks>
    /// The request is a customer-supplied JSON body, so nothing in it is trusted:
    ///  - a mapping id is written only when it is one of THIS product's mappings;
    ///  - a value id is written only when it is one of THAT mapping's values. nop's cart validation checks
    ///    the mapping's product (ShoppingCartService.cs:840) and never the value's mapping, so a foreign
    ///    value id, with its own price adjustment, would otherwise reach the line;
    ///  - the grid has no input for a non-combinable mapping (text, date, file), so nothing posted for one is written:
    ///    free text would skip nop's own parsing of those types;
    ///  - an attribute that takes one value (a list, radio buttons, squares) gets the first posted value only;
    ///    several would add up their price adjustments and match no combination, skipping its stock check;
    ///  - an attribute whose condition is unmet by the line's OWN selections is removed. The grid never
    ///    submits one, but the grid is not the only thing that can POST here, and nothing
    ///    downstream removes it: GetShoppingCartItemAttributeWarningsAsync filters its MAPPING list by
    ///    condition and never the submitted attributes.
    /// Unknown ids are dropped silently rather than rejected - a stale page after an admin edit is the
    /// common cause, and nop's own required-attribute validation then refuses the line anyway, with a
    /// sentence the customer can act on.
    /// </remarks>
    protected virtual async Task<string> ComposeAttributesXmlAsync(Product product, BulkOrderLine line)
    {
        var mappings = await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id);
        var byId = mappings.ToDictionary(mapping => mapping.Id);

        string xml = null;

        foreach (var selection in line.Selections ?? [])
        {
            if (!byId.TryGetValue(selection.ProductAttributeMappingId, out var mapping))
                continue;

            if (mapping.IsNonCombinable())
                continue;

            //GetProductAttributeValuesAsync is IStaticCacheManager-backed, so this is a cache read per
            //selection, not a query - the same accessor the builder reads
            var ownValueIds = (await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id))
                .Select(value => value.Id)
                .ToHashSet();

            var valueIds = (selection.ValueIds ?? []).Where(ownValueIds.Contains).Distinct();
            var takesSeveral = mapping.AttributeControlType is AttributeControlType.Checkboxes or AttributeControlType.ReadonlyCheckboxes;

            foreach (var valueId in takesSeveral ? valueIds : valueIds.Take(1))
                xml = _productAttributeParser.AddProductAttribute(xml, mapping, valueId.ToString());
        }

        if (string.IsNullOrEmpty(xml))
            return xml;

        //nop's own loop, in nop's own order (ParseProductAttributes:309-317): each condition is asked against
        //the XML as it stands when the loop reaches it, so a chain resolves the way the product page
        //resolves it. Not the builder's ParseCondition - this path must agree with nop's page, not with the
        //grid, and the two agree but are proven by different code.
        foreach (var mapping in mappings)
        {
            var conditionMet = await _productAttributeParser.IsConditionMetAsync(mapping, xml);

            if (conditionMet.HasValue && !conditionMet.Value)
                xml = _productAttributeParser.RemoveProductAttribute(xml, mapping);
        }

        return xml;
    }

    /// <summary>
    /// A warning as plain text: nop's and CW's warnings may carry markup (a link to a required product) or an
    /// HTML-encoded name, and the page shows them as text
    /// </summary>
    /// <param name="warning">Warning</param>
    /// <returns>The warning's text</returns>
    protected static string AsText(string warning) =>
        WebUtility.HtmlDecode(Regex.Replace(warning ?? string.Empty, "<[^>]*>", string.Empty));

    /// <summary>
    /// Reads the cart back and packages it with the line outcomes.
    /// </summary>
    /// <remarks>
    /// The cart is read AFTER the writes rather than tracked through them, and that is the point: nop merges
    /// a matching line into an existing one, and addRequiredProducts can add a product nobody typed a
    /// quantity for, so what the customer submitted does not determine what the cart now holds. Reading it
    /// back is the only answer that is right in both cases. It costs one query on a path that has already
    /// done one write per product and options.
    /// The per-product map is packname-scoped (it feeds the card's "n in cart", which always has been); the
    /// total is the full store cart, because that is what the header counter counts everywhere else.
    /// </remarks>
    protected virtual async Task<BulkOrderAddResult> ComposeResultAsync(Customer customer, int storeId,
        int added, IList<BulkOrderLineResult> results)
    {
        var quantities = await _bulkOrderService.GetCartQuantitiesAsync(customer, storeId);

        var fullCart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, storeId);

        return new BulkOrderAddResult(added, results, quantities, fullCart.Sum(item => item.Quantity));
    }

    #endregion

    #region Methods

    public virtual async Task<BulkOrderAddResult> AddToCartAsync(BulkOrderAddRequest request)
    {
        var results = new List<BulkOrderLineResult>();
        var added = 0;

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        if (request?.Lines is null or { Count: 0 })
            return await ComposeResultAsync(customer, store.Id, 0, results);

        //each line is a cart write with its own checks, so one submit adds a bounded number of them
        if (request.Lines.Count(line => line.Quantity > 0) > CWCoreDefaults.BulkOrderMaxLinesPerAdd)
        {
            var tooMany = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.BulkOrder.AddToCart.TooManyLines"),
                CWCoreDefaults.BulkOrderMaxLinesPerAdd);
            results.Add(new BulkOrderLineResult(-1, 0, null, [tooMany]));

            return await ComposeResultAsync(customer, store.Id, 0, results);
        }

        //one entry per PRODUCT that took at least one line, not one per line: nop logs once per add-to-cart
        //and a grid submit is dozens of them, which would bury the rest of the log. Logging once per
        //submit would name whichever product the loop touched last, which on a multi-product submit is the
        //wrong one - so this keeps nop's granularity of "a product was added".
        var addedProducts = new Dictionary<int, Product>();

        //the same visibility walk the list used, asked again on every submit, once per product. The page could be
        //minutes old, the customer's roles or packname allocation could have changed, and the product id came
        //from the browser. A product the customer cannot see must not enter the cart through this door.
        var visibleProducts = new Dictionary<int, Product>();

        //lines for the same product and options are added as one: one cart write, one set of checks
        var merged = new List<(Product product, string attributesXml, long quantity, List<int> indices)>();

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];

            //a blank box is not an error and not a line - the page submits every cell it rendered
            if (line.Quantity <= 0)
                continue;

            if (!visibleProducts.TryGetValue(line.ProductId, out var product))
                visibleProducts[line.ProductId] = product = await _bulkOrderService.GetVisibleProductByIdAsync(line.ProductId, store.Id);

            if (product == null)
            {
                results.Add(new BulkOrderLineResult(index, line.ProductId, line.CellKey,
                    [await _localizationService.GetResourceAsync("Plugins.CW.Core.BulkOrder.AddToCart.NotAvailable")]));

                continue;
            }

            var lineXml = await ComposeAttributesXmlAsync(product, line);
            var same = merged.FindIndex(entry => entry.product.Id == product.Id && entry.attributesXml == lineXml);

            if (same >= 0)
            {
                var entry = merged[same];
                entry.indices.Add(index);
                merged[same] = entry with { quantity = entry.quantity + line.Quantity };

                continue;
            }

            merged.Add((product, lineXml, line.Quantity, [index]));
        }

        foreach (var (product, attributesXml, totalQuantity, indices) in merged)
        {
            //a total past what a cart line holds is refused by nop's maximum quantity check
            var quantity = (int)Math.Min(totalQuantity, int.MaxValue);

            //The BASE-shaped overload (ShoppingCartOverrideService :283), not the one that takes a
            //packNameId. That method resolves the current packname itself and dispatches - to nop's own
            //AddToCartAsync when there is none, to the packname overload when there is one - which is what
            //"attaching the packname ambiently" means.
            //Passing packNameId explicitly is wrong here and fails silently: the packname overload (:460)
            //returns an EMPTY warning list when the id resolves to nothing, so a store with no packname in
            //context gets "added, no warnings" and an untouched cart.
            var warnings = await _shoppingCartService.AddToCartAsync(customer,
                product,
                ShoppingCartType.ShoppingCart,
                store.Id,
                attributesXml: attributesXml,
                quantity: quantity,
                addRequiredProducts: true);

            if (warnings.Count == 0)
            {
                added += quantity;
                addedProducts[product.Id] = product;
            }

            //every line merged into this one gets its outcome, as plain text
            var texts = warnings.Select(AsText).ToList();
            foreach (var index in indices)
                results.Add(new BulkOrderLineResult(index, request.Lines[index].ProductId, request.Lines[index].CellKey, texts));
        }

        //the same activity key and resource nop uses on the product page (ShoppingCartController :405-407) -
        //a store reading its activity log should not be able to tell which screen the add came from
        var activity = await _localizationService.GetResourceAsync("ActivityLog.PublicStore.AddToShoppingCart");

        foreach (var product in addedProducts.Values)
            await _customerActivityService.InsertActivityAsync("PublicStore.AddToShoppingCart",
                string.Format(activity, product.Name), product);

        return await ComposeResultAsync(customer, store.Id, added, results);
    }

    #endregion
}