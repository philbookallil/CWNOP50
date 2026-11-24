# Open item 4.23 — a packname order is checked on the lines it orders

2026-11-24. Closed the same day: code applied, unit tests pass, T1–T4, T6 and T7 pass; T5 waits for the PayPal round (item 1.1).
The lasting part is in `PackName_Order_Shipments.md` §9.0a.

## What was wrong and what changed

`PreparePlacePackNameOrderDetailsAsync` ran nop's `PrepareAndValidateShoppingCartAndCheckoutAttributesAsync`, which loads
the customer's whole cart, and swapped in the packname lines afterwards. A line of no packname was checked as the
selected packname and could refuse the order; it also counted towards the minimum order amounts. It now runs
`PrepareAndValidatePackNameCartAndCheckoutAttributesAsync`: nop's body with the packname cart loaded (`CLAUDE.md` §10.3).
No resource, registration, migration or view changed, so no `plugin.json` version bump.

| Path | Behaviour |
|---|---|
| Payment method that places its own order (`PlaceDelegatedPackNameOrderAsync`) | Unchanged: refuses a cart with a line of no packname (tested) |
| Exchange return order (`PrepareReturnOrderDetails`) | Unchanged: builds its own cart and calls neither method (read, not tested) |
| Plain nop order | Unchanged: nop's method checks every line (tested) |
| A replacement line of a closed packname | Was checked and then left out of the order; now not checked. The ordered lines are the same |

Dropped: pointing the flyout's minimum-subtotal check at the packname lines. The packname flyout never reads that flag
(item 3.24), and the checkout page already warns on the packname lines.

Not covered: nop's checks across lines (item 4.24) and the checkout's refreshes (item 4.25).

## Dev-site test plan

After build and restart, PackName on, a store with a packname limited to allocated products (Liam) and one that is not
(James). Side effects: two orders placed, cart lines added, a product unpublished and `OrderSettings.MinOrderSubtotalAmount`
changed, both put back.

| # | Step | Expected |
|---|---|---|
| T1 | PackName off, add a product Liam may not see; PackName on; add an allowed product for Liam and one for James. Liam selected, packname checkout, Confirm | Placed, with the two packname lines only; the plain line stays in the cart (before: "Product is not published") |
| T2 | The same cart again, James selected, Confirm | Placed, as before |
| T3 | Unpublish the product of Liam's own line, Confirm | Refused, "Product is not published"; publish it again |
| T4 | Minimum subtotal above the packname lines and below packname + plain lines, open the packname checkout | The page warns and offers no Confirm; a direct post is refused with the minimum message. Put the setting back |
| T5 | The plain line in the cart, a method of *OwnPlacementPaymentMethods* (sandbox; Eduardo signs in) | Refused: "Your cart holds items that cannot be ordered with your packname order…" |
| T6 | PackName off, a plain cart with an unpublished product, nop's checkout | Refused, as nop does |
| T7 | Approve an exchange that places its replacement order | Placed, as before |

## Test run (2026-11-24, dev site, store 1, Steve Gates impersonated)

The cause was also confirmed in the log of the original run: entry 8298 (Steve's order) was refused inside nop's
`PrepareAndValidateShoppingCartAndCheckoutAttributesAsync`, called from the packname placement.

| # | Result |
|---|---|
| T1 | Pass. Plain HTC (refused for Liam), Ray Ban for James, jeans for Liam, Liam selected: order 5036 holds the two packname lines, the HTC stays in the cart |
| T2 | Pass. The same cart, James selected: order 5037 |
| T3 | Pass. Jeans unpublished: refused, "Product is not published"; the log's stack is the new method |
| T4 | Pass. Minimum $100, packname lines $68.50, HTC $245: the page warns and offers no Confirm; a direct post is refused by the new method. The packname flyout still shows Checkout (item 3.24) |
| T5 | Not run. PayPal has no merchant or client id on this site and is not offered; nop's own checkout always sends a packname customer to the packname one. Unit tests only |
| T6 | Pass. PackName off, unpublished jeans in the cart, nop's one-page checkout: refused inside nop's own method |
| T7 | Pass. Return 1002 authorised (its additional fee switched off so that nothing was due): order 5038 placed and settled by subsidy, with the plain lines still in Steve's cart |

Left behind: orders 5036–5038; return 1002 completed; three lines in Steve's cart (6070 HTC and 6076 jeans, plain; 6075 Ray Ban for
James); product 32 re-saved through its form (a trailing space, `&trade;` written as ™, default base-price units). The
PackName switch, the minimum and product 32's *Published* are as they were: the MultiStore and Order settings compare identical.
