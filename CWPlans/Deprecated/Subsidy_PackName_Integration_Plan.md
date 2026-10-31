# Plan — Incorporate Subsidy / PackName Audit Findings into CW.Core (4.9)

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

**Project:** NopCommerce 4.9 Migration — `Nop.Plugin.CW.Core`
**Scope:** Wire the CreditNotes / Exchanges / ReturnRequest flows to the (work-in-progress) Subsidy and PackName subsystems, per the audit.
**Status:** Plan only — no code changed yet. (Updated with pre-flight results.)

---

## Subsidy WIP strategy (applies to all subsidy-boundary calls)

When a call that performs an action inside the subsidy boundary is needed, create the method and:

- **If it's clear what the method does** — implement it and add:
  `//TODO: check subsidy logic for exchanges integration`
- **If the implementation is unclear** — leave the method empty, assume it executed successfully, and add:
  `//TODO: add subsidy logic for exchanges integration`

**No new tables or persistent entities.** If persistence becomes unavoidable, alert before implementing. Creating DTOs / models is permitted.

### PackName WIP strategy (same convention, packname wording)

The same convention applies to packname-boundary calls:

- **If it's clear what the method does** — implement it and add:
  `//TODO: check packname logic for exchanges integration`
- **If the implementation is unclear** — leave the method empty, assume success, and add:
  `//TODO: add packname logic for exchanges integration`

Same rules: no new tables/persistent entities (alert first); DTOs/models allowed. Steps 2–4 used clear implementations, so each packname boundary call carries the `check` TODO.

---

## Credit application priority rules (authoritative — from the legacy audit)

These rules are the behavioural spec every step below must honour.

### A. When placing an order with BOTH credits available
1. **Credit Notes are applied FIRST** — they reduce the payable amount first and are the first stored balance consumed.
2. **Subsidy is applied SECOND** — only to the remainder left after credit notes.
- This holds in both the cart-total calculation and the actual balance consumption at placement (they agree). Subsidy is always computed on the post-credit-note total.

### B. When a return/exchange returns money to the customer
- **Customer-favour surplus** (replacement cheaper than the returned item, non-exact exchange) → **always credited as a Credit Note**, never subsidy. Mechanism: the full returned-item value is credited as a Credit Note; the (cheaper) exchange order consumes part of it; the leftover remains as Credit Note balance.
- **Originally-spent subsidy** on the returned order → **refunded back to Subsidy**, and only for **exchange-mode** returns, in the second pathway, capped at `min(remainingOrderSubsidy, exchangeSubtotal − fees)`. This is distinct from the surplus.
- **On return/exchange orders:** credit notes DO apply; **subsidy consumption is skipped** (`isReturnOrder` guard). Subsidy is only ever *refunded* on returns, never *spent*.

### C. Ordering of actions within the approval workflow
- Credit-note branch executes **first**, exchange branch (which performs the subsidy refund) executes **second**. They are independent but effectively mutually exclusive per return via the `ExchangeMode` gate (CreditNote-mode → credit note only; Exchange-mode → exchange order + possible subsidy refund + the forced credit note for the full returned value).

---

## Pre-flight results (verified against 4.9 code)

1. **PackName read/create exists.** `ICWOrderService` already exposes `GetPackNameOrderItemAsync(orderItemId)` and `InsertPackNameOrderItemAsync(...)` over the existing `CORE_PN_PackNameOrderItem` table; `OrderProcessingOverrideService` already uses them on normal placement. => Tier 2 needs **no new methods and no new tables**. An `OrderProcessingOverrideService` also exists (pattern to mirror for the exchange path).
2. **Total-calc ordering is a separate path.** The general cart total runs credit notes then exchange fees (`ShoppingCartAdditionalCalculationsAsync`); the 4.9 subsidy is a separate packname-cart computation (`CalculateSubsidyTotalAsync`), not chained after credit notes. => "Credit-notes-before-subsidy" cannot be enforced by a simple reorder; it depends on the WIP subsidy total netting off credit notes. Folded into the subsidy tier as a TODO (Step 6a).
3. **Combined page confirmed missing, with a ready hook.** The store-credit page is credits-only; `StoreCreditsModel.cs:9` already has `// TODO(4.9 migration): SubsidySectionModel Subsidy ...`. => Step 10 has a clean insertion point.

**No new tables are required anywhere in this plan.**

---

## Cross-cutting Step 0 — Per-store "active" gate (DOCUMENTED ONLY; setting does not exist yet)

- **Requirement:** the Exchanges logic and the Credit Notes logic must each execute **only when enabled for the store** — `isExchangeActive` gates the exchange/return workflow; `isCreditNoteActive` gates credit-note creation/application.
- **Gate points (where the guard will go once the setting exists):**
  - Credit Notes (`isCreditNoteActive`): `CreditNotesService.ApplyCreditNotesForOrderAsync`, `ExchangesLogic.ExecuteCreditNoteWorkflowAsync`, and the `EventConsumer` credit-note hooks (OrderPlaced apply / cancel undo).
  - Exchanges (`isExchangeActive`): `ExchangesLogic.ProcessReturnRequestStatusChangeAsync`, `ReturnsController.EditExchangeRequest` exchange path, and the customer return wizard entry.
- **Status:** the setting does not yet exist. **This step is included in the plan only** — no implementation now. When a per-store setting (`ISettings` with `IsExchangeActive` / `IsCreditNoteActive`, loaded per `storeScope`) is created, add the guards at the points above (early-return / no-op when disabled).
- **Placement:** conceptually first (foundational), but blocked on the setting; do not implement until the setting lands.

---

## Tier 1 — Clear, isolated, no subsidy-model dependency

### Step 1 — Fix the Credit-Note payment-method label (incl. the Subsidy-combined name) — DONE
- **Finding:** S3. `CreditNotesService.ApplyCreditNotesForOrderAsync` (~line 135) guards on an impossible condition `IsNullOrEmpty(x) && x.Contains("CreditNote")`, so the label is never set; and the legacy `"Subsidy"` -> `"Subsidy & CreditNote"` combine is missing.
- **Do:** correct the condition so an empty payment method becomes the credit-note method, and when the existing method is `"Subsidy"` set `"Subsidy & CreditNote"`.
- **Files:** `Services/CreditNotes/CreditNotesService.cs`. No subsidy call, no DTO, no table.
- **Dependencies:** none. Clearest — do first.
- **Status:** implemented. Payment-method names extracted to constants in `Constants/CWPaymentMethodSystemNames.cs` (`CREDIT_NOTE`, `SUBSIDY`, `SUBSIDY_AND_CREDIT_NOTE`).

---

## Tier 2 — PackName carry-through (fully implementable; existing entities & methods)

> Use the existing `ICWOrderService.GetPackNameOrderItemAsync` / `InsertPackNameOrderItemAsync` over the existing `PackNameOrderItem` table. No new methods, no new tables.

### Step 2 — Un-stub packname display on return/exchange screens — DONE
- **Finding:** P5. `ReturnRequestItemModelFactory.GetPackname` currently returns `"N/A"`.
- **Do:** read the real packname via `ICWOrderService.GetPackNameOrderItemAsync(returnedOrderItemId)` and surface it. Read-only, lowest risk of this tier.
- **Files:** `Factories/ReturnRequestItemModelFactory.cs`.
- **Status:** implemented. Injected `ICWOrderService`; replaced the static stub with async `GetPacknameAsync`; both call sites awaited.

### Step 3 — Carry packname onto the placed exchange order item — DONE
- **Finding:** P4 (legacy `CopyPacknameDataToExchangeItem` in `PlaceReturnOrder`).
- **Do:** after the exchange order item is created, read the returned item's `PackNameOrderItem` and insert a clone against the new order item via `ICWOrderService.InsertPackNameOrderItemAsync`.
- **Files:** `Services/Orders/OrderProcessingOverrideService.cs` — the return-order placement was consolidated here (legacy `ExchangeOrderService` no longer exists). Method: `MoveShoppingCartItemsToOrderItemsAsync(details, order, returnRequestItem)`, reached via `PlaceReturnOrderAsync` -> `SaveReturnOrderDetailsAsync`.
- **Status:** implemented. Clone inserted right after `RelateOrderItemToReturnRequest`, sourced from `returnRequestItem.ReturnedOrderItemId`, copying all `PackNameOrderItem` fields; null-safe when the returned item has no packname.

### Step 4 — Carry packname through the exchange cart add — DONE
- **Finding:** P1/P2/P3 (legacy `GetPacknameDataFromReturnedItem` -> `AddToCartTIG(CartItemOptions.Packname)`).
- **Do:** have `RegisterApprovedExchangedItemsAsync` resolve the source packname and add the replacement via the packname-aware cart overload.
- **Files:** `BusinessLogic/ExchangesLogic.cs`.
- **Status:** implemented. **No `CartItemOptions` DTO needed** — 4.9 `IShoppingCartOverrideService` already has an `AddToCartAsync(..., int packNameId, ...)` overload that inserts the `PackNameCartItem`. Injected `IShoppingCartOverrideService` (was `IShoppingCartService`) + `ICWOrderService`; resolve the packname from `returnRequestItem.ReturnedOrderItemId` and call the `packNameId` overload (falls back to the plain add when the returned item had no packname). No table.

---

## Tier 3 — Subsidy boundary calls (WIP strategy applies)

> Structure the calls now; stub where the subsidy model can't yet express the action. No persistence.

### Step 5 — Subsidy "read" boundary: was subsidy applied to this order? — DONE
- **Finding:** precursor to S1 (legacy `GetSubsidyAmountForOrder`).
- **Do:** add e.g. `GetSubsidyAppliedToOrderAsync(int orderId)` — derivable from the existing `GetSubsidyTotalByOrderIdAsync`. Implementation is clear -> **implement it** and add `//TODO: check subsidy logic for exchanges integration`.
- **Files:** `Services/Subsidies/ISubsidyCreditService.cs` + impl. DTO only if needed; no table.
- **Status:** implemented. Returns `totalForStock + totalForShipping` (incl tax) from `GetSubsidyTotalByOrderIdAsync`; carries the `check` TODO.
- **Since:** gone — no caller was left; `GetSubsidyTotalByOrderIdAsync` and `vw_CORE_SUB_OrderSubsidyTotal` were removed by subsidy phase 6 (2026-10-02). Per-order subsidy is the order's settlement row, `CORE_PN_OrderSettlement`.

### Step 6 — Subsidy refund on return approval (the S1 gap) — DONE
- **Finding:** S1 — legacy refunds original spent subsidy in `ExecuteExchangeWorkflow`'s second pathway (see priority rule B).
- **Do:** add a boundary method, e.g. `RefundSubsidyForReturnAsync(order, returnRequest, maxRefundableAmount)`. Implementation is unclear (new model is insert-only, no reverse concept) -> **leave it empty, assume success**, add `//TODO: add subsidy logic for exchanges integration`. Wire the call into `ExchangesLogic.ExecuteExchangeWorkflowAsync` at the legacy point (exchange-mode, second pathway, guarded by Step 5's "order had subsidy"). The surplus still routes to a Credit Note (already correct) — this step only re-establishes the subsidy-refund seam.
- **Files:** `Services/Subsidies/*` (new stub method), `BusinessLogic/ExchangesLogic.cs`. No table (stubbed).
- **Dependencies:** Step 5.
- **Status:** implemented. Empty stub `RefundSubsidyForReturnAsync` (returns `Task.CompletedTask`, `add` TODO); wired in the second pathway after `SetExchangeRequestNeedsCompletionAsync`, guarded by `GetSubsidyAppliedToOrderAsync > 0`, `maxRefundableAmount = min(subsidyApplied, exchangeTotal.total − fees)`. Injected `ISubsidyCreditService` into `ExchangesLogic`.

### Step 6a — Credit-notes-before-subsidy netting in cart totals (TODO note) — DONE
- **Finding:** pre-flight #2 / priority rule A. The 4.9 subsidy total is a separate packname path and does not currently net off credit notes.
- **Do:** at the subsidy-total boundary (`OrderTotalCalculationOverrideService.CalculateSubsidyTotalAsync` / `PerformSubsidy...`), add `//TODO: check subsidy logic for exchanges integration` noting subsidy should be computed on the post-credit-note remainder. No behavioural change now — enforcement waits on the subsidy WIP.
- **Files:** `Services/Orders/OrderTotalCalculationOverrideService.cs`.
- **Status:** implemented. TODO + note added at the top of `CalculateSubsidyTotalAsync`; no behavioural change.

### Step 7 — Subsidy undo on order cancellation — DONE
- **Finding:** S7 — legacy reverses subsidy on `OrderCancelledEvent`; 4.9 has the credit-note undo-on-cancel but not the subsidy one.
- **Do:** add `UndoSubsidyForOrderAsync(int orderId)` — unclear -> **empty stub, assume success**, `//TODO: add subsidy logic for exchanges integration`. Call it from the existing cancel handler in `EventConsumer` alongside `UndoCreditNotesForOrderAsync`.
- **Files:** `Services/Subsidies/*` (stub), `Services/EventConsumer.cs`. No table.
- **Dependencies:** independent of 5/6 but same subsidy-boundary theme.
- **Status:** implemented. Empty stub `UndoSubsidyForOrderAsync` (`add` TODO); injected `ISubsidyCreditService` into `EventConsumer`; called in the `OrderStatusChangedEvent`→Cancelled handler right after `UndoCreditNotesForOrderAsync`.

---

## Tier 4 — Combined displays (most dependent; UI/reporting)

### Step 8 — Combined "Subsidy & Credits" customer page — DONE
- **Finding:** S2 — legacy `SubsidyAndCreditsForMe` merges subsidy + credit-note data; 4.9 store-credit page is credits-only (confirmed).
- **Do:** populate the pre-marked `StoreCreditsModel` subsidy section (see `StoreCreditsModel.cs:9` TODO) and render it on the store-credit page, reading subsidy balance/history via existing services. Model/DTO only.
- **Dependencies:** both subsystems' read surfaces; larger, UI-touching.
- **Status:** implemented. Added `StoreCreditsModel.SubsidySectionModel`/`SubsidyLineModel`; new DTO `Domain/Subsidies/DTOs/CustomerSubsidySummary.cs`; subsidy read boundary `ISubsidyReportingService.GetCustomerSubsidySummaryAsync` **stubbed empty** (`add` TODO → returns "no subsidy"); `CreditNotesController` injects `ISubsidyReportingService` and maps the summary; `CreditsForMe.cshtml` renders a subsidy balance + history section guarded by `HasSubsidy` (page restructured so subsidy shows independently of store credit). Locale keys `Plugins.CW.Core.Subsidy.*` added (install on plugin update). Section is hidden today because the stub returns no subsidy.

### Step 9 — Combined subsidy + credit-note admin ledger/balance — N/A (already covered)
- **Finding:** S5 — legacy `SubsidyController` merges credit-note balance/history into the subsidy ledger.
- **Investigation (2026-06-23):** the 4.9 admin subsidy area (`SubsidyController` + `Views/Subsidy/*`) is **configuration-only** (product groups, subsidies, packname allocations, variations, topups) — there is **no per-customer subsidy ledger/balance screen** to merge into (`SubsidyModelFactory` has no ledger/balance/credit-note concept). Meanwhile the admin **Customer edit page** (`Areas/Admin/Views/Customer/_CreateOrUpdate.cshtml`) already shows **both** a Subsidy card (`PackNameSubsidies` component, ~line 76) and a Credit Notes card (`_CreateOrUpdate.CreditNotes.cshtml`, ~line 82).
- **Decision:** the legacy intent (admin sees a customer's subsidy + credit-note data together) is already satisfied by the two adjacent cards. A single merged ledger would be **net-new UI**, outside the infer-from-legacy baseline. **Marked N/A — nothing built.** Revisit only if the team explicitly wants a combined ledger screen.

---

## Notes

- **No new tables anywhere in this plan.** The only place persistence would eventually be required is the *real* subsidy refund/undo logic (Steps 6–7) — but under the WIP strategy those stay stubbed, so nothing persists yet. Alert before implementing if a real subsidy refund later needs a table/entity.
- **CreditNotes <-> PackName:** nothing to do — no coupling exists in legacy.
- **Setting gate (Step 0)** is documented only until the per-store `isExchangeActive` / `isCreditNoteActive` setting is created.

---

## Validation checklist (Subsidy / PackName)

Manual test scenarios to validate the integration. Each item = set up data, perform the action, confirm the outcome.

### PackName
- [ ] Create an order with a packname → validate the `PackNameOrderItem` is inserted correctly (baseline; existing behaviour).
- [ ] Open a return/exchange whose returned item has a packname → validate the packname **displays** on the admin and customer screens (not `"N/A"`). *(Step 2 / P5)*
- [ ] Approve an **exchange** for an item that has a packname → validate the packname is **cloned onto the new exchange order item** (`PackNameOrderItem` created against the placed order item, all fields copied). *(Step 3 / P4)*
- [ ] Approve an exchange that adds a replacement to the customer's cart → validate the packname is **carried onto the cart item**. *(Step 4 / P1–P3)*
- [ ] Return/exchange an item that has **no** packname → validate no error; display shows `"N/A"` and no `PackNameOrderItem` is created.

### Credit Notes — payment-method label
- [ ] Place an order paid **entirely by credit notes** (no prior payment method) → validate `PaymentMethodSystemName = "CWCreditNote"`. *(Step 1)*
- [ ] Place an order whose payment method is `"Subsidy"` and credit notes are applied → validate it becomes `"Subsidy & CreditNote"`. *(Step 1)*
- [ ] Place an order with another payment method + credit notes applied → validate the label is left **unchanged**.
- [ ] Apply credit notes with `appliedCreditNotesTotal == 0` → validate the order is **not** re-saved / label untouched.

### Credit Notes — order total vs. balance consistency
- [ ] **Simple order** (goods + shipping + tax only) fully/partly paid by credit notes → validate the amount subtracted from `Order.OrderTotal` **equals** the amount consumed from credit-note `Remain` and the `CWCreditNoteHistory` amount. (Also confirm `OrderSubtotal*`/`OrderShipping*`/`OrderTax` remain gross; only `OrderTotal` is net.)
- [ ] **Complex order** with credit notes **plus** a gift card, redeemed reward points, a payment-method fee, and/or an order-level discount → validate `OrderTotal` reduction still equals the `Remain`/history consumption. **Known risk:** the two are computed from different bases — cart total (`totals.TotalAmount`, net of gift cards/reward points/fee/discount) reduces `OrderTotal`, while `ApplyCreditNotesForOrderAsync` consumes `Remain` from `OrderSubtotalExclTax + OrderShippingExclTax + OrderTax` (gross). They can diverge; verify or fix the base.
- [ ] Credit balance is **not** double-consumed on normal orders: confirm `Remain` is decremented exactly once (at `OrderPlacedEvent`), not also during cart-total calculation (the cart calc is read-only).
- [ ] **CONFIRMED BUG — return/exchange orders double-apply credit notes.** `OrderProcessingOverrideService.PlaceReturnOrderAsync` calls `ApplyCreditNotesForOrderAsync` directly (line ~1134) **and** publishes `OrderPlacedEvent` (line ~1149), which `EventConsumer` (`IConsumer<OrderPlacedEvent>`, handler ~line 402) handles by applying credit notes **again** → `Remain` drained ~2× and duplicate `CWCreditNoteHistory` rows. Fix: remove the explicit line-1134 call (rely on the event) **or** guard the event handler to skip return-derived orders. Validate paid-status logic (`CheckOrderStatusAsync`/`ProcessOrderPaidAsync` run before the event) still holds after the fix.

### Credit application priority (order placement)
- [ ] Customer with **both** credit-note balance and subsidy available places an order → validate **credit notes are applied first** and **subsidy only to the remainder** (rule A); confirm in both the displayed total and the consumed balances.
- [ ] Exchange where the **replacement is cheaper** than the returned item → validate the surplus is credited as a **Credit Note** (never subsidy). *(rule B)*
- [ ] **Return/exchange order** placement → validate **subsidy is NOT consumed** (only credit notes apply on return orders). *(rule B)*

### Subsidy (stubs now; full validation once the subsidy WIP lands)
- [ ] Approve an **exchange-mode** return where the original order **had subsidy** → validate the subsidy-refund seam is invoked (currently a no-op stub → confirm the approval still completes without error). *(Step 6)*
- [ ] Cancel an order that consumed subsidy → validate the subsidy-undo seam is invoked (stub → confirm cancel completes; credit-note undo still runs). *(Step 7)*
- [ ] Subsidy total should be computed on the **post-credit-note** remainder → validate once the netting is enabled. *(Step 6a)*
- [ ] (When real logic lands) validate refunded amount = `min(remainingOrderSubsidy, exchangeSubtotal − fees)` and a `Refund` history row is written.

### Per-store active gate (once the setting exists — Step 0)
- [ ] `isCreditNoteActive = false` for the store → validate credit-note creation/application is **skipped**.
- [ ] `isExchangeActive = false` for the store → validate the exchange/return workflow is **skipped**.

### Combined displays
- [ ] Customer "My Store Credit" page → validate the **subsidy section** renders alongside credit notes. *(Step 8)*
- [ ] Admin subsidy ledger/balance → validate **credit-note balance/history** is merged in. *(Step 9)*

---

## Suggested execution order (summary)

0. (Cross-cutting, deferred) Per-store active-setting gate — documented only until the setting exists.
1. Fix CN payment-method label (S3).
2. Un-stub packname display (P5).
3. Packname onto placed exchange order item (P4).
4. Packname through exchange cart add (P1/P2/P3).
5. Subsidy read boundary — order-had-subsidy (implement + check TODO).
6. Subsidy refund on return approval (stub + add TODO; wire seam).
6a. Credit-notes-before-subsidy netting TODO at the subsidy-total boundary.
7. Subsidy undo on order cancel (stub + add TODO; wire seam).
8. Combined customer "Subsidy & Credits" page (S2).
9. Combined admin subsidy+credit-note ledger (S5).
