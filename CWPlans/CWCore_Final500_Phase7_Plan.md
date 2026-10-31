# CW.Core move to the final 5.00 — phase 7: regression

Phase 7 of `CWPlans/CWCore_Final500_Strategy.md` (§4). Every phase-4/5 part's test plan run again end to end on the
final 5.00, the feature switches both ways, performance against 4.9, phase 6's leftovers, and M3's deferred uninstall
check last. **Done when:** no open defect from the port. Written 2026-10-31.

## 1. Where, and the test text

- **Site:** `testnop50`, store 1, opened as `https://127.0.0.1:59579` (phase 6's host; the 4.9 site stays signed in on
  `localhost:65254` for the performance group and for comparisons). Admin signed in; storefront tests as Steve Gates
  (customer 4, packname manager) impersonated from the admin, or as the customer the test names.
- **The test text is the source spec's own test plan**, in the beta port source:
  `NOP50Beta/CWPlans/Deprecated/CWCore_Phase{part}_Spec.md`, §6 (§7 in 4b.1). Steps and expected results apply as
  written, with three standing readings:
  1. **"Fails until 4x" / "404 until 4x"** (4a T4, T13, T14; 4b T7; 4c T15): every part is ported now, so the grid,
     tab or page is expected to **work**.
  2. **Build, test and restart rows** (T1–T3 of each part, and the *Run the CW Core plugin update* rows) collapse into
     group 0 below: one build, one test run, one restart, no plugin update (the version has not changed since phase 6).
  3. **Ids and names** are M3's data (`CWCore_Final500_M3_Plan.md` §5): customers 4–8 Steve Gates, Arthur Holmes,
     James Pan, Brenda Lindgren, Victoria Terces; branches *Test Branch 1*, *1.1*–*1.4* and their lists from
     `TestData/CWCore_Phase4a_TestData.sql`; packname 1 *Steve Gates*, 13 *Test 1b*, 23 *Tom & Jerry*; subsidies 1 and
     2; product group 1; orders 6–10. Where a test names something M3's data lacks, the run creates it and says so.
- **Already done, not repeated:** 5h T5, T8b, T9–T16, T18, T19 (M4, passed 2026-10-31).

## 2. Groups

| Group | Source | Tests | Who |
|---|---|---|---|
| **0. Build and start** | each part's T1–T3 | Build 0 errors; `dotnet test Tests/CW.Tests -p:SolutionDir=<repo root>` all pass (631 CW.Core); restart; *System → Log* has no start-up error | Eduardo |
| **A. Foundation, MultiStore, customers** | 4.0 T4–T6; 4a T4–T15; 4b T4–T16; 4b.1 T4–T13 | 38 | Claude; 4b T9's storefront sign-in and 4b.1's *SQL PART 1* (T4, T13) are Eduardo's |
| **B. Subsidies admin** | 4c T4–T15 | 14 | Claude |
| **C. Credit notes admin** | 4d T4–T10 | 7 | Claude |
| **D. Exchanges configuration, return requests** | 4e T4–T17 | 14 | Claude |
| **E. Admin order page** | 4f T4–T12 | 9 | Claude |
| **F. Bulk ordering admin** | 4g T4–T11 | 8 | Claude |
| **G. Packname selector and management** | 5a T4–T14 | 11 | Claude |
| **H. Subsidy catalogue, bar, top-ups** | 5b T4–T15 | 12 | Claude |
| **I. Cart and packname checkout** | 5c T4–T16 | 13 | Claude |
| **J. Order details** | 5e T4–T11 | 8 | Claude |
| **K. My Subsidies, My Store Credit** | 5f T4–T13 | 10 | Claude; T8 needs `TestData/CWSubsidy_TestPastSeason.sql` (Eduardo) |
| **L. Returns wizard and exchanges** | 5g T4–T23 (T22b included) | 21 | Claude; T12 (keyboard) is Eduardo's |
| **M. Bulk ordering storefront, the rest** | 5h T4, T6, T7, T8, T17, T20 | 6 | Claude |
| **N. Feature switches both ways** | `Feature_Toggles_Reference.md` §1.1 and §1.5 (4.90 source `CWPlans/`) | PackName, Subsidy, CreditNotes, Exchanges, BulkOrder: each off, then its storefront and admin entry points and its restrictions on the rest of the store checked closed; then on, checked open. Each part's own switch test (4c T14, 4d T9, 4e T16, 4f T11, 5a T13, 5b T14, 5g T22) runs in its group | Claude |
| **P. Performance against 4.9** | — | §3 | Claude |
| **Q. Phase 6 leftovers** | phase 6 plan §5.4 | an invoice with a subtotal discount (4.9 and 5.00, same order); a subsidy top-up and a return restore on 5.00, then the packname's history shows both rows; the packname checkout's *Confirm* click watched on every order this run places | Claude |
| **U. Uninstall and reinstall** | M3 *Install / uninstall clean* (deferred, decision 8) | Last, after a backup of `testnop50`: uninstall CW.Core; `TestData/CWCore_M3_InstallCheck.sql` shows no CW object, resource, permission or setting left; install again; the same script shows M3's counts (43 tables, 11 views, 2 functions, 2 procedures, 1,162 resources, 10 permissions); restore the backup | Eduardo (Claude prepares the checks) |

Order: 0, then A to M in part order (each part's data feeds the next), N, Q, P, U.

## 3. Performance

Same pages on both sites, same role, warm (each page fetched once first, then timed 5 times; the median is the
figure). Timed from the browser as the server's response time (request start to last byte), so rendering and assets
do not count.

| Area | Pages |
|---|---|
| Storefront, as Steve impersonated, his packname selected | home; a category; a product; `/cart`; the packname checkout; `/SubsidyCatalog` and its items request; `/bulkordering` and one grid request; *My account → Orders*, an order's details; *My Subsidies*; *My Store Credit* |
| Admin | customer list and Steve's edit page; the store editor and its *Packnames* grid; the subsidy's history grid; order list and a packname order's page; *Credit Notes Report*; return request list |

The two databases differ in size (4.9's has 3,529 packnames, 5.00's 12), so a page whose work grows with the data is
named as such and not compared as like for like. **Flag:** 5.00 slower than 4.9 by more than 50 % on a page with
comparable data. A flag is investigated (the request's SQL, cache hits) before it is called a defect.

## 4. Side effects (dev database only)

As the source specs list them, on `testnop50`: branches, departments, cost centres, packname lists and packnames
created and deleted; customers created, one GDPR-deleted (4b T14); Victoria Terces's password changed (4b T9);
subsidies, product groups, top-ups and variations; credit notes; return reasons, actions, rules and fee rules; orders
(Check / Money Order), shipments, return requests and their approvals; carts; one category's ACL changed and put back;
settings and feature switches switched and set back (each recorded in §5). Welcome e-mails are queued, not sent.
Deletes only where a source test says so. Sign-in forms, the keyboard test and SQL are Eduardo's. Group U removes and
reinstalls CW.Core, then the backup puts the data back.

## 5. Results

Run 2026-10-31 on `testnop50` (groups A–Q, P). The per-test record is the tracker artifact
*CW.Core final 5.00 — phase 7 regression*. There are 182 results: 173 pass, 9 skipped and 0 failing, with the phase 7 fixes applied (`CWPlans/CWCore_Final500_Phase7Fixes_Spec.md`, checks F1–F4 passed). Group
U was skipped by decision, and the uninstall code was reviewed instead (§5.4). **Approved 2026-10-31.**

### 5.1 Outcome by group

| Group | Result |
|---|---|
| 0 | Pass: Eduardo's build, `CW.Tests` 776 passing (631 CW.Core), restart |
| A (4.0–4b.1) | All pass. 4a T12 failed in the run (branch address delete, defect 1) and passes after the fix. Eduardo ran 4b T9 (Victoria's storefront sign-in) and 4b.1 T13 (SQL PART 1 again) |
| B–F (4c–4g) | All pass |
| G (5a) | All pass (T13 run in N) |
| H (5b) | Pass. T8 skipped (no exhausted group with this data). T12 skipped (Steve manages his own list). T14 run in N |
| I (5c) | Pass. T13 skipped (one currency). T14 passes on a vendor set-up made for the test and taken down again |
| J (5e) | Pass. T8 skipped (one currency) |
| K (5f) | Pass. T6 skipped (one currency). For T8 and T9, Eduardo ran `TestData/CWSubsidy_TestPastSeason.sql`, which moved Steve's subsidy one year back. *Earlier seasons (1)* then shows 2025-26 with its top-up and order 6 |
| L (5g) | Pass. Eduardo ran T12 (keyboard) and T20 (second admin role). T15 skipped (one currency). T19 and T21 skipped: the exchange's difference was in the customer's favour, so the replacement became order 12 instead of a cart. T17 passes on a new request (6), after the status rule was set (§5.3) |
| M (5h) | T4, T6, T7, T8, T17 and T20 pass |
| N (switches) | All five pass both ways, with their restrictions. Subsidy off lifts Arthur's allocation gate (bulk ordering 15 cards, 5 when on). Credit notes off still settles an existing balance and still shows it: this is the liability rule, the same as 4.9 |
| Q (phase 6 leftovers) | Pass. Q1: the subtotal discount reads the same on the checkout, the order page and the invoice of order 14. The same order could not be built on 4.9 from its data, so there is no side-by-side invoice. Q2: top-ups and the return restore are in both history views. Q3: every packname order of the run submitted on the first click |
| P (performance) | No flag. 5.00 is as fast as or faster than 4.9 on every storefront page compared. On the admin pages the largest gap is the customer grid (+35 %), on unlike data. `/bulkordering` takes 417 ms on 5.00 and has no 4.9 counterpart (bulk ordering is closed for Steve there) |
| U | Skipped (decided 2026-10-31: CW.Core will not be uninstalled, and the data is worth more than the test). Code review in §5.4 |

### 5.2 Defects

All four are older than the move: the same lines are in the 4.90 source.

1. **Branch address delete** (`BranchController.AddressDelete` binds `branchId`, while the grid sends `EntityId`).
2. **Manual credit note activity text** labels both ids *StoreId* (`CreditNotesService.cs:285`).
3. **A plain return shows an empty exchange block** on *My account → Return requests* (`CustomerReturnRequests.cshtml:125`).
4. **`@T("Common.Loading")`** on the packname checkout is a key nobody installs. It logs a warning on every load.
   Correction to 4.0 T6: this warning is CW.Core's, not nop's.

The fixes are in `CWPlans/CWCore_Final500_Phase7Fixes_Spec.md`. Two further findings are left there for Eduardo to
decide: storefront `AccessDeniedView()` lands on *Page not found* (the same in 4.9), and the packname checkout keeps
its summary until a reload after its last line is removed. Outside CW.Core: nop's Google Analytics plugin logs
*This instance has already started one or more requests* when one request raises two cart events (bulk add).

### 5.3 Data and settings left on `testnop50`

- Return status rule *Return authorized* (id 21): *Perform store credit* and *Perform exchange* switched on. M3's data
  had them on *Received*, so an authorisation made no credit note. Kept, because that is the configuration the
  exchange workflow expects.
- New records from the tests: return requests 5 (plain, James), 6 (credit note, Victoria, authorised → credit note
  $43.50); orders 13 (Steve) and 14 (Victoria, uses $43.50 of that note); discount 3 *P7 subtotal $5* (now inactive);
  address 50 on Test Branch 1.2 (to be deleted by check F1 of the fix spec).
- Put back: packname 3's name (*Test  1b*), the store-1 *Allow customers to add address* override (removed), the
  store fee rule *Redelivery 5* (deleted), the five feature switches (all on), vendor settings (off) and products 36 and
  37 (no vendor), product group 1's icon (none) and its active state, subsidy 1 active, and the test cart lines.
- On 4.9 (`localhost:65254`), from Q1: discount 7 *P7 subtotal $5* (inactive) and a manual credit note of $43.50 for
  Victoria (order 5, *P7 Q1 comparison*).

### 5.4 Uninstall and reinstall, read instead of run

- `CWCorePlugin.InstallAsync`, `UpdateAsync` and `UninstallAsync` are identical to 4.9's. nop 5.00's uninstall path is
  identical to 4.90's: `plugin.UninstallAsync()`, then `ApplyDownMigrations`, and the uninstall is refused while a
  plugin that depends on CW.Core (Integration.Odoo) is installed.
- `SchemaMigration` lists all 43 tables that `CWNameCompatibility` maps (its other 12 entries are views). `Down()`
  drops them in reverse foreign-key order, so an uninstall deletes every CW.Core row. Reinstalling runs `Up()` again,
  then `UpdateDatabaseScriptsAsync` recreates the SQL objects with `CREATE OR ALTER`.
- As in 4.9, an uninstall leaves behind: the views, functions and procedures; `MultiStoreSettings`; the two message
  templates; the activity log types; and 20 locale resources outside `Plugins.CW.Core` (`Enums.*`,
  `Admin.Common.Actions`, `Admin.Common.Create`). A reinstall reuses or overwrites each of them.
- One cosmetic slip: the XML comment on `SchemaMigration.Down()` closes `</summary>` twice.
