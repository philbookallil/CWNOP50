# The exchanges, returns and credit-notes rebuild — the record

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

> **On 5.00.** Everything recorded here came across in the port to nopCommerce 5.00 (phases 4d, 4e and 5g of
> `CWPlans/CWCore_Port_Reference.md` §5) and passed its regression run. The port also closed §7.5 (the wizard now
> checks the replacement on the server). Line references below are re-pointed to the 5.00 code; dates and phase
> results are the 4.9 history.

**Paused 2026-08-27; picked up 2026-09-10 for phases 4b, 4b-3, 4b-4 and 3b, all applied and validated
against a live store on 2026-09-11** (§4). This is the single document to read when the work is picked up again. Both
exact-change specs and the 4b proposal have been consumed into §4 and §8 and deleted, per `CLAUDE.md` §0.4.

It replaces the roadmap, the proposal, the session status and every phase record. **Those files have been
retired** — this document is what survives of them, and it is written to be sufficient on its own. Where a
phase's working detail mattered, it was brought across; where it was scaffolding for work already applied
and tested, it was not.

Still live and **not** merged here, because they serve a different purpose:

| File | What it is |
|---|---|
| `Exchanges_Target_Design.md` | The design the rebuild aimed at, now mostly built — its status table says which parts. Phase records cite its section numbers throughout |
| `Exchanges_Returns_CreditNotes_Reference.md` | A map of the existing code. Reference, not plan |
| `Exchanges_Returns_CreditNotes_DevNotes.md` | The dated changelog |
| `Deprecated/Exchanges_Questions_For_Manager.md` | Manager-facing. Questions 1–14 answered; **15 and 16 opened 2026-09-11** and awaiting an answer |
| `Deprecated/Legacy_NOP41_Exchanges_Subsidy_Reference.md` | What 4.1 does, which is the tie-breaker for most arguments |
| `CWDocs/Exchanges_Returns_Credit_Notes_Functional_Guide.md` | The plain-English guide for non-developers |
| `Deprecated/Subsidy_PackName_Gap_Analysis_And_Known_Issues.md` | Unscheduled defects and improvements found along the way (its open items are carried by `Subsidy_PackName_Open_Items.md`) |

---

## 1. Where this got to, in one paragraph

Seven phases shipped and were tested against a live store: **0b** and **1e** (reason/action ids and
layering), **1** (the return case aggregate), **2** (the settlement pipeline), **3a** (the resolution
writer and 4.1's money model), **5** (subsidy restoration), and **9** (per-destination shipping
information), plus the packname-shipments work that was not a numbered phase. **3b** is specified and not
started. **4a** — delta settlement — is specified, was found to be necessary on the last day, and was
**not built**; a tactical fix stands in its place and §7 says exactly what that leaves wrong. **4b**
supersedes 4a: the manager reversed the settlement order (subsidy first) and ruled that the value must
come back in kind, once. It was specified 2026-09-10, applied over 2026-09-10/11, and **validated across
twenty-three live scenarios on 2026-09-11** — thirteen fixes came out of that validation, two of which
(12 and 13) were defects in ordinary checkout rather than in this work at all. **4b-3** followed on the same day: the pathway A/B decision now
asks the settlement pipeline instead of guessing, and an auto-placed exchange can be paid by the restored
entitlement. **8a** (pickup in store) is skipped by decision. **3b** is the only substantial piece left.
Nothing outstanding is a release blocker.

## 2. The rules this work ran under

These are not preferences. Each one was established by something going wrong.

1. **Build exactly what 4.1 does.** The tie-breaker for every open argument. Before calling anything in
   4.9 a defect, check `NOP41/`. *Exception, granted 2026-08-27:* where the manager rules 4.1's outcome
   itself wrong, we diverge — that is business decision 14. *Extended 2026-09-10 (decision 16):* for how
   subsidy and credit-note value moves, 4.1 is not the tie-breaker at all — its outcomes were ruled wrong.
2. **There are almost always two paths: packname and standard.** A change that handles only one is half a
   change.
3. **Balances are computed live, never stored.** Anything written must be visible to the availability
   views.
4. **The `.csproj` enumerates every view and stylesheet.** A new `.cshtml` with no entry is never copied
   to output, and fails at runtime rather than at build.
5. **Verify before diagnosing.** Search every layer — including `.cshtml` — before concluding something is
   absent. More than one round was lost to a confident answer that turned out to be invented.
6. **Say when you are guessing.** "I traced it and cannot tell which of these two it is, here is the one
   value that would settle it" beats a plausible fix.
7. **Exact changes, not descriptions.** Complete, copy-paste-ready code with no elisions and no
   placeholders. The developer applies them and then asks for validation.
8. **Defensive logging is never the fix.** Worth adding on its own merits, never instead of finding the
   cause.
9. **Documents describe the state as shipped**, not the state intended. If a phase stops half-done, the
   docs must say so.
10. **Never modify nopCommerce core** (`CLAUDE.md` §2). Customisations live in plugins.

## 3. The decisions

Business decisions are the manager's; the rest were taken by the team and recorded so they are not
relitigated.

| # | Decision | Where it bites |
|---|---|---|
| **Scope** | The subsidy-critical path: 0 → 1 → 2 → 3a → 5 → 4. 4 and 5 were swapped once it was clear the delta only exists after restoration | Ordering |
| **nop `ReturnRequest`** | **Keep as a satellite.** `ReturnCase` is the source of truth; nop rows are a projection maintained for its admin grid, custom numbers and stock templates | The whole aggregate |
| **Settlement order** | ~~Credit Notes = Priority 1, Subsidy = Priority 2~~ — **reversed by decision 15 (2026-09-10): Subsidy = 1, Credit Notes = 2.** The earlier row also misdescribed 4.1: at the money level 4.1 spent credit notes first (`NOP41 …/OrderProcessingServiceOverride.cs:376-417`); only product-group units and credit were consumed regardless of credit notes (`SubsidyService.cs:905-1087`), which is the over-consumption phase 2 closed | `SubsidySettlementSource`, `CreditNoteSettlementSource` |
| **Fee scope** | **Neither source may pay exchange fees.** Fees are added after settlement, so they are always cash | `OrderTotalCalculationOverrideService.cs:512` |
| **Budget mechanics** | **The money budget caps units as well as money** — `offeredQuantity = min(qty, floor(budget / unitPrice))`. An earlier draft said the opposite; it was reverted before phase 2 shipped, and the functional guide was corrected | `SubsidyAllocationService.cs:118` |
| **1 — Rejected items** | Charged as 4.1 charges them | Fees |
| **2 — Cheaper replacement** | The difference is the customer's | Settlement |
| **3 — Re-shipping fee** | As Sales Support do it today | Fees |
| **7** | Restoration is **in kind** — units to units, a group's credit to that group, global to global. *(See §7: this is currently not reachable.)* | Phase 5 |
| **8** | **An order is entirely collected or entirely delivered.** Never mixed | Killed phase 8b; simplified phase 9's stand-in `Order` |
| **9 — Repackaging** | **Per garment.** The asymmetry with redelivery is deliberate — one parcel out, so one redelivery band; each garment repacked separately | Fees |
| **10 — Partial approval** | **Whole line only, exactly as 4.1.** Deferred, not rejected | Admin screen |
| **11 — Cart timing** | **A line is priced and executed the moment it is authorised**, not when the case resolves. This *reversed* an earlier developer decision and decided the fee scope with it: redelivery is one band per return request, with Sales Support zeroing the box on lines that should not carry it | Phase 3a |
| **Parity over rules** | **Where a rule and an editable box can each solve an edge case, choose the box.** The manager watched Sales Support negotiate on the phone and kept the rules thin deliberately | Everything |
| **13 — Multi-branch documents** | **List every delivery address, with the packnames sharing it under each** | Phase 9 items 6 and 7 |
| **14 — Value back once** | **An exchange must give the returned value back exactly once, in the form it was paid.** Authorises divergence from 4.1, which gives it back twice | Phase 4a → 4b |
| **15** *(2026-09-10)* | **Subsidy settles before credit notes** — units and money alike, and the employer's subsidy before a manager's own store credit; the customer pays the remainder; fees after | Phase 4b-1 |
| **16** *(2026-09-10)* | 4.1 parity is not the tie-breaker for *how the value moves*: production 4.1 raised a full-price credit note on every exchange (all three rule tables allowed it on every exchange store, verified against the 4.1 data), refunded units as topup money on top, and so gave the value back twice | Rule 1, this area |
| **17** *(2026-09-10)* | The money credit note is raised whenever an exchange is performed, even if the reason/action pair does not allow store credit — a customer never pays twice for a garment | `IsCreditNoteNeeded` |
| **18** *(2026-09-10)* | Restoration runs for every accepted line, in both modes and on both pathways, **before** anything is added to a cart | Phase 4b-2 |
| **19** *(2026-09-10)* | Return for credit of a fully subsidised item with fees: nothing is deducted automatically; the approval screen warns and Sales Support adjust the fee quantities | Phase 4b-2 |
| **20** *(2026-09-10)* | Whether an order was invoiced never changes what comes back — subsidy always returns to the pot | `4a Refunds and Changes` |
| **21** *(2026-09-10)* | Shipping paid by subsidy is not restored on a return | Gap analysis S-17, closed |
| **22** *(2026-09-10)* | A restoration is scoped to the original order's subsidy period; a previous-period return restores into that lapsed period. Documented, not changed | `SubsidyRestoreItem` |
| **23** *(2026-09-11)* | **A partial return recovers subsidy first and cash second, not pro rata.** Return one of two $25 items funded by $4 subsidy and $46 cash and all $4 of subsidy comes back plus a $21 note. Returning the second later yields a pure-cash $25 note, so the totals reconcile either way; the only judgement is the ordering, and the employee gets their allowance back up front | §8, `RestoreSubsidyForReturnedItemAsync` |
| **24** *(2026-09-11)* | **An Exact Exchange is priced at what the customer actually paid** — the same price even where a tier price applied or the catalogue price has since moved. Not the live qty-1 price | Fixes 6, 8, 11; `ExchangeReplacementScope` |
| **25** *(2026-09-11)* | **A product's minimum and allowed quantities must never block a partial exchange.** The customer already bought the 2, 4 or 6 the rule demands; exchanging one of them is not a new purchase and nothing may stand in its way — at approval **or** at checkout, where nop revalidates | Fixes 7, 10 |
| **26** *(2026-09-11)* | A Credit-Note case goes final on approval, so the fee boxes disable and Undo Approval disappears. **Fix the wording, not the workflow** — the screen now says the fees must be set *before* approving | Fix 9, `ReturnsController` |
| **27** *(2026-09-11)* | **The pathway A/B decision is a settlement quote, not a guess**, and an auto-placed exchange may be paid by the restored entitlement. A quote that could not price anything falls to the cart — the safe direction | Phase 4b-3 |
| **28** *(2026-09-11)* | **A replacement the picker cannot fully specify is refused with its reason, not hidden.** Hiding it reads as the product being out of stock; the customer is told which details cannot be taken online and pointed at support. 4.1 has the same blind spot and says only *"Product selection is not valid"*, so this loses nothing that works live | Fix 21, `_ReplacementProductOptions.cshtml` |
| **29** *(2026-09-11)* | **A packname checkout deletes the cart lines it ordered, not the whole cart.** The order is built from a filtered view; anything the view excluded was never bought and must survive. 4.1 did exactly this (`OrderProcessingServiceOverride.cs:1134`) | Fix 22, gap analysis P-02 |
| **30 — manager** *(2026-09-11)* | **Fees stay per line, and Sales Support keep adjusting them by hand.** The customer is shown an *estimate*; staff set the real figure when they approve. This **reverses `Exchanges_Target_Design.md` §3's per-case redelivery band**, which was specified and never built — the shipped per-line behaviour (`ExchangeService.CalculateExchangeFeeAmountsForCaseAsync`, `ExchangeService.cs:953`) is correct and deliberate. Same call as *parity over rules*: where a rule and an editable box can each solve an edge case, the box wins | §7.3 closed, not fixed; design §3 marked reversed |
| **31** *(2026-09-11)* | **nop 4.90 offers no database transaction, so compensating actions are the correct design here — not a shortcut.** `BaseDataProvider` opens a fresh `DataConnection` per write (fifteen call sites), `INopDataProvider` exposes no `BeginTransaction`, and nop core contains no `TransactionScope`. The target design's §6.2 asked for atomicity that cannot exist on this platform | Phase 3b; `ReturnCaseService.ReopenCaseAsync`. *5.00 note:* a `TransactionScope` does work here — CW.Core's `PackNameTransaction` (`Services/PackNames/PackNameTransaction.cs:22`) uses one, and nop 5.00 adds `BaseDataProvider.CreateTransactionScope()` (`BaseDataProvider.cs:503`). The approval still uses compensation; moving it is not decided |
| **32** *(2026-09-11)* | **A lock is not a correctness guarantee in this codebase; an invariant that matters goes in the database.** nop's `MemoryCacheLocker` acquires through the non-atomic `IMemoryCache.GetOrCreate`, so simultaneous callers all believe they hold it — proven live, one case resolved three times. One resolution per case is now enforced by a unique index; the lock stays as cheap de-duplication | Fix 31, §6 |
| **33** *(2026-09-11)* | **An approval and its undo serialise on one key, and a busy lock fails loudly rather than proceeding.** A refused approval reports an error so the status is not advanced; a refused reopen is retried and then logged as an error, never silently skipped — proceeding would price the save from a resolution that should have been voided | Fixes 24, 26, 29 |

**Two decisions that were withdrawn**, recorded so they are not re-proposed: *product-group-constrained
exchanges* (dropped — any replacement is allowed, non-subsidised items are paid for by the customer), and
*"nothing executes until the case resolves"* (reversed by decision 11).

---

## 4. The phases, in the order they were built

### 0b — reason and action by id *(complete, tested 2026-08-27)*

`ReturnRequestItem` stored the reason and action as **free text**, and every rule lookup matched on that
text. Two live bugs followed: a return submitted in a **non-default language** stored a localised name
that matched no rule, so no rule ever resolved; and **renaming** a reason in admin silently broke every
in-flight return pointing at it.

Shipped: `ReturnReasonId` / `ReturnActionId` on the entity, the admin screen moved from free text to
dropdowns, and null-guarded lookups that **fail closed with a warning** rather than throwing.

### 1e — layering *(complete, tested 2026-08-27)*

`ReturnRequestStatusChangeData` sat in `Domain/` and depended on an admin model; a public factory served
admin models. Fixed by mapping at the controller boundary and splitting
`ReturnRequestExchangeModelFactory` into `Areas/Admin/Factories/`. The layer rule is in `CLAUDE.md` §5.1.

> **A lesson worth carrying.** 1e's own verification step named a *type* — and phase 3a later deleted
> that type entirely. The step became impossible to execute and its companion grep passed **vacuously**.
> Write verification against **what must remain true**, not against the object that currently makes it so.

### 1 — the return case aggregate *(complete)*

`ReturnCase` / `ReturnCaseLine` / `ReturnResolution` / `ReturnCharge`, their builders, the migration, and
the wizard writing and submitting a `Draft` case. `ReturnResolution` and `ReturnCharge` were left inert
here on purpose and are written by phase 3a.

### 2 — the settlement pipeline *(complete, tested 2026-08-19)*

The biggest single fix in the rebuild. It closed a **live over-consumption bug**: subsidy allocated
against the gross cart *and* credit notes applied against the gross order, draining both pools for one
order.

Shipped: `ISettlementSource` with `Priority` **as data** rather than as call order; credit notes and
subsidy as the two implementations; both the cart totals and order placement routed through the same
pipeline, so a quote and an apply cannot disagree. Also: the fourth duplicate allocation behind the
subsidy bar, returned items no longer consuming subsidy, and the approved-exchange cart link —
`ShoppingCartItemId` had always been 0, which made exchange fees and the returned-item subsidy rule
unreachable.

### 3a — the resolution writer, and 4.1's money model *(complete, tested 2026-08-26)*

A `ReturnResolution` per case, written once, with its fees frozen into `ReturnCharge` rows at that
moment. Before this, fees were recomputed from live rules every time, so editing a fee setting
retroactively changed settled returns.

Decision 11 landed in the middle of this phase and reversed its premise: a line executes the moment it is
authorised, and the resolution only records and freezes. The manual `StaffShipQty = 0` step is **kept
deliberately** — it is how Sales Support stop a parcel being charged redelivery twice.

### PackName order shipments *(complete, tested 2026-08-19 — not a numbered phase)*

Found during phase 2 testing: shipments were **unreachable** on a packname order, because a jQuery
selector meant to hide the order-level shipping panel also hid the shipments card and its Add button.

Rebuilt around the **shipping destination** — a distinct address plus method, and the packnames sharing
it. Per-destination Add, a server-side guard that a shipment cannot span two destinations
(`AddShipmentDestinationFilter`), and the per-packname shipping status unfrozen. Everything phase 9 later
did rests on this.

### 5 — subsidy restoration *(complete, tested 2026-08-26)*

Subsidy given back **in kind** as insert-only compensating rows in `CORE_SUB_SubsidyRestoreItem`, tagged
with `ReturnCaseLineId` — not `ReturnResolutionId`, because a line executes before its case resolves —
and netted into both availability views by a shared SQL fragment.

Credit notes needed nothing built: they restore **by re-issue**, which is why `ISettlementSource` never
gained a `RestoreAsync`. Cancellation was confirmed to need no write by reading the view SQL
(`o.OrderStatusId != 40`).

Also delivered: the dead excl-tax chain deleted from the three subsidy ledgers, and the ledgers indexed.

> **The bug that nearly shipped.** The first draft summed `ReturnedQuantity` across restore rows. One
> return writes **one row per funding bucket**, each carrying the same `ReturnedQuantity`, so a
> two-bucket item double-counted and the second partial return read as the whole item coming back. Fixed
> with `GroupBy(ReturnCaseLineId).Sum(line => line.Max(...))`. Caught in review because there was a spec
> to review against.

### 9 — per-destination shipping information *(complete, tested 2026-08-27)*

`Order.ShippingAddressId` on a packname order is a **placeholder** — the first packname's address — and
every nop surface that answered "where did this go" read it. The release-blocking part was the **packing
slip**: the document that goes on the box, so parcels were physically dispatched to the wrong branch.

Seven items shipped: the packing slip, the admin shipment list's location filters, the customer shipment
page, 16 `%Shipment.Shipping*%` tokens plus the three shipment templates, the customer order page's
**Delivered to** column, the invoice PDF, and the eight order-level emails. The last two are decision 13.

Two surfaces named in the original spec turned out **never to have been broken** — the customer's order
details page suppresses the order-level block on a packname order, and the admin shipment screens were
*silent* rather than wrong.

### 4b — subsidy first, value back in kind, replacement draws on it *(complete, tested 2026-09-11)*

Supersedes 4a. Decisions 15–22 are in §3; the 4.1/4.9 evidence behind them is summarised there. Three moves:

1. **Ordering** — `SubsidySettlementSource.Priority` 1, `CreditNoteSettlementSource.Priority` 2. Nothing
   else in the pipeline cares.
2. **Restoration on every authorised line**, both modes, before the credit note is sized and before the
   replacement reaches the cart — which also fixes an ordering bug where `RestrictToAllocatedQty`
   refused the replacement before the returned unit had been given back. `RestoreSubsidyForReturnedItemAsync`
   returns a `SubsidyRestoreResult` whose `MoneyEquivalent` (money buckets + units × unit price incl tax)
   is subtracted from the returned value to size the note: **the note is only ever the customer's own
   money.** `IsCreditNoteNeeded` is rules-or-exchange (decision 17); the cheaper-replacement clause and
   the replacement-price cap are gone — both fall out of the arithmetic.
3. **The replacement draws on the restored entitlement** — the returned-item skip in
   `SubsidyAllocationService.AllocateGoodsAsync` is deleted. Until 4b-3, pathway A (auto-place) runs
   only when the line gave nothing back, because `PlaceReturnOrderAsync` settles credit notes only.

**What validation added.** Thirteen fixes came out of running it. Three are worth carrying forward as
design, the rest are recorded here so they are not re-derived:

| Fix | What it was |
|---|---|
| 3 | Restoration budgeted units and money **independently**, so a partial return gave back a whole unit *and* a pro-rata slice of money for the same garment. Rewritten as two passes over one shared returned-value budget — **§8** |
| 5, 9 | The admin Fees and Charges panel stated a credit note that was never raised; the fees-not-deductible warning implied boxes that are already disabled by then (decision 26) |
| 4 | The customer-facing wizard promised store credit that no longer exists once subsidy is restored first |
| **6, 8, 11** | **An Exact Exchange must be priced at what the customer paid** (decision 24). `ExchangeReplacementScope` carries `(ReturnRequestItemId, ReturnedOrderItemId, ExchangeType)` through `GetShoppingCartItemUnitPriceEvent`. Three passes were needed because three different callers price the replacement |
| **7, 10** | **Quantity rules must not block a partial exchange** (decision 25) — exempted at the approval's add *and* at nop's revalidation at checkout |
| 12, 13 | Not this work at all: a customer with no `Customer.BillingAddressId` could never check out, and the checkout screen and the placed order could bill different addresses. See the changelog |

**The trap, stated once because it caught us three times.** A fix was correct for the path it was written
against while **a second path did the same job elsewhere**: 6 covered persisted cart rows and missed the
detached cart in `PlaceReturnOrderAsync` (8); 7 covered the approval's add and missed nop's revalidation
at checkout (10); 8 covered auto-place pricing and missed the **pathway A/B decision**, which prices a
detached cart before any scope opens (11). Before calling replacement pricing or replacement validation
done, sweep **every** caller. The scope now wraps the whole of `ExecuteExchangeWorkflowAsync`, which is
the outermost point at which a replacement is priced; there is no earlier caller left.

**Validated 2026-09-11 across twenty-three scenarios**, orders 81–112 and return cases 73–88, on a live
store with 10% GST. The settlement matrix (units, group credit, global credit, topups, notes, cash and
every mixture), both modes, both pathways, partial and whole returns, tier prices, 2/4/6 quantity rules,
undo-and-re-approve, manager carts ordering for several packnames, and returning one employee's line out
of a manager's order. Two that had never been exercised before:

- **Subsidised shipping** (`IncludeShippingOnSubsidy`). Switching a line from $0 to $20 freight moved
  Subsidy Used $30 → $50, and the order breaks it out as *For Stock $30.00 / For Shipping $20.00*. On the
  return the $30 of goods went back to the bucket it came from and **the $20 of freight did not** —
  decision 21 confirmed at runtime.
- **A quantity-only subsidy** (credit zero everywhere). An allocated unit covers the garment's full price;
  authorisation raises no note and does not throw; the unit comes back. This is §8's boundary case —
  value budget zero, unit budget one — and the zero value budget does not starve the units.

**Two things the run left for someone else.** The freight-plus-redelivery double charge is question 15 to
the manager, demonstrated on order 102; who owns the choice of billing address at checkout is question 16.
Neither is a defect in this phase.

### 4b-3 — the pathway decision asks the pipeline, and pathway A can spend subsidy *(complete, tested 2026-09-11)*

4b left one thing wrong. The pathway A/B test read
`!subsidyWasRestored && creditNotesBalance >= total && no fees`, in which `!subsidyWasRestored` stood in
for "credit notes are the right payer" — so the **everyday** case (faulty garment, no fees, one unit back,
one unit out) went to the cart for a $0 checkout. It had to: `PlaceReturnOrderAsync` settled with
`packNameCart: []`, so subsidy was structurally unable to pay an auto-placed order. Both halves moved
together.

1. **The decision is a quote, not a guess.** `IOrderProcessingOverrideService.QuoteReturnOrderSettlementAsync`
   walks the pipeline against the same packname cart and the same detached template that
   `PlaceReturnOrderAsync` will settle, so `BalanceDue == 0` is a promise. Auto-place when the quote covers
   the replacement and this line owes no fees. It subsumes the old test — a note-funded exchange with no
   fees still auto-places.
2. **`PlaceReturnOrderAsync` settles with the returned item's packname.** `BuildReturnOrderPackNameCartAsync`
   projects the detached template onto the packname carried by the returned order item, so the entitlement
   restored by this very approval pays for the replacement. Empty for a non-packname original, which keeps
   credit notes as the only payer there. `MoveShoppingCartItemsToOrderItemsAsync` returns the
   cart-item → order-item map that `SubsidySettlementSource.ApplyAsync` needs.
3. **Shipping is passed as empty, never computed**, on this path: a return-derived cart has no selected
   shipping option to rate, and an exchange's delivery is the redelivery **fee**, which settlement may
   never pay.

**Three invariants worth keeping, because breaking any one is silent:**

- **The projected cart items keep `Id == 0`.** `ExchangeReplacementPriceConsumer` branches on exactly that
  to choose between `ExchangeReplacementScope` and a saved cart row's link. A synthetic id sends it after a
  link that does not exist and re-prices the replacement live, undoing fixes 6, 8 and 11.
- **So the order-item map is keyed on 0.** A return-derived cart is always one line
  (`BuildProductsListForReturnOrderAsync` builds one), so `{ 0 → orderItemId }` is well defined. That
  assumption is load-bearing and now throws rather than silently overwriting.
- **An empty `SettlementPlan` is indistinguishable from a fully-covered one** — both report
  `BalanceDue` 0, because an empty plan has `SettleableAmount` 0. The decision therefore requires the quote
  to have priced something before trusting it, so a failed quote falls to the cart rather than placing an
  order nothing has paid for.

**Six fixes came out of reviewing and running it** (12 and 13 were general checkout defects found while
building a fixture, not this work):

| Fix | What it was |
|---|---|
| 12 | A customer with no `Customer.BillingAddressId` could never check out. The screen resolved and posted one; `PlaceOrderAsync` read only the column, which nothing but nop's stock checkout ever writes |
| 13 | The checkout screen and the placed order could bill different addresses — the factory ran its email/name matcher before consulting the address of record |
| 14 | `BuildCartSettlementContextAsync` built the packname cart from the customer's **saved** cart even when handed a detached one, so a return order's total was netted by subsidy allocated against whatever else was in their basket |
| 15 | The auto-placed order's total was the pre-subsidy figure. It is now corrected from the plan that was actually applied — and an invariant logs loudly if a quoted-as-covered order settles with a balance, since the order is marked Paid unconditionally |
| 17 | ⚠️ **`MoveShoppingCartItemsToOrderItemsAsync` cleared the customer's entire cart** — deleting what they were buying and, worse, other approved replacements whose cart rows carry the link the price consumer and the completion flow read. The order was built from a synthetic cart that was never in the cart, so there was nothing of its own to clear. Now scoped to this return's own rows. See gap **P-02**: the packname placement path still has the same bug |
| 19 | An auto-placed replacement had no `OrderShipping` row, so it showed "No shipping address recorded" and printed no ship-to on invoice, packing slip or email (decision 13) |

**Validated 2026-09-11**, orders 113–120 and returns #89–#93:

| Scenario | Result |
|---|---|
| Unit-funded Exact, faulty, no fees | Auto-placed at **$0.00**, paid by Subsidy, destination recorded |
| The same with fees | Cart, payable exactly the fees — then completed by the customer, fees paid |
| Catalogue price moved $30 → $45 after purchase | Auto-placed **at the price paid**. This is decision 24 through the auto-place path, which fix 11 never covered at runtime |
| Manager's order, employee's line returned | The **employee's** packname is charged: $40 → $30 → restored $40 → $10. The manager's own entitlement untouched |
| Customer's cart during an auto-place | A decoy row planted before approval survived intact |

**Different Style, run 2026-09-11.** Cheaper (Nike $30 → Custom T-Shirt $15, unit-funded, no fees)
auto-placed at $0.00 with the replacement priced **live** at $15 — the scope correctly does *not* freeze
Same/Different Style — and the $15 in the customer's favour raised no note, as decision 14 requires.
Dearer ($30 → $80) went to the **cart** with the balance payable, as it should.

⚠️ **But the dearer run exposed a defect the cheaper run had hidden — see §7.5.** The auto-place path does
not validate the replacement, so it placed an order for a product whose **required attribute was never
supplied**; the cart path refused the same product correctly.

**Not run:** a note-funded no-fee exchange (the subsumption claim — sound by inspection, since credit notes
do not depend on the packname cart, but not exercised). **Not reachable:** a non-packname original, because
every order item on this store carries a packname — the same conclusion as U15.

**Undo after an auto-place is 4.1's behaviour, not a loss.** 4.1's admin view
(`NOP41 .../Views/ReturnRequest/Edit.cshtml:51`) shows Undo Approval only when `NeedsCompletion`, so an
auto-placed exchange never had it there either. To reverse one, cancel the replacement order: every
consumption and balance query filters cancelled orders out (4.9 lines; on 5.00 `CWDatabaseService.cs:413, 439, 467, 649, 661, 728` and the
credit-note views `:1051, :1078`), so the entitlement and any notes come back without a reversal being written. 4.1
needed `OrderCancelledEvent` handlers for this because it stored balances; live-computed balances (§2 rule
3) mean 4.9 does not.

### 4b-4 — the picker refuses what it cannot specify; the packname checkout stops deleting unordered lines *(complete, tested 2026-09-11)*

Two defects closed on the same day 4b-3 was signed off, both found by following 4b-3's own findings
upstream. Neither changed a workflow; both removed a way for a customer to lose something silently.

**Fix 21 — the replacement picker (§7.5's other half).** The wizard's picker rendered only attributes it
could draw as a dropdown and dropped the rest, then let the customer choose the product anyway. The
product's required free-text, date or upload attribute was never collected, so the exchange could not be
completed on either pathway.

The picker now decides once, server-side, what it cannot collect, and refuses the selection with that
reason. `ProductsHelper.FindUncollectableRequiredAttributes` mirrors exactly what
`_ReplacementProductAttrs.cshtml` renders — the first colour attribute *with values*, plus every non-colour
attribute *with values* — and reports every other required attribute by name;
`ExchangesModelFactory.PrepareReplacementProductOptionsModelAsync` adds rental periods and gift-card
details, which the picker has never had fields for. The names reach the view and the script through one
model property and one resource string, so the static notice and the click-time error can never disagree.
Both entry points (the wizard's Same Style / Different Style, and Edit Exchange Item on the returns list)
funnel through `chooseProduct`, so one guard covers both.

Two bugs were found and fixed while reviewing it:

- `ExchangesHelper.HasProductAttributes` tested `Count >= 0`, true for every non-null list, so the guard
  never guarded anything and an attribute-free product rendered an empty attributes block carrying the
  message *"The product does not contain attributes that can be used"*. That message — inherited verbatim
  from 4.1 — is gone.
- A valueless attribute whose name began "Colour" rendered a dropdown containing only `- Select -`, a dead
  end: the customer was told to choose every option with no option to choose. Both the view and the helper
  now require `Values.Any()` on the colour attribute too.

**Conditional attributes are deliberately not treated as blocking.** nop enforces one only when its
controlling attribute is set, and the picker does not evaluate conditions, so blocking on them would refuse
products that are in fact orderable.

**Fix 22 — the packname checkout (gap analysis P-02).** `MovePackNameShoppingCartItemsToOrderItemsAsync`
ended with `ClearShoppingCartAsync(customer, storeId)`, which deletes **every** cart row for that customer
and store. But the order is built from `details.PackNamesCart`, which comes from the
`PackNameShoppingCartItem` **view** — a chain of INNER JOINs through the packname cart link, the packname,
its list, its branch and `SubsidyPackNameCanOrder`, filtered to `CanOrder = 1`
(`CWDatabaseService.cs:884`). A cart row with no packname link, or one whose packname can no longer order,
is not in the order — and was deleted anyway. The customer paid for what they ordered and silently lost the
rest of their cart. It now deletes exactly the rows that became order items, which is what 4.1 did.

Two details that matter if this is touched again: the **id** overload of `DeleteShoppingCartItemAsync` is
required, because `sc` is a view entity and the entity overload would aim the delete at the view; and
`resetCheckoutData` is false because `placeOrder` calls `ResetCheckoutDataAsync` immediately afterwards
with `clearCouponCodes` and `clearCheckoutAttributes`, the same order 4.1 used.

`ClearShoppingCartEvent` is no longer published on this path. Nothing in `Nop.Plugin.CW.Core` consumes it,
`EntityDeletedEvent<ShoppingCartItem>` still fires per row, and `Customer.HasShoppingCartItems` is still
maintained — the same trade fix 17 made on the return-order path.

### 3b — serialisation, and the invariant the database owns *(complete, tested 2026-09-11)*

Re-scoped on survey before a line was written, because two of its four original pieces were already gone:
`ReturnRequestStatusChangeData` had been retired, and "execution held until the case resolves" was
reversed by decision 11. The fee walk it was to close is closed by decision 30, not by code. What was
left was the lock and the transaction — and the transaction turned out not to exist (decision 31).

**What shipped, fixes 23-31:**

| Fix | Closes |
|---|---|
| 23 | Two administrators deciding sibling lines could each write a resolution — two sets of frozen charges, the customer billed twice |
| 24 | A double-submitted approval restored subsidy, raised a credit note and placed an order twice over |
| 25 | The staff fee-quantity cap existed only in the browser; the server stored and priced whatever was posted |
| 26 | Undo Approval could interleave with a running approval, reversing rows it was still writing |
| 27 | A resolution was written under the case lock but voided outside it, from two call sites |
| 28 | `ReturnsController`'s undo handler dereferenced a null `returnRequest`, giving a 500 instead of a message |
| 29 | A busy lock let the caller proceed and price from the stale resolution; now retried, then logged as an error |
| 30 | A stale comment promising "3b's transaction" in `ReturnCaseService.ReopenCaseAsync` |
| **31** | **The unique index that actually makes one-resolution-per-case true** |

⚠️ **The lesson of this phase, and the thing most likely to be undone by someone who skims it.** Fixes 23,
24, 26, 27 and 29 are *best-effort*. Live testing proved `MemoryCacheLocker` does not serialise genuinely
simultaneous callers (§6), so they de-duplicate collisions far enough apart to see each other — a
double-clicked Save — and nothing closer. Three simultaneous saves of one case's lines produced **three**
resolutions and three sets of frozen charges. **Fix 31's unique index on `ReturnResolution.ReturnCaseId`
is the only reason that invariant now holds.** Do not remove it in favour of "the lock already handles
it".

`ResolveCaseCoreAsync` catches the insert failure and re-reads rather than matching a provider-specific
duplicate-key number: if a resolution exists by then, a concurrent save won and this one stands down
having written nothing. Charges are written after the insert, so nothing is orphaned.

**Validated live**, 2026-09-11: two saves entering the approval workflow in the same millisecond, each
reopening a resolved case, each raising its own credit note, produced exactly **one** resolution and one
"resolved by a concurrent save" stand-down, with no error surfaced. The same shape before fix 31 produced
two. A double-clicked approval was refused with one credit note raised. `ClampStaffQuantity`'s negative
branch is unreachable through the UI — `ReturnExchangeRequestValidator` rejects a negative first — so it
is defensive only.

**Still open and deliberate:** a second credit note from a *genuinely simultaneous* double approval of the
same return has no natural database constraint, so it stays best-effort; the realistic double-click is
covered. The pure `Plan(...)` was not built — its value is unit-testability and it is the only half
blocked on `CLAUDE.md` §22 Note A.

### 4a — delta settlement *(specified, superseded by 4b — see §7)*

On the final day, a double-compensation bug was confirmed on live data: a $45 item funded by one
product-group unit, exchanged with $8 of fees, returned **both** the unit **and** a $45 credit note. The
customer paid $8 and kept a spare unit.

The correct design — settle only the difference — was specified and deferred, because it changes an
amount read by the cart page, the checkout, order placement and the admin screens, and the risk of the
figure shown diverging from the figure charged outweighed doing it in a hurry.

**A tactical fix is in the build instead.** Three changes: restoration is skipped when a credit note gave
the same value back; the exchange credit note is **capped at the replacement's price** so a cheaper
replacement leaves no windfall; and the credit-note workflow returns whether it actually wrote a note
rather than the caller assuming it did. §7 says what that leaves wrong.

---

### Housekeeping done at the pause *(2026-08-27)*

Four **write-only** columns dropped from the return case aggregate with their writes
(`ReturnCaseLine.ReplacementUnitPrice`, `ReturnCase.UploadedFileId`, `ReturnResolution.ExchangeOrderId`
and `.CreditNoteId`), plus seven service members with no callers. ⚠️ Two of those —
`DeleteReturnCaseLineAsync` and `DeleteReturnCaseLinesAsync` — were the aggregate's **only line-removal
API**; if phase 3a's re-open path ever needs to drop a line, one comes back. Detail in the DevNotes entry
for that date.

Also at the pause: `vw_CORE_SUB_OrderSubsidyTotal` changed from `UNION` to `UNION ALL` (each arm already
groups by `OrderId`, so the de-duplication could only ever collapse two zero rows).

---

## 5. What is not built

| | Work | State |
|---|---|---|
| **3b** | Serialisation and the resolution invariant — §4 above | ✅ **Complete, tested 2026-09-11.** Re-scoped on survey: `ReturnRequestStatusChangeData` was already retired, "execution held until the case resolves" was reversed by decision 11, and the fee walk is closed by decision 30 |
| **3b — the planner** | The pure `Plan(ReturnResolutionInput)` with no I/O, and `ResolveAsync` built on it | **Not built.** Its value is unit-testability, so it is the only piece genuinely blocked on `CLAUDE.md` §22 Note A. Nothing else needs it |
| **4a** | Delta settlement — §4 above | **Closed.** Superseded by **4b**, which shipped 2026-09-11 |
| **4** (rest) | `OrderReturnOrigin`, retiring `PlaceReturnOrderAsync`, frozen prices | Deferred. None is needed for 4.1 parity |
| **8a** | Pickup in store for packnames | ⏭️ **Skipped by decision, 2026-08-27.** Not needed for the first release. The spec stands; nothing depends on it |
| **6** | Collapse the three rule tables into `ReturnPolicy`; rewrite the admin config grids. Do **B7** in the same pass | Backlog. Needs a three-into-one data migration |
| **B4** | Credit-note liability reporting views — replace per-store application loops with aggregates | Backlog. Pure performance |
| **B5** | Jim2 / Punchout sync | **Out of scope by decision.** It is a *port* — 4.1 has both fully implemented — not a new build |
| **B6** | Five small cleanups | Backlog |
| **B7** | Remove orphaned rule rows when a reason or action is deleted | Backlog. Deliberately no FK constraints — they would break nop's own admin delete |
| — | `FeeConceptItem` versus `ReturnCharge` | Answer it when 3b lands, not before |

**One decision still owed:** `CLAUDE.md` §22 **Note A** — whether CW tests live in `Tests/Nop.Tests`
(which conflicts with the Cardinal Rule) or a new `Tests/Cw.Tests`. **No automated tests ship with any CW
plugin today.** Every phase was verified by a person following a written plan. That worked, and it does
not scale. *(Decided since: `Tests/CW.Tests`, `CLAUDE.md` §22 Note A. On 5.00 its `Core/` folder holds 631 CW.Core
tests.)*

## 6. Platform limits, and what will bite on the next nop upgrade

### 6.0 nop's `ILocker` does not serialise simultaneous callers — **read this before trusting any lock**

**Proven live, 2026-09-11 (phase 3b).** `MemoryCacheLocker.RunAsync` acquires through
`_memoryCache.GetOrCreate(key, factory)`. That extension is `TryGetValue` → on a miss run the factory →
`Set`, and it is **not atomic**; Microsoft documents that the factory may run more than once. Concurrent
callers all miss, each builds its own `Lazy`, each sets its own `started = true`, and **all of them
proceed**. The `isThreadSafe: true` on the `Lazy` protects one instance's value creation, which is
useless when every racer holds a different instance. `DistributedCacheLocker` has the same
read-then-write shape and is no better.

Measured behaviour: two requests ~140ms apart — the second correctly refused. Three requests dispatched
in the same tick — all three acquired, and one return case was resolved three times.

**So:** `ILocker` de-duplicates human-timescale collisions (a double-clicked Save) and nothing closer. Any
invariant that must hold belongs in the database — see decision 32 and fix 31's unique index on
`ReturnResolution.ReturnCaseId`. This applies equally to the pre-existing
`OrderProcessingOverrideService.RunWithPackNameLocksAsync`, which has never been anything stronger.

**And it is per-process.** `Presentation/Nop.Web.Framework/Infrastructure/NopStartup.cs:123-127` selects
`DistributedCacheLocker` only when `DistributedCacheConfig.Enabled`; otherwise `MemoryCacheLocker`, whose
state lives in `IMemoryCache` and whose own comment says *"the cache is not shared with other
instances"*. **If the store is ever deployed multi-instance, every lock in this plugin — subsidy included —
becomes decorative unless that flag is on.** This belongs in the production topology decision, not a
backlog.

### 6.1 Two things that will bite on the next nop upgrade

Recorded in `CLAUDE.md` §10.3 and §10.4, repeated here because they are easy to miss.

**Copied nop method bodies** — `CLAUDE.md` §10.3 lists them all (they were re-diffed against 5.00 in the port); the
first three were `ShipmentService.GetAllShipmentsAsync` (~113 lines),
`PdfService.PrintOrderToPdfAsync` (~60) and `PrintPackagingSlipToPdfAsync`. nop builds each inline with no
hook, so the body was copied and one part changed. **An upgrade can change the original underneath us
with no compile error and no test failure.** Diff each against the new base and re-apply.

**The view location expander puts plugin views ahead of theme views.** Where a theme customises a view the
plugin also ships, the theme's version silently never renders. Five files collide; the list and the check
are in `CLAUDE.md` §10.4, with the check to run when a theme is enabled. On 5.00 `CW.Standard` ships none of the
five; nop 5.00's `ThemeableRazorViewEngine` also lets a theme replace a CW view opened **by path**
(`Themes/{theme}/Plugins/CW.Core/Views/…`).

## 7. What is knowingly wrong, and should be fixed when this is picked up

**In priority order.** Everything here is understood; none of it is mysterious. The two items that used
to head this list — the value coming back as store credit rather than in kind, and a cheaper replacement
under-compensating a credit-funded original — were **fixed by phase 4b and removed 2026-09-11**. The
route is now in kind, and the under-compensation falls out of the arithmetic in §8.

### 7.3 ~~The customer's fee estimate is banded; the charge is per line~~ *(not a defect — closed 2026-09-11)*

**This entry was wrong, and is kept with its correction because the wrong version sent someone looking
for a fix.** It claimed the confirmation screen was "the only place a customer is shown one number and
charged another". It is not, because the screen does not claim to show the charge.

The two figures do differ. The wizard summary prices each rule **once over the summed quantity of the
whole return** (`_FeesAndChargesList.cshtml:18`, `_SummaryTotals.cshtml:38`);
`CalculateExchangeFeeAmountsForCaseAsync` prices it **once per line**. Two lines of 2 units against a
"$5 per 4 units" rule: the screen says $5, the raw server figure is $10.

**That gap is the manual step, not a bug.** Per decision 30 the per-line figure is the *pre-adjustment*
starting point, and Sales Support bring it to the real number with `StaffShipQty` / `StaffRepackQty` —
both editable boxes on the approval screen (`_FeeConceptRow.cshtml`, seeded at
`ReturnRequestExchangeModelFactory.cs:147-148`, saved and now server-clamped in
`ReturnsController.UpdateExchangeReturnDataAsync`). The summary's per-case figure is a reasonable estimate
of what staff will actually charge, which is why it was written that way.

**And the screen says so:** the block is titled *"Fees estimates"* and carries *"(**) Fee amounts are
estimates and may change after our staff reviews your return request."*

Nothing to fix here. What was genuinely wrong was the design document — see decision 30.

One cosmetic leftover: `_SummaryTotals.cshtml:36-39` re-implements `_FeesAndChargesList.cshtml:18`. Two
transcriptions of one loop in two views, agreeing today only because nobody has edited one of them.

### 7.4 Smaller, all documented where they occur

- **Restoration is not transactional** with the rest of the approval, and **cannot be made so** — nop 4.90
  has no transaction to build on (decision 31; see its 5.00 note — a `TransactionScope` is available on 5.00). If the process dies between registering the replacement
  and writing the ledger, the replacement is in the cart and the entitlement is not back. Re-saving fixes
  it. Compensating actions (`CleanupFailedExchangeWorkflowAsync`) are the correct design here; do not
  re-spec a transaction.
- **A queued approval email survives an undo.** `WorkflowMessageService.SendNotificationAsync` (nop 5.00: `WorkflowMessageService.cs:2894`) inserts
  a `QueuedEmail` row rather than sending, and `CleanupFailedExchangeWorkflowAsync` does not delete it — so
  an undone approval still mails the customer if the send task has not run. Needs a nullable
  `ApprovalQueuedEmailId` on `ReturnRequestItem`, set when the notification is queued and used to delete
  the row on cleanup. Found in phase 3b, not fixed.
- **Restored entitlement is spendable between approval and checkout.** 4.1 has the same gap.
- **A quantity-only subsidy shows no quantity anywhere** — the storefront panel reads $0.00 and the admin
  grid reads 0% used, before and after a unit is spent, because both are money-based. The allocation is
  consumed correctly; it simply cannot be seen. Gap analysis **M-06**.
- **Frozen figures drift if a fee rule is edited mid-case.** Narrower than 4.1, which re-prices for ever.
- **Undo does not cancel an auto-placed order** (pathway A). Exact 4.1 behaviour.
- ~~**An attribute named "Collar" is treated as the colour attribute.**~~ *Fixed (fix 32, before the 5.00 port).*
  `ProductsHelper.IsColourAttributeName` matched any name *starting with* `"col"`, so `Collar`,
  `Collection` and `Column` all matched. It now matches names starting with `color` or `colour`
  (`Helpers/ProductsHelper.cs:34`); `Tests/CW.Tests/Core/Helpers/ProductsHelperTests.cs` pins that *Collar* is not
  taken for the colour attribute.
- **A required attribute the picker cannot draw** still makes a product not exchangeable online — but the
  customer is now told which details cannot be taken and pointed at support, rather than being allowed to
  choose it and fail later. Fix 21, phase 4b-4.
- **`vw_CORE_SUB_OrderSubsidyTotal`** and **`CORE_SUB_SubsidyOrderShipping`** — the `UNION` was fixed
  2026-08-27; the missing index on the shipping table is a decision, not an oversight.
- **The customer's order page shows no order-level ship-to on a packname order.** It never did; the
  packname sections carry the real addresses. Conscious.

### 7.5 A hand-crafted post can still submit an unspecifiable replacement *(closed in the 5.00 port)*

**Found 2026-09-11 running phase 4b-3's P5/P6; both halves fixed the same day by phase 4b-4.** Kept here
because one narrow route remains open.

The original defect had two halves. `PlaceReturnOrderAsync` built a **detached** cart and never called
`AddToCartAsync`, so none of nop's item warnings ran — attributes, required products, rentals, gift cards.
Meanwhile the wizard's picker offered products whose required attributes it could not collect. Demonstrated
with nop's sample **Custom T-Shirt** and its required free-text attribute ("Enter your text"): pathway A
placed order 122 with the T-shirt line and **no attribute description at all** — a garment that can never
be produced — while pathway B correctly refused the identical replacement.

- **Fix 20** (phase 4b-3) closed the placement half: the auto-place path now refuses to place what a cart
  would reject, so a mis-specified replacement fails loudly at approval instead of shipping.
- **Fix 21** (phase 4b-4) closed the picker half: the product stays visible and is refused on selection
  with the reason, per decision 28.

**What is still open.** Fix 21 is a UI refusal. A hand-crafted POST of `replacement_id_<orderItemId>` for a
blocked product still creates a submitted exchange that support cannot complete — fix 20 stops it becoming
a bad order, but the case sits there. Closing it properly means a rule in
`Validators/Exchanges/ReturnWizardSubmissionValidator.cs`, which today has no product lookup and would need
`MustAsync` plus an injected service; roughly 30 lines. Deliberately deferred, not forgotten: the exposure
is a customer crafting a post against their own exchange, and the outcome is a case support can decline.

**Closed in the 5.00 port (phase 5g).** Not in the validator but in `IReturnSubmissionLogic.CheckSubmissionAsync`,
which every wizard post (gather, summary, confirm) runs: `ReturnSubmissionLogic.IsReplacementValidAsync` refuses a
replacement whose type does not fit, a gift card or rental, a required option without one of the product's values
(so a free-text attribute can never pass), and a product outside the packname's catalogue.

### 7.6 The quantity exemption is per row, and that is proven — not assumed

**Asked and answered 2026-09-11.** A fair worry: an approved exchange sits in the cart exempt from the
catalogue's selling quantities; if the customer then adds more items to that same cart, does the exemption
leak to them?

**It does not.** `ShoppingCartOverrideService.IsExchangeReplacementAsync(customerId, shoppingCartItemId, storeId)`
decides it **per row**, from the recorded `ReturnRequestItem.ShoppingCartItemId` link. The
`ExchangeReplacementScope.IsActive` short-circuit above it only fires while an approval is running — at
checkout no scope is open, so every row is judged on its own link.

Demonstrated end to end: a cart holding an approved replacement (product 28, qty 1) beside an ordinary
line the customer added (product 30, qty 2), then **both** products given a minimum the cart breaks —
28 → min 2, 30 → min 4. The checkout rendered exactly one warning, against the ordinary line:

```
Nike SB Zoom Stefan Janoski "Medium Mint"  NK_ZSJ_MM  $30.00   (no warning)
Oversized Women T-Shirt                    WM_OVR_TS  $24.00   "The minimum quantity allowed for purchase is 4."
```

and refused to place the order. The replacement was exempt, the ordinary item was not, in the same cart at
the same moment. Note also that the exemption never relaxes anything else: `GetStandardWarningsAsync`
re-validates a replacement at the **rule-satisfying** quantity rather than skipping the checks, so stock,
call-for-price and rental stay honest and can only ever be stricter than the row.

## 8. Restoration arithmetic — worth knowing before touching it

Undocumented outside the method body until now, and surprising. **Rewritten 2026-09-11 (fix 3): the
description that stood here before was the arithmetic that produced the bug.**

**Restoration is per bucket, mirroring consumption — not the reverse of it.**
`RestoreSubsidyForReturnedItemAsync` reads the `CORE_SUB_SubsidyOrderItem` rows for the order item, one
per funding bucket **ordered by `Id`** — the order the allocator wrote them, therefore the order they were
consumed in — and writes a compensating row per bucket.

**A whole-item return restores every bucket in full**, units and money, with no budgeting at all.

**A partial return spends one shared budget in two passes.** This is the part that was wrong: units and
money used to be budgeted *independently*, so returning one $21 garment gave back a whole unit **and** a
pro-rata slice of money for the same garment — the customer was compensated twice.

1. **Units first, against `unitBudget = cumulativeReturnedQuantity`.** Walk the buckets in consumption
   order taking `Quantity` then `QuantityTopup`, decrementing the budget as you go.
2. **Money second, against whatever value the units did not already cover** —
   `valueBudget = max(0, round((cumulativeQuantity − unitsRestored) × unitPriceInclTax, 2))` — spent in
   the allocator's own order: the product group's credit, its topup, global credit, the global topup.

The `max(0, …)` is load-bearing. **A unit cannot be split**, so a restored unit can be worth more than the
quantity being returned is owed; when it is, there is nothing left for the money buckets and the floor
stops the budget going negative. That rounding is in the customer's favour, and it is deliberate.

Worked through: return 1 of 3 on a line funded by 2 units and $15 of credit. `unitBudget` 1 → one whole
unit comes back; `unitsRestored` 1, so `valueBudget = (1 − 1) × price = 0` and **no money is restored**.
One unit, nothing else. Under the old arithmetic the same return also paid back $5.

**Decision 23 is the ordering, and it is a choice.** Subsidy is recovered first and cash second rather
than pro rata, so a customer returning half an order gets all of their allowance back up front. The
totals reconcile either way — the second half comes back as a pure-cash note — so this is a policy the
manager could reverse with a change to the value budget, not a correctness matter.

**Idempotency is keyed on `ReturnCaseLineId`**, not on the order item. A line can execute more than once
(undo then re-approve, or a re-opened case), so the line's own restore rows are deleted before the run.
And `previouslyReturnedQuantity` is grouped by case line taking the **max** within each group, never
summed across rows: an item funded from two buckets writes two rows carrying the same returned quantity,
and summing them would read the second partial return as the whole item coming back.

## 9. If you are picking this up cold

**State at 2026-09-12: exchanges, returns, credit notes and the subsidy integration are built and
validated against a live store. Nothing here blocks a release. Everything below is either an
improvement, a decision someone owes, or a limit of the platform.**

1. Read §2, then §3. Most questions are already answered there.
2. Read §6.0 before trusting any lock in this plugin, and §7 before writing anything — §7 is the
   difference between the design and the build.
3. Check `NOP41/` before calling anything a defect (§2 rule 1).
4. `Exchanges_Target_Design.md` is the design the phase records cite by section number — but **§3 of it is
   reversed by decision 30 and §6.2 is impossible by decision 31**, and both are marked in place. Do not
   implement either.
5. Tests live in **`Tests/CW.Tests`** (`CLAUDE.md` §22 Note A, closed 2026-09-12). Its 4.9 README (in the 4.90 source; not brought to 5.00) explains why
   it is one project with folders, and why these are Moq unit tests rather than container tests —
   `Nop.Tests.BaseNopTest` builds its container in a static constructor with no extension point, so a
   separate project can never register CW services into it.

### 9.1 What is actually left on exchanges

Nothing that blocks. In the order I would take them:

| Item | Where | Size |
|---|---|---|
| A queued approval email survives an undo — needs `ApprovalQueuedEmailId` on `ReturnRequestItem` | §7.4 | ~1 hour |
| `_SummaryTotals.cshtml:36-39` duplicates `_FeesAndChargesList.cshtml:18` — two transcriptions of one fee loop | §7.3 | Cosmetic |
| The pure `Plan(ReturnResolutionInput)` — now unblocked, since `Tests/CW.Tests` exists and it is designed to be pure | §5 | Medium |
| Phase 4 leftovers: `OrderReturnOrigin`, retiring `PlaceReturnOrderAsync`, frozen prices | §5 | None needed for 4.1 parity |
| Phase 6: collapse the three rule tables into `ReturnPolicy`, plus B7 | §5 | Needs a three-into-one data migration |

### 9.2 What someone else owes

- **Q15** (delivery + redelivery double charge) and **Q16** (billing address choice) — manager,
  `CWPlans/Deprecated/Exchanges_Questions_For_Manager.md`.
- **Deployment topology.** If the store runs multi-instance, every lock in this plugin — subsidy included —
  is decorative unless `DistributedCacheConfig.Enabled` is on (§6.0). Tested, not assumed.

### 9.3 Coverage, honestly

Scenarios P1-P6, P8, P9, P11 pass; P7 is unreachable (every order item on this store carries a packname)
and P10 is not applicable by design. Two gaps named in phase 4b-3's entry: a **note-funded no-fee
exchange** was never exercised (the subsumption claim is sound by inspection, since credit notes do not
depend on the packname cart), and a **non-packname original** is not reachable on this data. Phase 3b's
concurrency behaviour was proven live; the one race left without a database backstop — a second credit
note from a genuinely simultaneous double approval of the same return — is named in its entry.

**Automated coverage is thin and honest about it.** `Tests/CW.Tests` currently pins the fee arithmetic and
the picker's uncollectable-attribute rule — 34 cases, all green at 2026-09-12. *(On 5.00, 2026-10-31: 631 CW.Core
tests pass in `Tests/CW.Tests/Core`, and the port's regression run re-ran the returns and exchanges test plans end to
end.)* Everything else is still
verified by a person driving the live store against a written plan, which is where every defect phase 3b
found was actually caught.

### 9.4 The natural next body of work

`CWPlans/Deprecated/Subsidy_PackName_Gap_Analysis_And_Known_Issues.md` (now `CWPlans/Deprecated/`; its open items are carried by
`Subsidy_PackName_Open_Items.md`), which had its own sequenced plan. Its **P-01** — nop's standard checkout reachable
for a packname customer, placing an order with no packname, no destinations and no subsidy — was the blocker named
here on 2026-09-12; that document records it **closed by phase 2a, 2026-09-17**.
