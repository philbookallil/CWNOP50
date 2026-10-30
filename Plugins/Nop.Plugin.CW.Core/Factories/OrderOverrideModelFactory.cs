using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Core.Domain.Vendors;
using Nop.Plugin.CW.Core.Models.Orders;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Services.Vendors;
using Nop.Web.Factories;
using Nop.Web.Models.Common;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Shows the customer the address <b>this parcel</b> was sent to, rather than the order's placeholder.
/// </summary>
/// <remarks>
/// <para>
/// nop's shipment page embeds the whole order details model and renders
/// <c>Model.Order.ShippingAddress</c>, which on a packname order is the first packname's address — a
/// placeholder written at placement so the admin area has something to show. A customer with two parcels
/// going to two branches therefore sees the same address on both.
/// </para>
/// <para>
/// This is a post-process rather than a reimplementation: nop's method builds the model, and the one
/// property that is wrong is rebuilt afterwards. <b>The view needs no change.</b>
/// </para>
/// </remarks>
public class OrderOverrideModelFactory : OrderModelFactory
{
    #region Fields

    protected readonly ICWOrderService _cwOrderService;
    protected readonly IPackNameOrderModelFactory _packNameOrderModelFactory;

    #endregion

    #region Ctor

    public OrderOverrideModelFactory(AddressSettings addressSettings,
        CatalogSettings catalogSettings,
        IAddressModelFactory addressModelFactory,
        IAddressService addressService,
        ICountryService countryService,
        ICurrencyService currencyService,
        ICustomerService customerService,
        IDateTimeHelper dateTimeHelper,
        IGiftCardService giftCardService,
        ILocalizationService localizationService,
        IOrderProcessingService orderProcessingService,
        IOrderService orderService,
        IOrderTotalCalculationService orderTotalCalculationService,
        IPaymentPluginManager paymentPluginManager,
        IPaymentService paymentService,
        IPictureService pictureService,
        IPriceFormatter priceFormatter,
        IProductService productService,
        IRewardPointService rewardPointService,
        IShipmentService shipmentService,
        IShortTermCacheManager shortTermCacheManager,
        IStateProvinceService stateProvinceService,
        IStaticCacheManager staticCacheManager,
        IStoreContext storeContext,
        IUrlRecordService urlRecordService,
        IVendorService vendorService,
        IWebHelper webHelper,
        IWorkContext workContext,
        MediaSettings mediaSettings,
        OrderSettings orderSettings,
        PdfSettings pdfSettings,
        ReturnRequestSettings returnRequestSettings,
        RewardPointsSettings rewardPointsSettings,
        ShippingSettings shippingSettings,
        TaxSettings taxSettings,
        VendorSettings vendorSettings,
        ICWOrderService cwOrderService,
        IPackNameOrderModelFactory packNameOrderModelFactory)
        : base(addressSettings,
            catalogSettings,
            addressModelFactory,
            addressService,
            countryService,
            currencyService,
            customerService,
            dateTimeHelper,
            giftCardService,
            localizationService,
            orderProcessingService,
            orderService,
            orderTotalCalculationService,
            paymentPluginManager,
            paymentService,
            pictureService,
            priceFormatter,
            productService,
            rewardPointService,
            shipmentService,
            shortTermCacheManager,
            stateProvinceService,
            staticCacheManager,
            storeContext,
            urlRecordService,
            vendorService,
            webHelper,
            workContext,
            mediaSettings,
            orderSettings,
            pdfSettings,
            returnRequestSettings,
            rewardPointsSettings,
            shippingSettings,
            taxSettings,
            vendorSettings)
    {
        _cwOrderService = cwOrderService;
        _packNameOrderModelFactory = packNameOrderModelFactory;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Adds the packname groups and subsidy totals to the customer's order page, and labels each shipment
    /// with the destination it went to — but only when the order actually went to more than one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A customer with two parcels going to two branches has no way to tell which row is which: the table
    /// shows an id, a tracking number and some dates. The label answers that.
    /// </para>
    /// <para>
    /// The <c>destinations.Count &lt; 2</c> guard is both the display rule and the performance one. An
    /// order that went to one place needs no column, and the common case — every non-packname order in the
    /// shop — then costs a single cached read.
    /// </para>
    /// <para>
    /// nop also calls this method from <c>PrepareShipmentDetailsModelAsync</c>, which shows neither the
    /// packname details nor the labels. They are prepared there too and not used: that page is opened
    /// rarely, and keeping one path is simpler than a flag.
    /// </para>
    /// </remarks>
    public override async Task<OrderDetailsModel> PrepareOrderDetailsModelAsync(Order order)
    {
        var model = await base.PrepareOrderDetailsModelAsync(order);

        if (model == null)
            return model;

        //The packname groups and subsidy totals the order page shows, carried on a derived model so the view
        //only renders what its model carries; an order with no packname lines gets empty details.
        model = new CWOrderDetailsModel(model)
        {
            PackNameDetails = await _packNameOrderModelFactory.PreparePackNameOrderDetailsModelAsync(order.Id)
        };

        if (model.Shipments.Count == 0)
            return model;

        var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(order.Id);

        //Not a packname order, or everything went to one place: there is nothing to disambiguate.
        if (destinations.Count < 2)
            return model;

        foreach (var shipment in model.Shipments)
        {
            var destination = await _cwOrderService.GetShipmentDestinationAsync(shipment.Id, order.Id);

            if (destination != null)
                shipment.CustomProperties[CWCoreDefaults.SHIPMENT_DESTINATION_PROPERTY] = destination.Label;
        }

        return model;
    }

    public override async Task<ShipmentDetailsModel> PrepareShipmentDetailsModelAsync(Shipment shipment)
    {
        var model = await base.PrepareShipmentDetailsModelAsync(shipment);

        //Pickup orders show PickupAddress, not ShippingAddress, and an order is entirely collected or
        //entirely delivered — so there is nothing per-destination to correct.
        if (model?.Order == null || model.Order.PickupInStore)
            return model;

        var destination = await _cwOrderService.GetShipmentDestinationAsync(shipment.Id, shipment.OrderId);

        //Not a packname order, or a destination with no address recorded: leave nop's answer alone.
        //Unlike the packing slip, nothing is dispatched from this screen, so showing the order-level
        //address is no worse than showing nothing.
        if (destination?.ShippingAddressId == null)
            return model;

        var address = await _addressService.GetAddressByIdAsync(destination.ShippingAddressId.Value);

        if (address == null)
            return model;

        //The page prints the shipping method directly above the address, and
        //the order-level one is the same placeholder the address was. Correcting one without the other
        //would put the right branch next to the wrong carrier.
        if (!string.IsNullOrEmpty(destination.ShippingMethod))
            model.Order.ShippingMethod = destination.ShippingMethod;

        //A fresh model rather than repopulating the existing one, so no field of the placeholder address
        //can survive into the result.
        model.Order.ShippingAddress = new AddressModel();

        await _addressModelFactory.PrepareAddressModelAsync(model.Order.ShippingAddress,
            address: address,
            excludeProperties: false,
            addressSettings: _addressSettings);

        return model;
    }

    #endregion
}