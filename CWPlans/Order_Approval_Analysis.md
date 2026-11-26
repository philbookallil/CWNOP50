# Order Approval — Analysis and Options for 5.00

Status: **the engine is applied** (Part 1, 2026-11-17) **and aligned with 4.1** (2026-11-23, §10; run on the dev site the same day: A1–A18 pass, two with a note, §10.6). Part 2, the screens, is applied (2026-11-23) and run on the dev site (2026-11-24, B1–B20 pass, §11). The approver's page was redesigned and run on the dev site on 2026-11-24 (C1–C11 pass, §12). The lists of orders and approvers, and the labels of the subsidy and store credit histories, are applied and run on the dev site (2026-11-24, D1–D16 pass, §13). §1–§8 are the original analysis: where they differ, §10 holds.
Gap analysis item C3 / OI 2.2.1
(`LegacyVersions/NOP41_To_500_Gap_Analysis.md`).

**Decided (Eduardo, 2026-11-17):**

| # | Decision | Answers |
|---|---|---|
| D1 | A pending approval is a **real nop order held back** by a CW `OrderApproval` row (option A, §5.1). No pre-order, no held cart | Q1 |
| D2 | Approver levels for the first release: an **approver entity with rules on branch** (optionally with child branches), **cost centre, department and individual** (level L2, §5.2). No subsidy level | Q2 |
| D3 | **Card orders are not held for approval** in the first release (P1, §5.3): card methods are hidden when approval is required. Pay after approval (P2) is a later phase | Q4 |
| D4 | The threshold measures **gross − subsidy** (goods + shipping incl. tax, minus subsidy for goods and shipping), and approval is needed when it is **strictly over** the threshold. A threshold of 0 or empty means every order | Q3 |
| D5 | Rules are matched against **each line's packname** (its branch, cost centre, department, the packname itself), plus the **ordering customer's** individual rule. All matching approvers are asked, without duplicates; one decision covers the whole order | Q5 |
| D6 | **Exchange replacement orders and return orders are never held** | Q6 |
| D7 | Accept and decline e-mails go to the **ordering officer only** | Q7 |
| D8 | Accept **moves the order to Processing**; payment status is untouched | Q8 |
| D9 | Reminders at **3 and 7 days**, expiry at **14 days** (per-store settings). Expiry cancels the order and returns the lines to the cart, as a system decline | Q9 |
| D10 | Admins **may approve or decline** on the approver's behalf, under a new permission, recorded as "by admin X" | Q10 |
| D11 | **No approver login page** in the first release; approvers act from the e-mail links | Q11 |

| D12 | Phase 1 is specified in two parts. **Part 1, the engine**: tables, switch and settings, the rule check, the hold at placement, the payment-method filter, the approver pages, accept / decline / expiry, e-mails, guards, the admin order-page panel. Rules are inserted by a test-data SQL. **Part 2**: the rule panels on the branch, cost centre, department and customer pages, the approver list, the pending report, and the storefront approval status | spec split |
| D13 | Which payment methods can be held is a **per-store list**, `MultiStoreSettings.ApprovalPaymentMethods` (default `Payments.CheckMoneyOrder`). When an order needs approval, checkout offers only those methods and placement refuses any other. An order with nothing left to pay needs no method and can always be held | D3 detail |
| D14 | A **plain order** (no packname lines) matches the ordering customer's branch (`CORE_CustomerBranch`) and the customer's individual rule. Cost-centre and department rules do not apply to it | D5 detail |
| D15 | The switch is a **sixth CW feature**, `CWFeature.Approvals` (`MultiStoreSettings.ApprovalsEnable`), on Store Settings and Default settings. It is off by default and **independent of PackName** | switch |

D3, D4, D5, D13, D14 and the expiry default of D9 were replaced on 2026-11-23 (§10). Part 1's spec: `Deprecated/Order_Approval_Part1_Spec.md`.

Sources read:
- *Order Authorisation System* (2019): the 4.1 feature as sold.
- *Approval System — DRAFT* (2022): the redesign that 4.6 tried to build.
- The 4.1 code (`NOP41/Nop.Plugin.TIG.Base`) and the 4.6 code (`NOP46/Nop.Plugin.Jim2.BE`, `Jim2.Allowances`).
- The 5.00 code that the feature would touch (CW.Core order placement, settlement, credit notes, packnames; nop 5.00
  orders and payments).

---

## 1. What the business asked for

These points are common to both documents:

1. **Who asks for approval.** An *ordering officer* places an order: a manager ordering for staff, or an employee.
   Their orders need approval from a senior person.
2. **Who approves.** The *approver* usually has **no login**. They act from a link in an e-mail: **Accept**, or
   **Decline** with a reason.
3. **Until it is accepted the order is "not active".** It is not sent to accounts or the ERP, and is not fulfilled.
4. **Accept** releases the order and e-mails the ordering officer.
5. **Decline** cancels the order and e-mails the reason. The order's lines go **back to the officer's cart**,
   packnames included, so they can adjust it and order again. The cycle can repeat.
6. **Threshold.** "Approval required over $X" lets small orders go through without approval (2022 only).
7. **Approvers can be linked at several levels**: branch, subsidy, cost centre, department, or an individual
   customer. When several rules match, all their approvers are e-mailed, without duplicates (2022 only).
8. **Admin screens** (2022 only, the mock-ups):
   - an **Approver list**: name, e-mail, staff count, approvals, value approved;
   - an **Approver editor**: the staff assigned to one approver, with add, remove and change approver;
   - a **Pending orders report** with *Change approver* and *Send e-mail*;
   - "Waiting approval by … [Change]" on the admin order page.
9. **Credit card orders** (2022 only): the order skips payment at checkout. Once it is approved, the officer gets a
   "please pay" e-mail and a sign-in prompt, and pays on a payment page.
10. **Subsidy is pre-approval.** Both documents say that a subsidy already is an approved budget. Approval is for what
    the subsidy does not cover.

The 2022 document also floats letting an approver edit the order, and rejects it: the order would end up under the
approver's name. **Decline and reorder is the preferred path.**

---

## 2. What 4.1 built (the 2019 feature)

The feature was **branch-only and Purchase-Order-only**.

| Aspect | 4.1 |
|---|---|
| Configuration | `Branch.AuthorisationRequire`, `ApprovalEmail` (a comma list), `AuthorisationThreshold`. `OrderAuthorizationSettings.LeaderEmailAddresses` exists but nothing reads it |
| Trigger | The `OrderPlacedEvent` handler, using **the ordering customer's branch** and `OrderTotal > threshold` (incl. tax and shipping). Only for the `PurchaseOrder` payment method: card orders are never held |
| Pending state | A real nop **Order**, left in `Pending`. The only thing that "holds" it is that the Jim2 send refuses a pending PO order |
| Token | One GUID per order, **shared by every approver**. The approver's e-mail address is a free-text URL segment. 90-day "expiry" that only re-sends |
| Accept | A **GET** link changes the state. It writes `OrderStatus = Processing` straight to the repository (no nop events), then sends to Jim2 |
| Decline | A reason form. Writes `OrderStatus = Cancelled` straight to the repository, then `ReOrder` to the cart |
| Task | Daily: re-sends every unvalidated authorisation older than 90 days, forever |
| Admin | Branch fields only. No pending report, no admin approve or decline, no change approver |

**Defects worth not repeating:**
- **Decline skips `CancelOrder`.** As a result `OrderCancelledEvent` never fired, and **subsidy, credit notes and
  stock were never given back**.
- A **GET approves the order**, so mail scanners (Safe Links, antivirus) can approve orders on their own.
- The shared token plus the address in the URL means anyone holding one link can act **as any approver**, so the
  "approved by" record cannot be trusted.
- The cart restore **duplicated packname links** and threw on cart lines that were not in the order.
- An admin could move an order off Pending and so send a declined order to Jim2.
- The decline reason went into the e-mail **without HTML encoding**.

## 3. What 4.6 built (the 2022 redesign, never worked for the client)

**It held a pre-order, not an order.**

| Aspect | 4.6 |
|---|---|
| Configuration | `OrderApproval` attached to **Customer**, **Branch (card file)** or **Company**. The first level found wins, with no union. Five booleans plus a threshold (`ApprovalRequiredForAllowance`, `AllowApprovalWhenAllowanceExceeded`, `OrderingOfficerCannotPurchase`, …). Approvers are a separate `OrderApprover` (name, e-mail) table with a mapping. No cost-centre, department or subsidy level, despite the document |
| Pending state | **`PreOrder`, a full copy of the Order schema** with its own items, shipping, notes, gift cards, packname rows and subsidy rows. The real order is created **only on approval** |
| Subsidy | Reserved by `AllowancePreOrderItem` rows (4.6's name for subsidy). **Every subsidy SQL view had to union the pre-orders** with status Pending |
| Stock | Taken at pre-order time, **never returned on decline** |
| Credit notes | **Not handled at all** |
| Checkout | A 7-flag decision tuple. The officer could *pay instead of asking*, *pay all without subsidy*, or *pre-authorise the card* (HappenPay) |
| Approval | A GET shows the page and a POST acts, with anti-forgery. **One random token per approver**, valid 7 days. The approver might have to **choose a payment method and pay** on the approval page |
| On approve | Copies the pre-order into a real nop order outside the customer's context, then publishes `OrderPlacedEvent` |
| Admin | Approver list and editor, pending report, swap approver, resend |

**Why it never worked:**
1. **Configuration was a trap.** An approver e-mail typed into the configuration was **silently dropped** if no
   `OrderApprover` with that address existed. The pre-order then had no approvers, nobody was told, and it stayed
   pending for ever.
2. **Too many paths**:
   - three ways to skip approval at checkout;
   - three payment paths at approval: none, the approver pays, or a captured pre-authorisation;
   - two payment callbacks.
3. **A second copy of the order** that every subsidy view, report and e-mail had to learn about. Placement happened
   on approval, by an anonymous approver, outside the customer's context.
4. Tokens expired after 7 days with **no reminders and no expiry of the pre-order**. Subsidy and stock stayed locked
   until an admin noticed.
5. **Approval applied only to packname checkout.** The plain checkout bypassed it.
6. Security: `UpdatePaymentMethod` took no token (any visitor could change another order's approval payment method).
   The approver's identity was lost on the final order. After "swap approver" the old tokens still worked.

**The lesson from both versions:** 4.1's *shape* was right (a real order held back, cancel and reorder on decline). Its
*implementation* was wrong. 4.6's shape (a parallel pre-order) was the source of most of its complexity.

---

## 4. What 5.00 gives us today

**How it fits:**

| Fact (5.00) | Consequence for approval |
|---|---|
| Subsidy and credit notes are **consumed at placement**. `SettleOrderAsync` writes `SubsidyOrderItem`, `SubsidyOrderShipping` and `CreditNoteHistory`, plus the `CORE_PN_OrderSettlement` row (`OrderProcessingOverrideService.cs:1311`) | A held order **already reserves** its subsidy and store credit. No new reservation mechanism is needed |
| **A cancelled order gives everything back with no code.** The subsidy views and procedures filter `OrderStatusId != 40 AND Deleted = 0`, the credit-note views filter `!= 40` (`CWDatabaseService.cs:413, 439, 467, 654, 666, 733, 1056, 1083`). This is decision 7 / C-08 in `subsidy-system.md` | **Decline = nop `CancelOrderAsync`**. It also returns stock, reward points and gift cards (nop `OrderProcessingService.cs:2391`). This fixes 4.1's biggest defect for free |
| CW's `ReOrderAsync` override (`:1679`) puts packname lines back with their packname, and skips packnames the customer may no longer order for | **The "back to cart" step already exists**, without 4.1's duplicate links |
| `PackNameOrderItem` snapshots each line's packname, branch, department and cost centre | Approver rules can be evaluated **per line** against the packname ordered for, not only against the ordering manager |
| Message templates, token groups and a scheduled-task pattern exist (`PackNameWorkflowMessageService`, `MessageTokenProviderOverride`, `ProcessSubsidyExpiryRemindersTask`) | E-mails, reminders and expiry follow known patterns |
| Feature switches (`CWFeature`, `Feature_Toggles_Reference.md` §2) | A new **`Approvals`** switch per store, seeded off |
| nop `OrderStatusChangedEvent` handlers may change the status back (`OrderProcessingService.cs:1026`) | A held order can be kept from being moved to Processing by payment or shipping, through nop's own seam |

**What gets in the way:**

| Fact (5.00) | Consequence |
|---|---|
| nop has **no native order approval**. `Misc.RFQ` is a customer ↔ store-owner quote flow, not an approver flow | Built in CW.Core |
| nop's order statuses are fixed: Pending 10, Processing 20, Complete 30, Cancelled 40 | "Awaiting approval" is a **CW state beside the order**, not a new nop status |
| **The NAB gateway charges at placement and cannot capture later** (`SupportCapture => false`, `txnType 0`). Card data is never stored | Card orders cannot be "authorised now, captured on approval". The options are §5.3 |
| nop re-posting a payment only works for **redirection** methods (`CanRePostProcessPaymentAsync`). NAB is a Standard method | "Pay after approval" needs a **CW payment page** for an existing order. That is real work |
| nop sends its "order placed" e-mails at placement (`SendNotificationsAndSaveNotesAsync`) | The customer's placed e-mail must say "awaiting approval", and the store owner must not mistake it for a live order |
| **Nothing sends orders to an ERP yet** (Odoo syncs master data only) | "Not sent to accounts" is a **rule for the future push**: whatever exports orders must skip a held one. Today the held state matters for fulfilment (shipments), admin status changes and returns |
| Packname orders can be **placed by a payment method's own pages** (`OwnPlacementPaymentMethods`, PayPal) | The hold must be applied wherever the order is created, not in the checkout controller |

**A side finding, outside this feature's scope:** the credit-note views (`CWDatabaseService.cs:1056` and `:1083`) do
not filter `o.Deleted = 0`, while the subsidy views do. A deleted order that was never cancelled therefore still uses
the customer's store credit. `subsidy-system.md` says release happens on "cancelled or deleted". Approval would not
make this worse, because decline cancels. It is noted here so it gets its own fix.

---

## 5. The options

The design comes down to three independent choices: **what a pending approval *is*** (5.1), **how approvers are
found** (5.2) and **what happens with card payments** (5.3).

### 5.1 What a pending approval is

**A — A real order, held (recommended).** Checkout places a normal nop order. If the rules say approval is needed, a
CW `OrderApproval` row is written beside it, in status *Pending*, in the same placement. While that row is pending:
- the order stays nop-`Pending`;
- admins cannot add shipments, mark it Processing or Complete, or start a return on it (a global filter, as
  `PackNameOrderAdminGuardFilter` does today);
- a status change to Processing is reverted by an `OrderStatusChangedEvent` consumer;
- any future ERP export skips it.

On **accept**, the row becomes *Approved* and the order is released (nop status logic resumes; see §6.4).
On **decline**, the row becomes *Declined* with the reason, then:
- `CancelOrderAsync` runs, which gives back stock, subsidy and credit notes through the views, plus reward points and
  gift cards;
- `ReOrderAsync` puts the lines back in the ordering officer's cart;
- the officer is e-mailed.

- **For:** no second copy of the order. Subsidy and credit notes are reserved and released by the mechanisms already
  built and tested. Totals, settlement and invoices are computed once, at checkout, in the officer's own context.
  Every report and screen already shows the order. It is 4.1's shape, which the client knew, minus 4.1's defects.
- **Against:**
  - the order exists and has a number while it waits, so screens and e-mails must say "awaiting approval";
  - a declined order stays in history as Cancelled (which is also an audit trail);
  - stock is held while the order waits (the right behaviour for uniforms with sizes).

**B — A pre-order, the 4.6 way.** The cart is frozen into CW tables and the real order is created on accept.
- **For:** there is no order until it is approved.
- **Against:**
  - every subsidy and credit-note view, the settlement pipeline and the stock rules would need a parallel "reserved"
    path;
  - placement would run at accept time, outside the officer's context (packname locks, the settlement quote, prices
    and subsidy periods that may have changed meanwhile);
  - it is the design that failed.

  **Not recommended.**

**C — Hold the cart.** The cart is submitted for approval and locked, and accept places the order.
- **Against:** nothing is reserved. The officer's subsidy could be spent by another order before approval, and the
  placement problems of B remain.

  **Not recommended.**

### 5.2 How approvers are found

The ordering officer is often a **manager ordering for several packnames in one order**. Each line has its own
packname, branch, department and cost centre (`PackNameOrderItem`).

**Approver.** A CW entity: name, e-mail, store, and optionally a link to a nop customer. It is the "tag" of the 2022
document. Created implicitly when an e-mail is typed into a rule, so no address is ever silently dropped (the 4.6
trap).

**Rule.** *Scope* → approvers + an optional threshold. Possible scopes:

| Scope | Matches a line when | 2019 | 2022 doc | 4.6 built |
|---|---|---|---|---|
| Branch (optionally including child branches) | the line's packname is in that branch | ✔ | ✔ | ✔ (card file) |
| Cost centre | the line's cost centre | | ✔ | |
| Department | the line's department | | ✔ | |
| Customer / packname (individual) | the line's packname, or the ordering customer | | ✔ (a Brandworx request) | ✔ (customer) |
| Subsidy | the line is paid by that subsidy | | ✔ | flags only |

**Evaluation (proposed):**
1. For every line, collect the rules that match.
2. Approval is required if any matching rule's threshold is exceeded (§5.4 says by which amount).
3. The order's approvers are the **union of those rules' approvers, without duplicates**, as the 2022 document says.
4. **One decision covers the whole order.** There is no partial approval: a decliner sends the whole order back,
   and the officer reorders.

**Levels of build**, each a subset of the next:
- **L1 — Branch only**, like 2019: approvers and a threshold on the branch form. Smallest. Covers the client who
  used 4.1.
- **L2 — Approver entity + branch, cost centre, department, individual** (recommended). Brings the approver list and
  pending report of the 2022 mock-ups. The subsidy scope is left out (see Q2).
- **L3 — L2 + subsidy scope + per-scope flags** (4.6's `ApprovalRequiredForAllowance` and similar). This is where 4.6's
  complexity came from.

### 5.3 Card payments

NAB charges at placement and cannot capture later, and the card is never stored.

| Option | How | For | Against |
|---|---|---|---|
| **P1 — Approval only for orders not paid by card** (recommended for the first release) | Orders paid on account, or fully paid by subsidy and credit, go through approval. When approval is required, card methods are hidden at checkout. A card order is never held | No payment work. Matches 4.1, which only held PO orders | A store that pays by card cannot use approval yet |
| **P2 — Pay after approval** (the 2022 design) | When approval is required the payment step is skipped and the order is placed with payment Pending. On accept, a "please pay" e-mail and a sign-in banner lead to a **CW pay-for-order page** that runs the payment plugin on the existing order | The designed flow | A new payment page for an existing order, which nop has only for redirection methods. Unpaid approved orders need chasing. It applies to every payment plugin a store uses (NAB, PayPal) |
| **P3 — Charge, then refund on decline** | Card orders are charged at checkout. A decline refunds through the plugin | Simple to build if the plugin refunds | The officer is charged for orders that may be declined. NAB refund support would need checking. Not what the documents describe |

**Recommendation: P1 now, P2 as a later phase** if a card-paying client needs approval.

### 5.4 What the threshold is measured against

| Amount | Meaning |
|---|---|
| Order gross (goods + shipping, incl. tax) | Everything ordered, whoever pays |
| Gross − subsidy (recommended) | What is ordered beyond the pre-approved budget. Matches "a subsidy is a pre-approval" |
| What the customer pays (`OrderTotal`) | Also nets off store credit, which is the client's money coming back from returns |

**Recommended:** gross − subsidy, compared with **≥** ("over $X" means anything above, so an order of exactly $X
needs no approval: decide `>` or `≥` in Q3). An order fully covered by subsidy needs no approval unless the rule's
threshold is 0 ("always").

---

## 6. The recommended shape (A + L2 + P1), in more detail

### 6.1 Data (CW.Core)

These are the entities the spec would describe; they are not built.

| Entity (table) | Fields |
|---|---|
| `Approver` (`CORE_APR_Approver`) | Id, StoreId, Name, Email, CustomerId?, Active, Deleted |
| `ApprovalRule` (`CORE_APR_ApprovalRule`) | Id, StoreId, ScopeType (Branch / CostCenter / Department / PackName / Customer), ScopeId, IncludeChildBranches, Threshold?, Active |
| `ApprovalRuleApprover` (`CORE_APR_ApprovalRuleApprover`) | RuleId, ApproverId |
| `OrderApproval` (`CORE_APR_OrderApproval`) | Id, OrderId (unique, FK cascade), Status (Pending / Approved / Declined / Expired), ThresholdAmount, MeasuredAmount, DecidedByApproverId?, DecidedOnUtc?, DeclineReason, CreatedOnUtc |
| `OrderApprovalRequest` (`CORE_APR_OrderApprovalRequest`) | Id, OrderApprovalId, ApproverId (snapshot of name and e-mail), TokenHash, SentOnUtc, ExpiresOnUtc, LastReminderOnUtc, RevokedOnUtc |

### 6.2 Tokens and the approver's pages

- One **random token per approver per order**, at least 32 bytes. Only its **hash** is stored. It expires, and it
  dies with the decision, with *Change approver*, or with a resend.
- The link opens a page (GET) showing the order: its lines, packnames, destinations and totals, including subsidy and
  credit. **Accept and Decline are POSTs** with anti-forgery, so mail scanners cannot act on the link.
- The token says **who** the approver is. No e-mail address appears in the URL.
- **First decision wins, under a lock**, the same per-order lock pattern as placement. A second click shows "already
  approved by X on …".
- The decline reason is HTML-encoded wherever it is shown.

### 6.3 Where the hold is applied

- **In placement**, not in a controller. After `SettleOrderAsync`, in `PlacePackNameOrderAsync` and in the plain
  `PlaceOrderAsync` override, so packname checkout, plain checkout and own-placement payments are all covered.
- **Never** for exchange **return orders** (`SaveReturnOrderDetailsAsync`): those are system orders.
- For **exchange replacement** carts: see Q6.

### 6.4 Accept

Steps:
1. Mark the `OrderApproval` row *Approved*.
2. Revoke the other approvers' tokens.
3. Add an order note.
4. Send *Order.Approval.Accepted* to the officer (and to the packname employees? see Q7).
5. Run nop's `CheckOrderStatusAsync`.

An on-account order then stays nop-Pending, as any on-account order does today, but is now **released**: shipments
and status changes are allowed.

**Open:** whether "approved" should also move the order to Processing, so admins can see at a glance which orders
are live (Q8).

### 6.5 Decline

1. Mark the row *Declined* and store the reason.
2. `CancelOrderAsync`, which gives back stock, subsidy and store credit, reward points and gift cards.
3. `ReOrderAsync` into the officer's cart.
4. Send *Order.Approval.Declined* with the reason and the lines.

The subsidy period may have closed while the order waited. The credit then returns into a closed period, which is the
same as cancelling any order today.

### 6.6 Waiting

A scheduled task, with these steps:
- **Reminders** at configurable days.
- **Expiry** after N days. Recommended action: cancel and return the lines to the cart, as a decline by the system,
  with its own e-mail. This frees the subsidy and stock that 4.1 and 4.6 left locked for ever.
- Admins can **Change approver** (revokes the old tokens and sends new ones) and **Resend**, from the order page (the
  `OrderDetailsBlock` widget) and from the pending report.
- **Admins can also approve or decline** from the admin order page (a permission), for when the approver is
  unreachable.

### 6.7 What the storefront shows

- The completed page and the officer's placed e-mail say **"awaiting approval by …"**.
- The order list and order details show the approval status.
- The packname bar and My Subsidies already show the reserved credit as spent (it is consumed). They need a line
  saying it comes back if the order is declined.

### 6.8 Admin

- **Approvers:** a list (name, e-mail, staff covered, approvals, value approved) and an editor (the rules and staff
  they cover, *Change approver* for all of them, remove).
- **Approval rules on the existing forms:** the branch form, and the cost-centre and department panels of Manage
  Store. An individual rule on the customer or packname edit.
- **Pending approvals report:** order, officer, branch, cost centre, created, total, approvers, Change / View /
  Resend.
- The `Approvals` feature switch and its settings (reminder days, expiry days) on the store settings.

### 6.9 Feature switch (`Feature_Toggles_Reference.md`)

When the switch is **off**:
- no new order is held;
- the rule screens hide.

Orders that are already pending can still be **accepted, declined and expired**: by the rule "never gate finishing
existing work", switching the feature off must not strand them.

### 6.10 Size

| Piece | Size |
|---|---|
| Data, rules and evaluation; the hold in placement; tokens and the approver pages; accept and decline; e-mails (templates and tokens) | M–L |
| Guards (admin filter, status revert, returns), task (reminders and expiry), admin order-page panel | M |
| Approver list and editor, rule panels, pending report | M |
| P2, pay after approval (later) | L |

A sensible phasing:
1. **Phase 1:** A + L2 rules, P1, the approver pages, accept and decline, guards, task, order-page panel.
2. **Phase 2:** the approver list and editor and the pending report. These read the same tables, so the schema does
   not change.
3. **Later:** P2.

---

## 7. Questions for you (each with my recommendation)

**Q1. Which shape: a held real order (A), a pre-order (B) or a held cart (C)?**
Recommend **A**. Subsidy, credit notes and stock are already reserved at placement and given back on cancel. B is
what made 4.6 fail.

**Q2. Which approver levels for the first release?**
Recommend **L2**: branch (with an option to include child branches), cost centre, department, individual. **No
subsidy level**: a subsidy is already a pre-approval, and the threshold of Q3 already nets the subsidy off. Add it
later only if a client asks for "approve anything charged to subsidy X".

**Q3. What does the threshold measure, and is it "over" or "at or over"?**
Recommend **gross − subsidy, strictly over** (`>`). The 2019 and 2022 wording is "over $"; 4.1 used `>` and 4.6 used
`≥`. A threshold of 0 or empty means "every order".

**Q4. Card payments: P1 (no approval for card orders) now, and P2 (pay after approval) later?**
Recommend **yes**. NAB cannot capture later, and P2 needs a new pay-for-order page. P1 covers how 4.1 was actually
used (PO and on-account clients).

**Q5. Whose rules apply: each line's packname, or the ordering manager?**
Recommend **each line's packname**, plus the ordering customer's individual rule. A manager ordering for three
branches then asks the approvers of all three. 4.1 used only the manager's branch, which misses staff of other
branches the manager orders for.

**Q6. Do exchange replacement orders need approval?**
Recommend **no**. They replace items already approved. Return orders never do.

**Q7. Who is e-mailed on accept and decline: the ordering officer only, or also the packname employees the order
was for?**
Recommend **the officer only**, as both documents say.

**Q8. On accept, should the order move to Processing?**
Recommend **yes**, so that "approved" is visible in every nop order list without a CW column, and nothing between
accept and fulfilment looks like a held order. Payment status is untouched (on account stays Pending).

**Q9. What happens when an approval is not answered?**
Recommend **reminders at 3 and 7 days, expiry at 14** (all per-store settings). On expiry, cancel and return the
lines to the cart, as a system decline.

**Q10. May an admin approve or decline on the approver's behalf?**
Recommend **yes**, with a permission, recorded as "approved by admin X".

**Q11. Should an approver who has a store login see a "my approvals" page?**
Recommend **not in the first release**. E-mail links cover the documented case (approvers without logins).

---

## 8. Before the spec

- Confirm with the client which of their stores need this, and with which payment methods (it decides Q4).
- The ERP order push does not exist yet. Its spec must include "skip held orders", and this document is where that
  rule is recorded.
- Read every view and procedure the hold must respect, and check every feature reader against the switch
  (`CLAUDE.md` §0.4).

---

## 9. Re-evaluation against 4.1 (2026-11-23)

The manager's scope: *"You can also get the TIG NOP4.1 code that has all this logic and structure built in and working
well. The addition is to be able to assign Approvers to a Department and not just a Branch. That is the main change
between the systems."* Read here as: **4.1's behaviour, with approvers on a branch or a department.**

Read again for this section: every approval file in `NOP41` (the `TIG.Base` zip the manager supplied is identical to
`NOP41/Nop.Plugin.TIG.Base`: 1,096 `.cs` / `.cshtml` files, none different). 4.1's order authorisation has no
department link; its departments are customer roles on the **ordering customer's** account.

**Verdict: no redesign.** 4.1 and 5.00 have the same shape (a real order kept Pending, e-mail links, accept → Processing,
decline → Cancelled and back to the cart). Department is already a scope of the engine. Four rules decided on 2026-11-17
(D3/D13, D4, D5/D14) decide **which orders are held** differently from 4.1, and the scope is wider than asked.

### 9.1 What matches, and stays

| Part | 4.1 | 5.00 |
|---|---|---|
| Pending state | a real order, Pending | the same, plus an `OrderApproval` row and guards |
| Approvers | no login, e-mail link, several addresses, any one decides | the same; a token per approver |
| Threshold | strictly over; empty or 0 = every order | the same |
| Accept | Processing, then sent to Jim2 | Processing; sending to Odoo: §9.4 |
| Decline | reason, Cancelled, lines back to the cart with packnames | the same, through nop's cancel (4.1 wrote the status straight to the table) |
| Return orders | never held | never held |

Kept although 4.1 does not have them, because they correct a 4.1 defect (§2) or cannot change which orders are held: POST to
decide, hashed token per approver, reminders, admin decision / resend / change approver, admin guards.

### 9.2 What decides differently from 4.1

| # | Rule | 4.1 | 5.00 as built | Consequence |
|---|---|---|---|---|
| R1 | Whose branch / department | the **ordering customer's** branch, only (`GetBranchForStaff(order.CustomerId)`) | each **line's packname's** branch / department; the customer's branch only for a plain order | A manager of a branch that needs approval, ordering for staff of a branch that does not, is held in 4.1 and **not** in 5.00; the reverse is held only in 5.00. Both documents attach the approver to the ordering officer |
| R2 | Card orders | only Purchase Order orders are held; a card order goes through | card methods are hidden when approval is required | 5.00 takes away a way to pay that 4.1 allows |
| R3 | Amount measured | `Order.OrderTotal`: what is left after subsidy **and store credit**, discounts and gift cards (exception: the split orders of a store with `HNOrderSplit` and several packname lines are saved with their line prices plus shipping, nothing taken off) | goods + shipping − subsidy; store credit, discounts and gift cards not taken off | 5.00 holds some orders 4.1 does not |
| R4 | Nothing left to pay | has no Purchase Order payment, so never held | held when its rule matches (blank threshold, or store credit paid more than it) | follows from R2 and R3 |
| R5 | Not answered | re-sent after 90 days, never cancelled | reminders at 3 and 7 days, cancelled at 14 | a held order is cancelled by the system |

### 9.3 What is wider than asked

Cost-centre, packname and customer scopes, and *include child branches*, have no screen yet and no counterpart in 4.1 or in the
manager's message (the 2022 draft has cost centre and individual). Part 2 as planned (approver list and editor, pending
report) is also 2022, not 4.1.

### 9.4 A gap, whatever is decided

The Odoo order send (added after Part 1) refuses only deleted and cancelled orders (`OrderSyncService.SendOrderAsync`),
so **a held order can be sent to Odoo**. 4.1 refused exactly this (`Jim2LinkerService.SendOrderToJim2`). The send must refuse
an order whose approval is Pending, and the automatic trigger must also run on accept, not only on `OrderPlacedEvent`.

5.00 also has no Purchase Order payment method: `Payments.CheckMoneyOrder` stands in for it, and 4.1's PO number on the approval
e-mail has no counterpart.

### 9.5 Questions — all accepted as recommended (Eduardo, 2026-11-23)

| # | Question | Recommendation |
|---|---|---|
| A | Whose branch and department? | **The ordering customer's**: their branch, and the department of their own packname. 4.1's rule, and both documents' |
| B | Both the branch and the department have approvers | **Ask both**, duplicates removed; the first answer decides (2022 draft) |
| C | Card orders | **As 4.1**: not held, and card stays offered. The hold then reads one thing, the order's payment method; the checkout factory override and the placement refusal go |
| D | Amount | **`Order.OrderTotal`**, as 4.1 |
| E | Not answered | **Keep the reminders; expiry off (0) by default**, so a store opts in to the system cancelling an order |
| F | Scopes | **Branch and department only**; the other three scopes and *include child branches* are removed (nothing is committed or installed elsewhere) |
| G | Screens (Part 2) | **4.1's panel** (*Employees require authorisation?*, *Authorise over $*, *Authorising emails*, comma-separated) on the branch forms and the department panel, plus the approval status on the storefront. The approver list and pending report wait until asked for |

With A–G as recommended: no new tables; `IncludeChildBranches` dropped from `CORE_APR_ApprovalRule`; the rule check, the placement
hook and the settings change; tokens, approver pages, decisions, guards, task, e-mails and admin card stay.

---

## 10. The engine aligned with 4.1 (applied 2026-11-23)

Eduardo accepted §9.5 A–G as recommended. These replace D3, D4, D5, D13 and D14, and the expiry default of D9; D2's levels become
branch and department; D6 stays, and covers an order that holds any replacement.

### 10.1 Which orders are held

The order is judged **once it is placed**, on `OrderPlacedEvent`, as 4.1 did (`IOrderApprovalRuleService.EvaluateOrderAsync`). It is held
when all of these are true:

1. Approvals is on for the order's store.
2. `Order.OrderTotal` is over 0, and its payment method is in `MultiStoreSettings.ApprovalPaymentMethods`.
3. An active rule with at least one active approver covers the **customer who placed it**: a branch rule through their branch
   (`CORE_CustomerBranch`), a department rule through the department of their own packname in that store. Department rules need
   PackName on; branch rules do not.
4. The order total is strictly over that rule's threshold; empty or 0 means every order.
5. None of its items is an exchange replacement. The return is completed when its replacement is ordered, so declining such an order
   would put the replacement back in the cart as an ordinary line, to be paid again (Eduardo, 2026-11-23: never hold it).

When a branch and a department rule both match, both sets of approvers are asked, each person once, and the first answer decides.

The total is what is left to pay after subsidy, store credit, discounts and gift cards, so no separate subsidy or credit arithmetic
is needed.

### 10.2 What changed in the code

| Removed | Why |
|---|---|
| `CheckoutOverrideModelFactory` (and its registration), `PrepareOrderApprovalAsync`, the *PaymentMethodNotAllowed* refusal | Card orders are not held, so nothing is hidden or refused |
| `EvaluateCartAsync`, `ApprovalLineScope`, the branch-ancestor walk | The order is judged, not the cart |
| Scopes cost centre, packname, customer; `ApprovalRule.IncludeChildBranches` | Not in 4.1 or the manager's scope |
| `HoldOrderAsync`'s second approver check and its note | The rules are read immediately before |

`OrderApprovalPlacementContext` now does one thing: it keeps a held **plain** order's e-mails until CW has settled the order. They now go
even when the settlement fails. An order whose first links were never sent asks its rules again on the first reminder or on
*Send the links again*, and a reminder with nobody to send to stays due and is logged.

Kept as built: tokens, approver page, decisions, guards, task, e-mails, admin card, permission, switch. `ApprovalExpiryDays` defaults to 0
(never); the reminders stay at 3 and 7 days. The e-mails and the Approval card say *Order total*.

CW.Core `2026_11_23_DATA_0.1`. 1,264 CW tests pass; the approval tests are in `Tests/CW.Tests/Core/Services/Approvals/` (rule service,
event consumer, placement context) and `Core/Helpers/` (`OrderApprovalRuleHelper`, `OrderApprovalTokenHelper`).

### 10.3 Dev database

The one database that ran Part 1 was brought to this schema by a script, since removed. Any other database gets the tables from the plugin's
install or update. Test rules: `TestData/OrderApproval_TestData.sql` (local, not in the repository).

### 10.4 Still open

| # | What | Recommendation |
|---|---|---|
| O1 | **Odoo sends held orders** (§9.4) | Refuse a held order in `OrderSyncService.SendOrderAsync`; agree it with the developer of that send |
| O2 | Purchase Order number on the approval e-mail | Only if a client uses it; 5.00 has no Purchase Order payment method |
| O3 | Part 2: 4.1's panel on the branch forms and the department panel, and the approval status on the storefront (§9.5 G) | Applied 2026-11-23: §11 |

### 10.5 Test plan

Run T1, T2 and T7–T18, T21–T25 of Part 1 again only where a row below touches them; the rest of the engine did not change.

| # | Do | Expect |
|---|---|---|
| A1 | Start the site, update the plugin | The five `CORE_APR_*` tables; e-mail text *Order total*; version `2026_11_23_DATA_0.1` |
| A2 | Store Settings and Default settings | *Payment methods that wait for approval* with the new hint; expiry 0 unless overridden |
| A3 | Branch rule over $100. Customer of the branch orders over $100 on account | Held; the approver's e-mail shows the order total |
| A4 | The same, for a packname of **another** branch | Held (the customer's branch decides) |
| A5 | Customer of a branch without a rule orders for a packname of the branch with the rule | Not held |
| A6 | Department rule. Customer whose own packname is in the department orders | Held |
| A7 | A branch and a department rule, one approver in both | Each approver is e-mailed once; the first answer decides |
| A8 | Payment step of an order over the threshold | **Every** active payment method is offered |
| A9 | The same order paid by card or PayPal | Not held |
| A10 | Order over $100 before subsidy or store credit, under $100 after | Not held |
| A11 | Order fully covered by subsidy or store credit, rule without a threshold | Not held; Paid, Processing as any order |
| A12 | Switch off: the order of A3 | Not held. An order held before can still be accepted |
| A13 | PackName off for the store: department rule, then branch rule | Department rule holds nothing; branch rule holds |
| A14 | Accept one, decline one | As Part 1 T9 and T10 |
| A15 | Days 3 / 7 / 0, held order back-dated 20 days, run the task | Reminder sent; the order does **not** expire |
| A16 | Delete a held order's request rows in SQL, then *Send the links again*; and once more with the task's reminder | The approvers its rules name get a link |
| A17 | Plain order (PackName off) part-paid by store credit, over the threshold after it | Held; the e-mails show the store credit taken off |
| A18 | Order of exchange replacements alone; then one with a replacement and new goods over the threshold | Neither is held |

### 10.6 Test run (2026-11-23, dev site, store 1)

A1–A18 pass; none fails. Two carry a note: A9 charged no card (the order's method was taken off the waiting list instead, which is the
same check); A18 ran with a replacement beside new goods (5027, $758.95, not held), and the replacements-alone case is a unit test. The
run also covered Part 1's T19, T25 and T27. Part 1's T5 evidence that PayPal was hidden proved nothing: PayPal is not connected on
that site, so nop always hides it.

---

## 11. Part 2: the screens (applied 2026-11-23)

Eduardo approved the design on 2026-11-23: 4.1's panel on branches and departments, and the approval status on the customer's pages. The
approver list and the pending report wait until asked for.

### 11.1 The panel

*Order approval*: **Employees require authorisation?**, **Authorise over $**, **Authorising emails** (commas, semicolons, bars or new lines).

| Form | Where the panel is | Saved by |
|---|---|---|
| Manage Store → Branches | Branch details, also on a new branch (its store is known) | `MultiStoreController.SaveBranch` |
| CW Admin → Branches | Its own card on the edit page; not on Create, where the store can still change | `BranchController.Edit` |
| Manage Store → Departments | Department details, also on a new department | `MultiStoreController.SaveDepartment` |

- Shown only while Approvals is on for the store. The panel posts `ApprovalRule.Available`, so a form loaded **without** it never saves an
  empty rule over the one it did not show.
- A refused post without the panel shows the saved rule, not an empty panel. A panel posted after Approvals was switched off is dropped.
- Checked only while approval is required: an e-mail at least, every e-mail valid, the amount 0 to 99,999,999,999,999.
- The department form names its panel `DepartmentApprovalRule`: Manage Store keeps both forms on the page, and equal names would give equal ids.
- One rule per branch or department: unique index on (`ScopeTypeId`, `ScopeId`).
- Unticking switches the rule off and keeps its amount and e-mails. Deleting the branch or department switches it off too.
- Approvers are found by address within the store, or created and named by the address. *Change approver* on an order gives such an approver
  its first name. Changing a rule affects new orders only.
- Same permission as the form; no new one.

### 11.2 What the customer sees

| Page | What |
|---|---|
| Order completed (nop's and the packname one) and order details | `OrderApprovalStatus` in the zones `CheckoutCompletedTop` and `OrderDetailsPageTop`: waiting (by whom, and that subsidy and store credit come back if declined), approved, declined with the reason, not approved in time |
| My orders | *Pending - waiting for approval* (`OrderOverrideModelFactory`) |
| Order details | Re-order is hidden while the order is held |

No storefront view is copied, so nothing is added to the theme collision set. An order's status is shown whatever the Approvals switch says now.

### 11.3 Test plan

| # | Do | Expect |
|---|---|---|
| B1 | Start the site, update the plugin to the new version | The unique index exists; the new texts show |
| B2 | Manage Store → Test Branch 1 | The panel shows the rule in the database (over $100, approver@example.com) |
| B3 | Change the amount and the e-mails, save, reopen | Values kept; the *Saved* stamp; one rule row; approvers created |
| B4 | Tick with no e-mail, a bad e-mail, a negative amount, an amount over the limit | Each refused with its message; the typed values are shown again |
| B5 | Untick and save; reopen; tick again | Rule off, values kept; then on again |
| B6 | A new child branch, with the panel filled | Branch and rule created |
| B7 | Edit a field of the panel and leave the form | The unsaved-changes question |
| B8 | CW Admin → Branches: edit the same branch | The same rule; saves; not on Create |
| B9 | Manage Store → Departments: an existing and a new department | As B2–B6 |
| B10 | Delete a test branch and a test department that have rules | Their rules are off |
| B11 | Approvals off: the three forms | No panel. Saving the forms leaves the rules untouched |
| B12 | A form opened with Approvals off, saved after switching it on: once valid, once refused (a name already used) and then corrected | The rule is untouched |
| B12a | A form opened with Approvals on, saved after switching it off | Saved; the rule is untouched |
| B12b | Open a branch, then a department, and click the department panel's labels | The department's own fields answer; the branch form is not changed |
| B13 | Steve orders over the amount set in B3 | Held by the rule from the panel; e-mails to both addresses, greeted by address |
| B14 | The completed page, My orders, order details | Waiting by whom; *Pending - waiting for approval*; no Re-order |
| B15 | An order part-paid by store credit or subsidy, waiting | The line that they come back if declined |
| B16 | Accept one, decline one; open both as the customer | Approved by / declined by with the reason; decided by an address shown once |
| B17 | A plain order's completed page (PackName off) | The same status |
| B18 | An order that needs no approval | Nothing on the three pages |
| B19 | *Change approver* to an existing address with a name | The approver takes the name; the next change of name is kept out |
| B20 | Another customer opens the order's details address | nop refuses, as before |

### 11.4 Test run (2026-11-24, dev site, store 1)

B1–B20 with B12a and B12b pass; none fails. B15 ran with store credit ($60 of 5029, returned in full when it was declined); subsidy reads the
same settlement row and was not run. B12 covers the review's finding: a form opened while Approvals was off, refused once and saved again,
leaves the rule as it was. No error was logged by the run.

Decided 2026-11-24: nop's "Your order has been successfully processed!" stays on a held order's completed page, under the waiting message.

---

## 12. The approver's page (2026-11-24)

Redesigned after Eduardo saw it plain and as one flat list. Nothing changes in the hold, the e-mails or the links.

### 12.1 What it shows

- **Header:** order number, who placed it and when, and the status. **Summary:** people, items, payable.
- **One block per packname** (`<details>`): name with its colour, branch, department and cost centre, items and total; open it for the products
  and the delivery (method, address, and with whom when at most three share it). Five blocks or fewer are open; more are closed, with
  *Expand all* (`order-approval.js`, the only thing that needs the script). An order without packnames is one list, with its method and address above it (no address when collected in store); a line for no packname in a
  packname order has its own block, *Other products*.
- **Totals that add up:** products, delivery, subsidy, store credit, *Discounts and fees* (whatever else makes the total: discounts, gift cards,
  reward points, fees, priced checkout attributes; a cent is rounding), payable. All incl. tax.
- **Decision:** *Accept order*; *Decline order* opens the reason. A decline that did not go through keeps the form open with what was typed.
- A link that no longer decides shows the status and the decision, not what was ordered, as before.

Files: `Views/OrderApproval/Review.cshtml`, `_ReviewLines.cshtml`, `Content/Approvals/order-approval.css` / `.js`,
`OrderApprovalModelFactory` (`PrepareGroupsAsync`, `PrepareTotalsAsync`), `OrderApprovalReviewModel`. Plugin version `2026_11_24_DATA_0.2`.
Resources no longer read (rows stay until deleted by hand): `Plugins.CW.Core.OrderApproval.Intro`, `.Status`, `.Column.For`, `.Totals.Gross`.

### 12.2 Test plan

| # | Do | Expect |
|---|---|---|
| C1 | Open the link of 5019 (one packname) | Header, summary, one open block, totals, decision |
| C2 | An order for several packnames with two deliveries, subsidy | One block each, totals per person, rows add up to payable |
| C3 | An order for six or more packnames | Closed blocks, *Expand all* / *Collapse all*; opening one by one changes the button |
| C4 | A plain order (PackName off) | One list, no people, no block to open |
| C5 | Phone width | Everything stacks; no sideways scroll |
| C6 | Decline with no reason; with a reason | The browser asks for it; posted empty, the form comes back open with the message; then declined |
| C7 | Accept; open the link again | The message and status; no products |
| C8 | A wrong link | The message only |
| C9 | Signed out, and with the store closed | As before |
| C10 | A customer whose first name is HTML; an order line for a person with a second address line | The name shows as text; the delivery names the second line |

### 12.3 Test run (2026-11-24, dev site, store 1)

C1–C11 pass; none fails. Orders: 5019 (one packname, still held), 5031 (four packnames, three deliveries, subsidy and store credit; accepted),
5032 (six packnames; declined), 5033 (plain, PackName off, paid gift wrapping; accepted), 5034 (two packnames, still held). Signed out, and with
the store closed, the link opens; the response says `no-store`. One flaw, fixed in `order-approval.css`: the theme's `.attributes` rule left
20px above and below a product's size and colour (seen fixed the same day). A plain order showed its shipping method without the
address; decided the same day to show it (`GetPlainDeliveryAsync`), in the packname blocks' order. nop's own address line starts with the
country, so only its fields are used. C11, after the rebuild, passes: 5035 (plain, still held) shows the method, the name and the address.

Found on the way and not part of this: a leftover plain cart line could refuse a packname order. Fixed 2026-11-24 (`PackName_Order_Shipments.md` §9.0a).

---

## 13. The lists and the histories (2026-11-24)

Asked for by Eduardo: the orders waiting for approval, the approvers, and how subsidy and store credit look while an order waits.

### 13.1 What each version shows

| | 4.1 | 4.6 | 5.00 |
|---|---|---|---|
| Balance while the order waits | Reserved | Reserved | Reserved |
| Subsidy history | An ordinary use | "Pre-order: {0}. Consumed $ allowance (pending approval)" | Marked, below |
| Store credit history | An ordinary use | Credit notes were not part of its approvals | Marked, below |
| After a decline | Never given back (it skipped nop's cancel) | Given back | Given back |
| List of waiting orders | None | Yes | §13.2 |
| List of approvers | None, e-mails on the branch | Yes | §13.3 |

### 13.2 Order approvals › Orders

*CW Admin › Order approvals › Orders* (`OrderApprovalAdmin/List`): waiting orders by default; filters by status, store (when there are several),
order number, customer and approver. An approver finds the waiting orders with a link of theirs that still works, and every decided one they were
asked about. Each row: order, customer and branch, placed on, payable, subsidy, store credit, status, who it waits for (with the reminders sent) or
who decided. *View* opens the order page, where it is accepted or declined, so nobody decides without seeing it; *Send again* and *Change* post the
actions of the Approval card. *Send again* (here and on the card) asks those the order waits for now: before, it also asked the approver the order
had been changed from, whose new link worked. *Change* (here and on the card, 2026-11-25) is one box with the addresses the order waits for, edited
freely: those kept keep their link and are not e-mailed again, those taken out lose theirs, those added are sent one (`ChangeApproversAsync`). It
used to replace them all with one person. Refused when somebody else changed them meanwhile. When no e-mail can be queued for the new ones nothing
changes (*Send again* too: the old links go on working). Reminders go to the addresses waited for, not to an approver's newer address. A new person is named by the address; their name is set in *Approvers*. `OrderApprovalReportService`, `OrderApprovalListModelFactory`, `admin.approval.lists.js`.

### 13.3 Order approvals › Approvers

*CW Admin › Order approvals › Approvers* (`OrderApprovalAdmin/Approvers`): name, e-mail, the branches and departments that ask each one (with the
amount, or *off*), how many orders wait, and how many it accepted and declined. No *Add*: an approver is created by typing the address on a branch or
department, so none is unused (4.6's trap). `ApproverService`.

- **Edit:** name and address; an address names one approver of a store. A new name shows at once on the waiting orders (list and customer's
  order) and on who decides (`GetWaitingApproverNamesAsync`, changed 2026-11-25: they showed the name on the link). After a change of address the
  orders already waiting keep the links, and the name, sent to the old one. The Approval card's list of links keeps what was sent.
- **Replace:** another approver (found by address in the same store, or created) takes every rule of this one. Orders already waiting keep theirs.
- **Remove:** only when no rule in force names it and no order waits for it. It is taken out of the rules that are off.

**The boxes of approvers** (2026-11-25; *Change* on the list and the card, *Replace*, the panel of the branch and department forms): the store's
approvers are suggested by name or address while typing, each chosen one is a tag, and a new address can still be typed. What is posted is the same
list of addresses. An address that cannot be one is a red tag, and the server refuses it. An approver's whole name typed and left counts too, when
only one has it. The suggestions come with the page, or with the grid's rows on the two lists, so a change made on the list shows at once (`ApproverSuggestionModel`,
approvers not switched off). Enter takes a whole address as typed, never an approver whose address only contains it, and submits nothing;
`admin.approver.picker.js` over Tom Select, registered by `_ApproverPickerAssets`.

Both pages need *Manage order approvals* and show in the menu while Approvals is on for any store, or any order ever needed approval: they are not
gated on the switch, because a held order still has to be decided.

### 13.4 The histories

- **My Subsidies:** "Waiting for approval" on each use by a held order, and under the figures what the season's held orders hold (money of every
  kind, and units) — not a part of *Used*, which counts only the subsidy's global credit (`CustomerSubsidyHelper.WaitingForApproval`).
- **Admin packname subsidy history:** "(waiting for approval)" after the reason.
- **My Store Credit and the admin credit note history:** "Waiting for approval", and "Order cancelled, returned" on the uses of a cancelled order
  (`ICreditNotesService.GetGivenBackOrderIdsAsync`, the orders the balance views leave out). Before, a declined order's credit still read as applied.
- The balances, the views and the history procedure are unchanged; the labels read `GetHeldOrderIdsAsync`.

Plugin version `2026_11_25_DATA_0.4` (texts). No tables.

### 13.5 Test plan

| # | Do | Expect |
|---|---|---|
| D1 | Menu, with Approvals on; off in every store | Both items; still there (orders exist) |
| D2 | Orders list | 5019, 5034, 5035 waiting, newest first; amounts as on their pages |
| D3 | Each filter; status All, Declined | Rows as filtered; decided rows name who decided and have only View |
| D4 | Send again on a row | Asked to confirm; message; a new e-mail; the old link replaced; the list reloads |
| D5 | Change on a row: empty, bad address, good | Refused with the message, what was typed stays; then the row names the new approver |
| D5a | Send again after a Change | Only the new approver gets a link; the first one's links stay stopped |
| D5b | Change: take one of two out, add another, keep one; the same list again; a list with a bad address; an empty box | The kept one is not e-mailed and their old link works; the one out cannot answer; the new one gets a link. Nothing sent or noted. Refused, what was typed stays |
| D5c | The same on the order page's Approval card | The same |
| D6 | View | The order page, with its Approval card |
| D7 | Approvers list, search | Each with its branches and departments, *off* where off; counts as the database |
| D8 | Edit: empty name, another's address, a new name | Refused twice; saved, and the branch panel shows a changed address |
| D9 | Replace with a new address; with one already in the rule; with itself | Moved and created; named once; refused. A waiting order keeps its approver |
| D10 | Remove one not used; one used (posted directly) | Gone from the list; refused |
| D11 | My Subsidies of Liam (5034 waits) | Labels on 5034's rows only; waiting total |
| D12 | Admin packname subsidy history of Liam | The same, in words |
| D13 | My Store Credit of Steve; admin credit note history | 5029 and 5026 "Order cancelled, returned"; a held order with credit "Waiting for approval" |
| D14 | Decline that order | Its rows: gone from the subsidy history, "returned" in store credit; balances back |
| D15 | A name with HTML on an approver | Shown as text in both lists and both dialogs |
| D16 | Without *Manage order approvals* | No menu; the pages and actions refuse |

| D17 | Change on a row, with the picker: type part of a name, part of an address, a new address; Enter, comma, Tab; paste three | Suggested and added as tags; the new one offered as "Add new approver"; three tags; saved as D5b |
| D17a | Type ann@acme.com + Enter with joann@acme.com an approver; focus and Enter; arrow down and Enter; a half name and Enter | Ann's address, new; nothing; the one gone to; the first match |
| D18 | Type an address and press Save at once (with the mouse, tags filling the line); type "jane" and leave; a wrong address | Kept and saved; the whole name of one approver is them; a red tag, refused, it stays |
| D19 | Change on an order of another store; the same order's box opened twice; Cancel and open again | Only that store's approvers; the box starts from the order's addresses each time |
| D20 | The order page's Approval card: the picker, Enter in it, Change with nothing edited | Works as the list; Enter submits nothing; nothing sent |
| D21 | Replace: the approver itself is not suggested; pick one; type a new one | One tag only, a new one typed takes its place; several pasted are refused; after an Edit, the new address is suggested, not the old; saved as D9 |
| D22 | Branch page, Manage Store branch and department: open, leave without editing; edit and save; a wrong address | No unsaved-changes question; saved; red tag and the message |
| D23 | An approver with HTML in the name; one switched off | Shown as text in the suggestions and the tag; not suggested |

### 13.6 Test run (2026-11-24, dev site, store 1)

D1–D16 pass; none fails; no error was logged by it. 5034 was sent again, changed and sent again (only the new approver got the last link). Test Approver
and Second Approver were replaced and their rules put back by hand. 5039 (Liam's subsidy, $80 store credit) was held and declined: its subsidy rows
left the history, its credit reads "Order cancelled, returned", and both balances came back.

D8a (2026-11-25): an approver renamed to Jane Manager shows at once on 5019, with "(1 reminder sent)"; 5034, whose approver's address changed
after the link was sent, keeps the name sent.

D5b and D5c (2026-11-25) pass: 5035 on the list and 5034 on the card. The kept person's link of the day before still opens the order and they got no
e-mail; the one taken out reads "This link no longer works"; an old box is refused. With the request template off, *Save approvers* and *Send again*
fail and every link that worked goes on.

D17–D23 (2026-11-25, the approver picker) pass, with real keys and clicks: 5034 and 5035 each got new approvers typed and saved with the address still in the
box; only the new ones were e-mailed. D19 was checked with an approver of another store added to the page's suggestions, as the second store has none.
The rules of Test Branch 1, Test Branch 1.1 and Department 1 were edited and put back. Fixed after it (CSS): the space between a tag's name and its
address, and the size of the text being typed.

Seen, not caused by it: on Manage Store, a branch save that is refused hid the branch's packname lists, cost centres and addresses until the branch was opened
again. Fixed 2026-11-25: the branch form is one element (`#branch-detail-panel`) and a refused save replaces only that element (the
view also had one `</div>` too many, removed). Seen on the site: the lists stay, the message shows, nothing is written, and the
next save works.

Fixed after the run and seen on 2026-11-25: nop's "incl tax" beside every amount of the two lists; the message for a name or address that
is not valid.
