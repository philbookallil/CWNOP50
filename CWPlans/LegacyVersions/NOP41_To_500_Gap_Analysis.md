# nopCommerce 4.1 (TIG) → 5.00 (CW.Core) — what 4.1 could do that 5.00 cannot

**Date:** 2026-11-04. **Owner:** Eduardo. **Status:** inventory — every item needs a decision; nothing is scheduled.

**What this is.** The list of every ability of the 4.1 TIG plugins (`NOP41/`) that CW.Core on nopCommerce 5.00 does
not have, with what nop 5.00 now does natively, so each one can be taken one by one: port it, redesign it, or record it
as not needed. It was built by reading all seventeen 4.1 plugins in `NOP41/` (TIG.Base, .MultiStore, .BulkOrdering,
.Exchanges, .CreditNotes, .Subsidy, .CategoryStore, .TopicStore, .CustomCustomer, .DataImport, .Enquiry, .Freight,
.Sizes, .HubSpot, .Jim2, .NABPayment, .NAB.XMLPayment) against `Plugins/Nop.Plugin.CW.Core` and the nop 5.00 source
(`Libraries/`, `Presentation/`).

**What it is not.** Not a design and not a spec: when an item is taken up it gets its own document in `CWPlans/`, and
the item here is marked with its decision. Subsidy (rewritten from scratch) and bulk ordering (based on 4.6) are compared
**by ability, not by code** — a different shape is not a gap. Internal mechanics that 5.00 deliberately replaced (stored
mutable balances, history deleted on undo, credit notes before subsidy…) are recorded in §8 and are not gaps.

**Relation to other documents.**
- `CWPlans/NOP41_Gap_Rounds_Plan.md` is the order the items are taken in (rounds), what is done and where the next
  session starts.
- `CWPlans/Subsidy_PackName_Open_Items.md` §2 is the earlier, smaller 4.1 / 4.6 audit (2026-10-21). Every item of it
  that concerns 4.1 appears here with its number (`OI 2.x.x`); this document is the complete 4.1 list. When an item is
  decided, record it in both places (or remove it from Open Items, which is kept clean).
- `CWPlans/LegacyVersions/Legacy_NOP41_Exchanges_Subsidy_Reference.md` describes how 4.1 exchanges / credit notes /
  subsidy work; `LegacyVersions/BulkOrdering_NOP41_Improvements.md` what 4.1 bulk ordering did better than 4.6;
  `BulkOrdering_Future_Improvements.md` (F-numbers) the bulk ordering backlog.

**Evidence.** Paths are relative to the repository root. `41/` = `NOP41/Nop.Plugin.TIG.Base/Nop.Plugin.TIG.Base/`;
other 4.1 plugins are named by folder (`NOP41/Nop.Plugin.TIG.MultiStore/…`); `TIG.Subsidy/…` and `TIG.Exchanges/…` stand for
`NOP41/Nop.Plugin.TIG.Subsidy/Nop.Plugin.TIG.Subsidy/…` and `NOP41/Nop.Plugin.TIG.Exchanges/Nop.Plugin.TIG.Exchanges/…`. `CW/` = `Plugins/Nop.Plugin.CW.Core/`.
Line numbers are those of 2026-11-04 and are a pointer only. Items marked **[unverified]** were not settled by reading.

**Status words.**

| Word | Meaning |
|---|---|
| **Missing** | 4.1 had it, 5.00 has nothing doing the same |
| **Partial** | 5.00 does part of it; the missing part is named |
| **nop 5.00** | nop 5.00 does it natively (§6) — no CW work, maybe configuration or data |
| **Redesigned** | 5.00 does the same job another way, by decision (§8) |
| **Integration** | Belongs to an external system 5.00 does not use (Jim2, HubSpot, Zendesk, Punchout…) — §7 |
| **Dead in 4.1** | Present in 4.1 code but unused or commented out there — §9 |

**Suggested priority** (H / M / L) is my reading of the business impact, to be replaced by your decision.

---

## 1. Summary

- **The store settings page you remember exists**: `TIG.MultiStore`'s **Manage Store** page (§2). It edited the store
  record, TIG's settings **and** five of nop's own settings pages (General, Payment, Shipping, Catalog, Shopping cart),
  one store at a time. 5.00 has CW's own settings on Manage Store, but none of nop's and not the store record.
- **The "order per packname" setting does not exist in 4.1 or 4.6** (§3). What 4.1 had is `HNOrderSplit`: one order
  **per branch code**, built for Harvey Norman. It is the closest thing to what the manager described and is the
  starting point for that conversation.
- **67 items are missing, partial or need a manager decision** (§4, groups A–J; K is a note for CW.Freight's owner).
  The ones I would look at first:
  - ~~the approval e-mails of a return are never sent (D1, High, OI 2.1.1)~~ — done 2026-11-09;
  - cross-store completion of an exchange is a stub (D2, OI 2.1.2);
  - returns on part-shipped orders (D3, OI 2.1.3);
  - order authorisation by a branch leader (C3, OI 2.2.1);
  - the store settings page (A1);
  - limit login to the customer's own store (B5);
  - per-store category content (G1) and size charts (G3);
  - the welcome pack (C6) and "items ordered for me" (C7);
  - the admin customer / order list filters (J2, J3) and the per-store dashboard (J1).
- **About twenty 4.1 abilities are covered by nop 5.00 natively** (§6, plus A4, C13 and G2), from per-store customer settings to topics per store, country
  per store, maximum cart items, reorder and the admin maintenance tools.
- **Jim2, HubSpot / FashionBiz, Zendesk, Punchout and the old NAB REST gateway are integrations** (§7); Odoo replaces
  the Jim2 master data, one-way, and nothing yet replaces sending orders to an ERP.

---

## 2. The 4.1 store settings page ("Manage Store") and 5.00 today

### 2.1 What 4.1 had

`NOP41/Nop.Plugin.TIG.MultiStore/Controllers/TIGSettingController.cs:665` (`Manage(int id, string activeTab)`), view
`Views/MultiStore/Setting/Manage.cshtml:78-85`. Reached from the gear "Go to Settings" on each store box of *MultiStores ›
Manage Stores* and from *Quick Stores*; route `TIGAdmin/MultiStore/Setting/{action}/{id}`; permission
`AccessMultiStoreSettings` on `Manage` and on every save except the TIG Settings tab's (`SaveTIGSettings`,
`TIGSettingController.cs:873-900`, admin only); the tab-loading GETs check only `[AuthorizeAdmin]`. Opening
it set the admin's store scope to that store. Each tab loaded by AJAX and saved on its own.

| Tab | Content | Saved |
|---|---|---|
| **Store Settings** | nop's `Store` record: name, URL, SSL, hosts, default language, display order, company name / address / phone / VAT, localised names; delete store | `_storeService.UpdateStore` |
| **General Settings** | copy of nop 4.1's General settings: store information (closed, theme, customer may select theme, **logo**, EU cookie warning), social links, contact us / terms, sitemap, SEO, security, captcha, **PDF** (invoice logo, letter size, footer columns), localisation, full-text, top-menu and footer items, admin rich editor | per store, nop's "override for this store" checkboxes (security, localisation and full-text global) |
| **TIG Settings** | the TIG settings of the store — §5 has every field | straight to the store, no override checkboxes (`41/BusinessLogic/Settings/SettingsLogic.cs:138-161`) |
| **Payment Settings** | nop's payment-method grid (active, friendly name, display order) | **global**, not per store (`UpdatePaymentMethodModel`, `TIGSettingController.cs:428-447`) |
| **Shipping Settings** | nop's `ShippingSettings` (ship to same address, pick-up in store, free shipping over X, estimate shipping, hide shipping total, bypass method selection, origin address…) | per store with overrides |
| **Catalog Settings** | nop's `CatalogSettings` (61 per-store properties: SKU display, view modes, share, compare, product sorting, tags, new products, export/import…) | per store with overrides |
| **Shopping Cart Settings** | nop's `ShoppingCartSettings` (18: display cart after adding, max cart / wishlist items, carts shared between stores, images, discount / gift card box, mini cart…) | per store with overrides |
| *Exchanges and Returns*, *Fee Settings* | added by TIG.Exchanges (`Events/Multistore*TabsHandler.cs`) | — |
| *Jim2 Divisions* | added by TIG.Jim2 | — |

The plugin's other pages are in §4.1 (A2–A6).

### 2.2 What 5.00 has

*CW Admin › MultiStores › Edit* (`CW/Areas/Admin/Controllers/MultiStoreController.cs`, `Areas/Admin/Views/MultiStore/_CreateOrUpdate.cshtml:26-64`):
**Info** (name, URL and company name, read-only — `_StoreInfo.cshtml:14, 22, 30`), Branches, Departments, Cost centres,
PackNames, Product groups, Subsidies, **Settings** (`MultiStoreSettings` with override checkboxes — `_StoreSettings.cshtml`),
Exchanges & Returns, Fee Settings. *CW Admin › Default settings* (`Settings.cshtml`) is the same Settings form for store 0.

**No 5.00 CW page shows a nop setting.** Every nop setting the 4.1 page showed still exists in 5.00 (with the
exceptions in §2.3) and is edited on nop's own *Configuration › Settings* pages after choosing the store in nop's
store-scope switch; the store record is edited on *Configuration › Stores*.

### 2.3 nop settings of the 4.1 page that changed in 5.00

| 4.1 | 5.00 |
|---|---|
| `StoreInformationSettings.TwitterLink` / `GooglePlusLink` | `XLink` / removed (5.00 adds `InstagramLink`) |
| `CommonSettings.Sitemap*` | moved to `SitemapSettings`, same names |
| `SeoSettings.DefaultTitle`, `DefaultMetaKeywords`, `DefaultMetaDescription` | moved to the `Store` record |
| `SeoSettings.EnableJsBundling` / `EnableCssBundling`, `CommonSettings.FullTextMode`, `SecuritySettings.ForceSslForAllPages`, `EnableXsrfProtection*` | removed |
| `DisplayDefaultMenuItemSettings`, `DisplayDefaultFooterItemSettings` | removed — 5.00 has menus (`Libraries/Nop.Core/Domain/Menus`) |
| `ShippingSettings.EstimateShippingEnabled` | split into `EstimateShippingCartPageEnabled` / `EstimateShippingProductPageEnabled` |
| `CatalogSettings.NewProductsNumber` | `NewProductsPageSize` and its options |
| `CaptchaSettings.ShowOnNewsCommentPage` | removed (news is a plugin in 5.00) |

### 2.4 The gap, and the shapes it could take (for the decision, not a design)

The missing ability is **one place, per store, to see and change the settings that matter for that store**, nop's
included. Options, from least to most work:

1. **Links, not copies.** A card on Manage Store listing the nop settings pages that matter, each link opening nop's page
   with the store scope already set to this store (nop's `SetStoreScopeConfiguration`). No copied nop code, so nothing to
   re-diff on upgrades (CLAUDE.md §10.3). Store record stays on nop's Stores page, or its fields become editable on Info.
2. **A read-only overview.** A "Store overview" tab showing the current value of a chosen set of nop and CW settings for
   this store — marking which are overridden for the store and which come from the default — with a link to edit each.
   This answers "where do I check all the important settings" without owning nop's forms.
3. **4.1's approach: copies of nop's settings forms inside Manage Store.** Closest to 4.1, but every copied form is
   nop code to re-diff on each upgrade, which 4.1 itself shows going stale (its forms were 4.1's and saved payment
   methods globally).

Before choosing, the manager should say **which** settings the stores' staff actually use. Item **A1**.

---

## 3. "Create an order per packname" — what 4.1 and 4.6 had

**Verdict: no version had an order per packname.** 4.1 had **one order per branch code** (`HNOrderSplit`), which is the
closest; 4.6 had nothing; 4.1's per-employee subsidy mode changes who pays, not how many orders are made.

### 3.1 4.1 `BulkOrderSettings.HNOrderSplit` — one order per branch code

- **Setting.** Per store, on/off (`41/Domain/Setting/BulkOrderSettings.cs:21`), saved by the TIG Settings tab but not
  shown on its form (§5). Until 2025-08-12 it was hard-coded for stores 173 and 184 (Harvey Norman; comment at each use).
- **Groups by** the branch code on each cart line (`PacknameCartItem.BranchCode`, matched to the branch by its **card
  code**, `GetBranchByCardCode`) — taken from the "Branch Code"
  dropdown of the bulk-order packname selector when `EnableBranchCodeField` is on, otherwise from the branch of the
  packname's list. Packnames of different branches → different orders; several packnames of one branch → one order.
- **Placement** (`41/Services/Overrides/Orders/OrderProcessingServiceOverride.cs:1169-1296`), only when the cart has
  more than one packname line (`:1183`; otherwise the normal single order): payment is taken **once**
  for the whole cart, then for each branch group: the group's lines only; its own freight from the shipping option whose
  order-total band holds the group subtotal; billing **and** shipping address = the branch's address; tax and totals
  recalculated; a new order GUID; the group's lines moved into it; credit notes then subsidy applied to it; its own
  order-placed e-mail (`%BulkOrder.BranchCode%`, `%BulkOrder.BranchName%`) and its own invoice ("Tax Invoice", branch as
  Invoice To / Ship To). **N orders, one per branch code.**
- **What the user saw.** Shipping-method step skipped (first method), payment forced to **Purchase Order** and its step
  skipped (`41/Controllers/CheckoutController.cs:195-203, 244-253, 328`); My Account orders filterable by branch code;
  the completed page showed only the **last** order.
- **Defects seen in the code** (not tested): the stored subtotal includes freight (`:1244`); discounts and gift cards
  are not recalculated per group, and discount and gift-card usage are written for every sub-order (`:1257`, `:1260`);
  each sub-order takes only lines that have a packname row (`:1193`), so a line without one is never moved into any order.

### 3.2 Related, but not a split

- **Per-employee subsidy mode** (`subsidysettings.tig.allowperemployee`, `.defaultmode`; radio buttons Managers /
  Employees in the packname selector): in-memory sub-carts per employee decide **whose subsidy** pays each line; shipping
  goes to the manager's share. One order. In 5.00 each line is always charged to its own packname (OI 2.2.5).
- **Welcome pack one-order-per-employee** (`41/Services/BulkOrders/WelcomePackService.cs:1215-1250`): dead code — every
  caller commented out (§9).
- **4.6** places one order (or one approval pre-order) per checkout; no split anywhere.

### 3.3 What 5.00 does

One order per checkout, with **one `CORE_PN_OrderShipping` row per packname** (own address, method, freight, status),
per-destination shipments, a packname / branch / department / cost-centre snapshot on every line, one settlement row per
order, an invoice that prints every destination and a "For" line per item (`CWPlans/PackName_Order_Shipments.md`). This
was a decision (rebuild record decisions 8 and 13).

### 3.4 What to ask the manager (item C1)

1. Split by **packname**, by **branch**, or by something else (cost centre, department)? 4.1 split by branch.
2. Why — the customer's accounting needs one invoice per branch / per employee? the warehouse? an ERP rule? If it is
   only for documents, a per-packname or per-branch **invoice / packing slip** from the one order may be enough.
3. Payment: nop takes one payment per order. 4.1 forced Purchase Order. With card or PayPal, one payment for N orders
   needs a parent / child design, or the split is offered only with offline payment methods.
4. Billing address of each order (the manager's, or each branch's), order-level discounts and gift cards (split or on
   one order), and the completed page listing every order.

Conceptually 5.00 is closer to it than 4.1 was: the settlement pipeline already works per order and the shipping data
is already per destination; `PlacePackNameOrderAsync` would loop over groups. It reopens the single-order decisions,
so it is its own phase.

---

## 4. The gap list

Columns: **#** · **4.1 ability** · **Evidence** (4.1) · **5.00 status** · **Pri** (suggested) · **Notes / decision**.

### 4.1 A — Store management and settings (TIG.MultiStore)

| # | 4.1 ability | Evidence | 5.00 status | Pri | Notes / decision |
|---|---|---|---|---|---|
| A1 | **Manage Store page with nop's settings and the store record** (§2) | `TIG.MultiStore/Controllers/TIGSettingController.cs:665`; `Views/MultiStore/Setting/Manage.cshtml:78-85` | **Partial** — CW settings, exchanges and fees are there; nop's settings and the store record are not | M–H | §2.4 options; ask which settings staff use |
| A2 | **Quick Stores** — a store picker that jumps to that store's orders or its Manage page | `TIG.MultiStore/…TIGHome.QuickStores`, `QuickStores.cshtml:43` | **Missing** | L | nop's store filter on each list does the job one page at a time |
| A3 | **Store boxes home with counts, search, sort and pinned favourite stores** | `TIGHome.Index`, `TIGStore.PinStore` | **Partial** — the MultiStores grid, no pinning, no counts | L | Useful with ~85 stores |
| A4 | **Store › Orders / Shipments / Returns / Carts / Catalogue pages** per store, bulk packaging slips | `TIGStore.Orders`, `Shipments`, `PdfPackagingSlipAll`, `Returns`, `Carts`, `StoreCatalog` | **nop 5.00** (store filter on each list; nop prints packaging slips in bulk) | — | Only the navigation from the store page is lost |
| A5 | **Store Content** — per-store tick boxes of which topics and message templates a store uses | `TIG.MultiStore/Controllers/StoreContentController.cs:135` | **Partial** — nop 5.00 store mapping on each topic / template; no per-store matrix screen | L | |
| A6 | **SKU export / import per store** (with generate / clean attribute combinations) | `TIGStore.ExportSKUs`, `ImportSKUs` | **Missing** (Odoo syncs products) | L | Check whether Odoo makes it unnecessary |
| A7 | **Restrict a store's products to registered users** (setting that writes a Registered ACL row on every product) | `41/Services/Stores/TIGStoreService.cs:290` | **Missing** — doable by hand with nop ACL | L | |

### 4.2 B — Customers, branches, staff, login

| # | 4.1 ability | Evidence | 5.00 status | Pri | Notes / decision |
|---|---|---|---|---|---|
| B1 | **Employee changes their own branch** on My Account and at checkout (two settings) | `41/Controllers/CustomerController.cs:172-230`; `41/BusinessLogic/Branches/BranchesLogic.cs:356-429` | **Missing** — only an admin sets the branch | L–M | In 5.00 the branch follows the packname; a change moves the packname (CLAUDE.md §14.2) |
| B2 | **Branch addresses in My Account › Addresses**, flagged as branch addresses | `41/Factories/TIGAdminCustomerModelFactory.cs:441-470` | **Missing** | L–M | With OI 2.1.13 (4.6's packname addresses there) |
| B3 | **Recover deleted customers / staff / branches** | `TIG.MultiStore` `RecoveryCustomers`, `Staff.Recovery`, `Branches.Recovery` | **Missing** | L | Repair tools |
| B4 | **Branch: move to another store; delete with "move staff to"; allocate unallocated staff; change branch of selected staff** | `41/BusinessLogic/Branches/BranchesLogic.cs:55-438`; `TIG.MultiStore` `Staff.*` | **Partial** — CRUD, addresses, hierarchy and the delete rule are there | L | |
| B5 | **Limit login to the customer's own store** (setting; hard-coded exceptions for stores 164 / 166, "TIG Direct", "Capsule Collection"). The condition at `:249` also applies it, setting off, to every customer without the impersonation permission — so 4.1 always enforced it for ordinary customers | `41/Services/Overrides/Customer/CustomerRegistrationServiceOverride.cs:222-256` | **Missing** — nop 5.00 has no such check | M–H | With OI 2.2.5 (`RestrictLoginToDomain`, 4.6). Each store is its own domain: decide whether one account may sign in to two stores |
| B6 | **Employee ID login / registration field with a per-store label** | `41/Filters/CustomerActionsFilter.cs:85-93`; key `customersettings.tig.employeeenabled` | **Redesigned** — `PackName.EmployeeNumber`, `EmployeeNumberEnabled`; no login by employee ID, label not per store | L | |
| B7 | **Customer attributes limited to stores** | `41/Services/Customers/TIGCustomerService.cs:1997` | **Missing** — nop 5.00 customer attributes have no store mapping | L | Only if stores use different registration fields |
| B8 | **Roles installed by the plugins**: TIGAdministrators, Inventory, Accounts, Sales, CustomerAdmin, BranchStaff, BranchManager, PO, OrderingOfficer | `41/TIGBasePlugin.cs:249-430`; `TIG.MultiStore/TIGMultiStorePlugin.cs:290-310` | **Missing** — 5.00 creates only `DEP_` department roles | L | With J4 (permissions) |
| B9 | **Storefront customer upload in delete mode** (delete customers listed in an Excel file) | `41/Controllers/CustomerUploadController.cs:122-327` | **Missing** (5.00 imports create only; bulk delete is in the admin packname grid) | L | 4.1 had **no permission check** on this controller (`:126-127` commented) |

### 4.3 C — Ordering, checkout, bulk ordering

| # | 4.1 ability | Evidence | 5.00 status | Pri | Notes / decision |
|---|---|---|---|---|---|
| C1 | **One order per branch code** (`HNOrderSplit`) — and the manager's "order per packname" | §3 | **Missing** (one order, a destination per packname — by decision) | ask | OI 2.2.2 — **do not close it as superseded** until the manager answers §3.4 |
| C2 | **Per-employee / manager subsidy mode** for a manager's cart | `41/Helpers/ShoppingCart/ShoppingCartHelper.cs:400-470`; `TIGSubsidySettings.cs:10-11` | **Redesigned** — each line's packname pays | ask | OI 2.2.5. Ask whether a manager ever paid from his own subsidy for staff |
| C3 | **Order authorisation** — for Purchase Order payments, a branch leader approves / declines the order by e-mail link; per branch `AuthorisationRequire`, `ApprovalEmail` (the leaders), `AuthorisationThreshold`; 90-day resend task (`OrderAuthorizationSettings.LeaderEmailAddresses` is stored but read by nothing) | `41/Services/Orders/OrderAuthorizationService.cs:77-240`, `Controllers/OrderAuthorizationController.cs`, `Tasks/OrderAuthorizationTask.cs`; trigger `41/Services/Jim2/Jim2LinkerService.cs:245-266` from `Events/Handlers/OrderEventsHandler.cs:102-110` | **Missing** (4.6 had approval pre-orders, also missing) | ask (M) | OI 2.2.1 |
| C4 | **Branch-code field on the bulk add** (`EnableBranchCodeField`) | `NOP41/Nop.Plugin.TIG.BulkOrdering/Views/BulkOrders/Frontend/_PacknameSelector.cshtml:149-154` | **Missing** — the branch comes from the packname's list | L | Only needed for C1 by branch |
| C5 | **PO number / comment per bulk add**, stored on the cart and order line, with a configurable field title | `41/Domain/BulkOrder/PacknameOrderItem.cs:29-32`; `BulkOrderSettings.EnablePOCommentField`, `POCommentFieldTitle` | **Redesigned** — `PackName.POEntry` is a packname field; no per-line PO; title not configurable | L–M | An order-level PO fits nop's **checkout attributes** (native, rendered by the packname checkout). F9 |
| C6 | **Welcome pack** — Excel of employees / packnames with SKU, size, colour, quantity, branch, comment: creates the users or packnames **and fills the manager's cart**; error workbook; pending list; cancel / re-run; settings `UseExistingUsersWelcomePack`, `ShowWelcomePackTemplate` | `NOP41/Nop.Plugin.TIG.BulkOrdering/Controllers/WelcomePackController.cs:55-575`; `41/BusinessLogic/BulkOrdering/WelcomePackLogic.cs:154-330` | **Missing** as an ordering feature — 5.00's packname import creates packnames and accounts, no cart lines | M (ask) | New starters' uniform packs; ask the manager |
| C7 | **"Items ordered for me"** — a packname's account sees the order items a manager placed for them; admin "items ordered on behalf" | `NOP41/Nop.Plugin.TIG.BulkOrdering/Controllers/BulkOrderController.cs:1392-1433`, `BulkOrdersAdminController.cs:93-108` | **Missing** (My Subsidies shows the employee's orders as text; OI 4.1 is the manager side) | M | |
| C8 | **Staff badge product** — one cart line per name from a `\|`-separated list, quantity 1 each | `NOP41/Nop.Plugin.TIG.BulkOrdering/Controllers/BulkOrderController.cs:911-1100` | **Missing** | ask | The product type came from a modified 4.1 core **[unverified]** |
| C9 | **My Account order list filtered by branch code** | `41/Factories/TIGOrderModelFactory.cs:199-211` | **Missing** | L | |
| C10 | **Returned-items-only cart: no freight, shipping step skipped** | `41/Controllers/CheckoutController.cs:145-208`; `OrderTotalCalculationServiceOverride.cs:184-243` | **Missing** for the cart the customer completes | M | OI 2.1.4 / manager question Q15 |
| C11 | **Yearly quantity cap by product tag per store; "the free product must be removed first"** (Goodstart / Stride / TEEG, hard-coded) | `41/Services/Overrides/Orders/ShoppingCartServiceOverride.cs:735-805` | **Missing** (`RestrictToAllocatedQty` is per subsidy) | ask | OI 2.2.3 |
| C12 | **Bulk screen: gender filter, multi-select categories** | `NOP41/Nop.Plugin.TIG.BulkOrdering/Controllers/BulkOrderController.cs:696, 709` | **Partial** (4.6-based grid) | L | F6–F8 in `BulkOrdering_Future_Improvements.md` |
| C13 | **Max bulk order lines** for the whole cart (`MaxBulkOrderItems`) | `41/BusinessLogic/BulkOrdering/BulkOrdersLogic.cs:171-179` | **Partial** — `ShoppingCartSettings.MaximumShoppingCartItems`, which CW applies **per packname basket** (`CW/Services/Orders/ShoppingCartOverrideService.cs:468-500`), not to the whole cart | L | Close unless a whole-cart cap is wanted |
| C14 | **Hide branch addresses at checkout** (`TIGStoreSettings.HideBranchAddress`) and **roles shown the shipping-address step** (`SelectedDisplayRoleIds`) | `41/Controllers/CheckoutController.cs:557-601` | **Missing** (`DoNotPreSelectShippingAddress` is different) | L | OI 2.2.5 |

### 4.4 D — Exchanges and returns

| # | 4.1 ability | Evidence | 5.00 status | Pri | Notes / decision |
|---|---|---|---|---|---|
| D1 | **Approval e-mails** `ReturnAuthorisedPayment` / `ReturnAuthorisedExcepted` | `TIG.Exchanges/TIGExchangesPlugin.cs:232-260` | **Done** 2026-11-09 — `CWCorePlugin` installs both templates (install and update); an undone approval withdraws its unsent e-mail | **H** | OI 2.1.1 (closed). `Deprecated/R1A_Return_Emails_Invoice_Spec.md` |
| D2 | **Complete an exchange for a customer of another store** (cross-store impersonation by token) + daily expired-token cleanup | `41/Controllers/ImpersonationAPIController.cs`; `41/RouteConfig.cs:76-92`; `TIG.Exchanges/Tasks/ClearExpiredImpersonationTokensTask.cs` | **Missing** — a stub that redirects to a route nobody serves | H (if multi-store support is real) | OI 2.1.2 — port, or delete stub + table |
| D3 | **Return allowed on a Processing order with shipped items; window counted from the last shipment** | `41/Services/Overrides/Orders/OrderProcessingServiceOverride.cs:1407-1454` | **Partial** — nop 5.00's `ReturnRequestSettings.ReturnRequestsForCompletedOrdersOnly` (off) lets any order status return, but without the shipped-items check, and the window still counts from `CreatedOnUtc` (`Libraries/Nop.Services/Orders/OrderProcessingService.cs:3166-3172`) | **H** | OI 2.1.3 |
| D4 | **Admin return list: first / last name filters and a Customer column** | `41/Controllers/Admin/AdminReturnRequestController.cs:43` | **Done** 2026-11-09 (decided: dead code removed, name filters not ported) — the list is nop's own (Customer column, number / status / date filters); CW's list override, `CWReturnRequestService` and `CWReturnRequestSearchModel` are deleted | M | OI 2.1.5 (closed). `Deprecated/R1B_Admin_Returns_Picker_Spec.md` |
| D5 | **Return-edit Action dropdown filtered by the chosen reason** | `TIG.Exchanges/Controllers/ReturnsAdminController.cs:374` | **Done** 2026-11-09 — the Requested action dropdown lists what the store offers for the chosen reason and reloads when it changes; the save refuses an action the reason does not offer (the saved pair always allowed) | L | OI 2.2.6 (closed). `Deprecated/R1B_Admin_Returns_Picker_Spec.md` |
| D6 | **Rules removed when a return reason / action is deleted** | `TIG.Exchanges/Events/ReturnsEventsHandler.cs` | **Done** 2026-11-09, and stricter than 4.1 — the delete is refused while any return uses the reason / action; otherwise its rules and links are deleted with it | L | OI 2.1.12 (closed), the clean-up half of B7. `Deprecated/R1B_Admin_Returns_Picker_Spec.md` |
| D7 | **Packname on the return e-mails** (`%BulkOrder.Packname%`) | `41/…/MessageTokenProviderOverride.cs:1153-1160` | **Done** 2026-11-09 — `%PackName.Name%` on `Return.Receipt` (screen and PDF) and both approval e-mails, the packname of the returned line | M | OI 2.1.6 (closed). `Deprecated/R1A_Return_Emails_Invoice_Spec.md` |

### 4.5 E — Credit notes

Nothing missing. 4.1's manual note, customer tab, history, undo and liability report are all in 5.00; the
"Subsidy and credits for me" page became two pages (store credit, My Subsidies); 4.1 had no expiry.

### 4.6 F — Subsidy (compared by ability)

| # | 4.1 ability | Evidence | 5.00 status | Pri | Notes / decision |
|---|---|---|---|---|---|
| F1 | **Subsidy report to Excel** (per subsidy and per customer) | `TIG.Subsidy/Controllers/SubsidyController.cs:1914`; `TIG.MultiStore` `ExportCustomerSubsidyReportToExcel` | **Missing** | M | OI 2.1.11 (pattern: the packname Excel export) |
| F2 | **Product-group usage report** (% used per group) | `TIG.Subsidy/Controllers/SubsidyController.cs:147` | **Missing** | L | |
| F3 | **All-stores subsidy list with totals** (allocated / consumed / %), store filter | `TIG.Subsidy/Controllers/SubsidyController.cs:169-237` | **Partial** — per-store grid with total and % used | L | |
| F4 | **Bulk remove of subsidy assignments; search assignees by gender** | `TIG.Subsidy/Controllers/SubsidyController.cs:380-430, 1959-2056` | **Partial** — bulk add, remove one at a time, no gender filter | L | Gender filtering was removed on purpose (phase 5 Part 2) |
| F5 | **Subsidy top-up import** (Excel of top-ups for existing users) | `NOP41/Nop.Plugin.TIG.DataImport/Controllers/DataImportController.cs:752-925` | **Missing** — top-ups one at a time; the packname import sets credit only on registration | M | Useful at season start |
| F6 | **"Not enough subsidy, you will pay $X" warning on the product page's add-to-cart** (`EnableProductSubsidyCheck`) | `TIG.Subsidy/Views/…/SubsidyAddToCartOverride.cshtml:17-107` | **Partial** — the bar, the caps and the checkout breakdown carry the information; no product-page warning | L | |
| F7 | **Subsidy tokens** `%Subsidy.StartDate/EndDate/AvailableCredit/AvailableUnits%` in the welcome e-mail | `41/…/MessageTokenProviderOverride.cs:1112-1213` | **Done** 2026-11-09 — one list token, `%Subsidy.Details%`: each active subsidy with its season, credit and units | L–M | OI 2.1.7 (closed). `Deprecated/R1A_Return_Emails_Invoice_Spec.md` |
| F8 | **Subsidy end-date reminder e-mail** (a rule for SevenSpikes Customer Reminders) | `41/Services/Reminders/SubsidyEndReminderRule.cs` | **Done** 2026-11-10 — subsidy expiry reminders: a card on *Promotions › Reminders* (switch per store, start date, three follow-ups) and the hourly task `ProcessSubsidyExpiryRemindersTask` | M | `subsidy-system.md` §15; `Deprecated/Subsidy_Expiry_Reminders_Spec.md` |
| F9 | **Storefront manager reports (CSV)**: subsidy by store / branch, sales by style, sales by branch, stock on hand | `41/Controllers/CustomerController.cs:1613-1760`; `TIG.DataImport/Controllers/DataImportController.cs:1359-1420` | **Missing** | M (ask) | The stored procedures are not in the repository **[unverified]** |

Rollover, change of subsidy with transfer of remains, one active assignment, per-customer assignment and the subsidised
category filter are **Redesigned** (§8), not gaps.

### 4.7 G — Catalogue and content

| # | 4.1 ability | Evidence | 5.00 status | Pri | Notes / decision |
|---|---|---|---|---|---|
| G1 | **Category per store** — per-store description, picture, show on home page, include in top menu (`CategoryStore`) | `NOP41/Nop.Plugin.TIG.CategoryStore`; `41/Services/Overrides/Catalog/CategoryServiceOverride.cs:146-504` | **Missing** — nop 5.00 store mapping controls only visibility | M | With ~85 stores sharing one catalogue |
| G2 | **Topic per store** — per-store override of ~17 topic fields | `NOP41/Nop.Plugin.TIG.TopicStore`; `41/Services/Topic/TIGTopicService.cs:45-74` | **nop 5.00, different shape** — one topic per store with the same system name, store-mapped | — | Close as covered; it is configuration |
| G3 | **Size charts** — size groups, measurements, chart wizard (variants × measurements, picture, comment), assign products / attributes, convert attributes, merge duplicates, **chart on the product page** | `NOP41/Nop.Plugin.TIG.Sizes/Controllers/SizesController.cs`; `Components/SizesWidgetsViewComponent.cs:44-90` | **Missing** — nop 5.00 has nothing like it | M (ask) | A plugin of its own (`CW.Sizes`?) rather than CW.Core. **5.00.1:** with *Allow JavaScript in rich editor* off, saving a product in admin strips `<table>` from its description, so a size chart kept as a table in the description would not survive an admin save. Decided 2026-11-12: the setting stays on for now (`Nop_5.00.1_Upgrade_Record.md` §4) |
| G4 | **Embroidery codes** per attribute value / combination, in the SKU export | `41/Controllers/Admin/AdminProductController.cs:1304-1351`; `41/Domain/Category/Embroidery.cs` | **Missing** **[unverified whether Odoo brings them]** | ask | |
| G5 | **Custom Contact Us fields per store, with an enquiry log and report** | `NOP41/Nop.Plugin.TIG.Enquiry/Controllers/EnquirySettingController.cs` | **Missing** — the live 4.1 path posted to **Zendesk** and the custom fields were commented out | L | §7 for Zendesk; nop's Contact Us + `Widgets.Jotform` might do **[unverified]** |
| G6 | **Newsletter subscriptions active immediately** (`NewsletterAutoActive`) | `41/Controllers/TIGNewsletterController.cs:90` | **Missing** (nop's double opt-in) | L | |

### 4.8 H — Imports, exports and reports (admin)

| # | 4.1 ability | Evidence | 5.00 status | Pri | Notes / decision |
|---|---|---|---|---|---|
| H1 | **Packnames import: update and delete** as well as create | `NOP41/Nop.Plugin.TIG.DataImport/Controllers/DataImportController.cs:1148-1356` | **Partial** — 5.00 imports create only; updates and deletes go through the grid's bulk actions | L | |
| H2 | **Users import extras**: customer-attribute columns, newsletter, delayed welcome e-mail, Google address check; **users update** import | `DataImportController.cs:111-750` | **Partial / Missing** | L | |
| H3 | **Branches import / update / export (Excel)** | `NOP41/Nop.Plugin.TIG.DataImport/Controllers/BranchImportController.cs:76-742` | **Integration** — Odoo syncs branches; no Excel | L | Only if a store's branches do not come from Odoo |
| H4 | **Branch and packname exports per store** | `TIG.MultiStore` `ExportBranches`, `ExportPacknames` | **Partial** — packname export present, branch export missing | L | |

### 4.9 I — Invoices and e-mails

| # | 4.1 ability | Evidence | 5.00 status | Pri | Notes / decision |
|---|---|---|---|---|---|
| I1 | **Branch payment terms on the invoice** | `41/Services/Overrides/Common/PdfServiceOverride.cs:955-980` | **Missing** — `Branch.PaymentTerms` is editable and read by nothing | L | OI 2.1.9 |
| I2 | **ABN and banking information** on the invoice (and a widget) | `41/…/PdfServiceOverride.cs:993-1012, 1400`; `TIGBankingInfoSettings` | **Missing** | L–M | OI 2.1.10. nop's `Store.CompanyVat` and `PdfSettings.InvoiceFooterTextColumn1/2` may hold them without code |
| I3 | **"Tax Invoice" header and "GST" label** — printed only when `HNOrderSplit` is on | `41/…/PdfServiceOverride.cs:1164-1168, 1390-1393` | **Missing** | L | Goes with C1 |
| I4 | **Order e-mail copied to the billing-address e-mail** for branch-manager orders (`OrdersUseBillingAddressEmail`) | `41/…/WorkflowMessageServiceOverride.cs:193-208` | **Missing** | L–M | OI 2.1.8 |
| I5 | **Branch tokens** `%BulkOrder.BranchCode/BranchName%`, **`%Order.PO%`** | `41/…/WorkflowMessageServiceOverride.cs:126-147` | **Missing** | L | Goes with C1 / C5 |

### 4.10 J — Admin lists, dashboard, permissions

| # | 4.1 ability | Evidence | 5.00 status | Pri | Notes / decision |
|---|---|---|---|---|---|
| J1 | **Admin dashboard per store** — store filter on order / customer statistics, average and incomplete orders, bestsellers, popular search terms | `41/Components/AdminDashboardFiltersViewComponent.cs`; `41/Controllers/Admin/AdminOrderController.cs:161-300` | **Partial** — nop 5.00's Reports pages (Bestsellers, Sales summary) have a store filter; the dashboard tiles do not | M | |
| J2 | **Admin customer list filters**: branch, gender, active, subsidy, employee ID, deleted; subsidy column | `41/Models/Customers/TIGCustomerSearchModel.cs:24-49` | **Partial** — CW adds the store filter and branch column, nop 5.00 has the active filter; branch, gender, subsidy, employee ID and deleted filters and the subsidy column are missing | M | |
| J3 | **Admin order list**: *Ordered by* and *Subsidy* columns, branch and product-name filters | `41/Models/Orders/TIGOrderSearchModel.cs:12-29` | **Partial** — E / R badges; nop has store and product filters | M–L | F17 |
| J4 | **Finer permissions** (`AccessSubsidy`, `ManageProductGroups`; 4.6 `ManagePackNames`…) | `TIG.Subsidy/Security/SubsidyPermissionProvide.cs:10-11` | **Partial** — one `AccessMultiStore` gates stores, packnames and subsidies | M (before go-live) | OI 2.2.4; with B8 |
| J5 | **Replace an order line with a same-price product** (admin) | `41/Controllers/Admin/AdminProductController.cs:452-530` | **Missing** — packname orders are blocked from edits by decision | L | OI 4.11 |
| J6 | **Customer export with the TIG filters** | `41/Controllers/Admin/AdminCustomerController.cs:984-1056` | **Partial** — nop's export ignores CW's store filter **[read from code, not tested]** | L | |

### 4.11 K — Freight (CW.Freight belongs to another developer — note only, nothing to change here)

4.1 `TIG.Freight` applied three rules that CW.Freight stores but does not apply (`Plugins/Nop.Plugin.CW.Freight/Services/FreightService.cs:84`,
`References/Freight_Plugin_Reference.md` §4): **methods by customer role**, **ApplyPerBranch** (rate × branches in the
cart) and the **order-total band**. OI 2.2.6 says the bands "are in CW.Freight" — true for storage only. To raise with
the plugin's owner; ApplyPerBranch may be made unnecessary by CW.Core's per-destination shipping.

---

## 5. Settings inventory — every 4.1 TIG setting

The TIG Settings tab of Manage Store (§2) plus settings read elsewhere. "Status" as in the legend.

| 4.1 setting | What it did | 5.00 | Item |
|---|---|---|---|
| `TIGStoreSettings.AllowHomeDelivery` | home addresses at checkout besides branch addresses | **Present** `MultiStoreSettings.AllowHomeDelivery` | — |
| `TIGStoreSettings.HideBranchAddress` | hide branch addresses at checkout | **Missing** | C14 |
| `TIGStoreSettings.SelectedDisplayRoleIds` | roles shown the shipping-address step | **Missing** | C14 |
| `TIGStoreSettings.LoginRedirectUrl` | — | **Dead in 4.1**; 5.00 lands through `IPackNameLandingService` | — |
| `customersettings.tig.allowemployeechangebranch.myaccount` / `.checkout` | employee changes own branch | **Missing** | B1 |
| `customersettings.tig.allowstaffusealternativeaddress` | staff may use another address | **Redesigned** `AllowCustomersToAddAddress` | — |
| `customersettings.tig.defaultcardcode` | default bill-to of new branches | **Integration** (Jim2 card code) | — |
| `customersettings.tig.employeeenabled` / `.employeeenabledfieldtitle` | employee ID field and label | **Redesigned** `EmployeeNumberEnabled` | B6 |
| `customersettings.tig.Limitlogintostore` | login only to own store | **Missing** | B5 |
| `customersettings.tig.googleapikey` | Google address autocomplete | **Missing / Integration** | — |
| `storesettings.tig.misc.restrictproductstoregisteredusers` | Registered ACL on all products | **Missing** | A7 |
| `storesettings.tig.misc.importproductdefaultlogic` | TIG product import logic | **Missing** (nop import, Odoo) | — |
| `storesettings.tig.misc.deliverycutofftime` | Jim2 auto-delivery cut-off | **Integration** | — |
| `BulkOrderSettings.UseStaffAsPacknames` | staff are packnames | **Present** `UseStaffAsPackName` | — |
| `BulkOrderSettings.SendManagersToBulkOrders` | managers land on bulk ordering | **Present** `BulkOrderSendUserAfterLogin` | — |
| `BulkOrderSettings.MaxBulkOrderItems` | cap the cart's lines | **Partial** `ShoppingCartSettings.MaximumShoppingCartItems` (per packname basket in CW) | C13 |
| `BulkOrderSettings.EnablePOCommentField` / `POCommentFieldTitle` | PO comment per bulk add | **Redesigned** `POEntryEnabled`; title missing | C5 |
| `BulkOrderSettings.EnableBranchCodeField` | branch code per bulk add | **Missing** | C4 |
| `BulkOrderSettings.UseExistingUsersWelcomePack`, `ShowWelcomePackTemplate` | welcome pack | **Missing** | C6 |
| `BulkOrderSettings.AllowGenderSelection` | gender in the selector | **Present** `GenderEnabled` | — |
| `BulkOrderSettings.HidePrintNameInput` | hide print name | **Present** `PrintNameEnabled` (inverted) | — |
| `BulkOrderSettings.HNOrderSplit` | one order per branch | **Missing** | C1 |
| `subsidysettings.tig.allowperemployee` / `.defaultmode` | per-employee subsidy mode | **Redesigned** | C2 |
| `subsidysettings.tig.enableproductsubsidycheck` | product-page subsidy warning | **Partial** | F6 |
| `TIGBankingInfoSettings.ABN`, `.BankingInfo` | invoice ABN and banking block | **Missing** | I2 |
| `TIGNotificationSettings.OrdersUseBillingAddressEmail` | copy order e-mail to billing e-mail | **Missing** | I4 |
| `OrderAuthorizationSettings.LeaderEmailAddresses` | stored, read by nothing (the leaders are `Branch.ApprovalEmail`) | **Dead in 4.1**; the feature is missing | C3 |
| `TIGNewsletterSubscribersSettings.NewsletterAutoActive` | subscriptions active at once | **Missing** | G6 |
| `TIGCategorySettings.UseCategoriesPerStore` | category per store | **Missing** | G1 |
| `TIGTopicSettings.UseTopicPerStore` | topic per store | **nop 5.00** (store-mapped copies) | G2 |
| `TIGFreightSettings.LimitMethodsToCreated` | — | **Dead** in 4.1 and in CW.Freight | — |
| `TIGJim2Settings.*`, `Jim2ApiSettings`, `Jim2SyncSettings`, `PunchoutSettings.*`, `HubSpotSettings`, NAB `GatewayApiSettings` | integrations | **Integration** (NAB XML → `Payments.NABGateway`; Punchout → nop's `Misc.PunchOut` **[parity unverified]**) | §7 |

5.00 settings with no 4.1 counterpart are the new features (feature switches, packname fields, subsidy landing,
`IncludeShippingOnSubsidy`, the `BulkOrder*` group, shipping methods per store and per branch…).

---

## 6. What nop 5.00 now does natively (no CW work needed)

| 4.1 TIG ability | nop 5.00 |
|---|---|
| Customer & user settings per store (4.1's nop page was global; TIG.CustomCustomer made it per store) | *Settings › Customer and user* with store scope |
| Topics per store | several topics with one system name, each store-mapped (`TopicService.GetTopicBySystemNameAsync(systemName, storeId)`) |
| Countries per store | `Country` is store-mapped |
| Categories limited to stores (visibility only) | `Category.LimitedToStores` — per-store **content** is G1 |
| Max cart lines (`MaxBulkOrderItems`) | `ShoppingCartSettings.MaximumShoppingCartItems` (CW applies it per packname basket — C13) |
| Reorder, minimum order subtotal / total | `OrderSettings.IsReOrderAllowed`, `MinOrderSubtotalAmount`, `MinOrderTotalAmount` (CW's packname placement checks them) |
| Same-store impersonation | nop's impersonation + CW landing (`ExtendedCustomerController`) |
| Admin customer import / export | nop's Excel / XML import and export (+ CW's packname import) |
| Per-store order, shipment, return, cart and catalogue lists (MultiStore store pages) | nop's store filters; bulk packaging slips |
| Topic / message-template store matrix (Store Content) | store mapping on each topic and template |
| Run a scheduled task by URL | *System › Schedule tasks › Run now* |
| Restart application gated by a permission | `System.ManageMaintenance` |
| Hide *Customer roles* menu by permission | `Customers.CustomerRolesView` / `CustomerRolesCreateEditDelete` |
| Order payments block by permission | CW `Orders.AccessPayments` (CW, present) |
| Contact-us logging | `PublicStore.ContactUs` activity + queued e-mails |
| Menus (4.1 cleared the SevenSpikes mega-menu cache) | `Domain/Menus` |
| Punchout cXML | `Plugins/Nop.Plugin.Misc.PunchOut` **[parity with TIG's Punchout unverified]** |
| Every nop setting on the 4.1 Manage Store page | nop's *Configuration › Settings* pages (§2.3 lists what was renamed) |

---

## 7. External integrations — out of scope unless the business brings them back

| 4.1 | What it did | 5.00 |
|---|---|---|
| **Jim2** (`NOP41/Nop.Plugin.TIG.Jim2`, `41/Services/Jim2/*`) | send orders, pull shipment status and invoices, auto-deliver, couriers, divisions, branch / product syncs, missing-orders / branches / products check reports, 7 scheduled tasks | **Odoo** (`Plugins/Nop.Plugin.Integration.Odoo`) syncs stores, departments, products (with stock) and branches one-way. **Nothing sends orders to an ERP or brings shipments back** — ask whether Odoo will need it |
| **HubSpot / FashionBiz** (`NOP41/Nop.Plugin.TIG.HubSpot`) | HubSpot key per store; FashionBiz supplier tags → categories | none |
| **Zendesk** (`41/Controllers/TIGCommonController.cs:175-260`) | Contact Us became a Zendesk ticket | nop's Contact Us e-mail |
| **Punchout cXML** (`41/Services/Punchout/*`) | send orders (incl. exchange orders) | nop's `Misc.PunchOut` plugin exists, not checked |
| **NAB REST gateway** (`NOP41/Nop.Plugin.TIG.NABPayment`) | card payment, check API, test authorise, transaction reports | replaced by the NAB XML gateway: `Plugins/Nop.Plugin.Payments.NABGateway` (with reports) |
| **SevenSpikes** (Customer Reminders, mega menu) | subsidy-end reminder rule, mega menu | F8; nop menus |
| **Goodstart SFTP user sync, SKU export task** (`TIG.DataImport/Tasks`) | client-specific scheduled tasks | none |

**Security note.** Two 4.1 files hold real credentials in plain text: a Zendesk API token (`41/Controllers/TIGCommonController.cs`,
around line 349) and an SFTP host, user and password (`NOP41/Nop.Plugin.TIG.DataImport/Tasks/GoodstartSyncUsersTask.cs:24-26`).
`/NOP41` is in `.gitignore`, so they are not in the public repository; they must never be copied into 5.00 code
(CLAUDE.md §0.9), and whoever owns those accounts may want to rotate them.

---

## 8. Redesigned on purpose — not gaps

Recorded so they are not reopened by accident; each has its decision in the named document.

- Credit-note and subsidy balances computed from views over insert-only ledgers, instead of stored remains and history
  deleted on undo (`LegacyVersions/Legacy_NOP41_Exchanges_Subsidy_Reference.md` §10).
- **Subsidy before credit notes** (decision 15); subsidy given back **in kind** on an approved return, not as top-up
  money (`Exchanges_Rebuild_Record.md`).
- Subsidies assigned to **packnames**, several per packname, with variations and annual recurrence — replaces
  per-customer assignment, "one active assignment", rollover and change-with-transfer (`subsidy-system.md`).
- Subsidised products through the catalogue gate and the subsidy catalogue — replaces the category "subsidised only"
  filter.
- One order per checkout with a destination per packname (decisions 8, 13) — see C1.
- Store self-registration deleted (phase 8 Part 1); the CustomerAdmin storefront role became the packname manager
  (add / edit / import packnames).
- Different-style exchange replacements restricted to the packname's allocation (4.1 never enforced the 2018 spec).
- Jim2 / Punchout sync on exchange orders — out of scope (backlog B5).

---

## 9. Dead in 4.1 — nothing to port

- Welcome pack one-order-per-employee (`41/Services/BulkOrders/WelcomePackService.cs:1215-1250`) — every caller commented out.
- Enquiry custom fields on Contact Us (`41/Controllers/TIGCommonController.cs:120-170` commented out; Zendesk was live).
- `TIGStoreSettings.LoginRedirectUrl`, `TIGFreightSettings.LimitMethodsToCreated`, `RestrictProductsToRegisteredUsers`'s
  reader (only the save path acts), permissions `ManageInventory`, `ManageAccounts`, `AccessSales`, `AllowNopTemplates`
  (installed, never checked).
- NAB XML payments report (commented out in 4.1; 5.00's `Payments.NABGateway` has it working).
- TIG.MultiStore partials `_StoreBranches.cshtml`, `_SubsidySettings.cshtml` (never reached).

---

## 10. What 5.00 does that 4.1 never did (keep)

One settlement row per order read by every page, e-mail, invoice and report; insert-only ledgers; in-kind subsidy
restoration; the return case aggregate with execute-once approval; ids instead of localised names for return reasons
and actions; E / R order badges; several subsidies per packname with variations and annual seasons; subsidy catalogue,
bar and per-item checkout breakdown; manager top-ups from the storefront; My Subsidies; the catalogue gate on every ACL
path; quantity caps; packname Excel import for managers and admins with validation and error report; per-store feature
switches; finer exchange / credit-note permissions; per-destination shipments; the profit report with subsidy added back.

---

## 11. Documentation findings (to fix in the documents, no code)

1. **Fixed 2026-11-12** (§18 lists all ten). **CLAUDE.md §6.6 / §18** listed four permissions; the code also has `Orders.AccessPayments`, `AccessCreditNotes`, `AccessExchanges`,
   `CW.Plugin.AllowManualCreditNotes`, `CW.Plugin.AccessExchanges`, `CW.CORE.PublicStore.AccessBulkOrdering`
   (`CW/Services/Security/*.cs`).
2. **OI 2.2.6**: the return-edit dropdown is now confirmed missing (D5); the CW.Freight order-total bounds are stored but
   not applied (§4.11).
3. **OI 2.2.2** should stay open until the manager answers §3.4.

---

## 12. Not verified

- The welcome pack, staff badge and storefront reports were read from code only; whether stores still use them needs
  the manager.
- Embroidery codes and SKU data: whether the Odoo product sync brings them.
- nop's `Misc.PunchOut` against TIG's Punchout; `Widgets.Jotform` as a replacement for custom contact fields.
- 4.1 `Services/Overrides/ExportImport/*` order export columns, `ProductAttributeFormatterOverride`, and the
  `OrderServiceOverride.SearchOrders` aggregate reports were not read in detail.
- TIG.MultiStore `Scripts/UIAdapters/admin-links.js`, `shipment.js` (they rewrite nop admin links — what exactly).

---

## 13. Working the list

Take items in the order you choose; for each, record the decision in the **Notes / decision** cell:
**Port** (→ the phase document that owns it), **Redesign** (→ document), **Covered by nop** (→ the setting / page), or
**Not needed** (→ who decided, when). Items marked *ask* need the manager first; a short questions document for him can
be drawn from C1–C3, C6–C8, C11, B5, F9, G3, G4 and §7 (Jim2 orders to Odoo).
