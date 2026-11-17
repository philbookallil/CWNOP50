# "Allowance" → "subsidy" — fix spec

> **Status:** changes 1–13 applied by Claude on 2026-11-16 (wording only, with Eduardo's permission for this case).
> Still to do: the one-time step and the test below.

**Why.** The system's term is **subsidy**. "Allowance" was 4.6's name for it (`Jim2.Allowances`) and is not part of
this system, but it is still in three customer-facing texts and in the code around them. This spec removes it from
CW.Core completely. Rule: `CLAUDE.md` §7, *Terminology*.

**Scope.** Every "allowance" in the CW plugins, found with
`grep -rni allowance Plugins/Nop.Plugin.CW.* CWLibraries Tests/CW.Tests Themes/CW.Standard`. The one hit left is
`Plugins/Nop.Plugin.ExternalAuth.SSO/Services/Saml/Saml2ConfigurationFactory.cs:61`, a clock-skew "allowance" in the SAML
library, which has nothing to do with subsidies: leave it.

**What changes**
- the two notes under the return summary in the wizard (resources), and the "return approved for credit" e-mail;
- the e-mail token `%ReturnRequest.ReturnedToAllowance%` becomes `%ReturnRequest.ReturnedToSubsidy%`;
- the `returnedToAllowance` parameter becomes `returnedToSubsidy` (interface and implementations);
- the comments;
- plugin version, so the resources are updated.

Edits are given bottom to top within each file, so the line numbers above an edit stay right. Replace each whole line
shown.

---

## Change 1 — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

**Line 2045** — replace:

```
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.SubsidyReturned"] = "If this item was paid for with your allowance, that value is returned to your allowance when we approve the return, so the amounts above may be higher than what you actually receive as store credit.",
```

with:

```
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.SubsidyReturned"] = "If this item was paid for with your subsidy, that value is returned to your subsidy when we approve the return, so the amounts above may be higher than what you actually receive as store credit.",
```

**Line 2042** — replace:

```
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.NegativeAmount"] = "<strong>(*)</strong> A <em>negative</em> amount is in <strong>your favor</strong>. Anything you paid for with your allowance goes back to your allowance; only what you paid by card or cash comes back as store credit.",
```

with:

```
            ["Plugins.CW.Core.Exchanges.Summary.Disclaimer.NegativeAmount"] = "<strong>(*)</strong> A <em>negative</em> amount is in <strong>your favor</strong>. Anything you paid for with your subsidy goes back to your subsidy; only what you paid by card or cash comes back as store credit.",
```

**Line 268** — replace:

```
                "Sent to the customer when a return is authorised without a replacement: the store credit raised for it, and whether what their allowance paid was given back.");
```

with:

```
                "Sent to the customer when a return is authorised without a replacement: the store credit raised for it, and whether what their subsidy paid was given back.");
```

**Line 259** — replace:

```
                       "%if (%ReturnRequest.ReturnedToAllowance%) What your allowance paid for this item has been given back to your allowance.<br /> endif%" +
```

with:

```
                       "%if (%ReturnRequest.ReturnedToSubsidy%) What your subsidy paid for this item has been given back to your subsidy.<br /> endif%" +
```

## Change 2 — `Plugins/Nop.Plugin.CW.Core/plugin.json`, line 5

Replace:

```
  "Version": "2026_11_16_DATA_0.2",
```

with:

```
  "Version": "2026_11_16_DATA_0.3",
```

## Change 3 — `Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`

**Line 807** — replace:

```
        tokens.Add(new Token("ReturnRequest.ReturnedToAllowance", returnedToAllowance));
```

with:

```
        tokens.Add(new Token("ReturnRequest.ReturnedToSubsidy", returnedToSubsidy));
```

**Line 801** — replace:

```
    public async Task AddReturnCreditTokensAsync(IList<Token> tokens, decimal creditNoteAmount, bool returnedToAllowance)
```

with:

```
    public async Task AddReturnCreditTokensAsync(IList<Token> tokens, decimal creditNoteAmount, bool returnedToSubsidy)
```

**Line 799** — replace:

```
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
```

with:

```
    /// <param name="returnedToSubsidy">Whether what the subsidy paid for the item was given back</param>
```

**Lines 794–795** — replace:

```
    /// Adds the tokens of a return authorised without a replacement: the store credit raised and whether the allowance
    /// was given back
```

with:

```
    /// Adds the tokens of a return authorised without a replacement: the store credit raised and whether the subsidy
    /// was given back
```

## Change 4 — `Plugins/Nop.Plugin.CW.Core/Services/Messages/IMessageTokenProviderOverride.cs`

**Line 42** — replace:

```
    Task AddReturnCreditTokensAsync(IList<Token> tokens, decimal creditNoteAmount, bool returnedToAllowance);
```

with:

```
    Task AddReturnCreditTokensAsync(IList<Token> tokens, decimal creditNoteAmount, bool returnedToSubsidy);
```

**Line 40** — replace:

```
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
```

with:

```
    /// <param name="returnedToSubsidy">Whether what the subsidy paid for the item was given back</param>
```

**Lines 35–36** — replace:

```
    /// Adds the tokens of a return authorised without a replacement: the store credit raised and whether the allowance
    /// was given back
```

with:

```
    /// Adds the tokens of a return authorised without a replacement: the store credit raised and whether the subsidy
    /// was given back
```

## Change 5 — `Plugins/Nop.Plugin.CW.Core/Services/Messages/IExchangeWorkflowMessageService.cs`

**Line 48** — replace:

```
    Task<IList<int>> SendReturnRequestCreditCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, decimal creditNoteAmount, bool returnedToAllowance);
```

with:

```
    Task<IList<int>> SendReturnRequestCreditCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, decimal creditNoteAmount, bool returnedToSubsidy);
```

**Line 46** — replace:

```
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
```

with:

```
    /// <param name="returnedToSubsidy">Whether what the subsidy paid for the item was given back</param>
```

**Lines 40–41** — replace:

```
    /// Sends a notification when a Return Request is authorised without a replacement: the store credit raised and the
    /// allowance given back
```

with:

```
    /// Sends a notification when a Return Request is authorised without a replacement: the store credit raised and the
    /// subsidy given back
```

## Change 6 — `Plugins/Nop.Plugin.CW.Core/Services/Messages/ExchangeWorkflowMessageService.cs`

**Line 302** — replace:

```
            tokens => _messageTokenProviderOverride.AddReturnCreditTokensAsync(tokens, creditNoteAmount, returnedToAllowance));
```

with:

```
            tokens => _messageTokenProviderOverride.AddReturnCreditTokensAsync(tokens, creditNoteAmount, returnedToSubsidy));
```

**Line 299** — replace:

```
        decimal creditNoteAmount, bool returnedToAllowance)
```

with:

```
        decimal creditNoteAmount, bool returnedToSubsidy)
```

**Line 296** — replace:

```
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
```

with:

```
    /// <param name="returnedToSubsidy">Whether what the subsidy paid for the item was given back</param>
```

**Lines 290–291** — replace:

```
    /// Sends a notification when a Return Request is authorised without a replacement: the store credit raised and the
    /// allowance given back
```

with:

```
    /// Sends a notification when a Return Request is authorised without a replacement: the store credit raised and the
    /// subsidy given back
```

## Change 7 — `Plugins/Nop.Plugin.CW.Core/BusinessLogic/ExchangesLogic.cs`

**Line 345** — replace:

```
                creditNoteAmount, returnedToAllowance);
```

with:

```
                creditNoteAmount, returnedToSubsidy);
```

**Lines 338–339** — replace:

```
        //nothing came back (the fees took the whole note and no allowance was paid): there is nothing to tell
        if (creditNoteAmount <= decimal.Zero && !returnedToAllowance)
```

with:

```
        //nothing came back (the fees took the whole note and no subsidy was paid): there is nothing to tell
        if (creditNoteAmount <= decimal.Zero && !returnedToSubsidy)
```

**Line 336** — replace:

```
        OrderItem orderItem, decimal creditNoteAmount, bool returnedToAllowance)
```

with:

```
        OrderItem orderItem, decimal creditNoteAmount, bool returnedToSubsidy)
```

**Line 334** — replace:

```
    /// <param name="returnedToAllowance">Whether subsidy went back to the employee's allowance</param>
```

with:

```
    /// <param name="returnedToSubsidy">Whether what the subsidy paid for the item was given back</param>
```

**Line 327** — replace:

```
    /// The credit note and the restored allowance are already written when this runs, so a failure here is logged and
```

with:

```
    /// The credit note and the restored subsidy are already written when this runs, so a failure here is logged and
```

**Line 323** — replace:

```
    /// allowance given back, or both; nothing is sent when nothing came back. The id of the queued e-mail is kept so an
```

with:

```
    /// subsidy given back, or both; nothing is sent when nothing came back. The id of the queued e-mail is kept so an
```

## Change 8 — `Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

**Line 724** — replace:

```
            eventMessage.AdditionalTokens.Add("%ReturnRequest.ReturnedToAllowance%");
```

with:

```
            eventMessage.AdditionalTokens.Add("%ReturnRequest.ReturnedToSubsidy%");
```

**Line 719** — replace:

```
        //A return authorised without a replacement: the store credit raised and the allowance given back
```

with:

```
        //A return authorised without a replacement: the store credit raised and the subsidy given back
```

## Change 9 — `Plugins/Nop.Plugin.CW.Core/Services/Messages/CWTokenGroupNames.cs`, lines 34–35

Replace:

```
    /// Represents the tokens of a return authorised without a replacement: the store credit raised and the allowance
    /// given back
```

with:

```
    /// Represents the tokens of a return authorised without a replacement: the store credit raised and the subsidy
    /// given back
```

## Change 10 — `Plugins/Nop.Plugin.CW.Core/Domain/Messages/CWMessageTemplateSystemNames.cs`, line 18

Replace:

```
    /// allowance given back
```

with:

```
    /// subsidy given back
```

## Change 11 — `Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`, lines 169 and 128

The same line twice. Replace **both** occurrences of:

```
        //language and without nop's tax suffix (an allowance, not a price)
```

with:

```
        //language and without nop's tax suffix (a subsidy, not a price)
```

## Change 12 — `Plugins/Nop.Plugin.CW.Core/Services/Subsidies/SubsidyService.cs`

**Line 1117** — replace:

```
        //the allowance and its history row are saved together or not at all, as in SaveSubsidyTopupForPeriodAsync
```

with:

```
        //the top-up and its history row are saved together or not at all, as in SaveSubsidyTopupForPeriodAsync
```

**Line 1067** — replace:

```
    /// Records an addition to a packname's product-group allowance for the current ordering period.
```

with:

```
    /// Records a top-up of a packname's product-group subsidy for the current ordering period.
```

## Change 13 — `Plugins/Nop.Plugin.CW.Core/Controllers/PackNameController.cs`

**Line 972** — replace:

```
        //permanently delete an employee's packname and its allowance history.
```

with:

```
        //permanently delete an employee's packname and its subsidy history.
```

**Line 520** — replace:

```
    /// against that employee's allowance - no interaction, no token. The four
```

with:

```
    /// against that employee's subsidy - no interaction, no token. The four
```

---

## One-time step on each existing database

The "return approved for credit" e-mail is installed **once** (a store that edited it keeps its wording), so the plugin
update does not touch the copy already in the database, which still says "allowance" and uses the old token. Nothing
is in production, so no migration code is added: on each developer database, **before** running the plugin update,
delete the template *ReturnAuthorisedCredit.CustomerNotification* in *Content management › Message templates*. The
update (Change 2) then installs it again with the new wording, and its description with it.

## Test

1. Build; `dotnet test Tests/CW.Tests` stays green (no test reads the renamed token or parameter).
2. Delete the template (above), start the site, run the plugin update (*Configuration › Local plugins*, CW Core).
3. `grep -rni allowance Plugins/Nop.Plugin.CW.* CWLibraries Tests/CW.Tests Themes/CW.Standard` finds nothing.
4. *Content management › Message templates › ReturnAuthorisedCredit.CustomerNotification*: the body says "What your
   subsidy paid…" and uses `%ReturnRequest.ReturnedToSubsidy%`; the token appears in the template's token list.
5. As a packname customer, start a return and reach the summary step: both notes say "subsidy".
6. Authorise a credit return of an item paid by subsidy: the queued e-mail says "What your subsidy paid for this item
   has been given back to your subsidy."
