using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tax;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.Orders;
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Seo;

namespace Nop.Plugin.CW.Core.Factories;

public class PackNameOrderModelFactory : IPackNameOrderModelFactory
{
    #region Fields

    protected readonly ICWOrderService _cwOrderService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IPictureService _pictureService;
    protected readonly IProductService _productService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IUrlRecordService _urlRecordService;
    protected readonly IOrderService _orderService;
    protected readonly ICurrencyService _currencyService;
    protected readonly IWorkContext _workContext;
    protected readonly OrderSettings _orderSettings;
    protected readonly MediaSettings _mediaSettings;
    protected readonly IPackNameService _packNameService;
    protected readonly ISubsidyCreditService _subsidyCreditService;
    protected readonly ISubsidyService _subsidyService;

    #endregion

    #region Ctor

    public PackNameOrderModelFactory(ICWOrderService cwOrderService,
        IPriceFormatter priceFormatter,
        IPictureService pictureService,
        IProductService productService,
        ILocalizationService localizationService,
        IUrlRecordService urlRecordService,
        IOrderService orderService,
        ICurrencyService currencyService,
        IWorkContext workContext,
        OrderSettings orderSettings,
        MediaSettings mediaSettings,
        IPackNameService packNameService,
        ISubsidyCreditService subsidyCreditService,
        ISubsidyService subsidyService)
    {
        _cwOrderService = cwOrderService;
        _priceFormatter = priceFormatter;
        _pictureService = pictureService;
        _productService = productService;
        _localizationService = localizationService;
        _urlRecordService = urlRecordService;
        _orderService = orderService;
        _currencyService = currencyService;
        _workContext = workContext;
        _orderSettings = orderSettings;
        _mediaSettings = mediaSettings;
        _packNameService = packNameService;
        _subsidyCreditService = subsidyCreditService;
        _subsidyService = subsidyService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The subsidy that paid for one order line, as the checkout shows it: one row per product group the line
    /// drew units or credit from, or one row for global credit.
    /// </summary>
    protected async Task<IList<SubsidyItemBreakdownModel>> PrepareSubsidyBreakdownAsync(IEnumerable<SubsidyOrderItem> lineSubsidy, Order order, int languageId, IReadOnlyDictionary<int, ProductGroup> productGroupMap)
    {
        var rows = new List<SubsidyItemBreakdownModel>();

        foreach (var group in lineSubsidy.GroupBy(x => x.ProductGroupId))
        {
            var quantityUsed = group.Sum(x => x.Quantity + x.QuantityTopup);
            var creditUsed = group.Key.HasValue
                ? group.Sum(x => x.ProductGroupCredit + x.ProductGroupCreditTopup)
                : group.Sum(x => x.Credit + x.CreditTopup);

            if (quantityUsed == 0 && creditUsed == decimal.Zero)
                continue;

            ProductGroup productGroup = null;

            if (group.Key.HasValue)
                productGroupMap.TryGetValue(group.Key.Value, out productGroup);

            var creditDisplay = creditUsed > decimal.Zero
                ? await _priceFormatter.FormatPriceAsync(-_currencyService.ConvertCurrency(creditUsed, order.CurrencyRate), true, order.CustomerCurrencyCode, false, languageId)
                : null;

            rows.Add(new SubsidyItemBreakdownModel
            {
                ProductGroupId = group.Key,
                ProductGroupName = productGroup?.Name,
                ProductGroupIcon = productGroup?.Icon,
                ProductGroupColour = productGroup?.Colour,
                QuantityUsed = quantityUsed,
                CreditUsed = creditUsed,
                CreditDisplay = creditDisplay,
            });
        }

        return rows;
    }

    protected async Task<PackNameOrderItemModel> PrepareItemModelAsync(PackNameFullOrderItem item, Order order, int languageId, bool showThumbnail, IReadOnlyDictionary<int, Product> productMap, ILookup<int, SubsidyOrderItem> subsidyByOrderItemId, IReadOnlyDictionary<int, ProductGroup> productGroupMap)
    {
        if (!productMap.TryGetValue(item.ProductId, out var product) || product == null)
            return null;   // guard against stale/deleted product

        var model = new PackNameOrderItemModel
        {
            OrderItemId = item.Id,
            ProductName = await _localizationService.GetLocalizedAsync(product, x => x.Name),
            ProductSeName = await _urlRecordService.GetSeNameAsync(product),
            AttributeInfo = item.AttributeDescription,
            Sku = await _productService.FormatSkuAsync(product, item.AttributesXml),
            Quantity = item.Quantity,
        };

        if (order.CustomerTaxDisplayType == TaxDisplayType.IncludingTax)
        {
            var unitPrice = _currencyService.ConvertCurrency(item.UnitPriceInclTax, order.CurrencyRate);
            model.UnitPrice = await _priceFormatter.FormatPriceAsync(unitPrice, true, order.CustomerCurrencyCode, languageId, true);

            var subTotal = _currencyService.ConvertCurrency(item.PriceInclTax, order.CurrencyRate);
            model.SubTotal = await _priceFormatter.FormatPriceAsync(subTotal, true, order.CustomerCurrencyCode, languageId, true);
        }
        else
        {
            var unitPrice = _currencyService.ConvertCurrency(item.UnitPriceExclTax, order.CurrencyRate);
            model.UnitPrice = await _priceFormatter.FormatPriceAsync(unitPrice, true, order.CustomerCurrencyCode, languageId, false);

            var subTotal = _currencyService.ConvertCurrency(item.PriceExclTax, order.CurrencyRate);
            model.SubTotal = await _priceFormatter.FormatPriceAsync(subTotal, true, order.CustomerCurrencyCode, languageId, false);
        }

        if (showThumbnail)
        {
            var picture = await _pictureService.GetProductPictureAsync(product, item.AttributesXml);
            model.PictureUrl = (await _pictureService.GetPictureUrlAsync(picture, _mediaSettings.OrderThumbPictureSize)).Url;
            model.PictureAlternateText = string.Format(
                await _localizationService.GetResourceAsync("Media.Product.ImageAlternateTextFormat"),
                model.ProductName);
        }

        model.SubsidyBreakdown = await PrepareSubsidyBreakdownAsync(subsidyByOrderItemId[item.Id], order, languageId, productGroupMap);

        return model;
    }

    protected async Task<PackNameOrderGroupModel> PrepareGroupModelAsync(PackNameOrderGroupDTO dto, Order order, int languageId, bool showThumbnail, IReadOnlyDictionary<int, Product> productMap, IReadOnlyDictionary<int, PackName> packNameMap, ILookup<int, SubsidyOrderItem> subsidyByOrderItemId, IReadOnlyDictionary<int, ProductGroup> productGroupMap)
    {
        var itemModels = new List<PackNameOrderItemModel>();

        foreach (var item in dto.Items)
        {
            //a line whose product is gone has no model: it is left out rather than failing the page
            var itemModel = await PrepareItemModelAsync(item, order, languageId, showThumbnail, productMap, subsidyByOrderItemId, productGroupMap);

            if (itemModel != null)
                itemModels.Add(itemModel);
        }

        packNameMap.TryGetValue(dto.PackNameId, out var packNameEntity);
        var rawColour = packNameEntity?.Colour ?? "#666666";
        var foreColor = PackNameColourHelper.ForegroundFor(rawColour);
        var iconClass = packNameEntity?.Group == true ? "packname-group"
                       : packNameEntity?.IsCustomer == true ? "packname-customer"
                       : "packname-no-customer";

        return new PackNameOrderGroupModel
        {
            PackName = dto.PackName,
            Branch = dto.Branch,
            BranchCode = dto.BranchCode,
            Department = dto.Department,
            CostCenter = dto.CostCenter,
            Items = itemModels,
            IsSharedShipping = dto.IsSharedShipping,
            SharedWithPackNames = dto.SharedWithPackNames,
            ShippingMethod = dto.Shipping?.ShippingMethod,
            PickupInStore = dto.Shipping?.PickupInStore ?? false,
            AddressName = dto.AddressName,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            AddressCityStateZip = dto.AddressCityStateZip,
            AddressCountry = dto.AddressCountry,
            AddressPhone = dto.AddressPhone,
            Colour = rawColour,
            ForeColor = foreColor,
            PackNameIconClass = iconClass,
        };
    }

    #endregion

    #region Methods

    public async Task<PackNameOrderDetailsModel> PreparePackNameOrderDetailsModelAsync(int orderId)
    {
        var groups = await _cwOrderService.GetPackNameOrderGroupsAsync(orderId);

        if (groups.Count == 0)
            return new PackNameOrderDetailsModel();

        var order = await _orderService.GetOrderByIdAsync(orderId);
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;
        var showThumb = _orderSettings.ShowProductThumbnailInOrderDetailsPage;

        // The products and pack names of the order, read one by one before any model is built (each is a
        // cached read)
        var uniqueProductIds = groups.SelectMany(g => g.Items).Select(i => i.ProductId).Distinct().ToArray();
        var uniquePackNameIds = groups.Select(g => g.PackNameId).Distinct().ToList();

        //One by one, not GetProductsByIdsAsync: that leaves deleted products out, and an order keeps showing a
        //product deleted after it was placed. Each read is served from nop's entity cache.
        var productMap = new Dictionary<int, Product>();

        foreach (var productId in uniqueProductIds)
        {
            var product = await _productService.GetProductByIdAsync(productId);

            if (product != null)
                productMap[productId] = product;
        }

        var packNameMap = new Dictionary<int, PackName>();

        foreach (var packNameId in uniquePackNameIds)
        {
            var packName = await _packNameService.GetPackNameByIdAsync(packNameId);

            if (packName != null)
                packNameMap[packNameId] = packName;
        }

        // The subsidy ledger of the order, per line, and the product groups it names
        var subsidyByOrderItemId = (await _subsidyCreditService.GetSubsidyOrderItemsByOrderIdAsync(orderId)).ToLookup(x => x.OrderItemId);

        var productGroupIds = subsidyByOrderItemId.SelectMany(x => x).Where(x => x.ProductGroupId.HasValue).Select(x => x.ProductGroupId.Value).Distinct().ToList();
        var productGroupMap = productGroupIds.Count == 0
            ? new Dictionary<int, ProductGroup>()
            : (await _subsidyService.GetProductGroupsByIdsAsync(productGroupIds)).ToDictionary(x => x.Id);

        // One group model after another: the services they call are scoped to this request and not written for
        // concurrent use
        var groupModels = new List<PackNameOrderGroupModel>();

        foreach (var dto in groups)
            groupModels.Add(await PrepareGroupModelAsync(dto, order, languageId, showThumb, productMap, packNameMap, subsidyByOrderItemId, productGroupMap));

        var model = new PackNameOrderDetailsModel
        {
            Groups = groupModels,
            ShowProductThumbnail = showThumb,
        };

        // The settlement block, from the row written when the order was placed
        var settlement = await _cwOrderService.GetOrderSettlementAsync(orderId);

        if (settlement != null)
        {
            //what subsidy and store credit took off is written with a minus, as nop writes its own discounts on this page
            async Task<string> formatAsync(decimal amount, bool deduction = false)
            {
                var amountInCustomerCurrency = _currencyService.ConvertCurrency(amount, order.CurrencyRate);

                return await _priceFormatter.FormatPriceAsync(deduction ? -amountInCustomerCurrency : amountInCustomerCurrency, true, order.CustomerCurrencyCode, false, languageId);
            }

            var subsidyTotal = settlement.SubsidyStockAmount + settlement.SubsidyShippingAmount;

            model.SettlementTotals = new PackNameOrderSettlementModel
            {
                GrossTotal = await formatAsync(settlement.SettleableAmount),
                SubsidyForStock = await formatAsync(settlement.SubsidyStockAmount, true),
                SubsidyForShipping = await formatAsync(settlement.SubsidyShippingAmount, true),
                SubsidyTotal = await formatAsync(subsidyTotal, true),
                CreditNoteTotal = await formatAsync(settlement.CreditNoteAmount, true),
                PayableBalance = await formatAsync(order.OrderTotal),
                HasSubsidy = subsidyTotal > decimal.Zero,
                HasShippingSubsidy = settlement.SubsidyShippingAmount > decimal.Zero,
                HasCreditNotes = settlement.CreditNoteAmount > decimal.Zero,
            };
        }

        return model;
    }

    #endregion
}