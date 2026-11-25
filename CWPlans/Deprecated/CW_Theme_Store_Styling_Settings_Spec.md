# CW Theme — store styling settings (font, corner radius, header / nav / footer styles)

Spec, 2026-11-19. Status: **draft, awaiting approval**. Tier 2 (`CLAUDE.md` §0.3).

## 1. Why

Evolution Safety moves from a Voyage-based 4.x store (https://www.evolutionsafety.com.au/) to 5.00 on `CW.Standard`, and
the client wants to keep its look. The look must be **reproducible by a staff member from the admin**, without a
programmer or a web designer (decision, 2026-11-19). Today CW.Standard can set the accent colours, button radius, logos,
layouts, a dark nav bar and square corners. What Evolution's look needs that a setting cannot give:

| Evolution (measured 2026-11-19, 1400 px) | Today on CW.Standard | Gap |
|---|---|---|
| Font Poppins | System stack, fixed in `cw-brand.css` | **Font setting** |
| Cards / panels 16 px corners, buttons 3–6 px | One `--cws-radius` (8 px) in CSS; button radius is a setting | **Corner radius setting** |
| Main header row dark `#262627`, white top bar | White row, tinted top bar | **Header style** |
| Full-width nav bar in the brand blue `#275CAB`, white 13 px semibold links | Light, or dark (`DarkNavBar`) | **Nav bar style** with *Brand* |
| Footer dark `#262627`, blue headings, white text, blue legal bar | White columns, grey legal bar | **Footer style** |
| Home categories on a full-width dark band, white cards | Cards on white | **Home categories band** |

Smaller things (uppercase category titles, grey tiles on the category page, the active side-menu row in blue) stay
in the store's Custom CSS — the one-off surface the design guide (§6) intends.

Voyage is a paid theme: nothing is copied from its CSS or markup; every rule below is written for CW.Standard.

## 2. Design

The existing pattern is kept: the theme reads a plugin's setting **by key** in `Head.cshtml` and only **overrides CSS
tokens**; `cw-brand.css` draws everything from tokens. So each new option is a handful of token values, the theme still
renders when a plugin is missing, and the same tokens are open to Custom CSS and the AI assistant's catalogue.

### 2.1 CW.ThemeCore — theme-agnostic (per store, `CwThemeSettings`)

| Setting | Type, default | Emits (in `CwThemeCssService.GenerateRootCssAsync`) |
|---|---|---|
| `FontFamily` | string key, `""` = the theme's own font | `@font-face` rules for the font's weights, then `--cw-font:"<Family>",<fallback stack>` in `:root` |
| `CornerRadius` | int px, `0` = the theme's own | `--cw-radius:<n>px` (0–48, validated like `ButtonRadius`) |

Both are emitted whatever *Don't apply accent colours* says (like `ButtonRadius`: not colours).

**Fonts are shipped in the plugin**, served from the store itself — no request to Google (decision, 2026-11-19):

- `Domain/Fonts/CwFontCatalogue.cs` — the fixed list: key, display name, CSS family, weights, fallback stack.
  Keys: `poppins`, `inter`, `roboto`, `open-sans`, `montserrat`, `lato`. Weights 400, 500, 600, 700, Latin subset,
  `font-display: swap`.
- Files: `Content/Fonts/<key>/<key>-<weight>.woff2` plus that font's `OFL.txt` (all six are SIL Open Font License, which
  allows redistribution — fine for the public repository, §0.9). Downloaded once from Google Fonts' CSS2 API (Latin
  subset). One wildcard entry in the csproj (`Content\Fonts\**\*`, `CopyToOutputDirectory`) rather than 30 lines.
- `@font-face` URLs are `/Plugins/CW.ThemeCore/Content/Fonts/...` — same origin, so the Custom CSS URL policy does not
  apply (it governs only Custom CSS).
- An unknown key (font removed later) emits nothing → the theme's font.
- Admin: a dropdown "Font" on the Configure page next to *Button radius*, listing *Theme default* and the six names,
  each option rendered in its own font so staff can see it. *Corner radius* sits beside *Button radius* with the same
  slider + number editor.

`cw-brand.css` bridges: `--cws-font: var(--cw-font, "Segoe UI", …)` and `--cws-radius: var(--cw-radius, 8px)`.

### 2.2 CW.Theme.Standard — CW.Standard-specific (per store, `StandardThemeSettings`)

| Setting | Values | Replaces |
|---|---|---|
| `HeaderStyle` | `Light` (0, default) · `Dark` | — |
| `NavBarStyle` | `Light` (0, default) · `Dark` · `Brand` | `DarkNavBar` (bool) |
| `FooterStyle` | `Light` (0, default) · `Dark` | — |
| `HomeCategoriesBand` | bool, default off | — |
| `SquareCorners` | unchanged | — |

Enums live in `Plugins/Nop.Plugin.CW.Theme.Standard/Domain/` and render as dropdowns (`Html.GetEnumSelectListAsync`)
on the theme's Configure page, each with a hint saying which colour it uses.

**`DarkNavBar` folds into `NavBarStyle`** (decision, 2026-11-19). The `DATA` update writes
`standardthemesettings.navbarstyle = Dark` for every store row where `darknavbar` was `True`, then deletes the
`darknavbar` rows and its two resources.

`Head.cshtml` reads the four keys and writes one `<style>` of token overrides. Colours come from the store's existing
accents — no new colour settings:

| Option | Tokens set |
|---|---|
| Header `Dark` | `--cws-header-bg: var(--cws-ink)`, `--cws-header-fg: #fff`, `--cws-header-upper-bg: #fff` |
| Nav bar `Dark` | today's `DarkNavBar` block, unchanged (desktop only) |
| Nav bar `Brand` | `--cws-nav-bg: var(--cws-primary)`, `--cws-nav-fg: #fff`, `--cws-nav-hover-bg: var(--cws-primary-dark)`, `--cws-nav-hover-fg: #fff`, `--cws-nav-item-bg: transparent`, `--cws-nav-divider: rgba(255,255,255,.2)` (desktop only) |
| Footer `Dark` | `--cws-footer-bg: var(--cws-ink)`, `--cws-footer-fg: rgba(255,255,255,.85)`, `--cws-footer-title: var(--cws-primary-on-dark)`, `--cws-legal-bg: var(--cws-primary)`, `--cws-legal-fg: #fff` |
| Home categories band | `--cws-home-band-bg: var(--cws-ink)`, `--cws-home-band-fg: #fff` |

`--cws-primary-on-dark` is the primary lightened until it reads on the dark colour
(`color-mix(in srgb, var(--cws-primary) 80%, #fff)`), so a dark brand colour still shows as a heading.

**`cw-brand.css`** gains the tokens with today's look as their defaults (so every store without the settings is
unchanged) and reads them:

- header: `.cw-header .header-lower` background / text, `.header-upper` background; the search field stays white;
- nav bar: in `Dark` / `Brand` the bar spans the full window width (background drawn beyond the container with
  `box-shadow` + `clip-path`, no markup change) and loses its top / bottom border;
- footer: `.cw-footer`, `.cw-footer-middle` background, `.footer-block .title`, links, social icons; `.cw-footer-lower`
  background and text; the newsletter band gets a 1 px divider when it meets a dark footer;
- home: `.cw-featured-categories` on a full-width band with `--cws-home-band-bg`, section title and card titles in
  `--cws-home-band-fg`; cards keep their white picture box.

Light values are today's values exactly — checked by comparing computed styles before and after on home, category,
product, cart and checkout with every new setting at its default.

### 2.3 Not changed

- No nop file (Cardinal Rule). No CW.Core file.
- The Brand Designer's generator / AI: the new settings are not part of `BrandStyle` yet (open item, §6). The AI
  catalogue in `cw-theme.json` gets the new tokens, and the `--cws-font` description loses "system fonts only".
- Live preview: the Brand Designer's preview shows the saved settings of both plugins (it renders the real storefront).

## 3. Files

| Plugin / theme | File | Change |
|---|---|---|
| ThemeCore | `Settings/CwThemeSettings.cs` | `FontFamily`, `CornerRadius` |
| ThemeCore | `Domain/Fonts/CwFontCatalogue.cs`, `CwFont.cs` | **new** — the font list |
| ThemeCore | `CwThemeCoreDefaults.cs` | `CssVarFont`, `CssVarRadius`, fonts path |
| ThemeCore | `Services/CwThemeCssService.cs` | emit `@font-face` + the two variables (`:root` block becomes "font faces, then `:root{}`") |
| ThemeCore | `Areas/Admin/Models/CwThemeSettingsModel.cs`, `Validators/CwThemeSettingsValidator.cs`, `Controllers/CwThemeCoreController.cs` | the two fields, override flags, validation, available fonts |
| ThemeCore | `Views/Configure.cshtml` | font dropdown, corner-radius slider |
| ThemeCore | `CwThemeCorePlugin.cs` | install defaults, resources; `plugin.json` → `2026_11_19_DATA_1.8` |
| ThemeCore | `Content/Fonts/**`, csproj | **new** — 24 woff2 + 6 `OFL.txt`, one wildcard entry |
| Theme.Standard | `Domain/HeaderStyle.cs`, `NavBarStyle.cs`, `FooterStyle.cs` | **new** |
| Theme.Standard | `Settings/StandardThemeSettings.cs`, model, controller, `Views/Configure.cshtml` | the four settings, `DarkNavBar` removed |
| Theme.Standard | `StandardThemePlugin.cs`, `plugin.json` → `2026_11_19_DATA_1.3` | defaults, resources, the `DarkNavBar` → `NavBarStyle` move |
| CW.Standard | `Views/Shared/Head.cshtml` | read the keys, emit the overrides |
| CW.Standard | `Content/css/cw-brand.css` | new tokens with today's defaults; header, nav, footer, home rules read them |
| CW.Standard | `cw-theme.json` | AI catalogue: new tokens; `--cws-font` description |
| Tests | `Tests/CW.Tests/CW.ThemeCore/CwThemeCssServiceTests.cs`, new `CwFontCatalogueTests.cs` | font faces + variable for each key, unknown key, radius 0 / n, emitted with accents off; every catalogue file exists |
| Docs | `ThemeCore_Plugin_Reference.md`, `CW_Theme_DesignSwap_Guide.md` | the settings; this spec folds in and moves to `Deprecated/` when it lands (§0.4) |

The ThemeCore files also carry the uncommitted AI-assistant work; every change here is listed in §5 so the two can be
committed apart.

## 4. Evolution Safety — the recipe (store 1 on the local site)

What a staff member sets; the result is checked against the live site side by side.

| Where | Setting | Value |
|---|---|---|
| CW Theme | Primary / tint / dark | `275CAB` / `E9EFF8` / `262627` |
| CW Theme | Button radius / Corner radius | 5 / 16 |
| CW Theme | Font | Poppins |
| CW Theme | Logo | Evolution logo (from the live site) |
| CW Standard theme | Header / Nav bar / Footer | Dark / Brand / Dark |
| CW Standard theme | Home categories band | on |
| CW Theme | Custom CSS | uppercase category titles; grey tiles on the category page; active side-menu row in the primary colour (a few lines, listed in the test record) |

Out of scope: the home banner slider, the "Our commitment" text, the contact form and the site-features strip — store
content (widgets, topics), not theme.

## 5. Test plan

| # | Check |
|---|---|
| T1 | Build: both plugins and `CW.Tests`, 0 errors; all CW.ThemeCore tests pass |
| T2 | Plugin updates run: resources installed; a store with `darknavbar = True` ends with `navbarstyle = Dark` and no `darknavbar` row |
| T3 | Every new setting at its default: computed styles of header, nav, footer, home cards, buttons identical to before (home, category, product, cart, checkout, 1400 px and 375 px) |
| T4 | Font: each of the six applies (computed `font-family`, the woff2 loads with 200, no request leaves the store); *Theme default* emits nothing; per-store override |
| T5 | Corner radius 0 → theme's 8 px; 16 → cards 16 px; *Square corners* still wins |
| T6 | Header Dark, Nav Light / Dark / Brand, Footer Dark, Home band: each on its own and together, desktop and phone; text contrast on the dark and brand surfaces |
| T7 | Accents off: the header / nav / footer styles still render (from the theme's fallbacks) |
| T8 | Evolution recipe (§4) applied to store 1; side-by-side screenshots of home, category, product with the live site |
| T9 | Live preview in the Brand Designer shows the saved styles |

## 6. Open

- Whether the font and corner radius join the Brand Designer's `BrandStyle` (presets, AI answers).
- Whether the uppercase titles and the category-page tile colour become settings, if a second store wants them.
