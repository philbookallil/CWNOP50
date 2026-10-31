# Bulk Ordering 4.9 — phase plan

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

> **Reads with** `BulkOrdering_49_Design.md` (the agreed shape), `BulkOrdering_NOP46_Reference.md` (the
> functional baseline) and `BulkOrdering_Future_Improvements.md` (what is out of scope).
>
> **How this is used.** Each phase below states its scope, the files it touches, the contract it must satisfy,
> and its acceptance tests. The **exact-change spec** — file, method, before/after — is produced *per phase, at
> the time that phase starts*, not up front. Writing eight specs in advance would go stale the moment phase 1
> is applied, and `CLAUDE.md §0.4` says working documents are consumed rather than kept.
>
> **Standing agreement for this work:** exact changes are handed over; the programmer applies them; validation
> follows on a live nop instance. No source file under `Plugins/`, `Libraries/`, `Presentation/` or `Tests/` is
> edited directly.

---

## Ordering principle

Something visible and testable on the live instance as early as possible. Phases 1–2 put a working product list
on screen; phase 3 puts a working grid on screen; nothing is built for three phases before it can be looked at.

Dependency chain:

```
P0 ──> P1 ──> P2 ──> P3 ──> P5 ──> P6 ──> P9 ──> P6b ──> P7 ──> I14+I15 ──> **P8** ──> P10
                     └──> P4 ──┘
```

**Revised 2026-09-05, folded 2026-09-07.** **P6b** — "cover more than 4.6" — is **DONE**; see its section
below. The design review that produced it has been folded into this plan, `BulkOrdering_49_Design.md` §10.5
and `BulkOrdering_Future_Improvements.md`, and both working files (`BulkOrdering_49_Review.md`,
`BulkOrdering_49_P6b_Spec.md`) are deleted per `CLAUDE.md §0.4`. **P10** — entry points: the
login/impersonation redirect the `BulkOrderSendUserAfterLogin` setting promises, and a storefront link to the
screen — is **DONE (2026-09-09)**; see its section below. P7's scope below is the review's D-R4 version: cache
`LoadAsync`'s output, not the composed grid.

> **Every phase in this plan is now complete.** P0–P10, plus P6b and the I14/I15 pass. What remains is listed
> under "Still open across the build" at the foot of this document: two P7 measurements, two P9 interface
> items, one future improvement, and housekeeping. None of them blocks use of the screen.

**Order revised 2026-09-05.** P9 ran before P7 because the screen worked and was unpleasant to read, which is
the point at which layout stops being scaffolding. What remains of P9 — **I14** (the screen on a phone) and
**I15** (desktop density and alignment) — runs *after* P7 and before P8, at the client's preference: they are
one design pass over two widths, and doing them either side of a caching phase would mean designing the card
twice.

P4 (conditions) is gated on P0's data and can be built in parallel with P5 once P3 lands.

---

## P0 — Data reconnaissance  *(no code)*  ✅ **DONE 2026-08-28**

**Why.** Two design rules are written against data nobody has looked at. Both are cheap to check and expensive
to get wrong.

| Query | Settles |
|---|---|
| The conditional-attribute query in design §9 | Whether §9 rule 3 (force the depended-on attribute to be pinned) costs products their natural axis. **401 mappings carry a condition**, so this is live. |
| `SELECT COUNT(*) FROM Product_ProductAttributeCombination` grouped by `ProductId`, plus a count of products with attributes but **zero** combinations | How common design §7.4 (no-combinations fallback) actually is |
| Distribution of authorised attribute count per product, and value count per attribute | Whether `MaxAxisCount = 5` and `MaxCellsPerProduct = 400` are the right defaults |

**Outcome.** All four queries run. Results and their consequences are recorded in design **§9** (the
revised four-case condition rule set) and **§12.0** (catalogue shape). Rule 3 as originally drafted was
**replaced**, not confirmed — the data showed a colour depending on a size, which would have cost the natural
axis on an unknown number of products at the next client.

**Standing caveat.** The measurements are one client, one year-old backup. A second client with unknown
data is expected, so the numbers validate *mechanisms and defaults* only — no rule may depend on how a
particular client configured their catalogue.

---

## P1 — Scaffold: feature flag, settings, permission, routes, empty page  ✅ **DONE 2026-08-28**

**Scope.** Everything needed for `/bulkordering` to exist, be gated correctly, and be configurable — and
nothing else. The page renders an empty shell.

**Files.**

| File | Change |
|---|---|
| `Domain/Settings/CWFeature.cs` | add `BulkOrder = 5` |
| `Domain/Settings/CWFeatureSet.cs` | add `BulkOrder` to the record, the indexer and `From(...)` |
| `Services/Settings/CWFeatureService.cs` | probe `BulkOrderEnable` alongside the existing four flags |
| `Domain/Settings/MultiStoreSettings.cs` | replace the commented bulk block (`:7`–`:49`) with the live properties from design §12 |
| `Areas/Admin/Models/Settings/MultiStoreSettingsModel.cs` | the matching model properties + `_OverrideForStore` pairs |
| `Areas/Admin/Factories/CWSettingModelFactory.cs` | populate them and their override flags |
| `Areas/Admin/Controllers/MultiStoreController.cs` | save them, in **both** save paths |
| `Areas/Admin/Views/MultiStore/Settings.cshtml` + `_StoreSettings.cshtml` | a Bulk Ordering card |
| `Services/Security/CWPermissionConfigManager.cs` | `PUBLIC_ACCESS_BULKORDERING` constant + `AllConfigs` entry |
| `Controllers/BulkOrderController.cs` | new; `Index` only |
| `RouteProvider.cs` + `CWCoreDefaults.cs` | the four route names and patterns from design §14 |
| `Views/BulkOrder/BulkOrdering.cshtml` | the shell — title, layout, empty container |
| `Content/BulkOrder/bulk-order.css` | placeholder |
| `Infrastructure/NopStartup.cs` | DI registrations (services added in later phases are registered as they arrive) |
| `CWCorePlugin.cs` | locale resources for the settings card |
| `Nop.Plugin.CW.Core.csproj` | `<None Update>` for every new CSS/JS file — **`CLAUDE.md §9.8`: no glob, a missing entry is a silent 404 with no build warning** |

**Contract.**

- `[CheckCWFeature(CWFeature.BulkOrder, StoreSource = CWFeatureStoreSource.CurrentStore)]` and
  `[CheckPermission(...)]` on every action, in that order.
- `StoreSource` is stated explicitly. Per `Feature_Toggles_Reference.md §0.3` omitting it blocks the action and
  logs an error — a deliberate loud failure.
- Settings are per-store overridable, following the existing `SaveSettingOverridablePerStoreAsync` pattern used
  by `PrintNameProductAttributeId` in the same controller.

**Acceptance.**

| # | Check |
|---|---|
| P1.1 | Feature off for the store → `/bulkordering` is blocked by the feature filter, not by an exception |
| P1.2 | Feature on, permission absent → blocked by the permission filter |
| P1.3 | Both present → empty page renders, correct layout, CSS actually loads (check the network tab, not the csproj) |
| P1.4 | Settings save per store and per default store; override checkboxes behave like the neighbouring settings on the same page |
| P1.5 | `CWFeatureSet.From` returns the new flag; the four existing features are unaffected |

**Trap.** `MultiStoreSettings` already has the 4.6 bulk block commented out. It is a **partial port**, not a
specification — design §12 lists four deliberate divergences (notably `AxisAttributeIds` replacing the three
`Horizontal`/`Vertical*` slots, and dropping Refill). Apply the design, do not uncomment the block.

---

## P2 — Product list  ✅ **DONE 2026-08-31**

**Scope.** The paged, filtered, sorted product list — no grids yet. Each product row shows image, name, SKU,
price and "n in cart"; products with attributes show a placeholder where the grid will go.

**Files.** `Services/BulkOrder/IBulkOrderService` + implementation; `Factories/IBulkOrderModelFactory` +
implementation; `Models/BulkOrder/*`; `Controllers/BulkOrderController` (`Index`, `Items`);
`Views/BulkOrder/_Items.cshtml`; category navigation view component; `Content/BulkOrder/bulk-order.js`.

**Contract.**

- The four filter modes from design §16 (All Products / Latest ordered / Best sellers; `ContractItems`
  deferred), ported from `NOP46` reference §4 — same base predicate, same store-mapping and ACL application,
  same 3-month window for `LatestOrdered`.
- Sorting via a CW equivalent of `Jim2ProductSortingEnum` + the localized-name join.
- **`OrderBy` is `int?` and must be normalised before use.** `NOP46` D4: 4.6 casts `(SortEnum)command.OrderBy`
  on a nullable, on the one request path where the normalising code does not run.
- **All three visibility constraints of design §10.4a** — store mapping, ACL, and the packname subsidy
  allocation restriction. The third is the one a 4.6-derived port drops silently, because in 4.6 it lived
  inside the Allowances plugin's `IJim2AclService` replacement rather than in the bulk-order queries.
- Visible-category computation and its cache, with a cache event consumer that 4.6 lacks.
- Paging is AJAX — the pager calls the items endpoint, it does not navigate (`NOP46` reference §12.2).

**Acceptance.** Passed 2026-08-31, all eight groups. The spec is consumed and deleted; what survives is folded
into the design doc — the **request state contract (§4.3)**, the visible-category cache key and its three rules
(§10.5), and four new carried-over open items (§16).

**What P2 changed in the design as it was built**, because P3 inherits all of it:

- The whole page renders **server-side** on first load; JS takes over only on change. One request per page
  view, and the page works without JavaScript.
- The **category is a control, not a page** — its links are AJAX and the category tree is swapped together
  with the product rows, because the tree depends on the filter. Sort, page size and view mode survive a
  category change. A deliberate divergence from nop, where a category *is* a page.
- **One normalisation point.** Every request value is normalised in `PrepareItemsModelAsync` and everything is
  rendered from the normalised value. Design §4.3 rule 2 lists them.
- **Cancelled orders count for nothing** (`CountedOrderStatusIds`), a divergence from both 4.6 and nop.
- Orders stay scoped to the **logged-in customer**, not the packname — 4.6 parity, with the packname-scoped
  variant parked as **F17**.

**Lessons that cost a review round each**, recorded so P3 does not repeat them:

1. **Verify every nop API against the 4.9 source while writing the spec, not while validating it.** Four
   defects reached the editor because the spec was written from memory — `CacheKey`'s constructor arity, an
   `[Obsolete]` URL helper, a missing `_ViewImports` namespace, and a mis-ordered tuple deconstruction. The
   one change written the other way round (the model factory) had four problems caught *before* it was
   applied.
2. **An acceptance check can be wrong.** B15 described behaviour the markup never had and survived four
   review rounds because the check kept being read instead of the code.
3. **A leak fixed in the query is not fixed in the cache.** The order-ownership predicate and the cache key
   that omitted the customer id were the same bug at two layers, found a week apart.

---

## P3 — Grid builder and grid UI  *(read-only)*

**Scope.** The core of the design: classification, signatures, grid construction, and the rendered grid — with
**no prices, no stock, no add-to-cart**. Cells render as quantity boxes or dashes.

**Files.** `Services/BulkOrder/IBulkOrderGridBuilder` + implementation; `Domain/BulkOrder/*` grid records;
`Models/BulkOrder/BulkOrderGridModel|RowModel|CellModel|PinModel`; `Views/BulkOrder/_Grid.cshtml`,
`_Cell.cshtml`, `_PinnedStrip.cshtml`; the pin-change endpoint.

**Contract.** Design §5, §6, §7, §8. In particular:

- eligibility rules §5.1 in full, including checkboxes being pin-only;
- ordering §5.2 — configured attributes first **in configured order**, then the rest by `DisplayOrder`, `Id`;
  an attribute absent from the setting is still eligible for a free slot;
- the `CombinationKey` value type, with value **quantity ignored**, to match
  `FindProductAttributeCombinationAsync`'s `ignoreQuantity: true`;
- cell budget §5.4 with last-axis demotion and a visible notice;
- pin defaults §8.1 — required pins default to `IsPreSelected`, optional pins default to nothing.

**Acceptance.** Design test plan T1–T10. **T10 is the one that matters**: for sampled cells, the fast lookup
must return the same combination `FindProductAttributeCombinationAsync` returns for the equivalent XML. That is
the assertion that the optimisation did not change meaning.

**Note.** The grid builder returns domain records, not view models — `CLAUDE.md §5.1` forbids `Services/`
referencing `Models/`.

---

## P3 — DONE 2026-09-02

Acceptance run passed in full (46 checks). The spec, its four addenda and the check-2.1 harness are consumed
and deleted per `CLAUDE.md §0.4`; everything durable is folded into the design. The published run sheet is
the record of what was tested.

**What the phase changed after the spec was written** — all of it design-level, none of it patching:

| Finding | Where it now lives |
|---|---|
| A missing combination closes a cell only when nop would refuse it; "has combinations" is the wrong question | §7.4, rewritten; D5 superseded |
| §5.4's demoted-pin carve-out is conditional on §7.4 — unconditioned it fired only where it was wrong | §8.2 |
| A cell has two keys: `Selection` submits, `AxisSelection` identifies. Quantity carry-over uses the second | §8.4 |
| Per-product state is page-scoped, and that follows from §4.3 rule 1 rather than contradicting it | §8.4 |
| `ReadonlyCheckboxes` must submit exactly the `IsPreSelected` set — `ArraysEqual` compares order too | §12.4 |
| `DisableBuyButton` and `ProductType != Simple` were missing from the disabled-cell rule | §12.2 |
| An attribute-value picture must also be a picture **of the product** — nop inner joins `Product_Picture_Mapping` | before §16 |
| The grid skeleton cache, when built, must invalidate on `Product` — two settings columns are now inputs | §10.5 |
| Full nop parity audit: which rule gates a cell, and which phase owns it | §12.4 |
| The catalogue re-measured: zero restricted products, 401 conditional mappings, the cell-count distribution | §12.0 |

**Process lessons worth carrying, because each cost a round trip.**

1. **Write acceptance checks against the data, not from memory.** Six checks named products or attributes
   that did not have the shape described — Fit as a pin when it was an axis, Logo as a pin when it was
   axis 4, a rule-3 attribute that existed nowhere in the set. Each looked like a code failure first.
2. **A check that cannot be run is not a check.** 2.1 compared "the combination the grid used" against nop,
   and the grid never sends a combination id to the browser. It needed a harness, which should have been
   written with the check.
3. **Test data must be re-derived when the code changes.** Four of the five changes made during the phase
   had no coverage, in three cases because the exact shape that triggers the bug was the shape the data
   happened not to contain — every demoted axis required, every readonly value preselected.
4. **A silent skip in a seed script wastes an afternoon.** Part 3's picture section was wrapped in
   `IF @pic IS NOT NULL` with no `ELSE`; it did nothing and said nothing. Seed scripts `THROW` now.

**Carried forward into P4 and beyond.**

- **P9's structural half is done and its pager half is next** — see below.
- **The optional-axis gap (§16 item 9)**: an optional combinable attribute on an axis makes the
  short-signature combinations unreachable on a *restricted* product. Zero live products, so logged.
- **`ZZT-01` … `ZZT-22` and the four seed scripts stay.** They are the regression set for every later phase,
  not P3 scratch data.

---

## P4 — Conditional attributes ✅ DONE 2026-09-02

**Scope.** Design §9, §9.1, §9.2 and §9.3 — evaluate conditions once per product per pin-state; hide pinned
selectors whose condition is unmet (case 1); drop axis attributes whose condition is unmet (case 2); show a pin
conditional on an *axis* attribute when any rendered axis value satisfies it, and write its value only onto the
cells that do (case 3); demote **B**, the dependent attribute, when both are axes (case 4).

**Never the depended-on attribute.** An earlier draft said "force the depended-on attribute to be pin-only" and
it survived in this scope line until 2026-09-02. Design §9 supersedes it explicitly: it would take Colour off
the axes on any product where a logo depends on Colour — the natural axis and, on the measured data, twelve
attributes' worth of dependency.

**Built.** `AttributeCondition` (parsed once per product in `LoadAsync`, never `IsConditionMetAsync` per cell);
`BuildAsync` restructured around three blocked sets; per-cell filtering in `Compose`; the "Applies to X only"
note on the pin. Test data `TestData/BulkOrdering_P4_TestData.sql` + `_Images.sql` (ZZT-23 … ZZT-29, the first
conditional attributes in the ZZT set). All checks passed.

### What P4 learned, and where it now lives

Everything durable was moved into the design before the spec was deleted. Recorded here only as an index:

| Finding | Now in |
|---|---|
| Five behaviours of `IsConditionMetAsync` — three-state return, first-that-*loads*, deleted-attribute means **met**, empty `<Value>` is a real condition, cart validation drops unmet before the required check | design **§9.2** |
| The condition editor only offers this product's own mappings and imposes no display-order rule; `DeleteProductAttributeMappingAsync` never scrubs sibling conditions | design **§9.2** |
| `GenerateAllCombinationsAsync` strips unmet-condition attributes, so a conditional product's combinations have **mixed signatures** | design **§9.2** |
| The "applies to" note names **exactly one** value, never a list — count-first comparison plus one value per axis cell | design **§9.1** |
| `ParseCondition`'s one deliberate divergence from nop, and why it is unreachable | design **§9.2** |
| The §7.3 column scan's target signature varies by axis value — and the general lesson about non-uniform rules | design **§9.3** |

### Two gaps left open, deliberately

Both are recorded in `BulkOrdering_Future_Improvements.md` and neither blocks P5:

- **F19** — a pin hidden by case 1 or 2 loses its selection, because the DOM owns the pin state and a hidden
  pin has no node to read. Cosmetic until P6 makes conditional attributes submittable.
- **F20** — case 3 tests *all* of the depended-on attribute's values rather than the *rendered* ones, which
  §9.1 says. Reachable only on a restricted product whose satisfying axis value appears in no combination;
  the measured catalogue has zero restricted products.

And one UI gap, **I8** in `BulkOrdering_49_P9_Interface.md`: case 3 explains itself ("Applies to Navy only")
while cases 1 and 2 vanish silently — and case 1 is the dominant shape in the measured data (202 of 401
conditional mappings). Its fix and F19's are the same change, so decide them together.

---

## P5 — Pricing and availability ✅ DONE 2026-09-03

**Scope.** Design §10.3, §10.4, §12.2 and the new §12.5 — per-cell price, per-cell availability, the
disabled-cell rule, the price-display collapse, and the products the screen cannot order at all.

**Built.** `BulkOrderCellState`, `BulkOrderAvailability` + the `IBulkOrderAvailabilityProvider` seam,
`IBulkOrderPricingService` + `BulkOrderPricingContext` (everything async resolved once in `PrepareAsync`, then
a synchronous `StateFor` per cell), `BulkOrderUnsupportedReason`, and the model/factory/view/CSS work.
Test data `TestData/BulkOrdering_P5_TestData.sql` + `_Images.sql` (ZZT-30 … ZZT-38). All 33 checks passed.

### What P5 learned, and where it now lives

Everything durable was moved into the design before the specs were deleted. Recorded here only as an index:

| Finding | Now in |
|---|---|
| The overridden-price path and the adjustment path are **mutually exclusive** | design §10.3 |
| `GetUnitPriceAsync` uses **two different quantities five lines apart** — the value adjustment gets the line quantity, only `GetFinalPriceAsync` gets the tier-grouped one | design §10.3 |
| The tier quantity counts the **whole cart** for that product, not the packname slice — so the "in cart" badge's number must not be reused for it | design §10.3 |
| `FormatStockMessageAsync` resolves the combination by parsing every combination's XML, so its wording is mirrored rather than called | design §10.4 |
| The five warning families of `GetShoppingCartItemWarningsAsync`, and which phase owns each | design **§12.5** |
| Four product kinds the screen cannot order — gift card, rental, customer-enters-price, recurring — with the exact data nop demands for each | design **§12.5** |
| Downloadable and require-other-products need **nothing** at add-to-cart | design **§12.5** |
| Required-products measured three times and inert all three ways, with the latent risk that survives | design **§12.5**, and P6 below |

### Four defects found in acceptance, all mine, all worth remembering

1. **A product with no axes bypassed §12.2 entirely.** `Compose` produced a row with no cells when there were
   no axes, so nothing existed for the rule to attach to and the view improvised a bare quantity box. Design
   §4.2 had claimed this was "the same code path as a 5-axis garment" since the beginning; it was not, until
   the builder was made to emit the degenerate cell.
2. **`PriceRange` was computed and never rendered.** The model field and the factory logic landed; the
   product-line view change did not.
3. **`CallForPrice` was computed and thrown away** — no model property, so every such cell showed the
   out-of-stock marker instead. Two opposite meanings, one glyph.
4. **The pricing service still ran for products with no grid**, discarding everything it computed.

Three of the four share one shape: a value computed in the service layer and dropped before the view. That is
what the P5 checks now test for by naming the *rendered* result rather than the model property.

### The × and the dash — the change that mattered most

`–` meant "this combination is not sold" and `×` meant "it exists but we have run out", and a customer could
not tell them apart; they were also the same words to a screen reader. **A blocked cell now shows nop's own
reason in words** — *"Out of stock"*, *"Available in 2-4 days"*, *"Backordering"*, resolved from
`ProductAvailabilityRange` and `BackorderMode` exactly as the product page resolves it — and the dash is the
only symbol left in the grid, which is what lets it mean one thing. Found from a live product, not from the
plan.

### Two items left open, deliberately

- **F19/F20** in `BulkOrdering_Future_Improvements.md`, carried over from P4.
- **I9** in `BulkOrdering_49_P9_Interface.md`: a quantity box accepts more than its `max`, because HTML's
  `max` constrains the spinner and not typing. The clamp is interface work; the refusal is P6's, and the two
  must use the same number once P6 decides how a bulk line merges with an existing cart line.

---

## P6 — Add to cart ✅ DONE 2026-09-04

**Acceptance run passed in full.** Everything below this heading is the reasoning that produced the phase and
is kept for the record; what P6 actually built and the five things it got wrong first are indexed here.

### Where P6's findings now live

| Finding | Recorded in |
|---|---|
| Call the BASE-shaped `AddToCartAsync`, never the `packNameId` overload — the latter returns an empty warning list (= success) and adds nothing | design **§11.2**, and `Subsidy_PackName_Known_Issues_And_Improvements.md` **item 10** |
| nop has no client-side "cart changed" event; the two rendered cart blocks are the mechanism, as in nop AND in 4.6 | design **§11.3** |
| nop pins Newtonsoft to `DefaultContractResolver` — every JSON response is **PascalCase** | design **§11.3** reasoning; the live consequence is in `bulk-order.js` |
| `AddedQuantity` counts units, not lines | design **§11.2** |
| One activity-log entry per product, not per line | design **§11.2** |
| `DisplayCartAfterAddingProduct` deliberately ignored — a divergence from nop and 4.6, not an oversight | design **§11.2** |
| The three quantity rules (`OrderMinimumQuantity`, `OrderMaximumQuantity`, `AllowedQuantities`) with their measurements | design **§12.4** |
| The `[CheckCWFeature]` gate was missing from `BulkOrderController` since P2, and the AJAX calls did not send `X-Requested-With` | fixed; design **§14** already specified both |
| The cell-level "in cart" was never built in any phase | design **§11.3**, and **F21** |
| `Url.RouteUrl("Product", …)` returns null in 4.9 and Razor silently drops the `href` | design **§16.6** correction |
| Test products `ZZT-39 … ZZT-47` — the five quantity shapes and the four §12.5 kinds | `TestData/BulkOrdering_P6_TestData.sql` |
| The pager window hides pages; a failed cell's warning widens its column | `BulkOrdering_49_P9_Interface.md` **I10**, **I11** |

### Corrected 2026-09-05 — P6 passed with a hole in it

The acceptance run was clean and the phase was marked done, and both were true of the data it ran against.
Design **§12.5**'s required-text refusal was blocking the **whole product** for every §9.1 case-3 attribute —
101 published products, the largest conditional group on the catalogue — including the cells nop sells. It was
not caught because **no ZZT product had the shape**: `ZZT-25` is the case-3 product, but its dependent pin is
optional and *has* values, so the refusal never fired on it.

Fixed 2026-09-05 (design §12.5, "blocks CELLS, not always the product"), with `ZZT-48` … `ZZT-51` added as the
missing coverage and its own acceptance run passed. The phase-plan lesson is the one P3 already recorded and
this repeated: **test data must be re-derived when the code changes**, and a passing acceptance run proves the
shapes in the test data, never the shapes in the catalogue.

**Two lessons from this phase are in `tasks/lessons.md`** and are worth more than any single fix: a client and
a server agreeing in C# proves nothing about what crosses the wire, and an analysis document about the old
system is an index, not a substitute for its source.

---


**Scope.** `BulkOrderAddRequest` / `BulkOrderLine` / `BulkOrderSelection` in `BusinessLogic/Requests/`;
`IBulkOrderLogic` + implementation; the `AddToCart` endpoint; the client-side result handling from design §11.3.

**Added 2026-09-03 — the fourth warning family lands here.** Design §12.5 walked all five families of
`GetShoppingCartItemWarningsAsync`. Four kinds of product are refused before the grid is built (§12.5), but
**required products** is not one of them: nothing is missing from the screen, the customer simply has a
dependency.

**Measured, and the honest answer is that no client data exercises it.** Three products carry
`RequireOtherProducts`; all three are inert. Two have a NULL `RequiredProductIds`, which
`ParseRequiredProductIds` turns into a no-op. The third names product 5013, which is **soft-deleted**, and
`GetProductsByIdsAsync` passes `includeDeleted: false` — so it is dropped and the method returns before either
branch runs. Nothing auto-adds and nothing is refused. An earlier version of this note claimed `GR3942A`
silently adds 5013 to the cart; that was wrong, and the P6 obligation built on it does not exist.

So P6's baseline is passing `addRequiredProducts: true`, and the rest is **defensive**:

- **Both branches still need writing**, because each of the three is one plausible admin action from going
  live, down different paths: filling in the two NULL `RequiredProductIds` activates the **refusing** branch
  (`ShoppingCartService.cs:304`, auto-add is off on both), and restoring product 5013 activates the
  **silent-add** branch. Neither will be caught by testing against this catalogue, which is exactly why they
  should be written before they are needed.
- **If the silent-add branch ever fires, say what else was added.** A page that adds twenty lines at once
  would put products in the cart that nobody typed a quantity for, and the "N in cart" badges would not show
  them until a reload. The per-line result set in §11.3 is the right place.

And one **open question for the team**, recorded in §12.5 and now theoretical rather than live: nop auto-adds a
required product without checking whether the customer may see it, so on a subsidy store an unallocated
product could enter the cart.

**Contract.**

- selections travel as **data attributes → JSON**. No form-field-name encoding and no server-side string
  splitting anywhere (`NOP46` D3 and `NOP41` improvements I1);
- per D6 lines that pass are committed even when others fail;
- results are attributed **per cell**;
- the page is not reloaded and does not navigate.

**Acceptance.** T15–T20. T18 and T19 specifically: T18 verifies the `PackNameCartItem` row exists **by querying
the table** (`tasks/lessons.md` #10 — verifying a guard exists is not verifying it fires); T19 is the
prefix-id case that `NOP46` D3 broke.


**Carried in from the P3 parity audit (measured, not predicted).**

- **Re-measured 2026-09-03: 101 live products, and every one of them CONDITIONAL.** The 205 below was
  measured during P3 and did not filter `Deleted = 0 AND Published = 1`; 101 is the live count, and it is the
  one that matters. The two have not been reconciled further and the difference is recorded rather than
  guessed at. What changed the decision is not the count but the split: **zero are unconditional**, so by
  design §9.2 behaviour 5 the cart drops the attribute before checking `IsRequired` whenever its condition is
  unmet — which is the usual state. These products order normally until the customer selects the option that
  makes the field required. P6 therefore blocks them **per pin state** (a fifth `BulkOrderUnsupportedReason`,
  evaluated after §9.1's condition loop has settled) rather than removing 4% of the catalogue from the screen.
- **205 products have a REQUIRED free-text attribute** (`TextBox` / `MultilineTextbox` / `Datepicker` /
  `FileUpload` with `IsRequired = 1`) — 4.4% of the catalogue. Design §5.1 renders those pins as *"Not
  available on this screen"*, so the grid supplies no value, and
  `GetShoppingCartItemAttributeWarningsAsync` (`:869`) refuses **every line** of those products. Today that
  is invisible because nothing adds to cart yet; at P6 it becomes 205 products whose grid looks perfectly
  orderable and is not. This phase must decide between: excluding those products from the bulk list;
  rendering a real input in the pinned strip whose value is written onto every submitted line (which is
  improvement **F5**, per-line print name, arriving early); or letting the per-line result set report the
  failures. Do not discover this during P6 acceptance. Split the 205 by whether the requirement is
  conditional first — a conditionally-required attribute is only required when P4 says its condition is met.
- **P6 must not ship before P4.** nop strips unmet-condition attributes only inside
  `ProductAttributeParser.ParseProductAttributes(IFormCollection …)` (`:309–317`); P6 composes `AttributesXml`
  itself, and nothing downstream removes them — `GetShoppingCartItemAttributeWarningsAsync` filters its
  *mapping* list by condition but never the *submitted* attributes. So a P6 built before P4 writes order
  lines carrying attribute values, and their price adjustments, that nop's own product page would have
  removed. 401 conditional mappings across 221 products. The phase DAG already sequences P4 before P6; this
  is the note explaining why that edge is not negotiable.
---

## P6b — cover more than 4.6 ✅ **DONE 2026-09-07**

**What it delivered.** The optional axis's *(none)* value (§2) — an optional attribute can now be laid out on
an axis with a *None* row, and a line under it submits the other attributes only, which 4.6 had and 4.9 did
not. **D-R1** required pins no longer invent a value nop's own control would not default (a RadioList,
ColorSquares, ImageSquares or Checkboxes pin holds nothing and its cells read "Choose above"); **D-R2** rule 3
now fires only on a restricted product, so an attribute the combinations never mention is laid out on an axis
instead of pinned; **D-R3** an unsatisfiable condition hides its dependants instead of asking for a choice
that cannot be made; **D-R5** grouped and buy-disabled products get the §12.5 card and a link, not a greyed
box. **B1** the add path no longer trusts the request body — value ownership is checked and nop's own
condition-strip loop runs server-side; **B2** call-for-price parity for an impersonating admin; **B3** typed
quantities survive a sort, a filter or a page change, and what had nowhere to go is said out loud.

**B4, raised during the spec:** an over-stock quantity is no longer rewritten to the available amount. The box
keeps what the customer typed, takes a red border, and the card names the maximum — the clamp was a courtesy
that silently changed an order and then reported success.

**Three defects surfaced in the acceptance run**, none predicted by the spec. **B5** — `clearLineResults`
strips the over-max marks on the way out on the promise that the server's answer replaces them, and two exits
never get one (the feature gate's `{success:false}` and the network catch), so a refused quantity ended up
quietly accepted; re-derived in `submitLines`'s `.finally`. **B6** — `#bulk-order-content` *is* the flex
container, so the typed-lost note inserted as a direct child became a flex item and squeezed the product
column from 956px to 290px; the stylesheet already solved this for `.load-failed` and the rule is now keyed on
the shared `.no-result` class. **B7** — a server refusal outlived the value it described: correcting the cell
cleared neither the border nor the sentence until the next submit, so `clearRefusal` now withdraws that one
cell's verdict on edit, matched by axis key so another cell's refusal survives.

**Test data.** `ZZT-52` … `ZZT-57` plus `ZZT-56B` (`TestData/BulkOrdering_P6b_TestData.sql`), and `ZZT-58`
(`..._ZZT58.sql`) added after the run, when B3 proved untestable: the check had been written against `ZZT-05`,
whose Fit is a *Checkboxes* attribute and therefore pin-only by rule 2 whatever the restriction says. Cards
for all eight are in `..._Images.sql`. Two lessons came out of ZZT-58 alone (`tasks/lessons.md` #35, #36):
`AllowAddingOnlyExistingAttributeCombinations` does nothing unless `ManageStockByAttributes` is also set, and
a negative test must leave a survivor — an all-dash row is dropped as "a row of nothing", so the gap has to be
partial for a dash to render at all.

**Two expectations in the spec were wrong and the screen was right.** A merged-quantity refusal names the
combination's stock, not the remaining allowance — `GetQuantityProductWarningsAsync` formats
`ShoppingCart.QuantityExceedsStock` with `maximumQuantityCanBeAdded` and nothing subtracts the cart, so naming
the remainder would have been a deviation from nop, not a fix. And `IsNonCombinable()` is **false** for
`ReadonlyCheckboxes` (it covers only TextBox, MultilineTextbox, Datepicker and FileUpload), so readonly values
are combinable and reach the cart correctly; the earlier note to the contrary is struck.

---

## P7 — Caching and performance validation ✅ **DONE 2026-09-08**

**What it delivered.** The **grid-skeleton cache** of design §10.5: everything `BulkOrderGridBuilder.LoadAsync`
derives — localized names and prompts, values ordered with their picture ids, every combination's XML parsed
once into key parts, the parsed conditions — cached as a serializable DTO keyed on product, language and
`BulkOrderShowAttributeImages`. `LoadAsync` became a seam over `BuildSkeletonAsync` and `HydrateAsync`; the
mapping and combination **entities** are re-read per request from nop's own `IStaticCacheManager`-backed
accessors, so no entity, `ImmutableArray` or struct-keyed dictionary ever reaches the serializer. Seven cache
event consumers. The tier-price cart read became one read per page instead of one per card. **F18 closes.**

**Measured on the 4.9 test instance** (Extended Events, `TestData/BulkOrdering_P7_QueryTrace.sql`):

| | cold | warm |
|---|---|---|
| total queries, 15-product page | **418** | **108** |
| `ProductAttributeCombination` | 30 | **0** |
| `Product_ProductAttribute_Mapping` | 30 | **0** |
| `ProductAttributeValue` | 8 | **0** |

and the warm count is **flat against grid size**: 15 products at **13 cells** cost 106 queries, the same 15
products at **365 cells** cost 108. The per-product constant is **~32 fixed per page plus ~5 per product** —
`Picture` ≈2.1, `Discount` ≈2.1, `PackName` ≈0.8, all per *product*, none per cell.

**B8, found during the phase.** `NoneName` was a **`LocaleStringResource`**, not a `LocalizedProperty`, so none
of the six consumers would ever have cleared it and an edited resource would have shown the old label until
expiry. Removed from the skeleton and resolved per request in `HydrateAsync` — `GetResourceAsync` is itself
cached, so it costs a dictionary hit and deletes a whole class of invalidation. A seventh consumer would have
been a second thing to keep right.

**`CombinationKey.FromNormalized`.** `HydrateAsync` ran `Create`'s GroupBy/Distinct/Order pipeline per
combination per request, to merge and sort parts the skeleton already stores merged and sorted. Provably
redundant, so it was removed rather than measured — T21 counts queries and could never have seen it. The
precondition is carried by the name, the docs and a `Debug.Assert` written as one allocation-free linear pass,
because `Debug.Assert` arguments are still evaluated in the DEBUG builds the site is developed in.

**Acceptance: 16 of 18 verified**, most of them driven against the running instance. All seven consumers
proven, including the two that needed care: T23c must run on an **unrestricted** product, because on a
restricted one `ValuesOf` filters a new value out anyway and the check cannot tell that from a stale cache;
and T23e must use a value with **no colour square**, because `_Grid.cshtml` tests the square branch first.

**Two checks in the original acceptance run tested nothing and were rewritten.** T23a as "edit a combination's
Sku" is not a test of the combination consumer at all — `Sku` lives on the entity, which `HydrateAsync`
re-reads every request, so the tooltip changes whether or not our consumer fires; only adding or deleting a
combination exercises it. T21a as "compare two `BulkOrderMaxCellsPerProduct` values" is untestable on this
catalogue, where the largest product is 100 cells and nothing approaches the cap; it became the thin-page /
fat-page comparison above.

### T24 and T23h — closed 2026-09-09

Both were carried forward from P7 because neither could be exercised at the time. Both now have been, using
`CWPlans/TestData/BulkOrdering_P7_T24_T23h.sql` alongside the existing query trace.

**T24 — the cart is read once per page, not once per card.** With
`GroupTierPricesForDistinctShoppingCartItems` ON and a tier-priced product in the cart, the
`[ShoppingCartItem]` count was measured warm at `PageSize=15` and at `PageSize=100`: **the same number for
both**. That is the whole claim — a per-card read would have shown roughly 15 against roughly 100, and one
measurement could never have demonstrated it, which is why the procedure calls for two. Turning the setting
off lowered the count again, confirming the extra read belongs to this feature and not to something else:
`GetTierQuantitiesAsync` returns an empty dictionary without touching the cart when the setting is off
(`BulkOrderPricingService :327`).

*The figures themselves were not recorded* — the pass condition was the relationship between them rather
than their value, and the relationship is what was observed. Anyone re-running this should record all four.

**T23h — the restriction flag is honoured with no invalidation at all, and this test had been described
backwards.** `HydrateAsync :268` reads `RestrictsToExistingCombinations` from the **Product argument**, never
from the cached skeleton, with a comment saying why: a store owner flipping `ManageInventoryMethod` or
`AllowAddingOnlyExistingAttributeCombinations` must be honoured on the next request, and that is not worth an
invalidation. The carried-forward note framed the direct SQL `UPDATE` as a way to see past the cache; it is
the opposite — it removes the admin save's cache-clearing side effect so the **per-request read** can be seen
on its own. Verified on ZZT-58: flipping the flag by direct `UPDATE`, with no admin save and no cache clear,
changed the dashes on the very next page load, and flipping it back brought them return.

Together these close P7's last two open claims. Nothing in the caching design now rests on reasoning alone.

**Correction to this plan's own record:** the note above said T24 also needed "the corrected
`'%[[]ShoppingCartItem]%'` bucket". That correction had already landed when the trace script was rewritten and
is at its line 109; T24 needed setup, not a script fix.

---

### The original carried-forward note, kept for the reasoning

- **T24** — never exercised. `GroupTierPricesForDistinctShoppingCartItems` is **off** on the test store, so
  `GetTierQuantitiesAsync` returns empty without reading the cart and §5's path never runs. Turn the setting
  on, put a tier-priced product in the cart, and re-measure: the `[ShoppingCartItem]` count must be a small
  constant, not one per card.
- **T23h** — needs a direct SQL `UPDATE` of `AllowAddingOnlyExistingAttributeCombinations`. Saving the product
  in admin fires `BulkOrderProductCacheEventConsumer`, which clears the skeleton anyway, so the UI cannot tell
  "read per request" apart from "the consumer cleared it".

---

## P8 — Product details page variant ✅ **DONE 2026-09-09**

**Scope.** The `BulkOrderEnableOnProductPage` path — the grid embedded in the product page, replacing
`_ProductAttributes` and `_BasicAddToCart`, with the per-product gate ported from
`ShowBulkOrderingInProductPageAsync` (`NOP46` reference §14): tier-price check, control-type check, axis check.

**Also in scope: the per-product override** (design §12.3, DQ1 answered yes). One tri-state
`GenericAttribute` on `Product`, key `CW.BulkOrder.ShowOnProductPage`, plus a view component in
`AdminWidgetZones.ProductDetailsBlock` for the admin UI. Two constraints from §12.3 that the spec must honour:
the flag is read **only** on the product page and never in a list loop (per-request cache, one query per
product); and the store fallback (store-specific → store 0 → store setting) is ours to write, because nop's
`GetAttributeAsync` does not fall back.

**The template question, answered 2026-09-09: ALL of them, including themes not written yet.** 4.6 enforced
this gate in one product template (`ProductTemplate.SimpleVoyage.cshtml`) because every client ran Voyage. The
client's answer was that this must work in any theme they adopt. That is not a bigger version of the same
job — it rules the 4.6 approach out entirely, and the repository proves it rather than the argument doing so:
`Nop.Web`'s Simple template renders `_ProductAttributes` at :104 and `_ProductPrice` at :123, while
`CW.StandardTest`'s renders price at :109 and attributes at :138, with `ProductDetailsOverviewTop` moving from
:87 to :117. **A widget zone's position relative to the partials is a property of the theme.** Any rule of the
form "inject at zone X and suppress what follows" is right in one theme and wrong in the next.

**What was built instead: suppression through the MODEL.** Every partial on the product page is model-gated,
and the model is the one thing a theme cannot change. An `IConsumer<ModelPreparedEvent<BaseNopModel>>` empties
`ProductAttributes`, sets `DisableBuyButton`/`DisableWishlistButton`, clears `StockAvailability` and
`TierPrices`, and the stock partials then render nothing wherever that theme puts them. Published from
`BaseController`'s `[PublishModelEvents]` after the action and before any view, so it lands in time. Each
field was read in the shipped partial and its guard quoted beside the line that empties it — if a future nop
changes a guard this goes quietly wrong, which is why the acceptance run checks for ABSENCE, not just for the
grid's presence.

**The grid renders from a widget zone, on three of them, drawing once.** `ProductDetailsOverviewBottom`
(right place), `ProductDetailsBottom` (poor place), `ContentAfter` (bad place) — first one the theme renders
wins, the rest are no-ops behind a per-request flag. The third is the one that matters: zones 1 and 2 are both
declared in the product template, which is the file a theme is most likely to rewrite, so one edit can lose
both; `ContentAfter` is declared in `Views/Shared/_Root.cshtml`, so losing it too takes a second, separate
decision to gut the layout root. Without a net, an emptied model plus a missing zone is a product page with no
way to buy at all.

**Six of 4.6's gate conditions, three of them dropped** — each because 4.6 was working around something 4.9
no longer has. The tier-price exclusion goes (P5 prices tier products, P7 threaded the quantities through).
The three-attribute cap goes; the `NOP46` reference records what it cost — a product with four authorised
attributes was *silently dropped from the list*, counted in the paged total and rendering no row — which is
exactly what the cell budget and last-axis demotion replaced. `IsBulkOrderingCompatible` is replaced by
`UnsupportedReason`, which the grid builder already computes: **the gate builds the grid and asks it** rather
than keeping a second, cruder copy of that judgement. Two conditions were added: grouped products are refused
(one grid cannot stand in for several associated products' controls, and `UnsupportedReasonFor:551` already
returns `Grouped`), and nop's own cart-item EDIT flow is refused — `AllowCartItemEditing` re-renders this page
as an update form (`ProductController :164`), and our grid adds without being able to update a line.

**And the rule that differs from the list, deliberately.** A product the grid cannot express shows §12.5's
unsupported card on the LIST, because the alternative is the product vanishing from a page that counted it.
On the product page it shows nothing at all and nop's own controls stay: there is something to fall back to
here, and replacing a working form with an explanation of why the customer cannot buy would be a downgrade we
delivered. `ZZT-49` (required text on every cell) and `ZZT-48` (required text on one axis value) are the pair
that proves the rule is "can this be expressed", not "does it mention text" — one refuses, the other renders
with a notice.

**Acceptance, 2026-09-09: 29 checks, all passed**, across `DefaultClean` (Nop.Web ordering), `CW.StandardTest`
(its own, inverted ordering) and a throwaway `CW.Zoneless` theme built with both product-page zones deleted,
which is the only way the third zone can ever be exercised. Measured rather than eyeballed where it mattered:
assets are BUNDLED, so the qualifying page's bundles contain `pinned-col` and `initCard` while a non-qualifying
page's different bundles contain neither.

**Found on the way, and not ours.** `CW.Standard` cannot render at all unless `Nop.Plugin.CW.ThemeCore` is
installed — its `_Header.cshtml:48` invokes that plugin's `CwSecondLogo` view component, and an uninstalled
plugin registers none, so every storefront page throws. Nothing in `theme.json` declares the dependency.
Recorded in `CW_Theme_Architecture_Foundation.md`.

**Still open from this phase:** **F34**, the product page has no "n in cart" line — `applyCartQuantities`
writes it into `.bulk-order-product` elements the product page does not have, so it no-ops silently, and
closing it needs `QuantityInCart` on `BulkOrderGridModel`.

---

## P9 — Interface ✅ **DONE 2026-09-09**

**What it delivered.** I1–I5 the layout (2026-09-01), the pager's windowed numbers (09-02), then in one run on
09-05: **I6** the single box and its button as one cluster; **I9** the quantity clamp; **I10** the pager's
ellipsis and first/last; **I11** a failed cell's reason moved out of the cell into a per-card list, plus the
quantity box's border and shape; **I12** the category tree collapsed to nop's own open-branch rule; **I7** the
pinned strip as bounded controls; **I13** a page-wide add taking the customer to its first failure; **I8**
cases 1 and 2 rendering a disabled control instead of vanishing, which closed **F19** with it.

**Two items were found to be describing problems that had already been solved**, both because a backlog entry
was read as a standing fact rather than a report: I12's "pushes the first product below the fold" was a
symptom of the wrap defect P3 fixed, and I11's claim about the `AddToCart.SomeFailed` resource was never true
of the code. `tasks/lessons.md` #27 and #29.

**Three defects surfaced that no item had predicted**, all at narrow widths: `bulk-order.css` had no `@media`
block at all, the theme hides the category block entirely below 1000px, and between 900 and 1000px the tree
was hidden while the layout was still two columns. Fixed; the general rule — borrowing nop's class names means
inheriting nop's responsive assumptions — is design **§4.2**.

**I14 and I15 closed it, 2026-09-08 and 09-09.** I15 was the desktop pass — the quantity box 56×36 → 44×30,
the row 83 → 74px, the card's bottom margin 50 → 0, the pin tray's ragged heights levelled, and attribute
images boxed to a uniform 100×100 so unequal artwork stops giving a row two baselines. I14 was the phone, and
its root cause was one declaration rather than a missing design: `.bulk-order-layout` wraps and turns to a
column below 1000px, and *a wrapping column flex container sizes its items to content instead of stretching
them*, so `.bulk-order-main` grew to 1439px, `.product-body` never had to shrink, and `.grid-scroll`'s
`overflow-x: auto` had nothing to clip — the scroller meant to absorb a wide grid had never once engaged. The
document measured 1458px wide on a 375px screen with the Add button a thousand pixels off the edge; it now
measures 375 with nothing of ours overflowing.

Three things came out of it that the item had not predicted: the sticky row-label column was broken for any
product with more than one row axis, **on the desktop as well**, and its fix has to measure rather than
declare (`pinRowLabels`); the pinned strip's problem at 375px was ragged widths, not wrapping, because it had
already collapsed to one pin per row; and hiding the product image was the wrong fix — moved beside the name
with the grid on its own row it costs 6–13% of page height and the matrix keeps its full width.

**Verified on a physical Android phone, 2026-09-09**: twenty-one checks, all passed, including the two written
as judgement calls rather than pass/fail. No scroll affordance beyond the shaded edge of the pinned block is
needed — tested, not assumed. How to reach a localhost instance from a phone is recorded in the P9 document,
because Chrome's own port-forwarding UI does not work on this hardware and finding that out cost an hour.

**Still open from this phase, both deliberately**: **I16**, a cell that says "500 in stock" and refuses 3 —
not a parity break, so it waits for a decision about cell density. And **I17**, the theme's 456px notification
bar, which gives every page of the store a horizontal scroll on a phone and belongs to whoever owns
`CW.Standard`, not to us.

---

### The original scope note

**Scope: proposed, not yet agreed.** Added 2026-09-01 after the first look at the working screen.

No phase in this plan owns how the page reads. P4 is conditions, P5 pricing, P6 add to cart, P7 caching,
P8 the product-page variant — so the deliberately minimal CSS that shipped with P3, written to prove the
markup rather than to be used, is what a customer gets unless a phase exists for it. That was the right
call while the grid was being built and it stops being right now the grid works.

Collected in `BulkOrdering_49_P9_Interface.md`, which is the intake document rather than a spec. Seeded
with the pager (nop's catalogue pager renders windowed page numbers; ours renders Previous and Next only,
and the windowing algorithm in `Pager.cs:456–481` is worth copying exactly — though not its markup, since
paging here is AJAX and a link would discard every typed quantity on the page) and with density and
hierarchy notes from the first screenshot.

**Not everything in that file waits for this phase.** The product list wrapping below the category tree
was a flex line-breaking defect, not a preference, and was fixed during P3 rather than deferred. The rule
is that anything in the intake document which turns out to be a *defect* gets pulled forward; taste waits.

**Out of scope, permanently:** restyling the nop theme. The bulk screen lives inside whatever theme the
store runs and its CSS is page-scoped (`NOP46` D13); anything touching type, colour or spacing outside
`.bulk-order-*` gets undone by the next theme change.

---

## P10 — Entry points ✅ **DONE 2026-09-09**

**What it delivered.** The screen was reachable only by typing its URL, and `BulkOrderSendUserAfterLogin` had
been on the settings page since P1 with **nothing reading it** — a switch a store owner could turn on and
watch do nothing. Both are now real.

**The plan's one-line brief said "the login/impersonation redirect" as if it were one thing. It is two
mechanisms sharing a setting.** Signing in runs through `CustomerRegistrationService.SignInCustomerAsync`,
which returns the `IActionResult` and so decides the destination; impersonation never touches it — it writes a
`GenericAttribute` and redirects from our own `ExtendedCustomerController`. Finding that out first is what
kept the phase small.

**One gate, two questions, three call sites.** `IBulkOrderEntryService` exposes `CanUseBulkOrderingAsync`
(feature on, customer holds `PUBLIC_ACCESS_BULKORDERING`) and `ResolveLandingRouteAsync` (that, plus the
store opted in). Two methods rather than one because they are genuinely different questions: the header link
must appear whether or not the store also sends people to the screen after login. Defining the second in terms
of the first is what stops the link and the redirects disagreeing about who qualifies.

**The sign-in hook is the service, not the controller** — `CustomerRegistrationOverrideService`, beside the
three nop services this plugin already replaces. `SignInCustomerAsync` is where the destination is decided and
every route into the store passes through it: the login form, registration, external authentication. 4.6
patched each of its own logon call sites separately and had to keep them in step by hand. The base is always
called first, so cart migration, the auth cookie, `CustomerLoggedinEvent` and the activity log stay entirely
nop's; only the destination is reconsidered.

**Impersonation was already ours.** `Impersonate:853` and `PlaceOrder` both ended in
`RedirectToAction("Index", "Home")` in a file the plugin owns — two return statements, no fork. Both redirect
now, and *Place order* is the one the homepage suited least, since it exists to start ordering as that
customer. The gate is asked about the **impersonated** customer, not the admin: the admin's rights got them
this far, and whose session it is now is a different question.

**The link: one widget zone, not P8's three.** `HeaderLinksBefore` (`HeaderLinksAfter` already carries the
packname selector, and nop maps one component per zone per plugin). P8 needed a safety net because an emptied
model plus a missing zone is a product page nobody can buy from; here the worst case is a missing link and the
URL still works. Applying P8's reasoning rather than copying its answer. If a theme ever drops the zone, the
fallback needing no new mechanism is the `CustomerNavigationModel` the P8 dispatch already feeds.

**One bug worth remembering, found in the client's debugger.** The first version tested "did nop return its
homepage *route result*" as a proxy for "nop had nowhere particular to send them". But logging out lands on
the homepage, and the login link then carries `returnUrl` pointing at it — so the commonest path of all
returned a `RedirectResult("/")` and the redirect never fired. The rule "an explicit returnUrl wins" was right
in principle and too literal in practice: `"/"` is the homepage wearing a query string. `IsHomepageDestination`
now asks where the customer is being **sent** rather than how nop expressed it, and only the site root in its
few spellings counts — anything with a path is a real destination and still wins. `tasks/lessons.md` #39.

**Acceptance, 2026-09-09: 18 checks, all passed**, deliberately weighted toward the half where nothing should
change — signing in from a product page, from the cart with a guest cart to migrate, without the permission,
with either setting off. This code sits on the path every customer takes into the store and its failure mode
is silent: somebody who quietly loses what they were doing does not file a bug.

---

## Still open across the build (as at 2026-09-09)

Nothing here blocks use of the screen. **P7's T24 and T23h were closed on 2026-09-09** and have been
removed from this table.

| | What | Where |
|---|---|---|
| **I16** | A cell can say "500 in stock" and refuse 3 — not a parity break, waits on a decision about cell density | `BulkOrdering_49_P9_Interface.md` |
| **I17** | The theme's 456px notification bar gives every page of the store a horizontal scroll on a phone — belongs to whoever owns `CW.Standard` | same |
| **F34** | The product page never says "n in cart"; needs `QuantityInCart` on `BulkOrderGridModel` | `BulkOrdering_Future_Improvements.md` |
| Housekeeping | The *Test Language (P7)* second language is still on the test store | — |
| Housekeeping | `CW.Standard` cannot render without `CW.ThemeCore` installed | `CW_Theme_Architecture_Foundation.md` |


## Cross-cutting, every phase

- **Measurements run on the client backup, which is a nopCommerce 4.1 database** (recorded 2026-09-05). The
  4.9 database is the test instance and holds ZZT data only. Every measurement script is written to the
  **4.1 schema**, states that in its header, and marks any 4.9-only table or setting as *4.9 test DB only*.
  Table names to get right: `Product_ProductAttribute_Mapping`, `ProductAttributeValue`,
  `ProductAttributeCombination` (same name in both versions). `tasks/lessons.md` #31.

- **Razor compiles on first request.** A clean `dotnet build` proves nothing about a `.cshtml` — load the page
  (`tasks/lessons.md` #5, #9). Every phase's acceptance includes loading the affected pages.
- **Defaults classes split by layer**, following nop and the six existing CW examples:
  `Domain/{Feature}/{Feature}Defaults.cs` holds domain constants (no cache keys);
  `Services/{Feature}/{Feature}ServicesDefaults.cs` — note the **plural** — holds cache keys and prefixes only.
  A constant a Razor view consumes goes in `Domain/`: a view referencing `Nop.Plugin.CW.Core.Services.*` is a
  layer violation (`CLAUDE.md §5.1`). Bulk ordering gets `Domain/BulkOrder/BulkOrderDefaults.cs` in P1 and
  `Services/BulkOrder/BulkOrderServicesDefaults.cs` in P2, when the first cache key exists.
- **Adding locale resources? Bump `plugin.json`'s `Version`.** `CWCorePlugin.AddOrUpdateLocaleResources()` runs
  from `InstallAsync`, and from `UpdateAsync` only when the target version contains `DATA` — and nop skips
  `UpdateAsync` entirely when the version string is unchanged
  (`Nop.Services/Plugins/PluginService.cs:687`). Both gates must pass or every new label renders as its raw
  resource key, with no error. **This bit P1** and applies to every phase that adds a resource.
- **Every new CSS/JS file needs its own `.csproj` entry** or it 404s silently (`CLAUDE.md §9.8`). This csproj
  has two idioms — `<Content Include>` with `ExcludeFromSingleFile`/`CopyToPublishDirectory`, and plain
  `<None Update>` with `CopyToOutputDirectory`. Both deploy to the plugin output folder; follow whichever the
  neighbouring files in that folder use.
- **A `nop-override-store-checkbox` over a *group* of inputs needs `asp-parent-container`, not `asp-input`.**
  `asp-input` emits `#PropertyName`; if no element has that id the checkbox renders, looks right and controls
  nothing. Applies to any future setting rendered as more than one control.
- **Assets are registered as narrowly as the markup that needs them** — not globally, which is `NOP46` D13.
- **No user-visible string is hardcoded.** `NOP46` D19; key convention `Plugins.CW.Core.BulkOrder.*`.
- **No `EngineContext.Current.Resolve<T>()`**, no Bootstrap 3 idioms, no Kendo grids (`CLAUDE.md §23`).
- Lessons to re-read before each phase: **#10** (verify the guard fires, not that it exists), **#13** (no state
  mutation inside a LINQ predicate; independent verification), **#20** (an absent validator file is not an
  absent validation), **#22** (enumerate what can *undo* new state).

---

## Carried forward from P2

- **`<script>` blocks must not go inside `_Content.cshtml`, `_Items.cshtml` or the category navigation view.**
  `innerHTML` does not execute them, so they run on the server-rendered first page and silently never again.
  P3's grid wants per-row behaviour: delegate it from `init`, or bind it where the pager is bound.
- **The JS couples to enum member names by string** — `toggleOrderPicker` compares against `'LatestOrdered'`.
  P3 adds more of this coupling (pin state, attribute ids); decide then whether those travel as data
  attributes instead of literals.
- **`BulkOrderMaxCellsPerProduct` (400) is configured but has no consumer yet.** P3 is where the cell budget
  of design §5.4 starts reading it. **Measured and settled** — see below.

## Carried forward from P1

- `Areas/Admin/Views/MultiStore/_CreateOrUpdate.cshtml:140` passes the feature set to `Admin.StoreNav.init`
  as named properties (`packName:`, `subsidy:`, `exchanges:`, `creditNotes:`). Adding `BulkOrder` to
  `CWFeatureSet` did not break it, but **whenever a bulk-ordering admin nav item is added, that object needs a
  `bulkOrder:` line** or the nav will treat the feature as off.
- ~~P0's remaining queries are still outstanding and now gate **P3** as well as P4 — they set the defaults for
  `BulkOrderMaxCellsPerProduct` and confirm `MAX_AXIS_COUNT = 5`.~~ **Answered.** Projected cells per product,
  measured across the client catalogue (product of the value counts of every axis-eligible mapping — an upper
  bound, because it applies neither `MAX_AXIS_COUNT` nor `IsPinOnly` rule 3):

  | Projected cells | Products | Max axis-eligible attributes |
  |---|---|---|
  | ≤ 50 | 4,128 | 4 |
  | 51–200 | 54 | 4 |
  | 201–400 | **0** | — |
  | 401–2,000 | **0** | — |
  | > 2,000 | 13 | 15 |

  Two conclusions, both firmer than the guesses they replace:

  - **`BulkOrderMaxCellsPerProduct = 400` is confirmed, and the exact value does not matter.** The
    distribution is bimodal with nothing at all between 201 and 2,000, so every setting in that range renders
    the identical grid for every product in the catalogue. 400 sits in the middle of the empty band. It is a
    defensible default rather than a round number, and it is not worth tuning.
  - **`MAX_AXIS_COUNT = 5` is generous, and almost never binds.** 4,182 of 4,195 products have at most **four**
    axis-eligible attributes, so the cap is inert for 99.7% of the catalogue. It binds only on the 13 outliers,
    one of which has **15**. No real product exercises the boundary, which is precisely why the synthetic
    `ZZT-11` (six eligible against a cap of five) has to stay in the test data — check 3.6 is the only
    coverage the rule gets.

  The 13 outliers are the only products that will run the demotion loop more than once, so acceptance groups
  7 and 8 should be run against **those**, not only against `ZZT-13` / `ZZT-14`. A synthetic worst case that
  nobody's catalogue contains proves less than the real one that does.

## What is deliberately not phased

Everything in `BulkOrdering_Future_Improvements.md` (F1–F30), plus the two open items carried in design §16:
the `ContractItems` filter mode, and Favourites / Quote-list integration.
