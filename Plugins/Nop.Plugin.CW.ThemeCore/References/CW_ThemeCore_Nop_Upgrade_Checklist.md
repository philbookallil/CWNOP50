# CW.ThemeCore — nopCommerce upgrade checklist

**Run this on every nopCommerce upgrade.** CW.ThemeCore never edits nop, but a few places depend on how nop
behaves inside. A nop change there produces **no compile error and no test failure** — the feature just stops
working, or quietly does the wrong thing. For each item: compare the nop code named against what is described
here, and re-apply our change if nop moved.

---

## 1. Copied nop code

| Our code | Copied from | What we changed | What breaks if nop changes it |
|---|---|---|---|
| `Filters/CwPreviewCheckoutFilter.OnActionExecutionAsync` | `Nop.Web/Controllers/CheckoutController.OnePageCheckout` — the tail: load cart → `ICheckoutModelFactory.PrepareOnePageCheckoutModelAsync(cart)` → `View(model)` | Live-preview requests only. When the action bounces instead of rendering (checkout disabled, one-page checkout off, guest gate), the `OnePageCheckout` view is rendered anyway from the preview customer's cart. | If nop renames the view, changes the model-factory call or adds a new gate, the Brand Designer's Checkout preview drifts from the real page or falls back to the mock. |

---

## 2. Assumptions about nop internals

| Our code | Relies on | Check after upgrading |
|---|---|---|
| `Infrastructure/PreviewFramingStartup` (`Order => 550`) | nop's `INopStartup` pipeline orders: routing **400**, authentication **500**, authorization **600**, endpoints **900** (terminal) — unchanged in 5.00 (checked 2026-10-29). The framing middleware must run after authentication and before endpoints. | The `Order` values of nop's routing, authentication and endpoint startups. If endpoints moves below 550, the middleware never runs and every preview falls back to the mock. |
| `Infrastructure/CwPreviewFramingMiddleware` — impersonation | `IWorkContext.SetCurrentCustomerAsync(customer)` (`WebWorkContext`) caches the customer for the request **and** writes the `.Nop.Customer` cookie immediately via `Response.Cookies`. We snapshot the `Set-Cookie` header before the call and restore it after, so the preview customer's GUID never reaches the browser. | `WebWorkContext.SetCurrentCustomerAsync` / `SetCustomerCookie`. If nop starts writing the cookie later (e.g. in `OnStarting`) or elsewhere, the snapshot no longer catches it — the cookie leak comes back (live preview phase 3 spec §4). |
| `Filters/CwPreviewCheckoutFilter` | The order and kind of `OnePageCheckout`'s early returns (redirects / `Challenge`). The filter replaces any non-`ViewResult`. | A new early return that should **not** be overridden in the preview (rare — the filter only runs for preview requests). |
| `Services/Preview/CwPreviewCustomerService` | Preview customer = system account in the **Guests** role. nop's admin customer list defaults to the Registered role (keeps it hidden), and `CustomerService.DeleteGuestCustomersAsync` skips system accounts (keeps it from being purged). | Both behaviours. If either changes, the preview customer shows up in Admin → Customers, or is deleted with its cart (it is then just re-created on next preview). |
| `Views/Components/CwThemeHead/Default.cshtml` | nop's script/link tag helpers relocate a literal `<script>` to the footer and drop a literal `<link>` in the `HeadHtmlTag` zone, so we emit them through `Html.Raw`. | Not a breakage risk — if nop stops doing this, `Html.Raw` still works. Only revisit if you want to switch back to plain tags. |
| `Components/CwThemeHeadViewComponent`, `Components/CwSecondLogoViewComponent` (open their views by `~/Plugins/CW.ThemeCore/…` path) | **New in 5.00:** `ThemeableRazorViewEngine` (`Nop.Web.Framework/Themes`) serves a storefront view asked for by a plugin path from `~/Themes/{theme}/Plugins/CW.ThemeCore/…` when the active theme has that file. A theme may restyle the second logo or the head block this way; a replaced `CwThemeHead` view must still emit the custom CSS link and, for a preview request, the `cw-preview-bridge.js` script, or the live preview stops working with no error. | Whether the engine still applies to plugin paths and still skips the admin area. When a theme is enabled, look for a `Plugins/CW.ThemeCore/` folder in it. |
| `Infrastructure/CwPreviewFramingMiddleware` and `Filters/ThemeLayoutPageClassFilter` / `CwThemeHeadViewComponent` (read the working theme) | **New in 5.00:** `ThemesMiddleware` (`Nop.Services.Themes`, run from `NopCommonStartup`, order 100) resolves the working theme once per request into `HttpContext.Items` before the framing middleware (550) switches to the preview customer. The cart and checkout previews therefore render the theme of the admin's own session. That differs from the Configure page's choice (the store's default theme) only when *Allow customers to select a theme* is on. | `ThemesMiddleware`'s order, and whether the view expander still reads the theme only from `HttpContext.Items`. |

Background for all of the above: `CWPlans/Deprecated/CW_Theme_Live_Preview_Phase3_Spec.md` (the 4.90 live preview spec, history).

**Checked against nopCommerce 5.00 on 2026-10-29 (the beta) and again on the final 5.00 on 2026-11-02:** `OnePageCheckout` is identical to 4.90; `SetCustomerCookie` still
appends to `Response.Cookies` at once; `DeleteGuestCustomersAsync` still skips `IsSystemAccount`. Live preview of all
five pages (cart and checkout through the preview customer) passed on a local site.

---

## 3. Waiting on a nop upgrade

Things we did a certain way only because of what the current nop version allows. Re-check them when nop moves.

| Our code | Done this way because | When nop runs on .NET 10 |
|---|---|---|
| Brand Designer AI assistant (planned; **not built** — the spec `CWPlans/CW_Theme_AI_Assistant_Spec.md` is a draft, and the Configure page's suggestions come from the rule-based `CwBrandDesignerService`) | The spec chose the Messages REST API over nop's `HttpClient` because the official `Anthropic` NuGet SDK (≥ 11.0) needs System.Text.Json 10, which could not load on nop 4.90 / .NET 9 (plugins share the default load context, where STJ 9 is loaded). | **Now true: 5.00 runs on .NET 10.** Revisit the spec's transport decision (D1, §3.2) before it is approved; nothing in the code depends on it yet. |
