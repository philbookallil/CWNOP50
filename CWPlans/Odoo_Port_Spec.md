# Integration.Odoo — port from 4.90 to 5.00

Started 2026-10-26. Source: the 4.90 source's `Plugins/Nop.Plugin.Integration.Odoo` (`CLAUDE.local.md` §3.1). Target:
`Plugins/Nop.Plugin.Integration.Odoo` in this repository. `CWCore_Port_Strategy.md` D2 makes this plugin a go-live
prerequisite: 5.00 takes no data from 4.1 or 4.9, so stores, departments, branches and products arrive through this sync.

## 1. Decisions (2026-10-26)

| # | Decision |
|---|---|
| O1 | **Port and bring to CW standards.** The `CLAUDE.md` §10.5 checklist, then the CW conventions (region order, `[CheckPermission]`, no Razor in scripts, locale resources, a reference document, unit tests). Sync behaviour does not change. |
| O2 | **CW.Core coupling stays Pattern C** (`CLAUDE.md` §10.2): a DLL reference to the built `Nop.Plugin.CW.Core.dll` with `Private=false` / `ExcludeAssets=runtime`. The plugin uses `IBranchService`, `IMultiStoreService`, `ICustomerOverrideService.SaveDepartmentCustomerRole` and `IAclOverrideService.SaveAclDepartmentAsync`, all present and registered in CW.Core 5.00 (phase 2). CW.Core must be built first. |
| O3 | **The plugin is CW-owned** on the same terms as `ExternalAuth.SSO` and `Payments.NABGateway`: its name follows nop's `Integration.*` pattern and it keeps the SystemName `Integration.Odoo`. nop 5.00 ships no Odoo plugin, so there is no collision. |
| O4 | **One installation migration.** No 5.00 database has the Odoo tables, so the six 4.9 migrations (two create, two add-column, one unique-index, one create) collapse into one `SchemaMigration` that creates the three tables in their final shape, plus the two unique indexes on `Odoo_StoreMapping`. Table names are unchanged (`Odoo_Log`, `Odoo_LogEvent`, `Odoo_StoreMapping`). |
| O5 | **Locale keys move to `Plugins.Integration.Odoo.*`** (4.9 used the singular `Plugin.`). Nothing is installed in 5.00, so the rename is free. The strings live in `Infrastructure/OdooLocaleResources.cs`, as in the SSO plugin, and every key a model or view reads is installed (4.9 left the `LogEvent.Fields.*` keys out, so they rendered as lowercased keys). |

## 2. §10.5 checklist applied

| # | Item | In this plugin |
|---|---|---|
| 1 | `net10.0`, `SupportedVersions 5.00` | `.csproj`, `plugin.json` (`Version 2026_10_26_DATA_1.0`) |
| 2 | `IWebHelper` → `Nop.Services.Helpers` | `OdooIntegrationPlugin` |
| 3 | AutoMapper → Mapster | Not used (models are mapped by hand) |
| 4 | `Create.TableFor<T>()` removed | Replaced by `this.CreateTableIfNotExists<T>()` in the single migration (O4) |
| 5 | Views compile at runtime | Every view's types checked; each page loaded after the port |
| 6 | JSON keeps C# names | The sync actions all answer `{ ConnectionStatus, TestResultMessage }` (4.9's `SyncBranches` answered lowercase `success` / `message`); `Diagnostics` returns a model (PascalCase); the script reads those names |
| 7 | Package versions | No NuGet packages. The reference to `Nop.Web.Framework` is dropped (`Nop.Web` brings it); `CopyLocalLockFileAssemblies` false |
| 8 | Version bump | First 5.00 version |
| 9 | Copied nop bodies | None |
| 10 | Service locators | None; the background worker opens its own DI scope per request (`IServiceScopeFactory`), which 5.00 supports |
| 11 | Dependency on CW.Core | `plugin.json` declares `"DependsOnSystemNames": [ "CW.Core" ]`. Found in T3: without it nop installed the plugin on a site where CW.Core was not installed, and on restart the site would not start (`FileNotFoundException: Nop.Plugin.CW.Core` while loading the plugin's types). With it, Install answers "depends on … CW.Core" and queues nothing (`tasks/lessons.md` #21) |

## 3. CW standards pass

**Controller (`OdooAdminController`).** `[CheckPermission(MANAGE_PLUGINS)]` on every action instead of the inline
`AuthorizeAsync`; the per-action `[Area]` / `[AuthorizeAdmin]` duplicates go (they are on the class). `SyncProducts`,
`SyncBranches`, `SyncStores` and `SyncDepartments` all go through the one `QueueSyncAsync` (4.9 had two copies of it with
different JSON). Region order Fields → Ctor → Utilities → Methods.
- `TestConnection` is a GET (the page links to it) and tests the **saved** settings. 4.9 also bound a `ConfigurationModel`
  it never read, built a `Json(...)` result it threw away, and its comment described the opposite fallback to the code.
- `GetStores` is deleted: nothing calls it.

**Configure page.** The *Enable Odoo Integration* checkbox is removed with `OdooSettings.IsEnabled` and
`ConfigurationModel.IsEnabled`: the save never wrote it and nothing read it (`tasks/lessons.md` #12 — a form saves what it
displays, and here it displayed what it never saved). The inline script moves to `Content/Admin/Odoo/odoo-admin.js` with a
config div (`CLAUDE.md` §9.14, §9.15); its error handler read an undefined `data`.

**Sync Log page.** The two inline scripts (sync buttons, delete selected, the status renderer) move into the same file;
URLs come from the config div. `renderColumnEventInfo` stays global because `RenderCustom` names it.

**Dead code removed.** `Content/js/odoo-admin.js` and `Content/css/odoo-admin.css` (never referenced, never deployed —
not in the `.csproj`), `Views/OdooAdmin/_DiagnosticsResult.cshtml` (never rendered, and it read properties
`TestConnectionResponse` does not have), `Domain/OdooSyncLog.cs` (no table, no reader), the unused types in
`Models/OdooApiModels.cs`, the unused `OdooConstants` members, `IProductAttributeParser` in `ProductSyncService` (injected,
never called), the `DocumentFormat.*` and `Nop.Web.Models.Catalog` usings.

**Found during the port and fixed.**
- `Models/Responses/JSON/product.template.json` — a raw `search_read` dump from the client's Odoo, carrying its
  tenant URL. Referenced by nothing; **not brought into this public repository** (`CLAUDE.md` §0.9).
- `Services/OdooApiClient.cs` (marked DEPRECATED, never registered or called) and `Models/OdooApiModels.cs` (only it
  used them), `Models/Responses/TestConnectionResponse.cs` (replaced by `OdooConnectionResult`) — deleted.
- The trace grid rendered `ShortMessage` unencoded (`Encode = false`); messages carry Odoo product and partner names.
  Encoded now.
- Test Connection reported the API user's **language** as "Logged in as" (`context_get` → `lang`); it reports the
  user id now.
- The Configure page showed *Last Sync* as "now" when no sync had run, and in UTC; it shows nothing until a sync, then
  the admin's time zone. Saving redirects (post/redirect/get) instead of re-rendering.
- The Diagnostics card was hidden with `$('#diagnosticsResultCard')`, an id `nop-card` overwrites with its own; the
  script uses the card's real id.

**Admin menu.** Titles come from locale resources; URLs unchanged.

**Region order** in the services: helpers in one `Utilities` region (nested by topic in `ProductSyncService`) ahead of
the public methods (`CLAUDE.md` §7).

**Tests (`Tests/CW.Tests/Odoo/`).** Pure logic: `SyncTally`, `OdooSyncQueue` (claim, refusal, release, release on a
failed write), `StringOrFalseJsonConverter`, `OdooMany2OneConverter`. Internals reach the tests through
`InternalsVisibleTo`.

**Reference document** `Plugins/Nop.Plugin.Integration.Odoo/References/Odoo_Plugin_Reference.md`.

## 4. Not changed (noted for later)

- `OdooSettings.ApiKey` is stored in plain text in `Setting` (as in 4.9). Encrypting it is its own change.
- Sync behaviour, the Odoo JSON-2 calls and the order the syncs must run in (Stores → Departments → Products → Branches)
  are carried over as they are; the 4.9 design is in the 4.90 source's `CWPlans/Odoo_Store_Department_Product_Sync_Spec.md`.
- The view text is English in the markup, as in 4.9; only labels bound to model attributes use resources.

## 5. Test plan

| # | Test | Pass when |
|---|---|---|
| T1 | `dotnet build` of the plugin (CW.Core built first) | No errors; no new warnings in plugin code |
| T2 | `dotnet test Tests/CW.Tests` | All cases pass, the Odoo ones included |
| T3 | Install the plugin on the local site | Three tables and two unique indexes exist; locale resources installed; menu *Odoo › Settings / Sync Log* shows |
| T4 | Configure page: save URL, database, user, key; save again with the key blank | Values saved; a blank key keeps the stored one |
| T5 | *Test Connection* and *Run Diagnostics* with no reachable Odoo | Each shows a failure message, no server error |
| T6 | Sync Log page: each sync button | Answers *Queued* then a log event appears; a second click while running answers *Busy*; without a connection configured, *Failed* with the message |
| T7 | Expand a log event, open a trace, delete selected, clear log | Child grid loads, trace view opens, rows go |
| T8 | Feature-off case (`CLAUDE.md` §0.4) | The plugin reads no CW.Core feature data; it writes departments / branches / department ACL whatever the switches say, as in 4.9. Nothing to gate — recorded here so it is a decision, not an omission |
| T9 | Against a real Odoo 19 | Not run here (no instance); open |

### 5.1 Results (2026-10-26)

| Test | Result |
|---|---|
| T1 | **Passed.** `NopCommerce.sln` builds with no errors and no plugin warnings |
| T2 | **Passed.** 472 cases, the 14 Odoo cases (`Tests/CW.Tests/Integration.Odoo/`) included |
| T3 | **Passed 2026-10-27**, after CW.Core (`2026_10_26_DATA_0.1`) was installed. On 2026-10-26 the first install, with no CW.Core, stopped the site from starting; fixed with `DependsOnSystemNames` (§2 item 11). Now: three `Odoo_*` tables, `IX_Odoo_Log_LogEventId` and the two unique store-mapping indexes, 29 `Plugins.Integration.Odoo.*` resources and no `Plugin.Integration.Odoo.*`, settings without `IsEnabled`, menu *Odoo › Settings / Sync Log* |
| T4 | **Passed 2026-10-27**, after two fixes. (1) Both views died on first request: `AddScriptParts(…, Url.Content("~/…"))` throws in nop 5.00, which wants the raw `~/` path (`tasks/lessons.md` #24). (2) The save never happened: `<Nullable>annotations</Nullable>`, added in the port, made every non-nullable model `string` implicitly required, so the blank API key and the unposted status fields failed validation without a message (#23). The setting was removed with the dozen stray `?` annotations it covered. Then: saved, URL's trailing `/` trimmed, success notice, redirect; a second save with the key blank kept the stored key and saved the new database name |
| T5 | **Passed.** Against `https://odoo-test.invalid`: Test Connection shows *Failed — No such host is known*; Run Diagnostics shows the card with URL, database, key present and a red *Failed* badge |
| T6 | **Passed.** Each button answers *Queued* in run order and the worker writes one event per sync, *Done (errors)* with the DNS error traced; three simultaneous Store requests answer one *Queued*, two *Busy*. The *Failed* answer for an unconfigured connection was not exercised (the page cannot blank a saved key) |
| T7 | **Passed.** The child grid lists an event's traces; *View* opens the trace with its full message; *Delete selected* (with nop's confirmation) removes the event and its traces; *Clear log* empties both tables |
| T8 | **Passed by inspection.** The plugin reads no feature switch. The branch sync's new `ProvisionNewBranchAsync` call checks the PackName switch and `UseBranchesAsPackNameList` itself |
| T9 | **Passed 2026-10-28** against the client's Odoo test instance (its address is in `CLAUDE.local.md`), which runs **Odoo 20.0+e** (the plugin was written for 19; the JSON-2 calls work unchanged). Details below |

**T9 run (2026-10-28).** The instance had 2 stores and no departments, store-linked products or delivery addresses,
so test data was added in Odoo (all named *Test* / *CW Test*): departments 1–4 (*Test Warehouse* in both stores, *Test
Office*, one archived), a test company (partner 57) on store 1, delivery addresses 58–61, and POLO, Beanie, Cup
Respirator and Flat Fold Respirator linked to stores and departments. The instance also lacked `x_studio_external_id`,
which the plugin requests from both `x_store` and `x_department`; Odoo refuses the whole request (*Invalid field*), so
the Store and Department syncs cannot run without it. The two fields were added to Odoo (Text, empty).

| Run | Result |
|---|---|
| Test Connection / Diagnostics | *Connected*: Odoo 20.0+e, 53 products, API user id 9 |
| Stores, first run | 2 created and mapped (no URL in Odoo, so empty Url / Hosts; display order after the existing store; SSL off) |
| Departments, first run | 3 created, each with its store's `DEP_{storeId}_{name}` role (the same name in two stores gives two roles); the archived one skipped |
| Products, first run | 14 created in 60 s. The linked four: store mappings and department ACL as in Odoo; POLO 9 combinations, Cup / Flat Fold 3. The ten unlinked ones visible in every store (D10). One warning: Odoo's *Expenses* image is an SVG, which nop does not store; the product synced without it. Unpublished in Odoo → unpublished in nop |
| Branches, first run | 5 created: two head offices (top level), three delivery addresses under them, each with its address and state |
| Second run, nothing changed | Stores 2 skipped; Departments 3 updated (they upsert), 1 skipped; Products 14 skipped in 5 s; Branches 5 updated. No duplicate row anywhere (72 combinations, 5 branches, 3 roles) and no warning |
| Change run | Beanie's price changed in Odoo → updated in nop, the other 13 skipped. New delivery address under store 2's company → branch created, and with *Use branches as packname lists* on for that store, **its default packname list was created** (`ProvisionNewBranchAsync`). Archived delivery address → branch deactivated |

Odoo test data left on the test instance for the next run: departments 1–4, partners 57–61 (59 archived), the four product
links, Beanie at $3 (was $2), and the two `x_studio_external_id` fields. nop: `multistoresettings.usebranchesaspacknamelist`
= True for store 3.

**Fixed the same day.** The plugin requested `x_studio_external_id` and never used it (O1 was never built), so the
Store and Department syncs failed on any Odoo without the field. It was removed from both requests and from
`OdooStoreResponse` / `OdooDepartmentResponse`; the solution builds, the 517 CW tests pass, and a Store and Department
re-run against the test instance passed (2 skipped; 3 updated, 1 skipped; no new rows, no warnings). The two fields added to
the test instance are no longer needed by the plugin and can stay or go. After the Odoo 19 / 20 comparison (reference
doc §4) the unused `x_studio_contract_version` request went the same way; a Store and Product re-run passed (2 and 14
skipped as unchanged, no warnings). `qty_available`, first listed as unused, sets each combination's stock and stays.

**Re-check 2026-10-29.** Nothing new in Odoo since the test data: no store, department or product created or changed,
and one untracked edit to an existing company (the store 2 company), whose name, address, delivery addresses and store
link are unchanged. (The test records show the developer's own Odoo user as creator: they were written through that
browser session.) All four syncs re-run as a regression check: stores 2 skipped, departments 3 updated and 1 skipped,
products 14 skipped, branches 6 updated; no new rows, no warnings.

**Moved to this repository 2026-11-02**, and installed on this checkout's site by sharing the database the plugin was
already installed in (no second `InstallAsync`, which would have saved empty `OdooSettings` over the saved connection).
First run from here, on new data entered in Odoo: 2 stores created and mapped (no URL), 2 head-office branches created
for their companies (no delivery addresses yet), 2 products updated (one price change), 12 skipped; departments 3
updated, 1 archived skipped; no warnings. Odoo then counted 61 products against 14 sellable, so the products added since
2026-10-29 were not fetched: the product sync reads active, sellable templates only. Checked in Odoo: the 8 new
templates are embroidery logo products (category *Decoration*), not *Can be sold*, unpublished and linked to no store,
so skipping them is correct; the two updated products were a price change and an edit by an Odoo user, and the two new
stores were linked to existing companies, with no departments or delivery addresses yet.

**Changes after the port (2026-10-27).** The branch sync calls CW.Core's `IPackNameProvisioningService.ProvisionNewBranchAsync`
for each branch it creates, so a synced branch gets its default packname list as an admin-created one does (4.9 skipped
it; with 5.00 filled only from Odoo the automation would never have run). `NopCommerce.sln`'s build dependency was put
back on CW.Core after a local change pointed it at Nop.Core.
