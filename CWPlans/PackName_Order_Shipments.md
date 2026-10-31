# PackName order shipments

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

**Status: applied and tested (2026-08-19).** Shipments work on a packname order again, organised by
shipping destination. This is the single record, including the test plan.

> Picking this up cold: read §1 and §2. §7 is what is deliberately still wrong; §8 is the runnable test.

---

## 1. The problem

Shipping on a packname order is **per packname** — each has its own address, method and status — so the
order-level shipping block that nop renders does not describe it. It had been hidden with jQuery:

```js
$('#order-billing-shipping').find('.card.card-default').hide();
```

`#order-billing-shipping` contains **two** `.card.card-default` elements: the shipping method card and
the shipments card. The selector took both, and *Add new shipment* lives in the second one's footer. So
hiding the shipping panel had also removed the ability to create shipments at all.

The line above it — `.col-md-6.card-group').eq(1)` — was positional, and would have broken the next time
nop reshuffled that markup.

---

## 2. The unit that solves it: the shipping destination

nop's `Shipment` is **order-scoped and carries no address**. It assumes one destination per order. A
packname order has several.

But the data was already there:

- `PackNameOrderItem` maps every order item to exactly one packname;
- `OrderShipping` holds address + method + status per packname;
- `CWOrderService` already computed which packnames *share* a destination — same `ShippingAddressId` +
  `ShippingMethod`.

So the natural unit is neither the order nor the packname but the **shipping destination**: a distinct
(address, pickup, method) triple, and every packname — and therefore order item — that shares it.

```csharp
Domain/Orders/DTOs/OrderShippingDestination.cs
    Key                 // the lowest PackNameId sharing it — stable, URL-safe
    ShippingAddressId, PickupInStore, ShippingMethod
    AddressSummary      // one line, already localised, never empty
    ShippingStatus      // localised; the least advanced of its packnames
    PackNames, PackNameIds, OrderItemIds
    HasItemsToShip      // set by the admin factory
    Label               // AddressSummary · Method (PackNames)
```

Built by `ICWOrderService.GetOrderShippingDestinationsAsync(orderId)`, which returns **empty for a
non-packname order** — and that emptiness is also how the order screen decides which shipping block to
render.

Everything else follows from that one idea:

| Question | Answer |
|---|---|
| What can a shipment contain? | items from **one** destination — enforced server-side |
| Which destination did a shipment go to? | the **Ships to** column, derived from its items |
| What does "Add shipment" ask first? | nothing — the button is on the destination's own row |
| What does shipping update? | that destination's packnames' `OrderShipping.ShippingStatus` |

**Why not a shipments block per packname:** two packnames sharing a destination would each show the same
shipment, with no honest home for it. The destination is the grouping the data actually has.

---

## 3. What was built

### 3.1 The order page owns its own billing/shipping card

The jQuery is gone. `Areas/Admin/Views/Order/Edit.cshtml` resolves the destinations through
`IPackNameOrderModelFactory.PrepareShippingDestinationsAsync` — a factory, not a service, because
`CLAUDE.md` §5.1 says views reference their own area's factories — and then:

- packname order → `_CWOrderDetails.BillingShipping.cshtml`, and the **Products card is not rendered**;
- otherwise → nop's `_OrderDetails.BillingShipping.cshtml`, untouched.

Not rendering beats rendering-then-hiding: no flash of the wrong content, no grid initialised for a table
nobody sees, and nothing to re-break when nop moves a `div`.

`_CWOrderDetails.BillingAddress.cshtml` is the one copied block — nop keeps billing and shipping in a
single partial with no seam between them.

### 3.2 The Shipping destinations table

Right-hand column of the card. One row per destination: **Ships to · Method · PackNames · Status ·
[Add shipment]**.

The action lives on the row rather than in a card footer, and that is the whole reason the button is
small: the row already names the destination, so the label does not have to. A destination with nothing
left to ship shows a muted `—` — an empty cell in a table with buttons elsewhere reads as a rendering
fault.

Gated on `SHIPMENTS_CREATE_EDIT_DELETE`, which is what nop's `AddShipment` action requires. The old
footer leaned on view permission because it happened to sit inside the shipments card.

Everything here is suppressed, and nop's *"Shipping info not required"* shown instead, when
`order.IsShippable` is false.

### 3.3 The shipments grid

Full width below, with a **Ships to** column that nop's grid does not have. Fed by
`Areas/Admin/Controllers/CWOrderController.ShipmentsByOrder`, which wraps nop's own
`PrepareOrderShipmentListModelAsync` and resolves each shipment's destination from its items.

`CWOrderShipmentListModel : BasePagedListModel<CWShipmentModel>` exists so the payload is typed as the
row shape the grid renders, rather than smuggling a derived instance through nop's model and depending on
the serialiser's polymorphism rules.

nop's expandable child table — a shipment's products, warehouse, quantities — is preserved, along with
the caret column that opens it, and the date column still switches between *ready for pickup* and
*shipped* on `order.PickupInStore`.

### 3.4 One destination per shipment

`Filters/AddShipmentDestinationFilter.cs`, registered globally in `NopStartup` beside the two filters
already there. It follows `ReturnRequestAdminFilter`'s pattern — act on a core admin action after it
runs — rather than forking nop's 300-line `AddShipment.cshtml` or its 90-line action.

| Direction | What it does |
|---|---|
| **GET** with `?cwDestination={key}` | trims `ShipmentModel.Items` to that destination's order items, and puts the destination in `ViewData` for the banner |
| **POST** | reads every `qtyToAdd{orderItemId}` with a quantity, resolves each to a destination, and **refuses** if they are not all the same one |
| **POST**, on nop's "no products selected" redirect | re-attaches `cwDestination`, which the form would otherwise lose — it posts to a bare URL |

The GET makes the mistake hard; the POST makes it impossible, because a querystring is editable and a
form can be replayed.

### 3.5 The destination badge

`CWShipmentDestinationViewComponent` renders on two zones with two ways of knowing:

- **`OrderShipmentAddButtons`** — the filter has already resolved the destination into `ViewData`, and
  the view echoes the key back into the form as a hidden input, which is what makes §3.4's redirect
  work;
- **`OrderShipmentDetailsButtons`** — nothing was chosen, so it derives the destination from the saved
  shipment's items. A legacy shipment spanning two destinations shows both, joined.

Both zones sit in a page header's `float-right`, hence a compact badge rather than a full-width alert.
`ShipmentDetails` shows no address of its own, so on a multi-destination order this is the only thing
that answers *"where did this one go"*.

### 3.6 Per-packname shipping status, unfrozen

`ICWOrderService.UpdateOrderShippingAsync` existed and **was never called anywhere** — the status was
written once at placement as *Not yet shipped* and never changed, so that row on every packname card was
decoration.

`EventConsumer` now consumes `ShipmentSentEvent` and `ShipmentDeliveredEvent` and moves the destination's
packnames, mirroring `OrderProcessingService` one level down:

- **sent** → `PartiallyShipped` while anything is still unshipped or not yet in a shipment, else `Shipped`;
- **delivered** → `Delivered` **only** when nothing is outstanding anywhere in that destination, else the
  status is left alone.

**There is deliberately no ready-for-pickup handler.** `ShippingStatus` has no such member, and nop's
`ReadyForPickupAsync` stamps a date without touching the status at all — it is a shipment date, not an
order status.

Cache invalidation is already handled: `OrderShippingCacheEventConsumer` evicts the packname-groups key
whenever an `OrderShipping` row changes.

---

## 4. Traps worth remembering

| Trap | What actually happens |
|---|---|
| `ShippingStatus.ReadyForPickup` | **does not exist.** Five members: ShippingNotRequired 10, NotYetShipped 20, PartiallyShipped 25, Shipped 30, Delivered 40 |
| `order.PickupInStore` vs `destination.PickupInStore` | use the **order's**. nop's `ShipAsync` throws when the order is pickup and `ReadyForPickupAsync` throws when it is not, so the order flag governs which dates can exist. Reading the destination flag would strand a pickup destination on *Partially shipped* forever |
| `GetTotalNumberOfItemsCanBeAddedToShipmentAsync(orderItem)` | re-reads **every shipment and every shipment item on the order** on each call. Calling it per order item made opening a large fully-shipped order ~250 queries. Aggregate once instead |
| The admin area uses **Newtonsoft**, not System.Text.Json | `BaseAdminController.Json` overrides it, and `BasePagedListModel` carries `[JsonProperty]`. A first draft justified a model by a System.Text.Json behaviour that does not apply here |
| `BaseAdminController` already carries `[Area]`, `[AutoValidateAntiforgeryToken]`, `[ValidateIpAddress]`, `[AuthorizeAdmin]`, `[ValidateVendor]` | repeating them on a plugin admin controller implies the base does not have them |
| Admin view components resolve `View(model)` with no path | `ViewsPathsRegistry` registers `~/Plugins/CW.Core/Areas/Admin/Views/Shared/Components/{1}/{0}.cshtml` — but only for components in `Areas/Admin/Components/` |
| `Order.ShippingAddressId` on a packname order | a **placeholder** — the first packname's address, set only so nop does not throw. See §7 |
| Shipping per destination | free shipping, additional shipping charges and the shipping discount are evaluated on the lines going to **each destination**: nop's `AdjustShippingRateAsync(rate, cart)` runs `IsFreeShippingAsync(cart)` and adds every product's charge on the cart it is given, so handing it the whole cart made every destination free once the cart passed the threshold (measured: two destinations at $20, free over $400 → $20 each, where the whole-cart rule gave $0). The discount is applied per destination and recorded once |
| A rate that cannot be computed | is `null` (nop's contract → "calculated during checkout"), never $0; every caller in the plugin handles null |
| The checkout's AJAX contract | `RemoveShoppingCartItem` / `UpdateShoppingCartItem` answer `success = false` + `ShoppingCart.ItemNotFound` for an id not in the cart; `UpdateShippingAddress` refuses an invalid address (`PackNameCheckout.InvalidShippingAddress`) and leaves the assignment; `ConfirmOrder` logs exceptions; `IsAddressValidForCheckoutAsync` is read-only. A change re-rates only the packname changed (the others keep their options; `RatedFor` says what they were rated for) and the page redraws in one request |
| nop validates **detached** cart items | editing a non-packname order in admin restores a cart whose items carry `Id = order item id`, which can equal another customer's packname cart row; the cart-line packname window therefore resolves the packname only from a row that is this customer's and holds this product |
| Re-order | adds each line into the packname it was ordered for; a packname the customer no longer owns is skipped with a warning, never moved to another packname (`subsidy-system.md` §8.6) |

---

## 5. Where the code lives

| Area | Files |
|---|---|
| Destination DTO + service | `Domain/Orders/DTOs/OrderShippingDestination.cs`, `Services/Orders/CWOrderService.GetOrderShippingDestinationsAsync` |
| Order page | `Areas/Admin/Views/Order/Edit.cshtml`, `_CWOrderDetails.BillingShipping.cshtml`, `_CWOrderDetails.BillingAddress.cshtml` |
| Models | `Areas/Admin/Models/Orders/CWOrderBillingShippingModel.cs`, `CWShipmentModel.cs`, `CWOrderShipmentListModel.cs`, `CWShipmentDestinationModel.cs` |
| Grid data | `Areas/Admin/Controllers/CWOrderController.cs` |
| Factory | `Areas/Admin/Factories/PackNameOrderModelFactory.PrepareShippingDestinationsAsync` |
| Guard | `Filters/AddShipmentDestinationFilter.cs`, registered in `Infrastructure/NopStartup.cs` |
| Badge | `Areas/Admin/Components/CWShipmentDestinationViewComponent.cs` + its `Default.cshtml` |
| Status | `Services/EventConsumer.AdvancePackNameShippingAsync` |

---

## 6. Known limitation — a packname order's items cannot be amended in admin

Hiding the Products card means no add-product, no quantity change, no per-item refund from the order
page. The packname panels below are read-only.

This is not a regression — the deleted jQuery hid the same card — and it is arguably **protective**:
nop's *add product to order* would create an `OrderItem` with no `PackNameOrderItem`, which would be
invisible in every packname panel, belong to no destination, and never appear in a shipment's item list.
Every order item on a packname order gets one at placement, and the hidden card is what preserves that.

But *"the customer wants one more shirt on that order"* has no answer today except a new order. Worth
raising with the manager.

---

## 7. Deliberately still wrong

### 7.1 Addresses — phase 9, a release blocker

`Order.ShippingAddressId` is the first packname's address. Seven surfaces read it, and the first one is
not a display problem:

1. **the packing slip PDF** — the document that goes on the box, so a parcel can be dispatched to the
   wrong branch;
2. the order invoice PDF;
3. the customer's order details page;
4. order emails, including the confirmation;
5. the customer's shipment details page;
6. shipment sent / delivered / ready-for-pickup emails;
7. the admin shipment list's location filters.

The admin order page and the destination badge are correct — which makes the contradiction visible:
*Shipment Details* names the right destination in a badge, with a **Print packaging slip** button beside
it that produces a slip addressed to a different one.

✅ **Closed by phase 9 — applied and tested 2026-08-27.** All seven surfaces now read the shipment's or the order's real destinations; items 3 (the customer order details page) and the admin screens turned out never to have been broken. Record: **`CWPlans/Exchanges_Rebuild_Record.md` §4**.

**What the slip prints under each product** (subsidy phase 6 Part 2, 2026-10-07): a packname order's slip is a `CWShipmentDocument`, which prints the product's attribute lines under its name — nop's `ShipmentDocument` prints none — followed by who the line is for (`For: packname · department · cost centre`, empty parts left out). The invoice prints the same For line. A plain order, or a packname order with no recorded shipping group, gets nop's slip. Record: `CWPlans/subsidy-system.md` §7.

### 7.2 Pickup in store — phase 8, blocked on manager Q8

`OrderShipping.PickupInStore` is hardcoded `false` at placement, and nothing offers pickup at checkout,
so the pickup half of everything here is unreachable. **Do not delete the pickup branches as dead code**
— they are the correct shape for phase 8a.

**Full spec: `CWPlans/Exchanges_Rebuild_Record.md` §5.**

### 7.3 Deleting a shipment leaves statuses stale — matching nop

`OrderController.DeleteShipment` reverses booked inventory and deletes the shipment, but never touches
`Order.ShippingStatus`. Ours behaves identically.

Consuming `EntityDeletedEvent<Shipment>` and recomputing would make the packname status *more* correct
than nop's order-level one — and then the two would disagree on the same screen. Matching the wart is
more defensible until nop fixes it.

### 7.4 The order-level shipping method is no longer editable

Deliberate: on a packname order the order-level value is arbitrary, and the real ones are per
destination. Nothing downstream is known to read it.

---

## 8. Test plan

Prerequisites in §8.1 will cost an hour each if they are wrong — check them before building anything.

### 8.1 Prerequisites

**Products must be shippable.** Catalog → Products → **TEST-SHIRT** → *Shipping* tab: **Shipping
enabled** on, **Free shipping** off. Same for TEST-CAP and TEST-JACKET. If shipping is disabled the order
gets `ShippingNotRequired` and no shipment can exist at all.

**CW.Freight must be installed, active, and resolving a rate.** Admin → Configuration → Shipping
providers. This one is specific:
`PackNameCheckoutModelFactory.SetShippingOptionsByPackNamesAsync` asks for options with
`allowedShippingRateComputationMethodSystemName: "CW.Freight"` and **ignores every other provider** on the
packname checkout. If no CW.Freight rate resolves you cannot pick a shipping method and checkout blocks.

Confirm both by putting TEST-SHIRT in a cart and checking the packname box offers a shipping method.

### 8.2 Setup — one order that ships to two places

1. Admin → **CW Admin → Multi-Stores** → your store → **Branches**.
2. You have one Branch with one PackName. Add a **second PackName** on the same PackName List — call it
   **PN-B** — with a **different** shipping address (different street *and* suburb, so they are
   distinguishable at a glance).
3. **Customer A must be a manager** over the PackName List, not merely linked to one PackName. Log in as
   A and confirm the header **PackName selector** offers both. If it shows one, fix that first — a
   single-packname order cannot exercise any of this.
4. As A: select **PN-A** → add **TEST-SHIRT $45**. Switch to **PN-B** → add **TEST-CAP $20**.
5. Hover the header cart: *"PN-B's cart: 1"*, *"Total in cart: 2"*.
6. Packname checkout — each packname box has its own address and its own shipping method. Place the order.

### 8.3 The order page

| # | Expect |
|---|---|
| S1 | **Billing & Shipping**: billing address left, **Shipping destinations** table right |
| S2 | The table has **two rows** — PN-A's address and PN-B's — each with method, packname, **Status = Not yet shipped**, and a small **Add shipment** button |
| S3 | The **Products** card is **not** shown |
| S4 | A **Shipments** grid, empty, with a **Ships to** column — and **no card footer** below it |

### 8.4 Ship one destination

7. Click **Add shipment** on PN-A's row.

| # | Expect |
|---|---|
| S5 | A blue **Shipping to** badge naming PN-A |
| S6 | The product list contains **only TEST-SHIRT**. TEST-CAP must not be listed |

8. Quantity **1** for TEST-SHIRT, tick **Can ship**, Save.

| # | Expect |
|---|---|
| S7 | The grid has one row, **Ships to = PN-A's address** |
| S8 | Click the **caret** at the left of that row — it expands to show TEST-SHIRT, its warehouse and quantity |
| S9 | The destinations table reads **Shipped** on PN-A and **Not yet shipped** on PN-B |
| S10 | The packname panels below agree — PN-A shipped, PN-B not. Same answer in both places |
| S11 | PN-A's row now shows a muted **—** instead of a button; PN-B's button remains |

### 8.5 The guard — this one must fail

9. Edit the URL by hand to `/Admin/Order/AddShipment/<orderId>` with **no** `?cwDestination=`.

| # | Expect |
|---|---|
| S12 | No badge, and the **full** item list — both products |

10. Enter a quantity for **both** products and Save.

| # | Expect |
|---|---|
| S13 | **Refused**, with *"A shipment can only contain items going to one destination"* |
| S14 | **No new shipment** — the grid still has one row |

**If a shipment is created here, stop.** That is the whole point of the guard.

### 8.6 Shipment details

11. Click **View** on the shipment.

| # | Expect |
|---|---|
| S15 | The **Shipping to** badge names **PN-A** |
| S16 | The **Print packaging slip** button beside it produces a slip addressed to **PN-A** — the shipment's destination (fixed by phase 9, §7.1; `PdfOverrideService.PrintPackagingSlipToPdfAsync`) |

### 8.7 Finish, and order completion

12. Add a shipment for **PN-B** and mark it shipped.

| # | Expect |
|---|---|
| S17 | Both destination rows read **Shipped** |
| S18 | The order's own shipping status (Info card) reads **Shipped**, not Partially shipped |

13. Mark **both** shipments delivered.

| # | Expect |
|---|---|
| S19 | Both destination rows read **Delivered** |
| S20 | With **Complete order when delivered** on, the order status becomes **Complete**. With it off it was already Complete once everything shipped. Neither path needed any work from us — `CheckAndSaveOrderStatusAsync` reads `Order.ShippingStatus`, which nop still maintains order-wide |

14. *(optional, proves the partial case)* On a fresh two-destination order, ship **part** of one
destination's quantity and deliver that shipment. That destination must read **Partially shipped**, not
Delivered.

### 8.8 Permissions

15. Log in as a role with **Orders → view** but **not** shipments create/edit/delete.

| # | Expect |
|---|---|
| S21 | **No Add shipment button on any row**; the grid still renders. This is behaviour that did not exist before — the old footer leaned on view permission |

### 8.9 Regression — the most important test here

16. As **Customer B** (no packname, no subsidy), place an order and open it in admin.

| # | Expect |
|---|---|
| S22 | **Billing & Shipping** is exactly nop's — billing left, **shipping address** right, editable **shipping method**, shipping status |
| S23 | The **Products** card **is** shown |
| S24 | nop's own shipments grid — **no** Ships to column, **one** Add new shipment button |
| S25 | Creating a shipment works normally, no destination badge anywhere |

If anything on a non-packname order looks different, the `isPackNameOrder` switch is wrong.

### 8.10 A packname order that does not ship

17. Set TEST-CAP to **Shipping enabled off**, place a packname order for it alone.

| # | Expect |
|---|---|
| S26 | The card shows **"Shipping info not required"** — no destinations table, no grid, no buttons |

Turn shipping back on afterwards.

### 8.11 Result sheet

| # | Test | Pass | Notes |
|---|---|---|---|
| S1–S4 | Two destinations, buttons on their rows, no footer | | |
| S5–S6 | Narrowed item list and badge | | |
| S7–S11 | Ships to column, child table, per-destination status, — when done | | |
| S12–S14 | **Cross-destination shipment refused, nothing saved** | | |
| S15 | Destination badge on shipment details | | |
| S17–S20 | Order completes when everything is delivered | | |
| S21 | No button without create permission | | |
| S22–S25 | **Non-packname order unchanged** | | |
| S26 | Non-shipping packname order says "not required" | | |

### 8.12 If something fails

For §8.4–§8.6, the single most useful fact is **whether the item list narrowed** on the Add Shipment
screen. The filter does both jobs, so: list narrowed but no badge ⇒ the widget; neither happened ⇒ the
filter never matched the action.


---

## 9. Only the packname checkout may place a packname order

Everything above assumes the order was placed by `PackNameCheckoutController`. That is what writes
`PackNameOrderItem`, the per-packname `OrderShipping` destinations and the subsidy settlement; an
order placed any other way carries every packname's items as one ordinary cart, with one shipping
address, no destinations and **no subsidy consumed**, and it is invisible to every packname screen and
report. Phase 2a (2026-09-17) closed the three doors that led there. They are listed here because a
future change to any of them is a change to this feature.

| Door | What closes it | Notes |
|---|---|---|
| nop's own cart and checkout | `Filters/PackNameCheckoutRedirectFilter` | **Controller-level**, not a list of actions: every action on `CheckoutController` is intercepted. Since phase 7 (2026-10-08) there is no allow-list: a GET of `checkout/completed/{orderId}` is re-routed to `packname/checkout/completed/{orderId}` when the order is a packname order (`ICWOrderService.IsPackNameOrderAsync`) and passes through otherwise, so a payment plugin's return and a typed URL both land on the packname completed page |
| A payment plugin placing its own order | `OrderProcessingOverrideService.PlaceOrderAsync` | **Since phase 4c (2026-10-01): placed as a packname order** (`PlaceDelegatedPackNameOrderAsync` → `PlacePackNameOrderAsync`) when the customer's cart holds any raw `PackNameCartItem` row (read raw so a closed period still goes to the packname placement, which refuses it). Refused, before capture, when the cart holds a line the packname cart does not, when nothing is orderable, or when a stated `OrderTotal` differs from the cart total. The packname checkout hands over to methods in `MultiStoreSettings.OwnPlacementPaymentMethods` instead of placing an (unpaid) order itself. **PayPal round trip not yet tested** — PayPal Commerce stays disconnected on packname stores until it is. |
| Recurring payments | `OrderProcessingOverrideService.ProcessNextRecurringPaymentAsync` | nop writes these with `SaveOrderDetailsAsync` and never calls `PlaceOrderAsync`, and they are triggered from `OrderController` (`POST /customer/recurring-payments` with `retryLastPayment_{id}`) or the scheduled task — so neither of the other two doors sees them. It matters because `PlacePackNameOrderAsync` creates recurring payments itself (U-34). |

All three are gated on `CWFeature.PackName` for the order's store, so switching packnames off returns
the store to stock nop behaviour rather than leaving a cart that can never be checked out.

**Payment methods that place their own order — the hand-over.** A method listed in
`MultiStoreSettings.OwnPlacementPaymentMethods` (default `Payments.PayPalCommerce`; store 0, not on any screen — *All
settings*) takes the payment in its own pages and calls `PlaceOrderAsync` itself: PayPal Commerce's `ProcessPaymentAsync`
and `PostProcessPaymentAsync` do nothing, its controller places the order, then captures and marks it paid — so a refusal
at placement costs nothing. The packname checkout never places an order with such a method. `UpdatePaymentMethod` renders
a note (`Plugins.CW.Core.PackNameCheckout.OwnPlacementPaymentNote`) instead of the method's payment-info component —
PayPal's component is a script that redirects to its page the moment it renders, and in an AJAX response nop's
`NopScriptTagHelper` runs it inline, which is why the component cannot be shown there. The Checkout button then
(`PackNameCheckoutController`, `IsOwnPlacementPaymentMethodAsync`) validates the packname order, re-checks the quantity
cap, sets the customer's billing address (the one placement resolves) and shipping address (the first destination — the
address the order records as its own `ShippingAddressId`; the order still ships every packname to its own destination)
through `PrepareCustomerPaymentAddressesAsync`, and renders `Views/PackNameCheckout/PaymentHandoff.cshtml`, a full page
carrying the method's component. The method's amount comes from `GetShoppingCartAsync` + `GetShoppingCartTotalAsync`
(fee excluded), both CW overrides, so for a pure packname cart it equals the packname order's total; PayPal compares and
refuses a difference itself, and `PlaceDelegatedPackNameOrderAsync` refuses a `ProcessPaymentRequest.OrderTotal` that
differs (`PaymentAmountDiffers`), a cart holding other lines (`PackNameCartHasOtherItems`) or one that cannot be ordered
(`PackNameCartNotOrderable`). Buttons outside the payment step (product page, cart page) are not supported for packname
customers — shipping chosen inside PayPal can never match per-packname shipping — so packname stores keep *Display buttons
on product details* off. Removing a method from the list restores nop's behaviour (its component renders in the
checkout); a method missing from the list that nonetheless places its own order places the order unpaid. With packnames
off `PlaceOrderAsync` is nop's; the setting is read only by the packname checkout. The old refusal message
`Plugins.CW.Core.Order.PackNameOrderMustUsePackNameCheckout` is still what a refused delegation returns when the cart
mixes lines.

**Pending — the PayPal round trip (N-03 stays open until these pass; PayPal Commerce stays disconnected on packname
stores meanwhile).** Set-up: PayPal Commerce connected to a **sandbox** account, *Display buttons on payment method
page* on, *Display buttons on product details* off, payment type *Capture*; Manual and Check / Money Order active; the
sandbox buyer login in PayPal's popup is Eduardo's step. Fixture: Steve Gates impersonated; one packname with a credit
subsidy in period and a destination; a second packname with a different destination (subsidy P4C-CREDIT (14) is kept
for this).

| # | Test | Expected |
|---|---|---|
| T4 | PayPal selected, one packname **without a destination**, press Checkout | Refused on the packname checkout with the address message; no hand-over. (Blocked so far: a packname without a destination could not be created; the check runs before the hand-over in the code) |
| T5 | One packname, PayPal, Checkout → hand-over → PayPal page → approve in the sandbox | Order placed: `PackNameOrderItem` rows, one destination, subsidy consumed as the bar showed, **PaymentStatus Paid** with the PayPal capture id; the PayPal amount = the payable balance; the customer's shipping address = the destination |
| T6 | **Two packnames**, two destinations, PayPal | One order, two destinations, one PayPal payment of the payable balance; PayPal showed the first destination |
| T8 | **Mixed cart:** a line of no packname (packnames off → add → on) + a packname line, PayPal | Refused "This order contains PackName items and must be placed through the PackName checkout."; no order, nothing captured |
| T20 | Steve's cart over the quantity cap, PayPal selected (no sign-in needed — the refusal comes first) | The checkout reloads with the cap message; no hand-over page |
| — | After T5: delete the destination address from admin | Known: the customer's shipping address now points at that destination, so nop's foreign key refuses the delete (`PrepareCustomerPaymentAddressesAsync`) — decide what the hand-over should leave on the customer |

One thing to revisit rather than assume: no payment plugin in this solution is
`PaymentMethodType.Redirection`, so `CheckoutController.OpcCompleteRedirectionPayment` is unreachable
and needs no allow-list entry. If such a plugin is ever installed, the packname checkout's own
post-processing path needs review — it post-processes inline where nop's one-page checkout defers to
that action.

---

### 9.0 Routes of the packname checkout (phase 7)

| Route name | Pattern | Action |
|---|---|---|
| `PackNameCheckout` | `packname/checkout` | `PackNameCheckout.Checkout` (GET page, POST confirm) |
| `PackNameCheckoutCompleted` (`CWCoreDefaults.PackNameCheckoutCompletedRouteName`) | `packname/checkout/completed/{orderId:int?}` | `PackNameCheckout.Completed` — thank-you, order number, details link, the packname groups and the settlement rows (`PackNameOrderDetailsViewComponent` data); honours `OrderSettings.DisableOrderCompletedPage` (→ order details) and refuses another customer's or a plain order (→ homepage). Confirm redirects to nop's `CheckoutCompleted`; the filter above turns that into this route |

Coupons and gift cards stay out of the packname checkout (decision 14, phase 7): the `/cart` POSTs keep answering 403
and the page has no boxes; phase 6 settles any discount against the customer's share only, so nothing is at risk.

## 9.1 Nor may nop's admin order screens edit one

**Phase 2b part 1, decision 14.** The doors above stop an ordinary order becoming a packname order.
This one stops a placed packname order being taken apart afterwards.
`Filters/PackNameOrderAdminGuardFilter` blocks seven of nop's `OrderController` methods on any order
carrying `PackNameOrderItem` rows.

| Blocked | Why it had to be |
|---|---|
| `DeleteOrderItem` | **Hard-deletes** the `OrderItem` and cascades `PackNameOrderItem` and `SubsidyOrderItem` away, so consumed subsidy silently returns to available on an order that was placed and paid |
| `EditOrderItem` | Same rows, edited rather than removed; settlement is not re-run |
| `AddProductToOrder`, `AddProductToOrderDetails` | Adds a line belonging to no destination, invisible on every packname screen |
| `AddressEdit` | The addresses **are** the per-packname destinations; moving one moves every packname sharing it |
| `EditOrderTotals`, `EditShippingMethod` | The totals are the sum of per-packname settlement, and the shipping method is per destination (§7.4) |

Deliberately **not** blocked: `CancelOrder` and `Delete` — cancelling **is** the designed
subsidy-restore path, since the consumption view filters `OrderStatusId != 40 AND Deleted = 0`, so
blocking it would break the restore; the refund and void actions (money only, no packname coupling);
and `AddShipment`, which `AddShipmentDestinationFilter` already governs (§3.4). (nop 5.00 has no
`EditCreditCardInfo` action any more.)

Four things about the filter are load-bearing, and each of them was a defect first:

1. **It matches on the METHOD name, not the action name.** Four of the seven — `EditOrderTotals`,
   `EditShippingMethod`, `EditOrderItem`, `DeleteOrderItem` — are `[HttpPost, ActionName("Edit")]`
   dispatched by `FormValueRequired`, so the action name is `"Edit"` for every one of them and for the GET
   screen too (`AddProductToOrder*` and `AddressEdit` are actions of their own). `descriptor.MethodInfo.Name` is the only
   discriminator; `CoreActionFilter.IsActionName` cannot tell them apart and is not used here.
2. **Every identifier the action acts on is tested, not just the posted order id.** nop loads the
   order from the route and then takes the order *item* from a form key, or the address from
   `model.Address.Id`, and never checks either belongs to that order. So guarding the order id alone
   was bypassable: post a plain order's id with a packname order's item id in the button key and nop
   deletes the line. The order item and the address are each checked in their own right.
3. **Plural, not singular.** nop's order-item parse loop has no `break`, so the **last** matching form
   key wins; returning only the first candidate let a post carrying two button keys through. Every
   candidate is tested, which makes key order irrelevant.
4. **A packname order's own addresses count, not only its destinations.** `CORE_PN_OrderShipping`
   holds the per-packname destinations; the order's billing, shipping and pickup addresses are on the
   `Order` row and appear in no such row. Checking the destinations alone left the billing address
   editable through a **plain** order's id — verified live, and fixed by asking both questions
   (`ICWOrderService.IsPackNameOrderAddressAsync`).

**No feature-flag check, on purpose.** The first version asked
`ICWFeatureService.IsEnabledAsync(CWFeature.PackName)`, which resolves the *current* store — in the
admin area, whatever store the admin's host maps to, not the store the order belongs to. Admins edit
every store's orders from one host, so an order in another store was unguarded whenever the flag was
off here (rule 3). The `PackNameOrderItem` rows are a fact about the order and the flag is a setting:
an order carrying those rows needs protecting whether or not someone has since switched the feature
off.

**It fails closed.** A blocked method whose subject cannot be identified at all is blocked — that is
the branch a change to nop's model binding would land in, and letting it through is how a guard
quietly stops guarding. **GET redirects, POST returns 403**, for the same reason as the checkout
filter: a redirect returned to a grid or an AJAX caller renders as markup and reads as nothing having
happened.

Verified live 2026-09-21: all seven refused on a packname order, both bypass shapes refused, and — the
check that makes the rest mean anything — **all six of the same actions still work normally on an
ordinary order**.
