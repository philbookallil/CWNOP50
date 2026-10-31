# Subsidy and PackName — fix strategy (phase plan)

> **4.9 history.** Copied from the 4.90 source (`NOP49/CWPlans/`) on 2026-10-31, when CW.Core moved to nopCommerce 5.00. It is kept as the record of the 4.9 work and is **not updated for 5.00**: file paths, line numbers and versions are 4.9's. For how CW.Core works now, read the references in `CWPlans/` and `CWPlans/CWCore_Port_Reference.md`.

**Date:** 2026-09-12, revised 2026-10-21 (rev. 22 — **phase 10, documentation consolidation, closed: every phase of this strategy is closed except 4c's PayPal tests; what is left is `Subsidy_PackName_Open_Items.md`**; rev. 21 — phase 11, packname bulk actions and selector, closed; rev. 20 — phase 9, performance and caching, closed; phase 11 next unless Eduardo picks phase 10; rev. 19 — phase 12, X-04 admin Excel import, closed; phase 9 next; rev. 18 — phase 12 added: the admin-side Excel import, X-04, wanted by
Eduardo, and run **next, before phase 9**; rev. 17 — phase 8 Part 2, X-01 Excel import, closed: the manager's import
page, one registration service, one name per list, switched-off lists take nothing; **phase 8 closed**, phase 9 next;
rev. 16 — phase 8 Part 1, settings that do nothing, closed: N-01 with the
shared-address line, P-15, X-02, T-01 (except the X-01 rows) done, plus F1 (settings overrides) and F2 (the standalone
Branches page, kept by the manager); Part 2 (X-01) next; rev. 15 — phase 7, storefront features, closed: M-01 / M-06 / M-02 / P-10 / P-13 /
P-14 / X-03 / N-02 / F2 / F3 / M-04 / P-18 / S-30 done, M-05 / P-09 / G-01 / top-up corrections recorded, shared-address rate
re-homed to phase 8; phase 8 next; rev. 14 — phase 6 Part 2 (O-02, O-04, F6) closed: **phase 6 closed**, phase 7
next; rev. 13 — phase 6 Part 1, money semantics and one settlement summary, closed; rev. 12 — phase 6 opened: analysis in `Subsidy_Phase6_Money_Semantics.md`,
decisions 5 and 6 to be confirmed as binding, §4; rev. 11 — phase 5 Part 2, the allocation gate, closed: phase 5 closed;
rev. 10 — phase 5 Part 1, department ACL, closed; rev. 9: phase 4c
applied and tested except its three PayPal checks, which wait for a PayPal sandbox). **Status:** **phases 1, 2a,
2b, 2c, 2d, 3, 4a, 4b and 5 closed; 4c applied, PayPal tests pending (2026-10-01). Phase 6 closed 2026-10-07
(Part 1 2026-10-02, Part 2 2026-10-07). Phase 7 closed 2026-10-08. Phase 8 closed 2026-10-19 (Part 1 2026-10-11, Part 2 2026-10-19). Phase 12 (admin Excel import) closed 2026-10-20. Phase 9 (performance and caching) closed 2026-10-20. **Phase 11 next** (or 10, Eduardo's choice). **Condition:** PayPal Commerce stays disconnected on packname stores
until 4c's T5, T6 and T8 and phase 6's T20 pass. **Input:**
`Subsidy_PackName_Gap_Analysis_And_Known_Issues.md` (2026-09-09), `Exchanges_Rebuild_Record.md` (state at
2026-09-12), `CLAUDE.md`, `tasks/lessons.md`.

This document turns the 96 open items of the gap analysis, plus the Manage Store rework, into **twelve
phases** (1, 2a–2c, 3, 4a–4c, 5–10) ordered so that every Blocker
lands in the first five, and so that cheap low-priority items ride along with the high-priority item that
touches the same file or needs the same test fixture. Each phase gets its own document (spec + test plan in
one file, per `CLAUDE.md` §0.4) when it is picked up; this file only says *what goes together, in which order,
and why*.

Items already closed and not planned again: **P-02** (fix 22), **S-17** (decision 21), **C-09** (decision 15),
**P-12**, **S-15**, **C-08** (decisions, 2026-09-12), and everything phase 1 shipped — **S-01**, **S-02**,
**S-09**, **S-10**, **S-11** (bar the variation-aware end), **S-13**, **S-20**, **S-22**, **S-23**
(2026-09-16); and everything phase 3 closed — **S-11**'s variation-aware end, **S-12**, **S-16**, **S-18**,
**S-19**, **S-24**, **S-27**, **S-28**, **A-02**–**A-07**, **M-06** (admin half), with **S-04** and **S-14**
found already closed by phase 2b (2026-09-29). Items found during a phase and deferred are in gap analysis §17b.

---

## 1. The rules behind the ordering

1. **Blockers first, but by surface, not by ID.** A blocker is fixed in the phase that owns the file it lives
   in, together with everything else that lives there. Fixing S-04 alone and coming back for S-12, S-19 and
   A-05 later means opening the same popup, the same factory line and the same test walk three times.
2. **One test fixture per phase.** The expensive part of every fix on this project is the manual verification
   run (`Exchanges_Rebuild_Record.md` §9.3: no automated coverage beyond fee arithmetic). So items are grouped
   by *what a tester has to set up* — "a packname whose period has closed", "two stores and a forged id",
   "a multi-packname cart to three branches" — and a phase is the set of items one fixture can prove.
3. **No phase waits on a decision it does not need.** Phases 1–3 need no manager decision at all. The
   fourteen decisions in gap analysis §17 are batched into one question sheet sent *now* (§2 below), ordered
   by the phase that needs them first, so they arrive before the phase that consumes them.
4. **Pre-production rules apply** (`CLAUDE.md` §0.8): dead code, dead stubs and dead CSS found in a file a
   phase touches are deleted in that phase, not logged for Q-01. Q-01 therefore shrinks as the phases go and
   the last phase is documentation, not code.
5. **Re-derive before implementing** (lessons #29, #33, #45). Every line number in the gap analysis is dated
   2026-09-09 and the exchanges phases 4b-3/4b-4/3b landed after it. Each phase document starts by
   re-reading the files it names and re-checking that each premise still holds — the gap analysis itself
   warns that P-02 was closed under it and that P-01 looks closed with an empty cart (lesson #44).
6. **`Tests/CW.Tests` exists now** (Note A closed 2026-09-12). Where a phase produces or exposes pure logic
   — the period window, the quantity-cap rule, the settlement summary — it pins it with Moq unit tests there.
   SQL is verified by a regression script committed beside the phase document under `CWPlans/TestData/`.

---

## 2. Decisions — answered 2026-09-12 (Eduardo), open ones marked

Numbers are the gap analysis §17 numbers. **Answered** rows are binding for the phase that consumes them;
**pending manager** rows are built on the stated default and revisited if the manager's answer differs;
**open** rows still need an answer before their phase.

| # | Question | Answer | Phase | Status |
|---|---|---|---|---|
| 3 | Negative topups? | **No.** A different credit or period for one packname is done through a **variation**. S-14 rejects ≤ 0 on the admin topup and product-group models | 3 | Answered |
| 1 | "Cannot order": hidden or blocked? | **4.6 strategy**: catalogue hidden, add-to-cart refused with the notice, blocked packname marked in the selector and info strip. Lines already in the cart when the period closes: hidden from the checkout as today, but once C-01 makes every reader use the same filtered cart they vanish *consistently* (header, flyout, selector) and are no longer deleted at placement (P-02 fixed). Notice wording: port the 4.6 resource text | 4a | Answered |
| 2 | `IncludeManagers` (S-06) | **On** = the `OnlyAllocatedProducts` and `RestrictToAllocatedQty` rules apply to managers as well; **off** = managers are unrestricted, only non-manager packnames are restricted | 4b | Answered |
| 2 | Credit lifts the quantity cap (S-08)? | **Yes, the 4.6 rule** — credit still *available* on a subsidy or group containing the product lifts the cap; it returns once that credit is spent | 4b | Answered 2026-09-30 |
| 11 | Department packname's roles | **Department role replaces the branch role**, as 4.6: a packname in a department is treated as carrying the department's customer role instead of the branch's | 5 | Answered |
| 8 | Unallocated products | **Merely unsubsidised** by default; **forbidden when `OnlyAllocatedProducts` is on** — and then forbidden everywhere (product page, related, cross-sell, wishlist, category menu), not only in the list (C-04). Gender attribute convention (C-03): still to confirm | 5 | Answered (C-03 mechanism open) |
| 5 | Discount / gift card / reward points vs subsidy | **Answered by the 4.1 code, 2026-09-16** (manager asked for 4.1 as the reference): subsidy is computed on the **undiscounted gross** — `OrderSubtotalExclTax + OrderShippingExclTax + OrderTax`, never `order.OrderTotal` — so none of the three reduces it. A customer discount reduces the customer's share only. Matches the proposed default. Gap analysis §16c | 6 | **Answered** |
| 6 | Order columns gross vs net; GST; Odoo | **Answered by the 4.1 code, 2026-09-16:** gross columns untouched, subsidy recorded as a payment tender in an `OrderPayment` ledger (`PaymentAmount` = 4.9's `SettleableAmount`). GST is **not** apportioned — `OrderTax` is subsidised in full and never modified, so the store remits on the full gross. **Odoo: no impact** — `Nop.Plugin.Integration.Odoo` syncs products and branches only, with no order or total field anywhere in it. Gap analysis §16c | 6 | **Answered** |
| 7 | Unpaid redirect-gateway orders holding credit | **No limit — confirmed by the 4.1 code, 2026-09-16.** Subsidy is consumed at placement with no payment-status condition, and released by exactly one path: `OrderCancelledEvent` → `UndoSubsidyForOrder`. No voided/refunded/deleted release and nothing time-based. C-08 stays closed as by-design. **Copy 4.1's release-on-cancel** when phase 6 opens the settlement work. Gap analysis §16c | 6 | Answered — C-08 closed |
| 4 | Shipping subsidised by default (S-15) | **No** — `IncludeShippingOnSubsidy` stays off by default. S-15 closes as by-design; note it in `subsidy-system.md` | — | Answered — S-15 closed |
| 14 | Admin editing of packname orders (O-01) | **Block now** (phase 2a filter). The packname-aware rebuild stays on the backlog and, if wanted, is scheduled **after phase 6** — it has to re-run settlement, so it depends on the settlement summary. Badges (O-04): answered 2026-10-01 (phase 6 doc §1 C — admin *Sales → Orders* and *Customer → Orders*; both badges when both apply). Coupons (G-01): unanswered | 2a / 6 / 7 | Answered (G-01 open) |
| 9 | "My Subsidies" depth | **Everything**: per subsidy remaining, per group units + credit, expiry, history with order links — **and the page is redesigned**: the current history and layout are hard for a customer to follow. M-01 therefore includes a UX pass, not a straight port of the 4.6 view | 7 | Answered |
| 10 | Manager subsidy selection (M-02) | **Keep**; managers may also **remove** a subsidy | 7 | Answered |
| 12 | Manager scope (P-06) | **Today's 4.9 rule is the intended one**: a manager sees every list on his own branch (flags or not) and every `AllManagers` list anywhere in the store, plus `ParentManagers` lists on child branches and direct mappings. P-06 closes except the missing `b.Deleted = 0` on Path 2 (2b) and a paragraph in `MultiStore_Management_Reference.md` (10) | 2b | Answered |
| 12 | Branch-default-address-only mode (P-12) | **Not needed.** P-12 closes; `CLAUDE.md` §15.3 drops the non-existent `UseBranchAddress` | — | Answered — closed |
| 12 | Cost-centre delivery (P-11) | **Not needed** — cost-centre addresses are not offered at checkout. **`MultiStoreSettings.ShippingMethod` is to be implemented, not deleted**: a store must be able to choose which shipping method(s) packnames may use. New item **N-01**, phase 8 | 8 | Answered |
| 15 | PayPal Commerce for packnames: wait for 4c, or fix earlier? | **Wait for 4c** (Eduardo, 2026-09-16). Phase 2a refuses the placement, so neither the invisible-paid-order nor the silently-unpaid-order outcome can happen in the meantime; PayPal simply does not work for packname customers until 4c lands. 4c therefore does **not** need to jump ahead of 2b/2c/3 | 2a / 4c | Answered |
| 16 | Credit notes before subsidy, or subsidy first? | **Subsidy first — settled, and the code already does it.** `SubsidySettlementSource.Priority => 1`, `CreditNoteSettlementSource.Priority => 2`, in place since the exchanges rework. 4.1 applied credit notes first for the *money* but subsidy **units** before anything, which is why it was remembered the other way; 4.9 makes both halves consistent — the employer's entitlement is spent before the customer's own credit. Confirms decision 15 | 6 | Answered — verified in code 2026-09-16 |
| 13 | Excel import / self-registration | **Import: yes** (port, phase 8 — the manager's import; **the admin import too**, Eduardo 2026-10-19 → phase 12, X-04). **Self-registration: no** — store registration already exists and there is no Company; X-02 becomes *delete* the `StoreRegistration` entity, the slug settings and the validator rule. Summary channel for the import: unanswered (default e-mail) | 8 | Answered (channel open) |

**Decision 12 — the manager-scope rule, written down (P-06 answer, 2026-09-12).** A manager can see and
order for: (1) every active list on his own branch, regardless of `AllManagers` / `ParentManagers`;
(2) every `AllManagers` list anywhere in the store; (3) `ParentManagers` lists on descendant branches;
(4) lists he is mapped to directly. `AllManagers = false` with explicit owners therefore means "not visible
to managers of *other* branches unless mapped", not "private within the branch". This is what
`packname_get_listids_by_manager` already does; the only defect left is Path 2 joining soft-deleted branches.

**Follow-up from P-11 (no decision needed, flagging it):** if cost-centre addresses are never offered at
checkout, `CostCenterAddressMapping`, its admin popups (`_Branches.CostCenterDetail.Addresses.cshtml`,
`_CostCenters.CostCenterDetail.Addresses.cshtml`, `CostCenterAddressPopup.cshtml`), service methods and
cache keys are dead. Phase 2c (which owns those views) should delete them unless something else uses a
cost-centre address — confirm at that point. **Not done, and no longer phase 2c's:** checked 2026-09-28,
all three views, `CostCenterAddressMapping`, its builder, its cache consumer, its migration, the repository
field and three controller actions are still present. Left in place by decision (Eduardo, 2026-09-28) — it is
an entity plus a migration, not a view tidy-up. **Reassigned to phase 8**, which already owns the deletions.

**4.1 check (gap §19) — closed, no query needed.** 4.1 does not derive consumption from orders by date at
all: `NOP41/Nop.Plugin.TIG.Base/Services/Subsidies/SubsidyService.cs` keeps stored balances
(`SubsidyRemain.SubsidyCreditRemain`, `TopupRemain`, `ProductGroupRemains`) and decrements them at
placement; `AnnualRecurrence` is only consulted to skip the "not started yet" check (`:1262`). The
S-01/S-02 functions were written for 4.6 and cannot exist in 4.1. **Confirmed 2026-09-12:** the 4.9 model
is deliberately different — everything is computed, and annual balances **must** reset automatically. That
is the S-02 target behaviour, not a side effect to flag.

**No historical data (2026-09-15).** The 4.9 database is a test instance with test data only; there is no
production 4.9 environment. So no phase needs a data migration, a before/after balance diff, or a
backfill, and "how many existing rows does this change" stops being a question any phase has to answer.
Where a 4.1 figure is ever genuinely needed, it has to be written against the **4.1** schema and the 4.1
subsidy model — which is a different design (stored balances, not computed) on a different nop schema.

**The time frame — answered, and built in phase 1 (2026-09-15).** Subsidy periods are expressed in the
**store's local time**. Phase 1 makes the three period functions pure (no clock, no zone) and has the views
supply the store-local dates from nop's own `DefaultStoreTimeZoneId`, per store, falling back to the
instance setting and then the database server. This was raised as phase 1's own finding rather than by the
gap analysis; it is recorded in `subsidy-system.md` §3.

---

## 3. Phase overview

| Phase | Name | Blockers inside | Everything else riding along | Decisions | Size |
|---|---|---|---|---|---|
| 1 | ~~Period arithmetic and ledger integrity (SQL)~~ — **closed 2026-09-16** | S-01, S-02 | S-09, S-10, S-11, S-13, S-20, **the local-time frame**, H-01 (SQL groundwork), Q-01 SQL items | local time = store local (answered) | M |
| 2a | ~~Access hardening — storefront~~ — **closed 2026-09-17** | S-05, P-01, P-04 | O-01 (block), P-05, P-07, G-02, G-03, U-01, U-02, U-03, U-11, U-16, H-03 (all consumers), H-05 | 14 (block) | M |
| 2b | ~~Access hardening — admin~~ — **closed 2026-09-23** | — | A-01, U-04, U-05, U-08, U-09, U-10, U-12, U-14 (scoping), U-15, P-06 (Deleted filter only), P-16, P-17 | — | M |
| 2c | ~~**Manage Store rework** — branches → lists → packnames, visual + JS~~ — **closed 2026-09-28** | — | ~~U-07~~, ~~U-17~~, ~~U-18~~, ~~X-01 (hide)~~, ~~Q-01 admin-view items~~ all done; U-13 (grid half) and the P-11 follow-up moved out by decision | — | ~~M–L~~ Done |
| 2d | ~~**MultiStore JS consolidation** — the half of phase 2c's second deliverable that was not done~~ — **closed 2026-09-29** | — | ~~30 inline `<script>` blocks across 18 partials~~ → 12; ~~29 `alert()` calls~~; ~~~15 hard-coded action URLs in views~~; ~~U-44~~ done. The module's own 16 URLs remain, and `Settings.cshtml` was out of scope | — | ~~L~~ Done |
| 3 | ~~Admin subsidy screens~~ — **closed 2026-09-29** | ~~S-04~~ (closed by 2b) | ~~S-12, S-14, S-16, S-18, S-19, A-02, **A-06**, A-03, A-04, **A-07**, A-05, M-06 (admin half), Q-01 subsidy items~~ all done, plus S-24, S-27, S-28 and a store deletion that soft-deletes the store's data | — (3 answered; A, B, C answered 2026-09-29) | ~~M~~ Done |
| 4a | ~~One cart, one gate~~ — **closed 2026-09-30** | S-03 | C-01, C-02, K10, P-08, G-04, G-05, G-06, G-08, K1, U-06, C-10, O-03 | — (1 answered) | ~~L~~ Done |
| 4b | ~~Allocation rules at add-to-cart~~ — **closed 2026-09-30** | — | ~~S-06, S-07, S-08, ordering-cache lifetime (phase 3 §6 item 14)~~ all done, plus the store day in that lifetime, the exchange-replacement exemption and the subsidy switch taking effect at once | ~~S-08 half of 2~~ answered | ~~S–M~~ Done |
| 4c | **Any payment method places a correct packname order (N-03)** — **applied 2026-10-01; PayPal tests pending** | **N-03** | `PlaceOrderAsync` becomes packname-aware and delegates to `PlacePackNameOrderAsync`, so no payment plugin needs modifying | Requirement (Eduardo, 2026-09-16); decision 15 (PayPal waits for it) | not sized |
| 5 | Visibility — department ACL and the allocation gate — **CLOSED 2026-10-01** (Part 1: P-03; Part 2: C-04, C-03) | ~~P-03~~ | ~~C-04~~, ~~C-03~~ (`GenderFiltering` deleted), ~~U-14 (remainder)~~ | ~~C-03 mechanism~~ decided | M–L |
| 6 | ~~Money semantics and one settlement summary~~ — **CLOSED 2026-10-07** (Part 1 2026-10-02, Part 2 2026-10-07; the phase document is consumed — `subsidy-system.md` §7) | — | ~~C-05, C-06, C-07, M-03/K6, K5~~, ~~C-08 + S-15 documented~~, ~~4b carried item 1~~, R3 (found in review); **Part 2:** ~~O-02, O-04 (K8), F6~~ | ~~5, 6 binding; A–E~~ answered 2026-10-01; R3 answered 2026-10-02 | M–L |
| 7 | ~~Storefront features~~ — **CLOSED 2026-10-08** (gap analysis §17b "From phase 7"; the phase document is consumed) | — | ~~M-01 (K2–K5) with UX redesign, M-06 (storefront half), M-02, P-10, P-13, P-14, X-03, N-02, F2, F3, M-04/P-18, S-30, the 0 % vs "—" wording~~ all done; G-01, M-05, P-09, top-up corrections recorded; the shared-address rate → phase 8 | — | M |
| 8 | Settings that do nothing, and missing features — **CLOSED 2026-10-19** (Part 1 2026-10-11, Part 2 2026-10-19; gap analysis §17b "From phase 8 Part 1" / "Part 2"; the phase documents are consumed) | — | ~~T-01~~ (except the X-01 rows), ~~**N-01**~~ with the shared-address line, ~~P-15~~, ~~X-02 (delete)~~, ~~P-11 / P-12~~ closed by decision 12, ~~`TIG.PackName.ShippingAssignments` rows~~ no action; **Part 2:** ~~X-01 (port)~~ done, ~~`DeleteSubsidyVariationAsync`~~ deleted, six `plugins.tig.*` resources no action, the two switched-off cart-line behaviours recorded (no code) | ~~import channel~~ (on the page) | S–L |
| 9 | ~~Performance and caching~~ — **CLOSED 2026-10-20** (`Subsidy_Phase9_Performance.md` consumed; project closure record) | — | ~~H-01 (C# half), H-02, H-04, G-07~~ done; from phase 3: the SQL period functions' NULL branches made dead by `NOT NULL` dates, and their regression cases; from phase 6: the settleable amount memoised per request (R9), and phase 1 carried item 8 (`CreatedOnUtc` types, decision B) | H-01 a/b | M |
| 10 | ~~Documentation consolidation~~ — **CLOSED 2026-10-21** (every reference document reconciled with the code; the ten remaining phase documents and the two consumed specs moved to `_to_delete/`; `Subsidy_PackName_Open_Items.md` written; 4.1 / 4.6 port audit) | — | ~~U-19, Q-01 residue, doc updates~~ done; P-06 and U-15 documentation; the Q-01 residue and the code items earlier phases routed here are listed in the open items, not done (a documentation phase) | — | S |
| 11 | ~~**Packname bulk actions and selector** — U-13, both halves together~~ — **CLOSED 2026-10-21** (gap analysis §17b "From phase 11"; the phase document is consumed) | — | ~~U-13 (grid half): selection + bulk activate / deactivate / delete / move-to-list + export. U-13 (selector half): server-side search~~ done; plus the remembered selector list follows a moved packname (D11) | ~~D1–D11~~ answered 2026-10-21 | ~~M~~ Done |
| 12 | ~~**Admin Excel import of packnames** — X-04~~ — **CLOSED 2026-10-20** | — | ~~The import of phase 8 Part 2 offered in admin (Manage Store › PackNames): every active branch of the store, no manager, same template, checks, batches, result and error report~~ done | ~~whether *Allow to add by Excel* gates it; the creator named in the log and e-mail~~ answered (no; the administrator) | ~~M~~ Done |

Size: S = hours, M = days, L = a week or more (gap analysis §1 scale). Blockers land: 2 in phase 1, 3 in
phase 2a, 1 in phase 3, 1 in phase 4a, 1 in phase 5 — **all eight by the end of phase 5**, and with the
2026-09-12 answers none of them waits on a decision any more. *(2026-09-29: phase 3's one, S-04, turned out
to be closed by phase 2b. Open now: **S-03** (4a), **P-03** (5) and **N-03** (4c), raised after this count.
2026-10-01: S-03 and P-03 closed; **N-03** open until 4c's PayPal tests pass.)*

Phases 1, 2a, 2b, 2c and 3 are independent of each other and can be interleaved; 2c is the one to pull
forward if the admin UI matters most right now. Phases 4 and 5 can run in either order. Phases 6–8 follow.
9 and 10 close.

### Working agreement for every phase (2026-09-12)

- Application code (`Plugins/`, `Libraries/`, `Presentation/`) is **never edited by Claude**: every change is
  delivered as exact before/after blocks (file, anchor text, whole method where several edits land) and
  applied by Eduardo.
- **`Tests/CW.Tests` is the exception** — Claude may add and edit unit tests there directly.
- Documents under `CWPlans/` and `CWDocs/` may be created, edited and deleted freely.

---

## 4. The phases

### Phase 1 — Period arithmetic and ledger integrity (SQL) — **CLOSED 2026-09-16**

> **Closed.** `Subsidy_Phase1_Period_Arithmetic.md` was consumed on closure (`CLAUDE.md` §0.4);
> what it recorded now lives in `subsidy-system.md` §3 (the rule), the gap analysis §17b (the eight
> carried items) and `tasks/lessons.md` #47–#49 (the corrections). The table below is the record of
> what the phase covered, not a work list.
>
> **Shipped:** S-01, S-02, S-09, S-10, S-11 (minus the variation-aware end), S-13, S-20, plus
> **S-22** and **S-23**, both found while applying it. 23 unit tests, 55 SQL regression assertions,
> and a manual walk M0–M8. Re-runnable assets kept under `CWPlans/TestData/`.
>
> **Not shipped, deliberately:** H-01's view rewrite (the iTVF it needs now exists — phase 9), the
> variation-aware topup end (phase 3), the entity/column type disagreement (phase 6), **S-24** (the
> history grid's timestamps — phase 3).
>
> **The one thing to know before touching this again:** a subsidy period is the *store's local
> time*, and the SQL and `SubsidyPeriodHelper` are two halves of one rule. Change one, change the
> other, and keep `SubsidyPeriodHelperTests` green.

**Owns:** `Services/DataBase/CWDatabaseService.cs` (the two period UDFs, the credit views, the history SP,
`subsidy_get_topup_period_end`), `SubsidyService.GetTopupSearchWindow`, `SubsidyOrderingService` smart-TTL
helper, the ledger builders and one migration.

| Item | Why here |
|---|---|
| **S-01** Ordering period opens on 1 Jan | The bug |
| **S-02** Annual consumption never resets | Same function family, same fix shape |
| S-09 Inactive subsidies still pay | One `AND s.Active = 1` in the credit views rewritten here anyway |
| S-10 Soft-deleted orders still consume | One `AND o.Deleted = 0` beside every `OrderStatusId != 40` in the same views and SP |
| S-11 Topup window last-day disagreement; duplicate rows double-count | Same period arithmetic; the fix is the shared occurrence window + a filtered unique index on the topup/variation tables + `SUM` in the views. Per lesson #46 and `Exchanges_Rebuild_Record.md` §6.0 the **unique index is the guarantee**, the `ILocker` only an optimisation |
| S-13 Feb-29 crash in the C# TTL helper | The C# mirror of the same boundary rule; clamp with `DaysInMonth` |
| S-20 Money precision, `CreatedOnUtc` type | Goes into the migration this phase already needs |
| H-01 (SQL half) UDF in `WHERE` blocks parallel plans; three missing indexes | The recommended fix for S-01/S-02 — compute the current occurrence's `[start, end]` once — *is* the inline-TVF rewrite H-01 asks for. Indexes ride in the same migration |
| Q-01 SQL items | `GETDATE()` vs UTC skew; variation history joining `ap.CreatorId` instead of `svarh.CreatorId` |

**Shape of the fix:** one inline function that returns the current occurrence window for a subsidy (or a
variation, honouring `PackNamesOwnStartAndFinishDates`) from `@TODAY`; `is_in_ordering_period`,
`order_is_in_current_period` and `get_topup_period_end` all become `BETWEEN` against it. C# gets the same
rule in one pure helper (`SubsidyPeriod.CurrentOccurrence(...)`) used by `GetTopupSearchWindow` and the
smart TTL, unit-tested in `Tests/CW.Tests`.

**Test plan (one SQL regression script + one admin check):** same-year annual, cross-year annual, first year,
non-annual, null dates, Feb 29 in a non-leap year, last day of a non-annual period at 23:59, an order from
the previous occurrence (must not count), an inactive subsidy (must not pay), a soft-deleted order (must
release). Then the admin credit panel and the storefront bar for one packname before/after. **Decisions:**
none. One open sub-question (S-10): deleting a non-cancelled order will release credit once `o.Deleted = 0`
is added — that is the default; say so in the phase doc.

### Phase 2a — Access hardening, storefront — **CLOSED 2026-09-17**

**Closed** items: S-05, P-01, P-04, P-05, P-07, G-03, U-01, U-02, U-03, U-16 (the 500s and the
state-changing GET), H-05, P-19 (as a refusal), plus thirteen defects found while validating the
phase itself — U-25 to U-37. The surviving rules were folded into the permanent documents and the
working spec deleted per `CLAUDE.md` §0.4:

| Rule | Now lives in |
|---|---|
| Manager scope: one authority, the SP does not check `Manager`, the store comes from the manager, what `LimitAssignedBranches` reaches | `MultiStore_Management_Reference.md` §7b |
| The ownership cache and the six-entity table that proves its invalidation complete | `MultiStore_Management_Reference.md` §7.1 |
| The topup guard's checks | `subsidy-system.md` §8.1 (the spec that held them was consumed, 2026-10-21) |
| The selected packname is an authorisation input; only the packname checkout may place a packname order | `subsidy-system.md` rules 20 and 21 |
| The three doors and what closes each | `PackName_Order_Shipments.md` §9 |
| Settings are enforced server-side, not by hiding controls | `Feature_Toggles_Reference.md` §3.2 |

**What it deliberately did not do** — eight items, in the gap analysis §17b under "From phase 2a".
The two that change other phases: **"part 2" never ran**, so H-03's wider consumer audit, O-01's
admin filter, G-02's validator and U-11's customer-delete consumer move into **2b** (added to its
table below); and **no payment method can complete a packname purchase** until **4c**, which is
therefore now a release gate rather than a nice-to-have — if the store needs PayPal for packnames
before 2b/2c/3, 4c moves ahead of them.

**Two lessons worth carrying into the next phase**, both from how this one went (full text in
`tasks/lessons.md` #57–#60): reviewing a change set means reading the FILES it lands in, not the diff
— three validation passes each found only what the previous pass had introduced, and the serious
defects came out of the fourth, which read whole files; and a guard is proved complete by enumerating
its INPUTS in a table — routes that can place an order, request values, entities feeding a cached
query — not by grepping the code that changed.

### Phase 2b — Access hardening, admin

> **Status — CLOSED 2026-09-23.** Changes 1–146 across parts 1–4 and rounds T–X; the final run was
> **39 of 39 checks passing** on the live dev instance. The phase document
> was consumed on 2026-10-21; its findings live in `MultiStore_Management_Reference.md` §7b–§7d and `subsidy-system.md` §2.6, §11; the test results in the project's phase 2b closure records.
>
> It split into parts on writing — part 1 the security core, part 2 the money defects in the topup
> path, part 3 the sweep, part 4 the Active rule and the history marker — with a deliberate gate
> between each so that 55 changes were never in flight before a test ran.
>
> Two defects were found by **running** it that the static battery of 117 checks missed, both created
> by two individually-correct changes meeting: a validation redisplay that rendered a narrowed picker
> against a rejected value (500), and a join carrying a scalar that duplicated every history row.
> Lessons #70 and #71.
>
> Items that left the phase are filed in gap analysis **§18d**: U-42 (doc comments), U-43 (the admin
> customer→branch save validates nothing), U-44 (`@T()` inside a JS string, 114 sites — **before any
> translation work starts**), and ~130 U-17 literals in exchanges/returns/credit notes.


**Owns:** `Areas/Admin/Controllers/MultiStoreController.cs`, `Areas/Admin/Controllers/SubsidyController.cs`
(three popups), `SubsidyModelFactory` pickers, `AclDepartmentsViewComponent`, the manager-scope SP, and the
admin packname views.

| Item | Why here |
|---|---|
| A-01 Pickers not store-scoped; popup POSTs accept forged ids (K7) | Store scoping on the three POSTs + `storeId` on `SearchProductsAsync` |
| U-04 One-customer-per-packname / same-store rules only in JS | Server-side checks in the three save paths; extract the duplicated "0 → null, Manager = false" block into one place while there (Q-01) |
| U-05 Manager-assignment POSTs accept any id | Re-validate against the grid query |
| U-08 Saves trust posted `StoreId` | Entity-sourced store in the same save paths |
| U-09 Address popups edit any `Address` id | Same shape (packname, cost centre, branch) |
| U-10 Default-settings POST reports success on invalid model | Same controller |
| U-12 Mapping changes not activity-logged | Same POSTs |
| U-14 (scoping half) Department ACL picker lists every store | Filter by the entity's store mappings; the "make it work" half is phase 5 |
| U-15 Feature-toggle / permission gaps | Same actions |
| P-06 Manager-scope SP broader than 4.6 | **Decision 12: today's rule is intended.** Only change: `b.Deleted = 0` on Path 2. The rule itself is `MultiStore_Management_Reference.md` §7b.2 (phase 10) |
| P-16 `AllManagers` / `ParentManagers` exclusivity | Same `SavePackNameList` |
| P-17 Multi-store scoping holes in manager/department queries | Same service methods |
| H-03 (remaining half) The wider cache-consumer audit | **Re-homed from 2a, 2026-09-17.** 2a completed the ownership set's six inputs and documented them as a table (`MultiStore_Management_Reference.md` §7.1); every other cached query in the plugin is still unaudited. Use the same method: enumerate the query's inputs, then find the consumer for each |
| O-01 (block half) nop's `EditOrderItem` / `DeleteOrderItem` / `AddProductToOrderDetails` reachable on packname orders | **Re-homed from 2a.** Decision 14: block. Same filter pattern as the storefront one, which is now documented in `PackName_Order_Shipments.md` §9 — note its lesson: intercept the controller and name the exceptions, never enumerate actions |
| G-02 `AddAddress` NRE, no ownership, no validator | **Re-homed from 2a.** Same guard shape as the four maintenance actions 2a fixed (`MultiStore_Management_Reference.md` §7b) |
| U-11 Customer delete leaves the packname pointing at it | **Re-homed from 2a.** An `EntityDeletedEvent<Customer>` consumer |

2b is **controllers and services only** — the admin views it would otherwise touch (U-07, U-17, U-18) are
left to 2c, so the same markup is not fixed twice.

**Test plan:** the same two-store fixture as 2a, driven from admin: every popup and save with a forged id
from the other store → refused; activity log rows for mapping changes. The four rules in
`MultiStore_Management_Reference.md` §7b apply unchanged on the admin side — in particular **U-08 is the
admin twin of 2a's U-25**: a save that trusts a posted `StoreId` is the same defect, and the fix is the
same one (derive the store from the entity, overwrite the posted value before anything reads it).

### Phase 2c — Manage Store rework: branches → lists → packnames

> **Status — CLOSED 2026-09-28.** The record of what was built, the rules it established, the three
> infrastructure traps it uncovered and the decisions taken is
> consumed on 2026-10-21 into `MultiStore_Management_Reference.md` §8c–§8d (and `CLAUDE.md` §6.8, §7, §9.11–§9.15). Changes 148–282.
>
> Done: the "save never navigates" rule across nine detail forms; the branch page's three collections
> rebuilt as one consistent server-rendered row presentation; U-07, U-17, U-18, X-01 (hide half) and
> the Q-01 admin-view items. Along the way: two left-join NULL traps, an unguarded `.Value`, and a
> managers grid that hid four columns because nothing populated the settings model it reads.
>
> Moved out by decision, not left undone: **U-13 (grid half)** to its own phase (Eduardo, 2026-09-24),
> and the **P-11 follow-up** left in place (Eduardo, 2026-09-28) — both are larger than the phase.
>
> The plan text below is kept as written **before** the work, as the record of what was intended.

**Why it is a phase of its own.** The Branches tab of *Manage Store* is where an admin sets up a store's
structure, and it is hard to read: a branch's packname lists render as flat rows in one card, and clicking a
row injects a second card *inside* the row with a DataTables grid — so a list and its packnames look like
two unrelated cards, and with several lists it is not clear how many lists there are or which packnames
belong to which. The request (2026-09-12) is to make the hierarchy obvious and to review the JavaScript
behind it. That is a redesign (Tier 2), and the small view-level items from the gap analysis are folded into
it rather than patched first and redesigned after.

**Owns:** `Areas/Admin/Views/MultiStore/_Branches.*.cshtml` (tree, branch detail, lists, inline packnames,
list detail, managers, cost centres, addresses), `_PackNames.cshtml` and its detail partial,
`Content/Admin/MultiStore/admin.store.navigation.js` (~35 KB) and `multistore.css`, the `MultiStoreController`
actions those views call (`PackNamesInline`, `PackNamesList`, `SetDefaultPackNameList`, …) and
`Areas/Admin/Factories/PackNameModelFactory` / `MultiStoreModelFactory`.

**What the first read (2026-09-12, `_Branches.PackNameLists.cshtml`, `_Branches.PackNamesInline.cshtml`,
`_Branches.BranchDetail.cshtml`) already shows — inputs to the design, not the design:**

- *Structure.* One "lists" card whose rows each expand into a nested card + grid. Nothing visually states
  "this is list 2 of 4, it contains 37 packnames" beyond a count chip; the default-list marker is a filled
  circle with no label; the branch's own detail form sits above in a third collapsible card of the same
  style, so three levels of hierarchy share one visual weight.
- *Scripts in partials.* Every partial carries its own inline `<script>` block (this cited CLAUDE.md
  §9.8 — **the citation is wrong**: §9.8 is the Admin UI Libraries Reference and CLAUDE.md has no such
  rule. Checked 2026-09-24. The concern stands on its own; the section still does this throughout); the inline packnames partial redefines global functions (`renderGender`, `editBranchPackName`,
  `removeBranchPackName`) on every injection, so two open lists would fight; several assignments leak
  globals (`postData`, `$listRow`, `$pnCount` without `var`/`const`); action URLs are hard-coded strings;
  errors surface as `alert()`; collapse is an inline `onclick` IIFE per card header; the DataTables grid is
  created with `Length = 15` hard-coded and no search (U-18).
- *Text.* Hard-coded English in the views and in the JS (`"Set default"`, `"Edit"`, `"Loading…"`,
  `"packNames"`, `"Failed to load packNames."`) — U-17.
- *Worth verifying on a running page:* `_Branches.BranchDetail.cshtml:156` reads
  `.text('@("Plugins.CW.Core.Admin.Saving").Text')` — a string literal with `.Text`, which looks like a
  missing `T(` and would be a Razor compile error on first request. Either the page has never rendered that
  line or something else is going on; check before touching it (lessons #20, #27).

| Item | Why here |
|---|---|
| **Redesign** of the branches → lists → packnames presentation | The request: make lists visibly distinct containers with their packnames inside; make counts and the default list legible; one visual weight per hierarchy level |
| **JS review** of `admin.store.navigation.js` + the partial scripts | Move partial scripts into the feature script; one delegated handler set instead of per-injection globals; `Url.Action`-sourced endpoints passed as data attributes; a notification instead of `alert()`; no leaked globals |
| U-07 Branch filter on the PackNames tab ignored | Same screens |
| U-13 (grid half) Bulk activate / deactivate / delete / move-to-list + export on packname grids | Same grids; the selector's server-side search stays in phase 9 |
| U-17 Malformed markup, hard-coded English | Same views |
| U-18 Customer-edit PackName card cannot deep-link; inline grid has no search / hard-coded length | Same views; the `#packnames/pn:<id>` deep-link already exists on the MultiStore page |
| X-01 (hide half) Import button, modal, selector icon, both settings | The import button lives in `_PackNames.cshtml`; remove it here, port in phase 8 |
| Q-01 admin-view items | `PackNameModel` public-only fields never set; `PackNameListModel` unused collections; `PreparePackNameListModelAsync(model, entity)` with no callers |

**How it runs:** (1) a short design note with a mock-up of the branch page (lists as distinct containers,
packnames inside, counts and default state as text) agreed **before** any code; (2) the JS restructuring as
its own step with a behaviour checklist (select list, add/edit/delete list, set default, add/edit/delete
packname, deep-link restore, back from focus mode) run before and after so nothing regresses; (3) the
gap-analysis riders. **Decisions:** none from the manager; the design note is agreed with Eduardo.

### Phase 2d — MultiStore JS consolidation — **CLOSED 2026-09-29**

> **Status — CLOSED 2026-09-29.** Record consumed into `MultiStore_Management_Reference.md` §8d (2026-10-21).
> Changes 283–299.

This was the half of **phase 2c's second deliverable that was not done**: move partial scripts out of
the markup, one delegated handler set instead of per-injection globals, `Url.Action`-sourced endpoints
passed as data attributes, nop's error path instead of `alert()`, no leaked globals. Splitting it out
was a scheduling decision (2026-09-28) — a structural JS change across every screen in the section,
immediately after a large presentation change to those same screens, is a regression surface worth
isolating.

**Delivered:** 30 inline `<script>` blocks reduced to 12 (six partials now have none); ~45 globals
retired; 29 `alert()` calls moved to `display_nop_error`; ~15 hard-coded view URLs moved to
`Url.Action` on config divs; **U-44 closed for the MultiStore views** (`@T()` inside a JS string literal, the
Razor HTML-encoding trap — the Subsidy views followed in phase 3; the exchanges / returns views remain); nine inline `onclick=` attributes removed, three of them stored-name injection
vectors; the `class="class="` markup bug fixed in five partials.

**Bugs found while doing it:** the branch tree's three handlers were bound to `document` with no
namespace or teardown and accumulated on every tree reload; five leaked `postData` globals; an
Add-manager button that could never be hidden because `.d-flex` outranks `.d-none` in this build.

**Carried forward, by decision:** the module's own ~16 hard-coded action URLs (needs a URL map passed
through `Admin.StoreNav.init`); `Settings.cshtml`, the plugin-level defaults page, which has its own
copy of the visibility engine; the refresh buttons on pages that do not load the module.

**Also done here (2026-09-29):** every planning-document reference, phase number, gap-analysis code,
change number and dated note was stripped from the source comments — 185 across 61 files. The
technical content stayed; only the pointers went. The plan documents are now the only record.

### Phase 3 — Admin subsidy screens — **CLOSED 2026-09-29**

> **Status — CLOSED 2026-09-29.** Record: project `claude/Subsidy_Phase3_Closure_Record.md`; what survives is in `subsidy-system.md` §3, §11, §12.1 (changes
> 4.1–4.35; 24 of 24 checks passed, including the SQL regression). Carried items are in gap analysis §17b,
> "From phase 3".

**Delivered:** subsidy dates `NOT NULL` (decision B); the admin screens read one assignment-driven query
(decision C) — out-of-period seasons listed, deleted variations never shown, effective credit and dates per
the store's variation settings; Total / Used / Remaining in money and units and one percentage rule on the
panel and both grids; the top-up window follows a variation's end, and admin and storefront top-ups
outside it are refused with a message; the variation validator (end > start, both dates or neither, credit
bounds); the history SP's offset, creator name and dates-only arm; the whole Subsidy admin section in the
2d JavaScript shape, and the packname panel self-contained for the customer page; the product-group colour
on its preview; no subsidies card without the MultiStore permission (decision A); a deleted store's packname
lists, packnames, branches, departments, cost centres, subsidies and product groups soft-deleted (decided
after the store-deletion test).

**Found while doing it:** the build broke on `SubsidyOrderingService`, a consumer of the dates the change
list missed; three whole-file views arrived with a doubled byte-order mark and would not compile; the
regression script's planned "part G" had never been written (dropped — covered end to end).

**Decisions taken at closure (2026-09-29):** pre-season subsidies are not listed on the storefront (unless
`OrderOutsidePeriod`, as `subsidy-catalog-spec.md` already says); store deletion soft-deletes, and credit
notes / return cases / exchange rules are left as they are.

The original plan follows, unchanged.

**Owns:** `Areas/Admin/Views/Subsidy/*`, `Areas/Admin/Factories/SubsidyModelFactory.cs`,
`Areas/Admin/Validators/Subsidies/*`, `SubsidyReportingService` details queries, the `SubsidyController`
topup / variation / product-group actions.

| Item | Why here |
|---|---|
| **S-04** Variation popup binds `CompanyId`; `StoreId` not posted | The bug; derive the store from the assignment |
| S-12 Details read only in-period rows; NRE for out-of-period; deleted variations shown | The GET behind the same popup (`SubsidyModelFactory:709`); drive from `CORE_SUB_SubsidyPackName` with LEFT joins |
| S-19 Variation validator has no end > start | Same popup |
| S-18 Variation values shown regardless of store settings | Same grid |
| S-14 Topups accept ≤ 0; product-group inline edit accepts negatives | Same screens; storefront rules on the admin models. **Decision 3: reject** — reducing a packname's credit is done through a variation, so the variation popup (S-04) is the feature that replaces the negative topup |
| A-02 + **A-06** + M-06 (admin half) "Used %" always 0 % in the grids; the per-packname panel shows allocation, topup and a percentage but never **used** or **remaining**; a quantity-only subsidy shows nothing | Same factory method, same panel, same grid columns. A-06 is the cheap half — `totalCredit` and `totalCreditUsed` are already computed at `SubsidyModelFactory.cs:192-193` and discarded, so it is model properties and view rows with no new query. Then the percentage falls back to units when credit allocation is zero, and a units column sits beside the money. Note the panel computes a real percentage while the two grids render a hard-coded `0%`, so today one screen says 37 % and another says 0 % for the same packname (raised 2026-09-22) |
| A-03 Subsidies card blank without MultiStore permission | Decide the permission (4.6 used *Manage Customers*) or hide the card |
| A-04 Markup / JS defects | Same views |
| **A-07** Product-group detail never shows the chosen colour on the icon | Same view, same script. The three grids already tint `pg-icon` with `row.Colour`; the detail form's preview and picker do not, so the screen where the colour is chosen is the only one that does not show it. Raised 2026-09-29 |
| A-05 Controller deviations (log text, `SetPopupGridPageSize`, dead `SubsidyHistoryModel`, NRE on unknown subsidy) | Same controller and factory |
| ~~**S-23** Subsidy History shows the topup's running TOTAL on every row~~ — **fixed 2026-09-15** | `stop.Credit` → `stoph.Credit`. Taken out of phase order because the grid was misreporting money; verified 25 / 25. Nothing left for this phase |
| **S-24** Subsidy History *Created On* late by the web server's UTC offset | The SP returns `CreatedOn` as `datetimeoffset` (the UNION widens every arm), it materialises as server-local, and `SubsidyModelFactory:1021` then labels it `DateTimeKind.Utc`. Normalise per arm: `CAST(<col> AT TIME ZONE 'UTC' AS datetime2)` |
| Change 16 (phase 1) Variation history joins `ap.CreatorId` instead of `svarh.CreatorId` | Same SP, same grid — the three above are one whole-constant replacement and one fixture |
| S-16 Store deletion fails at the DB | Small consumer; found while in the subsidy services |
| Q-01 subsidy items | `SubsidyHistoryModel` + maps, `HistoryType` 8/9/11 resources, unused cache prefixes, `Async` suffixes |

**Test plan (one admin walk):** create a subsidy whose period opens next month, assign a packname, open the
variation popup (renders, saves, store derived), top it up on the last day of its period (phase 1's window),
try 0 and −10 (refused), edit a product group inline to −1 (refused), a quantity-only subsidy shows units
and a sensible %, deactivate a product group and confirm the catalogue changes at once (H-03 from 2a).
Then the history grid, which S-24 and change 16 share: every row's *Created On* reads in the store's
configured zone, matching the wall clock it was done at once that offset is applied, with order-derived
and subsidy-derived rows agreeing; and a varied subsidy names who varied it, not who assigned it. Run it
on a web server whose zone is **neither UTC nor the store's**, or S-24 cannot fail. Re-check S-23's two
topup rows in the same pass — it was fixed on 2026-09-15 and this phase rewrites the constant it lives in,
so the regression risk is real.

### Phase 4a — One cart, one gate

The gap analysis §18 step 2, plus P-01's belt from 2a already in place. **Owns:**
`ShoppingCartOverrideService` (`GetShoppingCartAsync`, `AddToCartAsync`, `ShoppingCartItemIsEqualAsync`),
`ProductOverrideService`, `PackNameContext`, `PackNameInfoViewComponent`, `PackNameModelFactory`
(public), `PackNameSelector/Default.cshtml`, `FlyoutShoppingCart/Default.cshtml`,
`OrderTotalCalculationOverrideService` shipping paths, `PackNameCheckoutController` AJAX endpoints,
`EventConsumer` packname-delete handling, and the browser side of the same disagreement:
`Content/SubsidyCatalog/subsidy-catalog.js`, `PackNameInfo/Default.cshtml`, `_OrderSummary.cshtml`.

| Item | Why here |
|---|---|
| C-01 Two definitions of "the cart" | Override `GetShoppingCartAsync` (4.6) so header, flyout, selector, checkout, payment-method AJAX and placement all read one filtered set. **Do this first** — S-03, C-02 and P-08 all depend on which cart is "the cart" |
| **S-03** "Cannot order" enforced nowhere | Re-instate the three 4.6 points: catalogue empty, add-to-cart refused with a readable warning, selector disables + info strip shows the notice. Delete the dead `PackNameService` stubs and `GetBlockOrderingPackNameIdsAsync` |
| G-13 Empty lists and empty departments in the selector's list dropdown | **Raised 2026-09-17.** Same dropdown S-03 marks up, and emptiness must be asked of the same cached readers the roster uses, so it is one selector pass. **Decided 2026-09-17: hide them all.** Whole method ready in the gap analysis. Accepted consequence: the **+** button adds into the *selected* list, so an empty list is no longer an add target and a new list gets its first packname from admin |
| C-02 / K10 `AddToCartAsync(packNameId)` merges into the context packname's line; success with no add | The packname overload owns the insert; equality takes the target packname explicitly; never find the row by timestamp; real warnings |
| P-08 Context not revalidated; cart lines survive delete | Context resolves only `!Deleted && Active` packname + list in the current store; packname delete cascades to `PackNameCartItem` + `ShoppingCartItem` + assignments; confirm the FK cascade the gap analysis could not (P-02 note) |
| G-04 Free shipping / shipping discounts on the whole cart | Same multi-packname checkout fixture; pass `packNameItems`; fix the double `taxRate` assignment |
| G-05 Shipping "cannot be calculated" reported as $0 | Same method; return `null` per nop's contract |
| G-06 Silent AJAX failures; unlogged `ConfirmOrder` exceptions | Same controller, same fixture |
| G-08 Re-order / cart merge into packnames no longer managed | Same `ReOrderAsync` / `MigrateShoppingCartAsync` paths C-02 rewrites |
| U-06 Info strip lost "no address / no shipping method" warnings | Same view component that S-03 gives the cannot-order notice |
| **G-09** Flyout *remove* silently does nothing for a cannot-order packname | Same handler C-01 rewrites; resolve the item from the reader the flyout drew it from, and answer `success = false` with a message when the id resolves to nothing |
| **G-10** Only add-to-cart refreshes the subsidy bar and group headers | One document-level cart-changed event, published by every mutation and subscribed to by the catalogue bar, the group headers and the `PackNameInfo` flyout — instead of three consumers each sniffing the add-to-cart URL. The publisher goes into the same handler G-09 corrects |
| K1 "Go To Cart" in the flyout | Same flyout view C-01 changes |
| C-10 Required-product auto-add; `MaximumShoppingCartItems` per packname | Same overload; decide the semantics in the phase doc (4.6: exempt required products, cap per packname) |
| O-03 Image width bound to `DefaultImageQuality`; `@inject` factory in `Details.cshtml` | Storefront order view, trivial, while the storefront views are open |

**Decision 1 — answered: the 4.6 strategy.** Catalogue hidden, add-to-cart refused with the notice,
blocked packnames marked in the selector and info strip; the notice text is ported from the 4.6 resources
(`Jim2.CurrentPackNameInfo.CannotOrder`, `PackName.BlockOrderingReason`). Lines already in the cart when a
period closes stay hidden by the view, and C-01 makes that consistent across every reader.

**Part 1 closed 2026-09-30** (record: project `claude/Subsidy_Phase4a_Part1_Closure_Record.md`; the design is `subsidy-system.md` §3.2 and §8.6): C-01, S-03, C-02 / K10, C-10, P-08,
G-06, G-08, G-09 and S-31. Decisions taken in the phase: the notice text (no 4.6 source in the repository);
a deactivated employee packname is not kept as a manager's context; re-order **skips** packnames no longer
ordered for; the line limit counts **per packname**, at add-to-cart and at checkout; an **inactive**
packname's cart lines are hidden like a closed period's; subsidies switched off have no effect anywhere,
SQL included (`Feature_Toggles_Reference.md` §1.5). **Part 2** (G-13, U-06, G-10, K1, G-04, G-05, O-03) is
next; it also owns showing a blocked current packname in the selector.

**Part 2 closed 2026-09-30:** G-13, U-06, G-10, K1, G-04, G-05, O-03 and the selector item; 17 of 17 checks.
Decisions taken: U-06 brings back only the no-address warning (shipping options depend on the cart, and the
checkout already warns); one cart-changed event in `cw-cart-events.js`, loaded while packnames are on;
shipping discounts applied per destination and recorded once; the packname order details travel on a
derived `CWOrderDetailsModel` (nop's `CustomProperties` holds strings only). **Phase 4a is closed.**

**Test plan (one fixture, reused by 4b):** a manager with three packnames — one in period, one whose period
closed with `OrderOutsidePeriod = false`, one deleted while selected — across two branches; a multi-packname
order to re-order. Checks: header count = selector badge = checkout lines = placed order lines; closed
packname sees an empty catalogue, add-to-cart shows the notice, selector shows it disabled; re-order lands
each line in its own packname; deleting a packname clears its lines and its selection; free-shipping
threshold evaluated per packname; a non-computable rate shows "calculated at checkout", not $0.
Two browser checks for G-09 / G-10, both run **without reloading the page**: standing on
`/SubsidyCatalog`, remove a line from the flyout — the row goes, and the bar, the group header and the
`PackNameInfo` flyout panel all move by the line's credit (the panel checked by *opening it after* the
removal, since a closed panel caches its HTML); and with the packname's period closed, remove a line —
it must actually leave the cart, or answer with a readable message, never `success = true` with the row
still there.

### Phase 4b — Allocation rules at add-to-cart — **CLOSED 2026-09-30**

**Closed 2026-09-30** (record: project `claude/Subsidy_Phase4b_Closure_Record.md`; the rules are `subsidy-system.md` §5.2; changes 4b.1–4b.9): S-06, S-07, S-08 and the
ordering-cache lifetime, plus the store's day in that lifetime, the wishlist excluded from the cap, an exchange
replacement exempt from it, a quantity change refusing only a raise, and a setting write clearing the cached
subsidy ordering results. Decisions: managers follow `IncludeManagers`; the cart counts through the allocation
(no money budget); credit available lifts the cap (4.6); only the subsidies containing the product decide.
Round 1 failed T10 (stale cache after switching subsidies) → 4b.9; round 2 passed 15 of 15. Carried items:
gap analysis §17b "From phase 4b".

Same fixture as 4a, one more subsidy with `RestrictToAllocatedQty` and a manager assigned. **Owns:**
`SubsidyCreditService.GetAllocatedQuantityRestrictionAsync`, `SubsidyOrderingService.GetOnlyAllocatedProductIdsAsync`,
`SubsidyOrderingService.GetSubsidyNextOrderingStatusChange`, the quantity-cap branch of `AddToCartAsync` /
`UpdateShoppingCartItemAsync`.

| Item | Why here |
|---|---|
| S-06 `OnlyAllocatedProducts` ignores `IncludeManagers` | **Decision 2 (answered):** `IncludeManagers` on = both `OnlyAllocatedProducts` and `RestrictToAllocatedQty` apply to managers too; off = managers unrestricted. Read the manager-aware view flag and apply the same test to the quantity cap |
| S-07 Cap checked per line, not per product group across the whole cart | In-cart figure from the same breakdown the subsidy bar uses |
| S-08 Cap exemptions differ from 4.6 both ways | Port the three conditions per subsidy / group containing the product. **Still open:** whether credit lifts the cap — default 4.6 (yes) |
| Ordering-state cache lifetime ignores `PackNamesOwnStartAndFinishDates` (found in phase 3's review; assigned here 2026-09-29) | `GetSubsidyNextOrderingStatusChange` takes a variation's dates whatever the store setting; the view it caches (`vw_CORE_SUB_SubsidyVariationInOrderingPeriod`) uses them only when the setting is on. With the setting off and a variation's dates later than the subsidy's, the storefront keeps the old ordering state until the cache expires. Fix: pass the store setting in and apply the same rule as `SubsidyPeriodHelper.EffectiveDurationEnd` (and its start counterpart). Same service as S-06's method, same fixture |

Separated from 4a only because the method it changes is the one 4a rewrites — so 4b is applied on top of
4a's `AddToCartAsync`, not before. The rule is pure enough to unit-test in
`Tests/CW.Tests` (allocation, cart contents, product → expected accept/refuse); the gap analysis's two
worked examples become the first two cases. Fix `subsidy-system.md` §5.2 and §14 rule 8 in the same phase.

### Phase 4c — Any payment method places a correct packname order (N-03) — **APPLIED 2026-10-01, PayPal tests pending**

**Applied 2026-10-01** (record: project `claude/Subsidy_Phase4c_Record.md`; the design and the pending PayPal tests are `PackName_Order_Shipments.md` §9; changes 4c.1–4c.7): `PlaceOrderAsync` places a
packname cart as a packname order (refusing a mixed cart, an unorderable one, or a stated amount that differs);
the packname checkout never places an order with a method listed in `MultiStoreSettings.OwnPlacementPaymentMethods`
(default PayPal Commerce) — it shows a note, then hands over after validating, with the billing address and the
first destination set on the customer. 8 of 11 checks passed; T4 could not be set up; **T5, T6, T8 (PayPal) wait
for a sandbox account** — Eduardo cannot sign in to PayPal from abroad (2026-10-01). Until they pass, PayPal Commerce
stays disconnected on packname stores.


**Owns:** `OrderProcessingOverrideService.PlaceOrderAsync` / `PlacePackNameOrderAsync`,
`PackNameCheckoutController`'s payment step, and the payment-info component rendering.

| Item | Why here |
|---|---|
| **N-03** The packname checkout supports no payment method that places its own order; selecting one places it UNPAID | **Requirement, Eduardo 2026-09-16:** the packname checkout must work with any payment method as nop's does. `PlaceOrderAsync` becomes packname-aware and delegates to `PlacePackNameOrderAsync` when the customer has packname cart items, so no plugin needs modifying and every future one works too |
| **P-19** (remainder) PayPal Commerce's own placement | Closed properly here rather than refused. 2a's refusal is the interim guard |

**Why after 4a and not in 2a.** "Resolve the packname cart" inside `PlaceOrderAsync` *is* C-01's
reader, and this method handles money — the one place not to guess which reader is authoritative.
Phase 2a refuses the placement in the meantime, so neither the invisible-order nor the unpaid-order
outcome can happen while this waits.

**Test plan:** PayPal Commerce configured, buttons on the payment step. A single-packname cart and a
multi-packname cart, each paid through PayPal: the order carries `PackNameOrderItem` rows per
packname, an `OrderShipping` destination each, subsidy consumed as the bar predicted, and the
payment captured — asserted against `[Order].PaymentStatusId` and the PayPal transaction, not the
confirmation page. Then the same two carts through a `Standard` method that *does* use the pipeline
(`Payments.Manual`), to prove the delegation did not break the path that already worked. And the
negative: an order placed with no packname cart still settles as a plain nop order.

### Phase 5 — Visibility: department ACL and the allocation gate — **CLOSED 2026-10-01**

> **Split in two (Eduardo, 2026-10-01).** **Part 1 — P-03 — closed 2026-10-01**
> (record: project `claude/Subsidy_Phase5_Part1_Closure_Record.md`; the rule is `CLAUDE.md` §15.2). Decision 11 was replaced by Eduardo's: what is *seen* follows the
> packname (its account's roles, or Registered + default packname roles, plus its department's role); what is
> *paid and allowed* follows the customer signed in. **Part 2** — C-04 (the allocation gate everywhere, including
> product pages and `PackNameCatalogScope`) and C-03, now **delete the `GenderFiltering` setting** instead of porting it.
> **Part 2 closed 2026-10-01** (record: project `claude/Subsidy_Phase5_Part2_Closure_Record.md`; `subsidy-system.md` §5.1): one gate (`IPackNameCatalogGate`) behind
> every ACL check and ACL query filter; exchanges — Exact / Same Style free, Different Style allocated only, a closed
> period never blocks them; a closed period hides the catalogue and its product pages; empty categories hidden
> everywhere. Found and fixed while testing: grouped-product pages (R1, R3). Carried forward: gap analysis §17b.

**Owns:** `CustomerOverrideService` (role-id overrides), `AclOverrideService`, `SubsidyCatalogModelFactory`,
`SubsidyCatalogCartService`, category / catalogue model factory hooks, `AclDepartmentsViewComponent`.

| Item | Why here |
|---|---|
| **P-03** Department ACL never enforced | **Decision 11 (answered): the department role replaces the branch role**, as 4.6. Override `GetCustomerRoleIdsAsync` / `GetCustomerRolesAsync` for the context packname outside admin — swap branch role for `Department.CustomerRoleId`, keep the rest; keep admin/webhook guards; verify nop 4.9's role-id cache key varies by packname |
| C-04 Catalogue restriction is list-only (K9) | **Decision 8 (answered): unallocated = unsubsidised, except when `OnlyAllocatedProducts` is on = forbidden.** So: override `AclService.AuthorizeAsync<Product>` on the same cached allocation list *only when the packname's subsidies restrict*, and hide categories / manufacturers with no visible product. Same ACL surface as P-03, same "what can this packname see" fixture |
| C-03 Gender filter not ported | Same catalogue factories; one mechanism applied to subsidy catalogue, category pages and bulk ordering under `GenderFiltering`. **Open:** the attribute / option naming convention in 4.9 catalogues |
| U-14 (remainder) Department picker meaningful only once P-03 works | Verify the 2b scoping now that the role fires |

Test plan: one store, department *Sales* with a role, a category restricted to it;
a Sales packname, a non-Sales packname, a manager ordering for each; a female packname on a subsidy
catalogue with men's garments. Checks are on the rendered menu, the product page, related / cross-sell
blocks and the add-to-cart result — not on the list page alone (K9's point).

### Phase 6 — Money semantics and one settlement summary — **CLOSED 2026-10-07**

> **Part 2 closed 2026-10-07** (record: project `claude/Subsidy_Phase6_Part2_Closure_Record.md`): O-02 — the For line on the invoice and the
> packing slip, which now also prints attribute lines (G1); the invoice's two discounts on separate rows (G2); O-04 /
> K8 — E (4.1's, on the exchange order) and R (new, on the order with an authorised return) on *Sales → Orders* and the
> customer's *Orders* tab, built as an endpoint + widget script (G3); F6 — one label set on every surface, negative
> deductions for customers and brackets in admin (decisions N1, N2). Changes 51–85, 17 of 17 tests. Carried forward:
> gap analysis §17b "From phase 6 Part 2" (the closed-packname exchange → next exchanges round; badge wrap → `Subsidy_PackName_Open_Items.md`).

> **Part 1 closed 2026-10-02** (record: project `claude/Subsidy_Phase6_Part1_Closure_Record.md`; the design is `subsidy-system.md` §7): decisions 5 and 6 binding as the 4.1
> answer; subsidy then credit notes settle the goods and shipping, customer discounts / fee / gift cards / reward points
> apply to the balance due, the subtotal discount limited to it (R3, option A); one settlement row per order
> (`CORE_PN_OrderSettlement`) read by every reader; the quantity cap re-checked before payment. C-05, C-06, C-07,
> M-03 / K6, K5, C-08, S-15 and 4b's carried item 1 closed. 28 of 29 tests; T20 (PayPal hand-over) waits with 4c's
> PayPal tests. Carried forward: gap analysis §17b. **Part 2:** O-02 ("For" line on the invoice and packing slip;
> decision D: PDF only) and O-04 (E / R badges; decision C: admin order list and customer orders tab, both badges
> when both apply) — both decided, §8 numbering continues from 50.
>
> **Opened 2026-10-01.** Analysis against the code, the reader table, the open decisions (5 and 6 to confirm, plus
> A–E) and the design are now `subsidy-system.md` §7 (the phase document is consumed); its §8 (the changes) was written once
> §1 is answered. Every premise in the table below still holds; what the analysis added: two unlisted readers of
> "gross" (the admin packname-details factory and the profit report), two copied nop bodies missing from
> `CLAUDE.md` §10.1 (added), and that decision 7's release-on-cancel is already computed by the views (no code).
> Also in scope: 4b carried item 1 (the cap at placement). Phase 1 carried item 8 (`CreatedOnUtc` types) is proposed
> for phase 9 instead.

**Owns:** `OrderTotalCalculationOverrideService` (settlement ordering), `OrderProcessingOverrideService`
(what is written to `Order`), `PackNameOrderModelFactory`, `OrderSubsidyCreditViewComponent`,
`MessageTokenProviderOverride.WriteTotalsAsync`, `PdfOverrideService` / `CWInvoiceDocument`, the credit
views (pending-payment exclusion), admin order list factories, `CWCorePlugin` install seeds.

| Item | Why here |
|---|---|
| C-05 Discount / gift cards / reward points before subsidy | **Decision 5 (pending manager, default 4.6):** settle subsidy before discount / gift card / reward points — pass `subtotal + shipping + fee + tax` to the pipeline and apply the rest to the balance due |
| C-06 Gross columns with a net total | **Decision 6 (pending manager, default):** keep gross columns, persist `SettleableAmount`, document the convention for reporting and Odoo |
| C-07 Every post-placement reader derives "gross" differently | Build **one** order-side settlement summary (gross = OrderTotal + credit notes + subsidy − fees, or persist `SettleableAmount`) and make every reader below use it |
| M-03 / K6 Subsidy and credit-note lines in e-mails, PDF, admin | Readers of the summary |
| K5 Per-line subsidy on the customer's order page | Same summary, per line from `CORE_SUB_SubsidyOrderItem` |
| ~~O-02 Per-line packname on invoice, packing slip, `%Order.Product(s)%`~~ | **Closed by Part 2** — invoice and packing slip (decision D: not the e-mail) |
| C-08 Pending-payment orders hold credit | **Closed by decision 7: no limit.** No code; one paragraph in `subsidy-system.md` saying an unpaid order holds its credit until cancelled |
| S-15 Shipping-subsidy default | **Closed by decision 4: off by default is correct.** One line in `subsidy-system.md` noting the deliberate inversion from 4.6 |
| ~~O-04 E / R badges on admin order lists (K8)~~ | **Closed by Part 2** — both lists, both badges (decisions C1, C2) |

Test plan: one order with subsidy + credit note + exchange fee + order discount + gift card, then the same
figure read on the checkout, the customer order page, the admin order page, the order e-mail, the invoice
PDF and `vw_CORE_SUB_OrderSubsidyTotal` — all six must agree, and one of them must be computed by hand
(lesson #13). Add a 3-decimal unit price. Unit-test the summary in `Tests/CW.Tests`.

### Phase 7 — Storefront features — **CLOSED 2026-10-08**

> **Closed 2026-10-08** (record: gap analysis §17b "From phase 7", `subsidy-system.md` §8.3–§8.5,
> `MultiStore_Management_Reference.md` §8e; the phase document is consumed): *My Subsidies* at `customer/subsidies` for
> the selected packname (M-01, K2–K5; one card per assignment — status, remaining with a usage bar, groups, history
> folded; mock-up approved, decision A) and M-06's storefront half (units shown, no `+ $0.00` on a units-only group);
> subsidy checkboxes on the manager's packname form (M-02); default roles, a random password and the welcome e-mail
> with nop's set-password link at registration (P-13, P-14, X-03, decisions D–F; five dead template names, token
> groups and attribute names deleted); the print-name pre-fill (P-10); the packname order-completed page reached from
> every path through the checkout filter (N-02, decision C); cap warnings on the checkout page (F3) and the
> pre-selected payment method's fee on first load (F2, change 62); `SubsidySendUserAfterLogin` and one landing resolver
> for sign-in, selection and impersonation, as 4.6 (M-04 / P-18, decision H); the top-up header grid (S-30); "—" for an
> out-of-period assignment (decision J). Recorded only: M-05, P-09 (I), G-01 (14 / G), top-up corrections closed by
> decision 3 (K); the shared-address shipping rate re-homed to phase 8 with N-01 (L). Also the Store Credit page
> rebuilt in the same style with its My Account item highlighted. 72 changes (63–72 after closure: a DataTables
> re-init on the list detail, history rows linked only to the viewer's own orders, links that look like links, the
> product-group colour field, no hover underline and the branch title as a handle), 15 unit tests, 19 of 19 checks.
> Carried forward: gap analysis §17b "From phase 7" (two `alert()`s and a catalogue-header `$0.00` → `Subsidy_PackName_Open_Items.md`; a dead
> exchanges stylesheet → next exchanges round; a manager opening his employees' orders — unowned feature; one
> unreproduced "Loading…" on the packname subsidies card).

**Owns:** new `CustomerSubsidies` route/controller/factory/view, `PackNameController.AddPackName /
EditPackName / RegisterCustomer`, `PackNameRegisterModel`, `EventConsumer` `ModelPreparedEvent<ProductDetailsModel>`,
`_OrderSummary.cshtml`, `CWCorePlugin` message templates, a CW workflow-message service.

| Item | Why here |
|---|---|
| M-01 "My Subsidies" page (K2, K3, K4, K5 link) + M-06 storefront half | **Decision 9 (answered): full detail, and a redesign** — per subsidy: remaining, per group units + credit, expiry; history with order links, reorganised so a customer can follow it (the 4.6 layout is the reference for *content*, not for *layout*). The services exist (subject to S-12 from phase 3). Delete the two dead stylesheets pushed on every page. Runs as: design note / mock-up agreed first, then build |
| M-02 Manager assigns subsidies on add/edit packname | **Decision 10 (answered): keep; managers may also remove.** Same forms |
| P-13 `DefaultPackNameCustomerRoleIds` never applied | `RegisterCustomer` and the admin associate/create flows |
| P-14 Password = e-mail, no e-mail, `ForceToChangePassword` dead | Same method as P-13 |
| X-03 Welcome template + `%Subsidy.SubsidyName%` token | The e-mail P-14 needs; also X-01's prerequisite |
| P-10 Print name pre-fill | One `ModelPreparedEvent` case |
| G-01 Coupon / gift-card entry on the packname checkout | ~~Decision 14; two boxes in `_OrderSummary` or "out of scope" recorded~~ **Recorded as out of scope (decision 14 / G)** |
| **N-02** Packname order-completed page | **Raised 2026-09-16.** Packnames in the order, each destination, subsidy per packname — same presentation problem and same data as M-01, so it rides with it. It also retires phase 2a's one wart: the access filter's `Completed` allow-list entry becomes a re-route to this page, and the exception stops existing |
| M-04 / P-18 Landing page after login / selection | ~~Decision; reuse the bulk-order landing hook~~ **Done (decision H): `IPackNameLandingService`, subsidy catalogue first, then bulk ordering** |
| M-05 Spec-attribute filtering semantics | ~~Record only~~ **Recorded** |
| P-09 "Ignore packname context" switch | ~~Decision; port via `ISession` only if an in-scope flow needs it~~ **Closed as not needed (decision I)** |

Test plan: a manager creates an employee (gets the configured roles, a random password, the welcome e-mail
with the subsidy list, forced change on first login), assigns two subsidies on the form, the employee opens
*My Subsidies* (remaining, units, groups, expiry, history with order links), the print name is pre-filled
on a product page and lands on the order line.

### Phase 8 — Settings that do nothing, and missing features — **CLOSED 2026-10-19** (Part 1 2026-10-11, Part 2 2026-10-19)

> **Part 2 closed 2026-10-19** (record: gap analysis §17b "From phase 8 Part 2", `MultiStore_Management_Reference.md`
> §8g, `Feature_Toggles_Reference.md` §4.4 / §5, `CLAUDE.md` §13.4 / §15.3, `tasks/lessons.md` #110–#119; the phase
> document is consumed): X-01 ported as the manager's import page behind the selector's Excel icon — synchronous, every
> row checked before anything is written, then ten rows per transaction (a single transaction froze the store in the
> first run), the result and an error report on the page instead of the 4.6 background job and e-mail (decision 13's
> channel); one registration service for the add form and the import; one name per list and "a switched-off list takes
> nothing new" on every writer; `AllowToAddByExcel` / `AllowSubsidyCountByExcel` read (T-01 closed);
> `DeleteSubsidyVariationAsync` deleted. 119 changes (the page redesigned in 115–119), three test runs, 71 checks in
> the last, all passed or recorded. Left: the items of §17b "From phase 8 Part 2" — several unowned (edit forms clearing
> hidden fields, no database guard for one name per list, Generate provisioning inactive customers).

> **Part 1 closed 2026-10-11** (record: gap analysis §17b "From phase 8 Part 1", `MultiStore_Management_Reference.md`
> §8f, `Feature_Toggles_Reference.md` §4.3, `CLAUDE.md` §13 / §14 / §15.3; the phase document is consumed): the store
> and branch shipping-method pickers filled from the CW.Freight plugin and enforced on save (N-01, decision 1); one
> shared-destination line on the checkout box with the charge the total makes once (decision 2); default list for a new
> branch and a packname for a customer an admin links to a branch, behind the PackName switch (P-15, decision 3);
> store self-registration deleted (X-02, decision 4); T-01 closed except the X-01 rows. Found in the run and fixed: the
> settings POSTs rewrote the automation overrides from unposted fields (F1); the standalone *Branches* page — kept, the
> manager wants it as in 4.1 — erased branch methods, never provisioned, allowed a looping or cross-store parent and
> deleted branches with children (F2); `DomainRegistrationFilter` left the UI. 80 changes, 12 unit tests, 30 of 30
> checks. **Part 2** (X-01 Excel import, plus the small carried items in the table above) — closed 2026-10-19, above.

**Owns:** `MultiStoreSettings`, both settings views, `MultiStoreSettingsValidator`, `CWCorePlugin` hints and
resources, `PackNameCheckoutModelFactory.PrepareAvailableAddressesAsync`, and — if decided — a new import
manager on nop 4.9's `ImportManager` / `PropertyManager` pattern.

| Item | Why here |
|---|---|
| T-01 Consolidated table of dead settings | For each: implement the consumer or delete model + view + validator + resource. By this point P-10, C-03, M-02, P-13, S-15 have resolved their rows in earlier phases; what is left is N-01, P-15, X-01, X-02 |
| P-12 "Branch default address only" | **Closed (decision 12): not needed.** Only the `CLAUDE.md` §15.3 correction |
| P-11 Cost-centre addresses at checkout | **Closed (decision 12): not needed.** See the dead-code follow-up in §2 (phase 2c) |
| **N-01 Store shipping-method policy for packnames** (replaces the dead `MultiStoreSettings.ShippingMethod`) | **New, decision 12:** a store chooses which shipping method(s) packnames may use. Settings UI (single vs multi-select to confirm in the phase doc), enforced in `SetShippingOptionsByPackNamesAsync` / `PrepareShippingOptionAsync`, with the existing per-branch fallback in `BranchService` layered under it. Sits here because the checkout shipping path is stable by then (phase 4a's G-04/G-05); the phase doc must re-read that path first |
| Shared-address shipping rate (phase 4a Part 1 carried #5, re-homed by phase 7 decision L) | With N-01: the per-destination rate shown at checkout and the total disagree when two packnames share an address |
| P-15 `UseStaffAsPackName` / `UseBranchesAsPackNameList` promise automation | Implement on branch / customer insert, or rename the settings and hints |
| X-01 Excel import (port) | **Decision 13 (answered): wanted.** Large. nop 4.9 `ImportManager` / `PropertyManager` + ClosedXML pattern, background queue, summary by e-mail (channel default — confirm), template columns following the store settings, N subsidy column groups per `AllowSubsidyCountByExcel`, welcome e-mail from X-03 |
| X-02 Store self-registration | **Decision 13 (answered): not wanted** — store registration already covers it. Delete `StoreRegistration`, the `RegistrationScreen` / `RegistrationScreenPageName` settings, their view fields and the validator rule |
| O-01 (rebuild half) | **Decision 14: blocked in 2a, rebuild deferred.** If it is ever wanted it comes after phase 6 (it must re-run settlement), as its own phase |

~~Update `CLAUDE.md` §15.3 in this phase — it lists `UseBranchAddress`, which does not exist.~~ Done at Part 1 closure (rewritten from `MultiStoreSettings.cs`).

### Phase 9 — Performance and caching — **CLOSED 2026-10-20**

> **Closed 2026-10-20** (record: the project closure record `claude/Subsidy_Phase9_Closure_Record.md`; `subsidy-system.md`
> §3 / §4 / §10 / §14, `Feature_Toggles_Reference.md` §1.5, gap analysis H-01 / H-02 / H-04 / G-07, `tasks/lessons.md`
> #121–#124; the phase document and its starter are consumed). H-01: credit read once per request for all the request's
> packnames (one query per view) and memoised in `SubsidyCreditService`, cleared by consumption/restore writes and before
> settlement; the dead consumers deleted. H-02: the next status-change instant cached; `MinimumOrderingCacheMinutes`.
> SQL: the five subsidy views `CROSS APPLY` the inline `subsidy_current_period` / `subsidy_topup_period_end`; the three
> scalar functions dropped; index `IX_CORE_SUB_SubsidyPackName_PackNameId`. The 15 `CreatedOnUtc` columns became
> `datetime2` UTC (migration `CWTimestampsDateTime2`, values moved to `+00:00` first). H-04: sequential reads, per-request
> cart-line maps. G-07: a change re-rates only that packname (`RatedFor`), one redraw request. Found and fixed: exchange
> orders settled outside the packname lock; the lock chain reported success when an inner lock was refused; cart updates
> rated the cart as read before the update. Checkout full load 5,445 → ~930 queries, SQL 4.1 s → 1–2.9 s; the browser time
> left (~12 s) is C#, open. 144 changes, 28 checks + unit tests (277) pass.

(Starter was `CWPlans/TIG49_New_Chat_Starter_Phase9.md`.)

**Owns:** `SubsidyCreditService` cache strategy, `SubsidyOrderingService` smart TTL, `SubsidyAllocationService`,
`PackNameCheckoutModelFactory`, the public `PackNameModelFactory` selector, `CWOrderService`.

| Item | Why here |
|---|---|
| H-01 (C# half) Credit cache per-request; nine dead invalidations; dead `OrderCacheEventConsumer` | Decision a/b. (a) delete the dead code; (b) short static cache with new consumers on `SubsidyOrderItem` / `SubsidyRestoreItem` / `Order`. The SQL half was phase 1 |
| H-02 Smart TTL runs two uncached queries first; hard-coded `3` | Cache the per-packname next-change date; `CacheConfig.ShortTermCacheTime` |
| H-04 N+1 and unbounded `Task.WhenAll` over scoped services | Batch and await sequentially; memoise the allocation beside the plan |
| G-07 Quadratic checkout recomputation | Rate once per distinct (address, method, items) group; return only changed panels |
| ~~U-13 (selector half)~~ | **Moved to phase 11**, which gathers both halves of U-13 — they share a service surface and the 2c/9 split did not hold |

Measure before and after on a manager with 50 packnames (query count, page time) — per store, through the
code's own visibility (lesson #28).

### Phase 10 — Documentation consolidation — **CLOSED 2026-10-21**

> **Closed 2026-10-21** (record: project `claude/Subsidy_Phase10_Closure_Record.md`). No application code. Seven
> read-only reviews (one lens each) checked every reference document against the code and inventoried the ten
> remaining phase documents and the two consumed specs; every stale statement found was corrected and every surviving
> design, decision and trap folded into its topic document: `subsidy-system.md` (insert-only top-ups, the catalogue
> gate, the one-cart rule §8.6, the info strip and cart-changed event §8.7, the history procedure §12.1, the
> settlement details of §7, rule 21 for 4c), `MultiStore_Management_Reference.md` (the four scope paths §7b.2, the
> Active rule §7c, the nullable branch §7d, eight new gotchas), `Feature_Toggles_Reference.md` (U-15 — closed since
> phase 2b), `CLAUDE.md` (§3 layout, §6.8 the DATA rule, §7 comments rule, §10.1 three more copied bodies, §13–§20
> reconciled, §22 Note A closed), `PackName_Order_Shipments.md` §9 (the own-placement hand-over and the pending PayPal
> tests kept verbatim), `Exchanges_Returns_CreditNotes_Reference.md` (replacement exemptions; the impersonation stub).
> The gap analysis has every closed item struck through, Q-01 closed with its residue listed, and a "From phase 10"
> section. **`Subsidy_PackName_Open_Items.md`** gathers everything left — the unowned items, the code items earlier
> phases had routed to phase 10 (not done here; a documentation phase), the pending PayPal tests, and the findings of
> a **4.1 / 4.6 port audit**: three defects (approval e-mail templates never installed; cross-store impersonation a
> stub landing on a route that does not exist; 4.1's return-eligibility rule not ported) and a dozen smaller gaps,
> each with evidence and a proposed owner. Moved to `_to_delete/`: `Subsidy_Phase1_Period_Arithmetic.md`,
> `Subsidy_Phase2b_Access_Admin.md`, `Subsidy_Phase2c_ManageStore_Rework.md`, `Subsidy_Phase3_Admin_Subsidy_Screens.md`,
> `Subsidy_Phase4a_One_Cart_One_Gate.md`, `Subsidy_Phase4b_Allocation_Rules.md`, `Subsidy_Phase4c_Any_Payment_Method.md`,
> `Subsidy_Phase5_Part1_Department_ACL.md`, `Subsidy_Phase5_Part2_Allocation_Gate.md`, `Subsidy_Phase6_Money_Semantics.md`,
> `change-topup-spec.md`, `packname-info-spec.md`, and the starter `TIG49_New_Chat_Starter_Phase10.md`.

The scope as written: `subsidy-system.md` (§5.2, §8, §8.2, §14), `CLAUDE.md` §14 / §15.3 / §16, `MultiStore_Management_Reference.md`
(manager-scope semantics from P-06), `change-topup-spec.md` §5.2 (S-05), `packname-info-spec.md` (U-19 —
mark implemented or fold), `Feature_Toggles_Reference.md` (U-15), and the gap analysis itself:
every item closed by a phase is struck through with the phase and date. Whatever
Q-01 residue survived the phases is listed. Per `CLAUDE.md` §0.4 the phase documents are
consumed into the topic documents and removed.

---

### Phase 11 — Packname bulk actions and selector (U-13) — **CLOSED 2026-10-21**

> **Closed 2026-10-21** (record: gap analysis U-13, its status paragraph and §17b "From phase 11",
> `MultiStore_Management_Reference.md` §5 / §8h, `Feature_Toggles_Reference.md`, `tasks/lessons.md` #125, the project
> closure record; the phase document and its starter are consumed). Both grids: per-grid selection, Activate / Deactivate /
> Move to list / Delete and an Excel export, ten rows per transaction, the single save's and delete's rules. The selector:
> first 50 + selected + those with cart lines, server search, counts-only badge refresh. Fixed on the way: per-customer cart
> counts written onto cached rosters, the list row counting deleted managers, the remembered list after a move. 38 changes,
> 3 unit-test classes (312 pass), 24 checks + 4 measurements pass. Left: the two views' fixed SQL cost per selector request
> (unowned).

**Why it is its own phase.** U-13 was split across two phases — the admin bulk actions in 2c, the
selector's server-side search in 9. Phase 2c's closing round showed that split does not hold: the
entry is sized **M** and its parts share a service surface. Moved out of 2c by decision (Eduardo,
2026-09-24) and gathered here (2026-09-28). The selector half moves out of phase 9 to join it.

**Owns:** selection state on the packname grids, bulk activate / deactivate / delete / move-to-list,
export, and server-side search for the manager selector.

| Item | Why here |
|---|---|
| U-13 (grid half) | Both admin packname grids offer per-row Edit / Delete only. Needs a selection model, the bulk endpoints and an export path |
| U-13 (selector half) | `PreparePackNamesModelForManagerAsync` (`PackNameModelFactory.cs:325`) returns **every active packname of a list** as JSON on every page load; TomSelect's `maxOptions: 100` hides the overflow rather than fixing it. Server-side search is the fix, and it is the same service surface the bulk actions need |

**Depends on:** nothing in 2c — the row collections it would act on are the two remaining grids
(`_PackNames.cshtml` and `_Branches.PackNamesInline.cshtml`), both deliberately left as grids.

---

### Phase 12 — Admin Excel import of packnames (X-04) — **CLOSED 2026-10-20**

> **Closed 2026-10-20** (record: gap analysis X-04 and its status paragraph, `MultiStore_Management_Reference.md` §5 /
> §8g, `Feature_Toggles_Reference.md` §4.1 / §4.4, `CLAUDE.md` §13.4 / §15.3 / §20, `tasks/lessons.md` #120, the project closure record; the
> phase document and its starter are consumed). *Manage Store › PackNames › Import from Excel* opens an admin page with
> the storefront page's body (shared partial), template, checks, ten-row batches, result and error report. Decided:
> *Allow to add by Excel* / *Allow managers to add packnames* do not gate it (as 4.6); the administrator is the creator
> (activity type `ImportPackNames`, `CreatorId`); the manager settings shape only a manager's import (every active
> branch, account and subsidy columns for an administrator); welcome e-mails in the store's default language; button on
> the PackNames tab only; no *Manager* column. 21 changes, 10 unit tests, 20 checks (19 pass, 1 info).


**Why it is its own phase.** Phase 8 Part 2 ported X-01 for managers only (the recommendation taken when the part was
opened); the 4.6 admin import (*Company › Employees › Import from Excel*) was recorded as not ported. Eduardo wants it
(2026-10-19). Phase 8 is closed, so it gets a phase of its own; it reuses the import service whole, so it does not
depend on phases 9–11 and can run before them if wanted.

**Owns:** an admin *Import from Excel* entry on the store's packname grid (Manage Store › PackNames tab, `_PackNames.cshtml`)
— template download, upload, result panel and error report in a dialog or page of the admin area — and an admin mode in
`IPackNameImportService` (no manager: every active branch of the store, the store's departments, cost centres and
subsidies; `LimitAssignedBranches` does not apply).

| Item | Why here |
|---|---|
| X-04 admin-side Excel import | Wanted (Eduardo, 2026-10-19). 4.6: `CompanyController.ImportPackNames` / `GetPackNamesTemplate`, ungated by *Allow to add by Excel*, any branch of the company, background job + e-mail. 4.9 reuses phase 8 Part 2's synchronous import (checks first, ten rows per transaction, result on the page) |

**Decide in the phase:** whether *Allow to add by Excel* / *Allow managers to add packnames* also gate the admin action
(4.6: no — they were manager settings); who the activity log and the welcome e-mail name as the creator; whether the
Branches tab's list view gets the button too. **Depends on:** nothing open. **Test plan:** the admin half of phase 8
Part 2's plan (template, refusals, batches with another user browsing, a stop part way, switches both ways) plus admin
permission and the store's PackName switch.

---

## 5. What is deliberately not in a phase

- **Contract items, Jim2 sync, attribute ACL, card files, Company, pre-orders, quotes, buffer stock,
  favourites, notifications, SevenSpikes, theme views, bulk ordering** — out of scope by the gap analysis.
- **4.6 `PreOrder` as the answer to C-08** — out of scope; the view-side exclusion is the in-scope answer.
- **Multi-instance locking** (`Exchanges_Rebuild_Record.md` §6.0) — a deployment-topology decision, not a
  code phase; but phase 1's unique indexes and phase 6's persisted summary are chosen so they hold without it.

---

## 6. How each phase is run

1. **Open with a re-read.** The phase document's first section lists the files it will touch, re-staged the
   same day, and re-checks every gap-analysis premise for that item against the current code (lessons #29,
   #45). A premise that no longer holds is struck through before any spec is written.
2. **Spec = exact changes.** File, anchor text, before/after, whole method where several edits land in one
   (lessons #25, #32, #33). Every replace carries its removal line so the negative grep exists (lesson #34).
3. **Test plan inside the phase document**, written before the code, naming the precondition each case must
   satisfy (lesson #35) and at least one value computed independently (lesson #13).
4. **Decisions consumed are quoted** in the phase document with their number from §2, so the record reads
   like `Exchanges_Rebuild_Record.md` §3.
5. **Closure = human approval** (`CLAUDE.md` §0.2), then the gap-analysis row is struck through with phase and
   date, and any lesson captured in `tasks/lessons.md`.
