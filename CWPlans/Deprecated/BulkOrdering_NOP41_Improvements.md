# Bulk Ordering — what nopCommerce 4.1 does better than 4.6

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

> **Purpose.** The 4.9 `CW.Core` rebuild is based on the **4.6** implementation
> (`CWPlans/BulkOrdering_NOP46_Reference.md`). This document exists for one job only: to record where the older
> **4.1** implementation is *better* or does something 4.6 does not do at all, so those ideas are not lost.
> It is **not** a description of 4.1, and nothing here is a decision.
>
> **Sources.** `NOP41/Nop.Plugin.TIG.BulkOrdering/` (controllers, views, scripts) and
> `NOP41/Nop.Plugin.TIG.Base/` (domain, business logic, services). Per `CLAUDE.md §3.1`, `/NOP41` is reference
> material only. Line numbers are from the working copy.
>
> Statements that could not be settled by reading are marked **[unverified]**.

---

## 0. The two designs in one paragraph each

**4.6** — a paged product list; each product with attributes loads its own quantity matrix by AJAX; the
customer types quantities; **Add all to Cart** is a plain form POST whose field *names* encode
`productId_attrId_valueId-…` triples; the server iterates the form keys and adds items to the cart one at a
time, accumulating warnings. Filters: one category (single-select tree) + one of four filter modes + sort +
page size. No packname/subsidy participation.

**4.1** — a single "bulk order" screen listing products as collapsible cards, extended by *Load more*; each
card is a small matrix (colour × main attribute, or a single attribute, or a bare quantity); a **packname
selector** at the top decides *who* the order is for (a group, or a named person) and carries subsidy, PO/comment
and branch-code data; **Add All to Cart** builds a typed request per product and posts them **sequentially over
AJAX behind a progress modal**, and the server validates a whole product's request as a unit before adding
anything. Filters: multi-select category tree (jsTree) + gender, persisted in browser storage.

---

## 1. Improvement candidates

Each item: what 4.1 does → what 4.6 does → why it matters → suggested position for 4.9.

---

### I1 — A typed request object instead of parsing form-field names

**4.1.** The controller builds a `BulkAddToCartRequest`
(`NOP41/Nop.Plugin.TIG.Base/Domain/BulkOrder/BulkAddToCartRequest.cs`):

```csharp
class BulkAddToCartRequest {
    Product Product;
    List<BulkProduct> Variants;      // BulkProduct { List<BulkProductAttribute> Attributes; int Quantity; }
    Packname Packname;
    string SubsidyMode;
    string POComment;  POCommentFieldType POCommentType;
    string BranchCode;
}
```

`BulkProductAttribute` holds the actual `ProductAttributeMapping` and `ProductAttributeValue` objects, not ids
(`Domain/BulkOrder/BulkProductAttribute.cs`). Four builders produce it from the form —
`BuildAddToCartRequestByColorMatrix` (`Controllers/BulkOrderController.cs:492`),
`BuildAddToCartRequestBySingleAttribute` (`:534`), `BuildAddToCartRequestByNoAttributes` (`:568`),
`BuildAddToCartRequestForPersonMode` (`:595`) — and everything downstream works on the object.

**4.6.** `AddItemsToCartFromBulkOrderList` (`Jim2ShoppingCartController.cs:236`) walks every form key, string-splits
`itemquantity_12_5_77-6_91-7_15` on `-` and `_`, and has a `switch (AttributesList.Length)` with cases
`>= 3 / 2 / 1`, indexing `IDsList[1]`, `IDsList[2]` without bounds checks. The whole method is duplicated for
Favourites (`:469`).

**Why it matters.** This is the single biggest blocker to "support more attributes": the 4.6 parser is
*written per arity*. A 4-attribute matrix needs a new `case`, a new nested block, and the same again in the
Favourites copy. A typed request with `List<(mappingId, valueId)>` per variant supports any arity with no new
code, is unit-testable without an HTTP form, and gives the service layer a real input record — which is also
what `CLAUDE.md §5.1` requires ("a service that needs data *from* a form takes an explicit input record it
owns").

**For 4.9.** Take this. The wire format can stay a form (or become JSON), but the boundary type should be
`BulkAddToCartRequest`-shaped and arity-agnostic. Put it in `BusinessLogic/Requests/` per §5.1.

---

### I2 — Validate the whole request, then add — instead of add-as-you-go

**4.1.** `BulkOrdersLogic.AddBulkProductsToCart` (`BusinessLogic/BulkOrdering/BulkOrdersLogic.cs:182`) runs a
gauntlet *before* touching the cart:

1. product exists;
2. `product.ProductType == SimpleProduct`;
3. at least one variant with a quantity;
4. `HasInvalidAttributesCombinations` (`:123`) — every requested attribute value must appear in some configured
   `ProductAttributeCombination`;
5. `ExceedsMaxBulkOrderItemsNumber` (`:171`) — see I3;
6. `ParseBulkAddToCartWarnings` (`:96`) — calls nop's `GetShoppingCartItemWarnings` for **every** variant, at the
   quantity it *would end up at* (`DetermineEventualVariantQuantity`, `:85`, sums with what is already in the
   cart for the same attribute set).

Only if all six pass does `AddToCart` (`:109`) actually add. Warnings are `Distinct()`-ed before being returned.

**4.6.** No pre-validation at all. Items are added in form-key order; nop's own warnings abort individual adds;
whatever succeeded before the failure stays in the cart. The user gets one concatenated warning string
(`Jim2.BulkOrder.AddToCart.Error` + `string.Join(" ", allWarnings)`) with no indication of which product or
which cell caused it, and the successful quantities have already been consumed.

**Why it matters.** On a bulk screen a partial add is worse than no add: the customer cannot tell what landed,
retyping the whole grid double-adds the successful lines, and stock-limited items get consumed by the first
half of the grid. This is the correctness difference between the two versions.

**For 4.9.** Take this, and extend it: 4.1 validates **per product**; a bulk screen should validate the
**whole submission** (all products, all cells) and then commit. That also makes the packname/subsidy budget
check (I11) meaningful, because a budget is a property of the submission, not of one product.

Note `DetermineEventualVariantQuantity`'s idea specifically: validate against *cart quantity + requested
quantity*, not requested quantity alone. 4.6 does compute a `quantityInCart` for display and subtracts it from
`MaxAvailable`, but nothing enforces it on POST.

---

### I3 — A cap on how much one bulk submission may add

**4.1.** `BulkOrderSettings.MaxBulkOrderItems` (`Domain/Setting/BulkOrderSettings.cs`), enforced by
`ExceedsMaxBulkOrderItemsNumber` (`BulkOrdersLogic.cs:171`), which counts how many *new, distinct* variants the
request would create (`UniqueVariantsCount`, `:158`) and refuses when `cart.Count + uniqueVariants > max`.

**4.6.** Nothing. A customer can submit a 100-row × 40-cell page in one POST.

**Why it matters.** It is the only guard against a single request creating thousands of `ShoppingCartItem`
rows, and it bounds the cost of the request. Cheap to implement, and it gives a clear error instead of a
timeout.

**For 4.9.** Take it, as a per-store setting. Consider capping *cells submitted* as well as *cart rows created*.

---

### I4 — Reject impossible attribute combinations, and don't render them

Two halves of the same idea.

**4.1, server:** `HasInvalidAttributesCombinations` (`BulkOrdersLogic.cs:123`) rejects the request if a requested
value is not part of any configured combination (only when the product has combinations configured at all).

**4.1, view:** `_SingleAttributeMatrix.cshtml:18` calls
`BulkProductAttributesHelper.IsAllowedAttributeCombination(allowedCombinations, colourValue, attributeValue)`
and renders a literal `-` instead of a quantity box for impossible cells. The allowed set is put on the model by
`AssignAllowedAttributeCombinations` (`BulkOrderController.cs:377`).

**4.6.** Renders a quantity box for **every** value cross-product cell, then discovers per cell that
`GetProductAttributeCombinationAsync` returns null — in which case the cell simply falls back to the product
price and (on the `ManageStockByAttributes` path) whatever `combination == null` yields. The customer sees an
enabled box for a size/colour that does not exist.

**Why it matters.** For correctness, and for the performance problem: the cartesian product is what makes the
matrix expensive (§20 of the 4.6 reference). Driving the grid from the **combination rows** rather than from
the value cross-product both fixes the UI and collapses the cost, and it is the only approach that survives
going past three attributes — 5 attributes × 6 values each is 7 776 cross-product cells but perhaps 60 real
combinations.

**For 4.9.** Take the idea, strengthen the implementation. 4.1's version is a per-value membership test
(`any combination contains this value`), which is weaker than "this *tuple* is a real combination"; the rebuild
should test tuples.

---

### I5 — Progressive add-all with a progress modal and per-product error surfacing

**4.1.** `addAllInBulkToCart` (`Scripts/bulk-orders.js:697`):

- scans every `.product-details-form`, collects `{productId, productName, productPrice, productQuantity, url,
  bulkData}` for each product that has any quantity typed;
- calls `allowBulkItemsAdd` (`:690`) → the packname/subsidy pre-check (I11), which can show a confirmation
  dialog and abort;
- opens a remodal dialog with a progress bar and posts the products **sequentially** through a recursive
  promise chain (`bulkAddItemsPromise`, `:580`), updating the bar and the message per product;
- on completion: `displayBulkProcessWarnings` (`:641`) adds `.error` to the cards that failed and
  **clears the inputs only of the cards that succeeded** (`clearoutFormFields`, `:625`);
- `updateItemCartCountIndicator` (`:106`) refreshes the "(n)" counter on each card that succeeded;
- `signalBulkOrderFinished` pings the server that the bulk order is done.

**4.6.** `Add all to Cart` is a form submit → full page POST → `RedirectToAction("BulkOrdering", …)`. Everything
the customer typed is gone; a notification bar says how it went in aggregate; a per-product "Add to cart" button
does the *same* full page POST (`BulkOrdering.cshtml:166` → hidden form `send-bulkformproducts`).

**Why it matters.** The whole value of a bulk screen is the state the customer has built up in it. Losing it on
every submit, and on every failure, is the biggest usability gap between the two versions.

**For 4.9.** Take the **behaviour**, not the implementation. 4.1's sequential per-product round-trip is chatty
(one HTTP request per product) and only exists because each product is validated separately (I2). With a
whole-submission validate-then-commit the right shape is **one** AJAX POST returning a per-cell result set, and
the page then clears the accepted cells, marks the rejected ones in place, and updates the cart widget — same UX,
one request.

---

### I6 — Multi-select category filter with persisted state

**4.1.** `_BulkOrdersFilter.cshtml` + `Scripts/bulk-orders-filter.js`: a jsTree with the `checkbox` plugin over
the category tree; selecting nodes fires `bulk-orders:filter` with the full id array; the selection is written
to browser storage under `bulk-order.filter.categories` (`:88`), gender under `bulk-order.filter.gender`
(`:84`), and a **Clear Filters** button resets both (`:123`). The filter panel is sticky
(`addStickyBehavior`, `bulk-orders-filter.js:14`).

**4.6.** A single-select nested `<ul>`; clicking a category is a full page navigation
(`Views/Shared/Components/BulkOrderCategoryNavigation/Default.cshtml`); one category at a time; no persistence;
no clear.

**Why it matters.** Bulk buyers work across several categories in one session. Multi-select plus remembered
state is a real workflow difference, and the server query already takes a `categoryIds` **list**
(`SearchProductsAsync`/`SearchContractedItemsOnlyAsync` in 4.6 accept `IList<int>`) — the 4.6 UI simply never
sends more than one.

**For 4.9.** Take multi-select and Clear. Persistence is a nice-to-have; if kept, it must be per-store and
per-customer-scoped in the key, and it must not be the source of truth (the URL should still carry the state, as
4.6's `history.replaceState` already does — that part of 4.6 is better than 4.1, which has no shareable URL).
Do **not** take jsTree; use whatever the 4.9 theme already ships.

---

### I7 — Gender filter

**4.1.** `_GenderFilter.cshtml` — three checkboxes (M/F/Unisex) mapped to a **specification attribute** named
`Gender` (`ITIGCustomerService.GetGenderAttributeOptionsIds`, used at `BulkOrderController.cs:404`), gated by
`BulkOrderSettings.AllowGenderSelection`.

**4.6.** Nothing.

**Why it matters.** 4.9 already has the concept on the *packname* side: `MultiStoreSettings.GenderEnabled` and
`GenderFiltering` (`CLAUDE.md §15.3`), and `PackName.Gender`. So the filter has a home; the open question is
whether 4.9 filters by product **specification attribute** (4.1's mechanism) or by the selected packname's gender
(which is what `GenderFiltering` implies today).

**For 4.9.** Worth taking, but confirm which of the two mechanisms is meant before designing it. **[unverified]**
— what settles it: read how `MultiStoreSettings.GenderFiltering` is consumed in `CW.Core` today. *(2026-10-01: it
was never read, and subsidy phase 5 Part 2 deleted it; a gender filter is now a new feature to specify.)*

---

### I8 — "Load more" instead of a pager

**4.1.** `fetchBulkProductsList(pageNumber, itemsPerPage)` (`Scripts/bulk-orders.js:453`) appends the next page's
rendered rows to the list; the server action is `FetchBulkProducts` (`BulkOrderController.cs:696`) returning
`{ items: <html>, totalCount }`; the button hides itself when exhausted (`enableLoadMoreElement`, `:443`).

**4.6.** A nop `PagerModel` pointing at `BulkOrderingPaged`, i.e. a **full page reload** per page
(`BulkOrderDefaults.RouteActionName = "BulkOrderingPaged"`).

**Why it matters.** Same reason as I5 — a pager that reloads the page destroys typed quantities on the current
page. Note the 4.6 item list *is* already loaded by AJAX (`BulkOrderingItems`); only the pager links bypass it.

**For 4.9.** At minimum, make paging AJAX so typed state survives (this is a small change to 4.6's own design —
the pager just needs to call `loadItems()` instead of navigating). Whether to go all the way to infinite append
is a UX decision; appending makes "Add all" span pages, which is either the feature or a hazard depending on
what the business wants. **Ask.**

---

### I9 — Collapsible product cards

**4.1.** Each card has a `.colapse-indicator`; clicking collapses it to a one-line summary
(`colapseProductDetailsView`, `Scripts/bulk-orders.js:127`, wired in `setupProductItems`, `:392`), and
collapsing **resets that card's inputs** (`:417`). Cards are collapsed after a successful add (`:909`).

**4.6.** Nothing; every matrix is always fully expanded, which is why the page is heavy and why the
`abbr-title` / `no-abbr-title` CSS gymnastics exist.

**For 4.9.** Worth taking, and it composes with a real performance idea: **collapsed products need not have
their matrix computed at all**. If the matrix loads on expand, the 4.6 "one AJAX request per attributed product"
(§2.2 of the reference) stops being a cost paid up front for products the customer never looks at.

---

### I10 — Ordering *on behalf of* a packname / person mode

**4.1.** The packname selector (`_PacknameSelector.cshtml`, `Scripts/packname-selector.js`, 1 202 lines) lets a
manager pick a packname (a named employee or a group) before adding. It drives:

- **person mode vs group mode** — `IsPersonModeBulkAdd` (`BulkOrderController.cs:472`) switches the whole form
  from a quantity-per-cell matrix to *one* quantity plus a normal attribute selection
  (`BuildAddToCartRequestForPersonMode`, `:595`); `enableBulkOrderProductFields` (`bulk-orders.js:159`) toggles
  which inputs are live;
- **PO / comment** and **branch code** fields, gated by `BulkOrderSettings.EnablePOCommentField`,
  `POCommentFieldTitle`, `EnableBranchCodeField` (`BuildPacknameSelectorModel`, `:160`);
- these travel on the request (`BulkAddToCartRequest.Packname / SubsidyMode / POComment / POCommentType /
  BranchCode`) into `CartItemOptions` (`Domain/BulkOrder/CartItemOptions.cs`) and are persisted per cart line
  (`PacknameCartItem`) and per order line (`PacknameOrderItem`).

**4.6.** Bulk ordering **is** packname-attaching, but *ambiently* rather than explicitly. The bulk add calls
plain `IShoppingCartService.AddToCartAsync` with no packname argument
(`Jim2ShoppingCartController.cs:303`, `:353`, `:403`, `:444`); the attachment happens inside
`ShoppingCartOverrideService.AddToCartAsync` (`Services/Orders/ShoppingCartOverrideService.cs:2449`), which
reads `IPackNameContext.GetCurrentPackNameAsync(customer)` (`:2462`) and writes a `PackNameCartItem` row against
the new cart line (`:3093`). `GetShoppingCartAsync` (`:2668`) and `GetAllShoppingCartAsync` (`:2725`) are
overridden the same way. So a bulk order placed while a packname is in context lands against that packname
exactly as a normal add-to-cart would — **the bulk screen does nothing special for it**, which is the right
design and should be preserved.

On top of that ambient behaviour, the bulk screen reads the context packname for **stock arithmetic**: when one
is selected it measures availability against `GetAllShoppingCartAsync` (every packname's reserved lines) instead
of just the current packname's cart (`BulkOrderModelFactory.cs:570`, `:675`).

What 4.6 does **not** have is any of the four things below.

**The four differences, precisely:**

| | 4.6 | 4.1 |
|---|---|---|
| Where the packname comes from | ambient — whatever `IPackNameContext` says at the moment of the add | chosen **on the bulk screen**, in a selector at the top, and posted **with** the request (`BulkAddToCartRequest.Packname`) |
| Form shape | always a quantity matrix | **person mode** — when a named person is selected the card switches to *one* quantity plus a normal single attribute pick (`IsPersonModeBulkAdd`, `BulkOrderController.cs:472`; `BuildAddToCartRequestForPersonMode`, `:595`; `enableBulkOrderProductFields`, `bulk-orders.js:159`) |
| Extra data captured per add | none | subsidy mode, PO / comment (+ its type), branch code — carried on the request into `CartItemOptions` and stored on `PacknameCartItem` / `PacknameOrderItem` |
| Budget check before adding | none | the whole submission is simulated against the packname's subsidy and the customer is told the shortfall before anything is added (see I11) |

**Why it matters.** 4.9's `CW.Core` already owns this domain: `PackName`, `PackNameCartItem`,
`PackNameOrderItem` with `POEntry`, `Comments`, `EmployeeNumber`, `PrintName`, `Gender`
(`CLAUDE.md §14.2`), plus `PackNameCheckoutController` / `PackNameCheckoutModelFactory`, and a
`ShoppingCartOverrideService` that already does the ambient attachment. So 4.6's model works in 4.9 unchanged;
the question is only whether the extra three rows of that table are wanted.

**For 4.9.** Keep 4.6's ambient attachment — it is the correct design and it means the bulk screen never has to
think about packnames for the *add* path. Then decide separately, per row: (a) an on-screen selector,
(b) person mode, (c) PO/comment/branch capture, (d) the subsidy pre-check (I11). (d) is the one with real
business value; (b) is the one with real cost. **Ask.**

---

### I11 — Subsidy budget checked *before* the add, with a stated shortfall

**4.1.** `allowedBySubsidy` (`Scripts/packname-selector.js:581`) simulates the whole submission against the
selected packname's subsidy **client-side**: it consumes product-group unit allocations first, then
product-group dollar allocations, then the general dollar subsidy, rejecting each item as it is covered; if
items remain it sums the uncovered amount and shows a confirmation
(`showNoSubsidyConfirmation`, `:450`) telling the customer what they will have to pay. `allowAddItems` (`:699`)
gates the add on that promise.

**4.6.** Nothing — Jim2 has no subsidy concept.

**Why it matters.** 4.9 **does** have subsidies, with a defined consumption order
(`CLAUDE.md §14.5`: product-group qty → product-group topup qty → product-group credit → product-group topup
credit → global credit → subsidy topup) and a service that implements it
(`Services/Subsidies/SubsidyAllocationService.cs`, `SubsidyAllocationRequest`). A bulk screen that lets a
customer type 40 lines and only discovers at checkout that the subsidy covers 12 of them is a support ticket
every time.

**For 4.9.** Take the *behaviour*, reject the *implementation*. 4.1 reimplements the subsidy consumption walk in
JavaScript, which is exactly the duplication `tasks/lessons.md` #11 describes (four independently-written copies
of the subsidy walk, one of which disagreed with the others). The 4.9 version must ask
`ISubsidyAllocationService` for a dry-run allocation of the submitted lines and render whatever it returns —
one implementation, one answer.

---

### I12 — A permission, not just a role list

**4.1.** `BulkOrderingPermissionProvider.AccessBulkOrdering` (`Security/BulkOrderingPermissionProvider.cs`), a
real nop `PermissionRecord` with default grants to super-admin, admin and branch-manager roles, checked in
`BulkOrderController.Index` (`:711`) and `ItemsOrderedForMe` (`:913`).

**4.6.** `Jim2DefaultSettings.EnableBulkOrdering` + `BulkOrderRoleIds` (an int list on a settings blob), checked
in one place and **not** checked on three of the four read endpoints (4.6 reference, D6).

**For 4.9.** `CW.Core` already has `CWPermissionConfigManager` and the `[CheckPermission(...)]` attribute
(`CLAUDE.md §6.6`, §9.1). A `CW.CORE.Public.AccessBulkOrdering` permission applied by attribute to every action
is both the house pattern and a fix for D6. Whether the *role list* setting survives alongside it is a
separate question — a permission on customer roles may make `BulkOrderRoleIds` redundant.

---

### I13 — An explicit "this product can't be bulk-ordered" message

**4.1.** `_BulkProductAttributes.cshtml:118` — when `ProductsHelper.SupportsBulkOrder` is false, the card renders
a message with a link to the product's normal page ("You now have the option to order this garment … by clicking
here").

**4.6.** A product with ≥4 authorised attributes is silently omitted from the list
(4.6 reference D5), leaving a page that shows fewer items than the pager claims, with no explanation.

**For 4.9.** Take the message. If the rebuild supports arbitrary attribute counts, the case shrinks to
genuinely unsupported control types (free-text, file upload, date) — but it does not vanish, and a silent
omission is the wrong answer for any of them.

---

### I14 — Per-product cart-count indicator refreshed in place

**4.1.** Each card shows `Name - SKU (n)` where `n` is that product's quantity in the cart
(`_BulkProductDetails.cshtml:20`, `:30`, `ProductsHelper.CountProductItemsInCart`), refreshed by AJAX after a successful
add (`updateItemCartCountIndicator`, `bulk-orders.js:106`).

**4.6.** Has the data (`QuantityInCart`, `QuantityInCartValue`, and per-cell `AttributesInCart`) and shows it —
but only on a full page render, and as a pre-built English string built in the factory
(`BulkOrderModelFactory.cs:609`).

**For 4.9.** 4.6's per-cell granularity is better than 4.1's per-product count; take 4.1's *refresh-in-place*
and drop the hardcoded string.

---

### I15 — Warning de-duplication and attribution

**4.1.** `ExtractWarnings` (`BulkOrderController.cs:477`) and `ParseBulkAddToCartWarnings`
(`BulkOrdersLogic.cs:96`) both `Distinct()`; failures are carried as
`{productId, productName, error}` (`handleBulkItemFailed`, `bulk-orders.js:562`) and rendered against the card
that produced them.

**4.6.** `allWarnings` is a flat `List<string>` joined with spaces into one notification; the same warning
repeated for 30 cells appears 30 times; nothing says which product or cell.

**For 4.9.** Take it, and go one better: attribute the warning to the **cell**, since the cell is the unit the
customer typed into. This falls out of I1 + I2 naturally.

---

### I16 — One place decides whether a product is bulk-orderable

**4.1.** `ProductsHelper.SupportsBulkOrder` / `SupportsColorMatrix` / `SupportsSingleBulkAttribute` /
`SupportsNoAttributesMode` (`NOP41/Nop.Plugin.TIG.Base/Helpers/ProductsHelper.cs`) — one predicate set, used by
both the view (`_BulkProductAttributes.cshtml:10`) and the controller (`BulkAddToCart`, `:647`), with
overloads for the entity and the view model.

**4.6.** The decision is spread across `ShowBulkOrderingInProductPageAsync` (six conditions, product page only),
`IsBulkOrderingCompatible` (control types, product page only), `MainAttribute() > 0` (list),
`Count < 4` (list, twice) — and the list path and the product-page path apply **different** rules.

**For 4.9.** Take the single-predicate idea. Note 4.1's *implementation* is bad — see N1 below.

---

## 2. Things in 4.1 that must NOT be carried over

| # | What | Why not |
|---|---|---|
| N1 | Attribute roles resolved by **name matching** — `IsColourAttributeName` matches a name starting with `"color"`/`"colour"`/`"col"`, `IsSizeAttribute` matches a name or text-prompt *containing* `"size"` (`ProductsHelper.cs:29`–`:62`) | Breaks on translation, on `"Collar"`, on `"Oversize"`. 4.6's settings-driven `HorizontalAttributeId` / `VerticalAttributeMainId` / `VerticalAttributeSecId` is the better mechanism and should stay. |
| N2 | `EngineContext.Current.Resolve<T>()` throughout `ProductsHelper` (`:151`, `:187`, `:188`, `:204`, `:230`, `:252`) | Forbidden by `CLAUDE.md §4` / §23 — constructor injection only. |
| N3 | Hardcoded store ids in business logic — `if (_storeContext.CurrentStore.Id == 173 \|\| … == 184)` (`BulkOrderController.cs:178`) | Self-evidently. |
| N4 | The bulk-order matrix limited to **colour × one attribute** (`SupportsColorMatrix` requires exactly 2 attributes, one of them colour) | Narrower than 4.6's three axes and than the 4.9 goal. |
| N5 | One HTTP POST per product on Add All (`bulkAddItemsPromise`, `bulk-orders.js:580`) | See I5 — the right target is one request for the submission. |
| N6 | Subsidy arithmetic reimplemented in JavaScript (`packname-selector.js:470`–`:695`) | `tasks/lessons.md` #11. One implementation, server-side. |
| N7 | `_TIG.*` global JS namespace, jsTree / remodal / chosen / underscore / store.legacy / tooltipster / stretchy / jquery.sticky, all registered per page (`Views/BulkOrders/Frontend/Index.cshtml:10`–`:26`) | Eight libraries for one screen. 4.9 should use what the theme already ships. |
| N8 | `Html.AddTIGSharedCSS` / `Html.AddTIGSharedScript` helpers | Do not exist in 4.9 — and `tasks/lessons.md` #5 is exactly this mistake (a 4.1 `Html.*` helper carried into a 4.9 view compiles fine and dies on first request). |
| N9 | Business logic class with no interface (`BulkOrdersLogic` is a bare concrete class, injected as such) | `CLAUDE.md §6.13` requires `IFooLogic`/`FooLogic`. |
| N10 | `WelcomePack*` bulk import (CSV of employees → packnames → orders) | A separate feature that happens to live in the same 4.1 plugin. Out of scope unless the business asks for it — but note it exists, because "bulk ordering" in 4.1 conversation may mean this. |

---

## 3. Where 4.6 is better than 4.1 (do not regress these)

Recorded so the rebuild does not "improve" its way backwards:

1. **Settings-driven attribute axes** (`HorizontalAttributeId` / `VerticalAttributeMainId` / `VerticalAttributeSecId`)
   rather than name sniffing — see N1.
2. **Three axes** rather than two.
3. **Filter modes** — All / Contract items / Latest ordered (or one specific past order) / Best sellers. 4.1 has
   no equivalent; "reorder what I bought last time" is a core bulk-buyer workflow.
4. **Sort options and page size** as first-class controls.
5. **URL state** — 4.6 keeps filter/sort/page/view-mode in the query string via `history.replaceState`, so a
   bulk view is linkable and survives a refresh. 4.1's state lives only in browser storage.
6. **Per-cell stock, ETA and price**, including per-warehouse breakdown and pre-order handling. 4.1 shows a bare
   quantity box.
7. **Grid / list view modes** and a mobile layout.
8. **Database-layer paging** on every query (4.1's `GetProductsList` renders `ProductDetailsModel` per product
   via `IProductModelFactory.PrepareProductDetailsModel`, the heaviest model in nop, for a list screen).
9. **Allowed-quantities support** (`Product.AllowedQuantities` → `<select>`).
10. **Both** an all-products add and a per-product add.

---

## 4. Open questions this document raises

These need answers before the 4.9 design, and each changes the shape of it:

1. **Does 4.9 bulk ordering order for a packname (I10)?** If yes, person-mode, PO/comment, branch code and the
   subsidy pre-check (I11) are all in scope, and the screen is materially bigger.
2. **Subsidy participation (I11).** If packnames are in, does the bulk screen show remaining subsidy live as
   quantities are typed, or only validate on submit?
3. **Paging model (I8).** Pager, AJAX pager, or infinite append — and if append, does "Add all" mean the loaded
   set or the current page?
4. **Gender filter (I7)** — by product specification attribute, or by the selected packname's gender?
5. **Contract items.** 4.6's `ContractItems` filter and `ApplyContractItemsOnlyConstraint` have no CW equivalent.
   Does that filter mode survive, map onto subsidy product groups, or disappear?
6. **Favourites / Quote list.** 4.6 wires bulk ordering into a wishlist ("product enquiry") and a quote list, and
   forks the whole view set for Favourites. Are either of those in scope for 4.9?
7. **Where do the settings live?** 4.6 puts them on the per-store Jim2 settings page; 4.9's analogue is
   `MultiStoreSettings` and `Areas/Admin/Views/MultiStore/Settings.cshtml`. Confirm that is the intended home.
