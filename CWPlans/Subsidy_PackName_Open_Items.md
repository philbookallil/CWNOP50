# Subsidy and PackName — Open Items

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

**Date:** 2026-10-21 (phase 10, documentation consolidation); re-checked 2026-10-31 against the 5.00 code and the port
(`CWCore_Port_Reference.md`). **Owner:** Eduardo.

This is the one list of what the subsidy / packname fix strategy (`Deprecated/Subsidy_PackName_Fix_Strategy.md`, phases
1–12, all closed except 4c's PayPal tests) left open: items no phase owned, the code items earlier phases routed to
phase 10 and phase 10 did not take (it was a documentation phase — see the Fix Strategy §4), the pending tests, and
the findings of the 4.1 / 4.6 port audit run at closure. Nothing here is scheduled. Each item says what it is, where
the evidence is, and what it needs — a decision, a small spec, or nothing. When an item is taken up it becomes a phase
(or a change in another round) and is removed from here; the history stays in the gap analysis §17b
(`LegacyVersions/Subsidy_PackName_Gap_Analysis_And_Known_Issues.md`) and the closure records.

**The port to 5.00 (2026-10-23 – 10-31).** CW.Core was ported without changing what these items describe, so almost
every item below is still open in 5.00, with its evidence re-pointed to the 5.00 file and line. The port, its phases
6–7 and the deferred fixes closed two items and part of four others; they are listed in §8. What the port itself left
open (the uninstall leftovers, storefront access-denied, the checkout summary after the last line is removed) is in
`CWCore_Port_Reference.md` §9 and is not repeated here. "4.9" below means the code as it stood before the port, when
the behaviour is history; file and line references without a version are the 5.00 code. A bare "phase N" is a phase of
the fix strategy, not of the port.

What this document deliberately does **not** repeat: the decisions already taken (the Fix Strategy §2,
`subsidy-system.md` §8.5), the behaviour of a switched-off feature (`Feature_Toggles_Reference.md` §5), and the
items recorded "by decision / no action" in the gap analysis §17b.

---

## 1. Pending — not open, waiting on something

| # | Item | Waits on | Where |
|---|---|---|---|
| 1.1 | **N-03 — the PayPal round trip of phase 4c** (T5, T6, T8, and T4 and phase 6's T20 with them). PayPal Commerce stays **disconnected on packname stores** until they pass. The port's phase 7 did not run it either | A PayPal **sandbox** account Eduardo can sign in to (he cannot from abroad) | Tests kept verbatim in `PackName_Order_Shipments.md` §9; the hand-over design is there too |
| 1.2 | **A payment method that places its own order sets the customer's shipping address to the first destination**, and that address's admin delete then fails on nop's foreign key (`PrepareCustomerPaymentAddressesAsync`). Decide what the hand-over should leave on the customer. | The PayPal round (1.1) | gap analysis §17b "From phase 9" #4 |

---

## 2. Found by the 4.1 / 4.6 port audit (2026-10-21) — each needs a decision

The audit enumerated every user-visible surface of `NOP41` (TIG.Base / Exchanges / Subsidy / CreditNotes) and `NOP46`
(Jim2.BE / Allowances / Common) and looked for its 4.9 counterpart by behaviour, leaving out everything the Fix
Strategy §5 and the gap analysis exclude by decision. 4.6: 96 surfaces ported, 7 ported differently and recorded, 3
missing. 4.1: 34 ported, 16 ported differently (all deliberate and recorded in `Exchanges_Rebuild_Record.md`), 6
excluded, 15 missing. Verified in code where marked ✔ (re-verified in the 5.00 code on 2026-10-31).

The complete 4.1 list — every TIG plugin, the store settings page and the settings inventory — is
`LegacyVersions/NOP41_To_500_Gap_Analysis.md` (2026-11-04); each item below appears there with its number.

### 2.1 Defects — CW.Core is incomplete where it meant to match (4.9, unchanged in 5.00)

| # | Item | Evidence | Need |
|---|---|---|---|
| 2.1.2 ✔ | **Cross-store impersonation is a stub.** `ReturnsController.ImpersonateForCompletion` on a return of another store calls `ICustomersLogic.RequestImpersonation`, which fabricates a token locally (the 4.1 API client is commented out), writes nothing to `CORE_EX_ImpersonationToken`, and redirects to `{otherStore}/impersonate/useToken?token=…` — **no route serves it**. Sales Support cannot finish a pathway-B exchange for a customer of another store (each store is its own domain). `Exchanges_Returns_CreditNotes_Reference.md` §8.1 says so. | 4.1 `TIG.Base/Controllers/ImpersonationAPIController.cs`, `RouteConfig.cs:76-90`, `CustomerController.cs:1171-1196`; `BusinessLogic/CustomersLogic.cs:95-171`, `Areas/Admin/Controllers/ReturnsController.cs:586-652` (the call at `:611`) | **High** if multi-store exchanges are a real case; otherwise delete the stub, the table and the dead attributes. Decide which. (4.1's daily *Clear Invalid Impersonation Tokens* task goes with whichever is chosen.) |
| 2.1.3 ✔ | **Return eligibility is nop's stock rule.** 4.1 allowed a return on a **Processing** order whose items had shipped (partially shipped / shipped / delivered — normal for a multi-packname order shipped in parts) and counted the return window from the **last shipment**; CW.Core calls nop's `IsReturnRequestAllowedAsync` (Complete only, days from `CreatedOnUtc`). CW.Core's "only shipped units" rule is item-level and does not open the order-level gate. nop 5.00's new return rules in CW's wizard are post-port business work (`CWCore_Port_Reference.md` §5, *Returns rules*) | 4.1 `TIG.Base/Services/Overrides/Orders/OrderProcessingServiceOverride.cs:1407-1454`; `Controllers/ExchangesController.cs:185, 222`, no override in `OrderProcessingOverrideService` | **High.** Decide the rule with the manager, then one override |
| 2.1.4 | **A returned-items-only cart still pays freight.** 4.1 zeroed shipping and skipped the shipping step when the cart held only exchange replacements; CW.Core zeroes shipping only for the **synthetic** auto-placed cart, so a pathway-B customer who completes the cart pays freight plus the redelivery fee. This is manager question **Q15** (`Deprecated/Exchanges_Questions_For_Manager.md`), recorded as open — it is also a lost 4.1 behaviour (`LegacyVersions/Legacy_NOP41_Exchanges_Subsidy_Reference.md` corrected). | 4.1 `OrderTotalCalculationServiceOverride.cs:184-243`, `TIG.Base/Controllers/CheckoutController.cs:145-208`; `Services/Orders/OrderTotalCalculationOverrideService.cs:543-549` | **Medium** (High on a store that charges freight). Manager decision Q15, then a spec |
| 2.1.8 | **Order-placed e-mail copied to the billing-address e-mail** for branch-manager orders (`OrdersUseBillingAddressEmail`, a store setting) — the branch mailbox got the confirmation. No CW.Core counterpart. | 4.1 `WorkflowMessageServiceOverride.cs:193-208`, `Domain/Setting/TIGNotificationSettings.cs` | Medium–Low; ask whether branches still want it |
| 2.1.9 ✔ | **`Branch.PaymentTerms` is editable on both branch forms and read by nothing.** 4.1 printed it on the invoice under *Invoice To* for branch-manager orders. | 4.1 `PdfServiceOverride.cs:955-980`; `grep PaymentTerms` → domain, builder, forms, model, factory, the two controllers and the resource only (nothing under `Services/Common/`) | Low. Print it on `CWInvoiceDocument`, or drop the field |
| 2.1.10 | **Banking-info block on the invoice** (`TIGBankingInfoSettings`, a view component). Not in CW.Core, and not in CW.ThemeCore either (checked in the 4.90 source on 2026-10-31: no banking setting or component; ThemeCore is not yet ported to 5.00). | 4.1 `PdfServiceOverride.cs:993-1012`, `Components/BankingInfo.cs` | Low |
| 2.1.11 | **Subsidy report → Excel** (per-subsidy usage, product-group report). `SubsidyController` has no export; the subsidy admin was rebuilt (phase 3), so this is a report, not a ledger internal. | 4.1 `TIG.Subsidy/Controllers/SubsidyController.cs:147, 1911-1930` | Low; the phase 11 export (`ClosedXML`, `Services/PackNames/Bulk/PackNameBulkService.cs`) is the pattern to reuse |
| 2.1.13 | **Packname delivery addresses in My Account › Addresses.** 4.6 listed, edited and deleted the customer's own packname's addresses from nop's address pages; in CW.Core the address a packname customer adds at the checkout can be corrected or removed only by an admin (`PackNameAddressPopup`). | 4.6 `Jim2.BE/Factories/CustomerOverrideModelFactory.cs:633-666`, `Controllers/Jim2CustomerController.cs:923-1067`; `Controllers/PackNameCheckoutController.cs:747-833` (`AddAddress` POST), no storefront address override | **Medium** when `AllowHomeDelivery` + `AllowCustomersToAddAddress` are on; Low otherwise |
| 2.1.14 | **Global Generate PackNames / Lists across every store** from the Default settings page (4.6 looped every active company). CW.Core has the two buttons per store only; provisioning now runs automatically on create, so the buttons are a repair tool. | 4.6 `Controllers/Admin/Jim2SettingController.cs:353-470, 2016-2066` | Low |
| 2.1.15 | **Per-box AJAX reload at the packname checkout** (4.6 `PackNameBoxItem` re-rendered one box; CW.Core re-renders the whole cart, in one request since phase 9). | 4.6 `Controllers/PackNameCheckoutController.cs:669-690` | Low, UX only |

### 2.2 Features 4.1 had that need a product decision before anyone ports them

| # | Item | Evidence | Note |
|---|---|---|---|
| 2.2.1 | **Order Authorization** — a branch leader approved or declined PO orders by e-mail link (`Branch.ApprovalEmail`, `AuthorisationRequire`, `AuthorisationThreshold`; templates `Order.Authorization.Confirmation/Canceled`; a 90-day resend task; tokens `%OrderAuth.Token%`, `%Customer.Leader%`, `%EmailApproverList%`, `%Order.PO%`). The hold existed to delay the Jim2 send, but the feature itself is not Jim2-dependent. Not in CW.Core, not in any CWPlans document, not on the excluded list. | 4.1 `TIG.Base/Services/Orders/OrderAuthorizationService.cs:77-240`, `Controllers/OrderAuthorizationController.cs`, `Tasks/OrderAuthorizationTask.cs`, `Domain/Branches/BranchRecord.cs:34-36` | **Ask the manager** whether the business still approves PO orders this way. Medium if yes |
| 2.2.2 | **One order per branch (`HNOrderSplit`)** — a multi-branch checkout became N orders, with `%BulkOrder.BranchCode/BranchName%` tokens. CW.Core keeps **one** order with a destination per packname (rebuild record decisions 8 and 13; phase 9) — a different design that supersedes it, but never recorded as such. | 4.1 `OrderProcessingServiceOverride.cs:1165-1240`, `Domain/Setting/BulkOrderSettings.cs` | Confirm with the manager that one order per checkout is wanted; then record the supersession and close |
| 2.2.3 | **Per-store yearly quantity cap by product tag, and "the free product must be removed first"** (Goodstart / Stride / TEEG — store names hard-coded in 4.1). `RestrictToAllocatedQty` is per subsidy, not per tag per year. | 4.1 `ShoppingCartServiceOverride.cs:735-805` | Ask whether those stores are coming to 5.00; a port needs a setting-driven shape |
| 2.2.4 | **Permissions collapsed.** 4.1 had `AccessSubsidy` and `ManageProductGroups`; 4.6 had `ManageCompanies`, `ManagePackNames`, `ManageJim2BESettings`. CW.Core gates stores, branches, lists, packnames **and** subsidies with one `CW.CORE.Admin.AccessMultiStore`, so a role cannot manage packnames without managing stores. | `Areas/Admin/Controllers/SubsidyController.cs` (all 35 actions carry the one permission) | Decide whether finer roles are wanted before go-live |
| 2.2.5 | **General B2B settings 4.6 had and CW.Core does not**: `HideCustomerInfo`, `DisableAddAddress` / `DisableEditAddress`, `HideBillingAddressAtCheckout` (+ roles — the packname checkout always hides billing), `DefaultCustomerAddress`, `RestrictLoginToDomain`, `ZipPostalCodeValidationEnabled`; 4.1's `TIGStoreSettings.HideBranchAddress`, `LoginRedirectUrl` (CW.Core has `IPackNameLandingService` instead), `MaxBulkOrderItems`, `EnableBranchCodeField`, `subsidysettings.tig.allowperemployee` / `defaultmode` (per-employee subsidy mode for manager carts — CW.Core always charges each line's packname). | 4.6 `Domain/Settings/Jim2DefaultSettings.cs:321-345`, `Jim2CustomerSettings.cs:11-28`; 4.1 `Domain/Setting/*` | All Low; list for the manager, port only what is asked for |

---

## 3. Code health left in the code (Q-01 residue and what phases routed to phase 10)

Verified present on 2026-10-21 in 4.9, and again on 2026-10-31 in 5.00 (line numbers are 5.00's). None blocks
anything; together they are one small code-health round, or they are picked up "when next touched" (`CLAUDE.md` §0.8
permits deleting dead code on sight). File paths under `Plugins/Nop.Plugin.CW.Core/`. Items 3.11 and 3.18 are closed
(§8).

| # | Item | Where | Note |
|---|---|---|---|
| 3.5 | **Three allocation filters duplicate the narrowed `ApplyAcl`** (since the catalogue gate, phase 5 Part 2): harmless, "remove or keep as a second check". | `Services/BulkOrder/BulkOrderService.cs:113-115` (`ApplyVisibilityAsync`), `Services/Catalog/ProductOverrideService.cs:508-516` (`GetCategoryFeaturedProductsAsync`), `:518-526` (`GetManufacturerFeaturedProductsAsync`); same pattern `:528-536` (homepage) and the restricted branch of `SearchProductsAsync` (`:144`, after the fast path at `:173`) | Decision: one source (the gate) or belt-and-braces |
| 3.6 | **Duplicated code in `MultiStoreController`**: the "0 → null, `Manager = false`" normalisation ×5 (around `:1367, 1415, 1520, 1570, 1881`); `SaveBranchPackName` / `SavePackName` near copies (`:1328`, `:1463`); the "clear the previous customer's context" block ×2 (`:1429-1439`, `:1584-1594`). | `Areas/Admin/Controllers/MultiStoreController.cs` | One helper each; or an `IPackNameLogic` |
| 3.7 | **`*CacheEvenConsumer` typo** in six class and file names. | `Services/MultiStores/Caching/CostCenterCacheEvenConsumer.cs`, `DepartmentCacheEvenConsumer.cs`, `Services/PackNames/Caching/DepartmentManagerCacheEvenConsumer.cs`, `PackNameCacheEvenConsumer.cs`, `PackNameListCacheEvenConsumer.cs`, `PackNameListManagerCacheEvenConsumer.cs` | Rename; the docs name them as they are |
| 3.8 | **Projections inheriting `BaseEntity`**: `SubsidyPackNameDTO`, `SubsidyProductGroupDTO`. | `Domain/Subsidies/DTOs/SubsidyPackNameDTO.cs:12`, `SubsidyProductGroupDTO.cs:5` | Cosmetic |
| 3.10 | **Admin subsidy CSS registered from `EventConsumer` (route-gated) instead of the views that need it** (`CLAUDE.md` §9.8). | `Services/EventConsumer.cs:625-638` | Minor |
| 3.16 | **`UpdateShoppingCartItemAsync` gates the quantity cap on the current store** where the add path and the warnings use the line's store. Same store wherever a customer edits a line; differs only for a cross-store admin call. | `Services/Orders/ShoppingCartOverrideService.cs:660` vs `:847`, `:1156` | Use the line's store |
| 3.17 | **`U-42` — stale XML doc comments**: phantom `<param>` names (e.g. `BusinessLogic/CustomersLogic.cs:92` `impersonatedUser` vs `impersonatedUserId`; `Extensions/NopEntitiesExtensions.cs:43`; `Factories/PackNameCheckoutModelFactory.cs:703, 738`; `Factories/SubsidyCatalogModelFactory.cs:85`; `Filters/CoreActionFilter.cs:317`; `Services/Messages/ExchangeMessageService.cs:140, 141`; a scan on 2026-10-31 found 19 in all, among them the `languageId` params of `ExchangeWorkflowMessageService.cs:141, 194`), three casing mismatches (`Services/PackNames/PackNameService.cs:265, 794`, `Services/Subsidies/SubsidyService.cs:288`), about twenty blocks with missing tags. | as listed | Comments only (Eduardo gave standing permission to fix comments) |
| 3.19 | **Legacy SQL branch**: `vw_CORE_SUB_SubsidyPackNameCanOrder`'s outside-period arm still guards `OrderOutsidePeriod = 1 AND RestrictToAllocatedQty = 1`, a pair `SaveSubsidyAsync` makes impossible; a row written otherwise would silently block ordering (`subsidy-system.md` §3.1). | `Services/DataBase/CWDatabaseService.cs:824-835` | Simplify to `OrderOutsidePeriod = 1`, or leave as documented |
| 3.21 | **Literal admin URLs in `Content/Admin/MultiStore/admin.store.navigation.js`** — 16 `'/Admin/MultiStore/…'` strings (around lines 528, 911–990, 1136), contrary to §9.14. A `.js` file cannot use `Url.Action`, so they need a config div on the host page (Manage Store). Found by the R2-C review, 2026-11-12. | `Content/Admin/MultiStore/admin.store.navigation.js` | Config div on `MultiStore/Edit`, read once by `Admin.StoreNav` |

---

## 4. Functional items no phase owned

| # | Item | Evidence | Note |
|---|---|---|---|
| 4.1 | **A manager opening his employees' orders.** nop's order page opens only the customer's own orders; a manager reading an employee's *My Subsidies* sees that employee's orders as plain sentences (decision 2026-10-08: the rows are not links). Making them openable needs a packname-aware exception on nop's order page. | phase 7 #11, phase 8 Part 1 #10, Part 2 #11 | Its own small phase if wanted |
| 4.2 | **Hidden packname fields are saved as posted.** The storefront and admin edit forms save Employee number, Print name, PO entry and Comments even when the store's setting is off, so an edit clears a value imported while the setting was on; with gender off an edit stores none. 4.6 did the same. | phase 8 Part 2 #1 | Decide whether hidden fields keep their value |
| 4.3 | **One name per list is checked, not enforced by the database.** Two registrations at the same instant can both pass; under *read committed snapshot* (Azure SQL's default) the import's in-transaction re-check does not wait for another import's uncommitted rows. Only a unique index on (list, name) closes it, and `SchemaMigration` creates none. Since the deferred fixes provisioning checks the name too (a clash is skipped and reported), so every insert path now checks, but nothing enforces. | phase 8 Part 2 #2; deferred fix 3 (`CWCore_Port_Reference.md` §8) | Decide once the deployment database is chosen |
| 4.4 | **Generate PackNames provisions inactive customers** (`GetCustomersWithoutPackNameByBranchAsync` skips deleted customers only — `Services/MultiStores/MultiStoreService.cs:1054`). Pre-existing, P-15's code. | phase 8 Part 2 #3 | Decide whether an inactive customer gets a packname |
| 4.6 | **Admin and storefront word the duplicate-name and too-long messages differently.** | phase 8 Part 2 #7 | Cosmetic |
| 4.8 | **Performance follow-ups** (measured on 4.9 dev data): the checkout still takes ~12 s for a manager with 45 packnames and the time is C#, not SQL (profile with MiniProfiler); the catalogue is slower in the browser than before phase 9 (3.1 s vs 1.9 s — two credit reads of ~470 ms); each selector request spends 60–70 ms of SQL in `vw_CORE_SUB_SubsidyPackNameCanOrder` (20–40 ms whatever the id count) and `vw_CORE_PN_PackNameShoppingCartItem` (~20 ms) — read both plans. The port's phase 7 found 5.00 as fast as or faster than 4.9 on every storefront page compared (packname checkout 62 / 158 ms, subsidy catalogue 121 / 283 ms, warm medians on `testnop50`), but on smaller data: the 45-packname case was not measured. | phase 9 #1–#2, phase 11 #1; `CWCore_Port_Reference.md` §2 | One performance round, when the numbers matter (on Odoo-synced data) |
| 4.9 | **No billing address of record**: the checkout's totals use the store's default tax address while the order bills the customer's own (T27, order 163: shown $25, charged $20) — only where tax differs by address; and **a customer with no address at all cannot check out** ("No billing address allocated"; the packname registration always creates one, an account made otherwise may not). | phase 9 #3–#4 | Decide whether the checkout offers the packname / branch address as billing |
| 4.11 | **The packname-aware rebuild of nop's admin order screens** (O-01's second half): `PackNameOrderAdminGuardFilter` blocks edit / add / address / totals / shipping-method on a packname order; a rebuild would have to re-run settlement. Decision 14 chose "block". Since the port's part 4f the order page no longer offers *Edit order totals* or the billing *Edit* on a packname order. | O-01, `subsidy-system.md` rule 23 | Not planned; here so it is not forgotten |
| 4.14 | **Three refusal shapes in one plugin** (phase 2b finding 33): `new ForbidResult()` renders nop's `/page-not-found`, `AccessDeniedView()` renders `/Admin/Security/AccessDenied` in admin and a 404 on a public controller, `new StatusCodeResult(403)` is a real 403. All refuse, so nothing is insecure, but an admin who trips a `ForbidResult` sees a 404. "One refusal shape per surface." The port met the storefront side again: `PackNameController` (26 calls) and `CustomerSubsidiesController` use `AccessDeniedView()`, which lands on *Page not found* (`CWCore_Port_Reference.md` §9). | `Subsidy_Phase2b` record (consumed) | A sweep |
| 4.15 | **Cosmetic**: the E / R badge wraps under a 3-digit order number in the narrow column (phase 6 Part 2 #3); the admin packname panel's top-up badge says the period is "closed" on an assignment that has not opened yet (`Areas/Admin/Views/Subsidy/_PackNameSubsidies.cshtml:62`, phase 7 #3); the subsidy catalogue header prints `$0.00` for a packname with no money credit at all (`Factories/SubsidyCatalogModelFactory.cs:349`, phase 7 #4); the collapsed checkout box header keeps the old address / method name until a reload (phase 8 Part 1 #5). | as listed | Only if wanted |
| 4.16 | **Admin → product → *Add new tier price* does not save on the dev site** (popup re-renders, no message, nothing logged; editing works). No CW code found on that path. Seen on 4.9; not re-tried on 5.00. | phase 5 Part 1 #8 | Investigate once on 5.00; may be nop's |
| 4.18 | **`PackName/SelectPackName` accepts a closed packname when posted directly** (the UI never offers it; `Controllers/PackNameController.cs:531`). | phase 4a Part 2 #2 | No action unless a reason appears |
| 4.19 | **Deleting a branch leaves its packname lists and packnames live.** `MultiStoreController.DeleteBranch` refuses a branch with sub-branches, then soft-deletes it; its lists, their packnames and their managers stay as they were, under a deleted branch (the tree no longer shows them). Since R2-B a manager in such a list has no store (`GetManagerStoreIdAsync` refuses it), so the storefront selector and the manager actions treat them as having none. Found by the R2-B review (2026-11-12). | `Areas/Admin/Controllers/MultiStoreController.cs` (`DeleteBranch`), `Services/Branches/BranchService.cs` (`DeleteBranchAsync`) | Decide: refuse deleting a branch that still has lists (like sub-branches), or delete / move its lists with it |

---

## 5. Carried to "the next exchanges round" — none has opened

| # | Item | From |
|---|---|---|
| 5.1 | **T16 was never run**: an exchange replacement added to a packname cart under `ExchangeReplacementScope` (no return case was available to approve in that run). | phase 4a Part 1 #1 |
| 5.2 | **An exchange replacement in a closed packname cannot be paid through the packname checkout** (the cart view hides it), and **an exchange cannot be authorised once the customer's packname is closed for ordering** (F8). | phase 4a Part 1 #2, phase 6 Part 2 #2 |
| 5.6 | `_SummaryTotals.cshtml:35-38` duplicates `_FeesAndChargesList.cshtml:17`; the pure `Plan(ReturnResolutionInput)`; collapsing the three rule tables into `ReturnPolicy` (the rest of B7: the rule clean-up on delete was done on 2026-11-09). | `Exchanges_Rebuild_Record.md` §9.1 |
| 5.7 | **Manager questions Q15** (delivery + redelivery double charge — item 2.1.4 above) **and Q16** (billing address choice). | `Deprecated/Exchanges_Questions_For_Manager.md` |

---

## 6. Deployment checklist — not code

| # | Item |
|---|---|
| 6.1 | **`DefaultStoreTimeZoneId` is load-bearing for money** — set the real store zone before go-live, per store if the estate spans zones. The 4.9 dev database had *SA Pacific Standard Time* at StoreId 0 (`subsidy-system.md` §3); check `testnop50` and the production database the same way. |
| 6.2 | **SQL Server 2016 SP1 or later** (`CREATE OR ALTER`); the deployment server is not chosen. **Read committed snapshot** (Azure SQL's default) changes what item 4.3 lets through. |
| 6.3 | **Multi-instance**: every lock in the plugin (subsidy settlement included) needs `DistributedCacheConfig.Enabled` on, or it is decorative (`Exchanges_Rebuild_Record.md` §6.0). Phase 1's unique indexes and phase 6's persisted settlement row hold without it. |
| 6.4 | **`cw-icons.svg` is cached for a year with no version in its URL** (`Extensions/CWIconHtmlHelperExtensions.cs:13`) — a browser holding an old copy shows a new icon blank until it reloads the file. |
| 6.5 | **A plugin never to deploy**: `Misc.RFQ` can be uninstalled (it cost ~1,300 queries per packname checkout action on 4.9). It is installed on `testnop50`, and its *Request a quote* button shows on the packname checkout (`CWCore_Port_Reference.md` §9). |

---

## 7. How to use this list

Pick an item, open a phase for it (or add it to the round named in §5) with its own document under `CWPlans/`, and
when it closes **remove it from this list** (the gap analysis §17b and the phase's closure record keep the history). Items
decided "no action" are removed the same way, with the decision recorded in the Fix Strategy §2. This document replaces the "Unowned" cells scattered through the gap analysis §17b as the place to
look.

---

## 8. Closed by the port to 5.00 (2026-10-31)

Recorded once here so the numbering above has no unexplained gaps; remove this section at the next pass.

| # | Item | Closed by |
|---|---|---|
| 3.11 | Dead stylesheet `Content/Exchanges/html-subsidy-credits-page.css` and its csproj entry | Not ported: one of the eight files dead in 4.9 that the port left behind (`CWCore_Port_Reference.md` §1). Neither the file nor the entry is in 5.00 |
| 3.18 | `CW.Common/Helpers/HttpContextHelper.GetRequestParam` returned the parameter name instead of its value | Fixed in `CW.Common` on 2026-10-22, before CW.Core came across: it returns the value (`CWLibraries/CW.Common/Helpers/HttpContextHelper.cs:78-92`, with a comment saying so); `CoreActionFilter.GetRequestParam` (`Filters/CoreActionFilter.cs:172-174`) passes it through |
| 6.6 | Check the dev database for the two approval templates, and for `multistoresettings.genderfiltering` rows | Answered. The templates are absent on the fresh 5.00 install and the e-mails do not go out (phase 6 of the port; folded into item 2.1.1, which round 1 part A closed on 2026-11-09). A fresh install has no `genderfiltering` rows, and nothing in the 5.00 code reads or writes the name |
| 6.5 (part) | The `vw_TIG_SUB_*` views left on the dev database from before the table rename | 5.00 starts from a new database (`testnop50`, port decisions D1/D2): the views never existed there, and no code creates them |
| 3.9 (part) | The commented `Order` line in `Infrastructure/NopStartup.cs` | Gone in 5.00 (the item keeps the rest) |
