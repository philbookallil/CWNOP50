# Round 1, phase A — return e-mails, packname and subsidy tokens, plain-order invoice

**Date:** 2026-11-06. **Owner:** Eduardo. **Status:** spec — ready to apply.

**What this phase does.** The first half of Round 1 of the 4.1 gap list (`LegacyVersions/NOP41_To_500_Gap_Analysis.md`):
what the customer receives after a return is approved, the packname and subsidy tokens, and the invoice of a plain order.
Phase R1-B (admin return page and the replacement picker: D5, D6, OI 5.4, D4, the OI 5.3 check) follows in its own
document.

| Item | What changes |
|---|---|
| **D1 / OI 2.1.1** | The two approval e-mails (`ReturnAuthorisedPayment.CustomerNotification`, `ReturnAuthorisedExcepted.CustomerNotification`) are installed, so an approval finally queues one. Written for 5.00. |
| **OI 5.6** (made live by D1) | The approval e-mail queued for a line is remembered (`ReturnRequestItem.ApprovalQueuedEmailId`); an undone approval deletes it while the queue has not sent it. A sent one stays as the record. |
| **Notify button** | *Notify completion required* re-sends the "payment needed" template with what the cart asks for now, instead of a hard-coded English e-mail that bypassed templates. The hard-coded sender is deleted (dead). |
| **D7 / OI 2.1.6** | `%PackName.Name%` (the packname the returned item was ordered for) on the return receipt (e-mail, confirmation screen, PDF) and both approval e-mails. The receipt gets a `FOR:` line; an existing receipt template gets it once, without touching the rest of its wording. |
| **F7 / OI 2.1.7** | `%Subsidy.Details%` in the welcome e-mail: one line per subsidy with its current season, its credit and its items. The welcome template's old subsidy-name line is replaced by it (only that exact line). `%Subsidy.SubsidyName%` stays. |
| **OI 5.5** | A plain (non-packname) order paid partly with store credit, or carrying exchange fees, is printed with those rows, laid out like its e-mail: nop's rows, then store credit and exchange fees, then the order total. A packname order with exchange fees but no settlement now prints its fees too. |

**Decisions taken (2026-11-05).** Spec applied by Eduardo (no direct code edits). F7 as one list token. D6 and D4 are
phase R1-B.

**What it deliberately does not do.**
- No approval e-mail for a **credit-note-only** approval: 4.1 had none either; nop's *Notify customer about status
  change* button stays the way to tell the customer.
- A return of several lines still sends **one approval e-mail per line** (each line is approved on its own, as in 4.1).
- An undo still does not cancel an auto-placed order (pathway A, the 4.1 behaviour); its "order placed" e-mail is
  withdrawn if unsent, like the other.
- The fee figures in the payment e-mail print `$0.00` for a fee that does not apply (the body says "where they apply");
  a store can reword its template.

**Feature switches** (`Feature_Toggles_Reference.md` §1). Nothing new opens when a feature is off: the approval
workflow and the Notify button are only reached with Exchanges on; `%PackName.Name%` is empty for an item ordered
without a packname (PackName off records none); `%Subsidy.Details%` is empty when no subsidy was assigned (Subsidy off
assigns none). The invoice rows read the order's own settlement and fees, which exist only when the features ran.

**Not verified by building.** The changes were checked against the physical files (staged 2026-11-06), but no .NET
SDK is reachable here, so the build is the first compile. The tests come with the phase and are already in
`Tests/CW.Tests` (they compile once the changes are applied).

## How to apply

Labels: **REPLACE** — the FIND block, at the lines given, becomes the REPLACE WITH block. **WHOLE FILE** — the file's
entire content becomes the block. **NEW FILE** — create the file with the block.

Each change names its file under its heading. Within a file the blocks are listed **bottom to top**, and the line
numbers of each FIND are those of the file as it stands when that block is reached, so applying them in the order
given never moves the next one. No change depends on another to be correct, but the project compiles only once all
are applied (new members are used across files).

## Changes
### ReturnRequestItem.cs

`Plugins/Nop.Plugin.CW.Core/Domain/Exchanges/ReturnRequestItem.cs`

The new column that remembers the approval e-mail queued for a line (OI 5.6).

#### 1. REPLACE — lines 84–89 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Domain/Exchanges/ReturnRequestItem.cs`

FIND (lines 84–89, 6 lines):

```csharp
    /// </summary>
    public decimal? AdditionalFeeAmount { get; set; }

    #region Custom properties

    /// <summary>
```

REPLACE WITH (12 lines):

```csharp
    /// </summary>
    public decimal? AdditionalFeeAmount { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the approval e-mail queued for the customer (FK-free reference to NOP QueuedEmail),
    /// so an undone approval can withdraw it before it is sent
    /// </summary>
    public int? ApprovalQueuedEmailId { get; set; }

    #region Custom properties

    /// <summary>
```

### ReturnRequestItemBuilder.cs

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Exchanges/ReturnRequestItemBuilder.cs`

The column in the schema, for a new installation.

#### 2. REPLACE — lines 24–28 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Exchanges/ReturnRequestItemBuilder.cs`

FIND (lines 24–28, 5 lines):

```csharp
            .WithColumn(nameof(ReturnRequestItem.ReturnedOrderItemId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.AdditionalFeeName)).AsString().Nullable()
            .WithColumn(nameof(ReturnRequestItem.AdditionalFeeAmount)).AsDecimal(18, 4).Nullable();
    }
}
```

REPLACE WITH (6 lines):

```csharp
            .WithColumn(nameof(ReturnRequestItem.ReturnedOrderItemId)).AsInt32().NotNullable()
            .WithColumn(nameof(ReturnRequestItem.AdditionalFeeName)).AsString().Nullable()
            .WithColumn(nameof(ReturnRequestItem.AdditionalFeeAmount)).AsDecimal(18, 4).Nullable()
            .WithColumn(nameof(ReturnRequestItem.ApprovalQueuedEmailId)).AsInt32().Nullable();
    }
}
```

### MigrationVersions.cs

`Plugins/Nop.Plugin.CW.Core/Data/MigrationVersions.cs`

The version stamp of the update migration.

#### 3. REPLACE — lines 10–12 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Data/MigrationVersions.cs`

FIND (lines 10–12, 3 lines):

```csharp
    /// </summary>
    public const string InitialSchema = "2026/10/23 12:00:00";
}
```

REPLACE WITH (8 lines):

```csharp
    /// </summary>
    public const string InitialSchema = "2026/10/23 12:00:00";

    /// <summary>
    /// ReturnRequestItem.ApprovalQueuedEmailId, on a database installed before the column was in the schema
    /// </summary>
    public const string ReturnRequestItemApprovalQueuedEmailId = "2026/11/06 12:00:00";
}
```

### ReturnRequestItemApprovalQueuedEmailIdMigration.cs

`Plugins/Nop.Plugin.CW.Core/Data/Migrations/ReturnRequestItemApprovalQueuedEmailIdMigration.cs`

The update migration that adds the column to `testnop50` (and any database installed before it). An installation marks it applied without running it.

#### 4. NEW FILE

`Plugins/Nop.Plugin.CW.Core/Data/Migrations/ReturnRequestItemApprovalQueuedEmailIdMigration.cs`

```csharp
using FluentMigrator;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Data.Migrations;

/// <summary>
/// Adds ReturnRequestItem.ApprovalQueuedEmailId to a database installed before the column was part of the schema
/// </summary>
/// <remarks>
/// An installation creates the column from the builder and nop marks this migration as applied without running it;
/// a plugin update runs it, and it adds the column only when it is missing.
/// </remarks>
[NopMigration(MigrationVersions.ReturnRequestItemApprovalQueuedEmailId, "CW.Core ReturnRequestItem.ApprovalQueuedEmailId", MigrationProcessType.Update)]
public class ReturnRequestItemApprovalQueuedEmailIdMigration : Migration
{
    public override void Up()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(ReturnRequestItem));

        if (!Schema.Table(table).Column(nameof(ReturnRequestItem.ApprovalQueuedEmailId)).Exists())
            Alter.Table(table).AddColumn(nameof(ReturnRequestItem.ApprovalQueuedEmailId)).AsInt32().Nullable();
    }

    public override void Down()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(ReturnRequestItem));

        if (Schema.Table(table).Column(nameof(ReturnRequestItem.ApprovalQueuedEmailId)).Exists())
            Delete.Column(nameof(ReturnRequestItem.ApprovalQueuedEmailId)).FromTable(table);
    }
}
```

### CWTokenGroupNames.cs

`Plugins/Nop.Plugin.CW.Core/Services/Messages/CWTokenGroupNames.cs`

The token group of `%PackName.Name%` on the return templates (D7).

#### 5. REPLACE — lines 15–18 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/CWTokenGroupNames.cs`

FIND (lines 15–18, 4 lines):

```csharp
    /// </summary>
    public const string SubsidyTokens = "Subsidy tokens";
}

```

REPLACE WITH (9 lines):

```csharp
    /// </summary>
    public const string SubsidyTokens = "Subsidy tokens";

    /// <summary>
    /// Represents the token naming the packname a returned item was ordered for
    /// </summary>
    public const string ReturnPackNameTokens = "Return packname tokens";
}

```

### IMessageTokenProviderOverride.cs

`Plugins/Nop.Plugin.CW.Core/Services/Messages/IMessageTokenProviderOverride.cs`

The packname token method (D7).

#### 6. REPLACE — lines 16–18 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/IMessageTokenProviderOverride.cs`

FIND (lines 16–18, 3 lines):

```csharp
    /// </param>
    Task AddExchangeReturnTokensAsync(IList<Token> tokens, ReturnRequest returnRequest, decimal pendingAmount, ReturnRequestItem returnRequestItem = null);
}
```

REPLACE WITH (11 lines):

```csharp
    /// </param>
    Task AddExchangeReturnTokensAsync(IList<Token> tokens, ReturnRequest returnRequest, decimal pendingAmount, ReturnRequestItem returnRequestItem = null);

    /// <summary>
    /// Adds the packname token of a returned item: the packname it was ordered for, empty when there is none.
    /// </summary>
    /// <param name="tokens">List of already added tokens</param>
    /// <param name="orderItem">The returned order item</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task AddReturnPackNameTokensAsync(IList<Token> tokens, OrderItem orderItem);
}
```

### MessageTokenProviderOverride.cs

`Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`

The packname token (D7) and the token group on the three return templates.

#### 7. REPLACE — lines 730–733 (1 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`

FIND (lines 730–733, 4 lines):

```csharp
    }

    #endregion
}
```

REPLACE WITH (18 lines):

```csharp
    }

    /// <summary>
    /// Adds the packname the returned item was ordered for, as it was named when the order was placed; empty for an
    /// item ordered without a packname
    /// </summary>
    /// <param name="tokens">List of already added tokens</param>
    /// <param name="orderItem">The returned order item</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task AddReturnPackNameTokensAsync(IList<Token> tokens, OrderItem orderItem)
    {
        var packNameOrderItem = orderItem == null ? null : await _cwOrderService.GetPackNameOrderItemAsync(orderItem.Id);

        tokens.Add(new Token("PackName.Name", packNameOrderItem?.PackName ?? string.Empty));
    }

    #endregion
}
```

#### 8. REPLACE — lines 348–352 (2 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`

FIND (lines 348–352, 5 lines):

```csharp
                    TokenGroupNames.CustomerTokens,
                    TokenGroupNames.ReturnRequestTokens,
                    CWTokenGroupNames.ExchangeReturnsTokens
                };
            case CWMessageTemplateSystemNames.CustomerRegistrationImport:
```

REPLACE WITH (6 lines):

```csharp
                    TokenGroupNames.CustomerTokens,
                    TokenGroupNames.ReturnRequestTokens,
                    CWTokenGroupNames.ExchangeReturnsTokens,
                    CWTokenGroupNames.ReturnPackNameTokens
                };
            case CWMessageTemplateSystemNames.CustomerRegistrationImport:
```

#### 9. REPLACE — lines 337–343 (3 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`

FIND (lines 337–343, 7 lines):

```csharp
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.OrderTokens,
                    TokenGroupNames.CustomerTokens,
                    TokenGroupNames.ReturnRequestTokens
                };

            case CWMessageTemplateSystemNames.ReturnAuthorisedPaymentCustomer:
```

REPLACE WITH (8 lines):

```csharp
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.OrderTokens,
                    TokenGroupNames.CustomerTokens,
                    TokenGroupNames.ReturnRequestTokens,
                    CWTokenGroupNames.ReturnPackNameTokens
                };

            case CWMessageTemplateSystemNames.ReturnAuthorisedPaymentCustomer:
```

### IExchangeWorkflowMessageService.cs

`Plugins/Nop.Plugin.CW.Core/Services/Messages/IExchangeWorkflowMessageService.cs`

`SendPendingReturnRequestNotificationAsync` is removed: the Notify button now re-sends the template (change in `ExchangesLogic`). Doc comments completed.

#### 10. WHOLE FILE — replace the entire content of the file with this

`Plugins/Nop.Plugin.CW.Core/Services/Messages/IExchangeWorkflowMessageService.cs`

```csharp
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Services.Messages;

public interface IExchangeWorkflowMessageService
{
    /// <summary>
    /// Sends the custom Return Request Receipt message template
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="languageId">The language identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestReceivedNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, int languageId);

    /// <summary>
    /// Sends a notification when a Return is approved and no extra money is needed
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="paidOrder">The order placed for the replacement</param>
    /// <param name="orderItem">The returned order item</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestApprovedExceptedCustomerNotificationAsync(ReturnRequest returnRequest, Order paidOrder, OrderItem orderItem);

    /// <summary>
    /// Sends a notification when a Return Request is approved but extra money is needed
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="pendingAmount">What the customer is asked to pay</param>
    /// <param name="returnRequestItem">
    /// The return request item as the caller holds it. Passing it keeps the email's itemised fees on the
    /// same instance the workflow priced against, rather than whatever a second read returns.
    /// </param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendReturnRequestApprovedPaymentCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, decimal pendingAmount, ReturnRequestItem returnRequestItem = null);
}
```

### ExchangeWorkflowMessageService.cs

`Plugins/Nop.Plugin.CW.Core/Services/Messages/ExchangeWorkflowMessageService.cs`

The three senders add `%PackName.Name%`; the hard-coded "pending" e-mail and its `GetDefaultEmailAccountAsync` helper are removed (dead); the namespace becomes file-scoped (the whole file is re-indented, hence whole file).

#### 11. WHOLE FILE — replace the entire content of the file with this

`Plugins/Nop.Plugin.CW.Core/Services/Messages/ExchangeWorkflowMessageService.cs`

```csharp
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Messages;

public class ExchangeWorkflowMessageService : WorkflowMessageService, IExchangeWorkflowMessageService
{
    #region Fields

    protected readonly IMessageTokenProviderOverride _messageTokenProviderOverride;

    #endregion

    #region Ctor

    public ExchangeWorkflowMessageService(CommonSettings commonSettings,
        EmailAccountSettings emailAccountSettings,
        IAddressService addressService,
        IAffiliateService affiliateService,
        ICustomerService customerService,
        IEmailAccountService emailAccountService,
        IEventPublisher eventPublisher,
        ILanguageService languageService,
        ILocalizationService localizationService,
        IMessageTemplateService messageTemplateService,
        IMessageTokenProvider messageTokenProvider,
        IOrderService orderService,
        IProductService productService,
        IQueuedEmailService queuedEmailService,
        IStoreContext storeContext,
        IStoreService storeService,
        ITokenizer tokenizer,
        MessagesSettings messagesSettings,
        IMessageTokenProviderOverride messageTokenProviderOverride) : base(commonSettings,
            emailAccountSettings,
            addressService,
            affiliateService,
            customerService,
            emailAccountService,
            eventPublisher,
            languageService,
            localizationService,
            messageTemplateService,
            messageTokenProvider,
            orderService,
            productService,
            queuedEmailService,
            storeContext,
            storeService,
            tokenizer,
            messagesSettings)
    {
        _messageTokenProviderOverride = messageTokenProviderOverride;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Sends the custom Return Request Receipt message template
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="languageId">The language identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    public async Task<IList<int>> SendReturnRequestReceivedNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, int languageId)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);

        var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
        var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
        languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

        var messageTemplates = await GetActiveMessageTemplatesAsync(CWMessageTemplateSystemNames.ReturnReceipt, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        //tokens
        var commonTokens = new List<Token>();
        await _messageTokenProvider.AddOrderTokensAsync(commonTokens, order, languageId);
        await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
        await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, order, orderItem, languageId);
        await _messageTokenProviderOverride.AddReturnPackNameTokensAsync(commonTokens, orderItem);

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            //email account
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            //event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);

            var toEmail = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.Email
                : customer.Email;
            var toName = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.FirstName
                : await _customerService.GetCustomerFullNameAsync(customer);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

    /// <summary>
    /// Sends a notification when a Return is approved and no extra money is needed
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="paidOrder">The order placed for the replacement</param>
    /// <param name="orderItem">The returned order item</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    public async Task<IList<int>> SendReturnRequestApprovedExceptedCustomerNotificationAsync(ReturnRequest returnRequest, Order paidOrder,
        OrderItem orderItem)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);

        int languageId = paidOrder.CustomerLanguageId;
        var store = await _storeService.GetStoreByIdAsync(paidOrder.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
        languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

        var messageTemplates = await GetActiveMessageTemplatesAsync(CWMessageTemplateSystemNames.ReturnAuthorisedExceptedCustomer, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        //tokens
        var commonTokens = new List<Token>();
        await _messageTokenProvider.AddOrderTokensAsync(commonTokens, paidOrder, languageId);
        await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
        await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, await _orderService.GetOrderByIdAsync(orderItem.OrderId), orderItem, languageId);
        await _messageTokenProviderOverride.AddReturnPackNameTokensAsync(commonTokens, orderItem);

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            //email account
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            //event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            var billingAddress = await _addressService.GetAddressByIdAsync(paidOrder.BillingAddressId);

            var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);
            var toEmail = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.Email
                : customer.Email;
            var toName = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.FirstName
                : await _customerService.GetCustomerFullNameAsync(customer);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

    /// <summary>
    /// Sends a notification when a Return Request is approved but extra money is needed
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="pendingAmount">What the customer is asked to pay</param>
    /// <param name="returnRequestItem">The return request item as the caller holds it, or null to load it</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    public async Task<IList<int>> SendReturnRequestApprovedPaymentCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem,
        decimal pendingAmount, ReturnRequestItem returnRequestItem = null)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);

        var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
        var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
        int languageId = order.CustomerLanguageId;
        languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

        var messageTemplates = await GetActiveMessageTemplatesAsync(CWMessageTemplateSystemNames.ReturnAuthorisedPaymentCustomer, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        //tokens
        var commonTokens = new List<Token>();
        await _messageTokenProvider.AddOrderTokensAsync(commonTokens, order, languageId);
        await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
        await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, order, orderItem, languageId);
        await _messageTokenProviderOverride.AddReturnPackNameTokensAsync(commonTokens, orderItem);

        await _messageTokenProviderOverride.AddExchangeReturnTokensAsync(commonTokens, returnRequest, pendingAmount, returnRequestItem);

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            //email account
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            //event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);

            var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);
            var toEmail = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.Email
                : customer.Email;
            var toName = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.FirstName
                : await _customerService.GetCustomerFullNameAsync(customer);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

    #endregion
}
```

### ExchangeMessageService.cs

`Plugins/Nop.Plugin.CW.Core/Services/Messages/ExchangeMessageService.cs`

The return receipt shown on the confirmation screen and printed on the PDF renders `%PackName.Name%` too.

#### 12. REPLACE — lines 160–165 (1 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/ExchangeMessageService.cs`

FIND (lines 160–165, 6 lines):

```csharp
            await _messageTokenProvider.AddOrderTokensAsync(commonTokens, order, languageId);
            await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
            await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, order, orderItem, languageId);
            await _messageTokenProvider.AddStoreTokensAsync(commonTokens, store, emailAccount, languageId);

            // event notification
```

REPLACE WITH (7 lines):

```csharp
            await _messageTokenProvider.AddOrderTokensAsync(commonTokens, order, languageId);
            await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
            await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, order, orderItem, languageId);
            await _messageTokenProviderOverride.AddReturnPackNameTokensAsync(commonTokens, orderItem);
            await _messageTokenProvider.AddStoreTokensAsync(commonTokens, store, emailAccount, languageId);

            // event notification
```

#### 13. REPLACE — lines 59–64 (2 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/ExchangeMessageService.cs`

FIND (lines 59–64, 6 lines):

```csharp
            _messageTemplateService = messageTemplateService;
            _emailAccountService = emailAccountService;
            _emailAccountSettings = emailAccountSettings;
        }

        #endregion
```

REPLACE WITH (7 lines):

```csharp
            _messageTemplateService = messageTemplateService;
            _emailAccountService = emailAccountService;
            _emailAccountSettings = emailAccountSettings;
            _messageTokenProviderOverride = messageTokenProviderOverride;
        }

        #endregion
```

#### 14. REPLACE — lines 46–50 (3 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/ExchangeMessageService.cs`

FIND (lines 46–50, 5 lines):

```csharp
            IMessageTemplateService messageTemplateService,
            IEmailAccountService emailAccountService,
            EmailAccountSettings emailAccountSettings)
        {
            _messageTokenProvider = messageTokenProvider;
```

REPLACE WITH (6 lines):

```csharp
            IMessageTemplateService messageTemplateService,
            IEmailAccountService emailAccountService,
            EmailAccountSettings emailAccountSettings,
            IMessageTokenProviderOverride messageTokenProviderOverride)
        {
            _messageTokenProvider = messageTokenProvider;
```

#### 15. REPLACE — lines 30–33 (4 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/ExchangeMessageService.cs`

FIND (lines 30–33, 4 lines):

```csharp
        protected readonly IEmailAccountService _emailAccountService;
        protected readonly EmailAccountSettings _emailAccountSettings;

        #endregion
```

REPLACE WITH (5 lines):

```csharp
        protected readonly IEmailAccountService _emailAccountService;
        protected readonly EmailAccountSettings _emailAccountSettings;
        protected readonly IMessageTokenProviderOverride _messageTokenProviderOverride;

        #endregion
```

### CustomerSubsidyHelper.cs

`Plugins/Nop.Plugin.CW.Core/Helpers/CustomerSubsidyHelper.cs`

One line of `%Subsidy.Details%` (F7).

#### 16. REPLACE — lines 146–148 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Helpers/CustomerSubsidyHelper.cs`

FIND (lines 146–148, 3 lines):

```csharp
        return Math.Clamp(percent, 0, 100);
    }
}
```

REPLACE WITH (15 lines):

```csharp
        return Math.Clamp(percent, 0, 100);
    }

    /// <summary>
    /// One subsidy as the welcome e-mail lists it: its period, then its credit and its items where it has them,
    /// separated by commas.
    /// </summary>
    /// <param name="period">The subsidy's name and season</param>
    /// <param name="credit">The credit, or null or empty when the subsidy has none</param>
    /// <param name="units">The items, or null or empty when the subsidy allocates none</param>
    public static string DetailsLine(string period, string credit, string units)
    {
        return string.Join(", ", new[] { period, credit, units }.Where(part => !string.IsNullOrEmpty(part)));
    }
}
```

### PackNameWorkflowMessageService.cs

`Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`

`%Subsidy.Details%` in the welcome e-mail: each subsidy with its season, credit and items (F7).

#### 17. REPLACE — lines 105–108 (1 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`

FIND (lines 105–108, 4 lines):

```csharp
        commonTokens.Add(new Token("PackName.Branch", branchName ?? string.Empty));
        commonTokens.Add(new Token("Subsidy.SubsidyName", string.Join(", ", subsidyNames ?? new List<string>())));

        return await messageTemplates.SelectAwait(async messageTemplate =>
```

REPLACE WITH (5 lines):

```csharp
        commonTokens.Add(new Token("PackName.Branch", branchName ?? string.Empty));
        commonTokens.Add(new Token("Subsidy.SubsidyName", string.Join(", ", subsidyNames ?? new List<string>())));
        commonTokens.Add(new Token("Subsidy.Details", subsidyNames?.Count > 0 ? await PrepareSubsidyDetailsAsync(packName, languageId) : string.Empty, true));

        return await messageTemplates.SelectAwait(async messageTemplate =>
```

#### 18. REPLACE — lines 73–78 (2 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`

FIND (lines 73–78, 6 lines):

```csharp
            messagesSettings)
    {
        _genericAttributeService = genericAttributeService;
    }

    #endregion
```

REPLACE WITH (57 lines):

```csharp
            messagesSettings)
    {
        _genericAttributeService = genericAttributeService;
        _subsidyReportingService = subsidyReportingService;
        _dateTimeHelper = dateTimeHelper;
        _priceFormatter = priceFormatter;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The subsidies of a packname as the welcome e-mail lists them: a title, then one line per live assignment with its
    /// current season, its credit and its items; empty when the packname has none.
    /// </summary>
    /// <param name="packName">The packname</param>
    /// <param name="languageId">Message language identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the HTML, encoded</returns>
    protected virtual async Task<string> PrepareSubsidyDetailsAsync(PackName packName, int languageId)
    {
        var assignments = (await _subsidyReportingService.GetSubsidyDetailsByPackNameIdAsync(packName.Id, inOrderingPeriodOnly: false))
            .Where(assignment => assignment.Active && assignment.AssignmentActive)
            .ToList();

        if (!assignments.Any())
            return string.Empty;

        //the season is judged on the store's calendar day, the frame the periods are expressed in
        var today = SubsidyPeriodHelper.ToStoreLocal(DateTime.UtcNow, _dateTimeHelper.DefaultStoreTimeZone).Date;

        var periodFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Period", languageId);
        var creditFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Credit", languageId);
        var unitsFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Units", languageId);

        var lines = new List<string>();

        foreach (var assignment in assignments)
        {
            var (seasonStart, seasonEnd) = CustomerSubsidyHelper.SeasonFor(assignment.DurationStart, assignment.DurationEnd, assignment.AnnualRecurrence, today);

            var credit = assignment.Credit + assignment.TopupCredit;
            var productGroups = await _subsidyReportingService.GetSubsidyPackNameProductGroupDetailsAsync(assignment.Id, assignment.SubsidyId, activeOnly: true);
            var units = productGroups.Sum(productGroup => productGroup.Quantity + productGroup.TopupQuantity);

            lines.Add(CustomerSubsidyHelper.DetailsLine(
                string.Format(periodFormat, WebUtility.HtmlEncode(assignment.Subsidy), seasonStart.ToShortDateString(), seasonEnd.ToShortDateString()),
                credit > decimal.Zero ? string.Format(creditFormat, WebUtility.HtmlEncode(await _priceFormatter.FormatPriceAsync(credit))) : null,
                units > 0 ? string.Format(unitsFormat, units) : null));
        }

        var title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Title", languageId);

        return $"{WebUtility.HtmlEncode(title)}<br />{string.Join("<br />", lines)}<br />";
    }

    #endregion
```

#### 19. REPLACE — lines 54–58 (3 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`

FIND (lines 54–58, 5 lines):

```csharp
        ITokenizer tokenizer,
        MessagesSettings messagesSettings,
        IGenericAttributeService genericAttributeService) : base(commonSettings,
            emailAccountSettings,
            addressService,
```

REPLACE WITH (8 lines):

```csharp
        ITokenizer tokenizer,
        MessagesSettings messagesSettings,
        IGenericAttributeService genericAttributeService,
        ISubsidyReportingService subsidyReportingService,
        IDateTimeHelper dateTimeHelper,
        IPriceFormatter priceFormatter) : base(commonSettings,
            emailAccountSettings,
            addressService,
```

#### 20. REPLACE — lines 29–36 (4 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`

FIND (lines 29–36, 8 lines):

```csharp
{
    #region Fields

    protected readonly IGenericAttributeService _genericAttributeService;

    #endregion

    #region Ctor
```

REPLACE WITH (11 lines):

```csharp
{
    #region Fields

    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ISubsidyReportingService _subsidyReportingService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly IPriceFormatter _priceFormatter;

    #endregion

    #region Ctor
```

#### 21. REPLACE — lines 6–17 (5 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`

FIND (lines 6–17, 12 lines):

```csharp
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Messages;
```

REPLACE WITH (16 lines):

```csharp
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Stores;
using System.Net;

namespace Nop.Plugin.CW.Core.Services.Messages;
```

### EventConsumer.cs

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

The two new tokens in the admin token lists.

#### 22. REPLACE — lines 747–751 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

FIND (lines 747–751, 5 lines):

```csharp
            eventMessage.AdditionalTokens.Add("%PackName.Branch%");
            eventMessage.AdditionalTokens.Add("%Subsidy.SubsidyName%");
        }

        //Shipment-scoped delivery address, so a shipment email can name the branch that parcel went to
```

REPLACE WITH (12 lines):

```csharp
            eventMessage.AdditionalTokens.Add("%PackName.Branch%");
            eventMessage.AdditionalTokens.Add("%Subsidy.SubsidyName%");
            eventMessage.AdditionalTokens.Add("%Subsidy.Details%");
        }

        //The return e-mails and the return receipt: the packname the returned item was ordered for (listed once when
        //every group is asked for, as the welcome e-mail's group lists it too)
        if ((eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.ReturnPackNameTokens))
            && !eventMessage.AdditionalTokens.Contains("%PackName.Name%"))
            eventMessage.AdditionalTokens.Add("%PackName.Name%");

        //Shipment-scoped delivery address, so a shipment email can name the branch that parcel went to
```

### IExchangesLogic.cs

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/IExchangesLogic.cs`

The Notify-completion re-send.

#### 23. REPLACE — lines 53–56 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/IExchangesLogic.cs`

FIND (lines 53–56, 4 lines):

```csharp
    Task<ReturnRequestWorkflowResult> ExecuteApprovedLineAsync(ReturnRequest returnRequest, int newStatusId);

    Task UndoReturnRequestApprovalAsync(ReturnRequest returnRequest);
}
```

REPLACE WITH (12 lines):

```csharp
    Task<ReturnRequestWorkflowResult> ExecuteApprovedLineAsync(ReturnRequest returnRequest, int newStatusId);

    /// <summary>
    /// Sends the customer the "your approved return needs a payment" e-mail again, with what the cart asks for
    /// now; nothing is sent unless the return is waiting for the customer to check out.
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    Task<IList<int>> SendCompletionRequiredNotificationAsync(ReturnRequest returnRequest);

    Task UndoReturnRequestApprovalAsync(ReturnRequest returnRequest);
}
```

### ExchangesLogic.cs

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

Remember the queued approval e-mail, withdraw it on undo (OI 5.6), and the Notify-completion re-send.

#### 24. REPLACE — lines 915–922 (1 of 8 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

FIND (lines 915–922, 8 lines):

```csharp
    }

    #endregion

    #region Undo / cleanup

    public virtual async Task UndoReturnRequestApprovalAsync(ReturnRequest returnRequest)
    {
```

REPLACE WITH (35 lines):

```csharp
    }

    #endregion

    #region Notifications

    public virtual async Task<IList<int>> SendCompletionRequiredNotificationAsync(ReturnRequest returnRequest)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);

        var returnRequestItem = await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);
        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);

        if (returnRequestItem is not { NeedsCompletion: true, IsCompleted: false } || orderItem == null)
            return new List<int>();

        //priced as the approval priced it: the replacement at what the customer paid for the returned item
        using var replacementScope = ExchangeReplacementScope.Begin(returnRequestItem);

        var creditNotesBalance = await DetermineCurrentCreditNotesBalanceAsync(returnRequest.CustomerId);
        var (total, exchangeFeesAmount) = await CalculateExchangeTotalAmountAsync(returnRequest, returnRequestItem);
        var pendingAmount = await DeterminePendingAmountAsync(returnRequest, total, exchangeFeesAmount, creditNotesBalance);

        var queuedEmailIds = await _exchangeWorkflowMessageService.SendReturnRequestApprovedPaymentCustomerNotificationAsync(returnRequest, orderItem, pendingAmount, returnRequestItem);
        await RecordApprovalEmailAsync(returnRequestItem, queuedEmailIds);

        return queuedEmailIds;
    }

    #endregion

    #region Undo / cleanup

    public virtual async Task UndoReturnRequestApprovalAsync(ReturnRequest returnRequest)
    {
```

#### 25. REPLACE — lines 471–478 (2 of 8 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

FIND (lines 471–478, 8 lines):

```csharp
    }

    protected async Task CleanupFailedExchangeWorkflowAsync(ReturnRequest returnRequest, bool creditNoteWasApplied)
    {
        if (creditNoteWasApplied)
            await _creditNotesService.UndoCreditNoteForReturnRequestAsync(returnRequest.Id);

        await UndoApprovedExchangeItemsAsync(returnRequest);
```

REPLACE WITH (47 lines):

```csharp
    }

    /// <summary>
    /// Keeps the id of the approval e-mail queued for the line, so an undo can withdraw it before it is sent.
    /// </summary>
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

    protected async Task CleanupFailedExchangeWorkflowAsync(ReturnRequest returnRequest, bool creditNoteWasApplied)
    {
        if (creditNoteWasApplied)
            await _creditNotesService.UndoCreditNoteForReturnRequestAsync(returnRequest.Id);

        await WithdrawApprovalEmailAsync(returnRequest);

        await UndoApprovedExchangeItemsAsync(returnRequest);
```

#### 26. REPLACE — lines 384–388 (3 of 8 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

FIND (lines 384–388, 5 lines):

```csharp
                    // itemises the same instance this workflow priced against — the controller has already
                    // committed it, so this is about not re-reading the row rather than outrunning a commit.
                    await _exchangeWorkflowMessageService.SendReturnRequestApprovedPaymentCustomerNotificationAsync(returnRequest, orderItem, pendingAmount, returnRequestItem);

                    #endregion
```

REPLACE WITH (6 lines):

```csharp
                    // itemises the same instance this workflow priced against — the controller has already
                    // committed it, so this is about not re-reading the row rather than outrunning a commit.
                    var queuedEmailIds = await _exchangeWorkflowMessageService.SendReturnRequestApprovedPaymentCustomerNotificationAsync(returnRequest, orderItem, pendingAmount, returnRequestItem);
                    await RecordApprovalEmailAsync(returnRequestItem, queuedEmailIds);

                    #endregion
```

#### 27. REPLACE — lines 355–361 (4 of 8 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

FIND (lines 355–361, 7 lines):

```csharp
                    var paidOrder = placeOrderResult.PlacedOrder;

                    // Notify the customer about the approval
                    await _exchangeWorkflowMessageService.SendReturnRequestApprovedExceptedCustomerNotificationAsync(returnRequest, paidOrder, orderItem);

                    #endregion
                }
```

REPLACE WITH (8 lines):

```csharp
                    var paidOrder = placeOrderResult.PlacedOrder;

                    // Notify the customer about the approval
                    var queuedEmailIds = await _exchangeWorkflowMessageService.SendReturnRequestApprovedExceptedCustomerNotificationAsync(returnRequest, paidOrder, orderItem);
                    await RecordApprovalEmailAsync(returnRequestItem, queuedEmailIds);

                    #endregion
                }
```

#### 28. REPLACE — lines 91–96 (5 of 8 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

FIND (lines 91–96, 6 lines):

```csharp
        _subsidyCreditService = subsidyCreditService;
        _returnCaseService = returnCaseService;
        _locker = locker;
    }

    #endregion
```

REPLACE WITH (7 lines):

```csharp
        _subsidyCreditService = subsidyCreditService;
        _returnCaseService = returnCaseService;
        _locker = locker;
        _queuedEmailService = queuedEmailService;
    }

    #endregion
```

#### 29. REPLACE — lines 70–74 (6 of 8 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

FIND (lines 70–74, 5 lines):

```csharp
        ISubsidyCreditService subsidyCreditService,
        IReturnCaseService returnCaseService,
        ILocker locker)
    {
        _orderService = orderService;
```

REPLACE WITH (6 lines):

```csharp
        ISubsidyCreditService subsidyCreditService,
        IReturnCaseService returnCaseService,
        ILocker locker,
        IQueuedEmailService queuedEmailService)
    {
        _orderService = orderService;
```

#### 30. REPLACE — lines 46–49 (7 of 8 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

FIND (lines 46–49, 4 lines):

```csharp
    protected readonly IReturnCaseService _returnCaseService;
    protected readonly ILocker _locker;

    #endregion
```

REPLACE WITH (5 lines):

```csharp
    protected readonly IReturnCaseService _returnCaseService;
    protected readonly ILocker _locker;
    protected readonly IQueuedEmailService _queuedEmailService;

    #endregion
```

#### 31. REPLACE — lines 17–22 (8 of 8 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

FIND (lines 17–22, 6 lines):

```csharp
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.BusinessLogic;
```

REPLACE WITH (7 lines):

```csharp
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.BusinessLogic;
```

### ReturnsController.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ReturnsController.cs`

*Notify completion required* re-sends the approval template; the controller no longer needs `IExchangeWorkflowMessageService`.

#### 32. REPLACE — lines 692–698 (1 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ReturnsController.cs`

FIND (lines 692–698, 7 lines):

```csharp
            else
            {
                // Notify the customer that actions are required
                int emailId = await _exchangeWorkflowMessageService.SendPendingReturnRequestNotificationAsync(returnRequest);
                _notificationService.SuccessNotification($"A message with ID: {emailId} has been queued to notify the customer about the pending actions needed to complete Return Request with ID: {returnRequest.Id}");
            }
        }
```

REPLACE WITH (11 lines):

```csharp
            else
            {
                //the approval's own "payment needed" e-mail again, with what the cart asks for now
                var queuedEmailIds = await _exchangesLogic.SendCompletionRequiredNotificationAsync(returnRequest);

                if (queuedEmailIds.Any())
                    _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Returns.CompletionNotification.Queued"));
                else
                    _notificationService.WarningNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Returns.CompletionNotification.NotQueued"));
            }
        }
```

#### 33. REPLACE — lines 86–90 (2 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ReturnsController.cs`

FIND (lines 86–90, 5 lines):

```csharp
        _logger = logger;
        _returnCaseService = returnCaseService;
        _exchangeWorkflowMessageService = exchangeWorkflowMessageService;
        _returnResolutionLogic = returnResolutionLogic;
    }
```

REPLACE WITH (4 lines):

```csharp
        _logger = logger;
        _returnCaseService = returnCaseService;
        _returnResolutionLogic = returnResolutionLogic;
    }
```

#### 34. REPLACE — lines 65–69 (3 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ReturnsController.cs`

FIND (lines 65–69, 5 lines):

```csharp
        ILogger logger,
        IReturnCaseService returnCaseService,
        IExchangeWorkflowMessageService exchangeWorkflowMessageService,
        IReturnResolutionLogic returnResolutionLogic) : base(customerActivityService,
            localizationService,
```

REPLACE WITH (4 lines):

```csharp
        ILogger logger,
        IReturnCaseService returnCaseService,
        IReturnResolutionLogic returnResolutionLogic) : base(customerActivityService,
            localizationService,
```

#### 35. REPLACE — lines 38–44 (4 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ReturnsController.cs`

FIND (lines 38–44, 7 lines):

```csharp
    protected readonly IStoreContext _storeContext;
    protected readonly ILogger _logger;
    protected readonly IReturnCaseService _returnCaseService;
    protected readonly IExchangeWorkflowMessageService _exchangeWorkflowMessageService;
    protected readonly IReturnResolutionLogic _returnResolutionLogic;

    #endregion
```

REPLACE WITH (6 lines):

```csharp
    protected readonly IStoreContext _storeContext;
    protected readonly ILogger _logger;
    protected readonly IReturnCaseService _returnCaseService;
    protected readonly IReturnResolutionLogic _returnResolutionLogic;

    #endregion
```

#### 36. REPLACE — lines 8–12 (5 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ReturnsController.cs`

FIND (lines 8–12, 5 lines):

```csharp
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Services.Catalog;
```

REPLACE WITH (4 lines):

```csharp
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Services.Catalog;
```

### CWInvoiceTotals.cs

`Plugins/Nop.Plugin.CW.Core/Services/Common/Pdf/CWInvoiceTotals.cs`

Which layout the totals take (OI 5.5).

#### 37. REPLACE — lines 36–38 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Common/Pdf/CWInvoiceTotals.cs`

FIND (lines 36–38, 3 lines):

```csharp
    [DisplayName("Plugins.CW.Core.Pdf.PayableBalance")]
    public string PayableBalance { get; set; }
}
```

REPLACE WITH (9 lines):

```csharp
    [DisplayName("Plugins.CW.Core.Pdf.PayableBalance")]
    public string PayableBalance { get; set; }

    /// <summary>
    /// Whether the order is a packname order, whose totals read in the order the money moved; a plain order keeps
    /// nop's rows, with its store credit and exchange fees after them.
    /// </summary>
    public bool IsPackNameOrder { get; set; }
}
```

### CWInvoiceDocument.cs

`Plugins/Nop.Plugin.CW.Core/Services/Common/Pdf/CWInvoiceDocument.cs`

The plain-order totals block: nop's rows, then store credit and exchange fees, then nop's order total (OI 5.5).

#### 38. REPLACE — lines 129–134 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Common/Pdf/CWInvoiceDocument.cs`

FIND (lines 129–134, 6 lines):

```csharp
        return summaryData;
    }

    #endregion

    #region Properties
```

REPLACE WITH (43 lines):

```csharp
        return summaryData;
    }

    /// <summary>
    /// Build the totals block of a plain (non-packname) order: nop's rows in nop's order, then the store credit that
    /// settled nop's total and the exchange fees, then nop's order total - the layout of the order e-mail.
    /// </summary>
    protected virtual PdfGrid CreatePlainOrderSummary(CWInvoiceTotals totals)
    {
        var summaryData = PdfDocumentHelper.BuildPdfGrid(numColumns: 1, DocumentRunDirection);

        if (!string.IsNullOrEmpty(totals.SubTotal))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.SubTotal, totals.SubTotal));
        if (!string.IsNullOrEmpty(totals.Discount))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.Discount, totals.Discount));
        if (!string.IsNullOrEmpty(totals.Shipping))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.Shipping, totals.Shipping));
        if (!string.IsNullOrEmpty(totals.PaymentMethodAdditionalFee))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.PaymentMethodAdditionalFee, totals.PaymentMethodAdditionalFee));
        if (!string.IsNullOrEmpty(totals.Tax))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.Tax, totals.Tax));

        foreach (var rate in totals.TaxRates)
            summaryData.AddCell(BuildPdfPCell(rate));

        foreach (var card in totals.GiftCards)
            summaryData.AddCell(BuildPdfPCell(card));

        if (!string.IsNullOrEmpty(totals.RewardPoints))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.RewardPoints, totals.RewardPoints));
        if (!string.IsNullOrEmpty(totals.CreditNotes))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.CreditNotes, totals.CreditNotes));
        if (!string.IsNullOrEmpty(totals.ExchangeFees))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.ExchangeFees, totals.ExchangeFees));
        if (!string.IsNullOrEmpty(totals.OrderTotal))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.OrderTotal, totals.OrderTotal));

        return summaryData;
    }

    #endregion

    #region Properties
```

#### 39. REPLACE — lines 89–94 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Common/Pdf/CWInvoiceDocument.cs`

FIND (lines 89–94, 6 lines):

```csharp
        if (Totals is not CWInvoiceTotals totals)
            return base.CreateSummary();

        var summaryData = PdfDocumentHelper.BuildPdfGrid(numColumns: 1, DocumentRunDirection);

        if (!string.IsNullOrEmpty(totals.SubTotal))
```

REPLACE WITH (9 lines):

```csharp
        if (Totals is not CWInvoiceTotals totals)
            return base.CreateSummary();

        if (!totals.IsPackNameOrder)
            return CreatePlainOrderSummary(totals);

        var summaryData = PdfDocumentHelper.BuildPdfGrid(numColumns: 1, DocumentRunDirection);

        if (!string.IsNullOrEmpty(totals.SubTotal))
```

### PdfOverrideService.cs

`Plugins/Nop.Plugin.CW.Core/Services/Common/PdfOverrideService.cs`

A plain order with store credit or exchange fees is printed by CW's document; exchange fees print even when nothing was settled (OI 5.5).

#### 40. REPLACE — lines 445–451 (1 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Common/PdfOverrideService.cs`

FIND (lines 445–451, 7 lines):

```csharp
            //--- the two lines that differ from nop's implementation ---
            //ShippingAddress stays the first destination so that CWInvoiceDocument can defer to nop's
            //own single-address layout whenever there is only one to show.
            ShippingAddress = shippingDestinations[0],
            ShippingDestinations = shippingDestinations,

            Products = await GetOrderProductItemsAsync(order, orderItems, language),
```

REPLACE WITH (7 lines):

```csharp
            //--- the two lines that differ from nop's implementation ---
            //ShippingAddress stays the first destination so that CWInvoiceDocument can defer to nop's
            //own single-address layout whenever there is only one to show.
            ShippingAddress = shippingDestinations.Count > 0 ? shippingDestinations[0] : await GetShippingAddressAsync(language, order),
            ShippingDestinations = shippingDestinations,

            Products = await GetOrderProductItemsAsync(order, orderItems, language),
```

#### 41. REPLACE — lines 404–417 (2 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Common/PdfOverrideService.cs`

FIND (lines 404–417, 14 lines):

```csharp
            .ToList();

        //Nothing being printed belongs to any destination. Rather than guess an address, hand the whole
        //document back to nop.
        if (printedDestinations.Count == 0)
        {
            await base.PrintOrderToPdfAsync(stream, order, language, store, vendor);

            return;
        }

        var shippingDestinations = await BuildShippingDestinationAddressesAsync(order, printedDestinations, language);

        var column1Lines = string.IsNullOrEmpty(pdfSettingsByStore.InvoiceFooterTextColumn1) ?
```

REPLACE WITH (18 lines):

```csharp
            .ToList();

        //Nothing being printed belongs to any destination, and no CW totals to print. Rather than guess an
        //address, hand the whole document back to nop.
        if (printedDestinations.Count == 0 && !printsCWTotals)
        {
            await base.PrintOrderToPdfAsync(stream, order, language, store, vendor);

            return;
        }

        //a plain order has no destinations: its own shipping address is nop's, and CWInvoiceDocument then lays
        //the addresses out as nop does
        var shippingDestinations = printedDestinations.Count > 0
            ? await BuildShippingDestinationAddressesAsync(order, printedDestinations, language)
            : new List<AddressItem>();

        var column1Lines = string.IsNullOrEmpty(pdfSettingsByStore.InvoiceFooterTextColumn1) ?
```

#### 42. REPLACE — lines 350–357 (3 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Common/PdfOverrideService.cs`

FIND (lines 350–357, 8 lines):

```csharp
        var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(order.Id);

        //Not a packname order — hand it back to nop untouched. This is the regression guard for every
        //ordinary order in the shop, and it is why the body below is only ever reached by a packname
        //one. GetOrderShippingDestinationsAsync is cached per order, so the guard costs almost nothing.
        if (destinations.Count == 0)
        {
            await base.PrintOrderToPdfAsync(stream, order, language, store, vendor);
```

REPLACE WITH (12 lines):

```csharp
        var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(order.Id);

        //nop's own document cannot print CW's totals rows (store credit, subsidy, exchange fees); a vendor sees
        //no totals at all
        var printsCWTotals = vendor is null && await HasCWTotalsAsync(order);

        //Not a packname order and nothing of CW's to total — hand it back to nop untouched. This is the
        //regression guard for every ordinary order in the shop. GetOrderShippingDestinationsAsync and the
        //settlement are cached per order, so the guard costs little.
        if (destinations.Count == 0 && !printsCWTotals)
        {
            await base.PrintOrderToPdfAsync(stream, order, language, store, vendor);
```

#### 43. REPLACE — lines 326–345 (4 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Common/PdfOverrideService.cs`

FIND (lines 326–345, 20 lines):

```csharp
            RewardPoints = totals.RewardPoints,
            OrderTotal = totals.OrderTotal,
            GrossTotal = await formatAsync(settlement.SettleableAmount),
            SubsidyForStock = settlement.SubsidyStockAmount > decimal.Zero ? await formatAsync(settlement.SubsidyStockAmount, true) : null,
            SubsidyForShipping = settlement.SubsidyShippingAmount > decimal.Zero ? await formatAsync(settlement.SubsidyShippingAmount, true) : null,
            CreditNotes = settlement.CreditNoteAmount > decimal.Zero ? await formatAsync(settlement.CreditNoteAmount, true) : null,
            ExchangeFees = exchangeFees > decimal.Zero ? await formatAsync(exchangeFees) : null,
            PayableBalance = totals.OrderTotal,
        };
    }

    /// <summary>
    /// Print an invoice naming every delivery address on the order, with the packnames sharing each.
    /// </summary>
    /// <remarks>
    /// nop's body is reproduced because <c>PrintOrderToPdfAsync</c> constructs its
    /// <c>InvoiceDocument</c> inline and offers no factory hook. Two things differ from it: the document
    /// type, and the two shipping properties on the initialiser. Everything else is nop's, line for line
    /// — the same accepted trade as <c>ShipmentOverrideService.GetAllShipmentsAsync</c>.
    /// </remarks>
```

REPLACE WITH (37 lines):

```csharp
            RewardPoints = totals.RewardPoints,
            OrderTotal = totals.OrderTotal,
            GrossTotal = settlement != null ? await formatAsync(settlement.SettleableAmount) : null,
            SubsidyForStock = settlement?.SubsidyStockAmount > decimal.Zero ? await formatAsync(settlement.SubsidyStockAmount, true) : null,
            SubsidyForShipping = settlement?.SubsidyShippingAmount > decimal.Zero ? await formatAsync(settlement.SubsidyShippingAmount, true) : null,
            CreditNotes = settlement?.CreditNoteAmount > decimal.Zero ? await formatAsync(settlement.CreditNoteAmount, true) : null,
            ExchangeFees = exchangeFees > decimal.Zero ? await formatAsync(exchangeFees) : null,
            PayableBalance = totals.OrderTotal,
            IsPackNameOrder = await _cwOrderService.IsPackNameOrderAsync(order.Id)
        };
    }

    /// <summary>
    /// Whether the order's invoice has rows of CW's to print: a settlement (subsidy or store credit paid for part of
    /// it) or exchange fees.
    /// </summary>
    /// <param name="order">The order</param>
    /// <returns>A task that represents the asynchronous operation; the task result is true when it has</returns>
    protected virtual async Task<bool> HasCWTotalsAsync(Order order)
    {
        if (await _cwOrderService.GetOrderSettlementAsync(order.Id) != null)
            return true;

        return (await _exchangeService.GetExchangeFeesAmountsAsync(order)).Values.Sum() > decimal.Zero;
    }

    /// <summary>
    /// Print an invoice naming every delivery address on the order, with the packnames sharing each, and the
    /// settlement and exchange-fee rows of any order that has them.
    /// </summary>
    /// <remarks>
    /// nop's body is reproduced because <c>PrintOrderToPdfAsync</c> constructs its
    /// <c>InvoiceDocument</c> inline and offers no factory hook. Two things differ from it: the document
    /// type, and the two shipping properties on the initialiser (a plain order keeps nop's own shipping address).
    /// Everything else is nop's, line for line
    /// — the same accepted trade as <c>ShipmentOverrideService.GetAllShipmentsAsync</c>.
    /// </remarks>
```

#### 44. REPLACE — lines 298–303 (5 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Common/PdfOverrideService.cs`

FIND (lines 298–303, 6 lines):

```csharp
            return await _priceFormatter.FormatPriceAsync(negative ? -inCustomerCurrency : inCustomerCurrency, true, order.CustomerCurrencyCode, false, languageId);
        }

        var exchangeFees = (await _exchangeService.GetExchangeFeesAmountsAsync(order)).Values.Sum();

        //nop writes the subtotal discount and the order discount into the one Discount field, the second over the
```

REPLACE WITH (4 lines):

```csharp
            return await _priceFormatter.FormatPriceAsync(negative ? -inCustomerCurrency : inCustomerCurrency, true, order.CustomerCurrencyCode, false, languageId);
        }

        //nop writes the subtotal discount and the order discount into the one Discount field, the second over the
```

#### 45. REPLACE — lines 285–292 (6 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Common/PdfOverrideService.cs`

FIND (lines 285–292, 8 lines):

```csharp
        var totals = await base.GetTotalsAsync(lang, order);

        var settlement = await _cwOrderService.GetOrderSettlementAsync(order.Id);

        if (settlement == null)
            return totals;

        var languageId = lang.Id;
```

REPLACE WITH (9 lines):

```csharp
        var totals = await base.GetTotalsAsync(lang, order);

        var settlement = await _cwOrderService.GetOrderSettlementAsync(order.Id);
        var exchangeFees = (await _exchangeService.GetExchangeFeesAmountsAsync(order)).Values.Sum();

        if (settlement == null && exchangeFees <= decimal.Zero)
            return totals;

        var languageId = lang.Id;
```

### CWCorePlugin.cs

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

Install the two approval templates (D1), the packname line on the receipt (D7), `%Subsidy.Details%` in the welcome e-mail (F7), and the new resources.

#### 46. REPLACE — lines 1600–1603 (1 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 1600–1603, 4 lines):

```csharp
            ["Plugins.CW.Core.Messages.Order.OrderDiscount"] = "Order discount:",
            ["Plugins.CW.Core.Messages.Order.PayableBalance"] = "Payable balance:",

            // Exchanges — customer-facing return wizard
```

REPLACE WITH (10 lines):

```csharp
            ["Plugins.CW.Core.Messages.Order.OrderDiscount"] = "Order discount:",
            ["Plugins.CW.Core.Messages.Order.PayableBalance"] = "Payable balance:",
            ["Plugins.CW.Core.Messages.Subsidy.Details.Title"] = "Your subsidies:",
            ["Plugins.CW.Core.Messages.Subsidy.Details.Period"] = "{0}: {1} to {2}",
            ["Plugins.CW.Core.Messages.Subsidy.Details.Credit"] = "{0} credit",
            ["Plugins.CW.Core.Messages.Subsidy.Details.Units"] = "{0} items",
            ["Plugins.CW.Core.Admin.Returns.CompletionNotification.Queued"] = "The customer has been sent the e-mail asking them to complete the checkout.",
            ["Plugins.CW.Core.Admin.Returns.CompletionNotification.NotQueued"] = "No e-mail was queued: the return is not waiting for the customer to check out, or the 'ReturnAuthorisedPayment.CustomerNotification' message template is not active for this store.",

            // Exchanges — customer-facing return wizard
```

#### 47. REPLACE — lines 192–200 (2 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 192–200, 9 lines):

```csharp
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(
            $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.CustomerRegistrationImport}",
            "Sent to a customer a manager registers for a packname: the packname details, the subsidies assigned and a link to set the password.");
    }

    /// <summary>
    /// Points the three shipment emails at the shipment's own delivery address instead of the order's.
```

REPLACE WITH (9 lines):

```csharp
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(
            $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.CustomerRegistrationImport}",
            "Sent to a customer a manager registers for a packname: the packname details, the subsidies assigned (each with its period, credit and items) and a link to set the password.");
    }

    /// <summary>
    /// Points the three shipment emails at the shipment's own delivery address instead of the order's.
```

#### 48. REPLACE — lines 185–189 (3 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 185–189, 5 lines):

```csharp
                   "%if (%PackName.EmployeeNumber% != \"\") Employee number: %PackName.EmployeeNumber%<br /> endif%" +
                   "Branch: %PackName.Branch%<br />" +
                   "%if (%Subsidy.SubsidyName% != \"\") Subsidy: %Subsidy.SubsidyName%<br /> endif%" +
                   "<br />To set your password and sign in, <a href=\"%Customer.PasswordRecoveryURL%\">click here</a>.<br /><br />" +
                   "If anything here is wrong, please let your manager know.<br />",
```

REPLACE WITH (5 lines):

```csharp
                   "%if (%PackName.EmployeeNumber% != \"\") Employee number: %PackName.EmployeeNumber%<br /> endif%" +
                   "Branch: %PackName.Branch%<br />" +
                   "%Subsidy.Details%" +
                   "<br />To set your password and sign in, <a href=\"%Customer.PasswordRecoveryURL%\">click here</a>.<br /><br />" +
                   "If anything here is wrong, please let your manager know.<br />",
```

#### 49. REPLACE — lines 159–164 (4 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 159–164, 6 lines):

```csharp
        await _localizationService.AddOrUpdateLocaleResourceAsync(
            $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.ReturnReceipt}",
            "Shown to the customer on the return confirmation screen and printed on the PDF return receipt.");
    }

    /// <summary>
```

REPLACE WITH (114 lines):

```csharp
        await _localizationService.AddOrUpdateLocaleResourceAsync(
            $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.ReturnReceipt}",
            "Shown to the customer on the return confirmation screen and printed on the PDF return receipt.");
    }

    /// <summary>
    /// Installs the two e-mails sent when a return is authorised, once each: a store that has edited one keeps its
    /// wording.
    /// </summary>
    protected async Task InstallReturnApprovalTemplatesAsync()
    {
        var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
            ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

        if (!(await _messageTemplateService.GetMessageTemplatesByNameAsync(CWMessageTemplateSystemNames.ReturnAuthorisedPaymentCustomer)).Any())
        {
            await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
            {
                Name = CWMessageTemplateSystemNames.ReturnAuthorisedPaymentCustomer,
                Subject = "Your return #%ReturnRequest.CustomNumber% is approved - please complete your order",
                Body = "Hello %Customer.FullName%,<br /><br />" +
                       "We have approved your return #%ReturnRequest.CustomNumber% (%ReturnRequest.Product.Quantity% x %ReturnRequest.Product.Name%" +
                       "%if (%PackName.Name% != \"\") for %PackName.Name% endif%).<br /><br />" +
                       "Your replacement is waiting in your shopping cart. To receive it, please sign in to " +
                       "<a href=\"%Store.URL%\">%Store.Name%</a> and complete the checkout.<br /><br />" +
                       "Amount to pay: <strong>%ReturnRequest.PendingAmount%</strong><br />" +
                       "This includes the redelivery fee (%ReturnRequest.ReShippingFee%) and the repackaging fee (%ReturnRequest.RePackagingFee%) " +
                       "where they apply.<br /><br />" +
                       "Thank you,<br />%Store.Name%<br />",
                IsActive = true,
                EmailAccountId = emailAccount?.Id ?? 0
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(
                $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.ReturnAuthorisedPaymentCustomer}",
                "Sent to the customer when a return is authorised and the replacement is put in their cart: they complete the checkout to pay what is still due (fees, a price difference). Also sent again by the return page's Notify completion required button.");
        }

        if (!(await _messageTemplateService.GetMessageTemplatesByNameAsync(CWMessageTemplateSystemNames.ReturnAuthorisedExceptedCustomer)).Any())
        {
            await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
            {
                Name = CWMessageTemplateSystemNames.ReturnAuthorisedExceptedCustomer,
                Subject = "Your return #%ReturnRequest.CustomNumber% is approved",
                Body = "Hello %Customer.FullName%,<br /><br />" +
                       "We have approved your return #%ReturnRequest.CustomNumber% (%ReturnRequest.Product.Quantity% x %ReturnRequest.Product.Name%" +
                       "%if (%PackName.Name% != \"\") for %PackName.Name% endif%).<br /><br />" +
                       "There is nothing to pay: we have placed order #%Order.OrderNumber% for your replacement, and we will let you know " +
                       "when it is on its way.<br /><br />" +
                       "Thank you,<br />%Store.Name%<br />",
                IsActive = true,
                EmailAccountId = emailAccount?.Id ?? 0
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(
                $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.ReturnAuthorisedExceptedCustomer}",
                "Sent to the customer when a return is authorised and nothing is due: the order for the replacement has been placed for them.");
        }
    }

    /// <summary>
    /// Adds the packname line under "RETURN REQUEST BY" in a return receipt installed before the line was in the
    /// default body.
    /// </summary>
    /// <remarks>
    /// Targeted, so a store's own wording survives, and idempotent: a body that already uses the packname token, or
    /// whose anchor line has been reworded, is left alone.
    /// </remarks>
    protected async Task AddPackNameToReturnReceiptTemplateAsync()
    {
        const string anchor = "RETURN REQUEST BY: %Customer.FullName%<br />";
        const string packNameLine = "%if (%PackName.Name% != \"\") FOR: %PackName.Name%<br /> endif%";

        foreach (var template in await _messageTemplateService.GetMessageTemplatesByNameAsync(CWMessageTemplateSystemNames.ReturnReceipt))
        {
            if (string.IsNullOrEmpty(template.Body) || template.Body.Contains("%PackName.Name%", StringComparison.OrdinalIgnoreCase))
                continue;

            var position = template.Body.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);

            if (position < 0)
                continue;

            template.Body = template.Body.Insert(position + anchor.Length, packNameLine);

            await _messageTemplateService.UpdateMessageTemplateAsync(template);
        }
    }

    /// <summary>
    /// Replaces the subsidy-name line of a welcome e-mail installed before <c>%Subsidy.Details%</c> existed with the
    /// details line: each subsidy with its period, credit and items.
    /// </summary>
    /// <remarks>
    /// Only the exact line the earlier default body carried is replaced, so a store's own wording survives; a re-run
    /// finds nothing to replace.
    /// </remarks>
    protected async Task PointCustomerRegistrationTemplateAtSubsidyDetailsAsync()
    {
        const string oldLine = "%if (%Subsidy.SubsidyName% != \"\") Subsidy: %Subsidy.SubsidyName%<br /> endif%";
        const string newLine = "%Subsidy.Details%";

        foreach (var template in await _messageTemplateService.GetMessageTemplatesByNameAsync(CWMessageTemplateSystemNames.CustomerRegistrationImport))
        {
            if (string.IsNullOrEmpty(template.Body) || !template.Body.Contains(oldLine, StringComparison.Ordinal))
                continue;

            template.Body = template.Body.Replace(oldLine, newLine, StringComparison.Ordinal);

            await _messageTemplateService.UpdateMessageTemplateAsync(template);
        }
    }

    /// <summary>
```

#### 50. REPLACE — lines 151–154 (5 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 151–154, 4 lines):

```csharp
                   "Attn: %Store.Name% RETURNS<br />" +
                   "RETURN REQUEST BY: %Customer.FullName%<br />" +
                   "REF: Order #%Order.OrderNumber%<br />" +
                   "%Store.CompanyAddress%<br />",
```

REPLACE WITH (5 lines):

```csharp
                   "Attn: %Store.Name% RETURNS<br />" +
                   "RETURN REQUEST BY: %Customer.FullName%<br />" +
                   "%if (%PackName.Name% != \"\") FOR: %PackName.Name%<br /> endif%" +
                   "REF: Order #%Order.OrderNumber%<br />" +
                   "%Store.CompanyAddress%<br />",
```

#### 51. REPLACE — lines 89–93 (6 of 6 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 89–93, 5 lines):

```csharp
        await InstallActivityLogTypesAsync();
        await InstallReturnReceiptTemplateAsync();
        await InstallCustomerRegistrationTemplateAsync();
        await PointShipmentTemplatesAtShipmentAddressAsync();
        await PointOrderTemplatesAtShippingDestinationsAsync();
```

REPLACE WITH (8 lines):

```csharp
        await InstallActivityLogTypesAsync();
        await InstallReturnReceiptTemplateAsync();
        await InstallReturnApprovalTemplatesAsync();
        await InstallCustomerRegistrationTemplateAsync();
        await AddPackNameToReturnReceiptTemplateAsync();
        await PointCustomerRegistrationTemplateAtSubsidyDetailsAsync();
        await PointShipmentTemplatesAtShipmentAddressAsync();
        await PointOrderTemplatesAtShippingDestinationsAsync();
```

### plugin.json

`Plugins/Nop.Plugin.CW.Core/plugin.json`

DATA version bump, so the update installs the templates and resources and runs the migration.

#### 52. REPLACE — lines 3–7 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/plugin.json`

FIND (lines 3–7, 5 lines):

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_10_31_DATA_0.2",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```

REPLACE WITH (5 lines):

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_06_DATA_0.3",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```


## Tests (already in `Tests/CW.Tests`, added by Claude)

| File | What it pins |
|---|---|
| `Core/BusinessLogic/ReturnApprovalEmailTests.cs` (new, 5 cases) | the queued approval e-mail is remembered; nothing is remembered when none was queued; an undo deletes an unsent one and clears the id; an undo keeps a sent one; an undo with no e-mail touches nothing |
| `Core/Helpers/CustomerSubsidyHelperTests.cs` (+2 cases) | a `%Subsidy.Details%` line lists period, credit and items, and leaves out what the subsidy does not give |
| `Core/TestObject.cs` | a non-generic `InvokeAsync` for protected methods that return `Task` |

CW.Core cases: 634 → 641.

## Build and checks

1. Build the solution (Razor views: none changed in this phase).
2. `dotnet test Tests/CW.Tests --filter "FullyQualifiedName~CW.Tests.Core"` — 641 pass. `NopStartupTests` covers the
   new constructor parameters of `ExchangesLogic`, `ExchangeMessageService`, `PackNameWorkflowMessageService` and
   `ReturnsController` (registration and cycles).
3. Start the site: the plugin version changes (`2026_11_06_DATA_0.3`), so nop runs the update migration and
   `UpdateAsync` (resources, templates, template edits).
4. Independent review of this spec: done before it was handed over (see the end of the document).

## Test plan (dev site `https://localhost:59579`, database `testnop50`)

**Side effects:** e-mails are queued to test customers (no e-mail account sends them unless one is configured), return
requests change status, a test packname with an account is added, two orders are placed. Nothing is deleted except the
queued e-mails T3 is about. For T3, pause nop's *Send emails* task (*System › Schedule tasks*, untick Enabled) and
re-enable it after T4.

| # | Steps | Expected |
|---|---|---|
| T1 | After the update: *Content management › Message templates*; then `SELECT ApprovalQueuedEmailId FROM CORE_EX_ReturnRequestItem` | `ReturnAuthorisedPayment.CustomerNotification` and `ReturnAuthorisedExcepted.CustomerNotification` exist, active, with descriptions; `Return.Receipt` has the `FOR: %PackName.Name%` line under `RETURN REQUEST BY`; `Customer.Subsidy.CustomerRegistrationImport` has `%Subsidy.Details%` where the subsidy-name line was; the column exists (all NULL) |
| T2 | Token lists: open each of the three return templates and the welcome template | `%PackName.Name%` is listed on the three return templates; `%Subsidy.Details%` on the welcome template |
| T3 | Packname store, *Send emails* paused: a customer submits an exchange with a fee; approve it (pathway B: replacement in cart) | *System › Queued emails* has the payment e-mail: return number, item, `for <packname>`, amount to pay; the line's `ApprovalQueuedEmailId` is that e-mail's id. Then **Undo approval**: the e-mail is gone from the queue, the id is NULL |
| T4 | Approve the same return again, re-enable *Send emails*, wait until it shows as sent; **Undo approval** | The sent e-mail stays in the list; the id is NULL |
| T5 | Approve again (pathway B), then **Notify completion required** | Success message; a new payment e-mail with the amount the cart shows now. On a return not waiting for checkout (e.g. pathway A, or pending) the button is not offered; posting it anyway shows the warning |
| T6 | An exchange with no fee that settlement covers (pathway A) | The "approved, nothing to pay" e-mail with the new order number |
| T7 | Return confirmation screen and the PDF return receipt for a packname item; then for an item ordered without a packname (plain store) | `FOR: <packname>` under `RETURN REQUEST BY`; no FOR line for the plain item |
| T8 | As a manager, add a packname with an account and two subsidies (one with credit, one with product-group items; one named with an `&`) | Welcome e-mail lists "Your subsidies:" then one line each: name, season, `$x credit` and/or `n items`; the `&` is shown correctly |
| T9 | Plain store (PackName off): place an order paid partly with a credit note; print its invoice | Totals: nop's rows, then **Store credit −$x**, then **Order total** = what was paid; same order as the order e-mail |
| T10 | Plain store: complete a pathway-B exchange through the cart (fees due); print the order's invoice | **Exchange fees** row before the order total |
| T11 | Regression: a packname order with subsidy (invoice) and a plain order with no credit or fees (invoice) | Packname invoice unchanged (money-moved layout, destinations); plain invoice is nop's own, unchanged |
| T12 | Exchanges feature off on a store: open a return | No approval workflow or Notify button reachable (unchanged); nothing new appears |

## After the phase

On approval: remove OI 2.1.1, 2.1.6, 2.1.7, 5.5 and the queued-e-mail part of 5.6 from
`Subsidy_PackName_Open_Items.md`; mark D1, D7, F7 done in the gap list; update `CLAUDE.md` §10.3 (the
`PdfOverrideService.PrintOrderToPdfAsync` row: plain orders with CW totals now go through the copy) and
`Exchanges_Returns_CreditNotes_Reference.md` (approval e-mails, Notify button). This document then moves to
`CWPlans/Deprecated/`.
