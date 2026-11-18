# Odoo Order Sync Handover

2026-11-18

The basic send of nop orders to Odoo 20 is built and tested (20 test cases pass): an admin sends orders by hand from *Odoo › Orders*. You take over two things: CW.Core's own data on the Odoo order, and the events that start a send automatically.

## What is built and where

Everything is in `Plugins/Nop.Plugin.Integration.Odoo`, version `2026_11_18_DATA_1.4`. The full design, decisions and test results are in `CWPlans/Odoo_Order_Sync_Spec.md`.

| Part | What it does | Where |
| --- | --- | --- |
| Orders page | Every order with its sync state; *Sync* per row, *Sync selected*, *Sync all listed*; *Cancel in Odoo* (with confirmation) for cancelled or deleted orders still open in Odoo; filters: store, sync state, payment status, dates | `Controllers/OdooAdminController.cs`, `Views/OdooAdmin/Orders.cshtml`, `Content/Admin/Odoo/odoo-admin.js` |
| What a row shows | Status text and which button a row gets | `Services/OdooOrderSyncSummary.cs` (pure, unit-tested) |
| The send and the cancel | Builds the Odoo orders from the nop order through CW.Core, sends, confirms, cancels | `Services/IOrderSyncService.cs`, `Services/OrderSyncService.cs` |
| Odoo order builder | Origin, line text, PO rule, note, the `create` values | `Services/OdooSaleOrderDraft.cs` (`OdooOrderBuilder`, internal, unit-tested) |
| Odoo API | `sale.order` find by origin / read / create / confirm / cancel; `res.partner` find or create a delivery contact; country, state, product by code | `Services/OdooApiService.cs` |
| Mapping table | One row per Odoo order of a nop order: order, destination key, Odoo id and name, status, message | `Domain/OdooOrderMapping.cs`, table `Odoo_OrderMapping` |
| Background run | Sends and cancels run on the existing sync queue; each run is a Sync Log event with a trace per order | `Services/IProductSyncQueue.cs` (`IOdooSyncQueue`), `Services/ProductSyncBackgroundService.cs`, `SyncEventType.OrderSend` / `OrderCancel` |
| Settings | Odoo delivery product and exchange fee product codes | Configure page, `Domain/OdooSettings.cs` |
| Permission | *Send orders to Odoo* (`Integration.Odoo.SendOrders`), Administrators by default; Settings and Sync Log keep *Manage plugins* | `Infrastructure/OdooPermissionConfigManager.cs` |
| Product sync addition | Saves the Odoo variant id of single-variant products (`Odoo.ProductVariantId`) | `Services/ProductSyncService.cs` |
| Tests | 41 Odoo cases | `Tests/CW.Tests/Integration.Odoo/` |

## How one order is sent

One nop order becomes one confirmed Odoo sale order **per delivery destination**. `OrderSyncService.BuildDraftsAsync` and `SendOrderAsync` do it in this order:

1. **Destinations.** A packname order: one per CW.Core destination (`ICWOrderService.GetOrderShippingDestinationsAsync`). A plain order: one, key 0, from the order's own address and shipping.
2. **Customer.** The ordering customer's branch (`IBranchService.GetBranchByCustomerIdAsync`), walked up to the top branch; its `CardCode` is the Odoo company id. No branch, or a card code that is not a number: the order is **held**.
3. **Lines.** Each item's Odoo variant (the combination's `ODOO_{id}`, else `Odoo.ProductVariantId`), quantity, unit price = `PriceExclTax` ÷ quantity to cents. The text adds nop's attribute description (print name included), the packname line "For / Branch / Dept / CC" from `PackNameOrderItem`, and the PO when it is not on the order. An unlinked product holds that destination.
4. **Shipping.** One delivery line on the configured delivery product (`Delivery_007` on the test instance), named after the nop method. No delivery product set and shipping charged: held.
5. **First destination only.** Exchange fee lines (one per fee type) and the order note: nop number and UTC date, "Exchange replacement order", subsidy and store credit from the settlement row, "Paid in nop". Other destinations' notes point to the first.
6. **Send.** A `Pending` mapping row is written first. Then: find by `origin` (`nop {number}`, plus `/ {key}` with several destinations), else create; save the Odoo id; confirm; read back. `Confirmed` only when Odoo says *sale*. Odoo's untaxed total is compared with nop's; more than one cent apart is kept as a warning. An Odoo error sets `Failed` with Odoo's own message.
7. **Delivery address.** Matched in Odoo by street, postcode and city under the company; a `delivery` contact is created when none matches.

Cancel: `action_cancel` on each open Odoo order, then read back; `Cancelled` only when Odoo says *cancel*.

## Your part 1: CW.Core's data on the Odoo order

What the send takes from CW.Core works and is tested against Odoo; review it and extend it. The plugin reaches CW.Core through the built DLL (`CLAUDE.md` §10.2 Pattern C) and reads no CW.Core feature switch.

| Data | Built as | Decision | Open for you |
| --- | --- | --- | --- |
| Packname destinations | One Odoo order per destination, each with its own delivery address | A5 | Nothing known |
| Packname, branch, department, cost centre | Line text "For: … · Branch: … · Dept: … · CC: …" from the `PackNameOrderItem` snapshot | A11 | Odoo Studio fields instead, if Odoo must report on them |
| PO number | Customer Reference when every line of that Odoo order has the same PO, else "PO: …" on each line. Read from the packname **at send time** (no snapshot exists) | A10 | A PO snapshot at placement, if a later change must not reach Odoo |
| Print name and other non-variant attributes | Line text, from nop's attribute description | A12 | Nothing known |
| Subsidy and store credit | Gross lines; amounts written in the order note from the settlement row (`CORE_PN_OrderSettlement`) | A9 | **The client's accountant decides** how Odoo should carry them |
| Exchange orders and fees | Note "Exchange replacement order"; one fee line per fee type on the exchange fee product | A14 | The fee product must be created in Odoo and set on Configure; not tested yet (T18) |
| Order-level discounts, gift cards, reward points, payment fee | **Not sent**; Odoo's total is higher by them, and the totals warning does not see it | B1 | Second phase |
| Tax on exchange fees; tax-exempt customers | Fees sent as nop holds them; Odoo adds GST to a fee product that has it, and to tax-exempt customers | B5 | Second phase |
| Returns and credit notes | Nothing reaches Odoo, so Odoo's stock and invoicing drift after a return | B6 | Second phase |

The code to change is `OrderSyncService.BuildDraftsAsync` and `AddOrderNotes`, and `OdooOrderBuilder` for the text rules (with their tests in `OdooOrderBuilderTests` and `OrderSyncServiceTests`).

## Your part 2: what starts a sync

Today nothing is sent automatically (A1). The hook is the same one the page's buttons use: an `IConsumer<TEvent>` that queues the order ids.

```csharp
var requestId = await _odooSyncQueue.TryEnqueueSyncAsync(SyncEventType.OrderSend, new[] { order.Id });
// SyncEventType.OrderCancel for a cancelled or deleted order
```

nop's own events are the likely triggers: `OrderPlacedEvent`, `OrderPaidEvent`, `OrderCancelledEvent`, and the order's soft delete (an `EntityUpdatedEvent<Order>` with `Deleted` set). Which event sends an order is the client's call; the page lists unpaid orders and has a payment-status filter for that reason (B3).

**The queue drops what it cannot take.** `TryEnqueueSyncAsync` returns `null` when a run of the same type is already queued or running: one `OrderSend` at a time, by design, so the page never sends twice. An event that arrives during a run is lost, and the queue lives in memory, so a restart loses a queued request too. Before wiring events, give them somewhere to wait, for example:

- a pending list (a table, or a flag on `Odoo_OrderMapping`) that the event writes and a scheduled task drains through `TryEnqueueSyncAsync`, or
- a scheduled task alone that sends `IOrderSyncService.GetSendableOrderIdsAsync(...)` with the filters that match the trigger (for example, paid only).

A re-send is safe: an order already confirmed is skipped, and one created but not confirmed is found by its origin, not created again.

## Open decisions and second-phase items

| Item | Who decides or does it | Status |
| --- | --- | --- |
| A9: how Odoo carries subsidy and store credit | The client's accountant | Open; the note carries the amounts until then |
| A14: the exchange fee service product in Odoo | Odoo admin, then its code on Configure | Not created; T18 waits on it |
| A dedicated Odoo API user for the integration | Odoo admin | The test instance uses a personal admin account (V5) |
| A17: one Odoo company or one per store | Client | One company today; revisit if production differs |
| B1: order-level discounts, gift cards, reward points, payment fee | Second phase | Not sent |
| B5: tax on exchange fees, tax-exempt customers | Second phase | Not handled |
| B6: returns and credit notes into Odoo | Second phase | Not handled |

Not in this version at all: editing an order already sent, reading anything back from Odoo (deliveries, invoices), sending the payment.

## Testing

The test plan is section 8 of the spec and the 2026-11-18 run is section 8.1: 20 cases pass, each checked on the Odoo order form as well. Four were not run:

| Case | What it needs |
| --- | --- |
| T17: a failed confirm keeps the Odoo id and Odoo's message | A change in Odoo between create and confirm (e.g. archive the delivery product) |
| T18: exchange fee lines | The exchange fee product (A14) and a completed exchange |
| T22b: cancel refused because the Odoo delivery is done | A delivery validated in Odoo |
| T24: the *Send orders to Odoo* permission without *Manage plugins* | A second admin account |

**Test data.** Two SQL scripts build orders on a local database. They are in [`TestData/`](TestData/) beside this file:

- [`Odoo_Order_Sync_TestOrders.sql`](TestData/Odoo_Order_Sync_TestOrders.sql): TEST-07 … TEST-22D, orders meant to be **sent** (plain, home address, no branch, unlinked product, packname with two destinations, POs, print name, subsidy settlement). Pressing *Sync* on them creates real sale orders on the Odoo test instance.
- [`Odoo_Order_Sync_Demo.sql`](TestData/Odoo_Order_Sync_Demo.sql): DEMO-01 … DEMO-13 in every sync state with made-up Odoo ids, for looking at the page only. *Sync* on a demo row does reach Odoo.

Both copy an existing order (`@SourceOrderId`, order 1). The test-order script also needs, in its `DECLARE` block at the top, ids that exist on your database: the ordering customer (`@CustomerId`), a customer in no branch (`@NoBranchCustomerId`), a branch whose top branch has the Odoo company id as card code (`@BranchId`) and that branch's address (`@BranchAddressId`). The orders use stores 1 to 3, and the lines use product ids from the `@Lines` table. The script links `@CustomerId` to the branch and switches PackName, Subsidy and Exchanges off on store 3 (T23); the clean-up deletes those three store-3 settings again. Set them before you run it, then build sending orders on a local development database only (`sqlcmd` needs `-I`: the CW tables have filtered indexes).

Each script starts with a CLEAN-UP block that removes what it made in nop; nothing removes what was sent to Odoo, so cancel those orders in Odoo. The test instance's address and access are not in the repository; ask for them.

Unit tests: `dotnet test Tests/CW.Tests -p:SolutionDir=<repo root> --filter "FullyQualifiedName~Odoo"` (41 cases).

## Traps

- **Origin is the duplicate guard.** `nop {number}`, plus `/ {destination key}` with several destinations. Changing its format makes every re-send create a second Odoo order.
- **Odoo replaces the order date on confirm.** The Odoo order shows the confirmation time, not nop's placement; nop's UTC date is in the note.
- **The totals check allows one cent** (`> 0.01m` in `SendDraftAsync`), decided 2026-11-18: 3 × 3.33 against 10.00 passes, 6 × 1.67 against 10.00 warns.
- **Locale keys are `Integration.Odoo.*`**, not `Plugins.Integration.Odoo.*`. A new or renamed key needs a DATA version bump in `plugin.json` and the plugin update to install.
- **A JS change needs the bundle cache emptied** (`Presentation/Nop.Web/wwwroot/bundles`, keep `Index.htm`) and a site restart; the minifier also renames local function names.
- **A field the plugin requests must exist in Odoo.** Odoo refuses the whole `search_read` on one unknown field.
- **The Orders page's buttons are disabled while a request runs**, and *Cancel in Odoo* asks first. An event-driven trigger bypasses both, so it needs its own guard against double sends (the queue's one-run limit is it today).
