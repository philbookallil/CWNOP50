# CW Feature Toggles — Complete Reference

Migrated to nopCommerce 5.00 on 2026-10-31: checked against the 5.00 code; the 4.9 copy is in `NOP49/CWPlans/`.

> **What this is.** The single source of truth for the per-store Feature Toggles system in
> `Nop.Plugin.CW.Core`: what it is, how it's built, everywhere it's wired in, and the design
> decisions behind it — including the ones that got reversed after review.
>
> **Read §1 (the golden rule) before touching anything in this area.** Almost every mistake made
> while building this — and there were several, documented in §6 — was a violation of it.
> If you are about to write code and just need to know what to do, §0 is the short version.
>
> **State:** implemented and code-reviewed. Every finding from the 2026-08-06 review has been applied
> except the items in §9, which are low-priority or need an architectural decision first.

---

## 0. Quick start — gating something new

If you're adding code and just need to know what to do, read this section and §1.1. Everything else
is reference.

### 0.1 Step 1 — decide *whether* it should be gated at all

Ask what the thing **is**, not how old the data is (§1.2):

| The code… | Gate it? |
|---|---|
| Creates a new feature record, or opens a form/link that leads to creating one | **Yes** |
| Consumes feature data on a *new* transaction (applies credit, spends subsidy, splits a cart) | **Yes** |
| Displays or completes a record that **already exists** | **No** — never |
| Reads anything hanging off an order (subsidy breakdown, credit note applied, return request) | **No** — never |
| Reverses/undoes something (order cancelled → return credit) | **No** — never |
| Shows a **balance the business owes** (credit notes) | **Not a flat gate** — `enabled ‖ has data`, read-only when off |
| Is a cache consumer, migration, or route registration | **No** — never |

When in doubt, re-read §1.1. The most common mistake on this codebase, made three times (§6.1–6.3),
was gating something that only ever runs against an already-existing record.

### 0.2 Step 2 — pick the right mechanism for where the code lives

| Where | How | Notes |
|---|---|---|
| **Controller action** | `[CheckCWFeature(CWFeature.X, StoreSource = …)]` | The default. See §0.3 for choosing `StoreSource`. |
| **Controller action whose store needs a DB lookup** | inject `ICWFeatureGuard`, call `BlockIfDisabledAsync(...)` | For actions receiving only an entity id where no `StoreSource` fits. |
| **View component** | **inline check inside `InvokeAsync`** | ⚠ The attribute is silently inert here (§6.8). Return `Content(string.Empty)`. |
| **Razor view** | `@{ var features = await cwFeatures.GetFeaturesAsync(storeId); }` then `@if (features.X)` | Already `@inject`-ed in both `_ViewImports.cshtml`. One call per render, reuse the snapshot. |
| **Service / business logic** | inject `ICWFeatureService`, call `IsEnabledAsync` / `IsEnabledForOrderAsync` | Order-scoped work must use `IsEnabledForOrderAsync(feature, order)`, never the current store. |
| **Model factory** | set a `bool` on the model; view reads the flag | Never put the feature expression inline in a view (§1.3). |
| **Admin menu / anything with no store scope** | `IsEnabledForAnyStoreAsync` | |
| **Client-side panel loading** | pass the flags into JS `init(...)` | Only where JS fires its own AJAX (§4.11). |

### 0.3 Step 3 — pick `StoreSource`

**This is where mistakes happen**, and they fail *closed and silently* (§6.9) — the action just
stops working, on every store, with only a log line.

| The action receives… | Use |
|---|---|
| `int storeId` (parameter, model property, route or query value) | `Parameter` |
| a `storeId` that is legitimately `0` (all-stores defaults page) | `Parameter` + `AllowDefaultStore = true` |
| `int orderId` | `Order` |
| `branchId` / `listId` / `packNameId` / `departmentId` / `subsidyId` / `productGroupId` / `subsidyPackNameId` | the matching entity source (§2.5) |
| the id under a different name than the default | the matching source + `ParameterName = "…"` |
| nothing store-identifying, and it's a **public store** action | `CurrentStore` — state it explicitly, there is no default |
| nothing store-identifying, and it's an **admin** action | ⚠ **not** `CurrentStore` — in the admin area that resolves to the *default* store, not the one being edited. Use an entity source or an inline guard. |

**`StoreSource` is mandatory.** Omitting it leaves the attribute on `CWFeatureStoreSource.NotSet`,
which blocks the action and logs an error — deliberately, so that forgetting it is loud instead of
quietly falling back to the current store (§2.5).

Two rules that catch most of the rest:

- **Create/edit actions gate on the *parent* id, never the nullable child id.** `PackNameListDetail(int
  branchId, int? listId)` gates on `branchId` — on create, `listId` is null and would fail closed.
- **Verify against the caller.** Open the view/JS that calls the action and confirm it actually sends
  the parameter your `StoreSource` looks for. §6.9 was exactly this mistake.

### 0.4 Copy-paste patterns

```csharp
// Controller action — the common case
[CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE)]
[CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Subsidy)]
public virtual async Task<IActionResult> SubsidyProductGroups(SubsidyProductGroupSearchModel searchModel) { … }
```

```csharp
// Controller action — inline guard, after loading the entity and after its null check.
// Inject ICWFeatureGuard; it wraps the feature check and the block-result shaping together.
var subsidy = await _subsidyService.GetSubsidyByIdAsync(id);
if (subsidy == null)
    return new ForbidResult();

if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.Subsidy, subsidy.StoreId, Request) is IActionResult blocked)
    return blocked;
```

```csharp
// View component — attributes DO NOT work here
public async Task<IViewComponentResult> InvokeAsync(int storeId)
{
    //filters do not run for view components, so the check must be here
    if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, storeId))
        return Content(string.Empty);
    …
}
```

```csharp
// Service, order-scoped
if (!await _cwFeatureService.IsEnabledForOrderAsync(CWFeature.CreditNotes, order))
    return;
```

```cshtml
@* Razor — one call, reuse the snapshot *@
@{
    var features = await cwFeatures.GetFeaturesAsync(Model.StoreId);   // admin: ALWAYS explicit
}
@if (features.Subsidy) { … }
```

```csharp
// Model factory — "enabled OR has data" (liability only, §1.3)
model.CreditNotesEnabled = await _cwFeatureService.IsEnabledAsync(CWFeature.CreditNotes, storeId);
model.DisplayCreditNotes = model.CreditNotesEnabled
    || await _creditNotesService.CustomerHasCreditNotesAsync(customer.Id);
```

### 0.5 Before you call it done

- [ ] Exercised the new screen once — the nop log has **no** `could not resolve store id` warning
- [ ] The feature-off path was tested, not just assumed
- [ ] If it's a view component: the guard is *inside* `InvokeAsync`, not an attribute
- [ ] If it's an admin action: `StoreSource` is not `CurrentStore`
- [ ] If it displays existing records: it is **not** gated (§0.1)
- [ ] If it reads `MultiStoreSettings.SubsidyEnable` directly: use `ICWFeatureService` instead — the
      `Subsidy && PackName` implication only exists in `CWFeatureSet.From` (§2.2). SQL is the one
      exception: it reads the mirror column `SubsidyEnabled` (§1.5)
- [ ] If it *restricts* what another feature does (blocks, filters, caps, hides): it switches off with its
      own feature too, and the feature-off test checks that the restriction is gone (§1.5)
- [ ] If the rule is in SQL (view, function, stored procedure): it joins
      `vw_CORE_SUB_StoreSubsidyVariationSetting.SubsidyEnabled`, or every caller is gated (§1.5)

---

## 1. What this is and the rule that governs it

CW runs many customer-facing "stores" off one nopCommerce install (`Nop.Core.Domain.Stores.Store`).
Five CW capabilities can be switched on or off **per store**:

| Feature | Backing setting (`MultiStoreSettings`) | What it gates |
|---|---|---|
| `PackName` | `PackNameEnable` | Employee/"PackName" accounts, PackName checkout, branch/department/cost-centre assignment |
| `Subsidy` | `SubsidyEnable` | Per-PackName subsidy allocation, subsidy catalog, subsidy credit applied at checkout |
| `Exchanges` | `ExchangesEnable` | The CW return/exchange wizard (replaces nop's standard return request flow) |
| `CreditNotes` | `CreditNotesEnable` | Store-credit issuance and consumption |
| `BulkOrder` | `BulkOrderEnable` | The bulk-ordering page, its header link and the product-page grid (§4.12; the bulk-ordering docs) |

Before this system, gating was done by checking whether a companion plugin was installed
(`PluginHelper.IsExchangesPluginInstalled()` etc.). That was replaced with a per-store setting and
one reusable service (`ICWFeatureService`) so the check is never duplicated or drifted.

### 1.1 The golden rule

> **Gate the doors, not the windows.**
>
> When a feature is **off** for a store: block every path that *creates, consumes, or modifies*
> that feature's data, and hide every entry point that invites the user to do so.
> **Never** hide the display of records that already exist, and never prevent in-flight work from
> being completed.

An order placed last month with a subsidy applied must still show its subsidy breakdown after
`SubsidyEnable` is turned off. A credit note with a remaining balance must still be visible and
still be reversible if the order is cancelled. A return request that was `NeedsCompletion` when
`ExchangesEnable` was switched off must still be completable by an admin.

### 1.2 Three categories — the question is never "is this old data?", it's "what is this thing?"

| Category | What it is | Rule | Examples |
|---|---|---|---|
| **Liability** | The business owes the customer something that survives the feature being switched off. Hiding it does not discharge it. | **Display always; block writes.** `Display = enabled ‖ has data`, `Enabled` drives the buttons/forms. | Credit note balance and history |
| **Capability** | A forward-looking ability that is inert the moment the feature is off. Nothing is owed; re-enabling restores it. | **Hide outright.** `Display = enabled`. | PackName assignment, subsidy allocation/entitlement |
| **Order-attached record** | A fact about a transaction that already completed. | **Never gate.** The order page is feature-independent. | Subsidy applied to order #123, credit note applied to order #456, a return request on order #789 |

Why credit notes differ from PackNames, concretely: a credit-note balance is money owed, there is a
`StoreLiabilityList` report built to answer "what do we owe?", and the customer can see it on
*Credits for me*. A PackName assignment is a configuration link — hiding it loses nothing.

**Subsidy sits with PackName, not with credit notes.** `CWFeatureSet.From` computes
`Subsidy = SubsidyEnable && PackNameEnable`, so whenever PackName is hidden, Subsidy is too. With
Subsidy off, an allocation can't be spent — the subsidy bar, catalog, cart application and order
totals are all blocked. The durable record of subsidy *usage* lives on the order (`SubsidyOrderItem`,
the settlement row `CORE_PN_OrderSettlement`) and stays visible there regardless. So the customer-page subsidies
panel is an entitlement view → **capability** → hide outright.

### 1.3 The "Enabled OR Has Data" pattern

Several UI containers must appear when the feature is off but records exist. This is defined once
per feature as a model flag set by the factory — **never** as an inline expression in a view:

```csharp
// in the model
public bool DisplayCreditNotes { get; set; }

// in the factory
model.DisplayCreditNotes =
    await _cwFeatureService.IsEnabledAsync(CWFeature.CreditNotes, storeId)
    || await _creditNotesService.CustomerHasCreditNotesAsync(customerId);
```

When the feature is off and the container is shown only because data exists, render it **read-only**:
hide add/edit/delete buttons and any write-affordance widgets.

### 1.4 Read paths that must NEVER be gated

| Site | Why |
|---|---|
| Cancelling an order: the credit-note and subsidy views (`Services/DataBase/CWDatabaseService.cs`) leave out orders with `OrderStatusId = 40` (cancelled), so the credit and the subsidy come back with no event and no write | Cancelling an old order must return its credit / subsidy regardless of the store's current setting. *(The former `OrderStatusChangedEvent` handler and its `UndoCreditNotesForOrderAsync` / `UndoSubsidyForOrderAsync` no longer exist.)* |
| `ICreditNotesService.GetCreditNoteHistoriesByOrderIdAsync` / `SearchCreditNotesByCustomerIdAsync` / `GetOpenCreditNotesByCustomerIdAsync` / balance checks | Balance and history display |
| `CreditNote.Remain` display anywhere | A customer with a live balance must be able to see it |
| `CORE_PN_OrderSettlement` (`ICWOrderService.GetOrderSettlementAsync`), `SubsidyOrderItem`, `SubsidyOrderShipping` reads on order detail (admin + public), the order e-mail, the invoice PDF and the profit report | Subsidy and store-credit breakdown of past orders — verified with subsidies switched off (phase 6 T13, 2026-10-02) |
| `ICWReturnRequestService` / `IExchangeService` reads for existing `ReturnRequestItem` rows | Existing return requests must remain viewable and processable to completion |
| `PackNameOrderItem` reads on order detail, the invoice and the packing slip (the For line, `PdfOverrideService.GetOrderProductItemsAsync`) | Which employee each line was for — verified with subsidies switched off (phase 6 Part 2 R1, 2026-10-07) |
| `IExchangeService.GetOrderReturnFlagsAsync` (`CWOrder/OrderBadges`, the E / R badges on the admin order lists) | Badges of past exchanges and returns — verified with subsidies switched off (phase 6 Part 2 R1) |
| `ICacheEventConsumer<T>` implementations under `Services/*/Caching/` | Cache correctness is not a feature |
| `Data/Migrations/*` | Schema is always created; the toggle is behavioural only |
| `RouteProvider.cs` | Routes are registered once at startup with no store context — gate the **controller actions**, never the route table |
| `ExchangesConfigController.GetOrderReturnRequests` | Pure read of return requests that already exist — see §6.2 for why an earlier draft got this wrong |
| Receipts for existing return requests (`GenerateReturnRequestReceipt(s)`) | In-flight/completed work, not creation. *(`EditExchangedItem` was listed here; the code gates it — see §6.2.)* |

**Corollary — in-flight work must complete.** Gate the *creation* of new records, never the
completion of ones that already exist (e.g. `ExchangesController.ReturnRequestGatherInfo`, not the admin completion
workflow; the credit-notes balance check inside `ExchangesLogic.ExecuteExchangeWorkflowAsync`).

### 1.5 Off means no effect — a feature's restrictions switch off with it

The golden rule is written about doors: actions, views, entry points. Its other half is easy to miss
because it does not look like a door. **When a feature is off for a store, nothing that feature has stored
may change how the rest of the store behaves.**

A feature does not only *offer* things; some also *restrict* what other features do. Subsidy is the one
that matters: it refuses ordering when a packname's period has closed, narrows the catalogue to allocated
products, caps quantities and hides cart lines. Every one of those restrictions is a subsidy reader and
switches off with the feature, exactly like the subsidy bar. With subsidies off, a packname behaves as if
it had no subsidy at all, however many subsidy rows the database still holds. Switching the feature back on
restores every restriction, because the rows were never touched.

**SQL is a reader too.** `ICWFeatureService` cannot reach a view, and several subsidy rules live in SQL.
Those views read the switch from **`vw_CORE_SUB_StoreSubsidyVariationSetting.SubsidyEnabled`**, the one
SQL mirror of `CWFeatureSet.From`: `SubsidyEnable` and `PackNameEnable`, the store's value first, then the
instance value, off when neither is set (the same answer as `GetFeaturesAsync`). It is a second definition
of one rule, allowed only because SQL cannot call C#; **if `CWFeatureSet.From` changes, that column changes
in the same change set.** Any new view, function or query that decides something from subsidy data either
joins that column or is called only from a gated C# path.

Where subsidies are switched off today:

| Layer | Where | What it covers |
|---|---|---|
| SQL | `vw_CORE_SUB_SubsidyPackNameCanOrder` joins `SubsidyEnabled = 1` | Can-order rule: cart view (`vw_CORE_PN_PackNameShoppingCartItem`), add-to-cart block, selector, info strip notice, catalogue block |
| SQL | `vw_CORE_SUB_SubsidyInOrderingPeriod`, `vw_CORE_SUB_SubsidyVariationInOrderingPeriod` join `SubsidyEnabled = 1` | "Only allocated products" (catalogue lists and bulk order), the info strip's subsidy button, `PackNameHasActiveProductGroupsAsync` |
| C# | `SubsidySettlementSource`, `SubsidyAllocationService.AllocateAsync` | Subsidy applied to totals, the checkout breakdown, order placement, the credit-note widget, exchange quotes |
| C# | `ShoppingCartOverrideService` (add, gated for the line's store; quantity change, for the current store; `GetSubsidyQuantityCapWarningsAsync`, for the cart's store) | Allocated-quantity cap and the allocation it runs |
| C# | `SubsidyBarViewComponent`, `SubsidyCatalogController`, `SubsidyController`, `PackNameSubsidiesViewComponent`, `SubsidyPackNamePanelProvider`, `EventConsumer` (account menu, page assets) | Entry points |
| never gated | subsidy restore on return, undo on cancel, order-page subsidy totals | In-flight and order-attached (§1.2, §1.4) |

Found missing on 2026-09-30, while specifying phase 4a: the three SQL rows above had no switch at all,
so a store with subsidies off was still blocked and restricted by old subsidy rows. See
`tasks/lessons-4.9.md` #85.

**Cached results follow the switch.** What those views return is held in the static cache (in-ordering
subsidies, ordering block, allocated products, and the instant a packname's ordering status next changes),
so a setting write must clear it too: `SettingCacheEventConsumer` clears `CW.feature.*` and the subsidy
ordering prefixes on any `multistoresettings.*` write, and on `datetimesettings.defaultstoretimezoneid`
(the store's day decides the periods), whatever path wrote it (phase 4b, 4b.9). Credit is not cached across
requests: it is read once per request and memoised in `SubsidyCreditService` (`subsidy-system.md` §4), so a
switch is seen by the next request. Without it, switching subsidies off
left the catalogue restricted until the cache expired. See `tasks/lessons-4.9.md` #88.

---

## 2. Architecture

All new code lives in `Nop.Plugin.CW.Core`, mostly under `Domain/Settings/`, `Services/Settings/`,
and `Filters/`.

### 2.1 `Domain/Settings/CWFeature.cs`

```csharp
public enum CWFeature
{
    /// <summary>PackNames (MultiStoreSettings.PackNameEnable)</summary>
    PackName = 1,
    /// <summary>Subsidies (MultiStoreSettings.SubsidyEnable)</summary>
    Subsidy = 2,
    /// <summary>Exchanges and returns (MultiStoreSettings.ExchangesEnable)</summary>
    Exchanges = 3,
    /// <summary>Credit notes (MultiStoreSettings.CreditNotesEnable)</summary>
    CreditNotes = 4,
    /// <summary>Bulk ordering (MultiStoreSettings.BulkOrderEnable)</summary>
    BulkOrder = 5
}
```

### 2.2 `Domain/Settings/CWFeatureSet.cs`

An immutable snapshot of all five flags for one store, so a view or factory that needs several
checks pays for **one** settings materialisation.

```csharp
public readonly record struct CWFeatureSet(bool PackName, bool Subsidy, bool Exchanges, bool CreditNotes, bool BulkOrder)
{
    public bool this[CWFeature feature] => feature switch
    {
        CWFeature.PackName => PackName,
        CWFeature.Subsidy => Subsidy,
        CWFeature.Exchanges => Exchanges,
        CWFeature.CreditNotes => CreditNotes,
        CWFeature.BulkOrder => BulkOrder,
        _ => false
    };

    public static CWFeatureSet From(MultiStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new CWFeatureSet(
            PackName: settings.PackNameEnable,
            //subsidies are allocated per PackName, so Subsidy is meaningless without PackName
            Subsidy: settings.SubsidyEnable && settings.PackNameEnable,
            Exchanges: settings.ExchangesEnable,
            CreditNotes: settings.CreditNotesEnable,
            BulkOrder: settings.BulkOrderEnable);
    }
}
```

**This one line — `Subsidy: settings.SubsidyEnable && settings.PackNameEnable` — is load-bearing.**
It's why the settings screens don't need to physically zero `SubsidyEnable` in the database when
`PackNameEnable` goes off (see §4.3): the *effective* value is always correct at read time,
regardless of what's stored. `CreditNotes`, `Exchanges` and `BulkOrder` are **not** implied by
anything — `CreditNote` keys on customer/order, `ReturnRequestItem` on return request; neither involves
a PackName. They stay independent.

`CWFeatureSet.Any` (an `OR` of all the flags) was added early, never used anywhere, and was removed.

### 2.3 `Services/Settings/ICWFeatureService.cs` / `CWFeatureService.cs`

```csharp
public interface ICWFeatureService
{
    Task<CWFeatureSet> GetFeaturesAsync(int? storeId = null);
    Task<bool> IsEnabledAsync(CWFeature feature, int? storeId = null);
    Task<bool> IsEnabledForOrderAsync(CWFeature feature, Order order);
    Task<CWFeatureSet> GetFeaturesForAnyStoreAsync();
    Task<bool> IsEnabledForAnyStoreAsync(CWFeature feature);
    void ResetRequestCache();
}
```

Implementation notes:

- Scoped service, injecting `ISettingService`, `IStoreContext`, `IStoreService`, `IStaticCacheManager`, `ILogger`.
- **Per-request memoisation** via `ConcurrentDictionary<int, CWFeatureSet>` (a `Dictionary` would not
  be safe — this codebase parallelises inside a request in several factories, e.g.
  `PackNameModelFactory`/`PackNameOrderModelFactory` use `Task.WhenAll`-style patterns).
  `LoadSettingAsync<MultiStoreSettings>(storeId)` already reads from nop's cache, so the memo just
  avoids repeated reflection-based property binding within one request.
- **`GetFeaturesForAnyStoreAsync` is the any-store primitive**; `IsEnabledForAnyStoreAsync(feature)`
  is a one-line indexer read over it. Prefer the set when you need more than one feature (the admin
  menu and the filter's `AnyStore` branch both do). See the note below on why it is shaped this way —
  it matters once a deployment has many stores.
- `storeId == null` resolves through `IStoreContext.GetCurrentStoreAsync()`. In the **admin area**
  this returns the *default* store, not the store being edited — every admin call site must pass an
  **explicit** `storeId`.
- `IsEnabledForOrderAsync` fails closed on a null order **and logs a warning** — a null order there is
  a caller bug, and silently blocking is the exact failure mode §6.1/§6.3 exist to prevent.
- `ResetRequestCache()` clears the per-request memo **only** — the name says so deliberately, because
  the static any-store cache is a separate concern (§2.4). Call it immediately after publishing
  `MultiStoreSettingsChangedEvent`, in case the same request goes on to read feature flags (both
  `MultiStoreController.StoreSettings` and `.Settings` POST actions do this).

#### Why `GetFeaturesForAnyStoreAsync` looks the way it does

CW deployments are expected to run **many** stores, and the obvious implementation of this method is
quadratic. Three deliberate choices keep it cheap; none of them are cosmetic.

**1. One cached pass for all the features, not one per feature.** The whole `CWFeatureSet` is cached
under a single key, and `IsEnabledForAnyStoreAsync` indexes into it. The earlier shape cached one
`bool` per feature, so a cold cache meant one independent walk over every store per feature.

**2. It reads the five backing settings directly instead of calling `LoadSettingAsync`.**
`SettingService.LoadSettingAsync<T>(storeId)` walks **every** property on the settings class, and each
property calls `GetSettingByKeyAsync`, which does a `FirstOrDefault(x => x.StoreId == storeId)` scan
over the rows sharing that setting name. With N stores that override `MultiStoreSettings`, that list
is N+1 long — so a per-store `LoadSettingAsync` loop costs roughly
*(properties × N × N)*. `MultiStoreSettings` has ~40 properties and this method needs 5 of them, so
reading the five keys directly cuts the constant by an order of magnitude:

```csharp
protected virtual async Task<bool> GetFlagForStoreAsync(string settingKey, int storeId)
    => await _settingService.GetSettingByKeyAsync(settingKey, defaultValue: false, storeId: storeId, loadSharedValueIfNotFound: true);
```

`loadSharedValueIfNotFound: true` is what applies nop's own fallback (a per-store row wins, otherwise
the storeId 0 row) — **do not hand-roll that rule here.** The keys come from
`ISettingService.GetSettingKey(probe, x => x.PackNameEnable)` rather than string literals, so a
property rename can't silently break the lookup.

**3. It early-exits** once all five features have been found enabled somewhere — the remaining stores
cannot change the answer. On a typical install this exits after the first store or two. The
worst case is a feature disabled in *every* store, which is exactly the case that decides whether the
admin menu node hides.

Two invariants to preserve if this is ever touched:

- **The `Subsidy = SubsidyEnable && PackName` implication must not be re-derived here.** The loop
  builds a `MultiStoreSettings` from the five flags and passes it through `CWFeatureSet.From`, so
  that rule keeps living in exactly one place (§2.2).
- **Do not reach for `ISettingService.GetAllSettingsAsync()`** to index the rows yourself. It is
  called with `cache => default`, i.e. **uncached — it hits the database on every call**. Indexing
  rows by store would remove the residual quadratic term, but at the cost of a DB round trip on a
  path that currently touches only cached state. Only worth revisiting with profiling behind it, and
  store counts in the thousands.

### 2.4 `Services/Settings/CWFeatureDefaults.cs`

```csharp
public static class CWFeatureDefaults
{
    public static CacheKey FeaturesForAnyStoreCacheKey => new("CW.feature.anystore.all");
    public static string FeaturePrefix => "CW.feature.";
}
```

One key, holding the whole any-store `CWFeatureSet` (§2.3). This is currently the **only**
`CWFeatureSet` that goes through `IStaticCacheManager` — per-store sets live in the per-request memo
and are never serialised. `DistributedCacheManager` serialises with `JsonConvert`, and a
`readonly record struct` round-trips through Newtonsoft, but it is untested on this codebase: if a
distributed cache (Redis) is ever enabled, verify this one key before trusting it. Caching a small
`int` bitmask instead is the bulletproof fallback if it ever misbehaves.

> **nop 5.00 caching API — check before copying advice from older 4.x.** `CacheKey` in 5.00 (as in 4.90) has
> exactly one constructor, `CacheKey(string key)`, and **no** `Prefixes` property; the
> `new CacheKey(key, prefix)` overload from 4.6/4.7 no longer exists. Prefix invalidation is done
> solely through `IStaticCacheManager.RemoveByPrefixAsync(string prefix, params object[] prefixParameters)`.
> Verify against `Libraries/Nop.Core/Caching/` rather than memory.

Invalidated in three places:

- **`Services/Stores/Caching/StoreCacheEventConsumer.cs`** on store insert/delete — a new or removed
  store changes the any-store answer without any settings being saved (see §6.5 for why this lives
  here and not in `EventConsumer.cs`).
- **`Services/Settings/Caching/SettingCacheEventConsumer.cs`** on any write of a `multistoresettings.*`
  setting or of `datetimesettings.defaultstoretimezoneid`, whatever path wrote it: clears
  `CWFeatureDefaults.FeaturePrefix` and the subsidy ordering prefixes (§1.5).
- **`EventConsumer.HandleEventAsync(MultiStoreSettingsChangedEvent)`**, published by the two CW settings
  actions, which clears the **whole** static cache:
  ```csharp
  await _staticCacheManager.ClearAsync();
  ```
  This flushes nop's caches too, not only the CW ones. *(An earlier version of this section said the
  handler removed only the `CW.` prefix, `RemoveByPrefixAsync(CWCoreDefaults.CachePrefix)`; the code in
  4.9 and in 5.00 calls `ClearAsync()`.)* **Keep new CW cache keys under the `CW.` prefix**
  (`CWCoreDefaults.CachePrefix`) all the same: it is the convention every CW key follows.

### 2.5 The block filter — `Filters/`

Four pieces, together giving a declarative `[CheckCWFeature(...)]` attribute plus an inline helper
for actions that can't use it.

**`CWFeatureStoreSource.cs`** — where the filter resolves the store id from:

```csharp
public enum CWFeatureStoreSource
{
    NotSet = 0,        // never valid — blocks and logs an error, so a missing StoreSource is loud
    CurrentStore = 1,  // IStoreContext — correct for public store, wrong in admin
    Parameter = 2,     // an action parameter / bound model property / route value / query / form
    Order = 3,         // the store of the order named by ParameterName (default "orderId")
    AnyStore = 4,      // enabled for at least one store — global admin pages with no store scope
    // …then the entity-scoped sources, 5-11 (see the table below)
}
```

**`NotSet` is the default value of `CheckCWFeatureAttribute.StoreSource`, and it always blocks.**
That is the point: the property used to default to `CurrentStore`, so an attribute written without a
`StoreSource` silently evaluated the current store — right for a public action, but in the admin area
that is the *default* store rather than the one being edited, which is wrong nearly every time. Now
omitting it fails closed and logs an **error** (not the usual warning) naming the action, because an
unset source is a coding mistake rather than an unresolvable request:

```csharp
if (_storeSource == CWFeatureStoreSource.NotSet)
{
    await _serviceProvider.GetRequiredService<ILogger>().ErrorAsync(
        $"{nameof(CheckCWFeatureFilter)}: [CheckCWFeature] on {context.ActionDescriptor.DisplayName} " +
        $"does not set {nameof(CheckCWFeatureAttribute.StoreSource)}. Blocking the request.");

    return false;
}
```

The enum values are compile-time only — never persisted, never sent to the client — so renumbering to
free up `0` was safe.

Entity-scoped sources were added beyond the original spec as the audit progressed — mostly while
gating the MultiStore admin area (§4.10), which is keyed almost entirely by entity id, not
`storeId`:

| Source | Default parameter | Resolves via |
|---|---|---|
| `Branch` | `branchId` | `Branch.StoreId` |
| `PackNameList` | `listId` | `PackNameList.StoreId` |
| `PackName` | `packNameId` | `PackName.PackNameListId` → `PackNameList.StoreId` |
| `Department` | `departmentId` | `Department.StoreId` |
| `Subsidy` | `subsidyId` | `Subsidy.StoreId` |
| `ProductGroup` | `productGroupId` | `ProductGroup.StoreId` |
| `SubsidyPackName` | `subsidyPackNameId` | `SubsidyPackName.SubsidyId` → `Subsidy.StoreId` |

**Adding a new entity source is one dictionary entry.** All seven live in a single static
`_entitySources` registry in `CheckCWFeatureFilter`, keyed by `CWFeatureStoreSource`:

```csharp
protected sealed record EntityStoreSource(string DefaultParameterName,
    Func<IServiceProvider, int, Task<int?>> GetStoreIdAsync);

private static readonly IReadOnlyDictionary<CWFeatureStoreSource, EntityStoreSource> _entitySources = new Dictionary<…>
{
    [CWFeatureStoreSource.Branch] = new("branchId", async (sp, id) =>
        (await sp.GetRequiredService<IBranchService>().GetBranchByIdAsync(id))?.StoreId),
    …
};
```

So a new source needs exactly two edits: a member on `CWFeatureStoreSource`, and an entry here. No
new constructor dependency, no new `switch` case, no new constant. Three properties fall out of the
design and are worth knowing:

- **Services resolve lazily** through the injected `IServiceProvider`, so an action pays only for the
  one service its own source uses — not for all of them. Same for `ILogger`, `IOrderService`,
  `IStoreContext` and `ICWFeatureBlockResultBuilder`, which are only needed by a subset of sources or
  only on the failure path. `ICWFeatureService` is the sole eagerly-injected dependency.
- **An unregistered source fails closed.** `Parameter`, `Order`, `AnyStore` and `CurrentStore` are
  explicit cases; everything else looks itself up in the registry, and a miss resolves to `null` and
  blocks. There is no "fall through to the current store" path any more.
- **A resolved store id of `0` is rejected for every source**, not just `Parameter`, unless the action
  sets `AllowDefaultStore = true`.

> **Note on `IServiceProvider`.** Injecting the provider is a deliberate, narrowly-scoped exception to
> the constructor-injection-only rule (CLAUDE.md §4). It is justified here because a filter's
> dependencies are genuinely conditional on the attribute's configuration, and the alternative is
> constructing nine service graphs per request to use one. It is **not** precedent for service
> location elsewhere — note it is constructor-injected and request-scoped, unlike
> `EngineContext.Current.Resolve<T>()`, which remains banned in new code.

> **Create actions take the parent id, never the nullable child id.** `PackNameListDetail(int
> branchId, int? listId)` and `BranchPackNameDetail(int listId, int? packNameId)` serve both create
> and edit. Gate on `branchId`/`listId` (the parent) — on create the child id is null, and a
> `PackName`/`PackNameList` source would fail closed on every "add new" click.

**`ICWFeatureBlockResultBuilder.cs` / `CWFeatureBlockResultBuilder.cs`** — builds the response for a
blocked request. The result table lives here rather than in the filter, so the attribute and the
inline guard produce byte-identical responses. ("Builder", not "Factory": `Factory` is a loaded term
in this codebase — §6.5 reserves it for the nop ModelFactory pattern, and this is not one.)

| Request shape | Response |
|---|---|
| AJAX GET | Empty body (the caller injects the response into a panel — anything else paints an error page inside it) |
| AJAX POST, DataTables grid read (form has a `draw` key) | A well-formed empty page: `{ draw, recordsTotal: 0, recordsFiltered: 0, data: [] }` — DataTables can't parse `{ success: false }` and raises its own JS alert instead of rendering an empty table, so grid reads get their own shape, checked *before* the generic AJAX-POST branch |
| AJAX POST (anything else) | `{ success: false, error: message }` — mirrors every CW admin POST's normal shape |
| Admin, full page / non-AJAX POST | Error notification + redirect to `Admin/Home/Index` |
| Public store | Redirect to `RedirectRouteName` if set, else the homepage |

**`CheckCWFeatureAttribute.cs`** — `IFilterFactory`, not `TypeFilterAttribute` (named properties like
`StoreSource = ...` need to be set after construction, which `TypeFilterAttribute`'s `Arguments`
array doesn't support). `IAsyncActionFilter`, not `IAsyncAuthorizationFilter` (authorization filters
run before model binding, so the bound store id wouldn't exist yet). `IsReusable => false` — the
filter depends on scoped services, so caching one instance across requests would leak the graph.

Features listed in one attribute are OR'd (`[CheckCWFeature(CWFeature.Exchanges, CWFeature.CreditNotes)]`
runs when *either* is on). Stacking two attributes ANDs them.

**`CheckCWFeatureFilter.cs`** — resolves the store id (or entity id, for the entity-scoped sources)
in this order: direct action argument → bound model property via cached reflection → route value →
query string → **form body**. Fails **closed** (blocks + logs a warning) when none of the five
resolve, and short-circuits before the database exists (`DataSettingsManager.IsDatabaseInstalled()`)
and after another filter has already produced a result (permission denials keep nop's standard
`AccessDenied` page — `[CheckPermission]` is an authorization filter and always runs first regardless
of declaration order).

**The form-body step exists because of a real shipped bug** (§6.8): a POST whose id is a plain
hidden form field that isn't a property on the *bound* model — the model binder never sees it, so
steps 1–4 all miss. `IFormCollection` keys are matched case-insensitively, and reading `Request.Form`
here is safe because model binding has already read and buffered it. Any future action with this
shape (an id posted in the form body but absent from the action's own model) hits this step
automatically — no per-action fix needed once it's in the filter.

`AllowDefaultStore` (bool property, default `false`): `storeId = 0` is nop's "all-stores defaults"
scope, not "not supplied" — but for *entity* ids (branch, subsidy, etc.) `0` really does mean
missing. Only the `Parameter` source's plain-int case treats `0` as ambiguous; set
`AllowDefaultStore = true` on actions that legitimately operate at storeId 0 (e.g. the global fee
settings page, which reuses the same actions as the per-store one).

**Inline guard — `ICWFeatureGuard`**, for the actions that receive only an entity id, so the store
needs a DB lookup the attribute can't do. It lives in `Filters/` next to the attribute deliberately:
the two are the **declarative and imperative forms of the same decision**, and a controller that
can't use one uses the other. `CWFeatureGuard` wraps the feature check and the block-result shaping
into one dependency, so a controller injects one thing instead of both `ICWFeatureService` and
`ICWFeatureBlockResultBuilder`:

```csharp
public interface ICWFeatureGuard
{
    /// <summary>Returns a block result when the feature is disabled for the store, otherwise null</summary>
    Task<IActionResult> BlockIfDisabledAsync(CWFeature feature, int storeId, HttpRequest request);
}

// usage — after loading the entity and after its null check
if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.Subsidy, subsidy.StoreId, Request) is IActionResult blocked)
    return blocked;
```

Because it goes through the same `ICWFeatureBlockResultBuilder`, an inline guard and an attribute
produce byte-identical responses. Used by `MultiStoreController`, `SubsidyController` and the admin
`CreditNotesController`.

### 2.6 Razor access

Both `_ViewImports.cshtml` files inject the service so no view has to do it individually:

- `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/_ViewImports.cshtml`
- `Plugins/Nop.Plugin.CW.Core/Views/_ViewImports.cshtml` (public — this one was **missing** the
  injection until an early audit pass caught it; every public view depends on it, so it's worth
  checking first if a public gate ever appears to silently no-op)

```cshtml
@using Nop.Plugin.CW.Core.Domain.Settings
@using Nop.Plugin.CW.Core.Services.Settings
@inject ICWFeatureService cwFeatures
```

Usage — one call per render, then reuse the snapshot:

```cshtml
@{
    var features = await cwFeatures.GetFeaturesAsync(Model.Id);   // admin: explicit storeId
}
@if (features.Subsidy) { … }
```

```cshtml
@{
    var features = await cwFeatures.GetFeaturesAsync();           // public: current store
}
```

### 2.7 Registration

```csharp
services.AddScoped<ICWFeatureService, CWFeatureService>();
services.AddScoped<ICWFeatureBlockResultBuilder, CWFeatureBlockResultBuilder>();
services.AddScoped<ICWFeatureGuard, CWFeatureGuard>();
```

Placed with the other CW registrations in `Infrastructure/NopStartup.cs`, order
`DependencyRegistrarConstants.OrderAfterBaseRegistrar`. `[CheckCWFeature]` itself needs no
registration — it activates its filter from the container per request via `IFilterFactory`.

### 2.8 Settings seeding — do not skip this on upgrade

`LoadSettingAsync<MultiStoreSettings>` returns `false` for a `bool` that was never saved. Switching
the gates from "plugin installed" to "setting enabled" would therefore turn every feature off on
every existing install unless the defaults are seeded. `CWCorePlugin.InstallOrUpdateAsync()` (called
from both `InstallAsync` and `UpdateAsync`) seeds the storeId = 0 defaults to `true` only when they
have never been written:

```csharp
var allStoreSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(0);

if (!await _settingService.SettingExistsAsync(allStoreSettings, x => x.PackNameEnable))
{
    allStoreSettings.PackNameEnable = true;
    await _settingService.SaveSettingAsync(allStoreSettings, x => x.PackNameEnable, 0);
}
// …same for SubsidyEnable, ExchangesEnable, CreditNotesEnable
```

Per-store overrides are opt-out from there. A newly created store inherits the storeId = 0 default
(currently `true` for all four), which is the intended behaviour. `BulkOrderEnable` is **not** seeded:
it reads `false` until a settings screen saves it, so bulk ordering starts off.

---

## 3. Settings screens — visibility (client-side) and persistence (server-side)

Two screens share one model (`MultiStoreSettingsModel`) through different actions/views:

| Screen | Route | Scope | View | Controller action |
|---|---|---|---|---|
| Store management (settings tab) | `/Admin/MultiStore/Edit/{id}` | one store | `_StoreSettings.cshtml` (AJAX tab) | `MultiStoreController.StoreSettings` (POST) |
| Default store settings | `/Admin/MultiStore/Settings` | `storeId = 0` (all-stores defaults) | `Settings.cshtml` (full page) | `MultiStoreController.Settings` (POST) |

The four `Enable` checkboxes always render — otherwise a feature can never be switched back on. Only
*dependent* fields (e.g. `AllowSubsidySelection`, `PrintNameProductAttributeId`,
`PackNamesOwnStartAndFinishDates`) are conditional.

### 3.1 Client-side: one visibility class per element, one predicate per class

**The rule**: one element carries exactly one visibility class; one visibility class is driven by
exactly one line of JS; that line spells out the *complete* predicate (including transitive
governors). `showElement()`/`hideElement()` are just `removeClass`/`addClass('d-none')` — no
reference counting — so an element with two visibility classes is last-writer-wins and breaks the
moment two governors disagree. This bit the codebase for real: `.pnlSubsidyEnable` combined with
`.pnlAllowManagersToAddPackNames` on the same row meant `AllowManagersToAddPackNames = off,
SubsidyEnable = on` left the row wrongly visible.

Three derived booleans do all the transitive work, computed once per pass (the element ids differ
by view — `_StoreSettings.cshtml` uses camelCase ids such as `subsidyEnable`, `Settings.cshtml` uses
`Html.IdFor(...)`; the predicate is the same):

```js
var packName = isChecked('PackNameEnable');
var subsidy  = packName && isChecked('SubsidyEnable');            // Subsidy requires PackName
var managers = packName && isChecked('AllowManagersToAddPackNames');
```

`applySettingVisibility()` (bound to `change` on every checkbox in the form, and run once on load)
sets one class per row from one of these booleans, or a combination for multi-parent rows
(`AllowSubsidySelection` needs `managers && subsidy`; `AllowEditSubsidyTopup` additionally needs
`AllowSubsidySelection` itself checked; `AllowSubsidyCountByExcel` needs `AllowToAddByExcel` checked
too). Every `<nop-nested-setting>` in both views carries `disable-auto-generation="true"` so nop's
tag helper stops emitting a competing `toggle_X()` function that would otherwise fight the
hand-written one over the same element.

### 3.2 The client hides; the server refuses

Visibility is a convenience, and until phase 2a (2026-09-17) it was the only thing some of these
settings did. Three are now enforced where it counts:

- **`AllowManagersToAddPackNames`** gates all four public maintenance actions — `AddPackName` GET and
  POST, `EditPackName` GET and POST — **and** `DeletePackName`, which was the one action that never
  read it, so with the setting off a manager could still permanently delete an employee's packname and
  its allowance history (gap analysis U-03, U-33).
- **`AllowEditSubsidyTopup`** (with `AllowManagersToAddPackNames`) gates both storefront topup
  endpoints inside `ValidateTopupAccessAsync`, not merely the *Change* button (S-05).
- **`CWFeature.PackName`** now gates the order-placement guard and the recurring-payment override as
  well as the checkout filter. That symmetry matters: the filter and the guard disagreeing about the
  toggle left a customer with packname cart items bounced between nop's checkout and a packname
  checkout that redirects back to it (U-35).

The rule to carry forward: a setting that hides a control must have a server-side reader, and the
reader loads it for the store the **actor** belongs to — for a manager that is their own store, derived
from their packname's list, never a store id the request carried (U-25).

**The admin half (phase 2b part 1).** The same rule read from the other side: on an admin screen the
actor may legitimately touch any store, so the store comes from the **entity being saved**, not from
the actor and not from the request. Two consequences:

- **`StoreId = 0` is not a store.** It is the all-stores default scope, and editing it is
  `DEFAULT_STORE_SETTINGS`, a different permission from `MULTISTORE_SETTINGS`. **Three** actions in
  `MultiStoreController` write a per-store setting from a posted id, and all three now refuse `0`:
  `StoreSettings` POST (change 11) and the two generators `GeneratePackNames` and
  `GeneratePackNameLists` (round I). The refusal is the same in each:
  `{"success":false,"message":"Default settings for all stores can only be edited from the default
  settings page"}` (resource `Plugins.CW.Core.Admin.MultiStore.Settings.DefaultScopeNotAllowed`).

  Only `StoreSettings` was ever exposed. It carries `[CheckPermission]` and **no `[CheckCWFeature]`**,
  so the posted `0` reached the writes — that is the one genuine permission crossing phase 2b found,
  and change 11 is what closes it. Verified live 2026-09-21: all 34 global setting rows byte-identical
  after the attempt, and store 1's own settings untouched. The two generators carry
  `[CheckCWFeature(..., StoreSource = Parameter)]`, which already failed closed on `0` (see the
  `AllowDefaultStore` rule below), so round I's guards there are **defence in depth, not a closed
  hole**. Confirmed live 2026-09-22: posting `StoreId = 0` to either generator is refused by the gate,
  with *"This feature is not enabled for this store."*, before the action body runs.

- **The gate rejects store `0` for every resolved source unless the action opts in.**
  `CheckCWFeatureFilter.IsAllowedAsync` ends with
  `if (storeId is null || (storeId == 0 && !_allowDefaultStore)) return await LogUnresolvedStoreAsync(context, parameterName)`.
  So `[CheckCWFeature]` with any resolved `StoreSource` is *also* a default-scope guard, and an
  unresolvable id fails closed the same way. `AllowDefaultStore = true` is the deliberate opt-out, used
  by the four `FeeRule` actions in `ExchangesConfigController` (`:257, :266, :292, :328`) because fee
  rules are legitimately definable at the all-stores scope under a single permission; each of those
  still checks `rule.StoreId != model.StoreId` itself. **An action that writes a per-store setting and
  has no `[CheckCWFeature]` has no such backstop and must refuse `0` in its own body.**
- **The two settings permissions are not interchangeable.** `MULTISTORE_SETTINGS` lets an admin
  configure a store; `DEFAULT_STORE_SETTINGS` lets them change what every store inherits. An action
  that accepts a scope from the request lets the first stand in for the second, whatever attribute is
  on it.

And a third, for any feature gate placed on an **admin** action: `CWFeature` resolves the *current*
store, which in the admin area is whatever store the admin's host maps to — not the store the entity
belongs to. Admins edit every store's data from one host, so a feature gate written that way guards
the wrong store. Either source the store from the entity (`CheckCWFeature`'s `StoreSource` /
`ParameterName`) or, where the thing being protected is a fact about the row rather than a setting,
do not gate on the flag at all — see the admin order guard in
`PackName_Order_Shipments.md` §9.1 for a case where removing the check was the fix.

Rows are never removed from the DOM — hidden inputs still `POST` their last value. That's
deliberate: it's what makes the server-side design in §3.2 work.

Implemented in `_StoreSettings.cshtml` and `Settings.cshtml` — verified against code, matches this
spec exactly.

### 3.2 Server-side: don't persist a forced-`false` — **this is the final, correct design**

> **This section documents a real reversal.** The original design (and the first implementation)
> coerced every dependent setting to `false` in the database whenever its governor was off —
> "hidden ≠ disabled, so a hidden checkbox still posts its old value; coerce on save so the database
> agrees with what's displayed." That shipped, was audited, found to have real ordering bugs, the
> bugs were fixed twice, and *then* a review question exposed that the entire design was wrong: it silently
> and permanently destroyed an admin's configuration (e.g. "PackNames Can Have Their Own Start And
> Finish Dates") every time they temporarily disabled a governing feature — with no way back except
> reconfiguring from scratch. The final design, implemented in code, removes the coercion entirely.

**Why persisting the zero was never actually necessary.** `CWFeatureSet.From` (§2.2) already
computes `Subsidy: settings.SubsidyEnable && settings.PackNameEnable` **at read time** — so the
effective feature state is correct regardless of what's physically stored. The database-level
zeroing was purely cosmetic: it kept `SubsidyEnable` from reading `true` on a settings screen where
its row is hidden anyway by the client-side rules in §3.1. There's nothing visible to disagree with,
so there's no correctness reason to persist the coercion — only a data-loss cost.

The other governed settings are PackName/Subsidy-only knobs, and every call site that reads them
directly (rather than through `ICWFeatureService`) turned out to already be gated:
`Areas/Admin/Controllers/SubsidyController.cs` and `Controllers/SubsidyCatalogController.cs` are
both behind `[CheckCWFeature(CWFeature.Subsidy, ...)]` at the class or action level, so a stale
stored value in `PackNamesOwnGlobalValue`/`PackNamesOwnStartAndFinishDates` is unreachable while
Subsidy is effectively off anyway.

**Current, correct implementation** — both `MultiStoreController.StoreSettings` (POST) and
`.Settings` (POST) simply save whatever was posted, for every setting, no exceptions:

```csharp
storeSettings = model.ToSettings(storeSettings);

// every setting — cascaded or not — saves as posted, no coercion
await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.PrintNameProductAttributeId, model.PrintNameProductAttributeId_OverrideForStore, storeId, false);
await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.AllowToAddByExcel, model.AllowToAddByExcel_OverrideForStore, storeId, false);
// … (SubsidyEnable, PackNamesOwnGlobalValue, PackNamesOwnStartAndFinishDates, etc. — all the same)
await _settingService.SaveSettingOverridablePerStoreAsync(storeSettings, x => x.SubsidyEnable, model.SubsidyEnable_OverrideForStore, storeId, false);
```

No `if (!model.X) storeSettings.Y = false;` fragments remain in either action. *(`RegistrationScreen` /
`RegistrationScreenPageName` no longer exist — store self-registration was deleted by phase 8 Part 1.)* The same rule
holds for the override **flags**: none is derived from another field's posted value (phase 8 Part 1 removed the last
such block from `StoreSettings`, `GeneratePackNames` and `GeneratePackNameLists` — an unticked override's input is
disabled, so it is not posted and binds `false` / `0`).

**Net effect**: disable a governor and save — the dependent rows vanish from the settings screen and
stop taking effect immediately (still correctly enforced by `ICWFeatureService`/`[CheckCWFeature]`
everywhere that matters). Re-enable the governor and save — every dependent row reappears exactly as
it was configured before. No reconfiguration needed.

**If you're re-deriving this from scratch**, do *not* re-introduce a `CascadeDisabledSettings`-style
coercion method. If a future requirement genuinely needs the stored value to reflect "off" (e.g. an
external system reads `MultiStoreSettings.SubsidyEnable` directly, bypassing `ICWFeatureService`),
solve it at that read site with the same `&& PackNameEnable` pattern `CWFeatureSet.From` uses —
don't solve it by destroying the admin's configuration again.

---

## 4. Complete map of gated locations

Every location below is implemented and verified against `Plugins/Nop.Plugin.CW.Core` as of
2026-08-06, with the admin-controller rows re-verified on 2026-10-21, and every entry re-checked
against the 5.00 code on 2026-10-31. "Feature" is the `CWFeature` value(s) checked; "Store source" is how the store id is
resolved (see §2.5).

### 4.1 Admin controllers

| Controller | Gating | Store source |
|---|---|---|
| `Areas/Admin/Controllers/SubsidyController.cs` | `[CheckCWFeature(CWFeature.Subsidy, ...)]` on 30 actions, all method-level (`Parameter` on the `ProductGroups` / `Subsidies` list and save actions, an entity-derived source elsewhere) + 5 inline `ICWFeatureGuard.BlockIfDisabledAsync` guards on `DeleteProductGroup` / `DeleteSubsidy` / `SubsidyPackNameDelete` / `SubsidyProductGroupUpdate` / `SubsidyProductGroupDelete`. Every action of the controller is gated | `Parameter` / `Subsidy` / `ProductGroup` / `SubsidyPackName` / `PackName` entity sources |
| `Areas/Admin/Controllers/MultiStoreController.cs` | 35 `[CheckCWFeature(CWFeature.PackName, ...)]` attributes plus 10 inline `ICWFeatureGuard.BlockIfDisabledAsync` guards (the actions whose store is the target list's or branch's: `SavePackNameList`, `DeletePackNameList`, `SaveBranchPackName`, `SavePackName`, `DeletePackName`, `CustomerPackNameCreatePopup` GET and POST, `PackNameAddresses`, `PackNameAddressPopup` POST, `PackNameAddressDelete`) across the PackName Lists / PackNames / PackName Addresses / Department Managers regions — every packname-address and create-popup action is gated (U-15 closed, phase 2b; re-verified 2026-10-21) and `GetListsByBranchId` carries `[CheckPermission]` + `[CheckCWFeature(…, StoreSource = Branch)]` — plus the four actions of the *PackName Import* region (phase 12: `ImportPackNames` page, `ImportPackNamesTemplate`, `ImportPackNames` POST, `ImportPackNamesErrorReport`). Those four are gated by the PackName switch only — *Allow managers to add packnames* / *Allow to add by Excel* are manager settings and do not apply; PackName off → the page redirects to the admin home, the POST answers `{ success:false, error }`; a missing or 0 `storeId` is refused by the filter. The admin template's subsidy columns follow the Subsidy feature (and the store's active subsidies), not `AllowSubsidySelection`. Phase 11 added the three *PackName Bulk* actions (`BulkPackNames`, `PackNameBulkMoveTargets`, `ExportPackNames`), same filter: PackName off → `{ success:false, error }` for the two POSTs (the export is fetched with `X-Requested-With`, so it gets the JSON and the page shows the message), an empty answer for the AJAX GET; the *PackNames* tab and the list grids are not shown. | `Parameter` / `Branch` / `PackNameList` / `PackName` / `Department` |
| `Areas/Admin/Controllers/ExchangesConfigController.cs` | All 14 actions but one gated `CWFeature.Exchanges` (13 attributes); `GetOrderReturnRequests` is deliberately left open (§1.4) | `Parameter` (with `AllowDefaultStore = true` on the 4 fee-rule actions, since `GlobalFeeSettings` reuses them at `storeId = 0`), `AnyStore` on `GlobalFeeSettings` |
| `Areas/Admin/Controllers/CreditNotesController.cs` | Inline guards on `SaveCreditNote`/`DeleteCreditNote` only (issuing/deleting a note). All list/report/history actions (`CustomerCreditNotesList`, `CreditNoteHistoryList`, `GetCreditNotesSummary`, `GetAvailableOrders`, `MainReport`, `StoreLiabilityList`, `StoreCustomerCreditNotesList`) are **deliberately ungated** — they display money owed (§1.2 liability rule) | inline: `SaveCreditNote` on the customer's `RegisteredInStoreId`, `DeleteCreditNote` on the note's `StoreId` |
| `Areas/Admin/Controllers/ReturnsController.cs` | Left ungated — configures nop's own return reasons/actions, not CW rules; gating would remove nop functionality from a store that never used CW exchanges | — |
| `Areas/Admin/Controllers/ExtendedCustomerController.cs` | No class-level gate (customer list/edit must always work); only the PackName/subsidy/credit-note sub-panels are gated, via the components in §4.2 and the view (§4.8) | — |
| `Areas/Admin/Controllers/BranchController.cs`, `CWOrderController.cs` | No gate. Branches are not a feature; the packname provisioning a branch save runs is gated in its service (§4.3). `CWOrder/OrderBadges` is an order-attached read (§1.4) | — |

`Areas/Admin/Controllers/MultiStoreController.StoreSettings`/`.Settings` (the settings-screen POST
actions) are **deliberately never gated** — locking the settings screen once a feature is off would
make it unrecoverable.

### 4.2 Admin view components

Guard at the top of `InvokeAsync`, return `Content(string.Empty)` when blocked — covers every
current and future caller in one place.

| Component | Feature | Notes |
|---|---|---|
| `Areas/Admin/Components/ExchangeSettingsComponent.cs` | `Exchanges` | |
| `Areas/Admin/Components/FeeSettingsComponent.cs` | `Exchanges` | |
| `Areas/Admin/Components/PackNameExtensionPanelsViewComponent.cs` | `PackName` | |
| `Areas/Admin/Components/PackNameSubsidiesViewComponent.cs` | `Subsidy`, for the packname's list's store | Also returns nothing without `CW.CORE.Admin.AccessMultiStore` (an inline permission check, §6.8) |
| `Areas/Admin/Components/BulkOrderProductPageAdminViewComponent.cs` | `BulkOrder`, any store (`IsEnabledForAnyStoreAsync`) | Product edit page; a product has no single store |
| `Components/SubsidyBarViewComponent.cs` | `Subsidy` | Public |
| `Components/CreditNotesWidgetsViewComponent.cs` | **No feature check.** Its three zones (`WIDGET_CREDITNOTES_FOR_CART`, `_ORDER_CUSTOMER`, `_ORDER_ADMIN`) all display amounts and keep their own `NeedToShow` logic; the cart amount follows the settlement, which is gated (§4.3). The write zone (`CreditNoteAddFormAsync`) named here before no longer exists | Admin + public |
| `Components/ExchangesWidgetsViewComponent.cs` | `Exchanges`, only on the **cart** zone (`ExchangesFeesForCartComponentAsync`) — the order zones (public + admin) render fees already charged and are correctly left open | |
| `Components/PackNameInfoViewComponent.cs` | `Subsidy`, for the subsidy button only (`HasSubsidies`) | Its PackName fallback is §4.6 |
| `Components/BulkOrderLinkViewComponent.cs` | `BulkOrder`, through `IBulkOrderEntryService.CanUseBulkOrderingAsync` | Public, header link |

`[CheckCWFeature]` itself does nothing on a view component — `IViewComponentHelper.InvokeAsync` calls
the component directly and never runs the MVC filter pipeline, so `IAsyncActionFilter` attributes on
a view component method are silently inert (this was a real shipped bug, §6.8). Every component
above guards **inside** `InvokeAsync` (or the zone method it dispatches to), by calling `ICWFeatureService`
or a gated service — never by attribute.

**`IPackNamePanelProvider` / `SubsidyPackNamePanelProvider`** is the odd one out — it's not a view
component itself, it's the plug-in point `PackNameExtensionPanelsViewComponent` calls to ask "does
this PackName get a Subsidies panel?" It gates by returning `null` instead of a model:

```csharp
public async Task<PackNamePanelModel> GetPanelAsync(int packNameId, string prefix = null)
{
    var packName = await _packNameService.GetPackNameByIdAsync(packNameId);
    var list = packName is null ? null : await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

    if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, list?.StoreId ?? 0))
        return null;

    return new PackNamePanelModel { Title = …, ViewComponentName = "PackNameSubsidies", AdditionalData = new { packNameId, prefix } };
}
```

`PackNameExtensionPanelsViewComponent` already filters out `null` panels from every provider it
calls, so this one change removes the Subsidies panel from **both** places a PackName can be edited
(`_Branches.PackNameDetail.cshtml` and `_PackNames.PackNameDetail.cshtml` both render through the
same extension-panels component) without either view needing its own check. Returning `null` was
chosen over adding an `IsAvailableAsync` method to the interface — equivalent behaviourally, and it
avoids changing an interface other plugins are meant to implement.

### 4.3 Business logic / order services (the money paths — gated last, most carefully)

| File | Site | Gate |
|---|---|---|
| `Services/Settlement/CreditNoteSettlementSource.cs` → `IsEnabledAsync` | Credit notes applied to a **new** order and its quote (settlement priority 2) | `CreditNotes` — `IsEnabledForOrderAsync` at placement, the cart's store for a quote. Genuinely a new-order case, not in-flight work, so gating here is correct. *(Replaces the former `EventConsumer.HandleEventAsync(OrderPlacedEvent)` auto-apply, which no longer exists.)* |
| `Services/Orders/OrderProcessingOverrideService.cs` | Packname order placement (the cart has packname lines), the recurring-payment override and `ReOrderAsync` | `PackName`, for the order's store. Exchange completion is **not** gated (§6.3) |
| `Services/Settlement/SubsidySettlementSource.cs` | Subsidy credit applied to cart/order totals (the settlement pipeline; `IsEnabledForOrderAsync` at placement) | `Subsidy` |
| `Services/Subsidies/SubsidyAllocationService.cs` → `AllocateAsync` | Per-line allocation behind the checkout breakdown and the subsidy bar (`GetCartSubsidyBreakdownAsync`) — a **second** entry point into the same subsidy math | `Subsidy` — easy to miss if only the totals are gated; both carry the check. *(Updated at phase 6 closure, 2026-10-02: the former `CalculateSubsidyTotalAsync` / `PreviewSubsidyBreakdownAsync` sites no longer exist.)* |
| `Services/Orders/ShoppingCartOverrideService.cs` | PackName cart splitting (`GetShoppingCartAsync`, `GetCartLinePackNameAsync`), subsidy validation on add-to-cart, quantity change and the cap warnings, the exchange replacement line checks (`GetCartLineReplacementAsync`, `IsExchangeReplacementAsync`) | `PackName` / `Subsidy` / `Exchanges` |
| `Services/Exchanges/ExchangeReplacementPriceConsumer.cs` | The replacement line's price while an exchange is written to the cart | `Exchanges`, for the cart item's store |
| `Services/PackNames/PackNameProvisioningService.cs` → `ProvisionNewBranchAsync`, `ProvisionCustomerBranchAsync` | The automation behind `UseBranchesAsPackNameList` / `UseStaffAsPackName`: a new branch's default list, a packname for a customer an admin links to a branch (callers: `MultiStoreController.SaveBranch`, `BranchController.Create`, `ExtendedCustomerController.Create` / `Edit`) | `PackName`, for the branch's store — nothing is created with the feature off (phase 8 Part 1). The Generate buttons reach the same service through actions already behind `[CheckCWFeature(PackName)]` |
| `Services/Messages/MessageTokenProviderOverride.cs` | Exchange-fee and credit-note message tokens | **Left unconditional** — see §6.7, an order-attached-record read that must survive a re-sent email |
| `BusinessLogic/ExchangesLogic.cs` → `ExecuteExchangeWorkflowAsync` | Credit-notes balance check while completing an exchange | **Left unconditional** — see §6.1, the most important reversal in this whole effort |

### 4.4 Public controllers

| Controller | Gating |
|---|---|
| `Controllers/SubsidyCatalogController.cs` | `[CheckCWFeature(CWFeature.Subsidy, StoreSource = CurrentStore)]` at class level |
| `Controllers/PackNameController.cs` | `[CheckCWFeature(CWFeature.PackName, StoreSource = CurrentStore)]` at class level — covers the selector's `PackNames` (paged, server search) and `PackNameCartCounts` (phase 11): PackName off → `{ success:false, error }` and no selector |
| `Controllers/PackNameCheckoutController.cs` | `[CheckCWFeature(CWFeature.PackName, StoreSource = CurrentStore, RedirectRouteName = NopRouteNames.Standard.CHECKOUT)]` at class level — a blocked request lands on nop's standard checkout, never the homepage, so a customer mid-purchase doesn't lose their cart |
| `Controllers/ExchangesController.cs` | Method-level on 7 actions: the wizard's `ReturnRequestGatherInfo`, `DisplayReturnRequestSummary`, `ReplacementProductList`, `ReplacementProductOptions`, `ConfirmReturnRequestSummary` (`CurrentStore`) and `GetActionsForReason` (`Parameter`), plus `EditExchangedItem` (`CurrentStore`; see §6.2). **Never class-level** — `GenerateReturnRequestReceipt(s)` must stay reachable for existing requests. `GetReturnedItemPackNameAsync` checks `PackName` for the order's store (§4.6) |
| `Controllers/CreditNotesController.cs` → `CreditsForMe` | **Not** a flat gate — liability rule: `CreditNotes enabled ‖ customer has credit notes` |
| `Controllers/CustomerSubsidiesController.cs` (*My Subsidies*, `customer/subsidies`) | `[CheckCWFeature(CWFeature.Subsidy, StoreSource = CurrentStore)]` at class level; inside, no selected packname → access denied (the page follows the ordering context) |
| `PackNameController.AddPackName / EditPackName` subsidy checkboxes | Rendered **and** honoured only when the Subsidy feature is on for the store and `AllowSubsidySelection` is set — a posted `SubsidyIds` is ignored otherwise (`PackNameModelFactory.PrepareAvailableSubsidiesAsync`, `PackNameController.SaveSubsidyAssignmentsAsync`) |
| `PackNameCheckoutController.Completed` (`packname/checkout/completed/{orderId}`) | Class-level PackName gate; a plain order or another customer's order → homepage |
| `Controllers/BulkOrderController.cs` | `[CheckCWFeature(CWFeature.BulkOrder, StoreSource = CurrentStore)]` at class level (§4.12) |
| `PackNameController.ImportPackNames` / `ImportPackNamesTemplate` / `ImportPackNamesErrorReport` (manager Excel import, phase 8 Part 2) | Class-level PackName gate (off → redirected to the home page, no selector, no icon); inside, a manager of the store with `AllowManagersToAddPackNames` **and** `AllowToAddByExcel` — anyone else gets access denied. The template's subsidy columns exist only with the Subsidy feature on and `AllowSubsidySelection`; a file that still holds values in columns the store no longer takes (a feature or setting switched off since the download) is refused, the same file with those columns empty imports. Unchanged by phase 12: the administrator's import (§4.1) is a separate entry point and leaves this gate as it is |

### 4.5 Public filters

`Filters/PackNameCheckoutRedirectFilter.cs` — redirects nop's `/cart`/`/checkout` to the PackName
checkout whenever the customer has a PackName; since phase 7 a GET of nop's `checkout/completed/{orderId}`
for a **packname order** is re-routed to the packname completed page (`ICWOrderService.IsPackNameOrderAsync`),
and other orders pass through — the allow-list of exceptions is gone. Short-circuits at the top of
`OnActionExecutionAsync` when `PackName` is off for the current store, **before**
`GetCurrentPackNameAsync` — this filter runs globally on every request, so the cheap memoised check
comes first.

`Filters/ReturnRequestSubmitFilter.cs` (added in the 5.00 port) — a global filter on nop's own return
request POST (`/returnrequest/{orderId}`). With `Exchanges` on for the **order's** store, a signed-in
customer's post is sent back to the return page, because the CW wizard is the way to return; a guest's
withdrawal-link post (it carries a token) passes. With `Exchanges` off, nop's post is used, with the
quantities capped at CW's shipped count either way. The return page itself follows the same switch:
`ReturnRequestOverrideModelFactory.PrepareSubmitReturnRequestModelAsync` filters the reasons and actions
by the store's rules only when `Exchanges` is on.

`Filters/PackNameOrderAdminGuardFilter.cs` deliberately reads **no** feature flag (§3.2, last paragraph).

### 4.6 The PackName fallback (making "off" behave like standard nopCommerce)

The single highest-leverage fix in the whole effort. Before it, `IPackNameContext` kept resolving a
customer's PackName even after `PackNameEnable` was switched off, so the header selector, info
banner, cart splitting and checkout redirect all kept behaving as if PackNames were on.

`Services/PackNames/PackNameContext.cs` → `SetCurrentPackNameAsync` is the single funnel that
populates all three internal caches (`_cachedPackName`, `_cachedPackNameList`, `_cachedPackNameBranch`).
Guarded as the **first statement**:

```csharp
if (customer != null && !await _cwFeatureService.IsEnabledAsync(CWFeature.PackName))
{
    _cachedPackName[customer.Id] = new PackName();
    _cachedPackNameList[customer.Id] = 0;
    _cachedPackNameBranch[customer.Id] = null;
    return;
}
```

Every downstream consumer treats a null/empty PackName as "not a PackName customer" and falls back
to standard nop behaviour — so this one guard covers the selector, info banner, cart splitting,
order totals, and the checkout redirect, without touching any of them individually. Two more small
guards complete the fallback:

- `EventConsumer.HandleEventAsync(CustomerLoggedinEvent)` skips seeding the PackName context on
  login when the feature is off.
- `PackNameCheckoutRedirectFilter` (§4.5) short-circuits before even calling into the context.

⚠ **`PackNameSelectorViewComponent` and `PackNameInfoViewComponent` have no feature check of their
own** for PackName — they render nothing purely because of this guard (`PackNameInfoViewComponent`
reads `Subsidy` only for its subsidy button, §4.2). That is intentional and correct, but it
means changing or bypassing `SetCurrentPackNameAsync` would silently make both reappear on stores
with PackNames off. Both components carry a comment pointing back at this section; keep it there.

**The catalogue role set rides on the same guard** (phase 5 Part 1, 2026-10-01).
`CustomerOverrideService.GetCustomerRoleIdsAsync` returns the packname's visibility role set only when
`GetCurrentPackNameAsync` returns a packname, so with PackNames off every ACL reader gets nop's own role ids. Two
readers cannot rely on the context because they open `PackNameCatalogScope` for a packname that is not the
selected one, so each checks the switch itself: `ShoppingCartOverrideService.GetCartLinePackNameAsync` for the
line's store, and `ExchangesController.GetReturnedItemPackNameAsync` for the order's store (and, in the same way,
`ReturnSubmissionLogic.IsReplacementValidAsync`). Tested both ways on a running site (T10).

**The allocation gate rides on both switches** (phase 5 Part 2, 2026-10-01). `IPackNameCatalogGate` answers for
`ICustomerOverrideService.GetCatalogPackNameAsync` — the packname in `PackNameCatalogScope`, else the selected one —
so with PackNames off there is no packname and nothing is restricted. With Subsidies off the SQL views the gate reads
(`PackNameCanOrderAsync`, `GetOnlyAllocatedProductIdsAsync`) report "can order" and "unrestricted", and the setting
write clears their caches (lessons #88); the restriction marker in the role set is recomputed per request, so menus
and category lists change keys at once. Tested off and on in one running site, no restart (T11).

### 4.7 Customer account navigation and public "Credits for me"

`Services/EventConsumer.cs` → `HandleEventAsync(ModelPreparedEvent<BaseNopModel>)` → `AddCustomerNavigationItemsAsync` (customer nav)
and `Controllers/CreditNotesController.CreditsForMe` (public page) **must use the same predicate**,
or the nav tab disappears while the URL still works (or the reverse):

```csharp
features.CreditNotes || await _creditNotesService.CustomerHasCreditNotesAsync(customer.Id)
```

"My subsidies" is a capability, so it's a flat `features.Subsidy` check (checked first, before the
PackName/subsidy-allocation queries, to save the DB round trips on stores with Subsidy off); the item is
inserted only while the selected packname has an assignment, and `CustomerSubsidiesController` carries the
same feature gate (§4.4).

Since phase 7 both pages pass their own tab id to `CustomerNavigationViewComponent`
(`CWCoreDefaults.CustomerCreditsForMeMenuTab` = 21, `CustomerSubsidiesMenuTab` = 22); nop marks an item active
only when `SelectedTab == item.Tab`, so a page that passes `-1` is never highlighted (the Store Credit page did,
from its first version to phase 7).

Other Subsidy-gated readers added by phase 7: `IPackNameLandingService` (returns no route when the feature is off,
whatever `SubsidySendUserAfterLogin` says); the welcome e-mail's `%Subsidy.SubsidyName%` token (empty when nothing is
assigned). PackName-gated: `PrintNamePrefillEventConsumer` (returns before reading the settings when PackName is off).

### 4.8 Admin customer/order pages — liability and capability side by side

`Areas/Admin/Views/Customer/_CreateOrUpdate.cshtml`: PackName and Subsidy cards are flat-gated
(`features.PackName` / `features.Subsidy` — capabilities, correct to hide outright). The credit
notes card uses `Model.DisplayCreditNotes` (liability — `enabled ‖ has data`), set by
`CustomerOverrideModelFactory.PrepareCreditNotesVisibilityAsync`, and renders read-only (no add
form, no delete buttons, an info banner) when shown only because records exist.

Order edit page (`Areas/Admin/Views/Order/Edit.cshtml`,
`_CWOrderDetails.Payments.cshtml`) and its widget components are **feature-independent
containers** — the order page itself is never gated; only write affordances inside it would be, and
none currently need to be, since every write path on an order (returns, credit notes, subsidy) is
already covered by the controllers/services above.

### 4.9 Admin main menu

`Services/EventConsumer.cs` → `GetAdminMenuItemAsync`. Uses the any-store aggregate
(`IsEnabledForAnyStoreAsync`), since the menu has no store context:

- `CWCoreDefaults.ExchangesAdminMenuSystemName` node → visible when Exchanges **or** CreditNotes is
  on anywhere
- `CWCoreDefaults.FeeSettingsAdminMenuSystemName` child → visible when Exchanges is on anywhere
- `CWCoreDefaults.CreditNotesReportAdminMenuSystemName` child → visible when CreditNotes is on
  anywhere

`MultiStores`, `Default settings` and `Branches` stay unconditional. The admin credit-note **reports**
(`MainReport`, `StoreLiabilityList`, `StoreCustomerCreditNotesList`) are deliberately left ungated
even though the menu node that leads to them is gated — a known, harmless mismatch: the menu hides,
but the URLs stay reachable, which is fine because those actions display money owed (§1.2).

Invalidated on store insert/delete by `Services/Stores/Caching/StoreCacheEventConsumer.cs` (§2.4) —
**not** by adding handlers to the general-purpose `EventConsumer.cs`; see §6.5 for why that
distinction matters.

### 4.10 MultiStore admin area — the containment tree

The `/Admin/MultiStore/Edit/{id}` page is why this whole effort felt like it touched everywhere:
it's a **containment tree loaded by independent AJAX calls**, not a single server-rendered page.
There is no natural cascade — hiding a parent panel does not stop a child panel's request, because
the child is fetched by its own URL, triggered by its own click.

```
MultiStore Edit (storeId)
├── Info                                              always
├── Branches                                          always
│   ├── Branch detail        BranchDetail             always
│   ├── Branch addresses     BranchAddresses          always
│   ├── Cost centres         BranchCostCenters        always
│   └── PackName lists       PackNameLists            [PackName]
│       ├── list detail      PackNameListDetail       [PackName]
│       │   └── managers     ManagerList              [PackName]
│       └── packnames grid   PackNamesInline          [PackName]
│           └── pn detail    BranchPackNameDetail     [PackName]
│               ├── addresses  PackNameAddresses      [PackName]
│               └── ext panels PackNameExtensionPanels[PackName]
│                   └── Subsidies  PackNameSubsidies  [Subsidy]      ← IPackNamePanelProvider (§4.2)
├── Departments                                       always
│   └── department detail    DepartmentDetail         always
│       └── managers         DepartmentManagerList    [PackName]     ← easy to miss, not a PackName-named panel
├── Cost centres             CostCenters              always
├── PackNames tab            PackNames                [PackName]
│   └── pn detail            PackNameDetail           [PackName] → same subtree as above
├── Product groups           Subsidy/ProductGroups    [Subsidy]
├── Subsidies                Subsidy/Subsidies        [Subsidy]
│   └── subsidy detail → packnames  SubsidyPackNames  [Subsidy]
├── Settings                 StoreSettings            always (permission-gated only)
├── Exchange settings        ExchangeSettings VC      [Exchanges]
└── Fee settings             FeeSettings VC           [Exchanges]
```

Two consequences, and they're the whole design of this area:

1. **Every action and every view component guards itself.** Never rely on a parent panel not
   rendering — every child has its own URL and can be hit directly.
2. **The client must not even request panels for disabled features**, or every branch click fires a
   request that comes back empty and leaves a silent blank slot (§4.11 is what stops this).

`[Subsidy]` nodes never also check `[PackName]` — `CWFeatureSet.From` already computes
`Subsidy = SubsidyEnable && PackNameEnable` (§2.2), so one check covers both. Branches stays
always-visible with only its *PackName lists* sub-panel gated — cost centres and branch addresses
are unaffected, so the Branches tab stays fully useful with PackNames off. Departments follows the
same pattern: the tab and department CRUD are always on, only the Managers sub-panel (which is
keyed by `PackNameId`, not by anything Department-specific) is gated.

### 4.11 Client-side panel-request gating — `Content/Admin/MultiStore/admin.store.navigation.js`

The MultiStore Edit page's tab/panel navigation is hand-rolled JS (not the DataTables pattern used
elsewhere), so it needs its own feature-awareness — otherwise it fires AJAX requests for panels the
server will just block, leaving blank slots instead of no slot at all. `init(storeId, features, resources)`
(called as `Admin.StoreNav.init(...)` from `_CreateOrUpdate.cshtml`) takes the same `CWFeatureSet` shape passed from the view (§3.1's pattern, applied to tab navigation
instead of settings-field visibility):

```js
let _features = { packName: true, subsidy: true, exchanges: true, creditNotes: true };

function init(storeId, features, resources) {
    _storeId = storeId;
    if (features) _features = features;
    …
}
```

Three call sites check it before firing a request:

- **`tabExists(tab)`** — used by `init` to redirect an unknown/hidden tab (e.g. a bookmarked
  `#subsidies` hash on a store with Subsidy off) back to `info`, and by the `hashchange` handler for
  the same reason (§6.10 — the `hashchange` handler originally didn't call this, only `init` did).
- **`loadPackNameListsPanel(branchId)`** — the single loader used by both `reloadSubPanels` and
  `afterBrowseDetailLoaded`, so guarding inside the loader itself (rather than at each call site)
  covers both callers in one place. When PackName is off it removes the panel slot outright rather
  than leaving an empty one:
  ```js
  function loadPackNameListsPanel(branchId) {
      if (!_features.packName) { $('#pnlists-panel').remove(); return; }
      ensureSubPanel('pnlists-panel');
      loadPartial('/Admin/MultiStore/PackNameLists', { branchId }, 'pnlists-panel', function () { });
  }
  ```
  (the 5.00 function is the same, spread over more lines with comments)
- **`renderFocusMode()`** — guards the `list`/`packname` focus-entity branches, so a stale hash like
  `#branches/branch:3/list:8/edit:list` on a PackName-off store exits focus mode instead of loading
  `PackNameListDetail`/`BranchPackNameDetail`.

`loadTabPanel` needs no separate guard: `tabExists()` already keeps `init` off a hidden tab, and
`goTab` is only reachable by clicking a tab that was rendered in the first place.

### 4.12 Bulk ordering

`CWFeature.BulkOrder` (`MultiStoreSettings.BulkOrderEnable`) came after the review this document records, and is
described in full in the bulk-ordering documents. Where it is read (5.00 code, 2026-10-31):

| Where | Gate |
|---|---|
| `Controllers/BulkOrderController.cs` | `[CheckCWFeature(CWFeature.BulkOrder, StoreSource = CurrentStore)]` at class level |
| `Services/BulkOrder/BulkOrderEntryService.CanUseBulkOrderingAsync` | current store — the header link (`BulkOrderLinkViewComponent`) and the landing after sign-in |
| `Services/BulkOrder/BulkOrderProductPageService.TryPrepareAsync` | current store — the product-page grid |
| `Areas/Admin/Components/BulkOrderProductPageAdminViewComponent.cs` | any store — the product edit page's settings |

It is not part of the admin menu, the settings seeding (§2.8) or `admin.store.navigation.js`.

---

## 5. Deliberately open / not implemented

These were considered and explicitly left as-is — not oversights.

| Item | Location | Reasoning |
|---|---|---|
| `FeeConceptViewComponent` fee-add affordance | `Areas/Admin/Views/ReturnRequest/_CreateOrUpdate.FeesAndCharges.cshtml` | Lives on the admin return-request page (feature-independent container). Adding a fee to an in-flight request is arguably part of completing it — gating it risks stranding a request mid-workflow. Left open. |
| `AclDepartmentsViewComponent` | Category/Manufacturer ACL → department mappings | Open question never resolved: is Departments part of the `PackName` feature, or an independent MultiStore concept? Currently ships un-gated (the safe default). Since phase 5 Part 1 the mapping takes effect: with PackNames off nobody carries a department role, so an entity restricted to departments only is hidden from everyone — nop's own ACL semantics. |
| `ReturnsController` reason/action popups | `ReturnRequestReasonCreatePopup`, `ReturnRequestActionCreatePopup` | Configure nop's own return reasons/actions, not CW rules — gating would remove nop functionality from stores that never used CW exchanges. |
| `ReturnRequestsConfirmationsViewComponent` gating | `Components/ReturnRequestsConfirmationsViewComponent.cs` | Its only caller (`ConfirmReturnRequestSummary`) is already gated, and the component only ever renders confirmations for requests that already exist — a display path. No gating added; the `Invoke` → `InvokeAsync` await bug in the same file *was* fixed (unrelated to feature toggles). |
| Packname cart lines with **PackNames switched off** | nop's own cart | nop merges a new line of the same product into an existing packname line; the line keeps its packname link, which is used again when packnames come back on. Decided by phase 8 Part 2 (2026-10-19): no code — switching the feature off makes the store behave as standard nop, and nop merges. |
| Other packnames' lines with **Subsidies switched off** | `PackNameCheckoutController` / the packname checkout | With subsidies on, lines belonging to packnames the checkout does not show are hidden (F5, phase 6); with subsidies off they are listed. Decided by phase 8 Part 2 (2026-10-19): no code — recorded as what the store does with the feature off. |
| `CustomerPackNameCreatePopup` with a customer who has no branch | `Areas/Admin/Controllers/MultiStoreController.cs` (`CustomerPackNameCreatePopup` GET / POST) | The popup gates on the store of the customer's **branch** (`CustomerBranch` → `Branch.StoreId`), not on `RegisteredInStoreId`; a customer with no branch gets the `CustomerPackNameCannotCreatePopup` view and no packname is offered, and the POST resolves the store from the posted list's branch (`ResolvePackNameStoreIdAsync`, `AccessDeniedView` when it cannot). The former `storeId == 0` edge case (a guest customer judged by the all-stores defaults) no longer exists. |

---

## 6. History — mistakes caught in review, and why the fix is what it is

This section exists so nobody re-introduces the same mistake. Every one of these was a live
recommendation or shipped-code finding that got reversed after a specific review question.

### 6.1 `ExchangesLogic` credit-notes balance — do not gate this (the most important reversal)

**What was proposed**: zero the credit-notes balance inside `ExecuteExchangeWorkflowAsync` when
`CreditNotes` is off for the order's store, reasoning it was the same class of fix as the
`OrderPlacedEvent` auto-apply gate.

**Why it was wrong**: `ExecuteExchangeWorkflowAsync` only ever runs against a return request that
**already exists**. That puts it inside two rules this project already committed to: credit notes
are a **liability** ("hiding it does not discharge it" — §1.2) and **in-flight work must complete**
(§1.4 corollary). Zeroing a real, existing balance would force the customer to pay a difference they
are not actually owed — a stronger violation of the golden rule than the visual-hiding cases the
rule was originally written for. `OrderPlacedEvent`'s auto-apply gate is genuinely different: it
fires on a brand-new order with no pre-existing in-flight record, so gating *that* is a defensible,
separate policy choice.

**Final state**: the balance check (`DetermineCurrentCreditNotesBalanceAsync`) runs unconditionally.
Only a dead commented-out `PluginHelper.IsCreditNotesPluginInstalled()` guard was deleted — pure
cleanup, no behavioural code.

### 6.2 `ExchangesController.EditExchangedItem` — same mistake, caught before shipping

**What was proposed**: gate this action on `Exchanges`, reasoning "it's a write, so it should be
gated like the other creation actions."

**Why it was wrong**: `returnRequestId` can only ever refer to a request that has already been
submitted — `InsertReturnRequestAsync` only runs inside `ConfirmReturnRequestSummary` (the wizard's
final commit), so there is no earlier point where a row exists to edit. This modifies an
**existing** exchange's replacement item — in-flight work, the same category as §6.1, not a
creation write.

**Final state at the time**: left ungated, alongside `GenerateReturnRequestReceipt(s)`. (It was then
unreferenced by any view/JS in the plugin — orphaned but live.)

**Now (4.9 and 5.00 code):** `EditExchangedItem` **is** gated —
`[CheckCWFeature(CWFeature.Exchanges, StoreSource = CurrentStore)]` — and it is wired: the customer's
returns page (`Views/ReturnRequest/CustomerReturnRequests.cshtml`, `editExchangedItemUrl`) calls it. It
also checks that the request is the customer's own and still *Pending*. So with Exchanges off, a customer
can no longer change the replacement of a pending exchange; an admin can still complete it. When this
was changed back, and why, is not recorded here.

### 6.3 `OrderProcessingOverrideService.RelateOrderItemToReturnRequest` — a live bug found the same way

Not a draft recommendation this time — **already-shipped code**, found while re-checking every
gating decision in this effort against the golden rule after §6.1/§6.2 turned up the same mistake
twice. `MoveShoppingCartItemsToOrderItemsAsync` takes a required, non-nullable `ReturnRequestItem`
parameter and is only ever called while completing an in-flight exchange — the same code path as
§6.1. Gating the "relate order item to return request" call meant completing an in-flight exchange
on a store that had switched `Exchanges` off would create the replacement order but silently drop
the link back to the originating return request — breaking the Returns grid, receipt generation,
and anything else that walks from the order to its return request.

**Fix**: the gate was removed; the call is now unconditional.

### 6.4 The settings-screen cascade — see §3.2

The largest reversal, documented in full there: a review question about losing configured settings
on a temporary disable exposed that the entire "coerce dependent settings to `false` on save" design
was unnecessary and destructive. Covered in detail in §3.2 rather than repeated here.

### 6.5 The any-store cache invalidation (G11) — a duplicate-work mistake, not a golden-rule one

**What was proposed**: add `IConsumer<EntityInsertedEvent<Store>>`/`EntityDeletedEvent<Store>>`
handlers to `Services/EventConsumer.cs` to invalidate `CWFeatureDefaults.FeaturePrefix` on store
create/delete.

**Why it was wrong**: `Services/Stores/Caching/StoreCacheEventConsumer.cs` — a small class extending
nop's own `CacheEventConsumer<Store>` base — already does exactly this, in the location this
codebase already uses for per-entity cache invalidation. The original audit only grepped the big
`EventConsumer.cs` god-class for the interface pattern and missed that a dedicated
`CacheEventConsumer<T>` subclass existed elsewhere. Adding the proposed handlers would have
double-invalidated the same cache key on every store insert/delete, not fixed anything.

**Lesson**: when checking whether a side effect exists in this codebase, search for the side effect
itself (e.g. `grep -r "CWFeatureDefaults.FeaturePrefix"`), not just for one file's implementation of
a pattern that might live somewhere else too.

### 6.6 The "unused import" cleanup (G13) — invalidated twice by the same blind spot

**What was proposed**: four files (`PackNameCheckoutModelFactory.cs`, `PackNameModelFactory.cs`,
`PackNameOrderModelFactory.cs`, `Services/MultiStores/MultiStoreService.cs`) carry a now-unused
`using CW.Common.Helpers;` and should have it removed.

**First correction**: `CW.Common.Helpers` isn't only `PluginHelper`/`FormatHelper`/`HttpContextHelper`
— it also has `TaskExtensions` (`WhenAll2`/`WhenAll3`, extension methods). `PackNameModelFactory.cs`
and `MultiStoreService.cs` both call `.WhenAll3()`. Two files dropped from the removal list.

**Second correction**: the namespace *also* has `ColorHelper.PerceivedBrightness()`, another
extension method. Both remaining "safe" files (`PackNameCheckoutModelFactory.cs`,
`PackNameOrderModelFactory.cs`) call it. **All four originally-flagged files genuinely need the
import.** G13 is fully invalid — no files were touched.

**Lesson, twice-confirmed**: a plain-text grep for a namespace's class names
(`PluginHelper`/`FormatHelper`/`HttpContextHelper`/`ColorHelper`) misses every call to one of its
extension methods, because C# extension-method call syntax (`thing.Method()`) never mentions the
declaring class. Any future "is this import actually unused" check against `CW.Common.Helpers`
needs to also check for `.WhenAll2(`, `.WhenAll3(`, `.PerceivedBrightness(`, and `.IsAjaxRequest(`
(an extension method on `HttpRequest`, in `HttpContextHelper.cs`) — not just grep the class names.

*5.00:* none of the four files imports `CW.Common.Helpers` any more. `TaskExtensions` (`WhenAll2` /
`WhenAll3`) is not in `CWLibraries/CW.Common`, and the two factories take a packname's text colour from
`Helpers/PackNameColourHelper.ForegroundFor` instead of `ColorHelper.PerceivedBrightness`. The lesson
stands: `PerceivedBrightness` and `IsAjaxRequest` are still extension methods there.

### 6.7 `MessageTokenProviderOverride` — the one place the rule reads the other way

The two blocks writing exchange-fee and credit-note totals into message tokens were originally
flagged for gating (`Exchanges`/`CreditNotes`). The correct read of §1.4: an order placed while a
feature was on has real fee/credit-note lines, and suppressing them from a *re-sent* email for that
order would misrepresent the total — a data-integrity problem in the other direction. Verified both
`WriteExchangeFeesTotalsAsync` (empty `foreach` on no fees) and `WriteCreditNotesTotalsAsync`
(explicit `if (creditNotesHistories.Count > 0)` guard) are already empty-safe, so the fix was pure
cleanup: delete two dead commented-out `PluginHelper` guard pairs, add no feature-check code.
*(Phase 6, 2026-10-02: `WriteCreditNotesTotalsAsync` was replaced by `WriteSettlementTotalsAsync`, which reads the
order's settlement row and writes nothing when there is none — the same rule, still ungated.)*

### 6.8 `[CheckCWFeature]` on a view component is inert, and it silently "worked" on the first pass

**What shipped first**: `ExchangeSettingsComponent`, `FeeSettingsComponent`, and
`SubsidyBarViewComponent` were gated by putting `[CheckCWFeature(...)]` directly on the
`InvokeAsync` method, matching the pattern used everywhere else.

**Why it did nothing**: view components are not MVC actions. `IViewComponentHelper.InvokeAsync`
calls the component class directly and never runs the action-filter pipeline, so any
`IAsyncActionFilter`/`IAsyncAuthorizationFilter` attribute on a view component method is inert —
it's never invoked, never blocks anything, and produces no error, which is exactly why it went
unnoticed. (The same mistake already existed independently:
`Areas/Admin/Components/PackNameSubsidiesViewComponent.cs` had carried a `[CheckPermission(...)]`
attribute that had never once run, for reasons no one had traced back to this — that pre-existing,
silently-broken attribute is probably what made the pattern look valid to copy.)

**Fix**: delete the attribute, inject the feature service into the component's constructor, and
guard as the literal first statement of `InvokeAsync`, returning `Content(string.Empty)` when
blocked. See the current pattern in §4.2. **Lesson**: attributes only work where ASP.NET Core's
filter pipeline runs — controller actions and Razor Pages, not view components, not plain service
methods. If a gate needs to sit on a view component, it has to be an inline check.

### 6.9 `PackNameSubsidies` — right feature, wrong `StoreSource`, blocked on every store

**What shipped first**: `[CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]`
on `SubsidyController.PackNameSubsidies(int packNameId, string prefix)`. `Parameter` defaults to
looking for a `storeId` argument — and this action has none; both of its callers
(`PackNameSubsidies/Default.cshtml`, `Subsidy/_PackNameSubsidies.cshtml`) only ever send
`packNameId` and `prefix`.

**Effect**: the filter failed to resolve a store id on *every* call, on *every* store — including
stores with Subsidy fully on. The panel header still rendered (its view component passes its own
guard independently), but the body stayed permanently empty, and the nop log filled with "could not
resolve store id" warnings.

**Fix**: `StoreSource = CWFeatureStoreSource.PackName` — resolving `packNameId` →
`PackName.PackNameListId` → `PackNameList.StoreId`, the same chain `PackNameSubsidiesViewComponent`
already used. **Lesson, generalised**: after adding `[CheckCWFeature]` to an action, check that its
`StoreSource` actually matches what the action's callers send — `Parameter` silently degrades to
"always blocked" rather than "always allowed" when the parameter it's looking for isn't there, so
this class of mistake fails *safe* but also fails *silently*. A quick way to catch it: after
exercising every tab once, the nop log should have zero "could not resolve store id" warnings; any
line at all means one attribute somewhere has the wrong `StoreSource` or `ParameterName`.

### 6.10 `hashchange` didn't re-run the same check `init` did

`admin.store.navigation.js`'s `init()` redirects an unrenderable tab (e.g. a bookmarked `#subsidies`
hash on a Subsidy-off store) back to `info` via `tabExists()`. The `hashchange` listener — fired
when the hash changes without a full page load, e.g. back/forward navigation or a manually edited
URL — called `parseHash()` but not `tabExists()`, so the same scenario reached via `hashchange`
left no active tab, a blank panel body, and one pointless request to a server action that then
correctly blocked it. Cosmetic (harmless, since the server-side gate is what actually matters) but
confusing. **Fix**: added the same `tabExists()` check (plus `activateTabLink()`, to move the
highlight) to the `hashchange` handler — see §4.11.

### 6.11 Blocked DataTables reads produced a JS alert instead of an empty grid

`CWFeatureBlockResultBuilder`'s AJAX-POST branch returns `{ success: false, error }`, which is the
right shape for a CW admin form POST but not for a DataTables server-side read — DataTables expects
`{ draw, recordsTotal, recordsFiltered, data }` and raises its own JS error on anything else. Only
reachable from a stale/bookmarked page (the panel that would normally request the grid is itself
gated), so cosmetic rather than a real gap — but it's a rough edge a QA pass would flag. **Fix**:
`CWFeatureBlockResultBuilder` special-cases the DataTables shape by checking for the `draw` key
DataTables always posts, and returns a well-formed empty page instead — see the response table in
§2.5.

---

## 7. Verification checklist

Set up **two stores**: store A with all four features (PackName, Subsidy, Exchanges, CreditNotes) on,
store B with all four off (bulk ordering has its own tests in the bulk-ordering documents). Store B has
customer **B1** with a PackName, subsidy allocations, credit-note history, and at least one past
return request; customer **B2** has none.

**Core filter behaviour**

- [ ] `StoreSource = Parameter` resolves a plain `int storeId` parameter, a `StoreId` property on a
      bound model (case-insensitively), a route value, and a query-string value
- [ ] Omitting the store id where `Parameter` is declared blocks and logs a warning (fails closed)
- [ ] `StoreSource = Order` resolves the store from the order; a non-existent order id blocks
- [ ] Two stacked attributes AND; two features in one attribute OR
- [ ] A permission denial on an action that also has `[CheckCWFeature]` shows nop's `AccessDenied`
      page, not the feature message
- [ ] Blocked admin AJAX GET returns a zero-length body; blocked admin non-AJAX GET redirects with
      the error notification surviving the redirect
- [ ] Blocked public `/SubsidyCatalog` → homepage; blocked `/packname/checkout` → nop's checkout
      (`RedirectRouteName`), cart intact
- [ ] `AllowDefaultStore = true` actions accept `storeId = 0`; plain `Parameter` actions reject it
- [ ] An attribute written without `StoreSource` blocks and writes an **error** to the nop log naming
      the action (§2.5) — worth confirming once on a scratch action, since it is the safety net for
      every gate added from here on
- [ ] Nothing throws during a fresh install, before the database exists
- [ ] After exercising every tab on the MultiStore Edit page for both a full-features store and a
      PackName-off store, the nop log has **zero** "could not resolve store id" warnings (§6.9)

**MultiStore admin area (containment tree, §4.10/§4.11)**

- [ ] Store B (PackName off): Branches tab shows branch detail, addresses and cost centres; **no**
      PackName lists panel and no empty/loading slot where it used to be; network tab shows no
      request to `PackNameLists`
- [ ] Store B: `GET PackNameLists`/`PackNamesInline` by hand return an empty body;
      `POST DeletePackName` with a store-B id returns `{ success: false }`
- [ ] Store B: no PackNames, Product groups or Subsidies tab; Exchange/Fee settings tabs still work;
      Departments tab works but shows no Managers panel
- [ ] Store B: manually setting the hash to `#subsidies` lands on Info with the Info tab
      highlighted, both on initial load and via `hashchange` (back/forward or manual edit)
- [ ] Store A (all on): branch → PackName lists → list → PackNames grid → PackName detail →
      Subsidies panel body **loads** (not just the header) — this is the regression check for §6.9
- [ ] Store A: PackName detail → "Add customer" popup → select a customer → **Save works** and does
      not redirect to the dashboard — the regression check for §6.8's form-body fix
- [ ] Store A with Subsidy off only: PackNames tab/lists still work, Subsidies panel gone from
      PackName detail, Product groups/Subsidies tabs gone
- [ ] A blocked DataTables grid read (e.g. hit a gated grid endpoint directly on a disabled store)
      renders an empty table, not a JS alert (§6.11)

**Settings screens**

- [ ] Turning `SubsidyEnable` off hides the subsidy-dependent rows immediately, no save needed
- [ ] Multi-governor rows (`AllowSubsidySelection`, `AllowEditSubsidyTopup`,
      `AllowSubsidyCountByExcel`) stay hidden regardless of which governor is off, and regardless of
      toggle order
- [ ] Set `PackNamesOwnStartAndFinishDates` checked → turn `PackNameEnable` off → save → the row is
      hidden but the *stored* value is still `true` in the DB
- [ ] Turn `PackNameEnable` back on → save without touching the now-visible row → it still reads
      checked (no reconfiguration needed — this is the behaviour §3.2 exists to guarantee)
- [ ] With `PackNameEnable` off, `Subsidy` is still effectively off everywhere it's checked (admin
      menu, `SubsidyController`/`SubsidyCatalogController` access, order totals) even though the
      stored `SubsidyEnable` value was left untouched
- [ ] Both settings screens (`StoreSettings` and `Settings`) behave identically

**Historical / in-flight data (the important half)**

- [ ] An order placed in store A with a subsidy applied still shows its breakdown after
      `SubsidyEnable` is turned off — admin and public
- [ ] B1's credit-note balance is still visible on "Credits for me" and the admin customer page;
      add form and delete buttons are gone; the list is not
- [ ] Cancelling that order still returns the credit and the subsidy
- [ ] A store-B return request that was `NeedsCompletion` before the switch-off can still be
      completed by an admin, **still consumes** an existing credit-notes balance if one covers the
      total, and the resulting replacement order still links back to the original return request
- [ ] Its receipt PDF still generates; `EditExchangedItem` on an existing request still works
- [ ] Placing a **new** order in store B applies no credit notes
- [ ] Cart totals in store B contain no subsidy discount even with stale `SubsidyPackName` rows,
      including on the checkout **preview** path, not just the final total

**Public fallback**

- [ ] B1 has no PackName selector or info banner on the storefront
- [ ] `/cart`/`/checkout` in store B are nop's standard flow; an order can be placed end to end
- [ ] Store B return flow posts to nop's standard return request, not `/Exchanges/Gather`
- [ ] B1 still sees "Credits for me" (has records); B2 does not
- [ ] Neither B1 nor B2 sees "My subsidies"

**Menu & performance**

- [ ] With Exchanges and CreditNotes off everywhere, the Exchanges & Returns menu node disappears;
      enabling either for one store brings it back after save
- [ ] Creating a new store while a feature is off everywhere else shows the menu node immediately,
      without a settings save (confirms `StoreCacheEventConsumer`, not a settings-changed event)
- [ ] Profile one admin MultiStore Edit render and one public product page: `MultiStoreSettings`
      materialisations per request ≤ number of distinct stores touched (the per-request memo is
      working); `IsEnabledForAnyStoreAsync` doesn't query per store on every admin page

**Messages**

- [ ] Re-sending the order-completed email for a store-A order with exchange fees and applied
      credit notes still shows both lines
- [ ] The same email for a new store-B order has no empty fee/credit-note header rows

---

## 8. Where things live — file index

### 8.1 The organisation rule

The feature-toggle code lives in **three** folders, split by layer. Adding a file anywhere else, or
to the wrong one of these three, is what starts the drift this section exists to prevent:

| Folder | Holds | Test |
|---|---|---|
| `Domain/Settings/` | `CWFeature`, `CWFeatureSet` | **what the features are** — plain types, no dependencies |
| `Services/Settings/` | `ICWFeatureService`, `CWFeatureService`, `CWFeatureDefaults` | **are they on?** — pure logic and caching |
| `Filters/` | `CheckCWFeatureAttribute`, `CheckCWFeatureFilter`, `CWFeatureStoreSource`, `ICWFeatureGuard`, `CWFeatureGuard`, `ICWFeatureBlockResultBuilder`, `CWFeatureBlockResultBuilder` | **what happens when they're off** — everything HTTP-aware |

> **The rule, in one line:** *nothing under `Services/Settings/` may reference
> `Microsoft.AspNetCore.*`.* If a type takes an `HttpRequest` or returns an `IActionResult`, it
> belongs in `Filters/` with the attribute.

That test is checkable by reading a file's `using` block, which is the property that matters — "put
feature-toggle things in `Services/Settings/`" is not enforceable, because the next person has to
guess what counts.

Two consequences that look surprising until you apply the rule:

- **`ICWFeatureGuard` is in `Filters/`, not `Services/`,** even though controllers inject it like a
  service. It returns `IActionResult`, and it is the imperative twin of `[CheckCWFeature]` — the two
  are alternatives to each other, so they are found together. Controllers already import `Filters`
  for the attribute, so this costs nothing at the call site.
- **The block-result builder stayed in `Filters/`** rather than moving next to the guard. Moving it
  into `Services/` would have pushed the MVC dependency deeper into the service layer instead of
  containing it.

**Folder and namespace must agree.** A file physically in `Filters/` that still declares
`namespace Nop.Plugin.CW.Core.Services.Settings` passes the folder test and fails the real one — the
MVC-typed member is still in the service namespace, and anyone navigating by namespace is misled.

### 8.2 File index

| Area | Path |
|---|---|
| Feature enum, feature set | `Domain/Settings/CWFeature.cs`, `CWFeatureSet.cs` |
| Service | `Services/Settings/ICWFeatureService.cs`, `CWFeatureService.cs`, `CWFeatureDefaults.cs` |
| Filter, guard and block result | `Filters/CheckCWFeatureAttribute.cs`, `CheckCWFeatureFilter.cs`, `CWFeatureStoreSource.cs`, `ICWFeatureGuard.cs`, `CWFeatureGuard.cs`, `ICWFeatureBlockResultBuilder.cs`, `CWFeatureBlockResultBuilder.cs` |
| Cache invalidation | `Services/Stores/Caching/StoreCacheEventConsumer.cs` (store insert/delete), `Services/Settings/Caching/SettingCacheEventConsumer.cs` (setting writes), `Services/EventConsumer.cs` (`MultiStoreSettingsChangedEvent` → `ClearAsync()`) |
| PackName fallback | `Services/PackNames/PackNameContext.cs`, `Services/EventConsumer.cs`, `Filters/PackNameCheckoutRedirectFilter.cs`, `Controllers/PackNameController.cs`, `Controllers/PackNameCheckoutController.cs` |
| Settings screens (client-side) | `Areas/Admin/Views/MultiStore/_StoreSettings.cshtml`, `Settings.cshtml` |
| Settings screens (server-side) | `Areas/Admin/Controllers/MultiStoreController.cs` (`StoreSettings`, `Settings` POST actions) |
| MultiStore admin area (containment tree, entity-scoped gating) | `Areas/Admin/Controllers/MultiStoreController.cs`, `Areas/Admin/Controllers/SubsidyController.cs`, `Content/Admin/MultiStore/admin.store.navigation.js`, `Areas/Admin/Views/MultiStore/_CreateOrUpdate.cshtml` |
| PackName panel providers | `Infrastructure/IPackNamePanelProvider.cs`, `Infrastructure/SubsidyPackNamePanelProvider.cs`, `Areas/Admin/Components/PackNameExtensionPanelsViewComponent.cs` |
| Public exchange wizard | `Controllers/ExchangesController.cs`, `Views/ReturnRequest/ReturnRequest.cshtml`, `BusinessLogic/ExchangesLogic.cs` |
| Credit notes | `Controllers/CreditNotesController.cs`, `Areas/Admin/Controllers/CreditNotesController.cs`, `Components/CreditNotesWidgetsViewComponent.cs`, `Areas/Admin/Factories/CustomerOverrideModelFactory.cs` |
| Subsidy | `Controllers/SubsidyCatalogController.cs`, `Controllers/CustomerSubsidiesController.cs`, `Areas/Admin/Controllers/SubsidyController.cs`, `Components/SubsidyBarViewComponent.cs`, `Services/Settlement/SubsidySettlementSource.cs`, `Services/Subsidies/SubsidyAllocationService.cs` |
| Admin nav / menu | `Services/EventConsumer.cs` (`GetAdminMenuItemAsync`, `HandleEventAsync(ModelPreparedEvent<...>)`) |
| Order/money paths | `Services/Settlement/SubsidySettlementSource.cs`, `CreditNoteSettlementSource.cs`, `Services/Orders/OrderProcessingOverrideService.cs`, `ShoppingCartOverrideService.cs` |
| Returns (5.00) | `Filters/ReturnRequestSubmitFilter.cs`, `Factories/ReturnRequestOverrideModelFactory.cs` |
| Bulk ordering | `Controllers/BulkOrderController.cs`, `Services/BulkOrder/BulkOrderEntryService.cs`, `BulkOrderProductPageService.cs`, `Areas/Admin/Components/BulkOrderProductPageAdminViewComponent.cs` |
| Message tokens | `Services/Messages/MessageTokenProviderOverride.cs` |

---
## 9. Open items

Every correctness, performance and readability finding from the 2026-08-06 code review has been
implemented and verified in code. What remains below is preventive work and two decisions that are
not mine to make. Entries are deleted as they are done; the durable knowledge from a completed item
moves into the relevant section above rather than living here.

### 9.1 Guard rails — prevention, not fixes

The most expensive mistakes this system has produced were all **silent**: no exception, no compile
error, just a feature that quietly stopped working. Three of the four are now structurally
impossible or loud — an unregistered store source can no longer fall through to the current store
(§2.5), and a missing `StoreSource` now blocks and logs an error rather than defaulting
(§2.5, was G2). One remains detectable only by inspection.

**G1 — startup self-check.** On application start, reflect over the plugin assembly and log an error
for:

- any type assignable to `ViewComponent` carrying `[CheckCWFeature]` — always a bug (§6.8). This part
  is **exact**: there are no false positives, because the attribute genuinely never runs there.
- any action whose `StoreSource` names an identifier that is not an action parameter, a bound-model
  property or a route token — catches §6.9 at boot rather than in production.

Roughly 40 lines in `NopStartup.Configure`, run once, wrapped so a diagnostic can never block
startup. **Not implemented — deliberately deferred.** The second check cannot see steps 4 and 5 of
`ResolveInt` (query string and form body), so it reports false positives for actions that legitimately
resolve their id there — `AssociateCustomerToPackNamePopup`, which posts `BranchId` in the form body,
is the known example. Handling that needs either a `SuppressStartupCheck` opt-out property on the
attribute plus a one-time triage pass, or narrowing the check to the view-component case only. Left
alone for now rather than adding noise; revisit if §6.9-class mistakes recur.

**G3 — tests.** The core of this system has no test coverage. CW tests live in `Tests/CW.Tests`
(`CLAUDE.md` §22 Note A; CW.Core's suite in `Tests/CW.Tests/Core`), and several of them mock
`ICWFeatureService` for a feature-off case (e.g. `PackNameCheckoutRedirectFilterTests`,
`ReturnRequestSubmitFilterTests`, `PackNameInfoViewComponentTests`), but none tests the filter, the
feature set or the block result. The highest-value targets, all pure and dependency-light:

- `CheckCWFeatureFilter`'s five-step resolution order in `ResolveInt`, and that each step is reached
  only when the earlier ones miss;
- its fail-closed behaviour — unresolved id, unregistered source, `NotSet` source, entity not found,
  `storeId == 0` without `AllowDefaultStore`;
- `CWFeatureSet.From`'s `Subsidy = SubsidyEnable && PackNameEnable` implication;
- `CWFeatureBlockResultBuilder`'s four response shapes, especially the DataTables `draw` branch.

**These tests have a home (`Tests/CW.Tests/Core/Filters/`); none are written yet.**

### 9.2 Notes for whoever picks these up

- The `AllowDefaultStore` rejection of `storeId == 0` applies to the **attribute** path only. The
  inline `ICWFeatureGuard` path has no equivalent; every inline caller today resolves the store from an entity
  (a list's or branch's store) and refuses before guarding when it cannot, so none reaches the guard with 0.

---

*Last verified against shipped code 2026-08-06; §4.1 admin controllers and §5 re-verified 2026-10-21; whole
document checked against the 5.00 code 2026-10-31.*
