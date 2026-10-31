# Returns, Exchanges, Credit Notes & Subsidies — Functional Guide

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWDocs/`.

**Who this is for:** anyone who needs to understand what the system does — store administrators, Sales
Support, managers, and anyone reviewing the customer experience. There is nothing technical in here: no
code, no database, no screens-that-do-not-exist-yet. It describes **how the functionality works**.

**A note on timing — read this once.** This guide describes **what is built and running today**, as of
**11 September 2026**, not what is planned. It was checked again on **31 October 2026**, when the system moved to
nopCommerce 5.00: the behaviour below came across unchanged, apart from the points marked *5.00*. Where a behaviour is agreed but not yet built, it is marked
**⏳ not yet built** at the point it appears, so nothing here can be mistaken for something you can use.
§9 lists where the rebuild has got to.

---

## 1. The short version

The store sells uniforms and workwear to businesses. Three things interact:

- **Subsidy** — an entitlement a company gives its staff. It can be *units* ("two shirts a year") or
  *money* ("$300 a year"), and it can be limited to certain kinds of product.
- **Credit notes** — store credit held by a customer. Usually created when something is returned; can also
  be issued by hand.
- **Returns and exchanges** — sending something back, either for store credit or to swap it for something
  else.

Most of the complexity lives in one question: **when someone returns something that was paid for with a
subsidy, what do they get back?** The answer the business has settled on is: *they get back exactly what
they spent* — a unit if they spent a unit, dollars to the same pot if they spent dollars.

---

## 2. Key concepts, in plain language

### Subsidy — the company's entitlement to its staff

A subsidy is what a company allows each of its staff to spend. It has:

- **A period** — for example a calendar year, optionally repeating each year.
- **An allocation**, which comes in two forms that behave very differently:
  - **Units** — "three polo shirts". Counted in garments. The price does not matter.
  - **Money** — "$250". Counted in dollars.
- **Product groups** — an allocation can be limited to a defined set of products. "Two items from the
  Outerwear group" is a unit allocation scoped to a group. "$100 towards Footwear" is a money allocation
  scoped to a group.
- **A global money pot** — money not tied to any group, usable on anything, and the only thing that can
  pay for delivery.
- **Top-ups** — extra allocation added during the period, either by a store administrator or by a
  manager. Top-ups add to what is there; they never replace it.

**The order things get used up** matters, and is fixed: units are always used before money. Within that,
group-specific allocation is used before the global pot. In full:

1. Product-group units
2. Product-group unit top-ups
3. Product-group money
4. Product-group money top-ups
5. Global money
6. Global money top-ups

The reason units go first, across *all* of an employee's subsidies, is to avoid a situation where the
system spends money from one subsidy while unused units are still sitting in another.

### Credit notes — store credit

A credit note is an amount of store credit belonging to one customer. It records what it was issued for
and how much of it is left. Credit notes are used **oldest first**, so an older note is fully spent before
a newer one is touched.

They are created in two ways:

- **Automatically**, when a return is approved and the outcome is store credit.
- **By hand**, by an administrator with the right permission, against a specific order.

A credit note can only be deleted while it is untouched. Once any of it has been spent, it stays on record.

### Returns and exchanges

A **return** is a request to send one or more items back from an order. For each item the customer chooses
what they want:

- **Credit** — send it back, receive store credit.
- **Exchange** — send it back, receive something else. Three flavours:
  - **Exact** — the identical item again (usually a fault or damage in transit).
  - **Same style** — the same product in a different size or colour. *This is the most common case by far.*
  - **Different style** — a different product altogether.

### Fees

Two standard fees, both configured per store:

- **Repackaging fee** — for handling and repackaging a returned garment. Charged **per garment**.
- **Redelivery fee** — for shipping the replacement out again. Charged **per returned item**, over that
  item's own *Re-Shipped Quantity*.

Both can be a flat amount or an amount per so-many-garments. A typical setup is *$3.00 per garment*
repackaging and *$5.00 per 4 garments* redelivery.

> **How one parcel ends up charged one redelivery fee.** Each returned item is its own return request and
> carries its own redelivery fee, so five garments coming back would be charged five times. Sales Support
> set the *Re-Shipped Quantity* to zero on the lines that should not carry it, leaving one line charged for
> the parcel. **This manual step is deliberate and is not being automated** — it is how the business has
> worked for ten years, and it lets staff adjust a fee after speaking to the customer. Confirmed by the
> manager, 2026-08-24.

Administrators can also add a **one-off fee** with a description, for anything unusual.

Whether a fee applies at all depends on the reason for the return. A faulty item normally attracts no fees;
a change of mind normally does.

**Fees are always paid by the customer.** Neither their store credit nor their subsidy is ever spent on a
fee — those pay for goods and delivery only. A return the customer chose is at the customer's expense, so
the fee is a separate amount they settle themselves.

There is one place a fee and store credit do meet, and it is the opposite direction: when a customer
returns something **for credit**, the fee is deducted from the credit note before it is issued (see
example 5). That reduces what we give them; it does not spend credit they already hold.

---

## 3. The customer experience

### 3.1 Placing an order

When a customer checks out, the system works out what they owe and applies what they are entitled to, in a
fixed order:

1. **Subsidy first** — the company's entitlement, following the consumption order in §2: units, then
   product-group money, then the global pot, each base before its top-up.
2. **Credit notes second** — any store credit they hold covers whatever subsidy did not.
3. **The customer pays the remainder**, if anything is left.
4. **Any returns fees are added on top**, and the customer pays those themselves (§2).

This order is the manager's decision of 10 September 2026 (the 2018 design said the same: *"Unit →
StyleGroup $ → Subsidy $ → Topup → Credit Note"*). It applies to units and money alike, and when a
manager orders for employees the **employer's** subsidy is spent before the manager's **own** store
credit. The reasoning: the entitlement is there to be used for uniform, and it lapses at the end of the
period, whereas store credit is the customer's and keeps.

> **✅ Live since 11 September 2026.** The previous system spent store credit first at the money level;
> that is now reversed, so a customer holding both sees their entitlement used before their store credit.

**How the order reads.** The checkout, the order page, the confirmation e-mail and the invoice all list the money the
same way, in the order it was applied: *Total before subsidy and credit* (goods and shipping at full price), then
*Subsidy applied* — split into *Subsidy (items)* and *Subsidy (shipping)* where shipping was subsidised — then *Store
credit*, then the customer's own discounts and fees, and last the *Payable balance*, which is what the customer pays.
The invoice and the packing slip also say, under each item, who it is for — *For: name · department · cost centre*.

### 3.2 Requesting a return

From their order history, the customer picks "Return Item(s)" on an eligible order and works through a
short sequence:

1. **Choose items and quantities.** Only items that have actually been shipped can be returned, and only
   the quantity not already returned. The return screens are for the signed-in customer who placed the order. *(5.00:
   a guest following a withdrawal link from an e-mail uses the store's standard return form instead.)* Each line shows a picture of the item as it was bought, in the
   colour ordered.
2. **Choose a reason.** The reason then determines which **actions** are offered — the two are linked, and
   the pairing is configured per store. Only the reasons the store has switched on are offered; a store with none
   shows the customer a message instead of the form.
3. **Choose what they want for each item** — credit or exchange, and for an exchange, which flavour and
   which replacement product. Picking *Same Style* opens the same garment with its size and colour
   already set to what they bought, so they change only what they mean to. *Different Style* opens a
   searchable, paged list of the store's range. The picture updates as they change colour, so the item
   being returned and its replacement can be compared side by side.

   A small number of products need details the return screens cannot take — a line of text to be printed,
   a date, a file to upload, a rental period, or gift-card recipient details. Those products are still
   shown in the list, because hiding them would look as though they were out of stock. If the customer
   tries to choose one they are told which details cannot be taken online and asked to pick something else
   or contact Sales Support, who can arrange the exchange for them.
4. **Review a summary**, including the fees that will apply, then confirm.

A progress indicator runs across all four steps. Fees that may apply are shown from step 3 onward, before
the customer commits to anything, and the *Next Step* button explains what is still missing rather than
simply being unavailable. If something is wrong when they submit, they stay on the same step with their
choices intact.

Once confirmed, a printable confirmation opens listing each return, and the store can require it to be
printed before the window is closed. The returns then appear in their account with their status; an exchange also
shows its type and replacement. While a return is still pending, the customer can change the replacement they picked
without starting over.

Everything the customer chooses is checked again by the system when they submit — the quantities, the reason and
action, credit or exchange, and that the replacement fits the exchange type, has every required option chosen and is
one their entitlement lets them order. A choice that does not pass is refused with a message.

### 3.3 What happens next

Sales Support reviews the return (§4). The customer then hears one of:

- **Approved, nothing further needed.** The exchange was fully covered — usually by the entitlement that
  came back with the returned garment, sometimes by store credit — and there were no fees. A replacement
  order has already been placed.
- **Approved, payment needed.** There is a balance to pay. The replacement is waiting in their cart.
- **Store credit issued.** They chose credit, and it is now on their account.
- **Declined**, for some or all items.

### 3.4 Completing an exchange that needs payment

The replacement item is placed in the customer's cart. When the cart contains only exchange items,
checkout is simplified — no delivery options to choose, and the exchange items themselves cannot be
edited or removed.

Two things worth knowing:

- **They can carry on shopping.** Ordinary items can be added and paid for in the same transaction. The
  value of the returned garment only ever counts towards the exchange, never towards unrelated shopping.
- **They cannot cancel the exchange themselves.** If they change their mind they ring in, and an
  administrator adjusts it.

There is no time limit on completing an exchange, and the price they were quoted at approval is the price
they pay, even if the item's price changes in the meantime.

### 3.5 Their store credit page

Under *My account › My Store Credit* customers see their available balance, each credit note (what it was issued
for, its amount and what is left of it) and every order their credit was applied to, with links to those orders.
Their subsidy position has its own page, *My account › Subsidies*: one card per subsidy of the packname they are
ordering for — whether it is open, remaining credit or units with a usage bar, the product groups, and a history of
every top-up, order and return behind the figures.

---

## 4. The administrator experience

### 4.1 Reviewing a return

Sales Support opens the return and sees the customer, the order, the items, the reason and action chosen,
the requested outcome, and the replacement product where relevant.

**Each item is judged individually.** One garment can come back clean and be accepted while another comes
back soiled or worn and is rejected. This is why approval is per item rather than for the whole return.

For each item the administrator can:

- **Accept or reject it**, with a note explaining why.
- **Adjust the quantities used to work out fees** — the number to be repackaged and the number to be
  redelivered — within what was actually returned.
- **Add a one-off fee** with a description.

**A rejected item costs the customer nothing.** No repackaging fee, and it does not count towards the
redelivery fee either. If every item is rejected there are no charges at all, no store credit, and no
exchange.

### 4.2 Approving

Approval happens by moving the return to an authorised status. At that point the system does everything
that follows automatically:

- Works out the fees and records them.
- Issues store credit if that is the outcome.
- Gives back any subsidy that was spent on the returned items (§5).
- Either places the replacement order outright, or puts the replacement in the customer's cart and tells
  them what is owed.
- Sends the customer the appropriate notification. *(5.00: the two approval e-mails — "approved, nothing further
  needed" and "approved, payment needed" — are only sent once their message templates have been created in the
  store's message templates. The system does not create them, so until someone does, the customer is not e-mailed
  when a return is authorised. Their "return received" e-mail and printable confirmation are not affected.)*

If anything fails partway, nothing is left half-done — the whole approval is undone.

Approving the same return twice does nothing the second time. There is no way to accidentally issue two
credit notes for one return.

### 4.3 Other things an administrator can do

- **Undo an approval** — resets the return and reverses what the approval did.
- **Resend a notification**, or nudge a customer who has not completed a pending exchange.
- **Complete a checkout on the customer's behalf** (*Impersonate*), when the return belongs to the store they are
  signed into. **Doing this for a return that belongs to a different store does not work** at present: that part of
  the previous system was never rebuilt.
- **Issue a credit note by hand** against an order — this needs its own permission, separate from general
  access.
- **Delete a credit note**, but only while none of it has been spent.
- **Spot orders with exchanges or returns** in the order list and on a customer's Orders tab: **E** marks an order that
  contains a replacement sent for an exchange, **R** an order with an item whose return was approved. An order can
  carry both.

### 4.4 Reporting

- Per store: how many credit notes have been issued, how many are fully used, the total issued value, and
  the **outstanding liability** — money the business still owes customers.
- Per store: which customers are holding a balance.
- Per customer: their credit notes and where each has been spent.

---

## 5. What happens to the money on a return

This is the part worth reading carefully, because it is where the design is most deliberate.

### 5.1 The guiding rule

**You get back exactly what you spent on the garment.** Not the cash equivalent — the same kind of thing,
to the same place. **Delivery is the one exception**, and §5.6 explains why.

| What paid for the returned item | What comes back |
|---|---|
| A subsidy **unit** | **One unit**, to the same product group. The price of the garment is irrelevant. |
| **Product-group money** | That money, to **that same product group's** allocation |
| **Global subsidy money** | That money, to the global pot |
| A **credit note** | A **new** credit note for the item's price. Nothing is put back onto the original note |
| The **customer's own money** | A credit note for that amount |

Money only comes back as a credit note when the return's reason and action **allow store credit** (§6.3).
Subsidy always comes back.

**Only the money the customer put in comes back as a credit note** — the returned garment's price minus
the subsidy that went back in kind. A garment paid entirely by a unit raises no credit note at all; one
paid $30 by subsidy and $15 by the customer raises a $15 note.

> **✅ Live since 11 September 2026.** The rule above is the real behaviour: the entitlement goes back the
> moment the return is authorised, and the replacement draws on it at checkout. Until then the system took
> a shortcut — every exchange raised a credit note for the garment's full price and that note paid for the
> replacement. The money was always right; the route was wrong, and a customer could watch a note appear
> and vanish.
>
> **✅ Live since 11 September 2026.** An exchange with no fees that the returned entitlement covers
> outright is now placed for the customer outright — no cart step, nothing to pay, a replacement order
> already on its way. Anything with a fee, or with a balance to settle, still goes to the cart as before.

### 5.2 Why this matters — an example

An employee has an entitlement of *two shirts a year*. They order a $45 shirt using one unit. It is the
wrong size, so they exchange it for the same shirt in a larger size, which happens to cost $40.

Under the rule above: **one unit comes back, and one unit is spent on the replacement.** Net effect: they
have used one of their two shirts, exactly as intended. The $5 price difference never enters into it,
because they never spent dollars — they spent a shirt.

The alternative — refunding $45 as credit and charging $40 — would leave them $5 of spendable credit and
still consume a unit, quietly turning a uniform entitlement into pocket money. That is the outcome this
rule exists to prevent.

### 5.3 When the replacement costs more or less

**"More" and "less" are measured against what the customer actually paid for the garment they returned** —
not against today's price. If they bought it on a bulk discount at $21 when the shelf price was $24, the
exchange is worked out at $21, and it stays $21 however the price moves afterwards.

- **Costs more** — the difference is settled the ordinary way: any remaining subsidy first, then credit
  notes, then the customer pays what is left.
- **Costs less, and the original was paid with money** (their own, or a credit note) — the difference
  becomes a credit note for them.
- **Costs less, and the original was paid with a unit** — there is no difference. A unit went back and a
  unit came out.
- **Costs less, and the original was paid with subsidy money** — the unspent amount simply stays in the
  pot it was returned to.

### 5.4 If the item is not covered by a subsidy

Then it works like ordinary retail: the customer pays for the replacement, less any store credit they
hold.

There is **no restriction on what a customer may exchange for.** They can swap a shirt for a jacket if
they want. If the replacement is not covered by their entitlement, they simply pay for it.

### 5.5 If an order is cancelled

Everything used on it comes back — subsidy and store credit both — automatically, with no action needed
from anyone.

### 5.6 Three things that are decided, not accidental

- **Delivery paid by subsidy is not given back on a return.** The parcel was shipped. (Manager, 2026-09-10.)
- **Whether the order was invoiced makes no difference.** The 2018 design wanted a credit note instead of
  subsidy once an order had reached accounts; there is no accounting link in the rebuild and the
  entitlement always goes back to the pot. (Manager, 2026-09-10.)
- **A return of an order from an earlier subsidy period gives the entitlement back to that earlier
  period.** A lapsed unit comes back lapsed, so nothing becomes spendable. (Manager, 2026-09-10.)
- **A return for credit of a garment paid entirely by subsidy has nothing to deduct the fees from.** The
  entitlement goes back, no credit note is raised, and no fee is collected. The approval screen says so,
  and Sales Support set the fee quantities to zero if the fees should not stand — the same box they use
  for every other exception. (Manager, 2026-09-10.)

### 5.7 Fees are worked out once

The fees quoted at approval are the fees charged. If someone later changes the fee settings for a store,
returns already approved are unaffected.

---

## 6. Configuration — the options available

### 6.1 Turning it on and off

Exchanges and credit notes can each be switched on or off **per store**. When off, the relevant features
disappear for that store's customers.

One rule is worth knowing: switching a feature off stops *new* activity, but never blocks *finishing*
something already in flight. An existing credit note balance stays visible and spendable, an approved
exchange can still be completed, and money owed still comes back on a cancellation.

### 6.2 Return reasons and actions

**Reasons** are why the customer is sending something back — faulty, wrong size, changed mind, and so on.
**Actions** are what they are asking for. Each store controls which actions are offered for which reason,
so a customer choosing "faulty" sees a different set of options from one choosing "changed mind".

### 6.3 Rules — what each combination allows

For each reason and each action, a store decides:

- Whether **store credit** is allowed
- Whether an **exchange** is allowed
- Whether the **repackaging fee** applies
- Whether the **redelivery fee** applies
- Whether the returned stock goes **back into inventory**
- Whether the reason or action is **disabled** for that store

A capability is only available when **everything agrees** — the reason, the action, and the return's
status all have to allow it. In practice the *Return authorized* status rule must have **Perform store credit** and
**Perform exchange** ticked, otherwise authorising a return gives nothing back and places no exchange. Anything not explicitly configured counts as *not allowed*. This is
deliberate: it fails safe. The practical consequence is that a newly added reason does nothing until it
has been configured, which is worth remembering when troubleshooting "why did nothing happen?".

#### ⚠️ "Allow store credit" also decides whether an exchange gives the value back

This is the one combination worth understanding before setting up a new reason or action, because the
result surprises people.

**Allow store credit does two jobs.** It decides whether the customer may choose *credit note* as the
outcome of their return — and it decides whether the value of the returned garment comes back **at all**,
including on an exchange.

**In the previous system, with it ticked** — and it is ticked on the Exchange action in 84 of 85 stores,
and on every exchange-enabled reason in all but one store (checked against the live rules on
10 September 2026) — authorising an exchange raises a credit note for the returned garment's **full**
price, whether the garment was paid by the customer or by their subsidy. On a garment with no fees the
note silently pays the automatically placed replacement, so nobody ever sees it — a uniform unit has been
turned into store credit and spent. On a garment with fees the note pays the replacement at checkout
**and** the subsidy is refunded on top, as money into the top-up pot — the same value comes back twice.
**With it unticked**, no note is raised and the customer pays the replacement's full price on top of the
fees — **$28** for a $20 cap, having already paid $20 for the original.

**The rebuild does not keep either outcome** (manager, 2026-09-10):

- The value comes back **once**, in the form it was paid: entitlement in kind, money as a credit note (§5.1).
- The money note is raised **whenever an exchange is performed**, whatever the store-credit tick says, so a
  customer can never pay twice for a garment. The tick still decides whether the customer may *choose*
  credit note as the outcome of a return, and whether a return for credit raises one.

So on an exchange the tick no longer changes what the customer gets back. It still matters on a return
for credit.

### 6.4 Fee settings

Each store sets, for each fee type:

- The **amount**
- Whether it is a **flat charge** or **per so-many garments**
- The **number of garments** the amount covers, when it is the second kind

Stores can inherit these from a **global default** rather than defining their own, and the settings screen
shows which values are inherited. Editing an inherited value creates a store-specific override rather than
changing it for everyone.

---

## 7. Worked examples

Using typical settings: repackaging **$3.00 per garment**, redelivery **$5.00 per 4 garments**.

### Example 1 — the everyday case: wrong size

An employee orders a shirt using one unit of their entitlement. It is too small.

They request an exchange, same style, larger size. Sales Support accepts it. Fees for the reason "wrong
size" are configured to apply: $3 repackaging, $5 redelivery — $8 total.

- One unit is returned to their entitlement, and no credit note is raised — they paid with a unit, not
  with money.
- The replacement waits in their cart; when they open it, the unit pays for the replacement.
- Fees are **$8**, and they pay those themselves in either case. Store credit and subsidy are never used
  for fees (§2) — even if this employee is holding $50 of store credit, the $8 is theirs to pay.
- The replacement waits in their cart until they do.

### Example 2 — five garments, one parcel

A site manager returns five jackets from one order, all the wrong size. That is five return requests, one
per jacket.

- Repackaging: 5 × $3 = **$15**
- Redelivery, if nothing is touched: 5 × $5 = **$25**, because each line carries its own fee
- Redelivery, after Sales Support zero the *Re-Shipped Quantity* on four of the five lines: **$5**
- Total as staff would normally leave it: **$20**

**Zeroing those four boxes is the step that makes it one fee per parcel.** It is not automatic, and that
is intended — the same box is how staff waive or adjust a fee after a conversation with the customer.

### Example 3 — one item rejected

Same as example 2, but one of the five jackets comes back visibly worn and is rejected.

- Repackaging: 4 × $3 = **$12** — nothing for the rejected one
- Redelivery: the rejected line raises no fee at all; of the four accepted lines, staff leave one carrying
  **$5** and zero the rest
- Total: **$17**

The rejected jacket costs the customer nothing — no repackaging, no redelivery, and it is not exchanged.

### Example 4 — faulty item

An employee reports a faulty garment. The store has configured "faulty" so that no fees apply.

- No repackaging fee, no redelivery fee.
- The unit returns to their entitlement and is spent again on the replacement.
- Nothing to pay. The replacement order is placed straight away and they are told it is on its way.

### Example 5 — returning for credit instead of a replacement

An employee returns a $60 jacket bought with their own money and asks for store credit rather than a
replacement. Fees apply: $3 repackaging, $5 redelivery — although with no redelivery happening, most
stores would configure this reason not to charge it.

Assuming just the $3: they receive a credit note for **$57**, which they can spend on any future order.

### Example 6 — exchanging for something dearer

An employee returns a $45 shirt bought with one unit, and asks for a $70 jacket instead — a different
product, not covered by any unit allocation they hold.

- One unit returns to their entitlement.
- The jacket is settled the ordinary way: any subsidy money available is applied first (the global pot —
  the returned unit cannot pay for a jacket), then any store credit they hold, then they pay the rest.
- The returned unit stays available for a future shirt.

---

## 8. Limits worth knowing

- **A return can only be requested for items that have shipped.** Something not yet dispatched has to be
  handled as a cancellation instead.
- **Quantities already returned cannot be returned again.**
- **A customer cannot cancel an approved exchange themselves** — they ring in.
- **There is no expiry** on an unpaid exchange. It waits indefinitely.
- **A credit note already issued stays with the customer**, whatever subsequently happens to the exchange.
- **Nothing is sent to the accounting system automatically at present.** Return outcomes and credit notes
  are not yet synchronised to Jim2; that connection existed in the previous system and has not yet been
  rebuilt.

---

## 9. Where the rebuild has got to

**Shipped** means built, tested and running in the rebuild. **Not yet** means agreed and specified but not
built — the previous system's behaviour still applies.

| | Previous system | Rebuild | Status |
|---|---|---|---|
| **Redelivery fee on a multi-item return** | Per item; staff zero the quantity on all but one line | **Unchanged, deliberately.** An earlier draft automated it; reversed 2026-08-24 — the manual box is also how staff adjust a fee after speaking to a customer | **No change** |
| **Subsidy given back on a return or exchange** | A credit note for the full price on every exchange (the live rules allow it everywhere); on an exchange with fees the subsidy is *also* refunded as top-up money — value back twice. On a return for credit, a full-price credit note even for a subsidised garment | Back **in kind**, once: units to units, group money to that group, global to global; only the customer's own money becomes a credit note (§5.1) | ✅ **Shipped 2026-09-11** |
| **Subsidy on a replacement that needs payment** | Charged again at checkout, from the refunded top-up money or another unit | The replacement draws on the restored entitlement first, then credit notes, then the customer pays | ✅ **Shipped 2026-09-11** |
| **Rejecting an item** | Not really supported — approval is effectively all-or-nothing | Per item, with rejected items free of charge | ✅ **Shipped** |
| **Fees on historical returns** | Recalculated from current settings, so editing a fee setting changes past returns | Recorded at approval and fixed | ✅ **Shipped** |
| **Price quoted at approval** | The customer pays whatever the price is when they check out | They pay what they actually paid for the garment they returned — a bulk or tier price included — however the catalogue price moves afterwards | ✅ **Shipped 2026-09-11** |
| **Approving twice** | Possible to double up | Has no effect the second time | ✅ **Shipped** |
| **Exchanging one of a pair** | A product that must be bought 2, 4 or 6 at a time could not be exchanged one at a time | One may be exchanged on its own. The customer already bought the pair; swapping one of them is not a new purchase | ✅ **Shipped 2026-09-11** |
| **A straightforward faulty-item swap** | The customer had to open their basket and complete a $0 checkout | Nothing to pay and no fee means the replacement order is simply placed for them | ✅ **Shipped 2026-09-11** |
| **Delivery paid by subsidy** | — | Covered by the global pot when the store allows it, and **not** given back on a return (§5.6) | ✅ **Shipped 2026-09-11** |
| **Order in which store credit and subsidy are applied** | Store credit first at the money level; product-group units and money were consumed anyway, so a unit and a credit note could both pay for one garment | **Subsidy first, then store credit** (§3.1), each consumed once | ✅ **Shipped 2026-09-11** |
| **Store credit *and* subsidy charged for the same order** | An order can consume store credit **and** the full subsidy for the same value, draining both | Each is consumed once, in order; together they never exceed the order value | ✅ **Shipped** |
| **A returned item sitting in the basket** | Consumes subsidy a second time when the replacement is checked out | The replacement consumes subsidy exactly once — the entitlement the returned garment consumed has already been given back | ✅ **Shipped 2026-09-11** |

Nothing about the customer's journey changes: the same wizard, the same choices, the same cart-based
completion for exchanges that need paying for.

The wizard itself has already been rebuilt — it was carried over from the previous system incompletely and
several parts of it did not work. That work is finished and is described in §3.2. What happens to the
money and the entitlement after a return is submitted was the last large piece, and it was tested against
a live store on 11 September 2026 across twenty-three scenarios covering every combination of units, group
money, global money, store credit and the customer's own money.

**Still to come:** recording which return an exchange order came from.

**One thing worth knowing about approvals.** When a return needs nothing further — no fee, and the
returned entitlement covers the replacement — approving it places the replacement order straight away and
closes the case, so there is no *Undo Approval* afterwards. That is how the previous system behaved too.
If one is approved in error, cancel the replacement order: the entitlement and any store credit go back to
the employee automatically. Returns that still owe a fee keep their *Undo Approval* as before.

**Two things are with the manager**, both in `CWPlans/Deprecated/Exchanges_Questions_For_Manager.md`: whether an
exchange should be charged delivery **and** the redelivery fee (question 15 — on a store with real freight
this is the whole delivery cost on every exchange), and whether a customer should be able to choose their
billing address (question 16).
