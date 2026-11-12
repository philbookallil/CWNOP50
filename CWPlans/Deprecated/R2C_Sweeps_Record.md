# R2-C — the two sweeps (Open Items 3.1, 3.2, 3.14)

**Date:** 2026-11-12. **Owner:** Eduardo. **Status:** closed 2026-11-12 (approved). Applied by Claude (permission given for this part only: no logic
change); built, tested (test run 1 below) and approved.

## 1. What changed

No behaviour changes except how a message is shown.

- **Text in scripts (3.2, 3.14).** No `@T(...)` inside a `<script>` string any more in CW's own views. Each script reads its
  texts from a hidden config div (`data-*`, read with `$(…).data('camelCase')`), per `CLAUDE.md` §9.14. A text that is
  put into HTML is escaped (`$('<span>').text(x).html()`); one that goes to `.text()`, `confirm()` or a dialog title is
  used as it is.
- **Messages (3.1).** `alert()` in admin views and scripts → `display_nop_error({ error: … })`; storefront: the packname
  checkout uses `displayBarNotification(…, 'error', 3500)` (as the same file already did), the returns wizard uses
  `displayBarNotification` on submit and its own `showPickerError` inside the product picker.
- **URLs.** The branch delete posts to `@Url.Action("DeleteBranch", "MultiStore")` on the button (was the literal
  `/Admin/MultiStore/DeleteBranch`); the info strip's flyout URL and the terms-of-service popup URL moved to data
  attributes.
- **3.14.** `PackNameInfoModel.HasSubsidies` doc comment now says what the component sets: a subsidy in its ordering
  period, and Subsidy on for the store.

Files — admin: `Branch/_CreateOrUpdate.Addresses`, `Customer/List` (CW's impersonate button only),
`Customer/_CreateOrUpdate.CreditNotes`, `Exchanges/_FeeRules`, `MultiStore/AssociateCustomerToPackNamePopup`,
`DepartmentManagerAddPopup`, `ManagerAddPopup`, `List`, `_Branches.BranchDetail`, `_Branches.PackNamesInline`,
`_PackNames.PackNameDetail`, `Shared/Components/AclDepartments`, `ExchangeSettings/_ReturnReasonsRules`,
`OrderReturnRequests`; scripts: `Content/Admin/MultiStore/admin.store.navigation.js`, `Content/Exchanges/exchanges-gather.js`;
storefront: `PackName/_AddPackName`, `_EditPackName`, `_PackName.Fields.Info`, `PackNameCheckout/Checkout`,
`_OrderSummary`, `Shared/Components/PackNameInfo`, `SubsidyBar`, `SubsidyCatalog/ChangeTopup`; `Models/PackNames/PackNameInfoModel.cs`.

## 2. Left on purpose

- **Lines copied from nop in nop-forked views**, so a re-diff against nop stays clean (§10.3 / §10.4):
  `Category` and `Manufacturer/_CreateOrUpdate.Mappings:18`, `Customer/List:250, 434, 501`,
  `Customer/_CreateOrUpdate.Info:349, 375`, `Product/_CreateOrUpdate.Info:273, 295`, `Views/Order/Details:53`,
  `PackNameCheckout/_CheckoutAttributes:190, 193, 194` (copy of nop's `Shared/_CheckoutAttributes`),
  `ReturnRequest/ReturnRequest:221, 230, 231`. They change when nop changes them.
- **`exchanges-gather.js:686`** — `window.alert(printReminder)` inside the return confirmation's `beforeClose`: it must
  block the close, which a notification bar cannot.
- **Other `'@Url.Action(...)'` in scripts** (generated, not literal URLs) — outside this sweep.

## 3. Found on the way

- `T(...)` returns `LocalizedString : HtmlString`, so `@T(...)` is written **raw**, not HTML-encoded — in a script
  string and in an attribute alike. The failure §9.14 guards against is a resource containing `'` (or `"` in an
  attribute) ending the string; an attribute is the safer place because resources contain apostrophes far more often than
  double quotes. §9.14's wording ("Razor HTML-encodes `@T`") is not accurate and could be corrected.
- `_Branches.BranchDetail.cshtml`: `postData = { id };` in the delete handler assigns an undeclared (global) variable —
  harmless, not changed (logic).

- Review (independent, 2026-11-12): nothing blocking. Applied: the two checkout messages that moved from `alert` to the
  bar are escaped, because the bar sets HTML where `alert` showed text. Not changed:
  - `$(…).data()` turns a purely numeric, `true`/`false` or `{…}` text into a non-string; no resource looks like that
    (`.attr('data-…')` would make it airtight).
  - `admin.store.navigation.js` still has 16 literal `/Admin/MultiStore/…` URLs. A `.js` file cannot use `Url.Action`,
    so they need a config div from the host page. That is a follow-up, not part of this sweep.
  - `Checkout.cshtml` `#read-terms` binds `keyup` but tests `keydown`, so Enter never opens the popup. It was copied
    from nop and is unchanged.
  - After a failed add or edit packname save, a second, identical config div appears. This is harmless.

## 4. Test plan

Build; empty `Presentation/Nop.Web/wwwroot/bundles` (two `.js` files changed, §9.11); run `Tests/CW.Tests`.

| # | Page | Check |
|---|---|---|
| T1 | Customer edit → Credit notes | Delete label, confirm, "select an order" / invalid amount → nop error popup |
| T2 | Exchanges config → Fee rules | Edit/Override/Delete labels, modal titles, validation → error popup |
| T3 | Manage Store → branch detail | Save / Saving… label, delete confirm, delete works (URL from the button), server error → popup |
| T4 | Manage Store → packnames inline delete failure; packname detail branch select placeholder, "select a branch" | error popup / placeholder |
| T5 | Manager / department-manager / associate-customer popups | gender names, Select button, "already has a packname" popup |
| T6 | Admin store list, branch addresses grid, customer list impersonate button, order return requests card, ACL departments, return-reason rules | labels as before |
| T7 | Storefront add / edit packname dialog | title and name label switch with Group; delete confirm; error popup |
| T8 | Packname checkout | remove-line confirm; failed update → bar notification; terms-of-service popup |
| T9 | Info strip subsidy flyout (and its error text); subsidy bar Change top-up dialog and its two inner dialogs | titles and texts |
| T10 | Returns wizard | picker "select all options" in the picker; submit with something missing → bar notification |

### Test run 1 (2026-11-12, dev site, after a build and an emptied bundle folder)

Tested in the browser. Each script's config div was read back and its render functions called. For the error paths,
`display_nop_error` / `displayBarNotification` / `confirm` / `$.ajax` were stubbed to capture what the page would show,
so nothing was written except as noted.

| # | Result |
|---|---|
| T1 | Pass — credit-notes labels; "Please select an order." through `display_nop_error`; Delete button rendered |
| T2 | Pass — Override / Edit labels, "Add a fee rule" title, amount validation through `display_nop_error` |
| T3 | Pass — Save → "Saving..." → Save; refusal through `display_nop_error`; delete confirm and URL `/Admin/MultiStore/DeleteBranch` (from the button) |
| T4 | Pass — inline packname delete failure "Could not delete."; branch select placeholder "Search"; "select a branch" message |
| T5 | Pass — gender names in both manager popups; Select button; "already has a packName" (nop's `display_nop_error` is itself an `alert` in 5.00) |
| T6 | Pass — Manage Store, branch address Default / Set as Default, Impersonate, order return card (deleted item, Edit), ACL departments, Link Actions |
| T7 | Pass — add / edit packname dialogs: titles and the name label switch with Group; delete confirm; error notification |
| T8 | Pass — remove confirm; both shipping updates show their message escaped in the bar; terms popup URL and title |
| T9 | Pass — info-strip flyout loads, and shows its error text on failure; Manage Topup and both inner dialog titles |
| T10 | Partly — the served `exchanges-gather.js` has both changes and no `alert(`; the wizard itself was not run (Steve Gates has no order that can be returned) |

Side effects: Steve Gates (impersonated) now has packname 1 selected (it was 29). A shirt was added to his cart and
then removed.
