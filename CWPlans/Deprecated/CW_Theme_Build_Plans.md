# Build Plans — TIG Custom Theme (consolidated)

**Project:** NopCommerce 4.90 (TIG) — custom theming initiative
**Companion docs (kept separate):** `CW_Theme_Architecture_Foundation.md` (architecture/decisions), `CW_Theme_Effort_Estimate.md` (effort).
**About this file:** consolidates the former Phase 1, Phase 2a (Custom CSS), and Phase 2b (First Theme) build-plan files into one record.

## Status at a glance

| Phase / item | Status |
|---|---|
| Phase 1 — `CW.ThemeCore` + Capability 1 (accent colours) | ✅ Implemented & validated (2026-07-23) |
| Phase 2a — Capability 2 (per-store Custom CSS) | ✅ Implemented & validated |
| Phase 2b — First TIG theme (Option B `CW.StandardTest`, Option A `CW.Standard`) | ✅ Implemented & validated |
| `DisableAccentColors` setting | ✅ Implemented & validated |
| Phase 2c — Per-theme plugin + Pattern-C base classes (`CW.Theme.Standard`) | ✅ Implemented (compiles); pending deploy + install verification |
| Token audit (WS3, TIG.Core CSS → `var(--cw-*)`) | ⏸ Postponed — coordinate with other programmer |
| Commit to git | ⚠️ Nothing committed yet |

> **Concept update (2026-08-10).** The client redesigned the theming concept: a small set of **predefined, fully-styled themes** to pick from, **plus** a **per-store, per-section layout selector** (Brooklyn/SevenSpikes-style), with the layout system living in **`CW.ThemeCore`**. **No new themes are built now** — **finishing the Voyage-equivalent `CW.Standard` is Task 1.** Theme-spawning/Tier-3 is dropped; the WYSIWYG visual CSS editor + AI remain on the roadmap. Full spec: [`CW_Theme_Layout_System_Plan.md`](CW_Theme_Layout_System_Plan.md).

---

## Cross-cutting design requirements

These apply to **every** TIG theme, on every page, regardless of phase.

- **Responsive / adaptive layout (required).** The design must adapt to different screen sizes and devices — desktop, tablet, and mobile — with **no horizontal overflow**, a working mobile (hamburger) menu, and content that reflows (grids drop columns, flex rows wrap). Whenever a page's layout is touched, verify at representative widths (≈375 px mobile, ≈768 px tablet, ≥1200 px desktop).

**Current state — does it meet the requirement? ✅ Yes at the tested breakpoints** (verified 2026-07-28, `CW.Standard` home page):

| Width | Overflow | Menu | Featured Categories |
|---|---|---|---|
| 375 px (mobile) | none | base hamburger (`.menu__toggle`) shows; nav collapsed | 1 column |
| 768 px (tablet) | none | hamburger nav | 2 columns |
| ≥1200 px (desktop) | none | full-width nav, items sized relative to menu area (`flex:1 1 auto`) | auto-fills |

- **Why it holds:** responsiveness is carried by the **base DefaultClean stylesheet** (its media queries + mobile menu). `cw-brand.css` layers mostly visual styling; the desktop nav rules (`flex`, `width:100%`, `nowrap`) go **inert on small screens** because the base flips `.menu` to `display:block` behind the hamburger, and the Featured grid uses `repeat(auto-fill, minmax(280px, 1fr))` which reflows on its own.
- **Cleanup noted:** the `@media (max-width:900px)` block in `cw-brand.css` targets pre-4.9 `ul/li` menu selectors (`.header-menu > ul > li`) that don't exist in 4.9's BEM markup — it is **dead** (harmless) and should be removed or rewritten. It is *not* what provides mobile behaviour (the base is).
- **Not yet covered:** the deeper pages (product details, cart, checkout, account) have had visual layering but **not** a dedicated multi-width pass — see Next steps #3. RTL likewise pending.

---

# Phase 1 — `CW.ThemeCore` + Capability 1 (accent colours) — ✅ DONE

**Outcome:** `Nop.Plugin.CW.ThemeCore` built (0/0), installed, per-store `:root{--cw-*}` block confirmed in the public `<head>`. Two deliberate simplifications vs. the original plan (§0.6): (a) no bespoke `IStaticCacheManager` layer (settings already cached, string build trivial); (b) no standalone `ITigThemeStyleSink` — the generation seam is `ITigThemeCssService`, consumed by the Phase 2a file-writer. Build note: build via the solution or pass `-p:SolutionDir=` (single-`.csproj` build leaves `$(SolutionDir)` undefined).

**Definition of done (met):**
1. Installs/uninstalls cleanly; appears under the TIG admin menu.
2. Admin sets accent colours (+ logo) **per store** via nop's store-scope override UI.
3. Injects a `:root{--cw-*}` block into `<head>` reflecting the current store.
4. Settings page has a **live in-page preview**.
5. All code under `Plugins/Nop.Plugin.CW.ThemeCore` (Cardinal Rule).

**Key design decisions:**

| Decision | Choice | Why |
|---|---|---|
| Coupling (Pattern C) | Base classes deferred; ships as a normal plugin | No per-theme consumer yet |
| Persistence | **`ISettings` only — no tables/migrations** | Colours + logo fit `Setting`/`Picture` |
| CSS generation | Build `:root{}` from settings; **no LESS/dotless** | CSS custom properties replace the NT LESS pipeline |
| Injection point | Core zone `PublicWidgetZones.HeadHtmlTag` | Works on any active theme; no dependency on a TIG theme |
| Injection form | **Hybrid** — inline `:root{}` tokens now; Phase 2a routes bulk Custom CSS to a versioned file | Small primitive inline; large payloads → cacheable file |
| Controller base | `BasePluginController` + explicit admin attributes | Framework doesn't reference TIG.Core |
| Tokens | `--cw-primary` (+tint), success, danger, warning, dark; placeholder defaults | Palette refinable; per-store hex is a setting |

**Project structure (no DB schema):** `TigThemeCorePlugin` (BasePlugin + IWidgetPlugin + IConsumer<AdminMenuCreatedEvent>), `Infrastructure/NopStartup`, `Settings/TigThemeSettings`, `Services/ITigThemeCssService`+impl, `Components/TigThemeHeadViewComponent`, `Areas/Admin/{Controllers,Models,Validators}`, `Services/Security/TigThemeCorePermissionConfigManager`, `Infrastructure/ViewLocationExpander`, `Views/{_ViewImports, Configure, Components/TigThemeHead/Default}`.

**Tasks (all done):** P1-T1 scaffold & lifecycle · P1-T2 settings · P1-T3 CSS-variable service · P1-T4 head injection widget · P1-T5 admin settings page · P1-T6 live preview · P1-T7 admin menu (anchored on `TIG.Core` by system name, root fallback) · P1-T8 localization · P1-T9 permission `CW.ThemeCore.Manage` · P1-T10 DI registration.

**Verification:** multi-store per-store colours inject correctly; live preview updates; invalid hex blocked; uninstall removes everything. Testing is manual (no TIG test project per Foundation §22 Note A).

---

# Phase 2a — Capability 2 (per-store Custom CSS) — ✅ DONE

**Outcome:** extends `CW.ThemeCore` (no new plugin). Benign CSS applies; hostile CSS sanitized. **Bug fixed during verification:** a literal `<link rel="stylesheet">` in the `HeadHtmlTag` widget-zone view was swallowed by nop/WebOptimizer's link tag helper (routed into `NopHtml`'s CSS collection, already emitted by `_Root.Head.GenerateCssFiles()` before the widget zone) — fixed by emitting the link via `@Html.Raw` (`tasks/lessons.md` #1).

**Definition of done (met):**
1. Admin enters Custom CSS **per store** on the settings page.
2. On save it's **sanitized** and a versioned `theme.custom-{storeId}.css` is (re)generated.
3. Public `<head>` links it **after** the inline `:root{}` tokens, cache-busted by `?v={version}`.
4. Uninstall removes generated files + settings + resources.

**Architecture principle — lean core, fat themes.** `CW.ThemeCore` holds only shared *machinery*; all *content* (views, stylesheets, generated CSS) belongs to each theme.
**Generated-file rule:** generated CSS lives inside the deployed plugin/theme, **never `wwwroot`** — resolved by a path resolver: core plugin folder now (`~/Plugins/CW.ThemeCore/Content/generated/theme.custom-{storeId}.css`), active-TIG-theme folder later (`~/Themes/{theme}/Content/css/…`). Regenerated on save; lazily re-created if missing.

**Resolved decisions:** generated-file location = inside deployed plugin/theme; `url()` handling = configurable setting `CustomCssUrlPolicy` (Allow / DataAndSameOriginOnly / Block, per-store, **default Allow**); hybrid injection (custom CSS → file, tokens inline).

**Tasks (all done):** C2-T1 `CustomCss` + `CustomCssUrlPolicy` on settings · C2-T2 `ITigCssSanitizer` · C2-T3 `ITigThemeCssPathResolver` · C2-T4 `ITigThemeCssFileService` · C2-T5 head-injection link (via `@Html.Raw`) · C2-T6 admin textarea + policy dropdown · C2-T7 lifecycle & locales.

**Sanitizer policy — always blocked (non-configurable):** `@import`, `expression(`, `javascript:`/`vbscript:`, `behavior:`/`-moz-binding`, and `<`/`>` (markup-breakout guard). Size cap enforced. `url()` handled per `CustomCssUrlPolicy`; on any removal the rest of the CSS is kept and a warning is surfaced.

**Verification:** benign applies; hostile stripped with warnings; each `url()` policy behaves; tokens render before the custom link; file lives in the deployed plugin folder; uninstall leaves nothing.

---

# Phase 2b — First TIG theme — ✅ DONE

**Goal:** stand up the theme process end-to-end so real design is a later swap of design-only elements. Palette placeholder = Voyage's primary `e43d51` (set as the test store's `--cw-primary` via ThemeCore admin — reuses Capability 1).

**Resolved decisions:**
- **Name:** `CW.Standard` (clean-room) + `CW.StandardTest` (Voyage copy) — **both retained**.
- **Dev/test store:** **the test store (store 4)** via per-store `DefaultStoreTheme` override; the other stores stay DefaultClean.
- **Skin source:** **both** — Option B first (adapt licensed Voyage; licence permits internal reuse), then Option A (clean-room own files).
- **Scope:** lean vertical slice; broad per-page rework is future.

**Iteration sequencing:**
- **Option B — done:** `Themes/Voyage` copied/adapted → `Themes/CW.StandardTest` (identifiers renamed, asset paths repointed), wired to the test store. Retained as a Voyage reference.
- **Option A — done:** clean-room `Themes/CW.Standard/` — `theme.json`, `_ViewImports`, `Head.cshtml`, and **original** CSS: a copy of nop's OSS `DefaultClean` base (layout + images) plus an original token-driven **brand/theme layer** `cw-brand.css` (typography, dark nav bar, search treatment, product cards with hover, prices, side blocks, forms, footer) that maps `--cw-*` → local tokens and layers over the base without replacing the layout. The test store points at `CW.Standard`. The `DESIGN TOKENS` block at the top of `cw-brand.css` + the ThemeCore accent colours are the designer's swap surface.

**Definition of done (met):** theme appears in admin list; the test store renders through it; changing `--cw-primary` re-skins live (design-swap proof); Capability 1 + 2 inject under the theme; other stores unaffected; only theme + plugin files touched.

**Cardinal-rule note:** all theme files under `Themes/TIG.*/`; no core Nop.Web view edited — overrides resolve via the themeable view-location expander.

**Lessons captured during Phase 2b:**
- Theme `_ViewImports` must include `@using Nop.Web.Extensions` (home of `ShouldUseRtlThemeAsync`) — trimming it too far breaks `Head.cshtml`.
- A clean-room theme that **replaces** the base stylesheet needs a *complete* stylesheet, or the layout breaks; the robust pattern is **DefaultClean base (nop OSS) + a thin token-driven brand layer**.

---

# Post-2b additions (2026-07-23)

- **`DisableAccentColors` setting** — per-store toggle on `TigThemeSettings` (default off = colours applied). When on, ThemeCore skips the `:root{--cw-*}` block, handing colour control to the theme; Custom CSS unaffected. Admin checkbox on the settings page. Validated.
- **`CW.Standard` evolved** from a plain DefaultClean-look into the clean-room **Voyage-like** polish described above (still original, still token-driven).

**Operational note (dev):** `CW.ThemeCore` is an `IWidgetPlugin` with `HideInWidgetList=true`. **Disabling** the plugin (admin → Local plugins → Enabled) — or uninstalling it — stops all injection; there's no widget-list toggle to re-enable it (hidden), so recovery is re-enable/reinstall (or restore the `widgetsettings.activewidgetsystemnames` entry). Expected behaviour; flagged so it isn't mistaken for a bug.

---

# Phase 2c — Per-theme plugin + Pattern-C base classes — ✅ IMPLEMENTED (deploy/install pending)

**Goal:** exercise base-class inheritance from the ThemeCore framework and give `CW.Standard` its own admin settings — completing the two-tier model.

**Part A — base classes added to `CW.ThemeCore`** (additive/public; central controller untouched):
- `BaseTigThemeSettings : ISettings` — base for a theme's own settings.
- `BaseTigThemeSettingsModel : BaseNopModel` — base admin model (store-scope context).
- `BaseTigThemeAdminController<TSettings,TModel> : BasePluginController` — template-method Configure GET/POST handling all store-scope override boilerplate; `[CheckPermission(CW.ThemeCore.Manage)]`; delegates theme-specific fields to abstract hooks (`GetConfigureViewPath`, `PrepareModelAsync`, `MapToSettings`, `SaveOverridablePerStoreAsync`).

**Part B — new plugin `Nop.Plugin.CW.Theme.Standard`:**
- `.csproj` uses a **`<ProjectReference>` to `Nop.Plugin.CW.ThemeCore.csproj`** with `<Private>false</Private>` + `<ExcludeAssets>runtime</ExcludeAssets>` (see the coupling decision below). `plugin.json` → `DependsOnSystemNames: ["CW.ThemeCore"]`.
- `StandardThemeSettings : BaseTigThemeSettings` (`LightNavBar`, `SquareCorners`), `StandardThemeSettingsModel : BaseTigThemeSettingsModel`, and a **thin** `StandardThemeAdminController : BaseTigThemeAdminController<…>` (just the hooks).
- Plugin lifecycle + menu (via `AdminMenuCreatedEvent`, anchored on `TIG.Core`) + Configure view + locales. No `INopStartup` needed.

**Part C — `CW.Standard` theme consumes the settings:** `Head.cshtml` reads them by **setting key** (`standardthemesettings.lightnavbar` / `.squarecorners`) — decoupled from the plugin's types, graceful when the plugin isn't installed — and emits `:root{…!important}` token overrides. `cw-brand.css` parameterises the nav bar via `--cws-nav-*` tokens; corner radius already tokenised.

**Verification (compile):** both plugins build with 0 errors; `CW.Theme.Standard` compiles against the base-class-bearing `CW.ThemeCore.dll`.

**Coupling decision (2026-07-24): `ProjectReference` to the ThemeCore framework is allowed here.**
§10.2 forbids `<ProjectReference>` between **peer feature plugins** for two reasons: (a) it doesn't want independent plugins coupling build order; (b) it prevents the plugin-loader tangle (a project reference copies the dependency's DLL into your output → duplicate assembly → load conflicts). Neither objection applies to a **theme → framework** relationship: `CW.Theme.Standard` genuinely *is* a dependent extension of the `CW.ThemeCore` framework (inherits its base classes), so (a) coupling build order is *correct*; and (b) is neutralised by `<Private>false</Private>` + `<ExcludeAssets>runtime</ExcludeAssets>`, which keep `CW.ThemeCore.dll` out of this plugin's output (no duplicate → no tangle) while keeping the compile reference. Net benefit: the `ProjectReference` gives the compile reference **and** an MSBuild build-order edge in one line — so the earlier "build ThemeCore first / build twice" friction is gone.

**Deploy notes:**
- Both plugins need the app **stopped** to deploy (DLL lock).
- **Build order is now automatic** via the `ProjectReference` — a solution build compiles `CW.ThemeCore` before `CW.Theme.Standard`.
- After deploy: install **CW.Theme.Standard** (Local plugins); it requires CW.ThemeCore installed.
- Test: Admin → **TIG Standard theme** → toggle *Light navigation bar* / *Square corners* (per store) → reload the test store → nav bar lightens / corners square.

**Verification caveat:** the C# compile is confirmed (dll produced, base classes resolve). The *post-build packaging* (nop's `ClearPluginAssemblies`/`NopClear` dedup + `PreserveCompilationContext`) can only be validated by a real, app-stopped solution build with each plugin in its own output folder — a redirected-`OutDir` compile-check confuses `ClearPluginAssemblies` and can throw a spurious `MSB3030`. On the real build, confirm `Plugins/CW.Theme.Standard/` contains **no** stray `Nop.Plugin.CW.ThemeCore.dll`.

---

# Deferred / next steps

> Re-prioritized 2026-08-10 per the concept redesign — see [`CW_Theme_Layout_System_Plan.md`](CW_Theme_Layout_System_Plan.md) §2 for the full roadmap.

1. **(TASK 1) Finish the Voyage-equivalent `CW.Standard`** — continue the CSS-first view-override backlog below (`_ProductBox` done). B2B is simpler → marketing-only views can be trimmed from the 75-view parity list.
2. **(TASK 2) Layout system foundation in `CW.ThemeCore`** — manifest + schema-driven per-store layout selector + theme-side read; proof slice = Product details One/Two Column. See the layout-system plan §4/§8.
3. **(TASK 3) Layouts per section** — Home, Catalog, Checkout (one-page); default now, alternates incrementally.
4. **(TASK 4, last) Bulk Order section** — gated on the CW.Core PackName/subsidy bulk-order flow being finished.
5. **Deploy + install-verify Phase 2c** — rebuild (ThemeCore first), install `CW.Theme.Standard`, confirm the toggles re-skin the test store.
6. **Token audit (WS3)** — refactor TIG.Core public CSS to `var(--cw-*)`. Postponed; **coordinate with the other programmer** (shared TIG.Core CSS). Palette decided, ready to run once coordinated.
7. **Commit** — the whole initiative (plugins, both themes, `CWPlans/` docs + the design-swap guide with the plugin) is uncommitted; checkpoint it before the next phase.
8. **AI-powered CSS generation + WYSIWYG visual editor (still wanted)** — separate phase; feasibility/estimate **done** at [`CW_Theme_AI_CSS_Editor_Feasibility_And_Estimate.md`](CW_Theme_AI_CSS_Editor_Feasibility_And_Estimate.md) (brief: [`CW_Theme_AI_CSS_Editor_Feasibility_Brief.md`](CW_Theme_AI_CSS_Editor_Feasibility_Brief.md)).

## Open decisions (deferred)

- **Logo settings overlap (raised 2026-08-10).** The theme's `CwThemeSettings.LogoPictureId` may duplicate nop core `StoreInformationSettings.LogoPictureId` (Admin → Settings → General & Common) — both per-store, likely the same logo. Decide whether to (a) drop the theme setting and read the core one, (b) keep it only as a theme-specific override, or (c) keep both. Same review covers whether `SecondLogoLinkUrl` stays (optional click-through) or the second logo is image-only. The second-logo picture + reverse-logo-order features themselves are built and not in question. Note also lives at `Plugins/Nop.Plugin.CW.ThemeCore/Settings/CwThemeSettings.cs`.

## Polish backlog (Task 1 — CW.Standard)

- ✅ **Compare / wishlist card icons (done 2026-08-10).** Replaced the tiny 42×15 sprite PNGs with crisp **inline-SVG icons via a `::before` CSS mask** (compare double-arrow + heart), dark-on-white by default, hover fills with `--cws-primary` + white icon + a subtle lift. CSS-only (no `_ProductBox` markup change); the `::before` approach also sidesteps the global `.button-2 { background:#fff !important }` shorthand. The old `compare-button.png` / `wishlist-button.png` are now unused by `CW.Standard` (still referenced by the base DefaultClean CSS; safe to leave). In `cw-brand.css`.

## Guided Brand Designer MVP — status + preview/point-and-click backlog (2026-08-25)

**Built this session** (in `CW.ThemeCore` → `Views/Configure.cshtml` + `Services/BrandDesigner/`). A safe, sandbox-scoped slice of the feasibility roadmap (Phase 3a G1 + a preview-only taste of 3c point-and-click), **not** the full G3 editor:

- **Prompt → style** — deterministic mock generator (`CwBrandDesignerService`, no external AI) returns a `BrandStyle` = 6 accent colours **+ button radius**. Reads a prompt as *base vibe → modifiers* (intensity / lightness / warmth / button shape); tweak-only prompts layer on the current form values.
- **Vibe chips, "🎲 Surprise me", Reset**, palette **swatches**, and a **live mini-storefront preview** (`#cw-theme-preview`) that recolours/reshapes from the `--cw-*` tokens.
- **Point-and-click element editor** — an **"Edit elements"** toggle; each preview element carries `data-cw-style="<region>"`; clicking opens an **inline popover** with that region's controls **plus an "ask AI to tweak this" box** that sends the selected `region` so the tweak is **scoped to that token**. All controls write to the same curated `--cw-*` tokens/fields — **never an arbitrary selector**, so no upgrade-fragility.
- **Persistence** — button radius is a real per-store setting (`CwThemeSettings.ButtonRadius` → `--cw-btn-radius`, consumed by `cw-brand.css` `--cws-radius-sm`); Save persists via the existing overridable-per-store pipeline.

**AI assistant scope — two output channels (clarified 2026-08-26).** The assistant is intended to produce output at **two levels**, not one:
1. **Brand settings** — the structured `BrandStyle` (curated `--cw-*` tokens + button radius). This is what the deterministic mock does today.
2. **Generated Custom CSS** — free-form CSS written into the existing **Custom CSS** field (Capability 2), for looks the curated dials don't cover. Routed through the existing `ICwCssSanitizer` + per-store generated-file pipeline, so it stays **admin-reviewable, sanitised and versioned** — the reviewable "escape hatch" that avoids the arbitrary-selector fragility. This channel arrives with the **real AI** (the `GenerateBrand` endpoint / `ICwBrandDesignerService` would gain a CSS-returning path; the mock does not attempt CSS). Matches the client's original ask in [`CW_Theme_AI_CSS_Editor_Feasibility_And_Estimate.md`](CW_Theme_AI_CSS_Editor_Feasibility_And_Estimate.md) §1(B). Client-facing explainer artifact covers this framing.

**Backlog — theme-preview & point-and-click improvements (requested 2026-08-25):**

1. ✅ **Higher-fidelity preview + view selector (done 2026-08-25).** Rebuilt the preview as a **multi-view mock** with a `<select>` (Home / Category listing / Product):
   - **Home** — faithful to the real storefront: tinted top bar (account links + "Go to cart"), header (CITYWEB logo + search bar + second logo), nav menu, "Welcome to our store" + intro, **Featured Categories** image cards with "View all", community poll + VOTE, footer.
   - **Category listing** & **Product** — lighter but recognisable mocks (sidebar + subcategory cards + product grid; product gallery + attributes + price + add-to-cart + wishlist/compare/email), reusing the shared card/button/badge styles.
   - Region hook vocabulary generalised to be page-agnostic: `header, nav, link, button, tint, danger, success, warning, dark` (JS `regionConfig` + C# `RegionToTarget`, both mapping to the curated `--cw-*` tokens; `button` also carries radius). Point-and-click + region-scoped AI editing work in every view; switching views closes any open popover.
   - **Mocks follow the real CW.Standard / core templates** (structure + class semantics), verified 2026-08-25 against: Home → `Themes/CW.Standard/Views/Shared/_Header.cshtml` + `.../Components/HomepageCategories/Default.cshtml`; Category → core `Views/Catalog/CategoryTemplate.ProductsInGridOrLines.cshtml` + `_CatalogSelectors.cshtml` + `Views/Shared/_ProductBox.cshtml`; Product → core `Views/Product/ProductTemplate.Simple.cshtml`. They remain **sandbox markup** (own `.cw-*` classes) but reproduce the real page anatomy. When those templates change, re-sync the mocks.
2. ✅ **Checkout & Shopping-cart views (done 2026-08-25; corrected against live screenshots).** Added `brand-preview/cart.html` and `brand-preview/checkout.html`, styled in `preview.css`, declared in `cw-theme.json`. **Theme-only change — no plugin edit** (the framework loops over declared views). Picker now covers Home / Category / Product / Cart / Checkout. First pass mis-guessed the structure; corrected to match the real pages: cart = SKU/Image/Product/Price/Qty/Total/Remove table + the Continue-shopping / Estimate-shipping / Gift-wrapping bar + left-stacked Discount/Gift-card boxes + full totals panel (Tax, terms, CHECKOUT + REQUEST A QUOTE); checkout = full-width **accordion step bars** (6 steps, blue active bar, navy collapsed) rather than pill progress.
   - **Diagnosis finding:** the mismatch was the *mock*, not theme application — the live pages ARE correctly CW.Standard-themed.
   - ✅ **Theme-polish gap fixed (2026-08-25).** The cart **"Estimate shipping"** button and the checkout **active-step bar** shipped in base DefaultClean **`--accent-blue-color` (#4ab2f1)**, ignoring the CW primary. `cw-brand.css` now overrides `.shopping-cart-page .common-buttons .estimate-shipping-button` and `.opc .allow .step-title` (+ its `.number`) to `--cws-primary`/`--cws-primary-dark`. Preview mocks flipped to `--cw-primary` to match (Estimate-shipping button now hooked as `button`; active step bar recolours with the brand).
   - ✅ **Blue-accent audit + batch (2026-08-25).** Swept all 141 `--accent-blue-*` usages in base `styles.css`. Nearly all already branded (every `.button-1`/`.button-2`/named button via `!important`; `.pager` current page; poll VOTE = `.button-2` outline; links via `a{color}`). Fixed the remaining B2B-visible fills in `cw-brand.css`: `.mini-shopping-cart button[type="button"]` (flyout checkout), `.eu-cookie-bar-notification button` (cookie accept), `.menu__toggle` (mobile hamburger). **Dead for this theme (no fix):** base `.footer-block`/`.footer-menu__toggle` (CW.Standard ships custom `.cw-footer`). **Left as edge/rare:** ui-datepicker, estimate-shipping popup internals, order-details print/actions, swiper active dot, compare-clear, attribute *squares* (store uses radio lists), and blog/news/forum (Wave 6, out of scope).

**Decision (2026-08-25) — per-theme mock sets, capability-gated.** The preview + element picker is a **CW-theme feature**, gated on a *capability* (not a hardcoded theme name):
- **CW.ThemeCore owns the framework** — token fields/generator/swatches, the view selector, the point-and-click picker/popover JS+CSS, detection, and a **generic non-editable fallback** preview.
- **Each CW theme owns its preview mock** — per-view HTML fragments (`home`/`category`/`product`/…) with `data-cw-style` hooks + a `preview.css`, shipped in the theme folder (`~/Themes/{theme}/brand-preview/`) and declared in the theme's `cw-theme.json` (`brandPreview` node). The hooks live in the theme (which owns its markup), keeping point-and-click curated and upgrade-safe.
- **Detection:** the controller resolves the active theme (`ResolveThemeForScopeAsync` → `StoreInformationSettings.DefaultStoreTheme`); an `IThemeBrandPreviewService` reads the theme's `brandPreview` declaration + fragment/CSS files. Present → accurate preview + picker enabled. Absent (e.g. nop **DefaultClean**) → **generic preview, picker disabled**, with a note.
- **Token nuance:** `--cw-*` dials only affect a theme that consumes them, so on a non-CW theme they're inert (picker disabled is honest). **Custom CSS (Capability 2) still applies to any theme.**
- **Region-hook vocabulary is the framework↔theme contract:** `header, nav, link, button, tint, danger, success, warning, dark` (`button` also carries radius). CW themes annotate with these.
- Implemented 2026-08-25: CW.Standard's mock migrated out of `Configure.cshtml` into `Themes/CW.Standard/brand-preview/` + `cw-theme.json`.

> **Constraint (current mock):** every preview view is **CW-owned sandbox markup**, not real nop DOM — preserving the no-arbitrary-selector, no-upgrade-fragility guarantee. See the **live/real preview** track below, which the client has since requested as the eventual replacement.

**Live / real preview track (client requested).** The client wants the preview to be the **real storefront** (not a mock) and to reflect **layout selections** live. **Cityweb runs one nop install serving each store on its own domain (multi-domain)**, so the preview must work **cross-origin** — designed in via a **signed URL preview token** (Data Protection) that authorises "preview mode" on any store domain, a **preview-scoped `frame-ancestors` header** limited to the admin origin, and **origin-pinned `postMessage`**. Approach: an `<iframe>` of the real storefront + a bridge that (a) sets draft `--cw-*` vars on `:root` and (b) toggles the layout CSS class on `<html>` (layouts are already CSS-class switches — the easy half). Decisions taken: **keep click-to-edit** (theme tags real elements / preview-only selector→region map; persisted changes stay token-only), **all five pages live** (Cart/Checkout via an admin-only preview route with dummy cart data). Rough effort ≈ **80–130 hrs** (senior dev, no AI). **Supersedes the mock for CW themes** (mock → non-CW / offline fallback). Key risks: framing header, Data-Protection key-ring sharing across app instances, cart/checkout dummy data. **Phase 1 DEPLOYED & WORKING (2026-09-14):** [`CW_Theme_Live_Preview_Phase1_Spec.md`](CW_Theme_Live_Preview_Phase1_Spec.md) — token service + framing middleware + bridge + head injection + admin side (iframe, token minting, representative Category/Product URLs, layout-class map, origin-pinned postMessage) for Home/Category/Product. Verified end-to-end after fixing two deploy bugs (middleware Order 3000 ran *after* `UseEndpoints` → moved to a dedicated startup at Order 550; the bridge config `<script>` was a literal tag that `NopScriptTagHelper` relocated to the footer → now emitted via `Html.Raw`). Polish added: **desktop-width iframe scaled to fit** (previews the desktop layout, not the narrow-iframe tablet view), and **"Edit elements" shows the mock while active** (live click-to-edit is Phase 2). Dev caveat: All-stores / test store scope only (shared `Store.Url`). Phase 2 = click-to-edit on the frame (**built and committed** — theme-owned selector→region map + `data-cw-style` tags, bridge edit-mode/selection relay, admin popover via scaled-iframe coordinate transform; see [`CW_Theme_Live_Preview_Phase2_Spec.md`](CW_Theme_Live_Preview_Phase2_Spec.md)). Phase 3 = live Cart/Checkout via a **per-store dummy "preview customer"** (Guests-role system account impersonated for the token-bearing request; GET-only; hidden from the admin customer list by its role; checkout rendered whatever the store's checkout settings) — **built and committed (176d965, 2026-09-23)**, see [`CW_Theme_Live_Preview_Phase3_Spec.md`](CW_Theme_Live_Preview_Phase3_Spec.md). **Code review 2026-10-05:** nine fixes (customer-cookie leak, dummy-cart refresh, duplicate-customer lock, preview-token renewal, config div per CLAUDE.md §9.14, and smaller clean-ups) — **tested 2026-10-08, all checks pass (Phase 3 spec §9.1), committed (5790df8)**; one finding **accepted, not fixed**: the page's own AJAX/posts carry no token and are guarded client-side only (Phase 3 spec §4.1). ThemeCore's nop-upgrade re-check items: [`Plugins/Nop.Plugin.CW.ThemeCore/CW_ThemeCore_Nop_Upgrade_Checklist.md`](../Plugins/Nop.Plugin.CW.ThemeCore/CW_ThemeCore_Nop_Upgrade_Checklist.md). **Not in production yet; target before go-live (end of year).**

**Client demo feedback (2026-09-13) — disposition.**
- ✅ **Configure page layout (settled by user 2026-09-13).** My "layout beside preview, palette below" reorg was rolled back; **the user then reorganised it themselves**: the "Choose Layout" dropdowns now live in the **designer-row left column, under the colour palette** (inside `#cw-theme-settings`), with the **preview on the right**; logos card no longer holds them. Verified nothing else was dropped (div balance + full feature set intact). Vibe-chip colouring kept.
- ✅ **(doing now) Brand the Swiper accents** — the NOP **Widgets.Swiper** slider (replaced the old Nivo/Niro slider) works fine with the token theme (CSS-only; no JS conflict), but Swiper's own pagination dots / arrows use base `--accent-blue`. Brand `.swiper-pagination-bullet-active` (+ nav) to `--cws-primary` in `cw-brand.css` (the one edge item left from the 2026-08-25 blue-accent audit).
- ✅ **Colour the vibe chips with each preset's primary (done 2026-09-13).** Client clarified with an image: "the styles buttons" = the **Brand-designer vibe chips** (Premium jewellery / Warm boutique / …), *not* storefront buttons. Each chip is now **filled with its own preset's primary colour** so the user sees the style at a glance. `CwThemeSettingsModel.BrandPresetNames` (list of names) → `BrandPresets` (`BrandPresetChipModel` = Name + PrimaryColor); controller populates from `GetPresets()`; chip loop drops `btn-outline-secondary` and sets inline `background/border-color` = preset primary, white text + brightness hover. Surprise-me / Reset stay outline (they're actions, not styles).
- ✅ **Show the selected logos in the preview (done 2026-09-14).** **Decision: "mirror the live store"** — the preview shows exactly what the storefront renders: **primary logo = nop core `StoreInformationSettings.LogoPictureId`** (what the CW.Standard header actually uses via the core `LogoViewComponent`), **second logo = `CwThemeSettings.SecondLogoPictureId`**. The **live frame already shows these** (it's the real header); the gap was the **home mock** header (only mock with a header — category/product have none), which showed placeholder text. Implemented: controller `PrepareLogoPreviewAsync` resolves both URLs via `IPictureService` → new model `PreviewPrimaryLogoUrl`/`PreviewSecondLogoUrl`; `home.html` header spans got `data-cw-logo="primary|second"` hooks; `Configure.cshtml` JS injects `<img class="cw-hp-logo-img">` into the slots on load (honours `ReverseLogos`); `preview.css` sizes the img. Empty logo → placeholder text kept. **Finding (flagged, not fixed):** this page's own `CwThemeSettings.LogoPictureId` field is **unused by the storefront** (the header uses the core setting) — the "decide later" overlap in `CwThemeSettings.cs`; left as a separate cleanup. **Deferred:** draft live-swap on picker change — nop's Picture editor emits no clean change event, and on the live frame a logo swap is DOM mutation (Phase 2 bridge territory); "mirror" reflects **saved** state. Needs rebuild + restart (C# change: new `IPictureService` dep + model props + controller method).
- ✅ **Layout section controls the preview (done with the live preview, 8cecd7b).** Picking a layout card changes the hidden `<select>`, whose `change` posts the draft to the frame; the bridge swaps the section's layout class on `<html>` (`buildLayout` in `Configure.cshtml` → `apply` in `cw-preview-bridge.js`). **Live preview only:** the *mock* (non-CW themes / live-preview fallback) still has no per-layout variants — not planned unless the mock needs it.
- ✅ **Wireframe layout pickers + full example on select (done 2026-09-14).** The layout `<select>`s are replaced with **wireframe cards** — a mini SVG page-diagram per layout option; clicking one selects it and updates the live preview (the "full example"). **Design:** the native `<select>` is kept but **`cw-sr-only`** (visually hidden) so it stays the source of truth for model binding **and** nop's per-store override enable/disable (`multi-store-override-option` toggles its `disabled` attr, mirrored onto the cards via a `MutationObserver`); the cards write to the select and dispatch its `change`, which drives `postDraftToFrame`. **Wireframes are manifest-driven:** `ThemeLayoutOption.Wireframe` token in `cw-theme.json` (`full`/`boxed`/`grid-2`/`grid-3`/`split`/`stack`/`narrow`, generic fallback) → `LayoutOptionModel` → a `@functions LayoutWireframe(token)` SVG switch in `Configure.cshtml`, so a theme declares its own layout diagrams. `LayoutSectionModel.AvailableLayouts` (SelectListItem) replaced by `Options` (Key/Title/Wireframe). Needs rebuild + restart (C# + manifest cache).

**Brand Designer — pending enhancements (added 2026-09-17, from the "where's the AI output?" discussion).**
> ▶ **NEXT TASK (set 2026-10-08): connect the Brand Designer to a real AI and design its assistance with themes.**
> Tier 2 — spec first. The real AI replaces the deterministic mock behind `ICwBrandDesignerService`; scope = the two
> output channels above, plus the two items below.
> **Spec drafted 2026-10-08: [`CW_Theme_AI_Assistant_Spec.md`](CW_Theme_AI_Assistant_Spec.md)** (Phase A tokens, Phase B
> CSS). Review corrections applied 2026-10-08; awaiting approval. **No code until the
> live-preview review fixes are tested and committed** (Phase 3 spec §9.1) — they touch the same files.
- 📋 **(to plan) AI also generates Custom CSS.** Client reaffirmed (2026-09-17): the assistant should write styles into the **Custom CSS** field (Capability 2) too, for looks the curated `--cw-*` dials don't cover. This is **channel 2** already scoped above ("AI assistant scope — two output channels") — it arrives with the **real-AI** connection: `GenerateBrand` / `ICwBrandDesignerService` gains a CSS-returning path routed through the existing `ICwCssSanitizer` + versioned per-store file (stays sanitised, reviewable, versioned). When wired, a Generate result fills the token fields, and any CSS goes to a **separate read-only review pane** (Preview / Append to Custom CSS / Discard) — the AI never writes the Custom CSS field itself; the admin appends and saves (decision D3, 2026-10-08, AI spec §6.2). The current mock returns tokens only, so this waits on the real model. Cross-ref [`CW_Theme_AI_CSS_Editor_Feasibility_And_Estimate.md`](CW_Theme_AI_CSS_Editor_Feasibility_And_Estimate.md) §1(B).
- 📋 **(to plan) "What changed" indicator after Generate.** The AI's output is applied straight into the settings + preview (no text reply). After Generate / a vibe chip / an inline AI tweak, briefly **highlight the fields that changed** — the colour swatches + hex inputs and the button-radius knob (and, once channel 2 lands, the Custom CSS box) — so it's obvious where the output landed. Small admin-UI change: a transient highlight class applied in `Configure.cshtml`'s `applyPalette` to only the fields whose value actually changed, fading after a moment. No storefront impact.

---

# Theme view-override backlog (parity with Voyage)

**Source:** every `.cshtml` under `Presentation/Nop.Web/Themes/Voyage/Views` is a view Voyage overrides — **76 files**. All map to **core** `Nop.Web/Views` (even the filter-level-value and mega-menu grid/list files are core in nop 4.9), so each can be cleanly re-versioned in `CW.Standard`. `_ViewImports.cshtml` is theme infra (already have ours) → **75 design views**.

**Guiding principle — CSS-first, version only for markup.** Voyage ships a `.cshtml` whenever SevenSpikes changed *markup*; much of our look is already carried by `cw-brand.css` (CSS only). **Version a view only when the change needs structure CSS can't reach** (added/re-ordered elements, new wrappers, conditional blocks). For each item below, first ask "can CSS do it?" — if yes, don't copy the view. When we do version one, we copy the **core** view (never Voyage's licensed markup) into `CW.Standard/Views/...` and restyle.

**Legend:** ✅ done · ⬜ candidate · 🔶 tied to a deferred interactive feature (MegaMenu / AjaxFilters / InstantSearch) — restyle-able now, but full parity waits on that feature.

### Wave 0 — already versioned
- ✅ `Shared/Head.cshtml`
- ✅ `Shared/Components/HeaderLinks/Default.cshtml`
- ✅ `Shared/Components/HomepageCategories/Default.cshtml`

### Wave 1 — global shell (highest impact: every page)
- ⬜ `Shared/_Root.cshtml`
- ⬜ `Shared/_Root.Head.cshtml`
- ✅ `Shared/_Header.cshtml` — width-constrained two-tier frame (slim utility bar: selectors left / links right · main row: logo + prominent search); menu stays below (via _Root), flyout cart kept in upper bar so its hover script works
- ⬜ `Shared/_ColumnsTwo.cshtml`
- ⬜ `Shared/_CurrentPageClass.cshtml`
- ✅ `Shared/Components/Logo/Default.cshtml` — prefers per-store `TigThemeSettings.LogoPictureId` (read by setting key, decoupled), falls back to `Model.LogoPath`
- ✅ `Shared/Components/Footer/Default.cshtml` — three-tier layout (dark newsletter band · columns + Selected offers · compact legal bar)
- ⬜ `Shared/Components/FooterMenu/Default.cshtml`
- ⬜ `Shared/Components/FooterMenu/_FooterItem.cshtml`
- ⬜ `Shared/Components/MainMenu/Default.cshtml`
- ⬜ `Shared/Components/MainMenu/_MenuItem.cshtml`
- 🔶 `Shared/Components/MainMenu/_MenuItem.Grid.cshtml`  *(mega-menu grid layout)*
- 🔶 `Shared/Components/MainMenu/_MenuItem.List.cshtml`  *(mega-menu list layout)*
- ⬜ `Shared/Components/CategoryNavigation/Default.cshtml`
- ⬜ `Shared/Components/FlyoutShoppingCart/Default.cshtml`
- ✅ `Shared/Components/NewsletterBox/Default.cshtml` — subtitle line + `.newsletter-form` (connected input/button); core subscribe AJAX preserved
- ✅ `Shared/Components/SocialButtons/Default.cshtml` — inline-SVG icons per network (accessible hidden labels); FontAwesome not loaded on storefront
- ⬜ `Shared/Components/TopicBlock/Default.cshtml`
- ⬜ `Shared/_ProductBox.cshtml`  *(shared product card — used by home/catalog/related)*

### Wave 2 — catalog (category / manufacturer / vendor / filters)
- ⬜ `Catalog/CategoryTemplate.ProductsInGridOrLines.cshtml`
- ⬜ `Catalog/ManufacturerAll.cshtml`
- ⬜ `Catalog/VendorAll.cshtml`
- 🔶 `Catalog/_CatalogFilters.cshtml`  *(AjaxFilters)*
- 🔶 `Catalog/SearchByFilterLevelValues.cshtml`  *(AjaxFilters)*
- 🔶 `Shared/Components/CompatibleWithFilterLevelValues/Default.cshtml`  *(AjaxFilters)*
- 🔶 `Shared/Components/FilterLevelValueSearch/Default.cshtml`  *(AjaxFilters)*

### Wave 3 — product details
- ⬜ `Product/ProductTemplate.Simple.cshtml`
- ⬜ `Product/ProductTemplate.Grouped.cshtml`
- ⬜ `Product/_ProductDetailsPictures.cshtml`
- ⬜ `Product/_ProductDetailsVideos.cshtml`
- ⬜ `Product/_ProductPrice.cshtml`
- ⬜ `Product/_ProductTierPrices.cshtml`
- ⬜ `Product/_AddToCart.cshtml`
- ⬜ `Product/_AddToWishlist.cshtml`
- ⬜ `Product/_ProductReviewOverview.cshtml`
- ⬜ `Product/_ProductReviews.cshtml`
- ⬜ `Product/_ProductEmailAFriendButton.cshtml`
- ⬜ `Product/_CompareProductsButton.cshtml`
- ⬜ `Product/CompareProducts.cshtml`
- ⬜ `Shared/_DiscountBox.cshtml`
- ⬜ `Shared/_AddWishlistModal.cshtml`
- ⬜ `Shared/_MoveToWishlistModal.cshtml`

### Wave 4 — cart / wishlist / checkout
- ⬜ `Shared/Components/OrderSummary/Default.cshtml`
- ⬜ `ShoppingCart/Wishlist.cshtml`
- ⬜ `ShoppingCart/_GiftCardBox.cshtml`
- ⬜ `Checkout/BillingAddress.cshtml`
- ⬜ `Checkout/ShippingAddress.cshtml`
- ⬜ `Checkout/Confirm.cshtml`
- ⬜ `Shared/_OrderReviewData.cshtml`
- ⬜ `Shared/_OrderCustomValues.cshtml`

### Wave 5 — account / orders / returns
- ⬜ `Customer/Info.cshtml`
- ⬜ `Customer/Login.cshtml`
- ⬜ `Customer/Register.cshtml`
- ⬜ `Customer/Addresses.cshtml`
- ⬜ `Customer/ChangePassword.cshtml`
- ⬜ `Customer/DownloadableProducts.cshtml`
- ⬜ `Customer/MultiFactorAuthentication.cshtml`
- ⬜ `Customer/UserAgreement.cshtml`
- ⬜ `Order/CustomerOrders.cshtml`
- ⬜ `Order/Details.cshtml`
- ⬜ `Order/ShipmentDetails.cshtml`
- ⬜ `Order/CustomerRecurringPayments.cshtml`
- ⬜ `Order/CustomerRewardPoints.cshtml`
- ⬜ `ReturnRequest/CustomerReturnRequests.cshtml`
- ⬜ `ReturnRequest/ReturnRequest.cshtml`
- ⬜ `BackInStockSubscription/CustomerSubscriptions.cshtml`

### Wave 6 — content (blog / news / forum) — ❌ OUT OF SCOPE (decided 2026-08-10)
> B2B store: blog / news / forum are not styled. Descoped from Task 1.
- ⬜ `Blog/List.cshtml`
- ⬜ `Blog/BlogPost.cshtml`
- ⬜ `News/NewsItem.cshtml`
- ⬜ `Boards/CustomerForumSubscriptions.cshtml`
- ⬜ `Boards/_MarkdownEditor.cshtml`

**Totals:** 76 Voyage files · 1 infra (skip) · **75 design views** · 3 done · **72 remaining** (of which 6 🔶 are gated on deferred interactive features). Each versioned view must pass the responsive check (see *Cross-cutting design requirements*).
