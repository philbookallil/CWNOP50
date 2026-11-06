# CW.ThemeCore, CW.Theme.Standard and the CW.Standard theme — port to nopCommerce 5.00

**Status:** applied 2026-10-29, approved 2026-11-06. What survives is in
`Plugins/Nop.Plugin.CW.ThemeCore/References/ThemeCore_Plugin_Reference.md`.
Q1 answered: "Go to cart" became a Theme.Standard resource. Tier 2 (`CLAUDE.md` §0.3).
**Source:** the 4.90 source (`CLAUDE.local.md`): `Plugins/Nop.Plugin.CW.ThemeCore`, `Plugins/Nop.Plugin.CW.Theme.Standard`,
`Presentation/Nop.Web/Themes/CW.Standard`.

## 1. Scope

| Item | In this pass | Notes |
|---|---|---|
| `Nop.Plugin.CW.ThemeCore` (framework: accent colours, custom CSS, layout system, Brand Designer, live preview) | Yes | 58 files |
| `Nop.Plugin.CW.Theme.Standard` (nav-bar and corner settings for `CW.Standard`) | Yes | Framework→extension `<ProjectReference>` to ThemeCore (§10.2 exception), unchanged |
| `Themes/CW.Standard` | Yes | 11 views, CSS, images, `brand-preview/`, `cw-theme.json` |
| `Themes/CW.StandardTest` | **No — deferred** | Built on Voyage (paid third-party: `voyage.js`, `voyage.*` fonts, Voyage views); the repository is public (§0.9). A reminder row goes in `CLAUDE.md` §3.2. Four of its views have no 5.00 counterpart (`Boards/*`, `News/NewsItem`, `Shared/_CurrentPageClass`) |
| `Themes/Voyage` | No | Already gitignored |

## 2. Plugins — §10.5 checklist

| # | Change | ThemeCore | Theme.Standard |
|---|---|---|---|
| 1 | `net10.0`; `SupportedVersions: ["5.00"]` | csproj, `plugin.json` | same |
| 2 | `IWebHelper` → `Nop.Services.Helpers` | `CwThemeCorePlugin.cs` (+ any other hit the build reports) | `StandardThemePlugin.cs` |
| 3–4 | Mapster / `CreateTableIfNotExists` | No profile, no migrations — nothing to do | — |
| 5 | Views compile at runtime | Grep `Configure.cshtml`, both component views, `_ViewImports` for moved types; load each page | `Configure.cshtml` |
| 6 | PascalCase JSON | Check the Brand Designer / preview AJAX handlers in `Configure.cshtml` against the actions | — |
| 8 | Version bump | `plugin.json` `Version` → `2026_10_29_DATA_1.3` (UpdateAsync re-installs locale resources) | `2026_10_29_DATA_1.1` |
| 10 | `IActionContextAccessor` / `EngineContext` | Grep; none found in the scan | — |
| 11 | `DependsOnSystemNames` | — | Already `["CW.ThemeCore"]` |
| 13 | `NopHtml.*Parts` take `~/` | The head component emits `<link>`/`<script>` through `Html.Raw` with `Url.Content` (deliberate, see the checklist doc); not a `NopHtml` call — keep | — |

Also: add both projects to `NopCommerce.sln` (ThemeCore before Theme.Standard); move `CW_ThemeCore_Nop_Upgrade_Checklist.md`
and `CW_Theme_DesignSwap_Guide.md` into `References/`, and write `References/ThemeCore_Plugin_Reference.md` (what the port
changed, §10.5 closing line).

### 2.1 nop internals ThemeCore relies on — checked against 5.00

| Assumption (`CW_ThemeCore_Nop_Upgrade_Checklist.md`) | 5.00 | Result |
|---|---|---|
| Startup orders: routing 400, authentication 500, endpoints 900; framing middleware at 550 | Routing 400, authentication 500, **authorization 600**, MVC 1000 (`NopMvcStartup`); `NopEndpoints` order to be confirmed | 550 still sits after authentication and before authorization/endpoints — confirm endpoints during build |
| `WebWorkContext.SetCurrentCustomerAsync` writes `.Nop.Customer` through `Response.Cookies` immediately | `SetCustomerCookie` → `Response.Cookies.Append` (`WebWorkContext.cs:133`), called from line 280 | Unchanged |
| `CheckoutController.OnePageCheckout` tail: cart → `PrepareOnePageCheckoutModelAsync(cart)` → `View(model)`; early returns are redirects / `Challenge` | Same tail (`CheckoutController.cs:1514`); early returns are `CART` / `CHECKOUT` redirects and `Challenge()` | Unchanged — re-diff the filter's copy line by line |
| Preview customer kept out of the guest purge (system account) | `DeleteGuestCustomersAsync` still filters by the Guests role; system-account skip to be confirmed | Confirm during build |
| AI client uses `HttpClient` because STJ 10 could not load on .NET 9 | 5.00 runs on .NET 10 | The class is not in the 4.9 tree; nothing to do. Note it in the reference doc |

## 3. CW.Standard theme — rebased on 5.00

Rule: start from the 5.00 file (DefaultClean theme file if it exists, else `Nop.Web/Views/...`) and re-apply CW's
change. Where 5.00 changed markup CW's change touches, the CW change is re-designed, not pasted.

| File | CW change (4.9) | nop 5.00 change | Port |
|---|---|---|---|
| `Views/_ViewImports.cshtml` | `@inject INopUrlHelper NopUrl`; extra usings (`NopLinksDefaults`, `Nop.Core.Http`, `Nop.Core.Domain.Customers`, `Nop.Web.Models.Menus`) | Removed `Nop.Web.Models.Boards`, `.News`, `.Polls` | 5.00 file + CW additions; verify `INopUrlHelper` still exists |
| `Views/Shared/Head.cshtml` | `cw-brand.css` after `styles.css`; Theme.Standard settings read by key → `:root` overrides | `Nop.Services.Helpers` → `Nop.Services.Themes`; `IUserAgentHelper` removed | 5.00 file + CW block. The inline `<style>` stays (per-store computed values, not a static asset) |
| `Views/Customer/Login.cshtml` | "Returning customer" title moved out of the `<form>`, `fieldset` class dropped | Email/phone login mode switcher (`data-mode`, `LoginByPhone`, mode buttons, script) | 5.00 file + the title move; the mode switcher kept intact; `cw-brand.css` checked for the new `.mode-switcher` |
| `Views/Shared/Components/HeaderLinks/Default.cshtml` | Cart link as a "Go to cart" button (`cw-cart-item`, `cw-cart-button`) | `IWebHelper` namespace | 5.00 file + CW change. **"Go to cart" is hard-coded** — replace with a `Plugins.CW.ThemeCore`-style resource? See question Q1 |
| `Views/Shared/Components/SocialButtons/Default.cshtml` | Inline-SVG icon per network + `sr-only` label | Twitter → **X** (`XLink`, `Footer.FollowUs.X`); **RSS removed** (News left core); **TikTok, Snapchat, Pinterest, Tumblr** added | 5.00 conditions; X icon replaces Twitter's; RSS dropped; four new original SVG icons; CSS classes `.x`, `.tiktok`, `.snapchat`, `.pinterest`, `.tumblr` |
| `Views/Shared/Components/OrderSummary/Default.cshtml` | `<div class="cw-cart-attr">` around the checkout-attribute selector + selected text | **Cart rewritten**: the `<table class="cart">` is gone (vendor list, new item markup); attributes now `_CheckoutAttributes` partial + `SelectedCheckoutAttributesViewComponent` (line 75) | 5.00 file + the wrapper around lines 75–76. `cw-brand.css` cart rules (`.cart .product-picture`, `.product-subtotal`, `.remove-from-cart`, `.cart-total td…`, ~lines 652–702) re-targeted at the 5.00 markup |
| `Footer`, `HomepageCategories`, `Logo`, `NewsletterBox`, `_Header` | various | **none** (4.9 = 5.00) | Copy as is |
| `Content/css/styles.css` | 4.9 DefaultClean + 1 line (line 8375 `#f5f5f5`) | DefaultClean styles.css changed ~1,280 lines | 5.00 DefaultClean `styles.css` + the one-line change (located by selector) |
| `Content/css/styles.rtl.css`, `print.css` | identical to DefaultClean 4.9 | rtl changed ~1,300 lines; print unchanged | Take 5.00 DefaultClean's |
| `Content/images/` | DefaultClean 4.9 set + CW `logo.png` | 5.00 adds `up.png`, `down.png`; drops `edit_`, `move`, `remove_`, `reply`, `topic-type1–3`, `vote-*`, `watch` (forums/news/polls) | 5.00 DefaultClean set + CW-only files; drop images nothing references |
| `Content/css/cw-brand.css` | CW's brand layer (887 lines) | — | Copy; audit every selector against the 5.00 markup (cart, login, social, header links, footer); drop rules for removed features (forums, news, polls, RSS) |
| `brand-preview/*`, `cw-theme.json`, `theme.json`, `preview.jpg` | static mock pages + layout manifest | — | Copy; check `cw-theme.json` regions against the 5.00 views |

## 4. Housekeeping

- `CLAUDE.md` §3.2: ThemeCore and Theme.Standard rows → Ported; add a row **"`Themes/CW.StandardTest` — deferred: Voyage-based,
  decide licence / gitignore before porting"**. Fix the duplicate `CW.Freight` / `CW.Core` rows only if asked (out of scope).
- §10.4 known-collision note: `CW.Standard` still ships none of CW.Core's five forked views.
- Move the 4.90 theme plans that still describe live design (`CW_Theme_Build_Plans.md`, `CW_Theme_Layout_System_Plan.md`,
  live preview phase specs) — **not in this pass**; they stay in the 4.90 source.

## 5. Questions

- **Q1.** HeaderLinks hard-codes "Go to cart" (English). Move it to a locale resource now (`Plugins.CW.Theme.Standard.GoToCart`,
  installed by Theme.Standard), or keep as in 4.9?

## 6. Test plan

| # | Check | Pass when |
|---|---|---|
| T1 | `dotnet build NopCommerce.sln` | 0 errors; no new warnings from the two plugins |
| T2 | Install ThemeCore, then Theme.Standard (refused before ThemeCore) | Both install; locale keys render as text |
| T3 | ThemeCore Configure page: accent colours, custom CSS save, layout options, Brand Designer generate | Saves persist; `custom.css` written and served; no console errors |
| T4 | Live preview (home, category, product, cart, checkout) | Preview renders in the iframe; `.Nop.Customer` cookie never set to the preview customer |
| T5 | Set store theme to `CW.Standard`; open home, category, product, cart, checkout, login, my account | Every page renders; no Razor errors; console clean |
| T6 | Cart: checkout attribute box (`cw-cart-attr`), line items, totals | Styled as in 4.9 on the 5.00 markup |
| T7 | Login: email ↔ phone mode switcher | Both modes work; titles of both columns aligned |
| T8 | Footer social icons with X, TikTok, Snapchat, Pinterest, Tumblr links set | Each shows its icon; empty links hidden |
| T9 | Theme.Standard: light nav bar, square corners on and off | `:root` overrides appear/disappear per store |
| T10 | Uninstall Theme.Standard | Theme falls back to defaults (settings read by key) |
| T11 | Feature-off: n/a (ThemeCore reads no CW.Core feature switch) | — |
