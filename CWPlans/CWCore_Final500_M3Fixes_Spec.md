# CW.Core move to the final 5.00 — M3 fixes: the return page and the long account name

Follows part M3 of `CWPlans/CWCore_Final500_Strategy.md`. Written 2026-10-30 against the files as committed after M2,
for the M3 findings Eduardo decided the same day (`CWCore_Final500_M3_Plan.md` §5):

| Finding | Decision | Changes |
|---|---|---|
| 1. With *Exchanges* on and no active reason or action rule, the return page shows required but empty lists, and the submit does nothing | Say so: the page shows a message instead of a form that cannot be sent | 1, 2, 3, 6 |
| 2. With *Exchanges* off, nop's return page offers only the actions with an active CW rule | Fix: with *Exchanges* off the page keeps nop's own lists, and the reason→action cascade is not wired | 3, 4 |
| 4. A first name over 1000 characters is cut by the *Add packname* form and refused by the Excel import | Both refuse: the form checks the account's text fields against their database limits, as the import does | 5 |

The registration's cut to 1000 characters (M2 changes 8–9) stays, as a backstop behind both checks.

**Labels.** **REPLACE** — find the FIND block (whole lines, at the lines given) and put the REPLACE block in its place.
Where one file takes several blocks they are listed bottom to top; apply them in the order lettered.

**How this spec was checked.** Every FIND block was cut from the file as committed and matched once, at the lines
given. Line numbers within a file are those of the file before any block of this spec is applied, because the blocks go
bottom to top.

---

### 1. REPLACE — the message's text

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\CWCorePlugin.cs` — line 1723

Depends on: change 2 (a new resource is installed only by the plugin update). 1 line becomes 2.

FIND:
```csharp
            ["Plugins.CW.Core.Exchanges.Wizard.Start.Step2"] = "<strong>Step 2 -</strong> Tell us why you are returning these items",
```

REPLACE WITH:
```csharp
            ["Plugins.CW.Core.Exchanges.Wizard.Start.Step2"] = "<strong>Step 2 -</strong> Tell us why you are returning these items",
            ["Plugins.CW.Core.Exchanges.Wizard.Start.ReturnsUnavailable"] = "Returns can't be requested online at the moment. Please contact us about returning items from this order.",
```

### 2. REPLACE — plugin version, so the update installs the new resource

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\plugin.json` — line 5

Depends on: nothing. After the build, run the plugin update (*Local plugins* → CW Core Plugin): the version contains
`DATA`, so `UpdateAsync` installs the resources.

FIND:
```json
  "Version": "2026_10_30_DATA_0.1",
```

REPLACE WITH:
```json
  "Version": "2026_10_30_DATA_0.2",
```

### 3. REPLACE (3a–3b) — the return page: the message, and the cascade only with *Exchanges* on

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Views\ReturnRequest\ReturnRequest.cshtml`

Depends on: changes 1 and 6 (the resource and the model's flag). Apply 3a, then 3b. 3a uses the `features` variable
the page declares at line 19.

**3a** — lines 240–248. The cascade calls `ExchangesController.GetActionsForReason`, which is closed while *Exchanges* is
off; with it off the page keeps nop's action list, which does not follow the reason. 9 lines become 15.

FIND:
```cshtml
    $(function () {
        cwExchangesGather.initReasonCascade({
            actionsForReasonUrl: '@Html.Raw(Url.Action("GetActionsForReason", "Exchanges"))',
            storeId: @currentStore.Id,
            reasonSelector: '.return-reasons',
            actionSelector: '.return-actions'
        });
        cwExchangesGather.submitOnce($('#return-start-form'));
    });
```

REPLACE WITH:
```cshtml
    $(function () {
        @*with Exchanges on, the store's rules decide a reason's actions; with it off the lists are nop's and fixed*@
        @if (features.Exchanges)
        {
            <text>
            cwExchangesGather.initReasonCascade({
                actionsForReasonUrl: '@Html.Raw(Url.Action("GetActionsForReason", "Exchanges"))',
                storeId: @currentStore.Id,
                reasonSelector: '.return-reasons',
                actionSelector: '.return-actions'
            });
            </text>
        }
        cwExchangesGather.submitOnce($('#return-start-form'));
    });
```

**3b** — lines 39–41. On the wizard, a store whose rules offer no reason or no action gets the message in place of
the form. A guest's withdrawal link (nop's form, `useWizard` false) keeps nop's form, which nop's post accepts without a
reason or action. 3 lines become 9.

FIND:
```cshtml
        }
        else
        {
```

REPLACE WITH:
```cshtml
        }
        else if (useWizard && Model.NoReturnOptions)
        {
            <div class="result">
                @T("Plugins.CW.Core.Exchanges.Wizard.Start.ReturnsUnavailable")
            </div>
        }
        else
        {
```

### 4. REPLACE — the page's reasons and actions follow the switch

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Factories\ReturnRequestOverrideModelFactory.cs` — lines 90–118

Depends on: change 6 (`NoReturnOptions`). With *Exchanges* off the reasons and actions are nop's (the base factory
fills both with every reason and action), and no reason is pre-selected, as on nop's page. With it on, nothing changes
except the new flag. 29 lines become 37.

FIND:
```csharp
            var store = await _storeContext.GetCurrentStoreAsync();

            //with Exchanges on, the reasons the store's rules offer (an inactive rule, or none, hides the reason)
            if (await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, store.Id))
            {
                var activeReasonIds = await _exchangeService.GetActiveReasonIdsAsync(store.Id);
                cwModel.AvailableReturnReasons = cwModel.AvailableReturnReasons.Where(reason => activeReasonIds.Contains(reason.Id)).ToList();
            }

            cwModel.ReturnReasonItems = cwModel.AvailableReturnReasons
                .Select(r => new SelectListItem { Text = r.Name, Value = r.Id.ToString() })
                .ToList();

            //the reason the page opens with (the first one when none is chosen yet), so its actions are listed from the start
            if (cwModel.AvailableReturnReasons.All(reason => reason.Id != cwModel.ReturnRequestReasonId))
                cwModel.ReturnRequestReasonId = cwModel.AvailableReturnReasons.FirstOrDefault()?.Id ?? 0;

            //return actions - same method the cascade calls, so both paths always agree
            cwModel.AvailableReturnActions = await (await _exchangeService.GetActiveActionsByReasonIdAsync(cwModel.ReturnRequestReasonId, store.Id))
                .SelectAwait(async returnAction => new SubmitReturnRequestModel.ReturnRequestActionModel
                {
                    Id = returnAction.Id,
                    Name = await _localizationService.GetLocalizedAsync(returnAction, x => x.Name)
                })
                .ToListAsync();

            cwModel.ReturnActionItems = cwModel.AvailableReturnActions
                .Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() })
                .ToList();
```

REPLACE WITH:
```csharp
            var store = await _storeContext.GetCurrentStoreAsync();

            //with Exchanges on, the store's rules decide the reasons and actions the page offers; with it off the page
            //keeps nop's lists, which the base factory filled
            if (await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, store.Id))
            {
                //an inactive rule, or none, hides the reason
                var activeReasonIds = await _exchangeService.GetActiveReasonIdsAsync(store.Id);
                cwModel.AvailableReturnReasons = cwModel.AvailableReturnReasons.Where(reason => activeReasonIds.Contains(reason.Id)).ToList();

                //the reason the page opens with (the first one when none is chosen yet), so its actions are listed from the start
                if (cwModel.AvailableReturnReasons.All(reason => reason.Id != cwModel.ReturnRequestReasonId))
                    cwModel.ReturnRequestReasonId = cwModel.AvailableReturnReasons.FirstOrDefault()?.Id ?? 0;

                //return actions - same method the cascade calls, so both paths always agree
                cwModel.AvailableReturnActions = await (await _exchangeService.GetActiveActionsByReasonIdAsync(cwModel.ReturnRequestReasonId, store.Id))
                    .SelectAwait(async returnAction => new SubmitReturnRequestModel.ReturnRequestActionModel
                    {
                        Id = returnAction.Id,
                        Name = await _localizationService.GetLocalizedAsync(returnAction, x => x.Name)
                    })
                    .ToListAsync();

                //nothing can be sent when no reason is offered, or when the store has no active action at all (a reason
                //without links offers every active action, so its own list is empty only then, or when its links all
                //point at inactive actions - the reason-0 call answers which)
                cwModel.NoReturnOptions = !cwModel.AvailableReturnReasons.Any()
                    || !cwModel.AvailableReturnActions.Any() && !(await _exchangeService.GetActiveActionsByReasonIdAsync(0, store.Id)).Any();
            }

            cwModel.ReturnReasonItems = cwModel.AvailableReturnReasons
                .Select(r => new SelectListItem { Text = r.Name, Value = r.Id.ToString() })
                .ToList();

            cwModel.ReturnActionItems = cwModel.AvailableReturnActions
                .Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() })
                .ToList();
```

Note on the last case: a store whose only active reason is linked solely to inactive actions, while other actions are
active, still shows the form with an empty action list for that reason. That is a configuration the admin page lets
someone make; the flag does not cover it, and changing the reason (the cascade) shows the other reasons' actions.

### 5. REPLACE (5a–5c) — the *Add packname* form checks the account's lengths

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Validators\PackNames\PackNameRegisterValidator.cs`

Depends on: nothing. nop's `CustomerInfoValidator` has no length rules, so the form passed a 1001-character first name
to the registration, which cut it (M2). The new rules come from nop's own helper (`SetDatabaseValidationRules<Customer>`,
the column sizes of the `Customer` mapping, 1000 for the name, address and contact fields), in a validator nested in this
one: it is private, so nop's validator scan (`AddValidatorsFromAssemblies`, public types only) never registers it for
nop's own customer pages. The fields the registration fills itself (`Company`, `VatNumber`) are skipped, as the
controller skips them. Errors show under each field (`Customer.FirstName` …), in FluentValidation's length message.
Apply 5a, then 5b, then 5c.

**5a** — lines 47–50. The nested validator. 4 lines become 16.

FIND:
```csharp
        //the lengths the database allows for the packname's text fields
        SetDatabaseValidationRules<PackName>();
    }
}
```

REPLACE WITH:
```csharp
        //the lengths the database allows for the packname's text fields
        SetDatabaseValidationRules<PackName>();
    }

    /// <summary>
    /// The lengths the database allows for the new account's text fields, as the Excel import checks them. Private, so
    /// nop's validator scan does not register it for nop's own customer pages
    /// </summary>
    private sealed class AccountLengthValidator : BaseNopValidator<CustomerInfoModel>
    {
        public AccountLengthValidator()
        {
            SetDatabaseValidationRules<Customer>(PackNameServicesDefaults.ServerSuppliedCustomerFields);
        }
    }
}
```

**5b** — line 36. Apply the lengths with nop's account rules. 1 line becomes 2.

FIND:
```csharp
            RuleFor(model => model.Customer).SetValidator(new CustomerInfoValidator(localizationService, stateProvinceService, customerSettings, taxSettings));
```

REPLACE WITH:
```csharp
            RuleFor(model => model.Customer).SetValidator(new CustomerInfoValidator(localizationService, stateProvinceService, customerSettings, taxSettings));
            RuleFor(model => model.Customer).SetValidator(new AccountLengthValidator());
```

**5c** — lines 6–9. The two namespaces the nested validator uses. 4 lines become 6.

FIND:
```csharp
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
```

REPLACE WITH:
```csharp
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using Nop.Web.Models.Customer;
```

### 6. REPLACE — the model's flag

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Models\ReturnRequests\CWSubmitReturnRequestModel.cs` — lines 34–39

Depends on: nothing. 6 lines become 11.

FIND:
```csharp
    /// <summary>
    /// Return actions for the currently selected reason, ready to bind to a dropdown
    /// </summary>
    public IList<SelectListItem> ReturnActionItems { get; set; } = new List<SelectListItem>();

    #endregion
```

REPLACE WITH:
```csharp
    /// <summary>
    /// Return actions for the currently selected reason, ready to bind to a dropdown
    /// </summary>
    public IList<SelectListItem> ReturnActionItems { get; set; } = new List<SelectListItem>();

    /// <summary>
    /// Whether the store's return rules offer no reason or no action, so no return can be requested on the page
    /// </summary>
    public bool NoReturnOptions { get; set; }

    #endregion
```

---

## Tests (Claude's, already in `Tests/CW.Tests/Core/Factories/ReturnRequestPageModelTests.cs`)

- `The_page_opens_with_the_first_reason_and_only_its_actions` and `A_reason_already_chosen_keeps_its_actions` described
  the *Exchanges* page but ran with the switch off; they now switch it on (with reasons 1 and 2 active).
- New: `With_exchanges_off_the_page_keeps_nops_reasons_and_actions` — both reasons, both actions, no reason pre-selected,
  and the exchange service never asked.
- New: `With_exchanges_on_and_no_active_reason_no_return_can_be_requested` and
  `With_exchanges_on_and_no_active_action_no_return_can_be_requested` — `NoReturnOptions` is set.

The key is `…Wizard.Start.ReturnsUnavailable`, not `…NoReturnOptions`: `Plugins.CW.Core.Exchanges.Wizard.NoReturnOptions`
already exists (a reason and action with no return mode). Reviewed 2026-10-30 (independent review): no problem found;
its two points taken (the guest's withdrawal link, the key's name). The form's length error is FluentValidation's
wording, the import's is its own `Import.Errors.TooLong`; accepted.

The validator change has no unit test: `SetDatabaseValidationRules` reads nop's mapping schema, which needs the nop
engine (as `ExtendedCustomerValidatorTests` notes). It is checked on the site (R3 below).

## Checks after applying

1. Rebuild the solution: 0 errors.
2. `dotnet test Tests\CW.Tests --filter "FullyQualifiedName~CW.Tests.Core"` — **631** passing (628 + 3 new).
3. Restart the site and run the plugin update (change 2); then, on `testnop50`:
   - **R1** (finding 1): on Manage Store → *Return Reason Rules*, set the three reasons inactive. As James, open
     `/returnrequest/8` (order 8, delivered, nothing returned yet): the page shows *Returns can't be requested online at
     the moment…* and no form. Set the reasons active again: the form is back.
   - **R2** (finding 2): switch *Exchanges* off for store 1. As James on `/returnrequest/8`: the actions list *Repair*,
     *Replacement* and *Store Credit* (nop's three), and changing the reason makes no request to `GetActionsForReason`.
     Switch *Exchanges* on again: the actions follow the reason (*Repair* is not offered).
   - **R3** (finding 4): as Steve, *Add packname* with an account and a 1001-character first name: the form shows a
     length error under *First name* and nothing is saved. 1000 characters: saved.
