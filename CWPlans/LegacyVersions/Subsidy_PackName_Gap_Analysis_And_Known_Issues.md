# Subsidy and PackName — gap analysis (4.6 → 4.9), known issues and improvements

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

**Date:** 2026-09-09; **reconciled 2026-10-21** (phase 10). **Status:** the fix strategy that worked this list is
complete (`Subsidy_PackName_Fix_Strategy.md`); every item a phase closed is struck through with the phase and date, and
**what is still open — unowned items, the code items the phases left behind, the pending PayPal tests and the findings
of the 4.1 / 4.6 port audit — is `Subsidy_PackName_Open_Items.md`**, the one list to work from now. This document is
the record of what was found and what each phase did about it (§17b).

This document is the single list of everything known to be missing, wrong or improvable in the 4.9 PackName and
Subsidy implementation (`Plugins/Nop.Plugin.CW.Core`). It merges:

- the ten items already raised on 2026-08-27 / 2026-09-04 (kept, renumbered `K1`–`K10`, updated with new evidence), and
- a full read of the nopCommerce **4.6** implementation (`NOP46/Nop.Plugin.Jim2.BE` for packnames, `NOP46/Nop.Plugin.Jim2.Allowances`
  for subsidies — "Allowance" in 4.6 is "Subsidy" in 4.9) against the 4.9 code, file by file.

Deliberately **out of scope** and not reported as gaps: contract items, ACL on product attributes / attribute values /
pictures, Jim2 card files and sync, the 4.6 Company model (replaced by Store + Branch + Department + Cost Centre),
pre-orders / approvals, quotes, buffer stock, favourites, Jim2 notifications, SevenSpikes, Voyage/Pavilion theme views,
bulk ordering (tracked separately).

Every claim below was checked against the source. Line numbers are those of the code on 2026-09-09; treat them as a
pointer, and anchor on the quoted text if the file has moved (lesson #33).

---

## 1. How to read the priorities

| Priority | Meaning |
|---|---|
| **Blocker** | A customer or manager cannot complete a core flow, or money / subsidy credit is computed or consumed wrongly, or data is silently lost. Must be fixed before go-live. |
| **Issue** | Wrong or missing behaviour with a limited scope or a workaround, or a security hole that needs a logged-in user to exploit. Fix before or shortly after go-live. |
| **Improvement** | Works, but the design, performance or maintainability should be better. |
| **Nice-to-have** | Cosmetic, optional, or a small clean-up. |

**Kind** says where the finding comes from: *Missing* (4.6 had it, 4.9 does not) · *Wrong* (ported, but 4.9 behaves
differently and incorrectly) · *Bug* (a 4.9 defect regardless of 4.6 — several are inherited from 4.6 verbatim) ·
*Gap* (incomplete or inconsistent 4.9) · *Changed* (a deliberate or accidental design change that needs a decision) ·
*Improvement*.

**Size**: Small (hours), Medium (days), Large (a week or more).

Counts: **10 Blockers**, **54 Issues**, **20 Improvements**, **20 Nice-to-have** (K2–K9 are folded into new items and
not counted twice). Fourteen decisions are needed before some of them can be built (§17).

**Phase 1 closed 2026-09-16** — struck through below, with what it actually shipped:
S-01, S-02 (Blockers), S-09, S-10, S-11, S-13 (Issues), S-20 (Nice-to-have). Two further defects
were found *while applying it* and fixed in the same pass: **S-22** (Blocker) and **S-23** (Issue,
fixed out of phase order). **Six Blockers remain open**: S-03, S-04, S-05,
P-01, P-03, P-04 (of the ten ever raised, P-02, S-01, S-02 and S-22 are closed). Phase 1's groundwork for **H-01** is in place — the `subsidy_current_period` iTVF exists, so
the credit views can `CROSS APPLY` it instead of calling scalar wrappers in the `WHERE`; that
rewrite is still phase 9's.

**Phase 3 closed 2026-09-29** — admin subsidy screens: S-12, S-16, S-18, S-19, S-24, S-27, S-28, A-02,
A-03, A-04, A-05, A-06, A-07 and M-06's admin half, plus the variation-aware end S-11 left open; S-04 and
S-14 were found already closed by phase 2b. **Blockers open at phase 3 closure: S-03, P-03 and N-03** — the
first two from the original ten, N-03 raised since (phases 4a, 5 and 4c own them).

**Phase 4a Part 1 closed 2026-09-30** — one cart, one gate: S-03 (Blocker), C-01, C-02 / K10, C-10, P-08,
G-06, G-08, G-09, plus S-31 found and fixed during it (subsidy rows restricting a store with subsidies off).
**Blockers open: P-03 (phase 5) and N-03 (phase 4c).** Record: project `claude/Subsidy_Phase4a_Part1_Closure_Record.md` / `…Part2…`.

**Phase 4a Part 2 closed 2026-09-30** — what the customer sees: G-13, U-06 (no-address warning; the
shipping-method warning deliberately not brought back), G-10, K1, G-04, G-05, O-03, and Part 1's selector
item. **Phase 4a is closed.** Next: phase 4b.

**Phase 5 Part 1 closed 2026-10-01** — department ACL: **P-03 (Blocker)**, plus a second half of it found
while testing — a department chosen when *creating* a category, manufacturer or product was lost until a second
save (closed in the same phase). **P-13** is partly closed: the default packname roles now count for what a
packname without an account sees, but are still not assigned at registration. **Blocker open: N-03 only (4c, its
PayPal tests).** Record: project `claude/Subsidy_Phase5_Part1_Closure_Record.md`. Next: phase 5 Part 2 (C-04, C-03).

**Phase 5 Part 2 closed 2026-10-01** — the allocation gate everywhere: **C-04** (with K9) — when a packname's
subsidies restrict it to its allocated products, the product, category and manufacturer pages, the menu, the
related / cross-sell / also-purchased blocks, grouped-product pages, the wishlist, add-to-cart, bulk ordering and
the exchange replacement picker all follow the allocation, and a closed ordering period hides the catalogue.
**C-03** closed by deleting the `GenderFiltering` setting (never read). **Phase 5 is closed.** **Blocker open:
N-03 only (4c, its PayPal tests).** Record: project `claude/Subsidy_Phase5_Part2_Closure_Record.md`. Next: phase 6.

**Phase 6 Part 1 closed 2026-10-02** — money semantics and one settlement summary: **C-05** (subsidy and store credit
settle the goods and shipping before any customer discount; discounts, gift cards and reward points reduce only the
balance due), **C-06** (nop's gross columns kept, `OrderTotal` = payable, subsidy a tender — decision 6), **C-07** and
**M-03 / K6** (one persisted settlement row per order, `CORE_PN_OrderSettlement`, read by the customer page, admin cards,
admin packname card, e-mail, invoice PDF and profit report), **K5** (per-line subsidy on the customer's order page),
**C-08** and **S-15** (by decision, documented in `subsidy-system.md`), 4b's carried item 1 (the quantity cap re-checked
at placement), and R3 found in review (a subtotal discount limited to what the customer still owes). 28 of 29 tests;
T20 (PayPal hand-over) waits with 4c's PayPal tests. **Blocker open: N-03 only.** Record:
project `claude/Subsidy_Phase6_Part1_Closure_Record.md`. Next: phase 6 Part 2 (O-02, O-04).

**Phase 6 Part 2 closed 2026-10-07 — phase 6 is closed.** **O-02**: the invoice and the packing slip print, under each
packname line, who it is for (`For: packname · department · cost centre`, empty parts left out); the packing slip now
also prints product attributes (`CWShipmentDocument`), and the invoice prints a subtotal discount and an order discount
on separate rows (nop's document overwrote the first with the second). **O-04** (with K8): admin *Sales → Orders* and a
customer's *Orders* tab show **E** on an order that holds an exchange replacement (4.1's badge) and **R** on an order
with an authorised return (new); an order can show both. **F6** (Part 1 carried item 5): one set of labels on every
surface — *Total before subsidy and credit*, *Subsidy applied*, *Subsidy (items)*, *Subsidy (shipping)*, *Store credit*,
*Subtotal discount*, *Order discount*, *Exchange fees*, *Payable balance* — deductions as negative amounts for customers
and bracketed in admin. 17 of 17 tests. **Blocker open: N-03 only.** Record: project `claude/Subsidy_Phase6_Part2_Closure_Record.md`
(decisions §11.1 and §11.6, results §11.7). Carried forward: §17b "From phase 6 Part 2".

**Phase 7 — storefront features — closed 2026-10-08.** **M-01** (K2–K5): *My Subsidies* at `customer/subsidies` for the
selected packname — one card per assignment with status, remaining and a usage bar, product groups and the history
folded (decision A; mock-up approved). **M-06** storefront half: units shown, no `+ $0.00` on a units-only group.
**M-02**: subsidy checkboxes on the manager's add / edit form, gated server-side. **P-13 / P-14 / X-03**: a
manager-created customer gets the store's default roles, a random password nobody sees and the welcome e-mail
(`Customer.Subsidy.CustomerRegistrationImport`) with nop's set-password link and the packname / subsidy tokens; the
five dead template names, token groups and attribute names deleted. **P-10**: print name pre-filled on the product
page. **N-02**: the packname order-completed page, reached from every path through the checkout filter. **F2 / F3**:
the fee of the pre-selected payment method on first load, the cap warnings before Confirm. **M-04 / P-18**:
`SubsidySendUserAfterLogin` and one landing resolver for sign-in, selection and impersonation (decision H, as 4.6).
**S-30**: the top-up dialog header. Decision J (out-of-period reads "—"). Recorded only: **M-05**, **P-09** (I),
**G-01** (14 / G), top-up corrections (K), shared-address shipping rate re-homed to phase 8 (L). Also: the Store Credit
page rebuilt in the same style with its My Account item highlighted (it never was). 72 changes (63–72 after closure,
see §17b), 15 unit tests, 19 of 19 checks. The phase document is consumed; what survives is in §17b "From phase 7",
`subsidy-system.md` §8.3–§8.5, `MultiStore_Management_Reference.md` §8e and the project closure record. Next: phase 8.

**Phase 8 Part 1 — settings that do nothing — closed 2026-10-11.** **N-01**: both store settings screens and both branch
forms (Manage Store panel, standalone *Branches* page) offer the methods the CW.Freight plugin offers in the store
(`IPackNameShippingMethodService`); a branch's selection overrides the store's, the first listed method is the default,
and every save keeps only offered names. **Shared address** (4a Part 1 carried #5): each box keeps its own rate and a
destination shared by several packnames gets one line — "Shipped in one parcel with X. Shipping to this address is
charged once: $R" — with the figure the total charges. **P-15**: `UseBranchesAsPackNameList` gives a new branch its
default list and `UseStaffAsPackName` gives a packname to a customer an admin links to a branch
(`IPackNameProvisioningService`, PackName feature-gated; the Generate buttons use the same service). **X-02**: store
self-registration deleted (entity, settings, validator rule, fields, slug block, resources). **T-01** closed except the
X-01 rows. Found in the run: **F1** — the three settings POSTs rewrote the automation overrides from unposted (disabled)
fields; **F2** — the standalone *Branches* page (kept: the manager wants it, as 4.1) erased branch methods, never
provisioned, could loop or cross-store a parent and deleted branches with children; `DomainRegistrationFilter` left the
UI. 80 changes, 12 unit tests, 30 of 30 checks. The phase document is consumed; what survives is in §17b "From phase 8
Part 1", `MultiStore_Management_Reference.md` §8f, `Feature_Toggles_Reference.md` §4.3, `CLAUDE.md` §13 / §14 / §15.3 and
the project closure record. Next: phase 8 Part 2 (X-01 Excel import).

**Phase 8 Part 2 — X-01 Excel import — closed 2026-10-19.** **X-01** ported: a manager with *Allow managers to add
packnames* and *Allow to add by Excel* opens `PackName/ImportPackNames` from the Excel icon beside the selector's plus,
downloads a template whose columns follow the store's settings (subsidy groups per `AllowSubsidyCountByExcel`, the
account and delivery-address blocks, custom attributes), and uploads it. Every row is checked first — against itself,
the store and nop's own `CustomerInfoValidator` / `AddressValidator` — and nothing is written unless all pass; the rows
are then written ten per transaction, so the store's other pages keep loading (500 rows with accounts in about 25 s). A
refused file lists its rows on the page and offers the file back with an *Errors* column; a row refused while it is
written stops the import at its batch and the page says what the batches before it created. The add form and the
import share one registration service (`IPackNameRegistrationService`: account, branch link, roles, own copy of the
delivery address, subsidies, welcome e-mail, in one transaction); one name per list on every manual save path; a branch
with no active list gets one. **T-01** closed (`AllowToAddByExcel`, `AllowSubsidyCountByExcel` now read, the count
validated 1–10). `DeleteSubsidyVariationAsync` deleted. 119 changes (the import page redesigned in 115–119 after the
second run), three test runs, 71 checks in the last (66 pass, 4 info, T58 pass with Eduardo browsing). The phase
document is consumed; what survives is in §17b "From phase 8 Part 2", `MultiStore_Management_Reference.md` §8g,
`Feature_Toggles_Reference.md` §4.4 and §5, `CLAUDE.md` §13.4 / §15.3, `tasks/lessons.md` #110–#119 and the project
closure record. Next: phase 12 (the admin Excel import, X-04 — Eduardo, 2026-10-19), then phase 9.

**Phase 12 — X-04 admin Excel import — closed 2026-10-20.** **X-04** done: *Manage Store › PackNames › Import from Excel*
(`/Admin/MultiStore/ImportPackNames?storeId=N`) opens the storefront import's page body (now the shared partial
`_PackNameImport.cshtml`) inside the admin card, with the same template, checks, ten-row batches, result panel and error
report. Gated by `ADMIN_ACCESS_MULTISTORE`, the store's PackName switch and a store that exists — not by *Allow managers
to add packnames* or *Allow to add by Excel* (as 4.6). The administrator is the creator (activity type `ImportPackNames`,
`SubsidyPackName.CreatorId`, variation history); the manager settings (`AllowToAddCustomerToPackName`,
`AllowSubsidySelection`, `LimitAssignedBranches`) shape only a manager's import, so an administrator gets every active
branch, the account block and the subsidy columns when the Subsidy feature is on; welcome e-mails in the store's default
language, else the administrator's. The file check, the in-memory copy of the upload and the page's JSON moved into the
service so both controllers share them. 21 changes, 10 unit tests (273 pass), 20 checks (19 pass, 1 info: store 1 has no
default language, so T12 used the fallback); 500 rows with accounts in 26.5 s with another customer browsing. The phase
document is consumed; what survives is in `MultiStore_Management_Reference.md` §5 / §8g, `Feature_Toggles_Reference.md`
§4.1 / §4.4, `CLAUDE.md` §13.4 / §15.3 / §20, `tasks/lessons.md` #120 and the project closure record. Next: phase 9.

**Phase 11 — packname bulk actions and selector (U-13) — closed 2026-10-21.** **U-13** done, both halves. Both admin
packname grids get a per-grid selection, Activate / Deactivate / Move to list / Delete and an Excel export, through one
toolbar and one script; every row is checked with the single save's and delete's rules, ten per transaction, each batch
reading its rows and the move target again. The storefront selector sends the first 50 packnames plus the selected one and
those with cart lines, and searches the server (name, and employee number when shown); the badges refresh alone. Fixed on
the way: cart counts written onto cached rosters (shared between managers), the manager's own packname added to a cached
list, the list row counting deleted managers, and the remembered selector list not following a moved packname. 38 changes,
3 new unit-test classes (312 pass), 24 checks + 4 measurements all pass; the answer for a list of 602 fell from 180,597 to
16,065 bytes; 500 packnames in one action in 1.4–1.6 s with another customer browsing. The phase document is consumed; what
survives is in §17b "From phase 11", `MultiStore_Management_Reference.md` §5 / §8h, `Feature_Toggles_Reference.md`,
`tasks/lessons.md` #125 and the project closure record.

---

## 2. Executive summary — the Blockers

| ID | What a user would see | Kind | Size |
|---|---|---|---|
| S-01 | An **annual** subsidy (e.g. July–September) is treated as "in period" from **1 January** of every year after the first. Credit is spent outside the campaign. Inherited from 4.6 SQL. | Bug | Small |
| S-02 | For an annual subsidy, **every order ever placed** before this year's end date counts as used credit — the balance never resets in year 2. Inherited from 4.6 SQL. | Bug | Small |
| S-03 | ~~A packname whose subsidy period has closed (and `OrderOutsidePeriod` is off) is **not blocked anywhere**: full catalogue, add-to-cart succeeds with a success message, the lines then vanish from the packname checkout. 4.6 hid the catalogue, refused the add and showed "cannot order".~~ — **closed by phase 4a Part 1, 2026-09-30** | Missing | Medium |
| S-04 | ~~The admin **variation popup** (per-packname credit / dates override) cannot open: the view binds to a property the model does not have.~~ — **closed by phase 2b**; confirmed by phase 3, 2026-09-29 | Bug | Small |
| S-05 | ~~A manager can **top up any packname in any store** from the storefront "Change topup" dialog by posting another packname's id.~~ — **closed by phase 2a, 2026-09-17** | Bug | Small |
| P-01 | ~~nop's **standard checkout** (`/onepagecheckout`, `/checkout/billingaddress`, …) is still reachable for a packname customer and places an order with **no packname, no destinations, no subsidy**. Same hole existed in 4.6.~~ — **closed by phase 2a, 2026-09-17** | Bug | Small |
| P-02 | ~~Placing a packname order **clears the whole nop cart**, deleting lines the packname checkout never showed.~~ **Fixed 2026-09-11 (fix 22, exchanges phase 4b-4).** Placement now deletes exactly the rows that became order items, as 4.6 did. | Wrong | Small |
| P-03 | ~~**Department visibility (ACL) is inert**: admin can restrict a category/product to a department, but no customer ever carries the department role, so the item disappears for everyone — including that department.~~ — **closed by phase 5 Part 1, 2026-10-01** | Missing | Medium |
| P-04 | ~~Adding or removing a **list manager** throws a `FormatException` inside a cache consumer (swallowed and logged), so the manager's scope stays cached with the old value — the manager keeps (or fails to get) access.~~ — **closed by phase 2a, 2026-09-17** | Bug | Small |

One more item is one decision away from Blocker: **C-05 order-level discounts / gift cards applied before subsidy**
(moves money between employer and employee compared with 4.6). ~~X-01 Excel import~~ — **ported by phase 8 Part 2,
2026-10-19**.

---

## 3. Master index

Area codes: **S** subsidy rules and data · **C** subsidy at cart/checkout/order · **A** subsidy admin UI · **M**
subsidy storefront (customer / manager) · **P** packname context, managers, visibility · **G** packname cart and
checkout · **O** packname order placement, order display, emails, PDF · **U** packname admin and public UI · **X**
missing features (import, registration, emails) · **T** settings that do nothing · **H** caching and performance ·
**Q** code health.

| ID | Title | Kind | Priority | Size | Decision? |
|---|---|---|---|---|---|
| S-01 | ~~Ordering-period SQL: AND/OR precedence opens the period from 1 Jan~~ — **closed by phase 1, 2026-09-16** | Bug | ~~Blocker~~ Done | S | `subsidy_current_period` |
| S-02 | ~~Used-credit SQL: annual recurrence never resets consumption~~ — **closed by phase 1, 2026-09-16** | Bug | ~~Blocker~~ Done | S | `subsidy_order_is_in_current_period` |
| S-03 | ~~"Cannot order" gate exists in SQL but is enforced nowhere~~ — **closed by phase 4a Part 1, 2026-09-30** | Missing | **Blocker** | M | wording |
| S-04 | ~~Admin variation popup broken (`CompanyId`) + StoreId not posted~~ — **closed by phase 2b**; confirmed by phase 3, 2026-09-29 | Bug | ~~**Blocker**~~ Done | S | |
| S-05 | ~~Storefront change-topup has no ownership / store check~~ — **closed by phase 2a, 2026-09-17** | Bug | ~~**Blocker**~~ Done | S | |
| S-06 | ~~`OnlyAllocatedProducts` ignores the manager exemption (`IncludeManagers`)~~ — **closed by phase 4b, 2026-09-30** | Wrong | ~~Issue~~ Done | S | |
| S-07 | ~~`RestrictToAllocatedQty` is checked per cart line, not per product group~~ — **closed by phase 4b, 2026-09-30** | Wrong | ~~Issue~~ Done | S–M | |
| S-08 | ~~`RestrictToAllocatedQty` exemptions differ from 4.6 in both directions~~ — **closed by phase 4b, 2026-09-30** | Wrong | ~~Issue~~ Done | S | ~~yes~~ decided |
| S-09 | ~~Inactive subsidies still grant credit~~ — **closed by phase 1, 2026-09-16** | Bug | ~~Issue~~ Done | S | `s.Active = 1` in both credit views |
| S-10 | ~~Soft-deleted orders keep consuming credit~~ — **closed by phase 1, 2026-09-16** | Bug | ~~Issue~~ Done | S | `o.Deleted = 0`, views + history SP |
| S-11 | ~~Topup period window: C# and SQL disagree on the last day; duplicate rows double-count~~ — **closed by phase 1, 2026-09-16**; the variation-aware end **closed by phase 3, 2026-09-29** | Bug | ~~Issue~~ Done | S–M | Duplicates made harmless by `SUM`, not impossible. Variation-aware end → **phase 3** |
| S-12 | ~~Admin/customer subsidy details read only in-period rows; out-of-period assignments vanish; NRE~~ — **closed by phase 3, 2026-09-29** | Wrong | ~~Issue~~ Done | S–M | |
| S-13 | ~~Feb-29 dates crash the smart-TTL calculator~~ — **closed by phase 1, 2026-09-16** | Bug | ~~Issue~~ Done | S | `SubsidyPeriodHelper.Anniversary` + `EOMONTH` |
| S-14 | ~~Admin topups accept zero / negative amounts; product-group inline edit accepts negatives~~ — **closed by phase 2b**; confirmed by phase 3, 2026-09-29 | Bug | ~~Issue~~ Done | S | yes |
| S-15 | ~~Shipping-subsidy default inverted vs 4.6 (`IncludeShippingOnSubsidy` off by default)~~ — **closed by decision 4; documented by phase 6, 2026-10-02** | Changed | Issue | S | yes |
| S-16 | ~~Store deletion not handled (4.6 handled company deletion)~~ — **closed by phase 3, 2026-09-29** | Missing | ~~Nice~~ Done | S | |
| S-17 | ~~Shipping credit is not netted by restorations~~ — **closed 2026-09-10** by decision 21: not restored, by design | Closed | — | — | — |
| S-18 | ~~Variation values shown regardless of store settings in subsidy→packnames grid~~ — **closed by phase 3, 2026-09-29** | Changed | ~~Nice~~ Done | S | |
| S-19 | ~~Variation validator does not check end > start~~ — **closed by phase 3, 2026-09-29** | Gap | ~~Nice~~ Done | S | |
| S-20 | ~~Money columns declared `AsDecimal()` without precision; `SubsidyRestoreItem.CreatedOnUtc` is a different type~~ — **closed by phase 1, 2026-09-16** | Gap | ~~Nice~~ Done | S | Migration `SubsidyPeriodIntegrity`; was `DECIMAL(19,5)`, not `(18,0)` |
| S-22 | ~~Topup window sends `DateTime.MinValue` to a `datetimeoffset` column~~ — **closed by phase 1, 2026-09-16** | Bug | ~~Blocker~~ Done | S | Bounds are `DateTime?`; predicate composed |
| S-23 | ~~Subsidy History shows the topup's running total on every row instead of that row's own amount~~ — **fixed 2026-09-15**, `stop.Credit` → `stoph.Credit` | Bug | ~~Issue~~ Done | S | Verified in the grid: 25 / 25 |
| S-24 | ~~Subsidy History *Created On* is late by the web server's UTC offset — a `datetimeoffset` read as local, then converted again~~ — **closed by phase 3, 2026-09-29** | Bug | ~~Issue~~ Done | S | |
| C-01 | ~~Two definitions of "the cart" disagree (header, selector, flyout, checkout, placement)~~ — **closed by phase 4a Part 1, 2026-09-30** | Wrong | Issue | M | yes |
| C-02 | ~~`AddToCartAsync(packNameId)` can merge into the *context* packname's line (re-order, migration)~~ — **closed by phase 4a Part 1, 2026-09-30** | Bug | Issue | S–M | |
| C-03 | ~~Subsidy gender filter in the catalogue not ported (`GenderFiltering` dead)~~ — **closed by phase 5 Part 2, 2026-10-01: setting deleted** | Missing | Issue | S–M | yes |
| C-04 | ~~Catalogue restriction is list-only: item page, related, cross-sell, categories, wishlist ignore it~~ — **closed by phase 5 Part 2, 2026-10-01** | Missing | Issue | M | yes (K9) |
| C-05 | ~~Order discount, gift cards, reward points applied **before** subsidy (4.6: after)~~ — **closed by phase 6 Part 1, 2026-10-02** | Changed | Issue | M | yes |
| C-06 | ~~Order stores gross subtotal/tax with a net total — reports and Odoo will not reconcile~~ — **closed by phase 6 Part 1, 2026-10-02** | Changed | Issue | M–L | yes |
| C-07 | ~~Post-placement readers derive "gross" differently from checkout~~ — **closed by phase 6 Part 1, 2026-10-02** | Gap | Issue | M | |
| C-08 | ~~Pending-payment (redirect gateway) orders consume credit until cancelled; no transaction around placement~~ — **closed by decision 7; documented by phase 6, 2026-10-02** | Gap | Issue | M | yes |
| C-09 | ~~Credit notes consume before subsidy~~ — **closed 2026-09-10**: reversed by decision 15 (subsidy first, phase 4b); budget walked in cart order across packnames remains as designed | Closed | — | — | — |
| C-10 | ~~Required-product auto-add and `MaximumShoppingCartItems` semantics across packnames~~ — **closed by phase 4a Part 1, 2026-09-30** | Gap | Nice | S | |
| A-01 | ~~Product / packname / product-group pickers not store-scoped; POSTs accept forged ids~~ **Closed 2026-09-21 (phase 2b part 1).** Each POST re-runs its own picker query and accepts only what it returns; the product picker is store-scoped and published-only. | Bug | ~~Issue~~ Done | S–M | (K7) |
| A-02 | ~~"Used %" column always "0%"~~ — **closed by phase 3, 2026-09-29** | Bug | ~~Improvement~~ Done | S | |
| A-03 | ~~Customer-edit subsidies panel requires MultiStore permission, blank otherwise~~ — **closed by phase 3, 2026-09-29** | Wrong | ~~Nice~~ Done | S | yes |
| A-04 | ~~Markup / JS defects in the subsidy admin views~~ — **closed by phase 3, 2026-09-29** | Bug | ~~Nice~~ Done | S | |
| A-05 | ~~Minor controller deviations (log text, null checks, dead history model)~~ — **closed by phase 3, 2026-09-29** | Gap | ~~Nice~~ Done | S | |
| A-06 | ~~The packname subsidy panel shows an allocation, a topup and a percentage, but never the amount **used** or **remaining**~~ — **closed by phase 3, 2026-09-29** | Missing | ~~Issue~~ Done | S | raised 2026-09-22 |
| A-07 | ~~Product-group detail: the chosen **colour is not applied to the icon preview**, so the edit screen never shows what the three grids will render~~ — **closed by phase 3, 2026-09-29** | Missing | ~~Nice~~ Done | S | raised 2026-09-29 |
| M-01 | ~~Customer "My Subsidies" page does not exist — dead link (root cause of K2)~~ — **closed by phase 7, 2026-10-08** | Missing | Issue | M | yes (K3–K5) |
| M-02 | ~~Manager cannot assign subsidies when adding/editing a packname (`AllowSubsidySelection` dead)~~ — **closed by phase 7, 2026-10-08** | Missing | Issue | M | yes |
| M-03 | ~~Subsidy/credit-note lines missing from emails, invoice PDF, admin gross/payable (K6)~~ — **closed by phase 6 Part 1, 2026-10-02** | Missing | Issue | M | |
| M-04 | ~~"Landing page after login = Subsidies" not ported~~ — **closed by phase 7, 2026-10-08** (`SubsidySendUserAfterLogin`) | Missing | Nice | S | yes |
| M-05 | ~~Subsidy catalogue keeps nop's strict specification-attribute filtering (4.6 kept unmapped products)~~ — **closed as recorded by phase 7, 2026-10-08** | Changed | Nice | S | yes |
| M-06 | ~~A quantity-only subsidy displays no quantity anywhere~~ — **admin half closed by phase 3, 2026-09-29; storefront half closed by phase 7, 2026-10-08** | Missing | Issue | S | |
| P-01 | ~~nop standard checkout reachable for packname customers~~ — **closed by phase 2a, 2026-09-17** | Bug | ~~**Blocker**~~ Done | S | |
| P-02 | ~~Placement clears the whole cart~~ — **fixed 2026-09-11**, fix 22 | Wrong | ~~Blocker~~ Done | S | Shipped with exchanges phase 4b-4 |
| P-03 | ~~Department ACL never enforced (no customer carries the `DEP_` role)~~ — **closed by phase 5 Part 1, 2026-10-01** | Missing | ~~**Blocker**~~ Done | M | ~~yes~~ decided |
| P-04 | ~~`PackNameListManagerMapping` cache consumer throws; manager scope stale~~ — **closed by phase 2a, 2026-09-17** | Bug | ~~**Blocker**~~ Done | S | |
| P-05 | ~~Cached manager list returned by reference and mutated (duplicates grow every render)~~ — **closed by phase 2a, 2026-09-17** | Bug | ~~Issue~~ Done | S | |
| P-06 | ~~Manager scope SP is broader than 4.6 (own-branch private lists, store-wide `AllManagers`, deleted branches)~~ — **closed**: the `b.Deleted = 0` filter on path 2 by phase 2b (2026-09-23), the rule itself kept as intended (decision 12) and written down by phase 10 (2026-10-21, `MultiStore_Management_Reference.md` §7b.2) | Changed | Issue | S | yes |
| P-07 | ~~Ownership checks ignore `PackName.Manager`; `SelectPackName` NRE for non-packname customers~~ — **closed by phase 2a, 2026-09-17** | Bug | ~~Issue~~ Done | S | |
| P-08 | ~~Current packname not revalidated (deleted / inactive / moved stays selected); cart lines not removed on delete~~ — **closed by phase 4a Part 1, 2026-09-30** | Wrong | Issue | M | |
| P-09 | ~~Session "ignore packname context" switch not ported~~ — **closed as not needed by phase 7, 2026-10-08** | Missing | Issue | S | yes |
| P-10 | ~~Print name no longer pre-filled into the product attribute (`PrintNameProductAttributeId` dead)~~ — **closed by phase 7, 2026-10-08** | Missing | Issue | S–M | |
| P-11 | ~~Cost-centre addresses never offered at checkout; `MultiStoreSettings.ShippingMethod` dead~~ — **cost-centre half closed by decision 12 (not needed); `ShippingMethod` implemented as N-01 by phase 8 Part 1, 2026-10-11** | Gap | ~~Issue~~ Done | M | yes |
| P-12 | ~~"Branch default address only / cannot select" policy lost (`UseBranchAddress`/`AllowSelectShipAddresses`)~~ — **closed by decision 12 (not needed); `CLAUDE.md` §15.3 corrected by phase 8 Part 1, 2026-10-11** | Changed | ~~Issue~~ Done | S | yes |
| P-13 | ~~`DefaultPackNameCustomerRoleIds` never applied~~ — **visibility half by phase 5 Part 1; registration half closed by phase 7, 2026-10-08** | Gap | Issue | S | |
| P-14 | ~~Manager-created customer: password = e-mail, no e-mail sent, `ForceToChangePassword` never written~~ — **closed by phase 7, 2026-10-08** | Gap | Issue | S–M | |
| P-15 | ~~`UseStaffAsPackName` / `UseBranchesAsPackNameList` promise automation that does not exist~~ — **closed by phase 8 Part 1, 2026-10-11** | Changed | ~~Improvement~~ Done | S | yes |
| P-16 | ~~`AllManagers` / `ParentManagers` exclusivity dropped~~ — **closed by phase 2b, 2026-09-23** (`SavePackNameList` forces `ParentManagers = false` when `AllManagers`; verified in code 2026-10-21) | Wrong | Improvement | S | |
| P-17 | ~~Multi-store scoping holes in manager/department queries and admin POSTs~~ — **closed by phase 2b, 2026-09-23** (store-filtered manager queries, the mapping POSTs check store / `Manager` / `Deleted`, the background context getter deleted) | Gap | Improvement | S | |
| P-18 | ~~Landing-page redirect after login / packname selection dropped~~ — **closed by phase 7, 2026-10-08** | Missing | Nice | S | yes |
| P-19 | ~~**PayPal Commerce places a nop order directly, bypassing the packname checkout** — no `PackNameOrderItem`, no destinations, no subsidy~~ — **closed by phase 2a, 2026-09-17** | Bug | ~~**Blocker**~~ Done | S | |
| N-03 | **The packname checkout supports no payment method that places its own order**, and selecting one places the order UNPAID — **applied by phase 4c, 2026-10-01; PayPal tests pending** | Missing / Bug | **Blocker** (until the PayPal tests pass) | M | ~~yes~~ decided |
| N-02 | ~~No packname order-completed page; the flow lands on nop's generic `checkout/completed`~~ — **closed by phase 7, 2026-10-08** | Missing | Improvement | S–M | yes |
| G-01 | ~~Coupon and gift-card entry has no home for packname customers~~ — **closed by decision 14, recorded by phase 7, 2026-10-08** (out of scope) | Missing | Issue | S–M | yes |
| G-02 | ~~`AddAddress` POST: null dereference, no ownership check, no address validation~~ **Closed 2026-09-21 (phase 2b part 1).** Guest returns a challenge, a forged or missing packname id returns `{success:false}`, ownership goes through `IsPackNameOwnedByManagerId`, and the store gate is read from the **packname's** store. | Bug | ~~Issue~~ Done | S | |
| G-03 | ~~Public `PackNameController` POSTs have no anti-forgery validation (CSRF)~~ — **closed by phase 2a, 2026-09-17** | Bug | ~~Issue~~ Done | S | |
| G-04 | ~~Free-shipping threshold and shipping discounts evaluated on the whole multi-packname cart~~ — **closed by phase 4a Part 2, 2026-09-30** | Bug | Issue | S | |
| G-05 | ~~Shipping "cannot be calculated" reported as $0~~ — **closed by phase 4a Part 2, 2026-09-30** | Bug | Issue | S | |
| G-06 | ~~Silent failures in checkout AJAX endpoints; unlogged `ConfirmOrder` exceptions~~ — **closed by phase 4a Part 1, 2026-09-30** | Gap | Issue | S | |
| G-07 | ~~Checkout recomputation is quadratic (every change re-rates every packname, reloads every panel)~~ — **closed by phase 9, 2026-10-20** | Improvement | Improvement | M | |
| G-08 | ~~Re-order and cart merge ignore whether the customer still manages the packname~~ — **closed by phase 4a Part 1, 2026-09-30** | Gap | Nice | S | |
| G-09 | ~~Flyout **remove** silently does nothing for a cannot-order packname — returns `success = true`~~ — **closed by phase 4a Part 1, 2026-09-30** | Bug | Issue | S | |
| G-10 | ~~Only *add* to cart refreshes the subsidy bar and product-group headers; remove and quantity change leave them stale~~ — **closed by phase 4a Part 2, 2026-09-30** | Bug | Issue | S | |
| O-01 | ~~Admin cannot edit lines on a packname order; nop's edit/add actions only hidden, not guarded~~ **Block half closed 2026-09-21 (phase 2b part 1, decision 14).** `PackNameOrderAdminGuardFilter` blocks seven methods and leaves cancel/delete open — `PackName_Order_Shipments.md` §9.1. The packname-aware **rebuild** stays open, deferred to after phase 6. | Missing | Issue (rebuild only) | L | yes |
| O-02 | ~~Per-line packname absent from invoice PDF and order e-mails~~ — **closed by phase 6 Part 2, 2026-10-07** (invoice and packing slip; e-mail left out by decision D) | Gap | Improvement | M | |
| O-03 | ~~Order-details image width bound to `DefaultImageQuality`; factory injected into the view~~ — **closed by phase 4a Part 2, 2026-09-30** | Bug | Nice | S | |
| O-04 | ~~E / R badges on the admin order lists (K8)~~ — **closed by phase 6 Part 2, 2026-10-07** | Missing | Improvement | S | yes |
| U-01 | ~~Public `EditPackName` never checks ownership; can move a packname to any branch of any store; GET → 500~~ — **closed by phase 2a, 2026-09-17** | Bug | ~~Issue~~ Done | S | |
| U-02 | ~~Public `EditPackName` POST dereferences a null packname~~ — **closed by phase 2a, 2026-09-17** | Bug | ~~Issue~~ Done | S | |
| U-03 | ~~`AllowManagersToAddPackNames` enforced only by hiding the button~~ — **closed by phase 2a, 2026-09-17** | Gap | ~~Issue~~ Done | S | |
| U-04 | ~~One-customer-per-packname / same-store rules enforced only in JavaScript~~ **Closed 2026-09-21 (phase 2b part 1).** Server-side in all three save paths, with the message rendered — the span was missing in both detail views, so the check worked and said nothing (correction E2). | Gap | ~~Issue~~ Done | S–M | |
| U-05 | ~~Manager-assignment POSTs accept any packname id~~ **Closed 2026-09-21 (phase 2b part 1).** Both mapping POSTs check the posted packname for store, for `Manager` and for `Deleted` — SP path 4 asks none of the three, so this is where that boundary lives. | Gap | ~~Issue~~ Done | S | |
| U-06 | ~~Packname info strip lost the 4.6 warnings (no address, no shipping method, cannot order)~~ — **closed by phase 4a Part 2, 2026-09-30** | Missing | Improvement | S | |
| U-07 | ~~Branch filter on the PackNames tab is rendered but ignored~~ — **closed by phase 2c, 2026-09-28.** The branch was already joined and projected; it needed one optional parameter, one `where` clause and one caller change | Bug | ~~Improvement~~ Done | S | |
| U-08 | ~~Admin save actions trust the posted `StoreId`; no list/branch/department/cost-centre ↔ store check~~ **Closed 2026-09-21 (phase 2b part 1).** The store comes from the entity in all seven save paths, and `StoreId`/`BranchId`/`PackNameListId` are `Ignore()`d in the mapper so a save cannot relocate an entity. | Gap | ~~Improvement~~ Done | S | |
| U-09 | ~~Admin address popups edit any `Address` id~~ **Closed 2026-09-21 (phase 2b part 1).** All three popups resolve the address through its owner. Verified live: another customer's address is byte-identical after all three attempts. | Bug | ~~Improvement~~ Done | S | |
| U-10 | ~~Default-settings POST reports success on validation failure~~ — **closed by phase 2b part 3B, 2026-09-23** | Bug | Improvement | S | |
| U-11 | ~~Customer delete leaves the packname pointing at a deleted customer~~ **Closed 2026-09-21 (phase 2b part 1).** An `EntityDeletedEvent<Customer>` consumer clears `CustomerId` and `Manager`; the packname row and the `PackNameCustomerId` order-item snapshot survive, and the grid no longer shows the deleted person's e-mail. | Gap | ~~Improvement~~ Done | S | |
| U-12 | ~~Manager add/edit and mapping changes are not activity-logged~~ — **mostly closed by phase 2b part 3B, 2026-09-23** (mapping add / remove and the storefront add log); the storefront `EditPackName` POST and the three admin address popups still do not log — `Subsidy_PackName_Open_Items.md` | Gap | Improvement | S | |
| U-13 | ~~No bulk actions / export on packname grids; selector ships every packname unpaged~~ — **closed by phase 11, 2026-10-21** (both halves; §17b "From phase 11") | Improvement | ~~Improvement~~ Done | M | |
| U-14 | ~~Department ACL picker lists every store's departments, unlabelled (K7 extension)~~ — **closed by phase 2b (scoping, 2026-09-23) and phase 5 Part 1 (the role takes effect, 2026-10-01)** | Gap | Issue | M | |
| U-15 | ~~Feature toggle missing on four admin packname-address / create-popup actions; `GetListsByBranchId` no permission~~ — **closed by phase 2b, 2026-09-23** (every action gated and permissioned; verified in code 2026-10-21); the `Feature_Toggles_Reference.md` claim corrected by phase 10 | Gap | Nice | S | |
| U-16 | ~~Public `AddPackName` GET throws 500 for non-managers / unknown lists; `GetPackNameCartCount` unguarded~~ — **closed by phase 2a, 2026-09-17** | Gap | ~~Nice~~ Done | S | |
| U-17 | ~~Malformed markup and hard-coded English strings in admin packname views~~ — **closed by phase 2c, 2026-09-24** | Bug | ~~Nice~~ Done | S | |
| U-18 | ~~Customer-edit PackName card cannot deep-link to the packname; branches tab inline grid has no search~~ — **closed by phase 2c, 2026-09-24** | Improvement | ~~Nice~~ Done | S | |
| U-19 | ~~`packname-info-spec.md` implemented but still says "ready for implementation"; icon names differ~~ — **closed by phase 10, 2026-10-21** (the spec is consumed into `subsidy-system.md` §8.7) | Gap | Nice | S | |
| X-01 | ~~Excel import of packnames (+ subsidies) not ported~~ — **ported by phase 8 Part 2, 2026-10-19** (the manager's import page behind the selector's Excel icon; no admin-side import) | Missing | ~~Issue*~~ Done | L | yes |
| X-02 | ~~Store self-registration screen: slug saved, no page behind it~~ — **deleted by phase 8 Part 1, 2026-10-11** (decision 13) | Missing | ~~Issue~~ Done | L | yes |
| X-03 | ~~Import welcome e-mail template and `%Subsidy.SubsidyName%` token never installed~~ — **closed by phase 7, 2026-10-08** | Missing | Issue | M | |
| X-04 | ~~Admin-side Excel import of packnames (4.6 *Company › Employees › Import from Excel*) not in 4.9 — only managers import, from the storefront.~~ — **closed by phase 12, 2026-10-20** | Missing | ~~Issue~~ Done | M | yes |
| T-01 | ~~Nine `MultiStoreSettings` are editable but never read (consolidated table)~~ — **closed by phase 8 Part 1, 2026-10-11; the X-01 rows (`AllowToAddByExcel`, `AllowSubsidyCountByExcel`) by phase 8 Part 2, 2026-10-19** | Gap | ~~Issue~~ Done | M | yes |
| H-01 | ~~Credit cache became per-request; nine invalidations and one consumer are dead code; heavy views run per request~~ — **closed by phase 9, 2026-10-20** | Changed | Improvement | S / M | yes |
| H-02 | ~~Smart TTL runs two uncached queries before every cached read; hard-coded `3`~~ — **closed by phase 9, 2026-10-20** | Improvement | Improvement | S | |
| H-03 | ~~Cache-invalidation gaps (subsidy: product group / variation / product; packname: 7 cases; orders: address groups)~~ — **closed by phase 2b's cache matrix (2026-09-22) and phase 4b's `SettingCacheEventConsumer` (2026-09-30)**; one residue — `EventConsumer` republishes an `EntityUpdatedEvent<PackNameCartItem>` nobody handles — `Subsidy_PackName_Open_Items.md` | Gap | Issue | S | |
| H-04 | ~~N+1 and unbounded `Task.WhenAll` over scoped services in credit loads, allocation, checkout and order factories~~ — **closed by phase 9, 2026-10-20** | Improvement | Improvement | M | |
| H-05 | ~~`IsPackNameOwnedByManagerId` cache-key explosion; two round trips per call~~ — **closed by phase 2a, 2026-09-17** | Improvement | ~~Improvement~~ Done | S | |
| Q-01 | Dead code, dead stubs, dead CSS, TODOs, stale docs (consolidated list). **Admin-view items closed by phase 2c, 2026-09-28; subsidy items (dead `HistoryType` values and resources, the unused cache prefix, `Async` suffixes, the dead variation `StoreId`) closed by phase 3, 2026-09-29**; the rest of the list is unchanged | Improvement | Nice | S | |
| K1 | ~~"Go To Cart" in the flyout for packname customers (extended)~~ — **closed by phase 4a Part 2, 2026-09-30** | Bug | Nice | S | |
| K2 | ~~My-account subsidy view does not load → root cause is M-01~~ — **closed with M-01 by phase 7, 2026-10-08** | — | see M-01 | | |
| K3 | ~~Same view is hard to read → folded into M-01~~ — **closed with M-01 by phase 7, 2026-10-08** | — | see M-01 | | yes |
| K4 | ~~Order id not clickable in subsidy history → folded into M-01~~ — **closed with M-01 by phase 7, 2026-10-08** (a row is a link only for the viewer's own orders — decision 2026-10-08) | — | see M-01 | | |
| K5 | ~~No per-line subsidy detail on the customer's order page → folded into M-01 / C-07~~ — **closed by phase 6 Part 1, 2026-10-02** | — | see M-01 | | yes |
| K6 | ~~Subsidy and credit notes missing from emails and PDFs → M-03~~ — **closed by phase 6 Part 1, 2026-10-02** | — | see M-03 | | |
| K7 | ~~Product group and department pickers show every store's products → A-01, U-14~~ — **closed with A-01 (phase 2b) and U-14 (phases 2b and 5 Part 1)** | — | see A-01 | | |
| K8 | ~~E / R badges on admin order lists → O-04~~ — **closed with it, 2026-10-07** | — | see O-04 | | yes |
| K9 | ~~Subsidy allocation is a list filter, never a gate~~ → C-04 — **closed with it, 2026-10-01** | — | see C-04 | | yes |
| K10 | ~~`AddToCartAsync` reports success without adding when the packname is unusable~~ — **closed by phase 4a Part 1, 2026-09-30** | Bug | Issue | S | yes |

\* X-01 becomes a Blocker if bulk onboarding is required at go-live.

---

## 4. Subsidy — rules, data and calculation (S)

### ~~S-01. Ordering-period SQL: AND/OR precedence opens the period from 1 January~~ — **closed by phase 1, 2026-09-16**
- **Kind:** Bug (inherited verbatim from 4.6).
- **4.6:** `Nop.Plugin.Jim2.Allowances/Services/Database/AllowancesDatabaseService.cs:334-340` (`allowance_is_in_ordering_period`).
- **4.9:** `Services/DataBase/CWDatabaseService.cs:459-480` (`subsidy_is_in_ordering_period`). The cross-year branch reads
  `YEAR(@DurationStart) < YEAR(@DurationEnd) AND ( afterStart ) OR ( beforeEnd )`. T-SQL binds `AND` tighter than `OR`,
  so it parses as `(cross-year AND afterStart) OR beforeEnd`.
- **What happens:** for a **same-year annual** subsidy the first operand is false and the whole annual test collapses to
  "today is before the end month/day". Example: subsidy 1 Jul – 30 Sep 2025, annual; today 15 Feb 2026. Outer guard
  `today >= 2025-07-01` passes; same-year branch false (Feb < Jul); cross-year branch `(false AND …) OR (2 < 9)` = **true**.
  The subsidy is "in period" from 1 January to 30 September every year after the first, instead of July–September.
  `CWPlans/subsidy-system.md §3` states the intended rule.
- **Should be:** parenthesise the cross-year branch as `YEAR(s) < YEAR(e) AND (afterStart OR beforeEnd)`. Better: compute
  the current occurrence's `[start, end]` once (as `subsidy_get_topup_period_end` already does) and share it with S-02.
- **Also note:** the C# smart-TTL helper (`SubsidyOrderingService.cs:55-90`) models the *correct* boundaries, so until
  this is fixed the cache can disagree with the SQL at the period edges (H-02).
- **Size:** Small, plus a regression script (same-year, cross-year, first year, null dates).

### ~~S-02. Used-credit SQL: annual recurrence never resets consumption~~ — **closed by phase 1, 2026-09-16**
- **Kind:** Bug (inherited).
- **4.6:** `AllowancesDatabaseService.cs:370-376`. **4.9:** `CWDatabaseService.cs:525-531` (`subsidy_order_is_in_current_period`):
  `YEAR(s) < YEAR(e) AND @OrderDate >= thisYearStart OR @OrderDate <= thisYearEnd`.
- **What happens:** for a same-year annual subsidy the test degenerates to `@OrderDate <= this year's DurationEnd`, i.e.
  **all orders ever placed** count as current consumption. Example: Jul–Sep annual, credit 500; a packname spent 450 in
  Sep 2025. In Aug 2026 `UsedCredit` still includes the 450 → only 50 available in 2026. The cross-year branch is wrong on
  its own too: in Jan 2027 for a Nov–Feb subsidy it tests `orderDate >= 2027-11-01` (a future date) instead of 2026-11-01.
  Called from the `used`, `usedShipping` and product-group `used` sub-queries (`CWDatabaseService.cs:620-624, 643-647, 702-705`),
  so global credit, shipping credit, product-group credit and quantities are all affected. The restoration rows
  (`CORE_SUB_SubsidyRestoreItem`) go through the same function and inherit the fix automatically.
- **Should be:** `@OrderDate BETWEEN currentOccurrenceStart AND currentOccurrenceEnd`, computed from `@TODAY`.
- **Not verified:** whether the 4.1 production system has the same SQL. If it does, the business has been living with this
  and the priority is "long-standing defect" rather than "regression" — worth one query against the 4.1 database.
- **Size:** Small.

### ~~S-03. "Cannot order" is computed in SQL but enforced nowhere — **Blocker**~~ — **closed by phase 4a Part 1, 2026-09-30**
- **Kind:** Missing. (Reported independently by four readers: subsidy core, cart, checkout, selector.)
- **4.6:** `AllowancesPackNameService.cs:69-73` overrides `PackNameCanOrderAsync` to read the view; `AllowancesShoppingCartService.cs:170-174`
  refuses add-to-cart (`QuantityOverRestrictedAllowance`); `AllowancesAclService.cs:106-131` returns an **empty catalogue**
  when `!CanOrder`; `Jim2.BE/Factories/PackNameModelFactory.cs:157,179-180` shows the "cannot order" notice under the
  packname info; `AllowancesPackNameModelFactory.cs:55` marks blocked packnames in the selector.
- **4.9:** the view `vw_CORE_SUB_SubsidyPackNameCanOrder` (`CWDatabaseService.cs:747-793`) and
  `SubsidyOrderingService.PackNameCanOrderAsync / GetSubsidyPackNameCanOrderAsync / GetOrderingBlockedPackNamesAsync`
  (`:132-176`) exist and are correct — and **nothing calls them**. `PackNameService.PackNameCanOrderAsync` (`:741-745`)
  returns `true`; `PackNameCanNotOrderMessageAsync` returns `""`; `PackNameModelFactory.GetBlockOrderingPackNameIdsAsync`
  (`Factories/PackNameModelFactory.cs:77-80`) returns an empty list; none of the three has a caller or an override.
  `GetOnlyAllocatedProductIdsAsync` (`SubsidyOrderingService.cs:219-238`) returns `null` (= unrestricted) when no subsidy
  is in ordering, so `ProductOverrideService` shows the **whole catalogue**. Add-to-cart (`ShoppingCartOverrideService.cs:497-507`)
  only checks the quantity cap, which is `null` when the credit view has no rows → the line is inserted. The only
  enforcement left is the `INNER JOIN … WHERE pno.CanOrder = 1` in `vw_CORE_PN_PackNameShoppingCartItem`
  (`CWDatabaseService.cs:832,838`), which makes the line **disappear from the packname checkout** while nop's own cart still
  counts it (C-01) and placement then deletes it (P-02). Resources `Jim2.CurrentPackNameInfo.CannotOrder` /
  `PackName.BlockOrderingReason` have no 4.9 equivalent.
- **What happens:** with the campaign closed (`OrderOutsidePeriod = false`), a 4.9 packname sees every product, adds to cart
  with a success message, and finds an empty checkout with no explanation. This is the K10 "silently lost sale" shape for
  the most common cause — period end.
- **Should be:** re-instate the gate at the three 4.6 points: (a) `ProductOverrideService` treats `CanOrder = false` as
  "restricted to nothing" (it already returns an empty page for an empty allocation list, `:151-153`); (b)
  `ShoppingCartOverrideService.AddToCartAsync(… packNameId …)` returns a readable warning when
  `ISubsidyOrderingService.PackNameCanOrderAsync` is false; (c) `PackNameInfoViewComponent` shows the notice and
  `PackNameModelFactory.PreparePackNamesModelForManagerAsync` calls `GetOrderingBlockedPackNamesAsync` so the selector
  disables blocked packnames (the view already renders the disabled state, `PackNameSelector/Default.cshtml:78,102-105`).
  Delete the dead `PackNameService` stubs. Decide whether lines already in the cart are hidden (today) or shown and blocked
  (C-01).
- **Decision:** the wording of the customer-facing notice.
- **Size:** Medium.

### ~~S-04. Admin variation popup cannot render — **Blocker**~~ — **closed by phase 2b** (the popup binds `StoreId`, the POST derives the store from the assignment); confirmed on re-derivation by phase 3, 2026-09-29
- **Kind:** Bug.
- **4.9:** `Areas/Admin/Views/Subsidy/SubsidyVariationEditPopup.cshtml:27` — `<input asp-for="CompanyId" type="hidden" />`;
  `Areas/Admin/Models/Subsidies/SubsidyVariationModel.cs` has `StoreId`, not `CompanyId` (grep across the plugin: this line
  is the only occurrence). The view is runtime-compiled, so the Razor error surfaces the first time an admin clicks
  *Change* next to a packname's credit / end date (`_PackNameSubsidies.cshtml:35,74`) or *Edit* in
  `_Subsidies.SubsidyDetail.PackNames.cshtml:167`. No variation can be created or edited from admin (lesson #9 shape).
- **Second defect in the same view:** `StoreId` is not posted, yet `SubsidyController.cs:489` does
  `LoadSettingAsync<MultiStoreSettings>(model.StoreId)` on POST — once the `CompanyId` line is removed the POST evaluates
  the store-0 defaults and either returns `AccessDeniedView()` (`:491-492`) or nulls out credit/dates (`:508-515, 525-527`).
- **Should be:** drop the `CompanyId` input and do not trust a posted store either — derive the store from
  `GetSubsidyPackNameDetailsByIdAsync(model.SubsidyPackNameId).StoreId` as the GET already does (`SubsidyModelFactory.cs:709-711`).
- **Size:** Small.

### S-05. ~~Storefront change-topup has no ownership or store check — **Blocker**~~ — **closed by phase 2a, 2026-09-17**
- **Kind:** Bug (4.6 was equally open, `AllowancePackNameController.cs:180-194, 738-748`).
- **4.9:** `Controllers/SubsidyCatalogController.cs:72-97` `ValidateTopupAccessAsync` checks only "current customer is a
  manager" and the current store's settings. `ChangeTopupSubsidy` POST (`:231-263`) then loads
  `GetSubsidyPackNameByIdAsync(model.SubsidyPackNameId)` from the **form** and saves — never compared with the context
  packname, with `IMultiStoreService.IsPackNameOwnedByManagerId` (used correctly in `PackNameController.cs:702`), or with
  `subsidy.StoreId`. Same in `ChangeTopupProductGroup` (`:281-312`) and the two GETs (`:217-227, 267-277`). A deleted
  subsidy makes `GetSubsidyByIdAsync` return null → `SaveSubsidyTopupForPeriodAsync(null)` throws in
  `GetTopupSearchWindow` → 500.
- **What happens:** any manager can add credit to any packname of any store by posting its `SubsidyPackNameId`, and the
  activity log records it under their name as a normal topup.
- **Should be:** load the assignment, require `subsidyPackName.PackNameId == contextPackName.Id` **and**
  `IsPackNameOwnedByManagerId(manager.Id, packName.Id)` **and** `subsidy.StoreId == currentStore.Id`; null-check the subsidy.
  (The spec that described the helper, `change-topup-spec.md`, is consumed; the guard is `subsidy-system.md` §8.1.)
- **Size:** Small.

### ~~S-06. `OnlyAllocatedProducts` ignores the manager exemption — Issue~~ — **closed by phase 4b, 2026-09-30**
> **Closed:** `GetOnlyAllocatedProductIdsAsync` applies `OnlyAllocatedProducts && (IncludeManagers || !packName.Manager)`, as the views do; the catalogue and bulk order both read it (T7). The in-ordering-only divergence from 4.6 stays as documented in `subsidy-system.md` §5.1.
- **Kind:** Wrong.
- **4.6:** the catalogue read the **view** flag, computed as `CASE WHEN OnlyAllocatedProducts = 1 AND (IncludeManagers = 1 OR pn.Manager = 0)`
  (`AllowancesDatabaseService.cs:400-401, 601`; consumer `AllowancesAclService.cs:112-119`).
- **4.9:** `SubsidyOrderingService.GetOnlyAllocatedProductIdsAsync:228-234` loads the entities and tests
  `subsidies.Any(s => !s.OnlyAllocatedProducts)`; `IncludeManagers` and `PackName.Manager` are never read in C# (grep:
  only the SQL views). `CWPlans/subsidy-system.md §5.1/§5.5` document the exemption that is not implemented. Consumers:
  `ProductOverrideService.cs:103`, `BulkOrderService.cs:100` (bulk ordering inherits the defect).
- **What happens:** a manager on a subsidy with `OnlyAllocatedProducts = 1, IncludeManagers = 0` is restricted to the
  allocated products; 4.6 showed them the full catalogue.
- **Should be:** read `SubsidyPackNameCanOrder.OnlyAllocatedProducts` (already manager-aware and cached by
  `GetSubsidyPackNameCanOrderAsync`) or apply `s.OnlyAllocatedProducts && (s.IncludeManagers || !packName.Manager)`.
  Record the second, deliberate divergence in the same method: 4.6 evaluated "all subsidies restrict" over **all active
  assignments regardless of period**; 4.9 over in-ordering ones only (documented as intended in §5.1 "Edge case").
- **Size:** Small.

### ~~S-07. `RestrictToAllocatedQty` is checked per cart line, not per product group — Issue~~ — **closed by phase 4b, 2026-09-30**
> **Closed:** what the cart takes is the subsidy allocation of the packname's whole cart, run with no money budget (`GetCartSubsidyBreakdownAsync` could not be used: it budgets from the cart total, empty while shipping cannot be rated). Add and quantity change both use it; a quantity change refuses only a raise (T1–T3, T9, T2b).
- **Kind:** Wrong.
- **4.6:** `AllowancesShoppingCartService.cs:210-226` — `inShoppingCartQty` is what the packname's **whole cart** already
  consumes from the matching product groups (`GetPackNameUsedAllowancesAsync`, `:687-722`); `:315-319` compares
  `inShoppingCart + quantity > allowedQuantity`; the update path does the same (`:724-749`, `:459-462`).
- **4.9:** `ShoppingCartOverrideService.cs:500-502` `var currentCartQty = shoppingCartItem?.Quantity ?? 0;` — only the
  same product + same attributes line; `UpdateShoppingCartItemAsync` (`:359-361`) compares only that line.
  `GetAllocatedQuantityRestrictionAsync` (`SubsidyCreditService.cs:149-173`) ignores the cart entirely.
- **Example:** group *Shirts* = {A, B}, quantity 2, nothing ordered. Add 2×A → OK. Add 2×B → 4.6 refuses (2 + 2 > 2);
  4.9 accepts (0 + 2 ≤ 2). The cart holds 4 units against an allocation of 2. Two attribute variants of the same product
  reproduce it too.
- **Should be:** the in-cart figure must be what the allocation would consume from the groups containing the product over
  the packname's whole cart — project it from the same `GetCartSubsidyBreakdownAsync` the subsidy bar uses
  (`SubsidyCatalogCartService.cs:56-93`), or at minimum sum every cart line whose product is in a matching group.
- **Size:** Small–Medium (with S-08).

### ~~S-08. `RestrictToAllocatedQty` exemptions differ from 4.6 in both directions — Issue~~ — **closed by phase 4b, 2026-09-30**
> **Closed (decision 2026-09-30: the 4.6 rule):** only the subsidies containing the product decide; an unrestricted one containing it lifts the cap, and so does credit still available on a subsidy or group containing it (credit, topups, variation credit). `Helpers/SubsidyQuantityCapHelper`, 15 unit tests; T4–T6, T8. `subsidy-system.md` §5.2 and rule 8 corrected.
- **Kind:** Wrong.
- **4.6:** `AllowancesShoppingCartService.cs:198-208` lifts the cap only if (i) an **unrestricted** subsidy *contains the
  product*, (ii) a subsidy *containing the product* has global credit / topup > 0, or (iii) a group *containing the
  product* has credit / topup > 0.
- **4.9:** `SubsidyCreditService.cs:156-158` `if (packNameCredit.SubsidyCredits.Any(sc => !sc.RestrictToAllocatedQty)) return null;`
  — any unrestricted subsidy lifts the cap whether or not it contains the product; there is **no credit exemption at all**
  (`:160-172`), although `subsidy-system.md §5.2 "Credit incompatibility"` and §14 rule 8 claim one (lesson #20).
- **Examples:** S1 restricted, *Shirts* qty 2 and Credit $50, product A $20, add 3×A → 4.6 accepts (2 by quantity, 1 from
  credit); 4.9 refuses. Reverse: S1 restricted (*Shirts* ∋ A), S2 unrestricted (*Pants* only), add 5×A → 4.6 refuses; 4.9 accepts.
- **Should be:** port the three 4.6 conditions per subsidy / group that contains the product, then fix the document.
- **Decision:** confirm "credit lifts the quantity cap" is still wanted.
- **Size:** Small.

### ~~S-09. Inactive subsidies still grant credit~~ — **closed by phase 1, 2026-09-16**
- **Kind:** Bug (inherited).
- **4.6:** `AllowancesDatabaseService.cs:404, 484-487` (credit view filters `Deleted`, never `Active`; ordering views do).
- **4.9:** `CWDatabaseService.cs:600, 652-656` (subsidy credit view) and `:678, 711` (product-group view) filter
  `s.Deleted = 0` only; the ordering views filter `s.Active = 1` (`:727, 743, 771, 792`). `SubsidyReportingService.cs:158`
  shows `Active` for display while `SubsidyCreditService.GetPackNameSubsidyCreditAsync` (`:76`) allocates from it.
- **What happens:** an admin unticks *Active* expecting the subsidy to stop paying; it keeps paying.
- **Should be:** `AND s.Active = 1` in both credit views (and the `used` sub-queries for symmetry). History is unaffected.
- **Size:** Small.

### ~~S-10. Soft-deleted orders keep consuming credit~~ — **closed by phase 1, 2026-09-16**
- **Kind:** Bug (inherited).
- **4.6:** every consumption sub-query filters `o.OrderStatusId != 40` only (`AllowancesDatabaseService.cs:230, 282, 422, 441, 459, 477, 531, 553`).
- **4.9:** `CWDatabaseService.cs:351, 376, 403, 619, 642, 701` — same, no `o.Deleted = 0`. nop's `DeleteOrderAsync` is a
  soft delete that does not change the status.
- **Should be:** `AND o.Deleted = 0` beside every `OrderStatusId != 40`, in the history SP too. Decide whether deleting a
  non-cancelled order should also release credit (cancelling first is nop's normal path).
- **Size:** Small.

### ~~S-11. Topup period window: C# and SQL disagree on the last day; duplicates double-count~~ — **closed by phase 1, 2026-09-16** (variation-aware end carried to phase 3)
- **Kind:** Bug.
- **4.6:** one topup row per assignment, `Credit += delta` (`AllowanceController.cs:866-877`); no windowing.
- **4.9:** `SubsidyService.GetTopupSearchWindow` (`:101-128`): non-annual → `(MinValue, DurationEnd]` where `DurationEnd`
  is a `date` materialised at **00:00**; annual → `(lastYearEnd 23:59:59, thisYearEnd 23:59:59]`. SQL
  `subsidy_get_topup_period_end` (`CWDatabaseService.cs:542-578`) returns the same constant for every non-annual row, so
  every non-annual topup row joins (`:604, :683`). `SaveSubsidyTopupForPeriodAsync` (`:1057-1096`) and
  `SaveProductGroupTopupForPeriodAsync` (`:1157-1201`) are read-then-insert with no lock and no unique index
  (`SubsidyLedgerIndexesAndCleanup.cs:79-88` adds none).
- **What happens:** on the **last day** of a non-annual period (any time after 00:00 UTC) the C# window excludes the existing
  row and *Add* inserts a **second** `SubsidyTopup` row. (The dialog's *Topup Credit* field is **not** a symptom: it is an
  amount to add and always opens at 0 — `SubsidyModelFactory.PrepareSubsidyTopupModelAsync:904` sets `model.Credit = 0`
  unconditionally, after mapping the found row, and `SaveSubsidyTopupForPeriodAsync` does `existing.Credit += creditToAdd`.
  The defect is invisible in the dialog; it shows up as a second row and a doubled total.) The credit view then joins both
  rows → two `SubsidyPackNameCredit` rows for one subsidy → `GetPackNameSubsidyCreditAsync` (`:90-125`) creates two
  `SubsidyCredit` entries with the same `SubsidyId` → available **and** used credit are double-counted by the allocation.
  Two managers adding a topup concurrently produce the same duplicate in any period. `SubsidyVariation` has the same
  duplication exposure (`FirstOrDefault` in `:573`; views LEFT JOIN all rows, `:603, :682`). The window is also computed
  from `s.DurationEnd` even when `PackNamesOwnStartAndFinishDates` gives the packname a variation period.
- **Should be:** non-annual window end = end of day (`DurationEnd.Date.AddDays(1).AddTicks(-1)` or compare on `date`);
  serialise the two `Save*ForPeriodAsync` methods with `ILocker` keyed on the assignment (the pattern
  `RestoreSubsidyForReturnedItemAsync` already uses, `SubsidyCreditService.cs:537-540`) or a filtered unique index;
  use the effective (variation-aware) end in both C# and SQL; aggregate topups with `SUM` in a sub-query so a duplicate
  can never multiply rows.
- **Size:** Small–Medium.

### ~~S-12. Subsidy details read only in-period rows — Issue~~ — **closed by phase 3, 2026-09-29**
- **Kind:** Wrong.
- **4.6:** `AllowanceService.cs:1501-1547` starts from the assignment table with a **LEFT** join to the credit view and
  computes `InOrderingPeriod` from the ordering views; `:342-373` likewise.
- **4.9:** `SubsidyReportingService.cs:142-149` and `:76-81` query `vw_CORE_SUB_SubsidyPackNameCredit` directly, whose
  WHERE keeps only `sp.Active = 1` **and** in ordering period (`CWDatabaseService.cs:652-656`).
- **What happens:** (a) `InOrderingPeriod` (`:170`, `PackNameSubsidyModel.cs:54`) can never be false — dead flag; (b) an
  admin cannot see, top up or vary a packname's subsidy **before its period opens** (the 4.6 pre-season workflow is gone);
  (c) `SubsidyModelFactory.cs:709-711` dereferences the null result → `NullReferenceException` for an out-of-period or
  inactive assignment; (d) `SubsidyCatalogModelFactory.cs:409, 469` drops those assignments from the customer catalogue
  (the spec `subsidy-catalog-spec.md:510-518` asks for zero-credit entries); (e) `:79` joins `SubsidyVariation` without
  `!Deleted` (4.6 filtered, `AllowanceService.cs:344-346`) → a deleted variation's values can show / rows duplicate.
- **Should be:** drive the details queries from `CORE_SUB_SubsidyPackName` with LEFT joins to the credit and ordering
  views, filter `!sv.Deleted`, and let callers decide on period.
- **Size:** Small–Medium.

### ~~S-13. Feb-29 dates crash the smart-TTL calculator~~ — **closed by phase 1, 2026-09-16**
- **Kind:** Bug.
- **4.9:** `SubsidyOrderingService.cs:72-78` builds `new DateTime(year, DurationStart.Month, DurationStart.Day)` (same
  shape in 4.6 `AllowanceService.cs:156-162`). A subsidy or variation dated 29 February throws
  `ArgumentOutOfRangeException` in a non-leap year from every page that touches
  `GetInOrderingSubsidyPackNamesByPackNameIdAsync`, `GetSubsidyPackNameCanOrderAsync`, `GetSubsidyProductsByPackNameIdAsync`
  or `PackNameHasActiveProductGroupsAsync`. Nothing validates the date (`SubsidyValidator.cs` requires both dates and end > start).
- **Should be:** clamp with `DateTime.DaysInMonth` as `GetTopupSearchWindow` already does (`SubsidyService.cs:111`).
- **Size:** Small.

### ~~S-14. Admin topups accept zero / negative amounts — Issue~~ — **closed by phase 2b** (validators refuse ≤ 0 and cap the amount; decision 3); confirmed by phase 3, 2026-09-29
- **Kind:** Bug (inherited).
- **4.6:** `AllowanceTopupValidator.cs:12`, `ProductGroupTopupValidator.cs:12` — `Reason` only; `AllowanceController.cs:685-706`.
- **4.9:** `Areas/Admin/Validators/Subsidies/SubsidyTopupValidator.cs:12`, `ProductGroupTopupValidator.cs:12` — `Reason`
  only; `SubsidyController.cs:737-761, 782-806` save whatever arrives (`SaveSubsidyTopupForPeriodAsync` adds
  unconditionally); `SubsidyProductGroupUpdate` (`:578-604`) writes `Quantity`/`Credit` with no range check and no
  validator for `SubsidyProductGroupModel`; `SubsidyValidator` has no `Credit >= 0`. The storefront validators do guard
  (`Validators/Subsidies/SubsidyTopupInputModelValidator.cs:14-16`, `ProductGroupTopupInputModelValidator.cs:14-19`).
- **What happens:** a topup of 0 creates a pointless history row; a negative topup silently reduces credit while showing as
  a "topup"; negative group quantity / credit produce negative remaining amounts in the views.
- **Should be:** the storefront rules on the admin models. **Decision:** is "reduce a packname's credit" a legitimate admin
  operation? If so, expose it explicitly with its own reason and history type.
- **Size:** Small.

### ~~S-15. Shipping-subsidy default inverted vs 4.6 — Issue~~ — **closed by decision 4 (off by default is correct); documented in `subsidy-system.md` §6 by phase 6, 2026-10-02**
- **Kind:** Changed.
- **4.6:** `Jim2DefaultSettings.cs:507 ExcludeShippingOnAllowance` (default false → shipping **is** subsidised).
- **4.9:** `MultiStoreSettings.cs:109 IncludeShippingOnSubsidy` (default false → shipping **not** subsidised); read at
  `SubsidyAllocationService.cs:314-317`. `CWCorePlugin.cs:80-125` seeds only the four `*Enable` flags.
- **Decision:** confirm with the business; if 4.6 behaviour is wanted, seed `IncludeShippingOnSubsidy = true` at install and
  make sure migrated stores set it deliberately.
- **Size:** Small.

### ~~S-16. Store deletion not handled — Nice-to-have~~ — **closed by phase 3, 2026-09-29** (decision: soft-delete everything of the store that can be — `StoreDeletedEventConsumer`)
- **4.6:** `CompanyEventsHandler.cs:38-60` soft-deleted a company's allowances and product groups.
- **4.9:** `SubsidyBuilder.cs:19` / `ProductGroupBuilder.cs:18` declare `ForeignKey<Store>()` (cascade) while the children
  declare `Rule.None` to `Subsidy`, so deleting a store that owns subsidies fails at the database. Add an
  `EntityDeletedEvent<Store>` consumer or document "stores with subsidies cannot be deleted".
- **Size:** Small.

### ~~S-17. Shipping credit is not netted by restorations — CLOSED 2026-09-10~~ — **closed 2026-09-10** (decision 21: shipping credit is not restored, by design)

> **Decided by the manager (decision 21): shipping paid by subsidy is not restored on a return — the
> parcel was shipped.** Recorded in `subsidy-system.md` §14 rule 18. The paragraph below is the analysis
> as it stood.
- `SubsidyNetConsumptionSource` (`CWDatabaseService.cs:89-100`) nets item buckets only; `usedShipping` reads
  `CORE_SUB_SubsidyOrderShipping` directly (`:629-651`); `RestoreSubsidyForReturnedItemAsync` restores items only. When a
  whole order is returned the shipping credit is never given back — probably intentional (shipping was incurred); record the
  decision in `subsidy-system.md §14`.

### ~~S-18. Variation values shown regardless of store settings — Nice-to-have~~ — **closed by phase 3, 2026-09-29**
- 4.6 `AllowanceService.cs:528-530` gated `VariationCredit/DurationStart/DurationEnd` on the two store settings; 4.9
  `SubsidyService.cs:405-407` returns them unconditionally, so the subsidy's *PackNames* grid shows a variation the store
  has switched off. Display only.

### ~~S-19. Variation validator does not check end > start — Nice-to-have~~ — **closed by phase 3, 2026-09-29** (also: both dates or neither; credit 0 … 1,000,000)
- `Areas/Admin/Validators/Subsidies/SubsidyVariationValidator.cs:8-14` — `Reason` only (same in 4.6). A variation with
  end < start is never in period and silently blocks the packname.

### ~~S-20. Column types~~ — **closed by phase 1, 2026-09-16**
- Every money column is `AsDecimal()` without precision (e.g. `SubsidyBuilder.cs:22`, `SubsidyOrderItemBuilder.cs:23-28`);
  nop core uses `AsDecimal(18, 4)`, and `RestoreSubsidyForReturnedItemAsync` rounds to 4 dp (`SubsidyCreditService.cs:579`).
  `SubsidyRestoreItemBuilder.cs:30` uses `AsDateTime2()` while every other ledger `CreatedOnUtc` is `AsDateTimeOffset()`;
  the history SP unions them. Pick one type for the ledger and note it in `CLAUDE.md §14`.

### ~~S-23. Subsidy History shows the topup's running total on every row~~ — **fixed 2026-09-15**

**Fixed out of phase order**, at Eduardo's request, because the grid was reporting money wrongly to anyone
who opened it. One token in `CreateSPGetSubsidyHistorySql`'s subsidy-topup arm: `stop.Credit` →
`stoph.Credit`. The joins were left alone — `stop` still ties the history rows to the assignment, nothing
is read from it now. Verified in the admin grid: two Adds of 25 read **$25.00** and **$25.00**.
S-24 and change 16 remain open in the same stored procedure and are still phase 3's.

The finding as filed:


- **Kind:** Bug. **Found 2026-09-15** by Eduardo, during phase 1's M5: two Adds of 25 produced two history
  rows reading **$50.00** and **$50.00**, where `CORE_SUB_SubsidyTopupHistory` holds 25.00000 and 25.00000.
- **4.9:** `CreateSPGetSubsidyHistorySql`'s subsidy-topup arm selects `stop.Credit as CreditTopup` — the
  **parent** `CORE_SUB_SubsidyTopup` row's accumulated balance — while the history row's own delta,
  `stoph.Credit`, is never read. Every history row for one assignment therefore shows the same current
  total, and the figure changes retroactively on every later Add.
- **The arm four lines below gets it right:** the product-group topup arm selects
  `pgtoph.[Credit] as ProductGroupCreditTopup`, the history row's own amount. The two arms of one UNION
  disagree, which is what makes this a slip rather than a design.
- **What happens:** the history stops being a history. A manager cannot see what was added when or by whom
  — every line reports today's balance under yesterday's reason and creator, and the column sums to
  *n* × the total. It also masks S-11: two duplicate rows and one accumulating row look identical here.
- **Should be:** `stoph.Credit as CreditTopup`. The `INNER JOIN` to `stop` stays — it is what ties the
  history to the assignment — but nothing should be read from it.
- **Fix with change 16 and in the same pass as S-12**: same stored procedure, same grid, same fixture.
- **Size:** Small (one token, in a large constant).

---

### ~~S-24. Subsidy History *Created On* is late by the web server's UTC offset — Issue~~ — **closed by phase 3, 2026-09-29**

- **Kind:** Bug. **Found 2026-09-15** by Eduardo, on the customer Subsidies tab during phase 1's M5.
- **The measurement, not an inference.** A topup made at 21:20 local on a UTC+2 web server is stored in
  `CORE_SUB_SubsidyTopupHistory` as `2026-09-15 19:20:53.2975573 +00:00` — offset zero, digits true UTC,
  **correct**. `datetimesettings.defaultstoretimezoneid` is `SA Pacific Standard Time` (UTC−5),
  `datetimesettings.allowcustomerstosettimezone` is `False`, and the admin customer has no `TimeZoneId`
  attribute — so the grid should read **14:20**. It reads **16:20**. The error is exactly the web server's
  offset, which is why it is invisible on a UTC host.
- **Mechanism (leading hypothesis, one check away from settled):** the history SP returns `CreatedOn` as a
  `datetimeoffset` — every subsidy `CreatedOnUtc` column is `AsDateTimeOffset()`, and in a UNION
  `datetimeoffset` outranks `datetime2`, so even the `[Order]` arms are widened to it. Materialising that
  into the DTO's `DateTime` via `DateTimeOffset.LocalDateTime` rather than `.UtcDateTime` yields
  `21:20:53` — the web server's wall clock. `SubsidyModelFactory.cs:1021` then calls
  `ConvertToUserTimeAsync(item.CreatedOn, DateTimeKind.Utc)`, asserting a UTC that is no longer true, and
  subtracts five hours from the wrong number: `21:20 − 5 = 16:20`.
- **Confirmed against the `[Order]` arms**, which the widening predicts are shifted too. Orders 117, 118
  and 123 store `17:01:11`, `17:02:12` and `18:14:34` UTC; in the store's zone those are 12:01, 12:02 and
  13:14, and the grid shows **2:01 PM, 2:02 PM and 3:14 PM** — every one exactly two hours late. So the
  column is **uniformly** wrong, not a mix of correct and shifted arms, and the cast is needed on all of
  them.
- **Should be:** normalise in the SP — `CAST(<col> AT TIME ZONE 'UTC' AS datetime2)` on each arm's
  `CreatedOn`, so the value the factory labels UTC genuinely is. Fixing it at the factory instead would
  need the DTO to become `DateTimeOffset`, which is the phase 6 type question; the SP cast is the small,
  local fix and it survives that change.
- **Not caused by phase 1.** Every arm is shifted, including the `[Order]` ones whose column phase 1 never
  touched — the widening happens in the UNION, so the defect predates any of this work and would show on
  any non-UTC web server. Change 14's alteration of `SubsidyRestoreItem.CreatedOnUtc` from `datetime2`
  changes nothing here: that arm was being widened and shifted already.
- **Fix with S-23 and change 16**: same stored procedure, same grid, same fixture.
- **Size:** Small.

---

### ~~S-22. The topup window sends `DateTime.MinValue` to a `datetimeoffset` column~~ — **closed by phase 1, 2026-09-16**

- **Kind:** Bug. **Found 2026-09-15** by Eduardo, opening a customer's Subsidies tab. Pre-dates phase 1 —
  the method's original form returned the same sentinel from the same two branches.
- **4.9:** `SubsidyTopup.CreatedOnUtc` and `ProductGroupTopup.CreatedOnUtc` are `DateTime` on the entity
  and `datetimeoffset` in the database (`SubsidyTopupBuilder.cs`, `ProductGroupTopupBuilder.cs`), so every
  window comparison hands a `DateTime` parameter to a `datetimeoffset` column and
  `SqlParameter.CoerceValue` runs `new DateTimeOffset(DateTime)`. `GetTopupSearchWindow` returns
  `DateTime.MinValue` as "no lower bound" whenever `DurationEnd` is null or `AnnualRecurrence` is false
  (`SubsidyService.cs`, five call sites). For a value of kind `Unspecified` that constructor applies the
  **web server's** local offset: `0001-01-01` minus a positive offset is before year 0, and it throws
  *"The UTC time represented when the offset is applied must be between year 0 and 10,000"*.
- **What happens:** every read of the topup window dies on a server in a zone ahead of UTC — the admin
  *Product Groups* grid on the customer and packname Subsidies tabs hangs on *Loading…*, and
  `GetSubsidyTopupForCurrentPeriodAsync` / `SaveSubsidyTopupForPeriodAsync` /
  `GetProductGroupTopupForCurrentPeriodAsync` / `SaveProductGroupTopupForPeriodAsync` fail with it, which
  takes out *Change Topup* and the subsidy bar's topup figures. Any non-annual subsidy is enough.
- **Why it was not caught earlier:** on a server *behind* UTC the same coercion succeeds and silently
  shifts the bound by the offset instead of throwing, so the sentinel is a wrong answer there rather than
  an error. A UTC server is exact. The estate is UTC−5, which is why this surfaced only once a zone ahead
  of UTC was in play — a deployment in Australia or Asia would have met it on day one.
- **Should be:** null for an unbounded end, on both ends, with the predicate composed rather than compared
  against a sentinel — an absent bound then emits no parameter and no SQL comparison. Separately, a real
  bound must carry `DateTimeKind.Utc` so it coerces at offset zero;
  `TimeZoneInfo.ConvertTimeToUtc` already returns that. The entity/column type disagreement is the
  underlying fault and is carried to phase 6.
- **Blocker, not an Issue**, because it is an unhandled exception on an admin page that any deployment
  outside the Americas hits immediately, and because it silently corrupts the window everywhere else.
- **Fixed in phase 1** as change 17, with
  `TopupWindow_UnboundedLowerBound_IsNullNotTheDateTimeSentinel` pinning it.
- **Size:** Small.

---

**Verified as correctly ported (no finding):** schema field-by-field (`CompanyId → StoreId`; `*ExclTax` columns dropped
by a documented migration; pre-order tables dropped by design); the credit-consumption walk is line-for-line equivalent
(`SubsidyCreditService.cs:175-456` vs `AllowanceService.cs:1860-2171`); `vw_CORE_SUB_SubsidyInOrderingPeriod`,
`vw_…VariationInOrderingPeriod`, `vw_…SubsidyPackNameCanOrder`, `vw_…StoreSubsidyVariationSetting` identical; the history SP
is an improvement over 4.6 (server-side paging, adds the *Refund* type); `RestoreSubsidyForReturnedItemAsync` is idempotent
and locked; all 13 activity-log types and every admin/bar/catalogue/order-totals resource are installed.

---

## 5. Subsidy at the cart, checkout and order (C)

**Architecture note (needed to read this section).** 4.6 subtracted subsidy *inside* the subtotal: the stored
`OrderSubtotal*`, `OrderTax`, `OrderShipping*` and `OrderTotal` were all **net** of subsidy and every screen added the
subsidy back for display. 4.9 computes everything at **full price** and settles afterwards through
`ISettlementPipeline` (**subsidy priority 1, credit notes priority 2** — `SubsidySettlementSource.cs:68`,
`CreditNoteSettlementSource.cs:36`; this note said the reverse until 2026-09-16, when the code was read), storing gross
subtotal/tax/shipping and a **net** `OrderTotal`. One class, `SubsidyAllocationService`, decides consumption for quote and
placement alike — the "four copies" divergence of lesson #11 is gone. The 4.9 design is cleaner; the findings below are
where the two disagree in *result*, plus 4.9 defects on their own merits.

### ~~C-01. Two definitions of "the cart" disagree everywhere the count or contents are shown — Issue~~ — **closed by phase 4a Part 1, 2026-09-30**
- **Kind:** Wrong / Gap (lesson #11).
- **4.6:** `ShoppingCartOverrideService.cs:2668-2710` overrode `GetShoppingCartAsync` to read the packname view filtered to
  the current packname, so nop's cart, flyout, header count and totals all saw one filtered set.
- **4.9:** `GetShoppingCartAsync` is **not** overridden. Readers: packname checkout / totals / placement use
  `GetPackNameShoppingCartItemsAsync` (view, CanOrder-filtered — `PackNameCheckoutController.cs:409,486`,
  `OrderTotalCalculationOverrideService.cs:287,438`); header count and flyout "All packnames" total use nop's cart
  (`PackNameCheckoutController.cs:959-963, 1018-1022`, `ShoppingCartOverrideModelFactory.cs:553,567`); selector badges use
  `PackNameService.GetCartCountsByCustomerAsync` (view); the flyout's current-packname items use `GetPackNameCartAsync`
  (a join, **not** CanOrder-filtered — `ShoppingCartOverrideService.cs:386-401`); payment-method / checkout-attribute AJAX
  use nop's cart (`:737, 780, 865`).
- **What happens:** a customer whose packname just lost its window sees "5" in the header, "3" in the selector, three lines
  at checkout and (P-02) loses the other two at placement. `IsPaymentWorkflowRequiredAsync` is evaluated on different carts
  in `Checkout`, `GetPaymentMethods` and `UpdatePaymentMethod`.
- **Should be:** one reader — override `GetShoppingCartAsync` as 4.6 did, or make every surface call
  `GetPackNameShoppingCartItemsAsync`. **Decision:** are cannot-order lines *hidden and warned* or *shown and blocked*?
- **Size:** Medium.

### ~~C-02. `AddToCartAsync(packNameId)` can merge into the context packname's line — Issue (Blocker for re-order)~~ — **closed by phase 4a Part 1, 2026-09-30**
- **Kind:** Bug.
- **4.6:** the packname overload searched only the target packname's cart and inserted the rows itself
  (`AllowancesShoppingCartService.cs:570-575, 648-672`; BE `ShoppingCartOverrideService.cs:3014-3019, 3076-3099`).
- **4.9:** `ShoppingCartOverrideService.cs:233-263` overrides `ShoppingCartItemIsEqualAsync` to compare the line's mapping
  with **`_packNameContext.GetCurrentPackNameAsync`**, not with the `packNameId` argument. `:493-495` searches the target
  packname's cart correctly, but on a miss `:536-540` delegates to `base.AddToCartAsync`, which reloads the **whole** customer
  cart and applies that equality; `:546-556` then guesses the new row by `CreatedOnUtc >= before`.
- **What happens:** context packname A, call for packname B (exactly what `ReOrderAsync` does at
  `OrderProcessingOverrideService.cs:1286-1291` for each line of a multi-packname order): B's cart has no match → base
  finds A's equal line → A's quantity is increased, no row created, no `PackNameCartItem` for B, `warnings` empty (success).
  With no packname in context (`:252` → true) or an unmapped line (`:258-259`) any equal line of any packname is bumped;
  `MigrateShoppingCartAsync` at login hits the same path.
- **Should be:** the packname overload owns the insert (as 4.6), or the equality check takes the target packname
  explicitly; never find the new row by timestamp. K10 (empty warnings on an unusable packname, `:462-463`) is unchanged.
- **Size:** Small–Medium.

### C-03. ~~Subsidy catalogue gender filter not ported — Issue~~ — **closed by phase 5 Part 2, 2026-10-01**
- **Closed by deletion (Eduardo, 2026-10-01):** `GenderFiltering` removed from `MultiStoreSettings`, the model, factory, both
  settings saves, both settings screens and the plugin resources. No clean-up of the stored row or the resources
  (not in production). A gender filter, if ever wanted, is a new feature.
- **4.6:** `AllowanceCatalogModelFactory.cs:319-341` builds the *Gender* specification-attribute option set from
  `packName.Gender` when `GenderEnabled`, passes it to `AllowanceCatalogService.SearchProductsAsync` (`:125-146`);
  category pages did the same via `CatalogOverrideModelFactory.cs:979-1000`.
- **4.9:** `SubsidyCatalogModelFactory.cs:262` calls `SearchProductsAsync(storeId, productIds, visibleIndividuallyOnly: true)`
  with no `filteredSpecOptions`; `SubsidyCatalogCartService.cs:119-186` supports the parameter, nobody passes it.
  `GenderFiltering` is read nowhere outside the settings screens. `subsidy-catalog-spec.md §7, §17.5` specified it;
  `BulkOrdering_Future_Improvements.md F7` flags the same question for bulk ordering.
- **What happens:** a female packname is shown men's garments in the subsidy catalogue and in category listings.
- **Should be:** decide the mechanism once (a "Gender" specification attribute, as 4.1/4.6) and apply it in the subsidy
  catalogue, category pages (nop 4.9 `CatalogProductsCommand.SpecificationOptionIds`) and bulk ordering under
  `GenderFiltering` — or delete the setting. **Decision:** attribute / option names in 4.9 catalogues.
- **Size:** Small–Medium.

### C-04. ~~Catalogue restriction is list-only (extends K9) — Issue~~ — **closed by phase 5 Part 2, 2026-10-01**
- **Closed:** `IPackNameCatalogGate` (rule in `PackNameCatalogGateHelper`) answers what the catalogue packname may see;
  `AclOverrideService` applies it in `AuthorizeAsync<TEntity>` (products, categories, manufacturers) and in `ApplyAcl`
  (product, category and manufacturer queries). Exchange replacements: Exact / Same Style unrestricted, Different
  Style allocated only, a closed period never blocks them. Record: project `claude/Subsidy_Phase5_Part2_Closure_Record.md`.
- **4.6:** `AllowancesAclService` overrode `GetVisibleCategoryIdsByCustomer` (`:133-202`), `GetVisibleManufacturerIdsByCustomer`
  (`:204-254`), `IsCategoryVisible` (`:343-378`) and per-product `AuthorizeCustomVisibilityAsync` (`:267-308`), with
  packname-keyed caches expiring at the next period change.
- **4.9:** `AclOverrideService.cs` (146 lines) contains only the department-role helpers — no visibility override, no
  `AuthorizeAsync` override. Allocation is applied in exactly the five `ProductOverrideService` list methods K9 lists
  (`:115-177, 451-477, 485-514`). Product page, related products, cross-sell, recently viewed, compare, wishlist and the
  category / manufacturer menus go through `IAclService.AuthorizeAsync(product)` and are untouched.
- **What happens:** a restricted packname sees the full category menu; clicking a category yields an empty page; "related
  products" on an allocated product can show — and add (K9) — non-allocated products.
- **Should be:** K9's business question first (*forbidden* vs *merely unsubsidised*). If forbidden: override
  `AclService.AuthorizeAsync<TEntity>` for `Product` (virtual in nop 4.9, `Libraries/Nop.Services/Security/AclService.cs`)
  to consult `GetOnlyAllocatedProductIdsAsync`, and hide categories / manufacturers with no visible product via a
  `CatalogModelFactory` or `ICategoryService` override using the same cached id list.
- **Size:** Medium.

### ~~C-05. Order discount, gift cards and reward points are applied before subsidy — Issue (decision)~~ — **closed by phase 6 Part 1, 2026-10-02** (decision 5: the 4.1 / 4.6 order; `subsidy-system.md` §7)
- **4.6:** `JimOrderTotalCalculationServiceOverride.cs:1411, 1456-1490` applied them to the already-subsidised (net) figure.
- **4.9:** `OrderTotalCalculationOverrideService.cs:336-371` applies them on the gross total; settlement runs afterwards (`:399-403`).
- **Example:** one item $100, 10 % order discount, subsidy $100. 4.6: subsidised subtotal 0 → total 0 → subsidy consumed
  **$100**. 4.9: 100 → 90 → subsidy covers 90 → subsidy consumed **$90**, discount history records $10. With a $50 gift
  card: 4.6 gift card untouched, subsidy $100; 4.9 gift card consumed $50, subsidy $50.
- **Decision:** does an order-level discount / gift card reduce the customer's payable or the employer's subsidy spend?
  If 4.6's answer: settle before discount / gift card / reward points (pass `subtotal + shipping + fee + tax` to the pipeline
  and apply the rest to `BalanceDue`). Interacts with C-09.
- **Size:** Medium.

### ~~C-06. Order records gross subtotal / tax with a net total — Issue (decision)~~ — **closed by phase 6 Part 1, 2026-10-02** (decision 6: gross columns stay, `OrderTotal` = payable, subsidy is a tender; the settlement row records the rest)
- **4.9:** `OrderProcessingOverrideService.cs:706-767`: `OrderSubTotalInclTax`, `OrderTaxTotal`, `OrderShippingTotal*` from
  the gross calculators; `OrderTotal` = payable (`:754`). `Order.OrderTax` therefore includes GST on the subsidised part.
- **What happens:** `OrderSubtotalInclTax + OrderShippingInclTax + fee − discount ≠ OrderTotal` on every subsidised order.
  nop's own sales reports sum `OrderTotal` and `OrderTax`; the Odoo integration (not read) and any accountant reading the
  Order table get inconsistent figures; who remits the GST on the subsidised part is implicit. K6 is the *display* side;
  this is the *data* side.
- **Decision (finance + Odoo owner):** keep gross columns + settlement ledger (what `SettlementPlan` supports) and document
  it for reporting / Odoo, or write net figures at placement from the plan.
- **Size:** Medium (decision) / Large (if columns change).

### ~~C-07. Post-placement readers derive "gross" differently from checkout — Issue~~ — **closed by phase 6 Part 1, 2026-10-02** (one persisted row, `CORE_PN_OrderSettlement`, read by every reader)
- Checkout: `OrderTotal = plan.SettleableAmount`, `PayableBalance = GetShoppingCartTotalAsync` (`ShoppingCartOverrideModelFactory.cs:912-922`).
  Customer order page: `gross = order.OrderTotal + subsidyTotal` (`PackNameOrderModelFactory.cs:193-216`) — credit notes
  and exchange fees are **not** added back. Admin order page: a single "Subsidy Credit" line
  (`OrderSubsidyCreditViewComponent.cs:66-99`). E-mail and PDF: nop totals (K6 / M-03). `vw_CORE_SUB_OrderSubsidyTotal`
  values quantity units as `Quantity × oi.UnitPriceInclTax` (`CWDatabaseService.cs:900-901`) while allocation valued them
  as `sciInclTax / Quantity` (`SubsidyAllocationService.cs:114`) — equal only while nop rounds the unit price; test with a
  3-decimal price. `subsidy-system.md §8 / §8.2` still name `PreviewSubsidyBreakdownAsync` and
  `ProcessPackNameSubsidyCredit`; neither exists.
- **Should be:** one order-side "settlement summary" (gross = OrderTotal + credit notes + subsidy − fees, or persist
  `SettleableAmount` on the order) used by order page, admin, e-mail and PDF; K5's per-line view reads the same source.
- **Re-checked 2026-10-01 (phase 6 opening):** every premise holds, and two readers were missing from the list — the admin
  packname-details factory (`Areas/Admin/Factories/PackNameOrderModelFactory.cs:204–261`, a **third** unit valuation,
  `(Quantity + QuantityTopup) × UnitPriceInclTax`) and `OrderReportOverrideService.ProfitReportAsync` (adds subsidy back onto
  nop's profit, not credit notes). The readers: `subsidy-system.md` §7 "Who reads it".
- **Size:** Medium.

### ~~C-08. Pending-payment orders consume credit; no transaction around placement — Issue~~ — **closed by decision 7 (no limit); documented in `subsidy-system.md` §7 by phase 6, 2026-10-02**
- Order saved (`OrderProcessingOverrideService.cs:1427`) → items + `PackNameOrderItem` → settlement (`SettleOrderAsync :1152-1189`)
  under `RunWithPackNameLocksAsync` (`:968-993`), idempotent (`:1160-1161`), no DB transaction (same as nop core). Payment
  failure → no order → nothing consumed. **Redirect gateways:** order and subsidy rows are written before the customer
  pays; an abandoned Pending order keeps the credit consumed until someone cancels it. 4.6 used a PreOrder whose rows only
  counted while approved (`AllowancesDatabaseService.cs:434-450`) — out of scope, so the in-scope equivalent is an
  abandoned-pending-order clean-up or excluding `PaymentStatus = Pending` orders older than N hours from the views.
  The `//TODO` stubs from the integration plan (`RefundSubsidyForReturnAsync`, `UndoSubsidyForOrderAsync`) no longer exist;
  `RestoreSubsidyForReturnedItemAsync` replaced them.
- **Decision:** the pending-payment rule. **Size:** Medium.
- **Re-checked 2026-10-01 (phase 6 opening):** release on cancel is computed, not evented — every consumption read carries
  `o.OrderStatusId != 40 AND o.Deleted = 0` (`CWDatabaseService.cs:416, 442, 470, 694, 718, 793`), restorations included, and
  credit-note `Remain` skips cancelled orders the same way. Decision 7's "copy 4.1's release-on-cancel" needs no code; phase 6
  closes C-08 with the `subsidy-system.md` paragraph only.

### ~~C-09. Credit notes before subsidy; budget walked in cart order across packnames — CLOSED 2026-09-10~~ — **closed 2026-09-10** (decision 15: subsidy first)

> **Decided by the manager (decision 15, `Exchanges_Rebuild_Record.md` §3): subsidy is
> consumed before credit notes — units and money alike, and the employer's subsidy before a manager's own
> store credit.** Phase 4b swaps the two `Priority` values. The cart-order walk across packnames stays.
> The paragraph below is the analysis as it stood.
- Priorities `CreditNoteSettlementSource.cs:35` (1), `SubsidySettlementSource.cs:75` (2); `AllocateGoodsAsync`
  (`SubsidyAllocationService.cs:78-155`) spends the remaining budget line by line in cart order. (a) A manager's own credit
  note is spent before the employer's subsidy; (b) with several packnames in one cart the first packname's lines get the
  budget first. Documented as design in `subsidy-system.md §8`; confirm with the business because it moves money between parties.

### ~~C-10. Required-product auto-add and `MaximumShoppingCartItems` — Nice-to-have~~ — **closed by phase 4a Part 1, 2026-09-30**
- Required products re-enter the packname overload with its own restriction (`ShoppingCartOverrideService.cs:598-599`; 4.6
  exempted them, `AllowancesShoppingCartService.cs:451-452`); `base.AddToCartAsync` applies `MaximumShoppingCartItems`
  to the whole multi-packname cart (4.6: per packname, `:629-633`). Decide the intended semantics.

**Verified as correct:** consumption is keyed on the cart line's packname (manager-for-employee works,
`SubsidyAllocationService.cs:94, 239-242`), shipping per packname, rows written per employee; bucket order, rounding,
cancelled-order exclusion and `OrderOutsidePeriod` zero-credit injection in the catalogue all match 4.6; the per-line
checkout breakdown (`packname-checkout-item-subsidy-spec.md`) is implemented; 4.9 adds restoration on return, which 4.6 did not have.

---

## 6. Subsidy — admin UI (A)

The admin CRUD surface is a faithful 1:1 port: every `AllowanceController` action, grid column, popup filter (all eight
packname filters), inline edit and FluentValidation rule has a `SubsidyController` equivalent; all 180 locale keys the
subsidy screens reference are installed; every action carries `[CheckPermission(ADMIN_ACCESS_MULTISTORE)]` and a feature
guard; every referenced CSS/JS has a csproj entry. The findings are S-04, S-12, S-14 above and:

### ~~A-01. Pickers not store-scoped; add-popup POSTs accept forged ids (evidence for K7) — Issue~~ — **closed by phase 2b part 1, 2026-09-21**
- `SubsidyModelFactory.cs:413-419` calls `SearchProductsAsync(showHidden: true, categoryIds…, keywords…)` with **no
  `storeId`** (nop 4.9's signature has one); `:386-389` category / manufacturer dropdowns are all-store. The three POSTs
  insert whatever ids arrive: `ProductGroupProductPopup` (`SubsidyController.cs:222-245`), `SubsidyPackNamePopup`
  (`:410-442`, the *list* query is store-scoped but the POST is not), `SubsidyProductGroupPopup` (`:659-699`).
- **What happens:** store-A admin can attach store-B products, packnames or product groups to a store-A subsidy →
  cross-store credit consumption becomes possible.
- **Should be:** pass `storeId: productGroup.StoreId`; scope the dropdowns; filter selected ids by store in all three POSTs.
- **Size:** Small–Medium.

### ~~A-02. "Used %" always "0%" — Improvement~~ — **closed by phase 3, 2026-09-29**
- `SubsidyModelFactory.cs:494` and `:583` (`//TODO: calculate percent`, carried from 4.6 `:554, :638`); rendered in
  `_Subsidies.cshtml:113-118` and `_Subsidies.SubsidyDetail.PackNames.cshtml:94-99`. The per-packname panel computes it
  (`:216`), so the data path exists. Aggregate from `SubsidyPackNameCredit` or drop the columns.

### ~~A-06. The packname subsidy panel never shows used or remaining — Issue~~ — **closed by phase 3, 2026-09-29**

*Raised by Eduardo 2026-09-22, from the running admin screen.*

The per-packname *Subsidies* panel (`_PackNameSubsidies.cshtml`, fed by
`SubsidyModelFactory.PreparePackNameSubsidiesModelAsync`) shows **Credit Allocation**, **Topup Credit**,
the period, the flags and a **% Used** — and nothing that answers the only two questions an admin
actually has: *how much has this packname spent, and how much is left?* A worked example from the test
instance: allocation $50.00, topup $250.00, "% Used 37%". The admin is left to infer ~$111 spent and
~$189 remaining from a rounded percentage of a total the panel never states either.

**Both numbers are already computed in that method and discarded** (`SubsidyModelFactory.cs:192-193`):

```csharp
var totalCredit     = credit + packNameSubsidy.TopupCredit;
var totalCreditUsed = packNameSubsidy.UsedCredit + packNameSubsidy.UsedCreditTopup;
```

`totalCredit` and `totalCreditUsed` feed `UsedPercent` and are then thrown away. The DTO behind them
(`ISubsidyReportingService.GetSubsidyDetailsByPackNameIdAsync`) already carries `UsedCredit`,
`UsedCreditTopup` and their quantity siblings, so there is **no new query** — this is model properties
and view rows, with `CreditFormatted` / `TopupCreditFormatted` as the formatting pattern to copy.

- **What to show:** Total (allocation + topup), Used, Remaining — each formatted as money — and, per
  M-06, the same three for **quantity** when the subsidy allocates units. Keep the percentage; it reads
  well next to the amounts rather than instead of them.
- **Phase 3**, with A-02 and M-06: same factory method, same panel, same grid columns, and A-02's
  percentage fix is the half that is already half-done here. Doing them apart means opening this file
  three times.
- **Size:** Small. Larger if phase 3 also takes the *aggregate* percentage in the two grids, which is
  A-02's own `//TODO`.
- **Note the asymmetry worth resolving at the same time:** the panel computes a real percentage while
  `_Subsidies.cshtml:113-118` and `_Subsidies.SubsidyDetail.PackNames.cshtml:94-99` render a hard-coded
  `0%` from the `//TODO` at `SubsidyModelFactory.cs:494, 583`. So the same store shows 37% on one screen
  and 0% on another for the same packname, which is worse than showing neither.

### ~~A-03. Customer-edit subsidies panel needs the MultiStore permission — Nice-to-have (decision)~~ — **closed by phase 3, 2026-09-29** (decision A: the card is not rendered without `CW.CORE.Admin.AccessMultiStore`)
- 4.6 `AllowanceController.cs:821-836` used *Manage Customers*; 4.9 `PackNameSubsidiesViewComponent.cs:24-25` and
  `SubsidyController.PackNameSubsidies` (`:705-716`) require `ADMIN_ACCESS_MULTISTORE`, so a customer-service admin sees
  an empty *Subsidies* card. If intended, do not render the card.

### ~~A-04. Markup / JS defects — Nice-to-have~~ — **closed by phase 3, 2026-09-29** (the whole Subsidy admin section in the 2d shape)
- ~~`<div class="class="card card-default">`~~ — **fixed 2026-09-29** in all five: the three MultiStore
  partials (`_CostCenters.cshtml:12`, `_PackNames.cshtml:12`, `_Departments.cshtml:12`) while phase 2d was
  touching them, and `_Subsidies.cshtml:12` / `_ProductGroups.cshtml:12` pulled forward with them (change 288)
  rather than waiting for this phase. The attribute parsed as an empty `class` with `card` and `card-default`
  swallowed as bogus boolean attributes, so those cards rendered unstyled. Nothing left here; `validateRestrictAllocatedQty`
  calls `e.preventDefault()` with `e` undefined (`_Subsidies.SubsidyDetail.cshtml:189-197`, copied from 4.6); `row.Name`
  injected unescaped into `onclick` (`_Subsidies.cshtml:150`, `_ProductGroups.cshtml:198`); hard-coded `/Admin/Subsidy/...`
  URLs instead of `Url.Action` (`_Subsidies.cshtml:265,281`, `_Subsidies.SubsidyDetail.cshtml:47`, `_ProductGroups.cshtml:193,209`,
  `_PackNameSubsidies.cshtml:57`, `PackNameSubsidies/Default.cshtml:11`).

### ~~A-07. Product-group colour is not shown on the icon — Nice-to-have~~ — **closed by phase 3, 2026-09-29**
*Raised by Eduardo, 2026-09-29.*

The three grids that list product groups already tint the icon with the record's colour —
`_ProductGroups.cshtml:193`, `_Subsidies.SubsidyDetail.ProductGroups.cshtml:155` and
`_PackNameSubsidies.cshtml:347` all render
`<svg class="pg-icon" style="color:' + (row.Colour ? row.Colour : "#000000") + '">`.

The **detail form does not**. In `_ProductGroups.ProductGroupDetail.cshtml` the selected-icon preview
(`#pg-icon-preview`, built server-side at `:89-102` and rebuilt client-side at `:149-155`) and every swatch in
`#pg-icon-picker` (`:108-119`) emit the `<svg class="pg-icon">` with no `color`, so they render black whatever
colour is picked. The farbtastic picker (`#pg-color-picker`, wired at `:190`) is bound to the
`#pg-colour-field` input only; nothing reacts to it. The result is that the one screen where the colour is
chosen is the one screen that never shows the outcome — the user picks a colour, saves, and first sees the
tinted icon back in the grid.

**Fix:** apply `Model.Colour` to the preview `<svg>` on render, and keep it in step on every change —
farbtastic's callback form (`$('#pg-color-picker').farbtastic(function (c) { … })`, then set
`#pg-colour-field` and the preview together), plus a `change`/`keyup` handler on `#pg-colour-field` for a
value typed in directly. `Views/PackName/_PackName.Fields.Info.cshtml:146` already does exactly this for the
packname colour and is the pattern to copy. The client-side rebuild at `:149-155` must carry the current
colour too, or picking an icon after picking a colour resets the preview to black. Tinting the picker
swatches is optional — arguably they should stay neutral so the icon shape is what is being compared.

**Do it with A-04**, which rewrites the same two views' scripts.

### ~~A-05. Minor controller deviations — Nice-to-have~~ — **closed by phase 3, 2026-09-29**
- Activity-log text formats the **Id** for Add/Edit (`SubsidyController.cs:118,138,311,330`) where 4.6 used the **Name**;
  `SubsidyVariationEditPopup` GET dereferences a missing assignment (`SubsidyModelFactory.cs:709`) → 500;
  `PrepareAddProductToProductGroupSearchModelAsync` uses `SetGridPageSize()` instead of `SetPopupGridPageSize()` (`:395`);
  `SubsidyHistoryModel` and its AutoMapper maps (`CWMapperConfigurationProfile.cs:184-185`) are dead;
  `GetSubsidyPackNameProductGroupsAsync` (`SubsidyService.cs:938-939`) NREs on an unknown subsidy id; the paged
  `GetProductsByProductGroupIdAsync` (`:800-804`) does not exclude deleted products (unpaged variant does).

---

## 7. Subsidy — storefront: customer and manager (M)

### ~~M-01. Customer "My Subsidies" page does not exist — Issue (root cause of K2; absorbs K3, K4, K5)~~ — **closed by phase 7, 2026-10-08** (`customer/subsidies`: one card per assignment of the selected packname — status, remaining with a usage bar, product groups, history folded; decision A)
- **4.6:** route `{lang}/customer/allowances` (`RouteConfig.cs:17-18`) → `AllowancePackNameController.Allowances()`
  (`:293-306`) → `AllowancePackNameModelFactory.cs:46-120` (per subsidy: name, credit, topup, remaining, used %, in-period
  flag; per product group: available / used / total incl. topups; full history) → `Views/Customer/Allowances.cshtml` (241 lines).
- **4.9:** `Services/EventConsumer.cs:460-466` inserts a My-Account item with `RouteName = "Plugin.CW.Core.CustomerSubsidies"`
  (`CWCoreDefaults.cs:149`); `RouteProvider.cs` registers only `PackNameCheckout` and the four `SubsidyCatalog*` routes.
  No controller, view or factory exists. nop's `CustomerNavigation/Default.cshtml:12` renders
  `<a href="@Url.RouteUrl(item.RouteName)">`, which yields `href=""` for an unknown route — a link to the current page.
  That is the "does not load" symptom. Left-overs of a planned port: `Content/MyAccount/subsidies.css` (no view uses its
  classes), `Domain/Subsidies/DTOs/CustomerSubsidySummary.cs` whose only producer is the stub
  `SubsidyReportingService.GetCustomerSubsidySummaryAsync` (`:60-63`, `//TODO`), consumed by `CreditNotesController.cs:91-92`
  on the Store Credit page (the section is hidden because the stub returns "no subsidy"). The 4.6 customer resources
  (`Plugins.Jim2.Allowances.CustomerAllowances.*`, incl. "not in ordering period") were never ported.
- **Should be:** register the route + action + factory + view (the services exist: `GetSubsidyDetailsByPackNameIdAsync`,
  `GetSubsidyPackNameProductGroupDetailsAsync`, `GetSubsidyPackNameHistoryAsync` — `SubsidyReportingService.cs:136,186,229`,
  subject to S-12), answering K3's three questions (*what do I have left, what can I spend it on, when does it expire*),
  with K4's order link, or point the menu at `SubsidyCatalog` in the interim. **Decision (K3/K5):** how much bucket detail
  a customer should see.
- **Size:** Medium.

### ~~M-02. Manager cannot assign subsidies when adding / editing a packname — Issue~~ — **closed by phase 7, 2026-10-08** (checkboxes on the add / edit form, gated on `AllowSubsidySelection` and the Subsidy feature server-side; decision 10)
- **4.6:** `AllowancePackNameController.cs:312-333` (add form lists active allowances when `AllowAllowanceSelection`),
  `:459-473` (insert one mapping per selected id), `:512-517, 636-664` (edit: remove unselected, insert new);
  `Views/Customer/_PackName.Fields.Allowances.cshtml` (92 lines).
- **4.9:** `PackNameController.AddPackName/EditPackName` (`:404-683`), `PackNameRegisterModel`, `Views/PackName/_PackName.Fields*`
  contain no subsidy members; `AllowSubsidySelection` is read nowhere; `Content/Customer/manage-subsidies.css` (the dialog's
  stylesheet) is registered globally (`EventConsumer.cs:610`) for markup nothing renders.
- **Should be:** `PackNameRegisterModel` gains `SubsidyEnabled / AvailableSubsidies / SubsidyIds`; factory fills from
  `GetActiveSubsidiesByStoreIdAsync`; POST creates / deletes `SubsidyPackName` rows. **Decision:** keep the feature, and may
  managers also *remove* a subsidy (4.6 allowed it)?
- **Size:** Medium.

### ~~M-03. Subsidy / credit-note lines missing from e-mails, invoice PDF and admin (K6, evidence) — Issue~~ — **closed by phase 6 Part 1, 2026-10-02** (subsidy for goods / shipping and store credit on the e-mail, PDF, admin Info / Payments cards and admin packname card)
- **4.6:** `AllowanceMessageTokenProvider.WriteTotalsAsync` (`:169-430`) wrote "Allowance used for stock", "… for shipping",
  "Payable balance".
- **4.9:** `MessageTokenProviderOverride.WriteTotalsAsync` (`:453-620`) writes exchange fees and credit notes (`:613`) but
  nothing from `ISubsidyCreditService.GetSubsidyTotalByOrderIdAsync` (which the admin page already uses,
  `OrderSubsidyCreditViewComponent.cs:38`); PDF uses nop's `GetTotalsAsync` (`PdfOverrideService.cs:349`); admin shows one
  "Subsidy Credit" line without subtotal / tax / gross / payable (4.6 `_OrderDetails.Info.cshtml` showed stock, shipping,
  payable). Build on C-07's single settlement summary.
- **Size:** Medium.

### ~~M-04. "Landing page after login = Subsidies" — Nice-to-have (decision)~~ — **closed by phase 7, 2026-10-08** (decision H: `SubsidySendUserAfterLogin` sends a packname with product groups to the subsidy catalogue after login, selection and impersonation, as 4.6)
- 4.6 `AllowanceLandingPageOption.Allowances`, `AllowancesPackNameService.cs:101-132` redirected a packname with active
  product groups to the subsidy catalogue; applied on login and after `SelectPackName`. 4.9 has only the bulk-order landing
  (`CustomerRegistrationOverrideService.cs:86-104`). Reuse the `IsHomepageDestination` hook if wanted (see P-18).

### ~~M-05. Specification-attribute filtering semantics changed — Nice-to-have (decision)~~ — **closed as recorded by phase 7, 2026-10-08** (nop's strict filtering kept; `subsidy-system.md`)
- 4.6 `AllowanceCatalogService.cs:121-141` kept products with **no** mapping for the filtered attribute; 4.9
  `SubsidyCatalogCartService.cs:175-197` uses nop's standard AND filtering. Probably acceptable; record it.

### ~~M-06. A quantity-only subsidy displays no quantity anywhere — Issue~~ — **admin half closed by phase 3, 2026-09-29** (panel units row, units-based %, Used columns); **storefront half closed by phase 7, 2026-10-08** (My Subsidies shows units; a units-only group prints no `+ $0.00` in the bar, flyout and catalogue — decision M)
- **Kind:** Gap in the display, not in the calculation.
- **What happens:** on a subsidy that allocates **units and no credit value** — a legitimate and supported shape; it is
  the only shape on which `RestrictToAllocatedQty` may be set (`CWPlans/subsidy-system.md` §5.2) — the storefront panel
  renders every product group as `+ $0.00` with no global line, and the admin grid renders `0% used`. Both read exactly
  the same before and after a unit is spent, so neither the employee nor Sales Support can see the entitlement at all.
- **Why:** both figures are money-based. The panel totals credit; the percentage divides by an allocation that is zero
  (A-02 — the percentage is a `//TODO` in any case).
- **Verified 2026-09-11:** on subsidy "Only Allocated" (credit 0; Shirts 5 units, Shoes 2 units) the panel and the grid
  were unchanged across an order that consumed a unit and a return that gave it back. The allocation **is** consumed and
  restored correctly — a follow-up cart of three units priced two at $0.00 and the third at full price — it simply
  cannot be seen.
- **Should be:** a units row beside the money row wherever an entitlement is shown: `Shoes — 2 units, $0.00`. Do it with
  M-01 (the customer "My Subsidies" page), which is the same screen, and fix A-02's percentage to fall back to units
  when the credit allocation is zero rather than dividing by it.
- **Size:** Small on the panel; A-02's percentage is the larger half.

---

## 8. PackName — context, managers, visibility (P)

### P-01. ~~nop's standard checkout is still reachable for a packname customer — **Blocker**~~ — **closed by phase 2a, 2026-09-17**
- **Kind:** Bug (4.6 had the same hole).
- **4.6:** `Filters/PackNameFilter.cs:42-54` — only `ShoppingCartController.Cart` GET and `CheckoutController.Index` GET redirected.
- **4.9:** `Filters/PackNameCheckoutRedirectFilter.cs:47-60` — the identical two predicates. nop's `CheckoutController`
  also exposes `BillingAddress`, `ShippingAddress`, `ShippingMethod`, `PaymentMethod`, `PaymentInfo`, `Confirm`,
  `ConfirmOrder`, `OnePageCheckout`, `OpcSave*`, `OpcConfirmOrder` — none intercepted. `OrderProcessingOverrideService.PlaceOrderAsync`
  (`:1233-1263`) is nop's placement plus settlement with `packNameCart: []`; it never writes `PackNameOrderItem` or
  `OrderShipping`. Because `GetShoppingCartAsync` is not overridden (C-01), nop's checkout sees every packname's items as one
  ordinary cart.
- **What happens:** a manager who types `/onepagecheckout` (or follows an old bookmark / e-mail link) places one nop order
  with every packname's items, one shipping address, no destinations, **no subsidy consumed**, invisible to every packname
  screen and report.
- **Re-verified live 2026-09-11** (customer 15, packname 15, store 1), and it **stands**. With items in the cart:
  `/checkout` redirects to `/packname/checkout` (the filter catches `CheckoutController.Index`), but **`/onepagecheckout`
  renders nop's full one-page checkout** — billing-address step, address book and all — and `/checkout/billingaddress`,
  `/checkout/shippingmethod` and `/checkout/confirm` all land there too via nop's own redirect. ⚠️ **With an EMPTY cart
  every one of those URLs redirects to `/packname/checkout`**, which looks exactly like the hole being closed and is not:
  the redirect comes from nop bouncing an empty cart to the cart page, which the filter then catches. Test this one with
  a loaded cart or the result is meaningless (lesson #38).
- **Should be:** redirect every `CheckoutController` action and `ShoppingCartController.StartCheckout` to `PackNameCheckout`
  when a packname is in context (same `CoreActionFilter` pattern); and make `PlaceOrderAsync` refuse when the customer has
  any `PackNameCartItem` rows — belt and braces.
- **Size:** Small.

### ~~P-02. Placement clears the whole cart — **FIXED 2026-09-11**~~ — **fixed 2026-09-11** (exchanges fix 22)
- **Kind:** Wrong.
- **Status 2026-09-11: closed, both copies.** The **return-order** path was fixed in exchanges phase 4b-3 (fix 17) —
  `MoveShoppingCartItemsToOrderItemsAsync` removes only cart rows belonging to the return being processed, the same
  scoping `ExchangesLogic.UndoApprovedExchangedItemsAsync` uses. The **packname placement** path — the one this item is
  about — was fixed the same way in phase 4b-4 (fix 22). The analysis below is kept because it explains *why* the two
  lists differ, which is not obvious and is easy to undo.
- **The fix:** `MovePackNameShoppingCartItemsToOrderItemsAsync` ends with
  `foreach (var sc in details.PackNamesCart) await _shoppingCartService.DeleteShoppingCartItemAsync(sc.Id, resetCheckoutData: false);`
  in place of `ClearShoppingCartAsync`. The **id** overload is required — `sc` is a `PackNameShoppingCartItem`, a view
  entity, and the entity overload would aim the delete at the view. `resetCheckoutData` is false because `placeOrder`
  calls `ResetCheckoutDataAsync` immediately afterwards with `clearCouponCodes` and `clearCheckoutAttributes`, which is
  the order 4.6 used.
- **Still open, and separate:** nothing deletes the `CORE_PN_PackNameCartItem` link row when its `ShoppingCartItem` goes,
  on either path, and no consumer does it. Whether the FK cascades has **not** been confirmed — the entity and its builder
  were not available when this was written. If it does not, that table has been accumulating orphans on every checkout
  since day one. Worth 10 minutes with `Domain/Orders/PackNameCartItem.cs` and its builder.
- **Also unchanged by the fix:** nop's base validation still validates the invisible lines, so a warning on one of them
  can still block placement with a message about a product the checkout does not display. That is C-01 / P-01 territory,
  not this item.
- **4.6:** `Jim2OrderProcessingService.cs:1478-1479` deleted `details.Cart` — the packname view cart, i.e. only the ordered lines.
- **4.9:** `OrderProcessingOverrideService.cs:1097` `await _shoppingCartService.ClearShoppingCartAsync(details.Customer, order.StoreId);`
  → nop deletes **every** `ShoppingCartItem` of the customer / store. The order itself is built from
  `vw_CORE_PN_PackNameShoppingCartItem` (`:786`), which `INNER JOIN`s the CanOrder view (`WHERE pno.CanOrder = 1`) and
  `INNER JOIN CORE_Branch b ON pnl.BranchId = b.Id` (`CWDatabaseService.cs:829-838`; `PackNameList.BranchId` is nullable).
- **What happens:** any line whose packname cannot order, whose list has no branch, or which has no `PackNameCartItem` (added
  before the customer became a packname, or merged from a guest cart) is excluded from the order **and then deleted**. Never
  seen, never paid, gone. nop's base validation still validates those invisible lines, so a warning on one of them blocks
  placement with a message about a product the checkout does not display.
- **Should be:** delete exactly `details.PackNamesCart` (as 4.6) and validate the same list; decide what to do with
  invisible lines (C-01).
- **Size:** Small.

### P-03. ~~Department visibility (ACL) is never enforced — **Blocker**~~ — **closed by phase 5 Part 1, 2026-10-01**
- **Kind:** Missing.
- **4.6:** `CustomerOverrideService.cs:254-307` rewrote the role set on the public store: a non-customer packname got the
  card-file default roles (`:267-270`) and, when the packname was in a department, the department role replaced the branch
  role (`:286-303`); `GetCustomerRoleIdsAsync` (`:780-881`) and `GetCustomerRolesAsync` (`:892-923`) were overridden;
  `Jim2AclService.ApplyAcl` (`:341-359`) then filtered by those ids. Guards for admin / webhook context at `:264, 279, 286, 787, 899`.
- **4.9:** `AclOverrideService.cs:99-143` **writes** `AclRecord` rows for `Department.CustomerRoleId` (`DEP_` roles) and
  `CustomerOverrideService.cs:105-113` **hides** those roles from role lists — but nothing overrides
  `GetCustomerRoleIdsAsync` / `GetCustomerRolesAsync` (grep: absent), no code maps any customer, packname or manager to a
  `DEP_` role, and nop's `AclService.ApplyAcl` filters on the plain role mappings.
- **What happens:** an admin restricts a category / product / manufacturer to Department *Sales* (the `AclDepartments`
  panel); `SubjectToAcl` becomes true; the only ACL row is the `DEP_` role; **the item disappears for everybody, including
  Sales packnames.** A manager ordering for a Sales packname sees exactly what the manager sees. The whole reason
  `Department.CustomerRoleId` exists is inert.
- **Should be:** override `CustomerService.GetCustomerRoleIdsAsync` / `GetCustomerRolesAsync` (4.6's pattern, registered
  in `NopStartup.cs:48`) to add `Department.CustomerRoleId` to the role set outside the admin area when
  `IPackNameContext.GetCurrentPackNameAsync` returns a packname with a `DepartmentId` (and use the packname's own scope for a
  non-customer packname). Keep the admin / webhook guards. Verify nop 4.9's short-term cache key for role ids varies by
  packname (4.6 used the role ids as key material, `Jim2AclService.cs:95-96`).
- **Decision:** does a manager ordering for a department packname see the *union* (own roles + department) or only the
  department view? 4.6 replaced the branch role with the department role and kept the rest.
- **Size:** Medium.
- **Closed (phase 5 Part 1, 2026-10-01).** Decided by Eduardo: what is **seen** follows the packname — its account's
  roles (or Registered + the store's default packname roles for a packname without one) plus its department's role;
  what is **paid and allowed** follows the customer signed in. `CustomerOverrideService.GetCustomerRoleIdsAsync`
  answers with that set in the public store for the signed-in customer, with the signed-in roles carried as negative
  ids so nop's price caches stay per customer (`CustomerRoleSetHelper`); `GetCustomerRolesAsync` is untouched; tier
  prices and plugin role limits read `GetSignedInCustomerRoleIdsAsync`; `AclOverrideService.AuthorizeAsync` reads the
  ids so pages follow the lists. Each cart line is checked against its own packname (`PackNameCatalogScope`), as is
  the exchange replacement picker. The department save on *Create* (`EventConsumer.HandleEntityEventAsync`) now
  writes to the event's entity. Record: project `claude/Subsidy_Phase5_Part1_Closure_Record.md`.

### P-04. ~~Manager-mapping cache consumer throws; manager scope stale — **Blocker**~~ — **closed by phase 2a, 2026-09-17**
- **Kind:** Bug.
- **4.9:** `Services/PackNames/Caching/PackNameListManagerCacheEvenConsumer.cs:16`
  `await RemoveAsync(PackNameServicesDefaults.PackNameOwnedByManagerCacheKey, entity.PackNameId);` — the key is
  `"CW.packname.owned.bymanager.{0}-{1}"` (`PackNameServicesDefaults.cs:138`). nop's `CacheEventConsumer.RemoveAsync`
  (`Libraries/Nop.Services/Caching/CacheEventConsumer.cs:83-87`) calls `_shortTermCacheManager.Remove(key, params)` →
  `PerRequestCacheManager.Remove` (`:70-72`) → `CacheKey.Create` → `string.Format("…{0}-{1}", [id])` (`CacheKey.cs:39`)
  → **`FormatException`**. `EventPublisher` swallows and logs it, so lines 17-19 (`PackNamesByManager`,
  `PackNameListsByManager`, `PackNameListIdsByManager`) never run.
- **What happens:** every add / remove of a list owner (admin `ManagerAddPopup` / `ManagerDelete`,
  `MultiStoreController.cs:1015-1055`) logs an error and leaves the manager's scope cached with the old value until the
  static cache expires — the manager keeps (or fails to get) access. Expect `FormatException: Index (zero based) must be
  greater than…` in the log after adding a list owner.
- **Should be:** `RemoveByPrefixAsync(PackNameOwnedByManagerCacheKeyAllPrefix + "{0}-", packNameId)` — never `RemoveAsync`
  with fewer parameters than placeholders. Audit every `RemoveAsync` in the plugin for the same shape.
- **Size:** Small.

### P-05. ~~Cached manager list returned by reference and mutated — Issue~~ — **closed by phase 2a, 2026-09-17**
- **4.6:** `CompanyService.cs:1325-1328` projected the cached value to a fresh list before callers added to it.
- **4.9:** `MultiStoreService.GetPackNameListsByManagerIdAsync` (`:714-730`) returns the cached `List<PackNameList>` object;
  `GetAvailablePackNameListsAsync` then does `lists.Add(packNameList)` (`:1027`), `lists.Add(depPackNameList)` (`:1041`) and
  `lists.Add(new PackNameList { Id = -department.Id … })` (`:1047-1053`). `MemoryCacheManager` stores references.
- **What happens:** each selector render appends the manager's own list and one virtual "(Dept)" list per department to the
  cached list *again*; the dropdown grows duplicates until the entry expires, and other consumers
  (`PackNameController.cs:492`, `GetVisibleBranchesForManagerAsync :739-746`) see virtual lists with negative ids and
  `BranchId = department.Id` treated as a branch id.
- **Should be:** return a copy (`.ToList()`), or build "available lists" into a new list. **Size:** Small.

### ~~P-06. Manager scope SP is broader than 4.6 — Issue (decision)~~ — **closed**: the `b.Deleted = 0` filter on path 2 by phase 2b (2026-09-23), the rule itself kept as intended (decision 12) and written down by phase 10 (2026-10-21, `MultiStore_Management_Reference.md` §7b.2)
- **4.6:** `CompanyService.cs:1304-1323` — a manager sees (a) `AllManagers` lists in his own card code **or its children**,
  (b) `ParentManagers` lists in child card codes, (c) lists mapped to him directly. Own-branch lists without `AllManagers`
  and without a direct mapping were **not** visible; only branches with a mapped ship address counted (`:1290-1302`).
- **4.9:** `CWDatabaseService.cs:126-160` (`packname_get_listids_by_manager`): Path 1 = **every** active list on the
  manager's own branch "regardless of flags" (`:126-131`); Path 2 = `AllManagers` lists **store-wide** (`:135-141`); Path 3 =
  `ParentManagers` in descendants; Path 4 = direct mapping. Path 2 joins `Branch` without `Deleted = 0` (`:138`).
- **What happens:** (1) a private list in a manager's own branch (the `AllManagers = false` + explicit owners model that
  `CLAUDE.md §14.2` documents) is visible to every manager of that branch; (2) an `AllManagers` list in an unrelated branch
  is visible store-wide; (3) lists on soft-deleted branches still surface. `MultiStore_Management_Reference.md` documents
  none of this.
- **Decision:** write the intended semantics down; if 4.6's, Path 1 requires `AllManagers = 1` or a mapping, Path 2 is
  restricted to the hierarchy, both filter `b.Deleted = 0`. **Size:** Small (SQL) once decided.

### P-07. ~~Ownership checks ignore `PackName.Manager`; `SelectPackName` NRE — Issue~~ — **closed by phase 2a, 2026-09-17**
- `PackNameController.SelectPackName` (`:362-393`) calls `IsPackNameOwnedByManagerId(manager.Id, id)` (`:374`) but never
  checks `manager.Manager`; the SP Path 1, `GetPackNameIdsByManagerId` (`MultiStoreService.cs:792-820`) and
  `IsPackNameOwnedByManagerId` (`:830-856`) do not either; `PreparePackNamesModelForManagerAsync` (`:266-350`) lists any
  packname the SP returns. `:374` dereferences `manager` without a null check → a logged-in customer with no packname
  hitting `/PackName/SelectPackName/5` gets a 500.
- **What happens:** a plain employee who types `/PackName/SelectPackName/{id}` for a colleague in the same branch becomes the
  ordering context for that colleague (the selector UI is hidden, `PackNameSelectorViewComponent.cs:101-102`, but the
  endpoint is open); `POST /PackName/PackNames?packNameListId=` lists colleagues to a non-manager.
- **Should be:** a non-manager owns only himself; handle `manager == null`. **Size:** Small.

### ~~P-08. Current packname is not revalidated; cart lines not removed on delete — Issue~~ — **closed by phase 4a Part 1, 2026-09-30**
- **4.6:** `PackNameContext.cs:165-185` filtered `!Deleted` (`:172`); admin edit cleared the previous customer's selection
  (`CompanyController.cs:1294-1305`); `PackNameEventsHandler.cs:55-66` deleted the packname's cart lines on delete,
  `:37-48` deleted packnames on list delete.
- **4.9:** `PackNameContext.cs:209-215` uses `GetByIdAsync(packNameId, cache => default)` — nop's `IRepository.GetByIdAsync`
  returns soft-deleted rows by default; nothing checks `packName.Active`, the list's `Active` / `Deleted` — only the store
  (`:148-153`). `EventConsumer.HandleEventAsync(EntityDeletedEvent<PackName>)` (`:761-772`) deletes only `SubsidyPackName`
  rows; no cart clean-up; no `EntityDeletedEvent<PackNameList>` consumer; `PackNameCartItem` FK cascade never fires because
  packnames are soft-deleted; shipping assignments stay in the customer's JSON attribute.
- **What happens:** after an admin deletes / deactivates a packname (or moves it to another store's list), a manager who had
  it selected keeps ordering as it; the lines stay in `ShoppingCartItem`, invisible to the checkout (view filters
  `pn.Deleted = 0`) but counted in the header (C-01) and deleted at the next placement (P-02).
- **Should be:** the context resolves only `!Deleted && Active` packnames whose list is `!Deleted && Active` and in the
  current store, clearing the attribute otherwise; packname delete cascades to `PackNameCartItem` + `ShoppingCartItem`
  and `assignments.ClearPackName(id)`, and clears `SelectedPackNameId` for customers pointing at it; list delete cascades to
  packnames (the service already deletes them, `PackNameService.cs:298-309`, so the per-entity events fire).
- **Size:** Medium.

### ~~P-09. Session "ignore packname context" switch not ported — Issue (decision)~~ — **closed as not needed by phase 7, 2026-10-08** (decision I; `subsidy-system.md`)
- 4.6 `PackNameContext.cs:232-248, 371-386` (`IgnorePackNameContext`, session key per customer; every getter returned null
  when set) let a flow run one request "as the customer, not as the packname" (payment-method roles, quotes, approvals).
  4.9 `IPackNameContext.cs:147-174` has no such member. Decide whether any in-scope flow (exchanges, return wizard, payment
  methods by role) needs it; if so, port it via `ISession`.

### ~~P-10. Print name no longer pre-filled into the product attribute — Issue~~ — **closed by phase 7, 2026-10-08** (`PrintNamePrefillEventConsumer` fills the text box of `PrintNameProductAttributeId` with the selected packname's print name; a cart-line edit keeps its own value)
- **4.6:** `ProductOverrideModelFactory.cs:447-489` — when `PrintNameEnabled && PrintNameProductAttributeId > 0` the current
  packname's `PrintName` became the `DefaultValue` of the mapped textbox attribute on the product page.
- **4.9:** `PrintNameProductAttributeId` is read nowhere outside settings and the packname *edit* form (`PackNameRegisterModel.cs:53`,
  `_PackName.Fields.Info.cshtml:123`, `Factories/PackNameModelFactory.cs:381,466`).
- **What happens:** the store owner picks the "Name to print" attribute and the customer retypes the name on every garment;
  the packname's print name never reaches the order line.
- **Should be:** a `ModelPreparedEvent<ProductDetailsModel>` case in `EventConsumer.HandleEventAsync(ModelPreparedEvent<BaseNopModel>)`
  (`:837-853` already dispatches on that model) setting `DefaultValue` from the current packname; the same per packname
  line at the packname checkout. **Size:** Small–Medium.

### ~~P-11. Cost-centre addresses never offered at checkout; `MultiStoreSettings.ShippingMethod` dead — Issue (decision)~~ — **closed**: cost-centre delivery not needed (decision 12, the address code left as it is); `ShippingMethod` implemented as N-01 by phase 8 Part 1, 2026-10-11
- `CostCenterAddressMapping` has a full admin UI, service and cache keys (`MultiStoreController.cs:1754-1870`,
  `MultiStoreService.cs:512-589`) but no factory, controller or view under `Factories/`, `Controllers/`,
  `Views/PackNameCheckout/` references it; `PrepareAvailableAddressesAsync` (`PackNameCheckoutModelFactory.cs:227-316`)
  offers branch + (if `AllowHomeDelivery`) packname / customer addresses; `IsAddressValidForCheckoutAsync`
  (`ShoppingCartOverrideService.cs:657-675`) accepts only those, so even a hand-posted cost-centre address is rejected.
  `MultiStoreSettings.ShippingMethod` (`:101`) is written by the settings screen and read nowhere (4.6 used it as the
  per-company fallback; 4.9's fallback lives in `BranchService.cs:578-596`).
- **Decision:** is cost-centre delivery wanted (and does it override the branch default)? Either offer + validate them and
  use `ShippingMethod` as the fallback in `SetShippingOptionsByPackNamesAsync:962-977`, or delete both. **Size:** Medium.

### ~~P-12. "Branch default address only / cannot select" policy lost — Issue (decision)~~ — **closed by decision 12 (not needed)**; `CLAUDE.md` §15.3 corrected by phase 8 Part 1, 2026-10-11
- **4.6:** `UseBranchAddress`, `AllowSelectShipAddresses`, `AllowCustomersToAddAddress` (`Jim2DefaultSettings.cs:479-483`);
  `PackNameCanSelectAddress = !UseBranchAddress || AllowSelectShipAddresses` (`ShoppingCartOverrideService.cs:3452-3455`);
  when selection was not allowed the only valid address was the branch default (`:3432-3437`), `UpdateShippingAddress`
  refused (`PackNameCheckoutController.cs:899-900`), and the default was pre-selected even with `DoNotPreSelectShippingAddress` on.
- **4.9:** `AllowHomeDelivery`, `AllowCustomersToAddAddress`, `DoNotPreSelectShippingAddress` (`MultiStoreSettings.cs:103-107`);
  every branch address is always selectable (`PrepareBranchAvailableAddresses:256-280`, `IsAddressValidForCheckoutAsync:662-663`);
  no `CanSelectAddress` on the models. `CLAUDE.md §15.3` still lists `UseBranchAddress`, which does not exist.
- **What happens:** a store that wants every parcel to go to the branch's one default address cannot enforce it.
- **Decision:** is "default branch address only" still required? If so, one boolean gating factory, controller and validity
  check. Update `CLAUDE.md §15.3`. **Size:** Small.

### ~~P-13. `DefaultPackNameCustomerRoleIds` never applied — Issue~~ — **closed by phase 7, 2026-10-08** (the roles are added at storefront registration by a manager; decision E)
- 4.6 `Jim2CustomerController.cs:1596-1603` and `Jim2CardFileSync.cs:838-844` added each configured role to the new employee
  customer. 4.9: read only in settings code; `PackNameController.RegisterCustomer` (`:208-315`) assigns nothing beyond
  nop's *Registered*; same for the admin associate / create-customer flows. Manager-created customers never get the roles
  the store configured (price lists, ACL). **Size:** Small.
- **Partly closed (phase 5 Part 1, 2026-10-01):** a packname **without** an account now sees the catalogue as Registered +
  these roles + its department. Assigning them to a customer at registration is still open; until it is, a product
  restricted to a default role is hidden from packnames **with** an account (decision 6 of that phase).

### ~~P-14. Manager-created customer: password = e-mail, no e-mail, `ForceToChangePassword` never written — Issue~~ — **closed by phase 7, 2026-10-08** (random password nobody sees, welcome e-mail with nop's set-password link; the dead attribute names deleted; decision D)
- 4.6 `Jim2CardFileSync.cs:726-736` had the same password shortcut and no force-change. 4.9 `PackNameController.cs:225-233`
  `var password = model.Email;`; `:301-302` publishes the events but sends no message;
  `ExtendedCustomerAttributeNames.ForceToChangePassword` (`:9`) is never written or read.
- **Should be:** a random password; nop's welcome or a "set your password" e-mail (`IWorkflowMessageService.SendCustomerWelcomeMessageAsync`
  / password-recovery token); implement the force-change (a login filter to `ChangePassword`, or use nop's
  `CustomerSettings.PasswordLifetime`) or delete the dead attribute. Ties to X-03. **Size:** Small–Medium.

### ~~P-15. `UseStaffAsPackName` / `UseBranchesAsPackNameList` promise automation that does not exist — Improvement (decision)~~ — **closed by phase 8 Part 1, 2026-10-11** (`IPackNameProvisioningService`: a new branch gets its default list, a customer an admin links to a branch gets a packname; PackName feature-gated; the Generate buttons use the same service)
- 4.6 `CompanyEventsHandler.cs:99-123` auto-created the default list when a card file was added and a packname for every
  customer without one. 4.9 stores the two settings and uses them only for the manual *Generate* buttons
  (`MultiStoreController.cs:171-198, 224-247`); the hint text still promises automation (`CWCorePlugin.cs:962`). A new
  branch has no default list until an admin presses Generate, so manager "Add packname" fails with "The manager has not
  access to any list from the selected branch" (`PackNameController.cs:504`). Implement the automation on branch /
  customer insert, or rename the settings and hints.

### ~~P-16. `AllManagers` / `ParentManagers` exclusivity dropped — Improvement~~ — **closed by phase 2b, 2026-09-23** (`SavePackNameList` forces `ParentManagers = false` when `AllManagers`; verified in code 2026-10-21)
- 4.6 `CompanyController.cs:890-891, 951-952` forced `ParentManagers = false` when `AllManagers`; 4.9 `SavePackNameList`
  (`MultiStoreController.cs:838-905`) and `PackNameListValidator` do not. Harmless for the SP (union) but the UI state is
  inconsistent.

### ~~P-17. Multi-store scoping holes — Improvement~~ — **closed by phase 2b, 2026-09-23** (store-filtered manager queries, the mapping POSTs check store / `Manager` / `Deleted`, the background context getter deleted)
- `GetDepartmentsByManagerIdAsync` (`MultiStoreService.cs:246-261`) has no `StoreId` / `Active` filter and the
  department-manager POST (`MultiStoreController.cs:2041-2063`) does not re-check the department's store;
  `GetPackNameListsByDepartmentIdsAsync` (`:271-285`) has no store filter (4.6 same);
  `PackNameContext.GetCurrentPackNameForBackgroundAsync` (`:291-300`) skips the store check (unused, but a trap); admin
  `AssociateCustomerToPackNamePopup` POST (`:1354-1369`) accepts any customer id (U-04).

### ~~P-18. Landing-page redirect after login / packname selection dropped — Nice-to-have (decision)~~ — **closed by phase 7, 2026-10-08** (with M-04: one `IPackNameLandingService` used by sign-in, selection and impersonation)
- 4.6 `PackNameService.cs:765-785` + `AllowancesPackNameService.cs:191-219` (subsidy half in scope), applied by
  `Jim2CustomerRegistrationService.cs:284-288` and `PackNameFilter.cs:494-508`. 4.9 `SelectPackName` (`:395-401`) always
  returns to `returnUrl` / home. Same decision as M-04.

**Verified as correct / improved in 4.9:** the context checks the selected packname's list belongs to the **current store**
(4.6 had no store notion); lazy self-selection makes impersonation work without an event consumer; the feature toggle
degrades every consumer to nop behaviour when off; cart counts are one grouped query per request; list delete cascades
to packnames in the service; `SelectPackName` validates ownership (4.6 did not); manager-created customers get a
`CustomerBranch` row.

---

## 9. PackName — cart and checkout (G)

### ~~G-01. Coupon and gift-card entry has no home for packname customers — Issue (decision)~~ — **closed by decision 14 / G, recorded by phase 7, 2026-10-08** (out of scope for packname stores; phase 6 keeps the money safe either way)
- 4.6 `Jim2ShoppingCartController.cs:2695-2992` had a `PackNameCart` page with `ApplyDiscountCoupon` / `ApplyGiftCard`.
  4.9 redirects the nop cart page to `PackNameCheckout`, whose views and controller contain no coupon / gift-card box,
  while the totals still compute and display discounts and gift cards (`ShoppingCartOverrideModelFactory.cs:930-956`).
  Add the two boxes to `_OrderSummary.cshtml` with mirror actions, or state that coupons / gift cards are out of scope for
  packname stores (see C-05 for what they would do to the subsidy).

### ~~G-02. `AddAddress` POST: null dereference, no ownership check, no address validation — Issue~~ — **closed by phase 2b part 1, 2026-09-21**

> **Not done in phase 2a** (its "part 2"). Moves to **phase 2b**.
- `PackNameCheckoutController.cs:637-642` adds a ModelState error for a null packname then reads `packName.PackNameListId`
  → `NullReferenceException` (4.6 `:709-714` same); GET (`:621-632`) and POST (`:635-673`) never check that
  `model.PackNameId` belongs to the current customer (compare `PackNameController.cs:373-374, 700-703`); no validator for
  `PackNameShippingAddressModel` (4.6 had `PackNameShippingAddressValidator.cs:12-20` with `SetValidator` on `Address`) —
  only custom-attribute warnings are checked (`:652-657`). An empty address is accepted and stored.
- **Should be:** `NotFound()` on null; ownership check; a `BaseNopValidator` with `RuleFor(x => x.Address).SetValidator(...)`.

### G-03. ~~Public `PackNameController` POSTs have no anti-forgery validation — Issue~~ — **closed by phase 2a, 2026-09-17**
- `PackNameController.cs:35-36` carries only `[CheckCWFeature]`; nop applies `[AutoValidateAntiforgeryToken]` per controller
  and every other CW public controller has it (`PackNameCheckoutController.cs:43`, `SubsidyCatalogController.cs:19`,
  `BulkOrderController.cs:18`). `PackNames` (`:347`), `AddPackName` (`:425`), `EditPackName` (`:587`), `DeletePackName`
  (`:685`) are CSRF-exposed although the views already send the token. Add the attribute. **Size:** Small.

### ~~G-04. Free-shipping threshold and shipping discounts evaluated on the whole cart — Issue (money, inherited)~~ — **closed by phase 4a Part 2, 2026-09-30**
- 4.6 `JimOrderTotalCalculationServiceOverride.cs:227, 243` and 4.9 `OrderTotalCalculationOverrideService.cs:187, 203`:
  `IsFreeShippingAsync(packNameItems)` per destination but `AdjustShippingRateAsync(rate, cart /* whole cart */, …)`.
  Ten packnames each spending $20 to ten branches all get free shipping when "free over $100" is on; shipping discounts
  apply once per destination against the whole-cart subtotal and `appliedDiscounts` accumulates duplicates (`:204`).
  Pass `packNameItems`. Also `taxRate` is assigned twice (`:234, :240`).

### ~~G-05. Shipping "cannot be calculated" reported as $0 — Issue (inherited)~~ — **closed by phase 4a Part 2, 2026-09-30**
- `OrderTotalCalculationOverrideService.cs:442-443` returns `decimal.Zero` when any packname price is null (4.6 `:411-412`
  same). nop's contract is `null` = not computable; `GetTaxTotalAsync`, `IsPaymentWorkflowRequiredAsync`, the OrderTotals
  component and reward points all read it. The internal total (`:304-305`) does treat it as null, so the two disagree.

### ~~G-06. Silent failures in checkout AJAX endpoints — Issue~~ — **closed by phase 4a Part 1, 2026-09-30**
- `PackNameCheckoutController.cs:708-724` — an invalid `addressId` returns `success = true` with no message;
  `IsAddressValidForCheckoutAsync:672-674` says "the caller is responsible for persisting the cleared assignment" and neither
  this caller nor `ValidateShippingData:301-310` does. `ConfirmOrder:395-398` turns every exception into a warning string
  with no log (nop logs a warning). `branchId` silently becomes 0 at `:706`.

### ~~G-07. Checkout recomputation is quadratic — Improvement — **closed by phase 9, 2026-10-20**~~ — **closed by phase 9, 2026-10-20**
- `PackNameCheckoutModelFactory.cs:897-1029` — one freight call per packname (`:933`) plus one per shared-address group
  (`:1013`), with `GetPackNameByIdAsync` / `GetAddressByIdAsync` per packname; called from six actions.
  `_OrderSummary.cshtml:270-280 loadPackNamesBody()` reloads **every** panel after any change, each request recomputing
  shipping (`PrepareShippingOptionAsync:521-549`) and rebuilding the branch address list (`:256-280`).
  `PreparePackNameCheckoutModelAsync:710` runs nop's full item preparation with `loadItems: false` and discards it; the AJAX
  `PackNameCartItems` does it again. For a manager with 50 packnames one address change ≈ 51 rate calls + 50 panel requests.
  Same design as 4.6. Rate once per distinct (address, method, items) group; return only the changed panels; skip item
  preparation when not needed.

### ~~G-08. Re-order and cart merge ignore whether the customer still manages the packname — Nice-to-have~~ — **closed by phase 4a Part 1, 2026-09-30**
- `OrderProcessingOverrideService.ReOrderAsync:1270-1309` adds lines for `packNameOrderItem.PackNameId` with no
  `IsPackNameOwnedByManagerId`; `MigrateShoppingCartAsync` (C-02) merges guest lines into whichever packname was last
  persisted. Re-order only into packnames still managed (fall back to own with a warning).

### ~~G-09. Flyout *remove* silently does nothing for a cannot-order packname — Issue~~ — **closed by phase 4a Part 1, 2026-09-30**

- **Kind:** Bug. **Found 2026-09-15**, running phase 1's M1 against a closed season. Not present in the
  2026-09-09 pass because the flyout's remove button postdates it (added with the exchanges rebuild, so that
  the flyout could be removed from on every page).
- **4.9:** `PackNameCheckoutController.RemoveShoppingCartItem` (`:978-1034`) resolves the item from
  `GetPackNameShoppingCartItemsAsync` (`:982-985`), which reads `vw_CORE_PN_PackNameShoppingCartItem` — and
  that view `INNER JOIN`s the CanOrder view with `WHERE pno.CanOrder = 1` (`CWDatabaseService.cs`). When the
  packname cannot order the list is empty, `cartItem` is null, the whole `if (cartItem != null)` block
  including the `UpdateShoppingCartItemAsync(..., quantity: 0, resetCheckoutData: true)` that does the
  removal is skipped — and the method still falls through to `return Json(new { success = true, … })`
  (`:1026`). `CWFlyoutCart.remove` only shows a message on `success === false`
  (`FlyoutShoppingCart/Default.cshtml:153-156`), so it refreshes the blocks and says nothing.
- **What happens:** the customer sees the item in the flyout (the flyout's own items come from
  `GetPackNameCartAsync`, which is **not** CanOrder-filtered — C-01), clicks remove, is asked to confirm,
  and nothing happens. No error, no log line. **The cart cannot be emptied**, and because the header count
  reads nop's cart it keeps showing the items for ever.
- **Should be:** `NotFound()` or `success = false` with a readable message when the id resolves to nothing —
  and, once C-01 makes one reader authoritative, the method should resolve the item from the same reader the
  flyout drew it from, so a line the customer can *see* is a line they can *remove*.
- **Same family as G-06** (silent success in the checkout AJAX endpoints) and **K10** (success without the
  side effect). Fix it with C-01 in the same phase — it is the same disagreement between the two carts.
- **Size:** Small.

### ~~G-10. Only *add* to cart refreshes the subsidy bar and the group headers — Issue~~ — **closed by phase 4a Part 2, 2026-09-30**

- **Kind:** Bug. **Found 2026-09-15** by Eduardo, removing a line from the flyout while standing on
  `/SubsidyCatalog`. Not a phase 1 regression: the refresh has only ever been wired to add-to-cart.
- **4.9 — three surfaces show cart-dependent subsidy figures, and each subscribes to add-to-cart only:**
  1. The catalogue bar. `subsidy-catalog.js:79-88` is a document-wide `ajaxComplete` filtered by
     `settings.url.toLowerCase().indexOf('addproducttocart') === -1`. Removal posts to
     `PackNameCheckout/RemoveShoppingCartItem`, so the filter rejects it and neither `loadBar()` (`:6-8`)
     nor `loadHeaders()` (`:10-17`) runs. `subsidy-catalog-spec.md §12` records this filter as the design,
     so the omission is in the spec too, not only the code.
  2. The product-group headers. Same hook, same filter. `In Cart` / `Available` on every group header, and
     the collapsed/expanded state derived from them (`_ProductGroups.cshtml:5-7`), keep the pre-removal
     figures.
  3. The `PackNameInfo` subsidy flyout. `PackNameInfo/Default.cshtml:126-142` chains
     `AjaxCart.success_process`, which is nop's add-to-cart pipeline. `CWFlyoutCart.remove` does not go
     through `AjaxCart`, so neither the immediate `loadContent()` (panel open) nor the `stale = true` flag
     (panel closed) fires — on the next open the panel re-shows the HTML it cached *before* the removal,
     which is worse than a stale visible figure because it looks freshly loaded.
- **What happens:** the customer removes a $40 line. The cart count drops, the flyout list drops the row,
  the checkout totals drop — and the bar still shows $40 in cart and $60 available out of $100. The
  numbers only agree again on a full page load. A customer who removes a line to free up credit is told
  they still have none.
- **Also missed by the same hook:** any cart mutation that is not nop's `AjaxCart` add — the
  `_OrderSummary.cshtml:100-175` remove and quantity paths on the packname checkout page (they refresh
  items, payment methods, totals and the flyout, never the bar), and a `RemoveShoppingCartItem` issued
  while the catalogue page is open in another tab. Nop's own cart-page *Update cart* is a full form POST,
  so that one self-heals.
- **Should be:** one document-level cart-changed event that every cart mutation publishes and all three
  surfaces subscribe to, instead of three consumers each sniffing one URL. `loadBar` / `loadHeaders` are
  private to the closure (`subsidy-catalog.js:1-8`), which is why the flyout handler could not call them
  even if it wanted to — it already falls back to `typeof loadCartItems === 'function'` probes
  (`FlyoutShoppingCart/Default.cshtml:165-170`). The publisher belongs in one place: `AddToCartAsync`'s
  response path, `RemoveShoppingCartItem`, and the checkout quantity path. The existing
  `subsidyTopupChangedEvent` / `subsidyGroupTopupChangedEvent` pair (`subsidy-catalog.js:91-99`) is the
  pattern to follow, and `refreshProductGroupHeaders`'s `typeof` guard (`SubsidyBar/Default.cshtml`) shows
  the opt-in convention already in use.
- **Same family as C-01** — the same disagreement between readers, one layer up: C-01 is two definitions of
  the cart on the server, G-10 is four views of the cart in the browser. Fix with C-01 and G-09 in the same
  phase; the handler G-09 corrects is the one G-10's publisher goes into.
- **Size:** Small.

---

### ~~G-11. A packname list with no branch locks its manager out of add/edit — Data / Issue~~ — **closed by decision, 2026-09-28** (`BranchId` stays nullable; the admin save refuses a list without a branch; the rules a branchless list follows are `MultiStore_Management_Reference.md` §7d)

- **Kind:** Data question, possibly an admin validation gap. **Found 2026-09-16** while validating phase
  2a's finding 2.
- **Why it surfaced now:** `PackNameController.ResolveTargetPackNameListAsync` resolves the manager's
  store through `IPackNameService.GetBranchByPackNameIdAsync` (`PackNameService:726-735`), which
  inner-joins `PackNameList.BranchId` to `Branch.Id` and filters `!b.Deleted`. A manager whose own list
  has `BranchId = NULL` resolves to `null`, so `AddPackName` / `EditPackName` refuse with "The manager
  has not access to any list from the selected branch".
- **Not a phase 2a regression:** the path that check replaced (`GetVisibleBranchesForManagerAsync`)
  also required `BranchId.HasValue`, and so did the `LimitAssignedBranches = true` branch, which has
  always filtered `pnl.BranchId.HasValue`. Only the pre-change-6 `false` branch was permissive, and it
  was permissive about everything — that was U-01.
- **The real gap:** `PackNameList.BranchId` is `int?` (`Domain/PackNames/PackNameList.cs:21`) and no
  admin validator requires it (`Validators/PackNames` holds only `PackNameRegisterValidator`, which
  validates a packname's branch, not a list's). So branch-less lists are creatable, and every
  manager-facing path then treats them as invisible.
- **Check before phase 2a testing:**
  `SELECT COUNT(*) FROM CORE_PN_PackNameList WHERE BranchId IS NULL AND Deleted = 0;`
  If that is not 0, decide with the manager whether those lists should carry a branch (data fix) or
  whether a branch-less list is legitimate (then the manager-facing paths need a defined answer for
  them — a code fix, not in 2a).
- **Should be:** `BranchId` required at the admin edit, or `NOT NULL` with a migration, once the manager
  confirms no legitimate branch-less list exists.
- **Size:** Small (validator), or none if the data is already clean.
- **2026-09-16:** `SELECT COUNT(*) FROM CORE_PN_PackNameList WHERE BranchId IS NULL AND Deleted = 0` returned
  **0** on Eduardo's data. ~~Closed as a data question~~; the missing validator remains a nice-to-have, not a blocker.

---

**Verified as correct:** `PackNameOrderItem` fields at placement (`:1059-1074`); `OrderShipping` rows per packname with
shared-address de-duplication (better than 4.6's one copy per packname); payment-method labelling `Subsidy` / `CreditNote`
only fills a blank method; events, activity log, `CheckOrderStatusAsync` / `ProcessOrderPaidAsync` ordering; minimum
order interval in minutes (matches nop 4.9; 4.6 used seconds); anti-forgery on `PackNameCheckoutController`; returned-item
floor / removal guards; shipments per destination and the *Delivered to* column; totals component rows (every 4.6 row plus
credit notes and exchange fees).

---

## 10. PackName — order display, admin order, e-mails, PDF (O)

### ~~O-01. Admin cannot edit lines on a packname order; nop's edit / add actions are hidden, not guarded — Issue (extends `PackName_Order_Shipments.md §6`)~~ — **block half closed by phase 2b part 1 (decision 14), 2026-09-21**; the packname-aware rebuild of the admin order screens is not planned — `Subsidy_PackName_Open_Items.md`

> **Not done in phase 2a**, which had it in "part 2". Moves to **phase 2b** — it is an admin-side guard.
- 4.6 `_OrderDetails.Products.cshtml:11,74,124-144,413,694,736` kept nop's editable grid with PackName / Department /
  Cost centre columns and *Add product*. 4.9 `Areas/Admin/Views/Order/Edit.cshtml:107-109` hides the Products card when
  `isPackNameOrder`; `PackNameOrderAdminDetails/Default.cshtml` is read-only; nop's `OrderController.EditOrderItem /
  DeleteOrderItem / AddProductToOrderDetails` remain reachable by URL and would create `OrderItem` rows with no
  `PackNameOrderItem`, no subsidy and no `OrderShipping` adjustment.
- **Decision:** (a) build a packname-aware edit (keeps `PackNameOrderItem`, re-runs settlement) — Large; or (b) an admin
  filter refusing those actions on packname orders, in the `AddShipmentDestinationFilter` pattern — Small.

### ~~O-02. Per-line packname absent from the invoice PDF and order e-mails — Improvement~~ — **closed by phase 6 Part 2, 2026-10-07**
- **Closed:** `PdfOverrideService.GetOrderProductItemsAsync` appends `For: packname · department · cost centre`
  (`PackNameOrderItemHelper.FormatFor`, resource `Plugins.CW.Core.Pdf.Product.For`) to each packname line's attribute
  list, read once per document from `vw_CORE_PN_PackNameOrderItem`; the packing slip prints attribute lines through
  `CWShipmentDocument`. Decision D: PDF only — the `%Order.Product(s)%` table is unchanged. Plain orders untouched.
- `PdfOverrideService.cs:199-343` adds the destination block but `CWInvoiceDocument` lines carry no packname / department /
  cost centre; `MessageTokenProviderOverride.AddOrderTokensAsync:436-443` adds `%Order.ShippingDestinations%` only. A "For"
  column (packname · department · cost centre) on invoice, packing slip and the `%Order.Product(s)%` table, from
  `vw_CORE_PN_PackNameOrderItem`. (4.6 did not have it either.)

### ~~O-03. Order-details image width bound to `DefaultImageQuality`; factory injected into the view — Nice-to-have~~ — **closed by phase 4a Part 2, 2026-09-30**
- `PackNameOrderDetails/Default.cshtml:101-102` `width="@mediaSettings.DefaultImageQuality"` (a JPEG quality, 80) instead
  of `OrderThumbPictureSize`; `Views/Order/Details.cshtml:11,27` `@inject IPackNameOrderModelFactory` (CLAUDE.md §5.1/§6.13).

### ~~O-04. E / R badges on the admin order lists (K8) — Improvement (decision)~~ — **closed by phase 6 Part 2, 2026-10-07**
- **Closed (decision C1, 4.1 validated):** **E** exactly as 4.1 — on the order holding the exchange replacement
  (`ReturnRequestItem.OrderItemId`, `ExchangeMode == Exchange`; 4.1 `TIGAdminOrderModelFactory.cs:282–294`) — and a new
  **R** on the original order when a return on it is authorised, repaired or refunded. Both lists (decision C2). Built
  as an endpoint + widget script (`CWOrder/OrderBadges`, `OrderListBadgesViewComponent`), not an `IOrderModelFactory`
  override. Note: the item text below says 4.1 put E on the original order — 4.1's code puts it on the exchange order.
- Unchanged from the original item: 4.1 has the **E** badge on the original order
  (`TIGAdminOrderModelFactory.cs:291-293`, `ExchangeMode == Exchange`); 4.9 has neither. Batch the lookup by order-item
  ids (a `GetReturnRequestItemsForOrderItemsAsync` may need adding), decide which of the three lists get it, decide whether
  an order can show both E and R.
- **Re-checked 2026-10-01 (phase 6 opening):** `IExchangeService.GetReturnRequestItemsForOrderItemsAsync(int[])` already exists
  (`IExchangeService.cs:148`); what is missing is an admin `IOrderModelFactory` override and the list view's badge column. The
  two decisions were answered on 2026-10-01 (Fix Strategy §2, decision 14 / O-04).

---

## 11. PackName — admin and public manager UI (U)

The admin area is ported almost 1:1 from 4.6 `CompanyController` (Company → Employees tab) into `MultiStoreController`
(Branches tab → lists → packnames; PackNames tab); every 4.6 action has a counterpart except the admin-side Excel import (X-01 was ported for managers only); all
actions carry `[CheckPermission(ADMIN_ACCESS_MULTISTORE)]` except `GetListsByBranchId`; all admin POSTs are anti-forgery
protected through `BaseAdminController`; 0 locale keys are referenced but missing (1,129 used vs 839 installed keys
checked); every referenced asset has a csproj entry; activity-log parity holds for admin CRUD.

### U-01. ~~Public `EditPackName` never checks ownership — Issue~~ — **closed by phase 2a, 2026-09-17**
- `PackNameController.cs:589-683`: loads `packName` by `model.Id` (`:594`), checks "is a manager" (`:602-605`) but never
  `IsPackNameOwnedByManagerId` before `UpdatePackName` (`:653`) — the check runs only *after* the save to decide what to
  select (`:666`). With `LimitAssignedBranches = false`, `GetPackNameListsByBranchIdAsync(branch.Id)` (`:631`) takes any
  posted branch id of any store. GET (`:575-585`) relies on the factory throwing `NopException` (`:484`) → 500 not 403.
  `DeletePackName` (`:702`) does it correctly. 4.6 `Jim2CustomerController.cs:1917-2043` had the same weakness.
- **What happens:** any manager can rename / move / regender any packname by posting its id, including into another store.
- **Should be:** the `DeletePackName` guard; restrict the target branch to `GetVisibleBranchesForManagerAsync`;
  `AccessDeniedView()` / `NotFound()` instead of throwing. **Size:** Small.

### U-02. ~~`EditPackName` POST null dereference — Issue~~ — **closed by phase 2a, 2026-09-17**
- `:594-608` adds a model error for a null / deleted packname then calls `GetPackNameListByIdAsync(packName.PackNameListId)`
  at `:608` (4.6 `:1922-1936` identical). Return early.

### U-03. ~~`AllowManagersToAddPackNames` enforced only by hiding the button — Issue~~ — **closed by phase 2a, 2026-09-17**
- `PackNameSelectorViewComponent.cs:93` hides it; `AddPackName` / `EditPackName` / `DeletePackName` never read the setting
  (4.6 same). Check it server-side, as `SubsidyCatalogController` checks `AllowEditSubsidyTopup`.

### ~~U-04. One-customer-per-packname and same-store rules enforced only in JavaScript — Issue~~ — **closed by phase 2b part 1, 2026-09-21**
- `AssociateCustomerToPackNamePopup.cshtml:162-169` alerts `AlreadyHasPackName`; the POST (`MultiStoreController.cs:1356-1368`)
  only loads the customer; `SavePackName` (`:1212-1327`), `SaveBranchPackName` (`:1113-1207`), `CustomerPackNameCreatePopup`
  (`:1443-1477`) map `model.CustomerId` straight into the entity; `PackNameService.Insert/UpdatePackNameAsync` (`:816-832`)
  are plain repository calls. Nothing checks that the customer is not already linked to another active packname, that the
  customer's branch store matches the list's store, or that `DepartmentId` / `CostCenterId` / `PackNameListId` belong to
  `StoreId`. `GetPackNameByCustomerIdAsync` returns the *first* match, so a duplicate link silently picks one.
- **Should be:** server-side checks in the three save paths (controller / service, not FluentValidation). **Size:** Small–Medium.

### ~~U-05. Manager-assignment POSTs accept any packname id — Issue~~ — **closed by phase 2b part 1, 2026-09-21**
- `MultiStoreController.cs:1015-1036` (list managers) and `:2041-2062` (department managers) insert every posted id; the
  grid query (`GetPackNameManagersByStoreAsync`, `MultiStoreService.cs:642-691`) is store-scoped and requires `Manager`, the
  POST re-checks nothing → a non-manager or a store-B packname can become an owner of a store-A list, and the SP (Path 4)
  then grants it. Re-validate against the grid query.

### ~~U-06. Packname info strip lost the 4.6 warnings — Improvement~~ — **closed by phase 4a Part 2, 2026-09-30**
- 4.6 `Jim2.BE/Factories/PackNameModelFactory.cs:178-192` + `CurrentPackNameInfo/Default.cshtml:337-348` showed
  "cannot order", "No Address allocated", "No default shipping methods allocated". 4.9 `PackNameInfoModel` has no
  `Warnings`; the missing-address condition surfaces only at placement (`OrderProcessingOverrideService.cs:622-624` throws).
  Re-add the two configuration warnings; the cannot-order one is S-03.

### ~~U-07. Branch filter on the PackNames tab is ignored — Improvement~~ — **closed by phase 2c, 2026-09-28**
- `_PackNames.cshtml:76-83,144` renders and posts `SearchBranchName`; `Areas/Admin/Factories/PackNameModelFactory.cs:404`
  does not pass it to `GetPackNamesAsync`.
- **Still open after phase 2c (checked 2026-09-24).** Not a view fix: `IPackNameService.GetPackNamesAsync`
  (`:253`) has **no branch-name parameter at all**, so this needs a service signature change and a branch
  join as well as the factory call. `SubsidyModelFactory:666` already passes a branch name to the subsidy
  variant and is the shape to copy.

### ~~U-08. Admin saves trust the posted `StoreId`; no list ↔ branch ↔ store check — Improvement~~ — **closed by phase 2b part 1, 2026-09-21**
- `SavePackNameList` (`:836-838`), `SavePackName` (`:1110-1113`), `SaveBranchPackName` (`:1209-1212`),
  `CustomerPackNameCreatePopup` POST (`:1439-1443`), `PackNamesList` (`:1083-1086`) are feature-gated on the hidden
  `StoreId` from the form; `SavePackName` maps `PackNameListId` (`:1280`) without checking it is in `StoreId` / `BranchId`.
  `Feature_Toggles_Reference.md §0.3` recommends entity sources for admin actions.

### ~~U-09. Admin address popups edit any `Address` id — Improvement~~ — **closed by phase 2b part 1, 2026-09-21**
- `MultiStoreController.cs:1541-1564` `PackNameAddressPopup` POST loads `model.Address.Id` and updates without
  `GetPackNameAddressAsync(packName.Id, id)` (the delete at `:1613` checks); same shape for cost-centre (`:1786-1846`) and
  branch (`:699-759`) popups; 4.6 `:1603` same.

### ~~U-10. Default-settings POST reports success on validation failure — Improvement~~ — **closed by phase 2b part 3B, 2026-09-23**
- `MultiStoreController.cs:2330-2416` — the `if (ModelState.IsValid)` block ends at `:2407`; the event publish, cache
  reset, "updated" notification and redirect run regardless (`:2409-2415`), so an invalid model discards the edits and says
  "updated". The per-store variant (`:2222-2230`) is correct.

### ~~U-11. Customer delete leaves the packname pointing at a deleted customer — Improvement~~ — **closed by phase 2b part 1, 2026-09-21**

> **Not done in phase 2a** (its "part 2"). Moves to **phase 2b**.
- `ExtendedCustomerController.cs:758-805` → `DeleteCustomerAsync` only; no consumer nulls `CustomerId` / `Manager` or removes
  manager mappings; grids keep showing "Customer = yes".

### ~~U-12. Manager add / edit and mapping changes not activity-logged — Improvement~~ — **mostly closed by phase 2b part 3B, 2026-09-23** (mapping add / remove and the storefront add log); the storefront `EditPackName` POST and the three admin address popups still do not log — `Subsidy_PackName_Open_Items.md`
- Public `PackNameController` logs only `DeletePackName` (`:711`); admin logs list / packname CRUD but not manager mapping
  add / remove (`:1015-1050, 2041-2076`), address changes or customer association (4.6 same).

### U-13. ~~No bulk actions / export on packname grids; selector ships every packname unpaged — Improvement~~ — **closed by phase 11, 2026-10-21**

> Done: selection + bulk activate / deactivate / delete / move-to-list + export on both grids, and a paged selector with
> server search (`MultiStore_Management_Reference.md` §8h). What it left is in §17b "From phase 11".
- Both versions offer per-row Edit / Delete only; `PreparePackNamesModelForManagerAsync` returns every active packname of
  a list as JSON on every page load (`Factories/PackNameModelFactory.cs:325`; TomSelect `maxOptions: 100` hides the rest).
  Add selection + bulk activate / deactivate / delete / move-to-list + export; server-side search for the selector.

### ~~U-14. Department ACL picker lists every store's departments, unlabelled (extends K7) — Issue~~ — **closed by phase 2b (scoping, 2026-09-23) and phase 5 Part 1 (the role takes effect, 2026-10-01)**
- `AclDepartmentsViewComponent.cs:51` `GetAllDepartmentsAsync()` (no store filter) on category / manufacturer / product
  mappings; options show `dep.Name` only; saved by `EventConsumer.cs:252-269` with no store check. Group by store and
  filter to the entity's store mappings. (Meaningful only once P-03 makes department ACL work.) *(2026-10-01: P-03 closed —
  department ACL now takes effect, tested with the picker on products and categories.)*

### ~~U-15. Feature-toggle and permission gaps — Nice-to-have~~ — **closed by phase 2b, 2026-09-23** (every action gated and permissioned; verified in code 2026-10-21); the `Feature_Toggles_Reference.md` claim corrected by phase 10
- `PackNameAddresses` (`:1484-1485`), `PackNameAddressPopup` POST (`:1525-1526`), `PackNameAddressDelete` (`:1596-1597`),
  `CustomerPackNameCreatePopup` GET (`:1419-1420`) lack the `CWFeature.PackName` guard; `GetListsByBranchId` (`:952-965`)
  lacks `[CheckPermission]`. `Feature_Toggles_Reference.md:746` claims coverage it does not have.

### U-16. ~~Public `AddPackName` GET throws 500; `GetPackNameCartCount` unguarded — Nice-to-have~~ — **closed by phase 2a, 2026-09-17**

> **Partly.** The three 500s and the state-changing GET are fixed, and `AddPackName` GET now also checks list
> ownership (U-31). `GetPackNameCartCount` is still unguarded and was re-filed as **U-27** for phase 2c — it returns
> one integer, read-only, and 2a's scope was the write paths.
- `PackNameController.cs:406-416` throws `InvalidOperationException` in three places (4.6 returned `NotFound()`);
  `GetPackNameCartCount` (`:354-360`) returns any packname's count with no ownership check; `SelectPackName` is a
  state-changing GET (4.6 same).

### U-17. ~~Malformed markup and hard-coded English — Nice-to-have~~ — **closed by phase 2c, 2026-09-24**

> Fixed: the broken `class="class="` attribute in `_PackNames.cshtml`; the six bare `Back` literals across
> the detail partials; `"manager"/"managers"`, `"packName"/"packNames"` and a bare `Edit` in
> `_Branches.PackNameLists.cshtml`; eleven `.text('Save')` literals and `loadPartial`'s `Loading…` in
> `admin.store.navigation.js`; the registration-failure message in `Controllers/PackNameController.cs`.
> The remaining `Admin.StoreNav.res(key, fallback)` English strings are **by design** — the fallback is what
> renders if a resource is missing, so a missing key degrades to readable text rather than to `undefined`.
- `_PackNames.cshtml:12` broken `class` attribute; literals in `_PackNames.cshtml:325,332,348,404,415`,
  `_Branches.PackNameLists.cshtml:13,83,88,104,112`, `_PackNames.PackNameDetail.cshtml:21`, `MultiStoreController.cs:399`,
  `PackNameController.cs:408,414,416,430,480,504,592,597,605,611,638,647` (4.6 had the same literals).

### U-18. ~~Customer-edit PackName card cannot deep-link; branches inline grid has no search — Nice-to-have~~ — **closed by phase 2c, 2026-09-24**

> Fixed: the customer card now carries an "Open in Manage Store" link to
> `/Admin/MultiStore/Edit/{StoreId}#packnames/pn:{Id}`, verified to land on the right packname's editor.
> Building it exposed that `PackNameModel.StoreId` had never been populated on that card —
> `CustomerOverrideModelFactory.PreparePackNameModelAsync` took `storeId` and used it only for the settings
> model — so the first version linked to store 0. The inline grid takes its length from the model instead of
> a hard-coded 15, and has a debounced search box scoped per list
> (`#pni-<listId>_SearchPackName`, via the two-argument `FilterParameter`) so it cannot collide with the
> store-level tab's `#SearchPackName`. `Branch.Field.PackNamesCount` now titles the count chip.
- `Areas/Admin/Views/Customer/_CreateOrUpdate.PackName.cshtml` is read-only with no link although the MultiStore page
  supports `#packnames/pn:<id>`; `_Branches.PackNamesInline.cshtml:37-39` has `Length = 15` hard-coded and no filters; the
  per-branch count resource `…Branch.Field.PackNamesCount` is installed but unused.

### ~~U-25. Both maintenance POSTs load their authorisation settings from the POSTED store id — Issue (Security)~~ — **fixed by phase 2a, 2026-09-17** (§5d finding 7)
- **Found 2026-09-16** validating phase 2a, in code phase 2a itself added. **Fixed in phase 2a, §5d finding 7.**
- `PackNameController.AddPackName` POST and `EditPackName` POST did
  `LoadSettingAsync<MultiStoreSettings>(model.StoreId)` on a form field. `MultiStoreSettings` is store-scoped and
  carries this controller's gates: `AllowManagersToAddPackNames` (the ONLY gate on the add path, U-03),
  `LimitAssignedBranches` (which decides how far U-01's branch check reaches), `AllowToAddCustomerToPackName` and
  `AllowHomeDelivery`. A manager could therefore post the id of a store whose settings suited them. `AddPackName`
  POST also passed the posted id into `RegisterCustomer(..., model.StoreId, ...)`, registering the new customer
  against a foreign store.
- **Not cross-store writes:** finding 2's store check still forced the target branch into the manager's own store.
  The exposure is the gates themselves plus the mis-stored customer.
- **Both GETs were already right** — they read the store from the resolved list. Only the writing half trusted the
  client, which is the shape worth remembering: a guard added to a GET and a POST in the same change, correct in one.
- **Fix:** `ResolveManagerStoreIdAsync(manager)` resolves the store from the branch the manager's own list hangs
  from, and `model.StoreId` is overwritten with it before any read, so settings, `RegisterCustomer` and the redisplay
  cannot disagree. Rows 13–15 of phase 2a matrix 1 prove it.

### ~~U-26. `customAttributes` is assigned and never read in `EditPackName` POST — Nice-to-have~~ — **fixed by phase 2a, 2026-09-17** (§5e)
- Pre-dates phase 2a and sits outside the blocks 2a touched, so it was left alone (`CLAUDE.md` §0.8 scopes dead-code
  removal to the code a phase touches). `AddPackName` POST uses its copy for the address; the edit path never inserts
  an address, so the local and the `ParseCustomAttributesAsync` call above it are both dead. One line plus its block.
  Do it in phase 2c.

### ~~U-27. `GetPackNameCartCount` returns any packname's cart count to any signed-in customer — Nice-to-have~~ — **closed by phase 4a Part 2, 2026-09-30** (the endpoint and service method were removed)
- `PackNameController.GetPackNameCartCount(int packNameId)` has no ownership check. Read-only and low value (an
  integer), but it is the last unguarded forged-id endpoint in this controller. Narrowed from U-16, which also
  covered the 500s and the state-changing GET — both now fixed in phase 2a. Guard it with
  `IsPackNameOwnedByManagerId` (or `packNameId == own packname`) in phase 2c.

### ~~U-19. `packname-info-spec.md` — Nice-to-have~~ — **closed by phase 10, 2026-10-21** (the spec is consumed into `subsidy-system.md` §8.7)
- Implemented (`PackNameInfoViewComponent.cs:48-88`, view, flyout, zone registration) but the spec still says "ready for
  implementation" and names `TigIcon("tig-icon-…")` where the code uses `CWIcon("cw-icon-…")`. Mark it implemented or fold
  it into `MultiStore_Management_Reference.md` (CLAUDE.md §0.4).

---

## 12. Missing features (X)

### ~~X-01. Excel import of packnames (with subsidies) not ported; both UIs still offer it — Issue (Blocker if bulk onboarding is needed at go-live)~~ — **ported by phase 8 Part 2, 2026-10-19** (manager page `PackName/ImportPackNames`, synchronous with the result on the page and a downloadable error report instead of the 4.6 background job and e-mail; see §17b "From phase 8 Part 2" and `MultiStore_Management_Reference.md` §8g)
- **4.6:** `Jim2ImportManager.cs:2320-2556` — a template whose columns follow the settings (Branch / Department / Cost centre
  dropdowns; EmployeeNumber / Gender / PrintName / POEntry / Comments gated by `*Enabled`; Manager; `AddCustomer` + customer
  columns; `AddAddress` + address block; attributes), validation per row, background execution, an "Import Summary" e-mail with
  a downloadable error report (`:1864-1990`, `ImportPackNameReport.cs`); `AllowancesImportManager.cs` (1,213 lines) adds N
  subsidy column groups (`Allowance / Credit / DurationStart / DurationEnd` × `AllowAllowanceCountByExcel`, `:187-195`),
  validates them (`:283-352`), creates assignment + variation per group (`:208-281`) and sends the welcome e-mail listing
  the subsidies (`:943-957`). Admin actions `CompanyController.cs:1881-1919`; public `Jim2CustomerController.cs:2088-2145`.
- **4.9:** `MultiStoreController.cs:2420-2462` — the two admin actions **commented out** (still referencing 4.6 services);
  `_PackNames.cshtml:23-26, 435-535` still renders the *Import* button and modal posting to `MultiStore/ImportPackNames`
  and `GetPackNamesTemplate` → 404 / silent failure; `PackNameSelector/Default.cshtml:37-44` shows the Excel icon linking to
  `PackName/ImportPackNames` when `AllowToAddByExcel` — no such action. grep `ClosedXML|ImportManager|xlsx|ImportPackName`:
  no service. Twelve validation resources, the `PublicStore.ImportPackNames` activity type (missing) and two message-template
  constants (X-03) are dead. Settings `AllowToAddByExcel`, `AllowSubsidyCountByExcel` do nothing.
- **What happens:** the only way to onboard a season's staff list is one packname at a time.
- **Should be:** port on nop 4.9's `ImportManager` / `PropertyManager<T,L>` + ClosedXML pattern
  (`Libraries/Nop.Services/ExportImport/`), mapping Company → Store, CardFile → Branch, honouring `LimitAssignedBranches`,
  `AllowToAddCustomerToPackName`, `AllowHomeDelivery`, `AllowSubsidySelection` / `AllowSubsidyCountByExcel`; run on
  `IBackgroundTaskQueue`; summary through e-mail (the 4.6 Jim2 notification centre is out of scope — decide the channel).
  Until then hide both buttons and both settings.
- **Decision:** wanted at go-live? summary channel? welcome e-mail (X-03) in the same delivery?
- **Size:** Large (port) / Small (hide).

### ~~X-02. Store self-registration screen: slug saved, no page behind it — Issue (decision)~~ — **deleted by phase 8 Part 1, 2026-10-11** (decision 13: entity, both settings, validator rule, view fields and toggle, slug block, five resources; `UrlRecord` rows with `EntityName = StoreRegistration` stay as dead data)
- **4.6:** `Jim2CustomerController.cs:1134-1146, 1187, 1480-1704` `CompanyRegister` (customer + card-code role + default
  employee roles + default list if none + packname), view `Views/Customer/CompanyRegister.cshtml`, route + slug transformer,
  `CompanyRegisterValidator`.
- **4.9:** `StoreRegistration` entity (ISlugSupported); `MultiStoreController.cs:2176-2198` saves the slug and the two
  settings; `MultiStoreSettingsValidator.cs:17-45` validates the page name; grep `StoreRegistration` outside `Domain/`: only
  those two files — no controller, view, route or slug handler.
- **What happens:** enabling "Registration screen" reserves a slug that 404s; self-registration as a packname does not exist.
- **Should be:** a public controller resolving the slug (`GenericUrlRouteProvider` / `SlugRouteTransformer` pattern), creating
  customer + `CustomerBranch` + packname in the branch's default list, applying `DefaultPackNameCustomerRoleIds` (P-13) —
  or delete the three settings, the entity and the validator rule. **Size:** Large / Small.

### ~~X-03. Import welcome template and `%Subsidy.SubsidyName%` token never installed — Issue~~ — **closed by phase 7, 2026-10-08** (`Customer.Subsidy.CustomerRegistrationImport` installed with `%PackName.*%` and `%Subsidy.SubsidyName%` tokens; five dead template names and token groups removed)
- **4.6:** `AllowancesPlugin.cs:425-468` installs `Customer.Allowance.CustomerRegistrationImport` (tokens
  `%Employee.EmployeeNumber%`, `%Employee.CustomerBranch%`, `%Allowance.AllowanceName%`, `%Customer.PasswordRecoveryURL%`);
  `AllowanceWorkflowMessageService.cs:85-130` sends it; `AllowanceMessageTokenProvider.cs:136-154, 673-695` registers the token group.
- **4.9:** `CWMessageTemplateSystemNames.cs:8, 28` declare `Customer.Import.WelcomeMessage` and
  `Customer.Subsidy.CustomerRegistrationImport`; `CWTokenGroupNames.cs:41` declares `SubsidyTokens`; nothing else references
  them — not installed in `CWCorePlugin` (only `ReturnReceipt` is, `:124-150`), no token in `MessageTokenProviderOverride.GetTokenGroups`
  (`:304-345`), no sender. Needed by X-01 and by P-14 (manager-created customers get no e-mail).
- **Should be:** install the template (same shape as `ReturnReceipt`), add `%Subsidy.SubsidyName%`, `%PackName.EmployeeNumber%`,
  `%PackName.Branch%`, add a `SendCustomerRegistrationImportMessageAsync` to a CW workflow-message service. **Size:** Medium.

### ~~X-04. Admin-side Excel import of packnames — Issue (wanted, Eduardo 2026-10-19) — **closed by phase 12, 2026-10-20**~~ — **closed by phase 12, 2026-10-20**

> Done as described below. Decided in the phase (Eduardo, 2026-10-19): the two manager settings do not gate the admin
> action; the administrator is the creator in the activity log, the subsidy assignments and the variation history; the
> welcome e-mail names nobody and is unchanged; no *Manager* column. See the phase 12 paragraph in the status section.

- **4.6:** *Company › Employees* tab, *Import from Excel* button and modal (`_CreateOrUpdate.Employees.cshtml:20, 140-230`)
  posting to `CompanyController.ImportPackNames(id, file)` and `GetPackNamesTemplate(id)` (`:1879-1919`): the template
  for the whole company (`Jim2ImportManager.GeneratePackNameTemplate(company)`, no manager check), the import into any
  branch of the company (`ImportPackNamesFromXlsxAsync(file, companyId)` — `LimitAssignedBranches` applies only to a
  manager, `:2512-2556`), run in the background with the summary e-mail. The button showed whatever *Allow to add by
  Excel* said; that setting gated only the storefront icon.
- **4.9:** phase 8 Part 2 ported X-01 for managers only (`PackNameController.ImportPackNames`, behind the selector's
  Excel icon). An administrator who is not a manager gets access denied; today he can import only by impersonating a
  manager, and then only into that manager's branches. `IPackNameImportService` already takes the store and the
  importing customer as inputs, and the checks, batches, result and error report do not depend on the caller.
- **Should be:** an *Import from Excel* action in admin (Manage Store, PackNames tab — the store's packname grid) with the
  same template, checks, batched writes, result panel and error report; every active branch of the store offered; no
  manager and no `LimitAssignedBranches`; admin permission `ADMIN_ACCESS_MULTISTORE` and the PackName feature of the
  store. **Decide in the phase:** whether *Allow to add by Excel* (and *Allow managers to add packnames*) also gate the
  admin action (4.6: no); who the activity log and the welcome e-mail name as the creator.
- **Size:** Medium. **Phase 12** (`Subsidy_PackName_Fix_Strategy.md`).

---

## 13. ~~Settings that do nothing (T-01) — Issue~~ — **closed by phase 8 Part 1, 2026-10-11; the X-01 rows by phase 8 Part 2, 2026-10-19**

> Every row now has a consumer or is gone: P-10, M-02, P-13, S-15, C-03 by earlier phases; `ShippingMethod` (N-01), `UseStaffAsPackName` / `UseBranchesAsPackNameList` (P-15) and `RegistrationScreen` / `RegistrationScreenPageName` (deleted, X-02) by phase 8 Part 1; `AllowCustomersToAddAddress` was read all along (`PackNameCanAddAddress` from the checkout `AddAddress` and the factory). `Branch.DomainRegistrationFilter` (not a setting, same class) left the UI; the column stays. `AllowToAddByExcel` (the selector's Excel icon and the import page) and `AllowSubsidyCountByExcel` (the template's subsidy groups, validated 1–10 where the screens show it) by phase 8 Part 2 (X-01). `CLAUDE.md` §15.3 rewritten from `MultiStoreSettings.cs`.

Every one of these is editable on the store settings screens (`_StoreSettings.cshtml`, `Settings.cshtml`) and read
**nowhere** else; each hint promises behaviour that 4.6 enforced. For each, either implement the consumer or delete the
setting from model, views, validator and resources. Update `CLAUDE.md §15.3`, which also lists `UseBranchAddress`
(does not exist — the setting is `AllowHomeDelivery`) and omits `AllowHomeDelivery`, `DoNotPreSelectShippingAddress`,
`ShippingMethod`, `AllowSubsidyCountByExcel`, `PackNameEnable`.

| Setting (`MultiStoreSettings`) | 4.6 enforced at | 4.9 status | Item |
|---|---|---|---|
| `UseStaffAsPackName` | background generators only (auto-creation on card-file add) | drives manual Generate buttons; hint promises automation | P-15 |
| `UseBranchesAsPackNameList` | same | same | P-15 |
| `PrintNameProductAttributeId` | `ProductOverrideModelFactory.cs:458-461` (pre-fill on product page) | never read | P-10 |
| ~~`GenderFiltering`~~ | `CatalogOverrideModelFactory.cs:989`, subsidy catalogue | **deleted by phase 5 Part 2, 2026-10-01** | ~~C-03~~ |
| `AllowSubsidySelection` | manager add/edit packname with subsidies | never read | M-02 |
| `AllowToAddByExcel` | import link + actions | ~~shows a link to a 404~~ read: Excel icon + import page (phase 8 Part 2) | X-01 |
| `AllowSubsidyCountByExcel` | `AllowancesImportManager.cs:190` | ~~never read (seeded to 3)~~ read: subsidy groups in the template (phase 8 Part 2) | X-01 |
| `RegistrationScreen` / `RegistrationScreenPageName` | `CompanyRegister` page | slug saved, no page | X-02 |
| `DefaultPackNameCustomerRoleIds` | applied on register / import | never read | P-13 |
| `ShippingMethod` | per-company shipping fallback | never read (fallback moved to `BranchService`) | P-11 |
| `AllowCustomersToAddAddress` | `PackNameCanAddAddress` | read inside `PackNameCanAddAddress` (`ShoppingCartOverrideService.cs:683-686`), whose only caller found is its interface — verify | P-12 |
| `IncludeShippingOnSubsidy` | (inverse `ExcludeShippingOnAllowance`) | read; **default inverted** | S-15 |

Also changed by design and worth a line in the docs: `CompanySystemEnabled` → `PackNameEnable` (feature toggle, improved);
`ShippingMethod` fallback per company → per store in `BranchService.cs:578-596`; `AllowToAddCustomerByExcel` →
`AllowToAddCustomerToPackName` (manual form only); `AllowSelectShipAddresses` dropped (P-12); `LandingPageUponLogin`
dropped (M-04 / P-18); 4.6 install forced `SendEmployeeToJim2Disabled`, 4.9 install seeds only the four `*Enable` flags —
every other packname / subsidy setting defaults to false / 0, and `UninstallAsync` (`CWCorePlugin.cs:1578-1590`) does not
delete `MultiStoreSettings`.

---

## 14. Caching and performance (H)

### ~~H-01. Credit cache became per-request; dead invalidations; heavy views run per request — Improvement (decision) — **closed by phase 9, 2026-10-20**~~ — **closed by phase 9, 2026-10-20**
- 4.6 cached `AllowanceCreditByPackNameId` in the **static** cache for 3 minutes with ten invalidating consumers
  (`AllowanceService.cs:1643`). 4.9 `SubsidyCreditService.cs:74-128` uses `IShortTermCacheManager`, which nop 4.9 registers
  as `PerRequestCacheManager` (`Presentation/Nop.Web.Framework/Infrastructure/NopStartup.cs:98`). Consequently the nine
  `RemoveByPrefix / RemoveAsync(SubsidyCreditByPackNameId…)` calls (`Subsidies/Caching/*.cs`, `PackNameCacheEvenConsumer.cs:38`,
  `Orders/Caching/OrderCacheEventConsumer.cs:16`, `PackNameOrderItemCacheEventConsumer.cs:16`) are no-ops, and
  `OrderCacheEventConsumer` exists only for that line (it runs a prefix scan on every `Order` change for nothing).
  Correctness is fine; cost: every request that touches subsidy (catalogue, bar, cart totals, checkout, admin tab) re-runs
  `vw_CORE_SUB_SubsidyPackNameCredit` + `vw_…ProductGroupPackNameCredit` per packname — views that aggregate the whole
  ledger through a **scalar UDF in the WHERE** (`CWDatabaseService.cs:619-624`), which blocks parallel plans. A manager cart
  with 50 packnames = 100 view executions per page.
- **Decision:** (a) keep per-request semantics and delete the dead invalidation + consumer, documenting it in
  `SubsidyServicesDefaults`; or (b) return to a short static cache invalidated on `SubsidyOrderItem` / `SubsidyRestoreItem` /
  `Order` events — those consumers do not exist and would need adding. Independently: convert the two period UDFs into
  inline expressions / an inline TVF (after S-01 / S-02), and add indexes `CORE_SUB_SubsidyPackName(PackNameId, SubsidyId)`,
  `CORE_SUB_SubsidyOrderShipping(SubsidyId, PackNameId)`, `CORE_PN_PackNameOrderItem(OrderItemId)`.
- **Size:** Small (cleanup) / Medium (perf).

### ~~H-02. Smart TTL runs two uncached queries before every cached read — Improvement — **closed by phase 9, 2026-10-20**~~ — **closed by phase 9, 2026-10-20**
- `SubsidyOrderingService.cs:281-305` calls `GetSubsidiesByIdsAsync` (`SubsidyService.cs:166-171`, no cache) and
  `GetSubsidyVariationsBySubsidyPackNameIdsAsync` (`:584-589`, no cache) *before* the cache lookup in `:108, :157, :189, :255`
  — each "cached" read saves one query and spends two. `:312-327` clamps with a hard-coded `3` instead of
  `CacheConfig.ShortTermCacheTime` (4.6 used the config). The next-change calculation uses variation dates whenever a
  variation exists, ignoring `PackNamesOwnStartAndFinishDates` (SQL honours it only when on; 4.6 same). Cache the per-packname
  next-change date itself (invalidated by the same consumers) and gate variation dates on the store setting. S-13 is the
  crash in the same helper.

### ~~H-03. Cache-invalidation gaps — Issue (stale data for up to `DefaultCacheTime`, 60 min)~~ — **closed by phase 2b's cache matrix (2026-09-22) and phase 4b's `SettingCacheEventConsumer` (2026-09-30)**; one residue — `EventConsumer` republishes an `EntityUpdatedEvent<PackNameCartItem>` nobody handles — `Subsidy_PackName_Open_Items.md`

> **Partly closed by phase 2a, 2026-09-17**: the ownership set's six invalidation inputs are complete and
> documented as a table in `MultiStore_Management_Reference.md` §7.1 (U-36 and U-37 were found and fixed by building
> it). The wider consumer audit — every OTHER cached query in the plugin — was phase 2a's "part 2" and never ran; it
> moves to **phase 2b**, which owns the admin side of the same services.
- **Subsidy:** `PackNameOrderingProducts.{pn}` (`SubsidyOrderingService.cs:186-209`) depends on `ProductGroup.Active/Deleted`
  and variation dates but `ProductGroupCacheEventConsumer.cs:15-16` and `SubsidyVariationCacheEventConsumer.cs:15-19` do
  not clear its prefix → deactivating a product group leaves its products visible / allocatable for up to an hour;
  `ProductsByProductGroup.{pg}` (`SubsidyService.cs:777-787`) is never cleared when a `Product` is deleted (4.6 same);
  `MultiStoreSettingsChangedEvent` (`EventConsumer.cs:633-636`, `ClearAsync`) fires only from the two CW settings actions,
  not from nop's generic *All settings* page (document). **Phase 4b, 2026-09-30:** `SettingCacheEventConsumer` now
  clears the subsidy ordering prefixes (and `CW.feature.*`) on any `multistoresettings.*` write, whatever the path.
- **PackName (7):** `DepartmentManagerCacheEvenConsumer.cs:13-18` clears only `DepartmentsByManager` — `PackNamesByManager`
  and `PackNameOwnedByManager` stay stale, so assigning a manager to a department grants no ordering scope until expiry;
  `DepartmentCacheEvenConsumer.cs:13-19` clears only the role-id set (deleting a department leaves four keys stale);
  `CostCenterCacheEvenConsumer.cs:11-16` clears the select-items key on **delete only** (a renamed / new cost centre does
  not appear in the packname form dropdown); `PackNameCacheEvenConsumer.cs:63` clears `ActivePackNamesByListId` for the
  new list only when a packname is moved (4.6 same); `GetPackNameIdsByManagerId(managerId, listIds, deptIds)`
  (`MultiStoreService.cs:792-820`) caches under `managerId` only — a caller passing a subset gets whatever was cached first;
  `IsPackNameOwnedByManagerId` (`:839-842`) does not require `pn.Active` while `GetPackNameIdsByManagerId` (`:803`) does;
  `CustomerCacheEventConsumer` does not clear `PackNameByCustomer` on customer delete.
- **Orders:** `PackNameOrderGroupsCacheKey` (`CWOrderService.cs:118`, static, embeds `Address` rows) is never invalidated on
  `Address` update or `PackNameOrderItem` change; `EntityUpdatedEvent<ShoppingCartItem>` republishes a `PackNameCartItem`
  updated event (`EventConsumer.cs:694-710`) that nothing consumes; `PackNameCartItem` inserts have no consumer (latent —
  the keys are per request).
- **Should be:** one consumer per entity clearing every derived key; give `GetPackNameIdsByManagerId` a key that includes
  its parameters or drop them. **Size:** Small.

### ~~H-04. N+1 and unbounded `Task.WhenAll` over scoped services — Improvement — **closed by phase 9, 2026-10-20**~~ — **closed by phase 9, 2026-10-20**
- `SubsidyCreditService.cs:133-141` (`Task.WhenAll` over packnames) and `:80-83` (over product groups) — each repository
  call opens its own LinqToDB `DataContext`, so N packnames × (2 views + G groups) concurrent connections per request;
  `SubsidyAllocationService.cs:87` one uncached `GetReturnRequestItemForCartItemAsync` per cart line (a batch variant exists,
  `ExchangeService.cs:594-599`), and the pass runs again for every `GetCartSubsidyBreakdownAsync` caller (checkout
  breakdown and bar each re-allocate, `OrderTotalCalculationOverrideService.cs:597-618`); `MultiStoreService.cs:1013-1017`
  (`WhenAll3`), `Factories/PackNameModelFactory.cs:86-100`, `CWOrderService.cs:144-158`, `PackNameOrderModelFactory.cs:126,
  170-175, 183` and the admin factory `:120-121, 204-210, 235` run scoped services concurrently — nop core never does;
  `WebWorkContext` caches are not written for concurrent use. Batch (`GetProductsByIdsAsync`, `GetAddressesByIdsAsync`,
  `PackNameId IN (...)`) and await sequentially; memoise the allocation alongside the plan. **Size:** Medium.

### H-05. ~~`IsPackNameOwnedByManagerId` cache-key explosion — Improvement~~ — **closed by phase 2a, 2026-09-17**
- `MultiStoreService.cs:839-854` — two `AnyAsync` round trips per call and one cache entry per (manager, packname) pair;
  with thousands of packnames this is a key explosion. A `HashSet<int>` from `GetPackNameIdsByManagerId` would serve both.
  `GetPackNameIdsByManagerId` itself (`:801-816`) is two round trips where one union query would do.

---

## 15. ~~Code health (Q-01) — Nice-to-have~~ — **closed by phase 10, 2026-10-21**

The consolidated list was worked off by phases 1 (SQL skew), 2a / 2c (admin-view items, the commented 4.6 lines),
3 (dead `HistoryType` values and resources, the unused prefixes, `Async` suffixes, the dead variation `StoreId`, the
`ap.CreatorId` join, the percent TODOs), 4a (`PackNameCanOrderAsync` stubs), 4c (the unused `cart` parameter), 7
(dead stylesheets, dead attribute names, the exchanges-integration TODO), 8 Part 2 / 12 (the commented import block)
and 9 (the dead credit cache and `OrderCacheEventConsumer`). The stale documents named here were corrected by
phase 10 (`subsidy-system.md`, `CLAUDE.md` §13–§20, `Feature_Toggles_Reference.md`, `MultiStore_Management_Reference.md`;
`change-topup-spec.md` and `packname-info-spec.md` consumed). **What is still in the code is listed in
`Subsidy_PackName_Open_Items.md` §3** (duplicated normalisation and near-copy saves in `MultiStoreController`, the
`*CacheEvenConsumer` typo, DTOs inheriting `BaseEntity`, two unused admin `PackNameModel` fields, a commented `Order`
in `NopStartup`, admin subsidy CSS still registered from `EventConsumer`, three duplicate allocation filters, two
TODOs, the remaining `alert()`s and `@T()`-in-script sites of U-44, five literal `data-url`s, a dead stylesheet in the
csproj).

## 16. The original ten items, updated

| # | Original title | Status now |
|---|---|---|
| K1 | ~~"Go To Cart" in the flyout for packname customers~~ — **closed by phase 4a Part 2, 2026-09-30** | Fixed: in a packname context the item count links to the packname checkout and "View cart" is not shown. The four cart-count readers around it are C-01. **Nice-to-have.** |
| K2 | ~~My-account subsidy view does not load~~ | Closed with M-01, phase 7 (2026-10-08): *My Subsidies* at `customer/subsidies`. |
| K3 | ~~Same view is hard to read~~ | Closed with M-01, phase 7: one card per subsidy, history folded (`subsidy-system.md` §8.3). |
| K4 | ~~Order id not clickable in subsidy history~~ | Closed with M-01, phase 7: a row links the viewer's own orders. |
| K5 | ~~No per-line subsidy detail on the customer's order page~~ | Closed by phase 6 Part 1, 2026-10-02 — formerly folded into M-01 / C-07 (data in `CORE_SUB_SubsidyOrderItem`; one settlement summary for all readers). |
| K6 | ~~Subsidy and credit notes missing from e-mails and PDFs~~ | Closed by phase 6 Part 1, 2026-10-02 (M-03, C-06 / C-07). |
| K7 | ~~Product group and department pickers show every store's products~~ | Closed with A-01 and U-14. Evidence added and widened to the three add-popup POSTs — A-01; department ACL picker — U-14. |
| K8 | ~~E / R badges on the admin order lists~~ | Closed with O-04, 2026-10-07. |
| K9 | ~~Subsidy allocation is a list filter, never a gate~~ | Closed with C-04, phase 5 Part 2 (2026-10-01). Widened to categories / manufacturers / related / cross-sell / wishlist — C-04. The business question (forbidden vs unsubsidised) is unchanged and gates both. |
| K10 | ~~`AddToCartAsync` reports success without adding when the packname is unusable~~ — **closed by phase 4a Part 1, 2026-09-30** | Fixed: the packname add refuses with a message and writes its own line. A second silent path in the same method — the context-packname merge — is C-02. S-03 is the common cause (period end) with the same symptom. **Issue.** |

---

## 17. Business decisions needed before building

1. **Wording** of the "cannot order" notice and whether cannot-order lines already in the cart are hidden or blocked (S-03, C-01).
2. **Manager exemptions** — confirm `IncludeManagers` semantics (S-06) and that credit lifts the quantity cap (S-08).
3. **Negative topups** — is reducing a packname's credit an allowed admin operation? (S-14)
4. **Shipping subsidised by default?** (S-15) and whether shipping credit is restored on a full return (S-17).
5. **Discounts / gift cards / reward points before or after subsidy** — who pays for a discount, the customer or the employer? (C-05, C-09)
6. **Order data convention** — gross columns + settlement ledger vs net columns; who remits GST on the subsidised part; Odoo impact (C-06).
7. **Pending-payment orders** — how long an unpaid redirect-gateway order may hold credit (C-08).
8. **Unallocated products** — forbidden or merely unsubsidised (K9 / C-04); gender attribute convention (C-03).
9. **Customer "My Subsidies"** — how much bucket detail a customer sees (M-01 / K3 / K5); interim link to the catalogue?
10. **Manager subsidy selection** — keep it; may managers also remove a subsidy? (M-02)
11. **Department view for managers** — union of roles or department-only (P-03).
12. **Manager scope** — which lists a manager sees (P-06); "branch default address only" mode (P-12); cost-centre delivery (P-11).
13. **Excel import and self-registration** — wanted at go-live; summary channel (X-01, X-02).
14. **Admin order editing** for packname orders — rebuild or block (O-01); ~~E/R badge scope (O-04)~~ answered 2026-10-07 (both lists, both badges, E as 4.1 + R); coupons / gift cards for packname customers (G-01).

---

## 16c. What the 4.1 code says — answers to decisions 5, 6 and 7

**Read on 2026-09-16** from `NOP41/Nop.Plugin.TIG.Base` and `NOP41/Nop.Plugin.TIG.Subsidy`, at the
manager's instruction to check how the 4.1 subsidy version handles these. All three questions are
answered by the code, and in every case **4.1 agrees with the default that was already proposed**.

Note the shape of 4.1's model before the specifics: subsidy is **never** written into the order's own
money columns. It is recorded as a **payment tender** in a separate `OrderPayment` ledger, and the
order's totals are left exactly as nop computed them. That single design choice is what answers all
three questions.

```csharp
// SubsidyService.RegisterSubsidyPaymentHistory (4.1)
_orderPaymentRepository.Insert(new OrderPayment
{
    OrderId       = order.Id,
    PaymentAmount = orderTotal - subsidyAmount,   // what the customer still owes
    Subsidy       = subsidyAmount,
    OrderTotal    = orderTotal
});
// the only Order column touched:
if (string.IsNullOrEmpty(order.PaymentMethodSystemName))
    order.PaymentMethodSystemName = "Subsidy";
```

### Decision 5 — discount / gift card / reward points before or after subsidy?

**4.1: subsidy is computed on the UNDISCOUNTED GROSS, and none of the three ever reduces it.** The
base is reconstructed from the order's component columns rather than taken from `order.OrderTotal`:

```csharp
// both SubsidiesLogic.ApplySubsidyForOrder and CreditNotesService.ApplyCreditNotesForOrder (4.1)
var orderTotal = order.OrderSubtotalExclTax + order.OrderShippingExclTax + order.OrderTax;
```

`order.OrderTotal` is the figure that carries order discounts, gift cards and reward points; this
expression deliberately does not use it. And `OrderSubtotalExclTax` is the subtotal **before** the
subtotal discount — confirmed in 4.9's own core, `OrderProcessingService.cs:354`:
`details.OrderSubTotalExclTax = subTotalWithoutDiscountExclTax`, with the discount kept separately in
`OrderSubTotalDiscountExclTax`. The cart-side path agrees: where it cannot use the cached totals it
calls `GetShoppingCartSubTotal(..., out var subTotalWithoutDiscount, ...)` and passes **that**.

So the employer's contribution is unaffected by any discount the customer brings — which is the 4.6
default and the answer already proposed. **Confirmed, from two independent code paths.**

One consequence worth stating out loud, because it is a policy and not an accident: a customer
discount or gift card reduces **the customer's** share only. It cannot reduce the subsidy, so it
cannot return employer money to the employee.

### Decision 6 — gross columns or net? Who remits GST? Odoo impact?

**Gross columns plus a ledger. 4.1 does exactly what 4.9 proposes**, and 4.9 is therefore closer to
4.1 here than to 4.6 — 4.6 subtracted subsidy *inside* the subtotal and stored net figures
(`subsidy-system.md` §5 architecture note). `OrderPayment` is 4.1's settlement row and
`PaymentAmount` is the direct analogue of 4.9's `SettleableAmount`.

**GST: 4.1 does not split it, and does not attempt to.** `order.OrderTax` is (a) included in full in
the subsidised base, so subsidy covers tax as well as goods, and (b) never modified. The tax on the
order therefore remains the whole tax on the whole order value, whoever tendered the money — the
store remits GST on the full gross and the subsidy is simply one of the tenders that paid it. There
is no per-tender tax apportionment anywhere in the 4.1 code.

**Odoo: no impact, and this is verifiable rather than assumed.** `Nop.Plugin.Integration.Odoo`
(4.9) syncs **products and branches only** — `ProductSyncService`, `BranchSyncService`,
`OdooProductResponse`, `ProductVariantResponse`, `CORE_Branch`, `CORE_BranchAddresses`. There is no
order sync and no order-total or tax field anywhere in it: a grep for `OrderTotal`, `OrderTax` and
`OrderSubtotal` across the whole plugin returns **nothing**. 4.1 had no Odoo integration at all.
So the choice of gross-versus-net columns cannot affect Odoo today; if order export is added later
it is that project's decision what to send, and the ledger gives it both figures.

### Decision 7 — how long may an unpaid redirect-gateway order hold subsidy credit?

**4.1: indefinitely. There is no time limit, and no payment-status condition of any kind.**

Subsidy is consumed **at placement**, inside `PlaceOrder`, immediately after nop's discount- and
gift-card-usage history and before notifications — `PerformAdditionalOrderWorkflow(order)`, called
from two places in `OrderProcessingServiceOverride` (`:1271`, `:1323`). Payment status is not
consulted. And the credit is released by exactly one path: `OrderCancelledEvent` →
`SubsidiesLogic.UndoSubsidyForOrder`, which reverses the `Consume` history entries and deletes them.

The 4.1 subsidy plugin registers three consumers in total — `CustomerEventHandler`,
`ShoppingCartItemEventsHandler` and `OrderCancelledEventHandler` — so there is no voided-,
refunded- or deleted-order release, and nothing time-based. nop does not cancel pending orders on its
own, so an abandoned redirect-gateway order holds its credit until a human cancels it.

**Confirmed: C-08's answer of "no limit" matches 4.1.** What 4.1 *does* have that is worth copying is
the release-on-cancel path; 4.9's equivalent should be checked against it when phase 6 opens the
settlement work.

### One place where 4.1 disagrees with a 4.9 decision — worth surfacing

**Ordering of credit notes versus subsidy — 4.1 differs, and 4.9 is nonetheless correct.** 4.1 applies
**credit notes first** for the money and hands subsidy the remainder:

```csharp
// OrderProcessingServiceOverride.PerformAdditionalOrderWorkflow (4.1)
if (isCreditNotesInstalled)
    _creditNotesService.ApplyCreditNotesForOrder(order, out remainOrderAmount);

if (isSubsidyAllowed && !isReturnOrder)
    if (hasSubsidyBalance && remainOrderAmount > 0)
        _subsidiesLogic.ApplySubsidyForOrder(order, out _, remainOrderAmount, allowsPerEmployeeSubsidy);
```

The cart-side calculation does the same — `cartTotalAmount -= appliedCreditNotesTotal` before the
subsidy is computed.

**4.9 does the opposite and that is the settled, correct behaviour** (Eduardo, 2026-09-16). The code
says so plainly:

```csharp
// SubsidySettlementSource.cs:68    — "Priority 1 — the company's entitlement is spent before the customer's"
public int Priority => 1;
// CreditNoteSettlementSource.cs:36 — "Priority 2 — the customer's own store credit is spent only on..."
public int Priority => 2;
```

That ordering came in with the **exchanges rework** and is what decision 15 records. Nothing here is
open.

**Why 4.1 looks like it disagrees, and why the manager remembered it the other way.** 4.1 splits the
two halves of a subsidy: the **unit / quantity** allocation is applied *before* anything else, and
only the **money** goes after credit notes. So a 4.1 order did consume subsidy units first, which is
what is visible to whoever is watching the balances — hence the recollection. The money ordering was
the incidental half, and 4.9 makes both halves consistent: the employer's entitlement, units and
credit alike, is spent before the customer's own store credit.

**This section previously recorded the opposite and reopened decision 15 as "decision 16".** That was
wrong twice over: the claim "`SettlementPipeline.cs:34-63` has credit notes at priority 1" was
**quoted from this document's own architecture note**, which predates the exchanges rework, and never
checked against the file. Decision 15 stands, decision 16 is withdrawn, and the architecture note in
§5 is corrected. Recorded as lesson #50.

Also noted from the same method, and not in the 4.9 analysis anywhere: **4.1 never applies subsidy to
a return order** (`!isReturnOrder`).

---

## 16b. Found 2026-09-16 while specifying phase 2a

### P-19. ~~PayPal Commerce places its order outside the packname checkout — **Blocker**~~ — **closed by phase 2a, 2026-09-17**

> **Closed as a refusal, not as support.** `PlaceOrderAsync` refuses any placement whose cart holds packname rows,
> which is what stops PayPal Commerce — and any other plugin that places its own order — writing a subsidy-less
> order; the refusal happens before capture, so no money moves. Making a packname order payable by any method is
> **N-03 / phase 4c**, and until then no payment plugin can complete a packname purchase.

- **Kind:** Bug. **Found 2026-09-16**, tracing which payment paths return into nop's
  `CheckoutController` for phase 2a's filter.
- **4.9:** `Nop.Plugin.Payments.PayPalCommerce` places orders through its **own** public controller,
  not through `PackNameCheckoutController`. `PayPalCommercePublicController.ApproveOrder` (`:102-130`)
  and `ConfirmOrder` (`:171-181`) call `PayPalCommerceModelFactory.PrepareOrderCompletedModelAsync`
  (`:282`) → `PayPalCommerceServiceManager.PlaceOrderAsync` (`:2105`) →
  **`_orderProcessingService.PlaceOrderAsync(paymentRequest)`** (`:2175`). That is nop's placement,
  not `PlacePackNameOrderAsync`, so the resulting order has **no `PackNameOrderItem` rows, no
  per-packname `OrderShipping` destinations and no subsidy consumed** — precisely P-01's harm, by a
  third route that phase 2a's filter does not touch (the filter guards `CheckoutController` and
  `ShoppingCartController`, not a payment plugin's own controller).
- **Then it redirects to `CHECKOUT_COMPLETED`** from three places (`:129`, `:181`, `:408`), so the
  customer sees an ordinary confirmation and nothing looks wrong.
- **Reachability confirmed by Eduardo, 2026-09-16:** PayPalCommerce is intended for packname stores
  and for any purchase in them. So this is a Blocker, not a conditional one.
- **Where the buttons appear** (`Components/Public/ButtonsViewComponent.cs:56-86`, three placements,
  each behind its own setting): `DisplayButtonsOnProductDetails` → product pages;
  `DisplayButtonsOnShoppingCart` → the cart page, but only when the route is `CART`, which P-01's
  filter already takes away from a packname customer — so that one placement is closed as a side
  effect; `DisplayButtonsOnPaymentMethod` → inside a checkout, which is the dangerous one.
- **One chokepoint closes all of it.** `NopStartup.cs:69` registers
  `IOrderProcessingService → OrderProcessingOverrideService` for the whole application, and
  `PayPalCommerceServiceManager.PlaceOrderAsync:2175` calls `_orderProcessingService.PlaceOrderAsync`
  — so **PayPal's placement already runs through CW.Core's own override**. Every third-party
  placement path does. `OrderProcessingOverrideService.PlaceOrderAsync` therefore does not need a
  belt *and* a button guard: it is the single place where an order with packname cart items but no
  packname cart can be caught, whatever route asked for it.
- **Should be:** in phase 2a, refuse there — fail safe, no invisible orders. Making PayPal actually
  *work* for a packname order is **N-03**, which is what Eduardo asked for and is a different piece
  of work.
- **Size:** Small (the guard). Suppressing buttons is NOT needed and would be the wrong fix, since
  the goal is for the payment method to work rather than disappear.

### N-03. The packname checkout supports no payment method that places its own order — **Blocker** — **applied by phase 4c, 2026-10-01; PayPal tests pending**

> **Applied, not closed.** `PlaceOrderAsync` now places a packname cart as a packname order, and the packname checkout
> hands over to a method that takes the payment in its own pages instead of placing an unpaid order
> (the phase document is consumed; the pending tests T4–T8 and T20 are kept verbatim in `PackName_Order_Shipments.md` §9). Everything but the PayPal round trip is tested; T5, T6 and T8 need a
> PayPal sandbox. **Until they pass, PayPal Commerce stays disconnected on packname stores.**


- **Kind:** Missing, with a live Bug inside it. **Found 2026-09-16** while answering P-19.
  **Requirement stated by Eduardo the same day:** the packname checkout must work with any payment
  method, as nop's standard checkout does.
- **The mechanism.** A payment plugin of the PayPalCommerce kind does not take money through nop's
  placement pipeline at all: `PayPalCommercePaymentMethod.ProcessPaymentAsync:71-74` returns an
  empty `ProcessPaymentResult` and `PostProcessPaymentAsync:81-84` returns
  `Task.CompletedTask`. All the payment work — approval, liability shift, capture — happens in the
  plugin's own buttons and controller, which then calls `PlaceOrderAsync` itself
  (`PayPalCommercePublicController:129, :181, :408`).
- **So there are exactly two outcomes today, and both are wrong:**
  1. The customer uses the PayPal buttons → the plugin places the order → paid, but with **no
     `PackNameOrderItem`, no destinations, no subsidy** (P-19).
  2. The customer selects PayPal and presses the packname checkout's own *Confirm* →
     `PlacePackNameOrderAsync` runs and calls `PostProcessPaymentAsync`, which does nothing → the
     packname data is correct and the order is **placed UNPAID**, as pending, with no payment taken
     and nothing to tell anyone.
- **Outcome 2 is the more dangerous of the two** and is not mentioned anywhere in the earlier
  analysis: an unpaid order that looks placed. It is reachable with `DisplayButtonsOnPaymentMethod`
  off, which is the configuration a store would choose to avoid outcome 1.
- **Should be:** `OrderProcessingOverrideService.PlaceOrderAsync` becomes packname-aware rather than
  refusing — when the customer has packname cart items it resolves the packname cart and delegates
  to `PlacePackNameOrderAsync` (`:1703`), so *any* plugin that places its own order gets a correct
  packname order for free, and no plugin needs modifying. That inverts today's structure, where
  `PlaceOrderAsync` settles with a hard-coded `packNameCart: []` (`:1465`).
- **It depends on C-01.** "Resolve the packname cart" is precisely the reader C-01 has to make
  single. Doing this before C-01 means choosing that reader in a method that handles money, which is
  the one place to avoid guessing. **Sequenced immediately after phase 4a**, with 2a's refusal
  standing in the meantime so neither wrong outcome can happen.
- **Also needs deciding:** whether the packname checkout renders the plugin's payment-info component
  (so the buttons appear in *its* payment step rather than nop's), and what happens to a
  multi-packname cart paid in one PayPal transaction — one payment, several packnames, several
  destinations. The settlement already handles that shape; the payment does not know about it.
- **Size:** Medium.

### ~~N-02. No packname order-completed page — Improvement~~ — **closed by phase 7, 2026-10-08** (`packname/checkout/completed/{orderId}`; nop's completed page re-routes there for a packname order; `DisableOrderCompletedPage` honoured)

- **Kind:** Missing. **Raised by Eduardo 2026-09-16** while reviewing phase 2a's filter.
- **4.9:** `PackNameCheckoutController` finishes with `RedirectToRoute("CheckoutCompleted", …)` in two
  places, landing on nop's generic `CheckoutController.Completed` and
  `Nop.Web/Views/Checkout/Completed.cshtml` — an order number and a thank-you, with nothing
  packname-specific.
- **Why it is worth having:** a packname order is the one order shape where "what did I just buy, and
  for whom" is not obvious. The page could show the packnames in the order, each one's destination,
  and the subsidy applied per packname — the same presentation problem as M-01's *My Subsidies*
  redesign, and the same data.
- **It also removes phase 2a's one wart.** The access filter has to allow `CheckoutController.Completed`
  purely because the packname flow redirects to it. With a page of our own, that allow-list entry
  becomes a **re-route** — the filter sends `checkout/completed?orderId=X` to the packname completed
  page when X is a packname order — and the exception disappears as a concept.
- **Do NOT expect owning the view to remove the allow-list on its own.** PayPalCommerce redirects to
  `CHECKOUT_COMPLETED` from three places of its own (P-19), so that route stays live for packname
  customers whatever the packname checkout does. Only the re-route form closes it.
- **Costs to note:** the `checkout_completed_top` / `checkout_completed_bottom` widget zones do not
  fire on a custom view (check whether any plugin subscribes before switching), and
  `OrderSettings.DisableOrderCompletedPage` has to be honoured or deliberately ignored.
- **Goes to phase 7** with M-01, which shares the presentation work. **Size:** Small–Medium.

---

## 17b. Carried forward from closed phases

Each line is something a closed phase deliberately did NOT do. Recorded here because the phase
documents are consumed on closure (`CLAUDE.md` §0.4) and these are the only surviving record.

### From phase 1 — period arithmetic, closed 2026-09-16

| # | Item | Goes to |
|---|---|---|
| 1 | **The topup window uses the SUBSIDY's end, not the variation's**, in both the C# window and the SQL join. S-11's remaining half | ~~Phase 3~~ **Done by phase 3, 2026-09-29** — both halves follow the variation's end when the store honours variation dates |
| 2 | **C# reads the instance store zone; SQL reads the per-store setting.** Identical for a single-zone estate and for any manager working inside one store; reachable only by a cross-store admin action | Revisit only if a store is configured in a different zone from the instance default |
| 3 | **NULL `DurationStart` / `DurationEnd` mean opposite things in C# and SQL.** Unreachable today — `SubsidyValidator` requires both | ~~Phase 3~~ **Done by phase 3, 2026-09-29** — the columns are `NOT NULL`; the SQL functions' now-dead NULL branches are listed under phase 3 below |
| 4 | **Variation history joins `ap.CreatorId` where it should join `svarh.CreatorId`**, so the history shows who *assigned* the subsidy rather than who *varied* it. Inherited from 4.6, display-only | ~~Phase 3~~ **Done by phase 3, 2026-09-29** |
| 5 | **The credit views still call scalar wrappers in the `WHERE`.** The `subsidy_current_period` iTVF they should `CROSS APPLY` now exists | ~~Phase 9~~ **Done by phase 9, 2026-10-20** — the five views `CROSS APPLY` the iTVFs; the scalar functions are dropped |
| 6 | **A non-annual subsidy buckets every topup into its single period**, including one created after the period ended. Arguably correct; now documented in `subsidy-system.md` §3 | Closed as documented |
| 7 | **`DefaultStoreTimeZoneId` is load-bearing for money.** It decides which season an order falls in and when a topup window closes. The dev database has *SA Pacific Standard Time* at StoreId 0 — a development value. **Whoever deploys must set the real store zone**, per store if the estate spans zones | **Deployment checklist.** `Subsidy_P1_Precheck.sql` checks 5 and 6 verify it on any environment |
| 8 | **`SubsidyTopup.CreatedOnUtc` and `ProductGroupTopup.CreatedOnUtc` are `DateTime` on the entity and `datetimeoffset` in the database** (and so is every other subsidy `CreatedOnUtc`). Every comparison coerces per parameter. Exact now that the bounds carry `DateTimeKind.Utc`, but the types should agree. Two `AsDateTimeOffset()` columns written from one `DateTime.UtcNow` in one method were observed storing different offsets (`+02:00` and `+00:00`) — same instant, unexplained | ~~Phase 9~~ **Done by phase 9, 2026-10-20** — the 15 columns are `datetime2` UTC (migration `CWTimestampsDateTime2`, each value moved to `+00:00` first) |

**Test assets kept** (they are re-runnable and phase 1's only executable record):
`CWPlans/TestData/Subsidy_P1_Precheck.sql`, `Subsidy_P1_PeriodRegression.sql` (55 assertions, Parts
A–F) and `Subsidy_P1_ManualTestData.sql` (blocks 0–10, with block 9 restoring and block 10 an
observation query; the fixture moves a subsidy's or order's dates around existing consumption rather than inserting
orders, so every path under test is the real one, and packname 13 was chosen because it has no live variation — a
variation makes the packname ignore the subsidy's dates and the test passes for the wrong reason). Unit tests:
`Tests/CW.Tests/Core/Helpers/SubsidyPeriodHelperTests.cs`. Re-run the regression script after any change to the
period functions or the credit views. An offset bug cannot be reproduced on a UTC host: test with the web server and
the store in different zones.

### From phase 3 — admin subsidy screens, closed 2026-09-29

**What it shipped.** Subsidy dates `NOT NULL`; one assignment-driven reporting query for the admin screens
(out-of-period seasons listed, deleted variations never shown, effective credit and dates applied per the
store's variation settings); a usage query feeding Total / Used / Remaining and one percentage rule on the
panel and both grids (credit, else units); the top-up window following a variation's end; admin and
storefront top-ups refused with a message outside the window; the variation validator; the history SP's
offset, creator and dates-only arm; the whole Subsidy admin section in the 2d JavaScript shape; the product
group colour on its preview; the customer-page card hidden without the MultiStore permission; and a store
deletion that soft-deletes the store's packname lists, packnames, branches, departments, cost centres,
subsidies and product groups. Record: project `claude/Subsidy_Phase3_Closure_Record.md` (changes 4.1–4.35, 24/24
checks).

| # | Item | Goes to |
|---|---|---|
| 1 | **The ordering-state cache lifetime ignores `PackNamesOwnStartAndFinishDates`** (`SubsidyOrderingService.GetSubsidyNextOrderingStatusChange` takes variation dates whatever the setting; the view it caches does not) | ~~Phase 4b~~ **Done by phase 4b, 2026-09-30** — effective dates per the store setting, measured from the store's day (`SubsidyPeriodHelper.EffectiveDurationStart`, `NextOrderingStatusChange`) |
| 2 | **Nothing can remove a variation.** `SubsidyService.DeleteSubsidyVariationAsync` has no callers; the popup can only overwrite | **Done by phase 8 Part 2, 2026-10-19:** the method deleted (variations are edited, never removed, on every screen) |
| 3 | **An out-of-period assignment reads 0 % on the panel and "-" on the grids** (the panel measures against the allocation, the grids count in-period assignments only) | ~~Phase 7~~ **Done by phase 7, 2026-10-08** — both print "—" with the tooltip "Not in its ordering period" (decision J) |
| 4 | **A product group's own credit pool is not in the panel's money rows** (it shows only in the per-group grid) | No action unless asked — as A-06 specified |
| 5 | **The SQL functions keep NULL branches the `NOT NULL` columns made unreachable** (`CreateFunctionSubsidyCurrentPeriodSql`, `CreateFunctionSubsidyTopupEffectivePeriodEndSql`), and the regression script still tests them | ~~Phase 9~~ **Done by phase 9, 2026-10-20** — branches removed; `Subsidy_P1_PeriodRegression.sql` replaced |
| 6 | **`Views/Exchanges/_ReplacementProductAttrs.cshtml` starts with two byte-order marks** — Razor fails with RZ2005 when the view renders. Pre-existing, found by the phase's BOM scan | **Next exchanges change round** — delete the invisible character on line 1 |
| 7 | **U-44 remainder** (exchanges / returns views and stragglers) | **Exchanges phase**; the remainder is listed in `Subsidy_PackName_Open_Items.md` §3 (75 sites at 2026-10-21) |
| 8 | **Six `plugins.tig.*` resource rows** from an old plugin name are still in `LocaleStringResource` | **Phase 8 Part 2, 2026-10-19: no action** — dead data in a non-production database, as the `TIG.PackName.ShippingAssignments` rows |
| 9 | **Credit notes, return cases, exchange/return rules and impersonation tokens keep their store id after a store is deleted** — no soft-delete column | By decision: left untouched (history and inert configuration) |
| 10 | **Existing data already overspent** (Arthur Holmes × PHASE5-TEST: $100 used of $50) | **Done:** phases 4a / 4b enforce the caps (4b: a line already over the cap can be brought down, never raised); the screens report it correctly |
| 11 | **Pre-season subsidies are not listed on the storefront** | By decision (2026-09-29): correct — and what `subsidy-catalog-spec.md` already says: its zero-credit entry is only for `OrderOutsidePeriod` subsidies |

**Test asset kept:** the phase 3 test tracker (a Claude artifact) — 24 checks with the fixtures and figures
each used. The regression script is unchanged and still passes (parts A–F).

### From phase 2a — access hardening (storefront), closed 2026-09-17

**What it shipped.** One authority for manager scope; the store derived from the manager, never from
the request; the ordering context revalidated on every resolution; the four maintenance actions gated
and ownership-checked; anti-forgery across the whole public controller; and the three doors to placing
a packname order outside the packname checkout closed. The surviving rules live in
`MultiStore_Management_Reference.md` §7.1 and §7b, `subsidy-system.md` §8.1 and rules 20–21,
`PackName_Order_Shipments.md` §9, `subsidy-system.md` §8.1 (the top-up guard) and `Feature_Toggles_Reference.md` §3.2.

| # | Item | Goes to |
|---|---|---|
| 1 | **A refused placement is an ugly error, not a handled one.** nop's checkout redisplays confirm with the message; PayPal's controller throws it as a `NopException` and shows its own error page | ~~Phase 4c~~ **Done by phase 4c** — the blanket refusal is gone; the three refusals left (mixed cart, nothing orderable, amount differs) still show on PayPal's own error page, by design |
| 2 | **An unauthorised `SelectPackName` id silently selects the caller's own packname** rather than refusing — it keeps a stale bookmark working instead of dead-ending a manager | Decision, recorded; revisit only if it ever masks a real problem |
| 3 | **If a `PaymentMethodType.Redirection` payment plugin is ever installed**, `PackNameCheckoutController`'s inline post-processing needs review — nop's one-page checkout defers that to `OpcCompleteRedirectionPayment`, which the packname flow never reaches. No plugin in this solution is that type today | Revisit when such a plugin is added (noted in `PackName_Order_Shipments.md` §9) |
| 4 | **Whether the packname checkout should render a payment plugin's own buttons**, and what a multi-packname cart paid in one transaction means | ~~Phase 4c~~ **Decided 2026-10-01:** methods listed in `OwnPlacementPaymentMethods` get a note and a hand-over to their own page; one payment for the whole cart, one order with a destination per packname |
| 5 | **A mixed cart still has two definitions of "the cart"**, and `PlaceOrderAsync` produces no `PackNameOrderItem` for one — it now places nothing at all instead | **Phase 4a** (C-01) |
| 6 | **"Part 2" never ran.** H-03's wider consumer audit, O-01's admin filter, G-02's validator and U-11's customer-delete consumer were scoped into this phase and are re-homed to **phase 2b** | **Phase 2b** |
| 7 | ~~**`PackNameList.Active = 0` silently revokes ownership** of every packname in the list, including the manager's own~~ **P-20 closed 2026-09-21 (phase 2b part 1, decision 1):** hide from ordering, do not revoke administration. `@RequireActiveList` on the SP; ordering passes 1, ownership passes 0 | ~~phase 2c~~ Done |
| 8 | **G-13 (hide empty lists and departments in the selector) is decided but not applied** — hide them all, whole method ready in this document | **Phase 4a**, which owns the selector view |

**Test asset kept:** the run sheet — 61 checks in run order (pre-flight, forged ids, a manager-UI
regression pass, nop's cart and checkout, payment plugins and recurring payments, cache invalidation,
selector growth) with the SQL assertions beside each. It lives as a Claude artifact rather than in the
repository. The matrices it was built from went with the phase document, so **re-derive rather than
recall** if this area is reopened: the three enumerations that made them complete are the route table
in `PackName_Order_Shipments.md` §9, the request-value rules in
`MultiStore_Management_Reference.md` §7b, and the six-entity cache table in §7.1.

---

### From phase 4a Part 1 — one cart, one gate, closed 2026-09-30

**What it shipped.** One definition of the cart: `GetShoppingCartAsync` leaves out the lines the packname
cart view leaves out (closed period, deleted or **inactive** packname, list without a live branch), so the
header, flyout, selector badges, checkout and nop's own validation agree. The closed-period block in the
catalogue, add-to-cart, selector and info strip. The packname add writes its own row and link (no merge into
another packname's line, no timestamp guess), refuses with a readable message, counts the line limit per
packname (checkout too) and adds required products to the same packname. Packname delete removes its cart
lines and shipping assignment. Re-order skips packnames the customer no longer orders for, and is plain nop
with packnames off. Checkout endpoints answer a missing line or refused address as a refusal and log
`ConfirmOrder` failures. Subsidy SQL reads the feature switch (S-31). 21 of 22 checks passed; T16 blocked.

| # | Item | Goes to |
|---|---|---|
| 1 | **T16 not run:** the exchange exemption from the closed-period and branch checks (`ExchangeReplacementScope.IsActive`) had no return case to approve | **Next exchanges round** — approve an exchange on an order of a closed packname |
| 2 | **An exchange replacement in a closed packname cannot be paid through the packname checkout** (the cart view hides it; `PlaceReturnOrderAsync` exchanges are unaffected) | **Next exchanges round** |
| 3 | **"Only allocated products" with subsidies switched off was not exercised** — no active packname has such a subsidy | ~~Phase 4b~~ **Done by phase 4b, 2026-09-30** (T10) — it first failed: the cached results outlived the switch until 4b.9 cleared them on the setting write |
| 4 | **With the context on a closed packname the selector shows nothing selected** (the view's `onItemAdd` removes a blocked item) | **Phase 4a Part 2**, which owns the selector |
| 5 | **The per-destination shipping rate shown at checkout and the total disagree when two packnames share an address** | ~~Phase 7 / N-01~~ ~~Phase 8 with N-01~~ **Done by phase 8 Part 1, 2026-10-11** — each box keeps its own rate; a shared destination gets one line naming the other packnames and the charge the total makes once |
| 6 | **Unmapped lines of a packname customer** are counted by nop and never shown at the packname checkout. Edge, pre-existing | No action unless seen |
| 7 | **`TIG.PackName.ShippingAssignments` customer attribute rows** from the old plugin name are still stored beside `CW.PackName.ShippingAssignments` | **No action** (phase 8 decision 5: dead data, not in production) |

### From phase 4a Part 2 — what the customer sees, closed 2026-09-30

**What it shipped.** The list dropdown offers only entries that load a packname (asked of the roster's own
readers). A blocked packname in context stays shown in the selector with its reason. The info strip warns
when a packname has no shipping address the checkout could use. One cart-changed event
(`Content/Common/cw-cart-events.js`) raised by every cart change — AjaxCart adds, flyout remove, checkout
remove and quantity change, bulk add — and followed by the subsidy bar, group headers, subsidy panel and
selector badges. The flyout links to the packname checkout and hides "View cart" in a packname context.
Free shipping, additional charges and shipping discounts per destination; uncomputable shipping is `null`,
not $0. Order images at `OrderThumbPictureSize`; the packname order details travel on `CWOrderDetailsModel`
(no factory in the view). The dead `GetPackNameCartCount` endpoint and service method were removed. 17 of
17 checks passed.

| # | Item | Goes to |
|---|---|---|
| 1 | **With packnames switched off, nop merges a new line into an existing packname line** of the same product (seen in T27b: pn 14's HTC went 3 → 4). Standard nop merge; the line keeps its packname link for when packnames come back on | **Phase 8 Part 2, 2026-10-19: no code** — the behaviour of a switched-off feature, recorded in `Feature_Toggles_Reference.md` §5 |
| 2 | **`PackName/SelectPackName` accepts a closed packname** when posted directly. The selector disables it and the strip explains it; add-to-cart refuses | No action unless a reason appears |
| 3 | **Not exercised with data:** a closed packname with no address (the view shows only the closed notice; checked in code), and the selector's "first offered list" fallback (the own-packname fallback resets the stored list first) | No action |
| 4 | **The shipment details page prepares the packname order details and does not show them** (nop builds the order model for it). Accepted in the phase | No action |
| 5 | **`cw-cart-events.js` also loads on admin pages** (the page event is shared); its guard returns without AjaxCart | No action |
| 6 | **Dev site:** the bulk-ordering ACL is Administrators only (the plugin installs Registered too); `OrderThumbPictureSize` and `DefaultImageQuality` are both 80, so the image-width fix is not visible there | Data, no action |

### From phase 4b — allocation rules at add-to-cart, closed 2026-09-30

**What it shipped.** The allocated-quantity cap as one rule (`Helpers/SubsidyQuantityCapHelper`): only the
subsidies containing the product decide; an unrestricted one or credit still available on them lifts it;
the whole packname cart counts, through the subsidy allocation run without a money budget. The add path
(never the wishlist, never an exchange replacement) and the quantity change (a raise only) use it; bulk order
and re-order go through the add. "Only allocated products" follows `IncludeManagers` in C#. The ordering-state
cache lifetime uses the effective dates and the store's day. A `multistoresettings.*` write clears the cached
subsidy ordering results, so switching subsidies takes effect at once. `GetAllocatedQuantityRestrictionAsync`
deleted. Record: project `claude/Subsidy_Phase4b_Closure_Record.md` (changes 4b.1–4b.9; 15 of 15 checks in round 2, after
round 1 failed T10).

| # | Item | Goes to |
|---|---|---|
| 1 | **The cap counts the current customer's cart only.** A manager and the employee (or two managers) carting for the same packname are each checked against the allocation on their own, and placement does not re-check the cap, so together they can go over it | **Phase 6** (money and ledger semantics) — decide whether placement enforces the cap |
| 2 | **Top-ups cannot be removed or reduced** (insert-only; a negative amount is refused). The only way back is to re-assign the subsidy, which drops every top-up of the assignment | ~~Phase 7~~ **Closed by decision 3 at phase 7 closure, 2026-10-08** (decision K): a variation is the correction; re-assigning resets the top-ups — recorded in `subsidy-system.md` |
| 3 | **"Credit spent by orders brings the cap back" was tested by unit test only** (`CreditSpent_TheCapReturns`); on the site the top-up was removed by re-assigning | No action |
| 4 | **Two code comments name phases**: `SubsidyAllocationService.cs:73` ("pre-4b skip", an exchanges phase) and `ExchangesLogic.cs:281` ("phase 4b-3") | **Next exchanges change round** |
| 5 | **`ShoppingCartOverrideService.cs` mixes CRLF and LF line endings** (pre-existing) | ~~Phase 10~~ **Closed**: the file is uniform CRLF (2026-10-21) |
| 6 | **Dev site:** test orders #136 and #137 (packnames "Test Phase 2a" and "test test", 2 × Oversized Women T-Shirt each) are Complete and reference the deleted fixture subsidy P4B-CAP (12); returns #99 and #100 are cancelled | Data — delete the orders if wanted |

### From phase 4c — any payment method places a packname order, applied 2026-10-01 (PayPal tests pending)

**What it shipped.** `PlaceOrderAsync` places a cart with packname lines as a packname order (no plugin modified),
refusing before capture a cart with lines the packname order would not contain, a cart with nothing orderable,
and a stated payment amount that differs. The packname checkout shows a note instead of the payment component of a
method listed in `MultiStoreSettings.OwnPlacementPaymentMethods` (default `Payments.PayPalCommerce`), and its
Checkout button validates, sets the billing address and the first destination on the customer, and hands over to
the method's own page. `PlacePackNameOrderAsync` lost its unused `cart` parameter. Record:
project `claude/Subsidy_Phase4c_Record.md` (4c.1–4c.7; 8 of 11 checks); the pending tests: `PackName_Order_Shipments.md` §9.

| # | Item | Goes to |
|---|---|---|
| 1 | **T5, T6, T8 not run** — the PayPal round trip (single and multi-packname order paid and captured, and the mixed-cart refusal) needs a PayPal sandbox; Eduardo cannot sign in to PayPal from abroad | **Before PayPal Commerce is connected on any packname store** — run them then |
| 2 | **T4 not set up** — a packname without a destination could not be created (the checkout always assigns a default); the shipping check runs before the hand-over in the code | No action |
| 3 | **The packname order total and the PayPal amount use the same cart only when the cart is pure packname** — the mixed-cart refusal guarantees it; a store that wants mixed carts paid by PayPal would need a decision | No action unless asked |
| 4 | **Recurring packname orders** stay unsupported (the next cycle is refused); PayPal's vault recurring flow was not looked at | **Phase 8** with the other unsupported features |
| 5 | **Dev site:** test orders #138, #139 (Steve's packnames) and #140 (John Smith, plain); fixture subsidy P4C-CREDIT (14) kept for the PayPal round | Data |

### From phase 5 Part 1 — department ACL, closed 2026-10-01

**What it shipped.** The visibility role set described under P-03; `PluginOverrideService` (role-limited plugins
follow the signed-in customer); tier prices on signed-in roles; `PackNameCatalogScope` for cart lines and the
exchange replacement picker (which now also refuses an option page for a product the packname may not see); the
department ACL kept on Create. 13 + 5 unit tests. Record: project `claude/Subsidy_Phase5_Part1_Closure_Record.md`.

| # | Item | Goes to |
|---|---|---|
| 1 | **Tax.Avalara** (installed) gates its exemption-certificate pages on `GetCustomerRoleIdsAsync`, so in the packname context they follow the packname's roles | No action unless Avalara becomes the tax provider with certificate roles |
| 2 | **`PackNameContext.GetCurrentPackNameAsync` caches only a real packname**, so for a customer without one every role lookup re-runs the selection | ~~Phase 9~~ **Done by phase 9, 2026-10-20** — the absent packname is cached for the request too, and a new packname clears the context |
| 3 | **P-13's other half** — default packname roles not assigned at registration | ~~Phase 7~~ **Done by phase 7, 2026-10-08** (with P-13) |
| 4 | **The exchange wizard accepts any posted `ReplacementProductId`**; a product the packname may not see is refused only at approval (which rolls back) | **Next exchanges round** — validate it in the returned item's window at submission |
| 5 | **`IsExchangeReplacementAsync` matches on the cart item id alone** — the shape fixed for the ACL window by R2; an admin order edit whose order item id equals a replacement's cart row skips the quantity rules | **Next exchanges round** |
| 6 | ~~**Product pages open by URL for a packname that cannot order** (lists are empty, pages are not), and the allocation gate reads the selected packname, not `PackNameCatalogScope`~~ | **Closed by phase 5 Part 2** (C-04) |
| 7 | **A category discount reaches subcategories only as far as the packname sees them** (nop builds the applied-category list through the ACL) | Accepted |
| 8 | **Admin → product → "Add new tier price" does not save on the dev site** (the popup re-renders, no message, nothing logged; editing works). No CW code found on that path | `Subsidy_PackName_Open_Items.md` §4 — investigate |
| 9 | **Dev site:** products 140–143 (P5 PA/PB/PR/PT), category 22 "P5 Category dept2 only", role P5-STEVE (30), order #141 (Steve; packnames 20 and 3; Check / Money Order; Pending) | Data — kept after Part 2 |

### From phase 5 Part 2 — the allocation gate, closed 2026-10-01

**What it shipped.** `IPackNameCatalogGate` / `PackNameCatalogGate` with the rule in `PackNameCatalogGateHelper`
(24 unit tests): what a packname may see, for the packname in `PackNameCatalogScope` or the selected one, widened to
grouped-product relatives and carried into nop's cache keys as a restriction marker in the role set.
`AclOverrideService` applies it in `AuthorizeAsync<TEntity>` and `ApplyAcl`; categories and manufacturers with no
visible product are hidden; four cache consumers clear the derived answers. `PackNameCatalogScope` carries the
exchange type (picker, options, add, saved cart line). `GenderFiltering` deleted (C-03). Record:
project `claude/Subsidy_Phase5_Part2_Closure_Record.md`.

| # | Item | Goes to |
|---|---|---|
| 1 | **The exchange replacement picker offers grouped products** (the parent, with "Choose this product") — its search does not exclude `ProductType.GroupedProduct`, for any packname; found in T9 | **Next exchanges round** — exclude grouped parents (offer their associated products) |
| 2 | **Three allocation filters are now duplicates of the narrowed `ApplyAcl`**: the `allocatedIds` WHERE in `BulkOrderService.ApplyVisibilityAsync`, and the filters in `ProductOverrideService.GetCategoryFeaturedProductsAsync` / `GetManufacturerFeaturedProductsAsync`. Harmless | `Subsidy_PackName_Open_Items.md` §3 — still present (2026-10-21); remove, or keep as a second check |
| 3 | **A packname is restricted only when every active subsidy restricts it** (`GetOnlyAllocatedProductIdsAsync`): one unrestricted subsidy opens the whole catalogue — the rule as designed, easy to miss when setting a store up | No action — the rule as designed (recorded in the spec §9.2) |
| 4 | **The visible id list goes to SQL as an `IN` list** (~2,100 parameter limit) — same limit the restricted search already had | **Phase 9: no change, 2026-10-20** — LinqToDB inlines an `int[]` `Contains` as literals, not parameters, so the 2,100-parameter limit does not apply; bounded by the products a packname is allocated |
| 5 | **Stored `multistoresettings.genderfiltering` row and its resources stay in the database** | No action (not in production) |
| 6 | **Dev site:** Part 2 fixture removed (P5B-ONLY, P5B-GROUP, PA's test related products); P4C-CREDIT back to unrestricted; bulk ordering permission back to Administrators | Data |

### From phase 6 Part 1 — money semantics and one settlement summary, closed 2026-10-02

**What it shipped.** A settleable amount for packname carts (goods at line price + shipping, incl. tax), settled by
subsidy then credit notes; customer discounts, fee, gift cards and reward points on the balance due; the subtotal discount
limited to the balance due and stored as applied (`OrderSettlementHelper.LimitDiscount`). One settlement row per order,
`CORE_PN_OrderSettlement`, written by `SettleOrderAsync` and read by the customer order page, admin Info / Payments cards,
admin packname card, e-mail, invoice PDF and profit report; `vw_CORE_SUB_OrderSubsidyTotal` and
`GetSubsidyTotalByOrderIdAsync` removed. The quantity cap re-checked before payment (replacements exempt; checked before
an own-placement hand-over). 14 unit tests (`OrderSettlementHelperTests`). Record: project `claude/Subsidy_Phase6_Part1_Closure_Record.md`
(§9: B1, B2, R1–R9; §10: results).

| # | Item | Goes to |
|---|---|---|
| 1 | **A packname customer cannot enter a gift card or coupon at all** — `PackNameCheckoutRedirectFilter` answers every POST to `/cart` with 403 and the packname checkout has no boxes (F1) | ~~Phase 7 — G-01~~ **Closed by decision 14 / G at phase 7, 2026-10-08** — out of scope for packname stores, the 403 stays |
| 2 | **The checkout panel leaves out the fee of the pre-ticked payment method on first load** (49.50 → 51.30 once the same radio is clicked); placement includes it (F2) | ~~Phase 7~~ **Done by phase 7, 2026-10-08** — nop's factory pre-selects the first method without saving it; the checkout page now saves the method it shows as selected, so the first-load totals carry its fee (change 62) |
| 3 | **A cart over the quantity cap shows no warning on the checkout page** — only Confirm refuses (F3) | ~~Phase 7~~ **Done by phase 7, 2026-10-08** — the cap warnings are shown on the checkout page on load (change 28) |
| 4 | **With subsidies off, the packname checkout lists other packnames' lines in the same cart** that are hidden with subsidies on (F5) | **Phase 8 Part 2, 2026-10-19: no code** — recorded in `Feature_Toggles_Reference.md` §5 |
| 5 | ~~**Wording across surfaces**: the e-mail calls the payable "Order Total", the customer page uses that label for the gross; two rows labelled "Discount" (subtotal and order total); "For Stock / For Shipping" bracketed on the panel, not on the order page; admin shows "(-$300.00)" (F6)~~ | **Closed by phase 6 Part 2, 2026-10-07** — one label set, deductions negative for customers and bracketed in admin (decisions N1, N2) |
| 6 | **Tax when the subtotal discount is limited** stays as nop computed it on the full discount (R8) | No action — documented, `subsidy-system.md` §7 |
| 7 | **The settleable amount is computed twice per request** (totals and applied discount); the plan is memoised, the amount is not (R9) | ~~Phase 9~~ **Done by phase 9, 2026-10-20** |
| 8 | **A cancelled order keeps its settlement row** (R6) | No action — the row records the placement |
| 9 | **A plain order paid with credit notes has no credit-note row on its PDF** — nop's `InvoiceDocument` prints plain orders | Next exchanges round |
| 10 | **The admin packname card hard-codes "Branch:", "Department:", "Cost Centre:", "Shared", "Shipping shared with:"** | `Subsidy_PackName_Open_Items.md` §3 (U-17 class; still hard-coded 2026-10-21) |
| 11 | **Phase 1 carried item 8** — subsidy `CreatedOnUtc` (and Branch, PackNameList, CustomerBranch) `DateTime` vs `datetimeoffset` | Phase 9, with H-01 (decision B) |
| 12 | **T20** — the cap checked before a PayPal Commerce hand-over (R5) is applied but untested | With 4c's PayPal tests (N-03) |
| 13 | **Orders placed before phase 6 have no settlement row** — the page shows nop's totals and no subsidy (dev #138–#141) | No action — decision 6b, not in production |
| 14 | **Dev data**: P4C-CREDIT / packname 20 top-up +$300 (cannot be reversed in admin); credit notes 52 and 53 ($15, not deletable); test orders #142–#148, returns #101–#102; customer 1045's Administrators role (to be removed by SQL) | Data |

### From phase 6 Part 2 — the For line, E / R badges and one set of labels, closed 2026-10-07

**What it shipped.** The For line on the invoice and the packing slip (O-02) and attribute lines on the slip
(`CWShipmentDocument`, G1); the subtotal and order discounts on separate invoice rows (G2); the E / R badges on the admin
order lists (O-04, K8) through `CWOrder/OrderBadges` and `OrderListBadgesViewComponent`; one label set on every surface
(F6, decisions N1 / N2), the e-mail laid out like the page and the invoice; the credit-note widget's literals replaced by
resources. Changes 51–85; 6 unit tests (`PackNameOrderItemHelperTests`); 17 of 17 tests. Record:
project `claude/Subsidy_Phase6_Part2_Closure_Record.md`.

| # | Item | Goes to |
|---|---|---|
| 1 | **The store's currency writes a negative amount in brackets** (`($300.00)`), so customer surfaces show deductions in brackets, like nop's own discounts; changes 77–78 (minus on the customer order page) show no visible difference with this currency (F7) | No action — consistent under any currency |
| 2 | **An exchange cannot be authorised once the customer's packname is closed for ordering** — the replacement cannot go into the cart; seen on return #98 (order #127, soft-deleted customer). `CleanupFailedExchangeWorkflowAsync` removed the credit note each attempt raised (checked: no `CORE_EX_CreditNote` row) (F8) | **Next exchanges round** — decide whether a closed packname should block a replacement, and say so before authorising |
| 3 | **In the narrow order-number column the badge wraps under a 3-digit number** (F9) | `Subsidy_PackName_Open_Items.md` §4 — cosmetic, only if wanted |
| 4 | **The badge script also loads on the admin *Create customer* page** — nop renders `CustomerDetailsButtons` there; no grid, so it does nothing (decision N3) | No action |
| 5 | **A packname order with no recorded shipping group falls back to nop's slip** — no attribute or For line; shipment 66 (#114, an exchange auto-placed before fix 19) refuses to print a slip at all, the guard as designed (F10) | No action |
| 6 | **E also shows on a cancelled exchange order** — as 4.1 did | No action (4.1 parity) |
| 7 | **The e-mail's `%Order.Product(s)%` table has no For line** (decision D: PDF only) | No action unless asked |
| 8 | **Dev data**: order #149 (Steve, Pending) with shipment #76 (not shipped); test vendor customer 1047 (`vendor.badges@cw.test`, Vendor 1, no password); discounts 5 and 6 (`… (P6 test)`, inactive); return #98 still Pending; customer 1045's Administrators role (remove by SQL if not done) | Data |

### From phase 7 — storefront features, closed 2026-10-08

**What it shipped.** My Subsidies (M-01, K2–K5, M-06's storefront half); subsidy checkboxes on the manager's packname
form (M-02); default roles, random password and the welcome e-mail with nop's set-password link at registration (P-13,
P-14, X-03); the print-name pre-fill (P-10); the packname order-completed page and its re-route (N-02); the cap
warnings on the checkout page (F3) and the pre-selected payment method's fee on first load (F2); the landing resolver
and `SubsidySendUserAfterLogin` (M-04 / P-18); the top-up header grid (S-30); the Store Credit page rebuilt in the same
style, its My Account item highlighted (it never was) and two dead resources removed. Changes 1–72 (63–72 found by
Eduardo the evening of the closure, items 10–14 below); 15 unit tests (`CustomerSubsidyHelperTests`,
`PackNameLandingServiceTests`); 19 of 19 checks. The phase document is consumed (`CLAUDE.md` §0.4); the decisions
live in `subsidy-system.md` §8.3–§8.5, the Manage Store traps in `MultiStore_Management_Reference.md` §8e.

| # | Item | Goes to |
|---|---|---|
| 1 | **`Views/PackNameCheckout/_OrderSummary.cshtml` still uses `alert()` twice** (U-44 class) | `Subsidy_PackName_Open_Items.md` §3 (still there 2026-10-21, with 16 more `alert()`s in CW views) |
| 2 | **`Content/Exchanges/html-subsidy-credits-page.css` is registered in the csproj and named for a page class nothing sets** — dead unless the exchanges round claims it | Next exchanges round |
| 3 | **The admin packname panel's top-up badge says the ordering period is "closed" on an assignment that has not opened yet** (`P3-NEXTMONTH`, opens 10/15) — the wording predates this phase | `Subsidy_PackName_Open_Items.md` §4 — cosmetic |
| 4 | **The subsidy catalogue header prints `$0.00` for a packname with no money credit at all** (`SubsidyCatalogModelFactory:333`); the per-group rule of M-06 does not cover the header | `Subsidy_PackName_Open_Items.md` §4 — if wanted |
| 5 | **Spend in the My Subsidies history reads `($30.00)`** — the store currency's bracket format (phase 6 Part 2 carried #1); a minus would need the formatter bypassed | No action |
| 6 | **A manager who fails the bulk-ordering gate lands on the homepage with `BulkOrderSendUserAfterLogin` on** (Steve on the dev site) — pre-existing rule of `BulkOrderEntryService.CanUseBulkOrderingAsync` | No action |
| 7 | **nop's password-recovery form signs the customer in after setting the password** — true for the welcome e-mail's link too; a manager testing the link in his own browser loses his session | No action (nop behaviour); noted for the functional guide |
| 8 | **`CLAUDE.md` §15.1 listed five customer attribute names (`StoreId`, `BranchId`, `EmployeeId`, `ForceToChangePassword`, `PinnedStores`) that exist nowhere in the code** — corrected at closure to the two real ones | Done |
| 9 | **Dev data**: packnames 28 / 29 (`P7 Employee One / Two`) with customers 1048 / 1049 (`p7.employee1/2@cw.test`; 1048's password set through the e-mail link); orders #152 (with a $1.80 fee) and #153, Pending, on packname 20; P4C-CREDIT's top-up on packname 20 raised to $350 and used up; product 29 carries the "Print" text-box attribute (mapping 205); queued e-mails 807 / 812 | Data, no action |
| 10 | **Editing a packname list with *All managers* off raised "Cannot reinitialise DataTable"** — the detail's load-time toggle refreshed the managers grid before the grid's own deferred initialisation (jQuery 3 runs ready callbacks asynchronously). Pre-existing since the Manage Store rework; the only partial that refreshes a grid from a toggle run on load | **Done** (change 63): refresh guarded by `$.fn.dataTable.isDataTable`; trap recorded in `MultiStore_Management_Reference.md` §8e and `CLAUDE.md` §9.16 |
| 11 | **A My Subsidies history row linked an order the viewer could not open** (a manager on an employee's page, an employee whose manager placed the order): nop's order page opens only the customer's own orders, so the link led to the login page | **Done** (64–66): `CanOpenOrder` set from one `GetOrdersByIdsAsync` per card; rows link only to the viewer's own orders. **Whether a manager should be able to open his employees' orders** is a feature — nop's ownership rule would need a packname-aware exception on the order page — **unowned; phase 8 or later if wanted** |
| 12 | **Links in the two My Account tables looked like text** — nop's theme sets `a { color: inherit }`; and the caption above the big figure sat on it | **Done** (67–68): page-accent colour and underline on the table links, 6px under the caption |
| 13 | **Product-group colour field: no hex visible, the wheel beside the page edge** — `.product-group-colour-value { display: flex }` squeezed the field and overflowed the 195px wheel; the field was never painted with its colour as the packname forms are | **Done** (69–70): wheel under the field, field painted by the wheel's callback |
| 14 | **"Branch details" and "+ Add list" underlined on hover** — on a flex control the underline runs under the caret and the gap; and the branch title was not a handle on the details | **Done** (71–72): the two hover rules darken instead of underlining (the only two in the plugin's admin styles); the branch title (name · code · staff) toggles the details like the disclosure |
| 15 | **The packname subsidies card on the store page stayed on "Loading…" once** for Eduardo; not reproduced by either of us afterwards (every packname of store 1 answers `PackNameSubsidies` with 200, and the click path, the API path and a full load all render). The panel's loader has no error branch (`$.get(...).done(...)` only), so any failed request leaves the message | **Open — watch for it.** When it recurs: the URL and the Network status of `/Admin/Subsidy/PackNameSubsidies`; then give the loader a `.fail` that shows the error |

### From phase 8 Part 1 — settings that do nothing, closed 2026-10-11

**What it shipped.** N-01: `IPackNameShippingMethodService` lists the CW.Freight methods of a store (CW.Core keeps no
reference to CW.Freight) for the store pickers on both settings screens and the branch pickers on both branch forms;
`KeepOfferedShippingMethodsAsync` drops any name the plugin does not offer on every save (nop's multi-select accepts typed
tags). The shared-destination line on the checkout box (`PackNameShippingModel.SharedWithPackNames` /
`SharedDestinationFee`; `PackNameShippingAssignments.SetSelectedOption` replaces `UpdateShippingOptionRate`). P-15:
`IPackNameProvisioningService` called from `MultiStoreController.SaveBranch` and `BranchController.Create` (new branch)
and `ExtendedCustomerController.Create` / `Edit` (branch link set or changed); not an event consumer (the manager's
add-packname flow inserts the branch link before its own packname). X-02 deleted. Changes 1–80 (46–59 from the apply
review, 60–80 from the run); 12 unit tests (`PackNameProvisioningServiceTests`); 30 of 30 checks (tracker
https://claude.ai/artifact/HMX8uXtAZpZPdwET5Fnpob). The phase document is consumed (`CLAUDE.md` §0.4).

| # | Item | Goes to |
|---|---|---|
| 1 | **F1 — a settings save rewrote the automation overrides.** `StoreSettings`, `GeneratePackNames` and `GeneratePackNameLists` recomputed `UseBranchesAsPackNameList_OverrideForStore` / `PackNameListNameFrom_OverrideForStore` as "posted ≠ stored" whenever the staff override was ticked; an unticked override disables its input, which is then not posted and binds false / 0. Seen: one save removed store 1's lists-switch override and wrote a name-rule override nobody chose. The Generate buttons also handed the *posted* name rule (0 with its override unticked) to the job. Pre-existing | **Done** (60–64): each override saved as posted; the generators read the rule saved for the store. Trap in `MultiStore_Management_Reference.md` §8f |
| 2 | **F2 — the standalone *Branches* page bypassed this phase.** The analysis called `BranchController` dead (the menu's `Branch/Index` was assumed missing) — wrong: `Index` redirects to `List`; the manager keeps the page (since 4.1). Its Edit erased the branch's methods (the form had no field), Create never provisioned and discarded its redirects, a refused create reset the store, Edit could re-parent across stores / to itself / to a sub-branch and wrote the hidden store id back, Delete removed a branch with children | **Done** (65–80): picker and offered-names filter, provisioning, returned redirects, parent checks (new message), the store never written on edit, Manage Store's delete rule; the two branch messages no longer say "new branch" |
| 3 | **`Branch.DomainRegistrationFilter`** had no reader once X-02 went | **Done** (66–77): out of both forms, the model and the resources; ignored by the mapping; the column stays unused (no migration) |
| 4 | **Standalone Create form: the parent list and the shipping methods are prepared for the store the form opens with**; choosing another store does not reload them (a wrong-store parent is refused, only methods offered in the chosen store are kept) | No action unless wanted — a store-change reload is new JavaScript |
| 5 | **The collapsed checkout box header keeps the old address / method name until a reload** (only the box body is reloaded after a change). Pre-existing | `Subsidy_PackName_Open_Items.md` §4 — cosmetic |
| 6 | **`BranchController.AddressEdit` loads the address by id without checking it belongs to the branch**, and **`SearchBranches` has no `[CheckPermission]`** (any admin-area user can list a store's branch names; used by the packname detail's branch picker). Read in the code, not exercised | With **P-17** (multi-store scoping holes) |
| 7 | **`IPackNameService.GetPackNameListsByBranchAsync` and `GetPackNameListsByBranchIdAsync`** are the same query with a different order | Dead / duplicate code — reported to Eduardo, unowned |
| 8 | **`CWFreightPlugin.DetermineShippingMethodRateAsync`'s comment** says banding is enforced in `GetAllCwShippingMethodsAsync`, which filters by store and country only | CW.Freight — out of this plan, reported |
| 9 | **Dead data left on purpose:** `UrlRecord` rows with `EntityName = StoreRegistration`; store-1 `multistoresettings.registrationscreen` / `registrationscreenpagename` rows; old `DomainRegistrationFilter` values | No action (not in production) |
| 10 | **A manager opening his employees' orders** (phase 7 carried #11) — raised again at phase 8's opening | **Unowned** (decision 6: not in phase 8; its own small phase if wanted) |
| 11 | **Phase 8 items outside Part 1's scope:** `DeleteSubsidyVariationAsync` (phase 3 #2), six `plugins.tig.*` rows (phase 3 #8), packnames-off line merge (4a Part 2 #1), subsidies-off foreign lines (6 Part 1 #4), `PackNameSelectorModel.CanAddByExcel` set and never read | **Done by phase 8 Part 2, 2026-10-19** (see "From phase 8 Part 2"); a manager opening his employees' orders stays unowned |
| 12 | **Dev data**: branches 27–32, lists 26–32 (28 is `TB1.3` on branch 5, made by Generate), packnames 30–32 and customers 1054–1056 (`p8.staff1/2/3@cw.test`) — all **deactivated**, never deleted; branch 30 keeps Freight Express; store 1 has no override rows for the four settings | Data, no action |

### From phase 8 Part 2 — X-01 Excel import, closed 2026-10-19

**What it shipped.** `PackNameController.ImportPackNames` (page, POST, template, error report) behind the selector's
Excel icon; `IPackNameImportService` (template, read, check, write in batches of ten — `PackNameImportDefaults`,
`PackNameImportLayout`, `PackNameImportResult`); `IPackNameRegistrationService` + `PackNameRegistration` (what the add
form did inline, now one transaction for both callers — `PackNameTransaction.Begin`, read committed); one name per
list on the storefront add / edit forms, the import and the three admin saves (`IPackNameService.PackNameNameExistsAsync`,
provisioning exempt); a switched-off list or branch takes no new packnames on any path, a branch with no active list
gets one (`EnsureDefaultPackNameListAsync`); text fields checked against their database length on every form;
`MultiStoreSettingsValidator` (Subsidy Count 1–10 where the screens show it); the index migration
`PackNameLookupIndexes` (`CustomerId`, `PackNameListId`); `DeleteSubsidyVariationAsync` and three other dead members
deleted. Changes 1–119 over seven review rounds and three test runs (trackers
https://claude.ai/artifact/HdsAMBnQeEbJFGqaBoLJ5Y, https://claude.ai/artifact/LDthj8UYNv2ULJYpHqQivG,
https://claude.ai/artifact/DBNc3bjNxuZ9rEobXErV3z — the last: 66 pass, 4 info, T58 pass). Unit tests
`PackNameImportServiceTests`, `PackNameRegistrationServiceTests`. Decisions: synchronous, all-or-nothing checks, 500
rows; writes in batches of ten (after T56 froze the store); a new account gets its own copy of the delivery address;
no VAT number asked; Company = the branch's name; switched-off lists take nothing; names checked per list only.

| # | Item | Goes to |
|---|---|---|
| 1 | **The edit forms (storefront and admin) save Employee number, Print name, PO entry and Comments even when their setting is off**, so an edit clears a value imported while the setting was on; they also save the gender they post, so with the store's gender off an edit stores none. 4.6 did the same | **Unowned** — decide whether hidden fields keep their value |
| 2 | **One name per list is checked, not enforced by the database.** Two registrations of the same name at the same instant can both pass; on *read committed snapshot* (Azure SQL's default) the import's in-transaction re-check (change 107) does not wait for another import's uncommitted rows. Only a unique index on list + name closes it, and provisioning (exempt, decision G1) may already have named two alike | **Unowned** — needs a decision on the index and on provisioning's duplicates; the deployment database is not chosen yet |
| 3 | **Generate PackNames provisions inactive customers**: `GetCustomersWithoutPackNameByBranchAsync` skips deleted customers only (seen at T27: a switched-off test customer got a packname). Pre-existing, P-15's code | **Unowned** — decide whether an inactive customer gets a packname |
| 4 | **A payment method that takes payment in its own pages (PayPal) sets the customer's shipping address to the first packname's destination** — a packname, branch or cost-centre address — and that address's admin delete then fails on nop's foreign key (`PrepareCustomerPaymentAddressesAsync`) | With phase 4c's PayPal tests (**unowned until then**) |
| 5 | **`cw-icons.svg` is cached for a year with no version in its URL**: a browser holding an older copy shows a new icon blank until it reloads the file (all CW icons share this) | Deployment checklist — **unowned** |
| 6 | **`BranchDetail` with a branch id that does not exist throws** (`BranchModelFactory.PrepareBranchModelAsync`, null branch) — 500 instead of not-found; hand-made admin URL only. Pre-existing | **Unowned** |
| 7 | **Admin and storefront word the duplicate-name and too-long messages differently** ("A packname named X already exists in this list" / "The name X already exists for the branch Y"; FluentValidation's length text vs "Name is longer than 255 characters") | **Unowned** — cosmetic |
| 8 | **Recorded, no action:** two imports into one branch with no active list at the same moment can each create a list; a template works in the language it was downloaded in; a subsidy period starting and ending on one day is refused (as the admin variation form; 4.6 took it); a store whose *Subsidy Count* is overridden to 0 while the Excel switch is inherited gets one group (4.6 read 0 as 3); an e-mail over 500 characters passes the checks but not the queued e-mail's field (nop's own registration too); a manager's roster cached during an import on *read committed snapshot* can stay stale until the next change; the error-report button returns to the page without a message when the file can no longer be read; an import into a branch outside the manager's own creates a list he may not see (the result says where the packnames went); a stopped import keeps the file chosen (importing it again refuses the rows already imported) | No action |
| 9 | **Not ported from 4.6, by decision:** ~~the admin-side import~~ (wanted after all — **X-04, phase 12**, Eduardo 2026-10-19), the `Manager` column (the add form cannot set Manager either), `ExcelBranchesFromCardCode`, `ContractItemsOnly`, the Jim2 contact sync, the notification centre and summary e-mail, `UseBranchAddress`, the background job | No action, except the admin-side import → **X-04, phase 12 (closed 2026-10-20)** |
| 10 | **Switched-off features and cart lines:** with packnames off nop merges a new line into an existing packname line (4a Part 2 #1); with subsidies off the packname checkout lists other packnames' lines (6 Part 1 #4) | **No code** — the behaviour of a switched-off feature, `Feature_Toggles_Reference.md` §5 |
| 11 | **A manager opening his employees' orders** (phase 7 #11, Part 1 decision 6) | **Unowned** |
| 12 | **Dev data:** every test packname, customer and list of the three runs (`P8B …`, `R2 …`, `R3 …`, `Q001–Q440`, `S001–S440`, the bulk files, the `p8b.*@cw.test` accounts, lists 33–39 and 47–49), cost centre 24 and subsidies 18–20 are **deactivated** by `CWPlans/TestData/Subsidy_Phase8_Part2_TestData_Deactivate.sql`; the test customer attributes (2, 3, 4, 5) were deleted with Eduardo's approval; about 3,000 welcome e-mails to `@cw.test` addresses stay queued | Data, no action |

### From phase 9 — performance and caching, closed 2026-10-20

**What it shipped.** Credit read once per request for all the request's packnames (one query per view) and memoised in
`SubsidyCreditService` — cleared by consumption and restore writes and before settlement, which reads again inside the
packname lock; the consumers that only cleared it deleted. The next ordering-status change of a packname statically
cached (`MinimumOrderingCacheMinutes` = 3), cleared by the subsidy, assignment, variation, packname and setting consumers
(store time zone included). The five subsidy views `CROSS APPLY` the inline `subsidy_current_period` and the new
`subsidy_topup_period_end`; the three scalar functions dropped (SQL Server 2016 cannot inline them); index
`IX_CORE_SUB_SubsidyPackName_PackNameId` (migration `SubsidyPackNameLookupIndex`). The 15 `CreatedOnUtc` columns of the
CW tables `datetime2` UTC (migration `CWTimestampsDateTime2`). Per-request cart-line maps (return requests, packnames)
and sequential reads instead of `Task.WhenAll`. Checkout: a change re-rates only the packname changed — the others keep
their options while their address and lines are those they were rated for (`RatedFor`) — and the page redraws in one
request; `PackNameBoxItem` deleted. CW.Freight caches its three tables. Exchange orders now settle under the packname
locks (20 tries, half a second apart); the lock chain passes an inner refusal out. 144 changes. Checkout full load
5,445 → ~930 queries (SQL 4.1 s → 1–2.9 s); a shipping change 5,422 → ~740 queries (SQL 17.6 s → 1.1–2.3 s). Tracker
https://claude.ai/artifact/4Cpn3yhVsbMkgjJJogVUuS (28 checks pass, T20b by unit test); unit tests 277 pass
(`PackNameLockChainTests` new).

| # | Item | Goes to |
|---|---|---|
| 1 | **The checkout still takes ~12 s for a manager with 45 packnames, and the time is C#, not SQL** (SQL is 1–2.9 s of it). Not profiled. Next step: nop's MiniProfiler on one full load | **Unowned** — a performance follow-up if wanted |
| 2 | **The catalogue is slower in the browser than before the phase** (3.1 s against 1.9 s), with 82 queries against 522: two credit reads of ~470 ms each dominate | **Unowned** — goes with item 1 |
| 3 | **With no billing address of record, the checkout's totals use the store's default tax address while the order bills the customer's own address** (T27, order 163: shown $25 to pay, charged $20). Only matters where tax differs by address; this store's rates are all 0 % | **Unowned** — older than the phase |
| 4 | **A customer with no address at all cannot check out**: "No billing address allocated" (seen with customer 1048). Registration through the packname form always creates one; an account made otherwise does not | **Unowned** — decide whether the checkout should offer the packname / branch address as billing |
| 5 | **In the by-product checkout view one packname's box is drawn on every product row**: repeated element ids, handlers bound once per row, so one radio change posts once per row. Older than the phase | **Unowned** |
| 6 | **A change an admin makes to a shipping method's rate is seen by a packname kept from the last rating only at the next full checkout load** — the accepted trade-off of G-07 | Recorded, by decision |
| 7 | **The exchange-order settlement settles unlocked after 20 refused tries** (logged as a warning), because the order is already placed and paid | Recorded, by decision |
| 8 | **Dev-only leftovers:** `vw_TIG_SUB_*` views from before the table rename still reference the dropped scalar functions (not in production; left by decision) | No action |
| 9 | **Dev data:** packnames `P9 IMP A 01–05`, `P9 IMP M 01–50` (Test Branch 2), 3506 / 3562 / 3563 / 3564, accounts 4498–4500 deactivated and list 1 back to its nine (`CWPlans/TestData/Subsidy_Phase9_Closure.sql`); orders 154–163 and returns #103–#105 are test orders; the NSW 10 % Apparel row stays in the inactive by-country tax table | Recorded |

### From phase 11 — packname bulk actions and selector, closed 2026-10-21

**What it shipped.** See the phase line at the top and `MultiStore_Management_Reference.md` §8h. Tracker
https://claude.ai/artifact/9zG9pBH3QnppDQ1h4CJnm7 (T0–T22 pass, measurements M1–M4); unit tests 312 pass
(`PackNameSelectorHelperTests`, `PackNameBulkHelperTests`, `PackNameBulkServiceTests` new).

| # | Item | Goes to |
|---|---|---|
| 1 | **Each selector request still spends ~60–70 ms of SQL**, most of it in two views that cost the same whatever they are asked: `vw_CORE_SUB_SubsidyPackNameCanOrder` 20–40 ms per call (36–43 ms for 50 ids, 19–28 ms for one) and `vw_CORE_PN_PackNameShoppingCartItem` ~20 ms. The page and the search answer are small now; the SQL did not fall. Next step: read both plans | **Unowned** — a performance follow-up, goes with phase 9's items 1–2 |
| 2 | **The manager mapping rows of deleted packnames stay** in `CORE_PN_PackNameListManagers` / `CORE_PN_DepartmentManagers` (no soft delete; every reader skips deleted packnames; the one count that did not is fixed) | Recorded, by decision (Eduardo, 2026-10-21) |
| 3 | **The bulk action name is matched ignoring case** (`delete` is Delete); refused are numbers and combined names | Recorded, by design |
| 4 | **The single edit form offers only the packname's branch's lists; the bulk move offers every switched-on list of the store** | Recorded — both follow the same save rules |
| 5 | **Dev data:** the selector fixture's 600 `P12 …` packnames of list 7 (ids 2503–3107) switched off again, Steve back on packname 20 *test test* / list 1, test account `p11.del1@cw.test` (4501) deactivated, XE session `CW_Phase9_Measure` stopped (`CWPlans/TestData/PackName_Phase11_Closure.sql`); packnames P11 DEL 1–3 (3565–3567) deleted by the tests | Recorded |

### From phase 10 — documentation consolidation, closed 2026-10-21

**What it did.** No application code. Every reference document re-read against `Nop.Plugin.CW.Core` and corrected
(`subsidy-system.md`, `MultiStore_Management_Reference.md`, `Feature_Toggles_Reference.md`, `CLAUDE.md` §3–§22,
`PackName_Order_Shipments.md`, `Exchanges_Returns_CreditNotes_Reference.md`, and one passage each in the Odoo sync spec,
the legacy 4.1 reference, the exchanges dev notes and the subsidy catalogue spec); what survived of the ten remaining
phase documents and of `change-topup-spec.md` and `packname-info-spec.md` folded into them, the twelve files moved to
`_to_delete/`; this document's closed items struck through and Q-01 closed with its residue listed; the open items of
every phase gathered in **`Subsidy_PackName_Open_Items.md`**, together with a port audit of 4.1 and 4.6 against 4.9
(three defects found: the two approval e-mail templates are never installed, cross-store impersonation is a stub
landing on a route that does not exist, and 4.1's return-eligibility rule — shipped-but-Processing orders, days from
the last shipment — has no 4.9 counterpart). Closure record: project `claude/Subsidy_Phase10_Closure_Record.md`.

| # | Item | Goes to |
|---|---|---|
| 1 | Everything this phase left open or unowned, including the code items earlier phases had routed to it | `Subsidy_PackName_Open_Items.md` |

## 18. Suggested sequencing

This is an ordering by dependency and risk, not an estimate.

> **Superseded by `Subsidy_PackName_Fix_Strategy.md`**, which is the live plan and orders the same
> items into eleven phases. This section is kept as the original dependency reading. Step 1 is done:
> phase 1 closed S-01, S-02, S-09, S-10, S-11, S-13 and S-20 on 2026-09-16.

1. ~~**Stop the bleeding (all Small):** S-01, S-02 (one SQL change + regression script)~~ · S-04 · S-05 · P-01 · ~~P-02~~ · P-04 ·
   G-03 · U-01 / U-02 · ~~S-09~~ · ~~S-10~~ · ~~S-13~~ · G-04 · G-05 · A-01.
2. **One cart, one gate:** C-01 (override `GetShoppingCartAsync` or one reader), then S-03 (catalogue, add-to-cart, selector,
   info strip), C-02 / K10 (packname overload owns the insert; real warnings), P-08 (context revalidation + cart cascade).
   These four touch the same two files and should be one phase with one test plan.
3. **Department visibility:** P-03 (+ U-14, H-03 department keys) — needs decision 11.
4. **Money semantics:** decisions 5–7, then C-05 / C-06 / C-07 / C-08 / M-03 (K6) as one "settlement summary" phase,
   S-07 / S-08 (quantity cap), S-11 (topup window + lock).
5. **Storefront features:** M-01 (K2–K5), M-02, P-10, ~~C-03~~, G-01, U-06.
6. **Admin / security hygiene:** U-03 … U-12, S-12, S-14, A-02 … A-05, U-15 … U-18.
7. **Missing features by decision:** X-01, X-02, X-03, P-13, P-14, P-15, T-01 clean-up.
8. **Performance and caching:** H-01 … H-05, G-07.
9. **Code health:** Q-01, doc updates (`subsidy-system.md`, `CLAUDE.md §15.3/§16`, `MultiStore_Management_Reference.md`).

---

## 18b. Found by the phase 2a whole-file review — 2026-09-16

Passes 1–3 of phase 2a's validation reviewed the diffs and kept finding what the previous diff had
introduced. This pass read every file the phase touches top to bottom, with four independent reviewers,
against three complete enumerations: every route that can place an order, every request-sourced value
in the two controllers, and every input of the ownership query mapped to the consumer that invalidates
it. Items marked **fixed in 2a** are in that phase's §5e, C1–C12; the rest carry the phase they belong
to. Numbers continue each area's existing series.

### Fixed in phase 2a §5e

- **U-28.** `PackNameController`'s two POSTs persisted `model.DepartmentId` / `model.CostCenterId`
  straight from the form. Both entities are store-scoped, and `DepartmentId` is an **ownership edge**
  (`GetOwnedPackNameIdsByManagerIdAsync` matches `pn.DepartmentId` against the manager's departments),
  so a posted foreign department id moved one store's packname into another store's manager scope.
  Validated against the manager's store (C8).
- **U-29.** `POST /PackName/PackNames` had no manager check. The factory validates the LIST, but
  `packname_get_listids_by_manager` has no `Manager = 1` predicate — path 1 returns every active list
  in the caller's own branch — so a plain employee got the whole branch roster (names, groups, ids),
  with `[]` for anything unowned making it a clean enumeration oracle. Manager gate added (C8).
- **U-30.** `SelectPackName` was a state-changing GET, therefore exempt from
  `[AutoValidateAntiforgeryToken]` (which validates unsafe methods only). An `<img src>` on any page
  the victim visited switched a manager's ordering context to a chosen employee, so the manager's next
  order spent that employee's allowance — no interaction, no token. Now `[HttpPost]`, with the four
  callers submitting a token-bearing form (C8, C9).
- **U-31.** `AddPackName` GET took the packname-list id from the route and trusted it. The model factory
  fans that list's store into every dropdown — departments, cost centres, and, when the store has
  `LimitAssignedBranches` off, its whole branch tree with breadcrumbs — so iterating ids harvested
  another store's org structure; the returned model then carried that store's `StoreId` into the POST.
  The list must now be one the manager owns, and settings come from the manager's store (C8).
- **U-32.** `EditPackName` POST dereferenced `GetCustomerBranchByIdAsync`, which returns null when a
  packname's customer has no `CustomerBranch` row (admin-created customers, imports) — **after**
  `UpdatePackName` had committed, so the 500 left the packname moved and the customer's branch not.
  Insert-or-update (C8).
- **U-33.** `DeletePackName` never read `AllowManagersToAddPackNames`, the gate the other three
  maintenance actions apply — so with manager maintenance switched off a manager could still
  permanently delete an employee's packname and its allowance history (C8).
- **U-34.** Recurring payments bypassed both P-01 guards. `ProcessNextRecurringPaymentAsync` writes the
  order with `SaveOrderDetailsAsync` and clones the initial order's items — it never calls
  `PlaceOrderAsync` — and it lives on `OrderController`, which the checkout filter does not touch.
  Customer-reachable (`POST /customer/recurring-payments`, `retryLastPayment_{id}`), and real for
  packnames because `PlacePackNameOrderAsync` creates recurring payments itself. Overridden to refuse
  until phase 4c (C2b).
- **U-35.** The checkout filter is gated on `CWFeature.PackName`; the `PlaceOrderAsync` guard was not.
  With the feature switched off while a customer still held `PackNameCartItem` rows, nop's checkout
  opened, the guard still refused, and `/packname-checkout` redirected **to** nop's checkout — a loop
  with no exit but emptying the cart. Guard now gated the same way (C2c).
- **U-36.** Two of the six entities feeding the ownership query had no consumer clearing
  `PackNameOwnedIdsByManagerCacheKey`: `DepartmentManagerMapping` (cleared only the department list) and
  `Department` (cleared only a customer-role key). A manager removed from a department kept edit and
  **topup** rights over its packnames for up to `DefaultCacheTime`; a deleted department kept granting
  them. Both consumers corrected (C3, C4).
- **U-37.** `PackNameCacheEvenConsumer` cleared `ActivePackNamesByListIdCacheKey` with the **new** list
  id, so a moved packname stayed on the old list's storefront roster, and cleared the department roster
  with a prefix formatted with a real list id while that query caches under list id 0 — so it was
  invalidated by nothing. Both are prefix clears now, using prefixes that already existed unused
  (C5, C6, C7).
- Also fixed there: **U-26** (dead `IFormCollection form` and `customAttributes` in `EditPackName`
  POST), the unmapped `pnci.Id` projection in the placement guard (`PackNameCartItem` has no `Id`
  column — `ShoppingCartItemId` is its primary key), nop's cart being reachable by **POST** (eight
  methods answer to `ActionName("Cart")`; `FormValueRequired` is an action constraint), and
  `GetPackNameIdsByManagerId`'s two parameters that changed the query but not the cache key.

- **U-38.** The stored ordering context was never revalidated — only its STORE was checked. `PackNameContext`
  restored `SelectedPackNameId` if the packname's list belonged to the current store
  (`PackNameContext.cs:148-152`), with no test of `Deleted` and, crucially, none of whether the customer may still
  use it. **Found 2026-09-17** by Eduardo: impersonate a manager, select a packname from a department, stop
  impersonating, remove the manager from that department's managers, impersonate again — the packname is still the
  context and the manager can still act for it. The guards phase 2a added all re-check the SUBJECT they are given,
  so a top-up is refused; but everything that takes the context as the actor's own packname — the catalogue, the
  subsidy bar, add-to-cart's `packNameId`, the packname checkout — keeps working against a packname the manager no
  longer manages. Deleting the packname, deactivating its list, moving its branch to another store and revoking a
  list ownership all leave the same stale context. Fixed in phase 2a §5f (C13): the restored selection must be
  non-deleted, in the current store, and either the customer's own packname or one a manager still owns by
  `IsPackNameOwnedByManagerId` — otherwise the attributes are cleared and the context falls back to the customer's
  own packname, so it self-heals on the next request.
- **U-39.** The packname selector handed the view a list id that is not one of its own options. The view component
  read `GetSelectedPackNameListIdAsync` and passed it to the Razor view, which marks `Selected` on the matching
  option and then calls `loadPackNames(@Model.SelectedPackNameListId)` on load. When the stored id has gone out of
  scope — a department virtual list (negative id) whose mapping was revoked, a list whose ownership was removed —
  no option matches, so TomSelect displays the first list while the AJAX load runs for the OLD id, whose factory
  answer is `[]`. The result Eduardo saw: the first list looks selected and the packname dropdown is empty and
  unusable, with no way to pick anything. Fixed in phase 2a §5f (C14): when the stored id is not among the
  available lists, the first available list is selected server-side and that is the id the view loads.

## 18c. Filed retrospectively from phase 2b — 2026-09-22

**U-40 and U-41 were cited in four shipped code comments before they were ever filed here.** Found by
following the references while correcting the phase 2b records: `grep "U-4[01]"` returns four hits in
`Plugins/Nop.Plugin.CW.Core/` and none in this document. Both are closed; they are written down so the
comments resolve.

- **U-40.** Every one of `packname_get_listids_by_manager`'s four paths filtered `Active = 1`, so
  unticking *Active* on a packname list locked its manager out of every packname in it — including the
  records they would need to touch to put it right. Ownership is not visibility. **Closed 2026-09-17
  (decision 1):** the SP takes `@RequireActiveList BIT = 1`; ordering passes 1, ownership passes 0
  (`MultiStoreService.cs:831`, `CWDatabaseService.cs:115`). This also closed P-20.
- **U-41.** `MultiStoreController.StoreSettings` took its scope from a hidden `StoreId` field and wrote
  it with `SaveSettingOverridablePerStoreAsync`, so posting `0` from a store's settings screen rewrote
  the **all-stores defaults** — feature switches included — under `ADMIN_ACCESS_MULTISTORE_SETTINGS`,
  when that scope belongs to `ADMIN_ACCESS_DEFAULT_STORE_SETTINGS`. The action carries no
  `[CheckCWFeature]`, so nothing else stood in the way. **Closed 2026-09-21 (phase 2b part 1, change
  11)**, verified live: all 34 global setting rows byte-identical after the attempt.

  `GeneratePackNames` and `GeneratePackNameLists` write the same three settings from the same posted
  id and were given the same refusal in round I (2026-09-22). Those two carry
  `[CheckCWFeature(StoreSource = Parameter)]`, which already fails closed on `0`, so **that half is
  defence in depth rather than a second open crossing** — round I's write-up said otherwise and was
  corrected on 2026-09-22 after checking `CheckCWFeatureFilter.IsAllowedAsync`. See
  `Feature_Toggles_Reference.md` §3.2 for the `AllowDefaultStore` rule that makes the gate a
  default-scope guard in its own right.

## 18d. Filed from phase 2b part 4 — 2026-09-23

Three items part 4 deliberately did not take. Each has a destination; none is a small fix.

### U-42. Stale and missing XML doc comments — Tidy-up — no phase
- **71 items, none user-visible.** 63 doc blocks name fewer `<param>` tags than the method has (20 of
  them just `pageIndex`/`pageSize` on paged overloads) and 8 differ only in casing (`PackName` vs
  `packName`). Part 4's change 129–131 fixed the four that named a parameter **that does not exist**,
  which is the subset that actively misleads.
- A missing tag is visibly missing and a casing difference is invisible to a reader; only DocFX cares.
  Worth a single mechanical pass whenever someone is already in those files.

### U-43. The admin customer→branch save validates nothing — Issue (Scope) — the customer phase
- `ExtendedCustomerController` writes `customerBranch.BranchId = model.BranchId` on the create path
  (`:386`) and the edit path (`:635`) with **no check of any kind** — not `Active`, not `Deleted`, and
  **not the store**. An admin can put a customer on another store's branch.
- **The missing store check is the serious half**, and it is pre-existing. Part 4's change 106/107
  filtered that picker and keeps the customer's current branch, so the screen is now correct; this is
  the write half, the same defence-in-depth split as round I.
- Not fixed in part 4 because the controller does not inject `IBranchService`: the fix is a constructor
  change, two call sites, resources and its own tests. Filing it rather than burying it in a correction
  round was deliberate.

### U-44. `@T(...)` inside a JavaScript string literal — Issue (Localisation) — before any translation — **MultiStore views closed by phase 2d, Subsidy views by phase 3 (2026-09-29)**; the rest (exchanges / returns views and stragglers) goes to the exchanges phase; the detector below was run on 2026-10-21 and its 75 hits are listed in `Subsidy_PackName_Open_Items.md` §3
- **114 sites across the plugin.** Razor HTML-encodes `@T(...)`, and because the value is assigned to a
  JS variable rather than written into markup, nothing ever decodes it.
- **55 of the 114 use a CW resource; exactly 2 of those resources were non-ASCII** and were rendering
  as `Loading products&#x2026;` on the returns wizard until change 143. The other 53 are safe only
  because their English text happens to be plain ASCII.
- **This becomes 55 broken strings the day the plugin is translated.** Spanish alone brings
  `Guardando…`, `¿Está seguro?`, `Añadir`. The failure is silent: no exception, no log entry, just
  entity text on screen.
- The fix is not 114 edits. It is one Razor helper — `@JsT("key")` returning
  `Html.Raw(JsonSerializer.Serialize(T(key).Text))` — then a mechanical sweep. Part 4's change 126
  already proves the pattern: the multistore admin bundle serialises through `JsonSerializer` +
  `Html.Raw` for exactly this reason, and those 31 strings are currently the only immune ones.
- **Detector** (re-runnable): scan every `.cshtml`, inside `<script>` blocks only, for
  `['"]@T\(` or `@T\([^)]*\)\.Text\s*['"]`.

### Noted, no action
- **`BranchModelFactory.PrepareBranchListModelAsync` does not map `Active`** (nor `StoreId`,
  `CardCode`, `BranchNumber`), so `BranchList` returns `Active: false` for every branch. Harmless
  today: `Branch/List.cshtml` renders BranchCode, Breadcrumb, StaffCount and Id, and never Active. It
  becomes a live defect the moment someone adds that column — which is more likely now that `Active`
  governs assignment.
- **`_Branches.BranchNodes.cshtml:8` computes `activeClass` and never uses it**, naming a
  `branch-inactive` class no stylesheet defines. The status dot (`dotClass`, line 9) is what actually
  marks an inactive branch. (Change 147, which was to remove it, was retired on 2026-09-28: `dotClass` is read on the next line and the `activeClass` line was found in use — not dead.)

### Carried to later phases

### ~~S-25. Topup save is a read-modify-write with no transaction or lock — Issue (Money) — phase 2b~~ — **closed by phase 2b part 2A, 2026-09-23** (top-up saves are insert-only; the views `SUM`)
- `SubsidyService.SaveSubsidyTopupForPeriodAsync` (`:1070-1113`) and `SaveProductGroupTopupForPeriodAsync`
  (`:1180-1222`) do `existing.Credit += creditToAdd` then a full-row `UpdateAsync`. No transaction, no
  `ILocker`, no concurrency column. The Save button (`ChangeTopupSubsidy.cshtml:25`) is `type="button"`,
  never disabled, with no in-flight flag, so a double-click is two requests.
- Two saves of 50 against a balance of 100 both read 100 and both write 150, while **both** insert a
  history row — `SubsidyTopupHistory` sums to 200 against a stored 150, permanently.
- The credit views SUM every topup row in the period and the schema deliberately has no uniqueness
  there (`CWDatabaseService.cs:599-608`), so **insert-only** is both correct and simpler than locking.

### ~~S-26. No upper bound on a topup; overflow is a 500 — Issue — phase 2b~~ — **closed by phase 2b part 2A, 2026-09-23** (`SubsidyTopupValidator`: `> 0`, `≤ 1,000,000`)
- `SubsidyTopupInputModelValidator` requires only `Credit > 0`; the column is `DECIMAL(18,4)`. A posted
  1e14 either overflows inside `InsertAsync` (unhandled → 500, and the AJAX call has no `error`
  handler, so the dialog just hangs) or, below that, grants unbounded credit with no second approval.
  `ProductGroupTopup.Quantity` is an unchecked `int` `+=`.

### ~~S-27. The topup guard does not test the ordering period, and C# and SQL disagree about it — Issue — phase 3~~ — **closed by phase 3, 2026-09-29** (admin and storefront refuse with a message; the window follows the variation's end)
- `ValidateTopupAccessAsync` checks existence, `Deleted`, `Active` and store, but not the period, while
  everything the screen shows is period-filtered in SQL (`subsidy_is_in_ordering_period`).
- `SubsidyPeriodHelper:106-108` + `SubsidyService:1078-1084` window topups on `CreatedOnUtc <= windowEnd`;
  `subsidy_get_topup_period_end` deliberately does the opposite ("a topup created before or after the
  season still counts"). So a topup posted for an expired subsidy inserts a row the lookup can never
  find again, and every later save inserts another.

### ~~S-28. A refused topup looks like a hung dialog — Nice-to-have — phase 2b~~ — **closed by phase 3, 2026-09-29** (localised messages, shown in nop's notification bar)
- `SubsidyCatalogController` returns `Json(new { success = false, message = "Access denied." })`, and
  `ChangeTopup.cshtml:121-129` branches on `success`, then on `html`, and does nothing else — `message`
  is read nowhere. Hard-coded English, unlike everything else in the controller.

### ~~S-29. UI gate weaker than the guard; activity log records two different ids — Nice-to-have — phase 2b~~ — **closed by phase 2b part 2A, 2026-09-23** (both paths log the assignment id and the amount added; the UI-gate half is moot since the context packname is revalidated for ownership)
- `SubsidyCatalogModelFactory:320-323` omits the `IsPackNameOwnedByManagerId` test the guard applies,
  so the button shows for a packname the manager does not control and opens an empty modal.
- Storefront logs `SubsidyPackNameId`, admin logs `SubsidyTopup.Id`, under the same keyword and the same
  resource, so `('412: 300.00')` cannot be resolved without knowing which screen wrote it. Both log the
  period balance, not the amount the manager entered; the product-group log records neither amount nor
  quantity.

### ~~S-30. The top-up dialog header: the two amounts do not line up — Nice-to-have (visual)~~ — **closed by phase 7, 2026-10-08** (one grid, Change under the amounts)
- **Found 2026-09-17** by Eduardo, looking at the Change-topup dialog header: *Subsidy $ 0.00* and *Topup $ 0.00 Change*
  sit on two rows whose right edges disagree, because the second row carries the **Change** button.
- **Cause:** `Content/SubsidyCatalog/change-topup.css:75-81` lays `.cw-topup-global-section` out as a column with
  `align-items: flex-end`, and `Views/SubsidyCatalog/ChangeTopup.cshtml:31-40` puts the button *inside* the second
  `.cw-topup-credit-row`. Each row is only as wide as its own content, so the topup row's amount is pushed left of the
  subsidy row's amount by exactly the width of the button. `.cw-topup-credit-value`'s `min-width: 54px` aligns the
  digits **within** a row, never across rows.
- **Wanted (Eduardo's words):** the Topup amount aligned with the Subsidy amount, and **Change** under it.
- **Fix — a label/value grid so both amounts share one right edge, with the button on its own line.** In
  `ChangeTopup.cshtml`, move the button out of the credit row:

```html
                <div class="cw-topup-global-section">
                    <div class="cw-topup-credit-row">
                        <span class="cw-topup-credit-label">@T("Plugins.CW.Core.ChangeTopup.BaseCredit")</span>
                        <span class="cw-topup-credit-value">@subsidy.CreditFormatted</span>
                    </div>
                    <div class="cw-topup-credit-row">
                        <span class="cw-topup-credit-label">@T("Plugins.CW.Core.ChangeTopup.TopupCredit")</span>
                        <strong class="cw-topup-credit-value subsidy-topup-credit-@subsidy.Id">@subsidy.TopupCreditFormatted</strong>
                    </div>
                    <button type="button" class="cw-topup-change-link cw-topup-change-link-block"
                            onclick="openChangeSubsidyTopup(@subsidy.Id)">
                        @T("Plugins.CW.Core.ChangeTopup.Change")
                    </button>
                </div>
```

  and in `change-topup.css`, replace the `.cw-topup-global-section` / `.cw-topup-credit-row` pair with a grid, keeping
  everything else:

```css
/* Credit + topup columns in the header: ONE grid, so the amounts share a right edge
   whatever the labels are translated to. Two rows of label/value, then the Change
   button on its own line under the value column. */

.cw-topup-global-section {
    display: grid;
    grid-template-columns: auto auto;
    justify-content: end;
    align-items: baseline;
    gap: 3px 6px;
    flex-shrink: 0;
}

.cw-topup-credit-row {
    display: contents;
}

.cw-topup-change-link-block {
    grid-column: 2;
    justify-self: end;
    padding-inline: 0;
}
```

  `display: contents` keeps the markup's row grouping while letting the grid place the labels and values, so no row is
  re-indented and the `subsidy-topup-credit-@subsidy.Id` hook the JS updates after a save is untouched.
- **Check the same pattern in the per-product-group rows** (`.cw-topup-pg-table`) before closing this: the Change link
  is reused there, and the fix must not push those cells around. Include the dialog in whatever visual pass 2b gets.

### ~~G-13. The list dropdown offers lists and departments with nothing in them — Improvement — phase 4a~~ — **closed by phase 4a Part 2, 2026-09-30**
- **Asked for 2026-09-17** by Eduardo: "a packname list with no packnames available, or empty departments, are being
  shown in the list dropdown … since they are empty I don't think it makes sense to show them there". Not a defect —
  the manager does have access to them — but each empty entry is a dead end: selecting it loads an empty packname
  dropdown.
- **Where:** `MultiStoreService.GetAvailablePackNameListsAsync` (`:1048-1101`) composes the entries — lists the
  manager owns, their own list, and either department lists (when `ShowDepartmentPackNamesByBranch`) or one virtual
  `Name (Dept)` entry per department with a negative id — and never asks whether any of them holds a packname the
  manager can see. Its only consumer is `PackNameSelectorViewComponent:55`, so the filter belongs inside it.
- **"Empty" has to mean the same thing the dropdown loads**, or the two disagree and an entry with packnames still
  looks empty. `PreparePackNamesModelForManagerAsync` resolves a roster three different ways, so emptiness does too:
  a list the manager OWNS → `GetActivePackNamesByListIdAsync(listId)`; a list reached only through a department →
  `GetActivePackNamesByListIdForDepartmentsAsync(listId, managerDepartmentIds)`; a virtual department entry →
  `GetActivePackNamesByListIdForDepartmentsAsync(0, [thatDepartmentId])`. Those are the same cached readers the
  roster itself calls (cached per list and per department set, and their invalidation was corrected in phase 2a C5
  and C6), so the check costs cache hits rather than queries and cannot drift from what loads.
- **The manager's own list always stays**, empty or not: it is how they order for themselves, and the roster adds
  their own packname to it even when they do not own the list.
- **The trade-off, which is the reason this is a decision and not just a filter.** The selector's **+** button posts
  `AddPackName/<the selected list id>` (`PackNameSelector/Default.cshtml:173`), so an entry that is not in the
  dropdown cannot be the target of an add. Hide every empty list and a newly created list can only get its first
  packname from admin. Three ways to settle it:
  1. **Hide them all** (what was asked). Simplest, and the storefront loses "add the first packname to a new list".
  2. **Keep an empty list the manager OWNS when `AllowManagersToAddPackNames` is on**, hide empty department entries
     and empty non-owned lists. Preserves the workflow; leaves some of the noise.
  3. Hide them all and give the add dialog its own list picker (a proper fix for the coupling, more work).
  Eduardo's call; (1) as asked, unless the store relies on creating lists and filling them from the storefront.
- **DECIDED 2026-09-17 (Eduardo): option 1 — hide them all.** An empty list is not offered even when the manager
  could add to it; a brand-new list gets its first packname from admin. Recorded so nobody re-opens it: the
  consequence was named and accepted, and if it bites in use, option 3 (a list picker inside the add dialog) is the
  way out, not un-hiding.
- **Code — REPLACE ⟨`GetAvailablePackNameListsAsync`⟩ in `Services/MultiStores/MultiStoreService.cs`**, the whole
  method, plus the helper after it:

```csharp
    public async Task<List<PackNameList>> GetAvailablePackNameListsAsync(int customerId)
    {
        var packName = await _packNameService.GetPackNameByCustomerIdAsync(customerId);

        if (!(packName?.Manager ?? false))
            return new List<PackNameList>();

        // Run these three in parallel — all independent of each other
        var (packNameList, lists, departments) = await (
            _packNameService.GetPackNameListByIdAsync(packName.PackNameListId),
            GetPackNameListsByManagerIdAsync(packName.Id),
            GetDepartmentsByManagerIdAsync(packName.Id)
        ).WhenAll3();

        if ((packNameList?.StoreId ?? 0) == 0)
            return new List<PackNameList>();

        var storeId = packNameList.StoreId;

        //The lists the manager OWNS, captured before the own list and the department lists are appended:
        //ownership decides WHICH emptiness question each entry gets asked below (gap analysis G-13).
        var ownedListIds = lists.Select(pnl => pnl.Id).ToList();
        var departmentIds = departments.Select(d => d.Id).ToList();

        //If the packName doesn't owned his packName list, we need to add it to be able for the packName to order for himself
        //(like packNames that are not manager are able to order for themselves
        if (!lists.Any(pnl => pnl.Id == packName.PackNameListId))
            lists.Add(packNameList);

        //Check whether the manager is mapped to departments
        if (departments.Any())
        {
            //Check the setting for showing the packnames in their own list or in a one big list for the department?
            var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

            //If we should show the packNames in their own branch, we need to find them and add the missing packname lists to the list
            if (storeSettings.ShowDepartmentPackNamesByBranch)
            {
                var depPackNameLists = await GetPackNameListsByDepartmentIdsAsync(departmentIds);
                foreach (var depPackNameList in depPackNameLists)
                    if (!lists.Any(pnl => pnl.Id == depPackNameList.Id))
                        lists.Add(depPackNameList);
            }
            else //Show one big list for each department. We add (dept) to the name because the department can have the same name as a list and could look like it is duplicated
            {
                foreach (var department in departments)
                {
                    lists.Add(new PackNameList
                    {
                        Id = -department.Id, //Use negative to be able to identify this list as a virtual list
                        StoreId = storeId,
                        BranchId = department.Id,
                        Name = $"{department.Name} ({await _localizationService.GetResourceAsync("Plugins.CW.Core.Department.Abbreviation")})",
                    });
                }
            }
        }

        //Offer nothing that would load nothing: an empty entry is a dead end, and selecting one leaves the
        //packname dropdown empty with no way forward (G-13, decided 2026-09-17). Emptiness is asked of the
        //SAME cached readers the roster uses, so the dropdown can never offer a list that loads nothing nor
        //hide one that would load something.
        var offered = new List<PackNameList>();

        foreach (var list in lists)
            if (await HasVisiblePackNamesAsync(list, packName.PackNameListId, ownedListIds, departmentIds))
                offered.Add(list);

        return offered.OrderBy(pnl => pnl.Name).ToList();
    }

    /// <summary>
    /// Whether a selector entry holds at least one packname this manager can see.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>PreparePackNamesModelForManagerAsync</c>'s three routes exactly — owned list, list reached
    /// through a department, virtual department entry — and calls the same cached readers, so "shown" and
    /// "loadable" cannot drift apart. Those entries are cached per list and per department set, so this costs
    /// cache hits rather than queries.
    /// </remarks>
    protected virtual async Task<bool> HasVisiblePackNamesAsync(PackNameList list, int managerListId, IList<int> ownedListIds, IList<int> departmentIds)
    {
        //The manager's own list always stays: it is how they order for themselves, and the roster adds their
        //own packname to it even when they do not own the list.
        if (list.Id == managerListId)
            return true;

        //A virtual department entry: the negative id carries the department id.
        if (list.Id < 0)
            return (await _packNameService.GetActivePackNamesByListIdForDepartmentsAsync(0, [Math.Abs(list.Id)])).Count > 0;

        //A list the manager owns: every active packname in it is theirs to see.
        if (ownedListIds.Contains(list.Id))
            return (await _packNameService.GetActivePackNamesByListIdAsync(list.Id)).Count > 0;

        //A list reached only through a department: only that department's packnames are visible in it.
        return (await _packNameService.GetActivePackNamesByListIdForDepartmentsAsync(list.Id, departmentIds.ToList())).Count > 0;
    }
```

- **Retest with it:** phase 2a matrix 4 counts the selector's options, and C14's "fall back to the first offered
  list" now falls back to a first list that is guaranteed to load a roster — which is a small improvement to C14, not
  a conflict. Both need one pass after this lands, which is why it is sequenced after 2a's matrices rather than into
  them. Filed to **phase 4a**, which already owns `PackNameSelector/Default.cshtml` and re-tests what the selector
  shows (S-03 marks blocked packnames there), so the selector gets one pass instead of two.
- **Size:** Small.

### G-12. `GetManagerStoreIdAsync` does not filter deleted lists or branches — Nice-to-have — phase 2c
- `MultiStoreService.cs:101-115` mirrors the SP's CTE but filters neither `pnl.Deleted` nor `b.Deleted`.

### ~~P-20. `PackNameList.Active = 0` silently revokes ownership — Issue (decision) — phase 2c~~ — **closed by phase 2b part 1, decision 1, 2026-09-17**
- The ownership predicate is `!Deleted` only, on purpose (a deactivated packname stays its manager's),
  but the list ids it works from come from the SP, whose every path filters `pnl.Active = 1`
  (`CWDatabaseService.cs:130, 139, 149, 159`). Deactivating a list therefore drops every packname in it
  — including the manager's own — out of the owned set, and the guards refuse edit and topup. This is
  the same failure the ownership remarks were written to prevent, one level up.
- Departments are asymmetric: `GetDepartmentsByManagerIdAsync` filters no `Active`. Needs a product
  decision (does list deactivation revoke management?), then either an SP variant without
  `pnl.Active = 1` or a documented rule.

---

## 18e. Filed from phase 4a — 2026-09-30

### ~~S-31. Subsidy rows still restricted a store with subsidies switched off — Issue — phase 4a~~ — **closed**: the SQL fix by phase 4a Part 1 (2026-09-30), the Store Credit stub deleted by phase 7 (2026-10-08)
- **Found 2026-09-30** by Eduardo's question on phase 4a Part 1. Three SQL views decided subsidy rules with
  no feature test: `vw_CORE_SUB_SubsidyPackNameCanOrder` (can-order block and the hidden cart lines) and
  the two "in ordering period" views (the "only allocated products" restriction on catalogue lists and
  bulk order, and the info strip's subsidy button). A store with the feature off but old subsidy rows was
  blocked and restricted by them.
- **Fixed in phase 4a Part 1** (change 4a.15): the views read `SubsidyEnabled` from
  `vw_CORE_SUB_StoreSubsidyVariationSetting`, the SQL mirror of `CWFeatureSet.Subsidy`. The rule is now
  `Feature_Toggles_Reference.md` §1.5; the review of every subsidy reader is in the phase document §5.
- **Still open, a stub:** `SubsidyReportingService.GetCustomerSubsidySummaryAsync` (the "Credits for me"
  subsidy section) returns "no subsidy" today. Whoever implements it must hide the section when subsidies
  are off — an entitlement is a capability (§1.2 of the feature reference). **Goes to:** the exchanges
  integration that fills the stub.

## 19. Not verified in this pass

- Whether the **4.1 production** SQL has S-01 / S-02 (changes their framing from regression to long-standing defect).
- Runtime confirmation of S-04 (Razor compile error), P-04 (`FormatException` in the log) and C-01/P-02 (the user-visible
  disappearance) — all derived from source, not from a running instance.
- `InsertAsync` on `ProductGroupProduct` (composite PK, no identity — `ProductGroupProductBuilder.cs:17-18`): nop's
  `InsertWithInt32IdentityAsync` runs `SELECT SCOPE_IDENTITY()`; whether LinqToDB tolerates the null identity needs a runtime
  check. The standard nop shape (identity `Id` + unique index) is recommended regardless.
- Rounding agreement between `vw_CORE_SUB_OrderSubsidyTotal` and the allocation for 3-decimal unit prices (C-07).
- Whether nop's data provider supports a transaction scope around items + settlement (C-08).
- Odoo / reporting consumers of `Order.OrderTax` / `OrderTotal` (C-06) — the Odoo plugin was not read.
- Whether `ShoppingCartOverrideService.PackNameCanAddAddress` has any caller (T-01, `AllowCustomersToAddAddress`).
- nop 4.9 `CustomerService.GetCustomerRoleIdsAsync` short-term cache key shape (P-03 implementation detail).
- nop 4.9 public `Nop.Web/Validators/**` were not in the analysed copy; G-02's "no nested validation" rests on
  FluentValidation semantics and 4.6's explicit `SetValidator`.
- 4.6 `Jim2CustomerController` base-class attributes (anti-forgery) — out of scope.
- The 4.6 → 4.9 packname **resource keys** were checked by family and by "used but not installed" (0 missing), not diffed
  one by one for wording.
- `lib/tom-select/*`, `Content/icons/*.svg` were absent from the analysed copy because the copy excluded them; they are
  present in the repository (checked on disk on 2026-09-09) — not a finding.
