# CW.Core — moving the beta port to the final nopCommerce 5.00

Written 2026-10-30. This repository is the final nop 5.00 (`release-5.00.0`) with no CW.Core in it yet. CW.Core comes
across from the beta port (the 5.00 beta port source, `CLAUDE.md` §3.1, where phases 1–5 were applied), takes what the
final release changed, and then finishes the port here (phases 6 and 7). The beta port's own map,
`CWPlans/CWCore_Port_Strategy.md` (copied here from the beta port source), stays the record of why things are the way they are: its §5
(decisions D1–D5, the phase amendments, the deferred fixes) and §7 (the move) are the inputs of this document.

**State (2026-10-30):** M0 and M1 committed (M1 built, CW.Core tests green). M2 applied and checked against the
spec, unit tests green (`CWPlans/CWCore_Final500_M2_Spec.md`; its runtime checks T1–T7 run in M3). M3 run and approved
2026-10-30: 31 of 31 checks pass (`CWPlans/CWCore_Final500_M3_Plan.md` §5); the uninstall check is deferred. The fixes
for three of its findings (`CWPlans/CWCore_Final500_M3Fixes_Spec.md`) applied 2026-10-30: build and tests green, R1–R3
passed. M4 run 2026-10-31: 12 of 12 pass (`CWPlans/CWCore_Final500_M4_Plan.md` §4), approved. Phase 6 run 2026-10-31:
14 of 14 pass, one after a fix (`CWPlans/CWCore_Final500_Phase6_Plan.md` §5; the fix
`CWPlans/CWCore_Final500_Phase6Fixes_Spec.md`, applied), approved. Phase 7 planned 2026-10-31 (`CWPlans/CWCore_Final500_Phase7_Plan.md`); run 2026-10-31, groups A–Q and P: 173 of 182 pass, 0 fail, 9 skipped. No defect comes from the port. Four older defects were fixed (`CWPlans/CWCore_Final500_Phase7Fixes_Spec.md`, applied). Group U was skipped by decision, and the uninstall code was reviewed instead. Approved 2026-10-31.

---

## 1. Starting point (checked 2026-10-30)

| | This repository | Beta port source |
|---|---|---|
| nop | **final 5.00**: 14 files in `Libraries/` and 12 in `Presentation/` differ from the beta (besides the installation language files), among them the new `Nop.Web.Framework/Themes/ThemeableRazorViewEngine.cs`. `NopVersion` is `5.00` / `0` in both | the unmodified beta |
| CW.Core | absent before M1 | `Plugins/Nop.Plugin.CW.Core`, phases 1–5 applied (`plugin.json` `2026_10_30_DATA_0.1`, `SupportedVersions` `5.00`), git clean |
| `CWLibraries/CW.Common` | absent before M1 — CW.Core references it (`..\..\CWLibraries\CW.Common\CW.Common.csproj`) | present |
| `Tests/CW.Tests` | Freight, SSO, NAB folders; `Core/` and the CW.Core reference from M1 | `Core/` (625 CW.Core tests after 5h), Odoo |
| Other CW plugins | `CW.Freight` (another developer's), `Payments.NABGateway`, `ExternalAuth.SSO` | the same, plus `Integration.Odoo` |
| `tasks/` | brought across from the beta port on 2026-10-30 (`lessons.md` #1–#21, `lessons-4.9.md`) | |

**Nothing of the 4.9 CW.Core is left unported as code.** The 4.9 plugin has 933 files, the beta port 899 (seven of them
new in 5.00). The 41 not brought across are the 33 files of `Data/Migrations/`, replaced by one `SchemaMigration` (D1),
and eight files the phase specs recorded as dead in 4.9 (`_CreditNoteHistoryTable`, `html-subsidy-credits-page.css`,
`Extensions/common.css`, `TypeConverterRegistrationStartUpTask`, `BalanceDueSummaryModel`, `CustomerCreditNotesModel`,
`OrderTotalsSummaryModel`, `Views/Exchanges/_ProductBox`). What is left from 4.9 is **verification**: phase 6
(outputs compared with 4.9), phase 7 (regression), the 5h runtime tests that waited for a fuller catalogue, and the
three deferred fixes (beta strategy §5.3, §7.4, §7.5).

---

## 2. The new `ThemeableRazorViewEngine` and CW.Core's views

### 2.1 What the final release added

`Nop.Web.Framework/Themes/ThemeableRazorViewEngine.cs` (new) wraps ASP.NET's `RazorViewEngine` and replaces the
`IRazorViewEngine` registration (`ServiceCollectionExtensions.cs`, lines 197–205: the comment, `TryAddSingleton<RazorViewEngine>` and the `Replace`). `MvcViewOptions.ViewEngines` takes
`IRazorViewEngine` from DI, so every lookup goes through it: controllers, partials, view components, layouts, and
nop's `RenderPartialViewToStringAsync` / `RenderViewComponentToStringAsync`. `Nop.Web.csproj` gained a target that
leaves `Themes\*\Plugins\**\*.cshtml` out of build-time Razor compilation (they compile at runtime).

| Lookup | What the engine does |
|---|---|
| **`FindView`** — a view asked for **by name** (`View()`, `View("Name")`, view components' `Default`, `<partial name="_X">`) | Passes straight to `RazorViewEngine.FindView`: **unchanged**. The view location expanders run as before (nop's theme expander at Order 100, CW's at Order 3003, CW's locations first) |
| **`GetView` / `GetPage`** — a view asked for **by path** | On the storefront only, when the request has a theme (`ThemesMiddleware` puts it in `HttpContext.Items`), and when the path resolves to `/Plugins/<folder>/….cshtml` with no `Admin`, `.` or `..` segment: tries `/Themes/{theme}/Plugins/<folder>/….cshtml` first, then the plugin's own file. Every other case (admin area, no `HttpContext`, no theme, non-plugin path such as `~/Views/Shared/_ColumnsOne.cshtml`) passes through unchanged |
| The `RazorView` returned | Rebuilt around the new engine, so the view's layout and partials are looked up through it too |

### 2.2 Effect on CW.Core

- **CW's forks of nop views keep working.** All 16 replaced nop views (beta strategy §5.2, *View overrides*) and the
  admin `_ViewImports` / `_ViewStart` forks are found by name through `ViewLocationExpander` + `ViewsPathsRegistry`,
  which the new engine does not touch. Same expander order, same default locations as on the beta.
- **The admin area is untouched**: the engine returns early on `area = Admin`, and every CW admin view also has an
  `Admin` path segment.
- **CW's storefront views opened by path now look in the theme first.** CW.Core uses 26 distinct
  `~/Plugins/CW.Core/Views/….cshtml` paths (36 occurrences) in 17 files (the packname checkout's `AddAddress` and `_PaymentMethodInfo`,
  `SubsidyCatalog/ChangeTopup*`, its view components, …). With `DefaultClean` there is no
  `Themes/DefaultClean/Plugins/` folder, so each falls back to CW's file: same result as on the beta, one extra
  (cached) lookup per path.
- **New capability, and a new silent override:** a theme can now replace any of those 26 views by putting a file at
  `Themes/{theme}/Plugins/CW.Core/Views/…`. It still cannot replace a CW fork (found by name). The 4.9 themes
  (`CW.Standard`, `CW.StandardTest`) have no `Plugins/` folder, so nothing collides today. Recorded in `CLAUDE.md`
  §10.4 for when a theme is enabled.
- **Layouts:** CW's storefront views use `_ColumnsOne`, `_ColumnsTwo`, `_Print` by name and
  `~/Views/Shared/_ColumnsOne.cshtml` by a non-plugin path — both pass through unchanged.

**Conclusion: CW.Core needs no code change for the new engine.** M3's runtime pass carries its checks (§4, M3).

### 2.3 Dead code found on the way

Found by this analysis and by the independent review, checked against the beta port's files. None of it changes what
resolves at runtime.

| Where | What | Why it is dead |
|---|---|---|
| `Infrastructure/ViewLocationExpander.cs`, the admin block | adds `~/Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Home/{0}.cshtml` | CW.Core deploys to `Plugins/CW.Core` (`.csproj` `OutputPath`) and has no `Areas/Admin/Views/Home` folder |
| `Infrastructure/ViewsPathsRegistry.cs`, `partialViewsPaths` | the same ten paths as `viewsPaths` | the expander concatenates both and `Distinct()` drops the copy |
| `ViewsPathsRegistry.cs`, `Areas/Admin/Views/{2}/{1}/{0}` (in both lists) | `{2}` is the area name | gives `Areas/Admin/Views/Admin/…`; no such folder exists |
| `Infrastructure/ViewPathsRegistrar.cs` and its `AddScoped` in `NopStartup` | the runtime registration API | nothing injects it and nothing calls its methods — checked in the beta port, the other CW plugins, `CW.Common`, the tests and 4.9. The beta's part 4.0 kept it (beta strategy §5.2, correction of 2026-10-26, when the expander was wrongly called dead); **removed by decision of 2026-10-30**, together with the registry's four static `Register*` / `Unregister*` methods, which only it called |

The expander and the registry's other nine paths are **not** dead: they are how CW's forks replace nop's views
(lesson #21), and `ViewLocationExpanderTests` keeps guarding that order. M2 removes all four rows; what stays is the
expander and one list of nine `~/Plugins/CW.Core/...` locations, in the same order as today.

---

## 3. The rest of the final release against what CW.Core is tied to

Lines changed between the beta and the final (`diff`, CR ignored). Each row is read in full in M2 and gets a line in
its spec: take it, or say why not.

| nop file (beta → final) | Lines | CW.Core tie (beta strategy §7.2) |
|---|---:|---|
| `Nop.Services/Catalog/ProductService.cs` | 51 | `ProductOverrideService` overrides it and **copies `SearchProductsAsync`** (`CLAUDE.md` §10.3): re-diff the copy |
| `Nop.Web/Controllers/CustomerController.cs` | 85 | sign-in path and CW's packname landing; phone sign-in stays off (§7.3) |
| `Nop.Web/Factories/CustomerModelFactory.cs` | 26 | storefront login / registration models |
| `Nop.Web/Views/Customer/Login.cshtml` | 18 | nop's view (not forked); check CW's landing still runs |
| `Nop.Web/Areas/Admin/Controllers/ProductController.cs` | 68 | CW forks the product edit view's *Info* tab (`Product/_CreateOrUpdate.Info`): does the model it renders change? |
| `Nop.Web/Controllers/ReturnRequestController.cs` | 15 | 5g's `ReturnRequestSubmitFilter` sits on its post |
| `Nop.Services/Gdpr/GdprService.cs` | 12 | reads the cart through the list overload of `GetShoppingCartAsync` (beta §3.1 #6) |
| `Nop.Services/Reminders/*` (3 tasks) | 97 | reminders are used (D3): what they read of packname carts and pending orders |
| `Catalog/PriceCalculationService.cs`, `PriceLists/*` | 32 | price lists off; the packname role set (beta §5.1) |
| `Security/DefaultPermissionConfigManager.cs` | 6 | CW's permissions |
| `Nop.Data/EntityRepository.cs` | 3 | every CW repository |
| `Nop.Web.csproj`, `Nop.Web.Framework.csproj` | 11 | the theme-plugin-view target (§2.1) and `Humanizer` back in the framework. CW.Core and CW.Common pin no packages, so nothing to re-pin |
| `GeoLookupService`, `SearchTermService`, `ArtificialIntelligenceService`, `MultiplePicturesMigration`, admin `ReminderController`, two admin views | 112 | no CW.Core tie expected; confirmed in M2 |

---

## 4. Parts

Same way of working as the beta port (`CLAUDE.md` §0.1–§0.4, `CLAUDE.local.md`): one spec per part in `CWPlans/` with
FIND / REPLACE changes in apply order; the build with the Razor views, the registration and cycle checks, the unit
tests and an independent review before hand-over; defects and decisions come as questions; the developer commits.

| Part | Brings | Done when |
|---|---|---|
| **M0. Repository set-up** (documents only) — **committed 2026-10-30** | This document; `CLAUDE.md` merged (the other developer's root copy + the beta port's: Mapster/script-path/options rows in §4, CW.Core row in §3.2, a §3.1 row for the beta port source, the new engine in §10.4); `CLAUDE.local.md` ported; `tasks/` copied; `/NOP50Beta` added to `.gitignore` (§5) | The developer has reviewed and committed them |
| **M1. The beta port, verbatim** — committed 2026-10-30 | `CWLibraries/CW.Common`, `Plugins/Nop.Plugin.CW.Core` and `Tests/CW.Tests/Core` copied unchanged from the beta port source (no `bin/`, `obj/`); the three projects added to `NopCommerce.sln`; `Tests/CW.Tests/CW.Tests.csproj` given its CW.Core reference. Beta strategy §4.2 rule 2: the verbatim copy is its own commit, so M2 reads as "what the final required" | The solution builds against the final nop; registration and cycle checks pass; the 625 CW.Core tests and the other suites pass |
| **M2. What the final 5.00 changed** — applied 2026-10-30, `CWCore_Final500_M2_Spec.md` | §3's re-diffs (`SearchProductsAsync` copy, sign-in and landing, the product *Info* fork, the return post, the cart readers, reminders); the dead view-location entries (§2.3); `plugin.json` `Version` bumped if any `UpdateAsync` work changes; `CLAUDE.md` §10.3 table re-checked | Spec applied; build, checks and tests green; reviewed |
| **M3. New database and runtime pass** — approved 2026-10-30, `CWCore_Final500_M3_Plan.md` | A fresh nop install on the new catalog `testnop50`; CW.Core installed (`SchemaMigration` + the SQL views, functions and procedures); §7.3's settings checked on the fresh install (withdrawal, auto-cancel, phone sign-in, vendors, price lists off; reminders on); a catalogue for §7.4 (size/colour combinations, a checkbox option, a required product, a three-level category, a closed ordering period); `CWPlans/TestData/` scripts rewritten for the new ids. Runtime checks: every CW storefront page opened by path (§2.2) loads under `DefaultClean`; one forked storefront view and one forked admin view render CW's version; the packname checkout's address and payment partials refresh through `RenderPartialViewToStringAsync`; then a smoke run of each phase-4/5 part's main flow | Install / uninstall clean (the uninstall deferred, decision 8); the checks pass |
| **M3 fixes** — applied 2026-10-30, `CWCore_Final500_M3Fixes_Spec.md` | M3's findings 1, 2 and 4: the return page's message when the store's rules offer no return, nop's own lists with *Exchanges* off, and the *Add packname* form refusing an over-long account field as the import does | Spec applied; build and tests green (631 CW.Core); its runtime checks R1–R3 pass |
| **M4. Deferred runtime tests** — run 2026-10-31, `CWCore_Final500_M4_Plan.md` | 5h's T5, T8b, T9–T16, T18, T19 (beta §7.4) | Pass |
| **Phase 6. Outputs** — approved 2026-10-31, `CWCore_Final500_Phase6_Plan.md`; fix `CWCore_Final500_Phase6Fixes_Spec.md` | Invoice and packing slip (SkiaSharp 4, the 5.00 PDF font), e-mails and tokens (packname rows, exchange e-mails), reports (profit with subsidy), each compared with 4.9's for the same order; the theme check of `CLAUDE.md` §10.4 | No difference that is not a 5.00 change |
| **Phase 7. Regression** — approved 2026-10-31, `CWCore_Final500_Phase7_Plan.md`; fixes `CWCore_Final500_Phase7Fixes_Spec.md` | Every phase-4/5 part's test plan end to end (beta port source `CWPlans/Deprecated/`, §6 of each); feature switches both ways; performance against 4.9 | No open defect from the port |
| **After the port** | Beta §5.3's three deferred fixes (each with the manager where it says so); `CLAUDE.md` §13–§21 brought up to date; then the other plugins — `Integration.Odoo` first (D2: 5.00's data comes from it, and it depends on CW.Core), then ThemeCore / Theme.Standard and the themes | |

M1 and M2 could be one delivery to save a build, but they stay two commits: M1 is a copy, M2 is the review surface.

---

## 5. Decisions and open items

Decided 2026-10-30:

1. `.gitignore` takes `/NOP50Beta` (committed with M0).
2. `CLAUDE.md` §3.2's rows for ThemeCore, Theme.Standard, `CW.Standard` and Odoo stay as the other developer wrote them;
   what they mean for this repository is in the developer's `CLAUDE.local.md`.
3. The lesson numbers in `CLAUDE.md` §10.5 rows 11–13 stay as they are; the other developer is asked for their lessons file.
4. `CWCore_Port_Strategy.md` is copied into `CWPlans/`; `Deprecated/` and `TestData/` stay in the beta port source.
5. M1's folders are copied by Claude (done).
6. The database is `testnop50`.
7. `ViewPathsRegistrar` is removed in M2 (§2.3).
8. The uninstall check of M3 runs later, not now: M3's data is kept for M4.
9. M3's findings outside CW.Core are recorded, not changed now (below).

Decided 2026-10-31:

10. `testnop50` keeps 4.9's tax settings (prices include tax, display including tax, tax suffix shown, shipping
    taxable), set for phase 6: testing uses 4.9's configuration.

Open:

- **Build and tests:** Claude's build environment cannot reach nuget.org or the .NET downloads this session, so the
  build, checks and test runs are the developer's until those hosts are allowed.

**For later — found in M3, outside CW.Core** (decision 9; not to be changed while only CW.Core is in scope):

- `Nop.Plugin.ExternalAuth.SSO` ships no `logo.jpg`: nop renders `<img src="null">` on *Local plugins*, four 404s for
  `/Admin/Plugin/null` in the log each time the page opens.
- `Misc.RFQ` is installed on `testnop50` (CLAUDE.md §3.2 lists it as not to be installed): its *Request a quote* button
  shows on CW's packname checkout, and *Requests for quote* / *Quotes* in the account menu. Uninstall it on the dev
  database, or decide whether the packname checkout should hide it.
- `AIRecommendation.GoogleAI` is installed and enabled on `testnop50` but answered no search (every search came back
  keyword-filtered by nop's standard path). If a store ever turns it on, CW's restricted search joins the AI results to
  the restricted query (M2 change 2), so the restriction holds; the search results themselves were not tested with it.
- The resource key `Common.Loading` does not exist in 5.00, but nop's own `OnePageCheckout.cshtml` and CW's packname
  checkout (`Views/PackNameCheckout/Checkout.cshtml:27`, copied from it) use it: a *Resource string … not found*
  warning each time the checkout opens (M4). For CW's copy: either add the resource or use another key.
- `Widgets.GoogleAnalytics` is installed on `testnop50` without being set up and logs an error on cart changes (M4).

**For later — found in phase 6:**

- `ReturnAuthorisedPayment.CustomerNotification` and `ReturnAuthorisedExcepted.CustomerNotification` are sent by
  `ExchangeWorkflowMessageService` only when the template exists, and CW.Core installs neither (on 4.9 too): the
  customer gets no e-mail when a return is authorised. A CW.Core gap carried from 4.9, not from the port.
