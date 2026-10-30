using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Nop.Web.Models.Order;
using System.Text.Json;

namespace Nop.Plugin.CW.Core.Factories;

public class ReturnRequestItemModelFactory : IReturnRequestItemModelFactory
{
    #region Fields

    protected readonly IExchangeService _exchangeService;
    protected readonly IProductService _productService;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly IOrderService _orderService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IDownloadService _downloadService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly IUrlRecordService _urlRecordService;
    protected readonly IStoreContext _storeContext;
    protected readonly ISettingService _settingService;
    protected readonly IExchangeMessageService _exchangeMessageService;
    protected readonly ILogger _logger;
    protected readonly IExchangesDisplayService _exchangesDisplayService;
    protected readonly IWorkContext _workContext;
    protected readonly ReturnRequestSettings _returnRequestSettings;

    #endregion

    #region Ctor

    public ReturnRequestItemModelFactory(IExchangeService exchangeService,
        IProductService productService,
        IReturnRequestService returnRequestService,
        IOrderService orderService,
        ILocalizationService localizationService,
        IDownloadService downloadService,
        IDateTimeHelper dateTimeHelper,
        IUrlRecordService urlRecordService,
        IStoreContext storeContext,
        ISettingService settingService,
        IExchangeMessageService exchangeMessageService,
        ILogger logger,
        IExchangesDisplayService exchangesDisplayService,
        IWorkContext workContext,
        ReturnRequestSettings returnRequestSettings)
    {
        _exchangeService = exchangeService;
        _productService = productService;
        _returnRequestService = returnRequestService;
        _orderService = orderService;
        _localizationService = localizationService;
        _downloadService = downloadService;
        _dateTimeHelper = dateTimeHelper;
        _urlRecordService = urlRecordService;
        _storeContext = storeContext;
        _settingService = settingService;
        _exchangeMessageService = exchangeMessageService;
        _logger = logger;
        _exchangesDisplayService = exchangesDisplayService;
        _workContext = workContext;
        _returnRequestSettings = returnRequestSettings;
    }

    #endregion

    #region Utilities

    protected async Task PrepareExchangeRequestModelAsync(CustomerExchangeRequestModel model, ReturnRequestItem returnRequestItem)
    {
        model.ExchangeType = returnRequestItem.ExchangeType;
        model.ExchangeTypeName = await _exchangesDisplayService.GetExchangeTypeNameAsync(returnRequestItem.ExchangeType);
        model.ExchangeReturnMode = returnRequestItem.ExchangeMode;

        var replacement = await _exchangeService.GetReplacementItemAsync(returnRequestItem.Id);
        if (replacement == null)
            return;

        var exchangeProduct = await _productService.GetProductByIdAsync(replacement.ProductId);
        if (exchangeProduct != null)
            model.ExchangeItemSku = exchangeProduct.Sku;

        model.ExchangeProductId = replacement.ProductId;
        model.ExchangeProductName = replacement.ProductName;
        model.ExchangeProductSeName = replacement.ProductSeName;
        model.ExchangeProductAttributesInfo = replacement.AttributesInfo;
        model.ExchangeProductAttributesXml = replacement.AttributesXml;
        model.ExchangeQuantity = replacement.Quantity;

        model.ExchangeProductPictureUrl = await _exchangesDisplayService.GetProductThumbUrlAsync(replacement.ProductId, replacement.AttributesXml, CWCoreDefaults.ReturnItemThumbSize);

        model.CurrentAttributesJson = JsonSerializer.Serialize(await _exchangesDisplayService.GetSelectedAttributeMapAsync(replacement.AttributesXml));
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepares the exchange detail for one return request.
    /// </summary>
    /// <param name="includeReceiptMessage">
    /// Only the confirmation panel displays the receipt message, and building it is expensive
    /// (message template + tokens). Leave false for list views.
    /// </param>
    public async Task<CustomerExchangeRequestModel> PrepareCustomerExchangeRequestModelAsync(CustomerReturnRequestsModel.ReturnRequestModel returnRequestModel, ReturnRequest returnRequest = null, OrderItem orderItem = null, bool includeReceiptMessage = false)
    {
        returnRequest ??= await _returnRequestService.GetReturnRequestByIdAsync(returnRequestModel.Id);

        var model = new CustomerExchangeRequestModel(returnRequestModel);

        if (returnRequest == null)
            return model;

        orderItem ??= await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);

        // Everything below reads from the order item. It can be gone on an old return, so return the
        // base detail rather than failing - one broken row must not blank the customer's whole list.
        if (orderItem == null)
        {
            await _logger.WarningAsync($"[Returns] Order item {returnRequest.OrderItemId} for return request {returnRequest.Id} no longer exists. Showing the return without its exchange detail.");
            return model;
        }

        model.OrderItemId = orderItem.Id;
        model.CustomOrderNumber = (await _orderService.GetOrderByIdAsync(orderItem.OrderId))?.CustomOrderNumber;

        var returnRequestItem = await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);

        if (returnRequestItem != null)
            await PrepareExchangeRequestModelAsync(model, returnRequestItem);

        if (includeReceiptMessage)
            model.ReceiptMessage = await _exchangeMessageService.GetExchangesReturnReceiptMessageAsync(returnRequest);

        model.Packname = await _exchangesDisplayService.GetPacknameAsync(orderItem.Id);
        model.ReturnedItemPictureUrl = await _exchangesDisplayService.GetProductThumbUrlAsync(orderItem.ProductId, orderItem.AttributesXml, CWCoreDefaults.ReturnItemThumbSize);
        model.AllowChangeReturnedItem = (returnRequest.ReturnRequestStatus == ReturnRequestStatus.Pending);

        var extraInfo = await _exchangesDisplayService.GetReturnRequestExtraInfoAsync(returnRequest, orderItem);

        if (extraInfo != null)
        {
            model.OrderId = extraInfo.OrderId;
            model.ReturnedItemSku = extraInfo.ReturnedItemSku;
            model.ReturnedItemAttributes = extraInfo.ReturnedItemAttributes;
        }

        return model;
    }

    public async Task<ReturnExchangesConfirmationModel> PrepareReturnExchangesConfirmationModelAsync(int[] returnRequestIds)
    {
        //only the customer's own return requests, as many as one confirmation shows
        var customer = await _workContext.GetCurrentCustomerAsync();
        var returnRequests = await _exchangeService.GetCustomerReturnRequestsByIdsAsync(customer.Id,
            (returnRequestIds ?? []).Distinct().Take(CWCoreDefaults.ReturnRequestsConfirmationMaxCount).ToArray());
        var model = new ReturnExchangesConfirmationModel
        {
            TitlePattern = await _localizationService.GetResourceAsync(_returnRequestSettings.UseEuWithdrawalLocales
                ? "Account.CustomerReturnRequests.Withdrawal.Title"
                : "Account.CustomerReturnRequests.Title")
        };

        var store = await _storeContext.GetCurrentStoreAsync();
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

        model.AllowCloseWithoutPrinting = storeSettings.AllowCloseWithoutPrintingReturnNote;

        foreach (var returnRequest in returnRequests)
        {
            var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);
            var product = orderItem == null ? null : await _productService.GetProductByIdAsync(orderItem.ProductId);

            if (product != null)
            {
                var download = await _downloadService.GetDownloadByIdAsync(returnRequest.UploadedFileId);

                var returnRequestModel = new CustomerReturnRequestsModel.ReturnRequestModel
                {
                    Id = returnRequest.Id,
                    CustomNumber = returnRequest.CustomNumber,
                    ReturnRequestStatus = await _localizationService.GetLocalizedEnumAsync(returnRequest.ReturnRequestStatus),
                    ProductId = product.Id,
                    ProductName = await _localizationService.GetLocalizedAsync(product, entity => entity.Name),
                    ProductSeName = await _urlRecordService.GetSeNameAsync(product),
                    Quantity = returnRequest.Quantity,
                    ReturnAction = returnRequest.RequestedAction,
                    ReturnReason = returnRequest.ReasonForReturn,
                    Comments = returnRequest.CustomerComments,
                    UploadedFileGuid = download != null ? download.DownloadGuid : Guid.Empty,
                    CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(returnRequest.CreatedOnUtc, DateTimeKind.Utc),
                };

                var exchangeRequestModel = await PrepareCustomerExchangeRequestModelAsync(returnRequestModel, returnRequest, orderItem, includeReceiptMessage: true);
                model.Items.Add(exchangeRequestModel);
            }
        }

        return model;
    }

    #endregion
}