# CW Theme — Design-Swap & Handoff Guide

**Audience:** designers (reskin without touching plumbing) and developers (create/extend CW themes).
**Scope:** how the CW theming system separates *design* from *mechanics*, exactly what to edit to change the look, and how to stand up a new theme. Reference theme: `CW.Standard`.

---

## 1. The mental model — two layers

| Layer | What it is | Who owns it | Where |
|---|---|---|---|
| **CW.ThemeCore** (plugin) | Central machinery: per-store **accent colours** (as CSS variables) + optional per-store **Custom CSS**. Shared across all themes. | Admin / developer | `Plugins/Nop.Plugin.CW.ThemeCore` + admin settings page |
| **The theme** (folder) | The **design**: layout, typography, components, spacing. | Designer / developer | `Presentation/Nop.Web/Themes/CW.<Name>/` |

The theme reads the brand colours ThemeCore publishes and styles the storefront around them. Change the colours in admin → the whole store re-skins with no code.

---

## 2. The three swap surfaces (least → most code)

| # | Surface | Change what | Requires code? | Rebuild? |
|---|---|---|---|---|
| 1 | **Accent colours** (per store) | Brand/primary + status colours | No — admin only | No |
| 2 | **Theme design layer** — `cw-brand.css` | Typography, spacing, radius, component look | CSS only | No (reload) |
| 3 | **Markup overrides** — Razor views | HTML structure of a page/component | Razor | No (reload) |
| + | **Per-store Custom CSS** (per store) | One-off store-specific tweaks | No — admin only | No |

Only changes to the **ThemeCore plugin itself** (C#) need a rebuild + app restart. Theme CSS/views are compiled at runtime — just reload.

---

## 3. Surface 1 — Accent colours (admin, per store, no code)

**Where:** Admin → **CW Theme** (under the CW menu group) → settings page.

1. Pick the **store scope** at the top (all-stores default, or a specific store).
2. Set the colours (hex, no `#`): **Primary**, **Primary tint**, **Success**, **Danger**, **Warning**, **Dark**. The live preview shows the effect.
3. Save. ThemeCore injects them into every page of that store as CSS variables:

| Variable | Setting | Typical use |
|---|---|---|
| `--cw-primary` | Primary colour | Brand accent: buttons, links, prices, nav hover |
| `--cw-primary-tint` | Primary tint | Subtle backgrounds, focus rings |
| `--cw-success` | Success | Positive notifications |
| `--cw-danger` | Danger | Errors / validation |
| `--cw-warning` | Warning | Warnings |
| `--cw-dark` | Dark | Ink / dark surfaces (nav bar, footer) |

**"Don't apply accent colours" toggle:** when checked (per store), ThemeCore stops emitting the `--cw-*` block and the theme controls colour entirely (using its own fallbacks). Custom CSS is unaffected.

---

## 4. Surface 2 — The theme design layer (`cw-brand.css`)

**File:** `Themes/CW.Standard/Content/css/cw-brand.css` — **this is the designer's main file.**

**How the stylesheets layer (important):**
- `Content/css/styles.css` = nop's OSS **DefaultClean base** (full layout). **Do not edit it** — it's the upgrade-safe base.
- `cw-brand.css` = the **CW design layer**, loaded *after* the base, overriding look without touching layout.

Both are wired in `Views/Shared/Head.cshtml`; you normally never touch that.

### 4.1 The `DESIGN TOKENS` block (top of `cw-brand.css`)

Edit these to change the look **globally**. They're the single swap surface:

| Local token | Bridges to / default | Controls |
|---|---|---|
| `--cws-primary` | `var(--cw-primary, #4a4a4a)` | Brand colour (flows from admin; fallback when accents off) |
| `--cws-primary-tint` | `var(--cw-primary-tint, …)` | Subtle backgrounds, focus rings |
| `--cws-primary-dark` | derived (`color-mix`) | Hover/active of primary |
| `--cws-ink` | `var(--cw-dark, …)` | Nav bar, footer, headings |
| `--cws-success/danger/warning` | `var(--cw-*)` | Status colours |
| `--cws-muted`, `--cws-border`, `--cws-bg` | neutrals | Text-muted, borders, backgrounds |
| `--cws-font` | system stack | Font family |
| `--cws-radius`, `--cws-radius-sm` | 8px / 5px | Corner rounding |
| `--cws-shadow`, `--cws-shadow-hover` | shadows | Card elevation |
| `--cws-transition` | .18s ease | Hover animation speed |
| `--cws-container` | 1200px | Max content width |

**The bridge pattern:** `--cws-primary: var(--cw-primary, #4a4a4a)` means "use the admin brand colour, or this fallback if accents are disabled." Keep this pattern when adding tokens.

### 4.2 Restyling a component

Below the tokens, `cw-brand.css` has clearly-labelled sections (Typography, Header, Main navigation bar, Buttons, Product cards, Side blocks, Forms, Footer, Responsive). To change a component's look, edit its section — always referencing tokens (`var(--cws-…)`), never hard-coded colours, so it stays swap-driven.

---

## 5. Surface 3 — Markup overrides (developer)

When the **HTML structure** (not just styling) must change, add a Razor view under the theme that mirrors the core path. The themeable view-location expander uses it automatically; any view you *don't* override falls through to core nop.

Examples (create only the ones you need):

| To change | Add file |
|---|---|
| Footer markup | `Themes/CW.Standard/Views/Shared/Components/Footer/Default.cshtml` |
| Product card | `Themes/CW.Standard/Views/Shared/_ProductBox.cshtml` |
| Home page | `Themes/CW.Standard/Views/Home/Index.cshtml` |
| Master layout | `Themes/CW.Standard/Views/Shared/_Root.cshtml` |

Copy the core view (`Presentation/Nop.Web/Views/...`) as a starting point, then edit. Prefer CSS (Surface 2) over markup overrides — fewer files to maintain across upgrades.

**Plugin views (nopCommerce 5.00).** A theme can also replace a storefront view a plugin opens **by path**: nop's
`ThemeableRazorViewEngine` serves `~/Themes/{theme}/Plugins/<plugin folder>/<same path>` when the theme has that file,
and the plugin's own file otherwise (not in the admin area). For CW.ThemeCore that means:

| To change | Add file |
|---|---|
| Second logo markup | `Themes/CW.<Name>/Plugins/CW.ThemeCore/Views/Components/CwSecondLogo/Default.cshtml` |
| Head block (colour variables, custom CSS link, preview bridge) | `Themes/CW.<Name>/Plugins/CW.ThemeCore/Views/Components/CwThemeHead/Default.cshtml` — keep everything the original emits; leaving out the `cw-preview-bridge.js` script breaks the Brand Designer's live preview without an error |

Two limits. A view CW.Core forks **by name** (order details, return requests, order totals, flyout cart) is found before
the theme's, so a theme cannot restyle those with a view file — only with CSS (`CLAUDE.md` §10.4). And a `Plugins/` folder
in a theme replaces the plugin's file silently, so check for one whenever a theme is enabled.

---

## 6. Per-store one-off Custom CSS (admin)

For store-specific tweaks that shouldn't live in the theme (e.g., one store needs a wider logo): Admin → **CW Theme** → **Custom CSS** (pick the store scope). It's sanitized, served as a cache-busted file, and applied **after** the theme CSS. The `url()` policy (Allow / data+same-origin / Block) is configurable per store; `@import`, `expression()`, `javascript:`/`vbscript:`, `behavior:`, and markup-breakout are always blocked.

---

## 7. Applying a real design (designer workflow)

1. **Colours:** set the brand + status colours in admin (Surface 1).
2. **Tokens:** edit the `DESIGN TOKENS` block for type scale, spacing, radius, neutrals (Surface 2).
3. **Components:** restyle sections in `cw-brand.css`, referencing tokens.
4. **Markup:** only where structure must change, add view overrides (Surface 3).
5. **Reload** to see changes (theme CSS/views are live — no rebuild). Iterate.

---

## 8. Creating a NEW CW theme

1. Copy `Themes/CW.Standard` → `Themes/CW.<NewName>`.
2. Edit `theme.json`: set `SystemName` = `CW.<NewName>`, `FriendlyName`, and the `PreviewImageUrl` path.
3. `Head.cshtml` resolves paths from the active theme name (`{themeName}`), so asset links usually need no change; double-check any hard-coded `~/Themes/CW.Standard/…` references.
4. Point a store at it: Admin → store's theme setting (`DefaultStoreTheme`), or the theme selector. A restart makes the new theme discoverable (nop caches the theme list at startup).
5. Reskin via the `DESIGN TOKENS` block + `cw-brand.css`.

> The repo also contains `CW.StandardTest` — a Voyage-derived reference skin. Use it only to *look at*; don't copy its (licensed) files into a new theme.

---

## 9. Rules & gotchas

- **Don't edit core nop files** (`Libraries/Nop.*`, `Presentation/Nop.*` views) — Cardinal Rule. All work goes in `Themes/CW.*` or `Plugins/Nop.Plugin.CW.*`.
- **`cw-brand.css` must load after `styles.css`.** `Head.cshtml` registers them with `NopHtml.AppendCssFileParts`, which
  inserts at the *front* of the list, so it registers `cw-brand.css` first. A new theme copied from CW.Standard keeps that
  order; registering them in reading order makes nop's base win every rule of equal specificity (body text, links,
  alerts), silently.
- **Don't edit the base `styles.css`** (DefaultClean base) — put all design in `cw-brand.css`. CW.Standard's copy carries
  one change from 4.90 (the desktop menu item background, `#f5f5f5`); on a nop upgrade take `DefaultClean`'s new file and
  re-apply only that line.
- **Reference tokens, not raw colours**, so the design stays swap-driven and accent-aware.
- **Theme `_ViewImports.cshtml` must include** `@using Nop.Web.Extensions` (home of `ShouldUseRtlThemeAsync`) plus the standard nop theme imports.
- **In head/widget-zone views, emit `<link>`/`<script>` via `@Html.Raw(...)`** — a literal `<link rel="stylesheet">` there gets intercepted by nop/WebOptimizer's tag helper and dropped (learned the hard way; see `tasks/lessons-4.9.md` #1).
- **What needs a rebuild:** ThemeCore plugin C# changes (rebuild + app restart). **What's live on reload:** theme CSS and Razor views (runtime compilation). New themes / theme-setting changes: app restart (theme-list + settings caches).
- **Disabling or uninstalling `CW.ThemeCore`** stops all colour/Custom-CSS injection (it's a hidden infra widget with no widget-list toggle) — re-enable/reinstall to restore.

---

## 10. Quick file map (`CW.Standard`)

```
Themes/CW.Standard/
├── theme.json                         theme identity (SystemName, FriendlyName, preview)
├── preview.jpg                        admin thumbnail
├── Views/
│   ├── _ViewImports.cshtml            usings/tag-helpers (must include Nop.Web.Extensions)
│   └── Shared/Head.cshtml             loads styles.css (base) + cw-brand.css (design)
└── Content/css/
    ├── styles.css                     nop OSS DefaultClean base — DO NOT EDIT
    ├── styles.rtl.css                 RTL variant of the base
    └── cw-brand.css                  ⭐ THE DESIGN LAYER — tokens + component styling
```

**Design lives in two places only:** the admin **accent colours** (brand) and **`cw-brand.css`** (everything else). That's the swap surface.
