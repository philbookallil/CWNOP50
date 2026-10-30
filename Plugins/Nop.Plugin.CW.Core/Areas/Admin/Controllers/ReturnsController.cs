using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

public class ReturnsController : ReturnRequestController
{
    #region Fields

    protected readonly IWorkContext _workContext;
    protected readonly IExchangeService _exchangeService;
    protected readonly IExchangesLogic _exchangesLogic;
    protected readonly IReturnRequestExchangeModelFactory _modelFactory;
    protected readonly ICustomersLogic _customersLogic;
    protected readonly IStoreService _storeService;
    protected readonly IStoreContext _storeContext;
    protected readonly ILogger _logger;
    protected readonly IReturnCaseService _returnCaseService;
    protected readonly IExchangeWorkflowMessageService _exchangeWorkflowMessageService;
    protected readonly IReturnResolutionLogic _returnResolutionLogic;

    #endregion

    #region Ctor

    public ReturnsController(ICustomerActivityService customerActivityService,
        ILocalizationService localizationService,
        ILocalizedEntityService localizedEntityService,
        INotificationService notificationService,
        IOrderService orderService,
        IProductService productService,
        IPermissionService permissionService,
        IReturnRequestModelFactory returnRequestModelFactory,
        IReturnRequestService returnRequestService,
        IWorkflowMessageService workflowMessageService,
        IWorkContext workContext,
        IExchangeService exchangeService,
        IExchangesLogic exchangesLogic,
        IReturnRequestExchangeModelFactory modelFactory,
        ICustomersLogic customersLogic,
        IStoreService storeService,
        IStoreContext storeContext,
        ILogger logger,
        IReturnCaseService returnCaseService,
        IExchangeWorkflowMessageService exchangeWorkflowMessageService,
        IReturnResolutionLogic returnResolutionLogic) : base(customerActivityService,
            localizationService,
            localizedEntityService,
            notificationService,
            orderService,
            productService,
            permissionService,
            returnRequestModelFactory,
            returnRequestService,
            workflowMessageService)
    {
        _workContext = workContext;
        _exchangeService = exchangeService;
        _exchangesLogic = exchangesLogic;
        _modelFactory = modelFactory;
        _customersLogic = customersLogic;
        _storeService = storeService;
        _storeContext = storeContext;
        _logger = logger;
        _returnCaseService = returnCaseService;
        _exchangeWorkflowMessageService = exchangeWorkflowMessageService;
        _returnResolutionLogic = returnResolutionLogic;
    }

    #endregion

    #region Utilities

    protected virtual async Task PrepareReturnRequestModelAsync(CWReturnRequestModel model, ReturnRequest returnRequest, bool excludeProperties, bool isFailedSubmit = false)
    {
        var returnRequestModel = await _returnRequestModelFactory.PrepareReturnRequestModelAsync(model, returnRequest, excludeProperties);

        if (isFailedSubmit)
        {
            model.ReturnRequestStatusId = returnRequest.ReturnRequestStatusId;
        }

        await _modelFactory.PrepareExchangeRequestModelAsync(model, returnRequestModel);

        if (isFailedSubmit)
        {
            model.IsEditable = true;
        }
    }

    protected async Task UpdateExchangeReturnDataAsync(CWReturnRequestModel model, ReturnRequest returnRequest)
    {
        var returnRequestItem = await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);

        if (returnRequestItem != null)
        {
            // Only update the shipped and repackaged quantities as the rest of the information cannot be altered.
            //
            // Clamped to the returned quantity. The browser caps these boxes too, but nothing stops a post
            // that skipped the script from charging repackaging on more garments than came back — and these
            // two boxes are the whole of how Sales Support set the real fee, so the server has to mean it.
            // Negatives floor at zero: zeroing a box is how staff drop a fee, so it stays legal.
            returnRequestItem.StaffRepackQty = ClampStaffQuantity(model.StaffRepackQty, returnRequest.Quantity);
            returnRequestItem.StaffShipQty = ClampStaffQuantity(model.StaffShipQty, returnRequest.Quantity);
            
            returnRequestItem.AdditionalFeeName = model.AdditionalFeeName;
            returnRequestItem.AdditionalFeeAmount = model.AdditionalFeeAmount;

            if (model.ReturnRequestReasonId > 0)
                returnRequestItem.ReturnReasonId = model.ReturnRequestReasonId;

            if (model.ReturnRequestActionId > 0)
                returnRequestItem.ReturnActionId = model.ReturnRequestActionId;

            // Make the updates
            await _exchangeService.UpdateReturnRequestItemAsync(returnRequestItem);
        }
    }

    /// <summary>
    /// A staff-entered fee quantity, held to the quantity actually returned.
    /// </summary>
    protected static int ClampStaffQuantity(int posted, int returnedQuantity)
    {
        if (posted < 0)
            return 0;

        return posted > returnedQuantity ? returnedQuantity : posted;
    }

    /// <summary>
    /// Maps the status the administrator selected to the decision it implies for this returned item.
    /// Returns null for statuses that do not decide anything, leaving the line Pending.
    /// </summary>
    /// <remarks>
    /// Every member of nop's ReturnRequestStatus is listed, deliberately, because a terminal status that
    /// fell through to null would leave its line Pending for ever and its case could never resolve.
    /// <para>
    /// ItemsRefunded and ItemsRepaired are mapped to nothing on purpose: nothing happens for them — no
    /// fee, no credit note, no exchange, no email — so calling them Accepted would invent a fee-charging
    /// rule this business has never had. A case containing such a
    /// line will not resolve; RecordLineDispositionAsync logs that rather than stalling quietly.
    /// </para>
    /// If nop adds a status the compiler will not warn — this list has to be revisited by hand.
    /// </remarks>
    protected static ReturnLineDisposition? MapStatusToDisposition(int returnRequestStatusId)
    {
        if (!Enum.IsDefined(typeof(ReturnRequestStatus), returnRequestStatusId))
            return null;

        return (ReturnRequestStatus)returnRequestStatusId switch
        {
            //The item came back and the customer got something for it: fees apply.
            ReturnRequestStatus.ReturnAuthorized => ReturnLineDisposition.Accepted,

            //Decided against. Rejected is the disposition that charges nothing: no fee, no credit note,
            //no exchange.
            ReturnRequestStatus.RequestRejected => ReturnLineDisposition.Rejected,
            ReturnRequestStatus.Cancelled => ReturnLineDisposition.Rejected,

            //Steps on the way, not decisions.
            ReturnRequestStatus.Pending => null,
            ReturnRequestStatus.Received => null,

            //ItemsRefunded, ItemsRepaired — see the remarks above.
            _ => null
        };
    }

    protected async Task RecordLineDispositionAsync(ReturnRequest returnRequest, CWReturnRequestModel model)
    {
        var disposition = MapStatusToDisposition(model.ReturnRequestStatusId);

        if (disposition == null)
        {
            //Pending and Received are steps on the way and decide nothing, so they are silent. Anything
            //else reaching here is a status whose meaning nobody has settled: the line stays Pending, and
            //a case with a Pending line never resolves, so its fees never freeze. Say so — this used to
            //stall silently and was invisible to every consistency check.
            var newStatus = (ReturnRequestStatus)model.ReturnRequestStatusId;

            if (newStatus is not (ReturnRequestStatus.Pending or ReturnRequestStatus.Received))
                await _logger.WarningAsync($"[Returns] Return request {returnRequest.Id} was set to {newStatus}, which does not decide the returned item either way. Its return case cannot resolve until every line is decided — see ReturnsController.MapStatusToDisposition.");

            return;
        }

        try
        {
            var caseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequest.Id);
            if (caseLine == null)
                return;

            //A resolved case was already re-opened by ReopenResolvedCaseIfDecisionChangesAsync, before the
            //workflow priced anything. By here there is never a resolution left to protect, so this only
            //has to write the decision.
            var staff = await _workContext.GetCurrentCustomerAsync();

            await _returnCaseService.SetLineDispositionAsync(caseLine, disposition.Value,
                model.DispositionReason, staff?.Id);
        }
        catch (Exception ex)
        {
            await _logger.WarningAsync($"[Returns] Could not record the disposition for return request {returnRequest.Id}. The return request itself was saved normally.", ex);
        }
    }

    /// <summary>
    /// Runs the approval workflow for this return request and reports what it did.
    /// </summary>
    /// <returns>False when the workflow failed, in which case the caller must not advance the status.</returns>
    protected async Task<bool> ExecuteStatusChangeWorkflowAsync(ReturnRequest returnRequest, CWReturnRequestModel model)
    {
        //Only a real status change triggers anything, and only when there is exchange data.
        if (!RequestStatusHasChanged(model) || !await HasExchangeRequestItemAsync(returnRequest))
            return true;

        ReturnRequestWorkflowResult workflowResult;

        try
        {
            workflowResult = await _exchangesLogic.ExecuteApprovedLineAsync(returnRequest, model.ReturnRequestStatusId);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"[Returns] The exchange workflow threw for return request {returnRequest.Id}. Nothing was advanced.", ex);
            _notificationService.ErrorNotification("There were errors during the Exchange workflow steps. Could not complete the workflow: " + ex.Message);

            return false;
        }

        if (workflowResult.Errors.Count > 0)
        {
            _notificationService.ErrorNotification("There were errors during the Exchange workflow steps. Could not complete the workflow. See errors below:");

            foreach (var error in workflowResult.Errors)
                _notificationService.ErrorNotification(error);

            return false;
        }

        if (workflowResult.SubsidyWasRestored)
            _notificationService.SuccessNotification($"The subsidy spent on the returned item was given back to the employee's entitlement for the Return Request with ID: {returnRequest.Id}");

        if (workflowResult.CreditNoteWasCreated)
            _notificationService.SuccessNotification($"A Credit Note was created to account for the Return Request with ID: {returnRequest.Id}");

        if (workflowResult.FeesNotDeductible)
            _notificationService.WarningNotification($"The Return Request with ID: {returnRequest.Id} carries fees, but the returned item was paid by subsidy, so there is no Credit Note to deduct them from. Nothing has been charged. Set the Repackaged / Re-Shipped quantities to zero and remove the additional fee BEFORE authorising a return like this one — once it is authorised the case is final and those boxes can no longer be edited.");

        if (workflowResult.OrderWasPlaced)
            _notificationService.SuccessNotification($"An Order has been created automatically to account for the Return Request with ID: {returnRequest.Id}");

        if (workflowResult.NeedsCompletion)
            _notificationService.WarningNotification($"The Return Request with ID: {returnRequest.Id} requires further actions for completion");

        return true;
    }

    /// <summary>
    /// Freezes the case's charges once every line of it has been decided. Records only — the lines have
    /// already executed.
    /// </summary>
    protected async Task RecordCaseResolutionAsync(ReturnRequest returnRequest)
    {
        try
        {
            var caseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequest.Id);

            if (caseLine == null)
                return;

            var staff = await _workContext.GetCurrentCustomerAsync();

            await _returnResolutionLogic.TryResolveCaseAsync(caseLine.ReturnCaseId, staff?.Id);
        }
        catch (Exception ex)
        {
            //Never blocks the save. The charges stay unfrozen and the next save of any line retries.
            await _logger.ErrorAsync($"[Returns] Could not record the resolution for the case of return request {returnRequest.Id}.", ex);
        }
    }

    /// <summary>
    /// Voids a resolved case's frozen outcome when this save is about to change one of its decisions —
    /// before anything is priced.
    /// </summary>
    /// <remarks>
    /// A rejected return stays re-openable, so a decision can genuinely change
    /// after its case has resolved. It has to be voided <b>here</b>, ahead of the approval workflow,
    /// because that workflow prices the fee to decide what to charge and which pathway to take — and while
    /// a resolution exists every reader returns the frozen charges. Those were priced while this line was
    /// rejected, so they hold nothing for it: the workflow would see a zero fee, issue a credit note with
    /// nothing deducted or place the exchange order outright, and the charge written moments later would
    /// have no collection point.
    /// <para>
    /// The line goes back to Pending so this save prices it from the live rules instead of reading the
    /// charge frozen while it was rejected. RecordLineDispositionAsync writes the real decision later in
    /// this same request.
    /// </para>
    /// Best-effort, like the other case writes: a failure here must not fail the administrator's save.
    /// </remarks>
    protected async Task ReopenResolvedCaseIfDecisionChangesAsync(ReturnRequest returnRequest, CWReturnRequestModel model)
    {
        var disposition = MapStatusToDisposition(model.ReturnRequestStatusId);

        if (disposition == null)
            return;

        try
        {
            var caseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequest.Id);

            if (caseLine == null || caseLine.Disposition == disposition.Value)
                return;

            var previousDisposition = caseLine.Disposition;

            //Under the case lock — writing a resolution and voiding one are the same critical section.
            //0 means there was nothing to void; -1 means the lock was busy and nothing was voided, which
            //TryReopenCaseAsync has already logged.
            var voidedCharges = await _returnResolutionLogic.TryReopenCaseAsync(caseLine.ReturnCaseId);

            if (voidedCharges < 0)
                return;

            await _returnCaseService.SetLineDispositionAsync(caseLine, ReturnLineDisposition.Pending, null, null);

            if (voidedCharges > 0 || previousDisposition != ReturnLineDisposition.Pending)
                await _logger.WarningAsync($"[Returns] Return request {returnRequest.Id} is moving from {previousDisposition} to {disposition.Value} on case {caseLine.ReturnCaseId}. Any resolution and {voidedCharges} frozen charge(s) were voided so this save prices from the live rules; the case re-resolves once every line is decided again.");
        }
        catch (Exception ex)
        {
            await _logger.WarningAsync($"[Returns] Could not re-open the resolved case for return request {returnRequest.Id} before pricing. Any fee charged on this save may have come from the previous resolution.", ex);
        }
    }

    /// <summary>
    /// The workflow runs when the status actually changes — never on a
    /// re-save of a line that is already where it was.
    /// </summary>
    /// <remarks>
    /// This is the whole of the "execute once" guarantee, and it works because Return Authorized is a
    /// one-way door: the validator refuses any transition out of it, and the final-status branch at the
    /// top of EditExchangeRequest returns before reaching here. The one way back in is a rejected line
    /// re-opened and then authorised, which is a genuine status change and should execute.
    /// </remarks>
    protected static bool RequestStatusHasChanged(CWReturnRequestModel model)
    {
        return model.OriginalRequestStatusId != model.ReturnRequestStatusId;
    }

    protected async Task<bool> HasExchangeRequestItemAsync(ReturnRequest returnRequest)
    {
        return await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id) != null;
    }

    /// <summary>
    /// Puts the case side of a return back to undecided when its approval is undone.
    /// </summary>
    /// <remarks>
    /// UndoReturnRequestApprovalAsync resets the nop row and removes what the customer was given — the
    /// credit note is voided and the replacement leaves the cart. The case is a second, separate record of
    /// the same decision and nothing was resetting it, so an undone return kept a line reading Accepted
    /// and, where its case had resolved, a resolution with frozen charges the customer no longer owes.
    /// <para>
    /// It cannot be left to ReopenResolvedCaseIfDecisionChangesAsync. That fires only when the disposition
    /// CHANGES, and after an undo the line still reads Accepted — so re-approving is Accepted to Accepted,
    /// no void, and TryResolveCaseAsync then finds a resolution already exists and re-freezes nothing. The
    /// stale record would survive every later save.
    /// </para>
    /// Best-effort, like the other case writes: a failure here must not fail the undo.
    /// </remarks>
    protected async Task RevertCaseDecisionAsync(ReturnRequest returnRequest)
    {
        try
        {
            var caseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequest.Id);

            if (caseLine == null)
                return;

            //Under the case lock — see TryReopenCaseAsync.
            var voidedCharges = await _returnResolutionLogic.TryReopenCaseAsync(caseLine.ReturnCaseId);

            if (voidedCharges < 0)
                return;

            if (voidedCharges > 0)
                await _logger.WarningAsync($"[Returns] Approval for return request {returnRequest.Id} was undone. Its case {caseLine.ReturnCaseId} had already resolved, so that resolution and {voidedCharges} frozen charge(s) were voided; the case re-resolves once every line is decided again.");

            await _returnCaseService.SetLineDispositionAsync(caseLine, ReturnLineDisposition.Pending, null, null);
        }
        catch (Exception ex)
        {
            await _logger.WarningAsync($"[Returns] Could not revert the case decision for return request {returnRequest.Id} after its approval was undone. The return itself was undone normally.", ex);
        }
    }

    #endregion

    #region Navigation

    /// <summary>
    /// The order this screen was opened from, or null when it was reached from the returns list.
    /// </summary>
    /// <remarks>
    /// Set by the returns grid on the order page and carried on the edit form, so it is present on the GET
    /// and on every POST. Read from the query string rather than the model deliberately: five different
    /// actions post this one form, and none of them should have to carry a navigation concern on its model.
    /// </remarks>
    protected int? BackToOrderId =>
        int.TryParse(Request.Query["backToOrder"], out var orderId) && orderId > 0 ? orderId : null;

    /// <summary>
    /// Where leaving this return goes: back to the order staff came from, or the returns list otherwise.
    /// </summary>
    /// <remarks>
    /// Every redirect that leaves the screen goes through here. Sales Support work an order's returns one
    /// after another, and being thrown to the global list after each save loses the parcel they were
    /// working through.
    /// </remarks>
    protected IActionResult RedirectAfterLeavingReturn() =>
        BackToOrderId.HasValue
            ? RedirectToAction("Edit", "Order", new { id = BackToOrderId.Value, area = "Admin" })
            : RedirectToAction("List", "ReturnRequest", new { area = "Admin" });

    /// <summary>
    /// Back to this same return, keeping the order it was opened from so the next save still knows.
    /// </summary>
    protected IActionResult RedirectToReturn(int returnRequestId) =>
        RedirectToAction("Edit", "ReturnRequest",
            new { id = returnRequestId, area = "Admin", backToOrder = BackToOrderId });

    #endregion

    #region Methods

    [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
    [FormValueRequired("save", "save-continue")]
    [CheckPermission(StandardPermission.Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE)]
    public async Task<IActionResult> EditExchangeRequest(CWReturnRequestModel model, bool continueEditing)
    {
        var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(model.Id);
        if (returnRequest == null)
            //No return request found with the specified id
            return RedirectAfterLeavingReturn();

        // When the Return Request has reached a final status only the staff notes may be updated.
        // Everything else (quantities, fees, status, reason, action, customer comments) must be preserved,
        // regardless of what the (locked) form posts.
        if (ExchangesHelper.IsFinalReturnRequestStatus(returnRequest.ReturnRequestStatus))
        {
            returnRequest.StaffNotes = model.StaffNotes;
            returnRequest.UpdatedOnUtc = DateTime.UtcNow;
            await _returnRequestService.UpdateReturnRequestAsync(returnRequest);

            await _customerActivityService.InsertActivityAsync("EditReturnRequest", string.Format(await _localizationService.GetResourceAsync("ActivityLog.EditReturnRequest"), returnRequest.Id), returnRequest);
            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.ReturnRequests.Updated"));

            return continueEditing ? RedirectToReturn(returnRequest.Id) : RedirectAfterLeavingReturn();
        }

        // If the staff did not enable the additional fee, discard any submitted values so it is never persisted
        if (!model.EnableAdditionalFee)
        {
            model.AdditionalFeeName = null;
            model.AdditionalFeeAmount = null;
        }

        if (ModelState.IsValid)
        {
            try
            {
                //A decision that changes on an already-resolved case voids its frozen outcome first, so
                //the freeze at the end of this method records the live figures rather than the old ones.
                await ReopenResolvedCaseIfDecisionChangesAsync(returnRequest, model);
                // This will update our custom Exchanges/Return on the database
                await UpdateExchangeReturnDataAsync(model, returnRequest);

                //Execute BEFORE the decision is recorded and before the status is advanced, so a failed
                //workflow leaves the line exactly where it was and the next save retries cleanly. The staff
                //quantities and the ad-hoc fee are committed above and are kept either way, so the
                //administrator does not lose their typing to a failed workflow.
                if (!await ExecuteStatusChangeWorkflowAsync(returnRequest, model))
                    return continueEditing
                        ? RedirectToReturn(returnRequest.Id)
                        : RedirectAfterLeavingReturn();

                await RecordLineDispositionAsync(returnRequest, model);
                // Return the entered quantity back to stock (mirrors the native Nop Return Request handling)
                var quantityToReturn = model.ReturnedQuantity - returnRequest.ReturnedQuantity;
                if (quantityToReturn < 0)
                {
                    _notificationService.ErrorNotification(string.Format(await _localizationService.GetResourceAsync("Admin.ReturnRequests.Fields.ReturnedQuantity.CannotBeLessThanQuantityAlreadyReturned"), returnRequest.ReturnedQuantity));
                }
                else
                {
                    if (quantityToReturn > 0)
                    {
                        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);
                        if (orderItem != null)
                        {
                            var product = await _productService.GetProductByIdAsync(orderItem.ProductId);
                            if (product != null)
                            {
                                var productStockChangedMessage = string.Format(await _localizationService.GetResourceAsync("Admin.ReturnRequests.QuantityReturnedToStock"), quantityToReturn);
                                await _productService.AdjustInventoryAsync(product, quantityToReturn, orderItem.AttributesXml, productStockChangedMessage);
                                _notificationService.SuccessNotification(productStockChangedMessage);
                            }
                        }
                    }
                    returnRequest.ReturnedQuantity = model.ReturnedQuantity;
                }

                var selectedReason = await _returnRequestService.GetReturnRequestReasonByIdAsync(model.ReturnRequestReasonId);
                var selectedAction = await _returnRequestService.GetReturnRequestActionByIdAsync(model.ReturnRequestActionId);

                if (selectedReason != null)
                    returnRequest.ReasonForReturn = await _localizationService.GetLocalizedAsync(selectedReason, x => x.Name);

                if (selectedAction != null)
                    returnRequest.RequestedAction = await _localizationService.GetLocalizedAsync(selectedAction, x => x.Name);

                // This will update the Return Request. Quantity is deliberately not assigned: a line
                // is accepted or rejected whole, so the returned quantity is the customer's and the
                // screen renders it read-only. Writing back a hidden field would be the one way it
                // could still change, and ReturnCaseLine.Quantity would not follow it.
                returnRequest.CustomerComments = model.CustomerComments;
                returnRequest.StaffNotes = model.StaffNotes;
                returnRequest.ReturnRequestStatusId = model.ReturnRequestStatusId;
                returnRequest.UpdatedOnUtc = DateTime.UtcNow;

                await _returnRequestService.UpdateReturnRequestAsync(returnRequest);

                //LAST. The line has already executed; this only freezes the case's charges, and only once
                //every line of it has been decided. See ReturnResolutionLogic.
                await RecordCaseResolutionAsync(returnRequest);

                //activity log
                await _customerActivityService.InsertActivityAsync("EditReturnRequest", string.Format(await _localizationService.GetResourceAsync("ActivityLog.EditReturnRequest"), returnRequest.Id), returnRequest);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.ReturnRequests.Updated"));

                return continueEditing ? RedirectToReturn(returnRequest.Id) : RedirectAfterLeavingReturn();
            }
            catch (NopException ex)
            {
                string message = $"Error updating the status of Return Request with ID {returnRequest.Id}: {ex.Message}";
                _notificationService.ErrorNotification(message);
                return RedirectAfterLeavingReturn();
            }
        }

        //If we got this far, something failed, redisplay form
        await PrepareReturnRequestModelAsync(model, returnRequest, true, true);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/ReturnRequest/Edit.cshtml", model);
    }

    [HttpPost, ActionName("EditExchangeRequest")]
    [FormValueRequired("impersonate")]
    [CheckPermission(StandardPermission.Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE)]
    public async Task<IActionResult> ImpersonateForCompletion(CWReturnRequestModel model)
    {
        try
        {
            var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(model.Id);
            if (returnRequest == null)
            {
                _notificationService.ErrorNotification("Cannot find Return Request with ID: " + model.Id);
                //No return request found with the specified id
                return RedirectAfterLeavingReturn();
            }

            var currentStore = await _storeContext.GetCurrentStoreAsync();
            if (currentStore != null)
            {
                // If the Return Request belongs to a different Store we need to Impersonate using the Token approach
                if (currentStore.Id != returnRequest.StoreId)
                {
                    var otherStore = await _storeService.GetStoreByIdAsync(returnRequest.StoreId);
                    if (otherStore != null)
                    {
                        var currentUser = await _workContext.GetCurrentCustomerAsync();
                        try
                        {
                            // We need to ask the other Store for an impersonation session to be able to login there
                            var redirectUrl = await _customersLogic.RequestImpersonation(otherStore, currentUser, returnRequest.CustomerId, "/cart");
                            return Redirect(redirectUrl);
                        }
                        catch (Exception ex)
                        {
                            await _notificationService.ErrorNotificationAsync(ex);
                            _notificationService.ErrorNotification("Impersonation failed due to internal error");

                            // Log the error
                            await _logger.ErrorAsync("Impersonation failed for Return Request", ex);
                            //Go back to the Returns Requests List as the Impersonation failed
                            return RedirectAfterLeavingReturn();
                        }
                    }
                    else
                    {
                        _notificationService.ErrorNotification("The store assigned to the Return Request does not exist. Cannot complete impersonation");
                        //No return request found with the specified id
                        return RedirectAfterLeavingReturn();
                    }
                }
                else // Impersonate with the default method given we are on the same Store
                {
                    // Perform customer impersonation
                    await _customersLogic.ImpersonateCustomer(returnRequest.CustomerId);
                    // Redirect to shopping cart to allow finishing the checkout in customer's behalf
                    return RedirectToAction("Cart", "ShoppingCart", new { area = "" });
                }
            }
            else
            {
                _notificationService.ErrorNotification("Cannot determine current Store");
                return RedirectAfterLeavingReturn();
            }

        }
        catch (NopException ex)
        {
            await _notificationService.ErrorNotificationAsync(ex);
            return RedirectAfterLeavingReturn();
        }
    }

    [HttpPost, ActionName("EditExchangeRequest")]
    [FormValueRequired("notify-customer")]
    [CheckPermission(StandardPermission.Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE)]
    public override async Task<IActionResult> NotifyCustomer(ReturnRequestModel model)
    {
        //try to get a return request with the specified id
        var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(model.Id);
        if (returnRequest == null)
            return RedirectAfterLeavingReturn();

        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);
        if (orderItem == null)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.ReturnRequests.OrderItemDeleted"));
            return RedirectToReturn(returnRequest.Id);
        }

        var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);

        var queuedEmailIds = await _workflowMessageService.SendReturnRequestStatusChangedCustomerNotificationAsync(returnRequest, orderItem, order);
        if (queuedEmailIds.Any())
            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.ReturnRequests.Notified"));

        return RedirectToReturn(returnRequest.Id);
    }

    [HttpPost, ActionName("EditExchangeRequest")]
    [FormValueRequired("notify-checkout")]
    [CheckPermission(StandardPermission.Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE)]
    public async Task<IActionResult> NotifyCompletionRequired(CWReturnRequestModel model)
    {
        try
        {
            var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(model.Id);
            if (returnRequest == null)
            {
                _notificationService.ErrorNotification("Cannot find Return Request with ID: " + model.Id);
            }
            else
            {
                // Notify the customer that actions are required
                int emailId = await _exchangeWorkflowMessageService.SendPendingReturnRequestNotificationAsync(returnRequest);
                _notificationService.SuccessNotification($"A message with ID: {emailId} has been queued to notify the customer about the pending actions needed to complete Return Request with ID: {returnRequest.Id}");
            }
        }
        catch (NopException ex)
        {
            await _notificationService.ErrorNotificationAsync(ex);
        }

        return RedirectAfterLeavingReturn();
    }

    [HttpPost, ActionName("EditExchangeRequest")]
    [FormValueRequired("undo-approval")]
    [CheckPermission(StandardPermission.Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE)]
    public async Task<IActionResult> UndoReturnRequestApproval(CWReturnRequestModel model)
    {
        try
        {
            var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(model.Id);

            if (returnRequest != null)
            {
                await _exchangesLogic.UndoReturnRequestApprovalAsync(returnRequest);

                //The case is the other half of the same decision, and nothing else resets it. See the
                //remarks on RevertCaseDecisionAsync for why re-approving would not clean it up later.
                await RevertCaseDecisionAsync(returnRequest);

                _notificationService.SuccessNotification($"The approval for Return Request {returnRequest.Id} has been undone/reset successfully. You can now start the approval workflow again");
            }
            else
            {
                //model.Id, not returnRequest.Id — this branch runs precisely because returnRequest is
                //null, so reading its Id threw a NullReferenceException, which the NopException catch
                //below does not handle: the administrator got a 500 instead of this message.
                _notificationService.ErrorNotification($"The Return Request with ID {model.Id} does not exist");
            }
        }
        catch (NopException ex)
        {
            await _notificationService.ErrorNotificationAsync(ex);
        }

        return RedirectAfterLeavingReturn();
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SETTINGS)]
    public virtual async Task<IActionResult> ReturnRequestReasonCreatePopup()
    {
        //prepare model
        var model = await _returnRequestModelFactory.PrepareReturnRequestReasonModelAsync(new ReturnRequestReasonModel(), null);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/ReturnRequest/ReturnRequestReasonCreatePopup.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SETTINGS)]
    public virtual async Task<IActionResult> ReturnRequestReasonCreatePopup(ReturnRequestReasonModel model)
    {
        if (ModelState.IsValid)
        {
            var returnRequestReason = model.ToEntity<ReturnRequestReason>();
            await _returnRequestService.InsertReturnRequestReasonAsync(returnRequestReason);

            //locales
            await UpdateLocalesAsync(returnRequestReason, model);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Configuration.Settings.Order.ReturnRequestReasons.Added"));

            ViewBag.RefreshPage = true;

            return View("~/Plugins/CW.Core/Areas/Admin/Views/ReturnRequest/ReturnRequestReasonCreatePopup.cshtml", model);
        }

        //prepare model
        model = await _returnRequestModelFactory.PrepareReturnRequestReasonModelAsync(model, null, true);

        //if we got this far, something failed, redisplay form
        return View("~/Plugins/CW.Core/Areas/Admin/Views/ReturnRequest/ReturnRequestReasonCreatePopup.cshtml", model);
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SETTINGS)]
    public virtual async Task<IActionResult> ReturnRequestActionCreatePopup()
    {
        //prepare model
        var model = await _returnRequestModelFactory.PrepareReturnRequestActionModelAsync(new ReturnRequestActionModel(), null);

        return View("~/Plugins/CW.Core/Areas/Admin/Views/ReturnRequest/ReturnRequestActionCreatePopup.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SETTINGS)]
    public virtual async Task<IActionResult> ReturnRequestActionCreatePopup(ReturnRequestActionModel model)
    {
        if (ModelState.IsValid)
        {
            var returnRequestAction = model.ToEntity<ReturnRequestAction>();
            await _returnRequestService.InsertReturnRequestActionAsync(returnRequestAction);

            //locales
            await UpdateLocalesAsync(returnRequestAction, model);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Configuration.Settings.Order.ReturnRequestActions.Added"));

            ViewBag.RefreshPage = true;

            return View("~/Plugins/CW.Core/Areas/Admin/Views/ReturnRequest/ReturnRequestActionCreatePopup.cshtml", model);
        }

        //prepare model
        model = await _returnRequestModelFactory.PrepareReturnRequestActionModelAsync(model, null, true);

        //if we got this far, something failed, redisplay form
        return View("~/Plugins/CW.Core/Areas/Admin/Views/ReturnRequest/ReturnRequestActionCreatePopup.cshtml", model);
    }

    #endregion
}