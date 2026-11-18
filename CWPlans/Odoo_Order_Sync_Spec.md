# Integration.Odoo — sending nop orders to Odoo

**Status: applied 2026-11-17 (changes 1–54), then the Orders page reworked the same day (§4.2) and on 2026-11-18 a confirmation added to *Cancel in Odoo* and the order buttons disabled while a request runs (§8.1 T21, T22; version
`2026_11_18_DATA_1.4`); build clean, 1097 CW tests pass, plugin updated on the local site, and the Configure and Orders pages checked in the browser (T3–T5; the filters list the right orders, the row shows *Sync*). Run against Odoo
2026-11-18 (§8.1): 20 cases pass, checked in Odoo too; T17, T18,
T22b and T24 were not run; not approved.** Written 2026-11-13, reviewed and
corrected 2026-11-16. Section 4 records the decisions (A9 still open; discounts, fee tax and returns left to a second phase, §4.1), section 5 what
was checked on the client's Odoo 20 test instance, section 6 the design, section 7 the changes in apply order and
section 8 the test plan (`CLAUDE.md` §0.3, Tier 2).

The plugin today is one-way, Odoo → nop (`Plugins/Nop.Plugin.Integration.Odoo/References/Odoo_Plugin_Reference.md`).
This adds the first nop → Odoo direction: a placed nop order becomes an Odoo `sale.order`.

---

## 1. Odoo 20: is the plugin right for it?

**The API is right.** Odoo 20's external API is the JSON-2 API the plugin already uses: `POST /json/2/<model>/<method>`,
`Authorization: bearer <key>`, optional `X-Odoo-Database`, `User-Agent`. Odoo 20's documentation describes it unchanged
from 19.0 ("New in version 19.0"); what Odoo 20 removed is the legacy RPC `db` service, which the plugin does not use.
The models and fields the plugin reads were checked on the Odoo 20 test instance on 2026-10-28 (reference §4).

**The labels are wrong.** Several places still say Odoo 19. Section 7 corrects each one (changes 17, 19–21, 39, 40, 46,
47; the reference document when this spec is folded into it):

| Where | Text |
|---|---|
| `plugin.json` `Description` | "Connects NopCommerce to Odoo 19 ERP via the JSON-2 API…" |
| `Views/OdooAdmin/Configure.cshtml` lines 25, 50 | "Odoo 19 Settings", "Odoo 19 Connection" |
| `Services/IOdooApiService.cs` line 8, `Services/OdooApiService.cs` lines 11, 71, 148 | doc comments |
| `References/Odoo_Plugin_Reference.md` §1 | "A one-way sync from Odoo 19" (and "Nothing is written back to Odoo", which this feature changes) |
| `CLAUDE.md` §3.2 | "One-way Odoo 19 → nop sync" |

**Three points the order feature must respect, from Odoo 20's API documentation:**

1. **One call = one transaction.** JSON-2 cannot chain calls in one transaction. The order and its lines go in **one
   `sale.order/create` call** (lines as one2many commands `[0, 0, {…}]`). Confirming the order (`action_confirm`) is a
   second call that can fail on its own, so the sync must handle "created but not confirmed".
2. **No duplicates on retry.** A timeout after a successful `create` must not create a second order. Before creating,
   the sync looks the order up by a key that carries the nop order (its `origin`, decision A3).
3. **A dedicated bot user** with only the sales rights the sync needs is Odoo's recommendation for integrations; the API
   key lasts at most three months (reference §4) and Odoo 20 can rotate keys through `res.users.apikeys/generate`.

---

## 2. What the plugin already knows that the order needs

| Order needs | Already in nop | How it is linked to Odoo |
|---|---|---|
| Odoo product of each line | nop `Product` | `ManufacturerPartNumber = ODOO_{product.template id}` — the **template**, not the variant |
| Odoo variant of each line | `ProductAttributeCombination` | `ManufacturerPartNumber = ODOO_{product.product id}`; matched from the order item's `AttributesXml` |
| Odoo variant of a product **without** variants | — | **Not stored today.** A template with one variant has no combination; the product sync will store its `product.product` id in the generic attribute `Odoo.ProductVariantId` (decision A6, changes 10–13) |
| Store | nop `Store` | `Odoo_StoreMapping` → `x_store` id |
| Department | CW.Core `Department` | No id kept: matched by (store, name), as the department sync does |
| Company / branch | CW.Core `Branch` | `CardCode` = `res.partner` id; a head office is the company, a branch is its `delivery` address |

---

## 3. CW.Core data that shapes an order

These are CW.Core's additions to nop orders and products. Each was a candidate for the Odoo order; section 4 records
which ones go to Odoo and where.

| CW.Core data | Entity / service | What it holds | Bearing on the Odoo order |
|---|---|---|---|
| **Packname per line** | `PackNameOrderItem` (`CORE_PN_PackNameOrderItem`) | A snapshot at placement: packname, its customer, branch (id, code, name), department, cost centre | Who each line is for; the employee name, branch, department and cost centre an invoice or picking may need |
| **Destinations** | `OrderShipping` (`CORE_PN_OrderShipping`), `ICWOrderService.GetOrderShippingDestinationsAsync` | One row per packname: address or pickup, method, shipping incl. / excl. tax, status. Packnames sharing (address, method) form one destination | An Odoo `sale.order` has **one** `partner_shipping_id`. A packname order can have several destinations |
| **Settlement** | `OrderSettlement` (`CORE_PN_OrderSettlement`), `ICWOrderService.GetOrderSettlementAsync` | What subsidy (goods, shipping) and store credit paid, incl. tax; `Order.OrderTotal` is what the customer pays | The Odoo order total differs from the customer's payment by these amounts |
| **Subsidy per line** | `SubsidyOrderItem`, `SubsidyOrderShipping` | Which subsidy and product group each line consumed, by bucket | Subsidy reporting by employee / group, if Odoo needs it |
| **Exchange fees** | `ReturnCharge` (`CORE_RET_ReturnCharge`) and the fee ledger | Re-shipping, repackaging and additional fees on a return order | Extra charge lines |
| **Exchange return orders** | `OrderProcessingOverrideService.SaveReturnOrderDetailsAsync` | An order placed by the exchange workflow: paid, no payment method, its replacement items | Send to Odoo as a normal order, as a replacement, or not at all |
| **Print name** | `MultiStoreSettings.PrintNameProductAttributeId`, `PrintNamePrefillEventConsumer` | A text attribute on the line, pre-filled with the packname's print name (embroidery / print) | It does not create a variant, so it is not part of the `product.product`: it has to travel as line text or an Odoo custom value |
| **Attributes that are not variants** | nop attributes from Odoo attributes with `create_variant = no_variant`, and nop-only attributes | Chosen values on the line's `AttributesXml` | Odoo's `product_no_variant_attribute_value_ids` / custom values, or line text |
| **Customer / account codes** | `CustomerBranch` (`BranchId`, `JimCardCode`), `Branch.CardCode`, `Branch.PaymentTerms` | The ordering customer's branch; `JimCardCode` is a code from the old ERP | Which Odoo partner the order is for; `PaymentTerms` is stored and read by nothing today |
| **PO number** | `PackName.POEntry` (when `POEntryEnabled`) | A purchase-order reference per packname | `client_order_ref` is the usual Odoo field, but it is per order |
| **Packname comments** | `PackName.Comments` | Free text | Order note, if wanted |
| **Bulk ordering** | `Domain/BulkOrder` | Only how lines are entered | None: a bulk order is an ordinary order once placed |
| **Credit notes** | `CreditNote`, `CreditNoteHistory` | Store credit issued and spent | Spent credit is already in the settlement; issuing credit (returns) is not part of this feature |

Nothing CW.Core adds to **products** changes what Odoo needs: CW.Core extends products only through nop's own ACL,
store mappings and attributes (department ACL, catalogue gate, bulk-ordering settings), and those govern who sees a
product in nop, not what is sold.

---

## 4. Decisions (taken 2026-11-13)

| # | Question | Decision |
|---|---|---|
| A1 | **When** does an order go to Odoo? | **By hand, from a page of the plugin**, in batches: *Send selected* or *Send all*. Nothing is sent automatically when an order is placed |
| A2 | Quotation or confirmed? | **Confirmed**: `create`, then `action_confirm`. An order created but not confirmed is shown as such and confirmed on the next send |
| A3 | Duplicate guard | **`origin = "nop {order number}"`**, a core field. The sync searches by it before creating. `client_order_ref` is left for the PO number (A10) |
| A4 | Customer | **The ordering customer's head-office company** (`CardCode` of the top branch) as `partner_id` and `partner_invoice_id`. No branch, or a branch not linked to Odoo: the order is **held** and the reason shown |
| A5 | Several destinations | **One Odoo order per destination**, each with its own `partner_shipping_id`, all with the same nop order number in `origin` |
| A6 | Variant of a product without variants | **The product sync stores it**: the single variant's `product.product` id is saved on the product, so sending needs no extra lookup |
| A7 | Address that is not an Odoo partner | **Create a `delivery` contact** under the client company and remember its id against the nop address, so the next order reuses it |
| A8 | Prices | **nop's line prices, excl. tax**; Odoo adds its own GST. A line whose Odoo price-list price differs is logged (A15). A test order must confirm Odoo 20 keeps the price sent (§5 V3) |
| A9 | Subsidy and store credit | **Open: the client's accountant decides.** Until then the Odoo order carries the gross lines, and the subsidy (goods, shipping) and store credit amounts are written in the order note |
| A10 | PO number | **On the order** (`client_order_ref`) when every line of that Odoo order has the same PO; otherwise on each line's description |
| A11 | Packname, branch, department, cost centre | **Line description**, e.g. "For: J. Smith · Branch: Depot East · Dept: Sales · CC: 4100". Studio fields can follow if Odoo needs to report on them |
| A12 | Print name and attributes that are not variants | **Line description**, e.g. "Print name: J. Smith" |
| A13 | Shipping | **One delivery line per Odoo order** (`is_delivery`) on the existing *Standard delivery* product, at nop's shipping price for that destination, named after the nop method |
| A14 | Exchange return orders | **Sent, marked as a replacement** (in `origin` and the note), with the exchange fees as service lines. The fee products must be created in Odoo first |
| A15 | Totals that differ | **Log a warning** on the send page and in the Sync Log; never block |
| A16 | Later changes | **Cancel only**, done **from the page**: it lists sent orders that were cancelled in nop, and an admin cancels their Odoo orders (`action_cancel`). No edits; nothing is read back from Odoo |
| A17 | Odoo company | **One company**: the API user's default (*Cityweb* on the test instance). Revisit if production has one company per store |
| A18 | Payment | **Not sent.** The note says "Paid by card in nop" when the order was paid |
| A19 | Which orders the page lists | **Orders not yet sent, placed on or after a start date** set on Configure; filters by store, date and status |

**Still needed before the build:** the accountant's answer to A9; in Odoo, the service products for the exchange fees
(A14); a test order on the instance to confirm A8.

### 4.1 Review points (raised 2026-11-16, decided 2026-11-17)

The review fixed what it could in sections 6–8. These needed a decision.

| # | Point | Decision |
|---|---|---|
| B1 | **Order-level discounts are not sent.** A line's price carries its own discount, but the subtotal discount, order discount, gift cards, reward points and a payment-method fee are on the order. Odoo's total is higher than nop's by them; the totals warning does not see it, since it compares lines, shipping and fees | **Second phase.** This version sends nothing for them |
| B2 | **Whose company the order is for**: the ordering customer's head office, or the head office of each line's branch as recorded at placement | **The ordering customer's**, as section 7 has it. One nop order, one Odoo customer |
| B3 | **Unpaid orders are listed** with every other order that is not cancelled | **A payment-status column and filter** on the list to send; the admin decides what to send (in changes 23, 24, 29, 30, 33–35, 43) |
| B4 | **Sending needs *Manage plugins*** | **A permission of its own** for the Orders page, granted to Administrators by default; Settings and Sync Log keep *Manage plugins* (changes 33, 42, 52, 53) |
| B5 | **Tax on exchange fees, and tax-exempt customers.** nop adds exchange fees to the order total as flat amounts, with no tax worked out on them (`OrderTotalCalculationOverrideService.GetShoppingCartTotalAsync`), so an Odoo fee product that adds GST makes Odoo's fee 10% higher than nop's. A customer who is tax exempt in nop still gets Odoo's GST | **Second phase.** This version sends what it has: the fee amount as it is, and no special handling of exempt customers. The totals warning shows a fee Odoo taxed |
| B6 | **Returns do not reach Odoo.** A return or credit note in nop changes nothing in Odoo, so Odoo's stock and invoicing drift after a return | **Second phase** |
| B7 | **Odoo behaviour not yet seen** (first test order): the price sent is kept (T8); a delivery line is accepted without a carrier; Odoo replaces the order date on confirmation (the nop date is in the note either way) | Checked by the test plan |

### 4.2 Scope and the Orders page (decided 2026-11-17)

This version is the **basic initial sync**: an order is sent from the Orders page, by hand. **Another developer takes
over** the parts that follow it:

- **CW.Core's own data on the Odoo order.** What section 6.2 builds from CW.Core (packname destinations, the line
  text, PO numbers, the subsidy and store-credit note, exchange fees) **stays as built**; that developer reviews and
  extends it. A9, B1, B5 and B6 go with it.
- **What starts a sync** (an order placed, paid, cancelled): an event consumer that queues the order on
  `IOdooSyncQueue` with `SyncEventType.OrderSend` / `OrderCancel`, as the page's buttons do.

The Orders page was reworked accordingly, after changes 1–54 were applied (so section 7 still shows the page as it
first was; the code is the reference for the page):

| Before | Now |
|---|---|
| Lists orders not yet sent, placed on or after a start date set on Configure (A19) | **Every order, synced or not**, newest first, with a **sync column** (*Not sent*, *Sent*, *Held*, *Failed*, *In progress*, *To cancel in Odoo*, *Cancelled in Odoo*, *Not sent (cancelled)*). A deleted order is listed only while an Odoo order of it is still open. The start date, its setting and its text are removed (the DATA 1.2 update deletes them) |
| Filter by Odoo status of the mapping rows | **Filter by sync state**: all, not sent, sent, held / failed / in progress, to cancel in Odoo; plus store, payment status and dates as before |
| *Send selected*, *Send all listed* | **A *Sync* button beside the sync status** (one column) on each row a send would act on, and the batch buttons *Sync selected* and *Sync all listed*. *Sync all listed* queues only the listed orders that need sending (not those already sent, cancelled or deleted), on every page (`IOrderSyncService.GetSendableOrderIdsAsync`) |
| A second grid for cancelled orders, with *Cancel all in Odoo* | **One table**: a cancelled or deleted order still open in Odoo shows *Cancel in Odoo* beside its status. No batch cancel |

The *Odoo sync* column shows **either the row's button or its status as text**: *Sync* (not sent), *Failed: sync again*, *Held: sync again*, *Finish sync* (in progress), *Cancel in Odoo* (to cancel), and otherwise the status alone (*Sent*, *Cancelled in Odoo*, *Not sent (cancelled)*); the reason of a failure or hold stays in the Message column.

**Locale keys** moved to the prefix `Integration.Odoo.*` the same day (version `2026_11_17_DATA_1.3`): every key in section 7 written `Plugins.Integration.Odoo.*` is now `Integration.Odoo.*`, and the update deletes the rows left under the old prefix.

**Demo data**: `CWPlans/TestData/Odoo_Order_Sync_Demo.sql` adds 13 orders (DEMO-01 … DEMO-13), copies of one existing order, in every order, payment and sync state, with made-up Odoo ids (900001 and up) and no call to Odoo; it starts by deleting the demo orders it made before, and that block alone is the clean-up. *Sync* on a demo row that is not sent does reach Odoo. The demo data showed that the *Not sent* and *Sent* filters also listed cancelled or deleted orders; they now list live orders only, like the column.

What a row says and which button it offers is `OdooOrderSyncSummary` (pure, unit-tested in
`Tests/CW.Tests/Integration.Odoo/OdooOrderSyncSummaryTests.cs`).

---

## 5. Checked on the Odoo 20 test instance (2026-11-13)

Read only: the instance's `/doc` field lists and the web client's own reads. Nothing was created or changed in Odoo.

| # | What was checked | Result | Bearing |
|---|---|---|---|
| V1 | `sale.order` fields | `partner_id`, `partner_invoice_id` and `partner_shipping_id` are **required**; so are `company_id` and `date_order`. `client_order_ref`, `origin`, `note` (html), `pricelist_id`, `payment_term_id`, `carrier_id`, `warehouse_id`, `fiscal_position_id` exist. Methods `create`, `action_confirm`, `action_cancel`, `action_draft` are exposed | The create sends all three partners |
| V1 | `sale.order.line` fields | `product_id`, `product_uom_qty`, `price_unit` (required), `discount`, `name` (text), `sequence`, `display_type`, `is_delivery`, **`tax_ids`** (`tax_id` is gone), **`product_uom_id`** (`product_uom` is gone), `product_no_variant_attribute_value_ids`, `product_custom_attribute_value_ids`, `analytic_distribution`, `technical_price_unit`. No `route_id` | Field names to use on Odoo 20 |
| V2 | Studio fields | **None** on `sale.order` or `sale.order.line`. `res.partner` has `x_studio_estore` and `x_studio_inherited_estore` (→ `x_store`); `product.template` has `x_studio_stores`, `x_studio_departments`, `x_studio_sync_revision`, `x_studio_style_code`, `x_studio_contract_version` | Any CW field on the order (A3, A11) must be added in Studio first |
| V3 | Prices | Every client company has **its own price list** (named "{client} - Price list"), set on the company partner and inherited by its delivery addresses. All in AUD. Odoo 20 keeps a hand-set `price_unit` apart from the price-list price (`technical_price_unit`); whether a `price_unit` sent on create survives was **not tested** (it needs an order created) | Decision A8: nop's prices and the client's Odoo price list can disagree |
| V4 | Delivery and service products | One carrier, *Standard delivery* (fixed, $0, product `[Delivery_007]`). No service product for subsidy, store credit or exchange fees | Decisions A9, A13, A14 need products created in Odoo first |
| V4 | Taxes | Sales taxes include *10% GST* (excluded from price) and the 0% ones | nop's prices are sent excl. tax; the GST comes from Odoo |
| V5 | API user | The key the plugin uses belongs to a **personal administrator account** (Sales manager, Settings, Contacts, Inventory). It can do everything the feature needs | Before go-live, a bot user with Sales and Contacts rights only (§1.3) |
| V6 | Apps | *Sales* (with *Delivery*) and *Inventory* are installed; one company, *Cityweb*, AUD | A17: one Odoo company for every store |
| — | Existing orders | Two draft orders made by hand. One is on the company partner, with a branch's delivery address as `partner_shipping_id` | Confirms the suggested shape of A4 |

---

## 6. Design

### 6.1 What is built

| Part | What it does |
|---|---|
| **Orders page** (`OdooAdmin/Orders`, menu *Odoo › Orders*) | Two grids. **To send**: orders placed on or after the start date, not cancelled, not yet confirmed in Odoo; filters store, placed from / to, Odoo status, payment status; *Send selected* and *Send all listed*. The page has its own permission (*Send orders to Odoo*), apart from *Manage plugins*. **To cancel**: orders cancelled or deleted in nop whose Odoo orders are still open, each with *Cancel in Odoo*, plus *Cancel all*. Each row shows its Odoo order(s), status and message |
| **Background run** | Both actions queue a request on the existing `IOdooSyncQueue` (new types `OrderSend`, `OrderCancel`, carrying the order ids), so the page never waits on Odoo. Each run is an event in the Sync Log, with a trace per order |
| **`Odoo_OrderMapping`** | One row per Odoo order of a nop order: nop order id, destination key, Odoo id and name, status (`Pending`, `Created`, `Confirmed`, `Held`, `Failed`, `Cancelled`), message. Unique on (order, destination) |
| **`IOrderSyncService` / `OrderSyncService`** | Builds the Odoo orders from the nop order through CW.Core's services, sends, confirms and cancels |
| **`OdooOrderBuilder`** (internal, pure) | Origin, line text, PO rule, note, the `create` values. Unit-tested |
| **API** (`IOdooApiService`) | `sale.order`: find by origin, read, create, confirm, cancel. `res.partner`: find a delivery address under a company, create a delivery contact. `res.country` / `res.country.state` by code. `product.product` by internal reference |
| **Product sync** | Stores the `product.product` id of a template with one variant (generic attribute `Odoo.ProductVariantId`), also on products skipped as unchanged, so no full resync is needed |
| **Configure page** | Start date; internal references of the Odoo delivery product and exchange-fee product; the Odoo 19 labels become Odoo 20 |

### 6.2 How one order is sent

1. **Destinations.** A packname order: one per CW.Core destination (`ICWOrderService.GetOrderShippingDestinationsAsync`),
   its shipping the sum of its packnames' `OrderShipping.ShippingExclTax`. A plain order: one, key 0, from the order's
   own address, method and `OrderShippingExclTax`. An order line that belongs to no destination holds the whole order:
   it would otherwise be left out without a word.
2. **Customer** (A4): the ordering customer's branch (`IBranchService.GetBranchByCustomerIdAsync`), walked up to the
   top branch; its `CardCode` is the Odoo company id. No branch, or a `CardCode` that is not a number: every
   destination is **held**.
3. **Lines.** Each order item: its Odoo variant (the combination's `ODOO_{id}`, else the product's
   `Odoo.ProductVariantId`); quantity; unit price = the line's `PriceExclTax` ÷ quantity, to cents; text = product
   name, nop's attribute description (so the print name and every attribute that is not a variant), the packname line
   (`For / Branch / Dept / CC`, from the `PackNameOrderItem` snapshot) and the PO when it is not on the order. An item
   whose product is not linked to an Odoo variant holds that destination.
4. **Shipping** (A13): one delivery line (`is_delivery`) on the configured delivery product, named after the nop method,
   unless the destination is a pickup. Without a delivery product set, a destination that charges shipping is held.
5. **First destination only:** the exchange fees (A14, `IExchangeService.GetExchangeFeesAmountsAsync(order)`, one line
   per fee type on the configured fee product) and the order notes: nop number, "Exchange replacement order", subsidy
   and store credit amounts from the settlement row (A9, for now), "Paid in nop" (A18). Every other destination's note
   points to the first one.
6. **Send.** A mapping row is written for every destination first (`Pending`), so a run that stops part-way leaves
   the order listed. Then each destination that is not already confirmed: if the mapping has no Odoo id, look the
   order up by `origin`; if Odoo has none, resolve the delivery partner (step 7), **create** it with its lines in one
   call, and save the Odoo id at once; then read it, **confirm** it if it is a quotation and read it again: it is
   `Confirmed` only when Odoo says its state is *sale*. Odoo's untaxed total is compared with what nop charged for the
   destination (its lines at their own totals, shipping, fees), and a difference is kept as a warning (A15). An Odoo
   error sets `Failed` with Odoo's message, keeping the Odoo id, so the next send confirms rather than creates.
   A cancel is checked the same way: `Cancelled` only when Odoo's state is *cancel* (or the order is gone from Odoo).
7. **Delivery partner** (A7): none for a pickup (the company). Otherwise search `res.partner` for the company itself or
   one of its contacts with the same street, postcode and city (case ignored); create a `delivery` contact under the
   company when none matches.

### 6.3 Where the build departs from section 4, and why

| # | Decision | Built as | Why |
|---|---|---|---|
| A3 | `origin = "nop {order number}"` | Same, with `" / {destination key}"` added when the order has several destinations | Each Odoo order needs its own key, or a retry of the second destination would find the first |
| A7 | "Remember its id against the nop address" | **Matched in Odoo** by street, postcode and city instead | nop copies the address onto each order (CW's checkout does too), so an order's address id is new every time and a nop-side memory would never be hit. The match also finds the branch delivery addresses the branch sync brought from Odoo, so a branch order uses the existing contact |
| A10 | PO number per packname | Read from the packname **when the order is sent** | `PackNameOrderItem` keeps no PO snapshot: a PO changed between placing and sending is sent as it now is |

### 6.4 Not in this version

Automatic sending; editing an order already sent; reading anything back from Odoo (deliveries, invoices); the payment;
a Studio field for packname data; per-store Odoo companies. Subsidy and store credit as Odoo lines wait for A9.

---

## 7. Exact changes

All paths are under `Plugins/Nop.Plugin.Integration.Odoo/` unless they start with `Tests/` or are `CLAUDE.md`. Changes
to one file are listed **bottom to top**; line numbers are those of the file as it stands when the change is reached.
New files are given whole.

### 7.1 Domain, data and settings

**1. New file `Domain/OdooOrderStatus.cs`**

```csharp
namespace Nop.Plugin.Integration.Odoo.Domain;

/// <summary>
/// Where one Odoo sale order made from a nop order stands.
/// </summary>
public enum OdooOrderStatus
{
    /// <summary>Created in Odoo, not yet confirmed: the next send confirms it.</summary>
    Created = 1,

    /// <summary>Created and confirmed in Odoo. Never sent again.</summary>
    Confirmed = 2,

    /// <summary>Not sent: something the order needs is missing in nop or in the plugin's settings. The message says what.</summary>
    Held = 3,

    /// <summary>Odoo refused a call. The message is Odoo's; the next send tries again.</summary>
    Failed = 4,

    /// <summary>Cancelled in Odoo after the nop order was cancelled or deleted.</summary>
    Cancelled = 5,

    /// <summary>Recorded before the send reached it: a run that stops part-way leaves the destination listed to send.</summary>
    Pending = 6
}
```

**2. New file `Domain/OdooOrderMapping.cs`**

```csharp
using Nop.Core;

namespace Nop.Plugin.Integration.Odoo.Domain;

/// <summary>
/// One Odoo sale order made from a nop order: one row per destination of the order (a plain order has one,
/// destination key 0). The row is the nop-side guard against sending an order twice; the Odoo order's
/// <c>origin</c> is the Odoo-side one.
/// </summary>
public class OdooOrderMapping : BaseEntity
{
    /// <summary>
    /// Gets or sets the nop order id.
    /// </summary>
    public int NopOrderId { get; set; }

    /// <summary>
    /// Gets or sets the destination: CW.Core's destination key (the lowest packname id sharing the address and
    /// method) on a packname order, 0 on a plain order.
    /// </summary>
    public int DestinationKey { get; set; }

    /// <summary>
    /// Gets or sets the Odoo sale.order id, once created.
    /// </summary>
    public int? OdooOrderId { get; set; }

    /// <summary>
    /// Gets or sets the Odoo order reference (e.g. S00042), once read back.
    /// </summary>
    public string OdooOrderName { get; set; }

    /// <summary>
    /// Gets or sets the status identifier.
    /// </summary>
    public int StatusId { get; set; }

    /// <summary>
    /// Gets or sets why the order is held or failed, or the warnings of the send that confirmed it.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC).
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the last update (UTC).
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public OdooOrderStatus Status
    {
        get => (OdooOrderStatus)StatusId;
        set => StatusId = (int)value;
    }
}
```

**3. New file `Data/Mapping/OdooOrderMappingBuilder.cs`**

```csharp
using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Data.Mapping;

public class OdooOrderMappingBuilder : NopEntityBuilder<OdooOrderMapping>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(OdooOrderMapping.NopOrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(OdooOrderMapping.DestinationKey)).AsInt32().NotNullable()
            .WithColumn(nameof(OdooOrderMapping.OdooOrderId)).AsInt32().Nullable()
            .WithColumn(nameof(OdooOrderMapping.OdooOrderName)).AsString(64).Nullable()
            .WithColumn(nameof(OdooOrderMapping.StatusId)).AsInt32().NotNullable()
            .WithColumn(nameof(OdooOrderMapping.Message)).AsString(int.MaxValue).Nullable();
    }
}
```

**4. New file `Data/Migrations/OrderMappingMigration.cs`**

```csharp
using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Data.Migrations;

/// <summary>
/// Adds <c>Odoo_OrderMapping</c>, the record of the Odoo sale orders made from nop orders. Runs on install and on
/// update (<see cref="MigrationProcessType.NoMatter"/>) and creates nothing that already exists.
/// </summary>
[NopMigration(MigrationVersions.AddOrderMapping, "Integration.Odoo order mapping", MigrationProcessType.NoMatter)]
public class OrderMappingMigration : Migration
{
    /// <summary>
    /// Apply schema changes.
    /// </summary>
    public override void Up()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(OdooOrderMapping));
        if (Schema.Table(table).Exists())
            return;

        this.CreateTableIfNotExists<OdooOrderMapping>();

        // One row per (order, destination): a send updates it, never adds a second.
        Create.Index($"IX_{table}_{nameof(OdooOrderMapping.NopOrderId)}_{nameof(OdooOrderMapping.DestinationKey)}")
            .OnTable(table)
            .OnColumn(nameof(OdooOrderMapping.NopOrderId)).Ascending()
            .OnColumn(nameof(OdooOrderMapping.DestinationKey)).Ascending()
            .WithOptions().Unique();
    }

    /// <summary>
    /// Revert schema changes.
    /// </summary>
    public override void Down()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(OdooOrderMapping));
        if (Schema.Table(table).Exists())
            Delete.Table(table);
    }
}
```

**5. `Data/MigrationVersions.cs`, lines 12–16**

FIND (lines 12–16):
```csharp
    /// <summary>
    /// Initial schema: <c>Odoo_Log</c>, <c>Odoo_LogEvent</c> and <c>Odoo_StoreMapping</c> with their indexes.
    /// </summary>
    public const string InitialSchema = "2026/10/26 00:00:00";
}
```
REPLACE WITH:
```csharp
    /// <summary>
    /// Initial schema: <c>Odoo_Log</c>, <c>Odoo_LogEvent</c> and <c>Odoo_StoreMapping</c> with their indexes.
    /// </summary>
    public const string InitialSchema = "2026/10/26 00:00:00";

    /// <summary>
    /// <c>Odoo_OrderMapping</c>: the Odoo sale orders made from nop orders.
    /// </summary>
    public const string AddOrderMapping = "2026/11/13 00:00:00";
}
```

**6. `Data/DataNameCompatibility.cs`, line 16**

FIND (line 16):
```csharp
        { typeof(OdooStoreMapping), "Odoo_StoreMapping" },
```
REPLACE WITH:
```csharp
        { typeof(OdooStoreMapping), "Odoo_StoreMapping" },
        { typeof(OdooOrderMapping), "Odoo_OrderMapping" },
```

**7. `Domain/SyncEventType.cs`, lines 10–12** (the file has no final line break)

FIND (lines 10–12):
```csharp
    Store = 2,
    Department = 3
}
```
REPLACE WITH:
```csharp
    Store = 2,
    Department = 3,

    /// <summary>
    /// Sends nop orders to Odoo as sale orders.
    /// </summary>
    OrderSend = 4,

    /// <summary>
    /// Cancels in Odoo the sale orders of nop orders cancelled after they were sent.
    /// </summary>
    OrderCancel = 5
}
```

**8. `Domain/OdooSettings.cs`, lines 36–38**

FIND (lines 36–38):
```csharp
    public DateTime? LastSyncDateTime { get; set; }

}
```
REPLACE WITH:
```csharp
    public DateTime? LastSyncDateTime { get; set; }

    /// <summary>
    /// Orders placed on or after this date (UTC) are listed to send on the Orders page; null lists none.
    /// </summary>
    public DateTime? OrderSyncStartDateUtc { get; set; }

    /// <summary>
    /// Internal reference (<c>default_code</c>) of the Odoo product used for the shipping line of a sale order.
    /// </summary>
    public string DeliveryProductCode { get; set; } = string.Empty;

    /// <summary>
    /// Internal reference (<c>default_code</c>) of the Odoo service product used for exchange fee lines.
    /// </summary>
    public string ExchangeFeeProductCode { get; set; } = string.Empty;
}
```

**9. `OdooConstants.cs`, lines 23–25**

FIND (lines 23–25):
```csharp
    /// <summary>Admin menu item of the Sync Log page.</summary>
    public const string SyncLogsMenuSystemName = "Odoo.Integration.SyncLogs";
}
```
REPLACE WITH:
```csharp
    /// <summary>Admin menu item of the Sync Log page.</summary>
    public const string SyncLogsMenuSystemName = "Odoo.Integration.SyncLogs";

    /// <summary>Admin menu item of the Orders page.</summary>
    public const string OrdersMenuSystemName = "Odoo.Integration.Orders";

    /// <summary>
    /// Prefix of the Odoo link kept in <c>ManufacturerPartNumber</c>: <c>ODOO_{template id}</c> on a product,
    /// <c>ODOO_{product.product id}</c> on a combination.
    /// </summary>
    public const string OdooLinkPrefix = "ODOO_";

    /// <summary>
    /// Generic attribute on a product whose Odoo template has exactly one variant: that variant's
    /// <c>product.product</c> id, which an order line needs. A product with variants keeps the id on each
    /// combination instead.
    /// </summary>
    public const string ProductVariantIdAttribute = "Odoo.ProductVariantId";
}
```

### 7.2 Product sync: the single variant's id

**10. `Services/ProductSyncService.cs`, lines 544–549**

FIND (lines 544–549):
```csharp
    /// <summary>
    /// Records the product's change marker as a generic attribute, so an unchanged product can be
    /// skipped on the next sync.
    /// </summary>
    private Task SaveProductSyncMarkerAsync(Product product, OdooProductResponse odooProduct) =>
        _genericAttributeService.SaveAttributeAsync(product, OdooProductSyncMarkerAttribute, GetSyncMarker(odooProduct));
```
REPLACE WITH:
```csharp
    /// <summary>
    /// Records the product's change marker as a generic attribute, so an unchanged product can be
    /// skipped on the next sync.
    /// </summary>
    private Task SaveProductSyncMarkerAsync(Product product, OdooProductResponse odooProduct) =>
        _genericAttributeService.SaveAttributeAsync(product, OdooProductSyncMarkerAttribute, GetSyncMarker(odooProduct));

    /// <summary>
    /// Records the Odoo variant of a template with exactly one variant, which an order line needs: such a
    /// product has no combination to carry it. Zero when the template has several variants, whose
    /// combinations carry their own. Written only when it changed.
    /// </summary>
    private async Task SaveSingleVariantIdAsync(Product product, OdooProductResponse odooProduct)
    {
        var variantId = odooProduct.ProductVariantIds?.Count == 1 ? odooProduct.ProductVariantIds[0] : 0;
        var stored = await _genericAttributeService.GetAttributeAsync<int>(product, OdooConstants.ProductVariantIdAttribute);

        if (stored != variantId)
            await _genericAttributeService.SaveAttributeAsync(product, OdooConstants.ProductVariantIdAttribute, variantId);
    }
```

**11. `Services/ProductSyncService.cs`, line 336**

FIND (line 336):
```csharp
            await _genericAttributeService.SaveAttributeAsync(product, OdooImageFingerprintAttribute, GetImageFingerprint(odooProduct));
```
REPLACE WITH:
```csharp
            await _genericAttributeService.SaveAttributeAsync(product, OdooImageFingerprintAttribute, GetImageFingerprint(odooProduct));
            await SaveSingleVariantIdAsync(product, odooProduct);
```

**12. `Services/ProductSyncService.cs`, line 313**

FIND (line 313):
```csharp
                await UpdateExistingProduct(logEventId, existingProduct, odooProduct);
```
REPLACE WITH:
```csharp
                await UpdateExistingProduct(logEventId, existingProduct, odooProduct);
                await SaveSingleVariantIdAsync(existingProduct, odooProduct);
```

**13. `Services/ProductSyncService.cs`, lines 306–308**

FIND (lines 306–308):
```csharp
                    await _odooLog.InformationAsync($"Product SKU {odooSku} unchanged ({currentMarker}). Skipped.",
                        logEventId, entityType: "Product", odooEntityId: odooProduct.Id, nopEntityId: existingProduct.Id);
                    return SyncOutcome.Skipped;
```
REPLACE WITH:
```csharp
                    // A product synced before the variant id was recorded gets it without a full resync.
                    await SaveSingleVariantIdAsync(existingProduct, odooProduct);
                    await _odooLog.InformationAsync($"Product SKU {odooSku} unchanged ({currentMarker}). Skipped.",
                        logEventId, entityType: "Product", odooEntityId: odooProduct.Id, nopEntityId: existingProduct.Id);
                    return SyncOutcome.Skipped;
```

**14. `Services/ProductSyncService.cs`, lines 44–48**

FIND (lines 44–48):
```csharp
    /// <summary>
    /// Prefix of the Odoo link kept in <c>ManufacturerPartNumber</c>: <c>ODOO_{template id}</c> on a product,
    /// <c>ODOO_{product.product id}</c> on a combination. It is the match key, ahead of SKU.
    /// </summary>
    private const string OdooLinkPrefix = "ODOO_";
```
REPLACE WITH:
```csharp
    /// <summary>
    /// Prefix of the Odoo link kept in <c>ManufacturerPartNumber</c>: <c>ODOO_{template id}</c> on a product,
    /// <c>ODOO_{product.product id}</c> on a combination. It is the match key, ahead of SKU.
    /// </summary>
    private const string OdooLinkPrefix = OdooConstants.OdooLinkPrefix;
```

### 7.3 Odoo API

**15. New file `Models/Responses/OdooSaleOrderResponse.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// The fields of an Odoo <c>sale.order</c> the order send reads back.
/// </summary>
public class OdooSaleOrderResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// The order reference, e.g. S00042.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Name { get; set; }

    /// <summary>
    /// draft, sent, sale or cancel.
    /// </summary>
    [JsonPropertyName("state")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string State { get; set; }

    /// <summary>
    /// The order total before tax.
    /// </summary>
    [JsonPropertyName("amount_untaxed")]
    public decimal AmountUntaxed { get; set; }
}
```

**16. `Services/IOdooApiService.cs`, lines 62–69**

FIND (lines 62–69):
```csharp
    /// <summary>
    /// Retrieves the given res.partner records (including archived ones), e.g. a store's companies,
    /// which map to nopCommerce head-office Branches.
    /// </summary>
    /// <param name="partnerIds">The Odoo res.partner IDs.</param>
    /// <returns>The partners that exist; ids that no longer exist in Odoo are simply absent.</returns>
    Task<List<OdooPartnerResponse>> GetPartnersByIdsAsync(IList<int> partnerIds);
}
```
REPLACE WITH:
```csharp
    /// <summary>
    /// Retrieves the given res.partner records (including archived ones), e.g. a store's companies,
    /// which map to nopCommerce head-office Branches.
    /// </summary>
    /// <param name="partnerIds">The Odoo res.partner IDs.</param>
    /// <returns>The partners that exist; ids that no longer exist in Odoo are simply absent.</returns>
    Task<List<OdooPartnerResponse>> GetPartnersByIdsAsync(IList<int> partnerIds);

    // ── Sale order endpoints ─────────────────────────────────────────────────

    /// <summary>
    /// The product.product id of the active product with the given internal reference (default_code), or null.
    /// </summary>
    Task<int?> GetProductVariantIdByCodeAsync(string defaultCode);

    /// <summary>
    /// The sale order with the given origin that is not cancelled, or null.
    /// </summary>
    Task<OdooSaleOrderResponse> FindSaleOrderByOriginAsync(string origin);

    /// <summary>
    /// The sale order with the given id, or null when it no longer exists.
    /// </summary>
    Task<OdooSaleOrderResponse> GetSaleOrderAsync(int saleOrderId);

    /// <summary>
    /// Creates a sale order with its lines in one call (one transaction in Odoo).
    /// </summary>
    /// <param name="values">The sale.order values, lines included as one2many create commands.</param>
    /// <returns>The new sale.order id.</returns>
    Task<int> CreateSaleOrderAsync(IDictionary<string, object> values);

    /// <summary>
    /// Confirms a quotation (action_confirm).
    /// </summary>
    Task ConfirmSaleOrderAsync(int saleOrderId);

    /// <summary>
    /// Cancels a sale order (action_cancel), without Odoo's confirmation wizard.
    /// </summary>
    Task CancelSaleOrderAsync(int saleOrderId);

    /// <summary>
    /// The company itself or one of its contacts at the given street, postcode and city (case ignored; an empty
    /// value matches an empty field), or null.
    /// </summary>
    Task<int?> FindDeliveryPartnerAsync(int companyPartnerId, string street, string zip, string city);

    /// <summary>
    /// Creates a res.partner.
    /// </summary>
    /// <returns>The new res.partner id.</returns>
    Task<int> CreatePartnerAsync(IDictionary<string, object> values);

    /// <summary>
    /// The res.country id of a two-letter ISO code, or null.
    /// </summary>
    Task<int?> GetCountryIdByCodeAsync(string twoLetterIsoCode);

    /// <summary>
    /// The res.country.state id of a state code within a country, or null.
    /// </summary>
    Task<int?> GetStateIdByCodeAsync(int countryId, string code);
}
```

**17. `Services/IOdooApiService.cs`, line 8**

FIND (line 8):
```csharp
/// Contract for all Odoo 19 JSON-2 API operations.
```
REPLACE WITH:
```csharp
/// Contract for all Odoo JSON-2 API operations (Odoo 19 and later; the client runs Odoo 20).
```

**18. `Services/OdooApiService.cs`, lines 671–674**

FIND (lines 671–674):
```csharp
    #endregion


    #region Connection
```
REPLACE WITH:
```csharp
    #endregion

    #region Sale order endpoints

    /// <summary>
    /// The sale.order fields the order send reads back.
    /// </summary>
    private static readonly string[] SaleOrderFields = { "id", "name", "state", "amount_untaxed" };

    /// <summary>
    /// The id of the first record of a <c>search_read</c> that asked for <c>id</c> only, or null.
    /// </summary>
    private static int? FirstId(List<JsonElement> records) =>
        records?.Count > 0 ? records[0].GetProperty("id").GetInt32() : null;

    /// <inheritdoc/>
    public async Task<int?> GetProductVariantIdByCodeAsync(string defaultCode)
    {
        var records = await CallAsync<List<JsonElement>>("product.product", "search_read", new
        {
            domain = new object[] { new object[] { "default_code", "=", defaultCode } },
            fields = new[] { "id" },
            limit = 1
        });

        return FirstId(records);
    }

    /// <inheritdoc/>
    public async Task<OdooSaleOrderResponse> FindSaleOrderByOriginAsync(string origin)
    {
        var orders = await CallAsync<List<OdooSaleOrderResponse>>("sale.order", "search_read", new
        {
            domain = new object[]
            {
                new object[] { "origin", "=", origin },
                new object[] { "state", "!=", "cancel" }
            },
            fields = SaleOrderFields,
            limit = 1,
            order = "id"
        });

        return orders?.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<OdooSaleOrderResponse> GetSaleOrderAsync(int saleOrderId)
    {
        var orders = await CallAsync<List<OdooSaleOrderResponse>>("sale.order", "search_read", new
        {
            domain = new object[] { new object[] { "id", "=", saleOrderId } },
            fields = SaleOrderFields,
            limit = 1
        });

        return orders?.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<int> CreateSaleOrderAsync(IDictionary<string, object> values)
    {
        var ids = await CallAsync<List<int>>("sale.order", "create", new { vals_list = new[] { values } });

        return ids?.Count > 0 ? ids[0] : throw new OdooApiException("Odoo created no sale order and gave no reason.");
    }

    /// <inheritdoc/>
    public Task ConfirmSaleOrderAsync(int saleOrderId) =>
        CallAsync<JsonElement>("sale.order", "action_confirm", new { ids = new[] { saleOrderId } });

    /// <inheritdoc/>
    public Task CancelSaleOrderAsync(int saleOrderId) =>
        // disable_cancel_warning: cancel at once rather than answer with the confirmation wizard
        CallAsync<JsonElement>("sale.order", "action_cancel", new
        {
            ids = new[] { saleOrderId },
            context = new { disable_cancel_warning = true }
        });

    /// <inheritdoc/>
    public async Task<int?> FindDeliveryPartnerAsync(int companyPartnerId, string street, string zip, string city)
    {
        var domain = new List<object>
        {
            "|",
            new object[] { "id", "=", companyPartnerId },
            new object[] { "parent_id", "=", companyPartnerId },
            Condition("street", street),
            Condition("zip", zip),
            Condition("city", city)
        };

        var records = await CallAsync<List<JsonElement>>("res.partner", "search_read", new
        {
            domain,
            fields = new[] { "id" },
            limit = 1,
            order = "id"
        });

        return FirstId(records);

        // An empty nop field matches an empty Odoo field; text matches whatever its case.
        static object[] Condition(string field, string value) => string.IsNullOrWhiteSpace(value)
            ? new object[] { field, "=", false }
            : new object[] { field, "=ilike", value.Trim() };
    }

    /// <inheritdoc/>
    public async Task<int> CreatePartnerAsync(IDictionary<string, object> values)
    {
        var ids = await CallAsync<List<int>>("res.partner", "create", new { vals_list = new[] { values } });

        return ids?.Count > 0 ? ids[0] : throw new OdooApiException("Odoo created no contact and gave no reason.");
    }

    /// <inheritdoc/>
    public async Task<int?> GetCountryIdByCodeAsync(string twoLetterIsoCode)
    {
        var records = await CallAsync<List<JsonElement>>("res.country", "search_read", new
        {
            domain = new object[] { new object[] { "code", "=", twoLetterIsoCode.ToUpperInvariant() } },
            fields = new[] { "id" },
            limit = 1
        });

        return FirstId(records);
    }

    /// <inheritdoc/>
    public async Task<int?> GetStateIdByCodeAsync(int countryId, string code)
    {
        var records = await CallAsync<List<JsonElement>>("res.country.state", "search_read", new
        {
            domain = new object[]
            {
                new object[] { "country_id", "=", countryId },
                new object[] { "code", "=ilike", code }
            },
            fields = new[] { "id" },
            limit = 1
        });

        return FirstId(records);
    }

    #endregion

    #region Connection
```

**19. `Services/OdooApiService.cs`, line 148**

FIND (line 148):
```csharp
        // Odoo 19 JSON-2 authentication: bearer token
```
REPLACE WITH:
```csharp
        // JSON-2 authentication (Odoo 19 and later): bearer token
```

**20. `Services/OdooApiService.cs`, line 71**

FIND (line 71):
```csharp
        // {"version_info": [19, 0, 0, "final", 0, ""], "version": "19.0"}
```
REPLACE WITH:
```csharp
        // {"version_info": [20, 0, 0, "final", 0, ""], "version": "20.0"}
```

**21. `Services/OdooApiService.cs`, line 11**

FIND (line 11):
```csharp
/// Implements communication with the Odoo 19 JSON-2 REST API.
```
REPLACE WITH:
```csharp
/// Implements communication with Odoo's JSON-2 API (Odoo 19 and later; the client runs Odoo 20).
```

### 7.4 The order send

**22. New file `Services/OdooSaleOrderDraft.cs`**

```csharp
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// One line of an Odoo sale order, as the send builds it.
/// </summary>
public class OdooSaleOrderLineDraft
{
    /// <summary>The Odoo product.product id.</summary>
    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The unit price excl. tax; Odoo adds its own taxes.</summary>
    public decimal PriceUnit { get; set; }

    /// <summary>The line's description.</summary>
    public string Name { get; set; }

    /// <summary>Whether this is the shipping line.</summary>
    public bool IsDelivery { get; set; }
}

/// <summary>
/// One Odoo sale order, built from one destination of a nop order.
/// </summary>
public class OdooSaleOrderDraft
{
    /// <summary>The destination key (0 on a plain order).</summary>
    public int DestinationKey { get; set; }

    /// <summary>The Odoo order's origin, which finds it again on a retry.</summary>
    public string Origin { get; set; }

    /// <summary>The Odoo company: customer and invoice address.</summary>
    public int CustomerPartnerId { get; set; }

    /// <summary>The Odoo delivery address, resolved just before the order is created.</summary>
    public int ShippingPartnerId { get; set; }

    /// <summary>The nop address the destination ships to; null for a pickup.</summary>
    public int? ShippingAddressId { get; set; }

    public DateTime OrderDateUtc { get; set; }

    /// <summary>The PO number, when every line of the order has the same one.</summary>
    public string ClientOrderRef { get; set; }

    public IList<string> NoteLines { get; } = new List<string>();

    public IList<OdooSaleOrderLineDraft> Lines { get; } = new List<OdooSaleOrderLineDraft>();

    /// <summary>
    /// What nop charged for this destination before tax: its lines at their own totals, its shipping and its
    /// fees. Odoo's total is compared with it.
    /// </summary>
    public decimal NopUntaxed { get; set; }

    /// <summary>Why the order cannot be sent; null when it can.</summary>
    public string HoldReason { get; set; }
}

/// <summary>
/// Turns a draft into what Odoo's JSON-2 API takes, and composes its texts. Pure: no nop or Odoo calls.
/// </summary>
internal static class OdooOrderBuilder
{
    private static readonly Regex _lineBreak = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _tag = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>
    /// The Odoo order's origin: "nop {order number}", plus " / {destination key}" when the nop order has several
    /// destinations, so each of its Odoo orders has its own.
    /// </summary>
    public static string Origin(string customOrderNumber, int destinationKey, bool severalDestinations) =>
        severalDestinations ? $"nop {customOrderNumber} / {destinationKey}" : $"nop {customOrderNumber}";

    /// <summary>
    /// nop's HTML attribute description as plain text: one line per &lt;br /&gt;, tags dropped, entities decoded,
    /// empty lines removed.
    /// </summary>
    public static string HtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = WebUtility.HtmlDecode(_tag.Replace(_lineBreak.Replace(html, "\n"), string.Empty));

        return string.Join("\n", text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
    }

    /// <summary>
    /// "For: … · Branch: … · Dept: … · CC: …", leaving out the empty parts; empty when all are.
    /// </summary>
    public static string PackNameInfo(string packName, string branch, string department, string costCenter)
    {
        var parts = new List<string>();

        void Add(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{label}: {value.Trim()}");
        }

        Add("For", packName);
        Add("Branch", branch);
        Add("Dept", department);
        Add("CC", costCenter);

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// A line's description, one item per text line: product name, its attributes, the packname line, the PO.
    /// </summary>
    public static string LineName(string productName, string attributeDescriptionHtml, string packNameInfo, string purchaseOrder)
    {
        var lines = new List<string> { productName?.Trim() ?? string.Empty };

        var attributes = HtmlToText(attributeDescriptionHtml);
        if (attributes.Length > 0)
            lines.Add(attributes);

        if (!string.IsNullOrWhiteSpace(packNameInfo))
            lines.Add(packNameInfo);

        if (!string.IsNullOrWhiteSpace(purchaseOrder))
            lines.Add($"PO: {purchaseOrder.Trim()}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// The order's PO number when every line has the same one; otherwise null, and each line carries its own.
    /// </summary>
    public static string CommonPurchaseOrder(IReadOnlyCollection<string> linePurchaseOrders)
    {
        if (linePurchaseOrders.Count == 0 || linePurchaseOrders.Any(string.IsNullOrWhiteSpace))
            return null;

        var distinct = linePurchaseOrders.Select(po => po.Trim()).Distinct(StringComparer.Ordinal).ToList();

        return distinct.Count == 1 ? distinct[0] : null;
    }

    /// <summary>
    /// A line's unit price excl. tax: nop's line total over its quantity, to cents (Odoo's price precision).
    /// </summary>
    public static decimal UnitPrice(decimal lineTotalExclTax, int quantity) =>
        quantity > 0 ? Math.Round(lineTotalExclTax / quantity, 2, MidpointRounding.AwayFromZero) : 0m;

    /// <summary>
    /// The note's HTML: each text line encoded, one paragraph each; empty when there is none.
    /// </summary>
    public static string NoteHtml(IEnumerable<string> lines) =>
        string.Concat(lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => $"<p>{WebUtility.HtmlEncode(line)}</p>"));

    /// <summary>
    /// The values of <c>sale.order/create</c>: the three partners, origin, date and the lines as one2many create
    /// commands, so the order and its lines go in one call.
    /// </summary>
    public static Dictionary<string, object> CreateValues(OdooSaleOrderDraft draft)
    {
        var values = new Dictionary<string, object>
        {
            ["partner_id"] = draft.CustomerPartnerId,
            ["partner_invoice_id"] = draft.CustomerPartnerId,
            ["partner_shipping_id"] = draft.ShippingPartnerId,
            ["origin"] = draft.Origin,
            ["date_order"] = draft.OrderDateUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["order_line"] = draft.Lines.Select((line, index) => (object)new object[] { 0, 0, LineValues(line, index) }).ToList()
        };

        if (!string.IsNullOrWhiteSpace(draft.ClientOrderRef))
            values["client_order_ref"] = draft.ClientOrderRef;

        var note = NoteHtml(draft.NoteLines);
        if (note.Length > 0)
            values["note"] = note;

        return values;
    }

    private static Dictionary<string, object> LineValues(OdooSaleOrderLineDraft line, int index)
    {
        var values = new Dictionary<string, object>
        {
            ["sequence"] = (index + 1) * 10,
            ["product_id"] = line.ProductId,
            ["product_uom_qty"] = line.Quantity,
            ["price_unit"] = line.PriceUnit,
            ["name"] = line.Name
        };

        if (line.IsDelivery)
            values["is_delivery"] = true;

        return values;
    }
}
```

**23. New file `Services/IOrderSyncService.cs`**

```csharp
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// The two lists of the Orders page.
/// </summary>
public enum OdooOrderListKind
{
    /// <summary>Orders placed since the start date, not cancelled, not yet all confirmed in Odoo.</summary>
    ToSend = 0,

    /// <summary>Orders cancelled or deleted in nop whose Odoo orders are still open.</summary>
    ToCancel = 1
}

/// <summary>
/// Sends nop orders to Odoo as sale orders, one per destination, and cancels them there.
/// </summary>
public interface IOrderSyncService
{
    /// <summary>
    /// One page of a list of the Orders page, newest first.
    /// </summary>
    /// <param name="statusId">To send only: -1 all, 0 never tried, otherwise an <see cref="OdooOrderStatus"/>.</param>
    /// <param name="paymentStatusId">To send only: 0 all, otherwise a nop payment status.</param>
    Task<IPagedList<Order>> SearchOrdersAsync(OdooOrderListKind kind, int storeId = 0,
        DateTime? createdFromUtc = null, DateTime? createdToUtc = null, int statusId = -1, int paymentStatusId = 0,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// The mapping rows of the given orders.
    /// </summary>
    Task<IList<OdooOrderMapping>> GetMappingsAsync(IList<int> orderIds);

    /// <summary>
    /// Sends the given orders: creates and confirms what is not yet confirmed, holds what cannot be sent.
    /// </summary>
    Task<OrderSyncResult> SendOrdersAsync(int logEventId, IReadOnlyList<int> orderIds);

    /// <summary>
    /// Cancels in Odoo the open sale orders of the given orders, which must be cancelled or deleted in nop.
    /// </summary>
    Task<OrderSyncResult> CancelOrdersAsync(int logEventId, IReadOnlyList<int> orderIds);
}

/// <summary>
/// The result of an order run, per nop order: Created when an Odoo order was created, Updated when an existing one
/// was confirmed or cancelled, Skipped otherwise (held, failed, nothing to do).
/// </summary>
public record OrderSyncResult(int Created, int Updated, int Skipped)
{
    public int Processed => Created + Updated + Skipped;
}
```

**24. New file `Services/OrderSyncService.cs`**

```csharp
using System.Globalization;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Models.Responses;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Orders;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <inheritdoc />
/// <remarks>
/// Each destination of an order (CW.Core's packname destinations; a plain order has one) becomes its own Odoo
/// sale order, recorded in <see cref="OdooOrderMapping"/>. The Odoo order is created with its lines in one call,
/// its id saved at once, then confirmed: an order whose confirmation failed is confirmed, not created again, on
/// the next send. Before creating, the order is looked up by its origin, which catches a create whose answer was
/// lost.
/// </remarks>
public class OrderSyncService : IOrderSyncService
{
    #region Fields

    protected readonly IAddressService _addressService;
    protected readonly IBranchService _branchService;
    protected readonly ICountryService _countryService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IExchangeService _exchangeService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IOdooApiService _odooApiService;
    protected readonly IOdooLogService _odooLog;
    protected readonly IOrderService _orderService;
    protected readonly IPackNameService _packNameService;
    protected readonly IProductAttributeParser _productAttributeParser;
    protected readonly IProductService _productService;
    protected readonly IRepository<OdooOrderMapping> _orderMappingRepository;
    protected readonly IRepository<Order> _orderRepository;
    protected readonly IStateProvinceService _stateProvinceService;
    protected readonly OdooSettings _settings;

    #endregion

    #region Ctor

    public OrderSyncService(
        IAddressService addressService,
        IBranchService branchService,
        ICountryService countryService,
        ICWOrderService cwOrderService,
        IExchangeService exchangeService,
        IGenericAttributeService genericAttributeService,
        IOdooApiService odooApiService,
        IOdooLogService odooLog,
        IOrderService orderService,
        IPackNameService packNameService,
        IProductAttributeParser productAttributeParser,
        IProductService productService,
        IRepository<OdooOrderMapping> orderMappingRepository,
        IRepository<Order> orderRepository,
        IStateProvinceService stateProvinceService,
        OdooSettings settings)
    {
        _addressService = addressService;
        _branchService = branchService;
        _countryService = countryService;
        _cwOrderService = cwOrderService;
        _exchangeService = exchangeService;
        _genericAttributeService = genericAttributeService;
        _odooApiService = odooApiService;
        _odooLog = odooLog;
        _orderService = orderService;
        _packNameService = packNameService;
        _productAttributeParser = productAttributeParser;
        _productService = productService;
        _orderMappingRepository = orderMappingRepository;
        _orderRepository = orderRepository;
        _stateProvinceService = stateProvinceService;
        _settings = settings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// What one run looks up once and reuses for every order of the batch.
    /// </summary>
    protected sealed class SendRun
    {
        public int? DeliveryProductId { get; init; }
        public int? FeeProductId { get; init; }
        public Dictionary<int, (int? PartnerId, string Reason)> CompanyByCustomerId { get; } = new();
        public Dictionary<string, int> PartnerByAddress { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int?> CountryIdByCode { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int?> StateIdByCode { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A destination of a nop order: where its items go, and what its shipping costs.
    /// </summary>
    protected sealed class Destination
    {
        public int Key { get; init; }
        public int? ShippingAddressId { get; init; }
        public bool PickupInStore { get; init; }
        public string ShippingMethod { get; init; }
        public decimal ShippingExclTax { get; init; }
        public IList<int> OrderItemIds { get; init; } = new List<int>();
    }

    #region Lookups

    /// <summary>
    /// The Odoo id in an <c>ODOO_{id}</c> link, or null.
    /// </summary>
    protected static int? ParseOdooLink(string link) =>
        link != null
        && link.StartsWith(OdooConstants.OdooLinkPrefix, StringComparison.Ordinal)
        && int.TryParse(link[OdooConstants.OdooLinkPrefix.Length..], out var id) ? id : null;

    /// <summary>
    /// The product.product id of an Odoo product's internal reference, logged when it is set but not found.
    /// </summary>
    protected async Task<int?> FindProductByCodeAsync(int logEventId, string code, string purpose)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var id = await _odooApiService.GetProductVariantIdByCodeAsync(code.Trim());
        if (id is null)
            await _odooLog.WarningAsync($"No active Odoo product has the internal reference '{code}' set as the {purpose} product.",
                logEventId, entityType: "Order");

        return id;
    }

    /// <summary>
    /// The Odoo company of a customer: the Odoo id of the top branch above the customer's branch.
    /// </summary>
    protected async Task<(int? PartnerId, string Reason)> ResolveCompanyAsync(SendRun run, int customerId)
    {
        if (run.CompanyByCustomerId.TryGetValue(customerId, out var known))
            return known;

        (int? PartnerId, string Reason) result;
        var branch = await _branchService.GetBranchByCustomerIdAsync(customerId);

        if (branch == null)
            result = (null, "The customer is in no branch, so there is no Odoo company to send the order to.");
        else
        {
            var headOffice = branch;

            // A parent is never a descendant (both branch saves check); the bound only guards hand-edited data.
            for (var depth = 0; headOffice.ParentBranchId is int parentId && depth < 50; depth++)
            {
                var parent = await _branchService.GetBranchByIdAsync(parentId);
                if (parent == null)
                    break;

                headOffice = parent;
            }

            result = int.TryParse(headOffice.CardCode, out var partnerId) && partnerId > 0
                ? (partnerId, null)
                : (null, $"Head office '{headOffice.Name}' is not linked to an Odoo company: its card code is not an Odoo id.");
        }

        run.CompanyByCustomerId[customerId] = result;

        return result;
    }

    /// <summary>
    /// The Odoo variant an order item is for: its combination's link, else the product's single variant.
    /// Null when the product did not come from Odoo or the variant is unknown.
    /// </summary>
    protected async Task<int?> ResolveVariantIdAsync(Product product, string attributesXml)
    {
        if (ParseOdooLink(product.ManufacturerPartNumber) is null)
            return null;

        if (!string.IsNullOrEmpty(attributesXml))
        {
            var combination = await _productAttributeParser.FindProductAttributeCombinationAsync(product, attributesXml);
            if (ParseOdooLink(combination?.ManufacturerPartNumber) is int variantId)
                return variantId;
        }

        var singleVariantId = await _genericAttributeService.GetAttributeAsync<int>(product, OdooConstants.ProductVariantIdAttribute);

        return singleVariantId > 0 ? singleVariantId : null;
    }

    /// <summary>
    /// The Odoo delivery address of a draft: the company for a pickup; otherwise the company or one of its
    /// contacts at the same street, postcode and city, or a new delivery contact under the company.
    /// </summary>
    protected async Task<int> ResolveShippingPartnerAsync(SendRun run, int logEventId, Order order, OdooSaleOrderDraft draft)
    {
        if (draft.ShippingAddressId is not int addressId || await _addressService.GetAddressByIdAsync(addressId) is not Address address)
            return draft.CustomerPartnerId;

        var key = $"{draft.CustomerPartnerId}|{address.Address1}|{address.ZipPostalCode}|{address.City}";
        if (run.PartnerByAddress.TryGetValue(key, out var known))
            return known;

        var partnerId = await _odooApiService.FindDeliveryPartnerAsync(draft.CustomerPartnerId, address.Address1, address.ZipPostalCode, address.City);

        if (partnerId is null)
        {
            var name = string.Join(" ", new[] { address.FirstName, address.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
            var values = new Dictionary<string, object>
            {
                ["parent_id"] = draft.CustomerPartnerId,
                ["type"] = "delivery",
                ["name"] = name.Length > 0 ? name : !string.IsNullOrWhiteSpace(address.Company) ? address.Company : address.Address1 ?? "Delivery address"
            };

            void Add(string field, string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    values[field] = value.Trim();
            }

            Add("street", address.Address1);
            Add("street2", address.Address2);
            Add("city", address.City);
            Add("zip", address.ZipPostalCode);
            Add("phone", address.PhoneNumber);
            Add("email", address.Email);

            if (address.CountryId is int countryId && await _countryService.GetCountryByIdAsync(countryId) is { } country
                && !string.IsNullOrWhiteSpace(country.TwoLetterIsoCode))
            {
                if (!run.CountryIdByCode.TryGetValue(country.TwoLetterIsoCode, out var odooCountryId))
                    run.CountryIdByCode[country.TwoLetterIsoCode] = odooCountryId = await _odooApiService.GetCountryIdByCodeAsync(country.TwoLetterIsoCode);

                if (odooCountryId is int odooCountry)
                {
                    values["country_id"] = odooCountry;

                    if (address.StateProvinceId is int stateId && await _stateProvinceService.GetStateProvinceByIdAsync(stateId) is { } state
                        && !string.IsNullOrWhiteSpace(state.Abbreviation))
                    {
                        var stateKey = $"{odooCountry}|{state.Abbreviation}";
                        if (!run.StateIdByCode.TryGetValue(stateKey, out var odooStateId))
                            run.StateIdByCode[stateKey] = odooStateId = await _odooApiService.GetStateIdByCodeAsync(odooCountry, state.Abbreviation);

                        if (odooStateId is int odooState)
                            values["state_id"] = odooState;
                    }
                }
            }

            partnerId = await _odooApiService.CreatePartnerAsync(values);
            await _odooLog.InformationAsync($"Order {order.CustomOrderNumber}: created Odoo delivery contact {partnerId} ({values["name"]}) under company {draft.CustomerPartnerId}.",
                logEventId, entityType: "Partner", odooEntityId: partnerId, nopEntityId: address.Id);
        }

        run.PartnerByAddress[key] = partnerId.Value;

        return partnerId.Value;
    }

    #endregion

    #region Building

    /// <summary>
    /// The destinations of an order: CW.Core's on a packname order, else one from the order itself.
    /// </summary>
    protected async Task<IList<Destination>> GetDestinationsAsync(Order order, IList<OrderItem> items)
    {
        if (await _cwOrderService.IsPackNameOrderAsync(order.Id))
        {
            var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(order.Id);
            if (destinations.Count > 0)
            {
                var shippings = await _cwOrderService.GetOrderShippingAsync(order.Id);

                return destinations.Select(destination => new Destination
                {
                    Key = destination.Key,
                    ShippingAddressId = destination.PickupInStore ? null : destination.ShippingAddressId,
                    PickupInStore = destination.PickupInStore,
                    ShippingMethod = destination.ShippingMethod,
                    ShippingExclTax = shippings
                        .Where(shipping => shipping.PackNameId is int packNameId && destination.PackNameIds.Contains(packNameId))
                        .Sum(shipping => shipping.ShippingExclTax),
                    OrderItemIds = destination.OrderItemIds
                }).ToList();
            }
        }

        return new List<Destination>
        {
            new()
            {
                Key = 0,
                ShippingAddressId = order.PickupInStore ? null : order.ShippingAddressId,
                PickupInStore = order.PickupInStore,
                ShippingMethod = order.ShippingMethod,
                ShippingExclTax = order.OrderShippingExclTax,
                OrderItemIds = items.Select(item => item.Id).ToList()
            }
        };
    }

    /// <summary>
    /// The display name of an exchange fee type.
    /// </summary>
    protected static string FeeName(FeeType feeType) => feeType switch
    {
        FeeType.ReShippingFee => "Re-shipping fee",
        FeeType.RepackagingFee => "Repackaging fee",
        FeeType.AdditionalFee => "Additional fee",
        _ => feeType.ToString()
    };

    /// <summary>
    /// The notes of the whole nop order, carried by its first Odoo order.
    /// </summary>
    protected static void AddOrderNotes(OdooSaleOrderDraft draft, Order order, int destinationCount, bool isExchange, OrderSettlement settlement)
    {
        static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

        // The nop date travels in the note: Odoo may put its own date on the order when it confirms it
        var placed = order.CreatedOnUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        draft.NoteLines.Add(destinationCount > 1
            ? $"nop order {order.CustomOrderNumber}, placed {placed} UTC, sent as {destinationCount} Odoo orders, one per delivery address. These notes cover the whole nop order."
            : $"nop order {order.CustomOrderNumber}, placed {placed} UTC.");

        if (isExchange)
            draft.NoteLines.Add("Exchange replacement order.");

        if (settlement != null)
        {
            if (settlement.SubsidyStockAmount > 0)
                draft.NoteLines.Add($"Subsidy paid {Money(settlement.SubsidyStockAmount)} of the goods (incl. tax).");
            if (settlement.SubsidyShippingAmount > 0)
                draft.NoteLines.Add($"Subsidy paid {Money(settlement.SubsidyShippingAmount)} of the shipping (incl. tax).");
            if (settlement.CreditNoteAmount > 0)
                draft.NoteLines.Add($"Store credit paid {Money(settlement.CreditNoteAmount)} (incl. tax).");

            draft.NoteLines.Add($"The customer's share in nop: {Money(order.OrderTotal)} (incl. tax).");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
            draft.NoteLines.Add(string.IsNullOrWhiteSpace(order.PaymentMethodSystemName)
                ? "Paid in nop."
                : $"Paid in nop ({order.PaymentMethodSystemName}).");
    }

    /// <summary>
    /// One draft per destination of the order. A draft that cannot be sent carries the reason; partners in
    /// Odoo are not resolved here, since resolving one may create a contact.
    /// </summary>
    protected async Task<IList<OdooSaleOrderDraft>> BuildDraftsAsync(SendRun run, Order order)
    {
        var items = await _orderService.GetOrderItemsAsync(order.Id);
        var itemsById = items.ToDictionary(item => item.Id);
        var destinations = await GetDestinationsAsync(order, items);
        var (companyId, companyReason) = await ResolveCompanyAsync(run, order.CustomerId);

        // A line in no destination would be left out of every Odoo order
        var uncovered = items.Count(item => !destinations.Any(destination => destination.OrderItemIds.Contains(item.Id)));
        var uncoveredReason = uncovered > 0
            ? $"{uncovered} order line(s) belong to no delivery destination, so the order would be sent incomplete."
            : null;

        // Who each line is for, as it was at placement, and the packnames' PO numbers as they are now
        var packNameItems = new Dictionary<int, PackNameOrderItem>();
        foreach (var item in items)
            if (await _cwOrderService.GetPackNameOrderItemAsync(item.Id) is { } packNameItem)
                packNameItems[item.Id] = packNameItem;

        var packNameIds = packNameItems.Values.Select(packNameItem => packNameItem.PackNameId).Distinct().ToArray();
        var purchaseOrderByPackNameId = packNameIds.Length == 0
            ? new Dictionary<int, string>()
            : (await _packNameService.GetPackNamesByIdsAsync(packNameIds)).ToDictionary(packName => packName.Id, packName => packName.POEntry);

        var isExchange = (await _exchangeService.GetReturnRequestItemsForOrderItemsAsync(items.Select(item => item.Id).ToArray())).Any();
        var fees = isExchange
            ? await _exchangeService.GetExchangeFeesAmountsAsync(order)
            : new Dictionary<FeeType, decimal>();
        var settlement = await _cwOrderService.GetOrderSettlementAsync(order.Id);

        var drafts = new List<OdooSaleOrderDraft>();

        foreach (var destination in destinations)
        {
            var draft = new OdooSaleOrderDraft
            {
                DestinationKey = destination.Key,
                Origin = OdooOrderBuilder.Origin(order.CustomOrderNumber, destination.Key, destinations.Count > 1),
                OrderDateUtc = order.CreatedOnUtc,
                ShippingAddressId = destination.ShippingAddressId
            };
            var isFirst = drafts.Count == 0;
            drafts.Add(draft);

            if (companyId is not int company)
            {
                draft.HoldReason = companyReason;
                continue;
            }

            draft.CustomerPartnerId = company;
            var reasons = new List<string>();
            if (uncoveredReason != null)
                reasons.Add(uncoveredReason);

            // Product lines
            var lines = new List<(OrderItem Item, Product Product, int VariantId, string PackNameInfo, string PurchaseOrder)>();
            foreach (var itemId in destination.OrderItemIds)
            {
                if (!itemsById.TryGetValue(itemId, out var item))
                    continue;

                var product = await _productService.GetProductByIdAsync(item.ProductId);
                if (product == null || await ResolveVariantIdAsync(product, item.AttributesXml) is not int variantId)
                {
                    reasons.Add($"'{product?.Name ?? $"Product {item.ProductId}"}' is not linked to an Odoo product variant.");
                    continue;
                }

                packNameItems.TryGetValue(item.Id, out var packNameItem);
                var purchaseOrder = packNameItem != null && purchaseOrderByPackNameId.TryGetValue(packNameItem.PackNameId, out var po) ? po : null;
                var info = packNameItem == null
                    ? null
                    : OdooOrderBuilder.PackNameInfo(packNameItem.PackName, packNameItem.Branch, packNameItem.Department, packNameItem.CostCenter);

                lines.Add((item, product, variantId, info, purchaseOrder));
            }

            draft.ClientOrderRef = OdooOrderBuilder.CommonPurchaseOrder(lines.Select(line => line.PurchaseOrder).ToList());

            foreach (var line in lines)
                draft.Lines.Add(new OdooSaleOrderLineDraft
                {
                    ProductId = line.VariantId,
                    Quantity = line.Item.Quantity,
                    PriceUnit = OdooOrderBuilder.UnitPrice(line.Item.PriceExclTax, line.Item.Quantity),
                    Name = OdooOrderBuilder.LineName(line.Product.Name, line.Item.AttributeDescription, line.PackNameInfo,
                        draft.ClientOrderRef == null ? line.PurchaseOrder : null)
                });

            draft.NopUntaxed = lines.Sum(line => line.Item.PriceExclTax);

            // Shipping line
            if (!destination.PickupInStore && (destination.ShippingExclTax > 0 || !string.IsNullOrWhiteSpace(destination.ShippingMethod)))
            {
                if (run.DeliveryProductId is int deliveryProductId)
                {
                    draft.Lines.Add(new OdooSaleOrderLineDraft
                    {
                        ProductId = deliveryProductId,
                        Quantity = 1,
                        PriceUnit = Math.Round(destination.ShippingExclTax, 2, MidpointRounding.AwayFromZero),
                        Name = string.IsNullOrWhiteSpace(destination.ShippingMethod) ? "Shipping" : destination.ShippingMethod,
                        IsDelivery = true
                    });
                    draft.NopUntaxed += destination.ShippingExclTax;
                }
                else if (destination.ShippingExclTax > 0)
                    reasons.Add("Shipping is charged, but no Odoo delivery product is set on the Odoo Settings page.");
            }

            // The fees and the notes of the whole order go on its first Odoo order
            if (isFirst)
            {
                foreach (var (feeType, amount) in fees.Where(fee => fee.Value > 0))
                {
                    if (run.FeeProductId is int feeProductId)
                    {
                        draft.Lines.Add(new OdooSaleOrderLineDraft
                        {
                            ProductId = feeProductId,
                            Quantity = 1,
                            PriceUnit = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
                            Name = FeeName(feeType)
                        });
                        draft.NopUntaxed += amount;
                    }
                    else
                        reasons.Add("The order has exchange fees, but no Odoo exchange fee product is set on the Odoo Settings page.");
                }

                AddOrderNotes(draft, order, destinations.Count, isExchange, settlement);
            }
            else
                draft.NoteLines.Add($"Part of nop order {order.CustomOrderNumber}; its notes are on the Odoo order with origin '{drafts[0].Origin}'.");

            if (draft.Lines.Count == 0)
                reasons.Add("Nothing to send: no line of this destination is linked to Odoo.");

            if (reasons.Count > 0)
                draft.HoldReason = string.Join(" ", reasons.Distinct());
        }

        return drafts;
    }

    #endregion

    #region Sending

    /// <summary>
    /// Saves a mapping row, inserting it the first time.
    /// </summary>
    protected async Task SaveMappingAsync(OdooOrderMapping mapping)
    {
        mapping.UpdatedOnUtc = DateTime.UtcNow;

        if (mapping.Id == 0)
            await _orderMappingRepository.InsertAsync(mapping, false);
        else
            await _orderMappingRepository.UpdateAsync(mapping, false);
    }

    /// <summary>
    /// Creates (or finds) and confirms the Odoo order of one draft.
    /// </summary>
    /// <returns>Created when it created the Odoo order, Updated when the order already existed.</returns>
    /// <remarks>Private: <see cref="SyncOutcome"/> is internal to the plugin.</remarks>
    private async Task<SyncOutcome> SendDraftAsync(SendRun run, int logEventId, Order order, OdooSaleOrderDraft draft, OdooOrderMapping mapping)
    {
        var created = false;

        // Created by an earlier send whose answer was lost
        if (mapping.OdooOrderId is null && await _odooApiService.FindSaleOrderByOriginAsync(draft.Origin) is OdooSaleOrderResponse existing)
        {
            mapping.OdooOrderId = existing.Id;
            await _odooLog.InformationAsync($"Order {order.CustomOrderNumber}: Odoo already has {existing.Name} with origin '{draft.Origin}'; it is used.",
                logEventId, entityType: "Order", odooEntityId: existing.Id, nopEntityId: order.Id);
        }

        if (mapping.OdooOrderId is null)
        {
            draft.ShippingPartnerId = await ResolveShippingPartnerAsync(run, logEventId, order, draft);
            mapping.OdooOrderId = await _odooApiService.CreateSaleOrderAsync(OdooOrderBuilder.CreateValues(draft));
            mapping.Status = OdooOrderStatus.Created;
            mapping.Message = null;

            // Saved before confirming: a failed confirmation must not lead to a second order
            await SaveMappingAsync(mapping);
            created = true;
        }

        var saleOrder = await _odooApiService.GetSaleOrderAsync(mapping.OdooOrderId.Value)
            ?? throw new OdooApiException($"Odoo sale order {mapping.OdooOrderId} no longer exists.");

        if (saleOrder.State == "cancel")
            throw new OdooApiException($"Odoo sale order {saleOrder.Name} is cancelled in Odoo.");

        if (saleOrder.State is "draft" or "sent")
        {
            await _odooApiService.ConfirmSaleOrderAsync(saleOrder.Id);

            // An answer without an error is not proof that Odoo confirmed: read the state back
            saleOrder = await _odooApiService.GetSaleOrderAsync(saleOrder.Id)
                ?? throw new OdooApiException($"Odoo sale order {mapping.OdooOrderId} no longer exists.");
            if (saleOrder.State != "sale")
                throw new OdooApiException($"Odoo did not confirm {saleOrder.Name}: its state is '{saleOrder.State}'.");
        }

        // Compared with what nop charged, not with the rounded prices sent. A draft held now but created by
        // an earlier send has no complete nop total to compare.
        var warning = draft.HoldReason == null && Math.Abs(saleOrder.AmountUntaxed - draft.NopUntaxed) > 0.01m
            ? $"Odoo's total before tax {saleOrder.AmountUntaxed.ToString("0.00", CultureInfo.InvariantCulture)} differs from nop's {draft.NopUntaxed.ToString("0.00", CultureInfo.InvariantCulture)}."
            : null;

        mapping.Status = OdooOrderStatus.Confirmed;
        mapping.OdooOrderName = saleOrder.Name;
        mapping.Message = warning;
        await SaveMappingAsync(mapping);

        if (warning != null)
            await _odooLog.WarningAsync($"Order {order.CustomOrderNumber} → {saleOrder.Name}: {warning}",
                logEventId, entityType: "Order", odooEntityId: saleOrder.Id, nopEntityId: order.Id);

        await _odooLog.InformationAsync($"Order {order.CustomOrderNumber} → Odoo {saleOrder.Name} ({(created ? "created" : "existing")}, confirmed).",
            logEventId, entityType: "Order", odooEntityId: saleOrder.Id, nopEntityId: order.Id);

        return created ? SyncOutcome.Created : SyncOutcome.Updated;
    }

    /// <summary>
    /// Sends every destination of an order that is not yet confirmed.
    /// </summary>
    private async Task<SyncOutcome> SendOrderAsync(SendRun run, int logEventId, Order order)
    {
        if (order.Deleted || order.OrderStatus == OrderStatus.Cancelled)
        {
            await _odooLog.InformationAsync($"Order {order.CustomOrderNumber} is deleted or cancelled in nop; not sent.",
                logEventId, entityType: "Order", nopEntityId: order.Id);
            return SyncOutcome.Skipped;
        }

        var mappings = (await _orderMappingRepository.Table.Where(mapping => mapping.NopOrderId == order.Id).ToListAsync())
            .ToDictionary(mapping => mapping.DestinationKey);

        var anyCreated = false;
        var anyConfirmed = false;

        var drafts = await BuildDraftsAsync(run, order);

        // A row for every destination before any is sent: if the run stops part-way, the destinations it did
        // not reach keep the order listed to send
        foreach (var draft in drafts.Where(draft => !mappings.ContainsKey(draft.DestinationKey)).ToList())
        {
            var pending = new OdooOrderMapping
            {
                NopOrderId = order.Id,
                DestinationKey = draft.DestinationKey,
                CreatedOnUtc = DateTime.UtcNow,
                Status = OdooOrderStatus.Pending
            };
            await SaveMappingAsync(pending);
            mappings[draft.DestinationKey] = pending;
        }

        foreach (var draft in drafts)
        {
            var mapping = mappings[draft.DestinationKey];

            if (mapping.Status is OdooOrderStatus.Confirmed or OdooOrderStatus.Cancelled)
                continue;

            // A draft held now but created by an earlier send is confirmed all the same
            if (draft.HoldReason != null && mapping.OdooOrderId is null)
            {
                mapping.Status = OdooOrderStatus.Held;
                mapping.Message = draft.HoldReason;
                await SaveMappingAsync(mapping);
                await _odooLog.WarningAsync($"Order {order.CustomOrderNumber} held: {draft.HoldReason}",
                    logEventId, entityType: "Order", nopEntityId: order.Id);
                continue;
            }

            try
            {
                var outcome = await SendDraftAsync(run, logEventId, order, draft, mapping);
                anyCreated |= outcome == SyncOutcome.Created;
                anyConfirmed |= outcome == SyncOutcome.Updated;
            }
            catch (Exception ex)
            {
                mapping.Status = OdooOrderStatus.Failed;
                mapping.Message = ex.Message;
                await SaveMappingAsync(mapping);
                await _odooLog.ErrorAsync($"Order {order.CustomOrderNumber}: {ex.Message}",
                    logEventId, fullMessage: ex.ToString(), entityType: "Order", odooEntityId: mapping.OdooOrderId, nopEntityId: order.Id);
            }
        }

        return anyCreated ? SyncOutcome.Created : anyConfirmed ? SyncOutcome.Updated : SyncOutcome.Skipped;
    }

    #endregion

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IPagedList<Order>> SearchOrdersAsync(OdooOrderListKind kind, int storeId = 0,
        DateTime? createdFromUtc = null, DateTime? createdToUtc = null, int statusId = -1, int paymentStatusId = 0,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var created = (int)OdooOrderStatus.Created;
        var confirmed = (int)OdooOrderStatus.Confirmed;
        var failed = (int)OdooOrderStatus.Failed;
        var cancelled = (int)OdooOrderStatus.Cancelled;
        var orderCancelled = (int)OrderStatus.Cancelled;

        var mappings = _orderMappingRepository.Table;
        IQueryable<Order> query = _orderRepository.Table;

        if (storeId > 0)
            query = query.Where(order => order.StoreId == storeId);
        if (createdFromUtc.HasValue)
            query = query.Where(order => order.CreatedOnUtc >= createdFromUtc.Value);
        if (createdToUtc.HasValue)
            query = query.Where(order => order.CreatedOnUtc < createdToUtc.Value);

        if (kind == OdooOrderListKind.ToCancel)
        {
            // A deleted order can no longer be cancelled in nop, so it counts as cancelled here
            query = query.Where(order => (order.Deleted || order.OrderStatusId == orderCancelled)
                && mappings.Any(mapping => mapping.NopOrderId == order.Id && mapping.OdooOrderId != null
                    && (mapping.StatusId == created || mapping.StatusId == confirmed || mapping.StatusId == failed)));
        }
        else
        {
            if (_settings.OrderSyncStartDateUtc is not DateTime startUtc)
                return new PagedList<Order>(new List<Order>(), pageIndex, pageSize);

            query = query.Where(order => !order.Deleted && order.OrderStatusId != orderCancelled && order.CreatedOnUtc >= startUtc
                && (!mappings.Any(mapping => mapping.NopOrderId == order.Id)
                    || mappings.Any(mapping => mapping.NopOrderId == order.Id && mapping.StatusId != confirmed && mapping.StatusId != cancelled)));

            if (statusId == 0)
                query = query.Where(order => !mappings.Any(mapping => mapping.NopOrderId == order.Id));
            else if (statusId > 0)
                query = query.Where(order => mappings.Any(mapping => mapping.NopOrderId == order.Id && mapping.StatusId == statusId));

            if (paymentStatusId > 0)
                query = query.Where(order => order.PaymentStatusId == paymentStatusId);
        }

        return await query.OrderByDescending(order => order.CreatedOnUtc).ToPagedListAsync(pageIndex, pageSize);
    }

    /// <inheritdoc />
    public async Task<IList<OdooOrderMapping>> GetMappingsAsync(IList<int> orderIds)
    {
        if (orderIds == null || orderIds.Count == 0)
            return new List<OdooOrderMapping>();

        return await _orderMappingRepository.Table.Where(mapping => orderIds.Contains(mapping.NopOrderId)).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<OrderSyncResult> SendOrdersAsync(int logEventId, IReadOnlyList<int> orderIds)
    {
        var run = new SendRun
        {
            DeliveryProductId = await FindProductByCodeAsync(logEventId, _settings.DeliveryProductCode, "delivery"),
            FeeProductId = await FindProductByCodeAsync(logEventId, _settings.ExchangeFeeProductCode, "exchange fee")
        };
        var tally = new SyncTally();

        foreach (var orderId in (orderIds ?? Array.Empty<int>()).Distinct())
        {
            var outcome = SyncOutcome.Skipped;
            try
            {
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null)
                    await _odooLog.WarningAsync($"Order {orderId} does not exist.", logEventId, entityType: "Order", nopEntityId: orderId);
                else
                    outcome = await SendOrderAsync(run, logEventId, order);
            }
            catch (Exception ex)
            {
                await _odooLog.ErrorAsync($"Order {orderId} could not be sent: {ex.Message}",
                    logEventId, fullMessage: ex.ToString(), entityType: "Order", nopEntityId: orderId);
            }
            finally
            {
                tally.Add(outcome);
            }
        }

        return new OrderSyncResult(tally.Created, tally.Updated, tally.Skipped);
    }

    /// <inheritdoc />
    public async Task<OrderSyncResult> CancelOrdersAsync(int logEventId, IReadOnlyList<int> orderIds)
    {
        var open = new[] { (int)OdooOrderStatus.Created, (int)OdooOrderStatus.Confirmed, (int)OdooOrderStatus.Failed };
        var tally = new SyncTally();

        foreach (var orderId in (orderIds ?? Array.Empty<int>()).Distinct())
        {
            var outcome = SyncOutcome.Skipped;
            try
            {
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null || (order.OrderStatus != OrderStatus.Cancelled && !order.Deleted))
                {
                    await _odooLog.WarningAsync($"Order {order?.CustomOrderNumber ?? orderId.ToString()} is neither cancelled nor deleted in nop; its Odoo orders are left alone.",
                        logEventId, entityType: "Order", nopEntityId: orderId);
                    continue;
                }

                var mappings = await _orderMappingRepository.Table
                    .Where(mapping => mapping.NopOrderId == orderId && mapping.OdooOrderId != null && open.Contains(mapping.StatusId))
                    .ToListAsync();

                foreach (var mapping in mappings)
                {
                    try
                    {
                        await _odooApiService.CancelSaleOrderAsync(mapping.OdooOrderId.Value);

                        // Odoo can answer with a confirmation step instead of cancelling: read the state back.
                        // An order that no longer exists in Odoo needs no cancelling.
                        var after = await _odooApiService.GetSaleOrderAsync(mapping.OdooOrderId.Value);
                        if (after != null && after.State != "cancel")
                            throw new OdooApiException($"its state is still '{after.State}'.");

                        mapping.Status = OdooOrderStatus.Cancelled;
                        mapping.Message = null;
                        await SaveMappingAsync(mapping);
                        await _odooLog.InformationAsync($"Order {order.CustomOrderNumber}: Odoo {mapping.OdooOrderName ?? mapping.OdooOrderId.ToString()} cancelled.",
                            logEventId, entityType: "Order", odooEntityId: mapping.OdooOrderId, nopEntityId: orderId);
                        outcome = SyncOutcome.Updated;
                    }
                    catch (Exception ex)
                    {
                        mapping.Message = $"Odoo refused the cancellation: {ex.Message}";
                        await SaveMappingAsync(mapping);
                        await _odooLog.ErrorAsync($"Order {order.CustomOrderNumber}: {mapping.Message}",
                            logEventId, fullMessage: ex.ToString(), entityType: "Order", odooEntityId: mapping.OdooOrderId, nopEntityId: orderId);
                    }
                }
            }
            catch (Exception ex)
            {
                await _odooLog.ErrorAsync($"Order {orderId} could not be cancelled in Odoo: {ex.Message}",
                    logEventId, fullMessage: ex.ToString(), entityType: "Order", nopEntityId: orderId);
            }
            finally
            {
                tally.Add(outcome);
            }
        }

        return new OrderSyncResult(tally.Created, tally.Updated, tally.Skipped);
    }

    #endregion
}
```

**25. `Infrastructure/NopStartup.cs`, lines 35–36**

FIND (lines 35–36):
```csharp
        // Register department sync (reuses CW.Core department/role provisioning)
        services.AddScoped<IDepartmentSyncService, DepartmentSyncService>();
```
REPLACE WITH:
```csharp
        // Register department sync (reuses CW.Core department/role provisioning)
        services.AddScoped<IDepartmentSyncService, DepartmentSyncService>();

        // Register the order send (nop orders → Odoo sale orders)
        services.AddScoped<IOrderSyncService, OrderSyncService>();
```

### 7.5 The queue and the background run

**26. `Services/IProductSyncQueue.cs`, whole file** (three places change)

```csharp
using Nop.Plugin.Integration.Odoo.Domain;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Represents a queued sync request with a type discriminator. <see cref="EntityIds"/> are the nop records the
/// request is for (the orders of an order send or cancel); null for the syncs that read everything from Odoo.
/// </summary>
public record OdooSyncRequest(Guid RequestId, DateTime QueuedAtUtc, SyncEventType SyncType, IReadOnlyList<int> EntityIds = null);

/// <summary>
/// Thin abstraction over the sync channel so controllers don't depend on the channel directly.
/// A sync type is claimed when it is queued and released when the background service finishes it,
/// so a second request of the same type is refused while one is waiting OR running.
/// </summary>
public interface IOdooSyncQueue
{
    /// <summary>
    /// Claims the sync type and queues a request in one atomic step.
    /// </summary>
    /// <param name="syncType">The sync to queue.</param>
    /// <param name="entityIds">The nop records the request is for, or null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The request ID, or null when a sync of that type is already queued or running.</returns>
    ValueTask<Guid?> TryEnqueueSyncAsync(SyncEventType syncType, IReadOnlyList<int> entityIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the sync type. Called by the background service when processing finishes, whatever
    /// the outcome.
    /// </summary>
    void SetSyncCompleted(SyncEventType syncType);
}

public class OdooSyncQueue : IOdooSyncQueue
{
    private readonly Channel<OdooSyncRequest> _channel;

    // Sync types currently queued or running; the key's presence is the claim, the value is unused.
    private readonly ConcurrentDictionary<SyncEventType, byte> _claimed = new();

    public OdooSyncQueue(Channel<OdooSyncRequest> channel)
    {
        _channel = channel;
    }

    public async ValueTask<Guid?> TryEnqueueSyncAsync(SyncEventType syncType, IReadOnlyList<int> entityIds = null, CancellationToken cancellationToken = default)
    {
        // TryAdd is atomic: of two simultaneous requests for the same type, exactly one claims it.
        if (!_claimed.TryAdd(syncType, 0))
            return null;

        try
        {
            var request = new OdooSyncRequest(Guid.NewGuid(), DateTime.UtcNow, syncType, entityIds);
            await _channel.Writer.WriteAsync(request, cancellationToken);
            return request.RequestId;
        }
        catch
        {
            // Nothing was queued, so nothing will ever release the claim — release it here.
            SetSyncCompleted(syncType);
            throw;
        }
    }

    public void SetSyncCompleted(SyncEventType syncType) => _claimed.TryRemove(syncType, out _);
}
```

**27. `Services/ProductSyncBackgroundService.cs`, lines 113–121**

FIND (lines 113–121):
```csharp
                case SyncEventType.Department:
                    var departmentSyncService = scope.ServiceProvider.GetRequiredService<IDepartmentSyncService>();
                    var deptResult = await departmentSyncService.SyncDepartmentsAsync(logEventId);
                    totalCreated = deptResult.Created;
                    totalUpdated = deptResult.Updated;
                    totalSkipped = deptResult.Skipped;
                    totalProcessed = deptResult.Processed;
                    break;
            }
```
REPLACE WITH:
```csharp
                case SyncEventType.Department:
                    var departmentSyncService = scope.ServiceProvider.GetRequiredService<IDepartmentSyncService>();
                    var deptResult = await departmentSyncService.SyncDepartmentsAsync(logEventId);
                    totalCreated = deptResult.Created;
                    totalUpdated = deptResult.Updated;
                    totalSkipped = deptResult.Skipped;
                    totalProcessed = deptResult.Processed;
                    break;

                case SyncEventType.OrderSend:
                case SyncEventType.OrderCancel:
                    var orderSyncService = scope.ServiceProvider.GetRequiredService<IOrderSyncService>();
                    var orderIds = request.EntityIds ?? Array.Empty<int>();
                    var orderResult = request.SyncType == SyncEventType.OrderSend
                        ? await orderSyncService.SendOrdersAsync(logEventId, orderIds)
                        : await orderSyncService.CancelOrdersAsync(logEventId, orderIds);
                    totalCreated = orderResult.Created;
                    totalUpdated = orderResult.Updated;
                    totalSkipped = orderResult.Skipped;
                    totalProcessed = orderResult.Processed;
                    break;
            }
```

**28. `Services/ProductSyncBackgroundService.cs`, lines 69–70**

FIND (lines 69–70):
```csharp
            SyncEventType.Department => "Department Sync",
            _ => "Unknown Sync"
```
REPLACE WITH:
```csharp
            SyncEventType.Department => "Department Sync",
            SyncEventType.OrderSend => "Order Send",
            SyncEventType.OrderCancel => "Order Cancel",
            _ => "Unknown Sync"
```

### 7.6 Admin: models, controller, views, script, menu, resources

**29. New file `Models/Orders/OdooOrderSearchModel.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Integration.Odoo.Models;

/// <summary>
/// The filters of the Orders page; <see cref="ListKind"/> says which of its two grids is asking.
/// </summary>
public partial record OdooOrderSearchModel : BaseSearchModel
{
    [NopResourceDisplayName("Plugins.Integration.Odoo.Orders.List.Store")]
    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Orders.List.CreatedFrom")]
    [UIHint("DateNullable")]
    public DateTime? CreatedFrom { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Orders.List.CreatedTo")]
    [UIHint("DateNullable")]
    public DateTime? CreatedTo { get; set; }

    /// <summary>
    /// -1 all, 0 never tried, otherwise an Odoo order status.
    /// </summary>
    [NopResourceDisplayName("Plugins.Integration.Odoo.Orders.List.Status")]
    public int StatusId { get; set; } = -1;

    /// <summary>
    /// 0 all, otherwise a nop payment status.
    /// </summary>
    [NopResourceDisplayName("Plugins.Integration.Odoo.Orders.List.PaymentStatus")]
    public int PaymentStatusId { get; set; }

    /// <summary>
    /// An <c>OdooOrderListKind</c>: 0 to send, 1 to cancel.
    /// </summary>
    public int ListKind { get; set; }

    /// <summary>
    /// True when no start date is set, so the list to send is empty.
    /// </summary>
    public bool StartDateMissing { get; set; }

    public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();

    public IList<SelectListItem> AvailableStatuses { get; set; } = new List<SelectListItem>();

    public IList<SelectListItem> AvailablePaymentStatuses { get; set; } = new List<SelectListItem>();
}
```

**30. New file `Models/Orders/OdooOrderModel.cs`**

```csharp
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Integration.Odoo.Models;

/// <summary>
/// A row of the Orders page: a nop order and where it stands in Odoo. Id is the nop order id.
/// </summary>
public partial record OdooOrderModel : BaseNopEntityModel
{
    public string CustomOrderNumber { get; set; }

    public string StoreName { get; set; }

    public DateTime CreatedOn { get; set; }

    public string OrderTotal { get; set; }

    /// <summary>
    /// nop's payment status of the order, localised.
    /// </summary>
    public string PaymentStatus { get; set; }

    /// <summary>
    /// The statuses of its Odoo orders, or "Not sent".
    /// </summary>
    public string OdooStatus { get; set; }

    /// <summary>
    /// The Odoo order references.
    /// </summary>
    public string OdooOrders { get; set; }

    /// <summary>
    /// Why it is held or failed, or the warnings of the send.
    /// </summary>
    public string Message { get; set; }
}
```

**31. New file `Models/Orders/OdooOrderListModel.cs`**

```csharp
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Integration.Odoo.Models;

public partial record OdooOrderListModel : BasePagedListModel<OdooOrderModel>;
```

**32. `Models/ConfigurationModel.cs`, lines 48–53**

FIND (lines 48–53):
```csharp
    public DateTime? LastSyncDateTime { get; set; }

    public string ConnectionStatus { get; set; }

    public string TestResultMessage { get; set; }
}
```
REPLACE WITH:
```csharp
    public DateTime? LastSyncDateTime { get; set; }

    public string ConnectionStatus { get; set; }

    public string TestResultMessage { get; set; }

    /// <summary>
    /// Orders placed on or after this date (the admin's time zone) are listed to send.
    /// </summary>
    [NopResourceDisplayName("Plugins.Integration.Odoo.Fields.OrderSyncStartDate")]
    [UIHint("DateNullable")]
    public DateTime? OrderSyncStartDate { get; init; }

    /// <summary>
    /// Internal reference of the Odoo product used for shipping lines.
    /// </summary>
    [NopResourceDisplayName("Plugins.Integration.Odoo.Fields.DeliveryProductCode")]
    public string DeliveryProductCode { get; init; }

    /// <summary>
    /// Internal reference of the Odoo service product used for exchange fee lines.
    /// </summary>
    [NopResourceDisplayName("Plugins.Integration.Odoo.Fields.ExchangeFeeProductCode")]
    public string ExchangeFeeProductCode { get; init; }
}
```

**33. `Controllers/OdooAdminController.cs`, whole file** (it changes in many places: fields, constructor, two
utilities, the Configure page, the Sync Log type list, and a new *Orders* region)

```csharp
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Infrastructure;
using Nop.Plugin.Integration.Odoo.Models;
using Nop.Plugin.Integration.Odoo.Services;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Integration.Odoo.Controllers;

/// <summary>
/// Admin pages of the Odoo integration: the connection settings, the sync log, from which the background syncs
/// are started, and the orders sent to Odoo.
/// </summary>
/// <remarks>
/// The sync and order actions answer <c>{ ConnectionStatus, TestResultMessage }</c>, read by
/// <c>Content/Admin/Odoo/odoo-admin.js</c>; ConnectionStatus is Failed, Busy, Queued or Error.
/// The Orders page has a permission of its own (<see cref="OdooPermissionConfigManager.SEND_ORDERS"/>), so
/// the staff who send orders need not manage plugins; everything else needs Manage plugins.
/// </remarks>
[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class OdooAdminController : BasePluginController
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IOdooApiService _odooApiService;
    protected readonly IOdooSyncQueue _syncQueue;
    protected readonly IOdooLogService _odooLogService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly IOrderSyncService _orderSyncService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public OdooAdminController(
        ISettingService settingService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        IOdooApiService odooApiService,
        IOdooSyncQueue syncQueue,
        IOdooLogService odooLogService,
        IDateTimeHelper dateTimeHelper,
        IOrderSyncService orderSyncService,
        IStoreService storeService)
    {
        _settingService = settingService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _odooApiService = odooApiService;
        _syncQueue = syncQueue;
        _odooLogService = odooLogService;
        _dateTimeHelper = dateTimeHelper;
        _orderSyncService = orderSyncService;
        _storeService = storeService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// A view from the plugin's <c>Views/OdooAdmin</c> folder.
    /// </summary>
    protected ViewResult PluginView(string name, object model)
        => View($"{OdooConstants.ViewsPath}{name}.cshtml", model);

    /// <summary>
    /// Whether the URL, database name and API key needed to call Odoo are saved.
    /// </summary>
    protected static bool IsConnectionConfigured(OdooSettings settings)
        => !string.IsNullOrWhiteSpace(settings.OdooUrl)
            && !string.IsNullOrWhiteSpace(settings.DatabaseName)
            && !string.IsNullOrWhiteSpace(settings.ApiKey);

    /// <summary>
    /// The Configure page model from the saved settings, dates in the admin's time zone. The API key itself never
    /// goes to the view.
    /// </summary>
    protected async Task<ConfigurationModel> PrepareConfigurationModelAsync(OdooSettings settings) => new()
    {
        OdooUrl = settings.OdooUrl,
        DatabaseName = settings.DatabaseName,
        Username = settings.Username,
        ApiKey = string.Empty,
        HasApiKey = !string.IsNullOrWhiteSpace(settings.ApiKey),
        LastSyncDateTime = settings.LastSyncDateTime.HasValue
            ? await _dateTimeHelper.ConvertToUserTimeAsync(settings.LastSyncDateTime.Value, DateTimeKind.Utc)
            : null,
        OrderSyncStartDate = settings.OrderSyncStartDateUtc.HasValue
            ? await _dateTimeHelper.ConvertToUserTimeAsync(settings.OrderSyncStartDateUtc.Value, DateTimeKind.Utc)
            : null,
        DeliveryProductCode = settings.DeliveryProductCode,
        ExchangeFeeProductCode = settings.ExchangeFeeProductCode
    };

    /// <summary>
    /// Queues a background sync of the given type and reports the outcome as JSON for the sync and order
    /// buttons: connection not configured, already queued or running, queued, or failed to queue.
    /// </summary>
    /// <param name="syncType">The sync to queue.</param>
    /// <param name="label">Display name of the sync type, e.g. "Store".</param>
    /// <param name="entityIds">The nop records the request is for (orders), or null.</param>
    protected async Task<IActionResult> QueueSyncAsync(SyncEventType syncType, string label, IReadOnlyList<int> entityIds = null)
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();

        if (!IsConnectionConfigured(settings))
        {
            return Json(new
            {
                ConnectionStatus = "Failed",
                TestResultMessage = "Please fill in Odoo URL, Database Name, and API Key before syncing."
            });
        }

        try
        {
            // Claim and enqueue in one step: returns at once, and refuses a duplicate while a sync of
            // this type is queued or running
            var requestId = await _syncQueue.TryEnqueueSyncAsync(syncType, entityIds);
            if (requestId is null)
            {
                return Json(new
                {
                    ConnectionStatus = "Busy",
                    TestResultMessage = $"{label} sync is already queued or running. Please wait for it to complete before starting another."
                });
            }

            await UpdateLastSyncDateTimeAsync();

            return Json(new
            {
                ConnectionStatus = "Queued",
                TestResultMessage = entityIds == null
                    ? $"{label} sync has been queued (ID: {requestId}). Check the Sync Logs for progress."
                    : $"{label} of {entityIds.Count} order(s) has been queued (ID: {requestId}). Check the Sync Logs for progress, then refresh this page."
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                ConnectionStatus = "Error",
                TestResultMessage = $"Failed to queue the {label.ToLowerInvariant()} sync: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Saves the current UTC time as the last sync date/time in the global <see cref="OdooSettings"/>.
    /// </summary>
    protected async Task UpdateLastSyncDateTimeAsync()
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();
        settings.LastSyncDateTime = DateTime.UtcNow;

        await _settingService.SaveSettingAsync(settings, storeId: 0);
        await _settingService.ClearCacheAsync();
    }

    /// <summary>
    /// The Orders page's date filters in UTC: from the start of the first day to the end of the last.
    /// </summary>
    protected async Task<(DateTime? FromUtc, DateTime? ToUtc)> ToUtcRangeAsync(OdooOrderSearchModel searchModel)
    {
        var timeZone = await _dateTimeHelper.GetCurrentTimeZoneAsync();

        DateTime? fromUtc = searchModel.CreatedFrom.HasValue
            ? _dateTimeHelper.ConvertToUtcTime(searchModel.CreatedFrom.Value.Date, timeZone)
            : null;
        DateTime? toUtc = searchModel.CreatedTo.HasValue
            ? _dateTimeHelper.ConvertToUtcTime(searchModel.CreatedTo.Value.Date, timeZone).AddDays(1)
            : null;

        return (fromUtc, toUtc);
    }

    #endregion

    #region Configuration

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure()
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();

        return PluginView("Configure", await PrepareConfigurationModelAsync(settings));
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return PluginView("Configure", model);

        var settings = await _settingService.LoadSettingAsync<OdooSettings>();

        settings.OdooUrl = model.OdooUrl.TrimEnd('/');
        settings.DatabaseName = model.DatabaseName.Trim();
        settings.Username = model.Username.Trim();

        // Only overwrite the stored API key if the user supplied a new one
        if (!string.IsNullOrWhiteSpace(model.ApiKey))
            settings.ApiKey = model.ApiKey.Trim();

        settings.OrderSyncStartDateUtc = model.OrderSyncStartDate.HasValue
            ? _dateTimeHelper.ConvertToUtcTime(model.OrderSyncStartDate.Value.Date, await _dateTimeHelper.GetCurrentTimeZoneAsync())
            : null;
        settings.DeliveryProductCode = model.DeliveryProductCode?.Trim() ?? string.Empty;
        settings.ExchangeFeeProductCode = model.ExchangeFeeProductCode?.Trim() ?? string.Empty;

        // StoreId = 0 → global setting (not store-scoped)
        await _settingService.SaveSettingAsync(settings, storeId: 0);
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction(nameof(Configure));
    }

    /// <summary>
    /// Tests the saved connection and shows the Configure page with the result.
    /// </summary>
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> TestConnection()
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();
        var model = await PrepareConfigurationModelAsync(settings);

        if (!IsConnectionConfigured(settings))
        {
            model.ConnectionStatus = "Failed";
            model.TestResultMessage = "Please fill in Odoo URL, Database Name, and API Key before testing.";
            return PluginView("Configure", model);
        }

        var result = await _odooApiService.TestConnectionAsync(settings.OdooUrl, settings.DatabaseName, settings.ApiKey);

        model.ConnectionStatus = result.Success ? "Connected" : "Failed";
        model.TestResultMessage = result.Success
            ? $"Success! Connected to Odoo version {result.OdooVersion}. Logged in as {result.LoggedInUser}. Product count: {result.ProductCount}."
            : $"Connection failed: {result.ErrorMessage}";

        return PluginView("Configure", model);
    }

    [HttpGet]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Diagnostics()
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();

        var model = new DiagnosticsModel
        {
            OdooUrl = settings.OdooUrl,
            DatabaseName = settings.DatabaseName,
            ApiKeyExists = !string.IsNullOrWhiteSpace(settings.ApiKey),
            ConnectionStatus = "Not tested"
        };

        if (!IsConnectionConfigured(settings))
        {
            model.TestResultMessage = "Please fill in Odoo URL, Database Name, and API Key before testing.";
            return Json(model);
        }

        try
        {
            var result = await _odooApiService.TestConnectionAsync(settings.OdooUrl, settings.DatabaseName, settings.ApiKey);

            model.ConnectionStatus = result.Success ? "Connected" : "Failed";
            model.TestResultMessage = result.Success
                ? $"Odoo Version: {result.OdooVersion}, Products: {result.ProductCount}, User: {result.LoggedInUser}"
                : result.ErrorMessage;

            if (result.ProductCount != null)
                await UpdateLastSyncDateTimeAsync();
        }
        catch (Exception ex)
        {
            model.ConnectionStatus = "Error";
            model.TestResultMessage = $"An error occurred: {ex.Message}";
        }

        return Json(model);
    }

    #endregion

    #region Syncs

    // The Sync Log page lists the buttons in run order: each sync resolves its parents from the ones before it.

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public Task<IActionResult> SyncStores() => QueueSyncAsync(SyncEventType.Store, "Store");

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public Task<IActionResult> SyncDepartments() => QueueSyncAsync(SyncEventType.Department, "Department");

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public Task<IActionResult> SyncProducts() => QueueSyncAsync(SyncEventType.Product, "Product");

    /// <summary>
    /// Queues a branch sync across all Odoo companies; each company's store is resolved through the
    /// store mapping, and companies with no mapped store are skipped.
    /// </summary>
    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public Task<IActionResult> SyncBranches() => QueueSyncAsync(SyncEventType.Branch, "Branch");

    #endregion

    #region Sync log

    [HttpGet]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public IActionResult SyncLogs()
    {
        var model = new OdooLogEventSearchModel();
        model.SetGridPageSize();

        model.AvailableEventTypes.Add(new SelectListItem { Text = "All", Value = "-1" });
        foreach (var type in new[] { SyncEventType.Store, SyncEventType.Department, SyncEventType.Product, SyncEventType.Branch,
                     SyncEventType.OrderSend, SyncEventType.OrderCancel })
            model.AvailableEventTypes.Add(new SelectListItem { Text = type.ToString(), Value = ((int)type).ToString() });

        return PluginView("SyncLogs", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> LogEventList(OdooLogEventSearchModel searchModel)
    {
        var eventType = searchModel.EventTypeId >= 0
            ? (SyncEventType?)searchModel.EventTypeId
            : null;

        var logEvents = await _odooLogService.GetAllLogEventsAsync(
            eventType: eventType,
            pageIndex: searchModel.Page - 1,
            pageSize: searchModel.PageSize);

        // Batch-load statistics for the current page
        var eventIds = logEvents.Select(e => e.Id).ToList();
        var stats = await _odooLogService.GetEventStatisticsAsync(eventIds);

        var model = await new OdooLogEventListModel().PrepareToGridAsync(searchModel, logEvents, () =>
        {
            return logEvents.SelectAwait(async logEvent =>
            {
                var (traceCount, hasErrors) = stats.TryGetValue(logEvent.Id, out var s) ? s : (0, false);

                var summary = logEvent.FinishedOnUtc.HasValue
                    ? $"Created: {logEvent.TotalCreated} | Updated: {logEvent.TotalUpdated} | Skipped: {logEvent.TotalSkipped} | Processed: {logEvent.TotalProcessed}"
                    : string.Empty;

                var status = logEvent.FinishedOnUtc.HasValue
                    ? (hasErrors ? "Done (with errors)" : "Done")
                    : "Running";

                return new OdooLogEventModel
                {
                    Id = logEvent.Id,
                    Name = logEvent.Name,
                    EventType = logEvent.EventType.ToString(),
                    StartedOn = await _dateTimeHelper.ConvertToUserTimeAsync(logEvent.StartedOnUtc, DateTimeKind.Utc),
                    FinishedOn = logEvent.FinishedOnUtc.HasValue
                        ? await _dateTimeHelper.ConvertToUserTimeAsync(logEvent.FinishedOnUtc.Value, DateTimeKind.Utc)
                        : null,
                    TraceCount = traceCount,
                    HasErrors = hasErrors,
                    Status = status,
                    Summary = summary
                };
            });
        });

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> LogEventTraceList(OdooLogSearchModel searchModel)
    {
        var logItems = await _odooLogService.GetLogsByEventIdAsync(
            searchModel.LogEventId,
            pageIndex: searchModel.Page - 1,
            pageSize: searchModel.PageSize);

        var model = await new OdooLogListModel().PrepareToGridAsync(searchModel, logItems, () =>
        {
            return logItems.SelectAwait(async logItem => new OdooLogModel
            {
                Id = logItem.Id,
                LogLevel = logItem.LogLevel.ToString(),
                ShortMessage = logItem.ShortMessage,
                FullMessage = logItem.FullMessage,
                EntityType = logItem.EntityType,
                OdooEntityId = logItem.OdooEntityId,
                NopEntityId = logItem.NopEntityId,
                CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(logItem.CreatedOnUtc, DateTimeKind.Utc),
                LogEventId = logItem.LogEventId ?? 0
            });
        });

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> LogEventDeleteSelected(ICollection<int> selectedIds)
    {
        if (selectedIds == null || !selectedIds.Any())
            return NoContent();

        var logEvents = new List<OdooLogEvent>();
        foreach (var id in selectedIds)
        {
            var logEvent = await _odooLogService.GetLogEventByIdAsync(id);
            if (logEvent != null)
                logEvents.Add(logEvent);
        }

        await _odooLogService.DeleteLogEventsAsync(logEvents);

        return Json(new { Result = true });
    }

    [HttpPost, ActionName("SyncLogs")]
    [FormValueRequired("clearall")]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> SyncLogClearAll()
    {
        await _odooLogService.ClearLogAsync();

        _notificationService.SuccessNotification("Odoo sync log has been cleared.");

        return RedirectToAction(nameof(SyncLogs));
    }

    [HttpGet]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> SyncLogView(int id)
    {
        var log = await _odooLogService.GetLogByIdAsync(id);
        if (log == null)
            return RedirectToAction(nameof(SyncLogs));

        var model = new OdooLogModel
        {
            Id = log.Id,
            LogLevel = log.LogLevel.ToString(),
            ShortMessage = log.ShortMessage,
            FullMessage = log.FullMessage,
            EntityType = log.EntityType,
            OdooEntityId = log.OdooEntityId,
            NopEntityId = log.NopEntityId,
            CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(log.CreatedOnUtc, DateTimeKind.Utc)
        };

        return PluginView("SyncLogView", model);
    }

    #endregion

    #region Orders

    [HttpGet]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public async Task<IActionResult> Orders()
    {
        var settings = await _settingService.LoadSettingAsync<OdooSettings>();

        var model = new OdooOrderSearchModel { StartDateMissing = !settings.OrderSyncStartDateUtc.HasValue };
        model.SetGridPageSize();

        model.AvailableStores.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Admin.Common.All"), Value = "0" });
        foreach (var store in await _storeService.GetAllStoresAsync())
            model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString() });

        model.AvailableStatuses.Add(new SelectListItem { Text = "All", Value = "-1" });
        model.AvailableStatuses.Add(new SelectListItem { Text = "Not sent", Value = "0" });
        foreach (var status in new[] { OdooOrderStatus.Held, OdooOrderStatus.Failed, OdooOrderStatus.Created, OdooOrderStatus.Pending })
            model.AvailableStatuses.Add(new SelectListItem { Text = status.ToString(), Value = ((int)status).ToString() });

        model.AvailablePaymentStatuses.Add(new SelectListItem { Text = await _localizationService.GetResourceAsync("Admin.Common.All"), Value = "0" });
        foreach (var paymentStatus in Enum.GetValues<PaymentStatus>())
            model.AvailablePaymentStatuses.Add(new SelectListItem
            {
                Text = await _localizationService.GetLocalizedEnumAsync(paymentStatus),
                Value = ((int)paymentStatus).ToString()
            });

        return PluginView("Orders", model);
    }

    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public async Task<IActionResult> OrderList(OdooOrderSearchModel searchModel)
    {
        var (fromUtc, toUtc) = await ToUtcRangeAsync(searchModel);
        var orders = await _orderSyncService.SearchOrdersAsync((OdooOrderListKind)searchModel.ListKind, searchModel.StoreId,
            fromUtc, toUtc, searchModel.StatusId, searchModel.PaymentStatusId, searchModel.Page - 1, searchModel.PageSize);

        var mappings = (await _orderSyncService.GetMappingsAsync(orders.Select(order => order.Id).ToList()))
            .ToLookup(mapping => mapping.NopOrderId);
        var storeNames = (await _storeService.GetAllStoresAsync()).ToDictionary(store => store.Id, store => store.Name);

        var model = await new OdooOrderListModel().PrepareToGridAsync(searchModel, orders, () =>
        {
            return orders.SelectAwait(async order =>
            {
                var rows = mappings[order.Id].OrderBy(mapping => mapping.DestinationKey).ToList();

                return new OdooOrderModel
                {
                    Id = order.Id,
                    CustomOrderNumber = order.CustomOrderNumber,
                    StoreName = storeNames.TryGetValue(order.StoreId, out var storeName) ? storeName : string.Empty,
                    CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(order.CreatedOnUtc, DateTimeKind.Utc),
                    OrderTotal = order.OrderTotal.ToString("0.00", CultureInfo.InvariantCulture),
                    PaymentStatus = await _localizationService.GetLocalizedEnumAsync(order.PaymentStatus),
                    OdooStatus = rows.Count == 0 ? "Not sent" : string.Join(", ", rows.Select(mapping => mapping.Status.ToString()).Distinct()),
                    OdooOrders = string.Join(", ", rows.Where(mapping => !string.IsNullOrEmpty(mapping.OdooOrderName)).Select(mapping => mapping.OdooOrderName)),
                    Message = string.Join(" | ", rows.Where(mapping => !string.IsNullOrEmpty(mapping.Message)).Select(mapping => mapping.Message))
                };
            });
        });

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public async Task<IActionResult> SendSelectedOrders(ICollection<int> selectedIds)
    {
        if (selectedIds == null || selectedIds.Count == 0)
            return NoContent();

        return await QueueSyncAsync(SyncEventType.OrderSend, "Order send", selectedIds.Distinct().ToList());
    }

    /// <summary>
    /// Queues every order the list to send shows with the posted filters, on every page.
    /// </summary>
    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public async Task<IActionResult> SendAllOrders(OdooOrderSearchModel searchModel)
    {
        var (fromUtc, toUtc) = await ToUtcRangeAsync(searchModel);
        var orders = await _orderSyncService.SearchOrdersAsync(OdooOrderListKind.ToSend, searchModel.StoreId, fromUtc, toUtc,
            searchModel.StatusId, searchModel.PaymentStatusId);

        if (orders.Count == 0)
            return Json(new { ConnectionStatus = "Failed", TestResultMessage = "No order to send." });

        return await QueueSyncAsync(SyncEventType.OrderSend, "Order send", orders.Select(order => order.Id).ToList());
    }

    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public Task<IActionResult> CancelOrder(int id) => QueueSyncAsync(SyncEventType.OrderCancel, "Order cancel", new List<int> { id });

    [HttpPost]
    [CheckPermission(OdooPermissionConfigManager.SEND_ORDERS)]
    public async Task<IActionResult> CancelAllOrders()
    {
        var orders = await _orderSyncService.SearchOrdersAsync(OdooOrderListKind.ToCancel);

        if (orders.Count == 0)
            return Json(new { ConnectionStatus = "Failed", TestResultMessage = "No order to cancel." });

        return await QueueSyncAsync(SyncEventType.OrderCancel, "Order cancel", orders.Select(order => order.Id).ToList());
    }

    #endregion
}
```

**34. New file `Views/OdooAdmin/Orders.cshtml`**

```cshtml
@model OdooOrderSearchModel
@using Nop.Plugin.Integration.Odoo.Services

@{
    ViewBag.PageTitle = "Odoo Orders";
    NopHtml.SetActiveMenuItemSystemName(OdooConstants.OrdersMenuSystemName);

    NopHtml.AddScriptParts(ResourceLocation.Footer, "~/Plugins/Integration.Odoo/Content/Admin/Odoo/odoo-admin.js");
}

<div class="odoo-orders-config"
     data-send-selected-url="@Url.Action("SendSelectedOrders", "OdooAdmin")"
     data-send-all-url="@Url.Action("SendAllOrders", "OdooAdmin")"
     data-cancel-url="@Url.Action("CancelOrder", "OdooAdmin")"
     data-cancel-all-url="@Url.Action("CancelAllOrders", "OdooAdmin")"
     data-nothing-selected="@T("Admin.Common.Alert.NothingSelected")"
     style="display: none;"></div>

<div class="content-header clearfix">
    <h1 class="float-left">
        Odoo Orders
    </h1>
    <div class="float-right">
        <button type="button" id="odoo-send-selected" class="btn btn-primary">
            <i class="fas fa-paper-plane"></i> Send selected
        </button>
        <button type="button" id="odoo-send-all" class="btn btn-info ml-1">
            <i class="fas fa-paper-plane"></i> Send all listed
        </button>
    </div>
</div>

<section class="content">
    <div class="container-fluid">
        <div class="form-horizontal">
            <div class="cards-group">
                @if (Model.StartDateMissing)
                {
                    <div class="alert alert-warning" role="alert">
                        No start date is set on the Odoo Settings page, so no order is listed to send.
                    </div>
                }

                <div class="card card-default" id="odoo-orders-result" style="display: none;">
                    <div class="card-body">
                        <span id="odooOrdersStatus" class="badge"></span>
                        <span id="odooOrdersMessage" class="ml-2"></span>
                    </div>
                </div>

                <div class="card card-default card-search">
                    <div class="card-body">
                        <div class="row search-row opened" data-hideAttribute="OdooOrdersPage.HideSearchBlock">
                            <div class="search-text">@T("Admin.Common.Search")</div>
                            <div class="icon-search"><i class="fas fa-magnifying-glass" aria-hidden="true"></i></div>
                            <div class="icon-collapse"><i class="far fa-angle-up" aria-hidden="true"></i></div>
                        </div>
                        <div class="search-body">
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group row">
                                        <div class="col-md-4"><nop-label asp-for="StoreId" /></div>
                                        <div class="col-md-8"><nop-select asp-for="StoreId" asp-items="Model.AvailableStores" /></div>
                                    </div>
                                    <div class="form-group row">
                                        <div class="col-md-4"><nop-label asp-for="StatusId" /></div>
                                        <div class="col-md-8"><nop-select asp-for="StatusId" asp-items="Model.AvailableStatuses" /></div>
                                    </div>
                                    <div class="form-group row">
                                        <div class="col-md-4"><nop-label asp-for="PaymentStatusId" /></div>
                                        <div class="col-md-8"><nop-select asp-for="PaymentStatusId" asp-items="Model.AvailablePaymentStatuses" /></div>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group row">
                                        <div class="col-md-4"><nop-label asp-for="CreatedFrom" /></div>
                                        <div class="col-md-8"><nop-editor asp-for="CreatedFrom" /></div>
                                    </div>
                                    <div class="form-group row">
                                        <div class="col-md-4"><nop-label asp-for="CreatedTo" /></div>
                                        <div class="col-md-8"><nop-editor asp-for="CreatedTo" /></div>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="text-center col-12">
                                    <button type="button" id="search-orders" class="btn btn-primary btn-search">
                                        <i class="fas fa-magnifying-glass"></i>
                                        @T("Admin.Common.Search")
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="card card-default">
                    <div class="card-header">
                        <h3 class="card-title">To send</h3>
                    </div>
                    <div class="card-body">
                        @await Html.PartialAsync("Table", new DataTablesModel
                        {
                            Name = "odoo-orders-grid",
                            UrlRead = new DataUrl("OrderList", "OdooAdmin", null),
                            SearchButtonId = "search-orders",
                            Length = Model.PageSize,
                            LengthMenu = Model.AvailablePageSizes,
                            Filters = new List<FilterParameter>
                            {
                                new FilterParameter(nameof(Model.StoreId)),
                                new FilterParameter(nameof(Model.StatusId)),
                                new FilterParameter(nameof(Model.PaymentStatusId)),
                                new FilterParameter(nameof(Model.CreatedFrom)),
                                new FilterParameter(nameof(Model.CreatedTo)),
                                new FilterParameter(nameof(Model.ListKind), (int)OdooOrderListKind.ToSend)
                            },
                            PrimaryKeyColumn = nameof(OdooOrderModel.Id),
                            ColumnCollection = new List<ColumnProperty>
                            {
                                new ColumnProperty(nameof(OdooOrderModel.Id))
                                {
                                    IsMasterCheckBox = true,
                                    Render = new RenderCheckBox("checkbox_orders"),
                                    ClassName = NopColumnClassDefaults.CenterAll,
                                    Width = "50"
                                },
                                new ColumnProperty(nameof(OdooOrderModel.CustomOrderNumber)) { Title = "Order #", Width = "90" },
                                new ColumnProperty(nameof(OdooOrderModel.StoreName)) { Title = "Store", Width = "150" },
                                new ColumnProperty(nameof(OdooOrderModel.CreatedOn)) { Title = "Placed on", Width = "150", Render = new RenderDate() },
                                new ColumnProperty(nameof(OdooOrderModel.OrderTotal)) { Title = "Total", Width = "80", ClassName = NopColumnClassDefaults.CenterAll },
                                new ColumnProperty(nameof(OdooOrderModel.PaymentStatus)) { Title = "Payment", Width = "100" },
                                new ColumnProperty(nameof(OdooOrderModel.OdooStatus)) { Title = "Odoo status", Width = "110" },
                                new ColumnProperty(nameof(OdooOrderModel.OdooOrders)) { Title = "Odoo orders", Width = "110" },
                                new ColumnProperty(nameof(OdooOrderModel.Message)) { Title = "Message" },
                                new ColumnProperty(nameof(OdooOrderModel.Id))
                                {
                                    Title = T("Admin.Common.View").Text,
                                    Width = "80",
                                    ClassName = NopColumnClassDefaults.Button,
                                    Render = new RenderButtonView(new DataUrl("~/Admin/Order/Edit/"))
                                }
                            }
                        })
                    </div>
                </div>

                <div class="card card-default">
                    <div class="card-header">
                        <h3 class="card-title">Cancelled or deleted in nop, still open in Odoo</h3>
                        <div class="card-tools">
                            <button type="button" id="odoo-cancel-all" class="btn btn-default btn-sm">
                                <i class="fas fa-ban"></i> Cancel all in Odoo
                            </button>
                        </div>
                    </div>
                    <div class="card-body">
                        @await Html.PartialAsync("Table", new DataTablesModel
                        {
                            Name = "odoo-cancel-grid",
                            UrlRead = new DataUrl("OrderList", "OdooAdmin", null),
                            Length = Model.PageSize,
                            LengthMenu = Model.AvailablePageSizes,
                            Filters = new List<FilterParameter>
                            {
                                new FilterParameter(nameof(Model.ListKind), (int)OdooOrderListKind.ToCancel)
                            },
                            ColumnCollection = new List<ColumnProperty>
                            {
                                new ColumnProperty(nameof(OdooOrderModel.CustomOrderNumber)) { Title = "Order #", Width = "90" },
                                new ColumnProperty(nameof(OdooOrderModel.StoreName)) { Title = "Store", Width = "150" },
                                new ColumnProperty(nameof(OdooOrderModel.CreatedOn)) { Title = "Placed on", Width = "150", Render = new RenderDate() },
                                new ColumnProperty(nameof(OdooOrderModel.OdooOrders)) { Title = "Odoo orders", Width = "110" },
                                new ColumnProperty(nameof(OdooOrderModel.OdooStatus)) { Title = "Odoo status", Width = "110" },
                                new ColumnProperty(nameof(OdooOrderModel.Message)) { Title = "Message" },
                                new ColumnProperty(nameof(OdooOrderModel.Id))
                                {
                                    Title = string.Empty,
                                    Width = "140",
                                    ClassName = NopColumnClassDefaults.Button,
                                    Render = new RenderCustom("renderOdooCancelButton")
                                }
                            }
                        })
                    </div>
                </div>
            </div>
        </div>
    </div>
</section>
```

**35. `Content/Admin/Odoo/odoo-admin.js`, lines 119–127**

FIND (lines 119–127):
```javascript
    $(function () {
        var $configure = $('.odoo-configure-config');
        if ($configure.length)
            initConfigure($configure);

        var $syncLog = $('.odoo-synclog-config');
        if ($syncLog.length)
            initSyncLog($syncLog);
    });
```
REPLACE WITH:
```javascript
    // ── Orders page ──────────────────────────────────────────────────────────

    function initOrders($cfg) {
        var $result = $('#odoo-orders-result');

        // Every order action queues a background run and answers { ConnectionStatus, TestResultMessage },
        // or 204 when nothing was selected.
        function queue(url, data) {
            $result.show();
            $('#odooOrdersStatus').removeClass('badge-success badge-danger').text('Loading...');
            $('#odooOrdersMessage').text('');

            $.ajax({
                url: url,
                type: 'POST',
                data: addAntiForgeryToken(data),
                traditional: true,
                success: function (answer, textStatus, jqXHR) {
                    if (jqXHR.status === 204) {
                        setBadge($('#odooOrdersStatus'), 'Nothing selected', false);
                        $('#odooOrdersMessage').text($cfg.data('nothingSelected'));
                        return;
                    }
                    var ok = answer.ConnectionStatus === 'Queued';
                    setBadge($('#odooOrdersStatus'), ok ? 'Queued' : 'Failed', ok);
                    $('#odooOrdersMessage').text(answer.TestResultMessage || answer.error || '');
                },
                error: function (xhr, status, error) {
                    setBadge($('#odooOrdersStatus'), 'Error', false);
                    $('#odooOrdersMessage').text('An error occurred: ' + error);
                }
            });
        }

        // selectedIds is the send grid's checkbox state (nop's Table partial).
        $('#odoo-send-selected').on('click', function () {
            queue($cfg.data('sendSelectedUrl'), { selectedIds: selectedIds });
            return false;
        });

        $('#odoo-send-all').on('click', function () {
            queue($cfg.data('sendAllUrl'), {
                StoreId: $('#StoreId').val(),
                StatusId: $('#StatusId').val(),
                PaymentStatusId: $('#PaymentStatusId').val(),
                CreatedFrom: $('#CreatedFrom').val(),
                CreatedTo: $('#CreatedTo').val()
            });
            return false;
        });

        $('#odoo-cancel-all').on('click', function () {
            queue($cfg.data('cancelAllUrl'), {});
            return false;
        });

        // The grid's rows are redrawn on every page, so the buttons are delegated from the table, which stays.
        $('#odoo-cancel-grid').off('.odoo').on('click.odoo', '.odoo-cancel-order', function () {
            queue($cfg.data('cancelUrl'), { id: $(this).data('id') });
            return false;
        });
    }

    $(function () {
        var $configure = $('.odoo-configure-config');
        if ($configure.length)
            initConfigure($configure);

        var $syncLog = $('.odoo-synclog-config');
        if ($syncLog.length)
            initSyncLog($syncLog);

        var $orders = $('.odoo-orders-config');
        if ($orders.length)
            initOrders($orders);
    });
```

**36. `Content/Admin/Odoo/odoo-admin.js`, lines 12–20**

FIND (lines 12–20):
```javascript
/**
 * Renders the Status column of the sync events grid (named by RenderCustom, so it must be global).
 */
function renderColumnEventInfo(data) {
    if (data === 'Running') return '<span class="badge bg-warning">Running</span>';
    if (data === 'Done (with errors)') return '<span class="badge bg-danger">Done (errors)</span>';
    if (data === 'Done') return '<span class="badge bg-success">Done</span>';
    return $('<div>').text(data || '').html();
}
```
REPLACE WITH:
```javascript
/**
 * Renders the Status column of the sync events grid (named by RenderCustom, so it must be global).
 */
function renderColumnEventInfo(data) {
    if (data === 'Running') return '<span class="badge bg-warning">Running</span>';
    if (data === 'Done (with errors)') return '<span class="badge bg-danger">Done (errors)</span>';
    if (data === 'Done') return '<span class="badge bg-success">Done</span>';
    return $('<div>').text(data || '').html();
}

/**
 * Renders the Cancel in Odoo button of the Orders page's cancel grid (named by RenderCustom, so it must be
 * global). data is the nop order id; the click is handled in initOrders.
 */
function renderOdooCancelButton(data) {
    return '<button type="button" class="btn btn-default odoo-cancel-order" data-id="' + parseInt(data, 10) + '">' +
        '<i class="fas fa-ban"></i> Cancel in Odoo</button>';
}
```

**37. `Content/Admin/Odoo/odoo-admin.js`, lines 1–9** (the header comment)

FIND (lines 1–9):
```javascript
/*
 * Odoo Integration — admin pages.
 *
 * Configure page (.odoo-configure-config): the Run Diagnostics button.
 * Sync Log page (.odoo-synclog-config): the sync buttons, Delete selected, and the grid's status column.
 *
 * Server values come from the config divs (CLAUDE.md §9.14). The actions answer with C# names as declared:
 * Diagnostics returns a model (OdooUrl, ConnectionStatus, ...), the sync actions { ConnectionStatus, TestResultMessage },
 * and [CheckPermission] answers { error } when access is denied.
```
REPLACE WITH:
```javascript
/*
 * Odoo Integration — admin pages.
 *
 * Configure page (.odoo-configure-config): the Run Diagnostics button.
 * Sync Log page (.odoo-synclog-config): the sync buttons, Delete selected, and the grid's status column.
 * Orders page (.odoo-orders-config): Send selected / Send all listed, Cancel in Odoo, Cancel all.
 *
 * Server values come from the config divs (CLAUDE.md §9.14). The actions answer with C# names as declared:
 * Diagnostics returns a model (OdooUrl, ConnectionStatus, ...), the sync and order actions { ConnectionStatus, TestResultMessage },
 * and [CheckPermission] answers { error } when access is denied.
```

**38. `Views/OdooAdmin/Configure.cshtml`, lines 97–99** (a new card after the connection card)

FIND (lines 97–99):
```cshtml
                    </nop-card>

                    @if (!string.IsNullOrEmpty(Model.ConnectionStatus))
```
REPLACE WITH:
```cshtml
                    </nop-card>

                    <nop-card asp-name="odoo-order-settings" asp-icon="fas fa-paper-plane" asp-title="Orders to Odoo" asp-hide-block-attribute-name="OdooSettingsPage.HideOrderSettingsBlock" asp-hide="false" asp-advanced="false">
                        <div class="card-body">
                            <p>
                                Orders are sent from the Orders page. Only orders placed on or after the start date are listed.
                            </p>
                            <hr />

                            <div class="form-group row">
                                <div class="col-md-3">
                                    <nop-label asp-for="OrderSyncStartDate" />
                                </div>
                                <div class="col-md-9">
                                    <nop-editor asp-for="OrderSyncStartDate" />
                                </div>
                            </div>

                            <div class="form-group row">
                                <div class="col-md-3">
                                    <nop-label asp-for="DeliveryProductCode" />
                                </div>
                                <div class="col-md-9">
                                    <input asp-for="DeliveryProductCode" type="text" class="form-control" placeholder="Delivery_007" />
                                </div>
                            </div>

                            <div class="form-group row">
                                <div class="col-md-3">
                                    <nop-label asp-for="ExchangeFeeProductCode" />
                                </div>
                                <div class="col-md-9">
                                    <input asp-for="ExchangeFeeProductCode" type="text" class="form-control" />
                                </div>
                            </div>
                        </div>
                    </nop-card>

                    @if (!string.IsNullOrEmpty(Model.ConnectionStatus))
```

**39. `Views/OdooAdmin/Configure.cshtml`, line 50**

FIND (line 50):
```cshtml
                    <nop-card asp-name="odoo-connection-settings" asp-icon="fas fa-plug" asp-title="Odoo 19 Connection" asp-hide-block-attribute-name="@hideOdooSettingsBlockAttributeName" asp-hide="@hideOdooSettingsBlock" asp-advanced="false">
```
REPLACE WITH:
```cshtml
                    <nop-card asp-name="odoo-connection-settings" asp-icon="fas fa-plug" asp-title="Odoo Connection" asp-hide-block-attribute-name="@hideOdooSettingsBlockAttributeName" asp-hide="@hideOdooSettingsBlock" asp-advanced="false">
```

**40. `Views/OdooAdmin/Configure.cshtml`, line 25**

FIND (line 25):
```cshtml
            Odoo 19 Settings
```
REPLACE WITH:
```cshtml
            Odoo Settings
```

**41. `Infrastructure/EventConsumer.cs`, lines 65–66**

FIND (lines 65–66):
```csharp
        menu.ChildNodes.Add(settingsMenu);
        menu.ChildNodes.Add(syncLogMenu);
```
REPLACE WITH:
```csharp
        menu.ChildNodes.Add(settingsMenu);
        menu.ChildNodes.Add(ordersMenu);
        menu.ChildNodes.Add(syncLogMenu);
```

**42. `Infrastructure/EventConsumer.cs`, line 48**

FIND (line 48):
```csharp
        var syncLogMenu = new AdminMenuItem
```
REPLACE WITH:
```csharp
        var ordersMenu = new AdminMenuItem
        {
            Title = await _localizationService.GetResourceAsync($"{OdooLocaleResources.Prefix}.Menu.Orders"),
            Url = $"/Admin/{OdooConstants.AdminController}/Orders",
            SystemName = OdooConstants.OrdersMenuSystemName,
            Visible = true,
            IconClass = "far fa-paper-plane",
            PermissionNames = new List<string> { OdooPermissionConfigManager.SEND_ORDERS },
        };
        var syncLogMenu = new AdminMenuItem
```

**43. `Infrastructure/OdooLocaleResources.cs`, lines 32–34**

FIND (lines 32–34):
```csharp
        [$"{Prefix}.Fields.ApiKey.Hint"] = "Generate an API key in Odoo: Settings → Technical → API Keys",

        // Sync events grid
```
REPLACE WITH:
```csharp
        [$"{Prefix}.Fields.ApiKey.Hint"] = "Generate an API key in Odoo: Settings → Technical → API Keys",
        [$"{Prefix}.Fields.OrderSyncStartDate"] = "Send orders placed from",
        [$"{Prefix}.Fields.OrderSyncStartDate.Hint"] = "Only orders placed on or after this date are listed to send on the Orders page. Empty: none are listed.",
        [$"{Prefix}.Fields.DeliveryProductCode"] = "Odoo delivery product",
        [$"{Prefix}.Fields.DeliveryProductCode.Hint"] = "Internal reference of the Odoo product used for shipping lines, e.g. Delivery_007.",
        [$"{Prefix}.Fields.ExchangeFeeProductCode"] = "Odoo exchange fee product",
        [$"{Prefix}.Fields.ExchangeFeeProductCode.Hint"] = "Internal reference of the Odoo service product used for exchange fee lines.",

        // Orders page
        [$"{Prefix}.Orders.List.Store"] = "Store",
        [$"{Prefix}.Orders.List.CreatedFrom"] = "Placed from",
        [$"{Prefix}.Orders.List.CreatedTo"] = "Placed to",
        [$"{Prefix}.Orders.List.Status"] = "Odoo status",
        [$"{Prefix}.Orders.List.PaymentStatus"] = "Payment status",

        // Sync events grid
```

**44. `Infrastructure/OdooLocaleResources.cs`, line 22**

FIND (line 22):
```csharp
        [$"{Prefix}.Menu.SyncLogs"] = "Sync Log",
```
REPLACE WITH:
```csharp
        [$"{Prefix}.Menu.Orders"] = "Orders",
        [$"{Prefix}.Menu.SyncLogs"] = "Sync Log",
```

### 7.7 Project, version and tests

**45. `Nop.Plugin.Integration.Odoo.csproj`, lines 45–47**

FIND (lines 45–47):
```xml
		<None Update="Views\OdooAdmin\SyncLogView.cshtml">
			<CopyToOutputDirectory>Always</CopyToOutputDirectory>
		</None>
```
REPLACE WITH:
```xml
		<None Update="Views\OdooAdmin\SyncLogView.cshtml">
			<CopyToOutputDirectory>Always</CopyToOutputDirectory>
		</None>
		<None Update="Views\OdooAdmin\Orders.cshtml">
			<CopyToOutputDirectory>Always</CopyToOutputDirectory>
		</None>
```

**46. `plugin.json`, lines 5 and 11**

FIND (line 11):
```json
  "Description": "Connects NopCommerce to Odoo 19 ERP via the JSON-2 API. Synchronises stores, departments, branches and products (variants, attributes, images)."
```
REPLACE WITH:
```json
  "Description": "Connects NopCommerce to Odoo (19 and later; the client runs Odoo 20) via the JSON-2 API. Synchronises stores, departments, branches and products (variants, attributes, images) from Odoo, and sends orders to Odoo as sale orders."
```
FIND (line 5):
```json
  "Version": "2026_10_26_DATA_1.0",
```
REPLACE WITH:
```json
  "Version": "2026_11_13_DATA_1.1",
```
The DATA version runs the update on the next start: the locale resources and `OrderMappingMigration`.

**47. `CLAUDE.md` §3.2, the `Nop.Plugin.Integration.Odoo` row** (line 286), Purpose column

FIND (in line 286):
```markdown
| `Nop.Plugin.Integration.Odoo` | One-way Odoo 19 → nop sync: stores, departments, products, branches |
```
REPLACE WITH:
```markdown
| `Nop.Plugin.Integration.Odoo` | Odoo 20 (JSON-2 API) ↔ nop: stores, departments, products and branches from Odoo; orders sent to Odoo as sale orders |
```

**48. New file `Tests/CW.Tests/Integration.Odoo/OdooOrderBuilderTests.cs`**

```csharp
using System.Text.Json;
using AwesomeAssertions;
using Nop.Plugin.Integration.Odoo.Services;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// The texts and the <c>sale.order/create</c> values the order send builds. Pure: no nop, no Odoo.
/// </summary>
[TestFixture]
public class OdooOrderBuilderTests
{
    #region Utilities

    private static OdooSaleOrderDraft Draft()
    {
        var draft = new OdooSaleOrderDraft
        {
            DestinationKey = 7,
            Origin = "nop 1042",
            CustomerPartnerId = 31,
            ShippingPartnerId = 61,
            OrderDateUtc = new DateTime(2026, 11, 13, 9, 5, 0, DateTimeKind.Utc)
        };
        draft.Lines.Add(new OdooSaleOrderLineDraft { ProductId = 61, Quantity = 2, PriceUnit = 30m, Name = "POLO (S)" });
        draft.Lines.Add(new OdooSaleOrderLineDraft { ProductId = 1, Quantity = 1, PriceUnit = 9.5m, Name = "Standard", IsDelivery = true });

        return draft;
    }

    #endregion

    [Test]
    public void Origin_is_the_order_number_with_the_destination_only_when_there_are_several()
    {
        OdooOrderBuilder.Origin("1042", 7, severalDestinations: false).Should().Be("nop 1042");
        OdooOrderBuilder.Origin("1042", 7, severalDestinations: true).Should().Be("nop 1042 / 7");
    }

    [Test]
    public void HtmlToText_keeps_one_line_per_break_and_decodes_entities()
    {
        OdooOrderBuilder.HtmlToText("Size: S<br />Print name: J. Smith &amp; Co<br><br/>")
            .Should().Be("Size: S\nPrint name: J. Smith & Co");

        OdooOrderBuilder.HtmlToText(null).Should().BeEmpty();
    }

    [Test]
    public void PackNameInfo_leaves_out_empty_parts()
    {
        OdooOrderBuilder.PackNameInfo("J. Smith", "Depot East", "", "4100")
            .Should().Be("For: J. Smith · Branch: Depot East · CC: 4100");

        OdooOrderBuilder.PackNameInfo(null, " ", null, null).Should().BeEmpty();
    }

    [Test]
    public void LineName_puts_name_attributes_packname_and_PO_on_their_own_lines()
    {
        OdooOrderBuilder.LineName("POLO", "Size: S<br />Print name: JS", "For: J. Smith", "PO-7")
            .Should().Be("POLO\nSize: S\nPrint name: JS\nFor: J. Smith\nPO: PO-7");

        OdooOrderBuilder.LineName("POLO", "", null, null).Should().Be("POLO");
    }

    [Test]
    public void CommonPurchaseOrder_is_set_only_when_every_line_has_the_same_one()
    {
        OdooOrderBuilder.CommonPurchaseOrder(new[] { "PO-7", " PO-7 " }).Should().Be("PO-7");
        OdooOrderBuilder.CommonPurchaseOrder(new[] { "PO-7", "PO-8" }).Should().BeNull();
        OdooOrderBuilder.CommonPurchaseOrder(new[] { "PO-7", null! }).Should().BeNull();
        OdooOrderBuilder.CommonPurchaseOrder(Array.Empty<string>()).Should().BeNull();
    }

    [Test]
    public void UnitPrice_is_the_line_total_over_the_quantity_to_cents()
    {
        OdooOrderBuilder.UnitPrice(10m, 3).Should().Be(3.33m);
        OdooOrderBuilder.UnitPrice(0.05m, 2).Should().Be(0.03m);
        OdooOrderBuilder.UnitPrice(10m, 0).Should().Be(0m);
    }

    [Test]
    public void CreateValues_has_the_three_partners_origin_date_and_lines()
    {
        var values = OdooOrderBuilder.CreateValues(Draft());

        values["partner_id"].Should().Be(31);
        values["partner_invoice_id"].Should().Be(31);
        values["partner_shipping_id"].Should().Be(61);
        values["origin"].Should().Be("nop 1042");
        values["date_order"].Should().Be("2026-11-13 09:05:00");
        values.Should().NotContainKey("client_order_ref");
        values.Should().NotContainKey("note");
    }

    [Test]
    public void CreateValues_sends_the_PO_and_the_note_only_when_there_are_some()
    {
        var draft = Draft();
        draft.ClientOrderRef = "PO-7";
        draft.NoteLines.Add("nop order 1042.");
        draft.NoteLines.Add("Subsidy <paid> 10.00");

        var values = OdooOrderBuilder.CreateValues(draft);

        values["client_order_ref"].Should().Be("PO-7");
        values["note"].Should().Be("<p>nop order 1042.</p><p>Subsidy &lt;paid&gt; 10.00</p>");
    }

    [Test]
    public void CreateValues_serialises_lines_as_one2many_create_commands()
    {
        var json = JsonSerializer.Serialize(OdooOrderBuilder.CreateValues(Draft()));
        using var document = JsonDocument.Parse(json);
        var lines = document.RootElement.GetProperty("order_line");

        lines.GetArrayLength().Should().Be(2);

        var first = lines[0];
        first[0].GetInt32().Should().Be(0);
        first[1].GetInt32().Should().Be(0);
        first[2].GetProperty("product_id").GetInt32().Should().Be(61);
        first[2].GetProperty("product_uom_qty").GetDecimal().Should().Be(2m);
        first[2].GetProperty("price_unit").GetDecimal().Should().Be(30m);
        first[2].GetProperty("sequence").GetInt32().Should().Be(10);
        first[2].TryGetProperty("is_delivery", out _).Should().BeFalse();

        lines[1][2].GetProperty("is_delivery").GetBoolean().Should().BeTrue();
    }
}
```

### 7.8 Added by the review of 2026-11-16

The review's other fixes are folded into the changes above (1, 22, 23, 24, 33, 34, 48). These three are new. Change 49
is in a file changes 18–21 already touched: its line numbers are those of the file after them.

**49. `Services/OdooApiService.cs`, lines 204–221** (Odoo's own error text reaches the log and the Orders page)

FIND (lines 204–221):
```csharp
    private static string ParseOdooError(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var msg))
                    return msg.GetString();
                if (error.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("message", out var dataMsg))
                    return dataMsg.GetString();
            }
        }
        catch { /* ignore parse failures */ }

        return null;
    }
```
REPLACE WITH:
```csharp
    /// <summary>
    /// The message of an Odoo error answer, or null when the body is not one. The JSON-2 API answers
    /// <c>{ "name", "message", "arguments", … }</c> at the top level; the older JSON-RPC shape nests the
    /// message under <c>error</c>.
    /// </summary>
    internal static string ParseOdooError(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                return message.GetString();

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("message", out var msg))
                    return msg.GetString();
                if (error.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("message", out var dataMsg))
                    return dataMsg.GetString();
            }
        }
        catch { /* ignore parse failures */ }

        return null;
    }
```

**50. New file `Tests/CW.Tests/Integration.Odoo/OdooApiErrorTests.cs`**

```csharp
using AwesomeAssertions;
using Nop.Plugin.Integration.Odoo.Services;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// What the sync log and the Orders page show when Odoo refuses a call: Odoo's own message, in either error shape.
/// </summary>
[TestFixture]
public class OdooApiErrorTests
{
    [Test]
    public void A_JSON2_error_gives_its_top_level_message()
    {
        const string body = """
            {"name": "odoo.exceptions.ValidationError", "message": "Not enough stock for POLO (S)",
             "arguments": ["Not enough stock for POLO (S)"], "context": {}, "debug": "Traceback ..."}
            """;

        OdooApiService.ParseOdooError(body).Should().Be("Not enough stock for POLO (S)");
    }

    [Test]
    public void A_JSON_RPC_error_gives_the_nested_message()
    {
        OdooApiService.ParseOdooError("""{"error": {"message": "Odoo Server Error", "data": {"message": "Access denied"}}}""")
            .Should().Be("Odoo Server Error");

        OdooApiService.ParseOdooError("""{"error": {"data": {"message": "Access denied"}}}""")
            .Should().Be("Access denied");
    }

    [Test]
    public void A_body_that_is_not_an_error_gives_null()
    {
        OdooApiService.ParseOdooError("[1, 2]").Should().BeNull();
        OdooApiService.ParseOdooError("{}").Should().BeNull();
        OdooApiService.ParseOdooError("<html>Bad gateway</html>").Should().BeNull();
    }
}
```

**51. New file `Tests/CW.Tests/Integration.Odoo/OrderSyncServiceTests.cs`**

```csharp
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.Integration.Odoo;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Models.Responses;
using Nop.Plugin.Integration.Odoo.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// The rules of the order send that protect against a second Odoo order and against a status nop only assumed:
/// what is held, what a retry does, and that confirmed and cancelled are Odoo's word, not ours. Odoo is a fake
/// with one sale order (id 900) whose state the tests drive; a plain order with one line is sent.
/// </summary>
[TestFixture]
public class OrderSyncServiceTests
{
    #region Utilities

    private const int OdooOrderId = 900;

    private List<OdooOrderMapping> _mappings = null!;
    private Mock<IOdooApiService> _odoo = null!;
    private Mock<IBranchService> _branches = null!;
    private Order _order = null!;
    private Product _product = null!;
    private OrderSyncService _service = null!;

    // The fake Odoo order
    private string _odooState = null!;
    private decimal _odooUntaxed;
    private bool _confirmWorks;
    private bool _cancelWorks;
    private string? _confirmError;

    [SetUp]
    public void SetUp()
    {
        _mappings = new List<OdooOrderMapping>();
        _odooState = "draft";
        _odooUntaxed = 60m;
        _confirmWorks = true;
        _cancelWorks = true;
        _confirmError = null;

        _order = new Order { Id = 5, CustomOrderNumber = "1042", CustomerId = 9, OrderStatus = OrderStatus.Processing, PickupInStore = true };
        _product = new Product { Id = 3, Name = "Polo", ManufacturerPartNumber = "ODOO_77" };
        var items = new List<OrderItem> { new() { Id = 50, OrderId = 5, ProductId = 3, Quantity = 2, PriceExclTax = 60m } };

        var mappingRepository = new Mock<IRepository<OdooOrderMapping>>();
        mappingRepository.Setup(repository => repository.Table).Returns(() => _mappings.AsQueryable());
        mappingRepository.Setup(repository => repository.InsertAsync(It.IsAny<OdooOrderMapping>(), It.IsAny<bool>()))
            .Returns((OdooOrderMapping mapping, bool _) =>
            {
                mapping.Id = _mappings.Count + 1;
                _mappings.Add(mapping);
                return Task.CompletedTask;
            });

        var orders = new Mock<IOrderService>();
        orders.Setup(service => service.GetOrderByIdAsync(5)).ReturnsAsync(() => _order);
        orders.Setup(service => service.GetOrderItemsAsync(5, null, null, 0)).ReturnsAsync(items);

        var products = new Mock<IProductService>();
        products.Setup(service => service.GetProductByIdAsync(3)).ReturnsAsync(() => _product);

        var genericAttributes = new Mock<IGenericAttributeService>();
        genericAttributes
            .Setup(service => service.GetAttributeAsync<int>(It.IsAny<BaseEntity>(), OdooConstants.ProductVariantIdAttribute, 0, 0))
            .ReturnsAsync(61);

        _branches = new Mock<IBranchService>();
        _branches.Setup(service => service.GetBranchByCustomerIdAsync(9))
            .ReturnsAsync(new Branch { Id = 1, Name = "Head Office", CardCode = "31" });

        var exchanges = new Mock<IExchangeService>();
        exchanges.Setup(service => service.GetReturnRequestItemsForOrderItemsAsync(It.IsAny<int[]>()))
            .ReturnsAsync(new List<ReturnRequestItem>());

        _odoo = new Mock<IOdooApiService>();
        _odoo.Setup(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>())).ReturnsAsync(OdooOrderId);
        _odoo.Setup(api => api.GetSaleOrderAsync(OdooOrderId))
            .ReturnsAsync(() => new OdooSaleOrderResponse { Id = OdooOrderId, Name = "S00900", State = _odooState, AmountUntaxed = _odooUntaxed });
        _odoo.Setup(api => api.ConfirmSaleOrderAsync(OdooOrderId)).Returns(() =>
        {
            if (_confirmError != null)
                throw new OdooApiException(_confirmError);
            if (_confirmWorks)
                _odooState = "sale";
            return Task.CompletedTask;
        });
        _odoo.Setup(api => api.CancelSaleOrderAsync(OdooOrderId)).Returns(() =>
        {
            if (_cancelWorks)
                _odooState = "cancel";
            return Task.CompletedTask;
        });

        _service = new OrderSyncService(
            Mock.Of<IAddressService>(),
            _branches.Object,
            Mock.Of<ICountryService>(),
            Mock.Of<ICWOrderService>(),
            exchanges.Object,
            genericAttributes.Object,
            _odoo.Object,
            Mock.Of<IOdooLogService>(),
            orders.Object,
            Mock.Of<IPackNameService>(),
            Mock.Of<IProductAttributeParser>(),
            products.Object,
            mappingRepository.Object,
            Mock.Of<IRepository<Order>>(),
            Mock.Of<IStateProvinceService>(),
            new OdooSettings());
    }

    private Task<OrderSyncResult> SendAsync() => _service.SendOrdersAsync(1, new[] { 5 });

    private OdooOrderMapping Mapping => _mappings.Single();

    #endregion

    [Test]
    public async Task A_sendable_order_is_created_confirmed_and_recorded()
    {
        IDictionary<string, object>? sent = null;
        _odoo.Setup(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()))
            .Callback((IDictionary<string, object> values) => sent = values)
            .ReturnsAsync(OdooOrderId);

        var result = await SendAsync();

        result.Should().Be(new OrderSyncResult(Created: 1, Updated: 0, Skipped: 0));
        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        Mapping.OdooOrderId.Should().Be(OdooOrderId);
        Mapping.OdooOrderName.Should().Be("S00900");
        Mapping.Message.Should().BeNull();

        sent.Should().NotBeNull();
        sent!["origin"].Should().Be("nop 1042");
        sent["partner_id"].Should().Be(31);
        // A pickup ships to the company itself
        sent["partner_shipping_id"].Should().Be(31);
    }

    [Test]
    public async Task An_order_Odoo_already_has_under_its_origin_is_not_created_again()
    {
        _odooState = "sale";
        _odoo.Setup(api => api.FindSaleOrderByOriginAsync("nop 1042"))
            .ReturnsAsync(new OdooSaleOrderResponse { Id = OdooOrderId, Name = "S00900", State = "sale", AmountUntaxed = 60m });

        var result = await SendAsync();

        result.Should().Be(new OrderSyncResult(Created: 0, Updated: 1, Skipped: 0));
        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        Mapping.OdooOrderId.Should().Be(OdooOrderId);
        _odoo.Verify(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()), Times.Never);
        _odoo.Verify(api => api.ConfirmSaleOrderAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task A_failed_confirmation_keeps_the_Odoo_id_and_the_next_send_confirms_without_creating()
    {
        _confirmError = "Not enough stock";

        await SendAsync();

        Mapping.Status.Should().Be(OdooOrderStatus.Failed);
        Mapping.Message.Should().Be("Not enough stock");
        Mapping.OdooOrderId.Should().Be(OdooOrderId);

        _confirmError = null;
        var result = await SendAsync();

        result.Should().Be(new OrderSyncResult(Created: 0, Updated: 1, Skipped: 0));
        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        _odoo.Verify(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()), Times.Once);
    }

    [Test]
    public async Task An_order_Odoo_leaves_as_a_quotation_is_failed_not_confirmed()
    {
        _confirmWorks = false;

        await SendAsync();

        Mapping.Status.Should().Be(OdooOrderStatus.Failed);
        Mapping.Message.Should().Contain("did not confirm");
        Mapping.OdooOrderId.Should().Be(OdooOrderId);
    }

    [Test]
    public async Task A_customer_in_no_branch_holds_the_order()
    {
        _branches.Setup(service => service.GetBranchByCustomerIdAsync(9)).ReturnsAsync((Branch)null!);

        var result = await SendAsync();

        result.Should().Be(new OrderSyncResult(Created: 0, Updated: 0, Skipped: 1));
        Mapping.Status.Should().Be(OdooOrderStatus.Held);
        Mapping.Message.Should().Contain("no branch");
        _odoo.Verify(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()), Times.Never);
    }

    [Test]
    public async Task A_product_that_did_not_come_from_Odoo_holds_the_order()
    {
        _product.ManufacturerPartNumber = null;

        await SendAsync();

        Mapping.Status.Should().Be(OdooOrderStatus.Held);
        Mapping.Message.Should().Contain("'Polo' is not linked to an Odoo product variant");
        _odoo.Verify(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()), Times.Never);
    }

    [Test]
    public async Task A_total_that_differs_from_what_nop_charged_is_kept_as_a_warning()
    {
        _odooUntaxed = 59.98m;

        await SendAsync();

        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        Mapping.Message.Should().Contain("59.98").And.Contain("60.00");
    }

    [Test]
    public async Task A_cancel_is_recorded_only_when_Odoo_says_the_order_is_cancelled()
    {
        await SendAsync();
        _order.OrderStatus = OrderStatus.Cancelled;

        _cancelWorks = false;
        var refused = await _service.CancelOrdersAsync(1, new[] { 5 });

        refused.Should().Be(new OrderSyncResult(Created: 0, Updated: 0, Skipped: 1));
        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        Mapping.Message.Should().Contain("still 'sale'");

        _cancelWorks = true;
        var cancelled = await _service.CancelOrdersAsync(1, new[] { 5 });

        cancelled.Should().Be(new OrderSyncResult(Created: 0, Updated: 1, Skipped: 0));
        Mapping.Status.Should().Be(OdooOrderStatus.Cancelled);
        Mapping.Message.Should().BeNull();
    }
}
```

**52. New file `Infrastructure/OdooPermissionConfigManager.cs`** (decision B4)

```csharp
using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Nop.Plugin.Integration.Odoo.Infrastructure;

/// <summary>
/// The plugin's own permissions. nop adds a permission it does not have yet when the application starts.
/// </summary>
public class OdooPermissionConfigManager : IPermissionConfigManager
{
    /// <summary>
    /// Open the Orders page, send orders to Odoo and cancel them there. The plugin's settings and sync log stay
    /// under Manage plugins.
    /// </summary>
    public const string SEND_ORDERS = "Integration.Odoo.SendOrders";

    /// <summary>
    /// Gets all permission configurations
    /// </summary>
    public IList<PermissionConfig> AllConfigs => new List<PermissionConfig>
    {
        new("Admin area. Odoo. Send orders to Odoo", SEND_ORDERS, nameof(StandardPermission.Orders), NopCustomerDefaults.AdministratorsRoleName)
    };
}
```

**53. `Infrastructure/EventConsumer.cs`, lines 63, 46 and 2** (each menu item names its permission, so a role sees only
the pages it may open; line numbers are those of the file after changes 41 and 42)

FIND (line 63):
```csharp
            IconClass = "far fa-list-alt",
```
REPLACE WITH:
```csharp
            IconClass = "far fa-list-alt",
            PermissionNames = new List<string> { StandardPermission.Configuration.MANAGE_PLUGINS },
```
FIND (line 46):
```csharp
            IconClass = "far fa-dot-circle",
```
REPLACE WITH:
```csharp
            IconClass = "far fa-dot-circle",
            PermissionNames = new List<string> { StandardPermission.Configuration.MANAGE_PLUGINS },
```
FIND (line 2):
```csharp
using Nop.Services.Plugins;
```
REPLACE WITH:
```csharp
using Nop.Services.Plugins;
using Nop.Services.Security;
```

**54. `Tests/CW.Tests/Integration.Odoo/SyncQueueTests.cs`, line 88** (found when the spec was applied: the queue's new
`entityIds` parameter sits before the cancellation token, which this test passed by position)

FIND (line 88):
```csharp
        var act = async () => await queue.TryEnqueueSyncAsync(SyncEventType.Product, cts.Token);
```
REPLACE WITH:
```csharp
        var act = async () => await queue.TryEnqueueSyncAsync(SyncEventType.Product, cancellationToken: cts.Token);
```

### 7.9 After the build

- Build the solution, run `dotnet test Tests/CW.Tests -p:SolutionDir=<repo root>` (the 14 Odoo cases become 34).
- The new permission *Admin area. Odoo. Send orders to Odoo* is added when the site starts, for Administrators. Grant
  it to the roles that send orders (Customers › Access control list).
- Empty `Presentation/Nop.Web/wwwroot/bundles` (§9.11 trap 1: `odoo-admin.js` changed).
- Restart the site: the DATA version runs the plugin update (resources, `Odoo_OrderMapping`).
- Once approved, fold this document into the plugin reference (`References/Odoo_Plugin_Reference.md`: §1 becomes
  two-way, a section on the order send, its open items) and move it to `CWPlans/Deprecated/` (§0.4).

---

## 8. Test plan

On the local site (`CLAUDE.local.md`) against the client's Odoo 20 test instance. Creating sale orders and contacts in
the test instance is part of the test: confirm with the developer before the first run, and note in §5.1 of
`CWPlans/Odoo_Port_Spec.md` what was created.

| # | Case | Steps | Expected |
|---|---|---|---|
| T1 | Build and unit tests | Build; run the CW tests | 0 errors; all pass, `OdooOrderBuilderTests`, `OdooApiErrorTests` and `OrderSyncServiceTests` included |
| T2 | Update | Restart the site | Plugin version `2026_11_13_DATA_1.1`; table `Odoo_OrderMapping` with its unique index; the new resources present; menu *Odoo › Orders* |
| T3 | Labels | Open Configure | Titles read "Odoo Settings", "Odoo Connection"; card *Orders to Odoo* |
| T4 | Settings | Set the delivery product `Delivery_007`; save; reopen | Value kept; no start-date field (§4.2) |
| T5 | Every order listed | Open Orders | Every order is listed with its sync state; a never-sent order shows *Sync* in its row, a sent one no button |
| T6 | Variant id | Run *Sync Products* | Each product of a single-variant Odoo template has `Odoo.ProductVariantId` (GenericAttribute); products with variants have 0 |
| T7 | Plain order | Place an order for a customer in a synced branch; Orders → select → *Send selected*; after the run, refresh | Sync Log *Order Send* run; Odoo order on the head-office company, delivery = the branch's Odoo delivery address, origin `nop {n}`, lines priced as nop (excl. tax), GST added, a *Standard delivery* line; state *Sales Order*; row leaves the send grid |
| T8 | Price kept (§5 V3) | In Odoo, open the T7 order | Line prices are nop's, not the client price list's; if not, the row's message shows the totals differ |
| T9 | Packname order, two destinations | Manager orders for two packnames shipping to different addresses | Two Odoo orders, origins `nop {n} / {key}`; each line names For / Branch / Dept / CC; the first carries the order notes, the second points to it |
| T10 | PO | Give both packnames the same PO; then different POs | Same: `client_order_ref` = the PO. Different: each line ends "PO: …" |
| T11 | Print name | Order a product with the print-name attribute | The line text has "Print name: …" |
| T12 | Home address | Order to an address not in Odoo | A *delivery* contact under the company is created (Sync Log trace); a second order to the same address reuses it |
| T13 | Held: no branch | Order by a customer with no branch | Status *Held*, message says so; nothing in Odoo |
| T14 | Held: product not from Odoo | Order a nop-only product | Held with the product named |
| T15 | Held: shipping, no product | Clear the delivery product; send an order with shipping | Held with the reason; set it again, resend: sent |
| T16 | Retry after lost answer | Send; in SQL delete its `Odoo_OrderMapping` row; send again | No second Odoo order: the trace says it found it by origin; the row is *Confirmed* again |
| T17 | Failed confirm | Archive the delivery product in Odoo between create and confirm, or use an order Odoo refuses | *Failed*, and the message is Odoo's own text, not "HTTP 4xx"; Odoo id kept; resend confirms without creating |
| T18 | Exchange order | Complete an exchange that charges a fee; set the fee product; send | Note "Exchange replacement order"; one fee line per fee type |
| T19 | Subsidy | Send a packname order paid partly by subsidy and store credit | Note lists subsidy (goods, shipping), store credit and the customer's share |
| T20 | Sync all listed | Several orders listed, some already sent; filter by store; *Sync all listed* | Only that store's listed orders that are not yet sent are queued; the trace names each |
| T20a | Row Sync | Press *Sync* on one never-sent order | Only that order is queued and sent; after a search its row reads *Sent* and the button is gone |
| T21 | Busy | *Sync all listed* twice quickly; then, while a run is going, press it again after the first answer | The page sends one request: while it runs every order button (top and rows) is disabled and a press is ignored, and the buttons come back with the answer. A press after the answer, while the run still goes, answers that an order send is already queued or running |
| T22 | Cancel | Cancel a sent order in nop; Orders, filter *To cancel in Odoo*; press *Cancel in Odoo* in its row; first answer *No, cancel*, then press it again and answer *Yes* | nop's *Are you sure?* dialog, with "The order's sales orders in Odoo will be cancelled…"; *No, cancel* sends nothing; *Yes*: Odoo order(s) cancelled, the row reads *Cancelled in Odoo* with no button |
| T22a | Deleted order | Delete (not cancel) a sent order in nop; open Orders | It is listed as *To cancel in Odoo*; its row's *Cancel in Odoo* cancels its Odoo order(s) |
| T22b | Cancel refused | Cancel in nop an order whose Odoo delivery is already done | The row stays *To cancel in Odoo* with Odoo's reason; nothing is marked cancelled |
| T22c | Totals warning | Send two orders whose line total does not divide evenly: 3 units for 10.00 (3 × 3.33 = 9.99), then 6 units for 10.00 (6 × 1.67 = 10.02) | Both sent and confirmed. The first has no message: a difference of one cent is accepted (decided 2026-11-18). The second's message says Odoo's total before tax 10.02 differs from nop's 10.00 |
| T23 | Feature off | A store with PackName, Subsidy and Exchanges off: send a plain order | Sent as a plain order: one destination, no packname line text, no subsidy note |
| T24 | Permission | Give a role *Send orders to Odoo* but not *Manage plugins*; sign in with it. Then a role with neither | First: the Odoo menu shows Orders only, the page works and sends; Settings and Sync Log are refused. Second: no Odoo menu, Orders refused by `[CheckPermission]` |
| T25 | Payment status | On Orders, filter by payment status *Paid*, then *Pending*; *Sync all listed* with *Paid* | The column shows each order's payment status; each filter lists only its orders; only the paid ones not yet sent are queued |

### 8.1 Results (2026-11-18)

Run on the local site against the client's Odoo 20 test instance, with the developer's go-ahead. The nop orders are
test data, not storefront checkouts: the local site had one order, no packnames and no ship-enabled products, so
`CWPlans/TestData/Odoo_Order_Sync_TestOrders.sql` (gitignored, like the demo script) builds TEST-07 … TEST-22C from
order 1's header with their own lines, shipping, totals, addresses, packname rows and settlement row. Odoo's side was
read from the plugin's own record (Sync Log traces, `Odoo_OrderMapping`, the confirm and cancel read-backs); the Odoo
order forms were then opened in Odoo the same day (T7–T11, T19).

| Test | Result |
|---|---|
| T6 | **Passed.** *Sync Products*: the seven single-variant products (Beanie, PAPR Respirator, STKS 420, STKS410, WS4001, WS6030 and the unpublished Donation / Expenses) got `Odoo.ProductVariantId`; products with variants have none |
| T7 | **Passed.** Order 1 → S00003; TEST-07 → S00005, created and confirmed (state read back *sale*). In Odoo: customer and invoice address *CW Test A&L Pty Ltd* (partner 57), delivery address its existing contact *CW Test A&L Site* (no contact created), origin `nop TEST-07`, lines Beanie 2 × 50.00 and STKS 420 1 × 10.00 with 10% GST, a `[Delivery_007] Standard delivery` line named "T4 Courier" at 6.82, untaxed 116.82, total 128.50 as in nop; the note gives the nop order, its UTC date and "Paid in nop (Payments.CheckMoneyOrder)". **Order date:** Odoo shows the confirmation time (09:35 UTC), not nop's placement (09:34): confirming replaces `date_order`, as B7 expected; nop's date is in the note. Confirming also created the Odoo delivery (picking) |
| T8 | **Passed.** Every order is on the *Default (AUD)* price list with nop's unit prices; no totals warning anywhere |
| T9 | **Passed.** TEST-09 → S00008 (origin `nop TEST-09 / 1`, delivery *CW Test A&L Site*) with Alice's two lines and the order notes ("sent as 2 Odoo orders, one per delivery address…"), and S00009 (delivery *Jane Tester*, the home contact) with Bob's line and "Part of nop order TEST-09; its notes are on the Odoo order with origin 'nop TEST-09 / 1'". Each has its own delivery line. Every line reads "For: … · Branch: CW Test A&L Site · Dept: Workshop · CC: CC-01" |
| T10 | **Passed.** Same PO: S00008 and S00009 have Customer Reference `PO-100` (Odoo titles them "S00008 (PO-100)") and no PO in the lines. Different POs: S00007 (TEST-10, one destination) has no Customer Reference and its lines end "PO: PO-100" and "PO: PO-200" |
| T11 | **Passed.** S00008's line reads "Cup Respirator (S)", then "Size: S", "Print name: ALICE" and the For line |
| T12 | **Passed.** TEST-12A created delivery contact 72 (Jane Tester) under company 57 (trace); TEST-09's home destination in the same run reused it; TEST-12B, sent alone later, found it in Odoo and created none |
| T13 | **Passed.** TEST-13 (customer in no branch): *Held*, "The customer is in no branch…"; nothing in Odoo |
| T14 | **Passed** with a variant-less line (Cup Respirator without a combination) rather than a nop-only product, which the site does not have: *Held*, "'Cup Respirator' is not linked to an Odoo product variant. Nothing to send…" |
| T15 | **Passed.** Delivery product cleared → TEST-15 *Held*, "Shipping is charged, but no Odoo delivery product is set…"; set again, *Held: sync again* → S00011 |
| T16 | **Passed.** TEST-07's mapping row deleted, sent again: "Odoo already has S00005 with origin 'nop TEST-07'; it is used", no second order, row *Confirmed* |
| T17 | **Not run** (needs a change in Odoo between create and confirm) |
| T18 | **Not run**: no exchange fee product in Odoo (A14) |
| T19 | **Passed.** S00008's note: "Subsidy paid 30.00 of the goods (incl. tax)", "Store credit paid 5.00 (incl. tax)", "The customer's share in nop: 79.00 (incl. tax)". Odoo's own total is the goods and shipping of that destination (84.50) by design: the settlement is in the note, not in the lines |
| T20 | **Passed.** Store filter on store 3, *Sync all listed*: one order queued, TEST-22C → S00004 |
| T20a | **Passed.** Row *Sync* on TEST-12B queued that order only → S00010; the row then read *Sent* with no button |
| T21 | **Passed.** Server: a second request while a run is queued or running answers *Busy* ("Order send sync is already queued or running…"). Page: at first both answers wrote the same message box and the last to arrive won (once the *Busy* answer was hidden by the *Queued* one). Fixed the same day in `odoo-admin.js` `queue()`: while a request runs, *Sync selected*, *Sync all listed* and the row buttons are disabled and a press is ignored; `complete` enables them again. Re-run with a double click on *Sync all listed*: buttons disabled during the request, one request sent, one *Queued* answer, buttons enabled after (run 41 held TEST-13 and TEST-14, nothing written to Odoo) |
| T22 | **Passed.** TEST-07 cancelled in nop → *Cancel in Odoo* → S00005 cancelled (state read back) → row *Cancelled in Odoo*, no button. The button had no confirmation; one was added the same day (version `2026_11_18_DATA_1.4`: nop's `nop-action-confirmation` dialog on the Orders page, resource `Integration.Odoo.Orders.CancelConfirm`, `odoo-admin.js` keeps the order id until *Yes*). Re-run on TEST-22D: the dialog shows the text; *No, cancel* posted nothing and started no run; *Yes* cancelled S00012 |
| T22a | **Passed.** TEST-12B deleted in nop → listed as *Deleted*, *Cancel in Odoo* → S00010 cancelled; the order then leaves the list |
| T22b | **Not run** (needs a delivery validated in Odoo) |
| T22c | **Passed** (test case rewritten 2026-11-18: one cent is accepted, decided the same day). TEST-22C (3 units for 10.00, Odoo 9.99) → S00004, no message. TEST-22D (6 units for 10.00, Odoo 10.02) → S00012, confirmed, message "Odoo's total before tax 10.02 differs from nop's 10.00." (also a warning trace) |
| T23 | **Passed.** TEST-22C's store (store 3) had PackName, Subsidy and Exchanges off: sent as a plain order with one destination |
| T24 | **Not run**: needs a second admin account to sign in with |
| T25 | **Passed.** *Pending* lists TEST-13, TEST-14 and order 1; *Paid* lists the paid orders only; *Sync all listed* with *Paid* queued the four unsent paid ones (TEST-07, -09, -10, -12A) |

**Left in Odoo (test instance):** sale orders S00003–S00012 (S00005, S00010 and S00012 cancelled, the rest confirmed sales
orders) and delivery contact 72 (Jane Tester, 12 Home Street, Brisbane) under partner 57. **Left in nop:** the
TEST-* orders, the *TEST Odoo packnames* list with three packnames, customer 28 linked to branch 2, and PackName /
Subsidy / Exchanges switched off for store 3; the script's CLEAN-UP block removes them (not the Odoo side).

**Findings:** (1) T22c — decided 2026-11-18: keep the one-cent tolerance, test case rewritten; (2) *Cancel in Odoo* without a confirmation — confirmation added 2026-11-18 (T22); (3) the
order page's message box when two answers race — buttons disabled while a request runs, 2026-11-18 (T21). All three are settled.
