# PageRenderingEvent — asset registration audit

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

**Status:** PARKED (2026-08-15), still parked in 5.00. Half of it was done in 4.9 before the port and came across
with the code. Three stylesheets are still open (§4 items 3, 5 and 7), and the 5.00 check found one new candidate
(`flyout-cart.css`, §2.2). `EventConsumer.cs` works as it is. The `packname-subsidies.css` / `product-groups.css`
route gating was the first change made, and it is still the pattern to follow.

> ### What changed since 4.9 wrote this
>
> - `MyAccount/subsidies.css` and `Customer/manage-subsidies.css` are **gone**: the files, their registrations, and the
>   two database queries that guarded one of them. Items 4 and 6 are done.
> - The "My Subsidies" page **exists** now (§2.1 is closed). It has its own stylesheet,
>   `Content/MyAccount/customer-subsidies.css`, registered from its own view.
> - `pack-name-selector.css` is registered by the selector's own view. Item 2 is done.
> - Still global: `cw-dialog.css`, `cw-icons.css`, `pg-icons.css` (when Subsidy is on), and `flyout-cart.css`, which
>   was added after the audit.

**Tier:** 2 (multiple files, needs a decision per stylesheet, requires verification against runtime output)
**Scope:** `Nop.Plugin.CW.Core/Services/EventConsumer.cs` → `HandleEventAsync(PageRenderingEvent)` (`EventConsumer.cs:600`)
and the stylesheets it registers. Nothing else.

---

## 1. Why

`HandleEventAsync(PageRenderingEvent)` runs on **every page render, in both the storefront and the admin
area** (nop raises it from `Views/Shared/_Root.Head.cshtml:48`). When the audit was written it registered five
stylesheets with no condition and, when the Subsidy feature was on, ran two database queries to decide on a sixth.

So a storefront visitor downloaded admin CSS, an admin downloaded storefront CSS, and every admin
page paid for a `GetCurrentPackNameAsync` + `GetAllSubsidyPackNamesByPackNameIdAsync` round trip whose
only purpose was to decide whether to add a stylesheet the admin area never uses. The queries are gone; the
registrations with no condition are not.

`CLAUDE.md` §9.8 *"Where to register an asset"* states the rule: register as narrowly as the markup
that needs it, and treat `PageRenderingEvent` as the last resort. This task brings the handler in line.

Two entries were already fixed separately (`packname-subsidies.css` and `product-groups.css`). They are gated on the
Subsidy feature and on the admin route values: area Admin, and controller `ExtendedCustomer` or `Customer` with action
`Create` / `Edit`, or `MultiStore` / `Edit` (`IsAdminPage`, `EventConsumer.cs:301`). That change is the pattern to
follow, not part of this task.

What the handler registers in 5.00 (`EventConsumer.cs:600-640`):

| Asset | Condition |
|---|---|
| `Content/Common/cw-dialog.css` | none |
| `Content/icons/cw-icons.css` | none |
| `Content/PackName/flyout-cart.css` | none |
| `Content/BulkOrder/bulk-order.css` + `bulk-order.js` | a bulk grid will draw on this product page (`BulkOrderProductPageContext.Grid`) |
| `Content/Common/cw-cart-events.js` | PackName feature on |
| `Content/icons/pg-icons.css` | Subsidy feature on |
| `Content/Admin/Subsidy/packname-subsidies.css` | Subsidy on + admin customer create / edit or Manage Store |
| `Content/Admin/Subsidy/product-groups.css` | Subsidy on + Manage Store |

---

## 2. Current state

Findings below come from tracing each stylesheet's selectors back to the markup that uses them. Treat the
**Verdict** column as a starting hypothesis to confirm, not a conclusion — see §3 for why the tracing is
not conclusive on its own.

| Stylesheet | Currently registered | Markup found using it | Audience | Verdict to confirm |
|---|---|---|---|---|
| `Content/Common/cw-dialog.css` | Every page, both areas | `Views/SubsidyCatalog/ChangeTopupProductGroup.cshtml`, `ChangeTopupSubsidy.cshtml`, `ChangeTopup.cshtml`, `Views/Shared/Components/PackNameInfo/Default.cshtml` (the loading spinner), `Views/Shared/Components/SubsidyBar/Default.cshtml` (the top-up dialog it opens) | Public only | Move to the consuming views. It is **also registered a second time**, in `Views/SubsidyCatalog/ChangeTopup.cshtml:4` via `AppendCssFileParts`. That copy does nothing: the three `ChangeTopup*` views are AJAX partials (`SubsidyCatalogController.ChangeTopup` returns `PartialView`; the other two are rendered to a string), so a `NopHtml` call inside them never reaches a `<head>`. The same goes for `change-topup.css` on the next line of that view. The dialog's host is `SubsidyBar/Default.cshtml`, which already registers `change-topup.css` itself. `cw-dialog.css` belongs there and in `PackNameInfo/Default.cshtml`. |
| `Content/icons/cw-icons.css` | Every page, both areas | Emitted by `Extensions/CWIconHtmlHelperExtensions.cs` (`Html.CwIcon`, the packname icon helpers) and hard-coded in a few views. Storefront: `PackName/_PackName.Fields.Info`, `PackName/_PackNameImport`, the five `PackNameCheckout` partials, `CustomerSubsidies/Index`, and the `FlyoutShoppingCart`, `PackNameInfo`, `PackNameOrderDetails`, `PackNameSelector` and `SubsidyBar` components. Admin: **one page**, the admin import page (`Areas/Admin/Views/MultiStore/ImportPackNames.cshtml`), through the shared `_PackNameImport` partial | Storefront, plus one admin page | The census of item 7 no longer supports "leave it global". Gate it to the storefront and register it on the admin import page itself, or keep it global with a comment saying why. |
| `Content/PackNameSelector/pack-name-selector.css` | **Done:** `Views/Shared/Components/PackNameSelector/Default.cshtml:7` (`AppendCssFileParts`) | that view | Public only | Closed. |
| `Content/icons/pg-icons.css` | Every page when Subsidy on, both areas | Admin: `Areas/Admin/Views/Subsidy/SubsidyProductGroupPopup`, `_ProductGroups`, `_ProductGroups.ProductGroupDetail`, the `PackNameSubsidies` component (`Areas/Admin/Views/Shared/Components/PackNameSubsidies/Default.cshtml`, on the customer page and Manage Store), and `Content/Admin/MultiStore/admin.store.navigation.js`. Public: `Html.PgIcon` from `SubsidyBar/Default`, `SubsidyBar/Flyout`, `SubsidyCatalog/ChangeTopup`, `_ProductGroupHeader`, `CustomerSubsidies/Index`, `PackNameCheckout/_CartByPackName`, `_CartByProduct`, `PackNameOrderDetails/Default` | Both | Needed in both areas, but not on every page. The admin consumers are AJAX partials or sit on the same host pages as `packname-subsidies.css`, so the admin half can share that gate (the popup's host is to be confirmed). |
| `Content/Customer/manage-subsidies.css` | — | — | — | **Done:** file, registration and `.csproj` entry deleted. |
| `Content/MyAccount/subsidies.css` | — | — | — | **Done:** file, registration and the two DB queries deleted. The real page's stylesheet is `Content/MyAccount/customer-subsidies.css`, registered from `Views/CustomerSubsidies/Index.cshtml:11`. |
| `Content/PackName/flyout-cart.css` | Every page, both areas (added after the audit) | `Views/Shared/Components/FlyoutShoppingCart/Default.cshtml` | Public only | New candidate, §2.2. |

### 2.1 The "My Subsidies" page — closed

In 4.9 the page did not exist: the nav tab linked to `Plugin.CW.Core.CustomerSubsidies`, which no route provided, and
`MyAccount/subsidies.css` styled a `.html-subsidy-list-page` body class that nothing set. In 5.00 the page is there:

- route `CWCoreDefaults.CustomerSubsidiesRouteName` (`"Plugin.CW.Core.CustomerSubsidies"`, `customer/subsidies`) is
  registered in `RouteProvider.cs:94` and served by `Controllers/CustomerSubsidiesController.Index`;
- the view is `Views/CustomerSubsidies/Index.cshtml`, and it registers its own stylesheet, as §9.8 asks;
- the nav tab is inserted by `EventConsumer.AddCustomerNavigationItemsAsync` (`EventConsumer.cs:438`, reached from the
  `ModelPreparedEvent<BaseNopModel>` handler). It shows when the selected packname has an active subsidy actively
  assigned to it (`ISubsidyReportingService.GetSubsidyDetailsByPackNameIdAsync`). Those queries decide whether a tab
  exists, which is a real conditional, so they stay.

### 2.2 `flyout-cart.css` — new since the audit

The file's own header says it is registered from the event because the flyout renders out of `_Header.cshtml` after
the `<head>` has been written, so a registration in the view would never reach the page. In nop 5.00 that does not look
true. `Views/Shared/_Root.cshtml` has `Layout = "_Root.Head"`, so the header (and every widget zone in `_Root.cshtml`)
renders as the body of `_Root.Head`, **before** `_Root.Head.cshtml` raises `PageRenderingEvent` and writes the CSS
links. `PackNameSelector`, rendered from the same header, already registers its stylesheet from its view, and that works.
The bulk grid's comment in the handler makes the same claim for a widget zone; re-read it on the same grounds.
**Unverified at runtime.** If the flyout view's own `AppendCssFileParts` reaches `<head>` on a full page load,
move the registration there. The flyout HTML that comes back by AJAX after add-to-cart needs nothing, because the
page that hosts it already has the stylesheet.

### The DB queries — done

The guard that ran `GetCurrentPackNameAsync` + `GetAllSubsidyPackNamesByPackNameIdAsync` on every page render, to decide
whether to emit `MyAccount/subsidies.css`, is gone from `HandleEventAsync(PageRenderingEvent)`. No page render calls
either method to register CSS any more.

---

## 3. What the tracing does not settle

Selector-to-markup grepping has three blind spots. Each must be closed before deleting anything:

1. **Themes.** In the 4.9 source, neither `CW.Standard` nor `CW.StandardTest` uses any of these class names. The 5.00
   repository has only `DefaultClean` under `Presentation/Nop.Web/Themes` today, and it does not use them either
   (`.flyout-cart` in its `styles.css` is nop's own class, not ours). Grep the CW themes again once they are on disk
   in this repository.
2. **Classes applied at runtime.** `AppendPageCssClassParts`, `dialogClass`, string concatenation in JS,
   and classes emitted from C# helpers do not appear as literals in markup. `cw-icons.css` and `pg-icons.css` are the
   clear examples: they are alive through `CWIconHtmlHelperExtensions` (`Html.CwIcon`, `Html.PgIcon`) and
   `admin.store.navigation.js`.
3. **Selectors that piggyback on nop's own classes.** This was `manage-subsidies.css`'s case, and that file is gone.
   It still applies to `flyout-cart.css`, which styles `.mini-shopping-cart .item`, the theme's own class.

**Method:** for each stylesheet, load the page(s) it is supposed to style, open DevTools, and confirm the
rules actually match elements. That is the acceptance evidence — not a grep result.

---

## 4. Work items

Do these in order; each is independently verifiable.

1. **Establish the baseline.** Record the current `<link>` set emitted on: a storefront product page, the
   customer subsidies page (`customer/subsidies`), the SubsidyCatalog page, admin customer edit, admin multistore
   edit, the admin packname import page, admin return request edit. Save the list. Every later step is checked
   against it.
2. ~~**`pack-name-selector.css`**~~ — **done** (registered in `Views/Shared/Components/PackNameSelector/Default.cshtml`).
3. **`cw-dialog.css`** — move it into `SubsidyBar/Default.cshtml` (the host of the top-up dialog and its three AJAX
   partials) and `PackNameInfo/Default.cshtml`. Then delete the inert registrations at the top of
   `Views/SubsidyCatalog/ChangeTopup.cshtml` (`cw-dialog.css` and `change-topup.css`; the host already registers
   the second). Use `AppendCssFileParts` (storefront default, §5).
4. ~~**`MyAccount/subsidies.css`**~~ — **done**: registration, DB queries and file are gone.
5. **`pg-icons.css`** — split the registration by audience. Public consumers register in their own views (or on
   their host page when they are AJAX partials). The admin half moves under the same `IsAdminPage` gate as
   `packname-subsidies.css`.
6. ~~**`manage-subsidies.css`**~~ — **done**: file, registration and `.csproj` entry are gone.
7. **`cw-icons.css`** — the census is in §2: every caller is on the storefront except the admin import page. Either
   gate the handler's registration to the storefront and add `AddCssFileParts` to
   `Areas/Admin/Views/MultiStore/ImportPackNames.cshtml`, or leave it global with a comment saying why. Record the
   decision either way.
8. **`flyout-cart.css`** (new) — check §2.2 on a running site. If the view's registration reaches `<head>`, move it into
   `FlyoutShoppingCart/Default.cshtml` and correct the comment at the top of the CSS file.
9. **Re-read the handler.** After the above it should be short. If anything is left that is neither
   feature-gated nor route-gated, justify it in a comment or move it.

---

## 5. Constraints

- **No behaviour change to what the user sees.** This is about *when* a stylesheet loads, not how anything
  looks. Any visual difference means a step was wrong.
- Follow `CLAUDE.md` §9.8 *"Where to register an asset"* for the target pattern of each move.
- **`Add*` vs `Append*`:** `Append` *prepends*. `AppendCssFileParts` does `_cssParts.Insert(0, ...)` and
  `AddCssFileParts` does `_cssParts.Add(...)` (`Nop.Web.Framework/UI/NopHtmlHelper.cs:463`, `:486`). It is a
  cascade-order choice, not a view-vs-component one. Storefront moves use `Append*` (the theme keeps the last word), admin
  moves use `Add*` (CW layers on top of AdminLTE). `EventConsumer` uses `Add*` for everything, storefront
  files included, so a moved file may land at a different position in `<head>` than it does today. That is the one place a
  visual difference could legitimately appear, and §6.4 is the check for it. See `CLAUDE.md` §9.8.
- The `packname-subsidies.css` / `product-groups.css` block is the reference implementation. Match its
  shape: a single feature check, `IsAdminPage` on the route values, and a comment saying which markup needs it.
- Deleting a stylesheet also means deleting its `.csproj` `<None Update>` entry (§9.8 — there is no glob).
- No commits (§0.2).

---

## 6. Acceptance

1. The pages from step 1 emit **no `<link>` for a stylesheet whose markup is not on the page**, and
   still emit every one whose markup is.
2. Storefront pages carry no `Content/Admin/**` stylesheet; admin pages carry no storefront-only one.
3. No page render calls `GetCurrentPackNameAsync` or `GetAllSubsidyPackNamesByPackNameIdAsync` for the
   purpose of registering CSS. (Already true in 5.00.)
4. A visual comparison against the step-1 baseline on every page shows no difference.
5. Any file deleted is gone from disk, from the handler, and from the `.csproj`.

---

## 7. Out of scope

- The per-file `<None Update>` entries in `Nop.Plugin.CW.Core.csproj` (26 for CSS in 5.00) could collapse to
  one glob. Related, but a separate change with its own build-verification needs.
- ~~Moving `public.packname.checkout.js` and `subsidy-catalog.js` into feature folders~~ — done. They are
  `Content/PackName/public.packname.checkout.js` and `Content/SubsidyCatalog/subsidy-catalog.js`, registered by
  `Views/PackNameCheckout/Checkout.cshtml` and `Views/SubsidyCatalog/Index.cshtml`.
- Any restyling. If a stylesheet turns out to be badly written, note it and move on.
- ~~Building the missing "My Subsidies" customer page~~ — done (§2.1).
