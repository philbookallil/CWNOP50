# Bulk Ordering — design (`Nop.Plugin.CW.Core`)

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/` (as `BulkOrdering_49_Design.md`).

> **Status:** every phase is built. In 4.9: P0–P6 and P9 by 2026-09-05, P6b on 09-07, P7 (the grid-skeleton
> cache, §10.5) on 09-08, I14/I15 (the phone and desktop passes of P9) on 09-08 and 09-09, P8 (the product-page
> grid, §19) and P10 (entry points, §20) on 09-09 — the phase plan's record
> (`CWPlans/Deprecated/BulkOrdering_49_Phase_Plan.md`). In 5.00 the services, factory and business logic came
> across in beta phase 2, the product-edit card in part 4g (2026-10-28) and the storefront (controller, view
> components, views, script, style sheet) in part 5h (2026-10-30); 5h's catalogue tests passed in M4 (2026-10-31,
> 12 of 12) — `CWPlans/CWCore_Port_Reference.md`. Part 5h also changed the add-to-cart path (§11.2) and what the
> request carries (§11.1); those changes are marked **5.00** where they land. The 2026-09-05 review
> (`BulkOrdering_49_Review.md`) was folded into this document, the phase plan and
> `BulkOrdering_Future_Improvements.md`, then deleted (phase plan, 2026-09-07); no copy exists under `NOP49/`.
>
> **Companion documents.** `BulkOrdering_NOP46_Reference.md` (what the 4.6 implementation does — the
> functional baseline) and `BulkOrdering_NOP41_Improvements.md` (what 4.1 did better), both in
> `CWPlans/Deprecated/`, and `BulkOrdering_Future_Improvements.md` (everything deliberately deferred out of this
> design).
>
> **Rule for this document.** Every claim about nop behaviour is derived from the nop source under
> `Libraries/` and `Presentation/`, cited as `File.cs:NNN` — re-pointed to the 5.00 source on 2026-10-31. Anything
> not verified that way is marked **[unverified]** with the check that would settle it. Lesson numbers are the 4.9
> ones, unchanged, in `tasks/lessons-4.9.md`. Lesson #12 exists because a document that
> is 90% source-derived and 10% recalled is more dangerous than one that is wholly recalled.

---

## 1. Goal

Replace 4.6's bulk-ordering screen with the **same functionality**, a **better implementation**, support for
**up to 5 grid attributes** (and any number of attributes overall), and a cost model that does not grow with
the attribute cross-product.

Functional parity target (from the 4.6 reference): paged product list, filter modes, sorting, page size, grid /
list view, per-cell quantity entry with price, stock, ETA and "already in cart", per-product add and add-all,
category navigation, the same store-level settings, and the product-details-page variant.

---

## 2. Decisions taken

Recorded per `tasks/lessons-4.9.md` #16 — source code says what the system does, decision records say what it is
*supposed* to do.

| # | Decision | Date |
|---|---|---|
| D1 | The grid is **combination-driven**. A quantity box exists only where a real `ProductAttributeCombination` exists. Impossible cells render as a dash and cannot be ordered. | 2026-08-28 |
| D2 | Up to **5 axis attributes**. Attributes beyond that, and attributes that cannot be an axis, are **pinned** — chosen once at the top of the product card and applied to every line added from that card. | 2026-08-28 |
| D3 | Axis settings are an **ordered preference list**, not a whitelist. An attribute absent from the list is still eligible for a free axis slot; a listed attribute absent from the product is simply skipped. See §5. | 2026-08-28 |
| D4 | **Conditional attributes are supported.** Conditions are evaluated once per product per pin-state, never per cell. See §9. | 2026-08-28 |
| D5 | A product with **no combinations configured** still renders a grid — nothing is invalid, so every cell is enterable and there are no dashes. See §7.4. | 2026-08-28 |
| D6 | Add-all follows **4.6's outcome semantics**: lines that pass are committed, lines that fail are reported. Improved only in *how* it is reported — per-cell attribution and the typed grid state survives. Atomic all-or-nothing is deferred (`Future_Improvements` F2). | 2026-08-28 |
| D7 | **No subsidy participation** on the bulk screen. Subsidy stays a cart/checkout concern, as in 4.6. Deferred (`Future_Improvements` F1). | 2026-08-28 |
| D8 | PackName attachment stays **ambient** — `ShoppingCartOverrideService.AddToCartAsync` already reads `IPackNameContext` and writes `PackNameCartItem` (`Plugins/Nop.Plugin.CW.Core/Services/Orders/ShoppingCartOverrideService.cs:526`, `:927`). The bulk screen does nothing packname-specific for the add. | 2026-08-28 |
| D9 | The print-name attribute (`MultiStoreSettings.PrintNameProductAttributeId`) is **pinned like any other non-combinable attribute**. Verified safe at the time: that setting then had no consumer in `CW.Core` beyond admin settings plumbing. It now has one, `PrintNamePrefillEventConsumer`, which pre-fills nop's product page only (`ProductDetailsModel`), not the grid. Deferred (`Future_Improvements` F5). | 2026-08-28 |
| D10 | Page-level pin defaults (set once, apply to every product on the page) are **not** in this design. Deferred (`Future_Improvements` F3). | 2026-08-28 |
| D11 | **Access is the permission alone.** `CW.CORE.PublicStore.AccessBulkOrdering`, defaulting to Registered + Administrators. 4.6's `BulkOrderRoleIds` setting is **not** ported — it is the same gate written twice and the two can disagree. *Behaviour change from 4.6.* | 2026-08-28 (P1) |
| D12 | The axis cap is the constant `BulkOrderDefaults.MAX_AXIS_COUNT = 5`, not a setting. The admin renders exactly five ordered slots, so a configurable lower cap could only contradict the UI. | 2026-08-28 (P1) |
| D13 | No `BulkOrderShowETA` setting. A setting that does nothing is worse than none; it lands with **F11** when an availability provider exists. | 2026-08-28 (P1) |

**Open for confirmation** — flagged rather than assumed:

- **D6 reading.** "Same as 4.6" is read as *commit-what-passes*, not as *full page reload* (a reload would
  contradict "improve the implementation"). If the intent was literal 4.6, say so.
- **Settings home.** §12 proposes a new `BulkOrderSettings : ISettings` rather than growing `MultiStoreSettings`,
  surfaced on the same admin settings page. Deviation from "keep it in store settings" in *class*, not in
  *place*.

---

## 3. Vocabulary

Used precisely throughout; the implementation should use the same words.

| Term | Meaning |
|---|---|
| **Axis attribute** | An attribute laid out on the grid. Axis 1 becomes the **columns**; axes 2..n become the **row grouping**. |
| **Pinned attribute** | An attribute chosen once, above the grid, and applied to every line added from that card. |
| **Cell** | The intersection of one column value and one row tuple. Holds a quantity box, or a dash. |
| **Row tuple** | The ordered list of axis-2..n values that labels one row. |
| **Selection** | `(ProductAttributeMappingId, ValueIds[])`. A list because checkbox attributes carry several values. |
| **Signature** | The set of *combinable* mapping ids a combination row was built from. See §6. |
| **Cell key** | `axis selections ∪ pinned selections with a value`, restricted to combinable mappings. The lookup key into the combination index. |
| **Grid skeleton** | The customer-independent part of a product's grid: axes, columns, row tuples, combination index. Cacheable. |

---

## 4. The screen

### 4.1 Request shape

4.6's 17 requests for a 15-item page become **one**:

```
1. GET /bulkordering                 the whole page: controls, category tree, product rows WITH their grids
2. GET /bulkordering/items?...       the tree and the rows again, on any control change (AJAX)
3. (on demand) GET /bulkordering/grid/{productId}?pins=...   ONE card's grid, when a pin changes
```

Request 1 renders everything server-side — request 2 is not fired on load (P2 as built, §4.3). 4.6's "one AJAX
request per attributed product" (`NOP46` reference §2.2) goes away: with the cost model in §10 a whole page of
grids is cheap enough to build in the item request. Request 3 exists only for pin changes, which are
user-initiated and infrequent.

### 4.2 Layout

```
┌─ Nurse Tunic — TUN-400 ──────────────────────────────────── 6 in cart ─┐
│  Sleeve  (•) Short  ( ) Long        Logo  [ Left chest       ▾ ]       │  ← pinned strip
│  Embroidery  [x] Name  [ ] Dept  [x] Logo  … (20)                      │
│  Name to print  [________________]                                     │
├────────────────────────────────────────────────────────────────────────┤
│  Colour   Fit    │   8     10    12    14    16    18    20    22       │  ← columns = axis 1 (Size)
│  ────────────────┼──────────────────────────────────────────────────    │
│  Navy     Reg    │ [   ] [   ] [ 2 ] [   ] [   ] [   ]   –     –        │
│                  │  12    12    10     8     4     0                    │  ← availability row
│  Navy     Slim   │ [   ] [   ] [   ] [   ]   –     –     –     –        │
│  White    Reg    │ [   ] [   ] [   ] [ 1 ] [   ] [   ] [   ] [   ]      │
├────────────────────────────────────────────────────────────────────────┤
│                                                    [ Add to cart ]      │
└────────────────────────────────────────────────────────────────────────┘
```

- Row label columns = one per axis 2..n (here Colour, Fit). With 5 axes there are 4 label columns.
- `–` = no combination exists for that cell. Not enterable, not submittable.
- Stock / price / ETA render per cell exactly as 4.6 does, under the same settings (`ShowProductStock`,
  `ShowProductAttributesPrice`, `BulkorderingShowETA`).

Degenerate cases, all the same code path:

| Axes | Layout |
|---|---|
| 0 (no attributes) | one quantity box |
| 1 | one row, one column per value |
| 2 | the classic Size × Colour grid |
| 3–5 | as above, with 2–4 row-label columns |

#### The card's shape, and three rules that are not obvious (folded in 2026-09-05)

Settled in P9 and recorded here because the working document that produced them has been consumed. All three
exist as comments at the point of use; this is the reasoning behind them, which a comment has no room for.

**The card is two columns: image left, everything else right.** The product text and the grid stack *inside*
the right column, so the grid gets a full column rather than a third of the row, and the image stays one clean
column down the page — nop's list view does the same thing with a fixed picture column
(`Themes/DefaultClean/Content/css/styles.css:6796`). An earlier attempt put the product header full-width *above* the
grid; the image then became a tall column with the header floating beside it and the grid starting under both,
which read as two unrelated things.

**`flex-basis: 0`, never `auto`, on any column that contains the grid.** With `auto`, a flex item's
hypothetical main size is its *content* width, and a wide table makes that enormous — so the browser breaks
the line and drops the whole product column below the category tree. `min-width: 0` does not prevent it:
line-breaking reads the basis, not the shrink floor. This applies to `.bulk-order-main` and to
`.bulk-order-product .product-body`.
**And it has a second edge**: an item that cannot wrap also cannot wrap when there is genuinely no room, so a
layout relying on this rule needs an explicit narrow-viewport breakpoint or the side column eats the screen.
That half was specified in P9 and lost for a fortnight, which is how it comes to be written down here.

**The grid's width is ours, not the theme's.** Both themes carry a global `table { width: 100% }`
(`CW.Standard/Content/css/styles.css:101`, in the 4.90 source — the CW themes are not yet in this repository). `.grid-table` states its own width so a theme dropping that rule
cannot silently collapse every grid to content width.

**Copying nop's class names means inheriting nop's responsive assumptions.** The bulk screen deliberately
reuses nop's own class names so a theme change improves it rather than breaking it (`NOP46` D13) — and the
same decision means inheriting rules written for a page shape this screen does not have. Found 2026-09-05:
`CW.Standard` hides `.block-category-navigation` outright below 1000px (`styles.css:7087`), because on nop's
storefront the header's CATEGORIES menu replaces it. On the bulk screen nothing replaces it, and that header
menu navigates to the catalogue rather than filtering this page, so the customer simply lost category
navigation on every narrow viewport. The block's list is hidden too (`:1490`) — the theme's mobile pattern is
an accordion whose opener ships with a *different* theme.
The rule this produces: where the theme's mobile treatment of a borrowed class assumes a replacement control
that this page does not have, restore it **inside `.bulk-order-side`** rather than editing the theme, and
restore whatever the theme's own desktop breakpoint restores — not just visibility. And check that any
behaviour being relied on is delegated: this column is replaced wholesale on every filter change, so a
handler bound at page load is gone after the first one.

**A wide grid scrolls; it must never wrap.** The grid is a *matrix*: a cell means something only at the
intersection of its column header and its row label. Wrap the columns onto a second line and a quantity box is
no longer under a size, so the customer cannot tell what they are typing into — which makes wrapping the one
option that is never available here, however tempting it looks next to a horizontal scrollbar. The full-width
card takes ordinary products below the scroll threshold; beyond that there are exactly two honest answers, and
the first is built: **freeze the row-label column** so it stays visible while the rest scrolls
(`position: sticky` on `th.row-axis, th.row-label`), and **band a very long axis into sections with the header
repeated**, which is not built and has had no product that needs it.

### 4.3 Request state contract

Written after P2, where five separate defects turned out to be one unanswered question: *who owns the request
state?* The answer, in five rules. **P3 adds pin state and typed quantities to this same request — they follow
these rules or they become a sixth place to keep state.**

1. **The DOM owns control state.** Every control contributes its own value at request time; nothing is mirrored
   into a JavaScript state object. This is also how nop's own catalogue works
   (`wwwroot/js/public.catalogproducts.js` collects each parameter from its control through a `before` event).
   The only JS-side state is a request token used to discard out-of-order responses.
2. **The server normalises once**, in `BulkOrderModelFactory.PrepareItemsModelAsync`, and everything else is
   rendered from the normalised value: filter mode, sorting, order id, category id, page size, page number,
   view mode. A value that cannot be honoured is replaced, never echoed — an unknown filter falls back to
   All products, a page past the end lands on the last page, a page size the store does not offer falls back
   to the default, a category that is not in the tree falls back to all categories.
3. **The first render is server-side.** The page shell renders the items partial inline; JS takes over only on
   change. One request per page view, no empty first paint, and the page number in the URL survives a refresh.
   Without JavaScript the first render and the category links work; the sort, page-size and filter selects,
   the view toggles, the pager, the pins and both add buttons do not (review F30 — not worth a form-post
   fallback).
4. **Anything whose content depends on the request state lives inside the swapped region.** The category tree
   depends on the filter, so it is swapped with the list (`_Content.cshtml` = tree + rows). A fragment left
   outside the swap goes stale silently.
5. **The URL is written from the same object that builds the request**, with `replaceState`, so a refresh
   reproduces the state exactly.

**Category selection is a control, not a page.** In nop a category *is* a page — its own URL, title, template —
so nop's category links carry no state and discarding sort and page size when you click one is normal. Bulk
ordering is one screen with a control bar, and the category is the sixth control: its links are AJAX (with a
real `href` kept for middle-click and no-JS), and sort, page size and view mode survive the change. This is a
deliberate divergence from nop, not an oversight.

**Rules the P2 queries settled**, recorded here because P3 inherits them:

| Rule | Detail |
|---|---|
| One query method, three filter modes | `BulkOrderService.SearchProductsAsync` owns the base predicate, the three visibility constraints, category, sorting and paging. The filter mode only supplies a product-id restriction. Two query paths diverge — `tasks/lessons-4.9.md` #11. |
| Cancelled orders count for nothing | `CountedOrderStatusIds` = Pending, Processing, Complete. Gates the order picker, "recently ordered" and the best-sellers report. 4.6 filtered on `!o.Deleted` alone in all three, and nop's `BestSellersReportAsync` defaults to every status — a deliberate divergence from both. |
| Orders are scoped to the logged-in customer | Not to the packname in context. 4.6 parity, verified in its source. The packname-scoped variant is parked as **F17** — it needs the packname id in the category cache key, so it is not a one-line change. |
| A selected order must belong to the customer | `ProductIdsOfOrder(orderId, customerId)`. Without it, `?OrderId=` lists a stranger's order contents. |
| Category filtering covers the whole subtree | `ICategoryService.GetChildCategoryIdsAsync`, not 4.6's "category + direct children" — the tree renders unbounded depth, so the 4.6 rule silently hid third-level products. |
| Sorting is a local enum | `BulkOrderSorting`, because nop's `ProductSortingEnum` has no SKU option and dropping SKU would regress 4.6 parity. Values match nop's where they overlap, and `CatalogSettings.ProductSortingEnumDisabled` is honoured. |
| The cart is resolved once per request | Never inside the per-product loop (`NOP46` D15). With a packname in context it is that packname's cart. |

---

## 5. Attribute classification

Runs once per product, per pin-state. Pure in-memory over already-loaded data.

### 5.1 Eligibility

A mapping is **pin-only** if any of:

1. `IsNonCombinable()` — `AttributeControlType` is `TextBox`, `MultilineTextbox`, `Datepicker` or `FileUpload`.
   Source: `Nop.Services/Catalog/ProductAttributeExtensions.cs:75`, which is `!ShouldHaveValues()` (`:15`, `:88`).
   These have no discrete values to lay out.
2. `AttributeControlType` is `Checkboxes` or `ReadonlyCheckboxes`. A cell is *one value*; a checkbox selection
   is a *set*. This is what keeps a 20-checkbox attribute out of the grid.
3. The product **has** combinations and this mapping appears in **no** signature. Putting it on an axis would
   make every cell key miss (§6).
4. It has zero authorised values.

Otherwise it is **axis-eligible**.

> Rule 3 is skipped when the product has no combinations at all — otherwise everything would be pin-only and
> D5 could not hold.

### 5.2 Ordering (D3)

```
ordered = [ eligible attributes named in BulkOrderSettings.AxisAttributeIds, in that order ]
        + [ remaining eligible attributes, ordered by ProductAttributeMapping.DisplayOrder, then Id ]
```

A configured attribute that the product does not have is skipped — it does not consume a slot and it is not
pinned, because it does not exist for that product.

Worked example. Setting = `[Size, Colour, Sleeve, Logo]`. Product has Colour, Logo, Material (no Size, no
Sleeve). Result: axis 1 = **Colour**, axis 2 = **Logo**, axis 3 = **Material**. Material is not in the setting
and still becomes an axis, because slots remain and it is eligible.

### 5.3 Axis / pin split

```
axes   = ordered.Take(min(MaxAxisCount, ordered.Count))       MaxAxisCount default 5
pinned = ordered.Skip(axes.Count) + all pin-only attributes
```

### 5.4 Cell budget

After the grid is built (§7), if `rows × columns > MaxCellsPerProduct` (default 400):

```
while (cells > budget && axes.Count > 1)
    demote the LAST axis to pinned, rebuild
```

A demoted axis needs a pin value to keep the grid meaningful, so pins default per §8.1. When one axis alone
still exceeds the budget the grid renders anyway — a single attribute with 500 values is a data problem, not a
layout problem — and the card shows a notice. The card also tells the customer which attribute was demoted:
*"Showing Size × Colour. Choose Fit above."*

### 5.5 Ordering of values

Columns and row tuples are ordered by `ProductAttributeValue.DisplayOrder`, then `Id`. Only values that appear
in at least one matching combination are rendered, so an all-dash column never appears.

---

## 6. Signatures and combination matching

### 6.1 Why signatures exist

`ProductAttributeParser.AreProductAttributesEqualAsync` (`Nop.Services/Catalog/ProductAttributeParser.cs:647`)
matches by **set equality** on combinable attributes — `if (attributes1.Count != attributes2.Count) return false`
(`:657`). A combination row therefore matches only a selection built from *exactly* the attributes that row was
built from. That set is its **signature**.

### 6.2 Mixed signatures are normal, not an anomaly

nop's own generator produces them. `GenerateAllCombinationsAsync` (`:798`) calls
`CreateCombination(allProductAttributeMappings)` (`:59`), which is a **power set** —
`for (i = 1; i < 2^n; i++)` over the mappings — so it generates rows for every *subset* of attributes.
`GenerateAttributeCombinationsAsync` (`Presentation/Nop.Web/Areas/Admin/Controllers/ProductController.cs`) then
filters each generated XML through `GetShoppingCartItemAttributeWarningsAsync`, which rejects a subset only
when it omits a **required** attribute (`Nop.Services/Orders/ShoppingCartService.cs:868`).

Consequence:

- every combinable attribute required → one signature per product;
- any **optional** combinable attribute → rows both with and without it, i.e. mixed signatures, by construction.

Both are legitimate purchasable variants: a `{Colour, Size}` row means *"no Fit chosen"*.

### 6.3 The rule — the pin state selects the signature

```
cellKey(cell) = { axis selections for this cell }
              ∪ { pinned selections that have a value }
              restricted to combinable mappings
```

Look up by exact set equality on that key. An optional pinned attribute left blank drops out of the key, and
the shorter-signature rows are what match. Choose a value for it and the longer-signature rows match instead.

No "most common signature" / "largest signature" / "refuse the grid" rule is needed, and the behaviour is
identical to what nop's own cart lookup would do.

### 6.4 The combination index

Per product, parse each combination's `AttributesXml` **once** into:

```csharp
readonly record struct CombinationKey(ImmutableArray<(int MappingId, ImmutableArray<int> ValueIds)> Parts);
// Parts sorted by MappingId and MERGED by MappingId; ValueIds sorted ascending; value QUANTITY ignored.
```

Value quantity is ignored to match nop: `FindProductAttributeCombinationAsync` (`:774`) calls
`AreProductAttributesEqualAsync` with the default `ignoreQuantity: true` (`:647`).

Parts are **merged by mapping id** for the same reason. `ParseValues` (`:507`–`:545`, the loop at `:523`–`:537`) iterates *every*
`ProductAttribute` node and appends the values of each one whose `ID` matches, so two nodes sharing an `ID` are
one attribute holding the union of their values. nop's own writer never emits duplicates, but imported or
hand-edited XML can, and an unmerged key would match nothing. Merging also makes `Parts.Length` the count of
distinct attributes, which is what lets a signature test start with a length comparison instead of building a
set per candidate.

Index: `Dictionary<CombinationKey, ProductAttributeCombination>`. Cell lookup is O(1). 4.6 re-parsed every
combination of the product **per cell** (`NOP46` reference §20).

---

## 7. Building the grid

### 7.1 Inputs

Product; authorised mappings; authorised values per mapping; the combination index; the axis/pin split; the
current pin selections.

### 7.2 Target signature

```
target = axes.MappingIds ∪ pinnedWithValue.MappingIds   (combinable only)
matching = combinations whose signature == target
```

### 7.3 Rows and columns

```
columns  = distinct axis-1 values appearing in matching, ordered by value DisplayOrder
rowTuples= distinct (axis2..axisN) value tuples appearing in matching, ordered by each axis's DisplayOrder
cell(r,c)= index.TryGetValue(cellKey(r, c))  ->  combination or dash
```

Row count is bounded by `matching.Count`, never by the value cross-product. That is the whole performance
claim: **cells scale with real SKUs, not with attribute count**.

**How P3 actually builds the rows, and why it is still bounded.** `Compose` generates the cross-product of the
axis 2..n values *that appear in `matching`*, then drops every row whose cells are all dashes. The surviving
rows are exactly the distinct tuples of `matching`, so the rendered grid matches this section — but the *build*
touches more tuples than it keeps, by the ratio between the cross-product of in-use values and the real tuple
count. On the measured catalogue that ratio is 1 for 93% of products (two attributes, one row axis) and
single-digit for the tail, against a largest real combination set of 108, so the absolute cost is small.
Deriving the tuples from `matching` directly removes the ratio entirely; it is not done because the rewrite
carries more risk than the saving is worth today. **Revisit if** profiling shows grid build time mattering, or
a catalogue arrives whose axis attributes carry many more values than there are combinations.

### 7.4 When a missing combination closes a cell (D5, revised)

**Superseded.** D5 originally said the fallback applies when a product has *no combinations*. That was the
wrong question. A cell must be closed when, and only when, **nop itself would refuse the sale**
(`ShoppingCartService.cs:490–512`):

```
restricts = product.ManageInventoryMethod == ManageStockByAttributes
            && product.AllowAddingOnlyExistingAttributeCombinations

cell exists = a combination matched  ||  !restricts
```

Both conditions, or the `case` is never reached / the miss produces no warning. Having combination rows is
**not** one of them. Measured on the live client catalogue, of the 4,343 products that *do* have combination
rows: 4,303 are `ManageInventoryMethodId = 0`, 39 are method 2 with the flag off, 1 is method 0 with the flag
on — **zero restrict**. Keying the grid on "has combinations" therefore dashed purchasable cells on the entire
catalogue. The grid is a front end for nop's rules; it may not invent stricter ones.

When `restricts` is false:

```
columns   = all authorised axis-1 values          (what nop's own product page offers)
rowTuples = cross-product of authorised values of axes 2..N
every cell exists, no dashes; a matched combination still supplies the Sku
```

subject to the same cell budget (§5.4), which is what stops the cross-product exploding. **The budget is
applied to the projected size before the cross-product is materialised** — the projection is just the product
of the axis value counts and is known without building anything. Composing first and demoting afterwards means
building the explosion in order to discover it: for a 15-attribute product that is six figures of cells, per
rebuild. This projection path now applies to nearly every product, not to the 13% §12.0 measured as having no
combinations at all.

When `restricts` is true, §7.3 stands unchanged: rows and columns come from `matching`, in-use values only, and
a missing combination is a dash.

The lookup runs either way — it is where the Sku comes from. Only the meaning of a **miss** depends on
`restricts`. Where no combination matches, stock and price come from product level, so those columns repeat;
that is correct, not a bug.

§5.1 rule 3 (a mapping in no signature is pin-only) is unaffected: it chooses axis versus pin, never
availability. The attribute is rendered, selectable and submitted either way.

### 7.5 Worked example

7-attribute garment: Colour (10, colour squares), Size (12), Fit (3), Sleeve (2), Embroidery (20 checkboxes),
Logo (4), Name-to-print (textbox). Merchant has 280 combinations over Colour × Size × Fit.

| | Value |
|---|---|
| Pin-only by type | Embroidery (checkboxes), Name-to-print (textbox) |
| Pin-only by signature | Sleeve, Logo (in no signature) |
| Axis-eligible | Colour, Size, Fit |
| Axes (setting `[Size, Colour, …]`) | 1 = Size (columns), 2 = Colour, 3 = Fit |
| Columns | 12 |
| Rows | distinct real (Colour, Fit) tuples ≈ 24 |
| Quantity boxes | 280, plus ~8 dashes |
| 4.6 would have rendered | 360 boxes, 80 of them for SKUs that do not exist |

---

## 8. Pinned attributes

A pin is one selection applied to **every line** of a card. Four questions have to be answered about each pin,
and P3 proved they are genuinely four rather than one — three of that phase's corrections came from conflating
them:

| Question | Answered by | Used for |
|---|---|---|
| What is selected? | `SelectedValueIds` | rendering the control |
| What would be selected with no customer input? | `DefaultValueIds` | so the client sends only what changed (§8.4) |
| Does it take part in combination matching? | signature membership (§8.3) | the target signature and the cell lookup |
| Does it reach the cart line? | any pin holding a value | the cell's submitted selection |

### 8.1 A pin holds a set, not a value

`Checkboxes` and `ReadonlyCheckboxes` are pin-only precisely because a checkbox selection is a **set** (§5.1
rule 2). A pin that could hold only one value would therefore be unable to express the very attribute type that
sent it to the strip, and the 17 products above five attributes — the ones 4.6 dropped silently — are exactly
where that bites. So the pin state is `IList<int>` throughout:

| Control type | Holds |
|---|---|
| `DropdownList`, `RadioList`, `ColorSquares`, `ImageSquares` | zero or one value |
| `Checkboxes` | zero or more |
| `ReadonlyCheckboxes` | always **all** values; nop renders them checked and disabled, so a customer selection is ignored rather than trusted |
| `TextBox`, `MultilineTextbox`, `Datepicker`, `FileUpload` | nothing — no value ids exist (§5.1 rule 1). P3 renders these read-only; P6 decides what a per-card text value means |

`CombinationKey` already modelled this: a key part's `ValueIds` is an array. The single-valued pin pipeline was
the outlier, not the design.

### 8.2 Defaults, and what overrides them

| Kind | Default |
|---|---|
| Required, has values | every `IsPreSelected` value, else the first by `DisplayOrder` |
| Optional, has values | **nothing selected** — a valid signature (§6.3) |
| Optional, **demoted from an axis by the cell budget**, product **restricts** (§7.4) | as *required* — *not* "nothing" |
| Optional, **demoted from an axis by the cell budget**, product does **not** restrict | "nothing" — the ordinary optional rule |
| Non-combinable (text/date/file) | empty |

Defaulting required pins is what guarantees the grid renders on first paint rather than "choose Fit first".

The demoted carve-out is **conditional on §7.4**, and the reason is easy to lose. It exists only because a
demoted axis appears in every combination signature, so leaving it out of the cell key matches nothing and the
grid renders empty. That failure requires a miss to *close* a cell — which happens only where the product
restricts. On an unrestricted product every cell renders whether the key matches or not, so forcing a value
rescues nothing and instead writes an attribute the customer never chose onto every submitted line — plus its
`PriceAdjustment` where the value carries one.

Measured on the client catalogue: **no product restricts**, so unconditioned the carve-out fires only where it
is wrong and never where it was designed to help. Twelve products (`Polo - * Design`, ids 3140 and 3504–3514)
demote three optional Back Logo attributes each; every one is `IsRequired = 0` with **no** preselected value,
so `ResolvePinValues` falls through to `values[0]` and the first back-logo position is applied to all 120
cells. None of those values carries a price adjustment, so the cost is a wrong **manufacturing instruction**,
not a wrong price — three embroidery positions on a garment nobody asked to have embroidered.

**The demoted row is not tidiness — without it the grid renders empty.** A demoted attribute was axis-eligible,
so by §5.1 rule 3 it appears in every matching combination's signature. Leaving it out of the cell key makes the
target shorter than any real combination's, `matching` comes back empty, and the customer sees "no variants"
beside a notice telling them to choose the very attribute that was demoted.

**An explicit clear is not the same as no opinion.** A request that mentions a pin with no values means the
customer emptied it, and that is honoured — including for a demoted or required pin, where the result is an
empty grid until they choose again. A request that does not mention the pin at all means no opinion, and the
default above applies. On the wire the difference is the `mappingId:0` sentinel.

### 8.3 Matching versus submission — two lists, not one

A pin can hold a value that no combination has ever heard of: §5.1 rule 3 pins an attribute *because* it
appears in no signature, and an embroidery choice on a garment whose combinations cover only colour and size is
the normal case, not an edge case. Such a pin must reach the cart and must **not** reach the signature:

- **Signature pins** — those whose mapping appears in at least one combination signature. These join the target
  signature and the cell lookup key. Including a pin the combinations never mention makes the target longer
  than any real combination's, so nothing matches and the grid goes blank.
- **All pins** — every pin holding a value. These form the cell's submitted selection, so the customer's choice
  lands on the cart line whether or not it participated in matching.

Where the product has no combinations at all (§7.4) the two lists are the same, because there is no signature
to be excluded from.

### 8.4 Changing a pin

Re-renders **that card only**, via request 3 (§4.1). The pin state travels in the query string, so the render is
stateless and the DOM stays the single source of truth (§4.3 rule 1). Quantities already typed in that card are
re-applied after the swap where the cell still exists; cells that no longer exist lose their quantity and the
card says so rather than discarding it silently.

The **list** request carries pin state too, or the first paging click would throw away every pin on the page.
That request covers every product on the page at once, so it sends only pins whose selection differs from
`DefaultValueIds` — at a page size of 100 with two required pins per product, sending all of them would exceed
the query-string limit and the request would fail outright.

**Per-product state is page-scoped, and that is a consequence of rule 1, not an oversight.** Added 2026-09-02
after check 4.9 was written expecting the opposite. `collectPins` reads the DOM, and the DOM holds one page.
So a pin set on page 1 rides along on every re-render that keeps that product on screen — a sort change, a
page-size change, a category click, a filter change — and is gone the moment you page away, because the
control it lived in no longer exists. Coming back to page 1 renders defaults.

That is the correct P3 behaviour and it is **consistent**: typed quantities are page-scoped for exactly the
same reason. Pins surviving a round trip while quantities did not would be worse than both being reset — the
customer would come back to a page that looked configured but was empty.

The real question this exposes is **what a multi-page bulk order means**, and it belongs to P6, where
quantities start to matter because there is finally something to submit. Two options, neither free:

- **Accumulate in the URL.** The URL is already the state carrier (rule 5) and is not a JS state object, so
  it does not break rule 1. But pins would pile up across pages and hit the same query-string ceiling the
  paragraph above is about, and a pin for a product the customer has long since left would keep being sent.
- **Keep a server-side working set** keyed by customer and packname. That is really a draft order, which is
  arguably what a multi-page bulk order has been all along — and is a much larger decision than paging.

Until then: page-scoped, stated here so nobody re-derives it from a lost tick.

**A cell has two keys, and confusing them loses the customer's typing.** Added 2026-09-02 after check 4.4
failed.

- **Selection** — axis values *plus* every pin holding a value. It is what the cell will submit in P6, and
  it legitimately changes whenever a pin changes.
- **Axis key** — the axis coordinates alone. It is what identifies the cell to the *customer*: "M / Navy"
  is the same cell before and after they touch a pin.

Quantity carry-over across a pin change must match on the **axis key**. Matching on `Selection` means every
cell of the card looks new after any pin change, so every typed quantity is discarded — which is what the
first implementation did, and the `quantities-lost` marker made it look deliberate.

`_Grid.cshtml` already carried this reasoning for the single-cell input — *"a cell's key legitimately
changes with the pins; this one must not"* — and it was applied there and nowhere else. The general rule
belongs here so the next reader does not have to rediscover it from a lost quantity.

### 8.5 What pins cost

A pinned attribute contributes **one selection per card**, so ordering Regular-M and Slim-M in one go requires
Fit to be an axis. That is the real trade-off of the design and is why D3 makes the axis order configurable —
the attributes people mix within one order belong on axes. Note this is about *variation across lines*, not
about set-valued attributes: a checkbox pin holding three values is still one selection applied to every line.

---

## 9. Conditional attributes (D4)

`ProductAttributeMapping.ConditionAttributeXml` makes an attribute visible only when another attribute has
given values.

**Cost.** `IsConditionMetAsync` (`Nop.Services/Catalog/ProductAttributeParser.cs`) reads
`pam.ConditionAttributeXml` — already loaded on the mapping — and compares parsed strings. Its only service call
is `ParseProductAttributeMappingsAsync(conditionAttributeXml)` → one `GetProductAttributeMappingByIdAsync` for
the depended-on attribute. So the cost is **one evaluation per conditional attribute, per product, per
pin-state**.

4.6's cost was not the feature but the placement: it called `IsConditionMetAsync` inside
`AddProductAttributeToXMLAsync`, which runs 1–3 times *per cell* — over a thousand evaluations for one
360-cell product (`NOP46` reference §8.1).

**Rule.** Evaluate all conditions once, after the pin selections are known, against an attributes-XML built
from the pins alone:

1. A **pinned** attribute whose condition is not met is hidden and contributes nothing to the cell key.
2. An **axis** attribute whose condition is not met is dropped from the axes before the grid is built; the
   remaining axes shift up and another eligible attribute may take the freed slot.
3. When a condition crosses between an axis and a pin, **§9.1 governs** — see the four-case rule set below.

**Superseded 2026-08-28.** Two earlier drafts of rule 3 are gone and must not be reintroduced: *"such an
attribute is forced pin-only"* (silent about which one) and *"the depended-on attribute **A** is forced
pin-only"*. The second is the one to watch for, because it reads as settled and is quoted in older notes: it
would take Colour off the axes for any product where something depends on Colour, which is the natural axis and
the common case. **§9.1 keeps the axis in all four cases** and pays for it with one cheap evaluation per card
plus one per submitted line. Where the two disagree, §9.1 wins.

**Measured on a client database (2026-08-28).** 401 mappings carry a `ConditionAttributeXml`, and the
depended-on attributes were read rather than guessed:

| Conditional attribute | Control type | Depends on | Mappings | Products |
|---|---|---|---|---|
| `Print Text` | TextBox | `Print Name` (dropdown) | 202 | 202 |
| `Back Logo 1…12` | ImageSquares | `Col. 3 Tone` (dropdown) | 14 each | 14 |
| `Print Branding` | Dropdown | `Print Name` | 4 | 4 |
| `Logo Position`, `Embroidery Size`, `Logo Options` | Radio / ImageSquares / TextBox | `Embroidery` | 1–2 each | 1–2 |
| `Collar Text` | TextBox | `Collar Print` (ImageSquares) | 2 | 2 |
| `Col. 2 Tone`, `Col. 3 Tone`, `Col. Solid`, `Ladies Size 4-26` | Dropdown | `Print Branding` | 1 each | 1 |
| `Col. Solid` | Dropdown | **`Sizes XS-XL`** | 1 | 1 |

> **These numbers are one client, one backup, a year old.** A second client is expected whose data is unknown.
> They are used here to confirm that the *mechanisms* are right and the defaults sane — **never** to tune the
> design to one dataset. Any rule that degrades depending on how a client happened to configure conditions is
> the wrong rule, however well it scores on this data.

The last two rows are the ones that matter: a **colour** attribute is depended on by twelve logo attributes,
and on one product a **colour depends on a size**. Under the earlier draft of rule 3 ("force the depended-on
attribute pin-only") those products would lose colour — and on another client's data, could lose it
everywhere. That is a rule whose cost is set by someone else's configuration, so it has been replaced.

### 9.1 The rule set (revised 2026-08-28)

Let **B** be an attribute carrying a condition, **A** the attribute it depends on. Four cases, and the axis
survives in all of them:

| # | B | A | Rule | Cost |
|---|---|---|---|---|
| 1 | pinned | pinned | Evaluate once per card from the pin state. When unmet, B is **rendered disabled** naming what unlocks it — not hidden. | none |
| 2 | axis | pinned | Drop B from the axes when unmet; the remaining axes shift up and another eligible attribute may take the freed slot. B then re-enters as a pin, and **case 1 applies**. | none |
| 3 | pinned | **axis** | Show B if **at least one rendered A value** satisfies the condition, marked as conditional. At **add-to-cart**, B's value is written only onto lines whose A value satisfies it. | none |
| 4 | axis | **axis** | Demote **B** to pinned, then case 3 applies. Demote B, never A — A is the attribute something else depends on, and is typically the primary (colour, size). | one axis, on the dependent attribute only |

**An unmet condition shows itself (added 2026-09-05, P9 I8).** Cases 1 and 2 used to make the attribute
disappear: on `ZZT-23`, unticking *Embroidery* removed *Print style* from the strip with no explanation, and on
`ZZT-24` an entire axis came and went. Both now render the control **disabled**, with *"Choose Embroidery
first"* beside it.

**Case 2 needs no separate mechanism.** Dropping B from the axes puts it back in the pin list on the next
pass, where the loop sees it as case 1 — the same collapse case 4 already makes into case 3. One rule, one
resource, and the customer sees the attribute rather than a sentence about its absence.

**This is nop's data handling with different presentation, and the divergence is deliberate.** nop renders its
own conditional fields always and hides them with an inline `display: none`
(`Views/Product/_ProductAttributes.cshtml:19,34`), then strips them server-side in
`ProductAttributeParser.GetProductAttributesXmlAsync:308–313`. We keep the control in the DOM and strip it in the builder — the same two
moves — but we do not hide it, because on nop's product page the attributes are a short vertical list where a
field appearing is noticeable, while here the strip is one row on one card of fifteen and a control leaving it
is not noticed at all.

**Two properties hold this together, and both are load-bearing:**

- **A blocked pin contributes nothing to any cell key.** `BuildAsync` excludes it from `allPins`, and that
  exclusion is the only thing that does so — nop would strip it inside `ParseProductAttributesAsync`, which P6
  never goes through (§12.4). Without it, an attribute the customer cannot currently choose, and its price
  adjustment, would land on every order line.
- **A blocked pin holds nothing, as far as anything depending on it is concerned.** In a chain — C depends on
  B, B depends on A — C must not read the value B is merely *remembering*. Blocking a mapping therefore
  restarts the pass, so nothing evaluated earlier in `data.Mappings` order keeps a stale answer.

**F19 closes as a side effect.** The control stays in the DOM, so its value travels with `collectPins` and
comes back on the next render: untick, re-tick, and the pin returns at what the customer chose rather than at
its default. That is also precisely what creates the chain hazard above — the remembered value is real state,
and the second property is what stops it being applied.

**Truly hidden is still a state**, and one case keeps it: a case-3 condition that **no** rendered axis value
can satisfy — a two-value condition, typically, since a cell offers one value and nop compares by count first.
Nothing the customer does on that card would reveal it, so a disabled control saying "choose A first" would be
a lie. nop never shows it either.

**Nothing in nop will strip an unmet attribute for us.** Case 3's *"written only onto lines whose A value
satisfies it"* is not a nicety — it is the only thing that does the job. nop strips unmet-condition
attributes inside `ProductAttributeParser.ParseProductAttributesAsync(Product, IFormCollection, …)` (in
`GetProductAttributesXmlAsync`, `:308–313`), which is
its **form-post** path; P6 composes `AttributesXml` itself and never goes through it, and
`GetShoppingCartItemAttributeWarningsAsync` filters its *mapping* list by condition but never the
*submitted* attributes. Get case 3 wrong and the order line silently carries a value, and its price
adjustment, that nop's own product page would have deleted. See §12.4 for why this makes P4 a hard
prerequisite of P6.

**Why case 3 works and costs nothing.** The expensive place to evaluate a condition is per cell at render time
— that is where 4.6's cost came from. Case 3 evaluates it twice, in cheap places: once per card at render
("is this pin relevant to anything on the grid?"), and once per submitted **line** at add-to-cart, where there
are a handful of lines rather than hundreds of cells.

Applied to the measured data: the 202-product `Print Text ← Print Name` case is **case 1** (both pinned — a
TextBox is pin-only by §5.1, and `Print Name` is not in the axis settings), so it costs nothing. The
`Back Logo N ← Col. 3 Tone` products are **case 3** — colour stays an axis, the logo pins are marked
conditional. Nothing in this dataset reaches case 4.

**UI obligation for case 3.** A pin that applies to only part of the grid must say so — the customer is
entering one value that will land on some lines and not others. A short note beside the control ("Applies to
Navy only") is enough; silently dropping the value on non-matching lines is not acceptable.

**The note names exactly one value, never a list.** An earlier draft of this paragraph read *"applies to Navy
and Black only"*, which cannot happen. `IsConditionMetAsync` compares the required set to the selected set by
**count first** (`ProductAttributeParser.cs:743`), and an axis cell contributes exactly one value for its
attribute — so a condition naming two values is unmet for every cell, and a condition naming one is met only by
that one. Case 3 therefore always resolves to a single A value. The corollary is the next line.

**A condition requiring two or more values can never be satisfied by an axis attribute, and its dependent is
correctly never shown** — on nop's own product page too, for the same reason. This is nop's rule, not ours;
P4's check 4-7 exists so that nobody later "fixes" the count comparison into a `Contains`.

### 9.2 Five behaviours of `IsConditionMetAsync`, read from source (2026-09-02, during P4)

Each of these changes what the grid must do, and each was got wrong at least once from memory:

| # | Behaviour | Source | Consequence |
|---|---|---|---|
| 1 | Empty `ConditionAttributeXml` returns **null**, not false | `:725–727` | Three states — no condition / met / unmet. A boolean loses the first. |
| 2 | It takes the first condition element whose id **loads**, not the first element | `:731`, `:438–443` | Ids that no longer resolve are skipped, not fatal. |
| 3 | When **no** id loads it returns **true** | `:732–733` | A condition pointing at a **deleted** attribute shows its dependent, unconditionally. `DeleteProductAttributeMappingAsync` is a bare row delete (`ProductAttributeService.cs:183`) and nothing scrubs the sibling conditions, so this is a normal live state. Hiding there would be stricter than the store. |
| 4 | Empty `<Value></Value>` entries are stripped from the required list | `:735–741` | Admin writes exactly that for "nothing selected" (`ProductController.cs:676–685`), so an **empty required list is a real condition** — met only when A holds nothing. It is not the same as having no condition. |
| 5 | Cart validation drops unmet-condition attributes **before** checking `IsRequired` | `ShoppingCartService.cs:853–861` | Hiding a *required* attribute whose condition is unmet is correct; the cart will not then demand it. |

Two more facts settled at the same time, both about what the *data* looks like rather than the comparison:

- **The condition editor only ever offers this product's other mappings**, filtered by `CanBeUsedAsCondition()`
  (`ProductModelFactory.cs:337–341`). So a condition id that is not one of this product's mappings means
  *deleted*, and behaviour 3 applies. It also imposes **no display-order rule**: A may come after B.
- **`GenerateAllCombinationsAsync` strips unmet-condition attributes from each generated combination**
  (`ProductAttributeParser.cs:875–884`). A product with a condition therefore has combinations of **mixed
  signature** — some carrying the conditional attribute, some not. The per-cell lookup key must have the same
  shape or a restricted product dashes cells the store would sell. P4 check 4-9.

**One deliberate divergence, stated rather than hidden.** `ParseCondition` treats a condition whose ids are
none of this product's mappings as **no condition**, where nop returns `true` (met). The rendered result is
identical — the attribute shows and behaves unconditionally — and the only way to reach a difference would be a
condition referencing a *live* mapping on a *different* product, which the condition editor cannot produce
(`ProductModelFactory.cs:337–341`) and which would cost a query per mapping to detect. If that ever appears in
a client's data it is corruption, and the answer is to fix the data.

### 9.3 The column scan's target signature varies by axis value (found in P4 acceptance, 2026-09-02)

A restricted product renders a column or row only for axis values that some combination actually carries
(§7.3). That scan compares each combination's signature against **one** target set — the axes plus the pins
that participate in signatures. Conditional attributes break that assumption, and the failure is not subtle:

Because `GenerateAllCombinationsAsync` strips unmet-condition attributes, a conditional product's combinations
have **mixed signatures**. With one fixed target that includes the conditional pin, every axis value that does
*not* satisfy the condition matches nothing — and loses its column **outright**. Not a dash: a missing column,
on cells the store would sell. On the P4 test data, ticking a Logo value on `ZZT-29` made the whole *Sand*
column disappear.

So the scan asks, per combination, *which* signature that combination should have **given the axis values it
itself names** — a conditional pin belongs to the target only where the depended-on axis value satisfies the
condition. The same question the per-cell filter asks, asked of a key instead of a cell. The fast path is
preserved: with no conditional pin in the signature there is one target set for the whole grid, allocated once,
which is every product that existed before P4.

**The general lesson, worth more than the fix.** P4's per-cell filtering was applied to the two places that
obviously needed it — what a cell submits and what a cell looks up — and missed the third, which runs *before*
the cells exist. When a rule stops being uniform across a grid, every place that assumed uniformity has to be
found, not just the ones the feature description mentions.

## 10. Data loading — the performance design

### 10.1 What 4.6 did per cell, and what replaces it

| Per cell | 4.6 | This design |
|---|---|---|
| Compose/parse attributes XML | 1–3 compositions, each with a mapping fetch, a condition check and an ACL check | none — work in `(mappingId, valueId)` tuples; serialise to XML only at add-to-cart |
| Find the combination | `GetProductAttributeCombinationAsync`, re-parsing every combination of the product | it **is** the row; O(1) dictionary lookup |
| Stock | a service call per cell | `ProductAttributeCombination.StockQuantity` / `.AllowOutOfStockOrders`, already loaded |
| Price | a full `GetUnitPriceAsync` per cell | see §10.3 |
| Quantity in cart | a cart scan per cell | index the cart once per page |

### 10.2 Batched loads, per item request

Flat in the number of cells; ~8 queries regardless of grid size:

1. the paged `Product` query (unchanged from 4.6, see `NOP46` reference §4);
2. `ProductAttributeMapping` for all product ids on the page — one query;
3. `ProductAttributeValue` for those mapping ids — one query;
4. `ProductAttributeCombination` for those product ids — one query;
5. `ProductAttribute` names for the distinct attribute ids — one query;
6. attribute-value pictures, only when `ShowAttributeImages` — one query, models cached as 4.6 already does;
7. the customer's cart lines for those products (packname-aware) — one query, each line's XML parsed once and
   indexed by `CombinationKey`;
8. product-level stock for products without combinations.

> **Decided 2026-08-28: do not batch these — use nop's own accessors.** `GetProductAttributeMappingsByProductIdAsync`,
> `GetProductAttributeValuesAsync` and `GetAllProductAttributeCombinationsAsync` are all `IStaticCacheManager`-backed
> with nop's own `ICacheEventConsumer` invalidation (`Nop.Services/Catalog/ProductAttributeService.cs`, keys
> `NopCatalogDefaults.ProductAttributeMappingsByProductCacheKey`, `…ProductAttributeValuesByAttributeCacheKey`,
> `…ProductAttributeCombinationsByProductCacheKey`). A hand-rolled batch query bypasses that and would need its
> own invalidation — a second store of the same state, which is the shape `tasks/lessons-4.9.md` #22 warns about.
> And once the **grid skeleton cache** (§10.5) is in, each product's attribute data is read once per
> invalidation anyway, so batching would save almost nothing. Items 2, 3 and 4 above therefore become
> per-product cached calls; only items 6, 7 and 8 are genuinely batched.

### 10.3 Pricing

Verified against nop's own `ShoppingCartService.GetUnitPriceAsync` (`Nop.Services/Orders/ShoppingCartService.cs`):

```
cellPrice = combination?.OverriddenPrice.HasValue == true
          ? GetFinalPriceAsync(product, customer, store, overridden: OverriddenPrice, additionalCharge: 0, qty)
          : GetFinalPriceAsync(product, customer, store, additionalCharge: Σ valueAdjustments, qty)
```

`GetProductAttributeValuePriceAdjustmentAsync` (`Nop.Services/Catalog/PriceCalculationService.cs`) depends only
on the **value**, the product price (for percentage adjustments) and the quantity — never on the other selected
values. Therefore:

- compute each value's adjustment **once per value**, not per cell;
- a cell's additional charge is the sum of its selections' adjustments — arithmetic, no service call;
- call `GetFinalPriceAsync` **once per distinct `(overriddenPrice, additionalCharge)` pair** across the product.
  For most products that is one call.

Then tax (`ITaxService.GetProductPriceAsync`) and currency conversion once per distinct price, and the
4.6 price-collapse rules (§8.2 of the reference: `SameAttributesPrice`, `ProductPriceRange`, `TaxSuffix`) applied
unchanged over the resulting set.

`ShoppingCartSettings.GroupTierPricesForDistinctShoppingCartItems` makes the tier quantity depend on what is
already in the cart for that product — resolve that quantity **once per product** from the cart index, not per
cell as 4.6 does.

### 10.4 Availability

Per cell, from data already in hand:

| Inventory method | Source |
|---|---|
| `ManageStockByAttributes` | `combination.StockQuantity`, `combination.AllowOutOfStockOrders` |
| `ManageStock` | product-level `GetTotalStockQuantityAsync`, once per product |
| `DontManageStock` | no stock shown |

The 4.6 defects here are **not** carried over: `ManageStock` products with backorders allowed get stock shown
(`NOP46` D2), and `CallForPrice` is not overwritten after the pre-order decision (`NOP46` D1).

There is no live-stock/ETA provider in `CW.Core`. The ETA columns (`FirstPODate`, `FirstPOQtys`,
`ShowETAInfo`) and the per-warehouse breakdown were Jim2-specific (`NOP46` reference §21). The model keeps the
fields and an `IBulkOrderAvailabilityProvider` seam so a CW stock integration can fill them later; the default
implementation returns nop stock and no ETA.

### 10.4a Product visibility — every constraint, and one 4.6 difference (now closed)

Bulk ordering must show exactly what the rest of the storefront shows. Three constraints apply, and the
third is the one a 4.6 port would silently drop:

| Constraint | Source (`BulkOrderService.ApplyVisibilityAsync`, `Services/BulkOrder/BulkOrderService.cs:102`) |
|---|---|
| Store mapping | `IStoreMappingService.ApplyStoreMapping(query, storeId)` |
| Customer-role ACL | `IAclService.ApplyAcl(query, customer)` |
| **PackName subsidy allocation** | `IPackNameCatalogGate.GetVisibleProductIdsAsync(customer)` (`BulkOrderService.GetAllocatedProductIdsAsync`, `:93`) — restricts when every active subsidy sets `OnlyAllocatedProducts`, shows nothing when the packname's ordering period is closed; `null` means unrestricted |

In 4.6 the third one lived in the **Allowances** plugin (4.6's name for Subsidy), which replaced `IJim2AclService`
with `AllowancesAclService` and overrode `ApplyCustomVisibilityConstraint`
(`NOP46/Nop.Plugin.Jim2.Allowances/Services/Security/AllowancesAclService.cs:318`). The 4.6 bulk-order queries
picked it up implicitly by calling that method. Here the equivalent is `IPackNameCatalogGate`
(`Services/PackNames/PackNameCatalogGate.cs`, `CLAUDE.md` §15.2), which `ProductOverrideService.SearchProductsAsync`,
`AclOverrideService`, the product page and add-to-cart all ask — so **`AllowanceProducts` visibility is not
optional and not a subsidy-screen concern; it is catalogue-wide**.

`BulkOrderService` asks the same gate, with the current customer; it no longer reads `IPackNameContext` and
`ISubsidyOrderingService.GetOnlyAllocatedProductIdsAsync` itself (the gate does, `PackNameCatalogGate.cs:122–123`).

> **Difference from 4.6 — closed.** 4.6's `ApplyProductGroupsConstraint`
> (`AllowancesAclService.cs:106`) had a **second** rule: if `GetAllowancePackNameCanOrderAsync(packName).CanOrder`
> is false, it returns **no products at all**. When this section was written (2026-08-28) the 4.9 catalogue had
> only the `OnlyAllocatedProducts` restriction, and bulk ordering matched the catalogue rather than add the gate on
> one screen. The catalogue gate (subsidy phase 5 Part 2, 2026-10-01, carried into 5.00) added it catalogue-wide:
> `canOrder` comes from `ISubsidyOrderingService.PackNameCanOrderAsync` (`PackNameCatalogGate.cs:122`) and a closed
> ordering period shows nothing — except an exchange replacement, which a closed period never blocks. Bulk ordering
> follows it with no code of its own.

### 10.5 Caching

The **grid skeleton** is everything `BulkOrderGridBuilder.LoadAsync` reads: the product's mappings, the
localized attribute names and prompts, each mapping's values in display order with their picture ids, the
pre-selected ids, every combination parsed once into its `CombinationKey`, the set of mapping ids that appear
in any signature, and the parsed conditions. It depends on the product, the language and
`BulkOrderShowAttributeImages` — nothing else. Everything after `LoadAsync` (`OrderEligible`,
`ResolvePinValues`, the condition loop, the budget loop, `Compose`) is pure in-memory work over that data and
runs per request: it depends on the pin state, the axis settings and the cell budget, and on a 400-cell
product costs less than one localized-string lookup.

```
CW.bulkorder.skeleton.{productId}-{languageId}-{showAttributeImages}
CW.bulkorder.skeleton.{productId}-      (prefix, for the consumers)
```

Cached as a **serializable record** (`BulkOrderSkeleton` in `Domain/BulkOrder/`: lists and `int`s, no
entities, no struct-keyed dictionaries) so it survives a distributed cache; `LoadAsync` becomes "get or build
the skeleton, then hydrate `GridData` from it" — rebuilding the index dictionary from a list of
`(key parts, combinationId, sku)` costs one dictionary insert per combination and no XML.
`RestrictsToExistingCombinations` is read from the `Product` passed in, never from the skeleton, so a flipped
inventory setting is honoured on the next request without an invalidation.

**Why not the composed grid.** Keying the composed grid on a `pinStateHash` is correct but wrong-headed:
`RequiresTextAttribute`, `RequiredTextAttributeName`, `IsBlockedByCondition`, `Pins[].SelectedValueIds`,
`DemotedAxisNames` (a condition case 2/4 demotes, and that depends on pins), `Columns`/`Rows` on a restricted
product and `AppliesToValueName` all vary with pin state — so a checkbox pin with twenty values is up to 2²⁰
entries per product, while the default state is the only one a page render uses.

**Per-value price adjustments are not customer-independent** and are not in the skeleton:
`GetProductAttributeValuePriceAdjustmentAsync(product, value, customer, store, …)` takes the customer, and a
percentage adjustment prices off the customer's price. They live in the per-request pricing service.

**What it saves.** nop's own `IStaticCacheManager` already caches the mapping, value, combination and
localized-property reads, so on a warm cache the skeleton saves the per-combination `XmlDocument` parse (F18),
the per-mapping/per-value loops and their localization lookups, and the condition parse. On a cold cache it
saves those plus the queries. What it does **not** save, and what T21 must count separately: per-request
pricing (`GetFinalPriceAsync` per distinct input, one value-adjustment call per value), the availability
provider, the cart read for tier quantities (since P7 one read per page, `BulkOrderPricingService.GetTierQuantitiesAsync`,
called once by the factory — `Factories/BulkOrderModelFactory.cs:868`; it was one per product before), picture
URLs, and the SeName.

**Invalidation** — `ICacheEventConsumer`s clearing the product's prefix: `ProductAttributeCombination`,
`ProductAttributeMapping`, `ProductAttributeValue` (each carries a product or mapping id; the value one needs
the mapping read to find the product — nop's own `ProductAttributeValueCacheEventConsumer` does the same),
`ProductAttributeValuePicture` (the picture ids are in the skeleton), and — because the skeleton holds
*localized strings* — `ProductAttribute` and `LocalizedProperty` for the key groups `ProductAttribute`,
`ProductAttributeValue`, `ProductAttributeMapping`, which clear the **whole** prefix (an attribute rename
touches every product using it; both are rare admin actions). `Product` is kept too, in
`BulkOrderProductCacheEventConsumer` (one line beside `VisibleCategoryIdsPrefix`): nothing in the skeleton
derives from a product column any more, but the consumer already exists and the cost of a spurious clear is
one rebuild.

Customer-specific layers — final prices, cart quantities, tax, currency, permissions — are computed on top and
are **not** cached.

The skeleton cache was built in P7 (2026-09-08) as described here: `BulkOrderServicesDefaults.SkeletonCacheKey`,
`SkeletonByProductPrefix` and `SkeletonPrefix` (`Services/BulkOrder/BulkOrderServicesDefaults.cs:39–45`), read by
`BulkOrderGridBuilder.LoadAsync` through `BuildSkeletonAsync` and `HydrateAsync`. The seven consumers named above are
in `Services/BulkOrder/Caching/`. The *(none)* value's label (§16 item 9) is a locale resource, not a
`LocalizedProperty`, so no consumer would clear it: it is kept out of the skeleton and resolved per request in
`HydrateAsync` (`BulkOrderGridBuilder.cs:274`, resource `Plugins.CW.Core.BulkOrder.Grid.AxisNone`).

The visible-category cache carries over from 4.6 with the CW prefix, and unlike 4.6 gets cache event
consumers. As built:

```
CW.bulkorder.visiblecategoryids.{filterType}-{storeId}-{customerRoleIds}-{orderId}-{customerId}-{allocationFingerprint}
```

Three things about that key were learned the hard way and must not be undone:

- **`customerId` is included whenever the filter reads orders at all** (Latest ordered) — not only when no specific order is
  selected. A selected order is read through `ProductIdsOfOrder(orderId, customerId)`, so its answer is
  customer-specific too; omitting the customer lets any two customers in the same role share one entry, and
  whoever loads it first decides what the other sees. The query is safe either way; the *cache* is what leaks.
- **`allocationFingerprint` must be deterministic across processes.** `System.HashCode` is seeded per process,
  so with a distributed cache two web servers key the same allocation differently. It is an ordered aggregate
  over the whole allocated set — a count/min/max digest also collides when one id inside the set changes.
- **`null` allocation (unrestricted) and an empty allocation (restricted to nothing) are different** and must
  produce different keys.
- **`orderId` is in the key only for Latest ordered, and only for one of the customer's own latest orders**
  (5.00, part 5h): any other filter keys it as 0 (`BulkOrderService.cs:318–321`), and the factory drops an order id
  that is not among the customer's latest `MAX_ORDER_PICKER_ITEMS` (`Factories/BulkOrderModelFactory.cs:803–808`).
  Before, an order id typed into the address made a cache entry and a full query each, under any filter.

Consumers: `Product`, `Category`, `ProductCategory`, `Order`, `OrderItem`, `StoreMapping`, `AclRecord`. Order and
order item matter because the Recently-ordered and Best-sellers trees are derived from orders — without them a
customer does not see the categories they just ordered from until the entry expires. Store mapping and ACL records
change what `ApplyVisibilityAsync` lets through without changing the key (the key carries the store and the role
ids, not the rows). Every order or product change clears the whole tree cache, and the cache does not follow
availability dates — recorded, not changed, in part 5h.

---

## 11. Add to cart

### 11.1 Contract

No form-key string parsing anywhere. Each quantity input carries its selections as data attributes; the browser
composes JSON.

```html
<!-- as built (_Grid.cshtml). The product id is on the enclosing .bulk-order-grid, not the input. -->
<input type="number" min="1" max="7" class="bulk-qty"
       data-selection="12:45,13:89"      <!-- mappingId:valueId pairs - axis values plus every pin holding a value -->
       data-axis-key="12:45"             <!-- axis values only: the cell's identity across pin changes (§8.4) -->
       aria-label="Size S, Colour Navy"  <!-- the cell's qualified name; also the prefix of its warning (P9 I11) -->
       title="TUN-400-S-NVY" />           <!-- the matched combination's Sku, when one matched -->
<!-- under Product.AllowedQuantities the input is a <select class="bulk-qty"> carrying the same data attributes -->
```

```csharp
// BusinessLogic/Requests/  — per CLAUDE.md §5.1: a service takes an input record it owns, never a view model.
public record BulkOrderAddRequest(IList<BulkOrderLine> Lines);

public record BulkOrderLine(
    int ProductId,
    IList<BulkOrderSelection> Selections,   // axis selections + the card's pinned selections, merged client-side
    int Quantity,
    string CellKey);                        // echoed back in the line's result, so the page can mark its cell

public record BulkOrderSelection(
    int ProductAttributeMappingId,
    IList<int> ValueIds,                    // list, for checkbox attributes
    string EnteredText);                    // 5.00: read by nothing (see below)
```

**5.00 (part 5h, fixed in 4.9 too):** `EnteredText` is still on the record but `BulkOrderLogic.ComposeAttributesXmlAsync`
no longer writes it. The grid has no input for a text, date or file attribute, and writing posted text as it came
skipped nop's own parsing of those types; so nothing posted for a non-combinable mapping is written. A product that
requires one still shows its notice (§12.5). And an attribute that takes one value (a list, radio buttons, squares)
gets the **first** posted value only — several would add up their price adjustments and match no combination,
skipping its stock check; only `Checkboxes` and `ReadonlyCheckboxes` take several (`BusinessLogic/BulkOrderLogic.cs`,
the summary above `ComposeAttributesXmlAsync` and its value loop).

This is arity-agnostic: a 4th or 5th axis needs no new code, which was the single biggest blocker in 4.6
(`NOP41` improvements I1).

### 11.2 Server flow

```
POST /bulkordering/addtocart      body: BulkOrderAddRequest
  refuse a body that is not a request, or holds an empty line (400)
  refuse the whole submit when more than BulkOrderMaxLinesPerAdd (1000) lines carry a quantity — nothing added
  for each line:
      resolve product, once per product per submit; reject if not visible to this customer
      compose attributesXml from Selections via IProductAttributeParser.AddProductAttribute
      merge with an earlier line of the same product and attributesXml (quantities summed)
  for each merged line:
      warnings = AddToCartAsync(customer, product, ShoppingCart, storeId, xml, quantity, addRequiredProducts: true)
      record { lineIndex, productId, cellKey, warnings as plain text } for every line merged into it
  return { addedQuantity, results[] }
```

`AddToCartAsync` is `CW.Core`'s override, which validates internally and attaches the packname ambiently (D8).
Per D6 lines that pass are committed even if others fail.

**5.00 (part 5h, fixed in 4.9 too).** The cap, the per-product visibility read and the merge are new
(`BusinessLogic/BulkOrderLogic.cs`; the cap is `CWCoreDefaults.BulkOrderMaxLinesPerAdd`, its message
`Plugins.CW.Core.BulkOrder.AddToCart.TooManyLines`; the empty-body check is `Controllers/BulkOrderController.cs:92–96`).
The grid never sends two lines for the same product and options, so the merge only changes a hand-made post:
two lines of 6 against a stock of 10 are now both refused, where 4.9 added the first. Warnings come back as plain
text (`AsText`: markup such as nop's required-product link stripped, HTML entities decoded), and the page still
writes them as text.

Validation cost is proportional to **lines submitted**, not cells rendered — typically a handful.

#### Built in P6, and the parts that are not obvious (2026-09-04)

**Call the BASE-shaped overload, never the one taking a `packNameId`.** `ShoppingCartOverrideService` has two:
`:526` (`public override`, nop's own signature) resolves the current packname itself and dispatches — to
`base.AddToCartAsync` when there is none, to the packname overload when there is one. That dispatch *is* what
"attaches the packname ambiently" means. The other (`:760`, `public virtual`, explicit `packNameId`) opened with
a guard that returned an **empty warning list** when the id resolved to nothing — and an empty list is this
codebase's success signal. Passing `packName?.Id ?? 0` therefore reported every line as added and touched no
cart at all on a store with no packname in context. Cost a full debugging round; recorded in 4.9's
`Subsidy_PackName_Known_Issues_And_Improvements.md` item 10 as a defect of the service, not of this design.
**Since fixed:** the guard now returns the `Plugins.CW.Core.PackName.NotAvailableForOrdering` warning
(`ShoppingCartOverrideService.cs:774–779`, "never an empty list for a line that was not added"). The rule above
still stands — the packname overload is not the ambient path.

**`BulkOrderLogic` does not resolve the packname.** It has no `IPackNameContext`. One place decides what the
current packname is, and it is the cart service.

**`AddedQuantity` counts UNITS, not lines.** The same response drives the card's "n in cart" and the header
counter, both of which are units; a third number counting lines made one action produce two different answers
a few inches apart. Renamed from `AddedCount` for exactly that reason.

**One activity-log entry per PRODUCT that took at least one line** — `PublicStore.AddToShoppingCart`, nop's own
key and resource. Not per line (a grid submit would bury the log) and not once per submit naming the last
product the loop touched, which is what 4.6 did.

**`ShoppingCartSettings.DisplayCartAfterAddingProduct` is deliberately ignored.** nop honours it and so did
4.6. This screen does not: its premise is filling many boxes across many cards and pressing one button, and
navigating to the cart after the first card makes the rest of the page unreachable and the per-cell failure
reporting impossible to show. **This is a divergence, not an oversight** — do not "fix" it.

### 11.3 Client behaviour on the response

- cells whose line succeeded: quantity cleared, the card's "n in cart" refreshed;
- cells whose line failed: quantity **kept**, cell marked, the warning shown against that cell;
- the mini-cart / top-cart counters refreshed from the response;
- the page is **not** reloaded and no navigation occurs.

This is the one place the implementation improves on 4.6 without changing the outcome semantics (D6).

#### How the refresh actually works, and one clause that was never built (2026-09-04)

**There is no client-side "cart changed" event in nop.** A grep of `wwwroot`, `Views` and `Themes` finds
nothing. What nop does — `ShoppingCartController.AddProductToCart_Details` (its `GetProductToCartDetailsAsync`, `:457–458`) →
`wwwroot/js/public.ajaxcart.js:151–160` — is return the two cart blocks as **rendered HTML** from the add
endpoint and write them into the DOM, taking the selectors from `AjaxCart` (which the theme's HeaderLinks
component initialises on every storefront page). 4.6 did the same
(`lib/public.ajaxbulkcart.js:110-120`). This design briefly specified a custom event instead; that was
invented, and it was a regression against 4.6 as well as against nop.

**The card's "n in cart" is refreshed from the WHOLE per-product map the response carries, never a delta.** nop
merges a matching line into an existing one, and `addRequiredProducts` can add a product nobody typed a
quantity for, so what the client submitted does not determine what the cart now holds. The map is
packname-scoped (`IBulkOrderService.GetCartQuantitiesAsync`, moved out of the factory in P6 so the render path
and the add path cannot disagree); the header total is the full store cart, because that is what the header
counts everywhere else.

**"the cell's 'in cart'" was removed from the list above because it was never built** — not in P3, P4, P5 or
P6. It needs each cart line's `AttributesXml` mapped back to a cell key, per product, on every list render.
Logged as **F21**; the card-level figure answers the common question today.

### 11.4 Per-product "Add to cart"

The same endpoint with the lines of one card. 4.6's separate hidden-form path
(`NOP46` reference §12.3, and defect D3 — the `id*=` substring selector) does not carry over.

---

## 12. Settings

**Revised 2026-08-28 — settings go on `MultiStoreSettings`, not a new class.** The earlier proposal
(a separate `BulkOrderSettings : ISettings`) was written without noticing two things in the codebase:

1. `Domain/Settings/MultiStoreSettings.cs` **then carried the whole 4.6 bulk-order block, commented out**
   (`:7`–`:49` in 4.9 at the time; P1 replaced it with the `#region Bulk Order` block that opens the class today,
   `:7`–`:54`) — a partial port someone started, naming `EnableBulkOrdering`, `BulkOrderRoleIds`,
   `BulkOrderingView`, `HorizontalAttributeId`, `VerticalAttributeMainId`, `VerticalAttributeSecId` and the
   rest exactly as 4.6 named them.
2. `CW.Core` has a **per-store feature-toggle system** (`CWPlans/Feature_Toggles_Reference.md`) whose enable
   flags live on `MultiStoreSettings` by convention — `PackNameEnable`, `SubsidyEnable`, `ExchangesEnable`,
   `CreditNotesEnable` — and are read through `CWFeatureSet.From(MultiStoreSettings)`
   (`Domain/Settings/CWFeatureSet.cs`).

A separate settings class would fight both. So: the settings in the table below are added to
`MultiStoreSettings`, replacing the commented block, and the master switch becomes **`BulkOrderEnable`**,
registered as a new `CWFeature`. See §14 for the gating consequences.

**Deliberate divergences from the commented block**, to be applied rather than uncommented:

| Commented (4.6 names) | This design | Why |
|---|---|---|
| `HorizontalAttributeId`, `VerticalAttributeMainId`, `VerticalAttributeSecId` | `AxisAttributeIds` (ordered `List<int>`) | D3 — three fixed slots cannot express 5 axes |
| `EnableBulkOrdering` | `BulkOrderEnable` | matches the `…Enable` convention the feature system requires |
| `EnableRefillOrdering`, `RefillOrderRoleIds`, `RefillPageImageSize`, `ShowRefillProductImages` | dropped | Refill is a Jim2-API feature, out of scope (§16.3) |
| `ShowProductImages`, `ShowProductStock`, `ShowAttributeImages`, `HideQtyArrows` | prefixed `BulkOrder…` | these are generic names on a settings class shared with PackNames and Subsidies; unprefixed they read as global |

| Setting | Type | Default | Notes |
|---|---|---|---|
| `BulkOrderEnable` | bool | false | the **feature flag** — see §14. 4.6 `EnableBulkOrdering` |
| `BulkOrderAxisAttributeIds` | `List<int>` | empty | **ordered**; `ProductAttribute` ids; D3 |
| `MaxAxisCount` | int | 5 | D2 |
| `MaxCellsPerProduct` | int | 400 | §5.4 |
| `DefaultViewMode` | int | 0 | 0 = tiled, 1 = list; 4.6 `BulkOrderingView` |
| `ProductImageSize` | int | 300 | 4.6 `BulkPageImageSize` |
| `ShowProductImages` | bool | true | |
| `ShowAttributeImages` | bool | false | |
| `AttributeImageSize` | int | 100 | |
| `AttributeImageSquaresSize` | int | 0 | 0 → `MediaSettings.ImageSquarePictureSize` |
| `HideQtyArrows` | bool | false | |
| `ShowAttributePrices` | bool | false | 4.6 `ShowProductAttributesPrice` |
| `ShowProductStock` | bool | true | |
| `SendUserToBulkOrderingAfterLogin` | bool | false | 4.6 `SendUserToBulkOrdering` |
| `EnableOnProductPage` | bool | false | 4.6 `EnableStandardProductBulkOrder` |
| `DefaultPageSize` | int | 15 | |
| `PageSizeOptions` | string | `15,30,50,100` | 4.6 hardcoded these in the controller |

**As built (P1):** every setting in the table is prefixed `BulkOrder` on `MultiStoreSettings`
(`BulkOrderDefaultViewMode`, `BulkOrderShowAttributePrices`, …); `MaxAxisCount` is **not** a setting but the
constant `BulkOrderDefaults.MAX_AXIS_COUNT` (D12); `SendUserToBulkOrderingAfterLogin` is
`BulkOrderSendUserAfterLogin`, which had no consumer until P10 (§20) and is now read by
`BulkOrderEntryService.ResolveLandingRouteAsync` — asked after the subsidy landing (`IPackNameLandingService`);
`EnableOnProductPage` is `BulkOrderEnableOnProductPage` (P8, §19). The defaults in the table are the property
initialisers in `MultiStoreSettings.cs:10–53`.

`AttributeImageSquaresSize` semantics and the `> 100` floor in the view (`NOP46` reference §15) are preserved.
There is no historical data to migrate (`CLAUDE.md §0.8` — pre-production).

---

### 12.0 What the catalogue actually looks like (measured 2026-08-28)

One client, one year-old backup. **Used to sanity-check defaults, not to tune them** — a second client with
unknown data is expected.

| Attributes per product | Products | |
|---|---|---|
| 1 | 98 | |
| **2** | **5,233** | ~93% — the classic Size × Colour grid, no pinning at all |
| 3 | 59 | |
| 4 | 196 | |
| 5 | 5 | |
| 6, 7, 13 | 1 each | |
| 15 | 12 | the twelve-back-logo products |
| 16 | 2 | |

- **17 products exceed 5 attributes**, so `MAX_AXIS_COUNT = 5` leaves only the tail pinning — and those are
  exactly the products 4.6 drops silently today (`NOP46` D5).
- **Largest real combination set is 108** (top 16 products all ≥ 77), so `BulkOrderMaxCellsPerProduct = 400`
  has comfortable headroom. Note cells = rows × columns and can exceed the combination count where the grid is
  sparse, which is why the budget is on **cells**, not combinations.
- **709 products have attributes but no combinations at all** (~13%). §7.4's fallback is therefore a
  mainstream path, not an edge case, and its cell budget is what stops a 15-attribute product from
  cross-product exploding.

The headline: the design's complexity is paid almost entirely by the tail. 93% of products take the simplest
path through it.

#### Re-measured 2026-09-01, during P3 acceptance prep

Run against the live catalogue with `Deleted = 0` (4,611 products). Where these disagree with the numbers
above, **the filter differs** — the P0 figures were taken without the deleted filter and before the last
import — so both are kept rather than one overwritten. Anything here that a later phase needs is stated as a
number, not a recollection, so nobody has to re-run the query.

| Fact | Value | Consequence |
|---|---|---|
| Products with combination rows | 4,343 | |
| …of those, `ManageStockByAttributes` **and** `AllowAddingOnlyExistingAttributeCombinations` | **0** | §7.4 — nothing in the catalogue restricts, so no cell ever dashes. The restricted branch of `Compose` is exercised only by test data. |
| …`ManageInventoryMethodId = 0`, flag off | 4,303 | combinations are decoration: Sku only |
| …`ManageInventoryMethodId = 2`, flag off | 39 | combinations gate stock, never existence |
| …`ManageInventoryMethodId = 0`, flag **on** | 1 | flag is dead at method 0 — but somebody sets it, so §7.4's restricted path is one admin click from mattering |
| Products with attributes and **no** combinations | 266 (5.8%) | cf. the 709 above; different filter, same conclusion — §7.4's fallback is mainstream |
| `ReadonlyCheckboxes` attributes with an unpreselected value | **0** | the `ResolvePinValues` fix has no live instance; it is insurance for the backup and the next client |
| Mappings with `ConditionAttributeXml` | **401 across 221 products** | P4's whole reason to exist; and the constraint that **P6 must not ship before P4** |
| `AttributeValueType.AssociatedToProduct` values | **0** | bundle validation is theoretical here; the dev database has 2, the client none |
| **Required** free-text mappings (`TextBox`/`Multiline`/`Datepicker`/`FileUpload`) | **205 across 205 products** | nop refuses every line; P6 must decide what the grid does with them |
| `ProductType != Simple` / `DisableBuyButton` / `CallForPrice` / `CustomerEntersPrice` | 1 / 7 / 5 / 0 | §12.2's `cellDisabled` rule — the first two were missing from it |
| Projected cells ≤ 50 / 51–200 / 201–400 / 401–2,000 / > 2,000 | 4,128 / 54 / **0** / **0** / 13 | the distribution is bimodal with an empty band, so **any** budget between 201 and 2,000 renders identically. 400 is confirmed and not worth tuning. |
| Max axis-eligible attributes, excluding the 13 outliers | **4** | `MAX_AXIS_COUNT = 5` never binds on 99.7% of products. Only `ZZT-11` covers the boundary. |

The 13 outliers are the twelve-back-logo products already named in the table above (ids 3140, 3504–3514) plus
`TDTEST` (4579). Their exact P3 behaviour was worked through in
`BulkOrdering_49_P3_DemotedPinCarveOut.md` (a P3 working document, consumed; no copy exists under `NOP49/`): a polo demotes three times and lands on 12 × 10 = 120 cells with
13 pins; `TDTEST` demotes once and lands on **exactly 400**, the budget boundary.

### 12.1 Settings audit — everything 4.6 bulk ordering reads

Added 2026-08-28 after a sweep of **every** `_jim2StoreSettings.*` reference in the 4.6 bulk-order code paths,
plus the customer-level and product-level flags those paths consult. The §12 table above covers the
`#region Bulk Order` block only; the settings below are consumed by bulk ordering but defined elsewhere, and
several were missing from this design entirely.

**A — inside `#region Bulk Order`.** All 21 accounted for: 17 carried into §12, 3 replaced by
`BulkOrderAxisAttributeIds`, 4 Refill settings dropped (§16.3).

**B — consumed by bulk ordering, defined elsewhere in `Jim2DefaultSettings`.**

| 4.6 setting | What it does in bulk ordering | Status |
|---|---|---|
| `AllowOutofStock` | third clause of the disabled-cell rule; suppresses `MaxAvailable`; flips `IsInStock` true when out of stock | **nop native** — `Product.AllowOutOfStockOrders`, `ProductAttributeCombination.AllowOutOfStockOrders`, `BackorderMode`. Do not port a store-wide override. |
| `HandledNoPrice` | price of 0/null becomes "Call for price" | **DQ2** — a Jim2 business rule; nop has only `Product.CallForPrice` |
| `HandledNoStock` | out of stock becomes "Call for price" | **DQ2** — same |
| `PreOrderOutOfStock` | out-of-stock + incoming ETA becomes a pre-order; `MaxAvailable = ETA − inCart` | **deferred with F11** — needs an ETA source |
| `InStockText` / `NoStockText` | the in/out-of-stock labels | **nop native** — locale resources `Products.Availability.InStock` / `.OutOfStock` |
| `ShowQtyPerWarehouse` | per-warehouse stock breakdown in the cell and its column headers | **deferred with F11** — needs a warehouse-aware provider |
| `ShowStockQty` + `ShowStockQtyCustomerRoleIds` | whether the numeric quantity shows at all, **gated by customer role** (`ProductOverrideService.ShowStockQuantity`, `:2709`) | **GAP — add.** Per-product is nop native (`Product.DisplayStockQuantity`); the **role gate** is the CW addition and has no equivalent |
| `ShowETA` + `ShowETACustomerRoleIds` | whether ETA is computed at all, **gated by customer role** (`:2736`) | **deferred with F11**, but the role-gate shape should land with the provider |
| `ContractOnlySupport` | enables the contract-items visibility constraint | **deferred with the `ContractItems` filter** (§16.1) |
| `ProductAttributeAlpha` + `ProductAttributeAlphaIds` | for the named attributes, sorts attribute **values alphabetically** instead of by `DisplayOrder`, by overriding `GetProductAttributeValuesAsync` (`Services/Catalog/JimProductAttributeServiceOverride.cs:66`) | **GAP — DQ3.** This silently changes grid column and row order and **contradicts §5.5 as written** |

**C — per-customer / per-packname flags (data, not settings).**

| 4.6 flag | Effect | Status |
|---|---|---|
| `Jim2WebLogon.ContractItemsOnly` | restricts the customer to contract items | deferred with §16.1 |
| `PackName.ContractItemsOnly` | same, per packname, and **takes precedence** when a packname is selected (`CustomerOverrideService.IsContractItemsOnlyAsync:1111`) | deferred with §16.1 |
| customer role `ContractedItems` | whether the "Contract Items Only" filter is offered at all (`IsContractedItemsAsync:1132`) | deferred with §16.1 |
| `BulkOrderRoleIds` | access to the bulk screen, by role | **covered** — §12 |

**D — per-product flags.**

| 4.6 flag | Effect | Status |
|---|---|---|
| `Jim2Product.LockBulkOrder` + `LockBulkOrderOverride` | per-product override of `EnableStandardProductBulkOrder`: force the bulk grid **on or off** for one product on its details page, regardless of the store setting (`Jim2ProductService.IsBulkOrderLocked:4333`) | **GAP — DQ1.** `CW.Core` has no `Jim2Product` equivalent and no product-level override entity at all (`Domain/Products/` holds only two small types) |

### 12.2 Decisions taken (2026-08-28)

| # | Question | Decision |
|---|---|---|
| **DQ1** | Per-product override of the product-page bulk grid | **Yes — via `GenericAttribute`.** See §12.3. |
| **DQ2** | Keep `HandledNoPrice` / `HandledNoStock` ("price is 0 → call for price", "out of stock → call for price") | **No.** Not ported. Deferred to `Future_Improvements` **F15**. The disabled-cell rule loses those two clauses and gets simpler; `Product.CallForPrice` remains the only route to a call-for-price cell. |
| **DQ3** | Port `ProductAttributeAlpha` / `ProductAttributeAlphaIds` (alphabetical attribute-value ordering) | **No.** It is not implemented anywhere in CW.Core (4.9 or 5.00), and bulk ordering is not the place to introduce a storefront-wide override of `GetProductAttributeValuesAsync`. §5.5 stands as written: **columns and rows order by `ProductAttributeValue.DisplayOrder`, then `Id`** — nop's own order. Stores wanting alphabetical order fix `DisplayOrder` in admin. |
| **DQ4** | Role-gated display of stock **quantities** (`ShowStockQty` + `ShowStockQtyCustomerRoleIds`) | **No.** Bulk ordering shows stock exactly as the rest of the site shows it — nop's `Product.DisplayStockAvailability` / `DisplayStockQuantity` per product, no CW role gate. Revisit only when a site-wide stock-quantity policy exists. Deferred to **F16**. |

Consequences for the design: §10.4's availability model has **no** store-level `AllowOutofStock`, no
`HandledNo*` rules, no role gate and no ETA — it reads `ProductAttributeCombination.StockQuantity` /
`AllowOutOfStockOrders`, product-level `GetTotalStockQuantityAsync`, `Product.DisplayStockAvailability` and
`Product.DisplayStockQuantity`, and nothing else. The disabled-cell rule reduces to:

```
cellDisabled = !product.Published
            || product.CallForPrice
            || product.DisableBuyButton                    // measured: 7 live products
            || product.ProductType != ProductType.Simple   // measured: 1 live product
            || (restrictsToExistingCombinations && combination is null)   // a dash, not a box
            || (!inStock && !allowOutOfStockOrders)
```

Three of those clauses were corrected after the fact and the reasons are worth keeping:

- **`DisableBuyButton` and `ProductType != Simple`** were missing. `GetStandardWarningsAsync` (`:358`, `:370`)
  refuses both outright, so without these clauses the grid puts a full set of quantity boxes in front of a
  product on which every single line fails. `ApplyVisibilityAsync` does not filter them either, so this rule
  is the only place they can be caught. Measured on the client catalogue: 7 and 1 products respectively —
  small, but the failure is total for those products, not partial.
- **`combination is null` is not sufficient on its own** — see §7.4. A missing combination disables a cell
  only when the product actually restricts to existing combinations. Measured: **zero** of 4,611 client
  products restrict, so on that data this clause never fires and the dash is reserved entirely for
  configurations that do not yet exist in the catalogue.

`CustomerEntersPrice` is deliberately absent: `GetStandardWarningsAsync` (`:384–392`) only refuses when the
entered price is out of range, and a bulk grid never lets a customer enter one. Measured: 0 live products.

That is a **material simplification** of 4.6's five-clause rule (`NOP46` reference §11.1) and it removes the
`"UP"` sentinel string, `MaxAvailable` arithmetic driven by settings, and the pre-order branch.

### 12.4 nop parity — what gates a cell, and which phase owns it

Folded in from the P3 parity audit, 2026-09-02. Written by walking
`ShoppingCartService.GetStandardWarningsAsync` (`:337`), `GetShoppingCartItemAttributeWarningsAsync` (`:818`)
and the `ManageInventoryMethod` switch (`:490`) end to end, then measuring each rule against the live
catalogue.

**First, the definition, because "the same as nop" has two answers.** nop's *product page* renders every
attribute value always and never hides an option — you find out at add-to-cart. nop's *cart validation*
refuses some of them. A grid of quantity boxes cannot copy the page: a box you can type into that is then
refused is worse than a dash. **The target is cart validation** — a cell is enterable exactly when
`GetShoppingCartItemWarningsAsync` would return no warnings for that selection.

**Cell-level rules** — these vary between cells of the same product, so the grid must model them:

| nop rule | Where | Owner |
|---|---|---|
| Missing combination refused when `ManageStockByAttributes` **and** `AllowAddingOnlyExistingAttributeCombinations` | `:490–512` | **P3** — §7.4 |
| Matched combination, `!AllowOutOfStockOrders`, `StockQuantity < qty` | `:496–497` | **P5** — §12.2 |
| An attribute whose `ConditionAttributeXml` is unmet is stripped | `:853–861` | **P4** |
| Required attribute with nothing chosen | `:868` | **P3** for value pins; **P6** for required free-text (205 live products) |
| `ReadonlyCheckboxes` must submit exactly the `IsPreSelected` set | `:906–920` | **P3** — see below |
| `AssociatedToProduct` values re-validate the associated product | `:970` | **P6** — 0 live products |
| Text min/max length | `:936` | **P6** |
| `OrderMinimumQuantity` | `:400` | **P6** — built |
| `OrderMaximumQuantity` | `:406` | **P6** — built |
| `AllowedQuantities` (a whitelist) | `:412–414` | **P6** — built |

**The three quantity rules were missing from this table until 2026-09-04**, and that is why no phase picked
them up: the section's stated target ("no warnings from `GetShoppingCartItemWarningsAsync`") always committed
us to them, but the table listed only rules about whether a cell *exists*. Measured on the client catalogue
(4,611 live products): **1** product with `AllowedQuantities`, **3** with a minimum above 1, **2** with a
maximum below the default — and **both of the last two are unpublished**, so five of the six shapes are
unreachable on real data. `ZZT-39 … ZZT-43` are those shapes, published.

What P6 built:

- `OrderMaximumQuantity` folds into the cell's `MaxAvailable` clamp, so a box's `max` is the lower of stock
  and the product cap. **Side effect worth knowing: `MaxAvailable` is now never null outside `Unorderable`** —
  every cell renders `max="10000"` where the attribute used to be absent. That is correct, not a defect.
- `OrderMinimumQuantity` drives the box's `min` and a grid-level notice using **nop's own**
  `ShoppingCart.MinimumQuantity` resource, so the notice and the warning are the same sentence (§11.2).
- `AllowedQuantities` replaces the number box with a **`<select>`**, which is what nop's own product page
  renders for it — a free number box is a control that cannot express a whitelist, and the customer would have
  to be refused to discover what the list is. Options above what the cell can supply are dropped; if that
  leaves none, the cell renders blocked with nop's stock wording rather than a dropdown whose only option is
  "None".

**Product-level rules** decide whether the product appears at all, and belong to `cellDisabled` (§12.2) or to
`ApplyVisibilityAsync`: `Deleted` / `Published` / dates / ACL / store mapping are modelled;
`ProductType != Simple`, `DisableBuyButton` and `CallForPrice` are in §12.2's rule; `CustomerEntersPrice` is
deliberately absent because a bulk grid never lets a customer enter a price.

**`ReadonlyCheckboxes` is the sharp one.** nop accepts *exactly* the `IsPreSelected` value ids and rejects
anything else with *"You cannot change read-only values"* — and `CommonHelper.ArraysEqual` (`:159`) compares
**length and order**, so a superset fails as surely as a wrong value. `ResolvePinValues` must therefore
resolve such a pin to `PreSelectedIds`, never to every value. Submitting all of them makes every line of that
product un-addable, and the store's own admin UI cannot produce the shape that reveals it.

**The one non-negotiable sequencing constraint this produced: P6 must not ship before P4.** nop strips
unmet-condition attributes only inside `ProductAttributeParser.ParseProductAttributesAsync(Product, IFormCollection, …)`
(in `GetProductAttributesXmlAsync`, `:308–313`). P6 composes `AttributesXml` itself, and nothing downstream removes them —
`GetShoppingCartItemAttributeWarningsAsync` filters its *mapping* list by condition but never the *submitted*
attributes. A P6 built before P4 would write order lines carrying attribute values, and their price
adjustments, that nop's own product page would have deleted. 401 conditional mappings across 221 products.

### 12.5 Products the bulk screen cannot order at all (added 2026-09-03)

Found from a live gift card, not from the plan. `GetShoppingCartItemWarningsAsync` (`:1154–1172`) validates
**five** families, and §12.2 and §12.4 between them had only ever examined two:

| # | Family | Method | Owner |
|---|---|---|---|
| 1 | Standard properties | `GetStandardWarningsAsync` (`:337`) | §12.2 |
| 2 | Selected attributes | `GetShoppingCartItemAttributeWarningsAsync` (`:818`) | §12.4, P3/P4/P6 |
| 3 | Gift cards | `GetShoppingCartItemGiftCardWarningsAsync` (`:1020–1059`) | **this section** |
| 4 | Required products | `GetRequiredProductWarningsAsync` (`:230–320`) | **P6** — see below |
| 5 | Rental | `GetRentalProductWarningsAsync` (`:1071`) | **this section** |

Plus one at cart level: `GetShoppingCartWarningsAsync` (`:1216–1218`) refuses to hold a **recurring** product
beside a standard one.

**The rule.** A product whose add-to-cart needs data a grid of quantity boxes has nowhere to collect is not
rendered as a grid at all. `BuildAsync` returns before `LoadAsync` — no attributes loaded, no combinations
parsed — and the card shows one sentence naming the reason plus a link to the product page, which already has
the form and already validates it.

| Kind | Column | What nop demands | Source |
|---|---|---|---|
| Gift card | `IsGiftCard` | Recipient name and sender name; **plus both email addresses when `GiftCardType = Virtual`**, and a malformed one is rejected | `:1038–1056` |
| Rental | `IsRental` | Start and end date per line, start before end | `:1081–1097` |
| Customer enters price | `CustomerEntersPrice` | A price within `Minimum`/`MaximumCustomerEnteredPrice` | `:384–392` |
| Recurring | `IsRecurring` | *No data* — but the cart refuses to mix it with standard products, and a bulk screen exists to add several at once | `:1216–1218` |

**Two kinds that were suspected and cleared.** **Downloadable** (`IsDownload`) needs nothing at add-to-cart —
`HasUserAgreement` is accepted at checkout. **Requires other products** is a dependency, not missing data, and
belongs to P6 rather than here; see the correction below.

**Why block rather than support.** A gift card needs four fields *per line*; a rental needs two dates *per
line*. A grid's premise is that a line is a coordinate and a number, and four text fields per cell either
breaks that premise or produces a 400-row form. For a product with no attributes — one box — a small form
would fit, but then bulk ordering would support gift cards only when they have no variants, which is a rule
no customer could predict from the screen. Supporting these is a different screen, not a bigger grid.

**Measured 2026-09-03.** Client database (one year old): **zero** of all four. So this change is protective
rather than corrective there — which is exactly why it is worth having, since a second client's data is
unknown. nop's own sample data has one of each, including both gift-card types, so the branches are testable
without inventing anything.

| Kind | Client DB | nop sample DB |
|---|---|---|
| Gift card | 0 | 3 (`VG_CR_025` Virtual, `PG_CR_050` and `PG_CR_100` Physical) |
| Rental | 0 | 1 (`EG_GEM_NL`) |
| Customer enters price | 0 | 2 (`IF_YOU_WT`, `SCI_FAITH` — donations) |
| Recurring | 0 | 1 (`SF_PRO_11`) |
| Downloadable — no action | 0 | 3 |
| Requires other products — see below | **3** | 0 |

**Recurring was the judgement call**, and the measurement made it easy: the client has none, so blocking costs
nothing today. Revisit if a store ever wants recurring products bulk-ordered — the block is one line in
`UnsupportedReasonFor`.

**A correction to record, because this claim was wrong twice.** *Requires other products* was first written off
as "nop adds them itself" — only true when `AutomaticallyAddRequiredProducts` is set (`:296`). It was then
recorded as "`GR3942A` silently adds product 5013 to the cart", and a P6 obligation was built on that. **That
was also wrong.** The measurement below settles it: nothing happens on this data at all.

**Measured 2026-09-03.** All three client products carry the flag, and all three are **inert — for two
different reasons**:

| Id | Sku | `RequireOtherProducts` | `AutomaticallyAdd…` | `RequiredProductIds` | What nop actually does |
|---|---|---|---|---|---|
| 4591 | `TN3904` | 1 | 0 | **NULL** | **Nothing.** `ParseRequiredProductIds` returns an empty array for a null column (`ProductService.cs:1402–1403`), so the method returns at `:260–261` |
| 4715 | `RWSZP704` | 1 | 0 | **NULL** | **Nothing**, same reason |
| 5012 | `GR3942A` | 1 | 1 | 5013 | **Nothing.** Product 5013 (`GR4136`) is `Deleted = 1`, and `GetProductsByIdsAsync` passes `includeDeleted: false` (`ProductService.cs:582`), so `AddDeletedFilter` drops it and `requiredProducts` comes back empty — again returning at `:260–261` |

So neither branch of `GetRequiredProductWarningsAsync` fires anywhere on the client catalogue: nothing is
auto-added, nothing is refused, and the earlier note about silent cart side-effects described something that
cannot happen.

**What survives is a latent risk, and it is now the whole reason to care.** Each of the three is one plausible
admin action away from going live, and they would go live down *different* branches:

- `TN3904` / `RWSZP704` — someone fills in `RequiredProductIds`. The flag already reads as enabled in admin,
  so this is a natural thing for an administrator to complete. Auto-add is **off** on both, so it is the
  **refusing** branch (`:313–316`) that activates, and it blocks the sale.
- `GR3942A` — someone restores product 5013, or repoints the id at a live product. Auto-add is **on**, so the
  **silent-add** branch activates.

P6 therefore needs both, but as defensive work rather than a fix for observed behaviour, and it should be
written knowing no current data exercises either path — which means neither will be caught by testing against
this catalogue.

**A data-quality note worth passing to the client, separately from this build.** `GR3942A` advertises a
dependency on a product that has been deleted. nop tolerates it silently; it is still a configuration that
means nothing, and the same is true of the two products whose `RequiredProductIds` is null.

**One question left open, now downgraded.** nop auto-adds a required product without checking whether the
customer may see it, so on a subsidy store an unallocated product could enter the cart (§10.4a). On this data
the auto-add never fires, so it is theoretical — but if it ever does fire, whether bulk ordering honours nop's
auto-add or refuses the parent is a question for the team, not a coding choice.

#### A required free-text attribute blocks CELLS, not always the product (corrected 2026-09-05)

`BulkOrderUnsupportedReason.RequiredTextAttribute` refused the whole product whenever a required attribute
with no values survived §9.1 into the pin list. That is right for a §9.1 **case 1** attribute, whose condition
the pin state answers once for the card — and wrong for **case 3**, whose condition depends on an axis and is
therefore answered *per cell*. Measured on the client catalogue: **101 published products carry exactly this
shape and every one of them is case 3**, so the screen refused 101 products outright, including the cells
whose axis value does not satisfy the condition and which nop sells. That is stricter than nop, which §12.4's
stated target forbids.

The rule now has three outcomes, and the third is what makes the first two safe:

| Where the attribute applies | Answer |
|---|---|
| Every line of the card — unconditional, or case 1 with the condition met | `RequiredTextAttribute`, the §12.5 card, no grid |
| Some cells — case 3, condition satisfied by some rendered axis values | those cells alone take no quantity; the grid renders |
| Every **rendered** cell — case 3 where the depended-on axis has one value | back to the §12.5 card |

The third row is not a special case bolted on: it is the second row observed after composing, and it is why
the check is asked of the composed cells rather than of the pin. `CV1690` is the live example — a "Label"
axis with a single value, so every cell carries it. Counted over cells where `Exists` is true only: a dash
cell was never orderable, so it neither needs explaining nor argues for rendering the grid.

`Compose` already knew, per cell, whether a conditional pin applied — that test decides what the cell submits
and looks up (§9.1 case 3). The block reuses the same test rather than adding a second one, because a cell
that submits an attribute while also refusing to take a quantity for it is incoherent.

**The attribute is named by its `TextPrompt`, falling back to its name** — the same fallback the pinned strip
renders. Naming it by the `ProductAttribute` sends the customer looking for a field that is not on the card.

**Known gap, logged rather than built.** A product with **two** required free-text attributes conditional on
*different* axis attributes names only the first in the notice, and can name one that blocked no rendered
cell. Zero products on the measured catalogue have two; the correct fix needs `Compose` to report which pins
actually blocked cells, which changes its return shape. Trigger: the first product with two.

**Coverage.** `ZZT-48` (partial block), `ZZT-49` (single-value axis, whole product), `ZZT-50` (two-value
condition, nothing blocked), `ZZT-51` (case 1, blocks on tick). None of these shapes existed in the ZZT set
before, which is why P4 and P6 both passed with the defect in place.

### 12.3 DQ1 — the per-product product-page override

**Shape.** One `GenericAttribute` on `Product`, tri-state:

| Key | `CW.BulkOrder.ShowOnProductPage` |
|---|---|
| absent or empty | follow the store setting `BulkOrderEnableOnProductPage` |
| `"1"` | force the bulk grid **on** for this product |
| `"0"` | force it **off** |

**Why one key rather than 4.6's two columns.** 4.6 used `LockBulkOrder` + `LockBulkOrderOverride`
(`Jim2ProductService.IsBulkOrderLocked:4333`), which is a nullable bool encoded as a pair, and phrased as a
negative — `Lock` meaning *hide*, so the expression reads `LockBulkOrderOverride ? LockBulkOrder :
!EnableStandardProductBulkOrder`, a double negative. A single tri-state key phrased positively says the same
thing without the reader having to unpick it.

**Why `GenericAttribute` rather than a CW product table.** Verified rather than assumed:

- it is sparse — a table would hold one nullable flag for a handful of products;
- no migration, and `CW.Core` has no product-level override entity today (`Domain/Products/` holds two small
  types), so a table would be a new pattern introduced for one flag;
- `GenericAttribute` carries a `StoreId`, so the override is per-store for free — which matches
  `BulkOrderEnableOnProductPage` being a per-store setting.

**The one constraint, and it must be respected.** `GetAttributesForEntityAsync`
(`Nop.Services/Common/GenericAttributeService.cs`) is backed by the **short-term (per-request)** cache, keyed
by `entityId` + `keyGroup`. One product per request is free; **reading this flag inside a loop over a product
list would be one query per product.** The flag is therefore read **only on the product details page (P8)** and
must never be consulted while building the bulk list. If that ever changes, it needs a batched read, not a
loop.

**Store fallback is ours to write.** nop's `GetAttributeAsync(entity, key, storeId)` filters
`props.Where(x => x.StoreId == storeId)` with **no fallback** to the store-agnostic row
(`Nop.Services/Common/GenericAttributeService.cs:195`). Read store-specific
first, then `storeId: 0`, then the store setting — in that order, explicitly
(`BulkOrderProductPageService.IsEnabledForProductAsync`, `Services/BulkOrder/BulkOrderProductPageService.cs:62–79`).
The admin card writes only the store-agnostic row (`storeId: 0`, `Services/EventConsumer.cs:587–588`), so a
store-specific row is honoured when present but nothing in CW.Core writes one.

**Admin UI.** A view component (`Areas/Admin/Components/BulkOrderProductPageAdminViewComponent.cs`, the card
*Bulk ordering*: follow the store setting / always / never) rendered into `AdminWidgetZones.ProductDetailsBlock`,
which nop invokes at `Presentation/Nop.Web/Areas/Admin/Views/Product/_CreateOrUpdate.cshtml:114`. The choice is
saved by `EventConsumer` from nop's posted product form, and since part 4g (fixed in 4.9 too) only for the product
whose `Id` the form posted — before, turning a grouped product into a simple one gave each of its associated
products the edited product's choice. **Not** an edit to
`CW.Core/Areas/Admin/Views/Product/_CreateOrUpdate.Info.cshtml` — that file is a 674-line fork of nop's core
Info tab and every addition to it is more to re-diff on the next nopCommerce upgrade (`CLAUDE.md §10.1`, §10.3). The
widget zone achieves the same placement with no fork.

> **Follow-up outside this design:** `CW.Core/Areas/Admin/Views/Product/_CreateOrUpdate.Info.cshtml` is a
> forked core admin view that is **not** listed in `CLAUDE.md §10.3`'s re-diff table. It should be added there.
> Noted per `CLAUDE.md §0.5` — spotted outside scope, logged rather than fixed silently. Still open in 5.00: the
> port merged it with nop 5.00 (8 lines differed upstream — `CWCore_Port_Reference.md`, Beta §3.2 #13), but §10.3 still
> does not name it.

---

## 13. Layers and files

Per `CLAUDE.md` §5, §7, §12, §9.8.

```
Plugins/Nop.Plugin.CW.Core/                     (as in 5.00, 2026-10-31)
├── Domain/BulkOrder/
│   ├── BulkOrderDefaults.cs                  MAX_AXIS_COUNT, view-mode names, MAX_ORDER_PICKER_ITEMS, PAGER_WINDOW
│   ├── BulkOrderFilterType.cs, BulkOrderSorting.cs, BulkOrderQuery.cs
│   ├── BulkOrderGrid.cs                      BulkOrderGrid / Axis / AxisValue / Pin / Row / Cell — the domain grid (§5–§9)
│   ├── BulkOrderSkeleton.cs                  the cached grid skeleton (§10.5, P7)
│   ├── CombinationKey.cs                     §6.4
│   ├── AttributeCondition.cs                 §9.2, parsed once per product
│   ├── BulkOrderCellState.cs                 §10.3/§10.4/§12.2 per cell (customer-specific, not on the skeleton)
│   ├── BulkOrderAvailability.cs              the §10.4 seam's answer
│   └── BulkOrderUnsupportedReason.cs         §12.5
│   (settings live on Domain/Settings/MultiStoreSettings.cs — see §12)
├── Services/BulkOrder/
│   ├── IBulkOrderService.cs / BulkOrderService.cs            product queries, visibility, visible categories, cart quantities
│   ├── IBulkOrderGridBuilder.cs / BulkOrderGridBuilder.cs    §5–§9: LoadAsync (BuildSkeletonAsync + HydrateAsync, the only service-touching part), classification, conditions, Compose
│   ├── IBulkOrderAvailabilityProvider.cs / BulkOrderAvailabilityProvider.cs   §10.4 seam, nop-only implementation
│   ├── IBulkOrderPricingService.cs / BulkOrderPricingService.cs + BulkOrderPricingContext.cs   §10.3; PrepareAsync once, StateFor per cell
│   ├── IBulkOrderProductPageService.cs / BulkOrderProductPageService.cs + BulkOrderProductPageContext.cs   §19 (P8)
│   ├── IBulkOrderEntryService.cs / BulkOrderEntryService.cs  §20 (P10)
│   ├── BulkOrderServicesDefaults.cs          cache keys and prefixes ONLY (house convention)
│   └── Caching/                              13 ICacheEventConsumers: the tree's (Product, Category, ProductCategory, Order,
│                                             OrderItem, StoreMapping, AclRecord) and the skeleton's (Product, ProductAttribute,
│                                             ProductAttributeMapping, ProductAttributeValue, ProductAttributeValuePicture,
│                                             ProductAttributeCombination, LocalizedProperty) — Product clears both
├── Services/Customers/CustomerRegistrationOverrideService.cs   the sign-in landing (§20.2)
├── Services/PackNames/PackNameLandingService.cs                 subsidy landing first, then bulk ordering (§20.1)
├── Services/EventConsumer.cs                 the product-page model emptying and asset registration (§19.2, §19.4); the product-card save (§12.3)
├── BusinessLogic/
│   ├── IBulkOrderLogic.cs / BulkOrderLogic.cs        §11.2
│   └── Requests/BulkOrderAddRequest.cs, BulkOrderAddResult.cs   §11.1, §11.3
├── Factories/IBulkOrderModelFactory.cs / BulkOrderModelFactory.cs
├── Models/BulkOrder/                          BulkOrderingModel, BulkOrderProductModel, BulkOrderGridModel (+Row/Cell/Value/Pin),
│                                              BulkOrderCommand, BulkOrderCategoryNavigationModel
├── Components/                                BulkOrderCategoryNavigationViewComponent.cs, BulkOrderLinkViewComponent.cs (§20.4),
│                                              BulkOrderProductPageViewComponent.cs (§19.3)
├── Areas/Admin/Components/BulkOrderProductPageAdminViewComponent.cs   the product-edit card (§12.3), model in
│                                              Areas/Admin/Models/Catalog/BulkOrderProductPageModel.cs
├── Controllers/BulkOrderController.cs         Index, Items, Grid, AddToCart (§14)
├── Views/BulkOrder/                           BulkOrdering.cshtml, _Content.cshtml (tree + rows, the swapped region),
│                                              _Items.cshtml, _Grid.cshtml (cells rendered by a local function, no _Cell.cshtml), _PinnedStrip.cshtml
├── Views/Shared/Components/                   BulkOrderCategoryNavigation/, BulkOrderLink/, BulkOrderProductPage/ (Default.cshtml each)
├── Areas/Admin/Views/Shared/Components/BulkOrderProductPageAdmin/Default.cshtml
└── Content/BulkOrder/                         bulk-order.css, bulk-order.js
```

The unit tests are in `Tests/CW.Tests/Core` (with the rest of CW.Core's suite).

Layer rules that this design leans on:

- `BulkOrderGridBuilder` returns a **domain-shaped** grid (`Domain/BulkOrder/` records), not a view model.
  `Services/` may not reference `Models/` (`CLAUDE.md §5.1`).
- `BulkOrderLogic` has an interface and is registered `AddScoped<IBulkOrderLogic, BulkOrderLogic>()`
  (`§6.13`) — 4.1's bare concrete `BulkOrdersLogic` is what that rule exists to prevent.
- Every CSS/JS file needs its own `.csproj` `<None Update>` entry or it silently 404s (`§9.8`).
- Assets registered in the view that uses them, not globally — 4.6 registers both bulk scripts on every page of
  the theme (`NOP46` D13).

*`ContractItems` has no CW equivalent — see §16.

---

## 14. Routes, controller, permissions

| Route name (`CWCoreDefaults`) | Pattern | Action |
|---|---|---|
| `Plugin.CW.Core.BulkOrdering` (`BULKORDER_INDEX`) | `bulkordering` | `BulkOrder.Index` |
| `Plugin.CW.Core.BulkOrdering.Items` (`BULKORDER_ITEMS`) | `bulkordering/items` | `BulkOrder.Items` |
| `Plugin.CW.Core.BulkOrdering.Grid` (`BULKORDER_GRID`) | `bulkordering/grid` — `productId` and `pins` in the query string | `BulkOrder.Grid` |
| `Plugin.CW.Core.BulkOrdering.AddToCart` (`BULKORDER_ADDTOCART`) | `bulkordering/addtocart` (POST, JSON body) | `BulkOrder.AddToCart` |

Mapped in `RouteProvider.cs:104–122`; names in `CWCoreDefaults.cs:130–136`. (The `CW.BulkOrdering.*` names and the
`grid/{productId:min(1)}` segment this table first proposed were never built.)

Every action carries **both** gates — the feature gate on the controller class (`Controllers/BulkOrderController.cs:17`),
the permission on each action (`:47`, `:55`, `:63`, `:89`):

```csharp
[CheckCWFeature(CWFeature.BulkOrder, StoreSource = CWFeatureStoreSource.CurrentStore)]
[CheckPermission(CWPermissionConfigManager.PUBLIC_ACCESS_BULKORDERING)]
```

- **Feature toggle** — bulk ordering creates new cart lines, which `Feature_Toggles_Reference.md §0.1`
  classifies as *"consumes feature data on a new transaction"* → gate it. `StoreSource` is **mandatory** and is
  `CurrentStore` for public-store actions (§0.3); omitting it blocks the action and logs an error rather than
  falling back silently. Requires `CWFeature.BulkOrder = 5`, a `BulkOrder` member on `CWFeatureSet` and its
  indexer, and the flag probe in `CWFeatureService` alongside the existing four.
- **Permission** — `CW.CORE.PublicStore.AccessBulkOrdering`, declared in `CWPermissionConfigManager` following
  the existing constant + `AllConfigs` entry pattern (`Services/Security/CWPermissionConfigManager.cs`).

Together these close `NOP46` D6, where three of four read endpoints had no gate at all and
`BulkOrderingItem/{id}` applied no ACL to the product it rendered. `Grid` and `Items` additionally re-apply
store mapping and ACL to the products they touch — neither a feature flag nor a permission is a substitute for
row-level visibility.

---

## 15. Localisation

Key convention `Plugins.CW.Core.BulkOrder.*` (`CLAUDE.md §7`). Every user-visible string is a resource — 4.6
hardcoded roughly a dozen in C# and Razor (`NOP46` D19), including `"n in cart"`, `"Add to cart"`,
`"All Products"` and the filter names. `"n in cart"` becomes a format resource with the count as a parameter,
formatted in the **view**, not composed in the factory.

---

**An attribute-value picture must also be a picture of the product.** Found 2026-09-02, when check 6.4
rendered nothing with correct-looking data in both `ProductAttributeValuePicture` and `Picture`.
`ProductAttributeService.GetProductAttributeValuePicturesAsync` (`:351`) inner joins
`Product_Picture_Mapping`, so a value picture whose `Picture` is not among the product's own pictures is
dropped by the join and the method returns an empty list — no error, no warning, just no image. nop's admin
picker only ever offers the product's existing pictures, so the UI cannot create the broken shape; a SQL
insert can, and did. Anything seeding this data must write **both** rows.

## 16. Carried-over open items

1. **`ContractItems` filter.** 4.6's contract-items concept came from `IJim2AclService`
   (`ApplyContractItemsOnlyConstraint`, `GetContractedItemsCategoryIdAsync`) and has no `CW.Core` equivalent.
   Decide: drop the filter mode, or map it onto subsidy product groups. Until decided the filter list is
   All Products / Latest ordered / Best sellers.
2. **Favourites and Quote list.** 4.6 wires bulk ordering into a wishlist ("product enquiry") and a quote list,
   and forks the entire view set for Favourites (`NOP46` D20). Neither exists in `CW.Core`. Out of scope unless
   asked for.
3. **Refill ordering.** Jim2-API driven, out of scope.
4. **A third near-copy of nop's product-visibility predicate.** `BulkOrderService.ApplyVisibilityAsync` joins
   `ProductService.SearchProductsAsync` and `SubsidyCatalogCartService.SearchProductsAsync`. They agree today.
   Consolidating them behind one CW query builder is a real improvement and a real risk to the subsidy
   catalogue, so it is logged rather than attempted inside a bulk-ordering phase (`CLAUDE.md §0.5`).
5. **`Admin.StoreNav.init` has no `bulkOrder` line.** `Areas/Admin/Views/MultiStore/_CreateOrUpdate.cshtml:221–226`
   lists `packName`, `subsidy`, `exchanges`, `creditNotes`. Nothing is broken because there is no bulk-ordering
   admin nav item yet; it becomes a bug the moment one is added.
6. ~~**`Url.RouteUrl<Product>` is `[Obsolete]` in 4.9** and still used in four CW views~~ **Closed in 5.00:** no
   CW.Core view uses it; `PackNameCheckout/_CartByPackName.cshtml` and `_CartByProduct.cshtml` now call
   `NopUrl.RouteGenericUrlAsync<Product>`, as `_Items.cshtml` and `_Grid.cshtml` do. As written for 4.9:
   (`PackNameCheckout/_CartByPackName.cshtml`, `_CartByProduct.cshtml` and two others). It resolves
   `INopUrlHelper` through `EngineContext.Current` and blocks on `.Result`. The correct form,
   `await NopUrl.RouteGenericUrlAsync<Product>(...)`, is already used in `Exchanges/_ProductBox.cshtml` and
   `Order/Details.cshtml`; the other four are a cleanup.

   > **Correction, 2026-09-04.** This line used to end "Bulk ordering uses the correct form", and that was
   > false. `_Items.cshtml` does, but `_Grid.cshtml`'s §12.5 card used `Url.RouteUrl("Product", new { SeName
   > = ... })` — a *string route name*, which does not exist in 4.9 at all, since product URLs are generic
   > routes resolved through the entity type. `Url.RouteUrl` returned null, Razor omitted the `href`
   > entirely, and the card rendered a link-coloured anchor that went nowhere. Fixed to
   > `RouteGenericUrlAsync<Product>`. The lesson is the failure mode, not the API: **a missing `href` still
   > renders as a link**, so a route that resolves to nothing looks exactly like one that works, and only a
   > click or the DOM inspector tells them apart. Anywhere a URL is built from a route name, that is worth
   > clicking once.

---
7. ~~**Combination stock is not reflected in the grid.**~~ **Closed in P5.** `BulkOrderPricingContext.StateFor`
   reads `combination.StockQuantity` / `AllowOutOfStockOrders` and the cell renders nop's own wording
   ("Out of stock", "Available in 2-4 days", "Backordering") — see the P5 section of the phase plan.
8. **`ManageInventoryMethodId = 2` products with the flag off get no benefit from their combinations
   beyond the Sku.** 39 live products are in that state. If the intent was per-variant stock control, the
   store's own settings are wrong, not the grid. Worth raising with the client rather than working around.
9. **An OPTIONAL combinable attribute on an axis makes the short-signature combinations unreachable.**
   Found 2026-09-01 while running check 2.4 against `ZZT-05` (Size x Colour x optional Fit, restricted,
   6 short + 4 long combinations). Fit is a dropdown, so `OrderEligible` puts it on axis 3; every cell then
   carries a Fit value, the target signature is always {Size, Colour, Fit}, and only the 4 long-signature
   rows can match — the grid renders **M only**. The 6 short-signature combinations, which nop would sell
   perfectly happily to a customer who leaves Fit blank, have no route in the grid at all. That is the §7.4
   failure in a new place: **the grid hides purchasable options.**
   ~~It bites only *restricted* products (an unrestricted one shows every value regardless)~~ — **corrected
   2026-09-05 (review §2): it bites every product.** An unrestricted product shows every *value*, not the
   *absence* of one; nop's product page offers an optional attribute's `---` option and sells the line
   without it, and the grid cannot. 4.6 appended an empty value to every optional attribute for exactly this
   (`NOP46` reference §7). **Decided:** an optional axis gets a synthetic *(none)* value contributing no key
   part — the second of the three candidates. Built in P6b; becomes §7.6 when it lands. The other two
   candidates (pin-only optional attributes; leave it to the customer) are rejected: the first removes
   optional colours from the axes, the second hides a purchasable option.

## 17. Test plan

Kept in this document per `CLAUDE.md §0.4` — a phase's test plan lives inside that phase's document.

### Grid construction

| # | Case | Expected |
|---|---|---|
| T1 | Product, 2 required attributes, full combination set | Grid = |A1| columns × |A2| rows, no dashes |
| T2 | Same, 20% of combinations deleted | Exactly the deleted cells render `–`, are not enterable, and are absent from the POST |
| T3 | Product with 7 attributes as in §7.5 | Axes = Size, Colour, Fit; pinned = Sleeve, Logo, Embroidery, Name; cell count = distinct (Colour,Fit) tuples × Size values |
| T4 | Setting `[Size, Colour, Sleeve, Logo]`, product has Colour, Logo, Material | Axis 1 = Colour, axis 2 = Logo, axis 3 = Material; nothing pinned that the product does not have |
| T5 | Product with a 20-value `Checkboxes` attribute | It is pinned, never an axis; its ticked values reach the cart line; cell count unaffected by it |
| T6 | Optional combinable attribute, mixed signatures present | Pin blank → short-signature rows match; pin set → long-signature rows match. **Both** verified against `FindProductAttributeCombinationAsync` for the same XML |
| T7 | Product with zero combinations | Full grid, no dashes, product-level stock and price repeated per cell (D5) |
| T8 | Axes would exceed `MaxCellsPerProduct` | Last axis demoted to pinned, notice shown, cell count under budget |
| T9 | 6+ axis-eligible attributes, `MaxAxisCount = 5` | Exactly 5 axes; the rest pinned in the §5.2 order |

### Correctness against nop

| # | Case | Expected |
|---|---|---|
| T10 | For 30 random cells across the page | `cellKey` lookup returns the same combination `FindProductAttributeCombinationAsync` returns for the composed XML. This is the assertion that the fast path did not change meaning. |
| T11 | Cell price | Equals `ShoppingCartService.GetUnitPriceAsync` for the same product/customer/attributes. Compute **independently**, not by re-running the grid code — `tasks/lessons-4.9.md` #13: two surfaces agreeing proves consistency, not correctness |
| T12 | Combination with `OverriddenPrice` set | Cell price uses the overridden path, not `base + adjustments` |
| T13 | Percentage `PriceAdjustmentUsePercentage` value | Adjustment matches `GetProductAttributeValuePriceAdjustmentAsync` |
| T14 | `AttributeValueType.AssociatedToProduct` value | Adjustment = associated product final price × `value.Quantity` |

### Add to cart

| # | Case | Expected |
|---|---|---|
| T15 | Submit 12 lines across 4 products, all valid | 12 cart lines; grid cleared; counters updated; **no page reload** |
| T16 | One line exceeds stock | That line rejected with its warning shown **on its own cell**; the other 11 committed (D6); the rejected quantity still in the box |
| T17 | Submit a cell that renders as a dash (crafted POST) | Rejected server-side — the dash is a UI state, the server must not rely on it |
| T18 | A packname is in context | Every created line has a `PackNameCartItem` row (D8) — verified by querying the table, not by reading the code (`tasks/lessons-4.9.md` #10: verify the guard *fires*) |
| T19 | Product ids 12 and 123 on the same page | Adding from product 12 adds **only** product 12's lines (`NOP46` D3 must not reappear) |
| T20 | Line with a pinned textbox value | **5.00:** nothing posted for a text, date or file attribute is written (§11.1) — the line's `AttributesXml` carries no entered text. (4.9: the entered text reached `ShoppingCartItem.AttributesXml`.) |

### Performance

| # | Case | Expected |
|---|---|---|
| T21 | 15-product page, ≥300 cells each | Query count is flat (~8) and independent of cell count. Measure with a profiler or SQL trace, not by inspection |
| T22 | Same page, repeat request | Grid skeleton served from cache; only customer-specific work repeats |
| T23 | Edit a combination in admin | The affected product's skeleton cache is invalidated and the next render reflects the change |

### Regression against the 4.6 defect list

T24: walk `NOP46` reference §19 D1–D20 and assert each is either fixed or explicitly out of scope. D1, D2, D3,
D5, D6, D13, D14, D15, D17, D19 are fixed by this design; D9, D10, D20 concern Favourites, which does not exist
in `CW.Core`.

### Consistency

T25 — the negative twin (`tasks/lessons-4.9.md` #19: a query that finds *"rows where A exists but B is wrong"*
cannot detect *"A was never created"*): for a submitted page, assert both that every created cart line maps to a
combination that was rendered, **and** that every cell with a non-zero typed quantity produced either a cart
line or a reported failure. Neither check alone is sufficient.

---

## 18. What this design does not do

Listed so the next reader does not mistake absence for oversight. All are in
`BulkOrdering_Future_Improvements.md` with the trigger for revisiting each: subsidy budget validation (F1),
atomic all-or-nothing add (F2), page-level pin defaults (F3), collapsible cards with lazy grid loading (F4),
per-line print name (F5), multi-select category filter (F6), gender filter (F7), AJAX/append paging (F8),
"order on behalf" packname selector with PO/branch capture (F9), a live availability/ETA provider (F11),
the `ContractItems` filter (F12), Favourites/Quote list (F13), an admin surface (F14), data-derived
call-for-price (F15), role-gated stock quantities (F16), packname-scoped order history (F17), lazy
combination indexing (F18 — closed by P7), rendered-only case-3 values (F20), a per-cell "in cart" marker
(F21), and — from the 2026-09-05 review — tier prices, old price, pre-order wording, call-for-price
enquiries, ± tap targets, bundle stock, shared `ManageStock` stock, a per-card text pin and the no-JS
wording (F22–F30).

---

## 19. The grid on the product details page — added by P8 (2026-09-09)

The `BulkOrderEnableOnProductPage` path: the bulk grid replacing the stock buy controls on one product's own
page. §12.3 already holds the per-product override; this section holds the mechanism, because the mechanism is
the design decision.

### 19.1 Why not a widget zone with suppression around it

The obvious shape — render the grid at a zone and hide the partials near it — cannot work, and the repository
settles it. Both product templates present here disagree about order:

| in the overview column | `Nop.Web` | `Themes/CW.StandardTest` |
|---|---|---|
| `_ProductAttributes` | :104 | :138 |
| `_ProductPrice` | :123 | :109 |
| `ProductDetailsOverviewTop` | :87 | :117 |
| `_AddToCart` | :130 | :159 |

(`Nop.Web` = `Views/Product/ProductTemplate.Simple.cshtml`, the same lines in 5.00; `CW.StandardTest` is read from
the 4.90 source — it is not ported, `CLAUDE.md` §3.2.) A zone's position relative to the partials is a property of the **theme**, not of nop. 4.6 could edit
`ProductTemplate.SimpleVoyage.cshtml` because every client ran that theme; the requirement here is any theme,
including ones not yet written.

### 19.2 The model is the contract

Every partial on that page is gated on `ProductDetailsModel`, and a theme may reorder, restyle or replace
markup but cannot change what the model contains. So the page is changed by emptying fields, in
`EventConsumer`'s `IConsumer<ModelPreparedEvent<BaseNopModel>>` — published from `BaseController`'s
`[PublishModelEvents]` after the action and before any view runs.

| partial | its guard | what is set |
|---|---|---|
| `_ProductAttributes` | `ProductAttributes.Count > 0` | `Clear()` |
| `_AddToCart` | `!DisableBuyButton \|\| CustomerEntersPrice` | both — the second is an OR |
| `_AddToWishlist` | `!DisableWishlistButton` | `true` — hidden, not replaced |
| `_Availability` | stock string or back-in-stock flag | cleared, `false` |
| `_ProductTierPrices` | model-derived | `TierPrices.Clear()` |
| `_ProductEstimateShipping` | `Enabled` | **untouched** — delivery, not selection |

Each guard was read in the shipped partial. If a future nop changes one, this fails silently — a working stock
control beside the grid, a page with two ways to buy that disagree. Acceptance therefore tests for **absence**.

The wishlist is hidden rather than replaced because it cannot be left standing: with `ProductAttributes`
emptied its form posts no attribute values, and nop rejects that for any product whose attributes are
required. Hidden beats broken; a real wishlist path is its own scope.

### 19.3 Three zones, one render

The component is registered on `ProductDetailsOverviewBottom`, `ProductDetailsBottom` and `ContentAfter`, in
that preference order, with a per-request flag making all but the first a no-op. This is not redundancy for
its own sake: an emptied model plus a missing zone is **a product page with no way to buy**. Zones 1 and 2 are
both declared in `ProductTemplate.Simple.cshtml` — the file a theme most often rewrites, so one edit loses
both. `ContentAfter` is declared in `Views/Shared/_Root.cshtml`, so losing it as well takes a second,
independent decision to gut the layout root, which breaks widgets generally rather than only ours. The
degradation is graded: right place, worse place, foot of the page, and only then nothing.

`ContentAfter` renders on every page in the store, so the component's first act is a null check on the
request-scoped context — no service call, no route inspection. The zones are mapped in `CWCorePlugin.cs:33–35`;
in 5.00 the component returns `ViewAsync` (only it raises nop 5.00's *model prepared* event for a view component).

**5.00 (part 5h, fixed in 4.9 too):** in the overview zone the grid sits inside nop's product form, which already
has its antiforgery field. The component's own second field of the same name made nop's *Estimate shipping* post
fail; it is gone, and the script reads the field nop 5.00 writes at the top of every page (`_Root.Head`).

### 19.4 The gate builds the grid rather than predicting it

`IBulkOrderProductPageService.TryPrepareAsync(int productId)` returns the grid or `null`, and `null` means
suppress nothing. Conditions run cheapest first — feature flag, then permission, then the override (one
per-request-cached query), then the build. There is deliberately **no product-type check**: the build's first
act is `UnsupportedReasonFor`, whose opening line returns `Grouped` for anything that is not a simple product.
The same call also covers gift cards, rentals, customer-entered prices, recurring products, disabled buy
buttons and required text attributes. A predicate beside it would be a second, cruder copy of a judgement the
builder already makes, and the two would drift. One check does follow the build: a grid that can hold at most one
quantity box is not drawn (`RendersAtMostOneCell`), because the page already has nop's own box; the list keeps the
single cell (`Services/BulkOrder/BulkOrderProductPageService.cs:122–160`).

Assets cannot be registered by the component — a component invoked from a widget zone runs while the BODY is
rendering, and `PageRenderingEvent` (`_Root.Head.cshtml:48`) has passed. They are registered there instead,
conditionally, so a page that draws no grid loads neither file. The list page gets away with appending inside
`BulkOrdering.cshtml` only because a view body executes before its layout.

### 19.5 Two flows the grid stands aside for

- **Editing a cart line.** `AllowCartItemEditing` re-renders this page as an update form
  (`ProductController :164`), with the line id on the model and the button reading Update. Our grid adds and
  has no concept of updating a line, so suppressing here would strand the customer with nothing to edit.
- **A product the grid cannot express.** On the LIST, §12.5 renders an unsupported card, because the
  alternative is the product vanishing from a page that counted it. Here nop's own working controls are the
  fallback, so we take nothing away. **The rule differs from the list because the situation does.**

---

## 20. Getting to the screen — added by P10 (2026-09-09)

Two entry points: the post-login redirect `BulkOrderSendUserAfterLogin` promises, and a storefront link.

### 20.1 One gate, two questions

`IBulkOrderEntryService` answers both, and they are not the same question:

| method | asks | used by |
|---|---|---|
| `CanUseBulkOrderingAsync(customer)` | feature on for the store **and** customer holds `PUBLIC_ACCESS_BULKORDERING` | the header link |
| `ResolveLandingRouteAsync(customer)` | that, **and** `BulkOrderSendUserAfterLogin` | sign-in, `Impersonate`, `PlaceOrder` — through `IPackNameLandingService` |

**Since P10 (subsidy phase 7, carried into 5.00):** the three landing call sites no longer call
`ResolveLandingRouteAsync` directly. They ask `IPackNameLandingService.ResolveSignInLandingRouteAsync`
(`Services/PackNames/PackNameLandingService.cs:86–91`), which tries the subsidy catalogue first
(`SubsidySendUserAfterLogin`, Subsidy on, the selected packname has active product groups) and only then asks
`ResolveLandingRouteAsync`. Callers: `CustomerRegistrationOverrideService.SignInCustomerAsync`
(`Services/Customers/CustomerRegistrationOverrideService.cs:159`) and
`ExtendedCustomerController.RedirectAfterImpersonationAsync` (`Areas/Admin/Controllers/ExtendedCustomerController.cs:1092`),
used by `Impersonate` and `PlaceOrder`. The header link still calls `CanUseBulkOrderingAsync`
(`Components/BulkOrderLinkViewComponent.cs:54`).

The second is defined in terms of the first. Four call sites must agree about who qualifies, and the failure
mode of disagreement is a link that appears for a customer the redirect skips — or a redirect to a page that
then refuses them. The permission is checked against the customer **passed in**, never the work context: the
impersonation path is asking about the customer being impersonated, not the admin.

### 20.2 Sign-in is overridden at the service

`CustomerRegistrationService.SignInCustomerAsync` is `virtual`, is where the destination is decided, and is
what *every* route into the store calls — login, registration, external authentication. Overriding it once
covers all three; 4.6 patched each of its own logon call sites and kept them in step by hand.

**5.00 exception — sign-in by phone code.** nop 5.00's `OtpSettings.LoginByPhoneEnabled` signs the customer in
without going through this path, so the landing (and the packname set-up at sign-in, and the guest-cart move) do
not run. Decided 2026-10-29: keep it off on packname stores (`CWCore_Port_Reference.md`, phase 5h).

The base runs first and unchanged, so cart migration, the authentication cookie, `CustomerLoggedinEvent` and
the activity log are nop's. Only the destination is reconsidered, and only when nop had nowhere particular to
send the customer.

**What "nowhere particular" means, and the trap in it.** The base returns a `RedirectToRouteResult` named
`HOMEPAGE` for its default and a `RedirectResult` when it honoured a local `returnUrl`. Testing only for the
first looks correct and is wrong for the commonest path in the store: logging out lands on the homepage, the
login link carries `returnUrl` pointing at it, and the customer gets `RedirectResult("/")` — the homepage
expressed differently, so the redirect never fires. `IsHomepageDestination` therefore asks about the
**destination**: the homepage route, or a URL that is the site root in one of its few spellings. Anything with
a path — a product page, a cart, a checkout step — is somebody in the middle of something and still wins.

### 20.3 Impersonation is a separate mechanism

It never touches sign-in: `ExtendedCustomerController.Impersonate` and `PlaceOrder` write the
`ImpersonatedCustomerIdAttribute` (`ImpersonateAsync`) and redirect through `RedirectAfterImpersonationAsync`.
Both consult the same gate.
`area = string.Empty` is required on the redirect — these run inside the Admin area, so `area` is an ambient
route value that would otherwise be carried into a URL for a public route that has none.

### 20.4 The link

`PublicWidgetZones.HeaderLinksBefore`, one zone. **Not** P8's three-zone net: there the net existed because an
emptied model plus a missing zone is a product page nobody can buy from, and here the worst case is a missing
link with the URL still working. Same reasoning, different consequence, different answer.

Gated on `CanUseBulkOrderingAsync` alone, so it appears whether or not the store redirects after login. The
component renders on every page, so that gate is its first act and is two cached reads. Styling is left to the
theme: `ico-bulk-order` follows nop's own `ico-` convention for that list, so a theme restyling its header
links picks it up without knowing the plugin exists.
