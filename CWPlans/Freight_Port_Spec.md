# CW.Freight — port from 4.90 to 5.00

Started 2026-10-26. Source: the 4.90 source's `Plugins/Nop.Plugin.CW.Freight` (`CLAUDE.local.md` §3.1). Target:
`Plugins/Nop.Plugin.CW.Freight` in this repository. `CWCore_Port_Strategy.md` places it before CW.Core phase 5: the
packname checkout offers only CW.Freight's methods, and `IPackNameShippingMethodService` (CW.Core) reads their names.

## 1. Decisions (2026-10-26)

| # | Decision |
|---|---|
| F1 | **Port plus the fixes that change no checkout result.** The `CLAUDE.md` §10.5 checklist and the CW conventions, plus the admin-side fixes in §3. Which methods a customer sees, and at what rate, stays as in 4.9. The stored-but-unread fields are open items (§4). |
| F2 | **No coupling to CW.Core.** CW.Core finds this plugin by SystemName (`CW.Freight`) through nop's `IShippingPluginManager`; this plugin references neither CW.Core nor `CW.Common`. No `DependsOnSystemNames`. |
| F3 | **One installation migration.** 5.00 takes no data from 4.1 or 4.9 (`CWCore_Port_Strategy.md` D2), so no database has the `TIG_FR_*` tables or lacks `ApplyPerBranch`. The 4.9 rename and add-column migrations are dropped; `SchemaMigration` creates the four `CW_FR_*` tables in their final shape. |
| F4 | **The CW.Core contract is kept and tested.** A request with one empty item, no address, no customer and a store id answers every method of that store, in display order. |
| F5 | **Unit tests** in `Tests/CW.Tests/CW.Freight` for the store / country filter, moved into an internal helper (`FreightMethodFilter`). |

## 2. §10.5 checklist

| # | Item | In this plugin |
|---|---|---|
| 1 | `net10.0`, `SupportedVersions 5.00` | `.csproj`, `plugin.json` (`Version 2026_10_26_DATA_1.2`) |
| 2 | `IWebHelper` → `Nop.Services.Helpers` | `CWFreightPlugin` |
| 3 | AutoMapper → Mapster | Not used (mapped by hand) |
| 4 | `Create.TableFor<T>()` removed | `this.CreateTableIfNotExists<T>()` (F3) |
| 5 | Views compile at runtime | Every view's types checked; each admin page loaded |
| 6 | JSON keeps C# names | The grid reads model properties (PascalCase); no plugin script |
| 7 | Package versions | No NuGet packages |
| 8 | Version bump | First 5.00 version; `UpdateAsync` installs the strings when the version holds `DATA` |
| 9 | Copied nop bodies | None |
| 10 | Service locators | None |
| 11 | Plugin DLL references | None (F2) |

## 3. Fixes made in the port

| # | 4.9 | 5.00 |
|---|---|---|
| P1 | The store list offered "All" (value 0); choosing it saved a `StoreId = 0` row, which hid the method from every store | "All" is not offered: no store selected = every store (hint says so). The filter also treats a `0` row as every store |
| P2 | The restricted-country list offered "All" (value 0), which restricted nothing | Not offered: no country selected = no restriction |
| P3 | The validator required at least one restricted country, role and store | Not required (empty has a meaning for each) |
| P4 | A refused Create post redisplayed an empty form; a refused Edit redisplayed the saved values | Both redisplay what was posted |
| P5 | `Admin.Common.FormErrors` notification — no such resource in 5.00 | Removed; the validation summary shows the errors |
| P6 | Bootstrap 5 `float-start` / `float-end` on a Bootstrap 4.6 admin | `float-left` / `float-right` |
| P7 | An inline script reloaded the grid on Search, which the grid's `SearchButtonId` already does (two requests per click) | Script removed |
| P8 | Empty `RouteProvider`; `private` fields | `RouteProvider` deleted; fields `protected readonly` (§7) |
| P9 | `ShippingOption.DisplayOrder` not set | Set from the method's display order |
| P10 | Locale strings inline in the plugin class, installed on install only | `Infrastructure/FreightLocaleResources.cs`, also installed by a `DATA` update |
| P11 | Edit's Delete button opened nothing: `<nop-delete-confirmation asp-action>` was given a URL, which the tag helper puts into the modal id (`#…-/Admin/CWFreight/DeleteMethod-…`), not a valid selector. Found in T6 | `asp-action="DeleteMethod"` — an action name, as nop's own views pass it |
| P12 | `ViewBag.Title`, which the 5.00 admin layout does not read (the tab showed "nopCommerce administration") | `ViewBag.PageTitle` on Create and Edit |

## 4. Open items (behaviour not changed — F1)

- **`LimitMethodsToCreated`** is saved on Configure and read by nothing.
- **Customer-role mappings** are saved and never applied to the options offered.
- **Lowest / highest order total** are saved and never applied (4.9's comment claiming the filter applies them was
  wrong). Applying them needs care: CW.Core's empty-item request (F4) has a zero subtotal.
- **`ApplyPerBranch`** is saved and read by nothing.
- The validator's messages are fixed English, not locale resources.

## 5. Test plan

| # | Test | Expect | Result (2026-10-26) |
|---|---|---|---|
| T1 | `dotnet build NopCommerce.sln` | No errors, no new warnings in the plugin | **Partly run** — the plugin project (and Nop.Web under it) built, 0 warnings; the whole solution was not built |
| T2 | `dotnet test Tests/CW.Tests` | All pass, Freight cases included | **Pass** — 480 / 480, 8 Freight |
| T3 | Install CW.Freight on the local site | Four `CW_FR_*` tables; Configure opens | **Pass** — `2026_10_26_DATA_1.2` installed; four tables, 24 strings, the setting; Configure and its grid load |
| T4 | Create a method with no store, no country, no role | Saved; grid shows it under every store filter | **Pass** — no mapping rows; listed under All and under store 1 |
| T5 | Create with a missing name | Form comes back with the posted values and the error | **Pass** — the client blocks it; posted to the server, the form returns the error with description, rate, display order and store kept |
| T6 | Edit, change stores; delete | Grid follows; delete removes the method and its mappings | **Pass after P11** — edit saves one row each for store, role and country and shows them again; Delete did nothing until P11, then removed the method and its three rows |
| T7 | Storefront checkout (plain cart) | The plugin's methods offered at their rates | **Pass** — with CW.Freight active and a test product in the cart, the cart's shipping estimate offers the method at $7.50 for a US address and leaves it out for Australia (restricted). The role mapping (Registered) did not filter, as §4 says. Test product, cart line and activation were removed afterwards |

Left on the local site: CW.Freight installed (inactive as a shipping provider) and the method `T4 Courier` (store 1, role
Registered, restricted in Australia).
