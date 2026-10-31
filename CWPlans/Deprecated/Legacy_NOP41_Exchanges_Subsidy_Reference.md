# Legacy nopCommerce 4.1 — Exchanges, Returns, Credit Notes & Subsidy (TIG plugins)

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

**What this is.** A description of how the **original nopCommerce 4.1 system** works, read directly from
the `Nop.Plugin.TIG.Base` / `Nop.Plugin.TIG.Exchanges` / `Nop.Plugin.TIG.Subsidy` source. This code does
not run in TIG49 — it is a reference copy, kept so the 4.9 rebuild can be checked against the behaviour
the business has actually relied on for ~10 years.

**Why it matters.** The 4.9 port (`Nop.Plugin.CW.Core`) carried across the *structure* of this system but
not all of its *behaviour*. Several rules that are missing or stubbed in 4.9 are fully implemented here.
§10 lists those differences explicitly.

**Companions:** `Exchanges_Returns_CreditNotes_Reference.md` (current 4.9 state),
`Exchanges_Target_Design.md` (what we build), `Exchanges_Rebuild_Record.md` (what was built, the decisions
behind it, and what is left).

---

## 1. Plugin layout, and why the logic lives in Base

Three plugins, but the business logic is nearly all in one:

| Plugin | Contents |
|---|---|
| **`Nop.Plugin.TIG.Base`** | ~920 files. **All** the Exchanges, Credit Notes and Subsidy domain entities, services and business logic, plus the order/cart/total service overrides that tie them together. |
| **`Nop.Plugin.TIG.Exchanges`** | 18 files. Controllers, views, widgets, permissions, DI wiring, a scheduled task. **No business logic.** |
| **`Nop.Plugin.TIG.Subsidy`** | 13 files. Controllers, views, widgets. **No business logic.** |

The reason is structural: Exchanges and Subsidy have to co-operate inside the *same* order-placement and
cart-total pipelines (§5, §6). Splitting the logic across two independently-installable plugins would mean
each having to reach into the other. Putting it all in `Base` and letting the satellite plugins be pure
presentation was the only way to express the interaction.

Both are gated at runtime by `PluginHelper.IsCreditNotesPluginInstalled()` /
`IsSubsidyPluginAllowed()` / `IsBulkOrdersPluginInstalled()`, checked before every interaction, and
services are injected as **optional constructor parameters defaulting to null**
(`ICreditNotesService creditNotesService = null`). That install-state check is the direct ancestor of
4.9's `CWFeature` toggle system — the merge into `CW.Core` plus feature toggles is the same idea done
declaratively.

---

## 2. Data model

### 2.1 Credit Notes

| Entity | Purpose |
|---|---|
| `CreditNoteRecord` | `CustomerId`, `OrderId`, `Amount` (issued), **`Remain` (stored, mutable balance)**, `ReturnReason`, `StoreId`, `ReturnRequestId` (null for manual notes), `Reference`, `Name` |
| `CreditNoteHistoryRecord` | One row per consumption: `CreditNoteId`, `CustomerId`, `OrderId`, `Amount`, `Remain` (balance snapshot after this event) |

`Remain` is a **stored column, mutated in place** on every apply and undo. History rows are **deleted** when
a consumption is reversed. This is the model 4.9 inherited and has since replaced with live views.

### 2.2 Exchanges / Returns

Extends nop's own `ReturnRequest` / `ReturnRequestReason` / `ReturnRequestAction` / `ReturnRequestStatus`:

| Entity | Purpose |
|---|---|
| `ReturnRequestItem` | 1:1 extension of a nop `ReturnRequest`: `ExchangeMode`, `ExchangeType`, `ReplacementItem`, `StaffShipQty`/`StaffRepackQty`, `ReturnRequestGroup` (Guid batching a submission), `NeedsCompletion`/`IsCompleted`, `ShoppingCartItemId`, `OrderItemId`, `ReturnedOrderItemId`, `AdditionalFeeName`/`AdditionalFeeAmount` |
| `ReturnReplacementItem` | The chosen replacement: `ProductId`, attributes XML/info, `Quantity`, `UnitPrice` |
| `ReturnReasonRule` / `ReturnActionRule` | Per store, per nop reason/action: `AllowStoreCredit`, `AllowExchange`, `AllowRepackagingFee`, `AllowShippingFee`, `BackInStock`, `IsDisabled` |
| `ReturnStatusRule` | Per store, per status: `PerformStoreCredit`, `PerformExchange`, `BackInStock`, `Authorised` |
| `ReasonActionRelationship` | Which actions are offered for a given reason, per store |
| `FeeRule` | Per store, per `FeeType`: `Amount`, `Operator`, `UnitsQuantity` |
| `ImpersonationToken` | Cross-store impersonation for completing an exchange |

**The 4.9 model is essentially identical** — the port kept these entities as-is. Note `ReturnRequestItem`
is 1:1 with `ReturnRequest`, so a return of three line items is three independent `ReturnRequest` rows
joined only by the `ReturnRequestGroup` Guid. That is the original design, not something the port
introduced.

### 2.3 Subsidy

Materially different from 4.9. The 4.1 model is **assignment-based with mutable remains**:

| Entity | Purpose |
|---|---|
| `Subsidy` | The subsidy definition (period, credit, product-group allocations) |
| `SubsidyAssignment` | Links a customer to a subsidy. Carries `Active`, `TopupRemainId`/`TopupRemain`, spending state |
| `SubsidyRemain` / `TopupRemain` / `SubsidyProductGroupRemain` / `TopupProductGroupRemain` | **Mutable remaining balances**, incremented/decremented in place |
| `SubsidyHistory` | Append-only audit ledger. `HistoryType` ∈ `{ Consume, Topup, Refund, Change, InitialAssignment, Rollover }`, plus `OrderId`, `NewSubsidyAssignmentId`, `TopupCreditAmount`, `TopupCreditRemain`, `Reason`, `CreatedByCustomerId` |
| `OrderItemSubsidy`, `SubsidySpendings`, `ProductGroupSubsidy`, `SubsidyAllocation` | Per-order/per-group consumption detail |

**Only one assignment is `Active` per customer at a time** — see the refund logic in §7.2, which
deactivates all others as a side effect.

> **The key architectural difference from 4.9.** In 4.1 both credit notes *and* subsidy are **stored,
> mutable balances with a separate audit ledger**. In 4.9, credit notes were re-implemented as **views
> computed over an insert-only ledger**, and subsidy likewise. Every "give it back" operation described
> below is therefore a *write* in 4.1, where 4.9 mostly needs no write at all.

---

## 3. Fees — how they are actually calculated

Fees are computed **per `ReturnRequestItem`**, i.e. per returned order line
(`ExchangeService.CalculateExchangeFeesAmount`). The order-level overload simply loops the lines and sums.

Which fees apply comes from the **intersection of the reason rule and the action rule**
(`GetFeeRulesForActionAndReason`):

- `ReShippingFee` applies only if `actionRule.AllowShippingFee && reasonRule.AllowShippingFee`
- `RepackagingFee` applies only if `actionRule.AllowRepackagingFee && reasonRule.AllowRepackagingFee`
- `AdditionalFee` is staff-entered ad-hoc, has no rule row

The amount (`ExchangesHelper.CalculateFeeAmount`):

```csharp
case FeeRuleOperator.PerUnitsQuantity:
    var quantityMultiplier = (int)Math.Ceiling((double)quantity / quantityFactor);
    amount = quantityMultiplier * feeAmountFactor;
    break;

case FeeRuleOperator.FixedPrice:
    amount = quantity > 0 ? feeAmountFactor : 0;
    break;
```

and the quantity (`ExchangesHelper.DetermineFeeRuleQuantity`) defaults to `returnRequest.Quantity` but is
**overridden by the staff-entered `StaffRepackQty` / `StaffShipQty`** when set.

### 3.1 "One re-shipping fee per return" is a staff action, not a structural rule

This matters, because it is easy to misread. There is **no return-level fee concept** in 4.1. A return of
three items produces three `ReturnRequestItem` rows, and if a `ReShippingFee` rule applies, each of them
computes a fee.

The way one-fee-per-shipment is achieved in practice is the `FixedPrice` operator plus staff setting
**`StaffShipQty = 0` on all but one line** — because `FixedPrice` returns `0` when quantity is `0`. The
admin edit screen exposes exactly these two quantities per return for that purpose.

So the business rule "one re-shipping fee per return" is real, but it is enforced *manually by Sales
Support*, not by the system. Any 4.9 redesign that introduces a genuine return-level fee should treat this
as **automating an existing manual step**.

> **Confirmed and superseded, 2026-08-21.** The manager confirmed staff do zero the quantities today, and
> gave the exact rule: *"$5 per units and in this case there are 4 units. This means that up to 4 items
> will be redelivered for a fee of $5. If there were 5-8 items it would be $10 and so on."* 4.9 phase 3a
> applies that band **per return request, over its own Re-Shipped Quantity** — exactly where 4.1 applies it.
> It is not spread across the return, and **staff still zero the quantity manually** on the lines that should
> not carry redelivery. The manager confirmed on 2026-08-24 (questions doc 11) that this manual step is the
> wanted behaviour, not a workaround to retire.
>
> **The manual workaround is not equivalent to the rule — it undercharges above one band.** Zeroing all
> but one line leaves a single line's quantity, so a six-item return bills `ceil(1/4) × $5` = **$5** where
> the rule says **$10**. So 4.1 overcharges when staff do nothing (`$5 per item`) and undercharges when
> they apply the workaround to a return of more than one band's worth. Neither figure is the rule.
> Staff must now **stop** zeroing: under 3a, a zeroed line removes its units from the band.
>
> Also worth knowing before porting anything from this file: `FixedPrice` returns `0` when the quantity is
> `0`, so the workaround suppresses a fixed-price fee too — and `CalculateFeeAmount` divides by
> `UnitsQuantity` with no zero guard in either version.

---

## 4. Customer return/exchange flow

1. **Orders page → "Return Item(s)"**, gated by nop's `IsReturnRequestAllowedAsync`.
2. **Gather info** — quantities per item, then reason; the reason drives which actions are offered
   (`ReasonActionRelationship` → `GetActiveActionsByReasonId`, fetched by AJAX).
3. **Mode choice** — Exchange or Credit Note, from the reason+action rule intersection
   (`DetermineEnabledReturnModes`). For Exchange, the customer picks *Same style* / *Different style* /
   *Exact*.
4. **`StyleChangeList`** — candidate replacements for "different style".
   ```csharp
   var products = _tigProductService.GetCompanyProducts(store.Id).ToList();
   ```
   All company products for the store, optionally filtered to/excluding the original product id.
   **No product-group or subsidy-allocation filtering** — see §9.
5. **Summary** — estimated fees from the applicable `FeeRule`s, then confirm.
6. **Confirm** creates the nop `ReturnRequest` (status `Pending`) plus the CW `ReturnRequestItem`, one pair
   per returned line, sharing a `ReturnRequestGroup` Guid.

---

## 5. Admin approval — the workflow engine

`ExchangesLogic.ProcessReturnRequestStatusChange(statusChangeData, returnRequest)`. Fires **only** when the
status changes to `ReturnAuthorized`.

1. Load the `ReturnStatusRule` for the new status. **If absent, or `Authorised == false`, stop** — nothing
   happens at all.
2. Load the reason and action rules, and compute `priceDiffInCustomerFavor` (only for non-`Exact`
   exchanges: replacement price minus original price, negative = customer is owed).
3. Overlay the staff-entered quantities and additional fee onto the loaded item, in memory.
4. **Credit note needed?**
   ```csharp
   return (statusRule.PerformStoreCredit && reasonRule.AllowStoreCredit && actionRule.AllowStoreCredit)
       || (priceDiffInCustomerFavor);
   ```
   If so → `ExecuteCreditNoteWorkflow`. The amount is the returned item's price × quantity; **if the
   customer's mode is Credit Note, fees are subtracted from it**. If the mode is Credit Note, the return is
   marked complete here.
5. **Exchange needed?** Only if the customer chose Exchange **and**
   `statusRule.PerformExchange && reasonRule.AllowExchange && actionRule.AllowExchange`
   → `ExecuteExchangeWorkflow` (§6).

Failure anywhere triggers `CleanupFailedExchangeWorkflow`, which undoes the credit note (if one was
created this run) and removes any cart items added.

`UndoReturnRequestApproval` runs the same cleanup and resets the status to `Pending`.

---

## 6. The two exchange pathways

`ExchangesLogic.ExecuteExchangeWorkflow` computes, up front:

```csharp
creditNotesBalance      = sum of the customer's CreditNoteRecord.Remain
exchangeTotalAmount     = replacement subtotal + exchangeFeesAmount
orderSubsidyTotal       = subsidy applied to the ORIGINAL order
refundedSubsidyForOrder = Σ SubsidyHistory where HistoryType == Refund for that order
availableRefundSubsidy  = exchangeTotalAmount - exchangeFeesAmount
remainOrderSubsidyTotal = orderSubsidyTotal - refundedSubsidyForOrder
```

**Pathway A — credit notes cover it and no fees are due**
(`creditNotesBalance >= exchangeTotalAmount && exchangeFeesAmount <= 0`):

- Places the replacement order immediately (`PlaceReturnOrder`).
- Marks the return complete, syncs to **Jim2** and **Punchout cXML**, sends the "approved" email.
- **No subsidy refund happens on this pathway.**

**Pathway B — anything else** (insufficient credit, or fees owed):

- Adds the replacement to the customer's cart, carrying the packname from the returned item, and links
  the cart item to the `ReturnRequestItem`.
- **Refunds subsidy** (§7.2).
- Marks the return `NeedsCompletion` and emails the customer the amount outstanding. If the pending amount
  computes negative (customer in credit), the amount quoted is just the fees.

The asymmetry is deliberate: on Pathway A the customer pays nothing further, so there is nothing to give
back; on Pathway B they are about to pay again, so the subsidy spent on the original order is released
first.

---

## 7. Subsidy and Credit Note interaction — the rules

### 7.1 Consumption order: Credit Notes first, Subsidy second

Enforced in **both** places, structurally:

**Cart totals** (`OrderTotalCalculationServiceOverride`):

```csharp
if (PluginHelper.IsCreditNotesPluginInstalled())
    PerformCreditNotesCalculations(totals, customer);      // subtracts from totals.TotalAmount

if (PluginHelper.IsSubsidyPluginAllowed())
    PerformSubsidyCalculations(totals, customer, cart, ignoreShipping);   // subtracts from what's left
```

**Order placement** (`OrderProcessingServiceOverride.PerformAdditionalOrderWorkflow`):

```csharp
if (isCreditNotesInstalled)
    _creditNotesService.ApplyCreditNotesForOrder(order, out remainOrderAmount);

// Subsidy is not applied on Return Orders
if (isSubsidyAllowed && !isReturnOrder)
{
    ...
    if (hasSubsidyBalance && remainOrderAmount > 0)
        _subsidiesLogic.ApplySubsidyForOrder(order, out _, remainOrderAmount, allowsPerEmployeeSubsidy);
}
```

Subsidy receives `remainOrderAmount` — the balance left *after* credit notes. The ordering is not a
convention, it is the data flow.

**But only at the money level.** Inside `SubsidyService.CalculateAppliedSubsidyProductGroupItems`
(`:905-1087`) product-group units and product-group credit are allocated **per order item, regardless of
the credit-note-reduced total**; only global credit and topup (`:1314-1454`) are bounded by it. So on a
unit-based subsidy 4.1 consumed the unit *and* the credit note for the same garment — which is why the
business remembers "the unit is always used first", and why phase 2 called it over-consumption. Verified
2026-09-10; `Exchanges_Subsidy_Value_Return_Rework.md` §1.1.

### 7.2 `isReturnOrder` — subsidy is never spent on an **auto-placed** return order

The guard above is real and load-bearing — **on pathway A only**. `PerformAdditionalOrderWorkflow` is
called from three places; the return-order path (line 732) passes `true`, the two normal checkout paths
pass the default `false`. A pathway B replacement is checked out through those normal paths, so subsidy
**is** applied to it — from the topup money §7.3 just refunded, or from another unit. In the default
(non-per-employee) mode the returned item is not excluded from that calculation (`BuildOrderSummaryFromOrder`,
`SubsidyService.cs:827-860`). Corrected 2026-09-10.

A second guard exists on the per-employee path:
`ShoppingCartHelper.BuildPerEmployeeSubsidyCarts(cart, manager, ignoreReturnedItems: true)` — returned
items are excluded from per-employee subsidy carts by default.

> **This guard was lost in the 4.9 port.** See §10.

### 7.3 Refunding subsidy on an exchange (Pathway B)

The cap is computed as:

```
refund = min( exchangeTotalAmount - exchangeFeesAmount ,
              orderSubsidyTotal - alreadyRefundedForThisOrder )
```

The second term is what makes it **idempotent**: prior refunds are re-derived from
`SubsidyHistory` rows of type `Refund` for that order, so approving twice cannot over-refund.

The refund itself, inside a `CommittableTransaction` (ReadCommitted):

1. Fetch the customer's assignments and the current active one.
2. For the **current** assignment: set `Active = true`, and **add the refund to `TopupRemain.TopupCreditRemain`**
   — creating a `TopupRemain` row if the assignment has none.
3. Write a `SubsidyHistory` row: `HistoryType = Refund`, `Reason = "Return by order {orderId}, RequestId: {returnRequestId}"`,
   `TopupCreditAmount = refund`, `OrderId`, `CreatedByCustomerId = current user`.
4. **Set `Active = false` on every other assignment**, then save all of them.

Two things worth noting. First, the refund lands in the **topup credit** bucket, not back into the
original allocation or product group — it becomes general credit. Second, step 4 is a real side effect:
approving an exchange **deactivates every other subsidy assignment the customer has**. That is consistent
with the "one active assignment" model but is easy to miss.

### 7.4 Order cancellation

Both systems undo explicitly, because both are mutable.

`CreditNotesLogic.UndoCreditNotesForOrder(orderId)`:
- Group history rows by credit note, add each group's `Amount` back onto `CreditNoteRecord.Remain`,
  **delete the history rows**.

`SubsidiesLogic.UndoSubsidyForOrder(orderId)`:
- Take `SubsidyHistory` rows of type **`Consume`** for the order, group by customer, and for each call
  `ReturnBackSpentSubsidy` → groups by assignment → `ReturnBackSpendingsOnSubsidy` restores the remains.
- **Delete all the subsidy history rows for the order.**

In both cases the audit trail for that order is destroyed, which is exactly the drift/forensics problem
the 4.9 credit-note redesign set out to remove.

### 7.5 Completing a Pathway B exchange

The customer pays through the normal cart and checkout, with the exchange items already in it. Special
handling applies when the cart contains **only** returned items
(`CheckoutController.HasOnlyReturnedItems`, `OrderTotalCalculationServiceOverride.GetReturnedItemsShippingTotal`):

- Shipping for returned items is excluded from the total.
- The shipping-method step is skipped.
- `ShoppingCartHelper.IsReturnedItem` lets the cart views identify these lines so they can be presented
  as fixed rather than editable.

So the "special payment screen" is the standard cart/checkout running in a constrained mode, not a
separate page. Worth confirming against the live 4.1 UI, since the manager describes it as a distinct
screen showing the items being paid for with no ability to change them.

### 7.6 Reason / action deletion cleans up its rules

`Nop.Plugin.TIG.Exchanges/Events/ReturnsEventsHandler` consumes
`EntityDeletedEvent<ReturnRequestReason>` and `EntityDeletedEvent<ReturnRequestAction>`, and deletes the
matching `ReturnReasonRule` / `ReturnActionRule` **for every store**. 4.9 has no equivalent (roadmap
item B7).

---

## 8. Downstream integrations

Both fire on Pathway A, immediately after the replacement order is placed:

- `_jim2LinkerService.SendOrderToJim2(placeOrderResult.PlacedOrder)` — Jim2 accounting sync, fully
  implemented in `Services/Jim2/Jim2LinkerService.cs` (1,848 lines) and `BusinessLogic/Jim2/Jim2Logic.cs`
  (2,367 lines).
- `_punchoutService.SendOrderToPunchout(placeOrderResult.PlacedOrder)` — Punchout cXML.

Neither exists in 4.9.

---

## 9. Product-group restriction on exchanges — not implemented in 4.1

**Live rule configuration, checked 2026-09-10** (`Exchanges_Subsidy_Value_Return_Rework.md` §1.2): the
Exchange action allows store credit in 84 of 85 stores, every exchange-enabled reason allows it in all but
one, and the Return Authorized status rule is `Authorised = PerformStoreCredit = PerformExchange = 1`
everywhere. So §5 step 4 fires on every exchange in production: a full-price credit note, subsidised or
not, on top of the §7.3 refund on pathway B.

The 2018 spec (`8c Subsidy Credit Note Scenarios.docx`) says an exchange against a unit-based subsidy must
stay within the same product group. **No such filtering exists in the 4.1 code.**
`ExchangesController.StyleChangeList` calls `GetCompanyProducts(store.Id)` and filters only by the
optional `productId` argument; there is no product-group, subsidy-allocation or availability check
anywhere in the replacement-selection path.

This answers the manager's "we should check the original code carefully to see what we really did here":
**we did not do it.** Building it in 4.9 is new work, not a port, and it will restrict something customers
can currently do.

---

## 10. What 4.9 changed, lost, or still owes

| Area | 4.1 | 4.9 today |
|---|---|---|
| Credit note balance | Stored mutable `Remain` | ✅ Improved — computed live from views over insert-only history |
| Subsidy availability | Stored mutable remains + `SubsidyHistory` ledger | ✅ Re-implemented — computed live from views; richer model (multiple product groups, period-scoped topups, per-PackName variations) |
| Credit-notes-before-subsidy ordering | ✅ Structural at the money level; product-group buckets consumed regardless | Reversed by decision 15: **subsidy first**, as pipeline priority data |
| **`isReturnOrder` guard** | ✅ Exists — subsidy never applied to an **auto-placed** return order (pathway A); pathway B checks out normally and does consume subsidy | Not needed: the returned line's consumption is reversed in kind at approval, so the replacement consumes exactly once (phase 4b) |
| Subsidy refund on exchange | ✅ Implemented as **money into topup credit**, capped, idempotent via `Refund` history rows — and, on production rules, alongside a full-price credit note | ✅ In kind, per bucket, insert-only (`RestoreSubsidyForReturnedItemAsync`); only the customer's money becomes a note |
| Subsidy undo on cancel | ✅ Explicit — restores remains, deletes history | ✅ Correct as a no-op — views exclude cancelled orders |
| Credit note undo on cancel | ✅ Explicit — restores `Remain`, deletes history | ✅ Correct as a no-op — same reason |
| Return reason/action identity | Matched by **localized display name** | ✅ Fixed — stored as ids (phase 0b) |
| Reason/action delete cleanup | ✅ `ReturnsEventsHandler` removes orphaned rules | ❌ Not ported (roadmap B7) |
| Fees | Per `ReturnRequestItem`; one-per-return achieved manually via `StaffShipQty = 0` | Same, unchanged |
| Product-group restriction on exchanges | ❌ Never built | ✅ Built in 4.9 (2026-10-01): a Different Style replacement follows the packname's allocation through the catalogue gate; Exact / Same Style are unrestricted (`Exchanges_Returns_CreditNotes_Reference.md` §9.4) |
| Jim2 / Punchout sync | ✅ Fully implemented | ❌ Not ported; stubs and `// TODO`s only |
| Exchange checkout | Normal cart with returned-items-only special casing (shipping suppressed, step skipped) | **Not inherited** — 4.9 zeroes shipping only for the auto-placed (synthetic) exchange cart; a pathway-B cart the customer completes is rated normally. Manager question Q15 / `Subsidy_PackName_Open_Items.md` |

### The two findings that matter most

1. **`isReturnOrder` was real.** The 4.9 integration plan claimed such a guard existed and I previously
   recorded that as an unfounded claim, because the identifier appears nowhere in `CW.Core`. It appears
   here, in `PerformAdditionalOrderWorkflow`, doing exactly what the plan described. The claim was
   accurate about 4.1 and simply never made it into the port. This is the direct cause of the 4.9
   double-consumption bug.

2. **4.1 already solved the subsidy refund**, including the two hard parts — capping the refund at what
   the original order actually consumed, and making it idempotent by re-deriving prior refunds from the
   history ledger. The 4.9 implementation does not need to invent that logic, only to re-express it
   against a view-computed ledger: the cap and the idempotency check become a query over compensating
   rows rather than a sum over `Refund` history rows.

### Where 4.1's approach should *not* be copied

- Deleting history on undo (both subsidy and credit notes). 4.9's insert-only model is strictly better and
  already proven for credit notes.
- Refunding into a general "topup credit" bucket rather than back to the allocation it came from — this
  loses the product-group association, which is precisely what the unrestricted-exchange gap in §9 would
  need if the product-group rule is ever enforced.
- The side effect in §7.3 step 4, deactivating every other subsidy assignment during a refund.
- Fees recomputed from live rules on every read, so historical returns change when a rule is edited.
