# CW Dashboard — Spec (consumed)

> Status: **built 2026-11-19 in phases 1–4 and consumed.** What survives (the design, the decisions, the figure
> definitions, the traps and what is open) is in the plugin's reference,
> `Plugins/Nop.Plugin.CW.Dashboard/References/Dashboard_Plugin_Reference.md`. This document is kept as history: the
> approved spec and each phase's record (§8.1–§8.4). Where they differ, the reference and the code win.

## 1. What it is

The Statistics Dashboard is a procurement overview for a store, shown in the **nop admin area**. It is modelled on
the "Procurement overview" mockup, without its *Quick actions* row. A second admin page configures it per store
(the "Dashboard Settings" mockup).

### 1.1 Decisions taken (2026-11-19)

| # | Decision |
|---|---|
| D1 | **Users are store managers, in the nop admin area.** A manager sees only their own store; administrators can pick any store |
| D2 | **Scope: role + own store.** Two new permissions. *Dashboard — own store* takes the store from the manager's packname (`IMultiStoreService.GetManagerStoreIdAsync`) and shows no store picker. *Dashboard — all stores* adds the picker. The manager's role gets only the first permission plus nop's *Access admin area*, so the rest of the admin area stays closed |
| D3 | **Configured by an administrator, per store.** It uses nop's store scope: an all-stores default plus *override for this store*, like `MultiStoreSettings`. A manager can only expand and collapse sections, within the rules |
| D4 | **A separate plugin, `Nop.Plugin.CW.Dashboard`**: SystemName `CW.Dashboard`, tables `DASH_*`, locale keys `Plugins.CW.Dashboard.*` |
| D5 | **It reads CW.Core through the built DLL (§10.2 Pattern C)**, the same way as `Integration.Odoo`: `<Reference>` with `Private=false` / `ExcludeAssets=runtime`, `DependsOnSystemNames: ["CW.Core"]`, and CW.Core built first in `NopCommerce.sln` |
| D6 | **Approvals, Vending, Insights and Ask (AI) are "to be defined".** None is built in this version. The section framework (§4) lets each one be added later as one class, with no change to the page |
| D7 | "Allowance" in the mockup is **Subsidy** everywhere (CLAUDE.md §7 terminology) |
| D8 | **The landing after admin sign-in does not change** (O4, 2026-11-19). A manager lands on nop's admin home, as every admin user does, and opens the dashboard from the menu. nop's `Admin/Home/Index` has no permission check of its own (each of its cards checks its own permission), so a manager sees the home page with only the cards their role allows |

## 2. Mockup → CW terms

| Mockup | CW / nop meaning |
|---|---|
| Site | **Branch** (`CORE_Branch`) |
| Employees | **Active packnames** in the store (`CORE_PN_PackName`, `Active`, not deleted, list not deleted) |
| Allowance | **Subsidy** |
| Category (Workwear / Footwear / PPE) | See open question **O2** |
| AUD excl. GST | nop's primary currency, amounts **excl. tax** |
| FY to date | The Australian financial year (1 July – 30 June); the start month is a setting (§5) |

## 3. Plugin shape

```
Plugins/Nop.Plugin.CW.Dashboard/
├── plugin.json                 Group CW, SupportedVersions 5.00, DependsOnSystemNames [CW.Core]
├── DashboardPlugin.cs          install/uninstall: settings, locale resources, permissions; UpdateAsync on DATA
├── Infrastructure/NopStartup.cs  services; Order after CW.Core
├── Areas/Admin/
│   ├── Controllers/DashboardController.cs           Index, Sections (AJAX), Settings GET/POST, RestoreDefaults
│   ├── Factories/DashboardModelFactory.cs
│   ├── Models/  (DashboardModel, DashboardFilterModel, per-section models, DashboardSettingsModel)
│   ├── Validators/DashboardSettingsValidator.cs
│   └── Views/Dashboard/ Index.cshtml, Settings.cshtml, _Section.*.cshtml
├── Services/
│   ├── IDashboardScopeService     who is asking → store id, branch ids, can-pick-store
│   ├── IDashboardQueryService     the aggregate queries (§6), cached
│   └── Sections/IDashboardSection + one class per section (§4)
├── Settings/DashboardSettings.cs  ISettings, per store
├── Services/Security/DashboardPermissionConfigManager.cs
├── Services/EventConsumer.cs      admin menu item under CW.Core
└── Content/Admin/Dashboard/       dashboard.css, dashboard.js (Chart.js from nop's lib_npm/chart.js)
```

The first version has **no tables**: every figure is computed from nop's and CW.Core's tables. `DASH_` is reserved
for later sections (Insights, Approvals).

### 3.1 Access

| Permission | Key | Default role | Gives |
|---|---|---|---|
| Own store | `CW.Dashboard.ViewOwnStore` | none | the dashboard for the store from `GetManagerStoreIdAsync(customer)`; no store picker. A user with no manager store sees a "no store" notice |
| All stores | `CW.Dashboard.ViewAllStores` | Administrators | the store picker |
| Configure | `CW.Dashboard.Configure` | Administrators | the Settings page |

Menu: a top-level **Statistics** item after nop's *Dashboard*, with *Dashboard* and *Dashboard settings* under it
(`BaseAdminMenuCreatedEventConsumer`). Not under *CW Admin*: CW.Core shows its whole menu only to users who may manage
plugins (`EventConsumer.CheckAccessAsync`), so a manager would never see it there. nop shows each item to the users
holding one of its permissions.
The landing after admin sign-in is nop's, unchanged (D8): the dashboard is reached from the menu.

### 3.2 Feature switches (§0.4: every reader is checked)

| Section | Closes when |
|---|---|
| Employees tile, Subsidy overview, packname columns | PackName off (Employees), Subsidy off (Subsidy overview) — for the selected store (`ICWFeatureService`) |
| Spend by site | PackName off: a plain store has no branch snapshot on its orders; the section falls back to the customer's branch (`CORE_CustomerBranch`) — see **O3** |

Test plan T8 covers the feature-off case.

## 4. Sections

Every section implements one interface:

```csharp
public interface IDashboardSection
{
    string SystemName { get; }                 // "SpendByBranch"
    int DisplayOrder { get; }
    Task<bool> IsAvailableAsync(DashboardScope scope);       // feature switches, data present
    Task<object> PrepareModelAsync(DashboardScope scope, DashboardFilter filter);
    string ViewPath { get; }
}
```

The page asks every registered section for its model and renders the visible ones in `DisplayOrder`. A section's
visibility and default state come from the settings (§5). A section added later (Vending, Approvals, Insights, Ask)
is one more class and one more settings row; nothing else changes.

| # | Section (SystemName) | Content | Source | v1 |
|---|---|---|---|---|
| 1 | `InvoicedSpend` tile | Spend in the period, excl. tax (definition **O1**) | `Order` | yes |
| 2 | `OpenOrders` tile | Orders with status Pending or Processing | `Order` | yes |
| 3 | `Employees` tile | Active packnames in the store (in the branch, when a branch is filtered) | `CORE_PN_PackName` | yes |
| 4 | `Approvals` tile | — | no approval workflow in 5.00 | **D6** |
| 5 | `MonthlySpend` | Line chart, one point per month of the period | `Order` | yes |
| 6 | `SpendByBranch` | Horizontal bars, top 4 branches + "Other" | `CORE_PN_PackNameOrderItem.BranchId` (the snapshot at placement) | yes |
| 7 | `SpendByCategory` | Doughnut, top 5 + "Other" | **O2** | yes |
| 8 | `AttentionBar` | Delayed orders (not shipped after *N* days). Approvals and vending alerts are added with their sections | `Order` / `Shipment` | yes |
| 9 | `Insights` | Rule-based cards (e.g. a category's spend ± *X*% on the same period last year) | settings thresholds | **D6** |
| 10 | `Vending` | — | no vending data | **D6** |
| 11 | `SubsidyOverview` | Allocation, used, remaining, % used bar, number of packnames with a subsidy | `vw_CORE_SUB_SubsidyPackNameCredit` (net of restorations) | yes |
| 12 | `LatestOrders` | Last 5 orders: number, packname / customer, branch, total excl. tax, status, date; "View all" → nop's order list (administrators) | `Order`, `CORE_PN_PackNameOrderItem` | yes |
| 13 | `AskAi` | — | would reuse ThemeCore's AI client settings; sends store data to an AI provider | **D6** |

### 4.1 Filters (page header)

- **Branch**: all branches of the store, or one. A manager is offered every branch of the store (D2; per-branch
  limiting was not chosen).
- **Period**: *FY to date* (default), *This month*, *Last 12 months*, *Previous FY*.
- **Store** picker: only with *ViewAllStores*.

The filters reload the sections by AJAX. Values reach the script through a config div (§9.14); one IIFE script
(§9.15).

## 5. Settings (`DashboardSettings`, per store)

| Setting | Default | Notes |
|---|---|---|
| `Sections` | every v1 section on | Serialised list of `{ SystemName, Enabled, DefaultState }`; `DefaultState` is `Expand`, `Collapse`, `AlwaysShow` (cannot be collapsed) or `UserMayHide` |
| `FinancialYearStartMonth` | 7 | July |
| `DelayedOrderDays` | 3 | Attention bar |
| `InsightSpendChangePercent` | 15 | Kept for Insights (D6); not shown until that section exists |
| `PendingApprovalThreshold`, `VendingStockAlertPercent` | — | Added with their sections, not now |

Settings page: the mockup's left column (toggle + default state per section) and right column (thresholds). The
mockup's *Global Controls → Apply rule set to [stores]* is nop's own **store scope selector** plus *override for this
store* checkboxes, the same as CW.Core's Store Settings. *Restore to defaults* deletes the store's overrides (or, at
all-stores scope, resets the defaults). The mockup lists *Vending Overview* twice; that is taken as a mistake.

A manager's own expand/collapse choice is remembered per customer (`GenericAttribute`
`CW.Dashboard.CollapsedSections`). A section set to `UserMayHide` can also be hidden by the manager.

## 6. Data and performance

- Every figure is **one aggregate query** run in SQL (LinqToDB `GroupBy` / `Sum` over `IRepository<T>.Table`). Never
  load orders into memory and sum them in C#.
- Orders counted: the store's orders, not deleted, **not cancelled**, `CreatedOnUtc` in the period (converted from the
  store's time zone), excluding exchange return orders (`CORE_EX_ReturnRequestItem`), which are not new spend.
- Branch filter: an order belongs to a branch through its `CORE_PN_PackNameOrderItem.BranchId` snapshot (a packname
  order can span branches, so spend is summed per line, not per order).
- Cache: `CW.dashboard.{section}.{storeId}-{branchId}-{period}` with a 10-minute lifetime. Order changes do not clear
  it (a dashboard may be 10 minutes behind). An index on `Order(StoreId, CreatedOnUtc)` comes from nop; the plan is
  checked against the test data before adding any index of our own.

## 7. Open questions

| # | Question | Proposed answer |
|---|---|---|
| O1 | "Invoiced spend": which amount? | Goods + shipping **excl. tax, before subsidy and store credit** (`OrderSubtotalExclTax` + `OrderShippingExclTax` − discounts), for orders with payment status Paid, Partially paid or Pending, not cancelled. That is what the client's organisation spent, whoever paid it. The alternative is `OrderTotal` (only what was charged, after subsidy), which would understate spend for subsidised stores |
| O2 | "Category": nop categories or subsidy product groups? | nop's **top-level category** of each product (a product in several takes its first by display order). Product groups exist only in subsidy stores and do not cover every product |
| O3 | Spend by site for a plain (non-packname) order | The customer's branch (`CORE_CustomerBranch`) at the time of the query, else "Other" |
| O4 | Landing after admin sign-in for a manager | **Decided: D8** — no change |
| O5 | Which customer is "the manager" | A customer whose own packname has `Manager = true`; the store is that packname's list's store. A manager of several stores is not supported today (`GetManagerStoreIdAsync` returns one) |

## 8. Build phases

1. **Plugin skeleton**: project, `plugin.json`, startup, permissions, menu, settings and Settings page, empty dashboard page with filters.
   *Built 2026-11-19 (`2026_11_19_DATA_0.1`); installed on the local site, T1, T3 and T9 pass (see §8.1).*

### 8.1 Phase 1 record

What was built, and where it differs from the sections above:

- **Controller `CWDashboardController`** (`Admin/CWDashboard/Index`, `Settings`, `RestoreDefaults`), on nop's
  `BaseAdminController`. Named with the CW prefix so it cannot collide with a nop or plugin `Dashboard` controller.
  Views by explicit path under `Areas/Admin/Views/Dashboard/`.
- **The scope service was built in phase 1, not phase 2**: the filters need the store and its branches.
  `DashboardScopeService.GetScopeAsync(requestedStoreId)`: *ViewAllStores* → the requested store if it exists, else the
  current store; *ViewOwnStore* → the customer's packname when `Manager` is set, its store from
  `GetManagerStoreIdAsync`, the requested id ignored; otherwise none (the page shows a notice).
- **The section list is `Domain/DashboardSectionNames`** (system names, page order, default state), not the
  `IDashboardSection` classes. Phase 3 adds those classes and keys them by these names. The `Sections` setting is JSON
  read through `DashboardSectionSettingsSerializer.Read`: every known section once, in page order. A stored row for an
  unknown section is dropped, and a section the stored value lacks takes its default. The posted table goes through the
  same read before it is saved.
- **Default states**: the three tiles, Monthly spend, Spend by category and Subsidy overview are *Always show*; Spend by
  site, the attention bar and Latest orders are *Expand*. The mockup's Approvals tile and its *Ask* (*Optional for user to
  hide*) row are left out (D6).
- **Store override**: one *override for this store* checkbox covers the whole section table (it disables the table
  while unticked), one each for the two thresholds. `InsightSpendChangePercent` is not added until Insights exists.
- **Filters** reload the page by GET (`storeId`, `branchId`, `period`); the sections load by AJAX in phase 3.
- Every section shows a "Coming soon" card. *Collapse* and *Hide* are AdminLTE's card widgets; the per-customer
  memory of them is phase 4.
- `NopCommerce.sln`: the project in the Plugins folder, with a build dependency on CW.Core.

**Traps met while testing (2026-11-19), all fixed:**

1. **`section` is a Razor directive.** A loop variable named `section` (`@section.Title`) fails to compile at
   runtime ("The 'section' directive must appear at the start of the line"); the build does not catch it.
2. **nop's menu shows a leaf only to a user holding *all* its `PermissionNames`** (a parent: any;
   `AdminMenu.checkPermissions`). The Dashboard leaf therefore names the one view permission the current user holds.
3. **A store-scoped input that is not overridden is disabled, so it is not posted** and binds as 0. Range rules
   apply only where the value is saved: at the all-stores scope, or with its override ticked. The page posts its
   scope, and the controller redraws the page instead of saving when the scope has changed since the page was
   opened.

**Test results, local site (administrator, five stores):**

| # | Result |
|---|---|
| T1 | Pass. Installed from the plugin list; the *Statistics* menu shows *Dashboard* and *Dashboard settings*; the three permissions are created |
| T3 | Pass. The picker lists the five stores; picking store 2 reloads with its two branches |
| T9 | Pass. Store 1 overriding the table with *Employees* off writes a store-1 row; store 1 hides the section and store 2 still shows it. *Restore to defaults* at store-1 scope deletes the row, and all nine sections are back |
| T4 | Pass. The manager (role *Store managers*: *Access admin area* + *Dashboard — own store*) lands on nop's admin home; the menu offers *Statistics → Dashboard* and not *Dashboard settings*; the dashboard opens on store 2 (the packname's list's store) with no store picker; the settings page, order list and customer list answer Access denied |
| T5 | Pass. `?StoreId=1` is ignored: the page still shows store 2 and its branches |
| T10 | Not run: per-customer collapse memory is phase 4 |

**Test manager account** (local database only): customer 63, role *Store managers* (id 9), manager packname 4
"Dashboard Manager" in list 2 (store 2, branch 2). E-mail and password are in `CWPlans/TestData/Dashboard_Test_Accounts.md`,
which is gitignored. Signing in with `curl` needs a browser User-Agent: nop treats a crawler-like agent as its
search-engine account and ignores the sign-in cookie.

### 8.2 Phase 2 record

**Built:**

- `Services/Periods/DashboardPeriodCalculator`: a period becomes local dates, start inclusive, end exclusive. The
  financial year starts in `FinancialYearStartMonth` (an out-of-range value reads as July). Local midnights become UTC
  with the offset of that date, so a daylight-saving change is handled. *Last 12 months* is eleven whole months plus
  the current one.
- `Services/Queries/DashboardQueryService` (`IDashboardQueryService`): one method per figure, each running one SQL
  statement from `DashboardSql` through `INopDataProvider.QueryAsync`, the way CW.Core runs its procedures. Each figure
  is cached for 10 minutes (`CW.dashboard.{figure}.{store}-{branch}-{period start}-{period end}-{delay days}`).
- **Why raw SQL rather than LINQ:** the monthly chart needs SQL Server's `AT TIME ZONE` to place each order in its
  local month, and the category breakdown needs a recursive CTE to find each category's top-level parent.
- **Time zone:** nop's store time-zone setting (`DateTimeSettings.DefaultStoreTimeZoneId`), else the server's own
  zone, as nop itself falls back. *Locally neither the store nor the instance has one set*, so the dashboard runs on
  the server's zone (US Eastern on this machine). A live site needs the store's time zone set.

**What the figures mean (in addition to O1–O5):**

| Figure | Definition |
|---|---|
| Spend line | An order item's price excl. tax, plus its share by value of the order's shipping excl. tax less its subtotal discount. Orders that count: not deleted, not cancelled, not refunded or voided. Every spend figure sums these lines, so tile, monthly chart and both breakdowns agree to the cent. The order-total discount (`Order.OrderDiscount`) is not deducted: nop stores it incl. tax only |
| Replacement lines | An exchange's replacement item (`ReturnRequestItem.OrderItemId`) is left out of the lines and of the order value its shipping is shared over |
| Branch of a line | The packname snapshot (`PackNameOrderItem.BranchId`), else the customer's current branch (O3). A line with neither is in the *null* slice ("Other" on the page) |
| Category | The top-level parent of the product's first category by display order. Deleted categories are skipped; a product in none is in the *null* slice |
| Open / delayed orders | Pending or processing now / those not shipped or partly shipped and placed more than `DelayedOrderDays` ago. Not bound to the period |
| Employees | Active packnames that stand for one person (group packnames left out) in the store's live lists, or the branch's lists. Null while PackName is off |
| Subsidy overview | Global and product-group credit with top-ups, used net of returns, from the two credit views, so the **current ordering periods**, not the dashboard period. Null while Subsidy is off |
| Latest orders | The newest orders with a counted line in scope, whatever the period; their spend is that of their lines in scope |

**Checked against the local data (store 2, FY to date):** spend 157.28 over 4 orders, the same as an independent
order-level sum (goods 130.00 + shipping 27.28; the cancelled order left out). The categories (101.22 + 33.03 + 23.03),
the branch and the monthly figure add up to the same total. Open orders 4 and employees 4 agree with direct counts.
Delayed orders are 0 with the 3-day threshold and 4 with the cut-off moved to now (all four are unshipped). Branch 1
gives zeros, and branch 2 gives everything, including two plain orders through their customer's branch. **Not checked
against data:** the subsidy overview (no assignment in an open ordering period locally) and the replacement-line
exclusion (no exchange has a replacement order locally).

**Tests (`Tests/CW.Tests/CW.Dashboard`, 33 cases):** the period maths (financial year either side of July, its first
day, another start month, invalid months, months listed, Sydney's two offsets and the skipped hour); the section-settings
reading (unreadable values, merge, unknown names, case, unknown state, round trip); and the scope rule (all stores, a
manager whatever is requested, no packname, a non-manager, a list with no store, no permission). The SQL itself is not
unit-tested: it needs a database.

### 8.3 Phase 3 record

**Built:**

- **One class per section** (`Areas/Admin/Factories/Sections/`, interface `IDashboardSection`: `SystemName`,
  `ViewPath`, `IsAvailableAsync`, `PrepareModelAsync`), all registered in `NopStartup`. They are factories, not
  services, because they build view models (§5.1). The page shows a section when the settings switch it on, a class
  exists for it, and `IsAvailableAsync` is true: Employees needs PackName on, Subsidy overview needs Subsidy on.
- **The cards load by AJAX:** `CWDashboard/Section?section=…&storeId=…&branchId=…&period=…` returns the section's
  partial view (`Views/Dashboard/Sections/_*.cshtml`). The store is decided again on every call, exactly as for the
  page. An unknown, switched-off or unavailable section answers 404. Each card loads on its own, so one slow or failing
  section doesn't hold up the rest; a failure shows a line of text in that card.
- **Charts:** Chart.js 4.5.1, the copy nop ships for its own dashboard (`~/lib_npm/chart.js/chart.umd.min.js`).
  - *Monthly spend* is one blue series, a line with an area beneath it; every month of the period is on the axis, with
    a hidden table of the same data for screen readers.
  - *Spend by site* is HTML bars in one colour: each site's name and value sit on one line with the bar beneath. The
    four largest sites are kept, and the rest plus lines with no site fold into a grey "Other".
  - *Spend by category* is a doughnut: the five largest categories plus a grey "Other", with a legend listing name,
    amount and share.
  - The palette is the data-viz reference palette in light mode (the admin area has no dark mode), defined once as CSS
    custom properties in `dashboard.css` and read by the script. Categorical colours go in fixed order, never cycled.
    The palette validator was not run: Node is not installed on this machine.
- **Amounts** are in the primary store currency with no tax suffix, since the page header says once that amounts are
  excl. tax. **Dates** follow the admin language's culture, like nop's own grids: 11/18/2026 with the local en-US
  language, 18/11/2026 with an en-AU one.
- **Order links** (order numbers, *View all orders*, *View orders*) are drawn only for a user who holds nop's
  `Orders.OrdersView`; a store manager gets plain text.
- **Status colours** are kept for status: order badges (pending = warning, processing = info, complete = success,
  cancelled = danger, always with the status name) and the attention bar (an icon and a sentence).
- `plugin.json` went to `DATA_0.3`: the update installs the new text and deletes the "Coming soon" placeholder resource.

**Trap met (2026-11-19):** a build that fails on the locked DLL (`MSB3027`, site running) still copies the new
`plugin.json`. A site started between that failed build and the next good one runs the **old** DLL's `UpdateAsync`
under the **new** version number, so the new resources are never installed and nop does not run the update again. The
fix was another version bump (`DATA_0.2` → `DATA_0.3`). Stop the site before building a plugin whose version changed.

**Checked (local site, store 2, FY to date):**

| # | Result |
|---|---|
| T6 | Pass. The page shows the phase 2 figures: spend $157.28 (4 orders); 4 open, 0 late to ship; 4 active packnames; monthly Jul–Oct $0.00, Nov $157.28; one site ($157.28); Clothing & Apparel $101.22 (64%), Eye & Face Protection $33.03 (21%), Respiratory Protection $23.03 (15%); the 4 latest orders with their packnames ("TEST Alice +1"), site, total and status |
| T8 | Pass. Store 3 (PackName and Subsidy off) shows neither *Employees* nor *Subsidy overview*; stores 1, 2, 4 and 5 show both |
| T4 / T5 again | Pass. As the manager, `Section?…&storeId=3` and `storeId=1` return store 2's figures; the manager's cards carry no order links; an unknown section answers 404 |
| Layout | No horizontal scroll at 375 px (cards stack) or at 1366 px (the latest-orders table fits its card) |
| T7 | Branch filter: covered by the phase 2 SQL checks (branch 1 zeros, branch 2 everything); in the browser only "All sites" was used |
| T10, T12 | Not run (T10 is phase 4; T12, the 10-minute cache, was not timed) |

**Tests:** 6 more (`DashboardFormatterTests`): folding into "Other", percentages and bar lengths, no "Other" when
nothing is left over, spend with no name alone, no spend, and month labels. Dashboard total 39; CW.Tests 1,314, all
passing.

### 8.4 Phase 4 record

**Built:**

- **The user's choices** are kept in the customer's generic attribute `CW.Dashboard.SectionStates` as JSON:
  section → `Expanded`, `Collapsed` or `Hidden` (`Services/Preferences/DashboardPreferenceService`). They are per
  customer and apply in every store, since a section is the same section everywhere. Unknown sections and states are
  refused on save and dropped on read. The attribute is deleted when no choice is left.
- **The store's rule bounds the choice** (`DashboardSectionView.Resolve`):
  - *Always show* ignores every choice and has no controls.
  - *Expand* and *Collapse* have a collapse control, and the user's last collapse or expand wins over the default.
  - Only *Optional for user to hide* has the hide control, and a stored *Hidden* is honoured only under that rule. If
    the store later takes the option away, the section shows again; the choice is kept, not deleted, so it applies
    again if the option comes back.
- **The page** draws each card from the resolved view (`Collapsed`, `CanCollapse`, `CanHide` replace the old `State`)
  and leaves hidden sections out. When the user has hidden any, it offers *Show hidden sections (n)*, which brings
  them all back (`ShowHiddenSections`, POST) and returns to the same filters; collapse choices stay.
- **Saving:** AdminLTE's card widget fires `collapsed.lte.cardwidget`, `expanded.lte.cardwidget` and
  `removed.lte.cardwidget` on the card. The script posts each to `SaveSectionState` with nop's
  `addAntiForgeryToken()`; the page now renders a token for it.
- `plugin.json` went to `DATA_0.4` for the new text (*Show hidden sections ({0})*).

**T10 (local site, store 2 given a test rule table: Spend by site = Collapse, Latest orders = Optional for user to
hide):**

| Step | Result |
|---|---|
| Rules on first load | Always-show sections have no controls; Spend by site starts collapsed; Latest orders has collapse and hide |
| Administrator expands Spend by site, collapses the attention bar, hides Latest orders | Stored for customer 1 as `{"SpendByBranch":"Expanded","AttentionBar":"Collapsed","LatestOrders":"Hidden"}`; after a reload the page shows exactly that, plus *Show hidden sections (1)* |
| *Show hidden sections* | Latest orders back, the attention bar still collapsed, filters kept, the button gone |
| Manager hides Latest orders | Stored for customer 63 only; the manager's page leaves it out and offers the button; the administrator's choices are untouched |
| Bad saves | Unknown section, unknown state, or no antiforgery token: 400 |

The test rule table and the test choices were removed afterwards.

**Tests:** 11 more (`DashboardPreferenceTests`): the resolution rules (always show, defaults, the user's choice
winning, hide only where allowed) and reading the stored value (unreadable values, unknown names and states, case,
round trip). Dashboard total 50; CW.Tests 1,325, all passing.

**Found outside this plugin:** the SSO plugin's four admin menu links (`ExternalAuth.SSO/Infrastructure/EventConsumer.cs`)
carry no permission names, so they show to any admin-area user, the manager included. The pages themselves refuse the
manager; only the links are wrong.
2. **Scope and queries**: `DashboardScopeService`, `DashboardQueryService`, unit tests for the period maths and the scope rules.
   *Built 2026-11-19; 33 unit tests pass; queries checked against the local data (see §8.2).*
3. **Sections v1**: tiles, monthly spend, spend by branch, spend by category, attention bar, subsidy overview, latest orders.
   *Built 2026-11-19 (`2026_11_19_DATA_0.3`); checked in the browser and as the manager (see §8.3).*
4. **Manager access**: per-customer collapse state, test on a manager account.
   *Built 2026-11-19 (`2026_11_19_DATA_0.4`); T10 passes as administrator and manager (see §8.4).*
5. Reference document, CLAUDE.md §3.2 row, test data notes.

## 9. Test plan

| # | Case | Expected |
|---|---|---|
| T1 | Install the plugin on a site with CW.Core | Installs; menu items appear under CW Admin; permissions exist |
| T2 | Install without CW.Core | nop refuses (DependsOnSystemNames) |
| T3 | Administrator opens the dashboard, picks store 1 and then store 2 | Figures change with the store; the picker is visible |
| T4 | Manager (only *ViewOwnStore* + admin access) signs in to admin | Lands on nop's admin home as before (D8); the menu shows *CW Admin → Dashboard*; the dashboard has no store picker; other admin pages are refused |
| T5 | Manager changes the store id in the request | Ignored; the manager's own store is used |
| T6 | Figures against SQL on the test data | Each tile and chart matches a hand-written query (FY to date and This month) |
| T7 | Branch filter | Spend by branch, tiles and latest orders follow the branch |
| T8 | Subsidy off for the store; PackName off | The Subsidy overview disappears; the Employees tile disappears; nothing errors |
| T9 | Settings: turn a section off at store scope only | Gone in that store, still shown in the others; *Restore to defaults* brings it back |
| T10 | `AlwaysShow` vs `Collapse` vs `UserMayHide` | No collapse button / starts collapsed / manager can hide it, and the choice persists across sign-ins |
| T11 | A cancelled order and an exchange return order in the period | Neither counts in spend |
| T12 | 10-minute cache | A new order shows within 10 minutes; the page does not query again on each reload |
