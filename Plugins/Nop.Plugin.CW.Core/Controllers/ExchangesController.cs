using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Products;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Common;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Controllers;
using Nop.Web.Framework.Controllers;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Controllers;

[AutoValidateAntiforgeryToken]
public class ExchangesController : BasePublicController
{
    #region Fields

    protected readonly IExchangeService _exchangeService;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly IOrderService _orderService;
    protected readonly IOrderProcessingService _orderProcessingService;
    protected readonly IReturnRequestOverrideModelFactory _returnRequestModelFactory;
    protected readonly IProductService _productService;
    protected readonly IWorkContext _workContext;
    protected readonly ILocalizationService _localizationService;
    protected readonly ICWPdfService _cwPdfService;
    protected readonly IExchangesLogic _exchangesLogic;
    protected readonly ILogger _logger;
    protected readonly IStoreContext _storeContext;
    protected readonly INotificationService _notificationService;
    protected readonly IExchangesModelFactory _exchangesModelFactory;
    protected readonly IValidator<ReturnWizardSubmission> _wizardValidator;
    protected readonly IReturnSubmissionLogic _returnSubmissionLogic;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IPackNameService _packNameService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IAclService _aclService;
    protected readonly IStoreMappingService _storeMappingService;
    protected readonly ILocker _locker;

    #endregion

    #region Ctor

    public ExchangesController(
        IExchangeService exchangeService,
        IReturnRequestService returnRequestService,
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        IReturnRequestOverrideModelFactory returnRequestModelFactory,
        IProductService productService,
        IWorkContext workContext,
        ILocalizationService localizationService,
        ICWPdfService cwPdfService,
        IExchangesLogic exchangesLogic,
        ILogger logger,
        IStoreContext storeContext,
        INotificationService notificationService,
        IExchangesModelFactory exchangesModelFactory,
        IValidator<ReturnWizardSubmission> wizardValidator,
        IReturnSubmissionLogic returnSubmissionLogic,
        ICWOrderService cwOrderService,
        IPackNameService packNameService,
        ICWFeatureService cwFeatureService,
        IAclService aclService,
        IStoreMappingService storeMappingService,
        ILocker locker)
    {
        _exchangeService = exchangeService;
        _returnRequestService = returnRequestService;
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _returnRequestModelFactory = returnRequestModelFactory;
        _productService = productService;
        _workContext = workContext;
        _localizationService = localizationService;
        _cwPdfService = cwPdfService;
        _exchangesLogic = exchangesLogic;
        _logger = logger;
        _storeContext = storeContext;
        _notificationService = notificationService;
        _exchangesModelFactory = exchangesModelFactory;
        _wizardValidator = wizardValidator;
        _returnSubmissionLogic = returnSubmissionLogic;
        _cwOrderService = cwOrderService;
        _packNameService = packNameService;
        _cwFeatureService = cwFeatureService;
        _aclService = aclService;
        _storeMappingService = storeMappingService;
        _locker = locker;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The packname the returned order item was ordered for, whose catalogue window the picker opens.
    /// </summary>
    /// <remarks>
    /// The replacement goes on that packname's line and is checked against what it may see when the exchange
    /// is approved. Offering what the packname selected right now may see would let the customer choose a
    /// product the approval then refuses, and hide one it would accept. Only for an item of the customer's own
    /// order, so the window cannot be used to browse another packname's catalogue.
    /// </remarks>
    /// <param name="orderItemId">Order item identifier of the line being returned</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packname; null when the item has none to follow
    /// </returns>
    protected virtual async Task<PackName> GetReturnedItemPackNameAsync(int orderItemId)
    {
        var (orderItem, order) = await GetOwnOrderItemAsync(orderItemId);
        if (orderItem == null)
            return null;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, order.StoreId))
            return null;

        var packNameOrderItem = await _cwOrderService.GetPackNameOrderItemAsync(orderItem.Id);

        return packNameOrderItem == null ? null : await _packNameService.GetPackNameByIdAsync(packNameOrderItem.PackNameId);
    }

    /// <summary>
    /// An order item of the current customer's own order
    /// </summary>
    /// <param name="orderItemId">Order item identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the order item and its order; both null when it is not the customer's
    /// </returns>
    protected virtual async Task<(OrderItem orderItem, Order order)> GetOwnOrderItemAsync(int orderItemId)
    {
        var orderItem = await _orderService.GetOrderItemByIdAsync(orderItemId);
        if (orderItem == null)
            return (null, null);

        var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (order == null || order.Deleted || order.CustomerId != customer.Id)
            return (null, null);

        return (orderItem, order);
    }

    /// <summary>
    /// The current customer's own order, when it can still take a return request
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the order; null when it is not the customer's or cannot take a return
    /// </returns>
    protected virtual async Task<Order> GetReturnableOrderAsync(int orderId)
    {
        var order = await _orderService.GetOrderByIdAsync(orderId);
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (order == null || order.Deleted || order.CustomerId != customer.Id)
            return null;

        return await _orderProcessingService.IsReturnRequestAllowedAsync(order) ? order : null;
    }

    /// <summary>
    /// Redisplays the gather step with validation messages, keeping the customer's selections intact.
    /// A redirect would discard them and strand the customer at the start of the flow.
    /// </summary>
    /// <param name="gatheredInfo">The model-bound hidden fields carried through the wizard</param>
    /// <param name="submission">The parsed post, replayed into the redisplayed form</param>
    /// <param name="errors">The messages to show</param>
    /// <returns>The gather view, or a redirect when the order no longer exists</returns>
    protected async Task<IActionResult> RedisplayGatherStepAsync(ReturnRequestGatherInfoModel gatheredInfo, ReturnWizardSubmission submission, IEnumerable<string> errors)
    {
        // RuleForEach raises the same message once per offending row - the customer should see it once
        foreach (var error in errors.Distinct())
            ModelState.AddModelError(string.Empty, error);

        var model = await _exchangesModelFactory.RebuildGatheredInfoAsync(gatheredInfo, submission);
        if (model == null)
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        return View("~/Plugins/CW.Core/Views/Exchanges/ReturnRequestGatherInfo.cshtml", model);
    }

    #endregion

    #region Methods

    [HttpPost, ActionName("Gather")]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.CurrentStore)]
    public async Task<IActionResult> ReturnRequestGatherInfo(int orderId, SubmitReturnRequestModel returnRequestModel, IFormCollection form)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null || order.Deleted || customer.Id != order.CustomerId)
            return Challenge();

        if (!await _orderProcessingService.IsReturnRequestAllowedAsync(order))
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        var submission = ReturnWizardSubmission.FromReturnPageForm(form);

        //the items, quantities, reason and action as the store allows them, whatever the page posted
        var errors = await _returnSubmissionLogic.CheckSubmissionAsync(order,
            returnRequestModel.ReturnRequestReasonId, returnRequestModel.ReturnRequestActionId, submission);
        if (errors.Count > 0)
        {
            foreach (var error in errors)
                ModelState.AddModelError(string.Empty, error);

            returnRequestModel = await _returnRequestModelFactory.PrepareSubmitReturnRequestModelAsync(returnRequestModel, order);

            return View("~/Plugins/CW.Core/Views/ReturnRequest/ReturnRequest.cshtml", returnRequestModel);
        }

        var store = await _storeContext.GetCurrentStoreAsync();
        var draftCase = await _returnSubmissionLogic.EnsureDraftReturnCaseAsync(order, returnRequestModel, store.Id, customer.Id);

        var model = await _exchangesModelFactory.PrepareGatherInfoModelAsync(
            order, returnRequestModel, submission, Guid.NewGuid(), draftCase?.Id ?? 0);

        // Nothing selected on the previous page - redisplay it with the form intact.
        // Setting Result instead would replace the form with the message and dead-end the customer.
        if (model.ReturnedItems.Count == 0)
        {
            ModelState.AddModelError(string.Empty, await _localizationService.GetResourceAsync("ReturnRequests.NoItemsSubmitted"));

            returnRequestModel = await _returnRequestModelFactory.PrepareSubmitReturnRequestModelAsync(returnRequestModel, order);

            return View("~/Plugins/CW.Core/Views/ReturnRequest/ReturnRequest.cshtml", returnRequestModel);
        }

        return View("~/Plugins/CW.Core/Views/Exchanges/ReturnRequestGatherInfo.cshtml", model);
    }

    [ActionName("ReturnRequestSummary")]
    [HttpPost]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.CurrentStore)]
    public async Task<IActionResult> DisplayReturnRequestSummary(ReturnRequestGatherInfoModel gatheredInfo, IFormCollection form)
    {
        var order = await GetReturnableOrderAsync(gatheredInfo.OrderId);
        if (order == null)
            return Challenge();

        var submission = ReturnWizardSubmission.FromGatherForm(form);
        var validation = await _wizardValidator.ValidateAsync(submission);

        if (!validation.IsValid)
            return await RedisplayGatherStepAsync(gatheredInfo, submission, validation.Errors.Select(error => error.ErrorMessage));

        var errors = await _returnSubmissionLogic.CheckSubmissionAsync(order,
            gatheredInfo.ReturnRequestReasonId, gatheredInfo.ReturnRequestActionId, submission);
        if (errors.Count > 0)
            return await RedisplayGatherStepAsync(gatheredInfo, submission, errors);

        var rebuilt = await _exchangesModelFactory.RebuildGatheredInfoAsync(gatheredInfo, submission);
        if (rebuilt == null)
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        var model = new ReturnRequestSummaryModel
        {
            Mode = submission.Mode.Value,
            GatheredInfo = rebuilt
        };

        await _exchangesModelFactory.PrepareSummaryFeesAsync(model);

        return View("ReturnRequestSummary", model);
    }

    /// <summary>
    /// Stage 1 of the picker: a paged, searchable grid of candidate replacements (Different Style)
    /// </summary>
    [HttpGet]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.CurrentStore)]
    public async Task<IActionResult> ReplacementProductList(int orderItemId, int excludeProductId,
        string keywords, int pageIndex = 0)
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        //the picker is for an item of the customer's own order
        var (ownOrderItem, _) = await GetOwnOrderItemAsync(orderItemId);
        if (ownOrderItem == null)
            return NotFound();

        //Opened here, not inside the awaited helper: what an async method sets on an AsyncLocal is undone when it returns.
        //The grid offers other products: a Different Style replacement, so the packname's allocation decides and its
        //ordering period does not.
        var returnedItemPackName = await GetReturnedItemPackNameAsync(orderItemId);
        using var catalogScope = returnedItemPackName == null ? null : PackNameCatalogScope.Begin(returnedItemPackName, ExchangeType.DifferentStyle);

        var model = await _exchangesModelFactory.PrepareReplacementProductListModelAsync(
            store.Id, orderItemId, excludeProductId, keywords, pageIndex, CWCoreDefaults.ReplacementPickerPageSize);

        return PartialView("_ReplacementProductList", model);
    }

    /// <summary>
    /// Stage 2 of the picker: the size / colour form for one chosen product
    /// </summary>
    [HttpGet]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.CurrentStore)]
    public async Task<IActionResult> ReplacementProductOptions(int productId, int orderItemId, int? returnRequestId)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null || product.Deleted)
            return NotFound();

        //the picker is for an item of the customer's own order; when editing an exchange, for that exchange's item
        var (orderItem, _) = await GetOwnOrderItemAsync(orderItemId);
        if (orderItem == null)
            return NotFound();

        //another product must be on sale; the returned product itself the customer already has
        if (!product.Published && orderItem.ProductId != product.Id)
            return NotFound();

        if (returnRequestId > 0)
        {
            var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(returnRequestId.Value);
            if (returnRequest == null || returnRequest.OrderItemId != orderItem.Id)
                return NotFound();
        }

        //Opened here, not inside the awaited helper: what an async method sets on an AsyncLocal is undone when it returns.
        //The returned product itself is a Same Style replacement, always allowed; any other is a Different Style one.
        var returnedItemPackName = await GetReturnedItemPackNameAsync(orderItemId);
        var replacement = orderItem.ProductId == product.Id
            ? ExchangeType.SameStyle
            : ExchangeType.DifferentStyle;
        using var catalogScope = returnedItemPackName == null ? null : PackNameCatalogScope.Begin(returnedItemPackName, replacement);

        //The grid only lists what the packname may see, but the id arrives in the query string.
        if (!await _aclService.AuthorizeAsync(product) || !await _storeMappingService.AuthorizeAsync(product))
            return NotFound();

        var model = await _exchangesModelFactory.PrepareReplacementProductOptionsModelAsync(product, orderItemId);

        // Show what the customer already has, so they change only what they mean to
        await _exchangesModelFactory.PreselectCurrentAttributesAsync(model.Product, returnRequestId, orderItemId);

        return PartialView("_ReplacementProductOptions", model);
    }

    [ActionName("ReturnRequestSummary")]
    [HttpPost]
    [FormValueRequired("confirm-return-request")]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.CurrentStore)]
    public async Task<IActionResult> ConfirmReturnRequestSummary(ReturnRequestSummaryModel model, IFormCollection form)
    {
        if (model.GatheredInfo == null)
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        var order = await GetReturnableOrderAsync(model.GatheredInfo.OrderId);
        if (order == null)
            return Challenge();

        var submission = ReturnWizardSubmission.FromGatherForm(form);
        var validation = await _wizardValidator.ValidateAsync(submission);

        // Reaching Confirm with an invalid post means stale or tampered state rather than a mistake,
        // but redisplaying the step is still the kindest handling
        if (!validation.IsValid)
            return await RedisplayGatherStepAsync(model.GatheredInfo, submission, validation.Errors.Select(error => error.ErrorMessage));

        var gatheredInfo = await _exchangesModelFactory.RebuildGatheredInfoAsync(model.GatheredInfo, submission);
        if (gatheredInfo == null)
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        var registeredReturnRequestsIds = new List<int>();
        var registered = false;
        IList<string> errors = new List<string>();

        //One confirmation of an order at a time, checked inside the lock: two tabs (two attempts) cannot both pass the
        //quantity check before either registers, and a second post of an attempt (a double click) adds nothing.
        var ran = await _locker.PerformActionWithLockAsync($"Nop.Plugin.CW.Core.Exchanges.Confirm.{order.Id}",
            TimeSpan.FromSeconds(60),
            async () =>
            {
                //an attempt already registered (the page posted again) gives back its requests; registration skips them
                var alreadyRegistered = submission.Lines.Where(line => line.Quantity > 0)
                    .Any(line => _exchangeService.IsReturnRequestAlreadyPerformed(line.OrderItemId, gatheredInfo.ReturnRequestGroupId, out _));

                if (!alreadyRegistered)
                {
                    errors = await _returnSubmissionLogic.CheckSubmissionAsync(order,
                        gatheredInfo.ReturnRequestReasonId, gatheredInfo.ReturnRequestActionId, submission);
                    if (errors.Count > 0)
                        return;
                }

                registered = await _returnSubmissionLogic.RegisterReturnRequestDataAsync(gatheredInfo, submission.Mode.Value, registeredReturnRequestsIds);
            });

        if (!ran)
            return RedirectToAction("CustomerReturnRequests", "ReturnRequest");

        if (errors.Count > 0)
            return await RedisplayGatherStepAsync(model.GatheredInfo, submission, errors);

        if (!registered)
        {
            // Nothing was registered - the posted state is gone, so there is nothing to redisplay
            _notificationService.ErrorNotification(
                await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.SessionExpired"));

            return RedirectToAction("ReturnRequest", "ReturnRequest", new { orderId = model.GatheredInfo.OrderId });
        }

        var routeValues = new RouteValueDictionary();
        if (registeredReturnRequestsIds.Count > 0)
            routeValues.Add("returnRequests", string.Join(",", registeredReturnRequestsIds));

        return RedirectToAction("CustomerReturnRequests", "ReturnRequest", routeValues);
    }

    [HttpGet]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<JsonResult> GetActionsForReason(int reasonId, int storeId)
    {
        var reasonActions = await _exchangeService.GetActiveActionsByReasonIdAsync(reasonId, storeId);

        var items = new List<SelectListItem>();
        foreach (var reasonAction in reasonActions)
            items.Add(new SelectListItem { Text = await _localizationService.GetLocalizedAsync(reasonAction, action => action.Name), Value = reasonAction.Id.ToString() });

        return Json(items);
    }

    [HttpGet]
    public async Task<IActionResult> GenerateReturnRequestReceipt(int id)
    {
        //only the customer's own return request
        var customer = await _workContext.GetCurrentCustomerAsync();
        var returnRequest = (await _exchangeService.GetCustomerReturnRequestsByIdsAsync(customer.Id, [id])).FirstOrDefault();

        if (returnRequest != null)
        {
            try
            {
                byte[] bytes;
                using (var stream = new MemoryStream())
                {
                    await _cwPdfService.PrintReturnExchangeRequestToPdfAsync(stream, returnRequest);
                    bytes = stream.ToArray();
                }
                return File(bytes, MimeTypes.ApplicationPdf, $"returnrequest_{returnRequest.Id}.pdf");
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Return request {id}: the receipt could not be printed", ex, customer);
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Receipt.PrintError"));
            }
        }
        else
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Receipt.NotFound"));
        }

        return RedirectToAction("CustomerReturnRequests", "ReturnRequest");
    }

    [HttpGet]
    public async Task<IActionResult> GenerateReturnRequestReceipts(int[] ids)
    {
        //only the customer's own return requests, as many as one confirmation shows
        var customer = await _workContext.GetCurrentCustomerAsync();
        var returnRequests = await _exchangeService.GetCustomerReturnRequestsByIdsAsync(customer.Id,
            (ids ?? []).Distinct().Take(CWCoreDefaults.ReturnRequestsConfirmationMaxCount).ToArray());

        if (returnRequests.Count == 0)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Receipt.NotFound"));
            return RedirectToAction("CustomerReturnRequests", "ReturnRequest");
        }

        try
        {
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                await _cwPdfService.PrintReturnExchangeRequestsToPdfAsync(stream, returnRequests);
                bytes = stream.ToArray();
            }

            return File(bytes, MimeTypes.ApplicationPdf, $"returnrequests_{DateTime.Now:hh_mm_ss_ddMMyyyy}.pdf");
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("The return request receipts could not be printed", ex, customer);
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Receipt.PrintError"));
            return RedirectToAction("CustomerReturnRequests", "ReturnRequest");
        }
    }

    [HttpPost]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.CurrentStore)]
    public async Task<JsonResult> EditExchangedItem(int returnRequestId, SelectedProductAttribute[] selectedAttributes)
    {
        var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(returnRequestId);
        var customer = await _workContext.GetCurrentCustomerAsync();

        // A customer may only edit their own return, and only while it is still pending
        if (returnRequest == null || returnRequest.CustomerId != customer.Id)
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Edit.NotFound") });

        if (returnRequest.ReturnRequestStatus != ReturnRequestStatus.Pending)
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Edit.NotPending") });

        try
        {
            var returnRequestItem = await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);
            if (returnRequestItem == null || !await _exchangesLogic.ModifyExchangedItemDetailsAsync(returnRequestItem, selectedAttributes ?? []))
                return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Edit.NoExchangeItem") });
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Error updating exchange item for return request {returnRequestId}", ex, customer);

            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.Edit.UpdateFailed") });
        }

        return Json(new { success = true, message = string.Empty });
    }

    #endregion
}