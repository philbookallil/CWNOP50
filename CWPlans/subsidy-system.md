# CW Subsidy System — Reference Document

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

> **Purpose**: Complete reference for the subsidy system as implemented in `Nop.Plugin.CW.Core`.
> Covers domain model, business rules, public store behaviour, flag semantics, ordering logic,
> and the key service/view layer. Keep this document updated when behaviour changes.
>
> **Last reconciled with the code: 2026-10-21** (documentation consolidation — every section re-read against
> `Nop.Plugin.CW.Core`; the phase documents it drew on are consumed). Last behaviour change 2026-10-20 (credit read once
> per request, the period functions inlined, `datetime2` timestamps — §3, §4, §9). Re-checked against the 5.00 code on
> 2026-10-31: the port changed the schema's delivery (one `Data/Migrations/SchemaMigration.cs`), My Subsidies' history
> (by season), the top-up saves (one transaction each) and the bar before the checkout (§8.2); the SQL in
> `CWDatabaseService` is unchanged. How the port went is `CWCore_Port_Reference.md`. The items still open are in
> `Subsidy_PackName_Open_Items.md`; the 4.9 gap analysis is history (`Deprecated/Subsidy_PackName_Gap_Analysis_And_Known_Issues.md`).
> Where this document and the code disagree, the code wins and this document is the bug — 4.9 lesson #27
> (`tasks/lessons-4.9.md`).
>
> A bare "phase N" in this document is a phase of the 4.9 subsidy / packname fix strategy
> (`Deprecated/Subsidy_PackName_Fix_Strategy.md`), and "decision N" one of its decisions; the port's own parts are
> named "the port's part 5b" and so on.

---

## 1. Overview

Subsidies allow Total Image Group to fund part (or all) of a PackName's order on their behalf.
A subsidy defines a pool of credit and/or quantity that a PackName can draw from during a
configured period. The system tracks exactly which credit source was consumed per order item
and per shipping line, and enforces catalogue and quantity restrictions at the public store.

**The subsidy switch.** Everything in this document applies only to a store with subsidies switched on
(`CWFeatureSet.Subsidy`: `SubsidyEnable` and `PackNameEnable`). With subsidies off, subsidy rows have **no
effect**: no credit, no catalogue restriction, no quantity cap, no ordering-period block, no hidden cart
lines. Order-attached records (what an order consumed) stay visible and cancellations still return credit.
C# asks `ICWFeatureService`; the three **ordering** views (`vw_CORE_SUB_SubsidyInOrderingPeriod`,
`vw_CORE_SUB_SubsidyVariationInOrderingPeriod`, `vw_CORE_SUB_SubsidyPackNameCanOrder`) join
`vw_CORE_SUB_StoreSubsidyVariationSetting.SubsidyEnabled = 1`, so a store with subsidies off has no in-period subsidy
and every packname `CanOrder`. The two **credit** views do not read the switch — they still compute credit — and
every C# reader of credit is gated itself (`SubsidySettlementSource.IsEnabledAsync`, the cart service's cap and
allocation, the catalogue, the admin panel); a new reader of `ISubsidyCreditService` must check `ICWFeatureService` too.
Switching takes effect at once: any `multistoresettings.*` write clears the feature cache and the cached
subsidy ordering results (in-ordering subsidies, ordering block, allocated products, credit) —
`SettingCacheEventConsumer`. The rule, and where each reader is switched off, is `Feature_Toggles_Reference.md` §1.5. **Any new reader
of subsidy data — C#, view, function or script — is checked against it before it is written.**

---

## 2. Core Concepts and Domain Entities

### 2.1 Product Group (`CORE_SUB_ProductGroup`)

A Product Group is a named collection of products. It is store-scoped and carries display
metadata for the public store subsidy bar and catalogue.

| Field | Description |
|---|---|
| `StoreId` | The store the group belongs to |
| `Name` | Display name |
| `Description` | Optional description |
| `Icon` | Symbol id in the plugin's `Content/icons/pg-icons.svg` sprite (e.g. `pg-icon-Shirts`); `ISubsidyService.GetProductGroupIconsAsync` lists them, cached |
| `Colour` | Hex colour for the icon in the subsidy bar. The admin form saves it as typed; `PgIcon` draws only a symbol id and a hex colour (`PackNameColourHelper.Normalize`) and leaves anything else out |
| `DisplayOrder` | Sort order in the subsidy bar |
| `Active` / `Deleted` | Soft-delete lifecycle |

Products are linked via `ProductGroupProduct` (`CORE_SUB_ProductGroupProduct`), which is a
simple join: `ProductGroupId → ProductId`.

Product Groups are reusable across subsidies. The same group (e.g. "Shirts") can be mapped
to multiple subsidies with different credit/quantity allowances in each.

### 2.2 Subsidy (`CORE_SUB_Subsidy`)

A Subsidy is the top-level funding rule. It is store-scoped.

| Field | Type | Description |
|---|---|---|
| `Name` | string | Admin label |
| `Description` | string | Notes |
| `Credit` | decimal | Global credit pool available to each PackName |
| `DurationStart` | DateTime | Period start. **Required** — `NOT NULL` since 2026-09-29 |
| `DurationEnd` | DateTime | Period end. **Required** — `NOT NULL` since 2026-09-29 |
| `AnnualRecurrence` | bool | If true, the start/end dates repeat every year |
| `OrderOutsidePeriod` | bool | Allow ordering when outside the period (but without subsidy credit) |
| `OnlyAllocatedProducts` | bool | Restrict the PackName to see/order only products in its product groups |
| `IncludeManagers` | bool | Apply `OnlyAllocatedProducts` and `RestrictToAllocatedQty` to manager PackNames too |
| `RestrictToAllocatedQty` | bool | Prevent ordering more units than the product-group quantity allocation |
| `Active` / `Deleted` | bool | Soft-delete lifecycle |

### 2.3 SubsidyProductGroup (`CORE_SUB_SubsidyProductGroup`)

Maps a Product Group into a Subsidy and adds per-subsidy credit and quantity allowances.

| Field | Type | Description |
|---|---|---|
| `SubsidyId` | int | Parent subsidy |
| `ProductGroupId` | int | The product group being linked |
| `Quantity` | int | Max units a PackName can order from this group using subsidy |
| `Credit` | decimal | Credit available for products in this group (`0 … 1,000,000`) |
| `DisplayOrder` | int | Sort order |
| `CreatorId` / `CreatedOnUtc` / `Deleted` | | Who allocated the group, when; soft-deleted on removal |

One live allocation per (subsidy, product group), enforced by a filtered unique index on `Deleted = 0`
(`UX_CORE_SUB_SubsidyProductGroup_SubsidyId_ProductGroupId`, created by `SchemaMigration.CreateLiveRowsUniqueIndex`;
4.9's migration `SubsidyAssignmentUniqueIndexes`) — the only
guard that holds under concurrent posts. A group removed and re-added is therefore two rows, one deleted.

### 2.4 SubsidyPackName (`CORE_SUB_SubsidyPackName`)

The assignment of a Subsidy to a specific PackName. All overrides (variation, topups) hang
off this record.

| Field | Type | Description |
|---|---|---|
| `PackNameId` | int | Target PackName |
| `SubsidyId` | int | The subsidy being assigned |
| `CreatorId` / `CreatedOnUtc` | | Who assigned it, when |
| `Active` / `Deleted` | bool | Can be deactivated without removing the assignment |

One live assignment per (subsidy, packname), enforced the same way (`UX_CORE_SUB_SubsidyPackName_SubsidyId_PackNameId`,
filtered on `Deleted = 0`). Re-assigning a subsidy to a packname is a new row — its top-ups start from zero, which is
the only way to reset them (§8.5).

### 2.5 SubsidyVariation (`CORE_SUB_SubsidyVariation`)

Overrides the global credit amount and/or period dates for a specific `SubsidyPackName`.
Only one live variation can exist per `SubsidyPackName` (`UX_CORE_SUB_SubsidyVariation_SubsidyPackNameId`, filtered on
`Deleted = 0`). Every save of a variation also writes a `SubsidyVariationHistory` row
(`CORE_SUB_SubsidyVariationHistory`: `SubsidyVariationId`, `Credit`, `DurationStart`, `DurationEnd`, `Reason`,
`CreatorId`, `CreatedOnUtc`), which is what the history procedure reads (§12.1).

| Field | Type | Description |
|---|---|---|
| `SubsidyPackNameId` | int | The specific PackName assignment |
| `Credit` | decimal? | If set, replaces the subsidy's global credit for this PackName (`0 … 1,000,000`) |
| `DurationStart` | DateTime? | If set, replaces the subsidy's start date for this PackName |
| `DurationEnd` | DateTime? | If set, replaces the subsidy's end date for this PackName — both dates or neither, end after start |
| `CreatedOnUtc` / `Deleted` | | |

Whether variations are applied is controlled by two store-level settings (`MultiStoreSettings`):

- **`PackNamesOwnGlobalValue`** — when `true`, each PackName's effective credit comes from
  `SubsidyVariation.Credit` (if a variation exists) rather than `Subsidy.Credit`.
- **`PackNamesOwnStartAndFinishDates`** — when `true`, a PackName's ordering period is
  determined by the variation's `DurationStart`/`DurationEnd` instead of the subsidy's dates.

**Critical: both settings are enforced entirely at the SQL view level — not in C# service code.**

`PackNamesOwnGlobalValue` is enforced in `vw_CORE_SUB_SubsidyPackNameCredit`:
```sql
CASE WHEN vset.PackNamesOwnGlobalValue = 1
     THEN COALESCE(svar.Credit, s.Credit)
     ELSE s.Credit
END AS Credit
```
When the setting is OFF, `s.Credit` (the base subsidy credit) is always used, regardless of any
`SubsidyVariation` record. When ON, the variation credit is used if present, otherwise falls
back to the base.

`PackNamesOwnStartAndFinishDates` is enforced in `vw_CORE_SUB_SubsidyVariationInOrderingPeriod`:
```sql
INNER JOIN vw_CORE_SUB_StoreSubsidyVariationSetting vset
        ON s.StoreId = vset.StoreId
       AND vset.PackNamesOwnStartAndFinishDates = 1
```
When the setting is OFF, this view returns **no rows**. Callers that LEFT JOIN against it
automatically fall back to the base subsidy period. This means the date-override behaviour is
completely transparent to the C# layer — it sees the right period regardless of the setting.

The settings are surfaced as a SQL view (`vw_CORE_SUB_StoreSubsidyVariationSetting`,
entity: `StoreSubsidyVariationSetting`) that is joined into the credit and ordering-period views.

### 2.6 SubsidyTopup (`CORE_SUB_SubsidyTopup`)

Adds extra global credit for a specific `SubsidyPackName`, beyond the base or variation credit.

**A top-up save inserts a row; it never reads or updates one.** Entering "50" adds a `SubsidyTopup` row with
`Credit = 50` and a `SubsidyTopupHistory` row with the same amount, the reason and the creator
(`SaveSubsidyTopupForPeriodAsync`). The credit views `SUM` every row of the current occurrence, so two saves of $50
are two rows and read as $100. There is no row-per-period invariant and no lock: a read-modify-write once lost one
of two concurrent saves and left the history summing to more than the balance, which is why the read was removed
rather than locked. Nothing in C# reads a single row as the period's total. The top-up row and its history row are
saved in one transaction (`PackNameTransaction.Begin` in `SaveSubsidyTopupForPeriodAsync` and
`SaveProductGroupTopupForPeriodAsync`), so a failed history row leaves no credit behind. The reason is at most 255
characters (`SubsidyTopupValidator.MaximumReasonLength`, used by all four top-up validators, admin and storefront). The
storefront dialog disables its save button while a save runs; the admin popups can still be posted twice by a
double-click (two rows, as two saves).

| Field | Type | Description |
|---|---|---|
| `SubsidyPackNameId` | int | The PackName assignment being topped up |
| `Credit` | decimal | The amount this save added (`> 0`, `≤ SubsidyTopupValidator.MaximumTopupCredit` = 1,000,000) |
| `CreatedOnUtc` | DateTime | When the row was written — this is what places it in an occurrence |

**Topup period rules.** A topup belongs to one occurrence of the ordering period, decided **at read time** from
`CreatedOnUtc` and the subsidy's current dates and `AnnualRecurrence` — no period dates are stored on the topup, so
changing the subsidy's dates re-buckets every existing topup with no data fixup. The occurrence a topup created at
time `T` belongs to (`[dbo].[subsidy_topup_period_end]`, §3):
- Non-annual subsidy → the single period; every topup belongs to it, whenever it was created.
- Annual recurrence → the first occurrence whose close (end of `DurationEnd`'s day, store time, anniversary clamped
  to the end of the month) is `≥ T`.

`CreatedOnUtc` is a `datetime2` holding UTC, converted to the store's zone (`AT TIME ZONE 'UTC' AT TIME ZONE <store
zone>`), so a topup added at 10:00 on the day a period closes belongs to the period closing that day. See §3 for the
arithmetic and the two implementations that must agree.

**A top-up outside the current occurrence's window is refused, not stored.** `ISubsidyService.IsTopupPeriodOpenAsync`
is asked first by the admin popups and the storefront dialog, and the save itself throws if the moment is outside the
window. There is no pre-loading of credit for a season that has not opened: a top-up is an addition to the season that
is running.

### 2.7 ProductGroupTopup (`CORE_SUB_ProductGroupTopup`)

Adds extra quantity and/or credit to a specific product group for a specific `SubsidyPackName`. As with
`SubsidyTopup`, every save **inserts** a row (`SaveProductGroupTopupForPeriodAsync`) plus a `ProductGroupTopupHistory`
row; the views `SUM` the rows of the current occurrence. At least one of `Quantity` / `Credit` must be `> 0` per save,
neither may be negative, and credit is capped at `MaximumTopupCredit`.

| Field | Type | Description |
|---|---|---|
| `SubsidyPackNameId` | int | The PackName assignment |
| `ProductGroupId` | int | The product group being topped up |
| `Quantity` | int | Units this save added |
| `Credit` | decimal | Credit this save added |
| `CreatedOnUtc` | DateTime | Places the row in an occurrence (same rules as §2.6) |

### 2.8 SubsidyOrderItem (`CORE_SUB_SubsidyOrderItem`)

Records how subsidy credit was consumed for a single order item at order placement.
One record per order item that had subsidy applied.

| Field | Type | Description |
|---|---|---|
| `OrderId` | int | Parent order |
| `OrderItemId` | int | The NOP OrderItem |
| `SubsidyId` | int | Which subsidy contributed |
| `ProductGroupId` | int? | Which product group contributed (null = global credit only) |
| `Quantity` | int | Units consumed from base product-group quantity allocation |
| `QuantityTopup` | int | Units consumed from product-group quantity topup |
| `ProductGroupCredit` | decimal | Credit consumed from base product-group credit |
| `ProductGroupCreditTopup` | decimal | Credit consumed from product-group credit topup |
| `Credit` | decimal | Credit consumed from base global subsidy credit |
| `CreditTopup` | decimal | Credit consumed from global subsidy topup |
| ~~All `ExclTax` variants~~ | — | **Removed 2026-08-26.** The tax-exclusive twins were written on every consumption row and read by nothing: `vw_CORE_SUB_OrderSubsidyTotal` summed them into `TotalForStockExclTax` / `TotalForShippingExclTax`, and both of that view's callers discarded the result. Dropped from `SubsidyOrderItem`, `SubsidyOrderShipping` and the view. If a GST split on subsidy is ever needed it returns as a field with a stated purpose — see `CWPlans/Exchanges_Rebuild_Record.md` §4 and §8 |

### 2.9 SubsidyOrderShipping (`CORE_SUB_SubsidyOrderShipping`)

Records how subsidy credit was consumed for the shipping charge of a PackName's order.
One record per PackName per order where shipping was subsidised.

| Field | Type | Description |
|---|---|---|
| `OrderId` | int | Parent order |
| `PackNameId` | int | The PackName whose shipping was subsidised |
| `SubsidyId` | int | Which subsidy contributed |
| `Credit` | decimal | Global credit consumed for shipping |
| `CreditTopup` | decimal | Topup credit consumed for shipping |

### 2.10 SubsidyTopupHistory (`CORE_SUB_SubsidyTopupHistory`)

Immutable audit log. A record is appended with every subsidy-level topup save, capturing the amount that was added,
the reason entered, and who made the change. Since top-ups are insert-only (§2.6) there is one history row per topup
row; the history survives to describe *why*, and is what the history procedure (§12.1) and the admin and My Subsidies
history read.

| Field | Type | Description |
|---|---|---|
| `SubsidyTopupId` | int | FK to the `SubsidyTopup` row this save wrote |
| `Credit` | decimal | The amount that was added in this operation |
| `Reason` | string | Optional free-text reason supplied by the manager |
| `CreatorId` | int | `CustomerId` of the manager who performed the topup |
| `CreatedOnUtc` | DateTime | UTC timestamp of the operation |

### 2.11 ProductGroupTopupHistory (`CORE_SUB_ProductGroupTopupHistory`)

Immutable audit log for product-group topup changes, parallel to §2.10.

| Field | Type | Description |
|---|---|---|
| `ProductGroupTopupId` | int | FK to the `ProductGroupTopup` record that was changed |
| `Quantity` | int | Quantity amount added in this operation |
| `Credit` | decimal | Credit amount added in this operation |
| `Reason` | string | Optional free-text reason supplied by the manager |
| `CreatorId` | int | `CustomerId` of the manager who performed the topup |
| `CreatedOnUtc` | DateTime | UTC timestamp of the operation |

---

## 3. The Ordering Period

A subsidy is "in its ordering period" when **the store's local today** falls inside the
**occurrence** of the period that is current, after applying any variation overrides.

Two rules carry all the weight here, and both were defects before 2026-09-16 (phase 1):

**A period is expressed in the store's local time.** A season ending on 30 September ends when
the 30th ends *in the store's own zone*, not in UTC and not in the web server's zone. The zone is
nop's `DateTimeSettings.DefaultStoreTimeZoneId`, read **per store**, falling back to the instance
default and then — in SQL only — to UTC. No period function reads a clock of its own: the store's
date is passed in as a parameter, and `vw_CORE_SUB_StoreSubsidyVariationSetting` is what supplies
it (`TimeZoneId` and `LocalToday` columns) to every view that needs it. (The credit views' top-up bucket
converts the same instant itself — `SYSDATETIMEOFFSET() AT TIME ZONE vset.TimeZoneId` — because it needs the local
*time*, not only the date; it is the same clock through the same zone.)

**An annual subsidy has real occurrences, not a month/day comparison.** `AnnualRecurrence` does not
mean "ignore the year". The current occurrence is computed by `[dbo].[subsidy_current_period]`,
which returns `PeriodStart` and `PeriodEnd` as actual dates for the year in question, and:

- **Anniversaries clamp to the end of the month.** A 29 February bound becomes 28 February in a
  non-leap year. Constructing the date directly raised in SQL (`DATEFROMPARTS(2026, 2, 29)`) and
  threw `ArgumentOutOfRangeException` in C#, from every page that read a packname's ordering
  state. The SQL clamps with `EOMONTH`; the C# does the same in `SubsidyPeriodHelper.Anniversary`.
  **Both bounds clamp** — start and end alike.
- **A season may wrap the calendar year.** When `YEAR(DurationStart) < YEAR(DurationEnd)` the
  occurrence spans New Year, and which occurrence is "current" depends on whether the store's
  today is at or past this year's start.
- **A closed season resets on its own.** Nothing is scheduled and no balance is written back: the
  occurrence moves forward with the calendar, and consumption is counted against the occurrence
  the consuming order falls in. So an annual balance is automatically full again on day one of the
  next season.

### The two functions, and the one C# class that must agree with them

Both are inline table-valued functions, so they expand into the calling view's plan; the views
`CROSS APPLY` them and do the date tests themselves. (Until 2026-10-20 three scalar wrappers did the
tests — `subsidy_is_in_ordering_period`, `subsidy_order_is_in_current_period`, `subsidy_get_topup_period_end`.
They are dropped: SQL Server before 2019 cannot inline a scalar UDF, and the deployment floor is 2016 SP1.)

| | Decides |
|---|---|
| `[dbo].[subsidy_current_period]` (iTVF) | `PeriodStart` / `PeriodEnd` of the occurrence current at `@AsOf`. The views test the store's today against it (in ordering period) and an order's local date against it (consumption resets) |
| `[dbo].[subsidy_topup_period_end]` (iTVF) | The close (23:59:59 store time) of the occurrence a topup created at a local time belongs to. It rolls **forward**: a topup added after this year's close belongs to next year's |
| `Helpers/SubsidyPeriodHelper` (C#) | The same arithmetic for the admin topup screens, unit-tested in `Tests/CW.Tests/Core/Helpers/SubsidyPeriodHelperTests.cs` |

**The SQL decides what a packname may spend; the C# decides which topup row the admin screen adds
to. Change one and change the other, and keep those tests green.** When the two disagreed, the
admin dialog searched a window that had already closed on the last day of a non-annual period,
found nothing, and inserted a *second* topup row while SQL counted both — double-counting both
available and used credit.

A topup window's bounds are **exclusive lower, inclusive upper**, so consecutive occurrences meet
exactly and a topup created in the seam belongs to precisely one of them. An absent bound is
`null`, never `DateTime.MinValue`: a sentinel is a real date to SQL. (Before 2026-10-20 the topup
`CreatedOnUtc` columns were `datetimeoffset` and `MinValue` threw on any web server ahead of UTC; they are
`datetime2` UTC now.)

**A variation moves the window.** When the store has `PackNamesOwnStartAndFinishDates` on and the
assignment's variation has an end date, that end is the one both halves use — the credit views'
top-up join and `SubsidyPeriodHelper.GetTopupSearchWindow` alike (`SubsidyPeriodHelper.EffectiveDurationEnd`
states the rule once). With the setting off, a variation's dates are stored but never applied, and the
admin grid does not show them. A variation has **both** dates or **neither**, end after start.

**A top-up outside the window is refused, not stored.** `ISubsidyService.IsTopupPeriodOpenAsync` is
asked first by the admin popups and the storefront dialog; outside the window the admin popup shows the
reason in its validation summary and the storefront answers with a message in nop's notification bar.
Before 2026-09-29 such a top-up was written and never counted.

> **One known divergence, harmless today.** The C# reads the *instance* store zone where the
> SQL reads the *per-store* setting — identical for a single-zone estate and for any manager
> working inside one store, and reachable only by a cross-store admin action.

> **One assumption, not a defence in code.** `SubsidyPeriodHelper.ToUtc` would throw for a store-local time inside a
> DST spring-forward gap. Window bounds are always 23:59:59 local and no zone the business uses transitions at that
> hour, so the case is unreachable.

> **Deployment note.** `DefaultStoreTimeZoneId` is load-bearing for money: it decides which season
> an order falls in and when a topup window closes. Set the real store zone before go-live, per
> store if the estate spans zones. In SQL, `TimeZoneId` falls back to the literal `N'UTC'` — not
> `CURRENT_TIMEZONE_ID()`, which needs SQL Server 2019 while the plugin's floor is 2016 SP1 (`CREATE OR ALTER`). An
> install that reaches the fallback has no store zone configured: fix the setting, never the literal. `AT TIME ZONE`
> is available on Azure SQL Database and SQL Server for Linux.

> **How the SQL objects reach a database.** The views, the two functions and the history procedure are (re)created
> by `ICWDatabaseService.UpdateDatabaseScriptsAsync`, run from the plugin's install and update — **not** on an
> application restart. After changing any SQL constant in `CWDatabaseService`, bump the plugin version and run the
> update, then confirm with `SELECT name, modify_date FROM sys.objects WHERE name LIKE 'subsidy_%' OR name LIKE
> 'vw_CORE_SUB_%'`; otherwise old SQL runs against new C#. The functions are created before any view that applies
> them: SQL Server's deferred name resolution covers tables and views, not a function in a `FROM` / `APPLY` clause.

### 3.1 OrderOutsidePeriod

When `OrderOutsidePeriod = true` and the subsidy is outside its period, the PackName is still
allowed to place orders — but **no subsidy credit is applied**. The PackName pays full price.

`OrderOutsidePeriod` and `RestrictToAllocatedQty` are mutually exclusive at save: `SaveSubsidyAsync` clears the
quantity restriction whenever ordering outside the period is allowed (§5.2). The `canorder` branch of
`vw_CORE_SUB_SubsidyPackNameCanOrder` still carries a guard for the pair (`OrderOutsidePeriod = 1 AND
(RestrictToAllocatedQty = 0 OR (IncludeManagers = 0 AND Manager = 1))`) — a row that somehow held both flags would
**block** a restricted, non-exempt packname outside the period, not relax it. The service makes that row impossible;
the guard is legacy, not a rule.

### 3.2 Determining If a PackName Can Currently Order

`vw_CORE_SUB_SubsidyPackNameCanOrder` (entity: `SubsidyPackNameCanOrder`) computes per PackName:

- `CanOrder` — true if at least one of the PackName's assigned subsidies is in period **or**
  has `OrderOutsidePeriod = true` (with the manager/RestrictToAllocatedQty qualifications).
- `OnlyAllocatedProducts` — true if ALL active assigned subsidies have the flag set (see §5.1).

Used by `ISubsidyOrderingService.PackNameCanOrderAsync` and `GetSubsidyPackNameCanOrderAsync`.
Only subsidies of a store with subsidies switched on are counted (`SubsidyEnabled = 1`): a packname in a
store with subsidies off always has `CanOrder = 1`.
The result is statically cached with a smart TTL that expires at the exact next ordering-period
status change for the PackName (calculated by `GetPackNameCacheTimeInMinutesAsync`). That instant is itself
statically cached (`PackNameNextSubsidyStatusChangeCacheKey`), so a cached read costs no query; it is
worked out again once it has passed, and cleared by the subsidy, assignment, variation, packname and
setting cache consumers (the store's time-zone setting included). The TTL never goes below
`MinimumOrderingCacheMinutes` (3).

**What a closed ordering period does** (`CanOrder = 0` for the selected packname): the catalogue and every product
page show nothing (the allocation gate, `CLAUDE.md` §15.2 — a closed period is "nothing allocated"); add-to-cart is
refused with `Plugins.CW.Core.PackName.CannotOrder` ("Ordering is closed for {0}: the subsidy ordering period has
ended."); the selector shows the packname disabled — or, when it is the one in context, selected with the tooltip
`PackName.BlockOrderingReason`; the info strip shows the notice; and its cart lines are **hidden, not deleted** —
`vw_CORE_PN_PackNameShoppingCartItem` joins `CanOrder = 1`, so they come back when the period reopens (§8.6). A
deleted or inactive packname, or one whose list has no live branch, is refused with
`PackName.NotAvailableForOrdering`. An exchange replacement is exempt from the closed-period check while it is
added (`ExchangeReplacementScope`), but the cart view still hides it in a closed packname, so it can be paid for only
through the exchange's own placement path. `PackName/SelectPackName` still accepts a closed packname when posted
directly (recorded, no action — `Subsidy_PackName_Open_Items.md` 4.18).

### 3.3 In-Period Service Methods

`ISubsidyOrderingService` provides:

| Method | Description |
|---|---|
| `GetInOrderingSubsidyPackNamesByPackNameIdAsync` | `SubsidyPackName` records that are currently active for ordering (in period OR OrderOutsidePeriod). Statically cached with smart TTL. |
| `GetSubsidyProductsByPackNameIdAsync` | Flat list of product IDs from all active subsidies' product groups. Statically cached with `PackNameOrderingProductsCacheKey`. |
| `GetOnlyAllocatedProductIdsAsync` | Returns allocated product IDs when all active subsidies restrict products (manager-aware through `IncludeManagers`); null otherwise. |
| `PackNameCanOrderAsync` / `GetSubsidyPackNameCanOrderAsync` | Can this PackName place any order at all — the boolean, or the view row (`CanOrder`, `OnlyAllocatedProducts`). |
| `GetOrderingBlockedPackNamesAsync(ids)` | The subset of the given packnames that cannot order — one query for the selector and the checkout. |
| `PackNameHasActiveProductGroupsAsync` | Whether the packname has any in-period product group — the landing-page test (§8.4). |
| `GetPackNameNextSubsidyStatusChangeAsync` / `GetPackNameCacheTimeInMinutesAsync` | The next instant the packname's ordering status changes, and the smart TTL derived from it (§3.2). |

---

## 4. Credit and Quantity — What Is Available

The available credit for a PackName is computed by `vw_CORE_SUB_SubsidyPackNameCredit`
(entity: `SubsidyPackNameCredit`) and `vw_CORE_SUB_ProductGroupPackNameCredit`
(entity: `ProductGroupPackNameCredit`). These views apply the ordering-period filter and
subtract already-used amounts from placed orders (excluding cancelled orders,
`OrderStatusId != 40`).

`ISubsidyCreditService.GetPackNameSubsidyCreditAsync` assembles these into a `PackNameCredit`
object, which contains a list of `SubsidyCredit` records (one per active subsidy).
Each `SubsidyCredit` contains `ProductGroupCredit` records (one per product group in that
subsidy), each of which includes `ProductIds` — the list of products in that group.

`PackNameCredit` lives for one request only: `SubsidyCreditService` reads the credit of all the
packnames it is asked for in one query per view (`LoadPackNameCreditsAsync`) and memoises them in the
(scoped) service; callers get clones. Nothing is cached across requests, so no consumer has to clear
it. Inside the request the memo is cleared by every write of consumption or restore
(`InsertSubsidyOrderItemAsync`, `InsertSubsidyOrderShippingAsync`, the restore writes) and by
`ClearPackNameCredits()`, which settlement calls inside the packname lock so that it always reads the
credit afresh.

### Available credit fields (per SubsidyCredit / ProductGroupCredit)

| Field | Meaning |
|---|---|
| `Credit` | Base allocation (global or product-group level) |
| `CreditTopup` | Sum of all topups |
| `AvailableCredit` | `Credit - UsedCredit` |
| `AvailableCreditTopup` | `CreditTopup - UsedCreditTopup` |
| `Quantity` | Base quantity allocation |
| `QuantityTopup` | Sum of quantity topups |
| `AvailableQuantity` | `Quantity - UsedQuantity` |
| `AvailableQuantityTopup` | `QuantityTopup - UsedQuantityTopup` |

**Note**: `AvailableQuantity` reflects DB-persisted orders only (not the current cart).
Cart quantities are tracked separately at checkout time.

---

## 5. Subsidy Flags — Detailed Rules

### 5.1 OnlyAllocatedProducts

**What it does**: Restricts what products the PackName can see and order in the entire public
store, across all catalogue pages, search, featured products, and homepage.

**When it applies**: Only when ALL currently active subsidies (in period OR OrderOutsidePeriod)
assigned to the PackName have `OnlyAllocatedProducts = true`. If even one active subsidy does
not restrict, the PackName sees the full catalogue.

**Manager exception**: Controlled by `IncludeManagers`. If `IncludeManagers = false`, manager
PackNames are exempt from the restriction even if the flag is set. If `IncludeManagers = true`,
managers are restricted the same as regular PackNames. The rule is applied in the SQL views
(`OnlyAllocatedProducts = 1 AND (IncludeManagers = 1 OR Manager = 0)`) and, since phase 4b, in C# by
`GetOnlyAllocatedProductIdsAsync`, which reads the packname's `Manager` flag — the catalogue and bulk order
both read it.

**Implementation — one gate.** `IPackNameCatalogGate` (`Services/PackNames/PackNameCatalogGate`,
`Helpers/PackNameCatalogGateHelper.VisibleProductIds`) answers "what may this packname see": `null` when unrestricted;
otherwise the allocated product ids (`GetOnlyAllocatedProductIdsAsync`) plus their grouped-product relatives; an
**empty** set when the packname cannot order (`PackNameCanOrderAsync` false — a closed period hides everything). The
packname it judges is the scope window's packname when one is open (`PackNameCatalogScope`), else the selected one
(`GetCatalogPackNameAsync`). `AclOverrideService` applies the gate in the generic `AuthorizeAsync<TEntity>` and in
`ApplyAcl` — so every ACL check and every ACL query filter in nop follows it, product pages, related and cross-sell
lists, the category menu and search included — and derives the visible **categories** (those mapped to a visible
product, with their ancestors) and **manufacturers** (those of a visible product) from the same set, so an empty
category disappears everywhere. `ProductOverrideService` (`SearchProductsAsync` and the featured / homepage lists) and
`BulkOrderService` read the same gate. **Do not filter on allocation anywhere else.** A crafted add-to-cart POST for a
product the packname may not see is refused with nop's own "Product is not published" — the same text as any ACL
refusal. Admin searches (`showHidden: true`) are never restricted: an administrator whose own packname is closed must
still see the product grid.

**Caching behaviour**: the allocated product id list is cached with the smart TTL (expires at the next
ordering-period boundary) under `PackNameOrderingProductsCacheKey`, invalidated by `PackNameCacheEvenConsumer` and
`SubsidyPackNameCacheEventConsumer` when membership changes, and by any `multistoresettings.*` write (switching
subsidies, packnames or packname dates). The smart TTL is measured from the store's day in the store's time zone,
and uses a variation's dates only where `PackNamesOwnStartAndFinishDates` is on — the same effective dates the views
use (`SubsidyPeriodHelper.EffectiveDurationStart/End`, `NextOrderingStatusChange`). The gate's visible categories and
manufacturers are cached by the **fingerprint** of the visible product set
(`PackNameServicesDefaults.CatalogueVisibleCategoryIdsCacheKey` / `…ManufacturerIds…`), cleared by the four
`Catalogue*CacheEventConsumer`s (category, product–category, product–manufacturer, product), and the whole answer is
memoised per request per (packname, purpose). The restriction is carried into nop's own cache keys by a marker
appended to the customer's role set (`CLAUDE.md` §15.2).

**Edge case — inactive subsidies**: The `OnlyAllocatedProducts` flag is evaluated only over
currently active (in-period or OrderOutsidePeriod) subsidies, not all historical assignments.
A subsidy outside its period with `OrderOutsidePeriod = false` does not count toward the
restriction check.

### 5.2 RestrictToAllocatedQty

**What it does**: Prevents a PackName's cart from taking more units of a product than are still
allocated in the product groups containing it.

**When it applies** (phase 4b, decisions of 2026-09-30): **only the subsidies whose groups contain the
product decide.** A subsidy that does not contain the product neither caps it nor lifts the cap on it.
- No in-ordering subsidy contains the product: no cap.
- A subsidy containing it does not restrict to the allocated quantity: no cap.
- A subsidy containing it, or a group containing it, still has **credit available** — credit, credit
  topups or a variation's credit, less what placed orders used: no cap, the extra units are paid from
  that credit. The cap returns once the credit is spent.
- Otherwise the cap is `Σ(AvailableQuantity + AvailableQuantityTopup)` of the groups containing the
  product, less what the rest of the packname's cart already takes from those groups.

**Manager exception**: follows `IncludeManagers`, as for `OnlyAllocatedProducts`: with it on, managers
are capped like any packname; off, they are not. The credit view delivers `RestrictToAllocatedQty`
already manager-aware (`RestrictToAllocatedQty = 1 AND (IncludeManagers = 1 OR Manager = 0)`).

**Outside-period ordering**: a subsidy that allows ordering outside its period cannot carry the quantity
restriction — `SaveSubsidyAsync` clears `RestrictToAllocatedQty` whenever `OrderOutsidePeriod` is set (§3.1) — so a
packname buying outside the period is buying at full price with no cap. The cap is decided by the in-ordering
subsidies containing the product (the credit view lists in-period assignments only).

**Implementation**: the rule is `Helpers/SubsidyQuantityCapHelper.GetQuantityLeft` (pure, unit-tested).
`ShoppingCartOverrideService.GetSubsidyQuantityLeftAsync` feeds it the packname's credit and **what the
cart already takes**: the subsidy allocation (`ISubsidyAllocationService.AllocateAsync`) run over the
packname's cart with no money budget, grouped by subsidy and product group — the same allocation the
subsidy bar and placement use, so a product in several groups is counted where the allocation places it.
The allocation runs only when a cap applies. Gated on `CWFeature.Subsidy` — for the line's store on the add path
and the warnings; the quantity-change path asks for the current store (`UpdateShoppingCartItemAsync`), which is the
same store everywhere a customer edits a line and a known inconsistency for a cross-store admin call
(`Subsidy_PackName_Open_Items.md` 3.16).
- **Add** (packname `AddToCartAsync`, shopping cart only — never the wishlist): the quantity being added is
  compared with what is left; the product's own line is part of what the cart takes. Bulk order and
  reorder go through the same path. **An exchange replacement is not capped** (`ExchangeReplacementScope`):
  the returned units were given back to the allocation at approval, and a refusal would roll the whole
  authorisation back.
- **Quantity change** (`UpdateShoppingCartItemAsync`): the line is left out of what the cart takes and its
  whole new quantity is compared with what is left. **Only a raise is refused** — a line already over what
  is left (the allocation was lowered after it was added) can always be brought down.
- Refusal message: `Plugins.CW.Core.Subsidy.ShoppingCart.SubsidyQtyExceeded` ("…There are {0} left.").

**Important**: `AvailableQuantity` in the credit view reflects placed orders only, not the
current cart — which is why the cart is counted through the allocation. The cart counted is the
current customer's: two customers ordering for the same packname are each checked against the
allocation on their own; placement re-checks the cap against placed orders before any payment is taken (§7), so the
second of two carts that each passed at add time is refused at placement.

**Credit and the cap**: credit **available** on a subsidy or group containing the product lifts the cap
(see "When it applies"); credit that has been spent does not. A zero-priced line takes no units, at
placement or in the cap.

**The flag cannot even be SET on a subsidy that allocates credit.** This is the admin-side half
of the incompatibility above, and it is enforced, not merely advisory:

> **A subsidy can be restricted to its allocated quantity only for as long as that subsidy
> allocates quantities and no credit value.**

What `SaveSubsidyAsync` enforces: `RestrictToAllocatedQty` survives a save only when `OnlyAllocatedProducts` is on,
`OrderOutsidePeriod` is off **and** the subsidy's own global `Credit` is zero; otherwise it is cleared, whatever was
posted (`IncludeManagers` is likewise cleared without `OnlyAllocatedProducts`). Product-group credit is **not**
examined at save — a group's credit lifts the cap at order time instead (the "credit available" rule above), so a
quantity-only group next to a credit group is restricted while the credit group is not. The flag therefore describes
a quantity-only subsidy in practice, and a subsidy that allocates global credit has nothing left to restrict.

**How this shows up in admin, and why it looks like a bug when it is not.** On the subsidy editor
(Manage Store → Subsidies → *subsidy*) the checkbox's panel (`.pnlRestrictToQty`) is shown by script only while
*View Only Allocated Products* is ticked, *Order outside period* is not, and *Credit* reads 0; otherwise it is hidden.
Posting `RestrictToAllocatedQty=true` directly to `/Admin/Subsidy/SaveSubsidy` with a credit returns
`{"success":true}` and the value reads back **false**, while `OnlyAllocatedProducts` posted in the same request does
persist. All of that is the rule working. It is **not** a broken screen and it is **not** a silently-dropped field —
do not "fix" it.

**Consequence for testing**: any scenario that needs `RestrictToAllocatedQty = true` needs a purpose-built
**quantity-only** subsidy — zero global credit, and zero credit on the group under test — with a packname, an
allocation, an order and a completed delivery behind it.

### 5.3 OrderOutsidePeriod

**What it does**: Allows a PackName to place orders when the subsidy is outside its date range.
Orders placed outside the period are at full price — no subsidy credit is applied.

**What it does NOT do**: It does not carry over any credit. The PackName simply bypasses the
"you cannot order" block. Outside the period:
- `OnlyAllocatedProducts`: still active for product visibility (the subsidy is still "assigned" and counted as
  active by `vw_CORE_SUB_SubsidyPackNameCanOrder`)
- `RestrictToAllocatedQty`: cannot be set on such a subsidy at all (§3.1, §5.2)

### 5.4 AnnualRecurrence

**What it does**: Makes the subsidy period repeat every year as real **occurrences** — `subsidy_current_period`
computes this year's (or the wrapping season's) start and end from the stored month and day, clamping anniversaries
to the end of the month (§3). It is not a month/day comparison that ignores the year. Useful for programs that run
the same months every year (e.g. "July to September uniform season"); the balance is full again on day one of the
next season with nothing scheduled.

### 5.5 IncludeManagers

**What it does**: Controls whether manager PackNames are subject to the same product and
quantity restrictions as regular PackNames (applied in the views and, since phase 4b, in C# for the
catalogue — §5.1).

- `IncludeManagers = false` (default): Managers can see all products and order any quantity,
  even if `OnlyAllocatedProducts` or `RestrictToAllocatedQty` are enabled on the subsidy.
- `IncludeManagers = true`: Managers are treated identically to regular PackNames for all
  restrictions.

---

## 6. Credit Consumption Order

When a PackName places an order, subsidy credit is consumed in a strict priority sequence.
This applies per order item and is recorded in `SubsidyOrderItem`.

```
1. Product-group base Quantity       (SubsidyProductGroup.Quantity)
2. Product-group topup Quantity      (ProductGroupTopup.Quantity)
3. Product-group base Credit         (SubsidyProductGroup.Credit)
4. Product-group topup Credit        (ProductGroupTopup.Credit)
5. Global Subsidy base Credit        (Subsidy.Credit or SubsidyVariation.Credit if applicable)
6. Global Subsidy topup Credit       (SubsidyTopup.Credit)
```

Quantity consumption means the system records how many units of the product were covered
by the product group's quantity allocation. Each unit consumed by quantity does NOT also
consume credit — they are mutually exclusive for that quantity. When the quantity allocation
is exhausted, remaining units fall through to credit-based consumption.

For shipping, only global credit (steps 5 and 6) is used, recorded in `SubsidyOrderShipping`.

**Subsidy runs before credit notes** (business decision 15, 2026-09-10 — units and money alike, and the
employer's subsidy before a manager's own store credit). In the settlement pipeline `SubsidySettlementSource`
is priority 1 and `CreditNoteSettlementSource` priority 2; credit notes see only what subsidy left
unsettled, and the customer pays the rest. Return fees are added after both and are always cash.

**Shipping is not subsidised by default** — deliberately, the inverse of 4.6 (decision 4, S-15):
`MultiStoreSettings.IncludeShippingOnSubsidy` is off unless a store turns it on. When it is on, shipping takes
only global credit (steps 5 and 6), after the goods.

---

## 7. How Credit and Quantity Are Applied at Checkout

*Decisions 5 and 6 of the fix strategy (2026-10-01): subsidy and store credit settle the undiscounted gross; the
customer's discounts reduce only the customer's share; one settlement row per order.*

**Where the totals are computed.** `OrderTotalCalculationOverrideService.GetShoppingCartTotalInternalAsync`.
For a packname cart (`IsPackNameCartAsync`; a cart built in memory for an exchange approval is priced as a
plain cart, whoever is signed in) it:

1. works out the **settleable amount** (`GetSettleableAmountAsync`): every line at its line price incl. tax —
   item discounts are inside the unit price, the subtotal discount is **not** taken off — plus shipping incl. tax.
   The payment-method fee is not in it (it is a cost of the method the customer chose);
2. quotes the settlement pipeline on it (`QuoteCartSettlementAsync`, memoised per request): subsidy first
   (`SubsidySettlementSource`, priority 1 — quantity, then product-group credit, then global credit, §6), credit
   notes second (`CreditNoteSettlementSource`, priority 2). What is left is the **balance due**;
3. applies the customer's discounts to the balance due only: the subtotal discount **limited to the balance due**
   (`GetPackNameSubTotalDiscountAsync` → `OrderSettlementHelper.LimitDiscount`; a discount that takes nothing off is
   not applied and its use is not recorded), + payment-method fee, − order discount (on what is left), − gift cards,
   − reward points; exchange fees are added last and are always cash.

So a 10 % discount on a fully subsidised order is 10 % of 0, and a gift card is never spent where subsidy pays.
Example (4.9 dev order #142): goods 360 + shipping 10 = 370; subsidy 300; store credit 15; balance 55; fee 2;
order discount 10 % of 57 = 5.70; payable **51.30**.

**What placement writes.** `OrderProcessingOverrideService` re-checks the quantity cap (§5.2) before any payment
(`PrepareAndValidateSubsidyQuantityCapAsync`; exchange replacement lines exempt), writes the order with **nop's gross
columns** (`OrderSubtotal*`, `OrderShipping*`, `OrderTax`, `TaxRates`), `OrderSubTotalDiscount*` = the discount
actually applied, and `OrderTotal` = what the customer pays. `SettleOrderAsync` then applies the pipeline —
`SubsidyOrderItem` / `SubsidyOrderShipping` and credit-note history rows, as before — and writes the order's
**settlement row**, `CORE_PN_OrderSettlement` (`SettleableAmount`, `SubsidyStockAmount`, `SubsidyShippingAmount`,
`CreditNoteAmount`, all incl. tax), once, only when something was covered. Subsidy is a tender: GST is not
apportioned between the subsidised and paid parts, and no Odoo mapping changes. `SettleOrderAsync` is idempotent: it
returns at once when the order already has a settlement row, credit-note history or subsidy items. The row has no fee
and no payable column by design — fees are read from their own ledger (`GetExchangeFeesAmountsAsync`) and the payable
*is* `Order.OrderTotal`, which the return-order path rewrites after settlement, so a stored copy would go stale.

**Who reads it.** Every reader of an order's subsidy / store credit reads that row
(`ICWOrderService.GetOrderSettlementAsync`, cached per order — nop's `MemoryCacheManager` never stores a null, so the
read inside the idempotency guard before the row exists does not cache "no row", and the row's own cache consumer
clears the key on insert): the customer order page (overview total = settleable amount; totals in the order the money
moved; per-line subsidy from `SubsidyOrderItem`), the admin Info and Payments cards ("Total before subsidy and
credit"), the admin packname card, the order e-mail (`WriteSettlementTotalsAsync`), the invoice PDF
(`PdfOverrideService.GetTotalsAsync` → `CWInvoiceTotals`, which carries the subtotal and order discounts apart — nop's
`InvoiceTotals` has one `Discount` slot that its own code fills with the subtotal discount and then overwrites with
the order discount) and the profit report (subsidy added back; store credit not). Nothing derives it again from the
ledgers: the row carries the plan's exact money, so no reader values units again — the per-line rows on the customer
page (`SubsidyOrderItem` units × `UnitPriceInclTax`) may differ from the row by rounding. On 4.9, orders placed
before 2026-10-02 had no row and showed nop's totals only. 5.00 starts from a new database and takes no 4.9 data
(port decision D1/D2, `CWCore_Port_Reference.md` §5), so every 5.00 order that subsidy or store credit paid for has
its row.

**How it is labelled** (phase 6 Part 2, 2026-10-07). On a packname order that subsidy or store credit paid for, every
surface — checkout panel, customer order page, order e-mail, invoice PDF, admin Info / Payments cards and admin packname
card — uses one set of labels, in the order the money moved: *Total before subsidy and credit* (the settleable amount),
*Subsidy applied* (total row, where shown), *Subsidy (items)*, *Subsidy (shipping)*, *Store credit*, *Subtotal discount*,
payment fee, *Order discount*, gift cards, reward points, *Exchange fees*, *Payable balance*. Customer surfaces format a
deduction as a negative amount (the currency decides how it looks — on the dev store `($300.00)`, the same as nop's own
discount rows); admin surfaces bracket an unsigned amount and their rows carry no colon, as nop's admin rows. Plain orders
keep nop's labels, with a *Store credit* row when credit paid for part of them. The admin summary card is titled
"Subsidy and store credit", its group heading "Subsidy", and on the Info / Payments cards the store-credit row sits
above the exchange-fee rows. Resources: `Plugins.CW.Core.Order.*`, `Plugins.CW.Core.Subsidy.ShoppingCart.Totals.*`,
`Plugins.CW.Core.ShoppingCart.Totals.*`, `Plugins.CW.Core.Messages.Order.*` (with the colon), `Plugins.CW.Core.Pdf.*`,
`Plugins.CW.Core.Admin.Order.*`.

**Reconciliation.** On every surface: settleable − subsidy − store credit − subtotal discount + fee − order discount −
gift cards − reward points + exchange fees = payable, exactly, unless a discount was clamped.

**Known limits, accepted.**
- *Plain carts keep nop's order:* a cart with no packname settles nop's own total (after discounts and gift cards),
  so its credit notes apply after the customer's discounts; on such an order "Total before subsidy and credit" is
  nop's total before credit notes, not goods + shipping.
- *Tax when the subtotal discount is limited:* `OrderTax` stays as nop computed it on the full discount, so it is
  slightly low in that case; every document shows the same stored figure.
- *Excl-tax display with a subtotal discount:* the tax row is the tax charged on the discounted goods while the
  settleable amount is the incl-tax full price, so in that display mode the rows are off by the tax on the discount.
- *A 3-decimal unit price* is rounded by nop before it reaches settlement.
- *Cancelled orders* keep their settlement row (it records the placement); the credit itself is back in the pool
  through the views.
- *Unpaid orders hold their credit* (decision 7, C-08): an order placed with a redirect gateway consumes subsidy and
  credit notes at placement, whatever its payment status, until it is cancelled or deleted. Release is computed, not
  evented — every consumption read in the views skips cancelled and deleted orders. No time limit.

---

## 8. Subsidy in the Public Store — Feature Map

| Feature | Triggered By | Service Used |
|---|---|---|
| Show subsidy bar | Any PackName with active subsidies | `ISubsidyCatalogCartService.GetPackNameSubsidyCartUsageAsync` — since phase 2 this **projects** the settlement pipeline's allocation via `IOrderTotalCalculationOverrideService.GetCartSubsidyBreakdownAsync`; it no longer simulates consumption of its own (it used to, with no credit-note budget, and disagreed with what placement wrote) |
| Show "Change Topup" button in bar | `SubsidyBarModel.CanEditSubsidyTopup = true` | See §8.1 |
| Restrict product catalogue (products, categories, manufacturers, related lists, search) | `OnlyAllocatedProducts` on all active subsidies, or a closed ordering period | `IPackNameCatalogGate` applied by `AclOverrideService` (`AuthorizeAsync<TEntity>`, `ApplyAcl`) and read by `ProductOverrideService` / `BulkOrderService` — §5.1 |
| Block adding a product the packname may not see | `OnlyAllocatedProducts` | the packname `AddToCartAsync` opens `PackNameCatalogScope.Begin(packName, …)` so nop's own ACL product check (`AclOverrideService.AuthorizeAsync`) refuses it — §5.1 |
| Show the packname info strip (branch, department, cost centre, PO, subsidy flyout, no-address and closed-period notices) | PackNames on, a packname selected, not on the catalogue / checkout pages | `PackNameInfoViewComponent` (`PublicWidgetZones.ContentBefore`); the flyout is `SubsidyCatalogController.BarFlyout` → `SubsidyBar/Flyout.cshtml`, loaded on demand — §8.7 |
| Restrict cart quantity | `RestrictToAllocatedQty` on every in-ordering subsidy **containing the product**, no credit available on them | `ShoppingCartOverrideService.GetSubsidyQuantityLeftAsync` → `SubsidyQuantityCapHelper.GetQuantityLeft` over the cart's allocation (§5.2) |
| Block PackName from ordering at all | No active subsidy and no OrderOutsidePeriod | `ISubsidyOrderingService.PackNameCanOrderAsync` |
| Deduct credit at checkout | Any active subsidy with remaining credit | **phase 2:** `OrderTotalCalculationOverrideService.GetShoppingCartTotalAsync` → `ISettlementPipeline` → `SubsidySettlementSource`. Subsidy runs **first** (`Priority` 1; credit notes 2) — decision 15 — so its budget is normally the whole settleable amount |
| Record subsidy on order | Order placement | **phase 2:** `OrderProcessingOverrideService.SettleOrderAsync` → `SubsidySettlementSource.ApplyAsync`, which still calls `ISubsidyCreditService.ApplyPackNameSubsidyCredit/Quantity` underneath. A replacement from an approved exchange is allocated like any other line: the returned item's consumption was already reversed in kind at approval (`RestoreSubsidyForReturnedItemAsync`, decision 18), so the restored unit or credit is what pays for it |
| Subsidise shipping | Remaining global credit after stock | `ISubsidyCreditService.GetPackNameShippingRemaining` |
| Show per-item subsidy breakdown in checkout cart | PackName has subsidy in current cart | `IOrderTotalCalculationOverrideService.GetCartSubsidyBreakdownAsync` → `PackNameCheckoutModelFactory.PopulateSubsidyBreakdownAsync` — see §8.2 |
| Show what an order settled (customer page, admin, e-mail, PDF, profit report) | Order has a settlement row | `ICWOrderService.GetOrderSettlementAsync` → `CORE_PN_OrderSettlement` — see §7 |
| *My Subsidies* (My Account › Subsidies, `customer/subsidies`) | Subsidy on; the selected packname has an assignment (the nav item appears with it) | `CustomerSubsidiesController` → `ICustomerSubsidiesModelFactory` → `ISubsidyReportingService.GetSubsidyDetailsByPackNameIdAsync` (every live assignment, in period or not), `GetSubsidyPackNameProductGroupDetailsAsync`, `GetSubsidyPackNameHistoryAsync`; `CustomerSubsidyHelper` for the season and status — see §8.3 |
| Land on the subsidy catalogue after login / packname selection / admin impersonation | `MultiStoreSettings.SubsidySendUserAfterLogin` and the selected packname has active product groups | `IPackNameLandingService` (subsidy catalogue first, then bulk ordering when `BulkOrderSendUserAfterLogin` says so); selection consults the subsidy setting only — see §8.4 |
| Print name pre-filled on the product page | `PrintNameEnabled` + `PrintNameProductAttributeId` (a text-box attribute) | `PrintNamePrefillEventConsumer` sets the box's default to the selected packname's print name; a cart-line edit keeps its own value |

### 8.1 Public Store Change Topup (Manager Feature)

Managers can adjust subsidy topups for their PackNames directly from the public store subsidy
bar, without going into the admin area.

**Gate conditions** (both must be true for the button to appear):
- `MultiStoreSettings.AllowEditSubsidyTopup = true`
- `MultiStoreSettings.AllowManagersToAddPackNames = true`
- The current customer must be a **manager PackName** (`PackName.Manager = true`)

**Target**: The topup is applied to the **currently selected employee PackName** (the PackName
the manager has selected in the PackName selector), not the manager's own PackName.

**Entry points** (two separate jQuery UI sub-dialogs):
- **Subsidy-level topup** — adds extra global credit to a specific `SubsidyPackName`.
  Calls `SubsidyCatalogController.ChangeTopupSubsidy` (GET to load form, POST to save).
- **Product-group topup** — adds extra quantity and/or credit for a specific product group.
  Calls `SubsidyCatalogController.ChangeTopupProductGroup` (GET to load form, POST to save).

**Important**: the amount entered is **what is added** — entering "50" inserts a $50 top-up row (§2.6); the dialog's
field is an amount to add and always opens at zero, which says nothing about whether top-ups exist. History is
recorded in `SubsidyTopupHistory` / `ProductGroupTopupHistory` (§2.10, §2.11) with the manager's `CustomerId` as
`CreatorId`. Both the storefront and the admin path log `EditSubsidyTopup` / `EditProductGroupTopup` with the
assignment id and the amount added.

**`CanEditSubsidyTopup`** is a computed bool on `SubsidyBarModel`, set by the model factory
after checking the settings and verifying the current customer is a manager.

**Server-side enforcement.** `CanEditSubsidyTopup` decides whether the button renders; it decides nothing about
whether a request is honoured. Both endpoints resolve their target through
`SubsidyCatalogController.ValidateTopupAccessAsync(subsidyPackNameId)`, which requires a packname context, a
**manager** caller, both settings for the current store, ownership of the context packname
(`IsPackNameOwnedByManagerId`), an assignment that belongs to that context packname and is **active** and not
deleted, and a subsidy that is active, not deleted and in the current store; then the ordering period must be open
(`IsTopupPeriodOpenAsync`, else `Plugins.CW.Core.ChangeTopup.PeriodClosed` in nop's notification bar). A
product-group topup also checks the group belongs to that subsidy (`IsProductGroupInSubsidyAsync`). The mutation
always uses the guard's resolved mapping (`mapping.Id`), never a posted id; a refusal answers
`{ success: false, message }` and the dialog shows the message. The dialogs are jQuery UI sub-dialogs opened from the
bar (`Views/SubsidyCatalog/ChangeTopup*.cshtml`, `Content/SubsidyCatalog/change-topup.css`, the shared
`Content/Common/cw-dialog.css`); the `packNameId` is never a URL parameter — the context packname is the target. The
same per-store settings are enforced in the SQL views (`vw_CORE_SUB_StoreSubsidyVariationSetting`), so a manager
cannot top up what the store does not allow.

### 8.2 Per-Item Subsidy Breakdown at Checkout

When a PackName views the PackName checkout page, each cart line shows a compact
breakdown of which subsidy credit sources were consumed for that item. The breakdown
appears stacked beneath the item subtotal inside the fixed-width subtotal column — it does
not affect row width or misalign the quantity controls.

#### How the preview is generated

The breakdown is a **projection of the settlement the totals already quoted**, not a simulation of its own.
`IOrderTotalCalculationOverrideService.GetCartSubsidyBreakdownAsync(packNameCart)` takes the request's memoised
plan (`QuoteCartSettlementAsync`) and asks `AllocateCartSubsidyAsync` → `ISubsidyAllocationService.AllocateAsync` for the
subsidy allocation **within the budget the plan gave subsidy** (`plan.BudgetFor(SettlementSourceNames.SUBSIDY)`), so the
per-line rows add up to the "Subsidy" row of the totals. **Before the packname checkout has given every packname a
shipping address and method**, shipping cannot be rated, so there is no settleable amount and no plan: the breakdown
then allocates the goods' subsidy with no money budget (`budgetInclTax: null`) and leaves shipping's share out until
it can be rated. This is what the subsidy bar shows on the catalogue and cart pages before the checkout is opened
(fixed in the port's part 5b; 4.9 showed an empty cart side and logged "Cart settlement skipped" on every page). The
checkout's totals and placement are unchanged. `AllocateAsync` takes a `SubsidyAllocationRequest` (the
customer, the store, the cart lines, the shipping prices per packname and the money budget incl. tax — `null` budget
for a units-only walk, as the quantity cap uses), runs the same `ApplyPackNameSubsidyQuantity` /
`ApplyPackNameSubsidyCredit` calls placement uses, on cloned credit state, and returns a `SubsidyAllocation` with:

- `Items` — `SubsidyOrderItem` rows, where `Id` is repurposed to hold the `ShoppingCartItemId` (**not** a database
  PK at this point);
- `ShippingCredits` — `SubsidyOrderShipping` rows, per packname.

`Items` may contain multiple records for a single cart item — one per
subsidy/product-group combination that contributed. When building the display model,
`Items` is grouped by `SubsidyOrderItem.Id` (= `ShoppingCartItemId`).

#### Display model — `SubsidyItemBreakdownModel`

`PackNameCheckoutModelFactory.PopulateSubsidyBreakdownAsync` converts the preview
into display models using nested `foreach` loops (not LINQ, because `await` cannot be
used inside non-async lambdas). One `SubsidyItemBreakdownModel` is produced per
product-group row per cart item:

| Field | Description |
|---|---|
| `ProductGroupId` | `null` = global credit row |
| `ProductGroupName` | Display name of the product group (`null` for global) |
| `ProductGroupIcon` | Symbol ID in `pg-icons.svg` (e.g. `pg-icon-Shirts`). `null` for global credit. |
| `ProductGroupColour` | CSS colour for the product group icon |
| `QuantityUsed` | Units consumed from quantity allocation (`Quantity + QuantityTopup`) |
| `CreditUsed` | Total credit consumed (decimal, before formatting) |
| `CreditDisplay` | Pre-formatted string via `IPriceFormatter.FormatPriceAsync(-creditUsed, showCurrency: true, showTax: false)`. The `showTax: false` flag suppresses the "incl tax" suffix. Negative sign indicates a reduction. |

`PackNameShoppingCartItemModel.SubsidyBreakdown` (`IList<SubsidyItemBreakdownModel>`)
carries these rows to the view. Rows where both `QuantityUsed == 0` and `CreditUsed == 0`
are skipped.

`PackNameBoxModel.ShippingSubsidyDisplay` holds a pre-formatted credit string for the
PackName's total shipping subsidy (if any), built from `SubsidyAllocation.ShippingCredits`
grouped by `PackNameId` and formatted with `showTax: false`.

#### View rendering — `_CartByPackName.cshtml`

The breakdown block is rendered **inside** `.cart-product-subtotal` (the fixed-width 180px
flex child), not as a sibling. This is critical — placing it as a sibling of `.cart-product-subtotal`
in the `.cart-product-info` flex row would widen some rows and misalign the quantity column.

`.cart-product-subtotal` is made a flex column (`align-items: flex-end; justify-content: center`)
so the subtotal price and the breakdown rows stack vertically within the same fixed-width cell.
`.cart-product-info` uses `align-items: center` (`Content/PackName/shopping-cart.css`).

Each `.subsidy-breakdown-row` renders at `0.70rem` and contains:

- A `cw-icon-subsidy` SVG icon (subsidy indicator) via `@Html.CWIcon("cw-icon-subsidy", "subsidy-indicator")`
- A product group icon via `@Html.PgIcon(b.ProductGroupIcon, null, b.ProductGroupColour)`, or
  a globe icon via `@Html.CWIcon("cw-icon-Globe")` for global credit rows
- `"N items"` when `QuantityUsed > 0`
- A `·` separator and `CreditDisplay` when both are present (e.g. `"2 items · −$10.00"`)
- A `title` tooltip with the product group name or the locale resource
  `Plugins.CW.Core.Subsidy.GlobalCredit` for global rows

Icons use SVG sprites (`cw-icons.svg` and `pg-icons.svg`) exclusively — no Font Awesome
`<i>` tags are used in these rows.

#### Important implementation notes

- **One source**: the breakdown, the subsidy bar (`SubsidyCatalogCartService`) and placement all come from the
  same allocation code inside the same budget (no budget only while shipping cannot be rated, above) — never add a
  second simulation.
- **`await` in LINQ lambdas**: `IPriceFormatter.FormatPriceAsync` is async. Because it
  cannot be awaited inside `Select` or `ToDictionary` lambdas, all breakdown and shipping
  accumulation code uses `foreach` loops.
- **CSS load for AJAX partials**: a registration inside a partial returned by an AJAX action goes nowhere (the
  `<head>` has already been rendered). The checkout cart CSS (`Content/PackName/shopping-cart.css`) is registered by
  `Views/PackNameCheckout/_OrderSummary.cshtml`, a partial rendered **with** the layout on the first load, which is
  the host of the AJAX-refreshed cart (`CLAUDE.md` §9.8).

### 8.3 My Subsidies — what the customer sees (phase 7)

The page follows the **selected packname**, exactly as the nav item and the catalogue do: a manager who has an
employee selected reads that employee's page, headed with the packname's name and a note saying so; with no
packname selected the page is refused. The *Subsidies* item in the account menu shows only when the page has a
subsidy to list: an active subsidy, actively assigned to the selected packname (`EventConsumer`, customer
navigation). All amounts are converted from the primary store currency to the customer's working currency. One card
per live assignment (`SubsidyPackNameDTO`, in period or not):

- **Status** from `CustomerSubsidyHelper.StatusFor`: *Open until <season end>* (with "resets every year" on an
  annual subsidy), *Opens <season start>* for a season that has not begun, *Closed since <season end>*. The season
  shown is the one the customer is in (`SeasonFor`: the anniversary year of an annual subsidy, the subsidy's own
  dates otherwise), expressed in the store's zone like every other period figure (§3).
- **Remaining** as the big figure: money when the subsidy allocates any (credit + top-up), otherwise units; a
  usage bar with *Used* and *Total*, and the `= credit + top-up` breakdown. A not-started or closed season shows
  what it will give / gave instead of a bar.
- **Product groups**: units left / used / total and credit left / used / total per group, "—" where a group does
  not allocate that kind. Inactive product groups are left off (`GetSubsidyPackNameProductGroupDetailsAsync(…,
  activeOnly: true)`); the catalogue and the bar still list them.
- **History by season** (changed in the port's part 5f): each `SubsidyHistory` row as a sentence (order placed,
  returned, top-up by whom with the reason, assigned, variation, …), units and credit signed. The whole history is
  read (no page limit) and sorted into seasons (`CustomerSubsidiesModelFactory.HistoryBySeason`,
  `CustomerSubsidyHelper.SeasonsUpTo` / `SeasonIndexFor`): the current season's rows sit under the figures, the earlier
  seasons under *Earlier seasons (n)*, each with its dates, what was topped up and used, and its rows. A movement
  belongs to a season by its store date (a top-up between seasons goes to the next one, a refund to its order's
  season); the assignment, the allocations and the variations stand in every season from their own. The 4.9 page
  showed the latest 100 rows. An order row is a **link only when the order belongs to the viewing customer** (`CanOpenOrder`, one
  `GetOrdersByIdsAsync` per card): nop's order page opens only the customer's own orders, so a manager reading an
  employee's page sees the employee's own orders as sentences, and an employee sees the orders his manager placed
  for him the same way. The theme sets `a { color: inherit }`, so the page's stylesheet colours and underlines the
  links it does render.

The figures come from the same reporting queries the admin panel reads, so the customer and the manager never
disagree. Negative money follows the store currency's format (brackets on the dev store, phase 6).

A **units-only** product group prints no money figure anywhere — bar, flyout, catalogue header, this page — the
`+ $0.00` of earlier versions is gone (`SubsidyCatalogProductGroupModel.TotalCredit`).

### 8.4 Landing after sign-in, packname selection and impersonation (phase 7)

`IPackNameLandingService` answers one question for the three entry points: *is there somewhere better than the
homepage?* `ResolveSignInLandingRouteAsync` (login — the `returnUrl` still wins — and admin impersonation) returns the
subsidy catalogue when `SubsidySendUserAfterLogin` is set for the store, Subsidy is on and the selected packname has
active product groups; otherwise bulk ordering when `BulkOrderSendUserAfterLogin` says so and the customer passes its
gate; otherwise `null`. `ResolveSelectionLandingRouteAsync` (after `SelectPackName`) consults the subsidy setting only:
the catalogue is about the packname just selected, bulk ordering is about the session. The setting lives beside
`BulkOrderSendUserAfterLogin` on both settings screens (subsidy-only row). This is 4.6's behaviour
(`Jim2CustomerRegistrationService`, `AllowancePackNameController`).

### 8.5 Recorded decisions — things deliberately not built

- **Correcting a top-up** (phase 4b carried #2, decision K / 3): a top-up cannot be removed or reduced — a negative
  amount is refused. The correction is a **variation** (credit / period override); re-assigning the subsidy to the
  packname resets its top-ups, which is the only way to start from zero.
- **Coupons and gift cards on the packname checkout** (G-01, decision 14 / G): out of scope for packname stores; the
  `/cart` POSTs keep answering 403 and the checkout has no boxes. Phase 6 settles any discount against the customer's
  share only, so a code entered anywhere else is harmless.
- **"Ignore packname context" session switch** (P-09, decision I): 4.6's switch is not ported. Every flow that would
  have used it selects a packname instead (a manager orders for himself by selecting his own packname); nothing in
  scope needs a packname-less session.
- **Specification-attribute filtering in the catalogue** (M-05): nop's strict filtering is kept — a product with no
  value for a filtered attribute is excluded, where 4.6 kept it. Deliberate; the catalogue is nop's.
- **A 4.6-style "no default shipping method" warning on the info strip** is not brought back: the options depend on
  the cart and the address, and the checkout warns per packname.

### 8.6 The packname cart — one definition

`ShoppingCartOverrideService.GetShoppingCartAsync` returns nop's own `ShoppingCartItem` rows minus the ids that
`vw_CORE_PN_PackNameShoppingCartItem` leaves out: lines of a packname whose period is closed (`CanOrder = 0`), of a
deleted or inactive packname, or of a list without a live branch (`GetHiddenPackNameCartItemIdsAsync`, cached per
request). The header count, the flyout, the selector badges, the checkout, the payment-method AJAX, nop's placement
checks and the packname checkout therefore all agree on what the cart is. Three rules keep it safe:

- **It never returns view rows.** nop updates and deletes whatever this method returns; a view entity would aim those
  writes at the view. A hidden line can still be removed — nop's update and delete load the row by id.
- **Hiding is per packname.** Every condition in the view belongs to the packname, so a packname's lines are all
  visible or all hidden, and they come back when the period reopens or the packname is reactivated. Lines with no
  packname link (a line added with packnames off) stay visible to nop and never reach the packname checkout.
- **With packnames off, nothing is hidden**; a wishlist is never a packname cart.

**The packname add owns its row.** `AddToCartAsync(…, packNameId, …)` inserts the `ShoppingCartItem` and its
`PackNameCartItem` link itself and merges only into a line of the same packname; `ShoppingCartItemIsEqualAsync` is
not overridden (an override that compared against the *context* packname blocked re-order merges). A required product
added automatically goes into the same packname as the product that needs it. A refused add writes its own warning
line and adds nothing (`PackName.CannotOrder`, `PackName.NotAvailableForOrdering`, `SubsidyQtyExceeded`, …).

**The line limit is per packname.** nop's `MaximumShoppingCartItems` counts the packname's lines on the add path, and
`GetShoppingCartWarningsAsync` removes nop's whole-cart message when no packname's basket exceeds the limit — only
that one message is re-asked; lines without a packname count as a basket of their own, and a cart with no packname
lines keeps nop's rule.

**Re-order** adds each line into the packname it was ordered for; a packname the customer no longer owns is skipped
with `Plugins.CW.Core.ReOrder.PackNameNoLongerManaged`, never moved into another packname. With packnames off the
lines go back as plain nop lines.

**The flyout in a packname context** links the item count to the packname checkout and shows no "View cart" button;
outside the context it is nop's.

**Deleting a packname** (`EventConsumer`) deletes its cart lines and shipping assignment — `CORE_PN_PackNameCartItem`
cascades from both `ShoppingCartItem` and `PackName`. A deactivated packname is dropped from the context on its next
read (a manager falls back to his own packname); its lines are hidden, not deleted. The stored `SelectedPackNameId`
needs no clean-up because `PackNameContext` replaces a stale one on read.

### 8.7 The packname info strip and the cart-changed event

`PackNameInfoViewComponent` (`PublicWidgetZones.ContentBefore`, `Views/Shared/Components/PackNameInfo/Default.cshtml`,
`Content/PackName/packname-info.css`) draws one strip under the header for the selected packname: branch, department,
cost centre and PO as chips (`@Html.CWIcon("cw-icon-…")` from `Content/icons/cw-icons.svg`), a subsidy trigger when the
packname has an **in-ordering** assignment (`GetInOrderingSubsidyPackNamesByPackNameIdAsync` — "has subsidies" means
"has a season running", not "has ever been assigned"), and two notices: *ordering closed* (`CanOrder = 0`) and *no
delivery address* (`PackNameHasShippingAddressAsync` — none on its branch and, with `AllowHomeDelivery`, none of its
own or its customer's; the same sources as the checkout's `IsAddressValidForCheckoutAsync`), text
`Plugins.CW.Core.PackNameInfo.NoShippingAddress`. The component makes no feature check of its own: `PackNameContext`
returns an empty packname when PackNames is off. The subsidy flyout is loaded on demand — `SubsidyCatalogController.
BarFlyout` returns `SubsidyBar/Flyout.cshtml` (`Content/SubsidyCatalog/subsidy-flyout.css`, registered by the host
view) — so the page load costs no factory call. The strip is hidden by CSS alone on the pages that carry the same
information themselves: the subsidy catalogue, the packname checkout, its completed page and the payment hand-over
(`packname-info.css` keys on those pages' body classes).

**One cart-changed event.** `Content/Common/cw-cart-events.js` (registered on every storefront page while PackNames is
on, `Services/EventConsumer.cs`) defines `CWCart.notifyChanged()`, which raises `cwCartChangedEvent` on `document`. It
hooks nop's `AjaxCart.success_process` once (nop looks the function up per call, so replacing it after load works; it
is registered with `AddScriptParts` so it loads after `public.ajaxcart.js`), and every other cart write — the flyout
remove, the checkout's remove and quantity change, the bulk-order add — calls `notifyChanged()` after the server
answers success. Subscribers: the subsidy bar and the catalogue's product-group headers, the strip's subsidy panel
(reloaded if open, else marked stale) and the selector's cart badges (the whole list in one call). On admin pages the
hook finds no `AjaxCart` and returns.

---

## 9. Database Views

| View | Entity Class | Purpose |
|---|---|---|
| `vw_CORE_SUB_SubsidyPackNameCredit` | `SubsidyPackNameCredit` | Per-PackName per-Subsidy: available credit, used credit, flags. Filtered to in-period subsidies only. Does not read the subsidy switch (its C# readers do — §1). |
| `vw_CORE_SUB_ProductGroupPackNameCredit` | `ProductGroupPackNameCredit` | Per-PackName per-ProductGroup: available credit and quantity, used amounts. Same note. |
| `vw_CORE_SUB_SubsidyPackNameCanOrder` | `SubsidyPackNameCanOrder` | Per-PackName: `CanOrder` and `OnlyAllocatedProducts` flags across all active subsidy assignments; subsidies of a store with subsidies off are not counted. Joined by `vw_CORE_PN_PackNameShoppingCartItem` (`CanOrder = 1`), which is what hides a closed packname's cart lines (§8.6). |
| `vw_CORE_SUB_SubsidyInOrderingPeriod` | `SubsidyInOrderingPeriod` | Per-Subsidy: whether it is currently in its ordering period. Subsidies of a store with subsidies off are not listed. |
| `vw_CORE_SUB_SubsidyVariationInOrderingPeriod` | `SubsidyVariationInOrderingPeriod` | Per-SubsidyPackName-Variation: in-period flag accounting for variation dates. Subsidies of a store with subsidies off are not listed. |
| ~~`vw_CORE_SUB_OrderSubsidyTotal`~~ | — | **Removed by phase 6 (2026-10-02).** What an order settled is its settlement row, `CORE_PN_OrderSettlement` (§7). |
| `vw_CORE_SUB_StoreSubsidyVariationSetting` | `StoreSubsidyVariationSetting` | Per-Store: exposes `PackNamesOwnGlobalValue` and `PackNamesOwnStartAndFinishDates` from `MultiStoreSettings`, **plus `TimeZoneId` and `LocalToday`** — the store's nop time zone and the date it is in that zone. This is how every period function gets the store's today without reading a clock. `TimeZoneId` falls back to `N'UTC'` when no store zone is configured. **`SubsidyEnabled`** is the SQL mirror of `CWFeatureSet.Subsidy` (see "The subsidy switch", §1). |

### 9.1 How Credit Resolution Works in the Views

`vw_CORE_SUB_SubsidyPackNameCredit` is the single source of truth for a PackName's effective
credit. Its `Credit` column accounts for variations automatically:

```sql
-- When PackNamesOwnGlobalValue = ON: use variation credit if present, else base credit
-- When PackNamesOwnGlobalValue = OFF: always use base credit
CASE WHEN vset.PackNamesOwnGlobalValue = 1
     THEN COALESCE(svar.Credit, s.Credit)
     ELSE s.Credit
END AS Credit
```

The `CreditTopup` column **`SUM`s** `SubsidyTopup.Credit` for the current occurrence only, through
an `OUTER APPLY` — windowed by `CreatedOnUtc` converted to the store's zone and bucketed by
`subsidy_topup_period_end`. It is a `SUM` rather than a join precisely so that a duplicate
topup row can never multiply the view's output: duplicates are harmless here rather than
impossible, because a topup's period is derived, not a column, and so cannot be made unique by an
index. (A *variation* can, and is: `UX_CORE_SUB_SubsidyVariation_SubsidyPackNameId`, filtered on
`Deleted = 0`.)

Both credit views also require `s.Active = 1` and exclude soft-deleted orders (`o.Deleted = 0`) as well as cancelled
ones (`OrderStatusId <> 40`) — an inactive subsidy grants nothing, and a cancelled order releases its credit. The two
are different states: nop's `DeleteOrderAsync` sets `Deleted` and leaves the status, so the views test both.

`vw_CORE_SUB_SubsidyVariationInOrderingPeriod` enables per-PackName period overrides:

```sql
-- INNER JOIN on setting = 1 means this view returns zero rows when the setting is OFF
-- Callers LEFT JOIN this view, so they fall back to the base subsidy period automatically
INNER JOIN vw_CORE_SUB_StoreSubsidyVariationSetting vset
        ON s.StoreId = vset.StoreId
       AND vset.PackNamesOwnStartAndFinishDates = 1
```

**Consequence**: C# service code never needs to check these settings directly. The views
always return the correct effective credit and period for each PackName.

### Important distinction: SubsidyPackNameCredit vs SubsidyPackNameCanOrder

`SubsidyPackNameCredit` filters by the current ordering period (the store's today inside `subsidy_current_period`),
making it correct for credit/quantity calculations. It does NOT include subsidies that are
outside their period (even if `OrderOutsidePeriod = true`).

`SubsidyPackNameCanOrder` computes `OnlyAllocatedProducts` across ALL active (non-deleted,
active flag) subsidy assignments regardless of ordering period. Use `ISubsidyOrderingService`
methods (which join the ordering-period views) for period-correct active-subsidy checks.

---

## 10. Caching Strategy

| Data | Cache Type | Cache Key | TTL | Invalidation |
|---|---|---|---|---|
| `PackNameCredit` (full credit object) | memo in `SubsidyCreditService` (scoped, per request) | — | Request lifetime | Cleared by consumption/restore writes and before settlement |
| Next ordering-status change of a packname | `IStaticCacheManager` | `PackNameNextSubsidyStatusChangeCacheKey` | Until the instant passes | Subsidy, SubsidyPackName, SubsidyVariation, PackName, setting events |
| `SubsidyPackNameCanOrder` (CanOrder + OnlyAllocatedProducts) | `IStaticCacheManager` | `CW.subsidy.orderingblocked.bypacknameid.{0}` | Smart TTL* | PackName/SubsidyPackName change events |
| In-ordering SubsidyPackNames | `IStaticCacheManager` | `CW.subsidy.inordering.subsidies.bypackname.{0}` | Smart TTL* | PackName/SubsidyPackName change events |
| Ordering product IDs | `IStaticCacheManager` | `CW.subsidy.orderingproducts.bypacknameid.{0}` | Smart TTL* | PackName/SubsidyPackName/ProductGroup change events |
| PackName has active product groups | `IStaticCacheManager` | various `PackNameHasActiveProductGroupsCacheKey` | Smart TTL* | PackName/SubsidyPackName change events |
| Visible categories / manufacturers of a restricted packname | `IStaticCacheManager` | `CW.catalogue.visible.{products,categories,manufacturers}.{fingerprint}` (`PackNameServicesDefaults`) | Default | The four `Catalogue*CacheEventConsumer`s (category, product–category, product–manufacturer, product) |
| An order's settlement row | `IStaticCacheManager` | `CW.order.settlement.{orderId}` | Default | `OrderSettlement` cache consumer on insert; a null is never cached |
| Hidden packname cart lines / per-packname cart maps | per-request (`_shortTermCacheManager`) | `CW.packname.hiddencartitemids.{customerId}`, `CW.cart.*` | Request | — |

*Smart TTL: `GetPackNameCacheTimeInMinutesAsync` calculates the exact number of minutes until
the PackName's next ordering-period status change (subsidy period start or end), at least
`MinimumOrderingCacheMinutes` and capped at the system default cache time. This ensures the cache expires precisely when the ordering eligibility
changes, not sooner or later.

---

## 11. Admin Management

Subsidies and product groups are managed inside **Manage Store** (CW Admin › Stores › *store*), on the store's
*Subsidies* and *Product groups* tabs; every `SubsidyController` action takes the store id and renders a partial of
that page (`Areas/Admin/Views/Subsidy/`). All of them require `CW.CORE.Admin.AccessMultiStore`. There is no separate
Subsidies menu entry.

Every money and quantity input is validated: subsidy credit and allocation / variation credit `0 … 1,000,000`
(`SubsidyTopupValidator.MaximumTopupCredit`), a top-up `> 0` and `≤` the same ceiling, a product-group top-up with at
least one of quantity / credit `> 0` and neither negative; a variation has both dates or neither, end after start. The
Excel import reuses the same ceiling. An overflow or a negative is a validation message, never a 500.

On the subsidy grid, *Total credit* is the **allocation** (`Credit` × assigned packnames), not the sum of in-period
usage rows — a season not yet open would otherwise read 0.

The PackName editing screen (Manage Store › PackNames, and the customer edit page's Subsidies card — shown only to a
role holding `CW.CORE.Admin.AccessMultiStore`; the component also answers empty without it) includes:
- **Subsidies tab**: lists all subsidies assigned to the PackName, showing credit, used amounts,
  period, and flags. Allows adding/removing subsidy assignments.
  A **manager** can also tick / untick the store's active subsidies on the storefront *Add / Edit packname* form
  when `AllowSubsidySelection` is set (phase 7, decision 10); the POST honours the list only when the setting and
  the Subsidy feature are on, and logs each assignment and removal.
- **PackName-level controls**: variation (credit/period override) and topup entry per subsidy.
- **Product group topup entry**: per product group, additional quantity or credit can be added.

Product groups are managed separately as store-level entities and are then linked into
subsidies via the Subsidy edit screen. A new group's default colour is minted by the model factory
(`NewProductGroupColour`), never by the view — a view does not assign the model; the preview icon is painted with the
group's colour.

The packname subsidies panel (store page and customer page) lists **every live assignment, in period or
not** — a season is set up before it opens — with Total / Used / Remaining credit, and units when any
product group allocates them. **One percentage rule** serves the panel and both grids: used ÷ total credit
when there is credit, otherwise used ÷ total units, otherwise "-". The storefront bar and catalogue list in-period
assignments only (a subsidy whose season has not started is not shown unless `OrderOutsidePeriod`); *My Subsidies*
(§8.3) lists every live assignment with its status. On the panel and the grids an assignment outside its ordering
period reads "—" for *% Used*, with the tooltip "Not in its ordering period" (phase 7, decision J).

**Deleting a store** soft-deletes, never removes, everything this plugin keeps for it — packname lists
(and their packnames and those packnames' subsidy assignments), branches, departments, cost centres,
subsidies (and their assignments and allocations) and product groups — in
`Services/Stores/StoreDeletedEventConsumer`. Credit notes, return cases, exchange / return rules and
impersonation tokens have no soft-delete flag and are left as they are.

---

## 12. ISubsidyReportingService — Credit-Aware Queries

`ISubsidyReportingService` is the correct service for any feature that needs to **display**
subsidy credit or product-group allocation to users (public store or admin). It reads from the
credit views (`vw_CORE_SUB_SubsidyPackNameCredit`, `vw_CORE_SUB_ProductGroupPackNameCredit`) so
the returned values already account for `PackNamesOwnGlobalValue` and period-windowed topups.

| Method | Returns | Use When |
|---|---|---|
| `GetSubsidyDetailsByPackNameIdAsync(packNameId, inOrderingPeriodOnly)` | `IList<SubsidyPackNameDTO>` — one entry per **live assignment** (assignment-driven, not view-driven) | The one source for the admin panel and grids (`inOrderingPeriodOnly: false` — a season being set up is listed), *My Subsidies* (`false`) and the storefront bar / catalogue (`true`). Never lists a deleted variation; applies the variation's credit and dates only where the store's `PackNamesOwn*` settings let it; a missing assignment is an exception, not a null. **Use this instead of calling `GetSubsidyByIdAsync` and reading `.Credit`** — the latter gives the raw base credit, bypassing variations. |
| `GetSubsidyPackNameProductGroupDetailsAsync(subsidyPackNameId, subsidyId, activeOnly = false)` | `IList<SubsidyProductGroupDTO>` — product groups with their base and topup amounts, used and available | The single per-group source for the admin panel, the storefront bar and *My Subsidies* (which passes `activeOnly: true`). |
| `GetSubsidyPackNameDetailsByIdAsync(subsidyPackNameId)` | Single `SubsidyPackNameDTO` | Load a single assignment's details (e.g. for a topup form header). |
| `GetSubsidyPackNameBasicInformationByIdAsync(subsidyPackNameId)` | Single `SubsidyPackNameDTO` (lightweight, no credit computation) | Retrieve name/ID for display only — cheaper than the full details query. |
| `GetSubsidyUsageBySubsidyIdsAsync(subsidyIds)` | `IList<SubsidyPackNameUsageDTO>` | The subsidy grid's used / remaining columns, one query for a page of subsidies. |
| `GetSubsidyPackNameHistoryAsync(packNameId, subsidyId, pageIndex = 0, pageSize = int.MaxValue)` | Paged `SubsidyHistory` | The history grid (admin) and *My Subsidies*, which reads it whole and sorts it by season (§8.3) — §12.1. |

**Key rule**: never call `ISubsidyService.GetSubsidyByIdAsync(id)` and use `.Credit` directly
for display purposes. That field is the raw base credit and ignores any `SubsidyVariation`
credit override. Use `GetSubsidyDetailsByPackNameIdAsync` so the view's resolved credit is used.

### 12.1 The history procedure — `subsidy_get_history`

`subsidy_get_history (@PackNameId, @SubsidyId, …)` unions the assignment, variation, top-up, product-group,
consumption, shipping and restore ledgers for one (packname, subsidy) into `SubsidyHistory` rows (`HistoryType`:
Subsidy 1, SubsidyTopUp 2, ProductGroup 3, ProductGroupTopup 4, SubsidyUsed 5, ProductGroupUsed 6, SubsidyVariation 7,
SubsidyUsedForShipping 10, Refund 12 — `Domain/Subsidies/Views/HistoryType.cs`; the pre-order values 8, 9 and 11 do
not exist). Rules the arms share:

- **`CreatedOn` is true UTC on every arm**, cast `AT TIME ZONE 'UTC'` to `datetime2`; the reader labels it `Utc`
  before converting to the store's zone. (In a `UNION` a `datetimeoffset` arm widens the others and the reader then
  materialised the web server's wall clock — two hours late on a UTC+2 host.)
- **The variation arm names who varied**, not who assigned (`svarh.CreatorId`), and is listed whatever the store's
  `PackNamesOwnGlobalValue` — a dates-only variation has history too.
- **`AssignmentRemoved` is one scalar for the whole result** (`EXISTS` no live assignment), never a join: a packname
  removed and re-added has two assignment rows, and a join doubled every history row.
- Soft-deleted orders are excluded (`o.Deleted = 0`) on every consumption arm.

It is executed with `_dataProvider.QueryAsync` from `SubsidyReportingService` (not through `ICWDatabaseService`, which
only creates the objects).

---

## 13. Entity Relationship Summary

```
ProductGroup ──< ProductGroupProduct >── Product (NOP)
     │
     └──< SubsidyProductGroup (credit, qty per subsidy) >── Subsidy
                                                                 │
                                                                 └──< SubsidyPackName >── PackName
                                                                          │
                                                                          ├── SubsidyVariation       (credit/period override, max 1 live)
                                                                          │     └── SubsidyVariationHistory  (audit log per save)
                                                                          ├──< SubsidyTopup          (one row per top-up save)
                                                                          │     └── SubsidyTopupHistory      (audit log per save)
                                                                          └──< ProductGroupTopup     (one row per top-up save, per group)
                                                                                └── ProductGroupTopupHistory (audit log per save)
```

Order recording:

```
Order (NOP)
  ├── OrderSettlement        (one row: what subsidy and store credit settled — §7)
  ├──< SubsidyOrderItem      (per OrderItem: which credit sources were consumed)
  ├──< SubsidyOrderShipping  (per PackName: shipping credit consumed)
  └──< SubsidyRestoreItem    (per authorised return line: what came back, bucket for bucket — rule 17)
```

---

## 14. Key Rules Summary

1. A subsidy only applies credit when the PackName is within the configured ordering period
   (or variation period if enabled). Outside-period ordering is allowed by `OrderOutsidePeriod`
   but no credit is drawn.

1a. **A period is the STORE'S local time, and an annual period has real occurrences** — not a
   month/day comparison. Anniversaries clamp to the end of the month (29 Feb → 28 Feb), a season
   may wrap the calendar year, and a closed season resets on its own with nothing scheduled and
   no balance written back. `subsidy_current_period` and `Helpers/SubsidyPeriodHelper` are two
   halves of one rule: change one, change the other, and keep `SubsidyPeriodHelperTests` green.
   See §3.

1b. **Ledger column types.** The money columns of the subsidy, allocation, top-up, consumption, shipping and restore
   tables are `DECIMAL(18, 4)` (`AsDecimal(18, 4)` in their builders under `Data/Mapping/Subsidies/`), matching nop
   core and the 4-dp rounding in `RestoreSubsidyForReturnedItemAsync`; `SubsidyVariation.Credit` and the three history
   tables' `Credit` keep the builder default `AsDecimal()` = `DECIMAL(19, 5)` — nothing is truncated
   (`Subsidy_PackName_Open_Items.md` 3.15). Every subsidy (and branch, list, customer-branch) `CreatedOnUtc` is
   `datetime2` holding UTC (`AsDateTime2()`), the same as nop's own. In 5.00 these types come from the builders,
   which the one `Data/Migrations/SchemaMigration.cs` creates on install; in 4.9 the migrations `SubsidyPeriodIntegrity`
   and `CWTimestampsDateTime2` (2026-10-20) altered them in place. A bound handed to SQL must be UTC, and an absent
   bound `null`.

2. `OnlyAllocatedProducts` applies only when **all** currently active subsidies for the PackName have
   it set. `RestrictToAllocatedQty` is decided per product by the active subsidies **containing that
   product** only (§5.2).

3. `IncludeManagers = false` exempts manager PackNames from product and quantity restrictions;
   `IncludeManagers = true` applies both to them (§5.5).

4. Credit is consumed in strict priority order (quantity first, then product-group credit,
   then global credit, each base before topup).

5. `AvailableQuantity` in the credit views reflects placed orders only — not the current
   shopping cart. What the cart takes is counted live at add-to-cart time through the subsidy
   allocation of the packname's cart (§5.2).

6. `order.OrderTotal` = what the customer pays; the gross columns stay nop's. What subsidy and store credit
   settled is the order's settlement row `CORE_PN_OrderSettlement` (§7); the per-source detail stays in
   `SubsidyOrderItem` / `SubsidyOrderShipping` and the credit-note history.

7. The smart TTL cache means period transitions (subsidy opens or closes) are reflected in the
   public store at the moment the period changes (at most `MinimumOrderingCacheMinutes` late), not
   after a fixed delay — store midnight included, with nothing changed in admin.

8. `RestrictToAllocatedQty` does not cap a product while a subsidy or group containing it still has
   credit **available** (credit, topups, variation credit, less placed orders); the cap returns once that
   credit is spent. Only the subsidies containing the product decide, and the whole packname cart counts
   (§5.2).

8a. **`RestrictToAllocatedQty` survives a save only on a subsidy with `OnlyAllocatedProducts` on,
   `OrderOutsidePeriod` off and zero global credit.** `SaveSubsidyAsync` clears it otherwise, whatever was posted,
   and the admin checkbox is shown only under the same conditions. That is the rule enforcing itself, not a broken
   screen. Product-group credit is not examined at save; it lifts the cap at order time. See §5.2.

9. Topups are period-scoped and do not carry over when the ordering period ends. Period
   membership is determined dynamically at read time from `CreatedOnUtc` and the subsidy's
   current dates — no period dates are stored on the topup record. This means changing
   subsidy dates automatically re-evaluates existing topups with no data fixup. Because the
   period is derived rather than stored, **a duplicate topup row cannot be prevented by a unique
   index**; the credit views `SUM` topups instead, which makes a duplicate harmless rather than
   impossible. A *variation* has no period dimension and so is one row per assignment by
   definition — that one IS an index (`UX_CORE_SUB_SubsidyVariation_SubsidyPackNameId`,
   filtered on `Deleted = 0`), as are the assignment and the allocation (§2.3, §2.4).

10. A topup outside the current occurrence's window is **refused, not stored** — by the guard
    (`IsTopupPeriodOpenAsync`) and by the save itself. There is no pre-loading of credit for a season that has not
    opened.

11. **Every top-up save inserts a row** (`SubsidyTopup`, `ProductGroupTopup`) plus its history row; nothing reads or
    updates an existing top-up, and the views `SUM` the rows of the current occurrence. The dialog's *Topup Credit*
    field is an **amount to add** and always opens at zero (`PrepareSubsidyTopupModelAsync` builds a blank model) —
    it is not a balance, and its reading zero says nothing about whether top-ups exist.

12. `PackNamesOwnGlobalValue` and `PackNamesOwnStartAndFinishDates` are enforced **at the SQL
    view level only** — C# service code never checks these settings directly. When OFF, the
    views silently return base credit / base period as if no variation exists. (`SubsidyPeriodHelper` follows the
    same settings for the top-up window and the smart TTL — §3.)

13. Never call `ISubsidyService.GetSubsidyByIdAsync(id).Credit` to display a PackName's credit.
    Use `ISubsidyReportingService.GetSubsidyDetailsByPackNameIdAsync(packNameId, inOrderingPeriodOnly)` — the returned
    DTO's `Credit` field is already variation-resolved by the view.

14. Topup amounts entered by a manager (admin or public store) are always **additive** — each save is a new row
    with the amount entered, never an absolute value. Enforced by the service methods; the validators refuse `≤ 0`
    and cap the amount.

15. The public store Change Topup button is only visible to manager PackNames and requires both
    `AllowEditSubsidyTopup` and `AllowManagersToAddPackNames` settings to be enabled. Topups
    are always applied to the currently selected **employee** PackName, never the manager's own.

16. **Subsidy is consumed before credit notes** (decision 15). The pipeline order is data
    (`ISettlementSource.Priority`): subsidy 1, credit notes 2.

17. **A return gives subsidy back in kind** — units to the same product group, group credit to
    that group, global credit to global, topups to their topup bucket — as insert-only rows in
    `CORE_SUB_SubsidyRestoreItem`, netted into the availability views. This happens when the line
    is authorised, before the replacement reaches the cart, for every accepted line in both
    Exchange and Credit-Note mode (decisions 7, 14, 18). Only the money the customer put in comes
    back as a credit note. Idempotency is keyed on the return case line: a line executed again (undo and re-approve,
    a re-opened case) first deletes its own restore rows (`DeleteSubsidyRestoreItemsForCaseLineAsync`), then writes
    them afresh.

18. **Shipping paid by subsidy is not restored on a return** (decision 21). The parcel was shipped;
    `SubsidyNetConsumptionSource` nets item buckets only.

19. **A restoration is scoped to the original order's subsidy period** (decision 22). A return of
    an order from a previous period restores into that lapsed period and nothing becomes spendable.
    Whether the order was invoiced never changes what comes back (decision 20).

20. **The selected packname is an authorisation input, not a preference** (phase 2a). Every storefront
    rule that spends subsidy — the catalogue, the bar, add-to-cart, the packname checkout — acts on
    whatever `IPackNameContext` returns, so the context is resolved with the same question the guards
    ask: the stored selection must be non-deleted, in the current store, and either the customer's own
    packname or one a manager still owns by `IsPackNameOwnedByManagerId`. Anything else is cleared and
    falls back to the customer's own packname. Before that, revoking a manager's department left the
    old packname in context and it kept spending that employee's subsidy (gap analysis U-38).

21. **A packname order is placed as a packname order whichever door it comes through.** nop's
    `CheckoutController` (every action except `Completed`) and `ShoppingCartController.Cart` are intercepted and
    sent to the packname checkout; `PlaceOrderAsync` — the door a payment method that places its own order comes
    through (PayPal Commerce) — reads the customer's raw `PackNameCartItem` rows and delegates to
    `PlaceDelegatedPackNameOrderAsync`, which places a full packname order (destinations, `PackNameOrderItem`,
    settlement) after three checks: the cart is a packname cart (`PackNameCartHasOtherItems` otherwise), it can be
    ordered (`PackNameCartNotOrderable`), and the method's stated `OrderTotal` equals the packname order's
    (`PaymentAmountDiffers`); `ProcessNextRecurringPaymentAsync` is refused when the initial order carries packname
    items (recurring packname orders are unsupported). All of it is gated on `CWFeature.PackName` for that store, so
    switching packnames off returns the store to stock nop behaviour. The hand-over to such a method is described in
    `PackName_Order_Shipments.md` §9; the methods that place their own order are
    `MultiStoreSettings.OwnPlacementPaymentMethods`.

22. **Deactivating a packname list hides it from ordering and does not revoke administration**
    (phase 2b part 1, decision 1 of 2026-09-17). `PackNameList.Active` is an ordering flag, not an
    ownership flag, so the two questions are asked with different arguments of the same procedure:
    `packname_get_listids_by_manager @RequireActiveList = 1` for the selector and the ordering paths,
    `= 0` for `IsPackNameOwnedByManagerId`. A manager therefore keeps the edit and the top-up for
    every packname in a list they have just deactivated — which is the point, since a deactivated
    list is usually one that needs fixing. The two answers are different sets and are cached under
    different keys (`MultiStore_Management_Reference.md` §7.1). Before this, unticking Active locked
    the manager out of exactly the records they had to repair (gap analysis P-20).

23. **A placed packname order is not editable through nop's admin order screens** (phase 2b part 1,
    decision 14). `PackNameOrderAdminGuardFilter` blocks `EditOrderItem`, `DeleteOrderItem`,
    `AddProductToOrder`, `AddProductToOrderDetails`, `AddressEdit`, `EditOrderTotals` and
    `EditShippingMethod`; `CancelOrder`, `Delete`, the refund and void actions and `AddShipment` are
    deliberately left open (nop 5.00 has no stored cards, so 4.9's `EditCreditCardInfo` is gone). Since the port's
    part 4f the order page does not offer *Edit order totals* or the billing *Edit* on a packname order, which the
    guard always refused. Cancelling **is** the designed subsidy-restore
    path, so blocking it would break the restore. A packname-aware rebuild of those screens (it would have to re-run
    settlement) is not planned — `Subsidy_PackName_Open_Items.md`.
    Every identifier the action acts on is tested, not only the posted order id — see
    `PackName_Order_Shipments.md` §9.1.
