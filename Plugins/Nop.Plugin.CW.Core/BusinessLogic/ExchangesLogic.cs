using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Products;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.BusinessLogic;

public class ExchangesLogic : IExchangesLogic
{
    #region Fields

    protected readonly IOrderService _orderService;
    protected readonly IProductAttributeFormatter _productAttributeFormatter;
    protected readonly IOrderTotalCalculationOverrideService _orderTotalCalculationService;
    protected readonly ICustomerService _customerService;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly IWorkContext _workContext;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly ISubsidyCreditService _subsidyCreditService;
    protected readonly IProductService _productService;
    protected readonly IProductAttributeService _productAttributeService;
    protected readonly IProductAttributeParser _productAttributeParser;
    protected readonly ILogger _logger;
    protected readonly ICreditNotesService _creditNotesService;
    protected readonly IExchangeService _exchangeService;
    protected readonly IOrderProcessingOverrideService _cwOrderProcessingService;
    protected readonly IExchangeWorkflowMessageService _exchangeWorkflowMessageService;
    protected readonly IReturnCaseService _returnCaseService;
    protected readonly ILocker _locker;

    #endregion

    #region Ctor

    public ExchangesLogic(IOrderService orderService,
        IExchangeService exchangeService,
        IProductAttributeFormatter productAttributeFormatter,
        IOrderTotalCalculationOverrideService orderTotalCalculationService,
        ICustomerService customerService,
        ICustomerActivityService customerActivityService,
        IWorkContext workContext,
        ICreditNotesService creditNotesService,
        IReturnRequestService returnRequestService,
        IShoppingCartOverrideService shoppingCartService,
        IProductAttributeService productAttributeService,
        IProductAttributeParser productAttributeParser,
        ILogger logger,
        IOrderProcessingOverrideService cwOrderProcessingService,
        IProductService productService,
        IExchangeWorkflowMessageService exchangeWorkflowMessageService,
        ICWOrderService cwOrderService,
        ISubsidyCreditService subsidyCreditService,
        IReturnCaseService returnCaseService,
        ILocker locker)
    {
        _orderService = orderService;
        _exchangeService = exchangeService;
        _productAttributeFormatter = productAttributeFormatter;
        _orderTotalCalculationService = orderTotalCalculationService;
        _customerService = customerService;
        _customerActivityService = customerActivityService;
        _workContext = workContext;
        _creditNotesService = creditNotesService;
        _returnRequestService = returnRequestService;
        _shoppingCartService = shoppingCartService;
        _productAttributeService = productAttributeService;
        _productAttributeParser = productAttributeParser;
        _logger = logger;
        _cwOrderProcessingService = cwOrderProcessingService;
        _productService = productService;
        _exchangeWorkflowMessageService = exchangeWorkflowMessageService;
        _cwOrderService = cwOrderService;
        _subsidyCreditService = subsidyCreditService;
        _returnCaseService = returnCaseService;
        _locker = locker;
    }

    #endregion

    #region Utilities

    #region Approval decisions

    protected async Task<decimal?> CalculateExchangeAmountAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
        // We simulate a "shopping cart list" in order to reuse the NOP logic to calculate the totals
        var productItems = await _exchangeService.BuildProductsListForReturnOrderAsync(returnRequest, returnRequestItem);
        return await CalculateCartItemsTotalAmountAsync(productItems);
    }

    /// <returns>The exchange total (amount + fees) and the fees component.</returns>
    protected async Task<(decimal total, decimal exchangeFeesAmount)> CalculateExchangeTotalAmountAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
        decimal? exchangeAmount = await CalculateExchangeAmountAsync(returnRequest, returnRequestItem);
        if (!exchangeAmount.HasValue)
        {
            throw new NopException("Cannot determine Exchange Order total amount");
        }
        var exchangeFeesAmount = await _exchangeService.CalculateExchangeFeesAmountAsync(returnRequestItem);
        return (exchangeAmount.Value + exchangeFeesAmount, exchangeFeesAmount);
    }

    protected async Task<decimal> DetermineCurrentCreditNotesBalanceAsync(int customerId)
    {
        var creditNotes = await _creditNotesService.GetOpenCreditNotesByCustomerIdAsync(customerId);
        return creditNotes.Sum(c => c.Remain);
    }

    /// <summary>
    /// Whether the money the customer put into the returned line comes back as a credit note.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules decide it: every one of the status, reason and action rules has to allow store credit.
    /// A missing rule row means "not configured" = "not allowed" (fail closed).
    /// </para>
    /// <para>
    /// And ALWAYS when an exchange is actually being performed, whatever the store-credit flags say.
    /// Without this, a reason/action pair that allows Exchange but not Store Credit charges the customer
    /// the replacement's full price on top of the fees, because nothing gives back what they already paid
    /// for the garment they sent in. A customer must never pay twice for one garment.
    /// </para>
    /// What the note is FOR is not decided here: it is only ever the customer's own money — the returned
    /// value minus the subsidy given back in kind. A cheaper replacement needs no rule of its own: the
    /// difference is left on the note or in the subsidy pot by arithmetic.
    /// </remarks>
    protected bool IsCreditNoteNeeded(ReturnStatusRule statusRule, ReturnReasonRule reasonRule, ReturnActionRule actionRule, bool exchangeNeeded)
    {
        var rulesAllowStoreCredit = statusRule is { PerformStoreCredit: true }
            && reasonRule is { AllowStoreCredit: true }
            && actionRule is { AllowStoreCredit: true };

        return rulesAllowStoreCredit || exchangeNeeded;
    }

    protected bool IsExchangeNeeded(ReturnStatusRule statusRule, ReturnReasonRule reasonRule, ReturnActionRule actionRule)
    {
        return statusRule is { PerformExchange: true }
            && reasonRule is { AllowExchange: true }
            && actionRule is { AllowExchange: true };
    }

    #endregion

    #region Restoration

    /// <summary>
    /// Gives back, in kind, the subsidy the returned quantity consumed. Runs before anything else the
    /// line does — before the credit note is sized and before the replacement reaches the cart — so the
    /// note is sized against what came back, and an add-to-cart guarded by RestrictToAllocatedQty sees
    /// the returned units.
    /// </summary>
    /// <returns>What was restored; <see cref="SubsidyRestoreResult.Empty"/> when nothing could be.</returns>
    protected async Task<SubsidyRestoreResult> RestoreReturnedLineAsync(ReturnRequest returnRequest, OrderItem orderItem)
    {
        var restoringLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequest.Id);

        if (restoringLine == null)
        {
            //No case line means no key to make restoration idempotent on, so it is skipped rather than
            //risked. A return raised before the case model existed lands here. The credit note is then
            //sized as if nothing came back.
            await _logger.WarningAsync($"[Exchanges] RR {returnRequest.Id}: no return case line, so the subsidy consumed by " +
                $"order item {orderItem.Id} was NOT restored. Restore it by hand if the order consumed subsidy.");

            return SubsidyRestoreResult.Empty;
        }

        var restored = await _subsidyCreditService.RestoreSubsidyForReturnedItemAsync(new SubsidyRestoreRequest
        {
            OrderItemId = orderItem.Id,
            OrderedQuantity = orderItem.Quantity,
            ReturnedQuantity = returnRequest.Quantity,
            ReturnCaseLineId = restoringLine.Id,
            ReturnRequestId = returnRequest.Id,
            ActingCustomerId = (await _workContext.GetCurrentCustomerAsync())?.Id
        });

        await _logger.InformationAsync($"[Exchanges] RR {returnRequest.Id}: restored subsidy for order item {orderItem.Id} " +
            $"({returnRequest.Quantity} of {orderItem.Quantity}) -> {restored.RowsWritten} compensating row(s), " +
            $"{restored.UnitsRestored} unit(s), {restored.CreditRestored} credit, money equivalent {restored.MoneyEquivalent}.");

        return restored;
    }

    #endregion

    #region Credit note workflow

    /// <summary>
    /// Raises the credit note for the money the customer put into the returned quantity — its value
    /// minus the subsidy just given back in kind. The value comes back exactly once, in the form it was
    /// paid.
    /// </summary>
    /// <returns>
    /// <c>noteCreated</c> is true only when a note was actually written. <c>feesNotDeductible</c> is
    /// true for a Credit-Note-mode line that carried fees but had no money to deduct them from — the
    /// caller surfaces it so Sales Support can adjust the fee quantities.
    /// </returns>
    protected async Task<(bool noteCreated, bool feesNotDeductible)> ExecuteCreditNoteWorkflowAsync(ReturnRequest returnRequest,
        ReturnRequestItem returnRequestItem,
        SubsidyRestoreResult restored)
    {
        var returnedValue = await DetermineExchangeCreditNoteAmountAsync(returnRequest)
            ?? throw new NopException("Cannot determine Credit Note amount");

        // Only the customer's own money comes back as a note. A unit is valued at the order item's unit
        // price, the same way the order's subsidy total values it, so a fully unit-funded line nets to
        // zero here and raises nothing.
        // Rounded to cents: a partial restoration is pro-rated to 4 dp, and a note is money the
        // customer sees.
        var creditNoteAmount = Math.Round(returnedValue - restored.MoneyEquivalent, 2, MidpointRounding.AwayFromZero);

        if (creditNoteAmount < decimal.Zero)
            creditNoteAmount = decimal.Zero;

        var feesNotDeductible = false;

        // If we are in Credit Note mode we must discount the fees from the credit note amount
        if (returnRequestItem.ExchangeMode == ExchangeReturnMode.CreditNote)
        {
            var (_, exchangeFeesAmount) = await CalculateExchangeTotalAmountAsync(returnRequest, returnRequestItem);

            // No money to take the fees from: the line was paid by subsidy, which went back in kind.
            // Nothing is deducted from anything — the approval screen says so, and the fee boxes are the
            // lever.
            //Strictly less than: a note that exactly equals the fees WAS fully deducted — the customer
            //receives nothing and the store collected the fee by not issuing it, which is the fee working,
            //not failing. Only a shortfall is worth interrupting anyone for.
            feesNotDeductible = exchangeFeesAmount > decimal.Zero && creditNoteAmount < exchangeFeesAmount;

            creditNoteAmount -= exchangeFeesAmount;
        }

        if (creditNoteAmount > decimal.Zero)
        {
            var actingCustomer = await _workContext.GetCurrentCustomerAsync();

            var order = await _exchangeService.GetOrderForReturnRequestAsync(returnRequest);
            var reason = $"Credit Note accounting for Return Request ID: {returnRequest.Id}";
            await _logger.InformationAsync($"[Exchanges] RR {returnRequest.Id}: creating Credit Note of {creditNoteAmount} " +
                $"(returned value {returnedValue}, subsidy restored in kind {restored.MoneyEquivalent}) for order {order?.Id} (customer {order?.CustomerId}).");
            await _creditNotesService.CreateCreditNotesForOrderAsync(order, creditNoteAmount, reason, returnRequest.Id, actingCustomer?.Id);

            return (true, feesNotDeductible);
        }

        await _logger.InformationAsync($"[Exchanges] RR {returnRequest.Id}: no Credit Note raised — returned value {returnedValue}, " +
            $"subsidy restored in kind {restored.MoneyEquivalent}, computed note {creditNoteAmount}.");

        return (false, feesNotDeductible);
    }

    #endregion

    #region Exchange workflow

    /// <summary>
    /// Places the replacement order outright, or puts the replacement in the customer's cart.
    /// </summary>
    /// <param name="subsidyWasRestored">
    /// True when the returned line gave subsidy back in kind. Not used to pick the pathway — a settlement
    /// quote decides that, not this guess — but kept on the workflow's contract, since the caller computes
    /// it either way and a future rule may want it.
    /// </param>
    protected async Task<ExchangeWorkflowResult> ExecuteExchangeWorkflowAsync(ReturnRequest returnRequest,
        ReturnRequestItem returnRequestItem,
        OrderItem orderItem,
        bool creditNoteWasApplied,
        bool subsidyWasRestored)
    {
        //Everything from here on prices the replacement, and the first thing it does is decide which
        //pathway this return takes. That decision settles the replacement through the pipeline, so its
        //value has to be the one the customer actually paid — a detached template priced
        //live reads the qty-1 price and routes a tier-priced Exact exchange to the cart when the note
        //covers it outright. Nested inside PlaceReturnOrderAsync and
        //RegisterApprovedExchangedItemsAsync, which open it again; Scope.Dispose restores rather than
        //clears, so the nesting is harmless.
        using var replacementScope = ExchangeReplacementScope.Begin(returnRequestItem);

        decimal creditNotesBalance = await DetermineCurrentCreditNotesBalanceAsync(returnRequest.CustomerId);
        var (total, exchangeFeesAmount) = await CalculateExchangeTotalAmountAsync(returnRequest, returnRequestItem);
        var result = new ExchangeWorkflowResult();

        try
        {
            // First pathway: settlement covers the replacement outright and this return owes no fees, so
            // the order can simply be placed and the customer left alone.
            //
            // The decision is a quote, not a guess. QuoteReturnOrderSettlementAsync walks the same
            // pipeline against the same packname cart that PlaceReturnOrderAsync will settle, so
            // BalanceDue == 0 is a promise rather than an estimate — which matters because the everyday
            // case (faulty garment, no fees, one unit back, one unit out) is fully covered and must not be
            // sent to the cart for a $0 checkout.
            //
            // The fee test is THIS return request's fees. Every fee is priced over this line, so the
            // figure is final the moment the line is authorised and the line can be settled on its own
            // without waiting for a sibling.
            var settlementQuote = await _cwOrderProcessingService.QuoteReturnOrderSettlementAsync(returnRequest, returnRequestItem);

            // A quote that FAILED is indistinguishable from one that covered everything: both report
            // BalanceDue 0, because an empty SettlementPlan has a SettleableAmount of 0. Five paths return
            // one — no customer, an empty cart, a zero total, and the two QuoteCartSettlementAsync logs a
            // warning for. So require the quote to have actually priced something before trusting it, and
            // let a replacement that is genuinely worth nothing through on its own value. A failure then
            // falls to the cart, which is the safe direction: worst case the customer clicks once on a $0
            // checkout, instead of an order being placed that nothing has paid for.
            var replacementValue = total - exchangeFeesAmount;

            var settlementCoversReplacement = replacementValue <= decimal.Zero
                || (settlementQuote.SettleableAmount > decimal.Zero && settlementQuote.BalanceDue <= decimal.Zero);

            // An auto-placed order is built from a DETACHED cart, so none of nop's item warnings run —
            // attributes, required products, rentals, gift cards. The cart pathway runs all of them via
            // AddToCartAsync. Without this check the two disagree, and pathway A will place an order for a
            // replacement the customer could never have carted: nop's Custom T-Shirt, whose required
            // "Enter your text" attribute the picker does not collect, went out as an order line with no
            // attributes at all. Falling to the cart is the safe direction — it fails loudly and correctly
            // there, and the case stays open for Sales Support instead of a garment shipping unmade.
            if (settlementCoversReplacement && exchangeFeesAmount <= decimal.Zero
                && await ReplacementPassesCartValidationAsync(returnRequest, returnRequestItem))
            {
                var placeOrderResult = await _cwOrderProcessingService.PlaceReturnOrderAsync(returnRequest, returnRequestItem);

                if (placeOrderResult.Success)
                {
                    // At this stage we can mark the Exchange Request as "complete"
                    await _exchangeService.SetExchangeRequestCompletedAsync(returnRequestItem);

                    result.NeedsCompletion = false;
                    result.PlacedOrder = placeOrderResult.PlacedOrder;

                    // TODO Sync the new order to the ERP

                    #region CW Notifications

                    var paidOrder = placeOrderResult.PlacedOrder;

                    // Notify the customer about the approval
                    await _exchangeWorkflowMessageService.SendReturnRequestApprovedExceptedCustomerNotificationAsync(returnRequest, paidOrder, orderItem);

                    #endregion
                }
                else
                {
                    result.Errors = placeOrderResult.Errors;

                    // Undo everything we did as the workflow could not be completed
                    await CleanupFailedExchangeWorkflowAsync(returnRequest, creditNoteWasApplied);
                }
            }
            else // Second pathway: the replacement goes to the cart and the customer checks out (extra money, fees, or the restored entitlement pays)
            {
                if (await RegisterApprovedExchangedItemsAsync(returnRequest, returnRequestItem))
                {
                    // We must flag the Exchange request as needing further actions (needs completion)
                    await _exchangeService.SetExchangeRequestNeedsCompletionAsync(returnRequestItem);

                    result.NeedsCompletion = true;

                    #region CW Notifications

                    var pendingAmount = await DeterminePendingAmountAsync(returnRequest, total, exchangeFeesAmount, creditNotesBalance);

                    // Notify the customer about the approval. The item is passed explicitly so the email
                    // itemises the same instance this workflow priced against — the controller has already
                    // committed it, so this is about not re-reading the row rather than outrunning a commit.
                    await _exchangeWorkflowMessageService.SendReturnRequestApprovedPaymentCustomerNotificationAsync(returnRequest, orderItem, pendingAmount, returnRequestItem);

                    #endregion

                }
                else // Given we could not add the approved items to the user's cart we must undo everything we did
                {
                    await CleanupFailedExchangeWorkflowAsync(returnRequest, creditNoteWasApplied);

                    result.AddError("Could not add approved exchanged items to the customer's cart");
                }
            }
        }
        catch (Exception ex)
        {
            result.AddError(ex.Message);

            //An unexpected error occurred, we need to undo everything
            await CleanupFailedExchangeWorkflowAsync(returnRequest, creditNoteWasApplied);
        }
        return result;
    }

    /// <summary>
    /// What the customer will be asked to pay when they open their cart. Three attempts, most exact
    /// first: the cart's payable total after subsidy, credit notes and fees (the figure the cart page
    /// shows); if nop cannot total the cart from here — it cannot rate shipping without a selected
    /// option, which an admin request never has — the settlement quote alone against the replacement's
    /// value, plus this line's fees (shipping on an exchange IS the redelivery fee, already in them);
    /// and only then a plain estimate, which knows nothing of subsidy.
    /// </summary>
    protected async Task<decimal> DeterminePendingAmountAsync(ReturnRequest returnRequest, decimal total, decimal exchangeFeesAmount, decimal creditNotesBalance)
    {
        var fallback = Math.Max(exchangeFeesAmount, total - creditNotesBalance);

        try
        {
            var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

            if (customer == null)
                return fallback;

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, returnRequest.StoreId);

            if (cart.Count == 0)
                return fallback;

            var (payable, _, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);

            if (payable.HasValue)
                return payable.Value;

            // Budget = the replacement's value, walked over the whole cart in cart order. Exact when the
            // replacement is the only line; an estimate when the customer already had other goods in
            // the cart, and the cart page is the authority either way.
            var replacementValue = total - exchangeFeesAmount;
            var plan = await _orderTotalCalculationService.QuoteCartSettlementAsync(cart, replacementValue);

            return plan.BalanceDue + exchangeFeesAmount;
        }
        catch (Exception ex)
        {
            await _logger.WarningAsync($"[Exchanges] RR {returnRequest.Id}: could not total the customer's cart for the pending-payment email; quoting {fallback} instead.", ex);

            return fallback;
        }
    }

    protected async Task UndoApprovedExchangeItemsAsync(ReturnRequest returnRequest)
    {
        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);
        var cartItems = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, returnRequest.StoreId);
        foreach (var item in cartItems)
        {
            var returnRequestItem = await _exchangeService.GetReturnRequestItemForCartItemAsync(item.Id);
            // Only undo cart items that belong to this return request
            if (returnRequestItem?.ReturnRequestId == returnRequest.Id)
            {
                returnRequestItem.ShoppingCartItemId = null;
                returnRequestItem.IsCompleted = false;
                returnRequestItem.NeedsCompletion = false;
                await _exchangeService.UpdateReturnRequestItemAsync(returnRequestItem);
                await _shoppingCartService.DeleteShoppingCartItemAsync(item);
            }
        }
    }

    protected async Task CleanupFailedExchangeWorkflowAsync(ReturnRequest returnRequest, bool creditNoteWasApplied)
    {
        if (creditNoteWasApplied)
            await _creditNotesService.UndoCreditNoteForReturnRequestAsync(returnRequest.Id);

        await UndoApprovedExchangeItemsAsync(returnRequest);

        //Undo has to reach the ledger too, or an undone approval leaves the entitlement given back.
        //RestoreSubsidyForReturnedItemAsync clears the line before writing, so re-approving after this
        //restores exactly once.
        var caseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequest.Id);

        if (caseLine != null)
            await _subsidyCreditService.DeleteSubsidyRestoreItemsForCaseLineAsync(caseLine.Id);
    }

    protected virtual async Task<bool> RegisterApprovedExchangedItemsAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);
        var productItems = await _exchangeService.BuildProductsListForReturnOrderAsync(returnRequest, returnRequestItem);

        // Resolve the packname from the originally returned item so it travels with the replacement items
        var sourcePackName = await _cwOrderService.GetPackNameOrderItemAsync(returnRequestItem.ReturnedOrderItemId);

        var addedCartItems = new List<ShoppingCartItem>();

        //The catalogue's selling rules do not govern a replacement — the customer already owns the item and
        //may be swapping one of a pack that can only be BOUGHT in twos. Held open across the whole loop and
        //closed by the using even if an add throws.
        using var replacementScope = ExchangeReplacementScope.Begin(returnRequestItem);

        //BuildProductsListForReturnOrderAsync returns DETACHED template items — ProductId, AttributesXml
        //and Quantity only, Id == 0. They describe what to add; they are not cart rows. Naming them
        //`template` so nothing is tempted to treat them as one again.
        foreach (var template in productItems)
        {
            var product = await _productService.GetProductByIdAsync(template.ProductId);

            if (product == null)
            {
                await _logger.ErrorAsync($"Exchange approval: product {template.ProductId} for return request {returnRequest.Id} no longer exists");
                await RollBackAsync();
                return false;
            }

            var warnings = sourcePackName != null
                ? await _shoppingCartService.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, returnRequest.StoreId,
                    sourcePackName.PackNameId, template.AttributesXml, quantity: template.Quantity)
                : await _shoppingCartService.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, returnRequest.StoreId,
                    template.AttributesXml, quantity: template.Quantity);

            if (warnings.Any())
            {
                await _logger.ErrorAsync($"Exchange approval: could not add product {product.Id} to the cart for return request {returnRequest.Id}: {string.Join("; ", warnings)}");
                await RollBackAsync();
                return false;
            }

            //AddToCartAsync returns warnings, not the row it wrote, so find it the same way AddToCartAsync
            //itself decided whether to merge or insert — same cart, same matcher. This is also why it must
            //run AFTER the add and not before: if the customer already had this product in the packname,
            //the add merged into that existing row and THAT is the row to link.
            var cart = sourcePackName != null
                ? await _shoppingCartService.GetPackNameCartAsync(customer, sourcePackName.PackNameId, returnRequest.StoreId)
                : await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, returnRequest.StoreId);

            var cartItem = await _shoppingCartService.FindShoppingCartItemInTheCartAsync(
                cart, ShoppingCartType.ShoppingCart, product, template.AttributesXml);

            if (cartItem == null)
            {
                //AddToCartAsync can return no warnings and still add nothing — e.g. the packname resolves
                //to null/inactive/deleted, where the packname overload returns an empty warning list and
                //exits. Never trust "no warnings" as proof the row exists.
                await _logger.ErrorAsync($"Exchange approval: product {product.Id} was not found in the cart after adding it for return request {returnRequest.Id}");
                await RollBackAsync();
                return false;
            }

            addedCartItems.Add(cartItem);

            // Relate this cart item to the Return Request Item so we can identify it once customer performs checkout later
            await _exchangeService.AssignCartItemToReturnRequestItemAsync(cartItem, returnRequestItem);
        }

        return true;

        async Task RollBackAsync()
        {
            foreach (var added in addedCartItems)
                await _shoppingCartService.DeleteShoppingCartItemAsync(added);

            returnRequestItem.ShoppingCartItemId = null;
            await _exchangeService.UpdateReturnRequestItemAsync(returnRequestItem);
        }
    }

    /// <summary>
    /// Whether the replacement would survive the validation a cart applies to it.
    /// </summary>
    /// <remarks>
    /// Called inside <see cref="ExchangeReplacementScope"/>, so the quantity exemption for a partial
    /// exchange (fixes 7 and 10) is in force and a 2/4/6 product is not rejected for being swapped one at
    /// a time. Everything else — required attributes above all — is enforced exactly as the cart enforces it.
    /// </remarks>
    protected virtual async Task<bool> ReplacementPassesCartValidationAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        if (customer == null)
            return false;

        var templates = await _exchangeService.BuildProductsListForReturnOrderAsync(returnRequest, returnRequestItem);

        foreach (var template in templates)
        {
            var product = await _productService.GetProductByIdAsync(template.ProductId);

            if (product == null)
                return false;

            var warnings = await _shoppingCartService.GetShoppingCartItemWarningsAsync(
                customer,
                ShoppingCartType.ShoppingCart,
                product,
                returnRequest.StoreId,
                template.AttributesXml,
                template.CustomerEnteredPrice,
                template.RentalStartDateUtc,
                template.RentalEndDateUtc,
                template.Quantity);

            if (!warnings.Any())
                continue;

            //Not silent: the case is about to take the longer route and Sales Support will see it fail in
            //the cart, so the reason belongs in the log where it can be matched to the return.
            await _logger.WarningAsync($"[Exchange] The replacement for return request {returnRequest.Id} cannot be added to a cart, so it will not be auto-placed: {string.Join("; ", warnings)}");

            return false;
        }

        return true;
    }

    #endregion

    #region Status-change workflow

    /// <summary>
    /// The key both the approval and its undo serialise on. One resource: they write the same rows.
    /// </summary>
    protected static string ApprovalLockKey(int returnRequestId) => $"cw_returnrequest_approve_{returnRequestId}";

    /// <summary>
    /// The body of UndoReturnRequestApprovalAsync, run under the return request's lock. Never call it
    /// directly — going straight here can interleave with a running approval.
    /// </summary>
    protected virtual async Task UndoReturnRequestApprovalCoreAsync(ReturnRequest returnRequest)
    {
        await CleanupFailedExchangeWorkflowAsync(returnRequest, true);

        // Reset the return request status
        returnRequest.ReturnRequestStatus = ReturnRequestStatus.Pending;
        await _returnRequestService.UpdateReturnRequestAsync(returnRequest);

        var currentCustomer = await _workContext.GetCurrentCustomerAsync();
        var activityLog = $"Approval for Return Request '{returnRequest.Id}' has been undone/reset";
        await _customerActivityService.InsertActivityAsync(currentCustomer, "EditReturnRequest", activityLog, returnRequest);
    }

    /// <summary>
    /// The body of ExecuteApprovedLineAsync, run under the return request's lock. Never call it directly
    /// — going straight here bypasses the guard against an approval executing twice.
    /// </summary>
    protected virtual async Task<ReturnRequestWorkflowResult> ExecuteApprovedLineCoreAsync(ReturnRequest returnRequest, int newStatusId)
    {
        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId) ?? throw new NopException("The Order Item related to this Return Request no longer exists");
        var workflowResult = new ReturnRequestWorkflowResult();
        var storeId = returnRequest.StoreId;

        //The inner gate, and it is not optional. The controller only knows that the status CHANGED;
        //this is where we decide whether the change is the one that authorises the return. Without it a
        //save that rejects, cancels or merely receives the item runs the whole approval workflow — the
        //rule below is looked up for Return Authorized no matter what was chosen, and that rule is
        //Authorised on every working store, so nothing further would stop it.
        if (newStatusId != (int)ReturnRequestStatus.ReturnAuthorized)
            return workflowResult;

        //Looked up BY the new status. Identical to hardcoding Return Authorized now that the
        //gate above has run, but it keeps the two in step if another status ever becomes authorising.
        //Reading the rule rather than the nop row also means the row's own status does not have to have
        //been written yet — it has not, at this point in the save.
        var statusRule = await _exchangeService.GetRuleByStatusIdAsync(newStatusId, storeId);

        await _logger.InformationAsync($"[Exchanges] RR {returnRequest.Id}: executing an authorised return request (store {storeId}). " +
            (statusRule == null
                ? "No matching Return Status Rule was found -> nothing is executed (no credit note, no exchange)."
                : $"StatusRule => Authorised={statusRule.Authorised}, PerformStoreCredit={statusRule.PerformStoreCredit}, PerformExchange={statusRule.PerformExchange}."));

        // Only process workflows for rules flagged Authorised
        if (statusRule is not { Authorised: true })
            return workflowResult;

        var returnRequestItem = await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);

        if (returnRequestItem == null)
            return workflowResult;

        //No overlay. The controller commits the staff quantities, the ad-hoc fee, the reason and the
        //action before calling this, so everything below is the persisted truth. Committing first, rather
        //than overlaying the posted model onto an in-memory item, reaches the same figures with nothing to
        //keep in step.
        var selectedReason = await _exchangeService.GetSelectedReasonAsync(returnRequestItem);
        var selectedAction = await _exchangeService.GetSelectedActionAsync(returnRequestItem);

        if (selectedReason == null || selectedAction == null)
        {
            await _logger.WarningAsync($"[Exchanges] RR {returnRequest.Id}: cannot resolve return reason " +
                $"(id {returnRequestItem?.ReturnReasonId}) or action (id {returnRequestItem?.ReturnActionId}) " +
                $"-> workflow stops, no credit note, no exchange.");
            return workflowResult;
        }

        var reasonRule = await _exchangeService.GetReturnReasonRuleByReasonIdAsync(selectedReason.Id, storeId);
        var actionRule = await _exchangeService.GetReturnActionRuleByActionIdAsync(selectedAction.Id, storeId);

        // Decided first, because the credit-note decision depends on it: an exchange must never charge the
        // customer again for value they have already paid.
        var exchangeNeeded = returnRequestItem.ExchangeMode == ExchangeReturnMode.Exchange
            && IsExchangeNeeded(statusRule, reasonRule, actionRule);

        var creditNoteNeeded = IsCreditNoteNeeded(statusRule, reasonRule, actionRule, exchangeNeeded);
        await _logger.InformationAsync($"[Exchanges] RR {returnRequest.Id}: exchangeMode={returnRequestItem.ExchangeMode}, " +
            $"reasonRule={(reasonRule == null ? "NOT FOUND" : $"AllowStoreCredit={reasonRule.AllowStoreCredit}, AllowExchange={reasonRule.AllowExchange}")}, " +
            $"actionRule={(actionRule == null ? "NOT FOUND" : $"AllowStoreCredit={actionRule.AllowStoreCredit}, AllowExchange={actionRule.AllowExchange}")}, " +
            $"exchangeNeeded={exchangeNeeded} => creditNoteNeeded={creditNoteNeeded}");

        // The rules deny both outcomes: the return is authorised but nothing is given back and nothing
        // is exchanged. Restoring here would hand back entitlement for a line the rules say does nothing.
        if (!creditNoteNeeded && !exchangeNeeded)
            return workflowResult;

        // 1. Value back in kind — units to units, group credit to that group, global to global — BEFORE
        //    the note is sized and BEFORE the replacement reaches the cart (decisions 7, 14, 18).
        var restored = await RestoreReturnedLineAsync(returnRequest, orderItem);

        workflowResult.SubsidyWasRestored = restored.RestoredAnything;

        // 2. The money the customer put in comes back as a credit note — returned value minus what went
        //    back in kind, minus the fees in Credit-Note mode.
        if (creditNoteNeeded)
        {
            //Not "true because we tried". A zero-or-negative amount writes nothing — a fully subsidised
            //line raises no note at all — and the cleanup below only undoes a note that exists.
            var (noteCreated, feesNotDeductible) = await ExecuteCreditNoteWorkflowAsync(returnRequest, returnRequestItem, restored);

            workflowResult.CreditNoteWasCreated = noteCreated;
            workflowResult.FeesNotDeductible = feesNotDeductible;
        }

        // 3. The replacement — placed outright or put in the cart, where the ordinary pipeline settles it
        //    against the restored entitlement first, then credit notes, then the customer.
        if (exchangeNeeded)
        {
            var exchangeWorkflowResult = await ExecuteExchangeWorkflowAsync(returnRequest, returnRequestItem, orderItem,
                workflowResult.CreditNoteWasCreated, workflowResult.SubsidyWasRestored);

            workflowResult.NeedsCompletion = exchangeWorkflowResult.NeedsCompletion;
            workflowResult.Errors = exchangeWorkflowResult.Errors;
            workflowResult.OrderWasPlaced = exchangeWorkflowResult.PlacedOrder != null;
        }
        else
        {
            // Nothing further is expected of this line, so close it. This has to cover an Exchange-mode
            // line that only produced a credit note as well as a Credit-Note-mode one. Closing only the
            // Credit-Note-mode case would be safe only if a line could never execute twice — but a case is
            // re-opened when a decision changes, so a line left with neither flag would raise a second
            // credit note.
            await _exchangeService.SetExchangeRequestCompletedAsync(returnRequestItem);
        }

        return workflowResult;
    }

    #endregion

    #endregion

    #region Methods

    #region Cart / price calculations (used by the return wizard)

    public IList<ShoppingCartItem> BuildSingleProductItemCart(Store store, int productId, Customer customer,
        string attributesXml, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(customer);

        var now = DateTime.UtcNow;
        return
        [
            new ShoppingCartItem
            {
                ShoppingCartType = ShoppingCartType.ShoppingCart,
                StoreId = store.Id,
                ProductId = productId,
                CustomerId = customer.Id,
                AttributesXml = attributesXml,
                CustomerEnteredPrice = decimal.Zero,
                Quantity = quantity,
                CreatedOnUtc = now,
                UpdatedOnUtc = now,
            }
        ];
    }

    /// <summary>
    /// Sub-total of the given cart (excludes shipping and other additional amounts), including tax.
    /// </summary>
    public virtual async Task<decimal?> CalculateCartItemsTotalAmountAsync(IList<ShoppingCartItem> cart)
    {
        var (_, _, _, subTotalWithDiscount, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, true);
        return subTotalWithDiscount;
    }

    /// <summary>
    /// The value of the quantity being returned — unit price incl tax × returned quantity. This is the
    /// ceiling of any credit note for the line; the note actually raised is this figure minus whatever
    /// subsidy went back in kind (ExecuteApprovedLineAsync).
    /// </summary>
    public async Task<decimal?> DetermineExchangeCreditNoteAmountAsync(ReturnRequest returnRequest)
    {
        var returnedItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);
        if (returnedItem != null)
        {
            var amount = ExchangesHelper.DetermineReturnedItemPrice(returnedItem);
            return amount * returnRequest.Quantity;
        }
        return null;
    }

    #endregion

    #region Replacement product attributes (used by the wizard "Change exchange item" action)

    public virtual async Task<string> GenerateProductAttributesXmlAsync(Product product, IEnumerable<SelectedProductAttribute> selectedAttributes)
    {
        var attributesXml = string.Empty;
        var productAttributes = await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id);
        foreach (var selectedAttribute in selectedAttributes)
        {
            var productAttribute = productAttributes.FirstOrDefault(x => x.Id == selectedAttribute.AttributeId);
            if (productAttribute == null)
                continue;

            // LinqToDB: attribute values are fetched via the service (no nav property)
            var attributeValues = await _productAttributeService.GetProductAttributeValuesAsync(productAttribute.Id);
            var attributeValue = attributeValues.FirstOrDefault(x => x.Id == selectedAttribute.AttributeValueId);
            if (attributeValue != null)
                attributesXml = _productAttributeParser.AddProductAttribute(attributesXml, productAttribute, attributeValue.Id.ToString());
        }
        return attributesXml;
    }

    /// <summary>
    /// Changes the options of an exchange's replacement; an option not posted keeps the value it has
    /// </summary>
    /// <param name="returnRequestItem">The exchange</param>
    /// <param name="selectedAttributes">The options chosen</param>
    /// <returns>A task that represents the asynchronous operation; the task result is false when the exchange has no replacement to change</returns>
    public virtual async Task<bool> ModifyExchangedItemDetailsAsync(ReturnRequestItem returnRequestItem, IEnumerable<SelectedProductAttribute> selectedAttributes)
    {
        var replacementItem = await _exchangeService.GetReplacementItemAsync(returnRequestItem.Id);
        if (replacementItem == null)
            return false;

        var replacementProduct = await _productService.GetProductByIdAsync(replacementItem.ProductId);
        if (replacementProduct == null)
            return false;

        //the replacement's current options, with the posted ones in their place
        var selections = new Dictionary<int, int>();
        foreach (var mapping in await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(replacementProduct.Id))
            if (int.TryParse(_productAttributeParser.ParseValues(replacementItem.AttributesXml, mapping.Id).FirstOrDefault(), out var currentValueId))
                selections[mapping.Id] = currentValueId;

        foreach (var selected in selectedAttributes)
            selections[selected.AttributeId] = selected.AttributeValueId;

        var attributesXml = await GenerateProductAttributesXmlAsync(replacementProduct,
            selections.Select(selection => new SelectedProductAttribute { AttributeId = selection.Key, AttributeValueId = selection.Value }));
        if (string.IsNullOrEmpty(attributesXml))
            return false;

        replacementItem.AttributesXml = attributesXml;
        replacementItem.AttributesInfo = await _productAttributeFormatter.FormatAttributesAsync(replacementProduct, attributesXml);

        await _exchangeService.UpdateReplacementItemAsync(replacementItem);

        return true;
    }

    #endregion

    #region Status-change workflow

    public virtual async Task<ReturnRequestWorkflowResult> ExecuteApprovedLineAsync(ReturnRequest returnRequest, int newStatusId)
    {
        var workflowResult = new ReturnRequestWorkflowResult();

        // Serialised on the return request. The core restores entitlement, raises a credit note and
        // places an order; running it twice concurrently — a double-clicked Save, a retried POST — does
        // all three twice, and nothing downstream would notice.
        //
        // 60 seconds, not the 30 used for the case lock: pathway A places a real order inside the core,
        // which takes the packname locks, adjusts inventory and settles. Lock ordering is safe because
        // this is the OUTERMOST lock — the credit-note, subsidy-credit and packname locks are only ever
        // taken further in, and nothing that holds them ever asks for this one.
        //
        // The lambda needs a BRACED body. An expression-bodied `async () => x = await ...` is a
        // Func<Task<T>> and will not convert to the Func<Task> this overload takes.
        var ran = await _locker.PerformActionWithLockAsync(
            ApprovalLockKey(returnRequest.Id),
            TimeSpan.FromSeconds(60),
            async () =>
            {
                workflowResult = await ExecuteApprovedLineCoreAsync(returnRequest, newStatusId);
            });

        if (!ran)
        {
            // Fail loudly, as RunWithPackNameLocksAsync does. The caller must NOT advance the status on
            // this path: the save that holds the lock is doing exactly this work, and advancing here
            // would mark the line authorised twice over. ExecuteStatusChangeWorkflowAsync already treats
            // a non-empty Errors list as "do not advance", so no controller change is needed.
            await _logger.WarningAsync($"[Exchanges] RR {returnRequest.Id}: an approval for this return request is already running; this save executed nothing.");

            workflowResult.Errors.Add("This return request is already being approved by another save. Nothing was changed — please reload the page and check the result before trying again.");
        }

        return workflowResult;
    }

    #endregion

    #region Undo / cleanup

    public virtual async Task UndoReturnRequestApprovalAsync(ReturnRequest returnRequest)
    {
        // Same key as ExecuteApprovedLineAsync, deliberately: undo reverses the credit note, the cart
        // items and the subsidy restore rows that an approval is writing, so the two must never overlap.
        var ran = await _locker.PerformActionWithLockAsync(
            ApprovalLockKey(returnRequest.Id),
            TimeSpan.FromSeconds(60),
            async () =>
            {
                await UndoReturnRequestApprovalCoreAsync(returnRequest);
            });

        if (!ran)
            throw new NopException("This return request is currently being approved. Nothing was undone — please reload the page and check its state before trying again.");
    }

    #endregion

    #endregion
}