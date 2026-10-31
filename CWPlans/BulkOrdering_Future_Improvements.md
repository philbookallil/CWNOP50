# Bulk Ordering — deferred improvements

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

> **Purpose.** Things deliberately left out of `BulkOrdering_Design.md` (4.9: `BulkOrdering_49_Design.md`), each with why it was deferred and
> **what would trigger revisiting it**. This is a parking list, not a backlog commitment.
>
> Nothing here blocks the first implementation. Add to it rather than widening the design.

---

| # | Item | Why deferred | Trigger to revisit |
|---|---|---|---|
| **F1** | **Subsidy budget validation on the bulk screen.** Dry-run the whole submission through `ISubsidyAllocationService` before adding, and tell the customer what the subsidy covers and what they will pay. | Decision D7 — subsidy stays a cart/checkout concern, as in 4.6. | First support ticket of the form *"I added 40 lines and only found out at checkout that the subsidy covered 12."* Also revisit if the bulk screen is opened to packname managers ordering on behalf of employees (F9), where the mismatch is worse. |
| **F2** | **Atomic all-or-nothing add.** Validate every line of the submission and commit only if all pass, instead of D6's commit-what-passes. 4.1 did this per product (`BulkOrdersLogic.AddBulkProductsToCart`, `NOP41/Nop.Plugin.TIG.Base/BusinessLogic/BulkOrdering/BulkOrdersLogic.cs:182`). | D6 keeps 4.6's outcome semantics. The per-cell error reporting in design §11.3 removes most of the pain — the customer can see exactly what failed and retry just that. | If partial adds cause real double-ordering in practice. The design's request shape (§11.1) already supports it: the change is a validation pass before the commit loop, not a reshape. |
| **F3** | **Page-level pin defaults.** Set a pinned attribute once at the top of the page; every product card that has that attribute pre-fills it, overridable per card. | Explicitly deferred (D10) — "let's keep it simple for now". | A catalogue where most products share the same pinned attributes (e.g. one embroidery position across 200 garments) and customers complain about setting it per card. |
| **F4** | **Collapsible product cards with lazy grid loading.** 4.1 collapsed cards (`Scripts/bulk-orders.js:127`, `:392`); collapsing also reset that card's inputs. Combined with lazy loading, a collapsed product's grid is never built. | The design's cost model (§10) makes building all grids up-front cheap enough that lazy loading is not needed for performance. Collapsing is purely a UX question. | Pages that are visually overwhelming, or a measured page-build cost that justifies it after all. |
| **F5** | **Per-line print name.** `MultiStoreSettings.PrintNameProductAttributeId` maps the packname's print name onto a product attribute. Under D9 that attribute is pinned, so one value applies to every line — but a print name is per person. | When deferred (2026-08-28) the setting had **no consumer** in `CW.Core` beyond admin settings plumbing. **No longer true:** `Services/PackNames/PrintNamePrefillEventConsumer.cs` now reads it (`:67–71`, with `PrintNameEnabled`) to pre-fill the packname's print name into that attribute on the product page; the setting is `Domain/Settings/MultiStoreSettings.cs:71`. The consumer acts only on nop's `ProductDetailsModel` (`:55`), so the bulk grid still pins the attribute and is not pre-filled — and the grid writes nothing for a text attribute anyway (design §11.1). | The moment that setting populates a cart line from the bulk screen. At that point a pinned print name becomes wrong and the cell needs a per-line text input — which also interacts with F9. |
| **F6** | **Multi-select category filter, with Clear.** 4.1 used a jsTree with checkboxes and persisted the selection (`Scripts/bulk-orders-filter.js`). The server query already takes a `categoryIds` **list**; 4.6's UI just never sends more than one. | Parity with 4.6 is single-select. | Bulk buyers working across several categories in one session. Cheap to add — the service signature already accommodates it. |
| **F7** | **Gender filter.** 4.1 filtered by a `Gender` **specification attribute** (`BulkOrderController.cs:404`), gated by a setting. | 4.6 has none, and it is unclear whether CW.Core should filter by product specification attribute or by the selected packname's gender — `MultiStoreSettings` has both `GenderEnabled` and `GenderFiltering`, and `PackName.Gender` exists. | Deciding which of the two mechanisms is meant. **[unverified]** — what settles it: read how `MultiStoreSettings.GenderFiltering` is consumed in `CW.Core` today. *(2026-10-01: it was never read, and subsidy phase 5 Part 2 deleted it; a gender filter is now a new feature to specify.)* |
| **F8** | **AJAX paging / load-more.** 4.6's pager is a full page reload (`BulkOrderingPaged`), which destroys typed quantities. 4.1 appended pages (`fetchBulkProductsList`, `bulk-orders.js:453`). | The design keeps a pager for parity. Note the item list is already AJAX — making the pager call `loadItems()` instead of navigating is a small change. | Any report of lost quantities on page change. If "load more" (append) is wanted rather than AJAX paging, first decide whether **Add all** means the loaded set or the current page. |
| **F9** | **"Order on behalf" packname selector, with PO / comment / branch code capture.** 4.1 chose the packname on the bulk screen itself and carried subsidy mode, PO/comment (+ type) and branch code on the request into `CartItemOptions`, stored per cart line and per order line. It also had a **person mode** where the card switched from a matrix to one quantity plus a single attribute pick. | D8 — packname attachment is ambient and already correct in `CW.Core`. The extra capture and person mode are a materially larger screen. | A business requirement to capture PO/comment/branch per bulk submission, or to order for a named person without switching packname context first. Person mode is the expensive half; the capture fields are the cheap half and could land alone. |
| **F10** | ~~**Conditional attributes that depend on an axis attribute.** Design §9 rule 3 forces such an attribute to be pin-only, because its condition cannot resolve per cell.~~ | **Built in P4, 2026-09-02** — design §9.1 case 3. The row is kept because the reason for deferring it was *wrong*: the condition resolves per cell for free, since a cell already names exactly one value per axis. Its `[unverified]` query is also answered — 401 conditional mappings across 221 products, so this area was never theoretical. | — |
| ~~**F19**~~ **DONE 2026-09-05** — closed by P9 I8, which stopped hiding the control at all: it renders disabled, so its value never leaves the DOM and survives the round trip. See design §9.1, "An unmet condition shows itself". Original entry: | **A hidden pin's selection is lost rather than remembered.** Tick `Embroidery = Yes`, choose `Print style = Puff`, untick Embroidery, re-tick it: Print style comes back at its **default**, not Puff. | Design §4.3 rule 1 — "the DOM owns the pin state". `collectPins` and `reloadGrid` read the pins out of the DOM, and a pin hidden by §9.1 case 1 or 2 has no DOM node to read, so its value never reaches the next request. nop's own product page differs here: it renders the conditional field always and only toggles its *visibility*, so the value survives the round trip. | **P6.** With nothing submitted this is a cosmetic annoyance; once a conditional attribute can be *required* and carries a price adjustment, silently resetting it is a real defect. The cheap fix is a hidden input carrying the last selection for hidden mappings — the server already ignores hidden mappings when it builds `pins`, so nothing downstream changes. The expensive fix (render hidden pins with `display:none`) also changes what "hidden" means and should be avoided. |
| **F20** | **Case 3 tests *all* of the depended-on attribute's values, not the *rendered* ones.** `SatisfyingAxisValueIds` reads `data.Values[dependsOn]`. On a **restricted** product `Compose` may then drop that value from the columns entirely, leaving a pin marked "Applies to Navy only" when no Navy column exists — it is shown but reaches nothing. | Design §9.1 case 3 says "at least one **rendered** A value", so this is a divergence from the design's own words, not from nop. It cannot be fixed in place: the rendered set is computed inside `Compose`, after the pin decision has been made, so honouring it means either a second `Compose` pass or moving the column scan ahead of the condition loop. | Needs all three of: a restricted product, a case-3 condition, and the satisfying axis value present in **no** combination. The measured client catalogue has **zero** restricted products, so it cannot occur there. Revisit if a client's data ever produces a restricted product with conditions — the settling query is the §7.4 restriction probe joined to `ConditionAttributeXml IS NOT NULL`. |
| **F11** | **A live availability / ETA provider.** 4.6 showed per-warehouse stock, first-PO date and incoming quantity from the Jim2 API. `CW.Core` has no equivalent, so the ETA columns are inert. | Design §10.4 keeps the model fields and an `IBulkOrderAvailabilityProvider` seam; the default implementation returns nop stock and no ETA. | A CW stock integration existing. The seam is the whole preparation — no further design needed until then. |
| **F12** | **`ContractItems` filter mode.** 4.6's fourth filter came from `IJim2AclService.ApplyContractItemsOnlyConstraint` / `GetContractedItemsCategoryIdAsync`. | No `CW.Core` equivalent exists. | Deciding whether the concept maps onto subsidy product groups, or is dropped for good. Until then the filter list is All Products / Latest ordered / Best sellers. |
| **F13** | **Bulk ordering for Favourites and the Quote list.** 4.6 wires the bulk screen into a wishlist ("product enquiry") and a quote list, and forks the entire view set for Favourites — ~230 duplicated controller lines and a parallel copy of every view. | Neither feature exists in `CW.Core`. | Either feature being ported. If it is, the grid builder and the add-to-cart contract are already cart-type-agnostic; only the endpoint and the destination differ. **Do not fork the views.** |
| **F14** | **An admin surface for bulk ordering.** 4.6 has none — everything is store settings. | Parity. | A need to preview a product's grid from admin, or to see which products are excluded from the bulk list and why (which is also the honest fix for 4.6's silent omission, `NOP46` D5). |
| **F15** | **"Call for price" derived from data** — 4.6's `HandledNoPrice` (a price of 0 or null becomes call-for-price) and `HandledNoStock` (out of stock becomes call-for-price). | DQ2 — not ported. `Product.CallForPrice` stays the only route to a call-for-price cell, which keeps the disabled-cell rule to four clauses. | A business need to hide prices on unpriced or unavailable lines rather than showing 0 or disabling the cell. Cheap if wanted: two booleans on `MultiStoreSettings` and two clauses in the availability provider. |
| **F16** | **Role-gated stock quantities** — 4.6's `ShowStockQty` + `ShowStockQtyCustomerRoleIds` decided whether a customer saw the number or just "In stock" (`ProductOverrideService.cs:2709`). | DQ4 — bulk ordering shows stock exactly as the rest of the site does. A bulk-order-only rule would be inconsistent with every other product surface. | A **site-wide** stock-quantity visibility policy being introduced. At that point bulk ordering should adopt it, not invent its own. |
| **F17** | **Packname-scoped order history on the bulk screen.** The "From order" picker and **Recently ordered** currently list the orders of the **logged-in customer**. Scope them instead to orders containing at least one item for the **packname in context**, and restrict the products to that packname's items within the order. | 4.6 does exactly what we do: `BulkOrderModelFactory.SetAvailableOrders:277` calls `SearchOrdersAsync(storeId, customerId)` and `BulkOrderServices.SearchLatestOrdersItemsAsync:525` filters `customerId == o.CustomerId`. No packname appears anywhere in 4.6's bulk-order path - the only extra scope is a **branch** filter inside `PreOrderServices.SearchOrdersAsync:162`, which `CW.Core` has no equivalent for. Left as-is pending the manager's judgement on whether the current behaviour is a problem in practice. | A manager reporting that picking a past order re-lists **other people's** items. The CW.Core model supports the change - `PackNameOrderItem` keys on `OrderItemId` and carries `PackNameId` - so a manager's single checkout for five people is one order holding five packnames' items, and the customer-scoped picker mixes them. For a customer with one packname the two behaviours are identical, so this only changes manager behaviour. **Cost:** a `PackNameOrderItem` join in `ProductIdsOfOrder`, `ProductIdsOfRecentOrders` and `GetCustomerOrdersAsync`, **plus the packname id in `BulkOrderServicesDefaults.VisibleCategoryIdsCacheKey`** - the Recently ordered tree becomes packname-dependent, and two packnames with no subsidy restriction share allocation fingerprint 0 today, so omitting it would reopen the cross-packname leak that check E2 exists to catch. |
| ~~**F18**~~ **DONE 2026-09-08 — closed by P7**, the grid-skeleton cache (design §10.5): each combination's XML is parsed once per skeleton build, not per request. Original entry: | **`BuildIndex` parses every combination even when the grid consumes almost none of them.** `LoadAsync` loads all `ProductAttributeCombination` rows and `ParseCombinationKey` walks each one's XML before the budget pre-check has had a chance to demote. A 432-combination product renders 72 cells; a real product with several thousand combinations pays several thousand `XmlDocument` loads to serve a few dozen `Sku` lookups. | Unchanged by the §7.4 revision, but newly visible: on an unrestricted product the index is now **only** a source of `Sku` and stock, never a gate, so the work is no longer load-bearing for correctness. | The grid skeleton cache (§10.5) landing — that alone removes the repeat cost. If profiling still shows it after that, the options are to index lazily on first cell miss, or to project `Id`/`Sku`/`StockQuantity`/`AttributesXml` rather than materialising whole entities. Do **not** drop the index to avoid the cost: `Sku` on the cell is a 4.6 parity feature. |

## F21 — a per-cell "in cart" marker (added 2026-09-04, from P6)

Design §11.3 originally asked for "the card's 'n in cart' **and the cell's 'in cart**' refreshed" after an add.
The card-level figure is built. The cell-level one **never was** — not in P3, P4, P5 or P6 — and §11.3 has been
corrected to describe only what exists. It was not dropped by P6; nobody ever built it, and it surfaced only
when P6 went to refresh it.

**What it would show.** Which *size and colour* is already in the cart, not just how many of the product
overall. That is genuinely more useful on a grid than the aggregate — a customer re-ordering a run of shirts
wants to know they already have twelve M/Navy.

**What it costs.** Each cart line's `AttributesXml` mapped back to a cell key, per product, on every list
render — a second pass over the cart plus a parse per line, on a page that renders up to 100 products. The
grid skeleton is customer-independent and cached (§10.5); this is not, so it cannot ride along with it.

**Trigger.** A customer asking for it, or the per-customer pass becoming cheap enough (P7, since built, cached only the customer-independent skeleton) that it
stops being a page-render cost. Not before: the card figure answers the common question, and P6's response
already carries the per-product map it would extend.


## F22–F30 — from the design review of 2026-09-05

Logged from `BulkOrdering_49_Review.md` §4, which held the reasoning; that review was folded into the design and the
phase plan and then deleted (2026-09-07), so each row here is now the whole record.

| # | Item | Why deferred | Trigger to revisit |
|---|---|---|---|
| **F22** | **Tier prices are invisible on the card.** The cell price is the qty-1 (or cart-grouped) price; nop's product page shows the tier table and prices the line at its quantity. 4.6's product-page gate refused tier-priced products outright. | Pricing a cell per typed quantity is a request per keystroke. | Review R4 count. Cheapest honest form: a "from {0} at {1}+" note in the card header from `GetTierPricesAsync`. |
| **F23** | **Old price / RRP not shown.** 4.6 showed RRP and "% saved" (Jim2-priced); nop's catalogue shows `OldPrice` struck through. | Parity with nop's *cart*, which was the target; the catalogue's price markup was never in scope. | R4 count. `Product.OldPrice > Price` → nop's own `prices.old-price` markup in `_Items.cshtml`. |
| **F24** | **Pre-order is not indicated.** `AvailableForPreOrder` / `PreOrderAvailabilityStartDateTimeUtc` change nop's button to "Pre-order" with a date; 4.6 said "Items with no availability will be pre-ordered". The cart applies no pre-order rule (no reference in `ShoppingCartService.cs`), so this is wording only. | Wording only. | R4 count. |
| **F25** | **Call-for-price → wishlist/enquiry.** 4.6 turned a call-for-price cell's button into add-to-wishlist. nop's own wishlist exists; the request needs one `ShoppingCartType` parameter. F13 was about 4.6's *forked* wishlist and skipped nop's own. | No store has asked. | A store wanting enquiries from the bulk screen. |
| **F26** | **± tap targets.** 4.6's `ShowQtyArrows` were explicit increase/decrease buttons; ours is the number input's native spinner, which phones do not render, so `BulkOrderHideQtyArrows` hides a control already absent where it matters. | Belongs to the phone design. | Was to fold into I14 (done 2026-09-09). **[unverified]** whether I14 added explicit ± buttons — `Content/BulkOrder/bulk-order.js` has no increase/decrease handler, so probably not; still open. |
| **F27** | **Bundle (associated-product) stock is not reflected in the cell.** nop validates the associated product's stock per line (`ShoppingCartService.cs:970–1004`); T14 covers the price only. | Zero live values (P3 measurement). | ✅ **DONE in P6b.** `BulkOrderPricingService.GetBundleLimitAsync` caps the cell at the associated product's stock and blocks it with that product's own message at zero. Coverage: `ZZT-56` + `ZZT-56B`. |
| **F28** | **`ManageStock` stock is one figure repeated in every cell.** nop validates each attributed line alone (`:427–432`), so twelve cells at `max=10` can add 120 units of a 10-stock product. A card-level sum would be *stricter than nop*. Exception nop does enforce: a product with a non-combinable attribute sums the cart (`:434–460`), which the I9 clamp cannot see. | Parity, deliberately. | R5 count; a client asking for the stricter behaviour explicitly. |
| **F29** | **A per-card text pin.** 101 live products are refused per cell for a required conditional text attribute. `BulkOrderSelection.EnteredText` still travels, but since part 5h (5.00, fixed in 4.9 too) `BulkOrderLogic` writes nothing posted for a text, date or file attribute (design §11.1); building this means writing the per-card text through nop's own parsing of those types, then one `<input>` in the strip written onto every line the condition applies to. F5's catch stands: "Print Text" is per *person*, and one value per card is wrong whenever a card is several people. | Same reason as F5. | A client saying the 101 are bulk-ordered with one text per line. Decide with F5 and F9. |
| **F30** | **No-JS fallback for the controls.** Only the first render and the category links work without JavaScript. | Design §4.3 rule 3 now says so; a form-post fallback is not worth building. | Never, unless accessibility policy demands it. |


---

## Not on this list, deliberately

These were considered and rejected outright rather than deferred — recorded so nobody re-proposes them:

| Item | Why not |
|---|---|
| Attribute roles detected by **name** (`"colour"`, `"size"`, …) as 4.1 did (`ProductsHelper.cs:29`–`:62`) | Breaks on translation, on `"Collar"`, on `"Oversize"`. The settings-driven ordered axis list (design D3) is strictly better. |
| Subsidy arithmetic reimplemented in JavaScript, as 4.1 did (`packname-selector.js:581`) | 4.9 lesson #11 (`tasks/lessons-4.9.md`) — four independently-written copies of the subsidy walk, one of which disagreed with the others. If F1 lands, it asks `ISubsidyAllocationService` and renders the answer. |
| One HTTP POST **per product** on add-all (4.1's `bulkAddItemsPromise`) | The design's single request with a per-line result set gives the same UX with one round trip. |
| Rendering the attribute-value **cross-product** and disabling impossible cells | The thing this whole design replaces. It is what makes >3 attributes impossible. |
| Encoding selections into form-field **names** (`itemquantity_12_5_77-6_91-7_15`) and string-splitting them server-side | 4.6's approach; per-arity parser, no bounds checks, duplicated for Favourites, and the direct cause of `NOP46` D3. Design §11.1 uses data attributes and JSON. |

---

## F31 — "the maximum quantity that can be added is N" ignores what is already in the cart

**Raised 2026-09-07, from P6b's L4.** With 6 of a cell in the cart and a stock of 10, typing 8 is refused with
*"The maximum quantity that can be added is 10."* — which reads as though 8 should have been fine.

nop's own wording. `GetQuantityProductWarningsAsync` (`ShoppingCartService.cs:559`, the message at `:576`) formats
`ShoppingCart.QuantityExceedsStock` with `maximumQuantityCanBeAdded`, the raw ceiling; the merge appears only
on the other side of the comparison, where `AddToCartAsync` (`:1629`) passes `existing + requested`. Every nop
screen says it this way.

**Not a bulk-ordering change.** §11.2 forbids telling the customer two different things about one rule on two
screens, so the grid must not rewrite this sentence alone. If a store wants it clearer it is a store-wide
resource change, and the honest phrasing needs a second placeholder (`{0}` in stock, `{1}` already in your
cart) which means overriding the resource for every screen at once.


---

## F32 — `PackName` is read once per card, always with the same argument

**Raised 2026-09-08, from P7's T21b trace.** The warm 15-product page runs the packname query **11–12 times**,
every time with the same `@customerId` and no product parameter — roughly 8% of the warm page's queries.

Ours, not nop's, and the identical shape to the tier-price read that P7 §5 fixed: a per-customer answer being
resolved inside a per-product loop. The fix is the same one — resolve it once in `BulkOrderModelFactory`
beside the other per-page reads and pass it down, or give the service a per-request cache.

**Trigger.** Any phase that touches the factory's product loop; it is the cheapest remaining query win on the
screen. Not done in P7 because P7's scope was the skeleton and this is a different service.

---

## F33 — a language added after install shows plugin strings as raw resource keys

**Raised 2026-09-08, from P7's T22a.** Adding a second language and rendering the grid in it showed the *None*
label as `plugins.cw.core.bulkorder.grid.axisnone`.

Standard nop behaviour, not a bulk-ordering defect: `AddOrUpdateLocaleResourceAsync` writes resources for the
languages that exist **at install time**, so a language created later has none and `GetResourceAsync` returns
the key. It affects every plugin string, not just this one.

**Deployment note rather than code.** A store adding a language after the plugin is installed must re-install
the plugin's resources or import them. Worth stating in the deployment instructions, because the symptom —
keys on screen in one language only — reads like a bug in the screen.

---

## F34 — the product page never says "n in cart" (added 2026-09-09, from P8 step 5)

The list card carries a `.product-in-cart` line that design §11.3 refreshes after every add, so a customer on
`/bulkordering` can see they already have three of something. `applyCartQuantities` writes that count into
`.bulk-order-product` elements carrying a `.product-in-cart` label — **neither of which the product page
has**, so it no-ops. Nothing breaks; the line is simply never there, and silently.

**Why it was not fixed in P8.** The count is `QuantityInCart` on the *list item* model (`Models/BulkOrder/BulkOrderProductModel.cs:31`).
`BulkOrderGridModel` — the only model the product-page component has — does not carry it. Closing this means
adding the field to the grid model and rendering a label in the P8 view, which touches a model the list path
also uses. Small, but not a rider on a script change.

**Trigger to revisit:** the next change that touches `BulkOrderGridModel` anyway, or a client noticing that
the two screens tell them different amounts about the same cart.

---

## Appendix — client measurements, 4.1 backup, run 2026-09-05

From the design review of 2026-09-05, kept because it is what sets priority on the items above. The SQL is
`TestData/BulkOrdering_Review_Measurements.sql` (in `NOP49/CWPlans/`), written to the **4.1 client schema**. One client, one year
old, ~95 stores. **Measurements set priority, never scope** (4.9 lesson #26, `tasks/lessons-4.9.md`): a zero here means
"unreachable on this data, covered synthetically, revisit if a client configures it".

| # | What it counted | Result | Priority it sets |
|---|---|---|---|
| R1 | optional axis-eligible mappings (§2) | 20 products, **194** mappings, 0 restricted; per store max 18, median 0, **3 of ~95 stores** | **Medium.** Three stores, but on those products it is the difference between orderable and not. |
| R2 | rule 3 on unrestricted products (D-R2) | 5 products, 9 mappings | **Low.** Built; one ZZT product as coverage. |
| R3 | required pins nop would not default (D-R1) | 26 RadioList, 40 ColorSquares, 1 ImageSquares — 67 mappings | **Medium.** An upper bound, not a lower one: these types are axis-eligible, so the default applies only after demotion or a §9.1 case 2/4 condition. Between 0 and 67; not settleable in SQL. |
| R4 | tier prices, old price, pre-order, grouped, buy-disabled | TierPriced **22**, OldPrice 0, PreOrder 5, Grouped 1, DisableBuy 7 | F22 **medium** (largest number in the run). F24 low, F23 lowest. D-R5 low, built anyway. |
| R5 | `ManageStock` products with attributes (F28) | *empty* | **None on this data.** |
| R6 | chained conditions (D-R3) | 0 | **Lowest.** One ZZT product with a chain is the only coverage. |
| R7 | stores trusting the login redirect | *empty* — the 4.9 test DB only; the settings do not exist in 4.1 | Moot; the setting defaults to off. |
