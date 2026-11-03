# Integration.Odoo — plugin reference

What the plugin is and how it works, for a developer meeting it cold. The 5.00 port is recorded in
`CWPlans/Odoo_Port_Spec.md`; the 4.9 design with its per-phase test plans is in the 4.90 source's
`CWPlans/Odoo_Store_Department_Product_Sync_Spec.md` (`CLAUDE.md` §3.1).

## 1. What it does

A one-way sync from Odoo 19 to nopCommerce over Odoo's JSON-2 API (`POST {url}/json/2/{model}/{method}`,
`Authorization: bearer {api key}`, `X-Odoo-Database`). Nothing is written back to Odoo.

| Sync | Odoo source | nop / CW.Core target |
|---|---|---|
| Stores | `x_store` | nop `Store`, linked through `Odoo_StoreMapping` |
| Departments | `x_department` (its `x_studio_store`) | CW.Core `Department` and its `DEP_{storeId}_{name}` customer role |
| Products | `product.template` / `product.product`, attributes, images | nop `Product`, attributes, combinations, pictures, categories, store mappings, department ACL |
| Branches | `x_store.x_studio_companies` (`res.partner`, type company) and their delivery addresses | CW.Core `Branch`: the company a top-level Head Office, each delivery address a branch under it |

**Run order: Stores → Departments → Products → Branches.** Each sync resolves its parents from the ones before it;
a record whose parent is not mapped yet is logged and skipped. The Sync Log page shows the buttons in that order.

## 2. Rules carried from the 4.9 design

| # | Rule |
|---|---|
| D3 | A store with no URL in Odoo is still created, with empty Url / Hosts. |
| D4 | Only active stores and departments are created. nop stores are never deleted. |
| D5 | Incremental: a store or product is skipped only when **both** `x_studio_sync_revision` and `write_date` are unchanged. Stores keep them on the mapping row, products in the `Odoo.ProductSyncMarker` generic attribute (`revision|write_date`). Departments upsert every run. |
| D6 / O8 | Identity. Stores: `Odoo_StoreMapping` (one-to-one, unique on both columns). Departments: CW.Core's natural key (store, name). Products: `ManufacturerPartNumber = ODOO_{template id}` first, then SKU, never a product linked to another template. Combinations: `ODOO_{product.product id}`, then the 4.9 composite link, then the variant code. Branches: `CardCode` = Odoo partner id. |
| D8 | A department **is** a role: created through CW.Core (`IMultiStoreService`, `ICustomerOverrideService.SaveDepartmentCustomerRole`), never reimplemented here. |
| D10 | Products with no store or department still sync. |
| D11 | Archived in Odoo → reflected in nop: department deactivated, product unpublished (never deleted, so a reactivation finds it again) with its marker cleared, delivery address → branch deactivated, store → logged only. Archived records are never created. |
| D12 | Store updates never destroy nop configuration: an empty Odoo URL never blanks Url / Hosts, an Odoo host is added to the host list, `DisplayOrder` and `SslEnabled` are set on create only, and a new store never sorts ahead of the existing ones. |
| D13 | Head Office = top-level branch; the parent is re-applied on every update, since Odoo owns the hierarchy. A new branch then goes through CW.Core's `ProvisionNewBranchAsync`, as one created in the admin does: its default packname list, when the store uses branches as packname lists and has the PackName feature on (added in 5.00; 4.9 skipped it). |
| D14 | Images: Odoo sets a product's first image; after that nop owns the pictures. A later change to Odoo's main image is logged once (SHA-256 fingerprint in `Odoo.MainImageFingerprint`), never applied. |
| O3 | Product visibility fails **closed**: a new product is created restricted, and unresolved stores / departments restrict it to what did resolve; the marker is saved only when both resolved, so the product is retried. |

## 3. Structure

| Part | Where |
|---|---|
| Admin pages | `Controllers/OdooAdminController` — Configure (settings, Test Connection, Run Diagnostics) and Sync Log (sync buttons, events grid with a child grid of traces, trace view). All actions `[CheckPermission(MANAGE_PLUGINS)]`. Views in `Views/OdooAdmin/`, script `Content/Admin/Odoo/odoo-admin.js` (server values through `.odoo-configure-config` / `.odoo-synclog-config`). |
| Background syncs | `IOdooSyncQueue` / `OdooSyncQueue` over a bounded channel; `OdooSyncBackgroundService` runs one request at a time, each in its own DI scope. A sync type is claimed when queued and released in a `finally` when done, so the same sync cannot be queued twice. |
| Odoo API | `IOdooApiService` / `OdooApiService` (named `HttpClient`, 30 s timeout). Responses in `Models/Responses/`; Odoo's `false` for an empty field and its `[id, name]` many-to-ones are read by the converters in `StringOrFalseJsonConverter.cs` and `OdooMany2OneConverter.cs`. A record that cannot be read is reported in the page's errors, not thrown. |
| Syncs | `StoreSyncService`, `DepartmentSyncService`, `ProductSyncService`, `BranchSyncService`; each counts every record once (`SyncTally`). |
| Sync log | `Odoo_LogEvent` (one row per sync run with its totals) and `Odoo_Log` (its traces); `IOdooLogService`. |
| Settings | `OdooSettings`, global (store 0): URL, database, username, API key, last sync. |
| Locale | `Infrastructure/OdooLocaleResources.cs`, keys `Plugins.Integration.Odoo.*`, installed on install and on a DATA version update. |

**CW.Core coupling** (`CLAUDE.md` §10.2 Pattern C): the `.csproj` references the built
`Presentation/Nop.Web/Plugins/CW.Core/Nop.Plugin.CW.Core.dll` with `Private=false` / `ExcludeAssets=runtime`, and
`NopCommerce.sln` builds CW.Core first. Without `Private=false` a second CW.Core DLL would land in this plugin's folder.
`plugin.json` declares `"DependsOnSystemNames": [ "CW.Core" ]`, so nop refuses to install the plugin before CW.Core:
installed without it, the plugin's types cannot load and **the site does not start**.
The services used: `IBranchService`, `IMultiStoreService`, `ICustomerOverrideService`, `IAclOverrideService`.

**Feature switches.** The plugin reads no CW.Core feature switch. Departments, branches and department ACL are written
whatever `MultiStoreSettings` says, as in 4.9: the sync fills the data, the switches decide what the store uses.

## 4. Odoo 19 and Odoo 20

Checked 2026-10-28 against the client's Odoo 20.0+e test instance and Odoo's 19.0 / 20.0 documentation.

- **API.** The plugin uses only the JSON-2 API (`POST /json/2/<model>/<method>`, bearer key, named parameters) and the
  unauthenticated `/web/version`. Odoo's JSON-2 page is the same for 19.0 and 20.0. What Odoo 20 changed is the legacy
  XML-RPC / JSON-RPC API (its `db` service removed; `common` / `object` removed in Odoo 22), which the plugin does not use.
- **Models and fields.** Every model, field, filter field and method the plugin uses exists on Odoo 20 with the type
  it expects (`product.template`, `product.product`, `product.template.attribute.line` / `.value`, `product.attribute`,
  `res.partner`, `res.users.context_get`, `x_store`, `x_department`).
- **Images.** Odoo 20 returns every binary field as an object `{content, size}`; the 4.9 run on Odoo 19 saw that shape
  only for some records. All three image fields read go through `OdooBinaryJsonConverter`, which takes both forms.
- **Attribute display types.** Odoo 20 offers `radio`, `pills`, `select`, `color`, `multi`, `image` and `range`. The
  plugin maps the first four; any other becomes a nop dropdown. The test instance uses only `radio` and `color`.
- **Partner types** are `contact`, `invoice`, `delivery`, `other`; the plugin reads `delivery`.
- **Instance prerequisites, either version.** External API access needs a Custom Odoo plan. An API key lasts at most
  three months, so the key in *Odoo › Settings* must be renewed before it expires (an expired key fails every sync and
  Test Connection). Odoo refuses a `search_read` that names a field it does not have, so the instance needs the apps
  whose fields the plugin reads: **Website / eCommerce** (`is_published`, `description_ecommerce`,
  `website_description`, `compare_list_price` on templates) and **Inventory** (`qty_available` on variants, which sets
  each combination's stock quantity).
- **Client contract.** The Studio models are the client's, not Odoo's: on the test instance `x_studio_companies` is a
  one2many (via `res.partner.x_studio_estore`). Every requested field is read except the core `product_variant_ids`;
  the unused `x_studio_external_id` and `x_studio_contract_version` requests were removed on 2026-10-28.

## 5. Tests

`Tests/CW.Tests/Integration.Odoo/`: the sync queue (claim, refusal, release, release on a failed write), `SyncTally`,
and the Odoo JSON converters. The syncs themselves need a live Odoo and are tested by hand (4.9 spec §5.6, §6.5, §7.5,
§8.3).

## 6. Open items

| # | Item |
|---|---|
| O1 / O2 | Meaning of `x_studio_external_id`, and first-run matching to stores that already exist in nop (by host, then name). |
| O4 | `write_date` is the template's: an edit that touches only a variant or an attribute value's `price_extra` is skipped until the template is next written. |
| O6 | **Closed (CW.Core design).** CW.Core 5.00 defines an inactive department as "no new assignments": access it already gives stays. So an archived Odoo department keeps the product ACL it had, and `SaveAclDepartmentAsync` adds no new inactive department. |
| O10 | `SaveAclDepartmentAsync` also drops a department whose store the product is not mapped to, and the sync still marks the product synced without logging it (as in 4.9). |
| P1 | `OdooSettings.ApiKey` is stored in plain text in `Setting`. |
| P2 | **Closed 2026-11-02.** The plugin list showed CW.Core's logo for Odoo. The cause was stale files in `Plugins/Integration.Odoo/` (CW.Core's `Areas/`, `Content/`, DLLs and `logo.png`, left by an earlier build); the current build writes only the plugin's own files plus `refs/Nop.Plugin.CW.Core.dll`, which Razor runtime compilation needs. The plugin now ships its own `logo.png` (CW style, "ODOO"), copied by a `.csproj` entry. A checkout whose output still holds the old files needs that folder emptied and rebuilt. |
| P3 | **Run 2026-10-28** against the client's Odoo test instance (Odoo 20.0+e; address in `CLAUDE.local.md`): all four syncs, a no-change second run and a change run pass (`CWPlans/Odoo_Port_Spec.md` §5.1). |
| P4 | **Closed 2026-10-28.** The Store and Department fetches requested `x_studio_external_id`, which nothing read; an Odoo without it refused the whole request. Removed from both requests and response classes. A field the plugin requests must exist in every Odoo it syncs from: Odoo fails the whole `search_read` on one unknown field. |
| P5 | An Odoo image nop cannot decode (an SVG, such as Odoo's default *Expenses* icon) is logged as a warning and the product syncs without a picture. |
