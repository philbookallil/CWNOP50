using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Tax;
using Nop.Core.Domain.Vendors;
using Nop.Core.Infrastructure;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Common.Pdf;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Common.Pdf;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Vendors;
using PdfRpt.Core.Contracts;
using System.Globalization;

namespace Nop.Plugin.CW.Core.Services.Common;

/// <summary>
/// Addresses the packing slip and the invoice to the destinations the goods actually go to.
/// </summary>
/// <remarks>
/// <para>
/// nop assumes one delivery address per order and puts it on <c>Order.ShippingAddressId</c>. A packname
/// order has several, so <c>OrderProcessingOverrideService</c> stores the <b>first</b> packname's address
/// there as a placeholder. Two documents are built from that placeholder and are therefore wrong on a
/// multi-destination order: the packing slip, which goes on the box and so sends parcels to the wrong
/// place, and the invoice, which names one branch and silently hides every other.
/// </para>
/// <para>
/// Two methods are overridden. Both of nop's bulk operations —
/// <c>PrintPackagingSlipsToPdfAsync</c> and <c>PrintOrdersToPdfAsync</c> — loop and call the single-document
/// method on <c>this</c>, so printing a batch is corrected by the same change.
/// </para>
/// <para>
/// The slip and the invoice answer the question differently on purpose. A slip belongs to one shipment,
/// which by <c>AddShipmentDestinationFilter</c> belongs to exactly one destination, so it names one. An
/// invoice covers the whole order, so it lists them all.
/// </para>
/// </remarks>
public class PdfOverrideService : PdfService
{
    #region Fields

    protected readonly ICWOrderService _cwOrderService;
    protected readonly IExchangeService _exchangeService;

    #endregion

    #region Ctor

    /// <remarks>
    /// Every parameter but the last is nop's, passed straight through. Long, and unavoidable when
    /// subclassing a nop service — <c>OrderProcessingOverrideService</c> and
    /// <c>OrderTotalCalculationOverrideService</c> have the same shape. Nothing is stored twice: nop's
    /// fields are <c>protected</c>, so the body below uses them directly.
    /// </remarks>
    public PdfOverrideService(AddressSettings addressSettings,
        CatalogSettings catalogSettings,
        CurrencySettings currencySettings,
        IAddressService addressService,
        IAttributeFormatter<AddressAttribute, AddressAttributeValue> addressAttributeFormatter,
        ICountryService countryService,
        ICurrencyService currencyService,
        IDateTimeHelper dateTimeHelper,
        IGiftCardService giftCardService,
        IHtmlFormatter htmlFormatter,
        ILanguageService languageService,
        ILocalizationService localizationService,
        IMeasureService measureService,
        INopFileProvider fileProvider,
        IOrderService orderService,
        IPaymentPluginManager paymentPluginManager,
        IPaymentService paymentService,
        IPictureService pictureService,
        IPriceFormatter priceFormatter,
        IProductService productService,
        IRewardPointService rewardPointService,
        ISettingService settingService,
        IShipmentService shipmentService,
        IStateProvinceService stateProvinceService,
        IStoreContext storeContext,
        IStoreService storeService,
        IThumbService thumbService,
        IVendorService vendorService,
        IWorkContext workContext,
        MeasureSettings measureSettings,
        TaxSettings taxSettings,
        VendorSettings vendorSettings,
        ICWOrderService cwOrderService,
        IExchangeService exchangeService)
        : base(addressSettings,
            catalogSettings,
            currencySettings,
            addressService,
            addressAttributeFormatter,
            countryService,
            currencyService,
            dateTimeHelper,
            giftCardService,
            htmlFormatter,
            languageService,
            localizationService,
            measureService,
            fileProvider,
            orderService,
            paymentPluginManager,
            paymentService,
            pictureService,
            priceFormatter,
            productService,
            rewardPointService,
            settingService,
            shipmentService,
            stateProvinceService,
            storeContext,
            storeService,
            thumbService,
            vendorService,
            workContext,
            measureSettings,
            taxSettings,
            vendorSettings)
    {
        _cwOrderService = cwOrderService;
        _exchangeService = exchangeService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// A stand-in order carrying the destination's address and shipping method, for the sole purpose of
    /// handing them to nop's address formatter.
    /// </summary>
    /// <remarks>
    /// <b>Never persisted, and never returned to a caller.</b> It exists because
    /// <see cref="PdfService.GetShippingAddressAsync"/> takes an <see cref="Order"/> and reads exactly the
    /// seven fields set below — it has no shipment in scope and no overload that takes an address. Passing
    /// a stand-in means the destination's address is formatted by *precisely* the code that formats every
    /// other address in a nop PDF: the AddressSettings toggles, the localised state and country, the
    /// formatted address line, the custom address attributes and the custom values. Copying that
    /// formatting here instead would be a hundred lines that silently drift on the next nop upgrade.
    /// <para>
    /// Two fields come from the destination rather than the order: the address, which is the point, and
    /// the shipping method, which was previously the order-level one and so could disagree with the
    /// address printed beside it.
    /// </para>
    /// </remarks>
    protected virtual Order BuildDestinationOrder(Order order, OrderShippingDestination destination)
    {
        return new Order
        {
            //read by GetShippingAddressAsync only for its exception message
            Id = order.Id,

            //gates the whole method: ShippingNotRequired produces an empty address block
            ShippingStatus = order.ShippingStatus,

            //an order is entirely collected or entirely delivered, so the
            //order-level pickup fields remain correct for every destination on it
            PickupInStore = order.PickupInStore,
            PickupAddressId = order.PickupAddressId,

            //shipping-related custom values are order-level and unchanged by the destination
            CustomValuesXml = order.CustomValuesXml,

            //--- the substitution ---
            ShippingAddressId = destination.ShippingAddressId,
            ShippingMethod = destination.ShippingMethod
        };
    }

    /// <summary>
    /// Format one <see cref="AddressItem"/> per destination, each tagged with the packnames that share it.
    /// </summary>
    /// <remarks>
    /// The packname list rides in as a <see cref="CustomValue"/> because that is the only member of
    /// <see cref="AddressItem"/> that <c>PdfDocument.BuildAddressTable</c> actually renders as free text —
    /// it emits <c>"{Name}: {Value}"</c> for each one, giving a <c>Packs: Uniform Store, Sports Kit</c>
    /// line at the foot of the block. <c>AddressItem.AddressAttributes</c> looks like the natural home and
    /// is not: <c>PdfService</c> fills it at lines 208 and 296 and <c>BuildAddressTable</c> never reads it,
    /// so anything written there disappears.
    /// </remarks>
    protected virtual async Task<List<AddressItem>> BuildShippingDestinationAddressesAsync(Order order,
        IList<OrderShippingDestination> destinations,
        Language language)
    {
        var packsLabel = await _localizationService
            .GetResourceAsync("Plugins.CW.Core.Pdf.ShippingDestination.Packs", language.Id);

        var addresses = new List<AddressItem>();

        foreach (var destination in destinations)
        {
            //Refuse rather than print a wrong address, the same rule the packing slip follows.
            //GetShippingAddressAsync would throw here anyway, but its message names the order and so
            //reads as though the order-level address were missing, which it is not.
            if (order.ShippingStatus != ShippingStatus.ShippingNotRequired
                && !destination.PickupInStore
                && destination.ShippingAddressId == null)
                throw new NopException($"Cannot print an invoice for order {order.Id}: shipping destination '{destination.Label}' has no address recorded.");

            var address = await GetShippingAddressAsync(language, BuildDestinationOrder(order, destination));

            if (destination.PackNames.Count > 0)
                address.CustomValues.Add(new CustomValue(packsLabel,
                    string.Join(", ", destination.PackNames),
                    CustomValueDisplayLocation.ShippingAddress));

            addresses.Add(address);
        }

        return addresses;
    }

    #endregion

    #region Methods

    /// <summary>
    /// nop's product rows, each packname line followed by who it is for: packname · department · cost centre.
    /// </summary>
    /// <remarks>
    /// The invoice prints <see cref="ProductItem.ProductAttributes"/> under the product name, and so does the packing
    /// slip through <see cref="CWShipmentDocument"/>. nop builds exactly one row per order item, in the order given, so
    /// the rows and the items are matched by position. A plain order has no packname lines and is returned as nop
    /// built it.
    /// </remarks>
    protected override async Task<List<ProductItem>> GetOrderProductItemsAsync(Order order, IList<OrderItem> orderItems, Language language, IList<ShipmentItem> shipmentItems = null)
    {
        var productItems = await base.GetOrderProductItemsAsync(order, orderItems, language, shipmentItems);

        var packNameItems = await _cwOrderService.GetPackNameFullOrderItemsAsync(order.Id);

        if (packNameItems.Count == 0)
            return productItems;

        var packNameItemByOrderItemId = packNameItems
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var forFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Pdf.Product.For", language.Id);

        for (var i = 0; i < orderItems.Count && i < productItems.Count; i++)
        {
            if (!packNameItemByOrderItemId.TryGetValue(orderItems[i].Id, out var packNameItem))
                continue;

            var forText = PackNameOrderItemHelper.FormatFor(packNameItem.PackName, packNameItem.Department, packNameItem.CostCenter);

            if (!string.IsNullOrEmpty(forText))
                productItems[i].ProductAttributes.Add(string.Format(forFormat, forText));
        }

        return productItems;
    }

    /// <summary>
    /// nop's totals, extended with the settlement rows when the order has a settlement: the total before
    /// subsidy and store credit, what each covered, the customer's own discounts, the exchange fees and what
    /// the customer paid.
    /// </summary>
    protected override async Task<InvoiceTotals> GetTotalsAsync(Language lang, Order order)
    {
        var totals = await base.GetTotalsAsync(lang, order);

        var settlement = await _cwOrderService.GetOrderSettlementAsync(order.Id);

        if (settlement == null)
            return totals;

        var languageId = lang.Id;

        async Task<string> formatAsync(decimal amount, bool negative = false)
        {
            var inCustomerCurrency = _currencyService.ConvertCurrency(amount, order.CurrencyRate);

            return await _priceFormatter.FormatPriceAsync(negative ? -inCustomerCurrency : inCustomerCurrency, true, order.CustomerCurrencyCode, false, languageId);
        }

        var exchangeFees = (await _exchangeService.GetExchangeFeesAmountsAsync(order)).Values.Sum();

        //nop writes the subtotal discount and the order discount into the one Discount field, the second over the
        //first; a settled order can carry both, so each gets its own row here
        string subTotalDiscount = null;

        if (order.OrderSubTotalDiscountExclTax > decimal.Zero)
        {
            var inclTax = order.CustomerTaxDisplayType == TaxDisplayType.IncludingTax && !_taxSettings.ForceTaxExclusionFromOrderSubtotal;
            var discountInCustomerCurrency = _currencyService.ConvertCurrency(inclTax ? order.OrderSubTotalDiscountInclTax : order.OrderSubTotalDiscountExclTax, order.CurrencyRate);

            subTotalDiscount = await _priceFormatter.FormatPriceAsync(-discountInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, inclTax);
        }

        return new CWInvoiceTotals
        {
            SubTotal = totals.SubTotal,
            Discount = totals.Discount,
            SubTotalDiscount = subTotalDiscount,
            OrderDiscount = order.OrderDiscount > decimal.Zero ? await formatAsync(order.OrderDiscount, true) : null,
            GiftCards = totals.GiftCards,
            Shipping = totals.Shipping,
            PaymentMethodAdditionalFee = totals.PaymentMethodAdditionalFee,
            Tax = totals.Tax,
            TaxRates = totals.TaxRates,
            RewardPoints = totals.RewardPoints,
            OrderTotal = totals.OrderTotal,
            GrossTotal = await formatAsync(settlement.SettleableAmount),
            SubsidyForStock = settlement.SubsidyStockAmount > decimal.Zero ? await formatAsync(settlement.SubsidyStockAmount, true) : null,
            SubsidyForShipping = settlement.SubsidyShippingAmount > decimal.Zero ? await formatAsync(settlement.SubsidyShippingAmount, true) : null,
            CreditNotes = settlement.CreditNoteAmount > decimal.Zero ? await formatAsync(settlement.CreditNoteAmount, true) : null,
            ExchangeFees = exchangeFees > decimal.Zero ? await formatAsync(exchangeFees) : null,
            PayableBalance = totals.OrderTotal,
        };
    }

    /// <summary>
    /// Print an invoice naming every delivery address on the order, with the packnames sharing each.
    /// </summary>
    /// <remarks>
    /// nop's body is reproduced because <c>PrintOrderToPdfAsync</c> constructs its
    /// <c>InvoiceDocument</c> inline and offers no factory hook. Two things differ from it: the document
    /// type, and the two shipping properties on the initialiser. Everything else is nop's, line for line
    /// — the same accepted trade as <c>ShipmentOverrideService.GetAllShipmentsAsync</c>.
    /// </remarks>
    public override async Task PrintOrderToPdfAsync(Stream stream, Order order, Language language = null, Store store = null, Vendor vendor = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(order.Id);

        //Not a packname order — hand it back to nop untouched. This is the regression guard for every
        //ordinary order in the shop, and it is why the body below is only ever reached by a packname
        //one. GetOrderShippingDestinationsAsync is cached per order, so the guard costs almost nothing.
        if (destinations.Count == 0)
        {
            await base.PrintOrderToPdfAsync(stream, order, language, store, vendor);

            return;
        }

        //store info
        store ??= await _storeContext.GetCurrentStoreAsync();

        var orderStore = order.StoreId == 0 || order.StoreId == store?.Id ?
            store : await _storeService.GetStoreByIdAsync(order.StoreId);

        //language info
        language ??= await _languageService.GetLanguageByIdAsync(order.CustomerLanguageId);

        if (language?.Published != true)
            language = await _workContext.GetWorkingLanguageAsync();

        //by default _pdfSettings contains settings for the current active store
        //and we need PdfSettings for the store which was used to place an order
        //so let's load it based on a store of the current order
        var pdfSettingsByStore = await _settingService.LoadSettingAsync<PdfSettings>(orderStore.Id);

        byte[] logo = null;
        var logoPicture = await _pictureService.GetPictureByIdAsync(pdfSettingsByStore.LogoPictureId);
        if (logoPicture != null)
        {
            logo = await _pictureService.LoadPictureBinaryAsync(logoPicture);

            if (logoPicture.MimeType == MimeTypes.ImageSvg)
            {
                await using var logoStream = new MemoryStream(logo);
                logo = await _pictureService.ConvertSvgToPngAsync(logoStream);
            }
        }

        var date = await _dateTimeHelper.ConvertToUserTimeAsync(order.CreatedOnUtc, DateTimeKind.Utc);

        //a vendor should have access only to products
        var orderItems = await _orderService.GetOrderItemsAsync(order.Id, vendorId: vendor?.Id ?? 0);

        //...and therefore only to the destinations their own products go to. Note this can narrow the
        //list to one, and that one is not necessarily the order-level placeholder address — which is
        //why the fall-back to base above is on "no destinations", not on "fewer than two".
        var printedOrderItemIds = orderItems.Select(item => item.Id).ToHashSet();

        var printedDestinations = destinations
            .Where(destination => destination.OrderItemIds.Any(printedOrderItemIds.Contains))
            .ToList();

        //Nothing being printed belongs to any destination. Rather than guess an address, hand the whole
        //document back to nop.
        if (printedDestinations.Count == 0)
        {
            await base.PrintOrderToPdfAsync(stream, order, language, store, vendor);

            return;
        }

        var shippingDestinations = await BuildShippingDestinationAddressesAsync(order, printedDestinations, language);

        var column1Lines = string.IsNullOrEmpty(pdfSettingsByStore.InvoiceFooterTextColumn1) ?
            new List<string>()
            : pdfSettingsByStore.InvoiceFooterTextColumn1
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

        var column2Lines = string.IsNullOrEmpty(pdfSettingsByStore.InvoiceFooterTextColumn2) ?
            new List<string>()
            : pdfSettingsByStore.InvoiceFooterTextColumn2
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();


        var (fontName, fontSize) = GetFontInfo(pdfSettingsByStore, language.Rtl);

        var document = new CWInvoiceDocument
        {
            StoreUrl = orderStore.Url?.Trim('/'),
            Language = language,
            FontName = fontName,
            FontSize = fontSize,
            ImageTargetSize = pdfSettingsByStore.ImageTargetSize,
            OrderDateUser = date.ToString("D", new CultureInfo(language.LanguageCulture)),
            LogoData = logo,
            OrderNumberText = order.CustomOrderNumber,
            PageSize = pdfSettingsByStore.LetterPageSizeEnabled ? PdfPageSize.Letter : PdfPageSize.A4,
            BillingAddress = await GetBillingAddressAsync(vendor, language, order),

            //--- the two lines that differ from nop's implementation ---
            //ShippingAddress stays the first destination so that CWInvoiceDocument can defer to nop's
            //own single-address layout whenever there is only one to show.
            ShippingAddress = shippingDestinations[0],
            ShippingDestinations = shippingDestinations,

            Products = await GetOrderProductItemsAsync(order, orderItems, language),
            ShowSkuInProductList = _catalogSettings.ShowSkuOnProductDetailsPage,
            ShowVendorInProductList = _vendorSettings.ShowVendorOnOrderDetailsPage,
            CheckoutAttributes = vendor is null ? _htmlFormatter.ConvertHtmlToPlainText(order.CheckoutAttributeDescription, true, true) : string.Empty, //vendors cannot see checkout attributes
            Totals = vendor is null ? await GetTotalsAsync(language, order) : new(), //vendors cannot see totals
            OrderNotes = await GetOrderNotesAsync(pdfSettingsByStore, order, language),
            FooterTextColumn1 = column1Lines,
            FooterTextColumn2 = column2Lines,
            GetResourceAsync = async (string resourceKey, int languageId) => await _localizationService.GetResourceAsync(resourceKey, languageId)
        };

        await using var pdfStream = new MemoryStream();
        document.Generate(pdfStream);

        pdfStream.Position = 0;
        await pdfStream.CopyToAsync(stream);
    }

    /// <summary>
    /// Print a packing slip, addressed to the destination the shipment actually went to.
    /// </summary>
    public override async Task PrintPackagingSlipToPdfAsync(Stream stream, Shipment shipment, Language language = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        ArgumentNullException.ThrowIfNull(shipment);

        var order = await _orderService.GetOrderByIdAsync(shipment.OrderId);

        var destination = order == null
            ? null
            : await _cwOrderService.GetShipmentDestinationAsync(shipment.Id, order.Id);

        //Not a packname order — hand it back to nop untouched. This is the regression guard for every
        //ordinary order in the shop, and it is why the body below is only ever reached by a packname one.
        if (destination == null)
        {
            await base.PrintPackagingSlipToPdfAsync(stream, shipment, language);

            return;
        }

        //Refuse rather than print. A slip carrying the wrong address is the defect this class exists to
        //remove, so a destination with no address has to stop the print and say which one it was. nop
        //itself throws in the equivalent situation.
        if (!destination.PickupInStore && destination.ShippingAddressId == null)
            throw new NopException($"Cannot print a packing slip for shipment {shipment.Id}: shipping destination '{destination.Label}' on order {order.Id} has no address recorded.");

        var pdfSettingsByStore = await _settingService.LoadSettingAsync<PdfSettings>(order.StoreId);

        //language info
        language ??= await _languageService.GetLanguageByIdAsync(order.CustomerLanguageId);

        if (language?.Published != true)
            language = await _workContext.GetWorkingLanguageAsync();

        var shipmentItems = await _shipmentService.GetShipmentItemsByShipmentIdAsync(shipment.Id);

        if (shipmentItems?.Any() != true)
            return;

        var orderItems = await shipmentItems
            .SelectAwait(async si => await _orderService.GetOrderItemByIdAsync(si.OrderItemId))
            .Where(pi => pi != null)
            .ToListAsync();

        if (orderItems?.Any() != true)
            return;

        var (fontName, fontSize) = GetFontInfo(pdfSettingsByStore, language.Rtl);

        await using var pdfStream = new MemoryStream();
        //--- the document type differs from nop's implementation: it prints the lines under each product ---
        var document = new CWShipmentDocument
        {
            PageSize = pdfSettingsByStore.LetterPageSizeEnabled ? PdfPageSize.Letter : PdfPageSize.A4,
            Language = language,
            FontName = fontName,
            FontSize = fontSize,
            ImageTargetSize = pdfSettingsByStore.ImageTargetSize,
            ShipmentNumberText = shipment.Id.ToString(),
            OrderNumberText = order.CustomOrderNumber,

            //--- and so does the address ---
            Address = await GetShippingAddressAsync(language, BuildDestinationOrder(order, destination)),

            Products = await GetOrderProductItemsAsync(order, orderItems, language, shipmentItems),
            GetResourceAsync = async (string resourceKey, int languageId) => await _localizationService.GetResourceAsync(resourceKey, languageId)
        };

        document.Generate(pdfStream);

        pdfStream.Position = 0;
        await pdfStream.CopyToAsync(stream);
    }

    #endregion
}