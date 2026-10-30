using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tax;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Shipping;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class PackNameOrderModelFactory : IPackNameOrderModelFactory
{
    #region Fields

    protected readonly ICWOrderService _cwOrderService;
    protected readonly IProductService _productService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IOrderService _orderService;
    protected readonly OrderSettings _orderSettings;
    protected readonly ICurrencyService _currencyService;
    protected readonly CurrencySettings _currencySettings;
    protected readonly IWorkContext _workContext;
    protected readonly ILocalizationService _localizationService;
    protected readonly TaxSettings _taxSettings;
    protected readonly IPictureService _pictureService;
    protected readonly MediaSettings _mediaSettings;
    protected readonly ISubsidyCreditService _subsidyCreditService;
    protected readonly IShipmentService _shipmentService;

    #endregion

    #region Ctor

    public PackNameOrderModelFactory(ICWOrderService cwOrderService,
        IProductService productService,
        IPriceFormatter priceFormatter,
        IOrderService orderService,
        OrderSettings orderSettings,
        ICurrencyService currencyService,
        CurrencySettings currencySettings,
        IWorkContext workContext,
        ILocalizationService localizationService,
        TaxSettings taxSettings,
        IPictureService pictureService,
        MediaSettings mediaSettings,
        ISubsidyCreditService subsidyCreditService,
        IShipmentService shipmentService)
    {
        _cwOrderService = cwOrderService;
        _productService = productService;
        _priceFormatter = priceFormatter;
        _orderService = orderService;
        _orderSettings = orderSettings;
        _currencyService = currencyService;
        _currencySettings = currencySettings;
        _workContext = workContext;
        _localizationService = localizationService;
        _taxSettings = taxSettings;
        _pictureService = pictureService;
        _mediaSettings = mediaSettings;
        _subsidyCreditService = subsidyCreditService;
        _shipmentService = shipmentService;
    }

    #endregion

    #region Utilities

    protected async Task<AdminPackNameOrderItemModel> PrepareItemModelAsync(PackNameFullOrderItem item, Order order, Currency primaryStoreCurrency, int languageId, IReadOnlyDictionary<int, Product> productMap)
    {
        if (!productMap.TryGetValue(item.ProductId, out var product) || product == null)
            return null;   // guard against stale/deleted product

        var model = new AdminPackNameOrderItemModel
        {
            ProductId = item.ProductId,
            ProductName = product.Name,
            AttributeInfo = item.AttributeDescription,
            Sku = await _productService.FormatSkuAsync(product, item.AttributesXml),
            Quantity = item.Quantity,
        };

        var picture = await _pictureService.GetProductPictureAsync(product, item.AttributesXml);
        model.PictureThumbnailUrl = (await _pictureService.GetPictureUrlAsync(picture, _mediaSettings.OrderThumbPictureSize)).Url;

        model.UnitPriceInclTax = await _priceFormatter.FormatOrderPriceAsync(
            item.UnitPriceInclTax, order.CurrencyRate, order.CustomerCurrencyCode,
            _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, true, true);

        model.UnitPriceExclTax = await _priceFormatter.FormatOrderPriceAsync(
            item.UnitPriceExclTax, order.CurrencyRate, order.CustomerCurrencyCode,
            _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, false, true);

        model.SubTotalInclTax = await _priceFormatter.FormatOrderPriceAsync(
            item.PriceInclTax, order.CurrencyRate, order.CustomerCurrencyCode,
            _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, true, true);

        model.SubTotalExclTax = await _priceFormatter.FormatOrderPriceAsync(
            item.PriceExclTax, order.CurrencyRate, order.CustomerCurrencyCode,
            _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, false, true);

        return model;
    }

    protected async Task<AdminPackNameOrderGroupModel> PrepareGroupModelAsync(PackNameOrderGroupDTO dto, Order order, Currency primaryStoreCurrency, int languageId, IReadOnlyDictionary<int, Product> productMap, IReadOnlyDictionary<int, decimal> orderItemSubsidyMap, decimal packNameShippingSubsidy)
    {
        var itemModels = new List<AdminPackNameOrderItemModel>();

        foreach (var item in dto.Items)
            itemModels.Add(await PrepareItemModelAsync(item, order, primaryStoreCurrency, languageId, productMap));

        var shippingStatus = dto.Shipping != null
            ? await _localizationService.GetLocalizedEnumAsync(dto.Shipping.ShippingStatus)
            : string.Empty;

        var shippingInclTax = dto.Shipping?.ShippingInclTax ?? 0m;
        var shippingExclTax = dto.Shipping?.ShippingExclTax ?? 0m;

        var model = new AdminPackNameOrderGroupModel
        {
            PackNameId = dto.PackNameId,
            PackName = dto.PackName,
            Branch = dto.Branch,
            BranchCode = dto.BranchCode,
            Department = dto.Department,
            CostCenter = dto.CostCenter,
            Items = itemModels,
            IsSharedShipping = dto.IsSharedShipping,
            SharedWithPackNames = dto.SharedWithPackNames,
            ShippingMethod = dto.Shipping?.ShippingMethod,
            ShippingStatus = shippingStatus,
            PickupInStore = dto.Shipping?.PickupInStore ?? false,
            AddressName = dto.AddressName,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            AddressCityStateZip = dto.AddressCityStateZip,
            AddressCountry = dto.AddressCountry,
            AddressPhone = dto.AddressPhone,
            ShippingInclTax = shippingInclTax,
            ShippingInclTaxFormatted = await _priceFormatter.FormatOrderPriceAsync(
                shippingInclTax, order.CurrencyRate, order.CustomerCurrencyCode,
                _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, true, true),
            ShippingExclTaxFormatted = await _priceFormatter.FormatOrderPriceAsync(
                shippingExclTax, order.CurrencyRate, order.CustomerCurrencyCode,
                _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, false, true),
        };

        var subsidyForStock = dto.Items.Sum(item => orderItemSubsidyMap.GetValueOrDefault(item.Id, 0m));
        var subsidyForShipping = packNameShippingSubsidy;
        var subsidyTotal = subsidyForStock + subsidyForShipping;

        if (subsidyTotal > 0)
        {
            model.HasSubsidy = true;
            model.HasShippingSubsidy = subsidyForShipping > 0;

            model.SubsidyForStockFormatted = await _priceFormatter.FormatOrderPriceAsync(subsidyForStock, order.CurrencyRate, order.CustomerCurrencyCode, _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, null, false);

            if (subsidyForShipping > 0)
                model.SubsidyForShippingFormatted = await _priceFormatter.FormatOrderPriceAsync(subsidyForShipping, order.CurrencyRate, order.CustomerCurrencyCode, _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, null, false);

            model.SubsidyTotalFormatted = await _priceFormatter.FormatOrderPriceAsync(subsidyTotal, order.CurrencyRate, order.CustomerCurrencyCode, _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, null, false);
        }

        return model;
    }

    #endregion

    #region Methods

    public async Task<AdminPackNameOrderDetailsModel> PreparePackNameOrderDetailsModelAsync(int orderId)
    {
        var groups = await _cwOrderService.GetPackNameOrderGroupsAsync(orderId);

        if (groups.Count == 0)
            return new AdminPackNameOrderDetailsModel();

        var order = await _orderService.GetOrderByIdAsync(orderId);
        var primaryStoreCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        // Every product needed across the groups, and the order's subsidy ledgers
        var uniqueProductIds = groups.SelectMany(g => g.Items).Select(i => i.ProductId).Distinct().ToArray();

        //One by one, not GetProductsByIdsAsync: that leaves deleted products out, and an order keeps showing a
        //product deleted after it was placed. Each read is served from nop's entity cache.
        var productMap = new Dictionary<int, Product>();

        foreach (var productId in uniqueProductIds)
        {
            var product = await _productService.GetProductByIdAsync(productId);

            if (product != null)
                productMap[productId] = product;
        }
        var subsidyItems = await _subsidyCreditService.GetSubsidyOrderItemsByOrderIdAsync(orderId);
        var subsidyShippings = await _subsidyCreditService.GetSubsidyOrderShippingsAsync(orderId);

        var orderItemPriceMap = groups.SelectMany(g => g.Items).ToDictionary(item => item.Id, item => item.UnitPriceInclTax);

        var orderItemSubsidyMap = subsidyItems
            .GroupBy(x => x.OrderItemId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var unitPrice = orderItemPriceMap.GetValueOrDefault(g.Key, 0m);
                    return g.Sum(x =>
                        x.Credit + x.CreditTopup + x.ProductGroupCredit + x.ProductGroupCreditTopup
                        + (x.Quantity + x.QuantityTopup) * unitPrice);
                });

        var packNameShippingMap = subsidyShippings.GroupBy(x => x.PackNameId).ToDictionary(g => g.Key, g => g.Sum(x => x.Credit + x.CreditTopup));

        // One group model after another: the services they call are scoped to this request and not written for
        // concurrent use
        var groupModels = new List<AdminPackNameOrderGroupModel>();

        foreach (var dto in groups)
            groupModels.Add(await PrepareGroupModelAsync(dto, order, primaryStoreCurrency, languageId, productMap, orderItemSubsidyMap, packNameShippingMap.GetValueOrDefault(dto.PackNameId, 0m)));

        var model = new AdminPackNameOrderDetailsModel
        {
            Groups = groupModels,
            TaxDisplayType = _taxSettings.TaxDisplayType,
            AllowCustomersToSelectTaxDisplayType = _taxSettings.AllowCustomersToSelectTaxDisplayType,
        };

        var settlement = await _cwOrderService.GetOrderSettlementAsync(orderId);

        if (settlement != null)
        {
            async Task<string> formatAsync(decimal amount) => await _priceFormatter.FormatOrderPriceAsync(
                amount, order.CurrencyRate, order.CustomerCurrencyCode,
                _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, null, false);

            var subsidyTotal = settlement.SubsidyStockAmount + settlement.SubsidyShippingAmount;

            model.SettlementTotals = new AdminPackNameOrderSettlementModel
            {
                GrossTotal = await formatAsync(settlement.SettleableAmount),
                SubsidyForStock = await formatAsync(settlement.SubsidyStockAmount),
                SubsidyForShipping = await formatAsync(settlement.SubsidyShippingAmount),
                SubsidyTotal = await formatAsync(subsidyTotal),
                CreditNoteTotal = await formatAsync(settlement.CreditNoteAmount),
                PayableBalance = await formatAsync(order.OrderTotal),
                HasSubsidy = subsidyTotal > decimal.Zero,
                HasShippingSubsidy = settlement.SubsidyShippingAmount > decimal.Zero,
                HasCreditNotes = settlement.CreditNoteAmount > decimal.Zero,
            };
        }

        return model;
    }

    public virtual async Task<IList<OrderShippingDestination>> PrepareShippingDestinationsAsync(int orderId)
    {
        var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(orderId);

        if (destinations.Count == 0)
            return destinations;

        //How much of each order item is already in a shipment, aggregated ONCE for the whole order.
        //IOrderService.GetTotalNumberOfItemsCanBeAddedToShipmentAsync re-reads every shipment and every
        //shipment item on each call, so asking it per order item turns opening this page into hundreds
        //of queries on a large, fully-shipped order. Same arithmetic, three queries instead.
        var shipments = await _shipmentService.GetShipmentsByOrderIdAsync(orderId);
        var inShipments = new Dictionary<int, int>();

        foreach (var shipment in shipments)
        {
            foreach (var shipmentItem in await _shipmentService.GetShipmentItemsByShipmentIdAsync(shipment.Id))
                inShipments[shipmentItem.OrderItemId] = inShipments.GetValueOrDefault(shipmentItem.OrderItemId) + shipmentItem.Quantity;
        }

        //isShipEnabled: true mirrors HasItemsToAddToShipmentAsync — only shippable products count
        var orderItems = (await _orderService.GetOrderItemsAsync(orderId, isShipEnabled: true))
            .ToDictionary(item => item.Id);

        foreach (var destination in destinations)
        {
            destination.HasItemsToShip = destination.OrderItemIds.Any(id =>
                orderItems.TryGetValue(id, out var orderItem) &&
                orderItem.Quantity - inShipments.GetValueOrDefault(id) > 0);
        }

        return destinations;
    }

    #endregion
}
