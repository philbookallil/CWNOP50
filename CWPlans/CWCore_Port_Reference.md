# CW.Core — the port from nopCommerce 4.90 to 5.00 (reference)

**Status: finished, 2026-10-31.** CW.Core runs on the final nopCommerce 5.00 in this repository. Phases 6 and 7 are
approved, and the deferred fixes are applied. This is the one document to keep about the port, for the next nop upgrade
and for anyone who needs to know why CW.Core's 5.00 code is the way it is. Everything else written during the port is
consolidated here. The originals are in `CWPlans/Deprecated/` (the list is in §10).

**How to read it.**
- §1–§2 summarise the port.
- §3 and §4 are what nop changed and where CW.Core was exposed. Read them before a nop upgrade.
- §5 holds the decisions, including each phase's own decisions.
- §6–§7 are the nop code and settings CW.Core depends on.
- §8 lists the defects found and fixed during the move, §9 what is still open, and §10 the sources and test material.

Parts carried over from the two strategies keep their original section numbers in their headings, marked *Beta §* (the
beta port's strategy, `CWCore_Port_Strategy.md`) or *Final §* (`CWCore_Final500_Strategy.md`). Cross-references inside
those parts, such as "§3.1 #6" or "§5.2", point to the *Beta §* headings of this document.

---

## 1. Summary

- **nop 5.00 is an evolution, not a rewrite.** These are all the same as in 4.90: the plugin model, the data layer
  (`IRepository<T>`, LinqToDB, FluentMigrator), the service-override pattern and the admin front end (AdminLTE 3.2,
  Bootstrap 4.6, jQuery 3.7, DataTables). About a third of nop's files changed.
- **The port ran in two stages** (decision D4):
  - **On the beta of 5.00:** phases 1–5. The C# core came across as one block, then the overrides were switched on one
    at a time, then the admin UI (parts 4.0–4g) and the storefront UI (parts 5a–5h) came across feature by feature.
  - **On the final 5.00:** parts M0–M4, then phases 6 (outputs) and 7 (regression), then the deferred fixes.
- **Code coverage.** Nothing of the 4.9 CW.Core is left unported. The 4.9 plugin has 933 files; 41 were not brought
  across:
  - the 33 migration files, replaced by one `Data/Migrations/SchemaMigration.cs` (D1);
  - eight files that were dead in 4.9.
- **Behaviour.** Phase 7 ran every part's test plan again: 173 of 182 results pass, 9 are skipped (data or a single
  currency), and none fail. No defect came from the port. The older defects found on the way are fixed (§8).
- **What the compiler could not see** was the real risk, and it is what §3.2, §6 and `CLAUDE.md` §10.3–§10.5 guard:
  - views compile at runtime;
  - copied nop method bodies keep their old behaviour silently;
  - the cart override must sit on the list overload of `GetShoppingCartAsync`;
  - templates that the install rewrites.

## 2. Timeline and results

| Part | Date | What it did | Result |
|---|---|---|---|
| Beta phases 1–3 | 2026-10-23 – 10-25 | The C# core, services, business logic and overrides on the 5.00 beta | Beta §5.2 |
| Beta phase 4 (4.0–4g) | 2026-10-26 – 10-28 | The admin UI, part by part | Beta §5.2; each part's test plan in its spec |
| Beta phase 5 (5a–5h) | 2026-10-28 – 10-30 | The storefront UI, part by part | Beta §5.2 |
| M0 | 2026-10-30 | Repository set-up: strategy, `CLAUDE.md` merged, `CLAUDE.local.md`, `tasks/`, `.gitignore` takes `/NOP50Beta` | Committed |
| M1 | 2026-10-30 | The beta port copied verbatim: `CWLibraries/CW.Common`, `Plugins/Nop.Plugin.CW.Core`, `Tests/CW.Tests/Core` | Built; 625 CW.Core tests green |
| M2 | 2026-10-30 | What the final release changed (§4): the restricted search's category order and AI branch; dead view-location code removed (`ViewPathsRegistrar`); account text fields capped at 1,000 characters; GDPR delete takes the hidden packname cart lines | Applied; tests green |
| M3 | 2026-10-30 | New database `testnop50`, CW.Core installed (43 tables, 11 views, 2 functions, 2 procedures), settings checked, runtime pass | 31 of 31 pass |
| M3 fixes | 2026-10-30 | The return page says so when the store's rules offer no return; nop's own reason and action lists with *Exchanges* off; the *Add packname* form checks the account's field lengths | Applied; 631 CW.Core tests |
| M4 | 2026-10-31 | 5h's bulk-ordering tests that needed a fuller catalogue | 12 of 12 pass |
| Phase 6 | 2026-10-31 | Invoice, packing slip, return note, e-mails, reports and export compared with 4.9 for the same orders | 14 of 14 pass, one after a fix (§8) |
| Phase 7 | 2026-10-31 | Every part's test plan again, feature switches both ways, performance against 4.9 | 173 of 182 pass, 0 fail; four older defects fixed (§8) |
| Deferred fixes | 2026-10-31 | Admin packname colour saves, provisioning name check; the cost-centre fix dropped | D1–D3 pass; 634 CW.Core tests |

Performance (phase 7, warm, median of five, ms):
- **Storefront:** 5.00 is as fast as or faster than 4.9 on every page compared, for example the packname checkout
  62 / 158, the subsidy catalogue 121 / 283 and order details 72 / 137.
- **Admin:** no page is more than 35 % slower, and that one compares unlike data.
- **Not compared:** `/bulkordering` takes 417 ms on 5.00 and had no 4.9 counterpart.

---

## 3. What nopCommerce changed from 4.90 to 5.00, and where CW.Core was exposed

From the beta strategy, written before the port (2026-10-22) and counted by script over the 4.90 nop source, the 5.00 beta and the 4.90 CW.Core. The final release changed little more (§4). Each item says how the port handled it in Beta §5.2.


#### Beta §2.1 Platform

| | 4.90 | 5.00 | Matters for CW.Core |
|---|---|---|---|
| .NET | 9.0 | **10.0** | `IActionContextAccessor` obsolete (ASPDEPR006) and not registered by nop (§3.2) |
| Object mapping | AutoMapper 14 | **Mapster 10** behind nop's `BaseMapperProfile` | 1 profile, 1 direct `Mapper.Map` call |
| Validation | FluentValidation 11 + `FluentValidation.AspNetCore` | **FluentValidation 12**; nop's own `[AutoValidation]` filter on `BaseController` and its own client-side adapters | Validators keep running: every CW.Core controller inherits `BaseAdminController` / `BasePublicController`. Validation now runs in an action filter, still before the action body (`CLAUDE.md` §9.13 holds) |
| LinqToDB | 5.4.1 | **6.5.0** (major) | Small direct surface: 7 `using LinqToDB`, 8 `DataParameter`, 5 `[NotColumn]`, 18 raw `QueryAsync` / `ExecuteNonQueryAsync`. Test every raw SQL path and every view entity |
| FluentMigrator | 7.1 | **8.0** (major); nop drops `Create.TableFor<T>()` for `this.CreateTableIfNotExists<T>()` | 52 `Create.TableFor` calls in 33 migration files |
| `System.Linq.Async` | 6.0.3 | **removed**; nop ships `SelectAwait`, `WhereAwait`, `ToListAsync` … in its own `System.Linq` class | CW.Core's 29 `SelectAwait` / 3 `ToAsyncEnumerable` keep compiling; check each overload resolves |
| SQL client | Microsoft.Data.SqlClient 6.1 | **7.1** | Connection-string defaults (encryption) — check the dev and server connection strings |
| Imaging / PDF | PdfRpt.Core, SkiaSharp 3.119, Svg.Skia 3 | PdfRpt.Core, **SkiaSharp 4.152, Svg.Skia 5**; `PdfDocument.Font` replaced by `FontName` / `FontSize` | CW's invoice and packing-slip PDFs (§3.1 #7, §3.2 #14) |
| Others | Markdig 0.42, Humanizer, MailKit 4.13 | Markdig 1.3, Humanizer removed, MailKit 4.18 | CW.Core uses none of them directly |
| Admin front end | AdminLTE 3.2, Bootstrap 4.6, jQuery 3.7 | **same** | 109 admin views and the admin JS keep their framework |
| Tests | NUnit 4.4, FluentAssertions 7.2 | NUnit 4.6, **AwesomeAssertions 9.6** | The 4.9 suite renames its assertion namespace (§4, phase 2) |

#### Beta §2.2 nop's code: removed, moved, changed

- **Forums, News and Polls left the core** and became plugins (`Misc.Forums`, `Misc.News`, `Misc.Polls`): 164 types
  removed, among them the entities, services, models and view components of those three modules.
- **Synchronous APIs are gone from the services.** `IStoreContext.GetCurrentStore()`, `ILanguageService.GetAllLanguages()`
  and the sync `ILogger.Warning/Error/Information` were removed; the sync versions nop still needs live in a new
  `ISyncCodeHelper`.
- **Members moved between services.** `ResetCheckoutDataAsync` moved from `ICustomerService` to `IShoppingCartService`;
  `IMessageTokenProvider.AddReturnRequestTokensAsync` takes the `Order`; `IHtmlFormatter.FormatText` takes only the text;
  `IPaymentService.GetMaskedCreditCardNumber` is gone; `GetShoppingCartAsync` takes a non-null `ShoppingCartType` and
  gained an overload for a list of types.
- **Types moved:** `IWebHelper` → `Nop.Services.Helpers`. `AutoMapperConfiguration` → `MapperConfiguration`.
- **Manual credit-card payments left the core** (to a plugin, which 5.00 then dropped): the card fields are gone from
  `ProcessPaymentRequest` and the admin `OrderModel`. `Order` keeps its card columns.
- **Return-request settings moved** from `OrderSettings` to a new `ReturnRequestSettings` (`ReturnRequestsEnabled`,
  `ReturnRequestsAllowFiles`, the number mask, the days available), with new withdrawal and completed-orders-only options.
- **View components:** `NopViewComponent` now offers `ViewAsync(...)`, which raises `ModelPrepared`; a plain
  `View(model)` still compiles but no longer raises it. nop's own components use `ViewAsync`.
- **Admin menu:** `AdminMenu` is built from `IHttpContextAccessor` and `LinkGenerator`; `AdminMenuCreatedEvent` is
  unchanged.
- **Large edits inside the services CW.Core overrides** (lines added / removed in nop's own file):
  `CustomerService` +216/−111, `MessageTokenProvider` +233/−206, `ShoppingCartService` +181/−92,
  `ProductService` +179/−50, `OrderProcessingService` +105/−40, admin `CustomerController` +365/−236,
  admin `OrderController` +177/−128, public `ReturnRequestController` +149/−20, `ShoppingCartController` +169/−36,
  `PriceCalculationService` +96.

#### Beta §2.3 New in 5.00 that touches what CW.Core does

| Feature (nop 5.00) | Where | Overlap with CW.Core |
|---|---|---|
| **Withdrawal form** (right of withdrawal, token link, captcha) | `ReturnRequestController.Find`, `ReturnRequestModelFactory.PrepareWithdrawalFormModelAsync`, `ReturnRequestSettings.WithdrawalLinkDaysValid` | CW.Core forks nop's public `ReturnRequest.cshtml` / `CustomerReturnRequests.cshtml` (its exchanges wizard entry), and nop's 5.00 public controller (+149 lines) now renders them. A second, nop-native return entry point would bypass the return case, subsidy restoration and credit notes |
| **Auto-cancel unpaid orders** (`AutoCancelOrdersTask`, `OrderSettings.AutoCancel*`, optional cart restore) | `Nop.Services/Orders` | Cancels pending-payment orders after a delay. Packname orders carry a settlement row and subsidy consumption; exchange orders carry fees. Cancelling them through nop must return subsidy and credit the way CW's own cancel does, or be off |
| **Reminders** — abandoned carts, pending orders, incomplete registrations (`Nop.Services/Reminders`, three scheduled tasks, `Order.LastPendingOrderFollowUp*`) | `ProcessAbandonedCartsTask` etc. | Low. Reminders go to the owner of the cart or order: a packname cart's lines belong to the manager's customer (`CORE_PN_PackNameCartItem` only maps them to packnames), and staff accounts are created active (`PackNameRegistrationService`), so the registration reminder (inactive accounts, e-mail validation) never selects them. To check: orders left in `PaymentStatus.Pending` (e.g. on account) get pending-order follow-ups |
| **Price lists** per customer and customer role (`PriceList*`, `IPriceListService`, `PriceCalculationService` +96) | `Nop.Services/PriceLists` | CW.Core swaps the role set the catalogue sees for a packname's roles (negative ids for the signed-in roles, `CLAUDE.md` §15.2). Price lists keyed on roles read that same set |
| **`ShoppingCartType.Stash`** (multi-vendor: lines of other vendors are stashed) | `ShoppingCartService.SetShoppingCartVendorAsync` | 54 `ShoppingCartType` uses in CW.Core; filters written as "not wishlist" now include stashed lines |
| Desired delivery date, SMS / OTP phone verification, contact-form attributes, AI recommendations, elFinder file manager (replaces Roxy) | checkout, registration, admin | Packname checkout is CW's own; manager-created customers go through `CustomerRegistrationService` (new constructor, +39/−11) |

#### Beta §2.4 What nopCommerce has published about 5.00

nopCommerce's own release notes stop at 4.70, and the forum thread "nopCommerce 5.00 beta released" and
docs.nopcommerce.com could not be read from here (refused). What follows comes from the GitHub release tags
(`release-4.90.8` against `release-5.00.0-beta`) and their commit messages, and each claim used above was checked
against the 5.00 source in this repository.

- **Timing.** The beta was tagged on 2026-09-30. No final release date is announced; the 5.00 milestone was due on
  2026-09-15 and was reported moved to 2026-10-12. 4.90.8 is still the "latest" release on GitHub.
- **Also new in 5.00:** login by phone with SMS / OTP (an SMS provider plugin type), AI SEO meta tags and an AI
  recommendation plugin type, 3D product objects, GPSR manufacturer fields, customer search history, PunchOut (cXML),
  non-returnable digital products and "returns only on completed orders". Removed: the Payments.Manual and accessiBe
  plugins, Roxy Fileman (elFinder replaces it), the BBCode / code formatters.
- **Upgrade path.** nop's `UpgradeTo500` migrations upgrade a 4.90 database: GPSR and delivery-date columns,
  reminder columns, contact-form tables, price-list tables, the search-term rework, the dropped `PictureId` columns.
- **Open on the 5.00 milestone** when last read: `ModelPreparedEvent` should carry the concrete model type (#7756 —
  CW.Core consumes `ModelPreparedEvent<BaseNopModel>` in `EventConsumer`), image URLs coming out as localhost (#7813),
  the DeleteGuests task (#7847), a PayPal capture error (#7810 — CW's own-placement payment flow uses PayPal).
- **Corrections to the published lists, from the code:** `ForeignKey<TPrimary>()` is still in 5.00 (CW.Core's 56
  builder calls keep compiling), and `INopUrlHelper` still exists (CW.Core's storefront `_ViewImports` injects it).

Sources: [nopCommerce releases](https://github.com/nopSolutions/nopCommerce/releases),
[release-5.00.0-beta](https://github.com/nopSolutions/nopCommerce/releases/tag/release-5.00.0-beta),
[5.00 milestone](https://github.com/nopSolutions/nopCommerce/milestone/20),
[forum: release timeline for 5.0 and .NET 10](https://www.nopcommerce.com/en/boards/topic/102721/estimated-release-timeline-for-nopcommerce-50-and-net-10-support).

### Beta §3 Where CW.Core is exposed


#### Beta §3.1 The compiler will find these

| # | What | Where in CW.Core | Fix |
|---|---|---|---|
| 1 | `Create.TableFor<T>()` removed | 52 calls, 33 migration files | `this.CreateTableIfNotExists<T>()`, or a new initial schema (decision D1) |
| 2 | AutoMapper | `Infrastructure/Mapper/CWMapperConfigurationProfile`, `CustomerOverrideModelFactory` (`AutoMapperConfiguration.Mapper.Map`) | Rewrite on `BaseMapperProfile` |
| 3 | `IWebHelper` namespace | 10 uses (C# and views) | `using Nop.Services.Helpers;` |
| 4 | Forums / News / Polls types | `CustomerOverrideService` constructor (4 repositories), `MessageTokenProviderOverride` (`INewsService`), `CustomerOverrideModelFactory` (`ForumSettings`) | Follow nop's new constructors (#5) |
| 5 | **New constructors on 8 nop base classes** | `CustomerOverrideService`, `CustomerRegistrationOverrideService`, `CustomerOverrideModelFactory`, `MessageTokenProviderOverride`, `OrderOverrideModelFactory`, `OrderProcessingOverrideService`, `ProductOverrideService`, `ShoppingCartOverrideService`. Three of them pass `IActionContextAccessor` to the 4.9 base (§3.2 #10) | Re-derive each constructor from 5.00 |
| 6 | **Overridden `GetShoppingCartAsync` changed shape** — 5.00 does everything in a new `List<int> shoppingCartTypes` overload; the single-type overload only forwards to it, and nop's own callers (`GdprService`, `ProductController`, `ShoppingCartService`) call the list one, some with no type at all | `ShoppingCartOverrideService` (overrides the 4.9 nullable-type method) | Override the **list** overload, or nop's callers go around the packname filtering. The `Wishlist` early exit becomes a check on the list; a null list now also returns `Stash` lines. Not caught as a behaviour change by the build — only the signature is |
| 7 | Removed / moved members called | `ResetCheckoutDataAsync` (2), `GetCurrentStore()` (1), `GetAllLanguages()` (2), sync logger (8), `AddReturnRequestTokensAsync` (4), `GetMaskedCreditCardNumber` (1), `PdfDocument.Font = ResolvePdfFont(...)` (2, `PdfOverrideService`) | Per §2.2; the PDF font follows 5.00's `GetFontInfo` |
| 8 | Credit-card fields removed (`ProcessPaymentRequest`, `ProcessPaymentResult.AllowStoringCreditCardNumber`, admin `OrderModel`) | `OrderProcessingOverrideService` (copied placement code); the admin views in §3.2 #13 | Follow 5.00's `OrderProcessingService` and `_OrderDetails.Info` |
| 9 | `OrderSettings.ReturnRequestsAllowFiles` moved | `ReturnSubmissionLogic`, `ReturnRequestOverrideModelFactory` | `ReturnRequestSettings` |

#### Beta §3.2 Only runtime will show these

| # | What | Why it is silent | Where |
|---|---|---|---|
| 10 | **`IActionContextAccessor` injected, not registered in 5.00** | DI throws when the class is resolved | `PackNameController` uses it itself (line 597): runtime failure on the packname pages. `ShoppingCartOverrideService`, `MessageTokenProviderOverride` and `CustomerRegistrationOverrideService` only pass it to the 4.9 base constructor, so the new constructors (§3.1 #5) remove it and the build catches them. Same finding as CW.Common (`tasks/lessons.md` #19) |
| 11 | Views reference removed namespaces | Razor compiles on first request | `Views/_ViewImports.cshtml` (`Nop.Web.Models.Boards/News/Polls`), `Areas/Admin/Views/_ViewImports.cshtml` (`…Admin.Models.Forums/News/Polls`) — every view under them fails |
| 12 | `IHtmlFormatter.FormatText` now one parameter | view | `Views/ReturnRequest/CustomerReturnRequests.cshtml` |
| 13 | **Forked nop views changed upstream** — same path (8 of 19) and renamed copies | the fork keeps 4.9 markup | Same path: `Areas/Admin/Views/Customer/_CreateOrUpdate.Info.cshtml` (46 lines differ in nop), `Views/ReturnRequest/ReturnRequest.cshtml` (17), `Views/Order/Details.cshtml` (15), `Views/ReturnRequest/CustomerReturnRequests.cshtml` (12), `Areas/Admin/Views/Product/_CreateOrUpdate.Info.cshtml` (8), `Areas/Admin/Views/Customer/List.cshtml` (7), both `_ViewImports`. Renamed copies: `_CWOrderDetails.Info` and `_CWOrderDetails.Payments` ← nop `_OrderDetails.Info` (**146** lines differ; they also read the removed card fields), `_CWOrderDetails.BillingAddress` / `.BillingShipping` ← `_OrderDetails.BillingShipping` (49), `AclDepartments` ← `AclCustomerRoles` (6). Close copies whose source did not change: `_ProductBox`, `_CheckoutAttributes`, `PackNameOrderTotals`, `_PaymentMethods`, `_ProductsGrid`, `_CreateOrUpdateExchange` |
| 14 | **Copied nop code** (`CLAUDE.md` §10.3) | old behaviour, no error | **Method bodies** (whitespace and brace-only lines ignored): changed upstream — `ProductService.SearchProductsAsync` (55 lines: the search-provider / AI recommendation branch), `PdfService.PrintOrderToPdfAsync` and `PrintPackagingSlipToPdfAsync` (the font, 4 lines each); unchanged — `ShipmentService.GetAllShipmentsAsync`, `MessageTokenProvider.WriteTotalsAsync`, `PdfService.GetTotalsAsync`, `InvoiceDocument.CreateSummary`, `OrderReportService.ProfitReportAsync`. **A whole controller, not yet in the §10.3 table:** `ExtendedCustomerController` takes nop's `admin/customer/list`, `create` and `edit/{id}` routes (`RouteProvider`) and carries nop's Create / Edit logic. 5.00's `CustomerController` adds the verified-phone check (`OtpSettings.LoginByPhoneEnabled`), price-list assignment (`SelectedPriceListIds`) and `PhoneSmsVerified` — all silently missing on CW's customer screens. Add it to the §10.3 table |
| 15 | Overrides that call `base` on a heavily changed base | base behaviour moved under them | the five services in §2.2 with +100 lines or more |
| 16 | New 5.00 features (§2.3) | they run whatever CW.Core does | withdrawal, auto-cancel, reminders, price lists, `Stash` |
| 17 | `ShoppingCartType` filters | `Stash` is a third value | 54 uses |
| 18 | View components return `View(model)` | still compiles; `ModelPrepared` is no longer raised for their models | 21 of CW.Core's 23 view components — change to `ViewAsync` |
| 19 | LinqToDB 6 | a query that compiled can translate or materialise differently | 18 raw `QueryAsync` / `ExecuteNonQueryAsync`, the SQL views read through `IRepository<View>` |
| 20 | **nop's admin mapping rules reach CW's maps** (found 2026-10-29, 5c follow-up) | Mapster has one global configuration; nop's admin profile applies `ForAllMaps` rules to every map that exists when it runs, CW's included when nop finds CW's profile first (4.9's AutoMapper kept each profile's rules to itself). The map compiles and runs, and drops members by the destination's interfaces: selected customer roles and available roles (`IAclSupportedModel`), stores (`IStoreMappingSupportedModel`), discounts (`IDiscountSupportedModel`), `Locales`, `CustomProperties`, `ActiveStoreScopeConfiguration`, `IsAdvancedSetting`, `PreTranslationAvailable`, plugin model fields | Hit the admin customer list (4b: opened on every customer). Every CW map checked in both load orders; `CWMapperGlobalRulesTests` now runs each CW map with nop's rules applied and fails on any member dropped that the profile does not leave out on purpose. Rule: never copy a nop model into a CW model that extends it with the mapper — prepare the CW model in place (it *is* nop's model); a new CW map adds its deliberate ignores to the test's list |

---

## 4. The final 5.00 release against the beta

#### Beta §7.0 What the final release changed (checked 2026-10-30)

- **Tags:** the beta is `release-5.00.0-beta` (commit `3872d24`, 2026-09-29); the final is `release-5.00.0`
  (`fbdbe18`, 2026-10-27).
- **122 files differ**, 70 of them installation language files. The repository's `Libraries/` and `Presentation/` are the
  beta, unmodified (checked against the tag), so they can be replaced by the final's.
- **nop issue #7756 did not land:** `PublishModelEventsFilter` and `NopViewComponent` are unchanged, so
  `ModelPreparedEvent<BaseNopModel>` still reaches CW's consumers.
- **Changed files that touch CW.Core**, to read first:
  - `Presentation/Nop.Web.Framework/Themes/ThemeableRazorViewEngine.cs` (**new**, 208 lines) and the change in
    `Infrastructure/Extensions/ServiceCollectionExtensions.cs` that registers it: CW's forked views replace nop's
    through its view location expander (§5.2, *View overrides*). The most important one;
  - `Libraries/Nop.Services/Catalog/ProductService.cs` (+/−49): CW overrides it and copies `SearchProductsAsync`
    (`CLAUDE.md` §10.3);
  - `Catalog/PriceCalculationService.cs`, `PriceLists/PriceListService.cs`, `IPriceListService.cs` (price lists: off);
  - `Reminders/ProcessAbandonedCartsTask.cs`, `ProcessPendingOrdersTask.cs`, `ProcessIncompleteRegistrationsTask.cs`
    (reminders are used, D3: check what they read of packname carts and orders);
  - `Gdpr/GdprService.cs` (reads the cart), `Nop.Data/EntityRepository.cs`, `Security/DefaultPermissionConfigManager.cs`;
  - storefront `Controllers/ReturnRequestController.cs` (5g's `ReturnRequestSubmitFilter` sits on its post),
    `Controllers/CustomerController.cs`, `Factories/CustomerModelFactory.cs`, `Views/Customer/Login.cshtml` (sign-in
    and CW's landing);
  - admin `Areas/Admin/Controllers/ProductController.cs` (CW forks the product edit view's *Info* tab);
  - `Nop.Web.csproj`, `Nop.Web.Framework.csproj` (package versions CW's `.csproj` files pin to the host);
  - `defaultResources.nopres.xml`, `Migrations/UpgradeTo500/*` (nop's own texts and upgrade; check no CW resource key
    clashes).
  - The rest are nop's own plugins (AI, Brevo, Forums, PunchOut, RFQ, Twilio, Avalara, UPS, …), admin styles and nop's
    tests.

### Final §2 The new `ThemeableRazorViewEngine` and CW.Core's views

#### Final §2.1 What the final release added

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

#### Final §2.2 Effect on CW.Core

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

#### Final §2.3 Dead code found on the way

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

### Final §3 The rest of the final release against what CW.Core is tied to

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

## 5. Decisions

### Beta §5 Decisions

Settled on 2026-10-23 with the manager's answers (recorded word for word in the doc "CW on nopCommerce 5.0 — decisions
for the manager").

| # | Question | Decision |
|---|---|---|
| D1 | **Database history** | **One clean initial schema** plus the SQL objects. The 33 4.9 migrations (incl. `RenameCWTablesVersion` and `RecreateAllTablesVersion`) stay in the 4.90 source as history; no 4.9 database is ever upgraded |
| D2 | **Where 5.00's data comes from** | **Nothing from 4.1 or 4.9** ("All the data will go to NOP 5.0.... We will never use 4.9"). 5.00 is filled by the Odoo sync and set up again, so `Integration.Odoo` must be ported to 5.00 before go-live |
| D3 | **New 5.00 features** | Withdrawal form: **off** (Australia only). Auto-cancel of unpaid orders: **off** (not answered; recommendation stands). Reminders: **used, and extended with subsidy reminders**. Price lists: **off during the port, then studied** with packnames and subsidies. `Stash`: **no vendors** — inert, but CW's cart filters still handle the value (§3.2 #17). Desired delivery date, AI texts, contact-form fields: not answered |
| D4 | **Port on the beta or wait for the final 5.00** | Port on the beta now, as far as possible, then move the beta port to the final 5.00 (a smaller step than from 4.9). Phase 7 includes that move |
| D5 | **Who copies the files** | Phase 1: the developer copies the folders; Claude specifies the 5.00 edits as exact changes (`CLAUDE.md` §0.1). Reviewed after phase 1 |

#### Beta §5.1 Work after the port (from the manager's answers)

Not part of the port itself; each gets its own design document. Listed so the port does not close a door on them.

| Item | What it involves | What the port must keep open |
|---|---|---|
| **PunchOut** ("a big thing for us") | nop's `Misc.PunchOut` plugin builds the cXML order from nop's cart (`GetShoppingCartAsync` + `GetUnitPriceAsync`) and intercepts nop's own checkout actions (`PunchOutSessionGuardAttribute`: `StartCheckout`, `Checkout`). CW orders go through the packname checkout, so a PunchOut session has to be routed into, or around, that checkout, and the cart it sends must carry the packname split | The cart override must answer the list overload correctly (§3.1 #6), or PunchOut reads the wrong lines |
| **Login by phone** | SMS / OTP sign-in (`Sms.Twilio` plugin, `OtpSettings`) | `ExtendedCustomerController` takes 5.00's phone-verification logic (§3.2 #14) — **done in 4b** (2026-10-26). Manager-created staff accounts (`PackNameRegistrationService`) go through `CustomerRegistrationService`, whose constructor changed (§3.1 #5) |
| **Subsidy reminders** | New reminder kinds on nop's `Reminders` (scheduled tasks + message templates), e.g. subsidy about to expire or unspent | Nothing in the port; reminders stay as nop ships them |
| **Price lists with packnames** | Study how per-customer / per-role price lists combine with the packname role set (`CLAUDE.md` §15.2) and subsidy pricing. Found in phase 3: nop 5.00's `PriceCalculationService` looks price lists up by `ICustomerService.GetCustomerRoleIdsAsync`, which in the packname context returns the packname's role set (plus negative ids), not the signed-in customer's — against CW's rule that what a customer pays follows the signed-in roles | Price lists off; the role-set override is ported unchanged |
| **Returns rules** | CW's own return rules come across in the port; nop's new rules (returns only on completed orders, digital products not returnable, in `ReturnRequestSettings`) are added to CW's returns wizard | Phase 5's returns feature reads `ReturnRequestSettings` (§3.1 #9) |
| **3D models / AI** | A later study | Before an AI-recommendation plugin is enabled, re-check `ProductOverrideService.SearchProductsAsync`: nop 5.00's AI branch orders the query by the plugin's results without paging or filtering it (nop's own code, kept in CW's copy) |
| **Desired delivery date** | nop 5.00's date picker at checkout | CW's packname order builder (a copy of `SaveOrderDetailsAsync`) does not take the date; add nop's block there with the packname checkout's own picker |

#### Beta §5.2 Amendments from the phases

- **Phase 2** (`CWCore_Phase2_Spec.md`, applied 2026-10-25): `EventConsumer` came across whole, so the storefront
  `BulkOrderModelFactory` and the nine `Content` files it registers on every page were brought forward from phases
  4–5. Its admin menu node, CW's permissions and the My Store Credit link appear from phase 2, their pages later. The
  "background queue runs a job" check moves to phase 4 (only the admin MultiStore controller queues jobs). Phase 3's
  test plan carries a runtime check for each code change of phase 2 (that spec's §8); phase 5 checks
  `ExchangesModelFactory`'s untyped `GetShoppingCartAsync(customer)`, which now also returns stash lines.
- **Phase 3** (`CWCore_Phase3_Spec.md`, applied 2026-10-26): all eleven overrides registered under nop's interfaces, in
  the planned order, with no change to the order or the code. Phase 5's test plan carries every "Waits for packname
  data" line of that spec's §4 (the catalogue gate, the packname role set, hidden cart lines and caps, settlement in the
  totals, packname placement, restricted lists, destinations, packname e-mail rows, the packname invoice and packing
  slip with the 5.00 font, profit with subsidy, the packname sign-in landing).
- **Phase 4** is applied in eight parts, one spec each (`CWCore_Phase4_0_Spec.md`, then 4a–4g): 4.0 admin foundation;
  4a MultiStore and branches (the whole `MultiStoreController`, so packname lists, packnames, import, bulk and store
  settings come with it); 4b customers; 4c subsidies; 4d credit notes; 4e exchanges and returns; 4f orders; 4g bulk
  ordering admin.
- **Phase 4.0** (applied 2026-10-26): admin models, validators, Mapster profile, feature guard, view expander. For phase
  5: `PackNameCheckoutModelFactory` must call nop's address validator with `ValidateAsync` (5.00's has a `MustAsync`
  rule). **Correction (2026-10-26):** the 4.0 spec called the view location expander dead code. It is not:
  `ViewsPathsRegistry`'s static constructor carries ten default plugin paths, and the expander is how CW's forked
  views replace nop's (see "View overrides" below). The expander, registrar and registry stay.
- **View overrides — audit 2026-10-26** (Claude's inventory and an independent one, matching):
  - *Mechanism:* CW.Core's `ViewLocationExpander` puts ten `~/Plugins/CW.Core/...` locations ahead of the theme's and
    nop's, for the storefront and the admin area alike. Its startup (Order 3003) registers after nop's theme expander
    (Order 100), so CW's locations come first. A CW view replaces a nop view when nop asks for it by name with the same
    controller and view name, or the same `Shared` name. nop 5.00 leaves all of this unchanged: same expander order,
    same default locations, views still returned by name. None of the replaced views was renamed or moved.
  - *The 16 replaced nop views:*
    - storefront: `Order/Details`, `ReturnRequest/ReturnRequest`, `ReturnRequest/CustomerReturnRequests`, and the
      `FlyoutShoppingCart` and `OrderTotals` components (phase 5);
    - admin: `Customer/Create`, `Edit`, `List`, `_CreateOrUpdate`, `_CreateOrUpdate.Addresses` and
      `_CreateOrUpdate.Info`, `Product/_CreateOrUpdate.Info`, `Category/` and `Manufacturer/_CreateOrUpdate.Mappings`
      (4b); `Order/Edit` (4f); `ReturnRequest/Edit` (4e).
    - Six of them changed in nop 5.00, and each fork takes the change in its part.
    - The `_ViewImports` / `_ViewStart` forks apply by folder: the admin ones in 4.0. The storefront `_ViewImports`
      (phase 5) must drop `Nop.Web.Models.Boards`, `.News` and `.Polls`, or no storefront plugin view compiles.
  - *No unintended shadowing:*
    - storefront files never match admin lookups, and the reverse;
    - three CW views share a name with a nop `Shared` partial (`_ProductBox`, `_CheckoutAttributes`,
      `_CreateOrUpdateAddress`), but only under CW's own controllers.
  - *nop partials the forks call by path* changed in 5.00 (`_OrderDetails.*`, the customer tabs, `Shared/Table`). They
    are nop's own files, so the forks get the 5.00 versions; the parts that port those forks check they still fit.
  - *Themes:* the CW themes are out of this port (`CLAUDE.md` §3.2, §10.4).
- **Phase 4a** (applied 2026-10-26): MultiStore and branches, 84 files unchanged from 4.9 plus the registrations. The
  background jobs that use the controller's own services after the request (Generate packnames / packname lists,
  clearing pre-selected shipping addresses) ran without error on 5.00, so they stay as they are. `RouteProvider.cs`
  moves to 4b: nop's default admin route serves every 4a URL. `NopStartupTests` gained the controller and view-component
  dependency test; the phase 3 override test reached the repo with it (it had been left in Claude's workspace). Test
  data: `CWPlans/TestData/CWCore_Phase4a_TestData.sql` (local, the folder is ignored) mirrors the 4.9 branches, lists
  and packnames with the same ids; Steve Gates (customer 4) is packname 1 and a manager.
- **Phase 4b** (applied 2026-10-26): customers and the department ACL.
  - *Customer screens:* `ExtendedCustomerController`, the customer factory and the eight customer forks re-merged with
    nop 5.00 (phone verification, price lists, `PhoneSmsVerified`, private-message settings), plus `RouteProvider`.
  - *Department ACL:* the `AclDepartments` component and the Category, Manufacturer and Product forks. The field follows
    5.00's `IsAdvancedSetting`.
  - *Fixed, in 4.9 too:* *Change password* and *GDPR delete* now work (nop 5.00's actions), and nop's customer
    validator now runs on CW's form (`ExtendedCustomerValidator`).
  - *Found in T6:* a customer's branch and their packname's branch can drift apart. The decision (2026-10-26) is to
    keep them in step everywhere. That is part 4b.1.
- **Phase 4b.1** (applied 2026-10-26): a customer and their packname stay in the same branch.
  - *The packname follows the customer:* changing a customer's branch moves the packname into the new branch's default
    list. This is checked before the save and saved in one transaction.
  - *The customer follows the packname:* a consumer of packname and list events puts the customer in the packname's
    branch for every write path.
  - *Decisions:*
    - clearing the branch is refused while the packname is in a branch;
    - the move happens only on a branch change;
    - nothing moves while the PackName feature is off;
    - store-wide lists are in step whatever the customer's branch;
    - a branch in another store is refused.
  - *For phase 5:* delete the storefront `PackNameController`'s own customer-branch block when porting it.
  - *Data out of step* from before: `CWPlans/TestData/CWCore_Phase4b1_BranchMismatch.sql`.
  - *Deferred:* two findings, in §5.3.
- **Phase 4c** (applied 2026-10-27): the subsidy admin (product groups, subsidies, assignments, top-ups, variations, the
  packname *Subsidies* panel). 17 files unchanged from 4.9, plus the factory registration and the view entries. The
  validators refuse bad input on 5.00, and the Mapster maps keep a saved entity's store, creation date and deleted flag
  (`CWMapperConfigurationProfileTests`). The panel's LINQ queries and the history procedure run on LinqToDB 6 without
  error; *Used* is checked with packname orders in phase 5.
- **Phase 4d** (applied 2026-10-27): the credit-notes admin (the customer page's *Credit notes* card and the store
  liability report). 4 files from 4.9; `_CreditNoteHistoryTable.cshtml` was dead in 4.9 (its model never existed) and
  is not ported. Fixed, in 4.9 too (decided 2026-10-27):
  - **Report:** the outstanding and per-customer values add up what is left on the notes (4.9: their original
    amounts). The per-customer value is shown as a price, and the issued-value column title comes from its resource.
  - **Customer card:** amounts and the balance are shown as prices. Refused saves and deletes show their message. A
    save for an unknown customer is refused.
  - *Remain* comes from the two balance views on 5.00 as on 4.9. The `CreditNotesWidgets` component (admin order
    page, cart) returns with the widget zones (4f, phase 5).
- **Phase 4e** (applied 2026-10-27): exchanges configuration and return requests.
  - *Brought across:* the store editor's *Exchanges and Returns* and *Fee Settings* tabs, the default fee settings,
    and the return-request edit page (CW's fork of nop's view, `ReturnsController`, `ReturnRequestAdminFilter`). nop's
    return-request controller, factory, model and views are identical in 4.9 and 5.00, so the fork needed no merge.
  - *Filters:* CW's global MVC filters register with `Configure<MvcOptions>` (4.9 called `AddControllersWithViews`
    again); the three still to come join that block.
  - *Fixed, in 4.9 too (decided 2026-10-27):*
    - the edit page keeps the returned quantity;
    - *Notify customer* no longer gives a 404;
    - *Impersonate*, *Checkout notification* and *Undo approval* check the return-request permission;
    - the returned quantity is capped at the requested quantity (nop's rule, in CW's validator);
    - the linked-actions popup saves an empty selection, and its script no longer throws;
    - *Undo approval* asks for confirmation.
  - *For phase 5 (decided 2026-10-27):* the return workflow is tested with the storefront in place. That covers 4e's
    T9–T14 on a request made by the returns wizard, and the exchange-specific checks listed under that spec's test
    plan: fees, approval into a credit note or replacement order, *Impersonate*, *Checkout notification*, *Undo
    approval* with its confirmation, and the three buttons refused without the permission.
  - *Recorded:* `ReturnRequestOverrideModelFactory.PrepareReturnRequestModelAsync` throws when the request's product
    or order is gone (both soft-deleted in nop; its caller, the order page's return grid, comes with 4f).
- **Phase 4f** (applied 2026-10-28): the admin order page.
  - *Brought across:* CW's fork of `Order/Edit` and its cards, the packname card, the shipment destination picker, the
    E / R badges (order list and customer page), and the `AddShipmentDestinationFilter` and
    `PackNameOrderAdminGuardFilter` filters (in the `Configure<MvcOptions>` block). `_CWOrderDetails.Info` was re-made
    by a three-way merge with nop 5.00's partial (stored cards removed; the new payment-method widget zone).
  - *Widget plugin:* `CWCorePlugin` implements `IWidgetPlugin` again, listing only the zones whose components exist
    (store credit, `OrderDetailsBlock`, the two shipment-button zones, the order-list and customer badge zones). 4g and
    phase 5 add theirs. It stays hidden on the widget list.
  - *Storefront `_ViewImports` early:* needed by the store-credit row's views. nop 5.00's removed `Boards`, `News` and
    `Polls` namespaces are gone for good; CW's `ShoppingCart`, `PackNameCheckout`, `Orders`, `Subsidies` and
    `ReturnRequests` model namespaces return in phase 5 with their models.
  - *Fixed, in 4.9 too (decided 2026-10-28):*
    - the *Payments* card no longer repeats the payment buttons, whose copies skipped nop's confirmation dialog;
    - a packname order no longer offers *Edit order totals* or the billing *Edit*, which the guard always refused.
  - *For phase 5:*
    - `ExchangesWidgets` is not ported yet: re-add its line to `_CWOrderDetails.Info` and to
      `_CWOrderDetails.Payments` (the exchange-fees rows) when it lands;
    - the packname-order checks of the 4f test plan (packname card, CW's billing and shipping layout, hidden actions,
      the guard, the destination picker) run with the packname checkout;
    - the packname checkout decides whether it offers nop 5.00's desired delivery date.
  - *Recorded:* 4.9 shows the subsidy and store-credit rows on both *Info* and *Payments* (left for phase 5's review
    of the order page). Deploy into a clean `Plugins\CW.Core` folder: a 4.9 `Views` file left there would be used.
- **Phase 4g** (applied 2026-10-28): the bulk ordering admin, the last admin part of phase 4.
  - *Brought across:* the *Bulk ordering* card on nop's product edit page (`BulkOrderProductPageAdminViewComponent`,
    in nop's `ProductDetailsBlock` zone): whether the product's page shows the bulk grid (follow the store, always,
    never). Its model, resources and save were already ported.
  - *Fixed, in 4.9 too (decided 2026-10-28):*
    - CW's product save (`EventConsumer`, product updated) copied the edited product's departments and bulk-grid
      choice onto every associated product when a grouped product was changed to simple. It now acts only on the
      product whose `Id` the form posted;
    - the card's content is in nop's `card-body`, so it collapses, and its hint shows once (the label's tooltip).
  - *For phase 5:* the storefront side of the choice (the product page's grid), with the rest of bulk ordering.
- **Phase 5 split (decided 2026-10-28):** eight parts, in §4.3's order. About 180 files are left in 4.9.
  - 5a: the packname selector, the info strip and storefront packname management (add, edit, import).
  - 5b: the subsidy catalogue, the subsidy bar and top-ups.
  - 5c: the cart (the cart model factory override, the mini cart, the order totals).
  - 5d: the packname checkout (with the redirect filter, using CW.Freight). *Merged into 5c on 2026-10-28; the later
    parts keep their letters.*
  - 5e: order details and history.
  - 5f: My Subsidies and My Store Credit.
  - 5g: the returns and exchanges wizard (brings back `ExchangesWidgets` and the two order-page lines, and runs 4e's
    deferred workflow tests).
  - 5h: storefront bulk ordering.
  - Each part's test plan also covers the phase 3 overrides its screens exercise, for packnames.
  - Not ported (dead in 4.9): `Infrastructure/TypeConverterRegistrationStartUpTask.cs` (empty) and
    `Extensions/common.css` (unused).
- **Phase 5a** (applied 2026-10-28): the packname selector, the info strip and storefront packname management.
  - *Brought across:* the selector (`HeaderLinksAfter`) and the info strip (`ContentBefore`) as widget zones,
    `PackNameController` (select, add, edit, delete, Excel import), the storefront `PackNameModelFactory` and its
    validator, the Tom Select library.
  - *5.00 breaks fixed:* the controller asked for `IActionContextAccessor`, which 5.00 no longer registers
    (`Url.IsLocalUrl` instead); the import page passed `Url.Content(...)` to `AppendScriptParts`, which 5.00 rejects
    (pass the `~/` path); `IWebHelper` moved to `Nop.Services.Helpers`.
  - *Fixed, in 4.9 too (decided 2026-10-28):*
    - a packname's colour is a hex colour on every storefront path (`PackNameColourHelper`, shared with the
      registration service); the selector also refuses any other colour (it wrote the colour into its HTML);
    - a packname reached through a department opens in the edit form on its own branch, and the selector's **+** on a
      list reached through a department opens the add form on the manager's own list (both led to error pages);
    - edit refuses a group with its own customer account, and saves in one transaction;
    - the edit form uses the packname's store; the selector's add and Excel buttons follow the manager's store.
  - *Follow-ups (asked for after the first test):* the dialogs show *Subsidies* only when there is an active subsidy
    to tick; the info strip's details style (below).
  - *Style rule for phase 5:* the packname's colour stays on who it is (its type icon and name); its details beside it
    (branch, department, cost centre, PO) have grey icons, all one size (14 px), slightly darker text, and a thin
    divider after the name. 5b (subsidy bar), 5d (checkout packname box) and 5e (order details) apply it to their
    packname details.
  - *For 5b:* the info strip's subsidy button is hidden (`subsidyFlyoutAvailable = false` in its view) until the
    flyout's endpoint (`SubsidyCatalog/BarFlyout`) exists; 5b sets it back.
  - *For 5d:* the checkout's packname box writes the packname's name and branch with `Html.Raw`; encode them.
  - *Tools:* the registration check now reports framework services nobody registers (its output was filtered too
    widely in 5a's first pass), and the cycle check tells same-named admin and storefront services apart.
- **Phase 5b** (applied 2026-10-28): the subsidy catalogue, the subsidy bar and its flyout, managers' top-ups.
  - *Brought across:* `SubsidyCatalogController`, `SubsidyCatalogModelFactory`, `SubsidyBarViewComponent`, the
    `Models/Subsidies` catalogue and top-up models, two top-up validators, the catalogue views and assets. The
    storefront `_ViewImports` has the `Models.Subsidies` namespace again. The info strip's subsidy button is back, and
    shows only while *Subsidy* is on for the store.
  - *5.00 break fixed:* the catalogue page passed `Url.Content(...)` to `AppendScriptParts` (as 5a's import page did).
  - *Fixed, in 4.9 too (decided 2026-10-28):*
    - top-ups: one save at a time (a double-click credited twice); the reason capped at 255 in the forms and in all four
      top-up validators, storefront and admin (a longer one failed after the credit was saved); each top-up and its
      history row saved in one transaction (`SubsidyService`, admin too); the storefront forms take the admin forms'
      amount and quantity limits;
    - *Change top-up* shows only when the top-up pages would allow it;
    - the catalogue script: the grid / list switch works; dead listeners removed; the collapse script runs after jQuery.
  - *Follow-ups from the test run:*
    - **the bar's cart side was empty until the packname checkout had been opened**: its figures came from the
      settlement, which needs every packname's shipping address and method (assigned only by the checkout page), so it
      gave up and logged *"[Settlement] Cart settlement skipped … the settleable amount could not be calculated"* on
      every page. `GetCartSubsidyBreakdownAsync` now allocates the goods' subsidy without the budget until shipping can
      be rated; the checkout's totals and placement are unchanged;
    - **an exhausted group was neither marked nor collapsed**: a group is exhausted when its units and credit are used
      up and no global credit is left after the cart (`RemainGlobalCredit`, was "no global credit allocated"); after a
      cart change or a top-up a group that becomes exhausted collapses, and one that stops being exhausted opens.
  - *Style:* the bar's packname block follows 5a's rule (13 px icons in the bar).
  - *Recorded:* the reason-length message is FluentValidation's English default (no resource yet); a deactivated
    manager can still top up (for the manager to confirm); the admin top-up popups can still be submitted twice by a
    double-click; amounts are shown in the working currency's symbol without conversion; the catalogue's `Items` action
    and `loadItems` / `configProductDetailsLink` are unused (a product's picture opens its page, as in 4.9).
  - *For 5c:* the bar's cart side says *No items in cart* when the cart's lines use no subsidy; consider wording such
    as "Nothing in the cart uses this subsidy".
  - *Spec habit:* new `.js` files go in the project as `None Update`, as Visual Studio writes them.
- **Phase 5c** (applied 2026-10-29): the cart and the packname checkout (5c and 5d merged).
  - *Brought across:* `ShoppingCartOverrideModelFactory` (in place of nop's storefront `IShoppingCartModelFactory`),
    the mini cart and order totals forks, `PackNameCheckoutController`, `PackNameCheckoutModelFactory`, the storefront
    `PackNameOrderModelFactory`, `PackNameCheckoutRedirectFilter`, the packname order totals and order details view
    components, the `Models/ShoppingCart`, `Models/PackNameCheckout` and `Models/Orders` checkout models, the checkout
    views and assets. The storefront `_ViewImports` has the three model namespaces again.
  - *5.00 breaks fixed:* the cart factory's base constructor takes `ReturnRequestSettings`; `ColorTranslator` is
    ambiguous (`PackNameColourHelper.ForegroundFor`, 4.9's rule); `IWebHelper`'s namespace; `Url.RouteUrl<Product>`
    removed (`NopUrl.RouteGenericUrlAsync<Product>`); `Url.Content` in `AppendScriptParts`; the checkout's address
    validator ran synchronously and 5.00's state rule is async (a 500 on load); the cart totals called
    `ExchangesWidgets`, which arrives with 5g (taken out; **5g puts it back** in the OrderTotals fork too).
  - *Added:* a vendor choice on the packname checkout for 5.00's one-vendor carts (*Vendor enabled* / *required*),
    which stashes the other vendors' lines as nop's cart page does (decided 2026-10-28).
  - *Fixed, in 4.9 too (decided 2026-10-28):*
    - a packname's name could run as script: encoded in the checkout boxes, totals, mini cart, order details, the info
      strip's notices, the closed-ordering cart warning and the *Add new address* dialog (`T("…", arg)` does not encode
      its argument: use `string.Format(T("…").Text, arg)`); colours in styles normalised;
    - the shipping total converted to the customer's currency, and *Calculated during checkout* until shipping is
      priced; a line whose packname is gone; the cart lines copy all of nop's line (`VendorId`, `UnitPriceValue` were
      lost); the payment fee's tax converted once; no mini cart buttons on an empty cart;
    - new packname addresses take nop's address rules (`PackNameShippingAddressValidator`); the gift card *remove*
      button on the checkout totals (it placed the order) taken out.
  - *Follow-up from the test run:* the admin customer list opened on every customer (a 4b break): nop's admin mapping
    rules reached CW's search model map (§3.2 #20). The factory prepares nop's model in place; the two maps went;
    every CW map is now checked by `CWMapperGlobalRulesTests`.
  - *Style:* the checkout's packname boxes and the order details' groups follow 5a's rule.
  - *Recorded:* the cart's tax suffix (`!hasNoTaxExempt`) looks inverted (for the manager); the packname checkout has
    no gift card or discount code entry and no desired delivery date; with a vendor chosen, a cart whose lines of it
    are all removed shows as empty (adding anything brings the rest back); `ForeColor` is no longer read by any view;
    `ShouldInject()` is never true and the cart reads the open return requests once more than it needs; CW's view
    components still return `View(model)` (§3.2 #18); the payment fee tax fix has no unit test.
  - *For 5h:* the closed-ordering warning now carries the packname's name HTML-encoded; the bulk order page shows
    warnings as text, so it must show them as markup (or decode them) when it is ported.
  - *Header count:* the header's *Shopping cart (n)* is the whole cart over all packnames, as in 4.9 (the mini cart
    shows the selected packname's count); a per-packname count would be a new behaviour, for the manager to decide.
  - *Test data:* `CWPlans/TestData/CWFreight_TestShippingMethods.sql` sets up CW.Freight's six test methods, the
    provider and the store's default methods.
- **Phase 5e** (applied 2026-10-29): the customer's order details page.
  - *Brought across:* `OrderOverrideModelFactory` (in place of nop's storefront `IOrderModelFactory`),
    `CWOrderDetailsModel`, CW's fork of `Views/Order/Details.cshtml` (packname groups, settlement totals, shipment
    destinations; also the print page). The order history list is nop's own.
  - *5.00 changes:* `ReturnRequestSettings` in the base constructor; the page's desired delivery date line and return
    button text merged in; the page's `ExchangesWidgets` call taken out (**5g puts it back**, with the cart totals').
  - *Fixed, in 4.9 too (decided 2026-10-29):* an order's store credit line in the order's currency and rate (customer
    page) and as nop's admin totals (admin page), not today's working currency; the packname order table follows nop's
    table (no links on the print page, SKU and vendor columns by the store's settings, download / licence links,
    rental dates, the product-line widget zone); a line whose product is gone no longer fails the page.
  - *Recorded:* on a packname order the order-level shipping block (status, custom values, desired delivery date) stays
    hidden with the placeholder address; nop's tax / shipping note is not shown under the packname table; the
    packname completed page shows the table without download links.
- **Phase 5f** (applied 2026-10-29): My Subsidies and My Store Credit.
  - *Brought across:* `CustomerSubsidiesController` and `CustomerSubsidiesModelFactory` (registered in `NopStartup`),
    `CreditNotesController` (*My Store Credit*), their models, views and style sheets. Four 4.9 files unused there were
    left behind.
  - *Fixed, in 4.9 too (decided 2026-10-29):* both pages' amounts converted to the customer's currency; a product
    group's icon drawn only from a symbol id and coloured only by a hex colour (`PgIcon`, also on the catalogue, the
    subsidy bar and the cart); the packname header normalised and in 5a's style; the *My Subsidies* menu item only
    when the page has a subsidy to list; inactive product groups left off My Subsidies; the store credit ledger
    newest first, its names read once.
  - *Changed (decided 2026-10-29):* My Subsidies' history by season. The current season's rows under the figures, the
    earlier seasons under *Earlier seasons (n)*, each with its dates, what was topped up and used, and its rows. A
    movement belongs to a season by its store date (a top-up between seasons to the next, a refund to its order's
    season); the assignment, allocations and variations stand for every season from their own. The whole history is
    read; no database change. Plugin version `2026_10_29_DATA_0.1` installs the six texts.
  - *Recorded:* the admin product group pages (4c) write a group's icon and colour as typed (admin-only input; for
    the post-port list); the catalogue (5b) still lists inactive groups' products and formats amounts unconverted; the
    resource *Showing the latest {0} movements* is unused.
  - *Test data:* `CWPlans/TestData/CWSubsidy_TestPastSeason.sql` moves one assignment's subsidy dates, assignment,
    first top-up and one order back a year (and back again) to give a recurring subsidy a past season.
- **Phase 5g** (applied 2026-10-29): the returns and exchanges wizard.
  - *Brought across:* CW's return page and My Returns (forks of nop's views and storefront factory),
    `ExchangesController` (gather, summary, picker, receipts, exchange edit), `ExchangesModelFactory`,
    `ReturnRequestItemModelFactory`, the confirmation panel, the wizard's views, script and styles; `ExchangesWidgets` back
    in the cart totals (5c), the order details page (5e), the admin order page (*Info* and *Payments*, 4f) and on the widget
    plugin. `Views/Exchanges/_ProductBox.cshtml` was dead and is not ported.
  - *5.00 changes:* nop's storefront return factory constructor (`ReturnRequestSettings`, `INopUrlHelper`); CW's factory
    calls nop's page method, and CW's models copy nop's with the record copy constructor (nop 5.00's new values kept);
    the two views take 5.00's titles, withdrawal token and reason / action switches; `ErrorAsync`, `ViewAsync`; a guest's
    withdrawal link goes through nop's form, the wizard is for the signed-in customer (decided 2026-10-29); script paths
    passed as `~/...` (`AppendScriptParts` throws on `Url.Content`, found in the test run).
  - *Fixed, in 4.9 too (decided 2026-10-29):* every wizard post checked on the server (returnable shipped units, the
    reason's action, the mode, each replacement against its type, its required options and the item's packname
    catalogue); nop's own return post filtered (`ReturnRequestSubmitFilter`: the wizard with *Exchanges* on, the
    quantities on every path); only the customer's own orders, items, requests and receipts; antiforgery on every post,
    *Confirm* checked and registered in a lock per order, forms submit once; amounts in the customer's (or the order's)
    currency; texts, fee and exchange names from resources, script texts JSON-encoded; the picker (type change drops the
    replacement, late loads, the edit's catalogue, the first reason's actions, localized action names); accessibility
    (the picker opens from the row's link, named controls, announced errors, focus); only the store's active return
    reasons (found in the test run); smaller ones (product link, order number, a deleted replacement, edit keeps the
    options not posted, the store's shopping cart only, a dead summary row, the print icon).
  - *4e's deferred checks* (fees, approval into a credit note and a replacement order, *Impersonate*, *Checkout
    notification*, *Undo approval*, the permission) passed with the wizard's requests.
  - *Recorded:* the receipt PDF, e-mails and admin keep `ExchangesHelper`'s English names; a different-style replacement
    outside the allocation is refused at approval (as 4.9); units count as shipped once on a shipment; the storefront order
    page shows an order's exchange fees with *Exchanges* off; My Returns reads about ten queries a row; an exchange edit
    does not update the return case line's copy, and keeps one value per option; a confirmation refused by a concurrent
    one goes to My Returns without the panel; `CountReturnedUnitsAsync` is unused.
- **Phase 5h** (applied 2026-10-30): storefront bulk ordering, the last part of phase 5. **Phase 5 is complete.**
  - *Brought across:* `BulkOrderController` (`/bulkordering`), the header link (`HeaderLinksBefore`), the page's category
    navigation, the product page grid (`ProductDetailsOverviewBottom`, `ProductDetailsBottom`, `ContentAfter`), the
    page's views, script and style sheet. Its services, factory and logic came in phase 2. Not ported (dead in 4.9):
    `Extensions/common.css`, `Infrastructure/TypeConverterRegistrationStartUpTask.cs`,
    `Models/CreditNotes/CustomerCreditNotesModel.cs`.
  - *5.00 changes:* the three view components use `ViewAsync`; the script by its `~/` path.
  - *Fixed, in 4.9 too (decided 2026-10-29):* one value per option unless it is a checkbox option (a hand-made post
    could stack price adjustments and skip the combination's stock); nothing written for an option the grid has no box
    for; at most 1000 filled boxes per add, each product's visibility asked once, lines of the same product and options
    added as one; warnings as plain text (5c's encoded packname name, nop's required-product link); a body that is not
    a request refused; the category tree's cache keyed by order only for *Latest ordered*, and only the customer's own
    orders; the chosen category kept on paging; adding waits for a card's grid reload, a failed reload puts the pinned
    options back; a reload no longer puts back what an add has just put in the cart; quantity boxes checked (whole
    numbers up to 2147483647); the product page's second antiforgery field (it broke *Estimate shipping*) removed;
    failures said in the card's live region, each *Add to cart* named for its product.
  - *Recorded:* best sellers are added up on every list (not cached); the category tree's cache is cleared by every
    order or product change and ignores availability dates; pinned options are not kept across pages; lines with the
    same product and options sent by hand are refused or added together; nop adds to a matching cart line without an
    overflow check.
  - *nop 5.00 options to keep off with packnames (decided 2026-10-29):* sign-in by phone code
    (`OtpSettings.LoginByPhoneEnabled`: it skips nop's sign-in path, so CW's landing, the packname set-up at sign-in and
    the cart move do not run); vendors (`VendorSettings`: stashed packname lines would vanish or double); price lists
    for single customers (nop caches prices per role, so one customer's price-list price can show to another of the same
    roles). This sharpens D3 and §5.1.
  - *Tests deferred (decided 2026-10-30):* T1–T4, T6–T8 and T17 passed on the beta; T5, T8b, T9–T16, T18 and T19 need a
    fuller catalogue and run on the final 5.00's database (§7).

### Final §5 Decisions of the move to the final 5.00


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

---

## 6. nop code CW.Core is tied to

Check these on every nop upgrade. A change to any of them can break CW.Core without a build error. `CLAUDE.md` §10.3 keeps the table of copied method bodies to re-diff, and §10.5 the porting checklist.


A change to any of these between the beta and the final can break CW.Core without a build error.

- **Copied method bodies:** `CLAUDE.md` §10.3's table (`SaveOrderDetailsAsync`, `PlaceOrderAsync`, `AddToCartAsync`,
  `SearchProductsAsync`, the PDF methods and the rest) and `ExtendedCustomerController`, a copy of nop's admin
  `CustomerController` (Create, Edit, List; merged with the beta in 4b).
- **Services CW.Core overrides by inheritance** (constructors re-derived on the beta; each override calls into its
  base): `AclService`, `CustomerService`, `CustomerRegistrationService`, `ShoppingCartService` (the **list** overload of
  `GetShoppingCartAsync`, §3.1 #6), `OrderTotalCalculationService`, `OrderProcessingService`, `ProductService`,
  `ShipmentService`, `MessageTokenProvider`, `WorkflowMessageService` (two), `PdfService` (and `InvoiceDocument`,
  `InvoiceTotals`, `ShipmentDocument`), `OrderReportService`, `PluginService`; nop's `PlaceOrderContainer`,
  `PlaceOrderResult`, `OrderItem` and `ShoppingCartItem` as base classes.
- **Model factories and controllers CW.Core inherits:** admin `CustomerModelFactory`, storefront
  `ShoppingCartModelFactory`, `OrderModelFactory` and `ReturnRequestModelFactory`, admin `ReturnRequestController`
  (`ReturnsController`). CW's return page, return list, order details and payment-method models derive from nop's
  with nop's record copy constructor (new members come along; check for renamed ones).
- **Forked views** (§5.2, *View overrides*): storefront `Order/Details`, `ReturnRequest/ReturnRequest`,
  `ReturnRequest/CustomerReturnRequests`, the `FlyoutShoppingCart` and `OrderTotals` components; admin `Customer/Create`,
  `Edit`, `List`, `_CreateOrUpdate`, `_CreateOrUpdate.Addresses`, `_CreateOrUpdate.Info`, `Product/_CreateOrUpdate.Info`,
  `Category/` and `Manufacturer/_CreateOrUpdate.Mappings`, `Order/Edit`, `ReturnRequest/Edit`; the renamed copies
  `_CWOrderDetails.Info`, `.Payments`, `.BillingAddress`, `.BillingShipping` (from `_OrderDetails.*`) and
  `AclDepartments` (from `AclCustomerRoles`); both `_ViewImports`.
- **Behaviours relied on at runtime:**
  - `ModelPreparedEvent<BaseNopModel>` raised by `PublishModelEventsFilter` after controller actions and by
    `NopViewComponent.ViewAsync` (`EventConsumer`, `PrintNamePrefillEventConsumer`, the product page grid). nop's
    issue #7756 (carry the concrete model type) was open on the 5.00 milestone: if it landed, these consumers stop
    firing silently;
  - `AppendScriptParts` / `AddScriptParts` taking `~/` paths (`CLAUDE.md` §4);
  - Mapster's single global configuration and nop's admin `ForAllMaps` rules (§3.2 #20; `CWMapperGlobalRulesTests`);
  - nop's antiforgery field at the top of every page (`_Root.Head`), which the bulk ordering script reads; the
    product details form and its zones; `AjaxCart`'s selectors; the widget zone names CW registers;
  - `ReturnRequestSettings` and the guest withdrawal token on nop's return pages (5g's filter on nop's return post);
  - `ILocker.PerformActionWithLockAsync` (5g's confirmation lock); `ShoppingCartType.Stash`;
  - the nop issues open on the 5.00 milestone when §2.4 was written: image URLs as localhost (#7813), the
    DeleteGuests task (#7847), a PayPal capture error (#7810, CW's own placement uses PayPal).

## 7. Settings kept as decided


- **Off:** the withdrawal form, auto-cancel of unpaid orders (D3); sign-in by phone code, vendors, price lists (5h;
  price lists until §5.1's study).
- **Used:** reminders (D3).
- Check each on the new database: a fresh install may switch some on by default.

On `testnop50` they were checked in M3 and are as decided (vendors, the vendor-account option, auto-cancel, the withdrawal form, phone sign-in and price lists off; reminders on).

---

## 8. Defects found and fixed during the move to the final 5.00

Only the phase 6 defect came from the port itself. The others were already in 4.9 or the beta and were found by these
checks.

| Found in | Defect | Fix |
|---|---|---|
| M2 (GDPR check) | A GDPR delete left the packname cart lines the cart override hides | `CustomerPermanentlyDeletedEventConsumer` deletes them too |
| M2 review | A manager-registered account's text fields were not capped as nop's registration caps them | Capped at 1,000 characters (`PackNameRegistrationService`). M3 fixes added the same check to the *Add packname* form |
| M3 | With *Exchanges* on and no active reason rule, the return page showed empty required lists and the submit did nothing | The page shows a message instead of the form |
| M3 | With *Exchanges* off, the return page still filtered the actions by CW's rules | With *Exchanges* off the page keeps nop's lists |
| Phase 6 (from the port) | The order and shipment e-mails printed the order's first address, not each destination. The beta's phase 1 removed the two template-rewrite calls from `CWCorePlugin.InstallOrUpdateAsync` "until phase 3", and they never came back | The calls are restored; plugin version `2026_10_31_DATA_0.1` |
| Phase 7 | Deleting a branch address on *CW Admin → Branches* threw *No branch found*: the grid sends `EntityId`, the action bound `branchId` | `BranchController.AddressDelete(int id, int entityId)` |
| Phase 7 | The manual credit note's activity text labelled both ids *StoreId* | Text corrected (`CreditNotesService`) |
| Phase 7 | A plain nop return showed an empty exchange block on *My account → Return requests* | The block shows only for `ExchangeReturnMode.Exchange` |
| Phase 7 | The packname checkout used `@T("Common.Loading")`, a key 5.00 does not have (a warning on every load) | `@T("Common.Wait")` |
| Deferred (5a review) | The admin packname saves stored the colour as typed | The five admin saves keep only a hex colour: a random one on a new packname, the old one on an edit |
| Deferred (4b.1 review) | Provisioning could create a packname whose name the list already had | Skipped: the customer page shows a warning, and *Generate packnames* logs one. Plugin version `2026_10_31_DATA_0.2` |
| Deferred (4b.1 review) | A branch-tied cost centre stayed on a packname moved to another branch | **Dropped.** Nothing ties a cost centre to its branch: every picker and save offers or accepts all of the store's cost centres. If branch restriction is wanted, that is a decision for the manager |

Also set during phase 7 on `testnop50`: the return status rule *Return authorized* performs store credit and exchange.
M3's data had these flags on *Received*, so an authorisation made no credit note. This is configuration, not code.

## 9. Open after the port

**CW.Core:**
- The *ReturnAuthorisedPayment* and *ReturnAuthorisedExcepted* customer e-mails are sent only when their templates
  exist, and CW.Core installs neither, on 4.9 as well. The customer gets no e-mail when a return is authorised.
- A storefront access-denied lands on nop's *Page not found* and logs a 404. `PackNameController` (26 calls) and
  `CustomerSubsidiesController` use `AccessDeniedView()`, which redirects to an admin-only action. 4.9 behaves the
  same. Which answer the storefront should give is a design choice.
- A comment in `Services/Common/CWPdfService.cs` (line 192) names `CWPdfService_Modernisation_Spec.md`, which
  `CLAUDE.md` §7 forbids. That spec is still open, and the return note is still built by hand with iTextSharp.
- After the last line is removed on the packname checkout, the summary stays until a reload. This is minor and was not
  compared with 4.9.
- The uninstall was not run (decided 2026-10-31: the data is worth more than the test). The code was read instead:
  - The install, update and uninstall are the same as 4.9's.
  - `SchemaMigration.Down()` drops all 43 tables, so an uninstall deletes every CW.Core row.
  - The SQL views, functions and procedures, `MultiStoreSettings`, the two message templates, the activity types and
    20 resources outside `Plugins.CW.Core` stay behind. A reinstall reuses them.

**Business work after the port** (Beta §5.1): PunchOut, sign-in by phone, subsidy reminders, price lists with
packnames, nop's new return rules in CW's wizard, 3D/AI, and the desired delivery date in the packname order builder.

**Outside CW.Core, seen on `testnop50`:**
- `ExternalAuth.SSO` has no `logo.jpg`.
- `Misc.RFQ` is installed: its *Request a quote* button shows on the packname checkout.
- `AIRecommendation.GoogleAI` is installed and enabled but served no search.
- nop's `OnePageCheckout.cshtml` uses the missing `Common.Loading` key too.
- `Widgets.GoogleAnalytics` is installed without being set up and logs errors on cart changes. Its HTTP client sets
  `BaseAddress` per request, and two cart events in one request (a bulk add) trip it.

**The other plugins** (`CLAUDE.md` §3.2):
- `Integration.Odoo` is first: 5.00's data comes from it (D2), and it depends on CW.Core.
- Then ThemeCore, Theme.Standard and the themes. When a theme arrives, re-run `CLAUDE.md` §10.4 step 2.
- Still to test: NAB against the test gateway, and SSO with Entra.

## 10. Sources and test material

**Consolidated here and moved to `CWPlans/Deprecated/`:**
- `CWCore_Port_Strategy.md`, the beta port's strategy (its §2–§3, §5, §7.2–§7.3 are carried above);
- `CWCore_Final500_Strategy.md`;
- `CWCore_Final500_M2_Spec.md`, `CWCore_Final500_M3_Plan.md`, `CWCore_Final500_M3Fixes_Spec.md` and
  `CWCore_Final500_M4_Plan.md`;
- `CWCore_Final500_Phase6_Plan.md`, `CWCore_Final500_Phase6Fixes_Spec.md`, `CWCore_Final500_Phase7_Plan.md` and
  `CWCore_Final500_Phase7Fixes_Spec.md`;
- `CWCore_Final500_DeferredFixes_Spec.md`.

Their exact changes are applied, and what they decided is in §2, §5 and §8.

**Each part's full test plan** (§6 of each spec) is in the beta port source, `CWPlans/Deprecated/CWCore_Phase{part}_Spec.md`
(`CLAUDE.md` §3.1). The phase 7 regression ran them all again. Its per-test record is the tracker artifact
*CW.Core final 5.00 — phase 7 regression*.

**Test data** for `testnop50` is in `CWPlans/TestData/`, which is local and not in git:
- `CWCore_M3_InstallCheck.sql`: install check (tables, views, functions, procedures, resources, permissions).
- `CWCore_Phase4a_TestData.sql` and `CWCore_Phase4e_TestData.sql`: the beta's admin test data.
- `CWFreight_TestShippingMethods.sql`.
- `CWSubsidy_TestPastSeason.sql`: moves a recurring subsidy's history one year back, and undoes it.

**`testnop50` settings that differ from a fresh install:**
- 4.9's tax settings: prices include tax, tax shown, shipping taxable (decision 10).
- The return rules M3 set up for store 1.
- The vendor-account option is off.
