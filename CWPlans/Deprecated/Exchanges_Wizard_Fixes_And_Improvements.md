# Return wizard — fixes and improvements

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

**Status: complete.** Consolidates twenty working documents produced while repairing and improving the
customer return wizard, between pausing the exchanges redesign after Phase 1c and resuming it.

Scope is the customer-facing flow end to end: *My account → order → Return items* through to *My account →
Return requests*, plus the admin return-request edit screen and the printed return receipt where the same
data is involved.

This document is **not** superseded by the exchanges rebuild and is not part of it: it covers the wizard
itself, not the money model. The two live companions are `Exchanges_Target_Design.md` (what the rebuild
builds) and `Exchanges_Rebuild_Record.md` (what was built, the decisions, and what is left). The
individual phase specs it used to name were retired when the rebuild was paused 2026-08-27.

---

## 1. Why this happened

The 4.1 → 4.9 port carried the C# and the Razor markup across but repeatedly dropped the JavaScript and
mis-translated EF idioms into LinqToDB. The wizard compiled, rendered, and did not work.

Five defect families accounted for almost everything found:

| # | Family | Instances |
|---|---|---|
| 1 | **JavaScript dropped, markup kept** | wizard picker, confirmation panel, Edit Exchange Item |
| 2 | **EF navigation property carried into LinqToDB** | `ReturnRequestItem.ReplacementItem`, `BuildSingleProductItemCart` |
| 3 | **Field declared, never assigned** | `_messagesHelper`, `CWPdfService._exchangesHelper` |
| 4 | **Model/factory ported, view not** | Packname column, `CustomerExchangeRequestModel`, confirmation view |
| 5 | **4.1 helper reused that no longer exists in 4.9** | `Html.GetQueryStringValue`, `GetThumbLocalPath`, `common.js` |

Family 2 alone produced five separate defects and is the reason a `virtual` nav-property sweep across CW
entities is still recommended (§8).

---

## 2. Functional repairs

### 2.1 Picker and wizard JavaScript

4.1's picker ran on **Remodal**, which nop 4.9 does not ship. The 480 lines of inline Razor JavaScript were
replaced with `Content/Exchanges/exchanges-gather.js`, a jQuery UI–based module.

Fixed along the way: the exchange-mode buttons did nothing; the modal rendered inline instead of as a
dialog; it opened before content loaded and grew off-screen (`sizeAndCentreModal` now runs after the AJAX
completes); `BuildSingleProductItemCart` threw `ArgumentNullException` because 4.1 set a `Customer`
navigation property and the port never substituted `CustomerId`; and the attribute parser looped **all**
values instead of the selected one, corrupting `attributesXml` and producing $0.00 price differences.

### 2.2 Reason → action cascade

Actions are now filtered by the selected reason. `GetActiveActionsByReasonIdAsync` was rewritten to match
4.1: an enabled rule row is required, and the list is narrowed only when links exist.

The factory returns `SelectListItem` rather than raw ids. **nop serialises these in PascalCase**
(`Text`/`Value`) with no camelCase policy — the JS reads either casing, which is what fixed the
"right number of blank options" symptom.

**Still open, found 2026-09-11.** The cascade narrows actions by reason, but it does not narrow by whether
the pair has any **return options** behind it. A customer can pick a reason and an action that look
perfectly ordinary, commit to them, and only at step 3 be told *"There are no return options available for
the reason and action you selected. Please choose a different combination."* — with a button back to the
start. On the current test store that is every combination of two of the six reasons.

The information needed to prevent it is already loaded: the same rule rows that drive the cascade decide
whether an exchange, a credit note or both are offered. Either narrow the **action** list to pairs that
lead somewhere, or — better, since a reason with no live action at all should not be offered either —
narrow the **reason** list first and cascade from there. The dead-end screen stays as the backstop for a
rule edited between page load and submit.

### 2.3 Replacement item never persisted — the worst defect

`ReturnRequestItem.ReplacementItem` was a `virtual` EF navigation property. LinqToDB neither cascades the
insert nor lazy-loads it, and **no repository for `ReturnReplacementItem` existed anywhere in the plugin**.
`CORE_EX_ReturnReplacementItem` was empty; eight read sites were dead.

Seven were display paths. The eighth, `BuildProductsListForReturnOrderAsync`, built the **replacement cart
on approval** — so approved exchanges created orders with the correct product and **no size or colour**.

Fixed by inserting the replacement separately after the parent has an id, adding
`GetReplacementItemAsync` / `UpdateReplacementItemAsync`, and **deleting the navigation property entirely**
so the next mistake is a compile error rather than a silent null.

### 2.4 Confirmation screen

The component, model and factory were ported; the view and its invocation were not. Rebuilt on 4.9 with
jQuery UI, including the "print before closing" guard, controlled by a new per-store setting
`AllowCloseWithoutPrintingReturnNote` (default: printing required).

The receipt message was blank because the `Return.Receipt` message template was never installed — 4.1's
`InstallGlobalReturnsReceiptTemplate` was not ported. It is now seeded on install, using
`%Store.CompanyName%` / `%Store.CompanyAddress%` tokens rather than 4.1's hard-coded TIG address.

### 2.5 Edit Exchange Item

Markup was byte-identical to 4.1; the ~145 lines of script behind it were missing. Rebuilt on the shared
picker. **`EditExchangedItem` had no ownership check** — any signed-in customer could POST an arbitrary
`returnRequestId` and rewrite another customer's exchange. It now verifies ownership and pending status,
and requires an antiforgery token.

Four 4.1 behaviours were deliberately **not** reproduced: colour was locked and size forced to change
(more restrictive than 4.1's own wizard); the modal closed on error, discarding the selection; the success
toast was destroyed by an immediate reload; and `location.reload(true)` used a deprecated argument.

### 2.6 PDF receipt

`GetFontAsync` was called once and the result aliased to five variables — `Font` is a reference type, so
every mutation applied to one object and the whole document rendered at 14pt bold. 4.1 called `GetFont()`
six times. Also fixed: the replacement product was read from the dead navigation property; the font
ignored the document's language and the return's store; and `"Exchange Type:"` displayed the exchange
*mode*, so the PDF and the confirmation screen disagreed. Both are now shown.

> Remaining PDF work is tracked separately in `CWPdfService_Modernisation_Spec.md` — the service is still
> written against the pre-4.60 imperative iText API.

---

## 3. Phased improvements

### Phase A — validation and error handling

Three problems, fixed together because fixing one alone would have made the code worse:

1. **Exceptions as control flow.** `GetReturnMode` threw three ways; `BuildReplacementProductModelAsync`
   used `int.Parse` on a possibly-absent form key, producing an **unhandled `FormatException`** for the
   most likely customer mistake in the flow.
2. **The customer was thrown out of the flow.** A validation failure redirected to the start, discarding
   every selection.
3. **Form parsing scattered across five methods**, each knowing its own string keys, none testable.

Now: the form is parsed once into `ReturnWizardSubmission` (the only type that knows field names),
validated by `ReturnWizardSubmissionValidator` (FluentValidation), and failures redisplay the step with
`ModelState` errors and the customer's selections intact — including the chosen mode, so the step reopens
where they left it rather than collapsing back to the mode buttons.

> **Parsing trap:** `exchange_mode` (the global mode) is a prefix of `exchange_mode_{id}` (the per-line
> type). The parser tests the exact match before prefixes. Do not reorder those branches.

### Phase B — usability

A progress indicator replacing hardcoded "STEP 3 / STEP 4" prose; the disabled Next Step button now
explains what is missing (`whatIsMissing`, sharing its wording with the submit-guard alert); applicable
fee **rules** are shown before committing rather than after; and a back link.

Fees show rules, not totals — quantities are not final until the summary, so a total there would be a
guess presented as fact.

### Phase C — responsive table

4.1 used **footable**, dropped in the port with nothing replacing it. Replaced with a CSS-only stacked
card treatment below 860px.

The prerequisite was switching the exchange columns from inline `style="display:none"` +
`$.show()` to an `is-hidden` class. jQuery's `.show()` writes an **inline** `display: table-cell` on a
`<td>`, which beats any stylesheet — the responsive layout would have silently reverted the moment the
customer switched to Exchange mode.

### Phase D — picker performance (re-scoped)

Originally a visual rework; investigation found a scaling defect that mattered more.

`StyleChangeList` loaded the store's **entire catalogue**, filtered in memory, and called
`PrepareProductDetailsModelAsync` — the full product-page model — **per product**.

**4.1 does exactly the same.** `GetCompanyProducts` is a thin wrapper over `SearchProducts` with
`pageSize = int.MaxValue`; its paging parameters exist and are never passed. What protects 4.1 in
production is data shape: each client store is a uniform range of tens of products, not a retail
catalogue.

Replaced with a **two-stage picker**: a paged, searchable grid of `ProductOverviewModel` (light), then one
product's options using the details model (heavy, but once). **Same Style skips stage 1 entirely**, and
`PreselectCurrentAttributesAsync` collapsed from O(products × attributes) to O(attributes).

---

## 4. Visual improvements

Product thumbnails in both the returned and exchange columns, on step 1, the wizard table, the summary and
the account list — **attribute-aware**, so a red-to-blue exchange is visible at a glance.

The picker resolves attribute pictures with **zero extra queries**: nop has already put them on
`ProductAttributeValueModel.PictureId` by the time the details model is built, and changing a dropdown
swaps the preview client-side with no request.

> **`PictureId` means two different things.** The property on the **entity** `ProductAttributeValue` is
> obsolete since nop 4.70 (pictures moved to `ProductAttributeValuePicture`) and returns null on any store
> created since. The property on the **view model** is current. Entity-side code must go through
> `IProductAttributeService.GetProductAttributeValuePicturesAsync`. Recorded as lesson #7.

The Packname column was removed entirely; the value now appears inside the product cell **only when there
is one**. That freed enough width to bring the responsive breakpoint back down and give both product
columns equal share.

---

## 5. Architecture

### 5.1 Layering

`ExchangesController` went from 740 lines to actions only. Nine methods moved:

| Moved to | Methods |
|---|---|
| `IReturnSubmissionLogic` | `RegisterReturnRequestDataAsync`, `RegisterReturnRequestItemDataAsync`, `BuildReplacementItem`, `EnsureDraftReturnCaseAsync`, `ResolveDraftReturnCaseAsync`, `RegisterReturnCaseLineAsync` |
| `IExchangesModelFactory` | `PreselectCurrentAttributesAsync`, `BuildFeeChargeItem`, `PopulateReturnRequestSummaryAsync`, `RebuildGatheredInfoAsync` |
| `IExchangesDisplayService` | `GetReturnRequestExtraInfoAsync`, `GetProductThumbUrlAsync`, `GetPacknameAsync`, `GetSelectedAttributeMapAsync` |

`ExchangesLogic` was 985 lines holding four unrelated responsibilities; it now holds the approval workflow
and exchange-cart concerns only.

`IExchangesDisplayService` exists because those four are shared, DI-needing **read** helpers with no
workflow — §6.13's definition of business logic fits neither. Three of them living in `ExchangesLogic` was
tolerable; five would have been drift.

### 5.2 Structural cleanups

`MessagesHelper` (eight injected dependencies and an interface) became `ExchangeMessageService`;
`ProductsHelper` and `ReturnRequestSummaryHelper` became `static class`; six pass-through wrappers were
deleted. `Helpers/` now holds only genuine static helpers.

Region convention recorded in `CLAUDE.md` §7: **visibility is the primary axis, topic the secondary one** —
protected helpers stay in one `Utilities` region, subdivided by **nested** regions when large; public
methods group into named domain regions. `ExchangeService` is the reference example.

---

## 6. Performance

| Fix | Before | After |
|---|---|---|
| **Step 1 shipment lookup** | `items × (1 + shipments)` — every item re-read the same shipments | `1 + shipments` |
| **Step 1 returned units** | one query per item | one grouped query |
| **Account list receipt message** | built for **every** row, displayed on **none** | opt-in, confirmation panel only |
| **Account list extra info** | computed twice per row — once in the factory, once **in the view** | once, from entities already loaded |
| **Admin sibling case lines** | 2 queries per line | 2 total |
| **Return registration** | reason/action fetched per item | hoisted |
| **Picker (Different Style)** | whole catalogue + full details model per product | one paged query, overview models |

A ten-line, three-shipment order went from roughly 50 queries on step 1 to 4.

`CustomerReturnRequests` remains **unpaged** — inherited from nop's action, and the ceiling on that page
once the per-row costs are gone.

---

## 7. Localisation

93 resources under `Plugins.CW.Core.Exchanges.*`, grouped `Wizard.*` / `Summary.*` / `Confirmation.*` /
`ReturnRequests.*`. Seven views went from four with **zero** `@T()` calls to full coverage; verified 93
defined, 93 used, no orphans either way.

JavaScript strings come through a `text` bundle on the settings object, with a `textOf(key, ...args)`
helper doing `{0}` substitution that **falls back to the key name** so a missing resource is visible rather
than blank.

Placeholder handling: **never wrap JSON in `Html.Raw` for an HTML attribute** — attribute context needs
`"` encoded as `&quot;`, which Razor does by default. `Html.Raw` is correct for JSON inside a `<script>`
block, the opposite context. Recorded as lesson #6.

---

## 8. Deliberately left

| Item | Why |
|---|---|
| **`virtual` nav-property sweep across CW entities** | The highest-value follow-up — this family caused five defects. Worth its own task. |
| `CWPdfService` on the pre-4.60 iText API | Tracked in `CWPdfService_Modernisation_Spec.md`; includes a latent bug where setting a store PDF logo makes the header vanish. |
| `CustomerReturnRequests` paging | Measure first — per-row cost is now small. |
| Jim2/ERP sync (`ExchangesLogic:227`) | Product decision, not a code one. |
| Packname inheritance on replacements (`ExchangesLogic:342`) | Current behaviour: a replacement inherits the returned item's packname. Never confirmed as intended. |
| `ExecuteExchangeWorkflowAsync` (101 lines) | Splitting would scatter rollback logic away from what it compensates for. |

---

## 9. Merged sources

Superseded by this document and deleted:

`Exchanges_Wizard_UI_Repair_Spec` · `Exchanges_Wizard_UI_Repair_Implementation` ·
`Exchanges_Reason_Action_Cascade_Spec` · `Exchanges_Wizard_Packname_Column_Spec` ·
`Exchanges_Confirmation_Screen_Spec` · `Exchanges_ReplacementItem_Persistence_Spec` ·
`Exchanges_Wizard_Improvements_Backlog` · `Exchanges_EditExchangeItem_Implementation` ·
`Exchanges_Wizard_Improvements_Spec` · `Exchanges_PhaseA_Exact_Changes` ·
`Exchanges_PhaseB_Exact_Changes` · `Exchanges_PhaseC_Exact_Changes` ·
`Exchanges_PhaseD_Picker_Redesign` · `Exchanges_ProductThumbnails_Exact_Changes` ·
`Exchanges_Wizard_Audit` · `Exchanges_Packname_And_Step1_Images` ·
`Exchanges_Wizard_Architecture_Review` · `Exchanges_Code_Organisation` ·
`Exchanges_Method_Size_And_DB_Review` · `Exchanges_Wizard_Final_Sweep`
