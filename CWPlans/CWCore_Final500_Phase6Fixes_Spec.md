# CW.Core move to the final 5.00 — phase 6 fixes: the order and shipment e-mail templates

Found in phase 6 (`CWPlans/CWCore_Final500_Phase6_Plan.md`, E1), written 2026-10-31.

## The defect

4.9's `CWCorePlugin.InstallOrUpdateAsync` points nop's e-mail templates at CW's tokens on every install and update:

- `PointShipmentTemplatesAtShipmentAddressAsync` — the three shipment e-mails (*sent*, *delivered*, *ready for pickup*)
  print the shipment's own destination (`%Shipment.Shipping…%`) instead of the order's first address.
- `PointOrderTemplatesAtShippingDestinationsAsync` — the eight order e-mails replace nop's single *Shipping Address*
  block with `%Order.ShippingDestinations%`: one block per destination, with its packnames.

The beta port's phase 1 removed both calls on purpose ("the two methods stay in the file, unused, until phase 3
calls them again", `NOP50Beta/CWPlans/Deprecated/CWCore_Phase1_Spec.md` change 6b), because the tokens they point at
are filled by the message-token override, which came back in phase 3. Phase 3 brought the override back but not the
calls. So in 5.00 both methods are declared and never called, and on `testnop50` an order e-mail of a packname order
with three destinations (order 9) shows nop's *Shipping Address* with the first destination only and *Shipping Method:
Standard Delivery, Standard Delivery, Standard Delivery*, where 4.9 shows *Shipping Address (test test)* per
destination. The other deferrals of phases 1 and 2 (widgets, department mappings, *My Store Credit*) did come back.

nop 5.00's default templates still carry `%if (%Order.Shippable%) … endif%` (11 places in `InstallRequiredData.cs`)
and the `%Order.Shipping…%` tokens, and `MessageTokenProviderOverride` still adds `%Order.ShippingDestinations%`
(`AddOrderTokensAsync`) and the `%Shipment.Shipping…%` tokens (`AddShipmentTokensAsync`), so restoring the two calls
is the whole fix. Both methods are idempotent: a second run finds nothing to replace. They rewrite
`MessageTemplate.Body` only, so a body translated into another language keeps nop's block, as on 4.9.

Reviewed 2026-10-31 (independent review): no problem found. With the change 5.00's `InstallOrUpdateAsync` is
identical to 4.9's; `InstallAsync`, `UpdateAsync` and `UninstallAsync` already were; nop 5.00's eleven template
bodies are byte-identical to 4.9's; `AddShipmentTokensAsync` adds all sixteen `%Shipment.…%` tokens the shipment
method writes, and nop 5.00 calls both token methods for the order and shipment e-mails.

**Labels.** **REPLACE** — find the FIND block (whole lines, at the lines given) and put the REPLACE block in its place.

---

### 1. REPLACE — the install and update rewrite the templates again

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\CWCorePlugin.cs` — lines 89–91

Depends on: change 2 (so that the update runs on a site already installed). 3 lines become 5.

FIND:
```csharp
        await InstallActivityLogTypesAsync();
        await InstallReturnReceiptTemplateAsync();
        await InstallCustomerRegistrationTemplateAsync();
```

REPLACE WITH:
```csharp
        await InstallActivityLogTypesAsync();
        await InstallReturnReceiptTemplateAsync();
        await InstallCustomerRegistrationTemplateAsync();
        await PointShipmentTemplatesAtShipmentAddressAsync();
        await PointOrderTemplatesAtShippingDestinationsAsync();
```

### 2. REPLACE — plugin version, so the update runs on `testnop50`

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\plugin.json` — line 5

Depends on: nothing. `UpdateAsync` calls `InstallOrUpdateAsync` on every version change; `DATA` also reinstalls the
resources (none change here).

FIND:
```json
  "Version": "2026_10_30_DATA_0.2",
```

REPLACE WITH:
```json
  "Version": "2026_10_31_DATA_0.3",
```

---

## Checks after applying

1. Rebuild: 0 errors. `dotnet test Tests\CW.Tests --filter "FullyQualifiedName~CW.Tests.Core"`: 631 passing (no test
   changes: the install runs against the nop engine, which the unit tests do not start).
2. Restart the site: nop runs the plugin update by itself at startup when `plugin.json`'s version changes
   (`AppStartedConsumer` → `PluginService.UpdatePluginsAsync`). *Configuration → Local plugins* then shows
   `2026_10_31_DATA_0.3`.
3. Claude checks on `testnop50`:
   - **F1** *Content management → Message templates*: the eight order templates contain `%Order.ShippingDestinations%`
     and no `%if (%Order.Shippable%)`; the three shipment templates contain `%Shipment.ShippingAddress1%` and no
     `%Order.ShippingAddress1%`.
   - **F2** a new packname order to several destinations: its *order placed* e-mail has one *Shipping Address
     (packnames)* block per destination, as 4.9's does.
   - **F3** a shipment of that order to its second destination: the *shipped* e-mail names that destination's address.
