using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Exchanges;

public class ExchangesDisplayService : IExchangesDisplayService
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IProductService _productService;
    protected readonly IProductAttributeService _productAttributeService;
    protected readonly IProductAttributeParser _productAttributeParser;
    protected readonly ILogger _logger;
    protected readonly IPictureService _pictureService;
    protected readonly IOrderService _orderService;
    protected readonly ICurrencyService _currencyService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IWorkContext _workContext;
    protected readonly ILocalizationService _localizationService;

    #endregion

    #region Ctor

    public ExchangesDisplayService(ICustomerService customerService,
        IReturnRequestService returnRequestService,
        IProductAttributeService productAttributeService,
        IProductAttributeParser productAttributeParser,
        ILogger logger,
        IProductService productService,
        ICWOrderService cwOrderService,
        IPictureService pictureService,
        IOrderService orderService,
        ICurrencyService currencyService,
        IPriceFormatter priceFormatter,
        IWorkContext workContext,
        ILocalizationService localizationService)
    {
        _customerService = customerService;
        _returnRequestService = returnRequestService;
        _productAttributeService = productAttributeService;
        _productAttributeParser = productAttributeParser;
        _logger = logger;
        _productService = productService;
        _cwOrderService = cwOrderService;
        _pictureService = pictureService;
        _orderService = orderService;
        _currencyService = currencyService;
        _priceFormatter = priceFormatter;
        _workContext = workContext;
        _localizationService = localizationService;
    }

    #endregion

    #region Utilities

    #endregion

    #region Methods

    /// <summary>
    /// Formats an amount kept in the primary store currency in the customer's working currency, a minus sign before a
    /// negative one
    /// </summary>
    /// <param name="amount">Amount in the primary store currency</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the formatted amount</returns>
    public virtual async Task<string> FormatPriceAsync(decimal amount)
    {
        var converted = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(Math.Abs(amount), await _workContext.GetWorkingCurrencyAsync());

        return (amount < decimal.Zero ? "-" : string.Empty) + await _priceFormatter.FormatPriceAsync(converted, true, false);
    }

    /// <summary>
    /// Formats an amount kept in the primary store currency in an order's currency, at the order's rate, a minus sign
    /// before a negative one
    /// </summary>
    /// <param name="amount">Amount in the primary store currency</param>
    /// <param name="order">Order</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the formatted amount</returns>
    public virtual async Task<string> FormatOrderPriceAsync(decimal amount, Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var converted = _currencyService.ConvertCurrency(Math.Abs(amount), order.CurrencyRate);
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        return (amount < decimal.Zero ? "-" : string.Empty)
            + await _priceFormatter.FormatPriceAsync(converted, true, order.CustomerCurrencyCode, false, languageId);
    }

    /// <summary>
    /// The customer-facing name of a fee type
    /// </summary>
    public virtual Task<string> GetFeeTypeNameAsync(FeeType feeType) => _localizationService.GetLocalizedEnumAsync(feeType);

    /// <summary>
    /// The customer-facing name of an exchange type
    /// </summary>
    public virtual Task<string> GetExchangeTypeNameAsync(ExchangeType exchangeType) => _localizationService.GetLocalizedEnumAsync(exchangeType);

    /// <summary>
    /// The customer-facing name of a return mode
    /// </summary>
    public virtual Task<string> GetExchangeModeNameAsync(ExchangeReturnMode exchangeMode) => _localizationService.GetLocalizedEnumAsync(exchangeMode);

    /// <summary>
    /// Extra display data for a return request, when the caller has only the id.
    /// Prefer the overload taking the entities when they are already loaded.
    /// </summary>
    /// <param name="returnRequestId">Return request identifier</param>
    /// <returns>The extra info, or null when the return request no longer exists</returns>
    public async Task<ReturnRequestExtraInfo> GetReturnRequestExtraInfoAsync(int returnRequestId)
    {
        var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(returnRequestId);

        if (returnRequest == null)
            return null;

        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);

        return await GetReturnRequestExtraInfoAsync(returnRequest, orderItem);
    }

    /// <summary>
    /// Extra display data for a return request, built from entities the caller already holds.
    /// Callers rendering a list should use this - the id overload re-reads the return request
    /// and order item they already have.
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The order item being returned</param>
    /// <returns>The extra info, or null when the return request is null</returns>
    public async Task<ReturnRequestExtraInfo> GetReturnRequestExtraInfoAsync(ReturnRequest returnRequest, OrderItem orderItem)
    {
        if (returnRequest == null)
            return null;

        if (orderItem == null)
        {
            // Expected on an old return whose order item was removed - not exceptional
            await _logger.WarningAsync($"[Returns] Order item {returnRequest.OrderItemId} for return request {returnRequest.Id} no longer exists.");
            return null;
        }

        var product = await _productService.GetProductByIdAsync(orderItem.ProductId);
        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        return new ReturnRequestExtraInfo
        {
            CustomerName = await _customerService.GetCustomerFullNameAsync(customer),
            OrderId = orderItem.OrderId,
            ReturnedItemAttributes = orderItem.AttributeDescription,
            ReturnedItemSku = product.Sku
        };
    }

    public virtual async Task<string> GetProductThumbUrlAsync(int productId, string attributesXml, int pictureSize)
    {
        var pictureId = 0;

        if (!string.IsNullOrEmpty(attributesXml))
            foreach (var value in await _productAttributeParser.ParseProductAttributeValuesAsync(attributesXml))
            {
                var valuePicture = (await _productAttributeService.GetProductAttributeValuePicturesAsync(value.Id))
                    .FirstOrDefault();

                if (valuePicture == null)
                    continue;

                pictureId = valuePicture.PictureId;
                break;
            }

        if (pictureId == 0)
            pictureId = (await _pictureService.GetPicturesByProductIdAsync(productId, 1)).FirstOrDefault()?.Id ?? 0;

        // GetPictureUrlAsync(0, ...) returns the store's default image, so a missing picture
        // degrades to a placeholder rather than a broken img
        return await _pictureService.GetPictureUrlAsync(pictureId, pictureSize);
    }

    /// <summary>
    /// The packname assigned to an order item, or null when the store does not use packnames
    /// or none was assigned. Callers decide how to present absence - never render a placeholder.
    /// </summary>
    /// <param name="orderItemId">Order item identifier</param>
    /// <returns>The packname, or null</returns>
    public virtual async Task<string> GetPacknameAsync(int orderItemId)
    {
        var packNameOrderItem = await _cwOrderService.GetPackNameOrderItemAsync(orderItemId);

        return !string.IsNullOrEmpty(packNameOrderItem?.PackName) ? packNameOrderItem.PackName : null;
    }

    /// <summary>
    /// Gets the selected attribute value id for every attribute in the given XML,
    /// keyed by attribute id. Used to detect whether a new selection differs.
    /// </summary>
    public async Task<IDictionary<string, string>> GetSelectedAttributeMapAsync(string attributesXml)
    {
        var map = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(attributesXml))
            return map;

        foreach (var attribute in await _productAttributeParser.ParseProductAttributeMappingsAsync(attributesXml))
        {
            var values = await _productAttributeParser.ParseProductAttributeValuesAsync(attributesXml, attribute.Id);
            var value = values.FirstOrDefault();

            if (value != null)
                map[attribute.Id.ToString()] = value.Id.ToString();
        }

        return map;
    }

    #endregion
}
