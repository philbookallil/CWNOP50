# Returns, exchanges and credit notes — fixes for the functional-guide findings

2026-11-16. Spec for Eduardo to apply (`CLAUDE.md` §0.1). Plugin: `Nop.Plugin.CW.Core`, version to `2026_11_16_DATA_0.1`.

The findings come from validating the workflow on the dev site for the functional guide
(`CWDocs/Exchanges_Returns_Credit_Notes_Functional_Guide.md`). The guide is updated to the corrected behaviour once
these changes are applied and re-tested.

## 1. What was decided

| # | Finding | Decision (Eduardo, 2026-11-16) |
|---|---|---|
| B1 | The **Back in stock** ticks on the reason, action and status rules are read by nothing | Use them: on authorise, when the reason, the action and the *Return authorized* status rules all tick Back in stock, the **whole returned quantity** goes back to stock |
| B2 | A rejected return tells the customer nothing; a credit return's approval sends no e-mail; the decision note is never shown to the customer | New e-mails for a rejected/cancelled return (with the decision note) and for a return approved without a replacement (credit raised, allowance given back). The decision note shows on the customer's **My returns** page |
| B3 | Units of a rejected return can never be returned again (nop counts Rejected as still asked for) | Free them: a rejected return's units can be returned again |
| B4 | Normal delivery is charged on an exchange replacement on top of the redelivery fee | Never charge delivery on a replacement: the redelivery fee is its delivery charge |
| B5 | With Credit Notes switched off the wizard still offers "Request a credit note" | Hide the credit option. Returns already in progress still complete, and an approved exchange still raises the note for the customer's own money |
| — | Refunded / Repaired statuses do nothing | **No change**: they stay labels (as in 4.1) |
| Q1 | A rejected return set back to Pending/Received keeps its case line Rejected | Re-open it: the line goes back to Pending |
| Q2 | The replacement exemptions (selling quantities, required products, subsidy caps, catalogue window) turn off with the Exchanges switch | Remove that check: a replacement already in a cart is work in progress |
| D1 | A credit note that has paid for an order can be deleted | Defect: refused, and no Delete button on such a note |
| D2 | Admin return page shows raw labels "StoreName", "BranchName", "CustomerName"; the packname and the replacement product are never shown | Defect: labels, and the packname and replacement (product, options, price) shown in Basic Data |
| D3 | A credit return shows "Exchange Type: Exact Exchange" (Bootstrap 3 `hidden` class) | Defect: the row is not drawn for a credit return |
| D4 | A replacement whose product requires other products passes the wizard but fails at approval ("Could not add approved exchanged items to the customer's cart") | Defect: a replacement is not held to its required products |
| D5 | A rejected credit return still shows "Most the customer could receive" | Defect: a rejected return shows that nothing is credited or charged |

## 2. What each fix does

- **B1** — `ExchangesLogic.ExecuteApprovedLineCoreAsync` reads the three rules it already loads and sets
  `ReturnRequestWorkflowResult.ReturnToStock` (`ExchangesHelper.IsBackInStock`). The return page's save then raises the
  returned quantity to the whole return before nop's existing stock adjustment, so stock moves once, through the same
  code and the same stock-history message as a quantity typed by hand. Set before the workflow can stop, so a return
  whose rules give neither credit nor exchange still puts the goods back. Undoing an approval does **not** take the
  stock out again (nop's returned quantity never goes down) — the guide will say so.
- **B2** — two new message templates, installed once (a store's edits survive):
  - `ReturnRejected.CustomerNotification` — sent by the return page's save when the status moves **to** Rejected or
    Cancelled from any other status (Rejected ↔ Cancelled and re-saves send nothing). Tokens:
    `%ReturnRequest.DecisionNote%`, `%ReturnRequest.HasDecisionNote%`. A failure to queue it is logged and never fails the save.
  - `ReturnAuthorisedCredit.CustomerNotification` — sent when an authorised return ends without a replacement (a credit
    return, or an exchange whose rules give only credit). Tokens: `%ReturnRequest.CreditNoteAmount%`,
    `%ReturnRequest.HasCreditNote%`, `%ReturnRequest.ReturnedToAllowance%` (the amount in the primary store currency,
    which is what a credit note is kept in). Not sent when nothing came back (the fees took the whole note and no
    allowance was paid). Its id is kept in
    `ApprovalQueuedEmailId`, so **Undo approval** withdraws it while unsent, exactly like the two existing approval e-mails.
  - The decision note shows on **My returns** once the return is decided (authorised, rejected, cancelled), and the
    admin hint under the note says the customer sees it.
  - *Design note:* the credit e-mail does not carry the decision note. The note is written by the save **after** the
    approval workflow (so a failed workflow leaves the line untouched), so inside the workflow it is not saved yet.
    The customer still sees it on My returns.
  - Both sends share one new helper in `ExchangeWorkflowMessageService`; the three existing send methods are left
    as they are (they could move onto it later — not needed for this change).
- **B3** — `ReturnRequestOverrideService` (new, Option B in CW.Core) overrides nop's
  `GetReturnRequestAvailabilityAsync`: it calls nop's method (which keeps nop's non-returnable / download filter) and
  recounts what is still asked for without Rejected and Cancelled returns. Because a rejected return's units can now go
  into a new return, **re-opening** a rejected or cancelled return (moving it to any other status) is refused when
  its units have been returned again since — otherwise both returns could be authorised and the customer credited
  twice for the same units. Staff then see a message and decide the newer return instead. **No nop body is copied**, so nothing joins
  the `CLAUDE.md` §10.3 re-diff table. Everything that asks "what can still be returned" follows it: CW's returnable
  quantities (wizard, `ReturnRequestSubmitFilter`), nop's return page and nop's "return allowed" check. It applies on
  every store, whatever the Exchanges switch says (it is nop's own count).
- *Review:* an independent review applied every block by script and found no compile problems. Its should-fix (the
  re-opening above), the currency of the credit amount and the empty credit e-mail are folded in.
- **B4** — `OrderTotalCalculationOverrideService.IsFreeShippingAsync` answers *free* for a cart, or a packname
  destination, whose lines are **all** exchange replacements. That one override reaches the packname destinations
  (`GetShippingTotalsByPackNames`), the plain cart, and the shipping-method page's rates (nop's `AdjustShippingRateAsync`).
  A parcel with other goods is rated as before — the other goods pay delivery as they always did. Not gated by the
  Exchanges switch: a replacement already in a cart is work in progress.
- **B5** — `ExchangeService.DetermineEnabledReturnModesAsync` adds Credit Note only while the store's Credit Notes
  switch is on. The wizard's mode buttons and the submission check both read it, so the option is hidden and a posted
  credit return is refused ("ModeNotAvailable"). Approval, undo and the exchange's own-money note are untouched.
- **D1** — the controller refuses a note with history (`CreditNoteHasHistoryAsync`, which had no caller) with a new
  resource message; the grid draws Delete only while Remaining equals Amount. A note whose order was cancelled and
  whose money came back may still show Delete; if it still has history the server refuses it with the message.
  The two existing messages said "StoreId" where they meant the note's id — corrected in the same method.
- **D2/D3** — labels on the model; packname and replacement rows in Basic Data (product links to its admin page;
  options as stored; price formatted). The exchange-type row is not drawn for a credit return; its hidden input is still
  posted, as before.
- **D4** — `ShoppingCartOverrideService.GetRequiredProductWarningsAsync` returns no warning for a replacement line,
  with the same rule as the selling-quantity exemption beside it (`IsExchangeReplacementAsync`: the scope while the
  approval adds the line or places the order, the recorded link afterwards). This also stops nop adding the required
  products automatically to a replacement.
- **Q1** — `ReturnsController.ReopenResolvedCaseIfDecisionChangesAsync` now uses `MapStatusToLineState`: the
  decision for Authorized/Rejected/Cancelled as before, and **Pending** for Pending and Received. A decided line set
  back to a step on the way is re-opened (any resolution voided, line Pending, decision note cleared) and the case
  re-resolves when it is decided again. Refunded/Repaired still leave the line alone. Re-opening a *rejected* return
  is still refused when its units have been returned again (B3 above).
- **Q2** — `ShoppingCartOverrideService.IsExchangeReplacementAsync` and `GetCartLineReplacementAsync` no longer check the
  Exchanges switch, so their `storeId` parameter is gone (it had no other use) and the four callers drop it.
- **D5** — `IsRejected` (status Rejected or Cancelled) on the admin model; the fees panel then shows "Nothing Credited or
  Charged (Return Rejected)" with 0, no estimate caveat, no "customer's favour" row, and the panel's live re-pricing
  keeps 0. Taken from the status, not the case line, so it follows what staff see in the status box.

## 3. How to apply

Changes are numbered in apply order. Each names its file; a file with several blocks lists them **bottom to top**
(`a` is lowest in the file) and the line numbers are those of the file as it stands when that block is reached.

Labels: **REPLACE lines X–Y** = select exactly those whole lines (they match FIND) and paste REPLACE WITH;
**ADD FILE** = create the file; **WHOLE FILE** = replace the file's entire contents. Every block starts and ends on a
whole line; the line count after each block is given in brackets.

**I could not build here** (my workspace has no .NET SDK and no NuGet access), so nothing below has been through the
compiler. The new and changed tests are already in `Tests/CW.Tests` (see §5) and **compile only after this spec is
applied** — please do not run the test project before.

## 4. Changes

### 1. Two helpers: the rejected statuses (D5, B2) and the three Back in stock ticks (B1)

`Plugins/Nop.Plugin.CW.Core/Helpers/ExchangesHelper.cs`

Depends on: nothing.

**1a. REPLACE lines 111–114** (4 lines → 29 lines)

FIND:

```csharp
    }

    public static int DetermineFeeRuleQuantity(FeeRule feeRule, ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
```

REPLACE WITH:

```csharp
    }

    /// <summary>
    /// Whether a status closes the return without an outcome: nothing is credited, exchanged or charged for it
    /// </summary>
    /// <param name="status">Return request status</param>
    /// <returns>True for Rejected and Cancelled</returns>
    public static bool IsRejectedReturnRequestStatus(ReturnRequestStatus status)
    {
        return status is ReturnRequestStatus.RequestRejected or ReturnRequestStatus.Cancelled;
    }

    /// <summary>
    /// Whether an authorised return puts the returned goods back in stock: the reason, the action and the status
    /// rules all have to say so
    /// </summary>
    /// <param name="statusRule">The rule of the status the return is set to</param>
    /// <param name="reasonRule">The rule of the return's reason</param>
    /// <param name="actionRule">The rule of the return's action</param>
    /// <returns>True when all three rules exist and tick Back in stock</returns>
    public static bool IsBackInStock(ReturnStatusRule statusRule, ReturnReasonRule reasonRule, ReturnActionRule actionRule)
    {
        return statusRule is { BackInStock: true }
            && reasonRule is { BackInStock: true }
            && actionRule is { BackInStock: true };
    }

    public static int DetermineFeeRuleQuantity(FeeRule feeRule, ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
```

### 2. The two new e-mails' system names (B2)

`Plugins/Nop.Plugin.CW.Core/Domain/Messages/CWMessageTemplateSystemNames.cs`

Depends on: nothing.

**2a. REPLACE lines 11–14** (4 lines → 15 lines)

FIND:

```csharp
    public const string ReturnAuthorisedPaymentCustomer = "ReturnAuthorisedPayment.CustomerNotification";
    public const string ReturnAuthorisedExceptedCustomer = "ReturnAuthorisedExcepted.CustomerNotification";

    #endregion
```

REPLACE WITH:

```csharp
    public const string ReturnAuthorisedPaymentCustomer = "ReturnAuthorisedPayment.CustomerNotification";
    public const string ReturnAuthorisedExceptedCustomer = "ReturnAuthorisedExcepted.CustomerNotification";

    /// <summary>
    /// Sent to the customer when a return is authorised without a replacement: the store credit raised and the
    /// allowance given back
    /// </summary>
    public const string ReturnAuthorisedCreditCustomer = "ReturnAuthorisedCredit.CustomerNotification";

    /// <summary>
    /// Sent to the customer when a return is rejected or cancelled, with the decision note
    /// </summary>
    public const string ReturnRejectedCustomer = "ReturnRejected.CustomerNotification";

    #endregion
```

### 3. The two new token groups (B2)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/CWTokenGroupNames.cs`

Depends on: nothing.

**3a. REPLACE lines 25–27** (3 lines → 14 lines)

FIND:

```csharp
    /// </summary>
    public const string SubsidyExpiryTokens = "Subsidy expiry tokens";
}
```

REPLACE WITH:

```csharp
    /// </summary>
    public const string SubsidyExpiryTokens = "Subsidy expiry tokens";

    /// <summary>
    /// Represents the tokens of a rejected or cancelled return: the decision note staff recorded
    /// </summary>
    public const string ReturnRejectionTokens = "Return rejection tokens";

    /// <summary>
    /// Represents the tokens of a return authorised without a replacement: the store credit raised and the allowance
    /// given back
    /// </summary>
    public const string ReturnCreditTokens = "Return credit tokens";
}
```

### 4. Two new token methods (B2)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/IMessageTokenProviderOverride.cs`

Depends on: nothing.

**4a. REPLACE lines 24–26** (3 lines → 20 lines)

FIND:

```csharp
    /// <returns>A task that represents the asynchronous operation</returns>
    Task AddReturnPackNameTokensAsync(IList<Token> tokens, OrderItem orderItem);
}
```

REPLACE WITH:

```csharp
    /// <returns>A task that represents the asynchronous operation</returns>
    Task AddReturnPackNameTokensAsync(IList<Token> tokens, OrderItem orderItem);

    /// <summary>
    /// Adds the tokens of a rejected or cancelled return: the decision note staff recorded
    /// </summary>
    /// <param name="tokens">List of already added tokens</param>
    /// <param name="decisionNote">The decision note; empty when there is none</param>
    void AddReturnRejectionTokens(IList<Token> tokens, string decisionNote);

    /// <summary>
    /// Adds the tokens of a return authorised without a replacement: the store credit raised and whether the allowance
    /// was given back
    /// </summary>
    /// <param name="tokens">List of already added tokens</param>
    /// <param name="creditNoteAmount">The amount of the credit note raised; zero when none was</param>
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task AddReturnCreditTokensAsync(IList<Token> tokens, decimal creditNoteAmount, bool returnedToAllowance);
}
```

### 5. The two token methods, and the token groups of the two new templates (B2)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`

Depends on: changes 2, 3, 4.

**5a. REPLACE lines 757–760** (4 lines → 35 lines)

FIND:

```csharp
    }

    #endregion
}
```

REPLACE WITH:

```csharp
    }

    /// <summary>
    /// Adds the tokens of a rejected or cancelled return: the decision note staff recorded
    /// </summary>
    /// <param name="tokens">List of already added tokens</param>
    /// <param name="decisionNote">The decision note; empty when there is none</param>
    public void AddReturnRejectionTokens(IList<Token> tokens, string decisionNote)
    {
        //encoded here so the note keeps its line breaks in the e-mail
        var noteHtml = WebUtility.HtmlEncode(decisionNote ?? string.Empty).ReplaceLineEndings("<br />");
        tokens.Add(new Token("ReturnRequest.DecisionNote", noteHtml, true));
        //a bool, so a template's %if% can test it whatever the note holds
        tokens.Add(new Token("ReturnRequest.HasDecisionNote", !string.IsNullOrWhiteSpace(decisionNote)));
    }

    /// <summary>
    /// Adds the tokens of a return authorised without a replacement: the store credit raised and whether the allowance
    /// was given back
    /// </summary>
    /// <param name="tokens">List of already added tokens</param>
    /// <param name="creditNoteAmount">The amount of the credit note raised; zero when none was</param>
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task AddReturnCreditTokensAsync(IList<Token> tokens, decimal creditNoteAmount, bool returnedToAllowance)
    {
        //a credit note is kept in the primary store currency, whoever's working currency is active
        var primaryCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        tokens.Add(new Token("ReturnRequest.CreditNoteAmount", await _priceFormatter.FormatPriceAsync(creditNoteAmount, true, primaryCurrency)));
        tokens.Add(new Token("ReturnRequest.HasCreditNote", creditNoteAmount > decimal.Zero));
        tokens.Add(new Token("ReturnRequest.ReturnedToAllowance", returnedToAllowance));
    }

    #endregion
}
```

**5b. REPLACE lines 349–352** (4 lines → 24 lines)

FIND:

```csharp
                    TokenGroupNames.ReturnRequestTokens,
                    CWTokenGroupNames.ExchangeReturnsTokens,
                    CWTokenGroupNames.ReturnPackNameTokens
                };
```

REPLACE WITH:

```csharp
                    TokenGroupNames.ReturnRequestTokens,
                    CWTokenGroupNames.ExchangeReturnsTokens,
                    CWTokenGroupNames.ReturnPackNameTokens
                };
            case CWMessageTemplateSystemNames.ReturnRejectedCustomer:
                return new[]
                {
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.OrderTokens,
                    TokenGroupNames.CustomerTokens,
                    TokenGroupNames.ReturnRequestTokens,
                    CWTokenGroupNames.ReturnRejectionTokens,
                    CWTokenGroupNames.ReturnPackNameTokens
                };
            case CWMessageTemplateSystemNames.ReturnAuthorisedCreditCustomer:
                return new[]
                {
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.OrderTokens,
                    TokenGroupNames.CustomerTokens,
                    TokenGroupNames.ReturnRequestTokens,
                    CWTokenGroupNames.ReturnCreditTokens,
                    CWTokenGroupNames.ReturnPackNameTokens
                };
```

### 6. The new tokens listed on the message-template page (B2)

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

Depends on: change 3.

**6a. REPLACE lines 710–713** (4 lines → 19 lines)

FIND:

```csharp
        }

        //The welcome e-mail of a customer registered by a manager: the packname and the subsidies assigned to it
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.SubsidyTokens))
```

REPLACE WITH:

```csharp
        }

        //A rejected or cancelled return: the decision note staff recorded
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.ReturnRejectionTokens))
        {
            eventMessage.AdditionalTokens.Add("%ReturnRequest.DecisionNote%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.HasDecisionNote%");
        }

        //A return authorised without a replacement: the store credit raised and the allowance given back
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.ReturnCreditTokens))
        {
            eventMessage.AdditionalTokens.Add("%ReturnRequest.CreditNoteAmount%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.HasCreditNote%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.ReturnedToAllowance%");
        }

        //The welcome e-mail of a customer registered by a manager: the packname and the subsidies assigned to it
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.SubsidyTokens))
```

### 7. Two new send methods (B2)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/IExchangeWorkflowMessageService.cs`

Depends on: nothing.

**7a. REPLACE lines 36–38** (3 lines → 23 lines)

FIND:

```csharp
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestApprovedPaymentCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, decimal pendingAmount, ReturnRequestItem returnRequestItem = null);
}
```

REPLACE WITH:

```csharp
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestApprovedPaymentCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, decimal pendingAmount, ReturnRequestItem returnRequestItem = null);

    /// <summary>
    /// Sends a notification when a Return Request is authorised without a replacement: the store credit raised and the
    /// allowance given back
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="creditNoteAmount">The amount of the credit note raised; zero when none was</param>
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestCreditCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, decimal creditNoteAmount, bool returnedToAllowance);

    /// <summary>
    /// Sends a notification when a Return Request is rejected or cancelled, with the decision note
    /// </summary>
    /// <param name="returnRequest">The return request, saved with its new status</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="decisionNote">The decision note recorded against the item; empty when there is none</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestRejectedCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, string decisionNote);
}
```

### 8. The two send methods, over one shared helper (B2)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/ExchangeWorkflowMessageService.cs`

Depends on: changes 2, 4, 7.

**8a. REPLACE lines 231–234** (4 lines → 38 lines)

FIND:

```csharp
    }

    #endregion
}
```

REPLACE WITH:

```csharp
    }

    /// <summary>
    /// Sends a notification when a Return Request is authorised without a replacement: the store credit raised and the
    /// allowance given back
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="creditNoteAmount">The amount of the credit note raised; zero when none was</param>
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    public virtual async Task<IList<int>> SendReturnRequestCreditCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem,
        decimal creditNoteAmount, bool returnedToAllowance)
    {
        return await SendReturnCustomerNotificationAsync(CWMessageTemplateSystemNames.ReturnAuthorisedCreditCustomer, returnRequest, orderItem,
            tokens => _messageTokenProviderOverride.AddReturnCreditTokensAsync(tokens, creditNoteAmount, returnedToAllowance));
    }

    /// <summary>
    /// Sends a notification when a Return Request is rejected or cancelled, with the decision note
    /// </summary>
    /// <param name="returnRequest">The return request, saved with its new status</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="decisionNote">The decision note recorded against the item; empty when there is none</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    public virtual async Task<IList<int>> SendReturnRequestRejectedCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem,
        string decisionNote)
    {
        return await SendReturnCustomerNotificationAsync(CWMessageTemplateSystemNames.ReturnRejectedCustomer, returnRequest, orderItem,
            tokens =>
            {
                _messageTokenProviderOverride.AddReturnRejectionTokens(tokens, decisionNote);
                return Task.CompletedTask;
            });
    }

    #endregion
}
```

**8b. REPLACE lines 65–68** (4 lines → 60 lines)

FIND:

```csharp
    {
        _messageTokenProviderOverride = messageTokenProviderOverride;
    }

```

REPLACE WITH:

```csharp
    {
        _messageTokenProviderOverride = messageTokenProviderOverride;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Sends a return e-mail to the customer who asked for the return, with the order, customer, return request and
    /// packname tokens, and the tokens the caller adds
    /// </summary>
    /// <param name="templateName">The message template's system name</param>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="addTokensAsync">Adds the e-mail's own tokens</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    protected virtual async Task<IList<int>> SendReturnCustomerNotificationAsync(string templateName, ReturnRequest returnRequest,
        OrderItem orderItem, Func<IList<Token>, Task> addTokensAsync)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);
        ArgumentNullException.ThrowIfNull(orderItem);

        var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
        var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
        var languageId = await EnsureLanguageIsActiveAsync(order.CustomerLanguageId, store.Id);

        var messageTemplates = await GetActiveMessageTemplatesAsync(templateName, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        //tokens
        var commonTokens = new List<Token>();
        await _messageTokenProvider.AddOrderTokensAsync(commonTokens, order, languageId);
        await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
        await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, order, orderItem, languageId);
        await _messageTokenProviderOverride.AddReturnPackNameTokensAsync(commonTokens, orderItem);
        await addTokensAsync(commonTokens);

        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);
        var isGuest = await _customerService.IsGuestAsync(customer);
        var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
        var toEmail = isGuest ? billingAddress?.Email : customer.Email;
        var toName = isGuest ? billingAddress?.FirstName : await _customerService.GetCustomerFullNameAsync(customer);

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            //email account
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            //event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

```

### 9. ReturnToStock (B1)

`Plugins/Nop.Plugin.CW.Core/Services/Exchanges/ReturnRequestWorkflowResult.cs`

Depends on: nothing.

**9a. REPLACE lines 35–38** (4 lines → 10 lines)

FIND:

```csharp
    public bool FeesNotDeductible { get; set; }

    #endregion
}
```

REPLACE WITH:

```csharp
    public bool FeesNotDeductible { get; set; }

    /// <summary>
    /// The reason, the action and the status rules all tick Back in stock: the whole returned quantity goes back to
    /// stock when the return is authorised
    /// </summary>
    public bool ReturnToStock { get; set; }

    #endregion
}
```

### 10. No credit return offered while Credit Notes is off (B5)

`Plugins/Nop.Plugin.CW.Core/Services/Exchanges/ExchangeService.cs`

Depends on: nothing.

**10a. REPLACE lines 1204–1208** (5 lines → 8 lines)

FIND:

```csharp
                modes.Add(ExchangeReturnMode.Exchange);
            }
            if (returnReasonRule.AllowStoreCredit && returnActionRule.AllowStoreCredit)
            {
                modes.Add(ExchangeReturnMode.CreditNote);
```

REPLACE WITH:

```csharp
                modes.Add(ExchangeReturnMode.Exchange);
            }
            //a store with Credit Notes switched off offers no credit return; an exchange still raises the note for the
            //customer's own money when it is approved
            if (returnReasonRule.AllowStoreCredit && returnActionRule.AllowStoreCredit
                && await _cwFeatureService.IsEnabledAsync(CWFeature.CreditNotes, storeId))
            {
                modes.Add(ExchangeReturnMode.CreditNote);
```

**10b. REPLACE lines 83–86** (4 lines → 5 lines)

FIND:

```csharp
        _shipmentRepository = shipmentRepository;
        _shipmentItemRepository = shipmentItemRepository;
    }

```

REPLACE WITH:

```csharp
        _shipmentRepository = shipmentRepository;
        _shipmentItemRepository = shipmentItemRepository;
        _cwFeatureService = cwFeatureService;
    }

```

**10c. REPLACE lines 62–66** (5 lines → 6 lines)

FIND:

```csharp
        IShortTermCacheManager shortTermCacheManager,
        IRepository<Shipment> shipmentRepository,
        IRepository<ShipmentItem> shipmentItemRepository)
    {
        _returnReasonRuleRepository = returnReasonRuleRepository;
```

REPLACE WITH:

```csharp
        IShortTermCacheManager shortTermCacheManager,
        IRepository<Shipment> shipmentRepository,
        IRepository<ShipmentItem> shipmentItemRepository,
        ICWFeatureService cwFeatureService)
    {
        _returnReasonRuleRepository = returnReasonRuleRepository;
```

**10d. REPLACE lines 39–42** (4 lines → 5 lines)

FIND:

```csharp
    protected readonly IRepository<Shipment> _shipmentRepository;
    protected readonly IRepository<ShipmentItem> _shipmentItemRepository;

    #endregion
```

REPLACE WITH:

```csharp
    protected readonly IRepository<Shipment> _shipmentRepository;
    protected readonly IRepository<ShipmentItem> _shipmentItemRepository;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion
```

**10e. REPLACE lines 8–14** (7 lines → 9 lines)

FIND:

```csharp
using Nop.Plugin.CW.Core.Domain.Exchanges.DTOs;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Services.Catalog;
using Nop.Services.Orders;
```

REPLACE WITH:

```csharp
using Nop.Plugin.CW.Core.Domain.Exchanges.DTOs;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Catalog;
using Nop.Services.Orders;
```

### 11. NEW FILE — a rejected return's units can be returned again (B3)

`Plugins/Nop.Plugin.CW.Core/Services/Orders/ReturnRequestOverrideService.cs`

Depends on: nothing.

**ADD FILE** (new; the folder already exists):

```csharp
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Orders;

/// <summary>
/// nop's return request service, with the units of a rejected return available to return again
/// </summary>
public class ReturnRequestOverrideService : ReturnRequestService
{
    #region Ctor

    public ReturnRequestOverrideService(IRepository<ReturnRequest> returnRequestRepository,
        IRepository<ReturnRequestAction> returnRequestActionRepository,
        IRepository<ReturnRequestReason> returnRequestReasonRepository,
        IRepository<OrderItem> orderItemRepository,
        IRepository<Product> productRepository,
        ReturnRequestSettings returnRequestSettings) : base(returnRequestRepository,
            returnRequestActionRepository,
            returnRequestReasonRepository,
            orderItemRepository,
            productRepository,
            returnRequestSettings)
    {
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the return request availability
    /// </summary>
    /// <remarks>
    /// nop works out which items can be returned (no non-returnable product and, unless allowed, no download); this
    /// recounts what is still asked for on each of them without the rejected returns, so a unit whose return was
    /// rejected can be returned again. The quantity never goes above what was ordered.
    /// </remarks>
    /// <param name="orderId">The order identifier</param>
    /// <returns>The <see cref="Task"/> containing the <see cref="ReturnRequestAvailability"/></returns>
    public override async Task<ReturnRequestAvailability> GetReturnRequestAvailabilityAsync(int orderId)
    {
        var availability = await base.GetReturnRequestAvailabilityAsync(orderId);

        if (availability?.ReturnableOrderItems == null || availability.ReturnableOrderItems.Count == 0)
            return availability;

        var orderItemIds = availability.ReturnableOrderItems.Select(item => item.OrderItem.Id).ToList();

        //nop gives back only a cancelled return's units; a rejected return's are given back too
        var cancelledStatusId = (int)ReturnRequestStatus.Cancelled;
        var rejectedStatusId = (int)ReturnRequestStatus.RequestRejected;

        var requested = await _returnRequestRepository.Table
            .Where(returnRequest => orderItemIds.Contains(returnRequest.OrderItemId)
                && returnRequest.ReturnRequestStatusId != cancelledStatusId
                && returnRequest.ReturnRequestStatusId != rejectedStatusId)
            .GroupBy(returnRequest => returnRequest.OrderItemId)
            .Select(group => new { OrderItemId = group.Key, Quantity = group.Sum(returnRequest => returnRequest.Quantity) })
            .ToDictionaryAsync(row => row.OrderItemId, row => row.Quantity);

        foreach (var returnable in availability.ReturnableOrderItems)
            returnable.AvailableQuantityForReturn = Math.Max(returnable.OrderItem.Quantity - requested.GetValueOrDefault(returnable.OrderItem.Id), 0);

        return availability;
    }

    #endregion
}
```

### 12. A replacement is not held to its required products (D4); the replacement checks no longer follow the Exchanges switch (Q2)

`Plugins/Nop.Plugin.CW.Core/Services/Orders/ShoppingCartOverrideService.cs`

Depends on: nothing.

**12a. REPLACE lines 1150–1154** (5 lines → 5 lines)

FIND:

```csharp
        {
            //An exchange replacement is not a purchase: the returned units went back to the allocation.
            if (await IsExchangeReplacementAsync(customer.Id, item.Id, item.StoreId))
                continue;

```

REPLACE WITH:

```csharp
        {
            //An exchange replacement is not a purchase: the returned units went back to the allocation.
            if (await IsExchangeReplacementAsync(customer.Id, item.Id))
                continue;

```

**12b. REPLACE lines 607–610** (4 lines → 23 lines)

FIND:

```csharp
        return await base.GetStandardWarningsAsync(customer, shoppingCartType, product, attributesXml,
            customerEnteredPrice, effectiveQuantity, shoppingCartItemId, storeId);
    }

```

REPLACE WITH:

```csharp
        return await base.GetStandardWarningsAsync(customer, shoppingCartType, product, attributesXml,
            customerEnteredPrice, effectiveQuantity, shoppingCartItemId, storeId);
    }

    /// <summary>
    /// Required-product warnings, not applied to an exchange replacement.
    /// </summary>
    /// <remarks>
    /// A product that requires others is sold with them; a replacement is not a sale. The customer already has the
    /// required products from the original order, and an exchange swaps one item. Without this the approval cannot put
    /// the replacement in the cart (or place its order), and nop's own required-product rule would add products the
    /// customer never asked for. As with the selling quantities, the exemption follows the row: the scope while the
    /// replacement is added, the recorded link afterwards.
    /// </remarks>
    protected override async Task<IList<string>> GetRequiredProductWarningsAsync(Customer customer, ShoppingCartType shoppingCartType, Product product,
        int storeId, int quantity, bool addRequiredProducts, int shoppingCartItemId)
    {
        if (customer != null && await IsExchangeReplacementAsync(customer.Id, shoppingCartItemId))
            return new List<string>();

        return await base.GetRequiredProductWarningsAsync(customer, shoppingCartType, product, storeId, quantity, addRequiredProducts, shoppingCartItemId);
    }

```

**12c. REPLACE lines 575–582** (8 lines → 8 lines)

FIND:

```csharp
        //method sets on an AsyncLocal is undone when it returns.
        var linePackName = await GetCartLinePackNameAsync(customer, product, shoppingCartItemId, storeId);
        var lineReplacement = linePackName == null ? null : await GetCartLineReplacementAsync(customer.Id, shoppingCartItemId, storeId);
        using var catalogScope = linePackName == null ? null : PackNameCatalogScope.Begin(linePackName, lineReplacement);

        if (product == null || quantity <= 0 || customer == null || !await IsExchangeReplacementAsync(customer.Id, shoppingCartItemId, storeId))
            return await base.GetStandardWarningsAsync(customer, shoppingCartType, product, attributesXml,
                customerEnteredPrice, quantity, shoppingCartItemId, storeId);
```

REPLACE WITH:

```csharp
        //method sets on an AsyncLocal is undone when it returns.
        var linePackName = await GetCartLinePackNameAsync(customer, product, shoppingCartItemId, storeId);
        var lineReplacement = linePackName == null ? null : await GetCartLineReplacementAsync(customer.Id, shoppingCartItemId);
        using var catalogScope = linePackName == null ? null : PackNameCatalogScope.Begin(linePackName, lineReplacement);

        if (product == null || quantity <= 0 || customer == null || !await IsExchangeReplacementAsync(customer.Id, shoppingCartItemId))
            return await base.GetStandardWarningsAsync(customer, shoppingCartType, product, attributesXml,
                customerEnteredPrice, quantity, shoppingCartItemId, storeId);
```

**12d. REPLACE lines 297–336** (40 lines → 38 lines)

FIND:

```csharp
    /// <param name="customerId">Identifier of the customer whose cart holds the line</param>
    /// <param name="shoppingCartItemId">Shopping cart item identifier; 0 for a line not yet saved</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the exchange type; null when the line is not a replacement
    /// </returns>
    protected virtual async Task<ExchangeType?> GetCartLineReplacementAsync(int customerId, int shoppingCartItemId, int storeId)
    {
        if (ExchangeReplacementScope.IsActive)
            return ExchangeReplacementScope.Current.ExchangeType;

        if (shoppingCartItemId <= 0 || !await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, storeId))
            return null;

        var cartReturnRequestItems = await _exchangeService.Value.GetCartReturnRequestItemsAsync(customerId);

        return cartReturnRequestItems.TryGetValue(shoppingCartItemId, out var returnRequestItem) ? returnRequestItem.ExchangeType : null;
    }

    /// <summary>
    /// Whether the row being validated is an exchange replacement — either one being written right now, or
    /// one already sitting in the cart with its link recorded.
    /// </summary>
    /// <remarks>
    /// Ordered so ordinary shopping pays almost nothing: the scope is a field read, the id test rules out
    /// every not-yet-saved row, and the feature flag is cached per request. Only a saved row on a store
    /// with exchanges switched on reaches the cart's return request items, which are read once per request.
    /// </remarks>
    protected virtual async Task<bool> IsExchangeReplacementAsync(int customerId, int shoppingCartItemId, int storeId)
    {
        if (ExchangeReplacementScope.IsActive)
            return true;

        if (shoppingCartItemId <= 0)
            return false;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, storeId))
            return false;

```

REPLACE WITH:

```csharp
    /// <param name="customerId">Identifier of the customer whose cart holds the line</param>
    /// <param name="shoppingCartItemId">Shopping cart item identifier; 0 for a line not yet saved</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the exchange type; null when the line is not a replacement
    /// </returns>
    protected virtual async Task<ExchangeType?> GetCartLineReplacementAsync(int customerId, int shoppingCartItemId)
    {
        if (ExchangeReplacementScope.IsActive)
            return ExchangeReplacementScope.Current.ExchangeType;

        //not gated by the Exchanges switch: a replacement already in a cart is work in progress and is checked out
        //whatever the switch says
        if (shoppingCartItemId <= 0)
            return null;

        var cartReturnRequestItems = await _exchangeService.Value.GetCartReturnRequestItemsAsync(customerId);

        return cartReturnRequestItems.TryGetValue(shoppingCartItemId, out var returnRequestItem) ? returnRequestItem.ExchangeType : null;
    }

    /// <summary>
    /// Whether the row being validated is an exchange replacement — either one being written right now, or
    /// one already sitting in the cart with its link recorded.
    /// </summary>
    /// <remarks>
    /// Ordered so ordinary shopping pays almost nothing: the scope is a field read and the id test rules out
    /// every not-yet-saved row. Only a saved row reaches the cart's return request items, which are read once per
    /// request. Not gated by the Exchanges switch: a replacement already in a cart is work in progress.
    /// </remarks>
    protected virtual async Task<bool> IsExchangeReplacementAsync(int customerId, int shoppingCartItemId)
    {
        if (ExchangeReplacementScope.IsActive)
            return true;

        if (shoppingCartItemId <= 0)
            return false;

```

### 13. No delivery charge on a cart or destination holding only replacements (B4)

`Plugins/Nop.Plugin.CW.Core/Services/Orders/OrderTotalCalculationOverrideService.cs`

Depends on: nothing.

**13a. REPLACE lines 564–567** (4 lines → 22 lines)

FIND:

```csharp

        return (totals.shippingTotal.Sum(x => x.Price.Value), totals.taxRate, totals.appliedDiscounts);
    }

```

REPLACE WITH:

```csharp

        return (totals.shippingTotal.Sum(x => x.Price.Value), totals.taxRate, totals.appliedDiscounts);
    }

    /// <summary>
    /// Gets a value indicating whether shipping is free
    /// </summary>
    /// <remarks>
    /// A cart, or a packname destination, that holds only exchange replacements ships free: the return's redelivery
    /// fee is the delivery charge of a replacement. A cart with other goods is rated as nop rates it.
    /// </remarks>
    /// <param name="cart">Cart</param>
    /// <param name="subTotal">Subtotal amount; pass null to calculate subtotal</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains a value indicating whether shipping is free</returns>
    public override async Task<bool> IsFreeShippingAsync(IList<ShoppingCartItem> cart, decimal? subTotal = null)
    {
        if (await IsReplacementOnlyCartAsync(cart))
            return true;

        return await base.IsFreeShippingAsync(cart, subTotal);
    }

```

**13b. REPLACE lines 502–507** (6 lines → 25 lines)

FIND:

```csharp
        return await _settlementPipeline.QuoteAsync(context, settleableAmount);
    }

    #endregion

    #region Overrides
```

REPLACE WITH:

```csharp
        return await _settlementPipeline.QuoteAsync(context, settleableAmount);
    }

    /// <summary>
    /// Whether every line of a cart is an exchange replacement
    /// </summary>
    /// <remarks>
    /// A line not saved yet (no id) is never counted as one: a return order's own cart is built in memory and is
    /// handled by GetShoppingCartShippingTotalAsync.
    /// </remarks>
    /// <param name="cart">Cart, or the lines of one packname destination</param>
    /// <returns>True when the cart has lines and all of them are exchange replacements</returns>
    protected virtual async Task<bool> IsReplacementOnlyCartAsync(IList<ShoppingCartItem> cart)
    {
        if (cart == null || cart.Count == 0 || cart.Any(item => item.Id <= 0))
            return false;

        var replacements = await _exchangeService.GetCartReturnRequestItemsAsync(cart[0].CustomerId);

        return cart.All(item => replacements.ContainsKey(item.Id));
    }

    #endregion

    #region Overrides
```

### 14. The rejection e-mail (B2)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/IExchangesLogic.cs`

Depends on: nothing.

**14a. REPLACE lines 61–64** (4 lines → 12 lines)

FIND:

```csharp
    Task<IList<int>> SendCompletionRequiredNotificationAsync(ReturnRequest returnRequest);

    Task UndoReturnRequestApprovalAsync(ReturnRequest returnRequest);
}
```

REPLACE WITH:

```csharp
    Task<IList<int>> SendCompletionRequiredNotificationAsync(ReturnRequest returnRequest);

    /// <summary>
    /// Sends the customer the e-mail saying their return was rejected or cancelled, with the decision note staff recorded
    /// </summary>
    /// <param name="returnRequest">The return request, already saved with its new status</param>
    /// <param name="decisionNote">The decision note recorded against the item; empty when there is none</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRejectedNotificationAsync(ReturnRequest returnRequest, string decisionNote);

    Task UndoReturnRequestApprovalAsync(ReturnRequest returnRequest);
}
```

### 15. Back in stock flag (B1); the credit e-mail on a return approved without a replacement, and the rejection e-mail (B2)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

Depends on: changes 1, 7, 9, 14.

**15a. REPLACE lines 1000–1003** (4 lines → 17 lines)

FIND:

```csharp
    #region Notifications

    public virtual async Task<IList<int>> SendCompletionRequiredNotificationAsync(ReturnRequest returnRequest)
    {
```

REPLACE WITH:

```csharp
    #region Notifications

    public virtual async Task<IList<int>> SendReturnRejectedNotificationAsync(ReturnRequest returnRequest, string decisionNote)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);

        //an old return whose order item is gone has no item to name in the e-mail
        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);

        if (orderItem == null)
            return new List<int>();

        return await _exchangeWorkflowMessageService.SendReturnRequestRejectedCustomerNotificationAsync(returnRequest, orderItem, decisionNote);
    }

    public virtual async Task<IList<int>> SendCompletionRequiredNotificationAsync(ReturnRequest returnRequest)
    {
```

**15b. REPLACE lines 779–835** (57 lines → 65 lines)

FIND:

```csharp
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

```

REPLACE WITH:

```csharp
        var actionRule = await _exchangeService.GetReturnActionRuleByActionIdAsync(selectedAction.Id, storeId);

        //Decided before anything below can stop the workflow: the goods have come back whatever the rules give for them
        workflowResult.ReturnToStock = ExchangesHelper.IsBackInStock(statusRule, reasonRule, actionRule);

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
        var creditNoteAmount = decimal.Zero;

        if (creditNoteNeeded)
        {
            //Not "true because we tried". A zero-or-negative amount writes nothing — a fully subsidised
            //line raises no note at all — and the cleanup below only undoes a note that exists.
            var (noteCreated, feesNotDeductible, noteAmount) = await ExecuteCreditNoteWorkflowAsync(returnRequest, returnRequestItem, restored);

            workflowResult.CreditNoteWasCreated = noteCreated;
            workflowResult.FeesNotDeductible = feesNotDeductible;
            creditNoteAmount = noteAmount;
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

            await SendCreditApprovalEmailAsync(returnRequest, returnRequestItem, orderItem, creditNoteAmount, restored.RestoredAnything);
        }

```

**15c. REPLACE lines 218–319** (102 lines → 136 lines)

FIND:

```csharp
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
    /// Keeps the id of the approval e-mail queued for the line, so an undo can withdraw it before it is sent.
    /// </summary>
    /// <remarks>
    /// One id: a store has one active template of each name, so an approval queues one e-mail.
    /// </remarks>
    /// <param name="returnRequestItem">The approved line</param>
    /// <param name="queuedEmailIds">The ids of the e-mails queued for the approval; nothing is recorded when none was</param>
    protected virtual async Task RecordApprovalEmailAsync(ReturnRequestItem returnRequestItem, IList<int> queuedEmailIds)
    {
        var queuedEmailId = queuedEmailIds?.FirstOrDefault() ?? 0;

        if (queuedEmailId <= 0)
            return;

        returnRequestItem.ApprovalQueuedEmailId = queuedEmailId;
        await _exchangeService.UpdateReturnRequestItemAsync(returnRequestItem);
    }

    /// <summary>
    /// Deletes the approval e-mail queued for the line when the queue has not sent it yet; a sent one stays, as the
    /// record of what the customer was told.
    /// </summary>
    /// <param name="returnRequest">The return request whose approval is being undone</param>
    protected virtual async Task WithdrawApprovalEmailAsync(ReturnRequest returnRequest)
    {
        var returnRequestItem = await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);

        if (returnRequestItem?.ApprovalQueuedEmailId is not int queuedEmailId)
            return;

        var queuedEmail = await _queuedEmailService.GetQueuedEmailByIdAsync(queuedEmailId);

        if (queuedEmail != null && !queuedEmail.SentOnUtc.HasValue)
            await _queuedEmailService.DeleteQueuedEmailAsync(queuedEmail);

        returnRequestItem.ApprovalQueuedEmailId = null;
        await _exchangeService.UpdateReturnRequestItemAsync(returnRequestItem);
    }

```

REPLACE WITH:

```csharp
    /// <c>noteCreated</c> is true only when a note was actually written. <c>feesNotDeductible</c> is
    /// true for a Credit-Note-mode line that carried fees but had no money to deduct them from — the
    /// caller surfaces it so Sales Support can adjust the fee quantities. <c>noteAmount</c> is the
    /// amount of the note written, zero when none was.
    /// </returns>
    protected async Task<(bool noteCreated, bool feesNotDeductible, decimal noteAmount)> ExecuteCreditNoteWorkflowAsync(ReturnRequest returnRequest,
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

            return (true, feesNotDeductible, creditNoteAmount);
        }

        await _logger.InformationAsync($"[Exchanges] RR {returnRequest.Id}: no Credit Note raised — returned value {returnedValue}, " +
            $"subsidy restored in kind {restored.MoneyEquivalent}, computed note {creditNoteAmount}.");

        return (false, feesNotDeductible, decimal.Zero);
    }

    #endregion

    #region Exchange workflow

    /// <summary>
    /// Keeps the id of the approval e-mail queued for the line, so an undo can withdraw it before it is sent.
    /// </summary>
    /// <remarks>
    /// One id: a store has one active template of each name, so an approval queues one e-mail.
    /// </remarks>
    /// <param name="returnRequestItem">The approved line</param>
    /// <param name="queuedEmailIds">The ids of the e-mails queued for the approval; nothing is recorded when none was</param>
    protected virtual async Task RecordApprovalEmailAsync(ReturnRequestItem returnRequestItem, IList<int> queuedEmailIds)
    {
        var queuedEmailId = queuedEmailIds?.FirstOrDefault() ?? 0;

        if (queuedEmailId <= 0)
            return;

        returnRequestItem.ApprovalQueuedEmailId = queuedEmailId;
        await _exchangeService.UpdateReturnRequestItemAsync(returnRequestItem);
    }

    /// <summary>
    /// Deletes the approval e-mail queued for the line when the queue has not sent it yet; a sent one stays, as the
    /// record of what the customer was told.
    /// </summary>
    /// <param name="returnRequest">The return request whose approval is being undone</param>
    protected virtual async Task WithdrawApprovalEmailAsync(ReturnRequest returnRequest)
    {
        var returnRequestItem = await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);

        if (returnRequestItem?.ApprovalQueuedEmailId is not int queuedEmailId)
            return;

        var queuedEmail = await _queuedEmailService.GetQueuedEmailByIdAsync(queuedEmailId);

        if (queuedEmail != null && !queuedEmail.SentOnUtc.HasValue)
            await _queuedEmailService.DeleteQueuedEmailAsync(queuedEmail);

        returnRequestItem.ApprovalQueuedEmailId = null;
        await _exchangeService.UpdateReturnRequestItemAsync(returnRequestItem);
    }

    /// <summary>
    /// Tells the customer what an approved return without a replacement gave back: the store credit raised, the
    /// allowance given back, or both; nothing is sent when nothing came back. The id of the queued e-mail is kept so an
    /// undo can withdraw it.
    /// </summary>
    /// <remarks>
    /// The credit note and the restored allowance are already written when this runs, so a failure here is logged and
    /// never undoes the approval: only the e-mail is missing.
    /// </remarks>
    /// <param name="returnRequest">The approved return request</param>
    /// <param name="returnRequestItem">The approved line</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="creditNoteAmount">The amount of the credit note raised; zero when none was</param>
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
    protected virtual async Task SendCreditApprovalEmailAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem,
        OrderItem orderItem, decimal creditNoteAmount, bool returnedToAllowance)
    {
        //nothing came back (the fees took the whole note and no allowance was paid): there is nothing to tell
        if (creditNoteAmount <= decimal.Zero && !returnedToAllowance)
            return;

        try
        {
            var queuedEmailIds = await _exchangeWorkflowMessageService.SendReturnRequestCreditCustomerNotificationAsync(returnRequest, orderItem,
                creditNoteAmount, returnedToAllowance);
            await RecordApprovalEmailAsync(returnRequestItem, queuedEmailIds);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"[Exchanges] RR {returnRequest.Id}: the return was approved, but the e-mail telling the customer could not be queued.", ex);
        }
    }

```

### 16. Back in stock on authorise (B1); the rejection e-mail (B2); a line set back to Pending/Received re-opens (Q1); no re-opening of a rejected return whose units were returned again (B3)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ReturnsController.cs`

Depends on: changes 1, 9, 14; change 27 for the message (at run time only).

**16a. REPLACE lines 480–558** (79 lines → 101 lines)

FIND:

```csharp
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
```

REPLACE WITH:

```csharp
        }

        var previousStatus = returnRequest.ReturnRequestStatus;

        // If the staff did not enable the additional fee, discard any submitted values so it is never persisted
        if (!model.EnableAdditionalFee)
        {
            model.AdditionalFeeName = null;
            model.AdditionalFeeAmount = null;
        }

        //Re-opening a rejected or cancelled return: its units may have gone into another return since
        if (ExchangesHelper.IsRejectedReturnRequestStatus(previousStatus)
            && !ExchangesHelper.IsRejectedReturnRequestStatus((ReturnRequestStatus)model.ReturnRequestStatusId)
            && !await AreReturnedUnitsStillFreeAsync(returnRequest))
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Returns.Reopen.UnitsReturnedAgain"));

            return RedirectToReturn(returnRequest.Id);
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
                var (workflowSucceeded, returnToStock) = await ExecuteStatusChangeWorkflowAsync(returnRequest, model);

                if (!workflowSucceeded)
                    return continueEditing
                        ? RedirectToReturn(returnRequest.Id)
                        : RedirectAfterLeavingReturn();

                await RecordLineDispositionAsync(returnRequest, model);

                //The rules put the goods back on the shelf: the whole returned quantity goes back to stock, whatever
                //was typed in the box
                if (returnToStock)
                    model.ReturnedQuantity = Math.Max(model.ReturnedQuantity, returnRequest.Quantity);

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

                await NotifyCustomerIfRejectedAsync(returnRequest, previousStatus, model.DispositionReason);

                //activity log
```

**16b. REPLACE lines 186–325** (140 lines → 211 lines)

FIND:

```csharp
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
```

REPLACE WITH:

```csharp
    }

    /// <summary>
    /// The state a status puts the return's case line in: its decision, or Pending for the steps on the way (Pending,
    /// Received), which re-open a decided line
    /// </summary>
    /// <param name="returnRequestStatusId">Return request status identifier</param>
    /// <returns>The line's state; null for a status that does not decide the line either way (Refunded, Repaired)</returns>
    protected static ReturnLineDisposition? MapStatusToLineState(int returnRequestStatusId)
    {
        if (MapStatusToDisposition(returnRequestStatusId) is { } decision)
            return decision;

        return (ReturnRequestStatus)returnRequestStatusId is ReturnRequestStatus.Pending or ReturnRequestStatus.Received
            ? ReturnLineDisposition.Pending
            : null;
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
    /// <returns>
    /// <c>succeeded</c> is false when the workflow failed, in which case the caller must not advance the status.
    /// <c>returnToStock</c> is true when the rules ask for the whole returned quantity to go back to stock.
    /// </returns>
    protected async Task<(bool succeeded, bool returnToStock)> ExecuteStatusChangeWorkflowAsync(ReturnRequest returnRequest, CWReturnRequestModel model)
    {
        //Only a real status change triggers anything, and only when there is exchange data.
        if (!RequestStatusHasChanged(model) || !await HasExchangeRequestItemAsync(returnRequest))
            return (true, false);

        ReturnRequestWorkflowResult workflowResult;

        try
        {
            workflowResult = await _exchangesLogic.ExecuteApprovedLineAsync(returnRequest, model.ReturnRequestStatusId);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"[Returns] The exchange workflow threw for return request {returnRequest.Id}. Nothing was advanced.", ex);
            _notificationService.ErrorNotification("There were errors during the Exchange workflow steps. Could not complete the workflow: " + ex.Message);

            return (false, false);
        }

        if (workflowResult.Errors.Count > 0)
        {
            _notificationService.ErrorNotification("There were errors during the Exchange workflow steps. Could not complete the workflow. See errors below:");

            foreach (var error in workflowResult.Errors)
                _notificationService.ErrorNotification(error);

            return (false, false);
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

        return (true, workflowResult.ReturnToStock);
    }

    /// <summary>
    /// Whether the units of a rejected or cancelled return are still free to be taken back by it
    /// </summary>
    /// <remarks>
    /// A rejected return's units can be returned again, so before such a return is re-opened the units must not have
    /// gone into another return: authorising both would give the customer the same units' value twice. The return is
    /// still rejected when this runs, so the count leaves it out. An item nop no longer lists as returnable is not
    /// held back here: there is nothing another return could have taken.
    /// </remarks>
    /// <param name="returnRequest">The rejected or cancelled return request</param>
    /// <returns>True when the units are still free, or the item is no longer listed</returns>
    protected async Task<bool> AreReturnedUnitsStillFreeAsync(ReturnRequest returnRequest)
    {
        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);

        if (orderItem == null)
            return true;

        var availability = await _returnRequestService.GetReturnRequestAvailabilityAsync(orderItem.OrderId);
        var returnable = availability?.ReturnableOrderItems?.FirstOrDefault(item => item.OrderItem.Id == orderItem.Id);

        return returnable == null || returnable.AvailableQuantityForReturn >= returnRequest.Quantity;
    }

    /// <summary>
    /// Sends the customer the rejection e-mail when this save rejected or cancelled the return. Moving between
    /// Rejected and Cancelled, or re-saving either, sends nothing.
    /// </summary>
    /// <param name="returnRequest">The return request, already saved with its new status</param>
    /// <param name="previousStatus">The status the return had before this save</param>
    /// <param name="decisionNote">The decision note recorded against the item</param>
    protected async Task NotifyCustomerIfRejectedAsync(ReturnRequest returnRequest, ReturnRequestStatus previousStatus, string decisionNote)
    {
        if (!ExchangesHelper.IsRejectedReturnRequestStatus(returnRequest.ReturnRequestStatus)
            || ExchangesHelper.IsRejectedReturnRequestStatus(previousStatus))
            return;

        try
        {
            await _exchangesLogic.SendReturnRejectedNotificationAsync(returnRequest, decisionNote);
        }
        catch (Exception ex)
        {
            //Never fails the save: the decision is recorded, only the e-mail is missing
            await _logger.ErrorAsync($"[Returns] Could not queue the rejection e-mail for return request {returnRequest.Id}.", ex);
        }
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
    /// <para>
    /// A rejected line set back to Pending or Received is re-opened the same way and stays Pending, so the case
    /// says what the status says and re-resolves once the line is decided again.
    /// </para>
    /// Best-effort, like the other case writes: a failure here must not fail the administrator's save.
    /// </remarks>
    protected async Task ReopenResolvedCaseIfDecisionChangesAsync(ReturnRequest returnRequest, CWReturnRequestModel model)
    {
        var disposition = MapStatusToLineState(model.ReturnRequestStatusId);

        if (disposition == null)
```

### 17. A spent credit note cannot be deleted (D1)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/CreditNotesController.cs`

Depends on: change 27 for its message (at run time only).

**17a. REPLACE lines 125–136** (12 lines → 17 lines)

FIND:

```csharp

        if (creditNote == null)
            return Json(new { success = false, message = $"Cannot delete Credit Note with StoreId {creditNoteId}: Credit Note does not exist" });

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.CreditNotes, creditNote.StoreId, Request) is IActionResult blocked)
            return blocked;

        await _creditNotesService.DeleteCreditNoteAsync(creditNote);

        return Json(new { success = true, message = $"Credit Note with StoreId {creditNoteId} deleted successfully!" });
    }

```

REPLACE WITH:

```csharp

        if (creditNote == null)
            return Json(new { success = false, message = $"Cannot delete Credit Note {creditNoteId}: Credit Note does not exist" });

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.CreditNotes, creditNote.StoreId, Request) is IActionResult blocked)
            return blocked;

        //A note that has paid for an order is part of that order's settlement: deleting it would take away the
        //record of the money that paid, so only a note never applied to an order can be deleted
        if (await _creditNotesService.CreditNoteHasHistoryAsync(creditNote.Id))
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.CreditNotes.Delete.Spent") });

        await _creditNotesService.DeleteCreditNoteAsync(creditNote);

        return Json(new { success = true, message = $"Credit Note {creditNoteId} deleted successfully!" });
    }

```

**17b. REPLACE lines 7–41** (35 lines → 39 lines)

FIND:

```csharp
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Customers;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

public class CreditNotesController : BaseAdminController
{
    #region Fields

    protected readonly ICreditNotesService _creditNotesService;
    protected readonly IPermissionService _permissionService;
    protected readonly ICreditNotesModelFactory _creditNotesModelFactory;
    protected readonly ICustomerService _customerService;
    protected readonly ICWFeatureGuard _cwFeatureGuard;

    #endregion

    #region Ctor

    public CreditNotesController(ICreditNotesService creditNotesService,
        IPermissionService permissionService,
        ICreditNotesModelFactory creditNotesModelFactory,
        ICustomerService customerService,
        ICWFeatureGuard cwFeatureGuard)
    {
        _creditNotesService = creditNotesService;
        _permissionService = permissionService;
        _creditNotesModelFactory = creditNotesModelFactory;
        _customerService = customerService;
        _cwFeatureGuard = cwFeatureGuard;
    }

```

REPLACE WITH:

```csharp
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

public class CreditNotesController : BaseAdminController
{
    #region Fields

    protected readonly ICreditNotesService _creditNotesService;
    protected readonly IPermissionService _permissionService;
    protected readonly ICreditNotesModelFactory _creditNotesModelFactory;
    protected readonly ICustomerService _customerService;
    protected readonly ICWFeatureGuard _cwFeatureGuard;
    protected readonly ILocalizationService _localizationService;

    #endregion

    #region Ctor

    public CreditNotesController(ICreditNotesService creditNotesService,
        IPermissionService permissionService,
        ICreditNotesModelFactory creditNotesModelFactory,
        ICustomerService customerService,
        ICWFeatureGuard cwFeatureGuard,
        ILocalizationService localizationService)
    {
        _creditNotesService = creditNotesService;
        _permissionService = permissionService;
        _creditNotesModelFactory = creditNotesModelFactory;
        _customerService = customerService;
        _cwFeatureGuard = cwFeatureGuard;
        _localizationService = localizationService;
    }

```

### 18. No Delete button on a spent credit note (D1)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Customer/_CreateOrUpdate.CreditNotes.cshtml`

Depends on: nothing.

**18a. REPLACE lines 203–206** (4 lines → 6 lines)

FIND:

```cshtml
                <text>return '';</text>
            }
            return '<button type="button" class="btn btn-danger btn-sm" onclick="deleteCreditNote(' + data + ')">' +
                '<i class="far fa-trash-can"></i> ' + $('<span>').text($cfg.data('deleteLabel')).html() + '</button>';
```

REPLACE WITH:

```cshtml
                <text>return '';</text>
            }
            //a note that has paid for an order cannot be deleted (the server refuses it too)
            if (row.Remain < row.Amount) return '';
            return '<button type="button" class="btn btn-danger btn-sm" onclick="deleteCreditNote(' + data + ')">' +
                '<i class="far fa-trash-can"></i> ' + $('<span>').text($cfg.data('deleteLabel')).html() + '</button>';
```

### 19. Labels for the Basic Data rows (D2) and IsRejected (D5)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/Exchanges/CWReturnRequestModel.cs`

Depends on: nothing.

**19a. REPLACE lines 177–190** (14 lines → 23 lines)

FIND:

```csharp
    public bool IsCompleted { get; set; }

    public int StoreId { get; set; }

    public string StoreName { get; set; }

    public int BranchId { get; set; }

    public string BranchName { get; set; }

    public string Packname { get; set; }

    public string CustomerName { get; set; }

```

REPLACE WITH:

```csharp
    public bool IsCompleted { get; set; }

    /// <summary>
    /// The return was rejected or cancelled: nothing is credited, exchanged or charged for it
    /// </summary>
    public bool IsRejected { get; set; }

    public int StoreId { get; set; }

    [DisplayName("Store")]
    public string StoreName { get; set; }

    public int BranchId { get; set; }

    [DisplayName("Branch")]
    public string BranchName { get; set; }

    [DisplayName("Ordered for (packname)")]
    public string Packname { get; set; }

    [DisplayName("Customer")]
    public string CustomerName { get; set; }

```

**19b. REPLACE lines 103–113** (11 lines → 11 lines)

FIND:

```csharp
    public int ReplacementProductId { get; set; }

    [DisplayName("Product Name")]
    public string ReplacementProductName { get; set; }

    [DisplayName("Attributes")]
    public string ReplacementProductAttrInfo { get; set; }

    [DisplayName("Price")]
    public decimal ReplacementProductPrice { get; set; }

```

REPLACE WITH:

```csharp
    public int ReplacementProductId { get; set; }

    [DisplayName("Replacement product")]
    public string ReplacementProductName { get; set; }

    [DisplayName("Replacement options")]
    public string ReplacementProductAttrInfo { get; set; }

    [DisplayName("Replacement price")]
    public decimal ReplacementProductPrice { get; set; }

```

### 20. IsRejected (D5)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/ReturnRequestExchangeModelFactory.cs`

Depends on: changes 1, 19.

**20a. REPLACE lines 139–142** (4 lines → 5 lines)

FIND:

```csharp
        model.OriginalRequestStatusId = requestData.ReturnRequestStatusId;
        model.IsEditable = !ExchangesHelper.IsFinalReturnRequestStatus((ReturnRequestStatus)requestData.ReturnRequestStatusId);

        if (string.IsNullOrEmpty(requestData.ReturnRequestStatusStr) && requestData.ReturnRequestStatusId >= 0)
```

REPLACE WITH:

```csharp
        model.OriginalRequestStatusId = requestData.ReturnRequestStatusId;
        model.IsEditable = !ExchangesHelper.IsFinalReturnRequestStatus((ReturnRequestStatus)requestData.ReturnRequestStatusId);
        model.IsRejected = ExchangesHelper.IsRejectedReturnRequestStatus((ReturnRequestStatus)requestData.ReturnRequestStatusId);

        if (string.IsNullOrEmpty(requestData.ReturnRequestStatusStr) && requestData.ReturnRequestStatusId >= 0)
```

### 21. Basic Data: packname, replacement product (D2); no exchange type on a credit return (D3); decision-note hint (B2)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/ReturnRequest/_CreateOrUpdateExchange.cshtml`

Depends on: change 19.

**21a. REPLACE lines 307–311** (5 lines → 5 lines)

FIND:

```cshtml
                                                <span asp-validation-for="DispositionReason"></span>
                                                <span class="form-text text-muted">
                                                    Recorded against this item when you authorise or reject it. Explain here if the item is being rejected.
                                                </span>
                                            }
```

REPLACE WITH:

```cshtml
                                                <span asp-validation-for="DispositionReason"></span>
                                                <span class="form-text text-muted">
                                                    Recorded against this item when you authorise or reject it, and shown to the customer on their returns page and in the e-mail sent when the item is rejected or cancelled. Explain here if the item is being rejected.
                                                </span>
                                            }
```

**21b. REPLACE lines 100–124** (25 lines → 76 lines)

FIND:

```cshtml
                                        </div>
                                    }
                                    @if (!string.IsNullOrEmpty(exchangeRequestModel.ExchangeModeName))
                                    {
                                        <div class="form-group row">
                                            <div class="col-md-3">
                                                <nop-label asp-for="@exchangeRequestModel.ExchangeModeName" />
                                            </div>
                                            <div class="col-md-9">
                                                <div class="form-text-row">@exchangeRequestModel.ExchangeModeName</div>
                                                <input type="hidden" name="ExchangeModeId" value="@exchangeRequestModel.ExchangeModeId" />
                                            </div>
                                        </div>
                                    }
                                    @if (!string.IsNullOrEmpty(exchangeRequestModel.ExchangeTypeName))
                                    {
                                        <div class="form-group row @Html.Raw(exchangeRequestModel.ExchangeReturnMode == ExchangeReturnMode.CreditNote ? " hidden" : string.Empty)">
                                            <div class="col-md-3">
                                                <nop-label asp-for="@exchangeRequestModel.ExchangeTypeName" />
                                            </div>
                                            <div class="col-md-9">
                                                <div class="form-text-row">@exchangeRequestModel.ExchangeTypeName</div>
                                                <input type="hidden" name="ExchangeTypeId" value="@exchangeRequestModel.ExchangeTypeId" />
                                            </div>
                                        </div>
```

REPLACE WITH:

```cshtml
                                        </div>
                                    }
                                    @if (!string.IsNullOrEmpty(exchangeRequestModel.Packname))
                                    {
                                        <div class="form-group row">
                                            <div class="col-md-3">
                                                <nop-label asp-for="@exchangeRequestModel.Packname" />
                                            </div>
                                            <div class="col-md-9">
                                                <div class="form-text-row">@exchangeRequestModel.Packname</div>
                                            </div>
                                        </div>
                                    }
                                    @if (!string.IsNullOrEmpty(exchangeRequestModel.ExchangeModeName))
                                    {
                                        <div class="form-group row">
                                            <div class="col-md-3">
                                                <nop-label asp-for="@exchangeRequestModel.ExchangeModeName" />
                                            </div>
                                            <div class="col-md-9">
                                                <div class="form-text-row">@exchangeRequestModel.ExchangeModeName</div>
                                                <input type="hidden" name="ExchangeModeId" value="@exchangeRequestModel.ExchangeModeId" />
                                            </div>
                                        </div>
                                    }
                                    @if (!string.IsNullOrEmpty(exchangeRequestModel.ExchangeTypeName))
                                    {
                                        //a credit return has no exchange type: the row is left out and only the posted value kept
                                        if (exchangeRequestModel.ExchangeReturnMode == ExchangeReturnMode.CreditNote)
                                        {
                                            <input type="hidden" name="ExchangeTypeId" value="@exchangeRequestModel.ExchangeTypeId" />
                                        }
                                        else
                                        {
                                            <div class="form-group row">
                                                <div class="col-md-3">
                                                    <nop-label asp-for="@exchangeRequestModel.ExchangeTypeName" />
                                                </div>
                                                <div class="col-md-9">
                                                    <div class="form-text-row">@exchangeRequestModel.ExchangeTypeName</div>
                                                    <input type="hidden" name="ExchangeTypeId" value="@exchangeRequestModel.ExchangeTypeId" />
                                                </div>
                                            </div>
                                        }
                                    }
                                    @if (exchangeRequestModel.ExchangeReturnMode == ExchangeReturnMode.Exchange && exchangeRequestModel.ReplacementProductId > 0)
                                    {
                                        <div class="form-group row">
                                            <div class="col-md-3">
                                                <nop-label asp-for="@exchangeRequestModel.ReplacementProductName" />
                                            </div>
                                            <div class="col-md-9">
                                                <div class="form-text-row">
                                                    <a asp-controller="Product" asp-action="Edit" asp-route-id="@exchangeRequestModel.ReplacementProductId">@exchangeRequestModel.ReplacementProductName</a>
                                                </div>
                                            </div>
                                        </div>
                                        @if (!string.IsNullOrWhiteSpace(exchangeRequestModel.ReplacementProductAttrInfo))
                                        {
                                            <div class="form-group row">
                                                <div class="col-md-3">
                                                    <nop-label asp-for="@exchangeRequestModel.ReplacementProductAttrInfo" />
                                                </div>
                                                <div class="col-md-9">
                                                    <div class="form-text-row">@Html.Raw(exchangeRequestModel.ReplacementProductAttrInfo)</div>
                                                </div>
                                            </div>
                                        }
                                        <div class="form-group row">
                                            <div class="col-md-3">
                                                <nop-label asp-for="@exchangeRequestModel.ReplacementProductPrice" />
                                            </div>
                                            <div class="col-md-9">
                                                <div class="form-text-row">@(await _priceFormatter.FormatPriceAsync(exchangeRequestModel.ReplacementProductPrice))</div>
                                            </div>
                                        </div>
```

### 22. A rejected return shows nothing credited or charged (D5)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/ReturnRequest/_CreateOrUpdate.FeesAndCharges.cshtml`

Depends on: change 19.

**WHOLE FILE** — replace all 281 lines of the file with:

```cshtml
@using Nop.Plugin.CW.Core.Extensions;
@using Nop.Core.Domain.Orders;
@using Nop.Plugin.CW.Core.Domain.Exchanges;
@using CW.Common.Helpers;

@model ReturnRequestModel

@inject IWorkContext _workContext

@{
    NopHtml.AddCssFileParts("~/Plugins/CW.Core/Content/Admin/Exchanges/return-request-fees.css");

    var exchangeRequestModel = Model as CWReturnRequestModel;
    var isExchangeModelData = exchangeRequestModel != null;

    var currency = await _workContext.GetWorkingCurrencyAsync();
}

<input asp-for="Id" type="hidden" />

<div asp-validation-summary="All" class="message-error"></div>

@if (isExchangeModelData)
{
    var productSubTotal = exchangeRequestModel.ProductPrice * Model.Quantity;
    var replacementProductTotal = exchangeRequestModel.ReplacementProductPrice * Model.Quantity;
    decimal priceDifference = exchangeRequestModel.ExchangeType != ExchangeType.Exact ?
    (replacementProductTotal - productSubTotal) : decimal.Zero;

    decimal shippingFee = 0;
    decimal repackagingFee = 0;
    //What is charged, not what is in the input box. Once the case has resolved the frozen charge wins and
    //editing the field no longer moves the money — this total has to follow the money.
    decimal additionalFee = exchangeRequestModel.AdditionalFeeChargedAmount;

    //A rejected or cancelled return gives nothing back and charges nothing, so no figure on this panel is a projection
    var isRejected = exchangeRequestModel.IsRejected;

    //Whether the credit-note figures on this panel are still a projection. A projection is not the answer:
    //subsidy goes back in kind and the note is only what is left over, so an unresolved case can
    //show a maximum and nothing firmer.
    var creditNoteIsEstimate = !isRejected && exchangeRequestModel.CreditNoteRaisedAmount == null;
    var creditNoteRaised = exchangeRequestModel.CreditNoteRaisedAmount ?? decimal.Zero;

    @*data-* values let the script mirror the total formula below exactly rather than scraping amounts out
      of the DOM, which would pick up the price-difference and credit-note rows by accident.*@
    <div class="card card-default cw-fees-admin"
         data-editable="@(exchangeRequestModel.IsEditable ? "true" : "false")"
         data-mode="@(exchangeRequestModel.ExchangeReturnMode == ExchangeReturnMode.Exchange ? "exchange" : "credit-note")"
         data-rejected="@(isRejected ? "true" : "false")"
         data-base-quantity="@Model.Quantity"
         data-price-difference="@priceDifference.ToString(System.Globalization.CultureInfo.InvariantCulture)"
         data-product-subtotal="@productSubTotal.ToString(System.Globalization.CultureInfo.InvariantCulture)"
         data-op-per-units="@((int)FeeRuleOperator.PerUnitsQuantity)"
         data-op-fixed-price="@((int)FeeRuleOperator.FixedPrice)">
        <div class="card-body">
            <table class="table table-bordered fees-admin">
                <colgroup>
                    <col class="concept" />
                    <col class="data" />
                    <col class="info" />
                    <col />
                </colgroup>

                @if (exchangeRequestModel.ExchangeReturnMode == ExchangeReturnMode.Exchange)
                {
                    <tr>
                        <td class="fees-label">
                            <label class="col-form-label">Product Sub-Total</label>
                        </td>
                        <td colspan="3">
                            <div class="form-text-row">
                                @currency.CurrencyCode
                                <span class="amount">@FormatHelper.FormatAbsPrice(productSubTotal)</span>
                            </div>
                        </td>
                    </tr>
                }

                @if (exchangeRequestModel.ExchangeType != ExchangeType.Exact)
                {
                    <tr class="price-diff">
                        <td class="fees-label">
                            <label class="col-form-label">Price Difference</label>
                        </td>
                        <td colspan="3">
                            <div class="form-text-row">
                                @currency.CurrencyCode
                                <span class="price-display">@FormatHelper.FormatAbsPrice(priceDifference)</span>
                                @if (priceDifference < 0)
                                {
                                    <span>&nbsp; (in customer's favour)</span>
                                }
                            </div>
                        </td>
                    </tr>
                }

                @if (exchangeRequestModel.ExchangeReturnMode == ExchangeReturnMode.CreditNote)
                {
                    <tr class="credit-note-info">
                        <td class="fees-label">
                            <label class="col-form-label">
                                @(creditNoteIsEstimate ? "Returned Value" : "Credit Note Raised")
                            </label>
                        </td>
                        <td colspan="3">
                            <div class="form-text-row">
                                @currency.CurrencyCode
                                <span class="price-display">@FormatHelper.FormatAbsPrice(creditNoteIsEstimate ? productSubTotal : creditNoteRaised)</span>
                                @if (isRejected)
                                {
                                    <span>&nbsp; (the return was rejected: nothing is credited)</span>
                                }
                                else if (!creditNoteIsEstimate && creditNoteRaised <= decimal.Zero)
                                {
                                    <span>&nbsp; (no credit note — the value went back to the employee's entitlement)</span>
                                }
                            </div>
                        </td>
                    </tr>
                }

                @if (exchangeRequestModel.ShowShippingFee)
                {
                    shippingFee = exchangeRequestModel.ShippingFeeAmount;
                    @await Component.InvokeAsync("FeeConcept", new { exchangeModel = exchangeRequestModel, feeType = FeeType.ReShippingFee })
                }
                @if (exchangeRequestModel.ShowRepackagingFee)
                {
                    repackagingFee = exchangeRequestModel.RepackagingFeeAmount;
                    @await Component.InvokeAsync("FeeConcept", new { exchangeModel = exchangeRequestModel, feeType = FeeType.RepackagingFee })
                }

                @await Component.InvokeAsync("FeeConcept", new { exchangeModel = exchangeRequestModel, feeType = FeeType.AdditionalFee })

                @if (priceDifference < 0 && !isRejected)
                {
                    <tr class="total-section">
                        <td class="fees-label">
                            <label class="col-form-label">Sub-Total on Customer's favour</label>
                        </td>
                        <td colspan="3">
                            <div class="form-text-row">
                                @currency.CurrencyCode
                                <span class="amount">@Math.Abs(priceDifference).ToString("F")</span>&nbsp;
                                <span>(returned to whatever paid for it — subsidy to the employee's entitlement, cash as a Credit Note)</span>
                            </div>
                        </td>
                    </tr>
                }
                <tr class="total-section total">
                    @{
                        decimal feesTotal = (shippingFee + repackagingFee + additionalFee);
                        decimal totalAmount = priceDifference >= decimal.Zero ? (priceDifference + feesTotal) : feesTotal;
                        if (isRejected)
                        {
                            totalAmount = decimal.Zero;
                        }
                        else if (exchangeRequestModel.ExchangeReturnMode == ExchangeReturnMode.CreditNote)
                        {
                            //An unresolved case can only bound it: this is the figure a wholly cash-funded
                            //line would produce, and any subsidy on the line reduces it.
                            totalAmount = creditNoteIsEstimate
                                ? productSubTotal - feesTotal
                                : creditNoteRaised;
                        }
                    }
                    <td class="fees-label">
                        @if (isRejected)
                        {
                            <label class="col-form-label">Nothing Credited or Charged (Return Rejected)</label>
                        }
                        else if (exchangeRequestModel.ExchangeReturnMode == ExchangeReturnMode.Exchange)
                        {
                            <label class="col-form-label">Minimum Payment Required</label>
                        }
                        else if (creditNoteIsEstimate)
                        {
                            <label class="col-form-label">Most the Customer Could Receive as Credit Notes</label>
                        }
                        else
                        {
                            <label class="col-form-label">Credit Notes Amount Received</label>
                        }
                    </td>
                    <td colspan="3">
                        <div class="form-text-row">
                            @currency.CurrencyCode
                            <span class="price-total">@Math.Abs(totalAmount).ToString("F")</span>
                        </div>
                    </td>
                </tr>

                @if (exchangeRequestModel.ExchangeReturnMode == ExchangeReturnMode.CreditNote && creditNoteIsEstimate)
                {
                    <tr class="credit-note-caveat">
                        <td colspan="4">
                            <div class="form-text-row">
                                <em>
                                    Anything on this line paid for by subsidy is given back to the employee's
                                    entitlement when the return is authorised, not raised as a Credit Note, so
                                    the figure above is an upper bound until then.
                                </em>
                            </div>
                        </td>
                    </tr>
                }
            </table>
        </div>
    </div>

    <script>
        $(function () {
            var $panel = $('.cw-fees-admin');
            if (!$panel.length) return;

            // ---- additional fee show/hide (unchanged behaviour) ----
            var $enable = $('#enable-additional-fee');
            var $fields = $('.additional-fee-fields');
            $enable.on('change', function () {
                if ($enable.is(':checked')) {
                    $fields.show();
                } else {
                    $fields.hide();
                    $fields.find('input[type="number"], input[type="text"]').val('');
                }
            });

            // A resolved case shows its FROZEN charges, and its credit-note row shows the note that was
            // actually raised. Re-pricing either here would put a live figure on screen while the customer
            // is charged the stored one, so the panel stays exactly as rendered.
            if ($panel.data('editable') !== true && $panel.data('editable') !== 'true') return;

            var OP_PER_UNITS = parseInt($panel.data('op-per-units'), 10);
            var OP_FIXED     = parseInt($panel.data('op-fixed-price'), 10);
            var baseQty      = parseInt($panel.data('base-quantity'), 10);
            var mode         = $panel.data('mode');
            var priceDiff    = parseFloat($panel.data('price-difference')) || 0;
            var productSub   = parseFloat($panel.data('product-subtotal')) || 0;
            var rejected     = $panel.data('rejected') === true || $panel.data('rejected') === 'true';

            // Mirrors ExchangesHelper.CalculateFeeAmount, including its divide-by-zero guard. If these two
            // ever disagree the screen lies about the money, so keep them in step.
            function calcFee(qty, amount, op, factor) {
                if (op === OP_PER_UNITS) {
                    if (!(factor > 0)) return 0;
                    return Math.ceil(qty / factor) * amount;
                }
                if (op === OP_FIXED) return qty > 0 ? amount : 0;
                return 0;
            }

            function repriceRow($row) {
                var $qty = $row.find('.fee-qty input[type="text"], .fee-qty input:not([type="hidden"])').first();
                var $op  = $row.find('.fee-operator');
                if (!$qty.length || !$op.length) return;   // no live rule on this row: leave it alone

                var qty = parseInt($qty.val(), 10);
                if (isNaN(qty) || qty < 0) qty = 0;

                var capped = qty > baseQty;
                if (capped) { qty = baseQty; $qty.val(qty); }

                var amount = calcFee(qty,
                                     parseFloat($op.siblings('.fee-amount').val()) || 0,
                                     parseInt($op.val(), 10),
                                     parseInt($op.siblings('.fee-qtyfactor').val(), 10));

                $row.find('.fee-total .value-container').text(amount.toFixed(2));
                $row.toggleClass('fee-qty-capped', capped);
                return amount;
            }

            function repriceAll() {
                var fees = 0;
                $panel.find('tr.fee-group').each(function () {
                    var a = repriceRow($(this));
                    if (typeof a === 'number') fees += a;
                });
                // the ad-hoc fee is typed, not priced
                $panel.find('.additional-fee-fields input[type="number"]').each(function () {
                    fees += parseFloat($(this).val()) || 0;
                });

                // Same formula as the Razor above — keep the two in step. In credit-note mode this is the
                // upper bound, which is what an unresolved case shows.
                var total = rejected
                    ? 0
                    : (mode === 'credit-note')
                        ? productSub - fees
                        : (priceDiff >= 0 ? priceDiff + fees : fees);

                $panel.find('.price-total').text(Math.abs(total).toFixed(2));
            }

            $panel.on('change blur', '.fee-qty input, .additional-fee-fields input', repriceAll);
            $panel.on('change', '#enable-additional-fee', repriceAll);
        });
    </script>}
```

### 23. DecisionNote (B2)

`Plugins/Nop.Plugin.CW.Core/Models/Exchanges/CustomerExchangeRequestModel.cs`

Depends on: nothing.

**23a. REPLACE lines 83–86** (4 lines → 9 lines)

FIND:

```csharp
    public string CurrentAttributesJson { get; set; }

    #endregion
}
```

REPLACE WITH:

```csharp
    public string CurrentAttributesJson { get; set; }

    /// <summary>
    /// The note staff recorded when they authorised or rejected the return; empty until the return is decided
    /// </summary>
    public string DecisionNote { get; set; }

    #endregion
}
```

### 24. The decision note on My returns (B2)

`Plugins/Nop.Plugin.CW.Core/Factories/ReturnRequestItemModelFactory.cs`

Depends on: changes 1, 23.

**24a. REPLACE lines 151–154** (4 lines → 9 lines)

FIND:

```csharp
        model.AllowChangeReturnedItem = (returnRequest.ReturnRequestStatus == ReturnRequestStatus.Pending);

        var extraInfo = await _exchangesDisplayService.GetReturnRequestExtraInfoAsync(returnRequest, orderItem);

```

REPLACE WITH:

```csharp
        model.AllowChangeReturnedItem = (returnRequest.ReturnRequestStatus == ReturnRequestStatus.Pending);

        //the note staff recorded with their decision, once the return has been decided
        if (returnRequest.ReturnRequestStatus == ReturnRequestStatus.ReturnAuthorized
            || ExchangesHelper.IsRejectedReturnRequestStatus(returnRequest.ReturnRequestStatus))
            model.DecisionNote = (await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequest.Id))?.DispositionReason;

        var extraInfo = await _exchangesDisplayService.GetReturnRequestExtraInfoAsync(returnRequest, orderItem);

```

**24b. REPLACE lines 38–77** (40 lines → 43 lines)

FIND:

```csharp
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

```

REPLACE WITH:

```csharp
    protected readonly IWorkContext _workContext;
    protected readonly ReturnRequestSettings _returnRequestSettings;
    protected readonly IReturnCaseService _returnCaseService;

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
        ReturnRequestSettings returnRequestSettings,
        IReturnCaseService returnCaseService)
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
        _returnCaseService = returnCaseService;
    }

```

**24c. REPLACE lines 3–9** (7 lines → 9 lines)

FIND:

```csharp
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
```

REPLACE WITH:

```csharp
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
```

### 25. The decision note on My returns (B2)

`Plugins/Nop.Plugin.CW.Core/Views/ReturnRequest/CustomerReturnRequests.cshtml`

Depends on: change 23.

**25a. REPLACE lines 170–173** (4 lines → 12 lines)

FIND:

```cshtml
                    }

                    @if (allowChangeReturn && exchangeRequestData != null && exchangeRequestData.ExchangeType != ExchangeType.Exact)
                    {
```

REPLACE WITH:

```cshtml
                    }

                    @if (!string.IsNullOrWhiteSpace(exchangeRequestData?.DecisionNote))
                    {
                        <li class="comments decision-note">
                            <label>@T("Plugins.CW.Core.Exchanges.ReturnRequests.DecisionNote")</label>
                            <div>@Html.Raw(htmlFormatter.FormatText(exchangeRequestData.DecisionNote))</div>
                        </li>
                    }

                    @if (allowChangeReturn && exchangeRequestData != null && exchangeRequestData.ExchangeType != ExchangeType.Exact)
                    {
```

### 26. Register ReturnRequestOverrideService (B3)

`Plugins/Nop.Plugin.CW.Core/Infrastructure/NopStartup.cs`

Depends on: change 11.

**26a. REPLACE lines 113–116** (4 lines → 7 lines)

FIND:

```csharp

        services.AddScoped<IShipmentService, ShipmentOverrideService>();

        services.AddScoped<IBulkOrderService, BulkOrderService>();
```

REPLACE WITH:

```csharp

        services.AddScoped<IShipmentService, ShipmentOverrideService>();

        //a rejected return's units can be returned again
        services.AddScoped<IReturnRequestService, ReturnRequestOverrideService>();

        services.AddScoped<IBulkOrderService, BulkOrderService>();
```

### 27. The two new templates and three resources (D1, B2)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

Depends on: change 2.

**27a. REPLACE lines 1983–1986** (4 lines → 5 lines)

FIND:

```csharp
            ["Plugins.CW.Core.Exchanges.ReturnRequests.ReplacementProduct"] = "Replacement Product:",
            ["Plugins.CW.Core.Exchanges.ReturnRequests.EditExchangeItem"] = "Edit Exchange Item",

            // Exchanges — return wizard: start page, checks, picker labels, summary totals, receipts
```

REPLACE WITH:

```csharp
            ["Plugins.CW.Core.Exchanges.ReturnRequests.ReplacementProduct"] = "Replacement Product:",
            ["Plugins.CW.Core.Exchanges.ReturnRequests.EditExchangeItem"] = "Edit Exchange Item",
            ["Plugins.CW.Core.Exchanges.ReturnRequests.DecisionNote"] = "Note from the store:",

            // Exchanges — return wizard: start page, checks, picker labels, summary totals, receipts
```

**27b. REPLACE lines 1861–1864** (4 lines → 5 lines)

FIND:

```csharp
            ["Plugins.CW.Core.Admin.Returns.Fields.Action.NotForReason"] = "The requested action is not offered for this reason. Please choose one from the list.",
            ["Plugins.CW.Core.Admin.Returns.Fields.Action.LoadError"] = "The actions for this reason could not be loaded. Please reload the page.",
            ["Plugins.CW.Core.Admin.Returns.CompletionNotification.NotQueued"] = "No e-mail was queued: the return is not waiting for the customer to check out, or the 'ReturnAuthorisedPayment.CustomerNotification' message template is not active for this store.",

```

REPLACE WITH:

```csharp
            ["Plugins.CW.Core.Admin.Returns.Fields.Action.NotForReason"] = "The requested action is not offered for this reason. Please choose one from the list.",
            ["Plugins.CW.Core.Admin.Returns.Fields.Action.LoadError"] = "The actions for this reason could not be loaded. Please reload the page.",
            ["Plugins.CW.Core.Admin.Returns.Reopen.UnitsReturnedAgain"] = "This return cannot be re-opened: the customer has returned the same units again in another return. Decide that return instead.",
            ["Plugins.CW.Core.Admin.Returns.CompletionNotification.NotQueued"] = "No e-mail was queued: the return is not waiting for the customer to check out, or the 'ReturnAuthorisedPayment.CustomerNotification' message template is not active for this store.",

```

**27c. REPLACE lines 1202–1205** (4 lines → 5 lines)

FIND:

```csharp
            ["Plugins.CW.Core.Admin.CreditNotes.Delete.ConfirmReturnRequest"] = "This credit note was created by a return request. Deleting it will NOT undo the return itself. Are you sure you want to delete it?",
            ["Plugins.CW.Core.Admin.CreditNotes.Delete.Error"] = "Failed to delete the credit note.",
            ["Plugins.CW.Core.Admin.CreditNotes.AppliedTitle"] = "Credit Notes",
            ["Plugins.CW.Core.Admin.CreditNotes.AppliedLabel"] = "Applied to this order",
```

REPLACE WITH:

```csharp
            ["Plugins.CW.Core.Admin.CreditNotes.Delete.ConfirmReturnRequest"] = "This credit note was created by a return request. Deleting it will NOT undo the return itself. Are you sure you want to delete it?",
            ["Plugins.CW.Core.Admin.CreditNotes.Delete.Error"] = "Failed to delete the credit note.",
            ["Plugins.CW.Core.Admin.CreditNotes.Delete.Spent"] = "This credit note has already paid for an order, so it cannot be deleted.",
            ["Plugins.CW.Core.Admin.CreditNotes.AppliedTitle"] = "Credit Notes",
            ["Plugins.CW.Core.Admin.CreditNotes.AppliedLabel"] = "Applied to this order",
```

**27d. REPLACE lines 232–235** (4 lines → 61 lines)

FIND:

```csharp
                $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.ReturnAuthorisedExceptedCustomer}",
                "Sent to the customer when a return is authorised and nothing is due: the order for the replacement has been placed for them.");
        }
    }
```

REPLACE WITH:

```csharp
                $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.ReturnAuthorisedExceptedCustomer}",
                "Sent to the customer when a return is authorised and nothing is due: the order for the replacement has been placed for them.");
        }
    }

    /// <summary>
    /// Installs the two e-mails that tell the customer how a return ended without a replacement, once each: a store that
    /// has edited one keeps its wording.
    /// </summary>
    protected async Task InstallReturnOutcomeTemplatesAsync()
    {
        var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
            ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

        if (!(await _messageTemplateService.GetMessageTemplatesByNameAsync(CWMessageTemplateSystemNames.ReturnAuthorisedCreditCustomer)).Any())
        {
            await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
            {
                Name = CWMessageTemplateSystemNames.ReturnAuthorisedCreditCustomer,
                Subject = "Your return #%ReturnRequest.CustomNumber% is approved",
                Body = "Hello %Customer.FullName%,<br /><br />" +
                       "We have approved your return #%ReturnRequest.CustomNumber%: %ReturnRequest.Product.Quantity% x %ReturnRequest.Product.Name%.<br />" +
                       "%if (%PackName.Name% != \"\") For: %PackName.Name%<br /> endif%" +
                       "<br />" +
                       "%if (%ReturnRequest.HasCreditNote%) We have added store credit of <strong>%ReturnRequest.CreditNoteAmount%</strong> to your account. " +
                       "It is used automatically at your next checkout.<br /> endif%" +
                       "%if (%ReturnRequest.ReturnedToAllowance%) What your allowance paid for this item has been given back to your allowance.<br /> endif%" +
                       "<br />" +
                       "Thank you,<br />%Store.Name%<br />",
                IsActive = true,
                EmailAccountId = emailAccount?.Id ?? 0
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(
                $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.ReturnAuthorisedCreditCustomer}",
                "Sent to the customer when a return is authorised without a replacement: the store credit raised for it, and whether what their allowance paid was given back.");
        }

        if (!(await _messageTemplateService.GetMessageTemplatesByNameAsync(CWMessageTemplateSystemNames.ReturnRejectedCustomer)).Any())
        {
            await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
            {
                Name = CWMessageTemplateSystemNames.ReturnRejectedCustomer,
                Subject = "Update on your return #%ReturnRequest.CustomNumber%",
                Body = "Hello %Customer.FullName%,<br /><br />" +
                       "Your return #%ReturnRequest.CustomNumber% (%ReturnRequest.Product.Quantity% x %ReturnRequest.Product.Name%) has been closed " +
                       "without a replacement or store credit. Status: %ReturnRequest.Status%.<br />" +
                       "%if (%PackName.Name% != \"\") For: %PackName.Name%<br /> endif%" +
                       "<br />" +
                       "%if (%ReturnRequest.HasDecisionNote%) Our note: %ReturnRequest.DecisionNote%<br /><br /> endif%" +
                       "If you have any questions, please contact us.<br /><br />" +
                       "Thank you,<br />%Store.Name%<br />",
                IsActive = true,
                EmailAccountId = emailAccount?.Id ?? 0
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(
                $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.ReturnRejectedCustomer}",
                "Sent to the customer when a return is rejected or cancelled, with the decision note recorded on the return page.");
        }
    }
```

**27e. REPLACE lines 97–100** (4 lines → 5 lines)

FIND:

```csharp
        await InstallReturnReceiptTemplateAsync();
        await InstallReturnApprovalTemplatesAsync();
        await InstallCustomerRegistrationTemplateAsync();
        await InstallSubsidyExpiryReminderTemplatesAsync();
```

REPLACE WITH:

```csharp
        await InstallReturnReceiptTemplateAsync();
        await InstallReturnApprovalTemplatesAsync();
        await InstallReturnOutcomeTemplatesAsync();
        await InstallCustomerRegistrationTemplateAsync();
        await InstallSubsidyExpiryReminderTemplatesAsync();
```

### 28. DATA version bump, so the update installs the templates and resources

`Plugins/Nop.Plugin.CW.Core/plugin.json`

Depends on: nothing.

**28a. REPLACE lines 3–7** (5 lines → 5 lines)

FIND:

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_12_DATA_0.2",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```

REPLACE WITH:

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_16_DATA_0.1",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```

### 29. A replacement is added to the cart without the products it requires (D4, found in the dev-site test run)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

Depends on: nothing. Added after the test run of 2026-11-16: change 12 stops the required-product *warning*, but
`AddToCartAsync` (nop's, and CW's packname copy) also *adds* the required products itself when the product says
"automatically add". For the hat that meant adding the shirt with no size chosen, which failed the whole approval
("Please select Size").

**29a. REPLACE lines 600–604** (5 lines → 7 lines)

FIND:

```csharp
            var warnings = sourcePackName != null
                ? await _shoppingCartService.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, returnRequest.StoreId,
                    sourcePackName.PackNameId, template.AttributesXml, quantity: template.Quantity)
                : await _shoppingCartService.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, returnRequest.StoreId,
                    template.AttributesXml, quantity: template.Quantity);
```

REPLACE WITH:

```csharp
            //addRequiredProducts: false - a replacement is not a sale, so the products it requires are not added
            //with it (the customer has them from the original order, and an unconfigured one fails the whole add)
            var warnings = sourcePackName != null
                ? await _shoppingCartService.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, returnRequest.StoreId,
                    sourcePackName.PackNameId, template.AttributesXml, quantity: template.Quantity, addRequiredProducts: false)
                : await _shoppingCartService.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, returnRequest.StoreId,
                    template.AttributesXml, quantity: template.Quantity, addRequiredProducts: false);
```

### 30. The customer's name row says "Customer name" (the e-mail row below it is nop's "Customer")

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/Exchanges/CWReturnRequestModel.cs`

Depends on: nothing. Added after the test run of 2026-11-16 (two rows read "Customer").

**30a. REPLACE line 197** (1 line → 1 line)

FIND:

```csharp
    [DisplayName("Customer")]
```

REPLACE WITH:

```csharp
    [DisplayName("Customer name")]
```

## 5. After applying

1. **Build** the solution (CW.Core views compile at run time — open the pages in step 4).
2. **Unit tests** — `dotnet test Tests/CW.Tests -p:SolutionDir=<repo root> --filter FullyQualifiedName~CW.Tests.Core`.
   48 new cases, already in the test project:
   - `Core/Helpers/ExchangesHelperTests.cs` — `IsRejectedReturnRequestStatus` (7), `IsBackInStock` (5)
   - `Core/Controllers/CreditNotesControllerTests.cs` — the controller's new constructor argument; spent note refused, unused note deleted (2)
   - `Core/Infrastructure/NopStartupTests.cs` — `ReturnRequestOverrideService` replaces `IReturnRequestService` (1)
   - `Core/Services/Orders/ReturnRequestAvailabilityTests.cs` (new) — rejected units free again, cancelled as nop, every other status kept, never below 0, other orders and non-returnable products untouched (10)
   - `Core/Services/Orders/ReplacementOnlyCartTests.cs` (new) — replacement-only cart, mixed cart, unsaved line, empty cart, required-product exemption (5)
   - `Core/Services/Exchanges/CreditReturnModeTests.cs` (new) — both modes, no credit with Credit Notes off (2)
   - `Core/Controllers/ReturnRejectionEmailTests.cs` (new) — sent on the move to Rejected/Cancelled only, never fails the save; a rejected return re-opens only while its units are free; each status's case-line state (16)
3. **Restart** — the DATA version makes the plugin update run: it installs the two templates (Content management →
   Message templates: `ReturnRejected.CustomerNotification`, `ReturnAuthorisedCredit.CustomerNotification`, both active)
   and the three resources. Check System → Log for errors from the update.
4. **Dev-site test run** (I can run it once you say the site is up; side effects: new returns, orders, credit notes and
   queued e-mails on dev data, stock moves on the test products):
   - T1 (D1) A customer's credit note that paid an order: no Delete button; a fresh manual note: Delete works.
   - T2 (D2/D3) An exchange return: Basic Data shows Store, Branch, packname, replacement product/options/price; a credit return shows no Exchange type.
   - T3 (D4) Exchange to a product with *Require other products* → authorise → replacement in cart (or order placed), no required product added.
   - T4 (D5) Reject a credit return → fees panel shows *Nothing Credited or Charged*.
   - T5 (B1) Tick Back in stock on reason, action and Return authorized rules → authorise → stock up by the whole quantity, once; untick one → no stock move.
   - T6 (B2) Reject with a note → e-mail queued with the note; My returns shows the note. Rejected → Cancelled → no second e-mail. Authorise a credit return → credit e-mail with the amount; Undo approval → the unsent e-mail is gone from the queue.
   - T7 (B3) After T4/T6 the same units can be returned again in the wizard; a pending return still holds its units. With the units in a new return, set the old rejected one to Pending → refused with the message.
   - T8 (B4) Replacement-only checkout (packname and plain): delivery 0, the redelivery fee still charged; add another product to the same destination → normal delivery.
   - T9 (B5) Switch Credit Notes off → wizard offers only Exchange; an in-progress credit return still authorises; switch back on.
   - T10 (Q1) Reject a return, then set it to Pending → the case line reads Pending (admin page's case lines); authorise it → it executes and the case resolves.
   - T11 (Q2) A replacement in the cart, then switch Exchanges off → checkout still works (no quantity or required-product warning, no delivery charge); switch back on.

## 6. Found on the way — not changed here

- `CWReturnRequestModel`'s copy constructor copies nop's members by hand (`CLAUDE.md` §4 says derive from nop's record
  copy constructor); `_CreateOrUpdate.FeesAndCharges.cshtml` keeps an inline `<script>` (§9.14/§9.15). Both pre-date
  this work.
- The scheduled task *Delete guests* fails every 10 minutes on the dev site
  (`FK_CORE_PN_PackName_CustomerId_Customer_Id`): a guest linked to a packname cannot be deleted.
- `CWDocs/images/returns/a-credit-mode-top.jpg` is unused by the guide; delete it when convenient.

## 7. Documents updated after the re-test

`CWDocs/Exchanges_Returns_Credit_Notes_Functional_Guide.md` (+ .docx), `CWPlans/Exchanges_Returns_CreditNotes_Reference.md`,
`CLAUDE.md` §13.4 (the new `IReturnRequestService` override), §14.6 (templates) — by me, once T1–T9 pass.
