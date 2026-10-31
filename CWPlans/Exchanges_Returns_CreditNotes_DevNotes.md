# Exchanges, Returns & Credit Notes — Developer Notes & Changelog (CW.Core)

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

## Purpose & how to use this file

This is the companion to `CWPlans/Exchanges_Returns_CreditNotes_Reference.md`. The split:

- **`Exchanges_Returns_CreditNotes_Reference.md`** — what exists today and how it works, written in
  present tense with no history or rationale. Safe to hand to anyone (PM included) who wants to
  understand the feature as it stands right now. Also the input for planning *new* work in this area.
- **This file** — the *why* and *when*: design evaluations done before implementing something, technical
  decisions validated along the way (with the evidence behind them), and a chronological changelog of
  what actually changed. Developer-only; assumes the reference doc as background.

**Workflow going forward:** when a change lands in this area —

1. Update the reference doc's relevant section(s) to describe the *new current state* in plain, dateless
   present tense (no "as of", no "previously", no cross-references into this file).
2. Add a dated entry to this file's **Changelog** (bottom of this document) summarising what changed and
   why, with a pointer to the reference doc section(s) it affects.
3. If the change involved a real design decision (more than one viable option, a tradeoff worth
   recording), add a numbered section here documenting the evaluation — question raised, options
   considered, evidence gathered, recommendation/verdict. Future work in this area should read as much of
   this file as is relevant before re-litigating a decision already made here.
4. If a bug or a piece of dead code is discovered while working on something else, add it to the
   reference doc's **Known issues** section as a plain, dateless fact (what's wrong, where) — and, if the
   *story* of how it was found is worth preserving (e.g. it surfaced while validating an unrelated
   change), add that narrative here instead of cluttering the reference doc.

---

## 1. `BusinessLogic/` classes formalised as an interfaced layer (2026-08-07)

**Question raised:** is the `BusinessLogic/` orchestrator-class pattern (`ExchangesLogic`, `CustomersLogic`,
`CWOrderLogic`) a legitimate pattern for this codebase, or a smell? None of the three had interfaces,
breaking the `IFooService`/`FooService` convention used everywhere else (and the NUnit + Moq testability
it enables).

**Conclusion:** the pattern itself is fine for genuine cross-service orchestration (`ExchangesLogic`,
`CustomersLogic` both coordinate 3+ services for one workflow) but `CWOrderLogic` didn't qualify — it was
a single permission check with no cross-service coordination, not an orchestrator. The convention is now
codified in `CLAUDE.md` §6.13.

**Changes made:**

- `ExchangesLogic` and `CustomersLogic` now implement new `IExchangesLogic`/`ICustomersLogic` interfaces
  and are registered/consumed by interface everywhere (`NopStartup.cs`, `ReturnsController`,
  `ExchangesController`, `CWPdfService`, `ReturnExchangeRequestValidator`, `CustomerReturnRequests.cshtml`).
- `CWOrderLogic` was deleted outright. Its sole caller, `Areas/Admin/Views/Order/Edit.cshtml`, had injected
  it directly — bypassing the Controller → ModelFactory → Model → View pattern (`CLAUDE.md` §6.5). The
  check is now inlined in that view using the `IPermissionService`/`CWPermissionConfigManager` already
  available globally via that area's `_ViewImports.cshtml`.

**Two pre-existing bugs surfaced while making this change** (neither caused by it, neither fixed at the time — the
reference doc's Known Issues section has the current statement; the first no longer exists in 5.00, whose
`CustomerReturnRequests.cshtml` injects no `IExchangesLogic`):

- `Views/ReturnRequest/CustomerReturnRequests.cshtml` calls `GetSelectedProductAttributeValueAsync` on the
  injected `IExchangesLogic` — that method doesn't exist there, it's defined on the unrelated
  `Helpers/ProductsHelper.cs`. This view has never compiled with those two lines live.
- `CustomersLogic.ImpersonateCustomer`'s `[CheckPermission(...)]` attribute is likely inert — that
  attribute only works via the ASP.NET Core MVC filter pipeline on a controller action, not on a plain
  business-logic method.

A third issue found at the same time — `CWPdfService._exchangesHelper` declared but never assigned, always
`null` — **was fixed in passing**: the class now takes `IExchangesLogic exchangesHelper` as a constructor
parameter and assigns it.

---

## 2. Design evaluation — live-computed Credit Note balances (2026-08-07)

**Question raised:** Subsidy availability is never stored as a mutable balance — it's computed live from
SQL views over insert-only ledger tables (`SubsidyOrderItem`, `SubsidyOrderShipping`, ...), specifically
to avoid the class of bug where a stored balance silently drifts from reality. Should Credit Notes — which
at the time *did* store a mutable balance (`CreditNote.Remain`) — be redesigned the same way? And should
Credit Notes' historical data (`CreditNoteHistory`) become a read-time SP over current data, the way
Subsidy's admin history feed (`subsidy_get_history`) is, instead of a stored table?

### 2.1 How each system worked at the time (verified against source)

**Credit Notes — stored, mutable balance (before this change):**

- `CreditNote.Remain` (`CORE_EX_CreditNote.Remain`, `decimal(18,2) NOT NULL`) was the current balance.
  `CreditNotesService.ApplyCreditNotesForOrderAsync` directly mutated it: `creditNote.Remain -=
  amount.Value` (clamped to 0 if it would go negative), then inserted a `CreditNoteHistory` row recording
  the amount consumed and the resulting balance snapshot.
- `CreditNotesLogic.UndoCreditNotesForOrderAsync` (run on order cancellation) did the reverse: it
  incremented `Remain` back up and then **deleted** the `CreditNoteHistory` rows for that order —
  destroying the record that the credit was ever applied and reversed.
- `IRepository<CreditNote>` was injected in exactly one place (`CreditNotesService`), and `.Remain` was
  read directly in 7 files across services, factories, controllers, and views — confirmed by a repo-wide
  grep; every one of those reads a `CreditNote`/`CreditNoteHistory` object already returned by
  `CreditNotesService`, none independently queried the raw table's `Remain` column except three
  `.Where(c => c.Remain > 0)` filters. **The blast radius of changing how `Remain` is produced was small
  and entirely contained inside `Services/CreditNotes/` + two admin factories.**

**Subsidy — computed, insert-only ledger (for comparison):**

- `SubsidyOrderItem` / `SubsidyOrderShipping` are written once at order placement and never updated or
  deleted anywhere in the codebase (confirmed by grep). They are a pure event log.
- "Available credit" is not a column anywhere — it's computed by `vw_CORE_SUB_SubsidyPackNameCredit` /
  `vw_CORE_SUB_ProductGroupPackNameCredit` as `allocation − SUM(consumption rows joined to non-cancelled
  orders)` (per `subsidy-system.md` §4; the join excludes `OrderStatusId = 40 / Cancelled`).
- Because of that exclusion, cancelling an order requires no explicit "undo" write at all — the moment
  `OrderStatusId` flips to `Cancelled`, every view that computes availability stops counting that order's
  consumption. This is exactly why `SubsidyCreditService.UndoSubsidyForOrderAsync` can be a no-op and
  still be correct.
- Admin history display (`ISubsidyReportingService.GetSubsidyPackNameHistoryAsync`) is backed by the
  `subsidy_get_history` stored procedure, which `UNION ALL`s **five distinct physical event tables** into
  one chronologically-ordered, paged feed. The SP exists *because there are multiple distinct kinds of
  events* that need merging — it is not a replacement for storing events; the events are still stored, in
  their own small tables. The SP is purely a read-time presentation layer over data that is 100% stored.

### 2.2 Proposal — apply the same pattern to Credit Notes

**A. Make `Remain` a computed value, not a stored column.**

```sql
-- Illustrative only — not a migration
SELECT
    cn.Id,
    cn.Amount - ISNULL(SUM(
        CASE WHEN o.OrderStatusId <> 40 THEN h.Amount ELSE 0 END
    ), 0) AS Remain
FROM CORE_EX_CreditNote cn
LEFT JOIN CORE_EX_CreditNoteHistory h ON h.CreditNoteId = cn.Id
LEFT JOIN [Order] o ON o.Id = h.OrderId
GROUP BY cn.Id, cn.Amount
```

`CreditNoteHistory` stays exactly as it is — a stored, insert-only ledger (already structurally identical
to `SubsidyOrderItem`, just not treated that way yet). Nothing about *what* gets written changes; only
*what gets deleted* and *what gets read directly vs. computed* changes.

**B. Stop deleting history on cancellation.** With `Remain` computed and cancelled orders excluded,
`CreditNotesLogic.UndoCreditNotesForOrderAsync` becomes unnecessary for balance correctness — same
conclusion as `UndoSubsidyForOrderAsync`. Replacing it with a no-op (or deleting it) also fixes a real,
separate problem: today it destroys the audit trail of "credit was applied, then the order was cancelled,
so it was reversed." Under the computed model, that history is preserved forever and the *reason* a
consumption no longer counts (the order's status) is visible right there in the join.

**C. Same idea extends to the per-row "Remain" snapshot on `CreditNoteHistory`** — identical drift risk as
the header balance. A SQL window function replaces it the same way:

```sql
Amount - SUM(Amount) OVER (PARTITION BY CreditNoteId ORDER BY CreatedOnUtc ROWS UNBOUNDED PRECEDING)
```

**D. The "SP instead of a table" question does NOT apply the same way, today.** `subsidy_get_history`'s
reason to exist is that Subsidy has five separate kinds of events to merge into one feed. Credit Notes
have exactly one kind of tracked event — consumption. Paging that single table gets no benefit from
becoming a stored procedure. **Do not build a SP now; revisit if/when a second Credit Note event type is
added** (e.g. if issuance gets its own audit table, or "note voided" is ever tracked separately).

**E. Related, smaller gap noticed while evaluating this:** Subsidy topups get first-class issuance audit
rows (`SubsidyTopupHistory`: amount, reason, who, when). Credit Note *creation* has no equivalent —
`CreateManualCreditNoteAsync` only writes a generic `ICustomerActivityService` log entry (shared,
unstructured), and `CreateCreditNotesForOrderAsync` doesn't log anything beyond the `CreditNote` row
itself. If admin reporting ever needs "who issued this note and why" as a first-class queryable field,
that's a small, separate schema addition — worth it only once a union-style history SP (point D) is
otherwise justified.

### 2.3 Pros

- **Eliminates an entire bug class.** A stored balance can drift from its ledger (missed write, a code
  path that forgot to insert history, manual DB fix, a double-apply bug). A computed balance cannot drift
  by construction — it's always exactly `Amount − Σ(qualifying history)`, recalculated fresh every read.
- **No more "undo on cancel" special case.** One less place for apply/undo to quietly diverge.
- **Preserves full audit history** — the computed model never deletes anything.
- **Makes bugs visible instead of silently clamped.** Today, `if (creditNote.Remain < 0) creditNote.Remain
  = 0;` quietly hides any case where more was consumed than was available. A computed value would show a
  *negative* `Remain` in that scenario instead.
- **Architectural consistency** with Subsidy — one mental model for "how much of this balance is left"
  instead of two.
- **Cheaper than Subsidy's version of the same idea** — no product groups, quantity vs. credit, topups,
  periods, or per-PackName variations. A single flat pool, one consumer table, no periods.
- **Small, contained blast radius** (§2.1).
- **No migration burden** — this branch wasn't in production at the time, so the `Remain` column could be
  dropped outright rather than requiring a keep-both/backfill/cutover dance.

### 2.4 Cons / risks

- **Concurrency needs deliberate handling, same as Subsidy already has.** A "read current available
  balance from a view, then decide how much to consume, then insert history" sequence is a
  read-then-write pattern — two concurrent orders for the same customer could theoretically both read the
  same "available" snapshot before either writes its consumption, over-spending the note. Not a new
  problem introduced by this change (Subsidy's equivalent methods have the identical shape) but worth
  stating explicitly. See §3 for the chosen mitigation.
- **View maintenance is one more thing to keep in sync with schema changes** — true of every view already
  in the system, not a new kind of maintenance burden.
- **Slightly less "obvious" to a future reader** used to plain stored columns — mitigated by this document
  and by following the exact naming/registration pattern Subsidy already uses.
- **The views for Credit Notes (like Subsidy's) aren't created via a tracked FluentMigrator migration** —
  a real process gap worth fixing regardless of this decision (views should ideally be migration-tracked
  like everything else, per `CLAUDE.md` §6.2 and §8).

### 2.5 Effort estimate (at the time)

Low-to-medium — meaningfully less work than the Subsidy views, because a complete working pattern already
existed to copy, the domain is simpler (no periods/topups/product groups), the blast radius was small and
fully mapped (§2.1), and no data migration/backfill was required.

### 2.6 Recommendation

Worth doing, and a good candidate to do *before* implementing the real `RefundSubsidyForReturnAsync`/
subsidy-side work — it removes the one meaningfully different balance-management pattern left in this
area. Treat A–C as one task; leave D (history-as-SP) and E (issuance audit table) deferred until a second
Credit Note event type actually exists.

**Implemented and validated 2026-08-07** — see the Changelog below and the reference doc's §5 (Data
model) and §6.1 (`CreditNotesService`) for the resulting current-state design.

---

## 3. Concurrency mechanism — `ILocker` vs `SemaphoreSlim` (validated 2026-08-07)

**Question raised during implementation:** the fix in §2 serializes the
read-available-balance → decide-consumption → insert-ledger-rows sequence (for both Credit Notes and,
since it has the identical shape, Subsidy order placement) using nopCommerce's `ILocker`
(`Nop.Core.Caching.ILocker`). Is that the right primitive, versus a hand-rolled `SemaphoreSlim`? How does
nopCommerce 4.9 handle this class of problem elsewhere, given `ILocker` had previously only been seen used
near caching code?

**How `ILocker` is wired up** (`Nop.Web.Framework.Infrastructure.NopStartup`) — registered centrally once
for the whole application, nothing for CW.Core to register itself:

- Distributed cache configured (Redis, SQL Server distributed cache — standard once a site runs more than
  one web server/instance behind a load balancer) → `services.AddSingleton<ILocker, DistributedCacheLocker>()`.
- Otherwise (single-instance/in-memory) → `services.AddSingleton<ILocker, MemoryCacheLocker>()`.

**Where nopCommerce core itself already uses it** — confirmed by reading both call sites in full:

- `Nop.Services.ScheduleTasks.ScheduleTaskRunner` — wraps every scheduled task execution in
  `_locker.PerformActionWithLockAsync(scheduleTask.Type, expiration, () => PerformTaskAsync(...))` so the
  same task can't run twice concurrently.
- `Nop.Web.Factories.SitemapModelFactory` — wraps sitemap XML generation the same way, to stop two
  concurrent requests regenerating the same file at once.

Both are exactly our class of problem — "don't let two callers do the same named unit of work at the same
time" — so `ILocker` is nopCommerce's standing answer here, just not one CW.Core had reached for before.

**`ILocker` vs `SemaphoreSlim`, concretely:**

| | `SemaphoreSlim` (hand-rolled) | `ILocker` |
|---|---|---|
| Scope | In-process only — two server instances behind a load balancer have completely separate semaphore tables, cannot see each other's lock | In-process on a single instance (`MemoryCacheLocker`) or cross-instance/cross-machine once a distributed cache is configured (`DistributedCacheLocker`) — matches whatever topology the store already runs |
| Stuck-lock protection | None built in — a crashed/hung holder leaks the lock forever | Built in — `expirationTime` guarantees release even on a mid-operation crash |
| Cleanup / memory growth | Must manage removing entries yourself or the dictionary grows unbounded | No cleanup needed — the "lock" is just a cache key with a TTL |
| Boilerplate / correctness risk | Acquire/`try`/`finally`/`Release()`/eviction logic all hand-written | One call: `PerformActionWithLockAsync(resource, expiration, action)` |
| Precedent in this codebase | None found anywhere in `Libraries/` or `Presentation/` | Already nopCommerce's own idiom |

**Verdict: `ILocker` is the better choice**, and materially more correct the moment the store runs more
than one instance — the realistic assumption for a production deployment even if day-one hosting is a
single instance. A `SemaphoreSlim`-based lock would work fine on a single instance, then silently stop
providing any protection at all once the site scales out, with no error or warning. `ILocker` degrades
gracefully instead.

**One honest caveat**, found while reading `DistributedCacheLocker`'s source (nopCommerce core, not ours
to change per the Cardinal Rule): its acquire step is a check-then-set, not atomic —

```csharp
if (!string.IsNullOrEmpty(await _distributedCache.GetStringAsync(resource)))
    return false;
// ← a second caller could read the same "empty" state right here, before either has written yet
await _distributedCache.SetStringAsync(resource, resource, ...);
```

Two callers arriving within microseconds of each other for the *same key* could theoretically both pass
the check and both proceed. Pre-existing nopCommerce core characteristic (also present in every other
`ILocker` usage in the codebase), not something introduced by this change, and the race window is
microseconds rather than the seconds-long window the lock is meant to close. `MemoryCacheLocker`'s
single-instance implementation doesn't have this gap (`IMemoryCache.GetOrCreate` is atomic). If a future
deployment needs an airtight guarantee even against microsecond-scale races on a distributed cache, the
stronger option is a SQL Server `sp_getapplock` wrapped in a small `CW.Core` helper — a new pattern this
codebase doesn't use anywhere today, for a risk window nopCommerce itself already accepts in its own
trusted scheduler subsystem. Not recommended unless a real incident shows it's needed.

**Applied:** `ApplyCreditNotesForOrderAsync` (Credit Notes) and
`MovePackNameShoppingCartItemsToOrderItemsAsync` (Subsidy) both wrap their
read-available-balance→decide→insert-ledger-rows sequence in `ILocker.PerformActionWithLockAsync`, keyed
per customer for Credit Notes and per PackName (locked in a fixed ascending order via a small
`RunWithPackNameLocksAsync` helper, to avoid deadlocking two orders that touch an overlapping set of
PackNames) for Subsidy.

---

## 4. Caching the Credit Note balance read — `IShortTermCacheManager` (validated 2026-08-10)

**Question raised:** `CreditNotesService`'s balance-read methods hit the DB on every call, with no
caching at all. Worth fixing — and if so, with nopCommerce's persistent `IStaticCacheManager`, the way
some other CW services use it, or something else?

**How often it's actually hit, confirmed by reading the call graph:** `CreditNotesService` is called from
`OrderTotalCalculationOverrideService.PerformCreditNotesCalculationsAsync`, part of the override of
nopCommerce's own `GetShoppingCartTotalAsync` — a method nopCommerce itself invokes multiple times per
single cart/checkout render (cart summary block, mini-cart widget, checkout review step; its parameter
type is literally named `CartTotalsForCaching`, nopCommerce's own acknowledgment that this gets recomputed
repeatedly per page). `CreditNotesWidgetsViewComponent` independently calls the balance check on the same
cart/order pages. A single cart page load can plausibly fire 3-4 identical queries for the same customer.

**How nopCommerce's two cache managers actually differ — corrected after an initial misread:**

- **`IStaticCacheManager`** — persistent, cross-request (in-memory or distributed cache per
  `DistributedCacheConfig`). Needs explicit invalidation on every write path via a
  `CacheEventConsumer<TEntity>`-style consumer, or it can serve stale data across unrelated requests until
  its expiry.
- **`IShortTermCacheManager`** (`PerRequestCacheManager`) — registered
  `services.AddScoped<IShortTermCacheManager, PerRequestCacheManager>()` — one instance per DI scope, i.e.
  per HTTP request, backed by an in-memory `ConcurrentTrie<object>` with no TTL. Discarded automatically
  when the request ends, so it can never serve data across two different requests — the only staleness
  risk is *within* one request, if something writes to the cached data after an earlier read in that same
  request.
- **nopCommerce closes that same-request gap architecturally, not by accident.** `IRepository<T>.Insert/
  Update/DeleteAsync` publish `EntityInsertedEvent<T>`/etc., and the base
  `Nop.Services.Caching.CacheEventConsumer<TEntity>` (auto-discovered as an `IConsumer<T>`, no manual DI
  registration needed) handles all three by calling `RemoveByPrefixAsync`/`RemoveAsync`, **both of which
  clear both caches in one call**:
  ```csharp
  protected virtual async Task RemoveByPrefixAsync(string prefix, params object[] prefixParameters)
  {
      _shortTermCacheManager.RemoveByPrefix(prefix, prefixParameters);
      await _staticCacheManager.RemoveByPrefixAsync(prefix, prefixParameters);
  }
  ```
  So when Subsidy's cache-event consumers clear `SubsidyCreditByPackNameIdPrefix` on every relevant write,
  it *is* live, useful code — it protects against exactly the same-request read-then-write-then-read
  sequence, for whichever request happens to be making the write. **(Correction: an earlier pass at this
  investigation only checked the `_staticCacheManager` line in isolation and concluded the short-term half
  was dead code — it is not. Lesson: read the whole method, not a grep-matched single line out of
  context.)**

**Design applied for Credit Notes, following the identical pattern:**

- Cache only the open-notes (`Remain > 0`) list read via `IShortTermCacheManager` — the one read every
  other hot-path caller ultimately depends on.
- Fold the "has balance" boolean check into the same cached list (`.Count > 0`) instead of a separate
  query/cache key.
- Do **not** cache the FIFO-allocation calculation itself — it takes `orderTotal` as a parameter that can
  legitimately change within a request (discounts, gift cards, shipping selection), so keying a cache on
  it either misses constantly or risks reusing a result computed against a total that's no longer current.
  Let it keep doing its greedy loop fresh every call against the cached list — pure in-memory work over at
  most a handful of rows, effectively free.
- Invalidate via `CacheEventConsumer<CreditNote>` and `CacheEventConsumer<CreditNoteHistory>`, mirroring
  Subsidy, so a write anywhere in the same request invalidates that specific customer's cached entry
  immediately — both entities know `CustomerId` directly, so this targets `RemoveAsync(cacheKey,
  entity.CustomerId)` rather than a store-wide `RemoveByPrefixAsync`, a narrower blast radius than Subsidy
  needs (a `Subsidy` row change can affect many PackNames at once; a `CreditNote`/`CreditNoteHistory` row
  only ever affects the one customer on it).
- **Not persistent `IStaticCacheManager` for this read** — not because invalidation would be hard to add
  (it costs nothing extra once the `CacheEventConsumer` classes exist), but because the underlying query
  is already cheap (one `SUM` over an insert-only table, no product groups/topups/periods) and the whole
  performance problem — "recomputed several times within one request" — is fully solved by per-request
  caching alone. Persistent caching would add a second cache layer for savings per-request caching
  already captures.

**Applied and validated 2026-08-10** — new `CreditNotesServicesDefaults`, `CreditNoteCacheEventConsumer`,
`CreditNoteHistoryCacheEventConsumer` classes, plus the `CreditNotesService` change. See the reference
doc's §6.1 for the resulting current-state description.

---

## 5. Further caching pass + `GetCreditNotesByCustomerIdAsync` rename (validated 2026-08-10)

**Question raised:** now that the balance read is cached, does anything else in `CreditNotesService`
deserve the same treatment — and separately, `GetCreditNotesByCustomerIdAsync` had two overloads (one
filtered to `Remain > 0`, one paged and unfiltered) sharing one name — should that be one method, or is
the paged one actually paging in memory instead of at the query level?

**Pagination check, corrected:** the paged overload called `query.ToPagedListAsync(pageIndex, pageSize)`
while `query` was still an `IQueryable<CreditNoteBalance>` — nothing materialized yet.
`Nop.Data.Extensions.AsyncIQueryableExtensions.ToPagedListAsync<T>` (nopCommerce's own extension) does
`source.CountAsync()` then `source.Skip(pageIndex * pageSize).Take(pageSize).ToListAsync()` **on the
`IQueryable`**, which LinqToDB translates to a `COUNT` query plus a proper `OFFSET/FETCH` query. It
already paged at the SQL level — nothing was fetched in full first. The suspicion was reasonable (this
exact anti-pattern is common enough to be worth checking every time), just not what was happening here.

**Per-method pass, checking actual repeat-call evidence for each read method** (the same standard applied
to the balance method — repetition *within one request* is what short-term caching can fix; repetition
*across* separate requests/actions cannot be fixed by it):

| Method | Repeat-call evidence within one request | Verdict |
|---|---|---|
| `GetCreditNoteByIdAsync(id)` | **Yes, confirmed.** The public credits page loops every `CreditNoteHistory` row for a customer and calls this per row — a single credit note consumed across several orders has several history rows pointing at the same `CreditNoteId`, so the same ID is legitimately re-fetched multiple times in one request. | **Cache (short-term)**, keyed by id. Complements, doesn't replace, the reference doc's §11.1 batch-fetch recommendation for the underlying N+1. |
| Open-notes list (was the non-paged `GetCreditNotesByCustomerIdAsync`) | Confirmed in §4 above — cart totals recomputed several times per render + the widget. | Already cached. |
| Paged/all-notes list (was the paged `GetCreditNotesByCustomerIdAsync` overload) | None found — each admin-grid call is its own request/action, called once per page view. | **Don't cache.** Already DB-paged correctly; admins want current data more than a query that only runs once anyway needs shaving. |
| `GetCreditNoteHistoriesByOrderIdAsync(orderId)` | None found — once per order-detail render or per email token generation. | Skip. |
| `GetCreditNoteHistoriesByCustomerIdAsync(customerId)` | None found — once per public credits-page render. | Skip. |
| `GetCreditNoteHistoriesByCreditNoteAsync(creditNoteId)` | None found — once per admin history-grid request. | Skip. |
| `CustomerHasCreditNotesAsync(customerId)` | Not repeated *within* a request, but called from **three independent gating spots** across many different requests for the same customer, and the answer is practically monotonic — the only way it flips back to `false` is every one of a customer's notes and history rows being deleted, a rare admin/undo path. | **Cache — but in `IStaticCacheManager` (persistent), not short-term.** The one method here where cross-request caching earns its keep: short-term gives zero benefit across separate requests, and the staleness risk of a persistent cache is negligible for a near-permanent boolean — especially since the same `CacheEventConsumer` classes from §4 already invalidate it at no extra cost. |
| `CustomerHasCreditNotesBalanceAsync` | Already folded into the cached open-notes list (§4). | Done. |
| `GetCreditNotesByStoreAsync(storeId)` | **Looks like a repeat pattern but isn't.** Three admin factory methods each loop over stores calling this once per store — but tracing the admin controller shows these are **three separate controller actions**, each its own DataTables AJAX request. Different requests get different `IShortTermCacheManager` instances, so per-request caching cannot collapse this — confirms the reference doc's §11.2 diagnosis (needs the `vw_CORE_EX_CreditNoteStoreSummary`-style aggregate view, not caching). | **Skip caching here.** |
| `CreditNoteHasHistoryAsync(creditNoteId)` | Called once per row in an admin delete-button-enablement grid — N calls, but N *different* ids, no repeated key. | Skip — caching only pays off when the *same* key repeats. |
| Writes (`ApplyCreditNotesForOrderAsync`, `CreateCreditNotesForOrderAsync`, `CreateManualCreditNoteAsync`, `UndoCreditNoteForReturnRequestAsync`, `DeleteCreditNoteAsync`, `UpdateCreditNoteAsync`) | — | N/A — these are the invalidation triggers, not cache candidates. |

**Design question — one `GetCreditNotesByCustomerIdAsync` method or two?** Kept two, renamed for clarity,
rather than merging:

- They filter differently (`Remain > 0` vs everything) and are consumed differently: the open-notes list
  feeds the FIFO consumption algorithm, which must see the complete set to be correct — paginating it
  could silently exclude a note that should have been spent next. The paged version is a UI/reporting
  concern (admin grid, unbounded growth over a customer's lifetime) that correctly wants a page at a time.
- Their return types already signalled the difference (`IList<CreditNote>` vs `IPagedList<CreditNote>`) —
  collapsing them behind one method with a `remainOnly`/paging-flag parameter would trade two small,
  single-purpose methods for one branchy method a caller has to read the body of to understand.
- Only the open-notes version is cacheable per-request in any meaningful way — merging would either cache
  the paged variant too (multiplies cache keys by pageIndex/pageSize for a query that's called once
  anyway) or complicate the caching logic with a conditional.
- The actual problem was the shared name, not the overload split. Renamed:
  - `GetCreditNotesByCustomerIdAsync(int customerId)` → **`GetOpenCreditNotesByCustomerIdAsync(int customerId)`**
  - `GetCreditNotesByCustomerIdAsync(int customerId, int pageIndex, int pageSize)` → **`SearchCreditNotesByCustomerIdAsync(int customerId, int pageIndex = 0, int pageSize = int.MaxValue)`**
- **Separate, smaller smell noticed in passing, not fixed:** `pageSize = int.MaxValue` as a default on
  `SearchCreditNotesByCustomerIdAsync` means calling it with no arguments silently fetches every row — the
  opposite of what a "paged" method should default to. Worth a follow-up to either drop the default or
  document why it's intentional (the current call sites always pass explicit `searchModel`-driven values,
  so it may simply never have been hit — but it's a footgun for the next caller).

**Applied and validated 2026-08-10**, together with the additional caching (`GetCreditNoteByIdAsync`
short-term, `CustomerHasCreditNotesAsync` static) proposed above. `ICreditNotesService` and every call site
were updated to the new names; confirmed via repo-wide grep that no active code still references the old
`GetCreditNotesByCustomerIdAsync` name — the only remaining hit is a commented-out line inside
`Areas/Admin/Controllers/CreditNotesController`'s dead `#region Methods OLD` block (already documented in
the reference doc's Known Issues as unreachable).

**Two minor things surfaced while validating this, neither fixed:**

- That same dead-code region's `CustomerCreditNotesGridData` action calls what is now
  `GetOpenCreditNotesByCustomerIdAsync` (`Remain > 0` only) rather than `SearchCreditNotesByCustomerIdAsync`
  (all notes, properly paged) — it already called the "wrong" one of the two overloads for a
  history-style grid before the rename too, just less obviously since both overloads shared one name.
  Moot today since the action is dead code, but worth knowing if it's ever revived. The live equivalent
  (`CustomerCreditNotesList` → `CreditNotesModelFactory.PrepareCreditNoteListModelAsync`) correctly uses
  `SearchCreditNotesByCustomerIdAsync`.
- `CreditNotesServicesDefaults.CustomerHasCreditNotesCacheKey`'s XML doc summary reads "Key for credit
  note histories by credit note Id" — a copy-paste leftover from the `CreditNoteByIdCacheKey` block above
  it. The `<remarks>` and the key itself are both correct; only the summary line is wrong. Cosmetic only.

---

## 6. Changelog

Newest first.

**2026-10-31 — CW.Core moved to nopCommerce 5.00.** This area came across in the port's phases 4d (credit-notes
admin), 4e (exchanges configuration, return-request edit page) and 5g (the returns wizard, My Returns, the
`ExchangesWidgets`), with the regression run of phase 7. Record: `CWPlans/CWCore_Port_Reference.md` §5 (*Phase 4d*,
*4e*, *5g*) and §8. What changed in this area, all reflected in the reference doc:

- **Credit-notes admin (4d):** the liability report's outstanding and per-customer values add up `Remain`, not the
  notes' original `Amount` (`CreditNotesModelFactory.cs:191, 281, 288`); amounts shown as prices.
- **Return-request edit page (4e):** the extra buttons (*Impersonate*, *Checkout notification*, *Undo approval*,
  *Notify customer*) are posts to `ActionName("EditExchangeRequest")` and each checks
  `Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE`; *Notify customer* no longer 404s; the returned quantity is capped at the
  requested quantity; *Undo approval* asks for confirmation; the linked-actions popup saves an empty selection.
- **Wizard (5g):** every post checked on the server (`IReturnSubmissionLogic.CheckSubmissionAsync`: active reason and
  linked action, returnable shipped units from `IExchangeService.GetReturnableQuantitiesAsync`, the mode, each
  replacement against its type, its required options and the packname catalogue) — this closes the hand-crafted-post
  gap of fix 21 below; nop's own return post filtered (`ReturnRequestSubmitFilter`); only the customer's own orders,
  requests and receipts (`GetCustomerReturnRequestsByIdsAsync`); *Confirm* registered under a lock per order; amounts
  in the customer's currency; only the store's active return reasons (`GetActiveReasonIdsAsync`). nop 5.00's
  withdrawal form: a guest's link goes through nop's form, the wizard is for the signed-in customer.
  *Edit Exchange Item* keeps the options not posted (`ModifyExchangedItemDetailsAsync` now returns `bool`).
- **M3 fixes:** with *Exchanges* on and no active reason rule the return page shows a message instead of an empty form;
  with *Exchanges* off it keeps nop's own reason and action lists.
- **Phase 7 fixes:** *My account → Return requests* shows the exchange block only for an `Exchange` request
  (`CustomerReturnRequests.cshtml:125`; a plain nop request showed an empty one); the manual credit note's activity
  text names its ids correctly (`CreditNotesService.cs:285`). The dev site's *Return authorized* status rule had to
  be given *Perform store credit* and *Perform exchange* before an authorisation raised anything — configuration, not
  code.
- **Open, carried from 4.9:** `ReturnAuthorisedPayment.CustomerNotification` / `ReturnAuthorisedExcepted.CustomerNotification`
  are sent only if the templates exist, and CW.Core installs neither, so no e-mail goes out on authorisation.
- The schema is one `Data/Migrations/SchemaMigration.cs`; the migrations named in older entries below
  (`ReturnCaseDeadColumnsCleanup`, `ReturnResolutionUniquePerCase`, …) no longer exist as files. The unique index on
  `ReturnResolution.ReturnCaseId` is `UX_CORE_RET_ReturnResolution_ReturnCaseId` (`SchemaMigration.cs:125`).

**2026-10-08 — Subsidy phase 7: the Store Credit page rebuilt.** `Views/CreditNotes/CreditsForMe.cshtml` lost its
`<style>` block to `Content/MyAccount/store-credit.css` and took the card layout of the new My Subsidies page (balance
card, *Your credit notes*, *Credit applied to orders*); order numbers link through `NopRouteNames.Standard.ORDER_DETAILS`.
The page now passes `CWCoreDefaults.CustomerCreditsForMeMenuTab` to the customer navigation — it passed `-1` before,
so its My Account item was never highlighted. The item's resource `Plugins.CW.Core.CustomerCreditsForMe` reads *My
Store Credit* (was *Credits for Me*), matching the page title; `Plugins.CW.Core.StoreCredit.WillBeApplied` and
`.NavTitle` had no reader and were removed. The never-shown subsidy section of the page (`CustomerSubsidySummary`,
`GetCustomerSubsidySummaryAsync`, `StoreCreditsModel.Subsidy*`) went with it — the subsidy position has its own page.
Same controller, model and liability rule; the order numbers in both tables are coloured and underlined because the
theme makes links inherit their colour. Record: gap analysis §17b "From phase 7".

**2026-10-07 — Subsidy phase 6 Part 2: E / R badges and the store-credit row.** The admin order lists show **E** (the
order holds an exchange replacement — 4.1's badge) and **R** (an item returned and authorised — new), from
`IExchangeService.GetOrderReturnFlagsAsync` behind `CWOrder/OrderBadges` and a widget script, instead of overriding nop's
admin order factory (a 43-service constructor) and copying its list view. The credit-note widget's row is now the
*Store credit* resource, a deduction on customer pages and bracketed in admin; the hard-coded "Credit Notes" and
"Calculated on next steps" are gone (`CreditNotesOrderSummaryModel.Message` removed). Reference §6.2, §7.1, §8.4.
Observed while testing: authorising return #98 failed at the replacement-to-cart step ("This packname is not available
for ordering") and `CleanupFailedExchangeWorkflowAsync` removed the credit note each attempt had raised — no
`CORE_EX_CreditNote` row was left. Record: `CWPlans/Deprecated/Subsidy_PackName_Gap_Analysis_And_Known_Issues.md` §17b "From phase 6 Part 2".

**2026-09-11 — Phase 3b: serialisation, and the invariant the database owns.** Re-scoped on survey before
any code was written — `ReturnRequestStatusChangeData` was already retired and "execution held until the
case resolves" was reversed by decision 11, so two of its four original pieces were gone, and the fee walk
it was to close is closed by decision 30 rather than by code. Detail and the validation record:
`CWPlans/Exchanges_Rebuild_Record.md` §4 (phase 3b), §6.0 and §7.4.

⚠️ **The finding that matters more than the fixes: nop's `ILocker` does not serialise simultaneous
callers.** `MemoryCacheLocker.RunAsync` acquires through `IMemoryCache.GetOrCreate`, which is
`TryGetValue` → run factory → `Set` and is **not atomic**. Concurrent callers all miss, each builds its
own `Lazy`, each sets its own `started = true`, and all proceed. Proven live: two requests ~140ms apart
behaved correctly (the second refused), while three dispatched in the same tick all acquired and resolved
one return case **three times**, freezing three sets of charges. `DistributedCacheLocker` has the same
read-then-write shape. This applies equally to the pre-existing `RunWithPackNameLocksAsync`.

**So the locks are de-duplication, and the database is the guarantee.** Fix 31 adds a unique index on
`ReturnResolution.ReturnCaseId` (`ReturnResolutionUniquePerCase`, which removes pre-existing duplicates
first or the index cannot be created), and `ResolveCaseCoreAsync` catches the insert failure and
**re-reads** rather than matching a provider-specific duplicate-key number: if a resolution exists by
then, a concurrent save won and this one stands down having written nothing. Charges are written after the
insert, so nothing is orphaned.

⚠️ **Do not remove that index in favour of "the lock already handles it".** It does not.

**And nop 4.90 has no transaction to build on** (decision 31 — *see the 5.00 note below*): `BaseDataProvider` opens a fresh
`DataConnection` per write across fifteen call sites, `INopDataProvider` exposes no `BeginTransaction`,
and nop core contains no `TransactionScope`. The target design's §6.2 asked for atomicity that cannot
exist on this platform, so `CleanupFailedExchangeWorkflowAsync` is the correct design rather than a
shortcut — now stated in `ReturnCaseService.ReopenCaseAsync`, whose comment used to promise "3b's
transaction".

> **5.00 note (2026-10-31, not re-decided).** A `TransactionScope` does work with nop's data provider: CW.Core's own
> `PackNameTransaction.Begin` (`Services/PackNames/PackNameTransaction.cs:22`, read committed) wraps the packname
> registration, import and bulk writes, and nop 5.00 adds `BaseDataProvider.CreateTransactionScope()`
> (`Libraries/Nop.Data/DataProviders/BaseDataProvider.cs:503`). The approval still relies on compensation
> (`ReturnCaseService.cs:245`). Whether to move it into a transaction is open.

**The rest of the phase, fixes 23-30:** the case resolution, the approval and its undo all serialise on
two keys (`cw_returncase_resolve_{id}`, `cw_returnrequest_approve_{id}`); a refused approval reports an
error so the controller does not advance the status, and a refused reopen is **retried three times** and
then logged as an error rather than silently skipped — proceeding would price the save from a resolution
that should have been voided. The staff fee quantities are clamped server-side to the returned quantity,
a cap that until now lived only in the browser. And `ReturnsController`'s undo handler no longer
dereferences a null `returnRequest` on the not-found branch, which was a guaranteed
`NullReferenceException` escaping the `NopException` catch as a 500.

**Lock ordering** is `approval → packname → case`; nothing under the case lock acquires anything, so there
is no cycle, and both lockers release in a `finally` so a throwing action still frees the key.

**Validated live** 2026-09-11: two saves entering the approval workflow in the same millisecond, each
reopening a resolved case, each raising its own credit note, produced exactly one resolution and one
stand-down. A double-clicked approval raised one credit note and refused the second save.

---

**2026-09-11 — Phase 4b-4: the replacement picker refuses what it cannot specify, and a packname
checkout stops deleting cart lines it never ordered.** Two defects, both closed the same day phase 4b-3
was signed off, both found by following 4b-3's own findings upstream. Detail and the validation record:
`CWPlans/Exchanges_Rebuild_Record.md` §4 (phase 4b-4) and §7.5.

**Fix 21 — the picker.** It rendered only attributes it could draw as a dropdown, silently dropped the
rest, and let the customer choose the product anyway; the required free-text, date or upload attribute was
never collected, so the exchange could not complete on either pathway. **4.1 has the same blind spot** —
its picker renders at most two attributes and its server reads only value ids
(`NOP41 …/ExchangesController.cs:298-319`), so a free-text attribute can never get in by construction. It
never bit live because 4.1's picker is fed by a curated company catalogue, not the whole product table.

The decision (28) was therefore to keep the product **visible** and refuse the selection with its reason,
rather than hide it — hiding reads as the product being out of stock, and 4.1 offers only *"Product
selection is not valid"* with no reason at all. `ProductsHelper.FindUncollectableRequiredAttributes`
mirrors exactly what `_ReplacementProductAttrs.cshtml` renders and names everything else that is required;
the factory adds rental periods and gift-card details, which the picker has never had fields for. One
model property and one resource string feed both the static notice and the click-time error, so they
cannot disagree. Both entry points funnel through `chooseProduct`, so one guard covers both.

⚠️ **The helper and the view must be changed together.** The helper's whole correctness is that it mirrors
the view's two `Values.Any()` filters; if the view starts rendering something the helper still counts as
uncollectable, the picker refuses a product it can in fact specify.

Two bugs found while reviewing it: `ExchangesHelper.HasProductAttributes` tested `Count >= 0` — true for
every non-null list — so an attribute-free product rendered an empty block carrying *"The product does not
contain attributes that can be used"* (inherited verbatim from 4.1, now gone); and a valueless attribute
named "Colour…" rendered a dropdown containing only `- Select -`, telling the customer to choose an option
that did not exist.

**Conditional attributes are deliberately not blocking** — nop enforces one only when its controlling
attribute is set, and the picker does not evaluate conditions.

**Still open, narrow:** this is a UI refusal. A hand-crafted POST can still submit a blocked replacement;
fix 20 stops it becoming a bad order, but the case sits there for support to decline. Closing it means a
`MustAsync` rule in `ReturnWizardSubmissionValidator`. Recorded in the Record §7.5. *(Closed in the 5.00 port,
phase 5g: `ReturnSubmissionLogic.IsReplacementValidAsync` refuses a replacement without a value for every
required option — see the 2026-10-31 entry.)*

**Fix 22 — the packname checkout** (gap analysis **P-02**, now closed).
`MovePackNameShoppingCartItemsToOrderItemsAsync` ended with `ClearShoppingCartAsync`, which deletes every
cart row for the customer and store. The order is built from `details.PackNamesCart`, which comes from the
`PackNameShoppingCartItem` **view** — INNER JOINs through the packname cart link, the packname, its list,
its branch and `SubsidyPackNameCanOrder`, filtered to `CanOrder = 1` (`CWDatabaseService.cs:798`; `:884` in 5.00). Any row
the view excluded was never ordered and was deleted anyway: the customer paid for what they bought and
silently lost the rest of their cart. It now deletes exactly the rows that became order items, which is
what 4.6 did (`OrderProcessingServiceOverride.cs:1134`).

⚠️ **Use the id overload of `DeleteShoppingCartItemAsync`.** `sc` is a view entity; the entity overload
would aim the delete at the view. `resetCheckoutData` is false because `placeOrder` calls
`ResetCheckoutDataAsync` immediately afterwards with `clearCouponCodes` and `clearCheckoutAttributes`.

`ClearShoppingCartEvent` is no longer published on this path — nothing in `Nop.Plugin.CW.Core` consumes it,
`EntityDeletedEvent<ShoppingCartItem>` still fires per row, and `Customer.HasShoppingCartItems` is still
maintained. Same trade fix 17 made on the return-order path.

**Unresolved and separate:** nothing deletes the `CORE_PN_PackNameCartItem` link row when its
`ShoppingCartItem` goes, on either path, and no consumer does it. Whether the FK cascades was **not**
confirmed. If it does not, that table has been accumulating orphans on every checkout since day one.
*(Checked 2026-10-31: it cascades. `PackNameCartItemBuilder.cs:21` declares `ForeignKey<ShoppingCartItem>()`,
whose default is `onDelete = Rule.Cascade` (`Libraries/Nop.Data/Extensions/FluentMigratorExtensions.cs:106`), so
the database deletes the link row with its cart line.)*

---

**2026-09-11 — Phase 4b-3: the pathway decision asks the settlement pipeline, and an auto-placed exchange
can be paid by the restored entitlement.** The everyday case — faulty garment, no fees, one unit back, one
unit out — is now placed outright instead of being left in the customer's cart at $0. Detail and the
validation record: `CWPlans/Exchanges_Rebuild_Record.md` §4 (phase 4b-3).

`IOrderProcessingOverrideService.QuoteReturnOrderSettlementAsync` walks the pipeline against the same
packname cart and detached template `PlaceReturnOrderAsync` will settle, so `BalanceDue == 0` is a promise
rather than an estimate; `BuildReturnOrderPackNameCartAsync` projects that template onto the packname the
**returned order item** carries, so a manager's order charges the employee whose line came back.

⚠️ **Three invariants, each silent if broken:** the projected cart items keep `Id == 0` (the price consumer
branches on it to choose between the scope and a saved row's link — a synthetic id re-prices the
replacement live and undoes fixes 6, 8 and 11); the order-item map is therefore keyed on 0 and now throws
rather than overwriting if a return-derived cart is ever more than one line; and an empty `SettlementPlan`
reads as fully covered, so the decision requires the quote to have priced something before trusting it.

Six fixes came out of review and the run. Two were **not this work** — ordinary checkout, every store:

- ⚠️ **A customer with no `Customer.BillingAddressId` could never check out.** The packname checkout screen
  resolved an address through a four-step fallback and posted it; `PlaceOrderAsync` read only the column,
  which nothing but nop's stock one-page checkout ever writes. Fixed by overriding
  `PrepareAndValidateBillingAddressAsync` to resolve from the customer's own addresses and **persist** the
  result. Seeded data masks this completely.
- **The checkout screen and the placed order could bill different addresses** — `GetCustomerDefaultAddress`
  ran its email/name matcher before consulting the address of record. The record now wins.

And four in this area:

- **`BuildCartSettlementContextAsync` settled a detached cart against the customer's SAVED cart**, so a
  return order's total was netted by subsidy allocated against whatever else was in their basket. It now
  short-circuits on a detached cart, the same `Id == 0` test `GetShoppingCartShippingTotalAsync` uses.
- **The auto-placed order's total was the pre-subsidy figure**, now corrected from the plan actually
  applied. An invariant logs loudly if a quoted-as-covered order settles with a balance, because
  `SaveReturnOrderDetailsAsync` marks these Paid unconditionally.
- ⚠️ **`MoveShoppingCartItemsToOrderItemsAsync` cleared the customer's ENTIRE cart** — the order is built
  from a synthetic cart that was never in the cart, so there was nothing of its own to clear, and the wipe
  took whatever they were buying plus other approved replacements whose rows carry the `ReturnRequestItem`
  link. Now scoped to this return's own rows, matching `UndoApprovedExchangedItemsAsync`. **The packname
  placement path still has the same bug** — gap analysis **P-02**.
- **An auto-placed replacement had no `OrderShipping` row**, so no ship-to reached the destinations block,
  the invoice, the packing slip or the confirmation email (decision 13).

✅ **Runtime verification passed 2026-09-11** — orders 113–120, returns #89–#93. Auto-place at $0.00 with
the destination recorded; fees still route to the cart and complete there; a catalogue price change after
purchase does not follow the customer (decision 24, exercised through the auto-place path for the first
time); a manager's order charges the employee's packname and not the manager's; and a decoy cart row
planted before approval survived. Not run: a note-funded no-fee exchange, and the two Different Style
cases. Not reachable on this store: a non-packname original.

**2026-09-11 — Phase 4b applied and validated; two checkout defects fixed along the way.** Subsidy now
settles before credit notes, an authorised line gives its value back **in kind** before the note is sized
or the replacement is carted, and the replacement draws on the restored entitlement. Detail and the
validation record: `CWPlans/Exchanges_Rebuild_Record.md` §4 (phase 4b) and §8 (the arithmetic, rewritten).
Thirteen fixes came out of validation. The three that are design rather than repair:

- **An Exact Exchange is priced at what the customer paid** (decision 24), not the live qty-1 price — so a
  tier-priced line exchanges at its tier price and a later catalogue change does not follow the customer.
  `ExchangeReplacementScope` (`AsyncLocal`) carries `(ReturnRequestItemId, ReturnedOrderItemId,
  ExchangeType)` into `GetShoppingCartItemUnitPriceEvent`, which sets `StopProcessing`. ⚠️ **Three passes
  were needed** — persisted cart rows, the detached cart in `PlaceReturnOrderAsync`, and the pathway A/B
  decision in `ExecuteExchangeWorkflowAsync`, which prices a detached template *before* any scope opens.
  The scope now wraps that whole method, which is the outermost point a replacement is priced.
- **A product's minimum and allowed quantities never block a partial exchange** (decision 25). The
  customer already bought the 2, 4 or 6 the rule demands. Exempted at the approval's add **and** in
  `GetStandardWarningsAsync`, because nop revalidates the cart at checkout and would otherwise strand the
  customer after approval had succeeded.
- **Partial restoration budgets units and money against one shared returned-value budget** — units first,
  money only for the value the units did not cover. It previously budgeted the two independently and paid
  a partial return back twice (one whole unit *and* a pro-rata slice of money). `Record` §8.

Two fixes were **not this work at all** — both are ordinary checkout, both hit every store:

- ⚠️ **A customer with no `Customer.BillingAddressId` could never check out.**
  `PackNameCheckoutModelFactory.GetCustomerDefaultAddress` resolves an address through a four-step
  fallback, renders it and posts it as `BillingAddressId`; `PlaceOrderAsync` never reads that posted
  value and falls through to nop's `PrepareAndValidateBillingAddressAsync`, which throws on
  `details.Customer.BillingAddressId is null` alone — and nothing but nop's stock one-page checkout ever
  writes that column, which a packname store does not use. Fixed by overriding the method in
  `OrderProcessingOverrideService` to resolve from the customer's own addresses and **persist** the result,
  so the state self-heals. Covers packname checkout, standard checkout and the auto-placed exchange order
  in one place. **Seeded test data masks this completely** — every seeded customer already has the column.
- **The checkout screen and the placed order could bill different addresses.** Pre-existing:
  `GetCustomerDefaultAddress` ran its email/name matcher *before* consulting `customer.BillingAddressId`,
  and with two matching addresses the matcher returns the newest — which need not be the one nop bills.
  The address of record now wins whenever it is still one of the customer's addresses. Silent divergence:
  neither side warned.

✅ **Runtime verification passed 2026-09-11** — twenty-three scenarios, orders 81–112, return cases 73–88,
live store at 10% GST. Includes the two that had never run before: subsidised shipping (goods restored,
freight not — decision 21) and a quantity-only subsidy (a unit covers the full price; authorisation
raises no note and does not throw).

**2026-09-10 — Phase 4b specified: subsidy first, value back in kind, replacement draws on it.** Spec,
decisions 15–22 and change set: `CWPlans/Exchanges_Rebuild_Record.md` §3 and §4 (the proposal and the
exact-change spec were both consumed into it on 2026-09-11). What it changes: `SubsidySettlementSource`
priority 1 / `CreditNoteSettlementSource` 2; `RestoreSubsidyForReturnedItemAsync` runs on every authorised
line, both modes, before the note and before the cart, and returns a `SubsidyRestoreResult`; the credit
note is the returned value minus `MoneyEquivalent`; `IsCreditNoteNeeded` is rules-or-exchange (the
cheaper-replacement clause and the replacement-price cap are gone); the returned-item skip in
`SubsidyAllocationService.AllocateGoodsAsync` is deleted and its `IExchangeService` dependency with it;
pathway A is gated on nothing having been restored until 4b-3; the approval screen warns when a
Credit-Note-mode line has fees but no money to deduct them from. **Three findings corrected in the docs
the same day:** 4.1 spent credit notes first at the money level but consumed product-group buckets
regardless (`Legacy…` §7.1); pathway B in 4.1 does apply subsidy at checkout (`Legacy…` §7.2); and the
live 4.1 rules raise a full-price credit note on every exchange (`Legacy…` §9, questions §12). Reference
§1, §8.2, §9.1 rewritten; functional guide §3.1, §5, §6.3, §9 rewritten. Gap analysis C-09 and S-17
closed. `subsidy-system.md` §6, §8, §14 and `CLAUDE.md` §14.5 carry the new order.

**2026-08-27 — Dead code removed from the return case aggregate.** Four **write-only** columns and their
writes: `ReturnCaseLine.ReplacementUnitPrice`, `ReturnCase.UploadedFileId` (nop's own
`ReturnRequest.UploadedFileId`, which the upload feature actually uses, is untouched),
`ReturnResolution.ExchangeOrderId` and `.CreditNoteId` (only ever assigned `null`). Dropped by migration
`ReturnCaseDeadColumnsCleanup` (in 5.00 the columns are simply absent from `SchemaMigration`), whose `Down()` recreates them **nullable with no default** — deliberately
unlike the phase 5 cleanup, whose `WithDefaultValue(0)` binds a constraint that then blocks a re-drop.
Also removed seven service members with no callers anywhere, `.cshtml` included: six on
`IReturnCaseService` (`GetReturnCasesByOrderIdAsync`, `GetReturnCaseByReturnRequestIdAsync`,
`CancelReturnCaseAsync`, `GetReturnCaseLineByIdAsync`, `DeleteReturnCaseLineAsync`,
`DeleteReturnCaseLinesAsync`) and `IExchangeService.GetReturnRequestItemForOrderItemAsync`. ⚠️ **The two
`Delete*` members were the aggregate's only line-removal API** — if phase 3a's re-open path ever needs to
drop a line, it will need one of them back. `plugin.json` → `2026_08_27_DATA_0.4`.

**2026-08-10 — Redesign phase 0 complete: reason/action by id, plus dead-code removal.** Spec:
`CWPlans/Exchanges_Rebuild_Record.md` §4. Validated against the applied code.
✅ **Runtime verification passed 2026-08-27**, jointly with phase 1e — that record's §3.

*Phase 0a — deletions and small fixes:*

- `ExchangeService.OrderItemIsExchangedItem` deleted — dead (zero callers) **and** broken: a missing
  `await` meant it compared a non-null `Task` and always returned `true`.
- `RegisterReturnRequestItem` → `RegisterReturnRequestItemAsync`, replacing a synchronous `Insert` in an
  async-first codebase; the controller helper became async with it.
- `CreditNotesReportsModelFactory` + interface + DI registration deleted — registered but never called,
  and a near-verbatim duplicate of `CreditNotesModelFactory.PrepareCreditNotesReportModelAsync`.
- `CreditNotesController`'s `#region Methods OLD` deleted, along with the two usings it alone needed.

*Phase 0b — reason/action stored as ids:*

`ReturnRequestItem` gained `ReturnReasonId` / `ReturnActionId` (`NOT NULL`), and every behavioural lookup
now resolves from them instead of matching nop's localized display strings. Reference doc §5.2.1 states
the resulting rule.

The bug being fixed had three independent triggers, all silent: a customer browsing in a non-default
language stored a translated string that never matched; renaming a Return Reason orphaned every historical
return that used it; and the admin edit screen persisted whatever free text was typed into it. The
consequences ranged from fees quietly vanishing from cart totals to a `NullReferenceException` in
`GetFeeRulesForActionAndReasonAsync`, which dereferenced both rules unguarded.

Notable decisions and findings:

- **No backfill.** This targets a new, empty database; migrating from the old nopCommerce 4.1 system is a
  separate cross-version exercise that will map ids as part of its own transformation. That also allowed
  `NOT NULL` rather than a nullable, legacy-tolerant column. *(Phase 1's `ReturnCase` will likely need
  these nullable, since a `Draft` case exists before the customer picks a reason.)*
- **Deleted reasons/actions dangle by design.** `NOT NULL` guarantees the column is populated, not that
  its target still exists; every consumer treats an unresolvable id as "no rule configured" and fails
  closed with a warning log. No FK constraints — one would make nop's own admin delete fail. Cleaning up
  the orphaned CW rule rows is roadmap item **B7**.
- **Admin free text → dropdowns.** The only way the id and the display string cannot diverge. This woke
  two latent bugs that were dormant purely because the view never rendered the existing
  `ReturnReasonsList`/`ReturnActionsList`: they were built with `"StoreId"` as the value field (a property
  neither entity has — enumerating the `SelectList` throws), and an unguarded `actions.FirstOrDefault`
  on a null list. The view was also posting `ReasonForReturn` twice, via a stray hidden input outside the
  `if (allowEdit)` block; it worked only because the model binder takes the first value.
- **Validation added.** There was no server-side rule on reason or action at all — the old inputs relied
  on `asp-required`, which is client-side only. With dropdowns an unselected value binds as `0`, which
  would have saved everything except the reason.
- **An ordering claim in the spec was wrong and got corrected before implementation.** It proposed running
  `UpdateExchangeReturnDataAsync` before the status workflow. That would have broken
  `EditExchangeRequest`'s deliberate all-or-nothing design, where the workflow runs first and every
  persistence step sits behind a `successfulWorkflow` flag so a failed approval commits nothing. The real
  issue — the workflow evaluating rules against the *stored* reason when an admin changes reason and
  status in one save — was pre-existing, and is now fixed the way the codebase already solved it for staff
  quantities: by carrying the values through `ReturnRequestStatusChangeData` and overlaying them in memory.
- **`ValidFeesAmountAsync` needed the same overlay**, or the "fees exceed the credit note" check would have
  validated against the old reason's fee rules.

This leaves **three** copies of the same "overlay unsaved admin edits onto the loaded item" pattern —
recorded in reference doc §10.3 and folded into roadmap item **B2**, whose planner collapses them.

**2026-08-10 — Structural review of Exchanges; `isReturnOrder` claim corrected.** A full read of
`ExchangesLogic`, `ExchangeService`, `ExchangesController` and both order-placement pipelines produced
`CWPlans/Exchanges_Rebuild_Record.md` §1 — a diagnosis of six structural problems plus a proposed target
model and phased sequencing. Nothing implemented; decisions still open (proposal §7).

Two corrections to the reference doc came out of it, both verified by repo-wide grep:

- **There is no `isReturnOrder` guard.** The integration plan claimed one existed to stop return-derived
  orders consuming subsidy; the identifier does not appear anywhere in the codebase. Reference doc §9.1
  corrected.
- **Pathway B exchanges consume subsidy twice.** Because Pathway B routes the replacement through the
  normal cart and therefore the normal `PlacePackNameOrderAsync` pipeline, `SubsidyOrderItem` rows are
  written for the replacement item, with no compensating reversal (`RefundSubsidyForReturnAsync` is a
  stub). Pathway A has the opposite behaviour — `PlaceReturnOrderAsync` never touches subsidy at all. The
  same business event therefore resolves two different ways depending on which pathway it takes. Recorded
  as a warning in reference doc §9.1; the structural fix is proposal phases 4–5.

Also noted while reading, and folded into the proposal rather than the reference doc: return reason/action
are matched by **localized display name** rather than ID (`GetSelectedReasonAsync`), which both breaks for
non-default-language customers and breaks historically whenever a reason is renamed — with an unguarded
null dereference downstream in `GetFeeRulesForActionAndReasonAsync`.

**2026-08-10 — Reference doc split into current-state reference + this dev-notes/changelog file.**
`Exchanges_Returns_CreditNotes_Reference.md` now describes only present-tense current state (no dates,
"as of", or design rationale); all historical narrative, design evaluations, and the changelog itself
moved here. Going forward, both files are the input for planning further work in this area — see
"Purpose & how to use this file" at the top of this document for the maintenance workflow.

**2026-08-10 — `CreditNotesService` read methods renamed and extended caching applied.** See §5 above for
the full writeup. `GetCreditNotesByCustomerIdAsync(customerId)` → `GetOpenCreditNotesByCustomerIdAsync(customerId)`;
`GetCreditNotesByCustomerIdAsync(customerId, pageIndex, pageSize)` → `SearchCreditNotesByCustomerIdAsync(...)`.
`GetCreditNoteByIdAsync` now cached per-request (`IShortTermCacheManager`); `CustomerHasCreditNotesAsync`
now cached persistently (`IStaticCacheManager`); both invalidated via the same `CreditNoteCacheEventConsumer`/
`CreditNoteHistoryCacheEventConsumer` pair from the previous entry.

**2026-08-10 — Credit Note balance read cached via `IShortTermCacheManager`.** See §4 above for the full
writeup. `CreditNotesService`'s open-notes (`Remain > 0`) list is now cached for the duration of the
request; the "has balance" check was folded into it instead of running a separate query. Invalidated
within the same request via two new classes, `CreditNoteCacheEventConsumer`/`CreditNoteHistoryCacheEventConsumer`,
targeting the specific customer on every `CreditNote`/`CreditNoteHistory` write.

**2026-08-07 — `BusinessLogic/` classes formalised as an interfaced layer.** See §1 above for the full
writeup. `ExchangesLogic`/`CustomersLogic` now implement `IExchangesLogic`/`ICustomersLogic`; `CWOrderLogic`
(not a genuine orchestrator) was deleted and its sole call site inlined. Codified in `CLAUDE.md` §6.13.

**2026-08-07 — Live-computed Credit Note balances, issuance tracking, concurrency guards.** See §2 and §3
above for the full design writeup and the concurrency-mechanism validation.

- `CreditNote.Remain` and `CreditNoteHistory.Remain` are no longer stored/mutable columns. Both are
  `[NotColumn]` properties populated at read time from two new SQL views — `vw_CORE_EX_CreditNoteRemain`
  (header balance) and `vw_CORE_EX_CreditNoteHistoryRemain` (per-event running balance) — created via
  `CWDatabaseService.UpdateDatabaseScriptsAsync()`, the same mechanism Subsidy's views use, not via
  FluentMigrator. Both views exclude any `CreditNoteHistory` row whose order is cancelled
  (`OrderStatusId = 40`) from their running totals, mirroring Subsidy's `vw_CORE_SUB_SubsidyPackNameCredit`
  exactly.
- `CreditNote` gained `CreatedByCustomerId` (nullable) — the "who" for a credit note, paired with the
  existing `ReturnReason` column as "why". Populated for manual admin-issued notes
  (`CreateManualCreditNoteAsync`) and for the returns workflow (`ExchangesLogic.ExecuteCreditNoteWorkflowAsync`
  now passes the acting `_workContext` customer through to `CreateCreditNotesForOrderAsync`).
- `CreditNotesLogic.cs` — originally planned to become a no-op `UndoCreditNotesForOrderAsync` method
  (mirroring `SubsidyCreditService.UndoSubsidyForOrderAsync`) — was **deleted outright** instead of kept
  as a no-op shell, since it no longer did anything and had no remaining callers once the "undo on cancel"
  behaviour became unnecessary (confirmed via repo-wide grep). Functionally equivalent to the original
  plan (order cancellation now needs zero explicit code on the Credit Notes side, exactly as it already
  didn't for Subsidy), just without leaving an empty class around.
- `ApplyCreditNotesForOrderAsync` (Credit Notes) and `MovePackNameShoppingCartItemsToOrderItemsAsync`
  (Subsidy, in `OrderProcessingOverrideService`) both now wrap their
  read-available-balance→decide→insert-ledger-rows sequence in `Nop.Core.Caching.ILocker`
  (`PerformActionWithLockAsync`) — see §3 for why `ILocker` was chosen over a hand-rolled `SemaphoreSlim`.
- Net effect: Credit Notes now needs no explicit "undo" step on order cancellation, the same as Subsidy
  already didn't (via view-exclusion) — the two systems that used to behave differently on cancellation
  now behave identically. See the reference doc's §9.6 for the current-state explanation of why the
  *return/exchange* case (as opposed to cancellation) is still a real, separate gap for Subsidy.

The step-by-step implementation guide originally used to apply this change
(`CWPlans/Implementation_Live_CreditNote_Balances.md`) was removed once the change was applied and
validated — this changelog entry plus the reference doc's §5/§6 are the durable record.

---

### Customer return wizard — repair and rework

The exchanges redesign was paused after Phase 1c to fix the customer-facing wizard, which had been ported
from 4.1 incompletely and did not work. That work is complete and is recorded in full in
**`CWPlans/Deprecated/Exchanges_Wizard_Fixes_And_Improvements.md`** — the defect history, the phased improvements, the
architecture moves and the query-count reductions. Twenty working documents were merged into it and
deleted. Affects reference doc §7 (public store workflow).

The parts worth carrying forward into future work in this area:

**One defect family caused five separate bugs.** `virtual` EF navigation properties survived the port into
LinqToDB, which neither cascades inserts nor lazy-loads. `ReturnRequestItem.ReplacementItem` was never
persisted at all — `CORE_EX_ReturnReplacementItem` was empty and eight read sites were dead, including one
that built the replacement cart on approval, so approved exchanges created orders with no size or colour.
**A sweep for `virtual` nav properties across CW entities is the highest-value follow-up from this work**
and has not been done.

**The port dropped JavaScript while keeping markup**, three times (picker, confirmation panel, Edit
Exchange Item). In each case the feature looked implemented and did nothing. Worth checking for whenever a
4.1 view is being compared against its 4.9 counterpart.

**4.1's picker had the same scaling defect we found**, so nothing was lost in the port:
`GetCompanyProducts` wraps `SearchProducts` with `pageSize = int.MaxValue` and its paging parameters are
never passed. What protects 4.1 in production is data shape — each client store is a uniform range of tens
of products. Recorded because the same assumption may be hiding elsewhere in the ported code.

**Two obsolete-API traps** were recorded as lessons rather than left as comments:
`ProductAttributeValue.PictureId` is obsolete since nop 4.70 and silently returns null on stores created
since (lesson #7), and JSON wrapped in `Html.Raw` inside an HTML attribute breaks the attribute
(lesson #6).

**Decisions taken that future work should not re-litigate:**

- `IExchangesDisplayService` holds shared, DI-needing *read* helpers (`GetPacknameAsync`,
  `GetProductThumbUrlAsync`, `GetSelectedAttributeMapAsync`, `GetReturnRequestExtraInfoAsync`). These fit
  neither §6.13's definition of business logic nor a factory. Three of them in `ExchangesLogic` was
  tolerable; five would have been drift.
- Absence is `null`, not `"N/A"`. Factories no longer invent placeholders; views decide how to render
  absence, usually by hiding the row. Two values keep a default deliberately: a missing store says so
  explicitly (it indicates deleted data, not absence), and fee type/operator names fall back to
  `"Unknown"` because a blank in an admin dropdown reads as "not configured".
- Fees are previewed as **rules**, not totals, before the summary. Quantities are not final until then, so
  a total would be a guess presented as fact.
- `ExecuteExchangeWorkflowAsync` (101 lines) was left unsplit: separating it would move rollback logic away
  from the operations it compensates for.
