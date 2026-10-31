# Exchanges, Returns & Credit Notes — Reference (CW.Core)

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

**Status:** Current-state reference. Describes how Exchanges/Returns/Credit Notes work today — no
changelog, no history, no dates. Suitable for anyone (including a PM) who wants to understand how this
functionality behaves right now, and is one of the two inputs (with the companion document below) for
planning further work here.

**Companion document:** `CWPlans/Exchanges_Returns_CreditNotes_DevNotes.md` carries the changelog and the
design rationale behind decisions made in this area — developer-only, read it alongside this file when
planning new work. See §14 for how the two files work together going forward.

**Legacy reference:** `CWPlans/Deprecated/Legacy_NOP41_Exchanges_Subsidy_Reference.md` describes the original
nopCommerce 4.1 implementation this was ported from — the behaviour the business has relied on for ~10
years. Consult it before changing any rule, and especially before assuming something is missing by
design rather than lost in the port.

**Scope:** `Nop.Plugin.CW.Core` — the legacy "CW Exchange" plugin (originally built for nopCommerce 4.1)
that was merged into CW.Core and now runs on nopCommerce 5.00 / .NET 10. Covers three
tightly-coupled subsystems: **Return Requests / Exchanges**, **Credit Notes**, and their (partial)
integration with **Subsidies** and **PackNames**.

**Primary sources used to build this document:**
- Code under `Plugins/Nop.Plugin.CW.Core` (Domain, Data, Services, BusinessLogic, Controllers, Factories, Areas/Admin, Views) — read directly.
- `4 Exchange and Returns.docx` — original business/functional spec (2018).
- `4a Refunds and Changes.docx` — order cancellation/adjustment rules incl. subsidy interplay.
- `8c Subsidy Credit Note Scenarios.docx` — subsidy ↔ credit note interaction rules.
- `CWPlans/Deprecated/Subsidy_PackName_Integration_Plan.md` — the 4.9-migration plan that wired (partially) Exchanges/Credit Notes to Subsidy/PackName. Several steps in that plan are now implemented; this document reflects **current code state**, not the plan's aspirational state.
- `CWPlans/Feature_Toggles_Reference.md` — the per-store `CWFeature` gating system, which gates this whole area (§4).
- `CWPlans/subsidy-system.md` — **the canonical reference for the current Subsidy system.** Product groups, credit/quantity allocation, additive period-scoped topups, per-PackName variations, credit consumption order, caching. §9 below only summarises the slice of this system that Exchanges/Returns touches — treat `subsidy-system.md` as the source of truth for anything Subsidy-specific, not this document.
- `CWPlans/Deprecated/subsidy-catalog-spec.md` — implementation spec for the public-store Subsidy Catalog page; the manager "Change Topup" feature and the PackName identity strip / flyout are described in `CWPlans/subsidy-system.md` §8.1 and §8.7 (their specs were consumed on 2026-10-21). Cross-checked against §9 for consistency; no Exchanges/Returns/Credit Notes content lives in these.

**Important note on the business-spec docx files (§2 below):** they describe the **original 2018 legacy
Subsidy design** — a simpler model (one global credit pool + a loose "unit-based vs global" distinction,
no product groups, no topups, no per-PackName variation). The current Subsidy system is a **re-implementation,
not a port** — it has per-product-group quantity/credit allocation, additive period-scoped topups, global
credit that also covers shipping, and per-PackName credit/period overrides (`SubsidyVariation`). Where
§2/§9 reference the docx's concepts (e.g. "global vs unit-based subsidy"), read them as the *original
intent* that the current Product Group model fulfils in a more granular way — see §9.5 for the mapping.

---

## 1. At a glance

| | |
|---|---|
| **Legacy origin** | "CW Exchange" plugin, nopCommerce 4.1, ~2018 |
| **Current home** | `Nop.Plugin.CW.Core` (merged in, not a separate plugin) |
| **Table prefix** | `CORE_EX_*` (Exchanges/Returns/Credit Notes), `CORE_SUB_*` (Subsidy); `vw_CORE_EX_*` (Credit Note live-balance views) |
| **Feature toggles** | `CWFeature.Exchanges` (`MultiStoreSettings.ExchangesEnable`), `CWFeature.CreditNotes` (`MultiStoreSettings.CreditNotesEnable`) — per store, via `ICWFeatureService` |
| **Public entry point** | "Return Item(s)" button on the customer's Order page → CW's own multi-step wizard (replaces nop's built-in one-shot return form) |
| **Admin entry point** | Admin ▸ Orders ▸ Return Requests (nop's grid/edit page, CW-extended) + Admin ▸ CW Core ▸ Exchanges and Returns (rules/fees/reports) |
| **Credit Notes = store credit.** | Not a nopCommerce concept — entirely built by this plugin. |
| **Subsidy integration** | PackName carry-through is done. Restoration in kind is built (`RestoreSubsidyForReturnedItemAsync`, `CORE_SUB_SubsidyRestoreItem`); it runs on every authorised line and the replacement draws on it — see §9 and `CWPlans/Exchanges_Rebuild_Record.md` §3–§4 and §8. |
| **Jim2/ERP sync** | **Not implemented.** The sync points are a `// TODO` and a commented-out ERP-number line — see §10.1. |

---

## 2. What this is supposed to do (business spec summary)

### 2.1 Exchange & Returns (`4 Exchange and Returns.docx`, 2018)

- Nop's built-in Return Authorisation (RA) system is the starting point; CW extended it.
- Customer flow: Orders page → "Return Item(s)" → pick quantities per item → pick **Return Reason** → reason drives which **Return Actions** are available (configurable Reason→Action linking) → customer picks Exchange or Credit Note.
  - **Exchange** sub-choices: *Same style* (same product, different variant), *Different style* (any allocated product), *Exact* (identical replacement).
  - **Credit Note**: simple interface, shows a statement of fees that will apply.
- Fees: repackaging fee + redelivery fee, calculated from rules, waived if the return reason is "faulty" (rule-driven), staff can adjust quantities used to calculate fees within order-quantity limits.
- Admin (Sales Support): Returns list → Edit Return form (CW-extended) → change status → a set of confirmations get sent once status = "Authorised" → if Exchange, a **new order is generated automatically** and logged against the original RMA/return-request ID.
- Backend configuration needed (all store-scoped): Return Reason rules, Return Action rules, Return Status rules (rule-based: **all** flags across status/reason/action rules must be `true` for a capability — e.g. store credit or exchange — to activate), and a Fee table (repackaging/redelivery), ideally with a "template store" other stores can inherit from.
- Downstream: return statuses and credit notes were meant to sync to **Jim2** (accounting system) via XML email or API — **this integration has not been built in CW.Core** (see §10.1).

### 2.2 Order Cancellations & Adjustments (`4a Refunds and Changes.docx`, 2018)

Payment-method/status matrix for how a cancellation or same-priced alteration should be settled:

| Payment Type | Status | Settlement |
|---|---|---|
| Prepaid | Pending / Released | Credit Note |
| Prepaid | Sent | Return & Exchange process |
| Subsidy | Pending / Released | Subsidy (give it back) |
| Subsidy | Invoiced | Credit Note, issued **to the employee** |
| Subsidy | Sent | Return & Exchange process, issued **to the employee** |

Key rule: **if a subsidised order can still be cancelled before it's processed/invoiced, reallocate the
subsidy back to the customer; once invoiced, issue a Credit Note instead.** This was the origin of the
4.1-era `RefundSubsidyForReturnAsync` / `UndoSubsidyForOrderAsync` stubs, which no longer exist. The manager has
since ruled that invoicing changes nothing: subsidy always goes back to the pot (decision 20, §9.1).

### 2.3 Subsidy ↔ Credit Note scenarios (`8c Subsidy Credit Note Scenarios.docx`, 2018)

- **Global ($) subsidy** vs **Unit-based subsidy**: a global-value subsidy isn't tied to specific products, so a standard credit note can always represent it. A unit-based subsidy is tied to a specific product group — a credit note standing in for it must represent the *same group*, not just a dollar amount.
- **Revoke flag concept**: instead of deleting a consumed-subsidy record when it's reversed (cancel / major adjustment), *flag it as revoked* so the original data survives for audit/reporting, and the flagged row is ignored going forward. The flag can carry a sub-value (`"s"` = still a subsidy, `"c"` = now a credit note) that downstream JIM2 sync logic reads to decide what to send.
- **Unit-subsidy price differences on exchange**: if the new item differs in value from the returned item:
  - New item **costs more** → the difference is charged to the customer directly (not sent to accounts as a subsidy gap).
  - New item **costs less** → the surplus becomes a **Credit Note** — either to the employee (their "benefit"), or, via a store setting, to the Branch Manager/Head Office instead.
- Exchanges against a unit-based subsidy must be filtered to the **same product group**; items already paid for out-of-pocket (subsidy exhausted) can exchange against anything.
- This is explicitly the most complex area and was expected to need both CW (NopCommerce) and Happen Business (Jim2) development. **The Jim2 side was never built; the NopCommerce side is partially built (see §9).**
- **This spec describes the original, simpler 2018 subsidy design.** The subsidy system was later re-implemented with materially more capability (per-product-group quantity *and* credit allocation across multiple groups, additive period-scoped topups, global credit that also pays for shipping, per-PackName credit/period overrides) — see §9.5 for the full mapping from these 2018 concepts to the current implementation, and `CWPlans/subsidy-system.md` for the canonical current-state reference.

---

## 3. Architecture overview

```
                         ┌─────────────────────────────────────────────┐
                         │              Customer (Public Store)         │
                         └───────────────────────┬───────────────────────┘
                                                  │ "Return Item(s)" on Order page
                                                  ▼
                     ReturnsController / ExchangesController (public)
              Gather info → Style/replacement pick → Summary → Confirm
                                                  │
                      registers ReturnRequest (nop) + ReturnRequestItem (CW)
                                                  │
                         ┌────────────────────────┴────────────────────────┐
                         ▼                                                 ▼
              Admin ▸ Return Requests grid/edit           CustomerReturnRequests / CreditsForMe
              (ReturnsController, admin area)             (customer-facing status + store credit)
                         │ status changed to "Return Authorized"
                         ▼
              ExchangesLogic.ExecuteApprovedLineAsync (locked per return request)
                 ├─ subsidy back in kind  → SubsidyCreditService.RestoreSubsidyForReturnedItemAsync
                 ├─ credit note needed?   → ExecuteCreditNoteWorkflowAsync → CreditNotesService
                 └─ exchange needed?      → ExecuteExchangeWorkflowAsync
                        ├─ settlement quote covers it + no fees → OrderProcessingOverrideService.PlaceReturnOrderAsync
                        │        (auto-places a new order, settled through the settlement pipeline)
                        └─ balance or fees due → add replacement to customer's cart
                                 (needs completion — customer must check out)
```

Everything lives inside `Nop.Plugin.CW.Core` — there is no longer a separate Exchange plugin assembly.
The main code groupings:

| Area | Path |
|---|---|
| Domain entities | `Domain/CreditNotes/*`, `Domain/Exchanges/*`, `Domain/Returns/*` (the return case) |
| DB mapping (table names) | `Data/Mapping/CWNameCompatibility.cs`, `Data/Mapping/CreditNotes/*`, `Data/Mapping/Exchanges/*`, `Data/Mapping/Returns/*` |
| Migrations | `Data/Migrations/SchemaMigration.cs` — one schema migration for all of CW.Core's tables and indexes |
| Services | `Services/CreditNotes/*`, `Services/Exchanges/*`, `Services/Returns/*`, `Services/Settlement/*` (the settlement pipeline), `Services/Orders/CWReturnRequestService.cs`, `Services/Orders/OrderProcessingOverrideService.cs` (return-order placement lives here) |
| Cross-cutting workflow ("business logic") | `BusinessLogic/ExchangesLogic.cs`, `BusinessLogic/ReturnSubmissionLogic.cs` (the wizard's checks and registration), `BusinessLogic/ReturnResolutionLogic.cs` (the case resolution) |
| Public controllers | `Controllers/ExchangesController.cs`, `Controllers/CreditNotesController.cs` |
| Filters | `Filters/ReturnRequestSubmitFilter.cs` (nop's own return post), `Filters/ReturnRequestAdminFilter.cs` |
| Admin controllers | `Areas/Admin/Controllers/ReturnsController.cs`, `ExchangesConfigController.cs`, `CreditNotesController.cs` |
| Admin factories | `Areas/Admin/Factories/CreditNotesModelFactory.cs`, `ExchangeSettingsModelFactory.cs`, `ReturnRequestExchangeModelFactory.cs`, `ReturnRequestOverrideModelFactory.cs` |
| Public factories | `Factories/ExchangesModelFactory.cs`, `Factories/ReturnRequestItemModelFactory.cs`, `Factories/ReturnRequestOverrideModelFactory.cs` |
| View components (widgets) | `Components/CreditNotesWidgetsViewComponent.cs`, `Components/ExchangesWidgetsViewComponent.cs`, `Components/ReturnRequestsConfirmationsViewComponent.cs`, `Areas/Admin/Components/OrderReturnRequestsViewComponent.cs`, `Areas/Admin/Components/ExchangeSettingsComponent.cs`, `Areas/Admin/Components/OrderListBadgesViewComponent.cs` |
| Views (public) | `Views/Exchanges/*`, `Views/ReturnRequest/*`, `Views/CreditNotes/CreditsForMe.cshtml`, `Views/Shared/Components/{CreditNotesWidgets,ExchangesWidgets,ReturnRequestsConfirmations}/*` |
| Views (admin) | `Areas/Admin/Views/ReturnRequest/*`, `Areas/Admin/Views/Exchanges/*`, `Areas/Admin/Views/CreditNotes/MainReport.cshtml`, `Areas/Admin/Views/Shared/Components/ExchangeSettings/*` |
| Credit-note settlement | `Services/Settlement/CreditNoteSettlementSource.cs` — credit notes are applied as the pipeline's second source when an order settles (`OrderProcessingOverrideService.SettleOrderAsync`); no event consumer applies them |

---

## 4. Feature toggles (per-store on/off)

Two independent `CWFeature` flags gate this whole area (see `CWPlans/Feature_Toggles_Reference.md` for
the full, authoritative reference — this is a summary):

- **`CWFeature.Exchanges`** ← `MultiStoreSettings.ExchangesEnable` — gates the CW return/exchange wizard (replaces nop's stock return form when on).
- **`CWFeature.CreditNotes`** ← `MultiStoreSettings.CreditNotesEnable` — gates store-credit issuance/consumption.

Rule of thumb baked into the code: **gate new work (starting a new wizard, applying credit on a *new*
order, issuing a *new* manual credit note), never gate finishing/displaying work that already exists**
(an in-flight return, an existing balance, undoing something on cancel must always run regardless of the
current setting). Concretely:

- Credit-note settlement on a new order — `CreditNoteSettlementSource.IsEnabledAsync` — is on when
  `IsEnabledForOrderAsync(CWFeature.CreditNotes, order)` is true **or the customer still holds a balance**
  (`CustomerHasCreditNotesBalanceAsync`): switching the feature off stops new notes, not the spending of a note the
  store already owes.
- Order cancellation needs no code and so no gate: the balance views exclude cancelled orders (§6.1, §9.6), so an
  old order's money comes back regardless of today's setting.
- `ExchangesLogic.ExecuteExchangeWorkflowAsync`'s credit-notes-balance check — **not** gated (completing an in-flight exchange).
- Admin `CreditNotesController.SaveCreditNote` / `DeleteCreditNote` — gated via `ICWFeatureGuard.BlockIfDisabledAsync` (new/destructive write).
- Admin list/report/history actions on `CreditNotesController` (balances owed) — **deliberately ungated**, they show money the store owes regardless of the toggle.
- `ExchangesConfigController.GetOrderReturnRequests` — deliberately ungated (pure read of requests that already exist).

---

## 5. Data model

### 5.1 Credit Notes

| Entity | Table | Purpose |
|---|---|---|
| `CreditNote` | `CORE_EX_CreditNote` | A store-credit balance: `CustomerId`, `OrderId` (source order), `Amount` (original), `ReturnReason`, `StoreId`, `ReturnRequestId` (nullable — null for manually-created notes), `CreatedByCustomerId` (nullable — who issued it; null for automatic return-driven notes) |
| `CreditNoteHistory` | `CORE_EX_CreditNoteHistory` | One row per consumption event, **permanent and insert-only, never updated or deleted**: `CreditNoteId`, `CustomerId`, `OrderId` (the order the credit was spent on), `Amount` (spent) |

`CreditNote.Remain` and `CreditNoteHistory.Remain` are not stored columns — both are `[NotColumn]`
properties computed live from `vw_CORE_EX_CreditNoteRemain` / `vw_CORE_EX_CreditNoteHistoryRemain`
(Amount minus every history row belonging to a non-cancelled order), the same live-view pattern Subsidy
uses for its own balances (§5.4).

Credit Notes are a **FIFO stack**: `CalculateAppliedCreditNotesAsync` always consumes the oldest note
(`OrderBy(c => c.CreatedOnUtc)`) first, spilling into the next when one is exhausted.

### 5.2 Exchanges / Returns

All of these extend nop's built-in `ReturnRequest` / `ReturnRequestReason` / `ReturnRequestAction` /
`ReturnRequestStatus` (unmodified core tables) with CW-only tables:

| Entity | Table | Purpose |
|---|---|---|
| `ReturnRequestItem` | `CORE_EX_ReturnRequestItem` | 1:1 extension of a nop `ReturnRequest`: `ReturnReasonId`/`ReturnActionId` (the authoritative reason/action — see §5.2.1), `ExchangeMode` (Exchange / CreditNote), `ExchangeType` (SameStyle / DifferentStyle / Exact), `ReplacementItem` (nav), `StaffShipQty`/`StaffRepackQty` (admin-adjusted fee quantities), `ReturnRequestGroup` (Guid batching several items submitted together), `NeedsCompletion`/`IsCompleted`, `ShoppingCartItemId` (while pending checkout), `OrderItemId` (once the exchange order is placed), `ReturnedOrderItemId`, `AdditionalFeeName`/`AdditionalFeeAmount` (staff-entered ad-hoc fee) |
| `ReturnReplacementItem` | `CORE_EX_ReturnReplacementItem` | The replacement product picked for an exchange: `ProductId`, attributes XML/info, `Quantity`, `UnitPrice` |
| `ReturnReasonRule` | `CORE_EX_ReturnReasonRule` | Per store, per nop Return Reason: `AllowStoreCredit`, `AllowExchange`, `AllowRepackagingFee`, `AllowShippingFee`, `BackInStock`, `IsDisabled` |
| `ReturnActionRule` | `CORE_EX_ReturnActionRule` | Same flag set, per store, per nop Return Action |
| `ReturnStatusRule` | `CORE_EX_ReturnStatusRule` | Per store, per nop Return Status: `PerformStoreCredit`, `PerformExchange`, `BackInStock`, `Authorised` |
| `ReasonActionRelationship` | `CORE_EX_ReasonActionRelationship` | Many-to-many: which Return Actions are offered for a given Return Reason, per store |
| `FeeRule` | `CORE_EX_FeeRule` | Per store, per `FeeType` (`ReShippingFee`/`RepackagingFee`; `AdditionalFee` is staff ad-hoc, has no rule row): `Amount`, `Operator` (`FixedPrice` / `PerUnitsQuantity`), `UnitsQuantity` |
| `ImpersonationToken` | `CORE_EX_ImpersonationToken` | One-time token letting admin "impersonate" a customer on a *different store* to finish an exchange checkout on their behalf (cross-store return). The table exists; nothing writes it (cross-store impersonation is a stub, §8.1) |

All rule tables follow the same pattern: **the rule row is optional**. If no row exists for a given
reason/action/status + store, the DTO projection (`ReturnReasonRuleDTO`, `ReturnActionRuleDTO`) defaults
every flag to `false` — i.e. "not configured" = "not allowed," fail-closed. The same principle applies in
code: `GetFeeRulesForActionAndReasonAsync` returns an empty list when either rule row is missing, and
`IsCreditNoteNeeded`/`IsExchangeNeeded` treat a null rule as "not allowed."

### 5.2.1 Reason and action are identified by ID, not by name

nop's `ReturnRequest` stores the reason and action as free-text strings (`ReasonForReturn`,
`RequestedAction`), and those strings are **localized** — the value written depends on the customer's
language. They are therefore display-only. **Every behavioural lookup resolves the reason and action from
`ReturnRequestItem.ReturnReasonId` / `ReturnActionId`** (`ExchangeService.GetSelectedReasonAsync` /
`GetSelectedActionAsync`, both of which take the CW item, not the nop request).

Both representations are written together — the wizard sets them at creation, and the admin edit screen's
dropdowns update both through `ReturnsController` — but only the ids are ever read to make a decision.
A stored id may still fail to resolve if the reason or action is later deleted; every consumer treats that
as "no rule configured" and fails closed, logging a warning rather than throwing.

### 5.2.2 The return case

Beside nop's `ReturnRequest` sits CW's return case (`Domain/Returns`, tables `CORE_RET_*`): `ReturnCase` (one per
submission: store, customer, order, case number, reason / action ids, status), `ReturnCaseLine` (one per returned
item, 1:1 with a nop `ReturnRequest`: mode, exchange type, replacement, per-line `Disposition`, staff quantities),
`ReturnResolution` (written once when every line is decided; one per case, enforced by the unique index
`UX_CORE_RET_ReturnResolution_ReturnCaseId`, `Data/Migrations/SchemaMigration.cs:125`) and `ReturnCharge` (the fees
frozen at resolution, each tied to the line it is collected with). `IReturnCaseService` reads and writes them;
`IReturnResolutionLogic` resolves, settles and re-opens a case. History and rules: `CWPlans/Exchanges_Rebuild_Record.md` §3–§4.

### 5.3 Enums

```csharp
ExchangeReturnMode { Exchange = 1, CreditNote = 2 }
ExchangeType       { SameStyle = 1, DifferentStyle = 2, Exact = 3 }
FeeType            { ReShippingFee = 1, RepackagingFee = 2, AdditionalFee = 10 }
FeeRuleOperator    { FixedPrice = 1, PerUnitsQuantity = 2 }
```

### 5.4 Views / stored procedures

Credit Notes has two views, following the Subsidy `vw_CORE_SUB_*` pattern: `vw_CORE_EX_CreditNoteRemain`
and `vw_CORE_EX_CreditNoteHistoryRemain` (§5.1). Exchanges/Returns proper has none yet — the
reporting-query candidates in §11.2 remain open.

**All of this SQL is in the repository**, in `Services/DataBase/CWDatabaseService.cs`, as interpolated
strings re-executed by `CWCorePlugin.UpdateAsync` → `UpdateDatabaseScriptsAsync`. A rebuild alone does not
redeploy them — the `plugin.json` version has to change.

**Returns write one ledger of their own:** `CORE_SUB_SubsidyRestoreItem`, the compensating rows phase 5
writes when a return is authorised. They are netted into the two subsidy availability views through the
shared `SubsidyNetConsumptionSource` fragment, and **not** into the order's settlement row (`CORE_PN_OrderSettlement`, what the order settled at placement). Credit
notes have no equivalent: a returned item's value comes back as a newly issued note, not as a reversal
(§8.2, and `CWPlans/Exchanges_Rebuild_Record.md` §4 and §8).

---

## 6. Service layer

### 6.1 `ICreditNotesService` / `CreditNotesService` (`Services/CreditNotes/`)

The only place `IRepository<CreditNote>`/`IRepository<CreditNoteHistory>` are injected. Every read goes
through the live-balance views (`IRepository<CreditNoteBalance>` / `IRepository<CreditNoteHistoryBalance>`)
instead of the base tables, and `Remain` is never written — only `CreditNoteHistory` rows are ever
inserted.

| Method | Behaviour |
|---|---|
| `GetOpenCreditNotesByCustomerIdAsync(customerId)` | Notes with `Remain > 0` (computed by the view), oldest first (drives FIFO consumption). Cached per-request via `IShortTermCacheManager`. |
| `SearchCreditNotesByCustomerIdAsync(customerId, pageIndex, pageSize)` | All notes regardless of balance, DB-paged. Not cached — each call is its own request and admin views want current data. |
| `CalculateAppliedCreditNotesAsync(customerId, orderTotal, bypassCache)` | **Read-only** simulation of how much of `orderTotal` would be covered by the customer's current (live-computed) balance — doesn't write anything |
| `ApplyCreditNotesForOrderAsync(order, settleableAmount)` | **Writes**, under an `ILocker` lock keyed per customer (`creditnotes_apply_customer_{id}`): inserts `CreditNoteHistory` rows only — `Remain` is never touched, it's recomputed on next read. Called once per order, from `CreditNoteSettlementSource.ApplyAsync` when the order settles |
| `CreateCreditNotesForOrderAsync(order, returnForCredit, reason, returnRequestId?, createdByCustomerId?)` | Issues a new note tied to a return (the "automatic" path); `createdByCustomerId` records the acting user |
| `CreateManualCreditNoteAsync(customerId, orderId, amount, reason, createdByUser?)` | Admin-issued note, requires `CreditNotesPermissionConfigManager.ALLOW_MANUAL_CREDITNOTES`; stores `createdByUser` on `CreditNote.CreatedByCustomerId` |
| `UndoCreditNoteForReturnRequestAsync(returnRequestId)` | Deletes the note + its history created for a given return (used when an exchange workflow fails and needs to roll back). Distinct from order cancellation, which needs no explicit step (see below) |
| `CustomerHasCreditNotesAsync` / `CustomerHasCreditNotesBalanceAsync` | "Has ever had a note" (cached persistently via `IStaticCacheManager`) vs "has positive balance right now" — the former still shows the My Account tab even at zero balance |

**Order cancellation needs no explicit step here.** `vw_CORE_EX_CreditNoteRemain` excludes any
`CreditNoteHistory` row belonging to a cancelled order from its running sum, so a cancelled order's
consumption stops counting the instant `OrderStatusId` becomes Cancelled — no code runs, no history rows
are deleted.

**Payment-method labelling** (in `OrderProcessingOverrideService.ApplySettlementPaymentMethodAsync`, not the
service): after settlement, an order whose `PaymentMethodSystemName` is still **empty** takes `"CreditNote"`,
`"Subsidy"` or `"Subsidy & CreditNote"` from what the plan applied (`Constants/CWPaymentMethodSystemNames.cs`). A
payment method the customer chose is never overwritten.

### 6.2 `IExchangeService` / `ExchangeService` (`Services/Exchanges/`)

The rules/config + return-request-item data-access layer. Large surface; grouped by region:

- **Reason / Action / Status rules** — CRUD + `GetReturn{Reason,Action}RulesAsync(storeId)` which
  LEFT-JOINs every nop reason/action against the CW rule table so ungated reasons/actions still show up
  (with all-false defaults) in the admin grid. This join pattern is efficient — no N+1 here.
- **Fee rules** — CRUD + `GetRuleByFeeTypeAsync(feeTypeId, storeId, fallbackToDefault)` (store override
  falls back to store 0 = global template, matching the docx's "template store other stores inherit
  from" idea).
- **Return Request Items** — the CW extension row: create/read/update, plus `GetReturnRequestItemFor{CartItem,OrderItem}Async` used to reconnect a cart/order item back to its originating return.
- **Reason → Action linking** — `LinkReasonToActionsAsync` diffs and syncs the M:N relationship; `GetActiveActionsByReasonIdAsync` is the query the customer wizard calls via AJAX (`GetActionsForReason`) to populate the Action dropdown once a Reason is picked.
- **Fee calculation** — `CalculateExchangeFeeAmountsForCaseAsync` prices each line of a return case on its own
  (rejected lines charge nothing); `GetExchangeFeeAmountsForCaseAsync` returns the frozen `ReturnCharge` rows once
  the case is resolved, and prices live before that. `CalculateExchangeFeesAmountAsync(ReturnRequestItem)` and
  `GetExchangeFeesAmountsAsync` (item / cart / order overloads, grouped by `FeeType`) read through them and feed the
  cart-total and order-detail displays. See §11.1 for the per-item query here.
- **Reason / action resolution** — `GetSelectedReasonAsync` / `GetSelectedActionAsync` take a
  `ReturnRequestItem` and resolve from its stored ids (§5.2.1). Both return null for an unset or
  since-deleted id; callers fail closed.
- **Order flags** — `GetOrderReturnFlagsAsync(orderIds)` answers, for a page of orders in two batched queries, whether
  each order holds an exchange replacement (**E**) and whether one of its items was returned and the return authorised
  (**R**). Read by the admin order-list badges (§8.4).
- **Returnable quantities** — `GetReturnableQuantitiesAsync(order)`: nop's own availability
  (`GetReturnRequestAvailabilityAsync` — ordered less already requested, without non-returnable products and, unless
  allowed, downloads) less the units not yet shipped, in one shipped-units query per order. The return page, the
  wizard's server checks and `ReturnRequestSubmitFilter` all read it. `CountReturnedUnitsAsync` (one grouped query)
  is still on the interface but has no caller.
- **Store and customer scope** — `GetActiveReasonIdsAsync(storeId)` (the reasons with an active reason rule: the only
  ones the wizard offers or accepts) and `GetCustomerReturnRequestsByIdsAsync(customerId, ids)` (the customer's own
  requests only — the confirmation panel and the receipts).
- **Miscellaneous** — `DetermineEnabledReturnModesAsync` (which of Exchange/CreditNote the customer is
  allowed to pick, from the Reason+Action rule intersection),
  `IsReturnRequestAlreadyPerformed` (idempotency guard keyed by `ReturnRequestGroup` Guid so re-posting
  the same wizard submission doesn't double-create requests).

### 6.3 `CWReturnRequestService` (`Services/Orders/`)

Thin — one method, `SearchReturnRequestsAsync`, a filtered/paged query over nop's `ReturnRequest` table
(store, customer, name, status, order item, custom number, date range). Used by admin search.

### 6.4 `ExchangesLogic` / `IExchangesLogic` (`BusinessLogic/ExchangesLogic.cs`)

**This is the workflow engine** — the piece that actually decides "does this return get a credit note,
an exchange order, both, or neither," and executes it. Registered and consumed via `IExchangesLogic`.

Entry point: `ExecuteApprovedLineAsync(returnRequest, newStatusId)`, called from admin
(`ReturnsController.ExecuteStatusChangeWorkflowAsync`, on the `EditExchangeRequest` post) when the status field
changed and the return has CW exchange data. The controller commits the staff quantities, ad-hoc fee, reason and
action **before** the call, so the workflow reads the persisted row (no overlay). The body runs under an `ILocker`
lock per return request (60 s); a busy lock executes nothing and returns an error, so the status is not advanced.
Logic (`ExecuteApprovedLineCoreAsync`):

1. Anything other than `ReturnAuthorized` **stops** here.
2. Load the `ReturnStatusRule` for the new status. If none exists or `Authorised == false`, **stop** — no credit
   note, no exchange, regardless of anything else. *Configuration note:* the *Return authorized* rule must also have
   *Perform store credit* / *Perform exchange* ticked, or an authorisation raises nothing.
3. Resolve the reason and action from the item's ids (§5.2.1) and load their rules; an unresolvable id stops the
   workflow with a warning.
4. `IsExchangeNeeded` = mode is `Exchange` **and** status `PerformExchange`, reason and action `AllowExchange`.
   `IsCreditNoteNeeded` = `(statusRule.PerformStoreCredit && reasonRule.AllowStoreCredit && actionRule.AllowStoreCredit) || exchangeNeeded`
   (decision 17: an exchange always gives the customer's own money back). If neither, stop.
5. **Subsidy back in kind first** — `RestoreReturnedLineAsync` → `SubsidyCreditService.RestoreSubsidyForReturnedItemAsync`
   (§9.1), before the note is sized and before the replacement reaches the cart.
6. If a credit note is needed → `ExecuteCreditNoteWorkflowAsync`: the returned value
   (`DetermineExchangeCreditNoteAmountAsync`) minus `SubsidyRestoreResult.MoneyEquivalent`, minus the fees in
   Credit-Note mode, rounded to cents; nothing is written for zero or less (a fully subsidised line raises no note).
   `FeesNotDeductible` is set when Credit-Note-mode fees have no money to come off.
7. If an exchange is needed → `ExecuteExchangeWorkflowAsync`, inside `ExchangeReplacementScope`:
   - **Pathway A (settlement covers the replacement, no fees on this line, and the replacement passes nop's cart
     validation):** decided by `QuoteReturnOrderSettlementAsync`, which walks the same pipeline
     `PlaceReturnOrderAsync` will settle. Places the replacement order immediately, marks the return complete,
     sends the "approved" customer notification. No customer action needed.
   - **Pathway B (a balance or fees owed):** adds the replacement to the customer's cart (carrying the packname —
     see §9.2), marks `NeedsCompletion = true`, and sends the "pending payment" notification with the amount still
     owed. At checkout the ordinary pipeline settles it against the restored entitlement first.
   - On any failure in either pathway: `CleanupFailedExchangeWorkflowAsync` — undoes the credit note (if
     one was created this run), the subsidy restore rows and any cart items added.
   Otherwise (no exchange) the line is marked complete.
8. Returns a `ReturnRequestWorkflowResult` (`CreditNoteWasCreated`, `SubsidyWasRestored`, `FeesNotDeductible`,
   `NeedsCompletion`, `OrderWasPlaced`, `Errors`) that the admin controller turns into notification banners. The
   return case's resolution is written by `IReturnResolutionLogic.TryResolveCaseAsync` once every line is decided.

**The customer e-mails at approval are sent only when their templates exist**
(`ReturnAuthorisedExcepted.CustomerNotification`, `ReturnAuthorisedPayment.CustomerNotification`,
`ExchangeWorkflowMessageService`), and CW.Core installs neither (it installs `Return.Receipt` only). Until someone
creates them, the customer gets no e-mail when a return is authorised.

Other responsibilities in this class: building the "single product" transient cart used to reuse nop's
official price-calculation pipeline (`CalculateCartItemsTotalAmountAsync`), the "modify exchanged item
attributes" wizard action, and `UndoReturnRequestApprovalAsync` (admin "undo approval" button — resets
status to Pending and rolls back everything the workflow did).

### 6.5 Order placement integration (`Services/Orders/OrderProcessingOverrideService.cs`)

`PlaceReturnOrderAsync(returnRequest, returnRequestItem)` is a customised copy of nop's standard
order-placement pipeline, specialised for "place an order that represents an approved exchange":

- Builds order details from the return's replacement item(s), saves the `Order`, moves cart items to
  `OrderItem`s (`MoveShoppingCartItemsToOrderItemsAsync`), relates each new `OrderItem` back to the
  `ReturnRequestItem` (`RelateOrderItemToReturnRequest`), and **clones the PackName** from the originally
  returned item onto the new order item (see §9.2).
- Settlement runs inside it like any packname order: `SettleOrderAsync` (subsidy first, credit notes second — the
  settlement pipeline, `subsidy-system.md` §7) with the returned item's packname, so the entitlement restored at
  approval is what pays for the replacement; the order's `OrderTotal` is rewritten after settlement to what the
  customer still owes. Exchange orders settle under the packname locks (20 tries half a second apart) and settle
  unlocked after that, logged as a warning, because the order is already placed and paid.
- **An approved replacement is added to the cart through the packname `AddToCartAsync(…, packNameId, …)` inside
  `ExchangeReplacementScope`**, which exempts it from the closed-ordering-period and branch checks, from the
  allocated-quantity cap (the returned units were given back at approval, and a refusal would roll the whole
  authorisation back) and, by exchange type, from the allocation gate (see §7 step 3). The exemption ends when the
  scope closes; at checkout each row is judged by its own `ReturnRequestItem.ShoppingCartItemId` link. Two known
  gaps: the packname cart view hides a replacement line whose packname's period is closed, so such a replacement can
  be paid only through `PlaceReturnOrderAsync`; and an exchange cannot be authorised at all once the customer's
  packname is closed for ordering (`Subsidy_PackName_Open_Items.md`).

---

## 7. Public store workflow

> The customer-facing flow was repaired and reworked after the redesign was paused at Phase 1c. See
> `CWPlans/Deprecated/Exchanges_Wizard_Fixes_And_Improvements.md` for the defect history, the two-stage picker, the
> validation redesign and the query-count reductions.

Entry point is the standard nop "Return Item(s)" button on an order in *My Account ▸ Orders*, gated by
`IOrderProcessingService.IsReturnRequestAllowedAsync`. The wizard is for the order's own signed-in customer; a
guest's withdrawal link (nop 5.00's withdrawal form) goes through nop's own form. With *Exchanges* on,
`ReturnRequestSubmitFilter` keeps nop's own return post to that withdrawal path, and on every path refuses a quantity
above what CW's return page offers. From there:

1. **`ReturnRequestOverrideModelFactory.PrepareSubmitReturnRequestModelAsync`** (overrides nop's stock
   factory) builds the initial "which items, how many" form. With *Exchanges* on, only the store's active return
   reasons are offered (`GetActiveReasonIdsAsync`); if there is none, the page shows a message instead of the form.
   With *Exchanges* off the page keeps nop's own lists. Each returnable item's *available quantity* is
   `ExchangeService.GetReturnableQuantitiesAsync(order)` — nop's availability less the units **not yet shipped**,
   one query per order (§6.2). The step also renders attribute-aware product thumbnails.
2. **`ExchangesController.ReturnRequestGatherInfo`** (`[HttpPost, ActionName("Gather")]`) — customer
   picked quantities + reason/action; builds a `ReturnRequestGatherInfoModel` (one row per selected
   item, with `AvailableExchangeModes` = the reason/action-driven allowed modes) and shows the "how would
   you like to proceed" step (Figure 5/6 in the docx: pick Exchange vs Credit Note, and for Exchange
   pick Same Style / Different Style / Exact).
3. **Replacement picker** — two AJAX stages. **`ReplacementProductList`** returns a paged, searchable
   grid built from `ProductOverviewModel`; **`ReplacementProductOptions`** returns one product's
   size/colour form built from `ProductDetailsModel`. *Same Style* enters stage 2 directly. This replaced
   `StyleChangeList`, which loaded the whole catalogue and built a full product-details model per product.
4. **`DisplayReturnRequestSummary`** (`ReturnRequestSummary` action) — the posted form is parsed once
   into a `ReturnWizardSubmission`, validated by `ReturnWizardSubmissionValidator` and checked on the server by
   `IReturnSubmissionLogic.CheckSubmissionAsync` (the reason is active and its action linked, returnable shipped
   units, the mode allowed, each replacement against its type, its required options and the item's packname
   catalogue). The same check runs on the gather post and on *Confirm*. On failure the
   gather step is **redisplayed with the customer's selections intact**; on success it computes
   `EstimatedFees` from the applicable `FeeRule`s and shows the summary (Figure 8/9: fees, "if faulty no
   cost" messaging comes from the rule engine, not hard-coded). Fee *rules* are also previewed on the
   gather step, before the customer commits.
5. **`ConfirmReturnRequestSummary`** (`[FormValueRequired("confirm-return-request")]`) —
   `IReturnSubmissionLogic.RegisterReturnRequestDataAsync` creates the nop `ReturnRequest` (status = `Pending`) + the CW
   `ReturnRequestItem` (mode/type/replacement), registered under a lock per order and guarded by
   `IsReturnRequestAlreadyPerformed` so a double-submit of the same `ReturnRequestGroup` doesn't duplicate. Sends nop's stock new-return emails
   **and** the CW `SendReturnRequestReceivedNotificationAsync`. Redirects to `CustomerReturnRequests`.
6. **`CustomerReturnRequests`** / **`GenerateReturnRequestReceipt(s)`** — customer's status list + PDF
   receipt download (`ICWPdfService`). A **confirmation panel** (`ReturnRequestsConfirmations` view
   component) opens over the list immediately after submitting, showing each new return and the
   `Return.Receipt` message template, with an optional print-before-closing guard
   (`MultiStoreSettings.AllowCloseWithoutPrintingReturnNote`). Pending returns offer **Edit Exchange
   Item**, which reopens the picker for the chosen replacement (`EditExchangedItem`; options not posted keep their
   value). The list shows the exchange block (type, replacement) only for an `ExchangeReturnMode.Exchange` request
   (`CustomerReturnRequests.cshtml:125`); a credit-note request and a plain nop request show nop's lines only.
7. If approved for exchange and the customer needs to **pay/complete** (Pathway B in §6.4) — the
   replacement is already sitting in their cart; they check out normally. The settlement pipeline applies the
   restored subsidy, then any credit notes, at that point.
8. **Store Credit page** (`CreditNotesController.CreditsForMe`, `Views/CreditNotes/CreditsForMe.cshtml`, *My account
   › My Store Credit*) — customer's credit-note balance, issued notes (oldest first) and consumption ledger (newest
   first), in the customer's currency. The subsidy position has its own page (*My Subsidies*, `customer/subsidies`).

### 7.1 Widgets on cart / order pages

- `CreditNotesWidgetsViewComponent` → `WIDGET_CREDITNOTES_FOR_CART` / `_ORDER_CUSTOMER` (public): shows
  the amount of credit that *would* apply to the current cart, or *did* apply to a placed order, as a *Store credit*
  row written as a deduction; before the cart's amount is known the row reads nop's *Calculated during checkout*.
  On the admin order page (`_ORDER_ADMIN`) the same amount is unsigned and bracketed. A packname order's settlement
  block shows its store credit itself, so the customer order page uses the widget only for an order without one.
  The admin Customer edit page's *Credit notes* card (manual issuance form, balance summary, grids) is not a widget:
  `Areas/Admin/Views/Customer/_CreateOrUpdate.CreditNotes.cshtml` calls the admin `CreditNotesController` directly
  (§8.3).
- `ExchangesWidgetsViewComponent` → `WIDGET_EXCHANGES_FEES_FOR_CART` (public cart): live fee preview for
  any exchange items sitting in the cart. `_FOR_ORDER` / `_FOR_ORDER_ADMIN`: fees actually charged on a
  placed order, customer- and admin-facing.

---

## 8. Admin workflow

### 8.1 Return Requests (extends nop's own admin area)

`Areas/Admin/Controllers/ReturnsController : ReturnRequestController` (nop's own admin controller,
subclassed — not a parallel controller) overrides/adds:

- **`EditExchangeRequest`** — the real workhorse. If the return is already in a *final* status
  (`ExchangesHelper.IsFinalReturnRequestStatus` — currently **only** `ReturnAuthorized` counts as final;
  worth double-checking against the intended status list, see §10.4), only `StaffNotes` can still be
  edited — quantities/fees/status are locked. Otherwise: if the status field changed **and** the return
  has CW exchange data, calls `IExchangesLogic.ProcessReturnRequestStatusChangeAsync` (§6.4) and surfaces
  its result as success/warning/error notifications. The CW fields (staff quantities clamped to the returned
  quantity, ad-hoc fee, reason, action) are committed before the workflow runs, and the status only after it
  succeeds; nop's own fields are persisted onto the base `ReturnRequest`, and stock is returned to
  inventory for any quantity delta. The extra buttons below are posts to the same action name
  (`[HttpPost, ActionName("EditExchangeRequest")]` with `FormValueRequired`), and each checks
  `StandardPermission.Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE`.
- **`ImpersonateForCompletion`** — "impersonate" button for Pathway B returns: logs the admin in as the
  customer (same-store). For a return belonging to **another store** it calls `ICustomersLogic.RequestImpersonation`,
  which is a **stub**: the token is fabricated locally (the 4.1 API client is commented out), nothing writes the
  `ImpersonationToken` table, and the redirect to `{otherStore}/impersonate/useToken` lands on a route no CW.Core
  controller serves. Cross-store impersonation therefore does not work (`Subsidy_PackName_Open_Items.md`).
- **`NotifyCustomer`** (override) / **`NotifyCompletionRequired`** — manual "resend status email" / "nudge
  the customer to complete a pending-payment exchange" actions.
- **`UndoReturnRequestApproval`** — calls `IExchangesLogic.UndoReturnRequestApprovalAsync`, resets status
  to Pending and rolls back credit notes/cart items created by a previous (mistaken) approval.
- **`ReturnRequestReasonCreatePopup`** / **`ReturnRequestActionCreatePopup`** — nop-standard popups for
  creating new Reason/Action master records (the CW rule rows are configured separately, see below).

The admin **Edit Return** view (`Areas/Admin/Views/ReturnRequest/Edit.cshtml`) is the CW-extended version
of nop's stock page (Figure 11/12 in the docx): shows exchange mode/type, replacement product, packname,
staff-adjustable repack/ship quantities, additional-fee toggle, and (once completed) links to the
auto-generated exchange order.

### 8.2 Exchanges configuration (`ExchangesConfigController`)

Where it shows: the admin menu's *CW Admin ▸ Exchanges & Returns* node holds *Fee Settings* (the global, store 0,
fee rules — `GlobalFeeSettings`) and the *Credit Notes Report* (§8.3); the per-store grids are on the store editor's
*Exchanges and Returns* and *Fee Settings* tabs (`ExchangeSettingsComponent`,
`Areas/Admin/Views/Shared/Components/ExchangeSettings/*`).

One consolidated controller (merged from a former split `ExchangesController` + `ExchangesConfigController`)
covering:

- **Fee rules** (`GlobalFeeSettings` page and the store tab) — CRUD grid for `FeeRule`, per store, with global (store 0)
  templates that a store can override (editing an inherited row from a store context **creates** a new
  store-level row rather than mutating the shared one — "4.10 behavior" per the code comment).
- **Return Reason rules / Return Action rules / Return Status rules** — three CRUD grids
  (`GetReturnReasonsRules`/`UpdateReturnReasonRule`, etc.), each upsert-style (looks up an existing rule
  row for the reason/action/status + store, updates it if found, otherwise creates it).
- **Reason → Action linking popup** (`AddLinkedActionsPopup`) — per store, tick which Actions are offered
  for a given Reason (feeds `GetActiveActionsByReasonIdAsync`, which the public wizard's AJAX
  `GetActionsForReason` call uses).
- **Order-details return-requests grid** (`GetOrderReturnRequests`) — powers the widget embedded in the
  standard nop Order Details admin page (via `OrderReturnRequestsViewComponent`, zone
  `AdminWidgetZones.OrderDetailsBlock`) showing return requests + credit notes applied for that order.

All actions are permission-gated with `CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES` and feature-gated
with `[CheckCWFeature(CWFeature.Exchanges, ...)]` — except `GetOrderReturnRequests`, deliberately left
open (it's a pure read of requests that already exist, per §4).

#### `AllowStoreCredit` gates two different things, and one of them is not obvious

The same flag on `ReturnReasonRule` / `ReturnActionRule` decides:

1. whether **Credit Note mode is offered** in the customer wizard —
   `ExchangeService.DetermineEnabledReturnModesAsync`;
2. whether a **credit note is issued on approval** for a return for credit — `ExchangesLogic.IsCreditNoteNeeded`, which is
   `(statusRule.PerformStoreCredit && reasonRule.AllowStoreCredit && actionRule.AllowStoreCredit)
   || exchangeNeeded`.

On an exchange the second clause raises the note whatever the tick says (decision 17), so an exchange never leaves
the customer paying twice for a garment.

**On an exchange, the credit note carries only the money the customer put in** — the returned value minus
the subsidy given back in kind (phase 4b, `ExchangesLogic.ExecuteCreditNoteWorkflowAsync`). Phase 5 built
no `RestoreAsync` for credit notes because a note is re-issued, never reversed (`CWPlans/Exchanges_Rebuild_Record.md`
§4 and §8).

**Live 4.1 rules, checked 2026-09-10:** the Exchange action allows store credit in 84 of 85 stores, every
exchange-enabled reason allows it in all but one store, and the *Return Authorized* status rule is
`1,1,1` everywhere. So the previous system raised a full-price note on every exchange, subsidised or not.
Since phase 4b `IsCreditNoteNeeded` is *rules allow store credit, or an exchange is being performed*
(decision 17), so the tick no longer changes what an exchange gives back; it still decides whether
Credit-Note mode is offered and whether a return for credit raises a note. The 4.1 clause "or the
replacement is cheaper" is gone — the difference falls out of the arithmetic.

### 8.4 Order lists — the E and R badges

*Sales → Orders* and the *Orders* tab of a customer mark each order with **E** (yellow, "Order has exchanged items")
when it holds the replacement of an exchange — a `ReturnRequestItem` with `ExchangeMode == Exchange` whose
`OrderItemId` is one of the order's items — and **R** (blue, "Order has returned items") when one of its own items was
returned and the return is *Return authorised*, *Item(s) repaired* or *Item(s) refunded*. Pending, received, rejected
and cancelled returns do not count. An order can show both. E is 4.1's badge (same column, same mode test, on the
exchange order); R is new. `OrderListBadgesViewComponent` (zones `OrderListButtons`, `CustomerDetailsButtons`) renders
a config div and `Content/Admin/Orders/admin.order.badges.js`; after each grid draw the script posts the page's order
ids to `CWOrder/OrderBadges` (`IExchangeService.GetOrderReturnFlagsAsync`, §6.2) and appends the badges to the
order-number cell. Nothing is cached; vendors get no badges. The badges read existing data and are never gated by the
exchanges or credit-notes switches.

### 8.3 Credit Notes (admin) (`Areas/Admin/Controllers/CreditNotesController`)

- **Customer-scoped**: `CustomerCreditNotesList` / `CreditNoteHistoryList` (grids on the Customer edit
  page's Credit Notes card), `GetCreditNotesSummary` (balance for the summary chip),
  `GetAvailableOrders` + `SaveCreditNote` (manual issuance form — requires
  `CreditNotesPermissionConfigManager.ALLOW_MANUAL_CREDITNOTES` **in addition to**
  `ADMIN_ACCESS_CREDITNOTES`), `DeleteCreditNote` (only allowed while the note has no consumption
  history — see `CreditNoteHasHistoryAsync`).
- **Store-level reporting** (`#region Liability report`): `MainReport` page, `StoreLiabilityList` (per
  store: notes issued, notes fully consumed, issued value, remaining liability value, % used),
  `StoreCustomerCreditNotesList` (per store: which customers are holding a balance). The outstanding and
  per-customer values add up what is **left** on the notes (`Remain`), not their original amounts. **These loop over
  every store and every note in application code** rather than aggregating in SQL — see §11.2, the
  clearest "should be a DB view" candidate in this whole area.

---

## 9. Subsidy / PackName integration — current state

The subsidy integration is built: subsidy settles first, an authorised line gives its subsidy back in kind, and the
replacement draws on it. What is still missing is in §9.4.

### 9.1 Subsidy — implemented vs stubbed

`ISubsidyCreditService` (in `Services/Subsidies/`) is shared by the whole Subsidy subsystem, not just
Exchanges. The three methods Exchanges/Returns actually calls:

| Method | State | Notes |
|---|---|---|
| ~~`GetSubsidyAppliedToOrderAsync(orderId)`~~ | **Gone** | No caller was left and it no longer exists; `GetSubsidyTotalByOrderIdAsync` and the `vw_CORE_SUB_OrderSubsidyTotal` view behind it were removed by phase 6 (2026-10-02). What an order settled is its settlement row: `ICWOrderService.GetOrderSettlementAsync` → `CORE_PN_OrderSettlement`. |
| `RestoreSubsidyForReturnedItemAsync(SubsidyRestoreRequest)` | ✅ **Implemented (phase 5); reachable on every authorised line since phase 4b** | Writes insert-only compensating rows to `CORE_SUB_SubsidyRestoreItem`, one per consumption bucket, keyed on the return case line (idempotent, locked per order item), netted into the availability views by `SubsidyNetConsumptionSource`. Returns a `SubsidyRestoreResult`; its `MoneyEquivalent` sizes the credit note. The 4.1-era stubs `RefundSubsidyForReturnAsync` / `UndoSubsidyForOrderAsync` no longer exist. |
| `DeleteSubsidyRestoreItemsForCaseLineAsync(returnCaseLineId)` | ✅ **Implemented** | Undo / re-open path. Cancellation needs no write: the views exclude cancelled orders (§9.6). |

**Authoritative credit-application priority rules** (from `CWPlans/Deprecated/Subsidy_PackName_Integration_Plan.md`,
verified against current code — still true):

1. **On order placement:** **Subsidy first, Credit Notes on the remainder** (decision 15, phase 4b;
   before 4b the rebuild ran credit notes first). Both the cart-total display and placement walk the same
   `ISettlementPipeline`, and the order is the sources' `Priority` values.
2. **On return/exchange:** subsidy is given back **in kind** on every authorised line (decision 18) and the
   replacement then **spends** it through the ordinary pipeline. A cheaper replacement leaves the
   difference where it came from — on the note if money-funded, in the pot if subsidy-funded (decision 7).
   The 2018 "surplus → credit note" rule is superseded.

> **Two placement pipelines, one settlement.** `PlaceReturnOrderAsync` (Pathway A, auto-placed exchange)
> settles with a packname cart projected onto the **returned order item's** packname
> (`BuildReturnOrderPackNameCartAsync`), so the entitlement restored by the approval pays for the replacement; a
> non-packname original settles with credit notes only. Pathway B puts the replacement in the customer's normal cart and
> checkout settles it through `MovePackNameShoppingCartItemsToOrderItemsAsync` → `SettleOrderAsync`, where
> the restored entitlement is the first thing the pipeline finds. Nothing consumes twice because the
> original consumption was reversed at approval.
3. Within one approval the order is: restore subsidy, then the credit note, then the exchange. An Exchange-mode
   line raises a note (its own money back) **and** the replacement; a Credit-Note-mode line raises the note only.

### 9.2 PackName — fully carried through

Unlike Subsidy, the PackName carry-through work described in the integration plan (Tier 2, Steps 2–4) is
**complete and live**:

- **Display**: `IExchangesDisplayService.GetPacknameAsync(orderItemId)` reads the real packname via
  `ICWOrderService.GetPackNameOrderItemAsync`; the public `ReturnRequestItemModelFactory`, the public
  `ReturnRequestOverrideModelFactory` and the admin `ReturnRequestExchangeModelFactory` all call it.
- **Exchange order placement**: `OrderProcessingOverrideService.MoveShoppingCartItemsToOrderItemsAsync`
  clones the returned item's `PackNameOrderItem` onto the new exchange order item.
- **Exchange cart add**: `ExchangesLogic.RegisterApprovedExchangedItemsAsync` resolves the source
  packname and adds the replacement to the cart via the packname-aware
  `IShoppingCartOverrideService.AddToCartAsync(..., packNameId, ...)` overload.

None of them carries a TODO marker any more.

### 9.3 Subsidy and store credit on the customer's pages

*My Store Credit* (`CreditsForMe.cshtml`) shows credit notes only. The subsidy position is on its own page, *My
Subsidies* (`CustomerSubsidiesController`, `customer/subsidies`). The never-shown subsidy section of the store-credit
page and its stub were removed.

### 9.4 What's genuinely missing (not just stubbed)

- The **Jim2/ERP sync** side of everything in §2.3 (flag-based subsidy-vs-credit-note determination sent
  to Jim2, the SQL-view hook Jim2 was meant to expose back) — **not started** in this codebase; it lives
  entirely outside NopCommerce and there's no local scaffold for it (see §10.1).
- The **unit-based-subsidy exchange filtering** rule ("exchange must stay within the same Product Group
  once the subsidy is unit-based, unless already paid out of pocket") is met by the **allocation gate** (`CLAUDE.md`
  §15.2): the replacement picker and the options page open the returned item's packname in `PackNameCatalogScope`
  with the exchange type — the grid as *Different Style*, the options page as *Same Style* for the returned product
  and *Different Style* otherwise — so a Different Style replacement is offered and accepted only from the packname's
  allocation (when its subsidies restrict to allocated products), while Exact / Same Style are unrestricted and a
  closed period never blocks a replacement. A saved replacement line carries its type from its `ReturnRequestItem`.
  Options answers 404 for a product the packname may not see. The wizard's **submission** checks the posted
  replacement the same way (`ReturnSubmissionLogic.IsReplacementValidAsync`: its type, every required option with
  one of the product's values, published and visible individually, and the packname's catalogue as *Different
  Style*). One residue: the picker's list (`ExchangesModelFactory.PrepareReplacementProductListModelAsync`) still
  offers grouped-product parents (`Subsidy_PackName_Open_Items.md`).
- The **Branch Manager / Head Office credit-note routing setting** mentioned in §2.3 Option 2 does not
  exist as a `MultiStoreSettings` flag anywhere in the codebase.

### 9.5 Current subsidy architecture (quick primer, from `subsidy-system.md`)

The current Subsidy system is a re-implementation, not a lift-and-shift of the 2018 design §2.3 describes.
Mapping old concept → current implementation:

| 2018 docx concept (`8c Subsidy Credit Note Scenarios.docx`) | Current implementation |
|---|---|
| "Global ($) subsidy" | `Subsidy.Credit` — a global credit pool per `SubsidyPackName`, consumed **last**, after product-group allocations (see order below). Also the **only** source used to subsidise shipping (`ISubsidyCreditService.GetPackNameShippingRemaining`) — a capability the 2018 design didn't call out at all. |
| "Unit-based subsidy tied to a small group of products" | `ProductGroup` + `SubsidyProductGroup` — a subsidy can link **multiple** product groups, each with its own `Quantity` (unit cap) **and** `Credit` (dollar cap), not a single flat unit allocation. Quantity and credit allocation are mutually exclusive per product (§5.2 of `subsidy-system.md`). |
| (not present in 2018 design) | **Additive, period-scoped topups** — `SubsidyTopup` (global) and `ProductGroupTopup` (per product group). Entering an amount always *adds* to the current period's topup, never sets it. Period membership is computed dynamically from `CreatedOnUtc` vs. the subsidy's `DurationEnd`/`AnnualRecurrence` — nothing is stored on the topup row itself, so changing the subsidy's dates automatically reclassifies existing topups. Topups added in the gap between periods roll onto the *next* period. Both a store admin (admin area) and a manager PackName (public store "Change Topup" panel, gated by `AllowEditSubsidyTopup` + `AllowManagersToAddPackNames`) can add topups; every change is written to an immutable `SubsidyTopupHistory`/`ProductGroupTopupHistory` audit row. |
| (not present in 2018 design) | **Per-PackName credit/period override** — `SubsidyVariation` (max one per `SubsidyPackName`) can replace the subsidy's global `Credit` and/or `DurationStart`/`DurationEnd` for one specific PackName. Whether the override applies is controlled by two store settings (`PackNamesOwnGlobalValue`, `PackNamesOwnStartAndFinishDates`), enforced **entirely in the SQL views** (`vw_CORE_SUB_SubsidyPackNameCredit`, `vw_CORE_SUB_SubsidyVariationInOrderingPeriod`) — the C# service layer never branches on these settings itself. |
| Strict consumption order (implicit in the docx) | Explicit and layered: **(1)** product-group quantity base → **(2)** product-group quantity topup → **(3)** product-group credit base → **(4)** product-group credit topup → **(5)** global subsidy credit base → **(6)** global subsidy topup. Across *all* of a PackName's active subsidies, all quantity is drained (steps 1–2) before any credit is touched (steps 3–6) — this is a three-pass sweep, not a per-subsidy loop, precisely so a product that's in a product group in two different subsidies doesn't get billed to Subsidy A's credit while Subsidy B still has quantity left. |

This is the model `ISubsidyCreditService` sits on top of. Exchanges calls only its restoration methods (§9.1), which
reverse the allocator's own buckets; they do not reimplement the consumption order.

### 9.6 Cancellation vs. return/exchange — two different mechanisms

Credit Notes and Subsidy use the same mechanism for order cancellation: both balances are computed by
views that exclude cancelled orders (`vw_CORE_EX_CreditNoteRemain`, §5.1; and
`vw_CORE_SUB_SubsidyPackNameCredit`/`vw_CORE_SUB_ProductGroupPackNameCredit`, per `subsidy-system.md` §4;
the `OrderStatusId != 40` filters in `Services/DataBase/CWDatabaseService.cs`), so a plain cancellation
(`OrderStatusId` → `Cancelled`) frees up both a customer's credit note balance and a PackName's subsidy the instant
the status changes, with zero explicit code needed.

**Return/exchange is different from cancellation.** The original order in a return/exchange is **not** cancelled —
it stays in whatever completed/paid status it was in, so its consumption keeps counting. The compensating action is
the restore row: `RestoreSubsidyForReturnedItemAsync` writes `CORE_SUB_SubsidyRestoreItem` rows (positive amounts,
negated by the availability views), tagged with the return case line. This is the current form of the docx's "revoke
flag" idea (`8c Subsidy Credit Note Scenarios.docx` §2.3): the consumption row is never changed or deleted, so
history and reporting stay intact.

---

## 10. Known issues / dead code / old-style code

Concrete, file-level findings from reviewing this area:

### 10.1 Jim2/ERP sync stubs

- `ExchangesLogic.ExecuteExchangeWorkflowAsync` (pathway A): `// TODO Sync the new order to the ERP`
  (`ExchangesLogic.cs:351`) — no service exists.
- `ReturnRequestExchangeModelFactory.PrepareRequestContextAsync`: `//model.ErpNumber = GetERPNumber(returnRequest);
  //TODO: ERP integration is pending` (`ReturnRequestExchangeModelFactory.cs:99`) — no ERP number is shown. The
  branch on the same screen is real: `GetBranchInfo` reads the customer's branch (`IBranchService.GetBranchByCustomerIdAsync`).

### 10.2 Dead / superseded code

- `IExchangeService.CountReturnedUnitsAsync` has no caller (the wizard reads `GetReturnableQuantitiesAsync`).

### 10.3 Duplicated "overlay unsaved admin edits" logic

Two places handle the admin's in-flight form values for the same `ReturnRequestItem` fields (reason, action, staff
quantities, ad-hoc fee), and they must be kept in step by hand:

- `ReturnExchangeRequestValidator.ValidFeesAmountAsync` — overlays the posted values on a freshly-loaded item so the
  "fees exceed the credit note" check uses them.
- `ReturnsController.UpdateExchangeReturnDataAsync` — persists those same fields, **before** the approval workflow
  runs. The workflow itself (`ExecuteApprovedLineCoreAsync`) no longer overlays: it reads the committed row.

Adding a field that participates in fee or rule evaluation means touching both. Tracked for
consolidation as roadmap item **B2**.

### 10.4 `ExchangesHelper.IsFinalReturnRequestStatus`

```csharp
private static readonly IList<ReturnRequestStatus> _finalStatuses = new List<ReturnRequestStatus>
{
    ReturnRequestStatus.ReturnAuthorized
};
```

Only `ReturnAuthorized` is treated as a "final, no longer editable" status. nop's stock
`ReturnRequestStatus` enum is `Pending` (0), `Received` (10), `ReturnAuthorized` (20), `ItemsRepaired`
(30), `ItemsRefunded` (40), `RequestRejected` (50), `Cancelled` (60) — so `RequestRejected`,
`ItemsRefunded` and `Cancelled` are all arguably terminal too. Worth confirming with the business whether
they should also lock the admin edit form; today staff can keep editing quantities, fees and status after
a return has been rejected or cancelled.

### 10.5 Old-style code smells (general)

- Several admin factory methods build reports by loading **entire entity lists into memory per store**
  and aggregating with LINQ (`Sum`, `Count`, `Where`) rather than querying aggregates — see §11.2, this
  is the biggest lever for the performance work the user flagged.

### 10.6 Business-logic layer — known issues

- **`CustomersLogic.ImpersonateCustomer`'s `[CheckPermission(...)]` attribute is inert.**
  `[CheckPermission(StandardPermission.Customers.CUSTOMERS_IMPERSONATION)]` (`CustomersLogic.cs:113`) is an MVC
  filter attribute — it only takes effect via the ASP.NET Core filter pipeline on a **controller action**. Applied
  to a plain method on a business-logic class, it does nothing. Its only caller,
  `ReturnsController.ImpersonateForCompletion` (§8.1), checks `Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE` but not
  `Customers.CUSTOMERS_IMPERSONATION`, so an admin who may edit returns but not impersonate customers can still
  impersonate through this button.

---

## 11. Performance findings (N+1 / redundant-query patterns)

Concrete, file-and-method-level list — Subsidy already uses DB views/SPs for reports and historical data
(`vw_CORE_SUB_*`); Exchanges/Credit Notes mostly does not yet (Credit Notes' balance views are the
exception — §5.4).

### 11.1 N+1 query patterns

| Location | Pattern | Fix direction |
|---|---|---|
| `ExchangeService.AddReturnedItemsFeesToTargetAsync` (behind `GetExchangeFeesAmountsAsync` for a cart or an order) | Loops every `ReturnRequestItem` and reads its return case line with `GetReturnCaseLineByReturnRequestIdAsync` **per item**; each case is then priced once (memoised within the call). An item raised before the case model existed is priced on its own (`CalculateExchangeFeeAmountsForItemAsync`, which loads its `ReturnRequest`). | Batch-load the case lines for the item set up front. |
| *My Returns* (`Views/ReturnRequest/CustomerReturnRequests.cshtml` and its factory) | About ten queries per row (recorded by the 5.00 port, phase 5g). | Batch per page. |
| `Areas/Admin/Factories/CreditNotesModelFactory.CustomersCreditNotesDetailsAsync` | Loads **all** credit notes with balance for a store, then for each **distinct customer** calls `_customerService.GetCustomerByIdAsync` inside the loop. | Batch-load customers by the distinct ID set once. |

### 11.2 "Should be a DB view / SP" candidates (reporting)

These loop over every store (or every note) in application code, loading full entity lists just to
`.Sum()`/`.Count()` them — classic candidates for a SQL aggregate view, following the exact pattern
Subsidy already uses (`vw_CORE_SUB_SubsidyPackNameCredit`, `vw_CORE_SUB_ProductGroupPackNameCredit`, etc.):

- **`CreditNotesModelFactory.PrepareCreditNotesReportModelAsync`** — loops `GetAllStoresAsync()`, and for
  each store loads **every** credit note for that store into memory to count/sum. One query per store,
  fetching full rows just to aggregate two numbers. → a single `vw_CORE_EX_CreditNoteStoreSummary` view
  (or a grouped SP) returning `{StoreId, CountWithBalance, TotalValue}` would collapse this to one query
  total.
- **`CreditNotesModelFactory.StoresLiabilityDataAsync`** — same store loop, plus computes
  `IssuedCreditNotesValue`, `LiabilityValue`, `PercentageUsed` per store in memory. Same fix.
- **`CreditNotesModelFactory.CustomersCreditNotesDetailsAsync`** — loads every credit note with balance
  for a store, groups by customer in memory (`Dictionary<int, ...>` accumulation), then resolves each
  customer's name one at a time (§11.1). → a view joining `CORE_EX_CreditNote` to `Customer`, grouped by
  customer, would replace this entirely and remove the N+1 in the same stroke.

**General recommendation:** once any of the above reports needs real production data volumes, add
a `vw_CORE_EX_CreditNoteStoreSummary` view (or similar) mirroring the Subsidy views' approach (the SQL in
`Services/DataBase/CWDatabaseService.cs`, run by the plugin update — §5.4 — and a `Domain/*/Views/*.cs` read-only
view entity registered in `CWNameCompatibility`), rather than continuing to aggregate in C#.

---

## 12. Open TODOs found in code (verbatim)

- `Areas/Admin/Factories/ReturnRequestExchangeModelFactory.cs:99` — `//model.ErpNumber = GetERPNumber(returnRequest); //TODO: ERP integration is pending`
- `BusinessLogic/ExchangesLogic.cs:351` — `// TODO Sync the new order to the ERP`

No packname or subsidy "exchanges integration" TODO is left in the code.

---

## 13. Cross-reference: business spec → implementation

| Spec item | Implemented where | Status |
|---|---|---|
| Return Item(s) wizard, reason→action linking | `ExchangesController` + `ReturnRequestOverrideModelFactory` + `ReasonActionRelationship` | ✅ Done |
| Exchange: Same/Different Style/Exact | `ExchangeType` enum, the two-stage replacement picker (§7 step 3), `PackNameCatalogScope` with the exchange type | ✅ Done (Different Style follows the packname's allocation — §9.4) |
| Credit Note system (doesn't exist in nop) | `CreditNote`/`CreditNoteHistory` + `CreditNotesService` | ✅ Done |
| Repackaging/redelivery fees, faulty = no cost | `FeeRule`, `ReturnReasonRule.AllowRepackagingFee`/`AllowShippingFee` | ✅ Done |
| Staff-adjustable fee quantities within order limits | `ReturnRequestItem.StaffShipQty`/`StaffRepackQty`, `ExchangesHelper.DetermineFeeRuleQuantity` | ✅ Done |
| Exchange approval auto-generates order | `ExchangesLogic.ExecuteExchangeWorkflowAsync` Pathway A → `OrderProcessingOverrideService.PlaceReturnOrderAsync` | ✅ Done |
| Return Reason/Action/Status rule tables (per store) | `ReturnReasonRule`/`ReturnActionRule`/`ReturnStatusRule` + `ExchangesConfigController` | ✅ Done |
| Fee table, store-level with template inheritance | `FeeRule` (store 0 = global, `fallbackToDefault`) | ✅ Done |
| Cross-store impersonation to complete a pending exchange | `ImpersonationToken` + `ReturnsController.ImpersonateForCompletion` → `CustomersLogic.RequestImpersonation` | ⚠️ Same-store impersonation works; cross-store is a stub — the token is made locally, nothing writes `CORE_EX_ImpersonationToken`, and no controller serves `impersonate/useToken` (§8.1) |
| Sync return/credit-note status to Jim2 | — | ❌ Not started (§10.1) |
| Cancel before invoice → subsidy back | `vw_CORE_SUB_SubsidyPackNameCredit`/`ProductGroupPackNameCredit` excluding cancelled orders (`OrderStatusId != 40`) | ✅ Done by design — no code runs on cancel (§9.6) |
| Invoiced/returned → subsidy back as credit to employee | `SubsidyCreditService.RestoreSubsidyForReturnedItemAsync` → `CORE_SUB_SubsidyRestoreItem` | ✅ Done, **in kind** rather than as a credit note: invoicing changes nothing, subsidy always returns to the pot (decision 20); only the customer's own money becomes a note (§9.1) |
| Global vs unit-based subsidy credit-note representation | `Subsidy.Credit` (global) vs `ProductGroup`/`SubsidyProductGroup` (unit-based, now multi-group) — see §9.5 | ✅ Re-implemented with more granularity (multiple product groups, quantity + credit per group) — the credit-**note** side specifically (product-group-aware credit notes for returns) is still ❌ not started |
| Revoke-flag concept for reversed subsidy | `CORE_SUB_SubsidyRestoreItem` (insert-only compensating rows; the consumption row is never changed) | ✅ Done for returns (§9.6); not needed for cancellation |
| Unit-subsidy price-difference handling (over/under) on exchange | Restore in kind, then settle the replacement through the pipeline (§6.4, §9.1) | ✅ Dearer: the difference settles normally, the customer pays the rest. Cheaper: a unit-funded original leaves no difference; a money-funded one leaves it on the note or in the pot. The 2018 "surplus → credit note" rule is superseded. ❌ The "route surplus to Branch Manager/Head Office" option is not built (§9.4) |
| PackName carried through returns/exchanges | `IExchangesDisplayService.GetPacknameAsync`, `OrderProcessingOverrideService`, `ExchangesLogic.RegisterApprovedExchangedItemsAsync` | ✅ Done (§9.2) |
| Subsidy and credit notes shown to the customer | *My Store Credit* (`CreditsForMe.cshtml`) and *My Subsidies* (`customer/subsidies`) | ✅ Two pages (§9.3) |
| Per-store on/off switch for Exchanges / Credit Notes | `CWFeature.Exchanges`/`CreditNotes` via `ICWFeatureService` | ✅ Done |

---

## 14. Keeping this document current

This file and `Exchanges_Returns_CreditNotes_DevNotes.md` are used together as the input for any further
work on Exchanges, Returns, Credit Notes, or their Subsidy/PackName integration.

When you land a fix, refactor, or feature in this area:

1. Update the relevant section above (data model, service behaviour, workflow step, known issues, or the
   cross-reference table in §13) to describe the *new current state* — present tense, no dates, no "as
   of," no "previously." This document should always read as if it were written today, regardless of when
   a given piece of functionality was actually built.
2. Add a dated entry to the DevNotes file's Changelog summarising what changed and why, with a pointer to
   the section(s) here it affects.
3. If the change involved comparing real options or a tradeoff worth remembering, add a section to the
   DevNotes file recording the evaluation — future work in this area should check there before
   re-litigating a decision already made.
4. If you close a gap listed in §9 or §10, remove or rewrite the entry here instead of leaving it — the
   "why it changed" story belongs in the DevNotes file, not here.
5. If you fix a performance item from §11, remove it from the table.
6. Periodically re-run `grep -rn "TODO" Plugins/Nop.Plugin.CW.Core --include=*.cs` scoped to
   Exchanges/CreditNotes/Subsidy files and reconcile against §12.
