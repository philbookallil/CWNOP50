using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Core.Domain.Vendors;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Attributes;
using Nop.Services.Blogs;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Tax;
using System.Net;
using System.Text;

namespace Nop.Plugin.CW.Core.Services.Messages;

/// <summary>
/// Override class for NOP service <see cref="MessageTokenProvider"/>
/// </summary>
public class MessageTokenProviderOverride : MessageTokenProvider, IMessageTokenProviderOverride
{
    #region Fields

    protected readonly IExchangeService _exchangeService;
    protected readonly ICWOrderService _cwOrderService;

    #endregion

    #region Ctor

    public MessageTokenProviderOverride(CatalogSettings catalogSettings,
            CurrencySettings currencySettings,
            IAddressService addressService, IAttributeFormatter<AddressAttribute,
            AddressAttributeValue> addressAttributeFormatter,
            IAttributeFormatter<CustomerAttribute, CustomerAttributeValue> customerAttributeFormatter,
            IAttributeFormatter<VendorAttribute,
            VendorAttributeValue> vendorAttributeFormatter,
            IBlogService blogService,
            ICountryService countryService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IDateTimeHelper dateTimeHelper,
            IEventPublisher eventPublisher,
            IGenericAttributeService genericAttributeService,
            IGiftCardService giftCardService,
            IHtmlFormatter htmlFormatter,
            IHttpContextAccessor httpContextAccessor,
            ILanguageService languageService,
            ILocalizationService localizationService,
            ILogger logger,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            IPaymentService paymentService,
            IPriceFormatter priceFormatter,
            IProductAttributeFormatter productAttributeFormatter,
            IProductService productService,
            IRewardPointService rewardPointService,
            IShipmentService shipmentService,
            IShoppingCartService shoppingCartService,
            IStateProvinceService stateProvinceService,
            IStoreContext storeContext,
            IStoreService storeService,
            ITaxService taxService,
            IUrlRecordService urlRecordService,
            IWorkContext workContext,
            LinkGenerator linkGenerator,
            MessageTemplatesSettings templatesSettings,
            OrderSettings orderSettings,
            PaymentSettings paymentSettings,
            StoreInformationSettings storeInformationSettings,
            TaxSettings taxSettings,
            IExchangeService exchangeService,
            ICWOrderService cwOrderService) : base(catalogSettings,
                currencySettings,
                addressService,
                addressAttributeFormatter,
                customerAttributeFormatter,
                vendorAttributeFormatter,
                blogService,
                countryService,
                currencyService,
                customerService,
                dateTimeHelper,
                eventPublisher,
                genericAttributeService,
                giftCardService,
                htmlFormatter,
                httpContextAccessor,
                languageService,
                localizationService,
                logger,
                orderService,
                paymentPluginManager,
                paymentService,
                priceFormatter,
                productAttributeFormatter,
                productService,
                rewardPointService,
                shipmentService,
                shoppingCartService,
                stateProvinceService,
                storeContext,
                storeService,
                taxService,
                urlRecordService,
                workContext,
                linkGenerator,
                templatesSettings,
                orderSettings,
                paymentSettings,
                storeInformationSettings,
                taxSettings)
    {
        _exchangeService = exchangeService;
        _cwOrderService = cwOrderService;
    }

    #endregion

    #region Utilities

    protected async Task WriteExchangeFeesTotalsAsync(Order order, int languageId, StringBuilder sb)
    {
        var orderExchangeFees = await _exchangeService.GetExchangeFeesAmountsAsync(order);
        foreach (var exchangeFee in orderExchangeFees)
        {
            var feeTypeName = ExchangesHelper.DetermineFeeTypeName(exchangeFee.Key);
            var feeAmount = _currencyService.ConvertCurrency(exchangeFee.Value, order.CurrencyRate);
            var feeAmountStr = await _priceFormatter.FormatPriceAsync(feeAmount, true, order.CustomerCurrencyCode, languageId, true);
            sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4 em;\"><strong>{feeTypeName}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4 em;\"><strong>{feeAmountStr}</strong></td></tr>");
        }
    }

    /// <summary>
    /// Appends one totals row in the shape of nop's own: the title in the middle cell, the value on the right.
    /// </summary>
    protected virtual void AppendTotalsRow(StringBuilder sb, string title, string value)
    {
        sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{title}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{value}</strong></td></tr>");
    }

    /// <summary>
    /// Writes the settlement rows of an order subsidy or store credit paid for: the total before them, the subsidy
    /// that paid for goods and for shipping, and the store credit applied.
    /// </summary>
    protected virtual async Task WriteSettlementTotalsAsync(Order order, OrderSettlement settlement, int languageId, StringBuilder sb)
    {
        async Task<string> formatAsync(decimal amount, bool negative)
        {
            var amountInCustomerCurrency = _currencyService.ConvertCurrency(amount, order.CurrencyRate);

            return await _priceFormatter.FormatPriceAsync(negative ? -amountInCustomerCurrency : amountInCustomerCurrency, true, order.CustomerCurrencyCode, false, languageId);
        }

        AppendTotalsRow(sb, await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Order.SettleableTotal", languageId), await formatAsync(settlement.SettleableAmount, false));

        if (settlement.SubsidyStockAmount > decimal.Zero)
            AppendTotalsRow(sb, await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Order.SubsidyForStock", languageId), await formatAsync(settlement.SubsidyStockAmount, true));

        if (settlement.SubsidyShippingAmount > decimal.Zero)
            AppendTotalsRow(sb, await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Order.SubsidyForShipping", languageId), await formatAsync(settlement.SubsidyShippingAmount, true));

        if (settlement.CreditNoteAmount > decimal.Zero)
            AppendTotalsRow(sb, await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Order.CreditNotes", languageId), await formatAsync(settlement.CreditNoteAmount, true));
    }

    /// <summary>
    /// Render one delivery-address block in the exact shape nop's default order templates use, so a
    /// single-destination order's email looks as it does today.
    /// </summary>
    /// <remarks>
    /// Every line is emitted whether or not it has content, blank ones included, because that is what the
    /// token-driven template does. Skipping empties would quietly re-lay-out every ordinary order email in
    /// the shop, which is the one thing this change must not do.
    /// </remarks>
    protected virtual async Task AppendShippingDestinationBlockAsync(StringBuilder sb,
        Address address,
        string shippingMethod,
        IList<string> packNames,
        string addressLabel,
        string methodLabel,
        int languageId)
    {
        //The heading is the only place the packnames appear. A single-destination or non-packname order
        //has none, so the heading is nop's own "Shipping Address" and nothing changes.
        sb.Append(WebUtility.HtmlEncode(addressLabel));

        if (packNames?.Count > 0)
            sb.Append($" ({WebUtility.HtmlEncode(string.Join(", ", packNames))})");

        sb.Append("<br />");

        var stateProvinceName = string.Empty;
        var countryName = string.Empty;

        if (address != null)
        {
            if (await _stateProvinceService.GetStateProvinceByAddressAsync(address) is StateProvince stateProvince)
                stateProvinceName = await _localizationService.GetLocalizedAsync(stateProvince, x => x.Name, languageId);

            if (await _countryService.GetCountryByAddressAsync(address) is Country country)
                countryName = await _localizationService.GetLocalizedAsync(country, x => x.Name, languageId);
        }

        sb.Append($"{WebUtility.HtmlEncode($"{address?.FirstName} {address?.LastName}".Trim())}<br />");
        sb.Append($"{WebUtility.HtmlEncode(address?.Address1 ?? string.Empty)}<br />");
        sb.Append($"{WebUtility.HtmlEncode(address?.Address2 ?? string.Empty)}<br />");
        sb.Append($"{WebUtility.HtmlEncode($"{address?.City} {address?.ZipPostalCode}".Trim())}<br />");
        sb.Append($"{WebUtility.HtmlEncode($"{stateProvinceName} {countryName}".Trim())}<br />");
        sb.Append("<br />");
        sb.Append($"{WebUtility.HtmlEncode(methodLabel)}: {WebUtility.HtmlEncode(shippingMethod ?? string.Empty)}<br />");
        sb.Append("<br />");
    }

    /// <summary>
    /// Build the whole delivery-address section of an order email: one block per destination on a
    /// packname order, the order's single address on every other.
    /// </summary>
    /// <remarks>
    /// This is what replaces nop's <c>%if (%Order.Shippable%) … endif%</c> block in the eight order-level
    /// templates. The emptiness rule is nop's own — the block appears when <c>Order.ShippingMethod</c> is
    /// set — extended to appear when the order-level method is blank
    /// but a destination has one, which can only happen on a packname order.
    /// </remarks>
    protected virtual async Task<string> BuildShippingDestinationsBlockAsync(Order order, int languageId, int vendorId)
    {
        var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(order.Id);

        //A vendor sees only their own products, so they must see only the branches those go to. None of
        //the eight templates is vendor-scoped today; this is here so that pasting the token into one
        //later cannot leak another vendor's delivery addresses.
        if (vendorId > 0 && destinations.Count > 0)
        {
            var vendorOrderItemIds = (await _orderService.GetOrderItemsAsync(order.Id, vendorId: vendorId))
                .Select(item => item.Id)
                .ToHashSet();

            destinations = destinations
                .Where(destination => destination.OrderItemIds.Any(vendorOrderItemIds.Contains))
                .ToList();
        }

        var shippable = !string.IsNullOrEmpty(order.ShippingMethod)
            || destinations.Any(destination => !string.IsNullOrEmpty(destination.ShippingMethod));

        if (!shippable)
            return string.Empty;

        var addressLabel = await _localizationService
            .GetResourceAsync("Plugins.CW.Core.Message.Order.ShippingAddress", languageId);

        var methodLabel = await _localizationService
            .GetResourceAsync("Plugins.CW.Core.Message.Order.ShippingMethod", languageId);

        var sb = new StringBuilder();

        //Not a packname order — nop's single address, resolved exactly as nop's own orderAddress helper
        //does: the PICKUP address on a pickup order.
        if (destinations.Count == 0)
        {
            var orderAddressId = (order.PickupInStore ? order.PickupAddressId : order.ShippingAddressId) ?? 0;

            await AppendShippingDestinationBlockAsync(sb,
                await _addressService.GetAddressByIdAsync(orderAddressId),
                order.ShippingMethod,
                null,
                addressLabel,
                methodLabel,
                languageId);

            return sb.ToString();
        }

        foreach (var destination in destinations)
        {
            //An order is entirely collected or entirely delivered, so a pickup
            //order keeps the order-level pickup address for every destination on it.
            var addressId = order.PickupInStore ? order.PickupAddressId : destination.ShippingAddressId;

            await AppendShippingDestinationBlockAsync(sb,
                addressId.HasValue ? await _addressService.GetAddressByIdAsync(addressId.Value) : null,
                string.IsNullOrEmpty(destination.ShippingMethod) ? order.ShippingMethod : destination.ShippingMethod,
                destination.PackNames,
                addressLabel,
                methodLabel,
                languageId);
        }

        return sb.ToString();
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Get token groups of message template
    /// </summary>
    /// <param name="messageTemplate">Message template</param>
    /// <returns>Collection of token group names</returns>
    public override IEnumerable<string> GetTokenGroups(MessageTemplate messageTemplate)
    {
        switch (messageTemplate.Name)
        {
            case CWMessageTemplateSystemNames.ReturnReceipt:
            case CWMessageTemplateSystemNames.ReturnAuthorisedExceptedCustomer:
                return new[]
                {
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.OrderTokens,
                    TokenGroupNames.CustomerTokens,
                    TokenGroupNames.ReturnRequestTokens
                };

            case CWMessageTemplateSystemNames.ReturnAuthorisedPaymentCustomer:
                return new[]
                {
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.OrderTokens,
                    TokenGroupNames.CustomerTokens,
                    TokenGroupNames.ReturnRequestTokens,
                    CWTokenGroupNames.ExchangeReturnsTokens
                };
            case CWMessageTemplateSystemNames.CustomerRegistrationImport:
                return new[]
                {
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.CustomerTokens,
                    CWTokenGroupNames.SubsidyTokens
                };
            default:
                return base.GetTokenGroups(messageTemplate);
        }
    }

    /// <summary>
    /// Adds shipment-scoped delivery-address tokens, so a shipment email can name the branch that parcel
    /// actually went to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three shipment templates print the delivery address from <c>%Order.Shipping…%</c>, and on a
    /// packname order that is the first packname's address — a placeholder. <b>Pre-empting those order
    /// tokens is not possible</b>: <c>SendShipmentSentCustomerNotificationAsync</c> calls
    /// <c>AddShipmentTokensAsync</c> and then <c>AddOrderTokensAsync</c>, so whatever we wrote would be
    /// overwritten moments later. New tokens, and the templates changed to use them, is the only route.
    /// </para>
    /// <para>
    /// The tokens mirror the seven the templates already use, plus company and method, so swapping them in
    /// a template is a find and replace. On a non-packname order they resolve to the order's own address,
    /// so they are safe to use on any template rather than only these three.
    /// </para>
    /// </remarks>
    public override async Task AddShipmentTokensAsync(IList<Token> tokens, Shipment shipment, int languageId)
    {
        await base.AddShipmentTokensAsync(tokens, shipment, languageId);

        var order = await _orderService.GetOrderByIdAsync(shipment.OrderId);

        if (order == null)
            return;

        var destination = await _cwOrderService.GetShipmentDestinationAsync(shipment.Id, order.Id);

        //Mirrors nop's own orderAddress helper: the %Order.Shipping*%
        //tokens resolve to the PICKUP address on a pickup order, so these have to as well or the
        //"ready for pickup" email loses the address it exists to communicate. A packname destination is
        //delivery-only, so it takes precedence when there is one.
        var addressId = destination?.ShippingAddressId
            ?? (order.PickupInStore ? order.PickupAddressId : order.ShippingAddressId);
        var shippingMethod = string.IsNullOrEmpty(destination?.ShippingMethod)
            ? order.ShippingMethod
            : destination.ShippingMethod;

        var address = addressId.HasValue
            ? await _addressService.GetAddressByIdAsync(addressId.Value)
            : null;

        var stateProvince = address == null
            ? null
            : await _stateProvinceService.GetStateProvinceByAddressAsync(address);

        var country = address == null
            ? null
            : await _countryService.GetCountryByAddressAsync(address);

        tokens.Add(new Token("Shipment.ShippingMethod", shippingMethod ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingFirstName", address?.FirstName ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingLastName", address?.LastName ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingCompany", address?.Company ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingAddress1", address?.Address1 ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingAddress2", address?.Address2 ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingCity", address?.City ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingZipPostalCode", address?.ZipPostalCode ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingStateProvince",
            stateProvince == null ? string.Empty : await _localizationService.GetLocalizedAsync(stateProvince, x => x.Name, languageId)));
        tokens.Add(new Token("Shipment.ShippingCountry",
            country == null ? string.Empty : await _localizationService.GetLocalizedAsync(country, x => x.Name, languageId)));
        tokens.Add(new Token("Shipment.ShippingPhoneNumber", address?.PhoneNumber ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingEmail", address?.Email ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingFaxNumber", address?.FaxNumber ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingCounty", address?.County ?? string.Empty));
        tokens.Add(new Token("Shipment.ShippingCustomAttributes",
            await _addressAttributeFormatter.FormatAttributesAsync(address?.CustomAttributes ?? string.Empty), true));
        tokens.Add(new Token("Shipment.ShippingAddressLine",
            address == null ? string.Empty : (await _addressService.FormatAddressAsync(address, languageId)).Item1));
    }

    /// <summary>
    /// Adds <c>%Order.ShippingDestinations%</c> — the order's whole delivery-address section, listing
    /// every destination on a packname order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The eight order-level templates printed one address block built from <c>%Order.Shipping…%</c>, and
    /// on a packname order that is the first packname's address — a placeholder, the same defect the
    /// packing slip and the invoice already had. Unlike the shipment emails there is nothing
    /// shipment-scoped to point them at, because an order-placed email has no shipment: the section itself
    /// has to become a list, so it becomes one token.
    /// </para>
    /// <para>
    /// nop's individual <c>%Order.Shipping…%</c> tokens are left exactly as nop sets them, so any template
    /// still using them behaves as before.
    /// </para>
    /// </remarks>
    public override async Task AddOrderTokensAsync(IList<Token> tokens, Order order, int languageId, int vendorId = 0)
    {
        await base.AddOrderTokensAsync(tokens, order, languageId, vendorId);

        //neverHtmlEncoded, because the value is markup — which is why every field inside it is encoded by
        //hand as it is written.
        tokens.Add(new Token("Order.ShippingDestinations",
            await BuildShippingDestinationsBlockAsync(order, languageId, vendorId), true));
    }

    /// <summary>
    /// Write order totals
    /// </summary>
    /// <param name="order">Order</param>
    /// <param name="language">Language</param>
    /// <param name="sb">StringBuilder</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task WriteTotalsAsync(Order order, Language language, StringBuilder sb)
    {
        //subtotal
        string cusSubTotal;
        var displaySubTotalDiscount = false;
        var cusSubTotalDiscount = string.Empty;
        var languageId = language.Id;
        if (order.CustomerTaxDisplayType == TaxDisplayType.IncludingTax && !_taxSettings.ForceTaxExclusionFromOrderSubtotal)
        {
            //including tax

            //subtotal
            var orderSubtotalInclTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderSubtotalInclTax, order.CurrencyRate);
            cusSubTotal = await _priceFormatter.FormatPriceAsync(orderSubtotalInclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, true);
            //discount (applied to order subtotal)
            var orderSubTotalDiscountInclTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderSubTotalDiscountInclTax, order.CurrencyRate);
            if (orderSubTotalDiscountInclTaxInCustomerCurrency > decimal.Zero)
            {
                cusSubTotalDiscount = await _priceFormatter.FormatPriceAsync(-orderSubTotalDiscountInclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, true);
                displaySubTotalDiscount = true;
            }
        }
        else
        {
            //excluding tax

            //subtotal
            var orderSubtotalExclTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderSubtotalExclTax, order.CurrencyRate);
            cusSubTotal = await _priceFormatter.FormatPriceAsync(orderSubtotalExclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, false);
            //discount (applied to order subtotal)
            var orderSubTotalDiscountExclTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderSubTotalDiscountExclTax, order.CurrencyRate);
            if (orderSubTotalDiscountExclTaxInCustomerCurrency > decimal.Zero)
            {
                cusSubTotalDiscount = await _priceFormatter.FormatPriceAsync(-orderSubTotalDiscountExclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, false);
                displaySubTotalDiscount = true;
            }
        }

        //shipping, payment method fee
        string cusShipTotal;
        string cusPaymentMethodAdditionalFee;
        var taxRates = new SortedDictionary<decimal, decimal>();
        var cusTaxTotal = string.Empty;
        var cusDiscount = string.Empty;
        if (order.CustomerTaxDisplayType == TaxDisplayType.IncludingTax)
        {
            //including tax

            //shipping
            var orderShippingInclTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderShippingInclTax, order.CurrencyRate);
            cusShipTotal = await _priceFormatter.FormatShippingPriceAsync(orderShippingInclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, true);
            //payment method additional fee
            var paymentMethodAdditionalFeeInclTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.PaymentMethodAdditionalFeeInclTax, order.CurrencyRate);
            cusPaymentMethodAdditionalFee = await _priceFormatter.FormatPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFeeInclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, true);
        }
        else
        {
            //excluding tax

            //shipping
            var orderShippingExclTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderShippingExclTax, order.CurrencyRate);
            cusShipTotal = await _priceFormatter.FormatShippingPriceAsync(orderShippingExclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, false);
            //payment method additional fee
            var paymentMethodAdditionalFeeExclTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.PaymentMethodAdditionalFeeExclTax, order.CurrencyRate);
            cusPaymentMethodAdditionalFee = await _priceFormatter.FormatPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFeeExclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, false);
        }

        //shipping
        var displayShipping = order.ShippingStatus != ShippingStatus.ShippingNotRequired;

        //payment method fee
        var displayPaymentMethodFee = order.PaymentMethodAdditionalFeeExclTax > decimal.Zero;

        //tax
        bool displayTax;
        bool displayTaxRates;
        if (_taxSettings.HideTaxInOrderSummary && order.CustomerTaxDisplayType == TaxDisplayType.IncludingTax)
        {
            displayTax = false;
            displayTaxRates = false;
        }
        else
        {
            if (order.OrderTax == 0 && _taxSettings.HideZeroTax)
            {
                displayTax = false;
                displayTaxRates = false;
            }
            else
            {
                taxRates = new SortedDictionary<decimal, decimal>();
                foreach (var tr in _orderService.ParseTaxRates(order, order.TaxRates))
                    taxRates.Add(tr.Key, _currencyService.ConvertCurrency(tr.Value, order.CurrencyRate));

                displayTaxRates = _taxSettings.DisplayTaxRates && taxRates.Any();
                displayTax = !displayTaxRates;

                var orderTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderTax, order.CurrencyRate);
                var taxStr = await _priceFormatter.FormatPriceAsync(orderTaxInCustomerCurrency, true, order.CustomerCurrencyCode,
                    false, languageId);
                cusTaxTotal = taxStr;
            }
        }

        //discount
        var displayDiscount = false;
        if (order.OrderDiscount > decimal.Zero)
        {
            var orderDiscountInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderDiscount, order.CurrencyRate);
            cusDiscount = await _priceFormatter.FormatPriceAsync(-orderDiscountInCustomerCurrency, true, order.CustomerCurrencyCode, false, languageId);
            displayDiscount = true;
        }

        //total
        var orderTotalInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderTotal, order.CurrencyRate);
        var cusTotal = await _priceFormatter.FormatPriceAsync(orderTotalInCustomerCurrency, true, order.CustomerCurrencyCode, false, languageId);

        //a packname order subsidy or store credit paid for is laid out in the order the money moved, as on the order
        //page and the invoice: the total before them, what they covered, then the customer's own discounts and fee.
        //A plain order keeps nop's layout: its store credit settled nop's total, after nop's own deductions
        var settlement = await _cwOrderService.GetOrderSettlementAsync(order.Id);
        var packNameSettlement = settlement != null && await _cwOrderService.IsPackNameOrderAsync(order.Id) ? settlement : null;

        //subtotal
        sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{await _localizationService.GetResourceAsync("Messages.Order.SubTotal", languageId)}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{cusSubTotal}</strong></td></tr>");

        //discount (applied to order subtotal)
        if (displaySubTotalDiscount && packNameSettlement == null)
        {
            sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{await _localizationService.GetResourceAsync("Messages.Order.SubTotalDiscount", languageId)}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{cusSubTotalDiscount}</strong></td></tr>");
        }

        //shipping
        if (displayShipping)
        {
            sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{await _localizationService.GetResourceAsync("Messages.Order.Shipping", languageId)}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{cusShipTotal}</strong></td></tr>");
        }

        //payment method fee
        if (displayPaymentMethodFee && packNameSettlement == null)
        {
            var paymentMethodFeeTitle = await _localizationService.GetResourceAsync("Messages.Order.PaymentMethodAdditionalFee", languageId);
            sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{paymentMethodFeeTitle}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{cusPaymentMethodAdditionalFee}</strong></td></tr>");
        }

        //tax
        if (displayTax)
        {
            sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{await _localizationService.GetResourceAsync("Messages.Order.Tax", languageId)}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{cusTaxTotal}</strong></td></tr>");
        }

        if (displayTaxRates)
        {
            foreach (var item in taxRates)
            {
                var taxRate = string.Format(await _localizationService.GetResourceAsync("Messages.Order.TaxRateLine"),
                    _priceFormatter.FormatTaxRate(item.Key));
                var taxValue = await _priceFormatter.FormatPriceAsync(item.Value, true, order.CustomerCurrencyCode, false, languageId);
                sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{taxRate}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{taxValue}</strong></td></tr>");
            }
        }

        if (packNameSettlement != null)
        {
            // What subsidy and store credit covered, then the customer's own discount and fee
            await WriteSettlementTotalsAsync(order, packNameSettlement, languageId, sb);

            if (displaySubTotalDiscount)
                AppendTotalsRow(sb, await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Order.SubTotalDiscount", languageId), cusSubTotalDiscount);

            if (displayPaymentMethodFee)
                AppendTotalsRow(sb, await _localizationService.GetResourceAsync("Messages.Order.PaymentMethodAdditionalFee", languageId), cusPaymentMethodAdditionalFee);
        }

        //discount
        if (displayDiscount)
        {
            sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{await _localizationService.GetResourceAsync(packNameSettlement == null ? "Messages.Order.TotalDiscount" : "Plugins.CW.Core.Messages.Order.OrderDiscount", languageId)}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{cusDiscount}</strong></td></tr>");
        }

        //gift cards
        foreach (var gcuh in await _giftCardService.GetGiftCardUsageHistoryAsync(order))
        {
            var giftCardText = string.Format(await _localizationService.GetResourceAsync("Messages.Order.GiftCardInfo", languageId),
                WebUtility.HtmlEncode((await _giftCardService.GetGiftCardByIdAsync(gcuh.GiftCardId))?.GiftCardCouponCode));
            var giftCardAmount = await _priceFormatter.FormatPriceAsync(-_currencyService.ConvertCurrency(gcuh.UsedValue, order.CurrencyRate), true, order.CustomerCurrencyCode,
                false, languageId);
            sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{giftCardText}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{giftCardAmount}</strong></td></tr>");
        }

        //reward points
        if (order.RedeemedRewardPointsEntryId.HasValue && await _rewardPointService.GetRewardPointsHistoryEntryByIdAsync(order.RedeemedRewardPointsEntryId.Value) is RewardPointsHistory redeemedRewardPointsEntry)
        {
            var rpTitle = string.Format(await _localizationService.GetResourceAsync("Messages.Order.RewardPoints", languageId),
                -redeemedRewardPointsEntry.Points);
            var rpAmount = await _priceFormatter.FormatPriceAsync(-_currencyService.ConvertCurrency(redeemedRewardPointsEntry.UsedAmount, order.CurrencyRate), true,
                order.CustomerCurrencyCode, false, languageId);
            sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{rpTitle}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{rpAmount}</strong></td></tr>");
        }

        //a plain order's store credit settled nop's total, so it follows nop's own deductions
        if (packNameSettlement == null && settlement?.CreditNoteAmount > decimal.Zero)
            AppendTotalsRow(sb, await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Order.CreditNotes", languageId),
                await _priceFormatter.FormatPriceAsync(-_currencyService.ConvertCurrency(settlement.CreditNoteAmount, order.CurrencyRate), true, order.CustomerCurrencyCode, false, languageId));

        // Exchange fees: always cash, added after every deduction
        await WriteExchangeFeesTotalsAsync(order, languageId, sb);

        //total
        sb.AppendLine($"<tr style=\"text-align:right;\"><td>&nbsp;</td><td colspan=\"2\" style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{await _localizationService.GetResourceAsync(packNameSettlement == null ? "Messages.Order.OrderTotal" : "Plugins.CW.Core.Messages.Order.PayableBalance", languageId)}</strong></td> <td style=\"background-color: {_templatesSettings.Color3};padding:0.6em 0.4em;\"><strong>{cusTotal}</strong></td></tr>");
    }

    #endregion

    #region Methods

    public async Task AddExchangeReturnTokensAsync(IList<Token> tokens, ReturnRequest returnRequest, decimal pendingAmount, ReturnRequestItem returnRequestItem = null)
    {
        tokens.Add(new Token("ReturnRequest.PendingAmount", await _priceFormatter.FormatPriceAsync(pendingAmount)));

        if (returnRequest != null && _exchangeService != null)
        {
            //The caller's instance wins, so the email quotes the figures the workflow actually priced against
            //rather than whatever a second read returns. Since the revert the controller commits the staff
            //quantities and ad-hoc fee BEFORE the workflow runs, so the two agree — this is now about not
            //re-reading the same row, not about outrunning a commit.
            returnRequestItem ??= await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);

            if (returnRequestItem != null)
            {
                var reshippingFee = decimal.Zero;
                var repackagingFee = decimal.Zero;
                var additionalFeeAmount = decimal.Zero;
                var additionalFeeName = string.Empty;

                var appliedFees = await _exchangeService.GetExchangeFeesAmountsAsync(returnRequestItem);

                if (appliedFees.ContainsKey(FeeType.RepackagingFee))
                {
                    repackagingFee = appliedFees[FeeType.RepackagingFee];
                }

                if (appliedFees.ContainsKey(FeeType.ReShippingFee))
                {
                    reshippingFee = appliedFees[FeeType.ReShippingFee];
                }

                if (!string.IsNullOrEmpty(returnRequestItem.AdditionalFeeName) && appliedFees.ContainsKey(FeeType.AdditionalFee))
                {
                    additionalFeeAmount = appliedFees[FeeType.AdditionalFee];
                    additionalFeeName = returnRequestItem.AdditionalFeeName;
                }

                tokens.Add(new Token("ReturnRequest.ReShippingFee", await _priceFormatter.FormatPriceAsync(reshippingFee)));
                tokens.Add(new Token("ReturnRequest.RePackagingFee", await _priceFormatter.FormatPriceAsync(repackagingFee)));
                tokens.Add(new Token("ReturnRequest.AdditionalFeeName", additionalFeeName));
                tokens.Add(new Token("ReturnRequest.AdditionalFeeAmount", await _priceFormatter.FormatPriceAsync(additionalFeeAmount)));
            }
        }
    }

    #endregion
}