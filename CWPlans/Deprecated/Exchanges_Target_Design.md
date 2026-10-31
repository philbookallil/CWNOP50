# Exchanges, Returns & Settlement — Target Design (CW.Core)

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

> **Status on 5.00: finished history, not a live design.** Most of this design was built in 4.9 (phases 0b–5 and
> 4b of `CWPlans/Exchanges_Rebuild_Record.md`) and came across in the port. The parts never built are deferred
> backlog with no plan behind them, and two sections were overruled. Read it for *why* the code is shaped the way it
> is; read the Reference and the Record for what exists. Section by section:
>
> | Section | On 5.00 |
> |---|---|
> | §2.1, §2.2, §2.3 layering | **Built** (phase 1e). `ReturnRequestStatusChangeData` is gone (the controller commits the fields, then calls `ExchangesLogic.ExecuteApprovedLineAsync`); `Areas/Admin/Factories/ReturnRequestExchangeModelFactory.cs` exists; the rule is `CLAUDE.md` §5.1 |
> | §3 domain model | **Built** — `Domain/Returns/ReturnCase`, `ReturnCaseLine`, `ReturnResolution`, `ReturnCharge` (`CORE_RET_*`). Differences: `ReturnCase.UploadedFileId`, `ReturnResolution.ExchangeOrderId` and `.CreditNoteId` were dropped as write-only; **`ReturnPolicy` not built**. The per-case redelivery band is **reversed** by decision 30: every fee is priced per line (`ExchangeService.CalculateExchangeFeeAmountsForCaseAsync`) and Sales Support adjust the staff quantities |
> | §3.1 rejected lines free | **Built** — rejected lines raise no charge |
> | §4.2 settlement pipeline | **Built** (phase 2) — `Services/Settlement/`; `ISettlementSource` has `Name`, `Priority`, `IsEnabledAsync`, `QuoteAsync`, `ApplyAsync` (no `Scope`, no `RestoreAsync`); Subsidy 1, Credit Notes 2 |
> | §4.3, §4.4 restoration in kind | **Built** (phase 5) — `CORE_SUB_SubsidyRestoreItem`, tagged with `ReturnCaseLineId`; credit notes are re-issued, never restored |
> | §5 delta settlement | **Built** as *restore in kind, then settle the replacement* (phase 4b). **`OrderReturnOrigin` not built** |
> | §5.1 no restriction on the replacement | Holds for *Exact* and *Same Style*. A *Different Style* replacement follows the packname's allocation gate when its subsidies restrict to allocated products (Reference §9.4) |
> | §6.1 pure planner | **Not built** |
> | §6.2 idempotent executor | **Partly built** — `ILocker` keys per return request and per case, plus the unique index `UX_CORE_RET_ReturnResolution_ReturnCaseId` (decision 32). Not one transaction (decision 31; see its 5.00 note in the Record). Notifications are **not** post-commit: they are queued during the approval and survive an undo (Record §7.4) |
> | §6.3 fees priced once | **Built** (phase 3a) — `ReturnCharge` rows frozen at resolution |
> | §7 constrained cart | **Kept.** §7.2 (server-side constraint on returned cart items) built in phase 2. §7.1 built only for an *Exact* exchange (priced at what was paid, decision 24). §7.3 (re-add from the resolution) **not built**. §7.4 mixed carts allowed |
> | §9 phase 7 | `StyleChangeList` replaced by the paged two-stage picker. Credit-note reporting views not built. Jim2 / Punchout out of scope |

**What this is.** The design we intend to build — the *specification* (what we build), as distinct from
the *diagnosis* (why change) that preceded it. The original diagnosis document was retired when the
rebuild was paused; what survives of it is summarised in `CWPlans/Exchanges_Rebuild_Record.md` §1.

**Inputs it is built from:**

- `Exchanges_Returns_CreditNotes_Reference.md` — the 4.9 state at the time (phase 0 applied).
- `Deprecated/Legacy_NOP41_Exchanges_Subsidy_Reference.md` — what 4.1 actually does, including rules that were lost
  in the port and rules that were never built.
- The original diagnosis of six structural problems in the 4.9 code — retired; see
  `CWPlans/Exchanges_Rebuild_Record.md` §1.
- `subsidy-system.md` — the re-implemented 4.9 subsidy model, which this design treats as settled.
- Manager decisions, recorded in `CWPlans/Exchanges_Rebuild_Record.md` §3 and §5.

**Stance.** We are not porting 4.1. Where 4.1 solved something well (the refund cap and its idempotency)
we take the idea and re-express it; where it solved something badly (mutable balances, deleting history on
undo, refund-then-respend) we do not. The Credit Note live-balance change and the Subsidy re-implementation
are the precedent: same behaviour, better mechanics.

---

## 1. Design principles

1. **Record decisions; derive everything else.** A balance is never stored. A fee is never recomputed.
   The distinction: *facts* (what was decided, what was consumed) are stored append-only; *sums over
   facts* are computed.
2. **One path for a rule.** If a rule is expressed twice — once for display and once for writing — the two
   will diverge. They already have (§4.1).
3. **An exchange is a modification of a purchase, not a new purchase.** This single reframing removes
   the double-consumption bug by construction (§5).
4. **Layers point inward.** Domain knows nothing. Services know Domain. Factories know Models and
   Services. Admin knows public; public never knows admin (§2).
5. **Fail closed, loudly.** Missing configuration means "not allowed" and says so in the log, rather than
   silently doing nothing — the 4.9 phase 0b pattern, applied throughout.

---

## 2. Layering — the two violations, and the rule going forward

Both issues raised are real, both are one file, and both should be fixed as part of this work rather than
carried forward into new code.

### 2.1 `Domain/Exchanges/ReturnRequestStatusChangeData.cs` depends on an Admin model

```csharp
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;   // ← Domain → Admin presentation

public ReturnRequestStatusChangeData(CWReturnRequestModel model) { ... }
```

The innermost layer depends on the outermost. It also means the class cannot be constructed or unit-tested
without an admin view model, which is precisely the coupling that makes the workflow untestable today.

**Fix:** this is an *application input DTO*, not a domain entity. It moves out of `Domain/` and becomes a
plain record with primitive members, constructed by the caller. In the new design it is subsumed by
`ReturnResolutionRequest` (§6.2), which is built by the controller and carries no model reference.

### 2.2 `Factories/ReturnRequestItemModelFactory` has a split personality

It lives in the public `Factories/` folder but serves both audiences — `CustomerExchangeRequestModel` and
`ReturnExchangesConfirmationModel` for the store, `CWReturnRequestModel` (an
`Areas/Admin/Models/` type) for the admin edit screen. Its interface imports
`Areas.Admin.Models.Exchanges`, so every public consumer transitively depends on admin models.

**Fix:** split by audience.

- `Factories/ReturnRequestItemModelFactory` keeps the two public methods.
- `Areas/Admin/Factories/ReturnRequestExchangeModelFactory` takes `PrepareExchangeRequestModelAsync`
  and the `CWReturnRequestModel` dependency. Its callers — `Areas/Admin/Controllers/ReturnsController` and
  `Filters/ReturnRequestAdminFilter` — are both already in the admin area.
- Shared read logic (packname lookup, store/branch info, price) moves to the service layer, which is where
  it belongs; both factories then call it.

### 2.3 The rule, to be added to `CLAUDE.md` §5

| Layer | May reference |
|---|---|
| `Domain/` | Nothing from this plugin. Entities, enums, and value objects only. |
| `Data/` | `Domain/` |
| `Services/`, `BusinessLogic/` | `Domain/`, `Data/`, other services. **Never `Models/` or `Areas/Admin/Models/`.** |
| `Models/` (public) | `Domain/` types only where genuinely needed |
| `Factories/` (public) | `Models/`, `Services/`. **Never `Areas/Admin/Models/`.** |
| `Areas/Admin/Factories/` | `Areas/Admin/Models/`, `Services/`, and public `Models/` if a screen genuinely reuses one |
| Controllers / Views | Their own area's factories and models |

A service needing data *from* a form takes an explicit input record it owns, never a view model.

---

## 3. Domain model

```
ReturnCase                          CORE_RET_ReturnCase
  Id, StoreId, CustomerId, OrderId, CaseNumber,
  ReturnReasonId, ReturnActionId,          ← ids, per phase 0b
  Status (Draft|Submitted|InReview|Resolved|Cancelled),   ← case-level lifecycle only
  CustomerComments, UploadedFileId,
  CreatedOnUtc, UpdatedOnUtc

ReturnCaseLine                      CORE_RET_ReturnCaseLine
  Id, ReturnCaseId, OrderItemId, Quantity,
  Mode (Exchange|Credit), ExchangeType,
  ReplacementProductId?, ReplacementAttributesXml?, ReplacementQuantity?,
  Disposition (Pending|Accepted|Rejected),   ← per-line accept/reject (manager decision 3)
  DispositionReason, DispositionOnUtc, DispositionByCustomerId,
  StaffShipQty?, StaffRepackQty?,
  ReturnRequestId?                           ← satellite link to the nop row

ReturnResolution                    CORE_RET_ReturnResolution
  Id, ReturnCaseId, ResolvedOnUtc, ResolvedByCustomerId,
  ExchangeOrderId?, CreditNoteId?,
  BalanceDue, Status (Pending|Settled|Cancelled)

ReturnCharge                        CORE_RET_ReturnCharge
  Id, ReturnResolutionId, ReturnCaseLineId?,   ← the line it is COLLECTED with, always set
                                              (nullable only for a pre-case-model return)
  FeeType, Amount, Quantity, SourceFeeRuleId?, Description

ReturnPolicy                        CORE_RET_ReturnPolicy   (not built — phase 6)
  Id, StoreId, ReturnReasonId, ReturnActionId?,
  AllowExchange, AllowStoreCredit,
  ChargeRepackagingFee, ChargeShippingFee,
  BackInStock, IsDisabled
```

Three things worth calling out.

**Status lives in two places, deliberately.** `ReturnCase.Status` is the lifecycle of the *conversation*
(drafted → submitted → being reviewed → resolved). `ReturnCaseLine.Disposition` is the per-item
accept/reject the manager asked for — one garment can come back clean and another dirty. Fees and credit
apply only to lines that are not rejected. This is why the case aggregate is still worth having even
though approval is per item: the case owns reason, action, the submission itself, and the return-level fee.

**Fee scope is fixed by fee type, not configurable per policy.** Confirmed by the manager: the
re-shipping fee is charged **once per return**, with its amount and operator coming from the store's
Exchange settings and falling back to the global (store 0) settings — the `FeeRule` store/global
inheritance that already exists. So scope is not a policy field:

| Fee type | Priced over | Collected with |
|---|---|---|
| `ReShippingFee` (Redelivery) | **The case** — one shipment back, one fee. The summed quantity of every line **not rejected** | **One anchor line** of the case, an exchange line by preference |
| `RepackagingFee` | **That line** — each garment is repackaged individually | Its own line |
| `AdditionalFee` | That line, the amount staff typed | Its own line |

**"Not rejected", not "accepted".** While a case is undecided its `Pending` lines are still expected back,
so they count towards the band. Excluding them would quote below the band and then jump upwards as each
line was approved; counting them quotes the full band from the start, so the figure can only ever shrink.
Rejected lines are excluded, which is what makes them free (§3.1).

The `FeeRule` itself is unchanged: `Amount`, `Operator` (`FixedPrice` / `PerUnitsQuantity`) and
`UnitsQuantity` still come from the store-or-global rule row. What changes is only the **scope over which
quantity is summed** before the operator is applied.

**Worked example, using a live store's settings** (Redelivery Fee `$5.00`, *Per Units*, Units Quantity
`4`; Repackaging Fee `$3.00`, *Per Units*, Units Quantity `1`) for a return of **5 garments, none rejected**:

| | Today (per line) | Target (shipping per case) |
|---|---|---|
| Redelivery | 5 lines × `ceil(1/4)` × $5 = **$25** | `ceil(5/4)` × $5 = **$10** |
| Repackaging | 5 × $3 = **$15** | 5 × $3 = **$15** (unchanged) |

The per-case figure is the intended one — $5 per 4 garments, rising to $10 at 5, staying $10 through 8,
$15 at 9. The $25 is what the system computes today **unless staff zero the shipping quantity on four of
the five lines**, which is the manual step this replaces (legacy §3.1).

### 3.1 Rejected lines are free

Confirmed: **a rejected item attracts no fees at all.** Concretely, in the planner:

- A rejected line raises **no repackaging charge**.
- Its quantity is **excluded from the case shipping total**, so it cannot push the redelivery fee into the
  next `Per Units` band.
- If **every** line is rejected, the resolution raises no charges, no credit note and no exchange.

This is why the table above says *not rejected* rather than simply *every line*.

**The return-level charge still carries a line.** `ReturnCharge.ReturnCaseLineId` is set on every charge
raised against a case: the redelivery fee is priced over the whole return and then attached to **one**
line — an exchange line by preference, because that is the one that reaches a cart. `Quantity` records
what it was priced over, so the row is not a claim that the fee covers only that line.

That is what makes it collectable exactly once even when a case checks out as two orders, and it is why
no reader needs to work out which cart item should count a line-less charge. An earlier draft of this
section said the charge hung off the resolution with a null line; that shape forced a de-duplication
guard into every reader, and the guard was itself a bug (`tasks/lessons.md` #13). The column stays
nullable only for a return raised before the case model existed.

It is also the operational shape staff already use: today they achieve one fee per parcel by zeroing
`StaffShipQty` on all but one line. The fee has always ridden one line; 3a automates the arithmetic.

> This **automates what Sales Support currently do by hand** — legacy §3.1 shows one-fee-per-return is
> achieved today by zeroing `StaffShipQty` on all but one line. Staff keep the ability to adjust the
> quantity; they no longer have to zero lines to avoid duplicate charges.

**`ReturnResolution` is the record of what was decided**, written once. Everything downstream references
it: the credit note, the exchange order, the charges, and the settlement movements in §4.

---

## 4. Settlement — the central idea

### 4.1 The problem this solves

Credit Notes and Subsidy are the same *shape* of thing: a pool the customer is entitled to, consumed by
orders and replenished by returns. But there is no shared concept, so:

- The ordering rule (originally "credit notes first, subsidy on the remainder"; **since decision 15,
  "subsidy first, credit notes on the remainder"**) is enforced by **call order** in one place and **not
  at all** in another. 4.1 enforced credit-notes-first in both the cart display and order placement at
  the money level, while consuming product-group buckets regardless; 4.9's packname subsidy path did not
  net off credit notes at all (reference doc §9.1).
- **Display and write are separate code paths** that must agree by hand. The cart shows what
  `PerformCreditNotesCalculations` + `CalculateSubsidyTotalAsync` compute; the order writes what
  `ApplyCreditNotesForOrderAsync` + the packname pipeline decide. Nothing forces them to match.
- A return needs the same ordering logic **in reverse**, which without a shared concept becomes a third
  copy.

### 4.2 The model

An ordered pipeline of **settlement sources**. Each source answers three questions about an amount:

```csharp
public interface ISettlementSource
{
    int Priority { get; }                    // 1 = Subsidy, 2 = Credit Notes (decision 15, 2026-09-10; was the reverse until phase 4b)
    SettlementScope Scope { get; }           // what it may pay for: goods, shipping, fees

    Task<SettlementQuote> QuoteAsync(SettlementContext context, decimal amount);
    Task<SettlementResult> ApplyAsync(SettlementContext context, decimal amount);
    Task RestoreAsync(SettlementContext context, ReturnResolution resolution, decimal amount);
}
```

`SettlementContext` carries customer, store, order/cart, and — where relevant — packname and the
product-group breakdown that Subsidy needs.

The pipeline walks sources in `Priority` order, passing the **remaining** amount to each. That makes the
ordering rule *data*, not call order, and it means one implementation serves both display (`Quote`) and
placement (`Apply`).

**The honest caveat:** Subsidy is much richer than Credit Notes — product groups, quantity-vs-credit,
period scoping, per-PackName variations. The interface above is deliberately **narrow**: it agrees only on
"how much of this amount can you cover, and record that you did." Each source keeps its own internals and
its own richer API for everything else. Trying to make Subsidy's product-group allocation fit a shared
interface would be a leaky abstraction and is not proposed. If in review this still feels like an
abstraction for two implementations, the fallback is a single `SettlementPipeline` service with the
ordering hard-coded and no interface — that still fixes the ordering and quote/apply divergence, which are
the actual bugs.

### 4.3 Restoration is a compensating movement, never a mutation

Both sources are already view-computed over insert-only ledgers in 4.9. `RestoreAsync` therefore inserts
**compensating rows tagged with the `ReturnCaseLineId`** — negative `SubsidyOrderItem`-shaped rows for
Subsidy, a `CreditNoteHistory`-shaped reversal for Credit Notes — and the existing availability views pick
them up with one extra `UNION ALL`.

This is 4.1's cap-and-idempotency logic re-expressed. 4.1 computed "already refunded" by summing
`SubsidyHistory` rows of type `Refund` for the order; we compute it by summing compensating rows for the
returned order item. Same guarantee, but without mutating a balance and without deleting history.

> **Phase 4b (2026-09-10).** Only the money the customer put in comes back as a credit note: the returned
> value minus the subsidy given back in kind (`SubsidyRestoreResult.MoneyEquivalent`). Restoration runs on
> every authorised line, both modes, before the note and before the cart. See
> `CWPlans/Exchanges_Rebuild_Record.md` §3 (decisions 15–22) and §4 (phase 4b).
>
> **As built (phase 5, 2026-08-26).** Two corrections to the paragraph above.
> **The tag is `ReturnCaseLineId`, not `ReturnResolutionId`.** After the 2026-08-24 reversal a line
> executes the moment it is authorised, but the resolution is only written once *every* line is decided —
> so on a multi-line parcel the resolution does not exist yet. The case line is where the work happens and
> is 1:1 with the return request; the resolution is one join away.
> **Only Subsidy restores.** Credit Notes have nothing to give back: a returned item's value comes back as
> a *newly issued* credit note (`DetermineExchangeCreditNoteAmountAsync`), which is why 4.1's
> `UndoCreditNotesForOrder` has no callers. `ISettlementSource` therefore gained no `RestoreAsync`.
> Full record: `CWPlans/Exchanges_Rebuild_Record.md` §4 and §8

### 4.4 Restoration is **in kind** — bucket for bucket

Confirmed by the manager: subsidy goes back to exactly where it came from.

- A **unit-based** allocation restores **units, not money**. Returning a size 10 and taking a size 12 moves
  one unit; the price of either garment is irrelevant.
- A **global $** subsidy restores to the global credit pool.
- A **product-group $** allocation restores to **that same product group's credit allocation**.

This falls out for free in the 4.9 model, because `SubsidyOrderItem` already decomposes a consumption into
exactly these buckets — `ProductGroupId`, `Quantity`, `QuantityTopup`, `ProductGroupCredit`,
`ProductGroupCreditTopup`, `Credit`, `CreditTopup`. A compensating row is that row negated, tagged with
the `ReturnResolutionId`. Each bucket therefore reverses itself with no mapping logic, and the
unit-versus-money distinction is preserved automatically.

> **4.1 does this in one place and not the other — worth knowing before "checking the 4.1 logic".**
> The **cancellation** path (`UndoSubsidyForOrder` → `BuildSubsidySpendings` → `ReturnBackSpendingsOnSubsidy`)
> is fully in kind: it decomposes spendings into global credit/topup and per-product-group
> quantity/credit/topup-quantity/topup-credit, and restores each to its own remain. **That is the rule
> described above, and it is the code to look at.**
>
> The **exchange** path does not. `ExecuteExchangeWorkflow` adds a single flat money amount to
> `TopupRemain.TopupCreditRemain` (legacy §7.3) — losing the product group, and converting returned
> *units* into general *credit*. Under the stated rule that is wrong: a customer exchanging a
> unit-allocated garment would gain spendable credit rather than moving a unit.
>
> The 4.9 design applies the cancellation path's granularity to **both** cases, which removes the
> inconsistency rather than porting it.

---

## 5. Exchange as delta settlement

This is the reframing that removes the worst bug.

Today (4.9): Pathway B refunds the subsidy spent on the original order, puts the replacement in the cart,
and the customer checks out — at which point the packname pipeline **consumes subsidy again**. Net effect
is meant to be zero; actual effect is a double charge because the refund is stubbed and the re-consumption
is unguarded. 4.1 avoided it with an `isReturnOrder` flag that suppressed consumption entirely (legacy
§7.2), which was lost in the port.

Restoring that flag would work. But it is a patch on a wrong frame. **An exchange is not a new purchase;
it is a modification of the original one.** So the exchange order should settle only the **difference**:

```
delta = replacement value − returned value + charges
```

Mechanically it is **restore in kind, then settle the replacement normally** — and the *delta* is what
falls out, per dimension rather than as one money figure:

1. **At resolution**, each accepted line's original consumption is reversed in kind (§4.4) — units back to
   units, group credit back to that group, credit-note consumption back to the note.
2. **At checkout**, the replacement settles through the normal pipeline like any other purchase.

For a size change this nets to zero without anything special: one unit returns to product group *G*, the
replacement consumes one unit from *G*. **Price never enters the calculation** — which is exactly the
manager's rule for unit-based allocations, and it is why the settlement has to work per dimension rather
than on a single money delta.

Where the two dimensions do diverge:

- **Dearer replacement** → the extra settles normally: subsidy first, then credit notes, then the customer
  pays the remainder (decision 15).
- **Cheaper replacement, money-funded** (own money or credit note) → the difference becomes a **Credit
  Note**, per the 2018 spec.
- **Cheaper replacement, unit-funded** → **no money difference exists.** A unit went back and a unit came
  out. The customer is not owed anything and no credit note is raised.
- **Cheaper replacement, $-allocation-funded** → the unspent credit stays in the pool it was restored to
  (global or that product group), not converted to a credit note.

The `isReturnOrder` guard becomes unnecessary: the exchange order never re-settles the original value,
because that value was already given back in kind. There is no flag to forget, and unlike the flag this is
also correct when the replacement costs *more*, where 4.1 would suppress consumption that should
legitimately happen.

> **Phase 4b makes this section real** (2026-09-10): the returned-item skip in `AllocateGoodsAsync` goes,
> restoration precedes the cart, and `IsCreditNoteNeeded` no longer forces a note on every exchange.
> `OrderReturnOrigin` is still not built and is not needed for it.
>
> **Known exposure, matching current behaviour.** Restoration happens at resolution but the replacement is
> settled at checkout, so between the two the restored entitlement is spendable. A customer could order
> something else with the restored unit and then have to pay cash for the exchange. 4.1 has the same gap
> (it refunds at approval, Pathway B). We are accepting it rather than building a reservation mechanism —
> the customer receives the value either way. Flagged because it is the kind of thing that surfaces later
> as a support question.

**Consequence for the exchange order:** it carries an explicit origin record rather than a boolean.

```
OrderReturnOrigin                   CORE_RET_OrderReturnOrigin   (not built — phase 4)
  Id, OrderId, ReturnResolutionId
```

Queryable ("which orders came from this return"), auditable, and it is what supplies the return credit
above. A boolean would tell us *that* an order came from a return; this tells us *which* return, which is
what the settlement needs.

### 5.1 No product-group restriction on the replacement

Decision reversed by the manager: **keep the original behaviour — any replacement is allowed.** In
practice most exchanges are a size change of the same item, and if the replacement is not covered by a
subsidy the customer simply pays for it.

The delta model handles this without special-casing, which is a good sign the frame is right. Returning
an item funded by product group *G* and exchanging for something outside *G* produces: a restoration of
*G*'s quantity or credit (tagged with `ProductGroupId`, §4.3), then a fresh settlement of the replacement
through the normal pipeline — which finds no group allocation, falls through to global subsidy credit if
any, and otherwise leaves the customer to pay. That is exactly the described behaviour, arrived at by the
general rule rather than a rule about exchanges.

This removes what was roadmap item 7a. `StyleChangeList` still needs its unrelated fix — it loads **every
published product for the store with no paging**.

---

## 6. Resolution — plan, then execute

### 6.1 A pure planner

All the decision-making moves into one function with no I/O:

```csharp
ReturnResolutionPlan Plan(ReturnResolutionInput input);
```

`ReturnResolutionInput` is an explicit record — case, lines with their dispositions, policy, prices,
current settlement quotes, and the staff-entered quantities. **This is where the three duplicated
"overlay the admin's unsaved edits" blocks collapse** (reference doc §10.3): the caller builds one input
record from the form, and there is nothing to overlay.

`ReturnResolutionPlan` states the outcome: charges to raise, credit note amount, whether an exchange order
is needed and for what, the settlement delta per source, and the balance due.

Deterministic, no database, no clock. Unit-testable — which matters because this subsystem has no tests
at all, and the rules here (rule intersection, price difference direction, fee scope, delta signs) are
exactly the kind that break silently.

### 6.2 An idempotent executor

```csharp
Task<ReturnResolutionResult> ResolveAsync(ReturnResolutionRequest request);
```

- Guarded by `ILocker` keyed on `ReturnCaseId`, and by `ReturnResolution.ResolvedOnUtc` — resolving twice
  is a no-op that returns the existing resolution rather than a second credit note.
- Writes the resolution, the charges, the credit note, the exchange order and the settlement movements as
  one unit.
- **Notifications are queued after the state change commits**, via the existing `IBackgroundTaskQueue`.
  Today an email can be sent and then rolled back by `CleanupFailedExchangeWorkflowAsync` — the one thing
  compensation cannot undo.

`ReturnResolutionRequest` is the replacement for `ReturnRequestStatusChangeData` and lives in the
application layer with no view-model dependency (§2.1).

### 6.3 Fees are priced once

`ReturnCharge` rows are written at resolution from the policy and fee rules in force **at that moment**.
Editing a fee rule afterwards does not change a resolved return. This kills the retroactive-drift problem,
removes the N+1 in the fee calculation, and makes the exchange order total trivially correct — the charges
are simply lines on it.

It also fixes a subtler thing found in the legacy read: deleting a Return Reason currently empties the fee
breakdown on historical orders, because fees are re-derived from live rules on every read.

---

## 7. Completing a paid exchange — the constrained cart, kept

**Decision reversed.** An earlier draft proposed a dedicated pending-payment order and no cart. The
manager has confirmed the screen he described *is* the existing constrained cart mode, and that it has
worked well — so we keep it. It is proven, customers and staff know it, and replacing it would be change
for its own sake.

The mechanism stays as legacy §7.5: the replacement goes into the customer's cart, and when the cart
contains **only** returned items, checkout runs in a constrained mode — shipping for returned items
excluded from the total, the shipping-method step skipped, and returned lines flagged
(`IsReturnedItem`) so the views present them as fixed rather than editable.

What we do change is the failure modes that proposal §2.5 identified, all of which can be addressed
*within* this approach:

1. **Freeze the price at resolution.** The amounts quoted in the approval email come from
   `ReturnResolution` and `ReturnCharge` (§6.3). The cart honours those recorded amounts instead of
   re-pricing the replacement at checkout time, so what the customer was told is what they are charged.
   This is the most valuable of the four and is independent of the cart itself.
2. **Enforce the constraint server-side, not just in the view.** Today the returned-item flag suppresses
   the edit controls; the quantity-change and remove endpoints should also reject a returned cart item.
   Confirmed: **the customer cannot cancel an approved exchange themselves** — they ring in and an admin
   adjusts it. So this is a hard server-side constraint with an **admin-only** adjustment path, not a
   customer-facing "cancel" button.
3. **Make the resolution the source of truth, not the cart row.** `ShoppingCartItemId` stays as a link,
   but if the cart item is lost the exchange is recoverable by re-adding from the resolution, rather than
   leaving a return stuck in `NeedsCompletion` pointing at nothing.
4. **Mixed carts are allowed** — confirmed: the customer can carry on shopping and pay for everything
   together. One order settles both. The one rule that matters: the **return credit is scoped to the
   exchange lines** and may not spill over onto unrelated shopping. If the replacement is cheaper, the
   surplus is handled per §5 — restored in kind or raised as a credit note — never applied to the rest of
   the basket.

Also confirmed, and requiring no work: **no expiry** on an unpaid exchange, and **an already-issued credit
note stays with the customer** regardless of what happens to the exchange. The returned garment goes back
into stock as usual.

Settlement (§5) is unaffected by keeping the cart: the exchange order is still the order placed from it, it
still carries an `OrderReturnOrigin`, and the restore-then-settle sequence is unchanged.

---

## 8. Traceability — what this fixes

| Problem | Where it came from | Fixed by |
|---|---|---|
| Fees per line only; per-return needs manual staff action | 4.1, inherited | Fee scope fixed by type — shipping per case, repackaging per line — with amounts from the existing store/global `FeeRule` (§3) |
| Subsidy consumed twice on a Pathway B exchange | `isReturnOrder` lost in the port | Delta settlement — nothing to re-consume (§5) |
| `RefundSubsidyForReturnAsync` stubbed | Never ported | `ISettlementSource.RestoreAsync` with compensating rows (§4.3) |
| Credit-notes-before-subsidy not structurally enforced | 4.9 regression vs 4.1 | Pipeline priority (§4.2) |
| Cart display and order placement can disagree | 4.9 | One `Quote`/`Apply` implementation per source (§4.2) |
| Approval not idempotent; emails un-rollback-able | 4.1, inherited | `ResolvedOnUtc` + `ILocker` + post-commit notifications (§6.2) |
| Fees recomputed from live rules, drift retroactively | 4.1, inherited | Priced once into `ReturnCharge` (§6.3) |
| Price drift between approval and payment | 4.1, inherited | Frozen at resolution; cart honours the recorded amounts (§7.1) |
| Approved exchange editable/removable from the cart | 4.1, inherited | Server-side constraint on returned cart items (§7.2) |
| A lost cart row orphans the return | 4.1, inherited | Resolution is the source of truth; cart row is recoverable (§7.3) |
| Three copies of "overlay unsaved edits" | 4.9 phase 0b | Single planner input record (§6.1) |
| Price calculation runs 2–3× per approval | 4.1, inherited | Planned once, reused (§6.1) |
| Restoration credits back to the wrong subsidy pool | 4.1 refunds into a general topup bucket | Compensating rows carry the consumption's `ProductGroupId` (§4.3) |
| Domain depends on Admin models | 4.9 | §2.1 |
| Public factory serves admin models | 4.9 | §2.2 |
| Orphaned rule rows on reason/action delete | not ported from 4.1 | Port `ReturnsEventsHandler` (roadmap B7) |

---

## 9. Revised phasing

Phase 0 is complete. The remainder, re-ordered for the decisions now known:

| Phase | Work | Notes |
|---|---|---|
| **1. Aggregate** | `ReturnCase` / `ReturnCaseLine` / `ReturnResolution` / `ReturnCharge` + migration + satellite sync. Wizard writes a `Draft` case. Per-line `Disposition`. | Satellite mapping is near 1:1 (per-line approval), the cheap case. Fix §2.1 and §2.2 here — the new code should not inherit the layering violations. |
| **2. Settlement pipeline** | `ISettlementSource` + pipeline; Credit Notes and Subsidy implement it; cart totals and order placement both routed through it. **No behaviour change intended** — this is the refactor that makes phases 3–5 possible. | Highest-value single step: fixes the ordering gap and the quote/apply divergence before anything depends on them. Verify cart totals are unchanged for a range of carts. |
| **3a. Resolution writer** ✅ | `ReturnResolution` written once per case when the last line is decided, with its fees priced into `ReturnCharge` rows at that moment. Idempotent via `GetResolutionByCaseIdAsync`. **Every fee is priced over its own return request line — nothing is banded across the case** (manager, 2026-08-24: do it exactly as 4.1 does). A line executes the moment it is authorised; the resolution only records and freezes. | **Applied and tested — 2026-08-26.** Record: `CWPlans/Exchanges_Rebuild_Record.md` §4. Delivers §6.3 (priced once) and the case half of §6.2. §4.2 (the quote/price seam) remains open. |
| **3b. Planner + executor** | Pure `Plan`, `ResolveAsync` under `ILocker` and a transaction, execution held until the case resolves, notifications post-commit. Retire `ReturnRequestStatusChangeData`. Closes the last fee walk — the customer wizard summary. | First unit-tested code in this subsystem. Needs the testing-location decision (`CLAUDE.md` §22 Note A). |
| **4. Delta settlement** | `OrderReturnOrigin` on the exchange order; return credit in the settlement context so only the delta settles; retire `PlaceReturnOrderAsync` in favour of the single pipeline. **The constrained cart stays** — this phase changes what the order settles, not how the customer reaches it. Adds the four cart hardening items (§7). | **Split 2026-09-10:** the settlement half is **phase 4b** (applied 2026-09-11; the spec was consumed into `CWPlans/Exchanges_Rebuild_Record.md` §4) plus 4b-3 (auto-place by quote). `OrderReturnOrigin`, frozen prices and the cart hardening remain "phase 4 rest". |
| **5. Restoration** ✅ | Subsidy restored **in kind** into `CORE_SUB_SubsidyRestoreItem`, tagged with `ReturnCaseLineId` and carrying the consumption's `ProductGroupId`, netted into both availability views. Credit notes restore by re-issue and needed nothing. Cancellation confirmed to need no write. | **Applied and tested — 2026-08-26.** Record: `CWPlans/Exchanges_Rebuild_Record.md` §4 and §8. Also removed the dead excl-tax chain from the subsidy ledgers |
| **6. Policy consolidation** | Collapse the three rule tables into `ReturnPolicy`; `ReturnStatusRule` becomes a trigger setting. | Moved later — phase 3's planner reads policy through one method regardless, so this becomes a swap behind that method. Do the reason/action delete cleanup (roadmap B7) in the same pass. |
| **7. Remaining gaps** | `StyleChangeList` paging; credit-note reporting views; Jim2/Punchout. | Jim2 and Punchout are **ports of working 4.1 code** (~4,200 lines), not new builds. The product-group restriction that was here has been dropped (§5.1). |

**Critical path to the subsidy goal: 1 → 2 → 5 → 4.** Phases 3b, 6 and 7 are independently valuable.

> **4 and 5 swapped, 2026-08-21.** `CWPlans/Exchanges_Rebuild_Record.md` §5 establishes that phase 4 cannot
> precede phase 5: §5 says the `isReturnOrder` guard becomes unnecessary *"because that value was already
> given back in kind"*, and without restoration there is nothing given back, so there is no delta to
> settle. Phase 3a is done; the order from here is **5 → 4**.

The notable change from the previous plan: **the settlement pipeline moves early**, because both the
exchange rework (4) and restoration (5) are far simpler on top of it, and because it fixes a live ordering
bug on its own.

---

## 10. Open questions

One remains, and it is ours rather than the business's.

1. **Testing location** — `Tests/Nop.Tests` conflicts with the Cardinal Rule; a parallel `Tests/CW.Tests`
   is the obvious alternative. Blocks phase 3b's tests, not its code. *(Decided: `Tests/CW.Tests`, `CLAUDE.md` §22
   Note A.)* (Both this document and the roadmap
   cite this as *"reference doc Note A"*; it actually lives in `CLAUDE.md` §22.)

~~2. Is the `ISettlementSource` interface worth it for two sources?~~ — **decided and delivered.** The
interface was taken (roadmap, *Decisions taken*) and shipped in phase 2, though as built it carries
`Name / Priority / IsEnabledAsync / QuoteAsync / ApplyAsync`: `SettlementScope` was deleted as dead and
`RestoreAsync` was deliberately not pre-declared. §4.2 above still shows the originally proposed shape —
treat `CWPlans/Exchanges_Rebuild_Record.md` §4 as authoritative for the interface. **Phase 5 deliberately added no
`RestoreAsync`**: only Subsidy restores, so the member would have been an abstraction over one
implementation (`CWPlans/Exchanges_Rebuild_Record.md` §4 and §8).

### Resolved

| Question | Answer | Where |
|---|---|---|
| Fee scope | Shipping per case, repackaging per line; amounts from store Exchange settings with global inheritance | §3 |
| Rejected lines and fees | **No fees at all** — and rejected quantity does not count toward the case shipping band. *Pending* quantity does | §3, §3.1 |
| Cheaper replacement, subsidy-funded | **Back to the subsidy, in kind** — units to units, global $ to global, product-group $ to that group. Not a credit note. One rule for all stores | §4.4, §5 |
| Settlement order | **Subsidy first, then credit notes** — units and money, employer's subsidy before a manager's own store credit (decision 15, 2026-09-10) | §4.2 |
| Exchange pair with store credit off, cash-funded original | The money note is raised anyway (decision 17) | `CWPlans/Exchanges_Rebuild_Record.md` §3 (decision 17) |
| Return for credit, fully subsidised, with fees | Nothing deducted; approval screen warns; Sales Support zero the fee boxes (decision 19) | Record §3 (decision 19) |
| Invoiced orders; shipping subsidy; previous-period returns | Back to the pot always; shipping not restored; previous-period restores into the lapsed period (decisions 20–22) | Record §3 (decisions 20–22) |
| Product-group restriction on replacements | Dropped; any replacement allowed, non-subsidised items paid by the customer | §5.1 |
| Payment flow | Constrained cart stays; hardened, not replaced | §7 |

> **Status, 2026-08-19.** §4.2 (the ordered pipeline, one Quote/Apply per source) and §7.2 (the server-side
> constraint on returned cart items — both the remove *and* the quantity endpoints) are **delivered** in phase 2 —
> see `CWPlans/Exchanges_Rebuild_Record.md` §4. §7.1, §7.3 and §7.4 remain phase 4. §4.3 (`RestoreAsync`) remains phase 5.
| Mixed carts | Allowed — keep shopping, one order settles both; return credit stays scoped to the exchange lines | §7.4 |
| Customer cancelling an exchange | Not permitted; admin adjusts on request | §7.2 |
| Unpaid exchange expiry | None | §7 |
| Credit note if an exchange is abandoned | Stays with the customer | §7 |
| Price drop between approval and payment | Customer pays the quoted price (prices do not drop on this B2B store) | §7.1 |
| Do staff zero `StaffShipQty` today? | Yes — automate it and show one shipping fee | §3 |
