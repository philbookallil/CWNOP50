# Round 1, phase B — admin return page, deleting reasons and actions, the replacement picker

**Date:** 2026-11-09. **Owner:** Eduardo. **Status:** spec — ready to apply.

**What this phase does.** The second half of Round 1 of the 4.1 gap list (`LegacyVersions/NOP41_To_500_Gap_Analysis.md`),
after phase R1-A (`Deprecated/R1A_Return_Emails_Invoice_Spec.md`): the admin return page, the return reasons and actions
in nop's settings, and the storefront replacement picker. It also takes the one finding of R1-A's site tests.

| Item | What changes |
|---|---|
| **D5 / OI 2.2.6** | On the admin return page, the *Requested action* dropdown lists the actions the return's store offers for the chosen *Reason for return* (the storefront wizard's rule), and reloads when the reason changes. The line's saved pair stays selectable, so an old return keeps saving after the rules change. A store with no enabled action rule at all offers every action. The save refuses an action the reason does not offer. A refused save now redisplays the reason and action staff posted. A plain nop return (no exchange line: posted with Exchanges off, or through a guest's withdrawal link) keeps the old dropdowns, since it stores neither choice. |
| **D6 / OI 2.1.12** (backlog B7, the clean-up half) | Deleting a return reason or action in nop's settings is refused while any return (line or case, any status) records it; staff see a message and can rename it instead. A reason or action no return uses is deleted as before, and its per-store CW rules and reason-action links are deleted with it. |
| **D4 / OI 2.1.5** | The admin return list override is removed: its first / last name branch was dead (no screen posts the filter) and the rest did what nop's list does. The list is nop's own; `CWReturnRequestService` and `CWReturnRequestSearchModel` are deleted. |
| **OI 5.4** | The replacement picker no longer offers a grouped product's parent (no price or options of its own); the options step and the submission refuse one posted by hand. |
| **OI 5.3** (check) | **Already closed by the port, no code.** The submission checks the posted replacement (`ReturnSubmissionLogic.IsReplacementValidAsync`: type, options, published, visible, ACL / store / packname catalogue), and `IsExchangeReplacementAsync` reads only the customer's own cart lines (`GetCartReturnRequestItemsAsync` joins the cart to `CustomerId`). The Open Items row was stale. |
| **OI 5.6** (part) | The unused `ProductsHelper.IsColourAttribute(ProductAttributeMapping, ProductAttribute)` is removed. |
| **R1-A finding** | The "payment needed" approval e-mail names the additional fee when there is one ("It also includes Damage fee ($5.00)."), through a new bool token `%ReturnRequest.HasAdditionalFee%`. An existing template gets the sentence once, in place of the blank line after "where they apply.", only when that wording is unchanged. |

**Decisions (2026-11-05 and 2026-11-09).** Spec applied by Eduardo (no direct code edits). D6: refuse the delete while a
return uses the record, otherwise clean up; *any* return counts, whatever its status. D4: delete the dead code — the 4.1
first / last name filters are not ported. OI 5.4: hide grouped parents (their variants stay unreachable as replacements).

**What it deliberately does not do.**
- The *Reason for return* dropdown still lists every reason (staff may need to reclassify a return with one the store
  no longer offers); only the actions follow the reason.
- No name filter on the admin return list (nop's list filters by number, status and dates; the Customer column shows
  the e-mail).
- A grouped product's variants (not *visible individually*) cannot be chosen as a Different Style replacement.
- The `_SummaryTotals.cshtml` / `_FeesAndChargesList.cshtml` duplication, the pure `Plan(ReturnResolutionInput)` and
  `ReturnPolicy` (the rest of OI 5.6 / B7) stay open.
- The fee sentence is added to an existing template's main body only, and only while it still has the default
  "where they apply.<br /><br />": a translated body, or a body re-saved in the admin editor (which rewrites the
  `<br />`), is left alone and needs the line added by hand (T1).
- Known limit of nop's tokenizer, not changed here: a string token holding a double quote breaks an `%if%` condition,
  and nop then drops the whole block. That is why the fee sentence tests a bool; the `%PackName.Name%` lines of R1-A
  test a string, so a packname whose name contains `"` prints no "For:" line.

**Feature switches** (`Feature_Toggles_Reference.md` §1). D5 changes a page reached for existing returns, which stay
completable with Exchanges off (§1.1); the dropdown reads the store's rules whatever the switch says, and falls back to
every action when the store has no enabled action rule. D6 has no switch check: reasons and actions are shared by every store, and the
returns that record them exist whatever a switch says. OI 5.4 changes the storefront picker, which is already closed when
Exchanges is off. The fee sentence is in an e-mail only the Exchanges approval sends. **Feature-off case:** T10.

**Not verified by building.** The changes were checked against the files as they stand on 2026-11-09 (after R1-A was
applied), but no .NET SDK is reachable here, so the build is the first compile. The tests come with the phase and are
already in `Tests/CW.Tests` (they compile once the changes are applied).

## How to apply

Labels: **REPLACE** — the FIND block, at the lines given, becomes the REPLACE WITH block. **WHOLE FILE** — the file's
entire content becomes the block. **NEW FILE** — create the file with the block. **DELETE FILE** — remove the file
(and nothing else: none of the three has a `.csproj` entry).

Each change names its file under its heading. Within a file the blocks are listed **bottom to top**, and the line
numbers of each FIND are those of the file as it stands when that block is reached, so applying them in the order
given never moves the next one. The project compiles only once all are applied (new members are used across files).

## Changes
### IReturnCaseService.cs

`Plugins/Nop.Plugin.CW.Core/Services/Returns/IReturnCaseService.cs`

D6: whether a return case records a reason or an action.

#### 1. REPLACE — lines 46–49 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Returns/IReturnCaseService.cs`

FIND (lines 46–49, 4 lines):

```csharp
    /// </exception>
    Task SubmitReturnCaseAsync(ReturnCase returnCase);

    #endregion
```

REPLACE WITH (18 lines):

```csharp
    /// </exception>
    Task SubmitReturnCaseAsync(ReturnCase returnCase);

    /// <summary>
    /// Whether a case, whatever its status, records this return reason
    /// </summary>
    /// <param name="returnReasonId">Return request reason identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result is true when a case uses it</returns>
    Task<bool> IsReturnReasonUsedByCaseAsync(int returnReasonId);

    /// <summary>
    /// Whether a case, whatever its status, records this requested action
    /// </summary>
    /// <param name="returnActionId">Return request action identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result is true when a case uses it</returns>
    Task<bool> IsReturnActionUsedByCaseAsync(int returnActionId);

    #endregion
```

### ReturnCaseService.cs

`Plugins/Nop.Plugin.CW.Core/Services/Returns/ReturnCaseService.cs`

D6: the two case queries.

#### 2. REPLACE — lines 112–117 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Returns/ReturnCaseService.cs`

FIND (lines 112–117, 6 lines):

```csharp
        returnCase.CaseNumber = GenerateCaseNumber(returnCase);

        await UpdateReturnCaseAsync(returnCase);
    }

    #endregion
```

REPLACE WITH (16 lines):

```csharp
        returnCase.CaseNumber = GenerateCaseNumber(returnCase);

        await UpdateReturnCaseAsync(returnCase);
    }

    public virtual async Task<bool> IsReturnReasonUsedByCaseAsync(int returnReasonId)
    {
        return await _returnCaseRepository.Table.AnyAsync(returnCase => returnCase.ReturnReasonId == returnReasonId);
    }

    public virtual async Task<bool> IsReturnActionUsedByCaseAsync(int returnActionId)
    {
        return await _returnCaseRepository.Table.AnyAsync(returnCase => returnCase.ReturnActionId == returnActionId);
    }

    #endregion
```

### IExchangeService.cs

`Plugins/Nop.Plugin.CW.Core/Services/Exchanges/IExchangeService.cs`

D6: whether any return uses a reason / action, and the clean-up of a deleted one's rules. D5: the actions staff may choose for a reason.

#### 3. REPLACE — lines 204–209 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Exchanges/IExchangeService.cs`

FIND (lines 204–209, 6 lines):

```csharp
        /// <returns>A task that represents the asynchronous operation; the task result contains the reason identifiers</returns>
        Task<IList<int>> GetActiveReasonIdsAsync(int storeId);

        #endregion

        #region Return Requests
```

REPLACE WITH (44 lines):

```csharp
        /// <returns>A task that represents the asynchronous operation; the task result contains the reason identifiers</returns>
        Task<IList<int>> GetActiveReasonIdsAsync(int storeId);

        /// <summary>
        /// The requested actions staff may give a return line with this reason: those the store offers for the reason, as
        /// the storefront wizard offers them, or every action when the store has no enabled action rule at all (its return
        /// rules are not set up)
        /// </summary>
        /// <param name="returnReasonId">Return request reason identifier</param>
        /// <param name="storeId">The return's store identifier</param>
        /// <returns>A task that represents the asynchronous operation; the task result contains the actions</returns>
        Task<IList<ReturnRequestAction>> GetStaffActionsByReasonIdAsync(int returnReasonId, int storeId);

        /// <summary>
        /// Whether a return (any line or case, whatever its status) records this reason
        /// </summary>
        /// <param name="returnReasonId">Return request reason identifier</param>
        /// <returns>A task that represents the asynchronous operation; the task result is true when a return uses it</returns>
        Task<bool> IsReturnReasonInUseAsync(int returnReasonId);

        /// <summary>
        /// Whether a return (any line or case, whatever its status) records this requested action
        /// </summary>
        /// <param name="returnActionId">Return request action identifier</param>
        /// <returns>A task that represents the asynchronous operation; the task result is true when a return uses it</returns>
        Task<bool> IsReturnActionInUseAsync(int returnActionId);

        /// <summary>
        /// Deletes every store's rule and action links of a return reason that no longer exists
        /// </summary>
        /// <param name="returnReasonId">Return request reason identifier</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeleteReturnReasonConfigurationAsync(int returnReasonId);

        /// <summary>
        /// Deletes every store's rule and reason links of a return action that no longer exists
        /// </summary>
        /// <param name="returnActionId">Return request action identifier</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeleteReturnActionConfigurationAsync(int returnActionId);

        #endregion

        #region Return Requests
```

### ExchangeService.cs

`Plugins/Nop.Plugin.CW.Core/Services/Exchanges/ExchangeService.cs`

D6 and D5 implementations.

#### 4. REPLACE — lines 784–789 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Exchanges/ExchangeService.cs`

FIND (lines 784–789, 6 lines):

```csharp
            : enabledActions;
    }

    #endregion

    #region Return Requests
```

REPLACE WITH (45 lines):

```csharp
            : enabledActions;
    }

    public virtual async Task<IList<ReturnRequestAction>> GetStaffActionsByReasonIdAsync(int returnReasonId, int storeId)
    {
        var offered = await GetActiveActionsByReasonIdAsync(returnReasonId, storeId);
        if (offered.Count > 0)
            return offered;

        //A store with no enabled action rule has not set its return rules up, and offers nothing; staff still have to be
        //able to save its returns. A store that has rules but offers nothing for this reason offers nothing to staff either.
        var storeHasRules = await _returnActionRuleRepository.Table.AnyAsync(rule => rule.StoreId == storeId && !rule.IsDisabled);

        return storeHasRules
            ? offered
            : await _returnRequestService.GetAllReturnRequestActionsAsync();
    }

    public virtual async Task<bool> IsReturnReasonInUseAsync(int returnReasonId)
    {
        return await _returnRequestItemRepository.Table.AnyAsync(item => item.ReturnReasonId == returnReasonId)
            || await _returnCaseService.IsReturnReasonUsedByCaseAsync(returnReasonId);
    }

    public virtual async Task<bool> IsReturnActionInUseAsync(int returnActionId)
    {
        return await _returnRequestItemRepository.Table.AnyAsync(item => item.ReturnActionId == returnActionId)
            || await _returnCaseService.IsReturnActionUsedByCaseAsync(returnActionId);
    }

    public virtual async Task DeleteReturnReasonConfigurationAsync(int returnReasonId)
    {
        await _returnReasonRuleRepository.DeleteAsync(rule => rule.ReturnReasonId == returnReasonId);
        await _reasonActionRelationshipRepository.DeleteAsync(link => link.ReasonId == returnReasonId);
    }

    public virtual async Task DeleteReturnActionConfigurationAsync(int returnActionId)
    {
        await _returnActionRuleRepository.DeleteAsync(rule => rule.ReturnActionId == returnActionId);
        await _reasonActionRelationshipRepository.DeleteAsync(link => link.ActionId == returnActionId);
    }

    #endregion

    #region Return Requests
```

### ReturnReasonActionDeleteGuardFilter.cs

`Plugins/Nop.Plugin.CW.Core/Filters/ReturnReasonActionDeleteGuardFilter.cs`

D6: refuses nop's delete of a reason or action that a return records, with a message, back on the record's page.

#### 5. NEW FILE

`Plugins/Nop.Plugin.CW.Core/Filters/ReturnReasonActionDeleteGuardFilter.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Controllers;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Refuses to delete a return reason or a requested action that a return records.
/// </summary>
/// <remarks>
/// <para>
/// A return line (<c>ReturnRequestItem</c>) and a return case keep the reason and the action by id only. nop deletes
/// the record with no check, so a return that used it would lose its fees (they are looked up through the reason and
/// action rules), its approval and its reports. Any return counts, whatever its status: a finished return is read
/// again by the reports and the case. Staff can rename a reason or an action instead.
/// </para>
/// <para>
/// <b>Matched on nop's controller type and method name.</b> <c>ReturnsController</c> derives from nop's
/// <c>ReturnRequestController</c>, so the same two actions are reachable under both controller names; the type test
/// covers both. Method names are pinned to nop by <c>ReturnReasonActionDeleteGuardFilterTests</c>.
/// </para>
/// <para>
/// <b>No feature-switch check.</b> Reasons and actions are shared by every store, and the returns that use them
/// stay whatever a store's Exchanges switch says.
/// </para>
/// <para>
/// When the delete goes ahead, <see cref="ReturnReasonActionDeletedEventConsumer"/> removes the record's rules and links.
/// </para>
/// </remarks>
public class ReturnReasonActionDeleteGuardFilter : IAsyncActionFilter
{
    #region Fields

    private const string ReasonDeleteMethod = nameof(ReturnRequestController.ReturnRequestReasonDelete);
    private const string ActionDeleteMethod = nameof(ReturnRequestController.ReturnRequestActionDelete);

    protected readonly IExchangeService _exchangeService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;

    #endregion

    #region Ctor

    public ReturnReasonActionDeleteGuardFilter(IExchangeService exchangeService,
        ILocalizationService localizationService,
        INotificationService notificationService)
    {
        _exchangeService = exchangeService;
        _localizationService = localizationService;
        _notificationService = notificationService;
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is not ReturnRequestController
            || context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor
            || actionDescriptor.MethodInfo.Name is not (ReasonDeleteMethod or ActionDeleteMethod))
        {
            await next();
            return;
        }

        //nop binds the record as the route's "id"; without one nop finds no record and deletes nothing
        if (!context.ActionArguments.TryGetValue("id", out var value) || value is not int id || id <= 0)
        {
            await next();
            return;
        }

        var isReason = actionDescriptor.MethodInfo.Name == ReasonDeleteMethod;

        var inUse = isReason
            ? await _exchangeService.IsReturnReasonInUseAsync(id)
            : await _exchangeService.IsReturnActionInUseAsync(id);

        if (!inUse)
        {
            await next();
            return;
        }

        _notificationService.ErrorNotification(await _localizationService.GetResourceAsync(isReason
            ? "Plugins.CW.Core.Admin.Returns.Reason.InUse"
            : "Plugins.CW.Core.Admin.Returns.Action.InUse"));

        //back to the record's own page, where the delete button was
        context.Result = new RedirectToActionResult(isReason ? "ReturnRequestReasonEdit" : "ReturnRequestActionEdit", "ReturnRequest", new { id });
    }

    #endregion
}
```

### ReturnReasonActionDeletedEventConsumer.cs

`Plugins/Nop.Plugin.CW.Core/Services/Exchanges/ReturnReasonActionDeletedEventConsumer.cs`

D6: when nop deletes a reason or action no return uses, its per-store rules and reason-action links go with it.

#### 6. NEW FILE

`Plugins/Nop.Plugin.CW.Core/Services/Exchanges/ReturnReasonActionDeletedEventConsumer.cs`

```csharp
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Services.Events;

namespace Nop.Plugin.CW.Core.Services.Exchanges;

/// <summary>
/// Removes the per-store rules and the reason-action links of a return reason or requested action deleted in nop's
/// settings
/// </summary>
/// <remarks>
/// Only a record no return uses gets this far (<see cref="Filters.ReturnReasonActionDeleteGuardFilter"/>). Without
/// this its rule rows would stay behind, pointing at nothing.
/// </remarks>
public class ReturnReasonActionDeletedEventConsumer : IConsumer<EntityDeletedEvent<ReturnRequestReason>>,
    IConsumer<EntityDeletedEvent<ReturnRequestAction>>
{
    #region Fields

    protected readonly IExchangeService _exchangeService;

    #endregion

    #region Ctor

    public ReturnReasonActionDeletedEventConsumer(IExchangeService exchangeService)
    {
        _exchangeService = exchangeService;
    }

    #endregion

    #region Methods

    public async Task HandleEventAsync(EntityDeletedEvent<ReturnRequestReason> eventMessage)
    {
        await _exchangeService.DeleteReturnReasonConfigurationAsync(eventMessage.Entity.Id);
    }

    public async Task HandleEventAsync(EntityDeletedEvent<ReturnRequestAction> eventMessage)
    {
        await _exchangeService.DeleteReturnActionConfigurationAsync(eventMessage.Entity.Id);
    }

    #endregion
}
```

### NopStartup.cs

`Plugins/Nop.Plugin.CW.Core/Infrastructure/NopStartup.cs`

D6: the filter is registered. D4: `ICWReturnRequestService` and the admin `IReturnRequestModelFactory` override are no longer registered (nop's Return requests list is used).

#### 7. REPLACE — lines 212–215 (1 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Infrastructure/NopStartup.cs`

FIND (lines 212–215, 4 lines):

```csharp
            options.Filters.Add<PackNameOrderAdminGuardFilter>(1);
            options.Filters.Add<ReturnRequestSubmitFilter>(1);
        });
    }
```

REPLACE WITH (5 lines):

```csharp
            options.Filters.Add<PackNameOrderAdminGuardFilter>(1);
            options.Filters.Add<ReturnRequestSubmitFilter>(1);
            options.Filters.Add<ReturnReasonActionDeleteGuardFilter>(1);
        });
    }
```

#### 8. REPLACE — lines 161–169 (2 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Infrastructure/NopStartup.cs`

FIND (lines 161–169, 9 lines):

```csharp
        services.AddScoped<ICWSettingModelFactory, CWSettingModelFactory>();
        services.AddScoped<ICWAddressModelFactory, CWAddressModelFactory>();

        services.AddScoped<ICreditNotesModelFactory, CreditNotesModelFactory>();
        services.AddScoped<IReturnRequestModelFactory, ReturnRequestOverrideModelFactory>();
        services.AddScoped<IReturnRequestOverrideModelFactory, ReturnRequestOverrideModelFactory>();
        services.AddScoped<IExchangeSettingsModelFactory, ExchangeSettingsModelFactory>();

        services.AddScoped<ISubsidyModelFactory, SubsidyModelFactory>();
```

REPLACE WITH (8 lines):

```csharp
        services.AddScoped<ICWSettingModelFactory, CWSettingModelFactory>();
        services.AddScoped<ICWAddressModelFactory, CWAddressModelFactory>();

        services.AddScoped<ICreditNotesModelFactory, CreditNotesModelFactory>();
        services.AddScoped<IReturnRequestOverrideModelFactory, ReturnRequestOverrideModelFactory>();
        services.AddScoped<IExchangeSettingsModelFactory, ExchangeSettingsModelFactory>();

        services.AddScoped<ISubsidyModelFactory, SubsidyModelFactory>();
```

#### 9. REPLACE — lines 84–88 (3 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Infrastructure/NopStartup.cs`

FIND (lines 84–88, 5 lines):

```csharp
        services.AddScoped<IOrderProcessingService, OrderProcessingOverrideService>();
        services.AddScoped<IOrderProcessingOverrideService, OrderProcessingOverrideService>();
        services.AddScoped<ICWReturnRequestService, CWReturnRequestService>();

        services.AddScoped<ISubsidyService, SubsidyService>();
```

REPLACE WITH (4 lines):

```csharp
        services.AddScoped<IOrderProcessingService, OrderProcessingOverrideService>();
        services.AddScoped<IOrderProcessingOverrideService, OrderProcessingOverrideService>();

        services.AddScoped<ISubsidyService, SubsidyService>();
```

### ExchangesHelper.cs

`Plugins/Nop.Plugin.CW.Core/Helpers/ExchangesHelper.cs`

D5: the rule for the requested action staff may save (pure, unit-tested).

#### 10. REPLACE — lines 146–149 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Helpers/ExchangesHelper.cs`

FIND (lines 146–149, 4 lines):

```csharp
        return productDetails?.ProductAttributes != null && productDetails.ProductAttributes.Count > 0;
    }
}

```

REPLACE WITH (26 lines):

```csharp
        return productDetails?.ProductAttributes != null && productDetails.ProductAttributes.Count > 0;
    }

    /// <summary>
    /// Whether staff may give a return line this requested action for this reason: one the store offers for the
    /// reason (the storefront wizard's rule), or the line's saved pair left as it is.
    /// </summary>
    /// <remarks>
    /// The saved pair is allowed so a return keeps saving after the store's rules change; it does not carry over to
    /// another reason, so changing the reason means choosing an action that reason offers.
    /// </remarks>
    /// <param name="actionId">The action chosen</param>
    /// <param name="reasonId">The reason chosen</param>
    /// <param name="offeredActionIds">The actions the store offers for that reason</param>
    /// <param name="savedReasonId">The reason saved on the line; 0 when the line has none</param>
    /// <param name="savedActionId">The action saved on the line; 0 when the line has none</param>
    /// <returns>True when the action may be saved</returns>
    public static bool IsReturnActionAllowed(int actionId, int reasonId, IEnumerable<int> offeredActionIds, int savedReasonId, int savedActionId)
    {
        if (actionId <= 0)
            return false;

        return offeredActionIds.Contains(actionId) || (reasonId == savedReasonId && actionId == savedActionId);
    }
}

```

### IReturnRequestExchangeModelFactory.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/IReturnRequestExchangeModelFactory.cs`

D5: the action dropdown items for a reason.

#### 11. REPLACE — lines 12–14 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/IReturnRequestExchangeModelFactory.cs`

FIND (lines 12–14, 3 lines):

```csharp
    /// <param name="requestData">nop's own return request model for this row</param>
    Task PrepareExchangeRequestModelAsync(CWReturnRequestModel model, ReturnRequestModel requestData);
}
```

REPLACE WITH (13 lines):

```csharp
    /// <param name="requestData">nop's own return request model for this row</param>
    Task PrepareExchangeRequestModelAsync(CWReturnRequestModel model, ReturnRequestModel requestData);

    /// <summary>
    /// The requested actions staff may choose for a return line with this reason: those the return's store offers for
    /// the reason, and the line's saved action while its saved reason is chosen. The first item is "select an action"
    /// (value 0)
    /// </summary>
    /// <param name="returnRequest">The return; its store's rules apply</param>
    /// <param name="reasonId">The reason chosen on the page</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the dropdown items</returns>
    Task<IList<SelectListItem>> PrepareAvailableReturnActionsAsync(ReturnRequest returnRequest, int reasonId);
}
```

#### 12. REPLACE — lines 1–2 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/IReturnRequestExchangeModelFactory.cs`

FIND (lines 1–2, 2 lines):

```csharp
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Web.Areas.Admin.Models.Orders;
```

REPLACE WITH (4 lines):

```csharp
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Web.Areas.Admin.Models.Orders;
```

### ReturnRequestExchangeModelFactory.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/ReturnRequestExchangeModelFactory.cs`

D5: the return page's Requested action dropdown follows the reason.

#### 13. REPLACE — lines 281–292 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/ReturnRequestExchangeModelFactory.cs`

FIND (lines 281–292, 12 lines):

```csharp
        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        await PrepareRequestContextAsync(model, returnRequest, store, customer);
        await PrepareReasonAndActionListsAsync(model, requestItem);
        await PrepareEditabilityStateAsync(model, requestData, returnRequest);
        await PrepareExchangeDetailAsync(model, requestData, requestItem, returnRequest, store, customer);
        await PrepareReturnCaseDetailAsync(model, requestData);
    }

    #endregion
}

```

REPLACE WITH (37 lines):

```csharp
        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        await PrepareRequestContextAsync(model, returnRequest, store, customer);
        await PrepareReasonAndActionListsAsync(model, returnRequest, requestItem);
        await PrepareEditabilityStateAsync(model, requestData, returnRequest);
        await PrepareExchangeDetailAsync(model, requestData, requestItem, returnRequest, store, customer);
        await PrepareReturnCaseDetailAsync(model, requestData);
    }

    public virtual async Task<IList<SelectListItem>> PrepareAvailableReturnActionsAsync(ReturnRequest returnRequest, int reasonId)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);

        var items = new List<SelectListItem>
        {
            new() { Value = "0", Text = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Returns.Fields.Action.Select") }
        };

        var offered = await _exchangeService.GetStaffActionsByReasonIdAsync(reasonId, returnRequest.StoreId);
        items.AddRange(offered.Select(action => new SelectListItem { Value = action.Id.ToString(), Text = action.Name }));

        //and the line's saved action while its saved reason is chosen, so a return keeps saving after the rules change
        var requestItem = await _exchangeService.GetReturnRequestItemAsync(returnRequest.Id);
        if (requestItem != null && requestItem.ReturnReasonId == reasonId && requestItem.ReturnActionId > 0
            && offered.All(action => action.Id != requestItem.ReturnActionId))
        {
            var savedAction = await _returnRequestService.GetReturnRequestActionByIdAsync(requestItem.ReturnActionId);
            if (savedAction != null)
                items.Add(new SelectListItem { Value = savedAction.Id.ToString(), Text = savedAction.Name });
        }

        return items;
    }

    #endregion
}

```

#### 14. REPLACE — lines 108–123 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/ReturnRequestExchangeModelFactory.cs`

FIND (lines 108–123, 16 lines):

```csharp
        }
    }

    protected async Task PrepareReasonAndActionListsAsync(CWReturnRequestModel model, ReturnRequestItem requestItem)
    {
        var reasons = await _returnRequestService.GetAllReturnRequestReasonsAsync();
        var actions = await _returnRequestService.GetAllReturnRequestActionsAsync();

        model.ReturnRequestReasonId = requestItem?.ReturnReasonId ?? 0;
        model.ReturnRequestActionId = requestItem?.ReturnActionId ?? 0;

        model.ReturnReasonsList = new SelectList(reasons, "Id", "Name", model.ReturnRequestReasonId);
        model.ReturnActionsList = new SelectList(actions, "Id", "Name", model.ReturnRequestActionId);
    }

    protected async Task PrepareEditabilityStateAsync(CWReturnRequestModel model, ReturnRequestModel requestData, ReturnRequest returnRequest)
```

REPLACE WITH (30 lines):

```csharp
        }
    }

    protected async Task PrepareReasonAndActionListsAsync(CWReturnRequestModel model, ReturnRequest returnRequest, ReturnRequestItem requestItem)
    {
        var reasons = await _returnRequestService.GetAllReturnRequestReasonsAsync();

        //a refused save redisplays what staff posted; the page itself opens on what the line has saved
        if (model.ReturnRequestReasonId <= 0)
        {
            model.ReturnRequestReasonId = requestItem?.ReturnReasonId ?? 0;
            model.ReturnRequestActionId = requestItem?.ReturnActionId ?? 0;
        }

        model.ReturnReasonsList = new SelectList(reasons, "Id", "Name", model.ReturnRequestReasonId);

        //a plain nop return (no exchange line) stores neither choice, so its dropdowns stay as they were: every action
        if (requestItem == null)
        {
            var allActions = await _returnRequestService.GetAllReturnRequestActionsAsync();
            model.ReturnActionsList = new SelectList(allActions, "Id", "Name", model.ReturnRequestActionId);

            return;
        }

        var actions = await PrepareAvailableReturnActionsAsync(returnRequest, model.ReturnRequestReasonId);
        model.ReturnActionsList = new SelectList(actions, nameof(SelectListItem.Value), nameof(SelectListItem.Text), model.ReturnRequestActionId.ToString());
    }

    protected async Task PrepareEditabilityStateAsync(CWReturnRequestModel model, ReturnRequestModel requestData, ReturnRequest returnRequest)
```

### ReturnExchangeRequestValidator.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Validators/Exchanges/ReturnExchangeRequestValidator.cs`

D5: a post with an action the reason does not offer is refused (the saved pair is always allowed).

#### 15. REPLACE — lines 85–88 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Validators/Exchanges/ReturnExchangeRequestValidator.cs`

FIND (lines 85–88, 4 lines):

```csharp
        RuleFor(m => m.ReturnRequestReasonId).GreaterThan(0).WithMessage("Please select a reason for return");
        RuleFor(m => m.ReturnRequestActionId).GreaterThan(0).WithMessage("Please select a requested action");

        //nop's own rule: its validator is not run for this model, and the save puts the difference back in stock
```

REPLACE WITH (8 lines):

```csharp
        RuleFor(m => m.ReturnRequestReasonId).GreaterThan(0).WithMessage("Please select a reason for return");
        RuleFor(m => m.ReturnRequestActionId).GreaterThan(0).WithMessage("Please select a requested action");
        RuleFor(m => m.ReturnRequestActionId)
            .MustAsync((model, _, _) => ValidReturnActionAsync(model, returnRequestService, exchangeService))
            .When(model => model.ReturnRequestActionId > 0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Returns.Fields.Action.NotForReason"));

        //nop's own rule: its validator is not run for this model, and the save puts the difference back in stock
```

#### 16. REPLACE — lines 75–80 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Validators/Exchanges/ReturnExchangeRequestValidator.cs`

FIND (lines 75–80, 6 lines):

```csharp
        return true;
    }

    #endregion

    public ReturnExchangeRequestValidator(IReturnRequestService returnRequestService,
```

REPLACE WITH (29 lines):

```csharp
        return true;
    }

    /// <summary>
    /// The requested action is one staff may give the line for the chosen reason (the store's offer, or the line's saved
    /// pair). The page filters the dropdown the same way; this holds a post that skips the page.
    /// </summary>
    private static async Task<bool> ValidReturnActionAsync(CWReturnRequestModel model, IReturnRequestService returnRequestService,
        IExchangeService exchangeService)
    {
        //a missing return is the controller's to report
        var returnRequest = await returnRequestService.GetReturnRequestByIdAsync(model.Id);
        if (returnRequest == null)
            return true;

        //a plain nop return (no exchange line) does not store the choice, so there is nothing to hold
        var returnRequestItem = await exchangeService.GetReturnRequestItemAsync(returnRequest.Id);
        if (returnRequestItem == null)
            return true;

        var offered = await exchangeService.GetStaffActionsByReasonIdAsync(model.ReturnRequestReasonId, returnRequest.StoreId);

        return ExchangesHelper.IsReturnActionAllowed(model.ReturnRequestActionId, model.ReturnRequestReasonId,
            offered.Select(action => action.Id), returnRequestItem.ReturnReasonId, returnRequestItem.ReturnActionId);
    }

    #endregion

    public ReturnExchangeRequestValidator(IReturnRequestService returnRequestService,
```

### ReturnsController.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ReturnsController.cs`

D5: the dropdown's AJAX source.

#### 17. REPLACE — lines 740–743 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ReturnsController.cs`

FIND (lines 740–743, 4 lines):

```csharp
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SETTINGS)]
    public virtual async Task<IActionResult> ReturnRequestReasonCreatePopup()
```

REPLACE WITH (22 lines):

```csharp
    }

    /// <summary>
    /// The requested actions of the return page's dropdown, for the reason chosen there
    /// </summary>
    /// <param name="returnRequestId">The return being edited; its store's rules apply</param>
    /// <param name="reasonId">The reason chosen</param>
    [HttpGet]
    [CheckPermission(StandardPermission.Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE)]
    public virtual async Task<IActionResult> ReturnActionsForReason(int returnRequestId, int reasonId)
    {
        var returnRequest = await _returnRequestService.GetReturnRequestByIdAsync(returnRequestId);
        if (returnRequest == null)
            return NotFound();

        var actions = await _modelFactory.PrepareAvailableReturnActionsAsync(returnRequest, reasonId);

        return Json(actions.Select(action => new { action.Value, action.Text }));
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SETTINGS)]
    public virtual async Task<IActionResult> ReturnRequestReasonCreatePopup()
```

### _CreateOrUpdateExchange.cshtml

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/ReturnRequest/_CreateOrUpdateExchange.cshtml`

D5: the config div and the script registration (editable returns only).

#### 18. REPLACE — lines 275–278 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/ReturnRequest/_CreateOrUpdateExchange.cshtml`

FIND (lines 275–278, 4 lines):

```cshtml
                                                <nop-select asp-for="ReturnRequestActionId" asp-items="@Model.ReturnActionsList" />
                                                <span asp-validation-for="ReturnRequestActionId"></span>
                                            }
                                            else
```

REPLACE WITH (12 lines):

```cshtml
                                                <nop-select asp-for="ReturnRequestActionId" asp-items="@Model.ReturnActionsList" />
                                                <span asp-validation-for="ReturnRequestActionId"></span>
                                                @if (actionsFollowReason)
                                                {
                                                    <div class="cw-return-actions-config"
                                                         data-url="@Url.Action("ReturnActionsForReason", "Returns")"
                                                         data-return-request-id="@Model.Id"
                                                         data-error-text="@T("Plugins.CW.Core.Admin.Returns.Fields.Action.LoadError")"
                                                         style="display: none;"></div>
                                                }
                                            }
                                            else
```

#### 19. REPLACE — lines 25–28 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/ReturnRequest/_CreateOrUpdateExchange.cshtml`

FIND (lines 25–28, 4 lines):

```cshtml
    var needsCompletion = isExchangeModelData ? exchangeRequestModel.NeedsCompletion : false;
    var isCompleted = isExchangeModelData ? exchangeRequestModel.IsCompleted : false;

}
```

REPLACE WITH (10 lines):

```cshtml
    var needsCompletion = isExchangeModelData ? exchangeRequestModel.NeedsCompletion : false;
    var isCompleted = isExchangeModelData ? exchangeRequestModel.IsCompleted : false;

    //the requested-action dropdown follows the reason while an exchange line can be edited (a plain nop return has no
    //exchange line, so no mode, and stores neither choice)
    var actionsFollowReason = allowEdit && isExchangeModelData && exchangeRequestModel.ExchangeModeId > 0;
    if (actionsFollowReason)
        NopHtml.AddScriptParts(ResourceLocation.Footer, "~/Plugins/CW.Core/Content/Admin/Exchanges/return-request-actions.js");

}
```

### return-request-actions.js

`Plugins/Nop.Plugin.CW.Core/Content/Admin/Exchanges/return-request-actions.js`

D5: reloads the action dropdown when the reason changes.

#### 20. NEW FILE

`Plugins/Nop.Plugin.CW.Core/Content/Admin/Exchanges/return-request-actions.js`

```javascript
/*
 * The admin return page: the Requested action dropdown follows the Reason for return, as the storefront wizard does.
 * Server values come from the .cw-return-actions-config div (CLAUDE.md 9.14). The dropdown and the config div are
 * only on the page while the return can still be edited.
 */
(function () {
    var $cfg = $('.cw-return-actions-config');
    if (!$cfg.length)
        return;

    var $reason = $('#ReturnRequestReasonId');
    var $action = $('#ReturnRequestActionId');

    //the dropdowns are part of the page, rendered once, so a namespaced rebind is enough
    $reason.off('.cwreturnactions').on('change.cwreturnactions', function () {
        var chosen = $action.val();

        $.ajax({
            cache: false,
            type: 'GET',
            url: $cfg.data('url'),
            data: { returnRequestId: $cfg.data('returnRequestId'), reasonId: $reason.val() },
            success: function (items) {
                $action.empty();
                $.each(items, function (i, item) {
                    $('<option></option>').val(item.Value).text(item.Text).appendTo($action);
                });

                //keep the chosen action when the new reason offers it; otherwise "select an action", so staff choose again
                $action.val($action.find('option[value="' + chosen + '"]').length ? chosen : '0');
                $action.trigger('change');
            },
            error: function () {
                display_nop_error({ error: $cfg.data('errorText') });
            }
        });
    });
})();
```

### Nop.Plugin.CW.Core.csproj

`Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`

D5: the new script deploys (no glob — CLAUDE.md §9.8).

#### 21. REPLACE — lines 31–34 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`

FIND (lines 31–34, 4 lines):

```xml
  </ItemGroup>
  <ItemGroup>
    <Content Include="Content\Admin\MultiStore\admin.packname.bulk.js">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
```

REPLACE WITH (7 lines):

```xml
  </ItemGroup>
  <ItemGroup>
    <Content Include="Content\Admin\Exchanges\return-request-actions.js">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="Content\Admin\MultiStore\admin.packname.bulk.js">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
```

#### 22. REPLACE — lines 22–25 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`

FIND (lines 22–25, 4 lines):

```xml
  </ItemGroup>
  <ItemGroup>
    <None Remove="Content\Admin\MultiStore\admin.packname.bulk.js" />
    <None Remove="Content\Admin\MultiStore\admin.store.navigation.js" />
```

REPLACE WITH (5 lines):

```xml
  </ItemGroup>
  <ItemGroup>
    <None Remove="Content\Admin\Exchanges\return-request-actions.js" />
    <None Remove="Content\Admin\MultiStore\admin.packname.bulk.js" />
    <None Remove="Content\Admin\MultiStore\admin.store.navigation.js" />
```

### ReturnRequestOverrideModelFactory.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/ReturnRequestOverrideModelFactory.cs`

D4: the list override and its first / last name branch are removed (no screen posted the filter; otherwise the same as nop's list). The class keeps the one-return model the exchange configuration screens use.

#### 23. WHOLE FILE — replace the entire content of the file with this

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/ReturnRequestOverrideModelFactory.cs`

```csharp
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Factories;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// The admin return request model of one return, for the exchange configuration screens
/// </summary>
/// <remarks>
/// It no longer overrides nop's list: the override read a first / last name filter that no screen posts and
/// otherwise did what nop's own does, so the Return requests list is nop's.
/// </remarks>
public class ReturnRequestOverrideModelFactory : ReturnRequestModelFactory, IReturnRequestOverrideModelFactory
{
    #region Ctor

    public ReturnRequestOverrideModelFactory(IBaseAdminModelFactory baseAdminModelFactory,
        IDateTimeHelper dateTimeHelper,
        IDownloadService downloadService,
        ICustomerService customerService,
        ILocalizationService localizationService,
        ILocalizedModelFactory localizedModelFactory,
        IOrderService orderService,
        IProductService productService,
        IReturnRequestService returnRequestService) : base(baseAdminModelFactory,
            dateTimeHelper,
            downloadService,
            customerService,
            localizationService,
            localizedModelFactory,
            orderService,
            productService,
            returnRequestService)
    {
    }

    #endregion

    #region Methods

    public async Task<ReturnRequestModel> PrepareReturnRequestModelAsync(ReturnRequest returnRequest, bool excludeProperties)
    {
        var model = new ReturnRequestModel();

        ArgumentNullException.ThrowIfNull(returnRequest);

        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);

        if (orderItem != null)
        {
            var product = await _productService.GetProductByIdAsync(orderItem.ProductId);
            model.ProductId = product.Id;
            model.ProductName = product.Name;
            model.OrderId = orderItem.OrderId;
            model.AttributeInfo = orderItem.AttributeDescription;
            var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
            model.CustomOrderNumber = order.CustomOrderNumber;
        }
        model.Id = returnRequest.Id;
        model.CustomNumber = returnRequest.CustomNumber;
        model.CustomerId = returnRequest.CustomerId;
        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        model.CustomerInfo = await _customerService.IsRegisteredAsync(customer)
            ? customer.Email : await _localizationService.GetResourceAsync("Admin.Customers.Guest");

        model.Quantity = returnRequest.Quantity;
        model.ReturnRequestStatusStr = await _localizationService.GetLocalizedEnumAsync(returnRequest.ReturnRequestStatus);

        var download = await _downloadService.GetDownloadByIdAsync(returnRequest.UploadedFileId);
        model.UploadedFileGuid = download != null ? download.DownloadGuid : Guid.Empty;
        model.CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(returnRequest.CreatedOnUtc, DateTimeKind.Utc);
        if (!excludeProperties)
        {
            model.ReasonForReturn = returnRequest.ReasonForReturn;
            model.RequestedAction = returnRequest.RequestedAction;
            model.CustomerComments = returnRequest.CustomerComments;
            model.StaffNotes = returnRequest.StaffNotes;
            model.ReturnRequestStatusId = returnRequest.ReturnRequestStatusId;
        }

        return model;
    }

    #endregion
}
```

### ExchangesModelFactory.cs

`Plugins/Nop.Plugin.CW.Core/Factories/ExchangesModelFactory.cs`

OI 5.4: the replacement grid lists simple products only.

#### 24. REPLACE — lines 408–411 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Factories/ExchangesModelFactory.cs`

FIND (lines 408–411, 4 lines):

```csharp
            keywords: string.IsNullOrWhiteSpace(keywords) ? null : keywords.Trim(),
            searchSku: true,
            visibleIndividuallyOnly: true,
            orderBy: ProductSortingEnum.NameAsc,
```

REPLACE WITH (6 lines):

```csharp
            keywords: string.IsNullOrWhiteSpace(keywords) ? null : keywords.Trim(),
            searchSku: true,
            //a grouped product's parent has no price or options of its own, and the picker cannot offer its variants
            productType: ProductType.SimpleProduct,
            visibleIndividuallyOnly: true,
            orderBy: ProductSortingEnum.NameAsc,
```

### ExchangesController.cs

`Plugins/Nop.Plugin.CW.Core/Controllers/ExchangesController.cs`

OI 5.4: the options step refuses a grouped product's parent.

#### 25. REPLACE — lines 340–343 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Controllers/ExchangesController.cs`

FIND (lines 340–343, 4 lines):

```csharp
            return NotFound();

        if (returnRequestId > 0)
        {
```

REPLACE WITH (8 lines):

```csharp
            return NotFound();

        //a grouped product's parent is not something to order; the grid does not offer one
        if (product.ProductType == ProductType.GroupedProduct)
            return NotFound();

        if (returnRequestId > 0)
        {
```

#### 26. REPLACE — lines 6–9 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Controllers/ExchangesController.cs`

FIND (lines 6–9, 4 lines):

```csharp
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
```

REPLACE WITH (5 lines):

```csharp
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
```

### ReturnSubmissionLogic.cs

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ReturnSubmissionLogic.cs`

OI 5.4: the submission refuses a grouped product's parent as a replacement.

#### 27. REPLACE — lines 156–162 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ReturnSubmissionLogic.cs`

FIND (lines 156–162, 7 lines):

```csharp
            return false;

        var product = await _productService.GetProductByIdAsync(line.ReplacementProductId.Value);
        if (product == null || product.Deleted || product.IsGiftCard || product.IsRental)
            return false;

        //every required option chosen, each one of the product's values
```

REPLACE WITH (7 lines):

```csharp
            return false;

        var product = await _productService.GetProductByIdAsync(line.ReplacementProductId.Value);
        if (product == null || product.Deleted || product.IsGiftCard || product.IsRental || product.ProductType == ProductType.GroupedProduct)
            return false;

        //every required option chosen, each one of the product's values
```

#### 28. REPLACE — lines 1–3 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/BusinessLogic/ReturnSubmissionLogic.cs`

FIND (lines 1–3, 3 lines):

```csharp
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
```

REPLACE WITH (4 lines):

```csharp
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
```

### ProductsHelper.cs

`Plugins/Nop.Plugin.CW.Core/Helpers/ProductsHelper.cs`

OI 5.6 (part): the unused mapping-level `IsColourAttribute` overload is removed.

#### 29. REPLACE — lines 59–75 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Helpers/ProductsHelper.cs`

FIND (lines 59–75, 17 lines):

```csharp
        public static bool IsColourAttribute(ProductDetailsModel.ProductAttributeModel attributeModel)
        {
            return IsColourAttributeType(attributeModel.AttributeControlType) || IsColourAttributeName(attributeModel.Name);
        }

        /// <remarks>
        /// Unused — kept only in case the mapping-level path is revived.
        /// If it ever is, give it the same two-pass treatment as <see cref="FindColourAttribute"/>; the
        /// loose "col" prefix must not be applied per attribute.
        /// </remarks>
        public static bool IsColourAttribute(ProductAttributeMapping attributeMapping, ProductAttribute productAttribute)
        {

            return IsColourAttributeType(attributeMapping.AttributeControlType) || IsColourAttributeName(productAttribute.Name);
        }

        /// <summary>
```

REPLACE WITH (6 lines):

```csharp
        public static bool IsColourAttribute(ProductDetailsModel.ProductAttributeModel attributeModel)
        {
            return IsColourAttributeType(attributeModel.AttributeControlType) || IsColourAttributeName(attributeModel.Name);
        }

        /// <summary>
```

### MessageTokenProviderOverride.cs

`Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`

Payment e-mail: `%ReturnRequest.HasAdditionalFee%`, a bool the template can test safely.

#### 30. REPLACE — lines 727–730 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`

FIND (lines 727–730, 4 lines):

```csharp
                tokens.Add(new Token("ReturnRequest.AdditionalFeeName", additionalFeeName));
                tokens.Add(new Token("ReturnRequest.AdditionalFeeAmount", await _priceFormatter.FormatPriceAsync(additionalFeeAmount)));
            }
        }
```

REPLACE WITH (7 lines):

```csharp
                tokens.Add(new Token("ReturnRequest.AdditionalFeeName", additionalFeeName));
                tokens.Add(new Token("ReturnRequest.AdditionalFeeAmount", await _priceFormatter.FormatPriceAsync(additionalFeeAmount)));
                //a bool, not a string, so a template's %if% can test it whatever the fee's name holds (a quote in a
                //string token breaks nop's condition, which then drops the whole %if% block)
                tokens.Add(new Token("ReturnRequest.HasAdditionalFee", additionalFeeAmount > decimal.Zero));
            }
        }
```

### EventConsumer.cs

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

The new token in the admin token list.

#### 31. REPLACE — lines 737–742 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

FIND (lines 737–742, 6 lines):

```csharp
            eventMessage.AdditionalTokens.Add("%ReturnRequest.RePackagingFee%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.AdditionalFeeName%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.AdditionalFeeAmount%");
        }

        //The welcome e-mail of a customer registered by a manager: the packname and the subsidies assigned to it
```

REPLACE WITH (7 lines):

```csharp
            eventMessage.AdditionalTokens.Add("%ReturnRequest.RePackagingFee%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.AdditionalFeeName%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.AdditionalFeeAmount%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.HasAdditionalFee%");
        }

        //The welcome e-mail of a customer registered by a manager: the packname and the subsidies assigned to it
```

### CWCorePlugin.cs

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

The payment e-mail names the additional fee (default body, and an existing template once), and the new resources.

#### 32. REPLACE — lines 1718–1723 (1 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 1718–1723, 6 lines):

```csharp
            ["Plugins.CW.Core.Messages.Subsidy.Details.Credit"] = "{0} credit",
            ["Plugins.CW.Core.Messages.Subsidy.Details.Units"] = "{0} items",
            ["Plugins.CW.Core.Admin.Returns.CompletionNotification.Queued"] = "The customer has been sent the e-mail asking them to complete the checkout.",
            ["Plugins.CW.Core.Admin.Returns.CompletionNotification.NotQueued"] = "No e-mail was queued: the return is not waiting for the customer to check out, or the 'ReturnAuthorisedPayment.CustomerNotification' message template is not active for this store.",

            // Exchanges — customer-facing return wizard
```

REPLACE WITH (11 lines):

```csharp
            ["Plugins.CW.Core.Messages.Subsidy.Details.Credit"] = "{0} credit",
            ["Plugins.CW.Core.Messages.Subsidy.Details.Units"] = "{0} items",
            ["Plugins.CW.Core.Admin.Returns.CompletionNotification.Queued"] = "The customer has been sent the e-mail asking them to complete the checkout.",
            ["Plugins.CW.Core.Admin.Returns.Reason.InUse"] = "This return reason cannot be deleted: returns use it. You can rename it instead.",
            ["Plugins.CW.Core.Admin.Returns.Action.InUse"] = "This return action cannot be deleted: returns use it. You can rename it instead.",
            ["Plugins.CW.Core.Admin.Returns.Fields.Action.Select"] = "Select an action",
            ["Plugins.CW.Core.Admin.Returns.Fields.Action.NotForReason"] = "The requested action is not offered for this reason. Please choose one from the list.",
            ["Plugins.CW.Core.Admin.Returns.Fields.Action.LoadError"] = "The actions for this reason could not be loaded. Please reload the page.",
            ["Plugins.CW.Core.Admin.Returns.CompletionNotification.NotQueued"] = "No e-mail was queued: the return is not waiting for the customer to check out, or the 'ReturnAuthorisedPayment.CustomerNotification' message template is not active for this store.",

            // Exchanges — customer-facing return wizard
```

#### 33. REPLACE — lines 246–251 (2 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 246–251, 6 lines):

```csharp
                continue;

            template.Body = template.Body.Insert(position + anchor.Length, packNameLine);

            await _messageTemplateService.UpdateMessageTemplateAsync(template);
        }
```

REPLACE WITH (38 lines):

```csharp
                continue;

            template.Body = template.Body.Insert(position + anchor.Length, packNameLine);

            await _messageTemplateService.UpdateMessageTemplateAsync(template);
        }
    }

    /// <summary>
    /// Adds the additional-fee sentence to a "payment needed" approval e-mail installed before the sentence was in the
    /// default body.
    /// </summary>
    /// <remarks>
    /// Targeted, so a store's own wording survives, and idempotent: a body that already uses the additional-fee tokens, or
    /// whose anchor sentence has been reworded, is left alone.
    /// </remarks>
    protected async Task AddAdditionalFeeToPaymentTemplateAsync()
    {
        const string anchor = "where they apply.<br /><br />";
        const string withFeeLine = "where they apply.<br />" +
            "%if (%ReturnRequest.HasAdditionalFee%) It also includes %ReturnRequest.AdditionalFeeName% (%ReturnRequest.AdditionalFeeAmount%).<br /> endif%" +
            "<br />";

        foreach (var template in await _messageTemplateService.GetMessageTemplatesByNameAsync(CWMessageTemplateSystemNames.ReturnAuthorisedPaymentCustomer))
        {
            if (string.IsNullOrEmpty(template.Body) || template.Body.Contains("%ReturnRequest.AdditionalFee", StringComparison.OrdinalIgnoreCase)
                || template.Body.Contains("%ReturnRequest.HasAdditionalFee%", StringComparison.OrdinalIgnoreCase))
                continue;

            var position = template.Body.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);

            if (position < 0)
                continue;

            template.Body = template.Body.Remove(position, anchor.Length).Insert(position, withFeeLine);

            await _messageTemplateService.UpdateMessageTemplateAsync(template);
        }
```

#### 34. REPLACE — lines 189–193 (3 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 189–193, 5 lines):

```csharp
                       "Amount to pay: <strong>%ReturnRequest.PendingAmount%</strong><br />" +
                       "This includes the redelivery fee (%ReturnRequest.ReShippingFee%) and the repackaging fee (%ReturnRequest.RePackagingFee%) " +
                       "where they apply.<br /><br />" +
                       "Thank you,<br />%Store.Name%<br />",
                IsActive = true,
```

REPLACE WITH (7 lines):

```csharp
                       "Amount to pay: <strong>%ReturnRequest.PendingAmount%</strong><br />" +
                       "This includes the redelivery fee (%ReturnRequest.ReShippingFee%) and the repackaging fee (%ReturnRequest.RePackagingFee%) " +
                       "where they apply.<br />" +
                       "%if (%ReturnRequest.HasAdditionalFee%) It also includes %ReturnRequest.AdditionalFeeName% (%ReturnRequest.AdditionalFeeAmount%).<br /> endif%" +
                       "<br />" +
                       "Thank you,<br />%Store.Name%<br />",
                IsActive = true,
```

#### 35. REPLACE — lines 92–95 (4 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 92–95, 4 lines):

```csharp
        await InstallCustomerRegistrationTemplateAsync();
        await AddPackNameToReturnReceiptTemplateAsync();
        await PointCustomerRegistrationTemplateAtSubsidyDetailsAsync();
        await PointShipmentTemplatesAtShipmentAddressAsync();
```

REPLACE WITH (5 lines):

```csharp
        await InstallCustomerRegistrationTemplateAsync();
        await AddPackNameToReturnReceiptTemplateAsync();
        await AddAdditionalFeeToPaymentTemplateAsync();
        await PointCustomerRegistrationTemplateAtSubsidyDetailsAsync();
        await PointShipmentTemplatesAtShipmentAddressAsync();
```

### plugin.json

`Plugins/Nop.Plugin.CW.Core/plugin.json`

DATA version bump, so the update installs the resources and updates the template.

#### 36. REPLACE — lines 3–7 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/plugin.json`

FIND (lines 3–7, 5 lines):

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_06_DATA_0.1",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```

REPLACE WITH (5 lines):

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_09_DATA_0.1",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```

### Files deleted (D4)

#### 37. DELETE FILE

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/Exchanges/CWReturnRequestSearchModel.cs`

#### 38. DELETE FILE

`Plugins/Nop.Plugin.CW.Core/Services/Orders/CWReturnRequestService.cs`

#### 39. DELETE FILE

`Plugins/Nop.Plugin.CW.Core/Services/Orders/ICWReturnRequestService.cs`

## Tests (already in `Tests/CW.Tests`, added by Claude)

| File | What it pins |
|---|---|
| `Core/Helpers/ExchangesHelperTests.cs` (+5) | `IsReturnActionAllowed`: an offered action is allowed, another is refused, the saved pair keeps saving, the saved action does not carry over to another reason, "select an action" (0) is never a choice |
| `Core/Filters/ReturnReasonActionDeleteGuardFilterTests.cs` (new, 6) | A reason / action a return uses is not deleted (redirect to its edit page, one error message); one no return uses is deleted; the same delete reached through `ReturnsController` is guarded; nop's other return-settings actions are not checked |
| `Core/Infrastructure/NopStartupTests.cs` (changed) | The admin `ReturnRequestOverrideModelFactory` is registered under its CW interface only — nop's admin `IReturnRequestModelFactory` is no longer replaced |

Not unit-tested (they need the database or the browser): the in-use queries, the rule clean-up, the dropdown's
reload, the template update. They are T2–T8 below.

## Build and checks

1. Build the solution, then `dotnet test Tests/CW.Tests -p:SolutionDir=<repo root>` (or Test Explorer).
2. Restart the site and run the plugin update (version `2026_11_09_DATA_0.1`): it installs the five new resources and
   adds the fee sentence to an existing "payment needed" template.
3. The new script is a new file, so no stale bundle can hide it; if the admin return page still behaves as before,
   empty `Presentation/Nop.Web/wwwroot/bundles` (`CLAUDE.md` §9.11).

## Test plan (dev site `https://localhost:59579`, database `testnop50`)

| # | Steps | Expected |
|---|---|---|
| T1 | After the update: *Content management → Message templates → ReturnAuthorisedPayment.CustomerNotification* | The body has the `%if (%ReturnRequest.HasAdditionalFee%) It also includes … endif%` line after "where they apply."; the allowed tokens list `%ReturnRequest.HasAdditionalFee%`. (A template already re-saved in the editor, or a translation, is not changed: add the line by hand) |
| T2 | *Exchanges → Reason rules*: link reason A to actions X and Y only (store 1). Open a **pending** return of store 1; choose reason A | *Requested action* reloads to "Select an action", X, Y; the action chosen before stays selected when A offers it, else "Select an action" |
| T3 | On the same return, pick reason A and post an action A does not offer (browser dev tools: add an `<option>` with its id) | Saving shows "The requested action is not offered for this reason…" next to reason A and A's actions (what was posted); nothing is saved |
| T3b | Open a **plain** return (no exchange line, e.g. RR 5); change its status and save | Saves as before; the action dropdown lists every action, with no "Select an action" |
| T4 | Open a return whose saved reason / action the rules no longer link (or unlink them after it was made); save it unchanged | The saved action is in the dropdown and the return saves |
| T5 | *Settings → Order settings → Return request reasons*: delete a reason a return uses (e.g. "Received Wrong Product") | Refused: back on the reason's page with "This return reason cannot be deleted…"; the reason is still listed |
| T6 | Add a new reason, give it a reason rule and a link on *Exchanges → Reason rules*, then delete it | Deleted; its rule row and links are gone (`CORE_EX_ReturnReasonRule` / `CORE_EX_ReasonActionRelationship` have no row for its id). Repeat T5–T6 for an action (`CORE_EX_ReturnActionRule`) |
| T7 | *Sales → Return requests*: filter by status, by dates, by custom number; page through | nop's list: the same rows and columns as before, the filters work |
| T8 | Storefront exchange wizard, Different Style: search the picker for a grouped product (create one with two associated products if the sample data has none); request `/Exchanges/ReplacementProductOptions?productId=<parent id>&orderItemId=<item>` by hand | The parent is not in the grid; the hand-made request answers 404 |
| T9 | Approve a pathway-B return with an additional fee (as R1-A T3), then one without | The queued e-mail has "It also includes <fee name> (<amount>)." with the fee; without a fee, no such sentence and no blank gap |
| T10 | Feature off: switch Exchanges off on store 1; open a pending return in admin and change its reason; switch it back on | The page works as with Exchanges on (existing returns stay completable); the storefront wizard stays closed (404) |
| T11 | Regression: storefront wizard — reason, action, mode and a Same Style exchange end to end | Unchanged |

## After the phase

On approval:
- `Subsidy_PackName_Open_Items.md`: remove 2.1.5, 2.1.12, 2.2.6, 5.3, 5.4 and the `IsColourAttribute` part of 5.6.
- `LegacyVersions/NOP41_To_500_Gap_Analysis.md`: D5 and D6 done; D4 done as "dead code removed, name filters not ported".
- `Exchanges_Rebuild_Record.md` §9.1 / backlog B7: the rule clean-up on delete is done (`ReturnPolicy` stays open).
- `CLAUDE.md` §13.4: drop the `ICWReturnRequestService` row and "(as nop's `IReturnRequestModelFactory` too)" on the
  admin `ReturnRequestOverrideModelFactory`; add `ReturnReasonActionDeleteGuardFilter` to the global MVC filters and
  `ReturnReasonActionDeletedEventConsumer` to the consumers a reader looks for.
- `Exchanges_Returns_CreditNotes_Reference.md`: §6.3 (`CWReturnRequestService` gone), §8.1 (the action dropdown, the
  delete guard), the payment e-mail's fee sentence.

This document then moves to `CWPlans/Deprecated/`.
