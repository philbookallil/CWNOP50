using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.BusinessLogic;

public class ReturnSubmissionLogic : IReturnSubmissionLogic
{
    #region Fields

    protected readonly IOrderService _orderService;
    protected readonly ICustomerService _customerService;
    protected readonly IWorkContext _workContext;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly ILogger _logger;
    protected readonly IExchangeService _exchangeService;
    protected readonly IExchangeWorkflowMessageService _exchangeWorkflowMessageService;
    protected readonly ReturnRequestSettings _returnRequestSettings;
    protected readonly IDownloadService _downloadService;
    protected readonly IStoreContext _storeContext;
    protected readonly ILocalizationService _localizationService;
    protected readonly ICustomNumberFormatter _customNumberFormatter;
    protected readonly IWorkflowMessageService _workflowMessageService;
    protected readonly IReturnCaseService _returnCaseService;
    protected readonly LocalizationSettings _localizationSettings;
    protected readonly IProductService _productService;
    protected readonly IAclService _aclService;
    protected readonly IStoreMappingService _storeMappingService;
    protected readonly IProductAttributeService _productAttributeService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IPackNameService _packNameService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public ReturnSubmissionLogic(IOrderService orderService,
        IExchangeService exchangeService,
        ICustomerService customerService,
        IWorkContext workContext,
        IReturnRequestService returnRequestService,
        ILogger logger,
        IExchangeWorkflowMessageService exchangeWorkflowMessageService,
        ReturnRequestSettings returnRequestSettings,
        IDownloadService downloadService,
        IStoreContext storeContext,
        ILocalizationService localizationService,
        ICustomNumberFormatter customNumberFormatter,
        IWorkflowMessageService workflowMessageService,
        IReturnCaseService returnCaseService,
        LocalizationSettings localizationSettings,
        IProductService productService,
        IAclService aclService,
        IStoreMappingService storeMappingService,
        IProductAttributeService productAttributeService,
        ICWOrderService cwOrderService,
        IPackNameService packNameService,
        ICWFeatureService cwFeatureService)
    {
        _orderService = orderService;
        _exchangeService = exchangeService;
        _customerService = customerService;
        _workContext = workContext;
        _returnRequestService = returnRequestService;
        _logger = logger;
        _exchangeWorkflowMessageService = exchangeWorkflowMessageService;
        _returnRequestSettings = returnRequestSettings;
        _downloadService = downloadService;
        _storeContext = storeContext;
        _localizationService = localizationService;
        _customNumberFormatter = customNumberFormatter;
        _workflowMessageService = workflowMessageService;
        _returnCaseService = returnCaseService;
        _localizationSettings = localizationSettings;
        _productService = productService;
        _aclService = aclService;
        _storeMappingService = storeMappingService;
        _productAttributeService = productAttributeService;
        _cwOrderService = cwOrderService;
        _packNameService = packNameService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Utilities

    protected async Task RegisterReturnRequestItemDataAsync(ReturnRequestGatherInfoModel.ReturnedOrderItemModel returnRequestItemData,
        ReturnRequest returnRequest,
        ExchangeReturnMode exchangeMode,
        Guid returnRequestGroup,
        int returnReasonId,
        int returnActionId)
    {
        var returnRequestItem = new ReturnRequestItem
        {
            ExchangeModeId = (int)exchangeMode,
            ExchangeTypeId = returnRequestItemData.ExchangeTypeId,
            ReturnRequestGroup = returnRequestGroup,
            ReturnedOrderItemId = returnRequestItemData.Item.Id,
            ReturnReasonId = returnReasonId,
            ReturnActionId = returnActionId
        };

        ReturnReplacementItem replacementProduct = null;

        if (returnRequestItemData.ReplacementProduct != null)
        {
            replacementProduct = BuildReplacementItem(returnRequestItemData.ReplacementProduct, returnRequestItemData.ReturnedQuantity);
        }

        await _exchangeService.RegisterReturnRequestItemAsync(returnRequest, returnRequestItem, replacementProduct);
    }

    /// <summary>
    /// Whether a replacement fits its exchange type and could be ordered: a same-style exchange swaps the returned
    /// product's options; a different-style one is another product, on sale to the customer and in the catalogue of the
    /// packname the item was ordered for (as the picker offered it). Either needs every required option, and none the
    /// picker cannot take (rental dates, gift card details).
    /// </summary>
    /// <param name="order">The returned item's order</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="line">The submitted line</param>
    /// <returns>A task that represents the asynchronous operation; the task result is true when it fits</returns>
    protected virtual async Task<bool> IsReplacementValidAsync(Order order, OrderItem orderItem, ReturnWizardLine line)
    {
        if (line.ExchangeType == ExchangeType.Exact)
            return !line.ReplacementProductId.HasValue;

        if (!line.ReplacementProductId.HasValue)
            return true;

        var sameProduct = line.ReplacementProductId.Value == orderItem.ProductId;
        if (line.ExchangeType == ExchangeType.SameStyle ? !sameProduct : sameProduct)
            return false;

        var product = await _productService.GetProductByIdAsync(line.ReplacementProductId.Value);
        if (product == null || product.Deleted || product.IsGiftCard || product.IsRental)
            return false;

        //every required option chosen, each one of the product's values
        foreach (var mapping in (await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id)).Where(mapping => mapping.IsRequired))
        {
            if (!line.ReplacementAttributes.TryGetValue(mapping.Id, out var valueId)
                || (await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id)).All(attributeValue => attributeValue.Id != valueId))
                return false;
        }

        //the returned product itself: the customer already has it
        if (sameProduct)
            return true;

        if (!product.Published || !product.VisibleIndividually)
            return false;

        //Opened here, not in an awaited helper: what an async method sets on an AsyncLocal is undone when it returns.
        //The catalogue is that of the packname the item was ordered for, as the picker's.
        var packNameOrderItem = await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, order.StoreId)
            ? await _cwOrderService.GetPackNameOrderItemAsync(orderItem.Id)
            : null;
        var packName = packNameOrderItem == null ? null : await _packNameService.GetPackNameByIdAsync(packNameOrderItem.PackNameId);
        using var catalogScope = packName == null ? null : PackNameCatalogScope.Begin(packName, ExchangeType.DifferentStyle);

        return await _aclService.AuthorizeAsync(product) && await _storeMappingService.AuthorizeAsync(product);
    }

    protected ReturnReplacementItem BuildReplacementItem(ReturnRequestGatherInfoModel.ReplacementProductModel replacementProduct, int quantity)
    {
        return new ReturnReplacementItem
        {
            AttributesInfo = replacementProduct.AttributesInfo,
            AttributesXml = replacementProduct.AttributesXml,
            ProductId = replacementProduct.ProductId,
            ProductName = replacementProduct.ProductName,
            ProductSeName = replacementProduct.ProductSeName,
            Quantity = quantity,
            UnitPrice = replacementProduct.UnitPrice
        };
    }

    /// <summary>
    /// Resolves the draft case a confirmation belongs to, checking it really belongs to this customer
    /// and order and has not already been submitted. Returns null when there is nothing safe to write to.
    /// </summary>
    protected async Task<ReturnCase> ResolveDraftReturnCaseAsync(ReturnRequestGatherInfoModel returnRequestInfo, int customerId, int storeId)
    {
        var returnCase = returnRequestInfo.ReturnCaseId > 0
            ? await _returnCaseService.GetReturnCaseByIdAsync(returnRequestInfo.ReturnCaseId)
            : null;

        // The id may not have survived the form posts — fall back to the customer's open draft
        returnCase ??= await _returnCaseService.GetDraftReturnCaseAsync(customerId, returnRequestInfo.OrderId, storeId);

        if (returnCase == null)
        {
            await _logger.WarningAsync($"[Returns] No draft return case found for order {returnRequestInfo.OrderId}, customer {customerId}. The return was registered without one.");
            return null;
        }

        // Never write to someone else's case, a case for a different order, or one already submitted
        if (returnCase.CustomerId != customerId
            || returnCase.OrderId != returnRequestInfo.OrderId
            || returnCase.Status != ReturnCaseStatus.Draft)
        {
            await _logger.WarningAsync($"[Returns] Return case {returnCase.Id} is not an eligible draft for order {returnRequestInfo.OrderId}, customer {customerId} (status {returnCase.Status}). The return was registered without one.");
            return null;
        }

        return returnCase;
    }

    /// <summary>
    /// Records one returned item on the case, linked to the NOP return request that mirrors it
    /// </summary>
    protected async Task RegisterReturnCaseLineAsync(ReturnCase returnCase,
        ReturnRequestGatherInfoModel.ReturnedOrderItemModel returnedItem,
        ExchangeReturnMode exchangeMode,
        int returnRequestId)
    {
        var line = new ReturnCaseLine
        {
            ReturnCaseId = returnCase.Id,
            OrderItemId = returnedItem.Item.Id,
            Quantity = returnedItem.ReturnedQuantity,
            Mode = exchangeMode,
            ExchangeTypeId = returnedItem.ExchangeTypeId,
            Disposition = ReturnLineDisposition.Pending,
            ReturnRequestId = returnRequestId
        };

        if (returnedItem.ReplacementProduct != null)
        {
            line.ReplacementProductId = returnedItem.ReplacementProduct.ProductId;
            line.ReplacementAttributesXml = returnedItem.ReplacementProduct.AttributesXml;
            line.ReplacementAttributesInfo = returnedItem.ReplacementProduct.AttributesInfo;
            line.ReplacementQuantity = returnedItem.ReturnedQuantity;
        }

        await _returnCaseService.InsertReturnCaseLineAsync(line);
    }

    #endregion

    #region Methods

    public virtual async Task<bool> RegisterReturnRequestDataAsync(ReturnRequestGatherInfoModel returnRequestInfo, ExchangeReturnMode exchangeMode, List<int> registeredReturnRequestsIds)
    {
        int count = 0;
        var downloadId = 0;

        if (_returnRequestSettings.ReturnRequestsAllowFiles)
        {
            var download = await _downloadService.GetDownloadByGuidAsync(returnRequestInfo.UploadedFileGuid);
            if (download != null)
                downloadId = download.Id;
        }
        var store = await _storeContext.GetCurrentStoreAsync();
        var order = await _orderService.GetOrderByIdAsync(returnRequestInfo.OrderId);
        var currentCustomer = await _workContext.GetCurrentCustomerAsync();

        var returnCase = await ResolveDraftReturnCaseAsync(returnRequestInfo, currentCustomer.Id, store.Id);

        // Read once - this used to re-query every order item on each iteration
        var orderItems = (await _orderService.GetOrderItemsAsync(order.Id, isNotReturnable: false))
            .ToDictionary(item => item.Id);

        // Reason and action are the same for every item in this submission
        var returnReason = await _returnRequestService.GetReturnRequestReasonByIdAsync(returnRequestInfo.ReturnRequestReasonId);
        var returnAction = await _returnRequestService.GetReturnRequestActionByIdAsync(returnRequestInfo.ReturnRequestActionId);

        var reasonName = returnReason != null
            ? await _localizationService.GetLocalizedAsync(returnReason, x => x.Name)
            : "not available";

        var actionName = returnAction != null
            ? await _localizationService.GetLocalizedAsync(returnAction, x => x.Name)
            : "not available";

        foreach (var returnedItem in returnRequestInfo.ReturnedItems)
        {
            if (!orderItems.TryGetValue(returnedItem.Item.Id, out var orderItem))
                continue;

            if (!_exchangeService.IsReturnRequestAlreadyPerformed(orderItem.Id, returnRequestInfo.ReturnRequestGroupId, out int returnRequestId))
            {
                var returnRequest = new ReturnRequest
                {
                    CustomNumber = string.Empty,
                    StoreId = store.Id,
                    OrderItemId = orderItem.Id,
                    Quantity = returnedItem.ReturnedQuantity,
                    CustomerId = currentCustomer.Id,
                    ReasonForReturn = reasonName,
                    RequestedAction = actionName,
                    CustomerComments = returnRequestInfo.Comments,
                    UploadedFileId = downloadId,
                    StaffNotes = string.Empty,
                    ReturnRequestStatus = ReturnRequestStatus.Pending,
                    CreatedOnUtc = DateTime.UtcNow,
                    UpdatedOnUtc = DateTime.UtcNow
                };

                await _returnRequestService.InsertReturnRequestAsync(returnRequest);

                await RegisterReturnRequestItemDataAsync(returnedItem, returnRequest, exchangeMode, returnRequestInfo.ReturnRequestGroupId, returnRequestInfo.ReturnRequestReasonId, returnRequestInfo.ReturnRequestActionId);

                //set return request custom number
                returnRequest.CustomNumber = _customNumberFormatter.GenerateReturnRequestCustomNumber(returnRequest);
                await _customerService.UpdateCustomerAsync(currentCustomer);
                //persist the generated custom number (LinqToDB has no cascade from UpdateCustomerAsync)
                await _returnRequestService.UpdateReturnRequestAsync(returnRequest);
                //notify store owner
                await _workflowMessageService.SendNewReturnRequestStoreOwnerNotificationAsync(returnRequest, orderItem, order, _localizationSettings.DefaultAdminLanguageId);
                //notify customer
                await _workflowMessageService.SendNewReturnRequestCustomerNotificationAsync(returnRequest, orderItem, order);

                await _exchangeWorkflowMessageService.SendReturnRequestReceivedNotificationAsync(returnRequest, orderItem, order.CustomerLanguageId);

                returnRequestId = returnRequest.Id;

                if (returnCase != null)
                    await RegisterReturnCaseLineAsync(returnCase, returnedItem, exchangeMode, returnRequest.Id);
            }

            count++;

            if (registeredReturnRequestsIds != null && returnRequestId > 0)
            {
                registeredReturnRequestsIds.Add(returnRequestId);
            }
        }

        if (returnCase != null && count > 0)
        {
            try
            {
                returnCase.ReturnReasonId = returnRequestInfo.ReturnRequestReasonId;
                returnCase.ReturnActionId = returnRequestInfo.ReturnRequestActionId;
                returnCase.CustomerComments = returnRequestInfo.Comments;

                await _returnCaseService.UpdateReturnCaseAsync(returnCase);
                await _returnCaseService.SubmitReturnCaseAsync(returnCase);
            }
            catch (Exception ex)
            {
                // The NOP return requests are already created and the customer has been notified —
                // a problem finalising the case must not fail the submission
                await _logger.WarningAsync($"[Returns] Return case {returnCase.Id} could not be submitted. The return itself was registered normally.", ex);
            }
        }

        return count > 0;
    }

    public virtual async Task<IList<string>> CheckSubmissionAsync(Order order, int returnReasonId, int returnActionId, ReturnWizardSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(submission);

        var errors = new List<string>();
        var store = await _storeContext.GetCurrentStoreAsync();

        var reason = await _returnRequestService.GetReturnRequestReasonByIdAsync(returnReasonId);
        var activeReasonIds = await _exchangeService.GetActiveReasonIdsAsync(store.Id);
        var actions = await _exchangeService.GetActiveActionsByReasonIdAsync(returnReasonId, store.Id);
        if (reason == null || !activeReasonIds.Contains(reason.Id) || actions.All(action => action.Id != returnActionId))
            errors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.ReasonActionNotAvailable"));

        var lines = submission.Lines.Where(line => line.Quantity != 0).ToList();
        var returnable = await _exchangeService.GetReturnableQuantitiesAsync(order);
        if (lines.Any(line => line.Quantity < 0 || !returnable.TryGetValue(line.OrderItemId, out var available) || line.Quantity > available))
            errors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.QuantityNotAvailable"));

        if (!submission.Mode.HasValue)
            return errors;

        var modes = await _exchangeService.DetermineEnabledReturnModesAsync(store.Id, returnReasonId, returnActionId);
        if (!modes.Contains(submission.Mode.Value))
            errors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.ModeNotAvailable"));

        //a credit note returns the items as they are: a type or replacement posted with it is not used
        if (submission.Mode != ExchangeReturnMode.Exchange)
            return errors;

        var orderItems = (await _orderService.GetOrderItemsAsync(order.Id)).ToDictionary(item => item.Id);

        foreach (var line in lines.Where(line => line.Quantity > 0))
        {
            if (!orderItems.TryGetValue(line.OrderItemId, out var orderItem) || await IsReplacementValidAsync(order, orderItem, line))
                continue;

            errors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.ReplacementNotAvailable"));
            break;
        }

        return errors;
    }

    /// <summary>
    /// Finds the customer's open draft case for this order, or creates one, and refreshes it with the
    /// reason, action and comments currently chosen. Best-effort: on failure the wizard continues on
    /// the legacy path and the return is unaffected.
    /// </summary>
    public async Task<ReturnCase> EnsureDraftReturnCaseAsync(Order order, SubmitReturnRequestModel returnRequestModel, int storeId, int customerId)
    {
        try
        {
            var returnCase = await _returnCaseService.GetDraftReturnCaseAsync(customerId, order.Id, storeId);

            if (returnCase == null)
            {
                returnCase = new ReturnCase
                {
                    StoreId = storeId,
                    CustomerId = customerId,
                    OrderId = order.Id,
                    Status = ReturnCaseStatus.Draft
                };

                await _returnCaseService.InsertReturnCaseAsync(returnCase);
            }

            returnCase.ReturnReasonId = returnRequestModel.ReturnRequestReasonId > 0
                ? returnRequestModel.ReturnRequestReasonId : null;
            returnCase.ReturnActionId = returnRequestModel.ReturnRequestActionId > 0
                ? returnRequestModel.ReturnRequestActionId : null;
            returnCase.CustomerComments = returnRequestModel.Comments;

            await _returnCaseService.UpdateReturnCaseAsync(returnCase);

            return returnCase;
        }
        catch (Exception ex)
        {
            // The return case is not yet authoritative — never block a customer's return over it
            await _logger.WarningAsync($"[Returns] Could not prepare a draft return case for order {order.Id}, customer {customerId}. Continuing without one.", ex);
            return null;
        }
    }

    #endregion
}
