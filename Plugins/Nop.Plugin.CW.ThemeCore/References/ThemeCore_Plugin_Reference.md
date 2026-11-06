# CW.ThemeCore, CW.Theme.Standard and the CW.Standard theme — reference

| Piece | What it is | SystemName / folder | Version |
|---|---|---|---|
| `Nop.Plugin.CW.ThemeCore` | The theme framework: per-store accent colours (CSS custom properties), custom CSS, layout options per storefront section, logos, the Brand Designer and its live preview | `CW.ThemeCore` | `2026_11_02_DATA_1.4` |
| `Nop.Plugin.CW.Theme.Standard` | Settings of the `CW.Standard` theme (light nav bar, square corners) and the theme's own storefront text | `CW.Theme.Standard`, depends on `CW.ThemeCore` | `2026_10_29_DATA_1.1` |
| `Themes/CW.Standard` | The storefront theme | `Presentation/Nop.Web/Themes/CW.Standard` | — |

Ported from 4.90 on 2026-10-29 (`CWPlans/Deprecated/ThemeCore_Port_Spec.md`). Two companion documents sit beside this one:
`CW_Theme_DesignSwap_Guide.md` (how to reskin, how to add a theme) and `CW_ThemeCore_Nop_Upgrade_Checklist.md` (the nop
internals ThemeCore relies on — **run it on every nopCommerce upgrade**).

## 1. How the pieces couple

- **Theme.Standard → ThemeCore:** a `<ProjectReference>` with `Private=false` / `ExcludeAssets=runtime` (`CLAUDE.md` §10.2
  framework exception); it inherits `BaseCwThemeAdminController`, `BaseCwThemeSettingsModel`, `BaseCwThemeSettings`. The
  build leaves a reference copy of ThemeCore in `Plugins/CW.Theme.Standard/refs/` — the same as 4.90, harmless.
- **Theme → plugins:** the theme reads settings **by key** (`standardthemesettings.lightnavbar`, `.squarecorners`,
  `cwthemesettings.logopictureid`, `cwthemesettings.reverselogos`), never by type, so it renders with defaults when
  a plugin is not installed. It renders ThemeCore's `CwSecondLogo` component by name.
- ThemeCore reads no CW.Core feature switch; nothing to check against `Feature_Toggles_Reference.md`.

## 2. What the 5.00 port changed (plugins)

| Change | Where |
|---|---|
| `net10.0`, `SupportedVersions: ["5.00"]`, both projects added to `NopCommerce.sln` | csproj, `plugin.json` |
| `IWebHelper` → `Nop.Services.Helpers` | both plugin classes |
| `IThemeContext` moved from `Nop.Web.Framework.Themes` to `Nop.Services.Themes` | `CwThemeHeadViewComponent`, `ThemeLayoutPageClassFilter` |
| `UpdateAsync` installs the resources only for a `DATA` version (`CLAUDE.md` §6.8) | `CwThemeCorePlugin`; Theme.Standard gained an `UpdateAsync` |
| `Plugins.CW.Theme.Standard.HeaderLinks.GoToCart` — the header's "Go to cart" was hard-coded English. The view reads it with `returnEmptyIfNotFound` and falls back to nop's own cart label, so the theme still renders without the plugin | `StandardThemePlugin`, theme `HeaderLinks` view |
| `StandardThemePlugin` is an `IMiscPlugin`, so `_ConfigurePlugin` gives its page the "Configure" heading and back link | `StandardThemePlugin` |
| The two documents moved into `References/` | — |

Nothing else needed changing: no Mapster profile, no migrations, no `IActionContextAccessor` or `EngineContext`; the
copied `OnePageCheckout` tail in `CwPreviewCheckoutFilter` is identical in 5.00.

## 3. What the 5.00 port changed (theme)

Every view started from its 5.00 counterpart (`DefaultClean` file if it has one, else `Nop.Web/Views`) with CW's change
put back on top. Each view's header comment states what CW changes.

| File | CW change | 5.00 change taken |
|---|---|---|
| `Views/_ViewImports.cshtml` | extra usings, `INopUrlHelper` | `Nop.Web.Models.Boards`, `.News`, `.Polls` removed (those features left core) |
| `Views/Shared/Head.cshtml` | `cw-brand.css` after `styles.css`; Theme.Standard overrides into `:root` | `@using Nop.Services.Themes` (for `IThemeContext`) |
| `Views/Customer/Login.cshtml` | "Returning customer" title outside the `<form>`, no `fieldset` class | email / phone login switcher; in the final 5.00 the phone field and its button render only when `LoginByPhone` is on (rebased again 2026-11-02) |
| `Views/Shared/Components/HeaderLinks/Default.cshtml` | cart link as a button | `IWebHelper` namespace |
| `Views/Shared/Components/SocialButtons/Default.cshtml` | inline SVG per network | Twitter → X; RSS gone; TikTok, Snapchat, Pinterest, Tumblr added (new original SVGs) |
| `Views/Shared/Components/OrderSummary/Default.cshtml` | `.cw-cart-attr` around the checkout attributes | the cart table moved to `_ShoppingCartItems`; vendor list |
| `Footer`, `HomepageCategories`, `Logo`, `NewsletterBox`, `_Header` | — | none (4.90 = 5.00) — copied |
| `Content/css/styles.css`, `.rtl.css`, `print.css` | one line: `.menu > .menu__item` background `#f5f5f5` (desktop media block, line 7528) | 5.00 `DefaultClean` files |
| `Content/images/` | — (4.90's set was `DefaultClean`'s) | 5.00 `DefaultClean` set (adds `up`/`down`, drops forum / news / poll icons) |
| `Content/css/cw-brand.css` | — | selectors checked against the 5.00 markup (cart, totals, checkout attributes, login, social, nav): all still match. Added: no empty `.cw-cart-attr` box when the store has no checkout attributes (present in 4.90 too) |

`brand-preview/home.html` keeps its poll mock: polls are now the `Misc.Polls` plugin, so a store may still show one.

**On a nopCommerce upgrade:** diff each of the six rebased views against its new nop counterpart (the header comment names
the CW change), take `DefaultClean`'s new CSS and images, re-apply the `styles.css` line, and re-check `cw-brand.css`
against any markup nop changed.

## 4. Tested (local site, 2026-10-29)

| # | Check | Result |
|---|---|---|
| T1 | Both projects build | 0 errors, 0 warnings from the plugins |
| T2 | Theme.Standard install refused before ThemeCore; both install | Pass |
| T3 | ThemeCore Configure page | Loads; no errors |
| T4 | Live preview: home, category, product, cart, checkout | All five render live (not the mock) with the preview bridge |
| T5 | `CW.Standard` storefront: home, login, register, product, category, cart, wishlist, one-page checkout, account pages, search, sitemap, contact | All 200, no Razor errors, no missing assets |
| T6 | Cart on the 5.00 markup | Pass |
| T7 | Login: titles aligned; email ↔ phone switch | Pass |
| T8 | Footer icons with all eight networks set | All render (test links removed afterwards) |
| T9 | Light nav bar / square corners on and off | `:root` overrides appear and go (reset to off afterwards) |
| T10 | Uninstall Theme.Standard, then reinstall | Uninstall removes its 7 resources and 2 settings; the theme renders every page with the defaults (no `:root` override, radius 8px) and the cart button falls back to nop's "Shopping cart". Reinstall restores both and "Go to cart" |
| T4b | Preview customer's identity never reaches the browser (curl with a browser user agent, preview token for `/cart` and `/onepagecheckout`) | Both previews render the preview customer's 3-line cart; no `Set-Cookie` carries its GUID — the browser gets a fresh empty guest, and a follow-up `/cart` with those cookies is empty |

nop 5.00 note for testing: a **pending uninstall is applied by "Reload list of plugins"** (`PluginController.ReloadList`),
not at startup; a pending install is applied at startup. With curl's default user agent nop answers as its built-in
`SearchEngine` account, so cookie checks need a browser user agent.

**Re-run in this repository on the final 5.00 (2026-11-02).** The work was brought across from the beta checkout; every
nop file it rests on was compared, and only `Views/Customer/Login.cshtml` had changed (§3). Both plugins installed in
order; both Configure pages load with their headings; live preview renders all five pages live, the cart with the
preview customer's three lines; the storefront pages render on CW.Standard with no errors and no missing assets; Login
keeps both titles aligned and, with phone sign-in off, shows no phone field or button. Installing ThemeCore saved its
default settings over the values left from the earlier run in the shared database.

## 4a. Unit tests

`Tests/CW.Tests/CW.ThemeCore/` — 137 cases, no database or nop engine:

| File | Covers |
|---|---|
| `CwCssSanitizerTests` | always-blocked constructs, the three url() policies, warnings, the size cap; URLs and keywords written with CSS escapes, `)` or escaped quotes in quoted URLs, backslash and other-scheme URLs, `image-set()` / `image()`; escapes that must survive (icon-font code points, escaped digits and colons in selectors) |
| `CwThemeCssServiceTests` | the `:root` block: only 3–8 hex digits are written, the accent-colour switch, the button radius |
| `CwPreviewTokenServiceTests` | round trip, other store, expired, tampered, foreign key ring, other purpose, garbage |
| `CwPreviewFramingMiddlewareTests` | `IsSafeOrigin` (no header or CSP injection) and `IsCartOrCheckout` (both `internal` for the tests) |
| `ThemeLayoutServiceTests` | selection with default and stale-value fallback, page classes, saving per store / all stores, overrides |
| `CwBrandDesignerServiceTests` | presets, base style, shape / intensity / lightness / warmth tweaks, region scoping, valid hex always |

## 4b. Custom CSS sanitizer — how it reads the CSS (fixed 2026-10-30)

The first version matched literal text, and the browser decodes CSS escapes before it acts, so `url(\68ttps://…)`,
`@\69mport`, `\75rl(…)`, `image-set("https://…")`, a `)` inside a quoted URL and `url("\\\\host/…")` all got past it.
`CwCssSanitizer` now:

- decodes **escaped letters** in the CSS it writes (an escaped letter means the same as the letter everywhere), so the
  keyword checks see `@import`, `url(`, `javascript`; other escapes stay as written, a kept hex escape always with its
  terminating space;
- reads each URL value the way the browser does before deciding (*data: / same-origin only*): every escape decoded,
  tabs and newlines dropped, ends trimmed, `\` read as `/`; only a relative URL or `data:` is kept, so any other scheme
  and any protocol-relative form is removed;
- matches quoted URLs as strings (a `)` or an escaped quote inside does not end them);
- treats `image-set()`, `-webkit-image-set()` and `image()` as URL holders: removed under *Block*, removed under
  *data: / same-origin only* when a string in them is external;
- catches `javascript` followed by an escaped colon.

## 4c. Settings validated on the storefront (2026-11-02)

Every setting was changed in the admin and measured on the storefront from the browser's computed styles (desktop width,
home, category, product, login and live-preview checkout): the six accent colours, the accent switch, the button radius
(0 = the theme's 5px), the colour check, both logos and the second-logo link, reverse logos, custom CSS with all three
URL policies, the four layouts, square corners, and a per-store override. All apply. Fixed by the run:

| Found | Fix |
|---|---|
| `CW.Standard/Views/Shared/Head.cshtml` registered `styles.css` then `cw-brand.css` with `AppendCssFileParts`, which inserts at the **front** of the list, so the bundle loaded the brand layer **before** nop's base and every base rule of equal specificity won: body text, content links, the success / error / warning bars, error text and side-block titles kept nop's colours (in 4.90 too) | Registered in reverse, with a comment: the page now loads jQuery UI, swiper, `styles.css`, then `cw-brand.css` |
| That new order then let every brand rule beat the base, including ones never seen working. On a desktop width `.header-menu { margin: 6px 0; }` dropped the base's `margin: 0 auto` and the menu sat at the left edge of the page; `a { color }` turned the *Administration* link blue on the dark bar. The first check missed both | `.header-menu` takes `margin: 6px auto`; `.admin-header-links a` inherits the bar's colour. Old and new orders compared by computed style at 1500 px on home, category, product, login, register, contact, search and cart: nothing else moves a page block off centre, the other differences are the brand's own cards, buttons and fields
| Two more from the same order, found on the checkout. Every `select` kept the base's fixed `height: 32px` under the brand's `padding: 10px 12px`, leaving 10 px for the text, so the selected option was cut off (billing address, sort and page size). The base's padding and `min-height` on the step number no longer applied, so its digit sat at the top of the circle, with the base's white right border showing | `select { height: auto; }` in the forms block; `.opc .step-title .number` is a 40 px flex circle with no border. The earlier comparison listed the select padding as brand styling because it compared properties, not whether the text still fit |
| The login page's *Forgot password?* link kept nop's grey: nop's `.login-page .returning-wrapper .inputs.reversed *` is more specific than the brand rule (in 4.90 too) | `cw-brand.css` targets `.inputs.reversed .forgot-password a`, with its hover |
| An invalid save showed the raw key `admin.common.formerrors`: both admin controllers asked for `Admin.Common.FormErrors`, which nop does not have | `Plugins.CW.ThemeCore.Configure.FormErrors`, installed by the `2026_11_02_DATA_1.4` update |

By design, not faults: the product-box *Add to cart* button also carries `button-2`, so it takes the outline style
(white, primary text and border); the header stacks at mobile widths.

## 5. Open

- **CW Standard Theme's *Light navigation bar* changes nothing visible.** The theme's default nav bar is already light
  (`--cws-nav-bg: transparent`), no rule reads `--cws-nav-hover-bg`, and the hint still says "instead of the dark
  default". Decide whether to remove the setting or turn it into a dark nav bar.

- **Brand Designer keywords match inside words:** "darker" hits Dark luxe's `dark`, "warmer" hits Warm boutique's `warm`,
  so those tweak prompts replace the current colours with a preset first. Recorded by a test; a whole-word match would
  fix it.
- `IsCartOrCheckout` matches `cart` / `checkout` as any path segment (e.g. `/category/cart`); preview requests only.

- `Themes/CW.StandardTest` is not ported (Voyage-based; `CLAUDE.md` §3.2).
- **Brand Designer AI assistant** — never built; the draft spec is `CWPlans/CW_Theme_AI_Assistant_Spec.md` (its transport
  choice predates .NET 10, see its 5.00 note). The Configure page's suggestions come from the rule-based
  `CwBrandDesignerService`.
- **Carried from the 4.90 build plan** (`CWPlans/Deprecated/CW_Theme_Build_Plans.md`, *Deferred / next steps* and the
  backlogs): whether `CwThemeSettings.LogoPictureId` should give way to nop's own store logo; the CW.Standard
  view-override backlog (waves 1–5, parity with Voyage); the token audit that moves CW.Core's storefront CSS onto
  `var(--cw-*)`; the bulk-order layout section.
- Generated custom CSS lives in the plugin's output folder (`Plugins/CW.ThemeCore/Content/generated/`): a deploy that
  replaces that folder removes every store's custom CSS until its settings are saved again (unchanged from 4.90).

## 6. Documents

| Document | What it is |
|---|---|
| `CW_Theme_DesignSwap_Guide.md` (this folder) | How to reskin and how to add a theme |
| `CW_ThemeCore_Nop_Upgrade_Checklist.md` (this folder) | What to re-check in nop on every upgrade |
| `CWPlans/CW_Theme_AI_Assistant_Spec.md` | The AI assistant, draft |
| `CWPlans/Deprecated/CW_Theme_Build_Plans.md`, `CW_Theme_Live_Preview_Phase3_Spec.md` | How the framework and the live preview were built (4.90, history) |

The other 4.90 theme documents (architecture foundation, layout system plan, live preview phases 1–2, effort estimate,
client overview, AI CSS editor feasibility) were not brought across; they are in the 4.90 source's `CWPlans/`.
