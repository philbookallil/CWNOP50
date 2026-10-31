# CW.Core move to the final 5.00 — phase 7 fixes

Found in phase 7 (`CWPlans/CWCore_Final500_Phase7_Plan.md` §5), written 2026-10-31. All four defects are older than the
move: the same lines are in the 4.90 source and the beta port. None needs a plugin update. Change 1 needs a rebuild.
Changes 2–4 are Razor views and a log message, and the views compile at runtime.

**Labels.** **REPLACE** — find the FIND block (whole lines, at the lines given) and put the REPLACE block in its place.
Within a file, the changes are given bottom to top, so the line numbers of the ones still to apply stay right.

---

## The defects

| # | Where | What happens | Found in |
|---|---|---|---|
| 1 | *CW Admin → Branches* → a branch → *Addresses* → *Delete* | The grid posts `id` and `EntityId` (`_CreateOrUpdate.Addresses.cshtml:8`). `BranchController.AddressDelete(int id, int branchId)` binds `branchId`, which gets 0, so it throws *No branch found with the specified id*. The address is never deleted. Every other CW address grid posts `EntityId` to an action that takes `entityId` (`MultiStoreController.BranchAddressDelete`, `PackNameAddressDelete`, `CostCenterAddressDelete`) | 4a T12 |
| 2 | Activity log, *Manual Credit Note creation* | The comment reads *created with StoreId {credit note id} … for customer StoreId: {customer id}*. Both labels are wrong | 4d |
| 3 | Storefront *My account → Return requests*, a plain nop return (no CW exchange row) | The entry shows *Exchange Type:* (blank) and *Replacement Product: - x 0*. The view tests `ExchangeReturnMode != CreditNote`, and a plain request's mode is the default 0, so it passes. Only `Exchange` (1) has a replacement | 5g T22 |
| 4 | Packname checkout | `@T("Common.Loading")` is a key that neither nop 5.00 nor the plugin installs. The spinner's label reads *common.loading*, and every page load logs *Resource string (common.loading) is not found*. nop's own *Common.Wait* ("Wait...") exists | log, every group |

---

### 1. REPLACE — the branch address delete reads the id the grid sends

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Areas\Admin\Controllers\BranchController.cs` — lines 368–372

Depends on: nothing. The action name, route and permission do not change. Its only caller is the grid in
`Areas\Admin\Views\Branch\_CreateOrUpdate.Addresses.cshtml`, line 8, which already sends `EntityId`. 5 lines stay 5.

FIND:
```csharp
    public virtual async Task<IActionResult> AddressDelete(int id, int branchId)
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(branchId)
            ?? throw new ArgumentException("No branch found with the specified id", nameof(branchId));
```

REPLACE WITH:
```csharp
    public virtual async Task<IActionResult> AddressDelete(int id, int entityId)
    {
        //try to get a branch with the specified id (the grid posts it as EntityId)
        var branch = await _branchService.GetBranchByIdAsync(entityId)
            ?? throw new ArgumentException("No branch found with the specified id", nameof(entityId));
```

### 2. REPLACE — the manual credit note's activity comment names what it prints

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Services\CreditNotes\CreditNotesService.cs` — line 285

Depends on: nothing. Rows already in the activity log keep their old wording. 1 line stays 1.

FIND:
```csharp
            $"Manual Credit Note has been created with StoreId {creditNote.Id} and amount {amount} for customer StoreId: {editedCustomer.Id}",
```

REPLACE WITH:
```csharp
            $"Manual Credit Note has been created with Id {creditNote.Id} and amount {amount} for customer Id: {editedCustomer.Id}",
```

### 3. REPLACE — only an exchange shows the exchange block on the customer's return list

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Views\ReturnRequest\CustomerReturnRequests.cshtml` — line 125

Depends on: nothing. `ExchangeReturnMode` has two values, `Exchange = 1` and `CreditNote = 2`
(`Domain\Exchanges\ExchangeReturnMode.cs`). Every CW request carries one of them
(`ReturnRequestItemModelFactory.PrepareExchangeRequestModelAsync`, line 86). A plain nop request has no CW row, so its
mode stays 0. Exchanges and credit-note requests show as before. A plain request shows nop's lines only. The admin
views already test `== ExchangeReturnMode.Exchange` (`_CreateOrUpdate.FeesAndCharges.cshtml`). 1 line stays 1.

FIND:
```cshtml
                        if (exchangeRequestData.ExchangeReturnMode != ExchangeReturnMode.CreditNote)
```

REPLACE WITH:
```cshtml
                        if (exchangeRequestData.ExchangeReturnMode == ExchangeReturnMode.Exchange)
```

### 4. REPLACE — the packname checkout's spinner uses a resource that exists

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Views\PackNameCheckout\Checkout.cshtml` — line 27

Depends on: nothing. `Common.Wait` ("Wait...") is one of nop's own resources (`defaultResources.nopres.xml`), so no
resource install and no version change are needed. 1 line stays 1.

FIND:
```cshtml
        <div id="packname-checkout-loading" class="packname-checkout-loading" role="status" aria-label="@T("Common.Loading")">
```

REPLACE WITH:
```cshtml
        <div id="packname-checkout-loading" class="packname-checkout-loading" role="status" aria-label="@T("Common.Wait")">
```

---

## Checks after applying

1. Rebuild: 0 errors. `dotnet test Tests\CW.Tests --filter "FullyQualifiedName~CW.Tests.Core"`: 631 passing (no test
   touches these lines).
2. Restart the site, so the rebuilt controller loads.
3. Claude checks on `testnop50`:
   - **F1** *CW Admin → Branches* → Test Branch 1.2 → *Addresses*: delete address 50 (left there by 4a T12). The grid
     refreshes without it. No *No branch found* in *System → Log*.
   - **F2** add a manual credit note on a customer's *Credit Notes* card, then delete it. The activity log reads
     *… with Id {n} … for customer Id: {customer}*.
   - **F3** James's *Return requests* (return 5, a plain request from 5g T22): no *Exchange Type* or *Replacement
     Product* lines. Victoria's return 6 (credit note) and Steve's return 4 (exchange) show as before.
   - **F4** open the packname checkout as Steve: the spinner's `aria-label` is *Wait...*, and no *common.loading*
     warning appears in the log.

## Not fixed here (for Eduardo to decide)

- **Storefront access denied lands on *Page not found*.** `Controllers\PackNameController.cs` (lines 498, 512, 547,
  604, 632) and `Controllers\CustomerSubsidiesController.cs` (line 59) return `AccessDeniedView()`. That is nop's
  admin helper: it redirects to `/Security/AccessDenied`, which exists only in the admin area. A non-manager who opens
  `/PackName/ImportPackNames` gets a 404 page, and a 404 error is logged. The behaviour is the same in 4.9. Which
  storefront answer is right (`Challenge()`, a redirect home, or nop's access-denied page) is a design choice, so no
  change is proposed.
- **The packname checkout after its last line is removed.** Removing the last line in place leaves *Order summary*
  and *Checkout* on the page until a reload, which then shows *Your Shopping Cart is empty!*. This is minor, and it
  was not compared with 4.9.
