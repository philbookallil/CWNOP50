# MultiStore Management — Architecture Reference

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

> Scope: `Nop.Plugin.CW.Core` → `Areas/Admin` MultiStore area (`/Admin/MultiStore/*`), the
> `admin.store.navigation.js` SPA shell, and the two settings scopes that feed it.
> Written as the canonical reference for anyone touching this area. Read before modifying it.
> Last reconciled with the code 2026-10-21 (documentation consolidation); the phase documents it drew on are consumed.

---

## 1. What MultiStore Management Is

CW runs many customer-facing "stores" off one nopCommerce install. A store is a nop `Store`
entity; everything CW hangs off it (branches, departments, cost centres, packName lists,
packNames, product groups, subsidies, exchange rules, fee rules, per-store settings) is
store-scoped.

**MultiStore Management** is the single admin screen where an administrator manages everything
belonging to one store. It replaces navigating through half a dozen separate admin pages.

There are **two distinct screens** and it matters which one you are in:

| Screen | Route | Scope | View |
|---|---|---|---|
| **Store management** | `/Admin/MultiStore/Edit/{id}` (route name `CW.Plugin.Admin.MultiStore.Edit`) | one store (`storeId > 0`) | `MultiStore/Edit.cshtml` |
| **Default store settings** | `/Admin/MultiStore/Settings` | all-stores defaults (`storeId == 0`) | `MultiStore/Settings.cshtml` |
| Store list | `/Admin/MultiStore/List` | — | `MultiStore/List.cshtml` |

Both settings screens bind the **same** model (`MultiStoreSettingsModel`) but through different
actions and different views. The `Edit` screen embeds settings as a tab
(`_StoreSettings.cshtml`, loaded by AJAX); the `Settings` screen is a standalone full-page form.

### Admin menu

Built in `Services/EventConsumer.cs` (`GetAdminMenuItemAsync`, via
`BaseAdminMenuCreatedEventConsumer`), inserted **before** the `Catalog` node:

```
CW Admin (Plugins.CW.Core.Admin.Menu)
├── MultiStores                → MultiStore/List        [ADMIN_ACCESS_MULTISTORE, ADMIN_ACCESS_MULTISTORE_SETTINGS]
├── Default settings           → MultiStore/Settings    [ADMIN_ACCESS_DEFAULT_STORE_SETTINGS]
├── Branches                   → Branch/Index           [ADMIN_ACCESS_BRANCH]   (redirects to Branch/List — the standalone page, §8f)
└── Exchanges & Returns                                 [ExchangesPermissionConfigManager.ACCESS_EXCHANGES]
    ├── Fee settings           → ExchangesConfig/GlobalFeeSettings   [ADMIN_ACCESS_EXCHANGES]
    └── Credit notes report    → CreditNotes/MainReport
```

The menu is **global** — it is not store-scoped and has no access to a "current store".

---

## 2. Page Shell Architecture

```
Edit.cshtml                       page chrome, CSS/JS registration, H1, calls partial
└── _CreateOrUpdate.cshtml        tab strip + one empty <div> shell per tab
    ├── _StoreInfo.cshtml         INFO tab — server-rendered, always in the DOM
    ├── ViewComponent "ExchangeSettings"   EXCHANGE SETTINGS tab — server-rendered inline
    ├── ViewComponent "FeeSettings"        FEE SETTINGS tab — server-rendered inline
    └── all other tabs            empty shells, filled by AJAX on first activation
```

`Edit.cshtml` registers:

- CSS (`NopHtml.AddCssFileParts`): `Content/Admin/Common/loader.css`, `Content/Admin/Common/admin-pager.css`,
  `Content/Admin/MultiStore/multistore.css`; `farbtastic.css` (colour picker) by a plain `<link>` to
  `~/lib_npm/farbstastic/farbtastic.css`
- JS: `~/Plugins/CW.Core/Content/Admin/MultiStore/admin.store.navigation.js` and `admin.packname.bulk.js`
  (`NopHtml.AppendScriptParts`, footer), `~/lib_npm/farbstastic/farbtastic.min.js` (head), `~/js/admin.table.js` (footer)
- `packname-subsidies.css` and `product-groups.css` are added separately by
  `EventConsumer.HandleEventAsync(PageRenderingEvent)` when the Subsidy feature is on and the route values are
  area Admin, controller `MultiStore`, action `Edit` (`IsAdminPage("MultiStore", "Edit")`).

Bootstrapping is one call in the script at the bottom of `_CreateOrUpdate.cshtml`, which hands over the store's
feature switches and the page's text bundle:

```js
Admin.StoreNav.init(@Model.Id, { packName, subsidy, exchanges, creditNotes }, storeNavResources);
```

### Critical consequence

**Tab visibility is decided server-side, once, when `_CreateOrUpdate.cshtml` renders.**
There is no client-side re-evaluation. Anything that changes which tabs should exist requires a
full page reload — which is exactly what the settings form does (`location.reload()` in
`_StoreSettings.cshtml`, see §6).

---

## 3. Client State Machine — `Content/Admin/MultiStore/admin.store.navigation.js`

A single IIFE module exposed as `Admin.StoreNav`. ~1,470 lines. No framework.

### 3.1 Public API

```js
Admin.StoreNav.init(storeId, features, resources)        // features: {packName, subsidy, exchanges, creditNotes}
Admin.StoreNav.navigate(updates, shouldLoadPanels = true)   // merge into state, rebuild hash, reload panels
Admin.StoreNav.goTab(tab)                                    // switch tab, reset all entity ids
Admin.StoreNav.selectBranch(path)                            // path = [{id}, {id}, …]
Admin.StoreNav.enterFocusMode(entity, entityId)              // 'list' | 'packname' | 'costcenter'
Admin.StoreNav.exitFocusMode()
Admin.StoreNav.cacheName(type, id, name)
Admin.StoreNav.loadNewChildDetail(parentId)
Admin.StoreNav.appendNodeToTree(result)                      // result: {id,name,branchCode,active,parentId}
Admin.StoreNav.updateNodeInTree(result)
Admin.StoreNav.getState()

// --- added by phase 2c ------------------------------------------------
Admin.StoreNav.res(key, fallback)                            // localised string, falls back to English
Admin.StoreNav.render                                        // grid render functions (§8d #4)
Admin.StoreNav.jsArg(value)                                  // QUOTED JS string literal, escaped for the JS
                                                             // parser then the HTML attribute. Use for any
                                                             // value interpolated into an onclick
Admin.StoreNav.bindInlineAdd({input, url, fields, onCreated, onError})
Admin.StoreNav.bindGridEmptyState({grid, hide, count})       // for the grids that remain
Admin.StoreNav.branchDetailOpen(value)                       // disclosure state, remembered for the session
Admin.StoreNav.reloadPackNameLists()
Admin.StoreNav.reloadCostCenters()
Admin.StoreNav.reloadBranchAddresses()

// the unsaved-edit gate and the saved stamp
Admin.StoreNav.watchForm($form)                              // take the baseline
Admin.StoreNav.markFormDirty($form)
Admin.StoreNav.releaseForm($form)
Admin.StoreNav.canLeaveForm()                                // memoised per gesture
Admin.StoreNav.markSaved($scope)
Admin.StoreNav.flashSavedAfterReload()
Admin.StoreNav.consumeSavedFlash()
```

### 3.2 State

```js
_state = {
  tab, branchPath[], listId, packNameId, costCenterId, departmentId,
  productGroupId, subsidyId,
  mode: 'browse' | 'focus',
  focusEntity: null | 'branch' | 'list' | 'costcenter' | 'packname',
  nameCache: {}          // "type:id" -> display name
}
```

`_rendered` tracks what is currently painted in each slot (`branchTree`, `branchDetail`,
`departments`, `costcenters`, `packnames`, `settings`, `productgroups`, `subsidies`) so a panel is
not refetched when it already shows the right content.

`_pendingRequests` holds the in-flight jqXHR per panel id; `loadPartial` aborts the previous
request for the same panel before issuing a new one.

### 3.3 URL hash

The hash **is** the state. Format:

```
#<tab>[/branch:<id>]*[/list:<id>][/pn:<id>][/cc:<id>][/dep:<id>][/pg:<id>][/sub:<id>][/edit:<entity>]
```

Examples: `#info`, `#branches/branch:3/branch:12`, `#branches/branch:3/list:8/pn:44/edit:packname`,
`#subsidies/sub:5`.

`buildHash()` serialises, `parseHash()` deserialises, `history.replaceState` is used everywhere
(never `pushState`) — so the browser Back button leaves the page rather than stepping through tabs.
A `hashchange` listener re-parses and reloads, which makes hand-edited/bookmarked URLs work.

### 3.4 Breadcrumb + name cache

`#section-breadcrumb` is rebuilt by `updateBreadcrumb()` on every navigation, per-tab. Labels come
from `_state.nameCache`, keyed `branch:`, `list:`, `pn:`, `cc:`, `dep:`, `pg:`, `sub:`.

On init (and on `hashchange` / `selectBranch`), `fetchMissingNames()` collects every id in state
that has no cached name and does **one** call to:

```
GET /Admin/MultiStore/BreadcrumbData
    ?storeId=&branchIds=1&branchIds=12&listId=&packNameId=&costCenterId=&departmentId=&productGroupId=&subsidyId=
```

(`traditional: true`, so repeated `branchIds` params.) Response is a flat
`[{ Type, Id, Name }, …]`. Navigation always waits for this before painting, except for
`tab === 'info'` which short-circuits. The action resolves every id along its chain from `storeId` — a branch only if
it is in that store, a list only on a branch in that chain, a packname only in that list — and leaves out whatever does
not resolve rather than naming it; a hash with a foreign id therefore shows `Entity #12`, not a name from another store.

### 3.5 Two rendering modes (branches tab only)

- **browse** — `#branch-panel` holds the branch tree (loaded once per store), `#branch-right-col`
  is a vertical stack: branch detail card, then sub-panels appended as sibling divs
  (`pnlists-panel`, `cc-panel`, `branch-addresses-panel`) via `ensureSubPanel()`.
- **focus** — the whole of `#branch-right-col` is replaced by a single edit form
  (packName list, packName, or cost centre). Entering focus sets `_rendered.branchDetail = null`
  so leaving it forces a reload of the browse stack.

### 3.6 Focus-form save contract

`bindFocusForm(formId, onSuccess)` is shared by all three focus forms
(`pnlist-detail-form`, `pn-detail-form`, `cc-detail-form`). It re-runs
`$.validator.unobtrusive.parse`, wires `#btn-focus-back`, `#btn-focus-save`, `#btn-focus-delete`,
then POSTs `$form.serialize()` and branches **on the response Content-Type**:

| Response | Meaning | Client behaviour |
|---|---|---|
| `text/html` | server-side validation failed | swap HTML into `#branch-right-col`, re-bind events |
| JSON `{ id, name, … }` | saved | cache the name, `exitFocusMode()` |
| JSON `{ success:false, error }` (delete) | delete refused | `alert(error)` |

**Any new focus form must honour this same dual contract**, or the client will paint JSON into
the DOM.

### 3.7 Tab → URL map

`loadTabPanel(tab)` (the only place AJAX tab URLs live — they are **hard-coded absolute paths**,
not `Url.Action`):

```js
departments:   '/Admin/MultiStore/Departments'
costcenters:   '/Admin/MultiStore/CostCenters'
packnames:     '/Admin/MultiStore/PackNames'
settings:      '/Admin/MultiStore/StoreSettings'
productgroups: '/Admin/Subsidy/ProductGroups'
subsidies:     '/Admin/Subsidy/Subsidies'
```

Note `productgroups` and `subsidies` are served by **`SubsidyController`**, not
`MultiStoreController`. All are called with `{ storeId }` and return partial HTML into
`#tab-panel-<tab>`.

---

## 4. Tab Inventory

| Tab key | Nav link id | Container | Rendering | Source |
|---|---|---|---|---|
| `info` | `#tab-info` | `#tab-content-info` | server, always present | `_StoreInfo.cshtml` (all fields disabled — display only) |
| `branches` | `#tab-branches` | `#tab-content-branches` → `#branch-panel` + `#branch-right-col` | AJAX, multi-panel | `BranchTree`, `BranchDetail`, `PackNameLists`, `BranchCostCenters`, `BranchAddresses`, `PackNameListDetail`, `BranchPackNameDetail`, `BranchCostCenterDetail` |
| `departments` | `#tab-departments` | `#tab-panel-departments` | AJAX | `Departments` → `_Departments.cshtml` |
| `costcenters` | `#tab-costcenters` | `#tab-panel-costcenters` | AJAX | `CostCenters` → `_CostCenters.cshtml` |
| `packnames` | `#tab-packnames` | `#tab-panel-packnames` | AJAX | `PackNames` → `_PackNames.cshtml` |
| `productgroups` | `#tab-productgroups` | `#tab-panel-productgroups` | AJAX | `Subsidy/ProductGroups` → `_ProductGroups.cshtml` |
| `subsidies` | `#tab-subsidies` | `#tab-panel-subsidies` | AJAX | `Subsidy/Subsidies` → `_Subsidies.cshtml` |
| `settings` | `#tab-settings` | `#tab-panel-settings` | AJAX, cached (`_rendered.settings`) | `StoreSettings` → `_StoreSettings.cshtml` |
| `exchangesettings` | `#tab-exchangesettings` | `#tab-panel-exchangesettings` | **server, inline ViewComponent** | `Component.InvokeAsync("ExchangeSettings", new { storeId })` |
| `feesettings` | `#tab-feesettings` | `#tab-panel-feesettings` | **server, inline ViewComponent** | `Component.InvokeAsync("FeeSettings", new { storeId })` |

### Current tab visibility conditions (in `_CreateOrUpdate.cshtml`)

```csharp
var hasViewSettingsPermission = await permissionService.AuthorizeAsync(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE_SETTINGS);
var features = await cwFeatures.GetFeaturesAsync(Model.Id);   // ICWFeatureService, injected in _ViewImports
```

- `packnames` → `features.PackName`
- `productgroups`, `subsidies` → `features.Subsidy`
- `exchangesettings`, `feesettings` → `features.Exchanges`
- `settings` → `hasViewSettingsPermission` (the nav `<li>` only; the empty shell is always rendered)
- `info`, `branches`, `departments`, `costcenters` → unconditional

The feature switches are the store's own (`Feature_Toggles_Reference.md`). The old
`PluginHelper.IsExchangePluginInstalled()` / `IsSubsidyPluginInstalled()` checks are gone.

The same `@if` wraps both the nav `<li>` and the body shell of each feature tab, so the two
`ViewComponent` tabs (`ExchangeSettings`, `FeeSettings`) are invoked on every render **of a store with
Exchanges on**, whether or not the tab is opened.

---

## 5. `MultiStoreController` — Action Inventory

`Areas/Admin/Controllers/MultiStoreController.cs`, ~3,180 lines, inherits `BaseAdminController`
(which already applies `[Area]`, `[AuthorizeAdmin]`, `[AutoValidateAntiforgeryToken]`,
`[ValidateIpAddress]`, `[ValidateVendor]`, `[SaveSelectedTab]`, `[NotNullValidationMessage]` —
do **not** re-apply them). Organised by `#region`:

| Region | Actions | Permission |
|---|---|---|
| **Stores** | `Index` (redirects to `List`), `List`, `StoreList` (POST/grid), `Edit(id)` | `ADMIN_ACCESS_MULTISTORE` + `ADMIN_ACCESS_MULTISTORE_SETTINGS` (`StoreList` also `ADMIN_ACCESS_BRANCH`) |
| **Breadcrum** (sic) | `BreadcrumbData(...)` — also `[CheckCWFeature(PackName, Parameter)]` | `ADMIN_ACCESS_MULTISTORE` + `ADMIN_ACCESS_MULTISTORE_SETTINGS` |
| **Branches** | `BranchTree`, `BranchDetail`, `SaveBranch`, `DeleteBranch` | `ADMIN_ACCESS_MULTISTORE` |
| **Branch Addresses** | `BranchAddresses`, address create/edit/delete + popup | `ADMIN_ACCESS_MULTISTORE` |
| **PackName Lists** | `PackNameLists`, `PackNameListDetail`, `SavePackNameList`, delete, manager add/remove + popup | `ADMIN_ACCESS_MULTISTORE` |
| **PackNames** | `PackNames`, `PackNamesInline`, `PackNameDetail`, `BranchPackNameDetail`, `SavePackName`, `SaveBranchPackName`, delete, customer association popups | `ADMIN_ACCESS_MULTISTORE` |
| **PackName Addresses** | list / create / edit / delete + popup | `ADMIN_ACCESS_MULTISTORE` |
| **Cost Centers** | `CostCenters`, `CostCenterDetail`, `BranchCostCenters`, `BranchCostCenterDetail`, `SaveCostCenter`, delete | `ADMIN_ACCESS_MULTISTORE` |
| **Cost Center Addresses** | list / create / edit / delete + popup | `ADMIN_ACCESS_MULTISTORE` |
| **Departments** | `Departments`, `DepartmentDetail`, `SaveDepartment`, delete, manager add/remove + popup | `ADMIN_ACCESS_MULTISTORE` |
| **Store Settings** | `StoreSettings` (GET partial), `StoreSettings` (POST), `GeneratePackNames`, `GeneratePackNameLists` | GET `ADMIN_ACCESS_MULTISTORE`; POST `ADMIN_ACCESS_MULTISTORE_SETTINGS` |
| **Default Stores Settings** | `Settings` (GET), `Settings` (POST, `[FormValueRequired("save")]`) | `ADMIN_ACCESS_DEFAULT_STORE_SETTINGS` |
| **PackNames** (bulk, same region) | `BulkPackNames` (POST, JSON), `PackNameBulkMoveTargets(storeId)` (GET, JSON), `ExportPackNames(PackNameSearchModel, selectedIds)` (POST, xlsx file) — each also `[CheckCWFeature(PackName, Parameter)]` (§8h) | `ADMIN_ACCESS_MULTISTORE` |
| **PackName Import** | `ImportPackNames(storeId)` (page), `ImportPackNamesTemplate(storeId)`, `ImportPackNames(storeId, importFile)` (POST, JSON), `ImportPackNamesErrorReport(storeId, importFile)` (POST, file) — every action also `[CheckCWFeature(PackName, Parameter)]`; a store id that names no store → the store list with *The store could not be found.* (the POST answers it as `message`) | `ADMIN_ACCESS_MULTISTORE` |

Model factories: `IMultiStoreModelFactory` (stores, branches tree, departments, cost centres),
`IPackNameModelFactory` (packName lists + packNames), `ICWSettingModelFactory` (settings),
`ICWAddressModelFactory` (all address sub-panels), `ISubsidyModelFactory` (product groups,
subsidies — via `SubsidyController`).

`GeneratePackNames` / `GeneratePackNameLists` save three settings inline (each override as posted), read back the
`PackNameListNameFrom` the store now has, and queue the real work through `IBackgroundTaskQueue`
(`QueuedHostedService`), returning `NullJsonResult` immediately. The job runs `IPackNameProvisioningService`
(`EnsureDefaultPackNameListAsync` per branch without a list, `CreatePackNamesForCustomersWithoutOneAsync` per list) — the
same service that provisions a new branch and a newly linked customer (§8f). They do **not** publish
`MultiStoreSettingsChangedEvent`.

---

## 6. Settings — Two Scopes, One Model

### 6.1 The model

`Areas/Admin/Models/Settings/MultiStoreSettingsModel.cs` : `BaseNopModel, ISettingsModel`.
For every setting `X` there is a companion `bool X_OverrideForStore`. Sections:
PackNames, CreditNotes && Exchanges (`CreditNotesEnable`, `ExchangesEnable`), Subsidy
(`SubsidyEnable`), Bulk Order, Store (default packname customer roles only — the registration screen was
deleted in phase 8 Part 1).

Backed by `Domain/Settings/MultiStoreSettings.cs : ISettings` — Bulk Order, PackNames (with `PackNameEnable`),
CreditNotes && Exchanges, Subsidy and Payment regions.

**Not on any screen:** `OwnPlacementPaymentMethods` (Payment region, phase 4c) — the payment methods that take the
payment in their own pages and place the order themselves (default `Payments.PayPalCommerce`, stored for store 0
by the plugin install/update). The packname checkout never places an order with one of them; it hands over to the
method's page. Edited through nop's *All settings* (`multistoresettings.ownplacementpaymentmethods`,
comma-separated). A method missing from the list would have its packname order placed unpaid.

### 6.2 Override mechanism (per-store scope only)

`<nop-override-store-checkbox asp-for="X_OverrideForStore" asp-input="X" asp-store-scope="@Model.ActiveStoreScopeConfiguration" />`
renders the standard nop "override for this store" checkbox. On save,
`ISettingService.SaveSettingOverridablePerStoreAsync(settings, x => x.X, model.X_OverrideForStore, storeId, false)`
either writes a store-scoped `Setting` row or deletes it (falling back to the `storeId = 0`
default). The last argument `false` means "don't clear cache yet".

`ICWSettingModelFactory.PrepareMultiStoreSettingsModelAsync` reads the flags back with
`SettingExistsAsync(storeSettings, x => x.X, storeId)`. It also lazily seeds
`AllowSubsidyCountByExcel = 3` into the storeId=0 defaults if the setting has never been saved.

The default-settings screen (`storeId == 0`) renders plain `<nop-label>` / `<nop-editor>` with
**no** override checkbox.

### 6.3 Save flow — per-store (`_StoreSettings.cshtml` → `StoreSettings` POST)

1. `saveSettings()` serialises `#multistore-settings-form` and POSTs by AJAX.
2. Controller loads `storeId = model.StoreId` (0 is refused — that scope belongs to the `Settings` action), keeps only
   the shipping-method names the CW.Freight plugin offers in the store (`KeepOfferedShippingMethodsAsync`), normalises
   `BulkOrderAxisAttributeIds`, then calls `SaveSettingOverridablePerStoreAsync` per property — **every value and every
   override flag as posted**, no coercion and no derived flags (`Feature_Toggles_Reference.md` §3.2; §8f trap 1).
3. *(Removed with store self-registration, phase 8 Part 1: there is no slug step any more.)*
4. Enabling `DoNotPreSelectShippingAddress` when it was previously off →
   `ClearSelectedShippingAddresses(storeId)`.
5. Publishes `MultiStoreSettingsChangedEvent`, shows a success notification.
6. Returns **JSON**:
   - success: `{ success: true }` → client calls **`location.reload()`**
   - failure: `{ success: false, settingssectionhtml: <rendered _StoreSettings> }` → client
     swaps `#tab-panel-settings`

**`location.reload()` on success is the mechanism by which tab visibility refreshes after a
setting change.** The hash survives the reload, so the user lands back on `#settings`.

### 6.4 Save flow — defaults (`Settings.cshtml` → `Settings` POST)

Plain full-page form POST (`[FormValueRequired("save")]`), same per-property saves against
`storeId = 0`, then `MultiStoreSettingsChangedEvent`, then `RedirectToAction("Settings")`.

### 6.5 Client-side interdependency toggles

`_StoreSettings.cshtml` (and the equivalent block in `Settings.cshtml`) has one function, `applySettingVisibility()`,
run on load and on every governing checkbox's change: `toggleRow(selector, visible)` per row group with the complete
predicate written once (`isChecked(key)` counts a checkbox absent from the screen as on). It also runs
`checkOverriddenStoreValue()` over every `.multi-store-override-option`.

Nested rows are marked by id (`#pnlPackNameSettings`, `#pnlPrintNameEnabled`, `#pnlUseBranchesAsPackNameList`,
`#pnlSubsidyEnableRow`, the bulk-order panels) or by class (`.pnlUseStaffAsPackName`, `.pnlUseBranchesAsPackNameList`,
`.pnlAllowHomeDelivery`, `.pnlAllowManagersToAddPackNames`, `.pnlSubsidySelection`, `.pnlAllowSubsidySelection`,
`.pnlAllowToAddByExcel`, `.pnlSubsidyOnly`, `.pnlPackNameOnly`). **Class-based groups are the pattern to follow for new
nested settings** — they allow several rows under one parent.

### 6.6 Card collapse state

Each `<nop-card>` persists its collapsed state in a customer `GenericAttribute`:
`MultiStoreSettings.HidePackNameBlock`, `.HideRegisterBlock`,
`.HideCreditNotesExchangesBlock`, `.HideSubsidyBlock`. The customer-page equivalents use the
`CustomerPage.Hide*Block` prefix.

---

## 7. Caching & Invalidation

- Settings are cached by nop's `SettingService` in `IStaticCacheManager`. `LoadSettingAsync<T>`
  is a cache read + property binding, not a DB hit.
- `EventConsumer.HandleEventAsync(MultiStoreSettingsChangedEvent)` calls
  **`_staticCacheManager.ClearAsync()`** — a full cache flush. Blunt but guarantees every
  derived cache (packName, subsidy, ACL, visibility) reflects the new settings.
- `MultiStoreSettingsChangedEvent` currently carries **no payload**. Adding a `StoreId` would
  allow targeted invalidation instead of a global flush — noted as a possible improvement, out of
  scope for now.
- CW entity caches are invalidated per-entity by `ICacheEventConsumer<T>` implementations under
  `Services/*/Caching/`. Prefixes: `CW.packname.*`, `CW.subsidy.*`, `CW.departments.*`,
  `CW.branch.*`.
- **Completeness is proved with a matrix, not a grep.** Every static cache key × the entities its lambda reads × the
  consumer that clears it. The matrix was built in full on 2026-09-22 (48 keys, 38 prefixes, 42 consumer entity
  types) and found no gap after six fixes; rebuild it whenever a cache key or a consumer is added. Short-term
  (`_shortTermCacheManager`, per request) keys are outside it, and a prefix declared as a `CacheKey` rather than a
  `string` is easy to misfile. "Is this symbol referenced?" is the wrong test for a cache prefix — the right one is
  "is there an entity whose change invalidates this key but cannot compute the key?" (two prefixes deleted as unused
  on that test had to come back).

### 7.1 The manager-ownership cache, and how to prove its invalidation is complete

`CW.packname.ownedids.bymanager.{managerId}` (`PackNameServicesDefaults.PackNameOwnedIdsByManagerCacheKey`, one
parameter) holds the **set of packname ids a manager owns** (`MultiStoreService.GetOwnedPackNameIdsByManagerIdAsync`).
The list ids it is built from are cached apart, under `CW.packnamelistids.bymanager.{managerId}-{requireActiveList}`
(`MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKey`). That second parameter arrived with phase 2b decision 1:
the scope SP takes `@RequireActiveList`, ordering passes `1` and the ownership query passes `0`, so the two answers
are different sets and must not share a key. Every storefront and admin access guard
reads it through `IsPackNameOwnedByManagerId`, so a stale entry is a privilege-retention bug, not a
display bug: it survives for `DefaultCacheTime` (60 minutes by default), and a manager whose access
was revoked keeps it for that long.

It replaced a per-`(manager, packname)` key in phase 2a (H-05): one entry per pair is a key explosion
at thousands of packnames, and the old pair key took one argument for a two-parameter pattern, threw
`FormatException` inside a cache consumer, and took the three invalidations below it down with it
(P-04).

**The completeness check is a table, not a grep.** The ownership query reads
`GetPackNameListIdsByManagerIdAsync` (the `packname_get_listids_by_manager` SP: `PackName`,
`PackNameList`, `PackNameListManagerMapping`, `Branch`) plus `GetDepartmentsByManagerIdAsync`
(`DepartmentManagerMapping`, `Department`) over `PackName` rows. Six entities, six consumers:

| Entity changed | Consumer | How |
|---|---|---|
| `PackName` | `PackNames/Caching/PackNameCacheEvenConsumer` | prefix — one packname can move between managers |
| `PackNameList` | `PackNames/Caching/PackNameListCacheEvenConsumer` | prefix — the SP's `AllManagers` / `ParentManagers` paths make one list affect every manager in the store |
| `PackNameListManagerMapping` | `PackNames/Caching/PackNameListManagerCacheEvenConsumer` | **keyed** on the manager for the one-parameter ownership key, **prefix** for the two-parameter list-ids key. A one-argument `RemoveAsync` against a two-placeholder key is the P-04 shape again: it throws `FormatException`, `EventPublisher` swallows it, and the clear silently never happens (2b correction C1) |
| `Branch` | `Branches/Caching/BranchCacheEventConsumer` | prefix — the SP walks the branch hierarchy |
| `DepartmentManagerMapping` | `PackNames/Caching/DepartmentManagerCacheEvenConsumer` | keyed on the manager |
| `Department` | `MultiStores/Caching/DepartmentCacheEvenConsumer` | prefix — one department maps to many managers |

The last two were missing until 2026-09-17 (U-36) and were found by building this table, after three
passes of grepping consumers had each found one more. **Add a row here whenever the ownership query
gains an input**, and remember that a soft delete publishes `EntityDeleted` after setting `Deleted`,
so a delete reaches the consumer like any update.

Related storefront caches, same file, all prefix-cleared by the packname and list consumers:
`CW.packname.activepacknames.bylistid.` and `CW.packname.activepacknames.fordepartments.bylistid.`
(the department roster caches under list id **0**, "any list", so a prefix formatted with a real list
id never matches it — U-37).

---

## 7b. Manager scope — the single authority

Four rules, settled in phase 2a. They apply to the storefront `PackNameController` /
`SubsidyCatalogController` and to anything admin-side that asks the same question.

1. **`IMultiStoreService.IsPackNameOwnedByManagerId(managerId, packNameId)` is the only authority.**
   Two arguments since 2026-09-17: it once took the manager's list ids and department ids as optional
   narrowing arguments while caching under the manager alone, so a caller passing a subset got
   whatever the first caller had cached. Ownership is `!Deleted` **only** — never `Active` — because a
   deactivated packname is still its manager's to edit; the listing query
   (`GetPackNameIdsByManagerId`) filters `Active` because it feeds dropdowns, and the two must not be
   confused.
2. **The SP does not check `Manager`.** `packname_get_listids_by_manager` answers "which lists does
   this packname id reach", and its path 1 returns every active list in the caller's own branch — for
   a non-manager too. So "is this customer a manager" is a **separate** check every caller makes
   (`GetCurrentManagerPackNameAsync` on the controller); ownership alone is not authorisation.
   `POST /PackName/PackNames` leaked a branch roster to an ordinary employee for exactly this reason
   (U-29).
3. **The store comes from the manager, never from the request.** `PackName` has no `StoreId`; it is
   `list → branch → StoreId`. `MultiStoreSettings` is store-scoped and carries the authorisation gates
   (`AllowManagersToAddPackNames`, `LimitAssignedBranches`), so loading them for a posted store id let
   a manager pick the store whose settings suited them (U-25). The storefront controller resolves it
   with `ResolveManagerStoreIdAsync`; `MultiStoreService.GetManagerStoreIdAsync` is the same lookup,
   `protected`, and not on the interface.
4. **`LimitAssignedBranches` decides where a manager may ADD a packname inside the store, never across
   stores.** This is the add-reach rule, not the visibility rule of §7b.2: off means *any list in any branch of the
   manager's own store*; on means *only lists the manager owns, in that branch*. The cross-store check runs before
   the setting is consulted, in both cases (U-01).

Two consequences worth knowing. Every id a request carries about someone else — packname, list,
branch, department, cost centre, store — is validated against one of these four rules or refused; a
posted department or cost centre is checked against the manager's store because `PackName.DepartmentId`
itself grants ownership (U-28).

**P-20 is closed** (phase 2b part 1, decision 1 of 2026-09-17): deactivating a packname list **hides it
from ordering and does not revoke administration**. `packname_get_listids_by_manager` takes
`@RequireActiveList BIT = 1`; all four paths filter `pnl.Active = 1` only when it is set; the ownership
query calls it with `0` and the selector with `1`. Verified live 2026-09-21: with a list deactivated it
vanishes from the manager's selector on the next request, while that manager's own edit and top-up for a
packname inside it keep working, and re-activating puts it back on the very next request with no restart.

### 7b.1 The admin half of the same four rules (phase 2b part 1)

Rules 1–4 above were written for the storefront. Every part-1 finding of phase 2b was one of them being
broken on the admin side, and three admin-specific corollaries came out of it. One framing first:
`CW.CORE.Admin.AccessMultiStore` is **one global flag** — there is no per-store admin permission — so any holder may
open any store's screen, and a forged cross-store id in an admin request is a feature-gate, settings-gate or
data-integrity defect, never a privilege escalation between admins. The one exception is `StoreId = 0` (rule 4 below).

1. **The store comes from the ENTITY in every save**, never from the posted model. `SavePackNameList`,
   `SavePackName`, `SaveBranchPackName`, `CustomerPackNameCreatePopup`, `SaveSubsidy`,
   `SaveProductGroup`, `SubsidyVariationEditPopup`, `SaveDepartment` and `SaveCostCenter` all took it from the request
   (the last two on insert; their update branches copy only `Name` and `Active`). Because
   store-scoped `MultiStoreSettings` carries the gates, a posted store id is a gate bypass — and via
   the mapping profile (`Infrastructure/Mapper/CWMapperConfigurationProfile`, Mapster since 5.00) it also *relocated* the entity, carrying its assignments into the other
   store. `StoreId`, `BranchId` and `PackNameListId` are `Ignore()`d in the profile for that reason;
   an entity's store is changed by a deliberate action or not at all.
2. **A picker that is store-scoped on read must be store-scoped on write.** Six findings were exactly
   this pair: the GET filtered correctly and the POST inserted whatever ids arrived. The fix is to
   re-run the picker's own query in the POST and accept only what it returns — not to write a second,
   parallel predicate, which is how the two drift apart again.
3. **The two manager-mapping POSTs are where SP path 4's store boundary lives.** The SP grants scope
   from `PackNameListManagerMapping` with no store predicate of its own, so `ManagerAddPopup` and
   `DepartmentManagerAddPopup` are the only place that boundary can be enforced. They check the posted
   packname for store, for `Manager` and for `Deleted` — all three, because path 4 asks none of them.
4. **`StoreId = 0` is not a store.** It is the all-stores default scope, which belongs to
   `DEFAULT_STORE_SETTINGS`; the per-store settings action must refuse it rather than treat it as a
   store id. This is the one genuine permission crossing in the phase. See
   `Feature_Toggles_Reference.md` §3.2.

A further rule came from the live testing rather than the analysis: **the store predicate on the
hierarchy CTE's recursive member is what stops scope crossing stores, and it holds even when the data
crosses.** A store-2 branch whose `ParentBranchId` points at a store-1 branch, carrying a
`ParentManagers` list, does not appear in a store-1 manager's selector (verified 2026-09-21). Since 2026-10-11 both
branch saves (Manage Store's `SaveBranch` and the standalone Branches page) also refuse a cross-store parent and a
parent loop (§8f #5), so the orphan state is no longer reachable through the UI; the CTE predicate stays as the
backstop.

### 7b.2 What the scope procedure returns — the four paths (P-06, decision 12)

`packname_get_listids_by_manager (@ManagerId, @RequireActiveList = 1)` (`Services/DataBase/CWDatabaseService.cs`,
called by `MultiStoreService.GetPackNameListIdsByManagerIdAsync`, cached under the manager id and the flag) is the one
place the manager-scope rule lives. **The root** is the branch of the manager packname's *own list*; **the hierarchy**
is that branch plus its descendants where `Branch.Deleted = 0` and `Branch.StoreId` = the root's store (the store
predicate on the recursive member is what keeps scope inside one store even when a `ParentBranchId` points across).
The result is the union of:

| Path | Returns | Flags consulted |
|---|---|---|
| 1 — own branch | **Every** list on the root branch itself | none — `AllManagers` / `ParentManagers` / mappings are irrelevant on the manager's own branch |
| 2 — store-wide | Every list with `AllManagers = 1` on any non-deleted branch of the manager's **store** | hierarchy irrelevant |
| 3 — descendants | Lists with `ParentManagers = 1` on **descendant** branches of the root (never the root itself) | `ParentManagers` means "visible to the managers of ancestor branches" |
| 4 — direct mapping | Lists the manager is mapped to in `CORE_PN_PackNameListManagers` | no store or branch predicate — the mapping **is** the grant; the store boundary is enforced where the mapping is written (§7b.1 #3) |

Every path filters `PackNameList.Deleted = 0`; `PackNameList.Active = 1` only with `@RequireActiveList = 1`
(visibility — the selector and the ordering paths), not for ownership (`IsPackNameOwnedByManagerId` passes `0`).
`Branch.Deleted = 0` holds on paths 1–3 through the CTE and explicitly on path 2; **`Branch.Active` is never
filtered** — a deactivated branch's lists stay visible and orderable (`Active` means "no new assignments", §7c). The
procedure does **not** check `PackName.Manager` (rule 2 above). So `AllManagers = false` with explicit owners means
"not visible to managers of *other* branches unless mapped", not "private within the branch". Decision 12
(2026-09-12): this rule — broader than 4.6's — is the intended one.

**Department managers** are a separate route, not in the procedure: `GetPackNameIdsByManagerId` /
`GetOwnedPackNameIdsByManagerIdAsync` union the list-scope packnames with the packnames whose `DepartmentId` is in
`GetDepartmentsByManagerIdAsync` (store-filtered through the manager's store). The storefront selector adds either
each branch's list holding only the department's packnames (`ShowDepartmentPackNamesByBranch`) or one virtual list per
department with a **negative** id, always adds the manager's own list, offers only entries that load at least one
packname (`HasVisiblePackNamesAsync`, asked of the same cached readers the roster uses), and a stored selection on a
hidden list falls back to the first offered; the **+** button adds into the selected list, so a new empty list gets
its first packname from admin. A non-manager gets no selector at all.

A manager whose own list has no branch (`BranchId NULL`, §7d) reaches nothing through paths 1 and 3 and only
store-wide `AllManagers` lists through path 2.

### 7c. The `Active` flag on Branch, Department and CostCenter — "no new assignments"

`Active` on a branch, department or cost centre means **no new assignments** (decision of 2026-09-22). An inactive one
keeps every packname already on it working — ownership, scope, ACL and the department role are unaffected — and only
disappears from the assignment pickers, admin and storefront. Two rules make that safe:

- **The filter is applied at the picker, never at the service.** `GetAllBranchesByStoreIdAsync` also feeds the branch
  tree and the manager-scope computation, and `GetDepartmentsByStoreIdAsync` feeds the admin grid, which must keep
  listing inactive rows so they can be reactivated (`GetCostCenterSelectItemsAsync` filters at source because both
  its callers are pickers).
- **Every picker keeps the value currently assigned** (`keepId`: `Active || Id == keepId`, in both
  `PackNameModelFactory`s) even when it is inactive — otherwise opening a packname whose department was deactivated
  would silently blank the field on the next save.

The server refuses a *new* inactive target while accepting an unchanged one — `MultiStoreController.
ValidatePackNameTargetAsync` and the storefront `PackNameController`'s equivalent; the Excel import and the bulk move
apply the same rule. `PackNameList.Active` and `PackName.Active` are different flags with their own meaning (§7b:
a switched-off list is hidden from ordering and takes no new packnames, §8g #5; an inactive packname is hidden from
the selector and its cart lines are hidden).

### 7d. `PackNameList.BranchId` is nullable — the rules that follow

`BranchId` stays nullable by decision (2026-09-28; the requirement may return) although every insert path sets it —
the admin save refuses a list without a branch (`Branch.Required`). A list without a branch has **no store scope**:
it supplies no manager, appears in no manager picker, and is excluded by the inner joins in `MultiStoreService` and by
scope paths 1–3 (path 4, the direct mapping, is the grant and is bounded where the mapping is written). Where the
branch is only decoration the join is a left join and the null is handled explicitly — `(b == null || !b.Deleted)`,
never a bare `!b.Deleted`, which SQL evaluates to NULL and silently drops the row. Nothing dereferences
`BranchId.Value` without `HasValue`. And **a manager must have a customer** (`user != null && !user.Deleted`): a
manager orders on behalf of others and has to sign in.

---

## 8. Gotchas

1. **Tab strip is server-rendered once.** No client re-evaluation. Changing which tabs exist
   requires a page reload.
2. **AJAX tab URLs are hard-coded strings** in `admin.store.navigation.js`, not `Url.Action` — 16 `'/Admin/…'`
   literals, left by decision (a URL map handed to `init` is the fix if ever wanted). Renaming a controller or action
   silently breaks a tab. Two of them point at `SubsidyController`, not `MultiStoreController`. Five detail partials
   also still carry a literal `data-url="/Admin/MultiStore/…"` (`Subsidy_PackName_Open_Items.md`).
3. **The `exchangesettings` / `feesettings` ViewComponents run on every page load of a store with Exchanges
   on**, even when the user never opens the tab. With Exchanges off neither the tab nor the component renders.
4. **Focus forms must return HTML on validation failure and JSON on success.** Getting this
   backwards paints raw JSON into the page.
5. `history.replaceState` only — the browser Back button exits the page rather than undoing a
   tab change.
6. `_rendered` means a panel will not refresh just because you navigated back to it. If a save
   changes data in another panel you must null the relevant `_rendered` entry or reload.
7. The **settings tab is cached** by `_rendered.settings`, so it renders once per page load;
   after a save the page reloads anyway.
8. `#branch-right-col` is rebuilt wholesale by `loadBranchDetail`; sub-panels are re-appended by
   `ensureSubPanel()`, which also removes orphaned panels left outside the current right column.
9. Stale AJAX: if a feature is switched off in one browser tab, an already-open MultiStore page
   in another tab can still POST to the now-disabled controller actions. Server-side enforcement
   is required, not just hidden UI.
9a. **No `beforeunload`, by decision.** The unsaved-edits guard (`canLeaveForm()`) covers in-page navigation only;
   leaving through nop's left menu or closing the tab loses an open form. `hashchange` is not gated either — the module
   uses `replaceState` only, so the hash never changes under it.
9b. **`#branch-right-col` is a CSS-grid item and needs `min-width: 0`.** Without it the column will not shrink below
   its widest table, the page scrolls horizontally and every right-margin fix appears to do nothing.
9c. **DI cycles around the context.** `PackNameContext` takes `IMultiStoreService` lazily, and `WebWorkContext` /
   `PackNameContext` are `Lazy<>` inside `CustomerOverrideService` because both depend on `ICustomerService`; a nested
   `GetCustomerRoleIdsAsync` during that resolution gets nop's answer through a **static** `AsyncLocal<bool>` guard
   (an instance field broke under `Task.WhenAll`). `GetCurrentPackNameAsync` is asked only for the signed-in customer —
   asking it for anyone else selects a packname for them and writes their attributes. Event consumers are resolved at
   publish time, so a service may be injected into `EventConsumer` without a cycle.
9d. **nop validates detached cart items.** Editing a non-packname order in admin restores a cart whose items carry
   `Id = order item id`, which can equal another customer's packname cart row; the cart-line packname window therefore
   resolves the packname only from a row that is this customer's and holds this product.
9f. **The department ACL picker is store-scoped.** `AclDepartmentsViewComponent` lists only the departments of the stores
   the category / manufacturer / product is mapped to, grouped by store, and `AclOverrideService.SaveAclDepartmentAsync`
   re-applies the same filter on save. Department roles are minted per store: a department from another store would
   restrict the entity to a role nobody in its own store holds and silently drop it from that catalogue.
9g. **Every write on the store page is activity-logged** — mappings (list managers, department managers), addresses,
   customer association, branch delete, the settings saves (both scopes) and the two Generate buttons (with store and
   count). The generators check `ModelState` and `Enum.IsDefined` on the posted rule, and a settings POST that fails
   validation no longer reports success. The storefront `EditPackName` POST and the three admin address popups do
   **not** log (`Subsidy_PackName_Open_Items.md`).
9h. **Deleting a packname** deletes its cart lines (FK cascade on `CORE_PN_PackNameCartItem` from both parents) and its
   shipping assignment; deleting the **customer** clears `PackName.CustomerId` and `Manager` and leaves the packname
   (`EntityDeletedEvent<Customer>` consumer). A deactivated packname is dropped from the storefront context on its next
   read (`subsidy-system.md` §8.6).

### 8b. Added by phase 2c (2026-09-24) — three that cost days each

10. **A `.js` edit does not take effect until `Presentation/Nop.Web/wwwroot/bundles` is emptied.**
    `WebOptimizer` has `EnableDiskCache: true` pointing there, so the minified bundle survives both a
    rebuild and a full restart. `.cshtml` and `.cs` are fine on hot reload — which is exactly why view
    changes appear immediately and JS changes appear to do nothing.
    Diagnosis, one line in the console: `Object.keys(Admin.StoreNav)`. If the source file has the
    export and that list does not, it is the cache. Do not debug the code.
11. **In `admin.store.navigation.js`, use `var` — not `let` — for a closure variable read by a hoisted
    inner function.** NUglify inverts an early `return` guard into a wrapping `if` block and moves
    `let` declarations inside it while function declarations stay hoisted outside, so the inner
    function binds to an unrelated outer variable. The symptom is a silent no-op with no console
    error, reproducible only in the minified bundle.
12. **A partial that binds delegated handlers to `#pnlists-panel`, `#cc-panel` or
    `#branch-addresses-panel` must `.off('.ns')` first and namespace every handler.** `ensureSubPanel`
    creates those divs **once** and `loadPartial` only swaps their `innerHTML`, so the element outlives
    every render of the partial and handler sets stack — measured 1 → 2 → 3 → 4 over three reloads,
    which made Delete ask four times. Handlers bound to elements *inside* the replaced markup are safe.
    `e.stopPropagation()` does not help: the duplicates are on the *same* element.
13. **FluentValidation runs at model binding, before the controller body.** A controller cannot
    "derive" a value a validator requires — `CostCenterValidator` and `PackNameListValidator` both
    demand `StoreId != 0`, so a post that omits it is refused before `SaveCostCenter` /
    `SavePackNameList` can take the store from the branch. Those partials render no span for `StoreId`
    and no validation summary, so the refusal is invisible: the request returns 200 with the
    validation-failure partial and the UI reports nothing.

---

## 8c. Branch-page collections — the presentation contract (phase 2c)

The branch page's three collections — packname lists, cost centres, addresses — are **server-rendered
row lists**, not grids. A collection is a label, not a card.

```
.ms-collection > .ms-collection-head ( caret · .ms-collection-label · .ms-collection-count · .ms-collection-action )
                 .ms-rows  > .ms-row [.selected|.is-default]
                              > .ms-row-main ( .ms-row-lead · .ms-row-info ( .ms-row-name · .ms-row-line · .ms-row-meta > .ms-row-chip* ) · .ms-row-actions )
                 .ms-add-row ( .ms-add-plus · .ms-add-input · .ms-add-error )
```

- The **add row is the empty state**. A collection with nothing in it is one dashed row, never a
  headed table with no rows.
- **Counts come from the model**, not from a DataTables draw callback.
- `.ms-row-chip` is `white-space: nowrap` — short labels only. Content that can be long uses
  `.ms-row-line`, which truncates with an ellipsis and carries the full text in a `title`.
- `.pnlist-inline-panel` and `.pn-count` stay packname-specific; everything else is generic.
- `.card-ms-inner-section` is a **contract**: `formState()` excludes `.card-ms-inner-section :input`
  because the child grids sit inside the `<form>` and DataTables' page-length select posts a name. A
  new child section keeps that class or paging a grid reads as an unsaved edit.
- **Save never navigates.** A detail form stays open after a successful save and takes a
  `Saved hh:mm` stamp; on a create it reloads for the new id, which is what unlocks the child grids (their `UrlRead`
  carries the id). Whether a save was a create is read from the form's `IsNew` **before** posting: the insert branch
  answers `{success, id, name}` with no `isNew`. There is no "Save and continue edit" button because Save already does
  not navigate. Leaving is always explicit and gated by `canLeaveForm()`; the guard lives **inside** the eleven
  teardown functions (`navigate`, `goTab`, `selectBranch`, `enterFocusMode`, `exitFocusMode`, `loadNewChildDetail`,
  the five `exit…Details`, the tree row click), not at their call sites, and detects edits by comparing against a
  baseline, not by listening to events.
- **Collection weight decreases with depth**; collections are labels, not cards; `.ms-row-chip` is `nowrap`, long
  text goes in `.ms-row-line`; `_Branches.PackNamesInline` is the only grid left on the branch page.

### 8d. The JavaScript contract for a MultiStore partial

Four rules, all of them load-bearing. A partial that breaks one fails silently.

1. **One `<script>` per partial, and its body is an IIFE.** Nothing the partial defines is reachable
   from outside it. A function that has to be global to serve an `onclick` is the smell this removes.
2. **No Razor inside a `<script>`.** Server values arrive on a hidden config div as `data-`
   attributes and are read with `.data()`. Razor HTML-encodes `@T(...)` and `@Model` output into a JS
   string literal — an apostrophe becomes `&#x27;`, a quote terminates the literal — while attribute
   values encode and decode symmetrically. Action URLs go the same way: `@Url.Action(...)` on the
   div, never a hard-coded path in the script.
3. **A partial that delegates to a long-lived panel must `.off('.ns')` first and namespace every
   handler.** `#pnlists-panel`, `#cc-panel`, `#branch-addresses-panel` and `#branch-panel` are
   created once and only have their `innerHTML` swapped; `document` is never replaced at all.
   Handlers bound to elements *inside* the replaced markup are safe — those are new nodes each render.
4. **Grid buttons carry `data-id` and nothing else.** The render functions live in
   `Admin.StoreNav.render.*` and `RenderCustom` emits the dotted path as a raw JS expression. The
   row's other fields come from `$(grid).DataTable().row(tr).data()`, never interpolated into markup.

Errors go through `display_nop_error({ error: ... })`, nop's own path, which also handles a ModelState
error array. Popup refresh buttons keep their `id` — it is the popup's `btnId` contract — and take
`class="ms-grid-refresh" data-grid="#the-grid"`, handled once in `Admin.StoreNav.init`. A button that
reloads a server-rendered panel instead of a DataTable keeps its own handler and says so in a comment.

**The Subsidy tabs follow the same contract** (phase 3, 2026-09-29): `_Subsidies`, `_ProductGroups` and
their detail partials. Two additions came with them. A **child grid inside a tab's detail panel** gets its
own button class (`btn-subsidy-variation-edit`, not `btn-tab-edit`), because the detail panel sits inside
`#tab-panel-*` and the tab's delegated handlers would otherwise catch the click. And the **packname
subsidies panel is rendered on two pages** — the store page and nop's customer page, which does not load
this module — so its render functions live in their own namespace, `CW.PackNameSubsidies`, declared once
by the component view (`window.CW = window.CW || {}; CW.PackNameSubsidies = CW.PackNameSubsidies || …`),
and its handlers delegate from `.packname-subsidies-container`.

### 8e. Added after phase 7 (2026-10-08) — four found by using the page

1. **A ready callback never runs before the script that follows it.** nop's `Table` partial initialises its
   DataTable inside `$(function () { … })`, and jQuery 3 runs those callbacks asynchronously even when the DOM is
   already ready. A partial injected by `loadPartial` whose own script runs `updateTable('#grid')` on load therefore
   refreshes a grid that does not exist yet: `$('#grid').DataTable()` creates an empty table and the real
   initialisation a tick later is refused with "Cannot reinitialise DataTable". Refresh a grid only from a handler or
   a callback; a toggle that runs on load guards the call with `$.fn.dataTable.isDataTable(sel)`
   (`_Branches.PackNameListDetail.cshtml`, change 63). Every other refresh in the plugin already runs from a click,
   an AJAX callback or the grid's own `draw.dt`.
2. **The colour field is a field with the wheel under it** (`_Branches.PackNameDetail`, `_PackNames.PackNameDetail`,
   and since change 69–70 `_ProductGroups.ProductGroupDetail`): farbtastic's wheel is 195px wide, so a `display: flex`
   column with the field beside it collapses the field and pushes the wheel out of the panel. When the wheel is built
   in callback form (because something else must repaint on the same event), the callback paints the field itself —
   `val(colour).css({ backgroundColor: colour, color: wheel.hsl[2] > 0.5 ? '#000' : '#fff' })` — which is exactly what
   farbtastic's own `linkTo` does. A new product group's default colour is minted in the model factory
   (`SubsidyModelFactory.NewProductGroupColour`), never in Razor — a view does not assign the model — and the preview
   icon is painted with the group's colour. A packname's colour is checked on every admin save
   (`Helpers/PackNameColourHelper.Normalize`, 5.00): only a hex colour is kept; a new packname without one gets a
   random colour, and an edit that posts something else keeps the colour it had.
3. **No `text-decoration: underline` on a flex control.** The line runs under the caret and the gap too and reads as
   a stray rule. `.ms-disclosure` and `.ms-collection-action` darken on hover instead (`#2a6ebb`); they were the only
   two hover underlines in `Content/Admin`.
4. **The branch title is a second handle on the branch details.** `.ms-branch-title` (name · code · staff) shares the
   disclosure's click handler, carries its tooltip and a pointer; a new branch keeps a plain heading because its form
   is always open.

Watch for: the packname subsidies card (`PackNameSubsidies/Default.cshtml`) stayed on "Loading…" once and could not be
reproduced; its loader is `$.get(...).done(...)` with no `.fail`, so a failed request leaves the message. If it
recurs, read the Network status of `/Admin/Subsidy/PackNameSubsidies` first.

---

### 8f. Added by phase 8 Part 1 (2026-10-11) — shipping pickers, provisioning, the standalone Branches page

1. **An override that is unticked is not posted.** `checkOverriddenStoreValue()` disables the input of every unticked
   *override for this store*, and a disabled input is not in the POST, so it binds `false` / `0`. Never derive one
   override flag from another field's posted value: `StoreSettings`, `GeneratePackNames` and `GeneratePackNameLists`
   used to recompute the lists-switch and name-rule overrides as "posted ≠ stored" whenever the staff override was
   ticked, and one save silently replaced store 1's automation settings. Save every flag as posted; a job that needs a
   setting reads the value saved for the store, not the posted one (`LoadSettingAsync<MultiStoreSettings>(storeId)`
   after the saves — they clear the cache).
2. **Two pickers with one id in one page.** The store tab and the branch detail are in the same DOM; nop's
   `MultiSelectString` template starts select2 with `$("#id")` and the store tab's override checkbox disables `#id`, so
   both hit whichever element comes first. The branch property is therefore `BranchShippingMethods`, the store's
   `SelectedShippingMethods`. Give every new multi-select on this page an id no other tab uses.
3. **Shipping methods are names the CW.Freight plugin offers.** `IPackNameShippingMethodService.GetShippingMethodNamesAsync(storeId)`
   asks the active `CW.Freight` rate-computation plugin (a request with one empty item — it refuses an empty request),
   so CW.Core keeps no reference to CW.Freight. nop's multi-select accepts typed tags, so every save filters through
   `KeepOfferedShippingMethodsAsync`: the store settings (both scopes), `SaveBranch` and the standalone branch forms. A
   branch's selection overrides the store's (`BranchService.GetShippingMethodsByBranchId`); empty means "the store's";
   the first listed method is the default at the checkout. A refused branch save re-renders the picker with its options.
4. **Provisioning is called, not consumed.** `IPackNameProvisioningService` runs from `SaveBranch` and
   `BranchController.Create` (`ProvisionNewBranchAsync`: new branch → default list when `UseBranchesAsPackNameList`,
   named by `PackNameListNameFrom`) and from `ExtendedCustomerController.Create` / `Edit` (branch link set or changed →
   a packname when `UseStaffAsPackName` and the customer has none, in the branch's default list, created if missing;
   name falls back to the e-mail, then the username). `Create` calls `ProvisionCustomerBranchAsync`; `Edit` first asks
   `CheckCustomerBranchChangeAsync` and then calls `ApplyCustomerBranchAsync`, which also moves an existing packname into
   the new branch's default list (5.00, CW.Core part 4b.1). Both entry points check the PackName feature for the
   branch's store. Provisioning is not an event consumer: the manager's add-packname flow inserts the branch link before
   its own packname, and a consumer would create a second one. The **reverse** direction is a consumer, and it creates
   nothing: `PackNameCustomerBranchEventConsumer` (packname inserted or updated, list updated) puts the packname's
   customer in the branch of the packname's list (`SetCustomerBranchFromPackNameAsync`,
   `SetCustomerBranchesFromPackNameListAsync`).
5. **The standalone Branches page (`BranchController`, `Views/Branch/*`) is live and kept** — the manager wants it, as in
   4.1. Anything a branch save does on Manage Store must be done here too: the shipping picker and filter, provisioning
   on create, the same-store parent check (and, because this page can re-parent, the loop check: a branch cannot sit
   under itself or a sub-branch), never writing the store on an edit, and Manage Store's delete rule (sub-branches,
   packname lists or cost centres keep the branch). A refused create keeps the store it chose. Its Create form prepares
   the parent list and the methods for the store it opened with; choosing another store does not reload them.
6. **`DomainRegistrationFilter` is gone from the UI** (both forms, `BranchModel`, the resources; the mapping ignores
   the entity column). The column stays unused.

### 8g. Added by phase 8 Part 2 (2026-10-19) — the Excel import, registration and the packname save rules

1. **Two ways in, one registration.** The storefront add form (`PackNameController.AddPackName`) and the manager's
   Excel import (`PackNameController.ImportPackNames` → `IPackNameImportService`) both map to a `PackNameRegistration`
   and call `IPackNameRegistrationService.RegisterAsync`: account (random password, profile, attributes, default
   packname roles, `CustomerBranch`), packname, delivery address, subsidy assignments (a variation when credit or dates
   are given), welcome e-mail and activity line, in **one** transaction (`PackNameTransaction.Begin` — a
   `TransactionScope` with async flow and *read committed*; a bare `TransactionScope` is serializable). A refused
   account leaves nothing behind. Anything a registration must do is added there, not in a caller. The admin
   *Create packname for customer* popup and the admin saves are separate writers and follow the rules below on their own.
   **Two importers (phase 12).** The administrator's import (*Manage Store › PackNames › Import from Excel*,
   `MultiStoreController` region *PackName Import*) is the same service: `BuildAdministratorLayoutAsync(storeId,
   administrator)` beside the manager's `BuildLayoutAsync(manager, storeId)`, both through the protected
   `BuildLayoutForImporterAsync`. `PackNameImportLayout.Manager` is null for an administrator and `CreatedByCustomerId`
   names who imports: the administrator is the creator of the packnames, subsidy assignments and variation history, and
   the activity type is `ImportPackNames` (*Import packnames from Excel (admin)*) instead of `PublicStore.ImportPackNames`.
   An administrator's row goes into `IPackNameRegistrationService.ResolveTargetPackNameListAsync(branch)` (the branch's
   active default list, else its first active one — the manager overload calls it when `LimitAssignedBranches` is off),
   and `PackNameRegistration.ByAdministrator` lets `AssignSubsidiesAsync` skip `AllowSubsidySelection` (the Subsidy
   feature still decides). *Allow managers to add packnames* and *Allow to add by Excel* do not gate the admin import.
   The file check (`CheckFileAsync`) and the copy of the upload into memory (`ReadFileAsync`) live in the service, and
   `PackNameImportResultExtensions.ToPageAnswer()` builds the page's camelCase JSON, so both controllers share them.
   Welcome e-mails of an admin import go in the store's default language when it has a published one, otherwise the
   administrator's working language.
2. **The import checks everything first, then writes in batches of ten.** Every row is checked — against the file
   (duplicate names, e-mails, usernames), the store (branches, departments, cost centres, subsidies, existing names and
   accounts) and nop's own `CustomerInfoValidator` / `AddressValidator` (the add form's rules; do not re-write a rule
   nop already applies) — and nothing is written unless all pass. The rows are then written
   `PackNameImportDefaults.WriteBatchSize` (10) per transaction, so the store's other pages never wait for the whole file
   (one transaction for 500 rows froze every other customer's pages for 21 s). Inside a batch every call is awaited in
   sequence: two connections under one scope promote it to a distributed transaction. A row refused while it is written
   (a name or e-mail taken since the checks) or an exception rolls back its batch and stops: the batches before it stay,
   `PackNameImportResult.FirstRowNotImported` says where it stopped and the page shows what was created. Exceptions are
   logged after the scope is gone, so the log row is not rolled back. Lists the write creates are kept in a local
   dictionary, never re-read: a cache filled inside the scope survives its rollback.
3. **The template follows the store, and the file must match it.** Columns are found by header (order free, unknown
   ignored): Branch and Name required; Group, Department, Cost centre; Employee number, Gender, Print name, PO entry,
   Colour, Comments per their `*Enabled` settings; `AllowSubsidyCountByExcel` groups of Subsidy / credit / start / end
   (Subsidy feature on + `AllowSubsidySelection`; credit with `PackNamesOwnGlobalValue`, dates with
   `PackNamesOwnStartAndFinishDates`); the account block with `AllowToAddCustomerToPackName`; the address block with
   `AllowHomeDelivery`. The settings that say what **managers** may do shape only a manager's template: an
   administrator always gets the account block and gets the subsidy columns whenever the Subsidy feature is on and the
   store has active subsidies, whatever `AllowToAddCustomerToPackName` / `AllowSubsidySelection` say; the settings that
   describe the store (field switches, `AllowHomeDelivery`, own value / dates, `AllowSubsidyCountByExcel`) apply to both.
   A column the store no longer takes is refused when it holds values; a column given twice is
   refused; e-mail or address columns without their *Add customer* / *Add address* column are refused as missing it.
   Only rows that hold a value are read (`RowsUsed(XLCellsUsedOptions.Contents)`); 500 rows and 2 MB at most. Options
   that share a name are told apart in the dropdowns ("Winter #19", "Cost centre (Branch)").
4. **One name per list on every manual writer** — storefront add and edit, the import (checked first and again inside
   the batch's transaction) and the three admin saves (`IPackNameService.PackNameNameExistsAsync`, ignoring case) — for
   a new packname, a new name or a move; a packname saved with its own name keeps it. Provisioning is exempt (decision
   G1), so two packnames may share a name. Nothing in the database enforces it (gap analysis §17b "From phase 8 Part 2" #2).
5. **Switched off means takes nothing new.** A switched-off list or branch takes no new packname and no move on any
   path (form, import, admin saves, popup); a packname already there still saves. A branch whose lists are all off
   counts as having none: the import and the admin saves create its list (`EnsureDefaultPackNameListAsync`, active, the
   default only when the branch has none), the storefront form refuses ("The selected branch has no active packname
   list."). Opening a page or popup never creates a list (lesson #114). Under `LimitAssignedBranches` the import offers
   only the branches where the manager owns a list, so it never creates one there. `LimitAssignedBranches` is a manager
   setting: an administrator is offered every active branch of the store.
6. **The account gets its own copy of the delivery address** (billing and shipping), never the packname's row: nop's My
   Account, checkout and admin customer pages delete an account's address row outright. Without a delivery address the
   account gets one from its profile fields when they make a valid address. Company is the branch's name; no VAT number
   is asked. The packname's *Addresses* tab and the checkout compare by content, so the copy is not listed twice.
7. **Every key a save reports on must show on each form that posts to it** (lesson #116): the admin saves report list
   problems on `PackNameListId`, which both the PackNames tab and the Branches panel render.
8. **The import page** (`Views/PackName/ImportPackNames.cshtml`, `Content/PackName/packname-import.js` / `.css`) reads
   only the import's JSON (`success`, `rowCount`, `packNamesCreated`, `customersCreated`, `welcomeEmailsQueued`,
   `packNameListsCreated`, `lists`, `fileErrors`, `rowErrors`, `hasErrorReport`, `firstRowNotImported`, or a
   `message`); anything else (a signed-out session answers with a page) shows "The import could not be completed".
   The error report re-posts the same file to `ImportPackNamesErrorReport` with the page's form. The page has its own
   button styles: the theme's `button-1` renders as a grey outlined box there. The body is the shared partial
   `Views/PackName/_PackNameImport.cshtml`; each page sets `TemplateUrl`, `ImportUrl` and `ErrorReportUrl` on
   `PackNameImportModel` (the admin page also `StoreId`, posted as a hidden field). The admin page
   (`Areas/Admin/Views/MultiStore/ImportPackNames.cshtml`) wraps it in an AdminLTE card with a link back to
   `/admin/multistore/edit/{id}#packnames`. The report form has a literal `action`, so it carries
   `asp-antiforgery="true"`. The script shows `message` or `error` (the feature filter answers `{ success:false, error }`).

### 8h. Added by phase 11 (2026-10-21) — bulk actions, export and the selector's server search

**Bulk actions.** Both packname grids (the *PackNames* tab and an open list on the *Branches* tab) share one toolbar
(`_PackNames.BulkToolbar.cshtml`, `@model string` = the toolbar's id) and one script, `Content/Admin/MultiStore/admin.packname.bulk.js`
(`Admin.PackNameBulk { render.check, bind, unselect }`, loaded by `Edit.cshtml`; texts from `_CreateOrUpdate.cshtml`'s
`bulk.*` keys). The rules:

1. **Selection is per grid**, kept across pages, cleared when the grid's search changes (`preXhr`), at most 500. nop's master
   checkbox is not used: it keeps one page-global `selectedIds`, and the store grid and an open list grid are on the page
   together. A single row delete calls `unselect`; an action's answer takes the packnames acted on out of **every** grid's
   selection and redraws every packname grid. An empty list grid hides its toolbar.
2. **`BulkPackNames` → `IPackNameBulkService.ApplyAsync`** (`Services/PackNames/Bulk/`). Refused whole, nothing written: no ids,
   more than 500, an action name that is not exactly one of `Activate`, `Deactivate`, `Delete`, `Move` (case is ignored —
   `delete` **is** Delete; `Enum.TryParse` is not used because it takes `7` or `Activate,Deactivate`), or a move target that
   is not a switched-on list of the store in a switched-on branch. Otherwise **ten packnames per transaction**
   (`PackNameBulkDefaults.WriteBatchSize`), and each batch **reads its packnames, their lists and the move target again inside
   its transaction** (lesson #117): a packname edited, or a target switched off, while an earlier batch was written is
   acted on as it is now. Per packname: gone / deleted / other store → skipped "it no longer exists in this store";
   activate/deactivate already so, or move into its own list → unchanged; a name the target list has, or one moved into it
   earlier in the same action → skipped "the list X already has a packname with this name"; otherwise the single save's or
   delete's call, with one `EditPackName` / `DeletePackName` activity row. An exception rolls back its batch and stops; the
   batches before it stay; the error is logged **after** the transaction (a log row inside it would roll back).
3. **Delete is the single delete** (soft): the delete event removes the subsidy assignments (soft) and cart lines (hard).
   The question counts managers and accounts ("the accounts are kept"). The manager mapping rows
   (`CORE_PN_PackNameListManagers` / `CORE_PN_DepartmentManagers`) are kept — they have no soft delete and nothing but the
   list row's count read them; `GetPackNameListDetailsByBranchAsync` now counts only managers that are not deleted.
4. **Caches after the commit.** `PackNameCacheEvenConsumer` clears on each write, which is before the batch commits; a
   reader that does not wait for the batch's locks can cache the old row meanwhile. So after each committed batch the service
   clears again every key the consumer clears (`ClearPackNameCachesAsync`; a key added to the consumer is added there too).
   A failure of that clear is logged as a warning and the next batches run.
5. **Move targets** (`PackNameBulkMoveTargets`): the store's switched-on lists in switched-on branches, as "branch path >>
   list", by text; a list's own grid leaves itself out client-side. The single edit form offers only its branch's lists
   (branch, then list); the bulk move offers every list of the store.
6. **Export** (`ExportPackNames`): fetched by the script with `X-Requested-With`, so a feature or permission refusal answers
   JSON and shows its message (nop's `display_nop_error`, a browser alert) instead of redirecting; the page never navigates.
   The scope is the grid's store, list and **last searched** filters (not what is typed and not searched yet), or the ticked
   rows when there are any; `PreparePackNamesForExportAsync` runs the grid's own `SearchPackNamesAsync` on every page.
   Columns follow the store's switches (employee number, print name, gender…) plus list, account e-mail and Active; every
   value is written as text (ClosedXML: `007` keeps its zeros, `=…` is not a formula).
7. **Measured** (2026-10-21, dev): 500 packnames activate or deactivate in 1.4–1.6 s; another customer browsing the
   storefront meanwhile saw no stall.

**The storefront selector** (`Views/Shared/Components/PackNameSelector/Default.cshtml`, `PackNameController.PackNames` /
`PackNameCartCounts`, `Helpers/PackNameSelectorHelper.SelectPage`) sends one page: the first 50 by name, plus the selected
packname and every packname with cart lines; `HasMore` when the roster holds more. Typing asks the server only when the
first page said `HasMore` (contains, ignoring case, on the name and — when the store shows employee numbers — the employee
number); a short list is searched locally. The cart-changed event refreshes only the badges (`PackNameCartCounts`, POST).
Every server value the script needs is on the config div `#pack-name-selector-config`. The rosters the service caches are
read-only (`IReadOnlyList<PackNameDTO>`, no `InCart`): writing per-customer values onto a cached DTO shared them between
managers. The list dropdown offers only lists and department entries that load at least one packname, always including
the manager's own list; a packname that cannot order is shown disabled — or, when it is the one in context, selected
with the reason as its tooltip (§7b.2, `subsidy-system.md` §3.2). Measured on a list of 602: the answer fell from 180,597 to 16,065 bytes; the SQL per request did not
(about 60–70 ms), because `vw_CORE_SUB_SubsidyPackNameCanOrder` costs 20–40 ms per call whatever the number of ids and
`vw_CORE_PN_PackNameShoppingCartItem` about 20 ms (gap analysis §17b).

**The remembered list follows a moved packname.** `PackNameContext` updates `SelectedPackNameListId` to the selected
packname's list when the selection is still valid and the remembered id is a real list (positive) other than the
packname's; a department entry (negative id) stays.

---

## 9. Recipe — Adding a New Tab

1. Add the nav `<li>` and the empty `<div id="tab-content-x"><div id="tab-panel-x"></div></div>`
   shell to `_CreateOrUpdate.cshtml` (wrap **both** in the same visibility condition).
2. Add a branch to `loadPanels()` in `admin.store.navigation.js` and an entry in the
   `loadTabPanel` URL map.
3. Add a `[CheckPermission] public async Task<IActionResult> X(int storeId)` action returning
   `PartialView("_X", model)`.
4. If the tab has drill-down state, add the id to `_state`, `buildHash()`, `parseHash()`,
   `goTab()`, `selectBranch()`, `missingIds()`, `fetchMissingNames()`, `updateBreadcrumb()`,
   and to the `BreadcrumbData` action. **All nine** — missing one produces a breadcrumb that
   shows `Entity #12` instead of a name, or a hash that does not round-trip.
5. If it has an edit form inside the branches tab, reuse `bindFocusForm` and honour the
   HTML-vs-JSON response contract.

---

## 10. File Map

```
Areas/Admin/
├── Controllers/MultiStoreController.cs          all MultiStore actions
├── Controllers/SubsidyController.cs             ProductGroups + Subsidies tabs
├── Controllers/ExchangesConfigController.cs     global fee settings
├── Factories/MultiStoreModelFactory.cs
├── Factories/CWSettingModelFactory.cs          both settings scopes
├── Factories/PackNameModelFactory.cs
├── Factories/SubsidyModelFactory.cs
├── Factories/CWAddressModelFactory.cs
├── Models/MultiStores/                          MultiStoreModel, Department*, CostCenter*, BranchTree/Node, BreadcrumbItem
│                                                 + CostCentersModel / CostCenterRowModel  (2c: branch collection)
├── Models/Branches/BranchAddressesModel.cs      2c: branch address collection
├── Models/PackNames/PackNameListsModel.cs       + PackNameListRowModel
├── Models/Settings/MultiStoreSettingsModel.cs
├── Validators/MultiStores/                      DepartmentValidator, CostCenterValidator (the settings validator was deleted with store self-registration)
├── Components/ExchangeSettingsComponent.cs      exchangesettings tab
├── Components/FeeSettingsComponent.cs           feesettings tab
└── Views/MultiStore/
    ├── Edit.cshtml  List.cshtml  Settings.cshtml
    ├── _CreateOrUpdate.cshtml  _StoreInfo.cshtml  _StoreSettings.cshtml
    ├── _Branches.*.cshtml       branches tab panels
    ├── _Departments*.cshtml  _CostCenters*.cshtml  _PackNames*.cshtml  (_PackNames.BulkToolbar.cshtml: both grids' toolbar)
    └── *Popup.cshtml            address / manager / customer-association popups

Domain/Settings/MultiStoreSettings.cs
Domain/Settings/MultiStoreSettingsChangedEvent.cs
Services/EventConsumer.cs                        admin menu + settings-changed handler
Content/Admin/MultiStore/admin.store.navigation.js   the SPA shell
Content/Admin/MultiStore/admin.packname.bulk.js      both packname grids' selection, bulk actions and export (§8h)
Services/PackNames/Bulk/                          IPackNameBulkService + request / result / defaults (§8h)
Helpers/PackNameBulkHelper.cs  Helpers/PackNameSelectorHelper.cs
Areas/Admin/Models/PackNames/PackNameBulkModel.cs
Content/Admin/MultiStore/multistore.css
```

> Both of those are bundled and minified by WebOptimizer, and the bundle is **disk-cached** in
> `Presentation/Nop.Web/wwwroot/bundles`. Empty that folder after editing either — see gotcha 10.
