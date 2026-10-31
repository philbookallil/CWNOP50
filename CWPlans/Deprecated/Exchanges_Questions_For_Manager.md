# Exchanges rebuild — questions for the manager

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

Written to be readable without knowing the system. Each question says what happens today, what we need
decided, and what changes depending on the answer.

**Status, 2026-09-11: questions 1–14 are answered. Two new ones, 15 and 16, are open** — both surfaced
while testing the finished subsidy rework against a live store, and neither could have been seen before
then because no test order had ever been charged real freight. Question 7 was reopened and decided the
other way; question 12 got its data; six new questions (14) were answered on 2026-09-10. The decisions so
far are all recorded in `Exchanges_Rebuild_Record.md` §3, as decisions 15–27.

**The manager's answer of 2026-08-24 governs all three of the late questions**, and it is one sentence:
*build exactly what the 4.1 system does and nothing more.* His reasoning is recorded in full in the boxes
on 9 and 10, but the principle behind it is worth stating once at the top, because it should be the tie-
breaker on anything else that comes up:

> **The flexibility is the feature.** The customer is on the phone with a Sales Support person and they
> negotiate. The manager watched that happen when the workflow was designed and deliberately kept the rules
> thin, because Admin told him a stricter system would not work for them. Ten years on they are, in his
> words, *super happy* with it. So when a rule and an editable box can each solve an edge case, **4.1
> chooses the box** — and we should too. 99% of returns fit the defaults; the remaining 1% is a phone call
> and a typed-over number, not a feature.

The practical form of that answer: **the two editable fee quantities and the ad-hoc fee are the escape
valve for every edge case in questions 9–11.** They are already in 4.9 and they already work — see the
verification note in question 9's box.

Questions 1–5 were originally written as blocking and were answered in conversation; the answers were
recorded in `CWPlans/Exchanges_Rebuild_Record.md` §3 and §5 under different numbers and never copied back here, so they
read as open for several weeks. Each now carries an answer box quoting the roadmap decision it maps to.

| Here | Roadmap decision | Status |
|---|---|---|
| 1 Rejected items | 6 | answered |
| 2 Cheaper replacement, subsidy-funded | 7 | answered |
| 3 Re-shipping fee, one per return | 1 | answered |
| 4 Waiting-to-be-paid exchange | 8 | answered |
| 5 Price change before payment | 9 | answered |
| 6 Store credit paying fees | 10 | answered |
| 7 Store credit vs units | 15 | **REOPENED and ANSWERED 2026-09-10 — subsidy first** |
| 8 Part collected, part delivered | — | answered |
| 9 Repackaging fee banding | 9 (new) | **ANSWERED 2026-08-24** |
| 10 Accept some units, reject others, within one line | — | **ANSWERED 2026-08-24** |
| 11 Hold the cart until the whole return is decided | — | **ANSWERED 2026-08-24 — no, do it as 4.1 does** |
| 12 "Allow store credit" and exchanges | 16, 17 | **Data obtained 2026-09-10** — see the box |
| 14 Six questions from the subsidy value rework | 15, 17, 19–22 | **ANSWERED 2026-09-10** |
| 15 Delivery charged twice on an exchange | — | **OPEN — added 2026-09-11** |
| 16 Who chooses the billing address at checkout | — | **OPEN — added 2026-09-11** |

**Question 6 was added on 2026-08-17** and **answered the same day** — see the box at the top of it.

**Question 7 was added on 2026-08-18 and withdrawn the same day** — the existing 4.1 system already
answers it. See the box on it; there is an optional improvement noted there worth raising later.

---

## 1. Rejected items — what do we charge? — **ANSWERED**

> **Answer: a rejected item attracts no fees at all.** No repackaging fee, and its quantity is excluded
> from the redelivery band, so a rejection can lower the redelivery fee but never raise it. If every item
> on a return is rejected there are no charges, no credit note and no exchange.
>
> Recorded as roadmap decision **6**; built in `Exchanges_Target_Design.md` §3.1 and phase 3a.
>
> **Still not decided, and not blocking:** what physically happens to a rejected garment, whether that
> costs anything, and whether the customer is told which items were rejected and why. Worth asking when
> convenient.

The question as asked:

**Today:** you confirmed returns are approved item by item, because one garment can come back clean and
another dirty or worn. What we do not have written down is what happens to the charges when an item is
rejected.

**Please decide:**

- If an item is rejected, is the customer still charged the **repackaging fee** for it?
- A customer returns three items, two accepted and one rejected. There is **one re-shipping fee per
  return** — is that still charged in full?
- If **every** item in a return is rejected, is anything charged at all?
- What physically happens to a rejected garment — posted back, held at the warehouse, disposed of? Is
  there a cost, and does the customer pay it?
- Should the customer be **told which items were rejected and why**, or just that the return was declined?

**Why it matters:** these charges will be calculated automatically. Whatever we pick becomes the default
on every return, so it should match what Sales Support already do by hand.

---

## 2. Cheaper replacement paid for by subsidy — who gets the difference? — **ANSWERED**

> **Answer: back to the subsidy, in kind — option (b).** Units restore units, with the price irrelevant;
> global credit restores to global; product-group credit restores to that exact product group. **One rule
> for every store**, not a per-store setting. It does not become a credit note.
>
> Recorded as roadmap decision **7**; specified in `Exchanges_Target_Design.md` §4.4 and §5. Built in
> phase 5, which has not started.

The question as asked:

**Today:** when someone exchanges an item for something cheaper, the difference is in their favour. If
they originally paid with their own money or a credit note, they get a credit note back — that part is
clear and works.

**The unclear case is when the original item was paid for by the employee's subsidy** (their uniform
entitlement, i.e. company money rather than the employee's).

**Please choose where the difference should go:**

- **(a)** A credit note to the **employee**, which they can spend on anything.
- **(b)** Back into the **employee's subsidy** entitlement, so it can only be spent on uniform.
- **(c)** To the **Branch Manager or Head Office**, on the basis that it was company money.

Also: should this be a **setting each store can choose**, or one rule for everyone?

**Why it matters:** this decides whether company money ends up as an employee benefit. The 2018
specification mentions option (c) as a store setting, but it was never actually built — so whatever you
choose here is a decision, not a restoration of old behaviour.

---

## 3. Re-shipping fee — what are Sales Support doing today? — **ANSWERED**

> **Answer: yes, staff zero the quantity by hand today — and the fee is one per return.** The manager
> then made the arithmetic exact: *"$5 per units and in this case there are 4 units. This means that up to
> 4 items will be redelivered for a fee of $5. If there were 5-8 items it would be $10 and so on."*
>
> Recorded as roadmap decision **1**. Built in **phase 3a** (2026-08-21), which prices the band over the
> whole return automatically.
>
> ⚠️ **Two things to tell Sales Support and Finance.** Staff must **stop zeroing `StaffShipQty`** — the
> field still adjusts the quantity, but zeroing lines now *removes* their units from the band and
> undercharges the return. And the manual workaround was never exactly right: it undercharges as soon as a
> return crosses a band (six items with all but one zeroed billed $5 where the rule says $10), while
> doing nothing at all overcharged. Billing on multi-item returns will move in both directions.

The question as asked:

**Today:** the system works out a re-shipping fee for **each returned item**. Our reading of the code is
that staff avoid double-charging by manually setting the shipping quantity to **zero on all but one item**
when several come back in the same parcel.

**Please confirm:** is that what actually happens on every multi-item return?

**Why it matters:**

- **If yes** — we automate it, and customers see no change. Good.
- **If no**, and staff have not been zeroing those quantities, then customers have been charged a shipping
  fee **per item**. Moving to one fee per return would then **reduce what we bill**, which is the right
  outcome but is a real change to revenue that Finance should probably hear about first.

We are not assuming either way — we just need to know which it is before we make it automatic.

---

## 4. What can a customer do while an exchange is waiting to be paid? — **ANSWERED**

> **Answer:** they may keep shopping and pay for everything in one order (mixed carts allowed). They
> **cannot cancel** an approved exchange themselves — they ring in and an administrator adjusts it. A
> credit note already issued stays with them regardless. There is **no expiry**, and the returned garment
> goes back into stock as usual.
>
> Recorded as roadmap decision **8**; specified in `Exchanges_Target_Design.md` §7. The server-side
> constraint that stops a customer editing or removing an approved exchange from the cart shipped in
> phase 2; the rest is phase 4.

The question as asked:

**Today:** when an exchange is approved and the customer owes money, the replacement item sits in their
shopping cart until they check out. What is not defined is what else they may do in the meantime.

**Please decide:**

- Can they **add ordinary shopping to that same cart** and pay for everything in one go, or should the
  exchange be paid on its own?
- Can they **change their mind and cancel** the exchange? If a credit note was already issued for the
  returned item, does the customer keep it?
- If they **never pay**, should the exchange expire after a period — and if so, how long, and what happens
  to the returned garment?

**Why it matters:** none of this is currently defined, so the behaviour today is whatever happens to fall
out. We would rather choose it deliberately.

---

## 5. If the price changes between approval and payment — **ANSWERED**

> **Answer: the customer pays the amount quoted at approval, in both directions.** Moot in practice —
> prices do not drop on this B2B store.
>
> Recorded as roadmap decision **9**. ⚠️ Note this **overrides the default proposed below**: the draft
> said we would honour the lower of the two prices unless told otherwise, and we were told otherwise.
> Built in phase 4 (frozen prices, target design §7.1), not started.

The question as asked:

**Today:** the customer is charged whatever the replacement costs at the moment they pay, which may differ
from the amount quoted in their approval email.

**We plan to change this** so the quoted amount is what they pay — no surprises.

**Please decide the one awkward case:** if the item's price **drops** between approval and payment, should
the customer get the lower price, or the amount we quoted them?

**Why it matters:** locking the price protects customers from increases, but strictly applied it also
denies them a decrease. Most retailers honour the lower of the two; we would default to that unless you
would rather we did not.

---

## 6. Can store credit pay the fees on an exchange? *(added 2026-08-17 — **ANSWERED**)*

> **Answered 2026-08-17:** *"We should keep the existing logic… Do not use store credit or subsidy… I
> remember now that the return is at the expense of the user if it is their choice."*
>
> **Decision: fees are cash-only.** Neither store credit nor subsidy is ever spent on a repackaging,
> redelivery or additional fee. The current code is correct and the written description was wrong;
> `CWDocs/Exchanges_Returns_Credit_Notes_Functional_Guide.md` §2 and example 1 have been corrected.
>
> **Unchanged by this, and not to be confused with it:** when a customer returns something *for credit*,
> the fee is still deducted from the credit note **before it is issued** (`ExchangesLogic.cs:185`,
> `creditNoteAmount -= exchangeFeesAmount`). That reduces what we give them — the manager's "at the
> expense of the user" — and is not the same thing as spending credit they already hold.

The question as asked:

**Today:** when a customer exchanges an item, they may be charged a repackaging fee and a redelivery fee.
Separately, they may be holding store credit from an earlier return.

Right now the system treats those two as completely separate. The fees are added **after** any store
credit and any subsidy have been applied, so store credit never pays a fee — the customer pays it, in
cash, on the card, even when the business still owes them credit.

**But our own written description of how the system should behave says the opposite.** It gives this
example: *"They owe $8 in fees. If they hold store credit, it covers it; otherwise they pay it."*

One of the two is wrong, and we need to know which before we rebuild this part.

**Please decide:**

- A customer owes **$8 in fees** on an exchange and holds **$50 of store credit**. Do they pay the $8, or
  does the store credit cover it and leave them $42?
- Same question if it is their **subsidy** rather than store credit — should company uniform money ever
  pay a returns fee, or only the customer's own money?

**Why it matters:**

- **If store credit covers fees** — the customer pays nothing on a typical exchange, and the fee quietly
  reduces what we owe them. Fewer card payments, fewer abandoned exchanges.
- **If the customer pays fees separately** — the fee is always visible and always collected, and store
  credit stays reserved for goods. This is what the code does today, so choosing it means no change.

There is a reason to think the current behaviour is deliberate rather than an oversight: the exchange
workflow explicitly refuses the "no extra payment needed" route whenever a fee exists, **even when the
customer's credit would have covered it**. That looks like someone wanting fees to be visibly paid. We
would rather confirm that than assume it.

---

## 7. Store credit versus *unit* entitlement — which is used first? *(added 2026-08-18 — withdrawn; **REOPENED and ANSWERED 2026-09-10**)*

> ## **ANSWERED — manager, 2026-09-10: the subsidy first. Option B, for units *and* money.**
>
> Subsidy is spent before store credit, and when a manager orders for employees the **employer's** subsidy
> is spent before the manager's **own** store credit. This is what the 2018 design said (*"Unit → StyleGroup $
> → Subsidy $ → Topup → Credit Note"*, `8 NOPSubsidy.docx`). Recorded as decision **15**; built as phase
> 4b-1 (a swap of the two settlement priorities).
>
> The box below was the reason the question was withdrawn. It is right about the 4.1 code at the money
> level — and wrong in what it implies for units: 4.1 consumed product-group units regardless of store
> credit (`SubsidyService.cs:905-1087`), so the "unit left untouched" case never actually happened there.
>
> **Not asked** *(the original box, kept for the record)*. Before sending this we checked what the current 4.1 system does, and it already answers
> it: **store credit is used first, ahead of units.** When store credit covers the order, the subsidy step
> is never reached at all, so the unit is left untouched
> (`OrderProcessingServiceOverride.cs` L414: `if (hasSubsidyBalance && remainOrderAmount > 0)`).
>
> That matches the answer to question 6 and the rule as stated, so the 4.9 rebuild keeps it. No decision
> needed.
>
> **Worth raising separately, as an improvement rather than a question:** the case below is real, and the
> current behaviour can leave an employee having spent their own store credit while a unit of their
> uniform entitlement lapses unused at the end of the period. Nobody has ever chosen that — it is a
> consequence of the ordering, not a decision. If the business would rather the entitlement were used
> first, that is a small change. Ask when there is a quieter moment; it does not block the rebuild.

The question as drafted:

**This is a follow-up to your answer that store credit is used before subsidy.** That answer is being
applied, and it is right for the *money* part of a subsidy. Building it surfaced a case nobody had
separated out before, so we would rather ask than assume.

**The background.** A subsidy has two different kinds of allocation:

- **Money** — a dollar amount the employee can spend on uniform.
- **Units** — "this employee gets 3 shirts", tied to a specific group of products. A unit is not money.
  It cannot be spent on anything else, and it does not carry over: unused units lapse when the subsidy
  period ends.

Your answer clearly covers the money. What it does not settle is what happens to **units**.

**The case.** An employee has one shirt unit left, and $50 of store credit from an earlier return. They
order a $45 shirt that their unit covers.

- **Option A — store credit first, exactly as stated.** The $45 comes off their store credit, leaving
  them $5. The unit is untouched, and stays available until the period ends. If they do not order another
  shirt before then, it lapses and nobody gets the value.
- **Option B — the unit first.** The unit covers the shirt. Their $50 store credit is untouched and stays
  spendable on anything, with no expiry.

Either way the employee pays nothing today. The difference is what they are left holding, and who ends up
paying for the shirt.

**Please decide:** when an employee has **both** an unused unit that covers the item **and** store credit,
which should be used?

**Why it matters:**

- **Option A** keeps the company's uniform budget unspent. If the period ends before the employee orders
  again, that budget is never used — a saving for the company, and the employee has spent their own credit.
- **Option B** uses the uniform allocation for what it was allocated for, and leaves the employee holding
  store credit they can spend whenever they like.

This only affects unit-based allocations. Wherever a subsidy is a dollar amount, store credit still goes
first exactly as you said.

---

## 8. Can one order be part collected, part delivered? — **ANSWERED 2026-08-21**

**Answer: an order is always either entirely collected or entirely delivered.**

The question was whether *some* packnames on an order could be collected from a store while others were
delivered. They cannot, and that is now the rule.

**What it means for us:** the larger half of the pickup work is not needed. Collection will be offered at
checkout for the whole order, and a basket that mixes the two will be refused with a message explaining
the choice. Recorded in `CWPlans/Exchanges_Rebuild_Record.md` §5, where phase 8b is cancelled.

---

## 9. Should the repackaging fee also cover several items at once? *(added 2026-08-21)*

> ## **ANSWERED — manager, 2026-08-24: keep it exactly as it is.**
>
> > *"I think we should keep this as it currently is as it is working well for 10 years. With that edge
> > case the admin staff member has the ability to manually change the fees after conversation with the
> > customer. This gives us the flexibility and all cases. 99% of cases fit what we have."*
>
> **Decision: repackaging stays PER GARMENT.** This confirms the working assumption below, so **nothing
> changes in the code.** The asymmetry with redelivery is deliberate and stays: one parcel goes back out,
> so one redelivery band; each garment is repacked on its own, so one repackaging fee each.
>
> **The edge case is answered by the box, not by a rule.** If a particular return should not be charged
> repackaging three times, the Sales Support person types a different number into *Repackaged Quantity*
> after speaking to the customer. That is what they have done for ten years and it is what the manager is
> protecting.
>
> ### Verified in code, 2026-08-24 — the escape valve survived the redesign intact
>
> The manager's answer only holds if those boxes still work, so this was checked line by line against
> 4.1 rather than assumed:
>
> | | 4.1 | 4.9 today |
> |---|---|---|
> | Editable *Re-Shipped Quantity* / *Repackaged Quantity* | `ReturnRequestItem.StaffShipQty` / `StaffRepackQty` | **same two fields, same names** |
> | Free-text ad-hoc fee | `AdditionalFeeName` + `AdditionalFeeAmount` | **same two fields** |
> | Which quantity a fee rule prices over | `ExchangesHelper.DetermineFeeRuleQuantity` — the staff value when set, else the returned quantity | **identical method, identical logic** |
> | When the boxes lock | `IsEditable` = status is not *Return Authorized*; and the validator refuses any transition out of *Return Authorized* | **identical rule, identical one-way door** |
>
> **And the case-level redelivery band reads them.** `CalculateExchangeFeeAmountsForCaseAsync` bands over
> `shipping.Sum(entry => DetermineFeeRuleQuantity(shippingRule, entry.Request, entry.Item))` — it *sums the
> staff-entered quantities* across the return. So whatever Sales Support type is still exactly what is
> charged, and the old hand-practice of zeroing the quantity on the other lines still works precisely as it
> always did. It is simply no longer necessary, which is what the manager asked for in question 3.
>
> **Conclusion: no work.** Question 9 required a code change only if the answer had been *"band it like
> redelivery"*. It was not.


Thank you for the answer on the redelivery fee — "$5 covers up to 4 items going back out, 5 to 8 items is
$10". We are building that now, so the system will work it out automatically and Sales Support will no
longer need to adjust the quantities by hand to avoid the fee being charged several times over.

**One follow-up:** should the repackaging fee work the same way — one charge covering a number of items —
or is it genuinely per garment?

**Why we are asking, and why it is not urgent:** with your current setting of $3 per 1 unit, the two are
identical: three garments cost $9 either way. It would only start to matter if you ever set repackaging to
something like $3 per 2 units.

> **Working assumption — developer, 2026-08-21, pending the manager: repackaging stays PER GARMENT.**
> This is exact 4.1 parity — 4.1 priced *both* fees per returned item and had no return-level concept at
> all. It is also what the code does today, so the assumption costs nothing to hold.
>
> **Clarification, because the settings screen makes the two fees look identical — and it is right, they
> are.** Both fee rules have the same three fields: an amount, a rule type (*Fixed price* or *Per units*)
> and a units quantity. Nothing in the configuration differs. What differs is **the quantity the rule is
> applied to**:
>
> | | Priced over |
> |---|---|
> | Redelivery | the **whole return** — every garment going back out, counted once |
> | Repackaging | **each line on its own**, then added up |
>
> At *per 1 unit* the two are arithmetically identical, which is why the difference is invisible today.
> At *per 2 units*, three garments on three lines cost **$6** the redelivery way and **$9** the
> repackaging way.
>
> The reason for the difference is physical rather than technical: one parcel goes back out, so one
> redelivery fee; each garment is repacked on its own, so one repackaging fee each. **This asymmetry is
> new** — it was introduced when the redelivery banding was built, from the manager's wording, which was
> specifically about redelivery. He has not been asked about repackaging.

---

## 10. If three shirts come back and one is dirty, can you accept two and reject one? *(added 2026-08-21)*

> ## **ANSWERED — manager, 2026-08-24: NO. Whole line only, exactly as it is now.**
>
> > *"Can we just do it exactly as it is now... We can go back to it later.... By building exactly what we
> > have now for returns is what we need to move on and do nothing more."*
>
> **Decision: no accepted-quantity per line.** This confirms the working assumption below, so **nothing
> changes in the code** — the returned quantity is already rendered read-only with a hidden field and the
> controller deliberately does not write it back, with a comment saying why. `ReturnCaseLine.Quantity`
> cannot drift from the nop row.
>
> **Explicitly deferred, not rejected** — *"we can go back to it later"*. If it is ever revisited, the shape
> is already written down in the *"Yes"* branch below: an accepted quantity on the line, with fees, credit
> note and replacement all pricing from it instead of from the returned quantity.
>
> **How the dirty shirt is handled in the meantime.** Accept the line, then adjust the money by hand — the
> two fee quantity boxes and the ad-hoc fee are exactly the lever the manager is pointing at. It is a phone
> call and a typed number rather than a feature, which is the trade he has chosen deliberately for ten
> years.


> **Working assumption — developer, 2026-08-21, pending the manager: NO, whole line only.**
> Matching what 4.1 does, per the standing rule. Consequence being applied: the returned quantity goes back
> to **read-only** on the approval screen, exactly as 4.1 renders it. 4.9 had made it editable, which was an
> undocumented divergence and a rough form of partial approval that also left `ReturnCaseLine.Quantity`
> out of step with the nop row.
>
> **If the answer comes back "yes":** the line needs an accepted quantity of its own, and the fees, the
> credit note and the replacement all price from it instead of from the returned quantity. That is real
> work, not a setting — hence asking before building it twice.

**Why we are asking.** You told us returns are approved item by item, because one garment can come back
clean and another dirty or worn. We built that — each *line* of a return is accepted or rejected on its own.

But a line can be **three of the same shirt**, and today the decision covers all three together. Your own
reason for wanting item-by-item approval seems to apply just as much inside a line as between lines.

**Please decide:** when a customer returns three identical shirts and one is unusable, do Sales Support
need to accept two and reject one?

**What changes depending on the answer:**

- **No, whole line only** — nothing to do. This is what the old system did: the quantity on a return could
  not be changed at all, so a return was accepted or rejected in full.
- **Yes** — we record an accepted quantity per line, and the fees, the credit note and the replacement are
  all calculated from it. It is a real piece of work, not a setting, so we would rather know now than
  build it twice.

**Worth knowing:** the new system currently lets staff edit the quantity on the approval screen, which is a
rough way of doing this — reduce three to two and only two are exchanged. The old system did not allow
that. If the answer is "no", we should take that box away, because at the moment it can be changed without
anything recording why.

---

## 11. Should a customer wait for the whole return to be decided before they can pay? *(added 2026-08-21)*

> ## **ANSWERED — 2026-08-24: NO. Do it exactly as 4.1 does.**
>
> The developer put the recommendation below to the manager and the answer came back to **keep 4.1's
> behaviour**: a line is priced on its own and executed on its own, the moment it is authorised. The
> customer can pay for it straight away without waiting for the rest of the return.
>
> **This decides the fee scope as well, not just the timing** — the two cannot be separated. Per-line
> execution and a redelivery band taken across the return are incompatible: the band is only final once
> every line is decided, and executing at approval bills a line before that. So the case-level band goes
> too, and redelivery returns to 4.1's scope: **one band per return request, over its own
> *Re-Shipped Quantity*, with Sales Support zeroing the box on the lines that should not carry it.**
>
> **Manager decision 3 still holds** — *"$5 covers up to 4 items"* is exactly `ceil(qty / units) × amount`,
> applied where 4.1 applies it: four units of one product on one return request is one $5 band.
>
> **Consequences accepted by this decision**, stated so nobody rediscovers them as bugs:
>
> 1. A customer can pay for one line before the rest of the return is decided.
> 2. A multi-product return charges redelivery once per return request unless the boxes are zeroed. **This
>    manual step is the feature, not a defect — do not automate it.**
> 3. §18.9 is closed by decision rather than by code. Each line now carries its own complete fee, so
>    nothing is left unbilled; the risk moves from under-collection to over-collection unless someone types
>    a zero, which is the trade the business has run on for ten years.
>
> **Implementation:** `CWPlans/Exchanges_Rebuild_Record.md` §4. It reverses the
> hold-execution rework (files 1–8 of the rework spec) and the case-level band, and keeps the freeze,
> the return case aggregate, the §18.8 re-open and the cart quantity floor.
>
> ---
>
> *The recommendation that was put to him, kept for the record:*
>
> The manager's answer of 2026-08-24 was about **fees and flexibility**. It did not mention *when the
> customer can pay*, which is what this question actually asks. It should not be read as an answer to it.
>
> **The recommendation is to keep the hold, and the reason is the manager's own priority.** Holding the
> cart back does not remove the Sales Support person's flexibility — it **protects** it:
>
> - Nothing is billed until the last line is decided, so **every line stays adjustable right up to that
>   moment.** The phone call can still change the answer.
> - Releasing early is what *closes* the negotiation window. Once the customer has paid, the fee cannot be
>   renegotiated — and that is the one situation where the editable box is no help.
> - It is also strictly more forgiving than 4.1 on a multi-line return. In 4.1 the first line was charged
>   its own full redelivery fee the moment it was authorised; in 4.9 the band keeps recalculating as the
>   remaining lines are decided.
>
> **What is still worth putting to him, in one sentence:** *"a customer whose return is looked at over
> several days will not be able to pay, or get their credit note, until the last item is decided — today
> they could pay for the first item straight away."* That is the only customer-visible consequence, and it
> is the only part of this question he has not spoken to.
>
> **If he says no:** revert the hold-execution rework — see `CWPlans/Exchanges_Rebuild_Record.md` §4, files
> 1–8 — and accept that a return decided over more than one sitting can be billed the wrong redelivery fee,
> in either direction. The cart quantity
> floor) are independent and stay either way.


> **Working assumption — developer, 2026-08-21, pending the manager: YES, wait.**
> It is the only point at which the fee totals are known, so it is the only point at which the customer can
> be billed correctly. Being built as phase 3b's *plan then execute*.
>
> **If the answer comes back "no":** we keep the current early-checkout behaviour and accept that a return
> decided over more than one sitting can be billed the wrong redelivery fee — see
> `CWPlans/Exchanges_Rebuild_Record.md` §4 for exactly how, and in both directions.

**Today:** each item of a return is approved on its own, and as soon as one is approved the replacement
goes into the customer's cart and they can pay for it — even if the other items on the same return have not
been looked at yet.

**The problem that causes.** The redelivery fee is *one fee for the whole parcel*, as you described. We
cannot know the final figure until every item has been decided. So if a customer pays early, they can be
charged the wrong amount — sometimes too much, sometimes nothing at all.

**What we would like to change:** keep approving and rejecting item by item exactly as now, but hold the
replacement out of the customer's cart until the **whole return** has been decided. Then the amount is
known and it is charged once, correctly.

**Please confirm you are happy with the trade-off:** a customer whose return is looked at over several days
would not be able to pay — or receive their credit note — until the last item has been decided. Today they
could pay for the first item immediately.

Our reading is that this is the right way round, because the alternative is billing the wrong figure. But it
is a change to what customers experience, so we would rather you agreed it.

---

## 12. "Allow store credit" and exchanges — *(added 2026-08-26; **data obtained and superseded 2026-09-10**)*

> ## **Answered by the live rules, 2026-09-10.**
>
> All three rule tables were queried on the 4.1 database:
> the **Exchange** action allows store credit in **84 of 85** stores (store 13 is the exception); every
> exchange-enabled reason allows it in all but one store (76); the *Return Authorized* status rule is
> `Authorised = PerformStoreCredit = PerformExchange = 1` in all 85. So the "on" branch below is what every
> exchange store runs: **a full-price credit note on every exchange, subsidised or not, and on an
> exchange with fees the subsidy refunded on top — the value comes back twice.** On a no-fee exchange the
> note silently pays the auto-placed replacement, which is why nobody ever saw one.
>
> **Superseded by decisions 14, 16 and 17:** the rebuild gives the value back once, in kind, and raises the
> money note on every exchange regardless of the tick, so the trap described below no longer exists on an
> exchange. The tick still governs Credit-Note mode and returns for credit.

Found while testing the returns work. Nothing is broken, nothing is changing, and no decision is being
asked for — but it would be hard to diagnose later, so it is written down here and in the functional
guide (§6.3).

**The setting.** Every Return Reason and every Return Action has an *Allow Store Credit* tick box. It
quietly does two jobs: it decides whether the customer may choose "credit note" as the outcome of their
return, and it decides whether the customer gets the value of the returned garment back at all.

**The case.** If a reason and action pair allows Exchange but does **not** allow Store Credit, and a
customer exchanges an item for the same item in a different size:

- They send the garment back.
- Nothing is credited to them for it.
- They are charged the full price of the replacement again, on top of the return fees.

On a $20 cap with $5 redelivery and $3 repackaging, the customer pays **$28** for the exchange — $48 in
total for one $20 cap.

**With the tick on**, authorising the return raises a credit note for the returned garment's full value,
that credit note pays for the replacement, and the customer pays only the $8 of fees.

⚠️ **We do not yet know which way your reason/action pairs are actually set, and it changes the answer.**
Before this note goes any further we need to look at the live rules screen. If exchange pairs have store
credit **off**, the $28 above is simply what happens until the rebuild finishes giving the entitlement
back in a spendable form — nothing to raise with you at all. If they have it **on**, the returned value
comes back twice on a subsidised exchange, which is a different and more serious conversation.

**This is exactly what the current 4.1 system does today** — the two methods that decide it are
byte-for-byte identical there. It is not something the rebuild introduces, and per the *build exactly
what 4.1 does* rule we are leaving it alone.

**What is worth knowing** is that this is a configuration trap rather than a fault. If anyone sets up a
new return reason or action and ticks Allow Exchange without ticking Allow Store Credit, every exchange
against that pair will overcharge the customer, and nothing on the screen warns them.

**One small ask:** could someone confirm that no reason or action currently in use has Allow Exchange
ticked without Allow Store Credit? If they are all set correctly, this stays theoretical.

If you would rather we did something about it, the cheapest option is a warning on the rules screen when
that combination is saved.

---

## 14. Six questions from the subsidy value rework — **ANSWERED 2026-09-10**

Asked and answered in one pass. The decisions they produced are 15–22 in `Exchanges_Rebuild_Record.md` §3.

| # | Question | Answer | Decision |
|---|---|---|---|
| 1 | Exchange pair with "Allow store credit" unticked, customer paid cash — raise the money note anyway? | **Yes** — a customer never pays twice for a garment | 17 |
| 2 | Subsidy first, then credit notes — for units *and* money, and for a manager ordering for employees? | **Yes to all three** | 15 |
| 3 | Return for credit of a fully subsidised item with fees — no note to deduct from | **Sales Support zero the fee boxes; the approval screen warns** | 19 |
| 4 | Does "invoiced" still matter (`4a` wanted a credit note after invoicing)? | **No — back to the pot always** | 20 |
| 5 | Shipping paid by subsidy on a returned order — restore it? | **No** | 21 |
| 6 | A return of an order from a previous subsidy period restores into the lapsed period | **Correct as is, documented** | 22 |

---

## 13. An order delivered to several branches — what should the invoice and the confirmation email say? — **ANSWERED 2026-08-26**

> **Answer: option B — list every delivery address, and under each one the packnames that share it**,
> the same way the screens already present it. Recorded as roadmap decision 13.
>
> ✅ **Built and tested 2026-08-27**, on both documents. Record:
> `CWPlans/Exchanges_Rebuild_Record.md` §4, items 6 and 7.

**The question as asked.** This one has no precedent to fall back on: the previous system never
delivered a single order to more than one branch, so there is no existing behaviour to copy. Whatever you
choose is what we build.

**The situation.** A site manager can place one order covering several packnames that ship to different
branches. Everywhere that matters operationally we now name the right branch — the packing slip on each
parcel, the customer's order page, and the admin screens. **Two documents showed only the first
branch**, because they were built when an order could only go to one place:

- the **invoice PDF**;
- the **order confirmation email**.

Both printed one address, and on a multi-branch order it was simply the wrong one. **Both now list every branch**, each with the packnames going there.

**Please choose what those two should say:**

| | Option | What it looks like |
|---|---|---|
| **A** | Leave it | Keeps showing the first branch's address. Cheapest, but the document says something untrue |
| **B** | **List every delivery address** | *"Ship to: Branch A, 12 High St … / Branch B, 40 Mill Rd …"* — the same information the customer already sees on their order page |
| **C** | Point at the breakdown | *"Delivered to 3 addresses — see the packname breakdown"* |
| **D** | Leave the block off | No ship-to at all on a multi-branch order |

**Our recommendation is B**, for three reasons: it matches what the customer's own order page already
shows, so there is no new idea to explain; on a commercial document a wrong address is worse than a long
one; and addresses are grouped, so in practice the list is two or three lines, not twenty.

**Whichever you pick, an order going to a single branch — the overwhelming majority — looks exactly as it
does today.** Only multi-branch orders change.


---

## 15. When you exchange something, should the customer pay delivery *and* the redelivery fee? *(added 2026-09-11 — **OPEN**)*

**Today: they pay both, and nothing in the system decides that — it simply adds them together.**

This never showed up before because every test order shipped free. As soon as we tested against an
address that quotes real freight, it appeared immediately. A customer exchanging a **$24 shirt** was
asked for:

| | |
|---|---|
| The replacement shirt | $24.00 |
| **Delivery** | **$20.00** |
| Redelivery fee | $5.00 |
| Repackaging fee | $3.00 |
| **To pay** | **$28.00** — the shirt itself is covered by their entitlement |

So $28 of charges to swap a $24 shirt, $20 of which is freight the customer is being asked for a second
time.

**Please choose:**

- **(a)** The **redelivery fee replaces** the freight. The fee exists precisely because the parcel has to
  go out again, so charging freight on top is charging twice for one thing.
- **(b)** The redelivery fee is an **administration** charge — someone's time, the paperwork, the
  packaging — and freight stands as well, because the courier really does have to be paid again.
- **(c)** **Per store.** Some of your stores absorb freight and some pass it on, so let each one decide.

**Why it matters:** on a free-delivery store nothing changes whichever you pick. On a store that charges
real freight, (a) and (b) differ by the whole delivery cost on **every single exchange** — and it is the
difference between an exchange feeling fair and feeling like a penalty for having been sent the wrong
size.

**What we would do if you have no strong view:** (c), defaulting to (b). It is the smallest change, it
keeps today's behaviour as the default so nothing moves under anyone, and it lets you set (a) on the
stores where freight is expensive enough to matter.

---

## 16. Should a customer be able to choose which address their order is billed to? *(added 2026-09-11 — **OPEN**)*

**Today: no — and the screen is misleading about it.**

The checkout screen shows a billing address. The customer cannot change it, and the address it shows is
**worked out by the screen, not by the part of the system that actually bills the order**. Until this week
those two could disagree without anyone noticing: we saw a live order where the screen said one address
and the order was billed to another.

That specific mismatch is fixed — the screen now shows exactly what will be billed. But it left a
question we cannot answer for you.

**Please choose:**

- **(a)** **Leave it.** One billing address per person, worked out automatically, not the customer's
  business. Simplest, and it is what happens today.
- **(b)** **Let them pick**, from the addresses already on their account, the same way they pick a
  delivery address. More work: the screen needs a selector and the choice has to be remembered.
- **(c)** **Show it but lock it**, with a line saying who to contact to change it — so at least nobody is
  confused about why it cannot be edited.

**Why it matters:** for uniform ordering the billing address is usually the employer's and the employee
has no reason to touch it, which argues for (a). It matters more if you ever have people ordering across
several companies or cost centres from one login, where the wrong billing address on an invoice becomes
someone's problem at month end.

**What we would do if you have no strong view:** (a). Nobody has asked for it, and (c) is a ten-minute
change we can add later if anyone does.

---

## Not questions for you — noted so you know they are handled

- Whether to build a shared internal structure for credit notes and subsidy — a technical decision.
- Where automated tests live — a technical decision.
- The exchange payment screen — **settled**: we are keeping the existing cart-based flow you confirmed
  works well, and only tightening it so an approved exchange cannot be edited or accidentally deleted.
- Restricting exchanges to the same product group — **settled**: dropped, keeping the current behaviour
  where any replacement is allowed and non-subsidised items are paid for by the customer.
