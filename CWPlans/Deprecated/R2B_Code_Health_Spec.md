# Round 2, part B — code health

**Date:** 2026-11-12. **Owner:** Eduardo. **Status:** applied, tested and approved 2026-11-12 (closed). **Round plan:** `NOP41_Gap_Rounds_Plan.md` (Round 2).

**What this part does.** It closes the code-health items of Round 2, each re-checked against the code on 2026-11-12 (after R2-A was applied), plus one tidy-up R2-A left behind.

| Item | What changes | Changes |
|---|---|---|
| **3.12** | The dead `EventConsumer` handler for cart-line updates is deleted, with the two fields and constructor parameters only it used | 1–4 |
| **3.13** | `GetPackNameListsByBranchAsync` (name order) is deleted. Its three callers use `GetPackNameListsByBranchIdAsync`; the two whose screens list by name sort by name themselves. Nothing the user sees changes | 6–9, 11, 25–26 |
| **3.9** | The unused `ForeColor` and `Warnings` are deleted from the admin `PackNameModel`. The region `Duplicated from customer service` is renamed | 10, 12–14 |
| **Two routes to a manager's store** (from the R2-A validation) | `GetManagerStoreIdAsync` goes on `IMultiStoreService`, and the storefront packname controller and selector use it instead of going through the list's branch. It now also refuses a list whose branch is deleted, as the branch route did, so nobody gains access. A list with no branch keeps its own store. The branch cache clear that R2-A removed comes back, because branches matter again | 5, 15–17, 19, 20 |
| **3.20** | Every address write is logged against its owner: Manage Store's three popups and three deletes, the branch set-default, the standalone *Branches* page's four actions. So is the storefront *Edit packname* save. In all, 12 writes | 18, 21–24, 27–33 |
| **3.4** | The admin packname order card's hard-coded texts become resources: the four the item named, plus the *Shared* badge and its tooltip in the same view. The packname names stay encoded: `@T(key, args)` would return an unencoded `HtmlString`, so the texts use `string.Format(T(key).Text, …)` | 34 |
| **3.3** | The five literal `data-url="/Admin/MultiStore/…"` become `@Url.Action(...)` | 35–39 |
| **3.15** | The four `Credit` columns that defaulted to `DECIMAL(19, 5)` declare `(18, 4)`. This reaches new installations only, with no migration | 40–43 |
| — | The new resources: ten activity-log descriptions and five order-card texts. DATA version bump | 44–48 |

**Decisions in this spec.** None needed you. Each follows an existing pattern:
- An address write is logged under its owner's existing *edit* activity type (`EditBranch`, `EditPackName`, `EditCostCenter`) with its own description, so no new activity types are installed.
- The storefront packname edit uses the same `EditPackName` type as the admin edit, as the storefront delete already uses `DeletePackName`.
- 3.13 keeps the id-ordered method, because the provisioning and registration fallbacks depend on its order.

**The resource dictionary** (`CWCorePlugin`, changes 44–47) is a deliberate exception to lesson #47: it is one member of about 1,000 lines, so its four insertions are given as four blocks.

**Dead code this part removes** (lesson #56):
- 3.12: the handler, its interface entry, the two fields and their constructor parameters.
- 3.13: the duplicate method.
- 3.9: the two model fields.
- Two routes: `PackNameController` and the selector no longer use `GetBranchByPackNameIdAsync` for the store. The method stays, because four other callers use it for the branch itself.

Nothing else becomes dead.

**Not in this part:**
- The address a packname customer adds at the checkout (`PackNameCheckoutController.AddAddress`) is a storefront customer action and is not logged, like nop's own address book.
- The two sweeps (`alert()` and `@T` in scripts) are R2-C. So is a sixth literal admin URL, found by the review: `_Branches.BranchDetail.cshtml:285` (`$.post('/Admin/MultiStore/DeleteBranch', ...)`). It sits inside a script, which is the R2-C sweep's ground.
- **Found by the review, older than this round:** `DeleteBranch` soft-deletes a branch and leaves its packname lists and packnames live. This spec keeps their managers refused (above), but the lists themselves are recorded as a new open item, 4.19, for a decision.

## How to apply

Labels:
- **REPLACE:** the FIND block, at the lines given, becomes the REPLACE WITH block.
- **WHOLE FILE:** the file's entire content becomes the block.

Each change names its file under its heading. Within a file the blocks are listed **bottom to top**. The line numbers of each FIND are those of the file as it stands when that block is reached, so applying them in the order given never moves the next one. Every FIND and REPLACE starts and ends on a whole line. The project compiles only once all of them are applied.

## Changes
### EventConsumer.cs

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

**3.12.** `EventConsumer` handled a cart-line update only to republish it as an update of the matching `PackNameCartItem` row, and nothing consumes that event. This change deletes:
- the handler;
- its `IConsumer<>` entry;
- the two fields only it used (`_packNameCartItemRepository`, `_eventPublisher`);
- their constructor parameters.

The constructor is given whole.

#### 1. REPLACE — lines 706–732 (1 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

FIND (lines 706–732, 27 lines):

```csharp
    }

    /// <summary>
    /// Handle entity deleted event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityUpdatedEvent<ShoppingCartItem> eventMessage)
    {
        if (eventMessage.Entity is null)
            return;

        var query = from item in _packNameCartItemRepository.Table
                    where item.ShoppingCartItemId == eventMessage.Entity.Id
                    select item;

        var packNameCartItem = await query.FirstOrDefaultAsync();

        if (packNameCartItem != null)
        {
            //event notification for cache management
            await _eventPublisher.EntityUpdatedAsync(packNameCartItem);
        }
    }

    public Task HandleEventAsync(AdditionalTokensAddedEvent eventMessage)
    {
```

REPLACE WITH (4 lines):

```csharp
    }

    public Task HandleEventAsync(AdditionalTokensAddedEvent eventMessage)
    {
```

#### 2. REPLACE — lines 98–150 (2 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

FIND (lines 98–150, 53 lines):

```csharp
    public EventConsumer(IPermissionService permissionService,
        ILocalizationService localizationService,
        IPluginManager<IPlugin> pluginManager,
        IAdminMenu adminMenu,
        IStaticCacheManager staticCacheManager,
        IHttpContextAccessor httpContextAccessor,
        IAclOverrideService aclService,
        IPackNameService packNameService,
        IPackNameContext packNameContext,
        IWorkContext workContext,
        IRepository<PackNameCartItem> packNameCartItemRepository,
        IEventPublisher eventPublisher,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        ISubsidyService subsidyService,
        ISubsidyReportingService subsidyReportingService,
        ICWFeatureService cwFeatureService,
        ICreditNotesService creditNotesService,
        ICWOrderService cwOrderService,
        IOrderService orderService,
        IShipmentService shipmentService,
        IExchangeService exchangeService,
        IReturnResolutionLogic returnResolutionLogic,
        IBulkOrderProductPageService bulkOrderProductPageService,
        BulkOrderProductPageContext bulkOrderProductPageContext,
        IGenericAttributeService genericAttributeService,
        IShoppingCartOverrideService shoppingCartService) : base(pluginManager)
    {
        _permissionService = permissionService;
        _localizationService = localizationService;
        _adminMenu = adminMenu;
        _staticCacheManager = staticCacheManager;
        _httpContextAccessor = httpContextAccessor;
        _aclService = aclService;
        _packNameService = packNameService;
        _packNameCartItemRepository = packNameCartItemRepository;
        _eventPublisher = eventPublisher;
        _workContext = workContext;
        _packNameContext = packNameContext;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _subsidyService = subsidyService;
        _subsidyReportingService = subsidyReportingService;
        _cwFeatureService = cwFeatureService;
        _creditNotesService = creditNotesService;
        _cwOrderService = cwOrderService;
        _orderService = orderService;
        _shipmentService = shipmentService;
        _exchangeService = exchangeService;
        _returnResolutionLogic = returnResolutionLogic;
        _bulkOrderProductPageService = bulkOrderProductPageService;
        _bulkOrderProductPageContext = bulkOrderProductPageContext;
        _genericAttributeService = genericAttributeService;
        _shoppingCartService = shoppingCartService;
    }
```

REPLACE WITH (49 lines):

```csharp
    public EventConsumer(IPermissionService permissionService,
        ILocalizationService localizationService,
        IPluginManager<IPlugin> pluginManager,
        IAdminMenu adminMenu,
        IStaticCacheManager staticCacheManager,
        IHttpContextAccessor httpContextAccessor,
        IAclOverrideService aclService,
        IPackNameService packNameService,
        IPackNameContext packNameContext,
        IWorkContext workContext,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        ISubsidyService subsidyService,
        ISubsidyReportingService subsidyReportingService,
        ICWFeatureService cwFeatureService,
        ICreditNotesService creditNotesService,
        ICWOrderService cwOrderService,
        IOrderService orderService,
        IShipmentService shipmentService,
        IExchangeService exchangeService,
        IReturnResolutionLogic returnResolutionLogic,
        IBulkOrderProductPageService bulkOrderProductPageService,
        BulkOrderProductPageContext bulkOrderProductPageContext,
        IGenericAttributeService genericAttributeService,
        IShoppingCartOverrideService shoppingCartService) : base(pluginManager)
    {
        _permissionService = permissionService;
        _localizationService = localizationService;
        _adminMenu = adminMenu;
        _staticCacheManager = staticCacheManager;
        _httpContextAccessor = httpContextAccessor;
        _aclService = aclService;
        _packNameService = packNameService;
        _workContext = workContext;
        _packNameContext = packNameContext;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _subsidyService = subsidyService;
        _subsidyReportingService = subsidyReportingService;
        _cwFeatureService = cwFeatureService;
        _creditNotesService = creditNotesService;
        _cwOrderService = cwOrderService;
        _orderService = orderService;
        _shipmentService = shipmentService;
        _exchangeService = exchangeService;
        _returnResolutionLogic = returnResolutionLogic;
        _bulkOrderProductPageService = bulkOrderProductPageService;
        _bulkOrderProductPageContext = bulkOrderProductPageContext;
        _genericAttributeService = genericAttributeService;
        _shoppingCartService = shoppingCartService;
    }
```

#### 3. REPLACE — lines 73–78 (3 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

FIND (lines 73–78, 6 lines):

```csharp
    protected readonly IAclOverrideService _aclService;
    protected readonly IPackNameService _packNameService;
    protected readonly IRepository<PackNameCartItem> _packNameCartItemRepository;
    protected readonly IEventPublisher _eventPublisher;
    protected readonly IWorkContext _workContext;
    protected readonly IPackNameContext _packNameContext;
```

REPLACE WITH (4 lines):

```csharp
    protected readonly IAclOverrideService _aclService;
    protected readonly IPackNameService _packNameService;
    protected readonly IWorkContext _workContext;
    protected readonly IPackNameContext _packNameContext;
```

#### 4. REPLACE — lines 52–56 (4 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`

FIND (lines 52–56, 5 lines):

```csharp
    IConsumer<EntityInsertedEvent<Product>>,
    IConsumer<CustomerLoggedinEvent>,
    IConsumer<EntityUpdatedEvent<ShoppingCartItem>>,
    IConsumer<AdminMenuCreatedEvent>,
    IConsumer<AdditionalTokensAddedEvent>,
```

REPLACE WITH (4 lines):

```csharp
    IConsumer<EntityInsertedEvent<Product>>,
    IConsumer<CustomerLoggedinEvent>,
    IConsumer<AdminMenuCreatedEvent>,
    IConsumer<AdditionalTokensAddedEvent>,
```

### BranchCacheEventConsumer.cs

`Plugins/Nop.Plugin.CW.Core/Services/Branches/Caching/BranchCacheEventConsumer.cs`

**Two routes, continued.** A manager's store depends on the list's branch again, because a deleted branch refuses. So a branch change clears the manager-store cache again. This is the line R2-A removed.

#### 5. REPLACE — lines 39–42 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Branches/Caching/BranchCacheEventConsumer.cs`

FIND (lines 39–42, 4 lines):

```csharp
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKeyAllPrefix);
        await RemoveAsync(BranchServicesDefaults.DefaultShippingMethodsByBranchCacheKey, entity.Id);
        await RemoveByPrefixAsync(BranchServicesDefaults.BranchByCustomerIdPrefix);
```

REPLACE WITH (5 lines):

```csharp
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(BranchServicesDefaults.ManagerStoreIdCacheKeyAllPrefix);
        await RemoveAsync(BranchServicesDefaults.DefaultShippingMethodsByBranchCacheKey, entity.Id);
        await RemoveByPrefixAsync(BranchServicesDefaults.BranchByCustomerIdPrefix);
```

### IPackNameService.cs

`Plugins/Nop.Plugin.CW.Core/Services/PackNames/IPackNameService.cs`

**3.13.** `GetPackNameListsByBranchAsync` and `GetPackNameListsByBranchIdAsync` ran the same query and differed only in order (name vs id). This keeps the id-ordered one, which provisioning and registration rely on (their fallback is the first active list in creation order). Its doc comment, which had two summary lines, now says what it returns.

#### 6. REPLACE — lines 69–79 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/PackNames/IPackNameService.cs`

FIND (lines 69–79, 11 lines):

```csharp
        Task<IList<PackNameList>> GetActivePackNameListsByStoreIdAsync(int storeId);

        /// <summary>
        /// Gets packaging name list by branch identifier
        /// </summary>
        /// <param name="branchId">Branch identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the packNameLists
        /// </returns>
        Task<IList<PackNameList>> GetPackNameListsByBranchIdAsync(int branchId);
```

REPLACE WITH (11 lines):

```csharp
        Task<IList<PackNameList>> GetActivePackNameListsByStoreIdAsync(int storeId);

        /// <summary>
        /// Gets the packname lists of a branch, deleted ones left out, in the order they were created
        /// </summary>
        /// <param name="branchId">Branch identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the packname lists; the first active one is the fallback when a branch has no default
        /// </returns>
        Task<IList<PackNameList>> GetPackNameListsByBranchIdAsync(int branchId);
```

#### 7. REPLACE — lines 37–51 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/PackNames/IPackNameService.cs`

FIND (lines 37–51, 15 lines):

```csharp
        /// The task result contains the packNameLists
        /// </returns>
        Task<IList<PackNameList>> GetPackNameListsByBranchAsync(int branchId);

        /// <summary>
        /// Gets packNameLists by branch identifier
        /// </summary>
        /// <param name="branchId">Branch identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the packNameLists
        /// </returns>
        Task<IList<PackNameListDTO>> GetPackNameListDetailsByBranchAsync(int branchId);

        /// <summary>
```

REPLACE WITH (5 lines):

```csharp
        /// The task result contains the packNameLists
        /// </returns>
        Task<IList<PackNameListDTO>> GetPackNameListDetailsByBranchAsync(int branchId);

        /// <summary>
```

### PackNameService.cs

`Plugins/Nop.Plugin.CW.Core/Services/PackNames/PackNameService.cs`

**3.13**: the name-ordered method is deleted. **3.9**: the region `Duplicated from customer service` is renamed `Customer addresses`. It holds the live `GetCustomerAddressAsync`, which nothing duplicates.

#### 8. REPLACE — lines 239–249 (1 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/PackNames/PackNameService.cs`

FIND (lines 239–249, 11 lines):

```csharp
    }

    /// <summary>
    /// Gets packaging name list by branch identifier
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<IList<PackNameList>> GetPackNameListsByBranchIdAsync(int branchId)
```

REPLACE WITH (11 lines):

```csharp
    }

    /// <summary>
    /// Gets the packname lists of a branch, deleted ones left out, in the order they were created
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packname lists; the first active one is the fallback when a branch has no default
    /// </returns>
    public async Task<IList<PackNameList>> GetPackNameListsByBranchIdAsync(int branchId)
```

#### 9. REPLACE — lines 139–164 (2 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/PackNames/PackNameService.cs`

FIND (lines 139–164, 26 lines):

```csharp
    {
        return await _packNameListRepository.GetByIdsAsync(packNameListIds);
    }

    /// <summary>
    /// Gets packNameLists by branch identifier
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<IList<PackNameList>> GetPackNameListsByBranchAsync(int branchId)
    {
        if (branchId == 0)
            return [];

        var query = from pnl in _packNameListRepository.Table
                    where pnl.BranchId == branchId && !pnl.Deleted
                    orderby pnl.Name
                    select pnl;

        return await query.ToListAsync();
    }

    /// <summary>
```

REPLACE WITH (5 lines):

```csharp
    {
        return await _packNameListRepository.GetByIdsAsync(packNameListIds);
    }

    /// <summary>
```

#### 10. REPLACE — lines 87–91 (3 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/PackNames/PackNameService.cs`

FIND (lines 87–91, 5 lines):

```csharp
    #region Methods

    #region Duplicated from customer service

    /// <summary>
```

REPLACE WITH (5 lines):

```csharp
    #region Methods

    #region Customer addresses

    /// <summary>
```

### PackNameModelFactory.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/PackNameModelFactory.cs`

**3.13.** The list picker of the admin packname form now calls the remaining method and sorts by name itself, so it shows the same order as before. The fallback is also unchanged: the default list, or else the first active list by name.

#### 11. REPLACE — lines 116–121 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/PackNameModelFactory.cs`

FIND (lines 116–121, 6 lines):

```csharp
            return listId;

        var packNameLists = (await _packNameService.GetPackNameListsByBranchAsync(branchId))
            .Where(packNameList => packNameList.Active || packNameList.Id == listId)
            .ToList();
        var activeLists = packNameLists.Where(packNameList => packNameList.Active).ToList();
```

REPLACE WITH (7 lines):

```csharp
            return listId;

        var packNameLists = (await _packNameService.GetPackNameListsByBranchIdAsync(branchId))
            .Where(packNameList => packNameList.Active || packNameList.Id == listId)
            .OrderBy(packNameList => packNameList.Name)
            .ToList();
        var activeLists = packNameLists.Where(packNameList => packNameList.Active).ToList();
```

### PackNameModel.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/PackNames/PackNameModel.cs`

**3.9.** `ForeColor` and `Warnings` are deleted: no view, factory or controller reads or writes them. The storefront and cart models have fields with the same names, and those stay. The Mapster map `PackName → PackNameModel` maps by name, so it loses nothing.

#### 12. REPLACE — lines 101–107 (1 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/PackNames/PackNameModel.cs`

FIND (lines 101–107, 7 lines):

```csharp
    public bool IsNew { get; set; }

    public IList<string> Warnings { get; set; }

    public CWAddressSearchModel CWAddressSearchModel { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }
```

REPLACE WITH (5 lines):

```csharp
    public bool IsNew { get; set; }

    public CWAddressSearchModel CWAddressSearchModel { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }
```

#### 13. REPLACE — lines 49–54 (2 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/PackNames/PackNameModel.cs`

FIND (lines 49–54, 6 lines):

```csharp
    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Colour")]
    public string Colour { get; set; }

    public string ForeColor { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender")]
```

REPLACE WITH (4 lines):

```csharp
    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Colour")]
    public string Colour { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender")]
```

#### 14. REPLACE — lines 20–24 (3 of 3 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/PackNames/PackNameModel.cs`

FIND (lines 20–24, 5 lines):

```csharp
        CWAddressSearchModel = new CWAddressSearchModel();
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
        Warnings = new List<string>();
        Active = true;
    }
```

REPLACE WITH (4 lines):

```csharp
        CWAddressSearchModel = new CWAddressSearchModel();
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
        Active = true;
    }
```

### IMultiStoreService.cs

`Plugins/Nop.Plugin.CW.Core/Services/MultiStores/IMultiStoreService.cs`

**Two routes to one answer.** R2-A made the store a manager belongs to come from their own list. The storefront packname controller and selector still worked it out through the list's branch. `GetManagerStoreIdAsync` is now on the interface so both use the one cached answer.

#### 15. REPLACE — lines 226–229 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/MultiStores/IMultiStoreService.cs`

FIND (lines 226–229, 4 lines):

```csharp
    #region PackNames

    Task<IPagedList<PackNameDTO>> GetPackNameManagersByStoreAsync(int storeId, int branchId, int packNameListId = 0, string name = null, bool fromOtherBrances = false,
        int pageIndex = 0, int pageSize = int.MaxValue);
```

REPLACE WITH (15 lines):

```csharp
    #region PackNames

    /// <summary>
    /// Gets the store of a manager: the store of the list their own packname is in. Store-scoped settings and
    /// lookups for a manager take the store from here, never from the request
    /// </summary>
    /// <param name="managerId">The manager's packname identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store identifier; null when the packname or its list is missing or deleted
    /// </returns>
    Task<int?> GetManagerStoreIdAsync(int managerId);

    Task<IPagedList<PackNameDTO>> GetPackNameManagersByStoreAsync(int storeId, int branchId, int packNameListId = 0, string name = null, bool fromOtherBrances = false,
        int pageIndex = 0, int pageSize = int.MaxValue);
```

### MultiStoreService.cs

`Plugins/Nop.Plugin.CW.Core/Services/MultiStores/MultiStoreService.cs`

The method moves from `Utilities` to the public `PackNames` region and becomes `public virtual`. It also refuses a list whose branch is deleted. The branch route the storefront used refused that case, and deleting a branch leaves its lists in place, so without this a manager on a deleted branch would get in. A list with no branch is accepted with its own store. The two callers inside the class are unaffected.

#### 16. REPLACE — lines 658–661 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/MultiStores/MultiStoreService.cs`

FIND (lines 658–661, 4 lines):

```csharp
    #region PackNames

    public async Task<IPagedList<PackNameDTO>> GetPackNameManagersByStoreAsync(int storeId, int branchId, int packNameListId = 0, string name = null, bool fromOtherBrances = false,
        int pageIndex = 0, int pageSize = int.MaxValue)
```

REPLACE WITH (30 lines):

```csharp
    #region PackNames

    /// <summary>
    /// Gets the store of a manager: the store of the list their own packname is in. Store-scoped settings and
    /// lookups for a manager take the store from here, never from the request
    /// </summary>
    /// <param name="managerId">The manager's packname identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store identifier; null when the packname, its list or the list's branch is
    /// missing or deleted (deleting a branch leaves its lists in place, and their managers lose their store with it)
    /// </returns>
    public virtual async Task<int?> GetManagerStoreIdAsync(int managerId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            BranchServicesDefaults.ManagerStoreIdCacheKey, managerId);

        return await _staticCacheManager.GetAsync(key, async () =>
            await (
                from pn in _packNameRepository.Table
                join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                where pn.Id == managerId && !pn.Deleted && !pnl.Deleted
                    && (pnl.BranchId == null || _branchRepository.Table.Any(branch => branch.Id == pnl.BranchId && !branch.Deleted))
                select (int?)pnl.StoreId
            ).FirstOrDefaultAsync()
        );
    }

    public async Task<IPagedList<PackNameDTO>> GetPackNameManagersByStoreAsync(int storeId, int branchId, int packNameListId = 0, string name = null, bool fromOtherBrances = false,
        int pageIndex = 0, int pageSize = int.MaxValue)
```

#### 17. REPLACE — lines 88–115 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/MultiStores/MultiStoreService.cs`

FIND (lines 88–115, 28 lines):

```csharp
    #endregion

    #region Utilities

    /// <summary>
    /// Gets the store of a manager: the store of the list their own packname is in
    /// </summary>
    /// <param name="managerId">The manager's packname identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store identifier; null when the packname or its list is missing or deleted
    /// </returns>
    protected virtual async Task<int?> GetManagerStoreIdAsync(int managerId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            BranchServicesDefaults.ManagerStoreIdCacheKey, managerId);

        return await _staticCacheManager.GetAsync(key, async () =>
            await (
                from pn in _packNameRepository.Table
                join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                where pn.Id == managerId && !pn.Deleted && !pnl.Deleted
                select (int?)pnl.StoreId
            ).FirstOrDefaultAsync()
        );
    }

    /// <summary>
```

REPLACE WITH (5 lines):

```csharp
    #endregion

    #region Utilities

    /// <summary>
```

### PackNameController.cs

`Plugins/Nop.Plugin.CW.Core/Controllers/PackNameController.cs`

**Two routes, continued:** `ResolveManagerStoreIdAsync` now asks `IMultiStoreService.GetManagerStoreIdAsync`, and is given whole with its doc comment. The remark it kept still holds: the store is never taken from the request.

**3.20, storefront:** the manager's *Edit packname* save is logged (`EditPackName`, as the admin edit and the storefront delete are), once the transaction has committed.

#### 18. REPLACE — lines 926–929 (1 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Controllers/PackNameController.cs`

FIND (lines 926–929, 4 lines):

```csharp
            }

            //Verify that the manager can still select the packname: the branch may have moved to one
            //whose lists the manager does not own, even though the move itself was permitted.
```

REPLACE WITH (8 lines):

```csharp
            }

            //activity log, once the save is committed
            await _customerActivityService.InsertActivityAsync("EditPackName",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditPackName"), packName.Name), packName);

            //Verify that the manager can still select the packname: the branch may have moved to one
            //whose lists the manager does not own, even though the move itself was permitted.
```

#### 19. REPLACE — lines 225–254 (2 of 2 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Controllers/PackNameController.cs`

FIND (lines 225–254, 30 lines):

```csharp
    }

    /// <summary>
    /// The store a manager belongs to, resolved from the branch their own packname list hangs from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PackName</c> has no <c>StoreId</c> (the store comes from list → branch) and
    /// <c>MultiStoreService.GetManagerStoreIdAsync</c> is protected and not on the interface, so
    /// <c>GetBranchByPackNameIdAsync</c> is the route. It skips deleted branches.
    /// </para>
    /// <para>
    /// <b>Never take the store from the request.</b> <c>MultiStoreSettings</c> is store-scoped and
    /// carries this controller's authorisation gates — <c>AllowManagersToAddPackNames</c> and
    /// <c>LimitAssignedBranches</c>. Loading them for a posted store id lets a manager pick the
    /// store whose settings suit them. Both GETs already read the store from the
    /// list; this is how the POSTs do the same.
    /// </para>
    /// </remarks>
    protected virtual async Task<int?> ResolveManagerStoreIdAsync(PackName manager)
    {
        if (manager == null)
            return null;

        var managerBranch = await _packNameService.GetBranchByPackNameIdAsync(manager.Id);

        return managerBranch?.StoreId;
    }

    /// <summary>
```

REPLACE WITH (24 lines):

```csharp
    }

    /// <summary>
    /// The store a manager belongs to: the store of the list their own packname is in
    /// (<c>IMultiStoreService.GetManagerStoreIdAsync</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never take the store from the request.</b> <c>MultiStoreSettings</c> is store-scoped and
    /// carries this controller's authorisation gates — <c>AllowManagersToAddPackNames</c> and
    /// <c>LimitAssignedBranches</c>. Loading them for a posted store id lets a manager pick the
    /// store whose settings suit them. Both GETs already read the store from the
    /// list; this is how the POSTs do the same.
    /// </para>
    /// </remarks>
    protected virtual async Task<int?> ResolveManagerStoreIdAsync(PackName manager)
    {
        if (manager == null)
            return null;

        return await _multiStoreService.GetManagerStoreIdAsync(manager.Id);
    }

    /// <summary>
```

### PackNameSelectorViewComponent.cs

`Plugins/Nop.Plugin.CW.Core/Components/PackNameSelectorViewComponent.cs`

**Two routes, continued.** The *Add* and *Excel* buttons read the settings of the store the manager belongs to; that store now comes from the same method.

#### 20. REPLACE — lines 105–110 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Components/PackNameSelectorViewComponent.cs`

FIND (lines 105–110, 6 lines):

```csharp
        //the add and Excel buttons follow the settings of the manager's own store, as the actions behind them do
        var manager = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);
        var managerBranch = manager == null ? null : await _packNameService.GetBranchByPackNameIdAsync(manager.Id);
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(managerBranch?.StoreId ?? store.Id);

        model.CanAddPackNames = storeSettings.AllowManagersToAddPackNames;
```

REPLACE WITH (6 lines):

```csharp
        //the add and Excel buttons follow the settings of the manager's own store, as the actions behind them do
        var manager = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);
        var managerStoreId = manager == null ? null : await _multiStoreService.GetManagerStoreIdAsync(manager.Id);
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(managerStoreId ?? store.Id);

        model.CanAddPackNames = storeSettings.AllowManagersToAddPackNames;
```

### MultiStoreController.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

**3.13.** The two list callers move to the remaining method:
- `SetDefaultPackNameList` doesn't care about order;
- `GetListsByBranchId` sorts by name itself, so its picker and fallback are unchanged.

**3.20.** Every address write on Manage Store is logged against its owner, under the owner's existing edit type (`EditBranch`, `EditPackName`, `EditCostCenter`):
- the three popups' saves, as *add* or *edit*;
- the three deletes, only once the address is actually removed;
- setting a branch's default address.

#### 21. REPLACE — lines 2533–2539 (1 of 9 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 2533–2539, 7 lines):

```csharp
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _multiStoreService.RemoveCostCenterAddressAsync(costCenter, address))
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

        return new NullJsonResult();
```

REPLACE WITH (13 lines):

```csharp
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _multiStoreService.RemoveCostCenterAddressAsync(costCenter, address))
        {
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

            //activity log
            await _customerActivityService.InsertActivityAsync("EditCostCenter",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteCostCenterAddress"), costCenter.Id, address.Id), costCenter);
        }

        return new NullJsonResult();
```

#### 22. REPLACE — lines 2506–2511 (2 of 9 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 2506–2511, 6 lines):

```csharp
                await _multiStoreService.InsertCostCenterAddressAsync(costCenter, address);
            }

            ViewBag.RefreshPage = true;

            return PartialView(new CWAddressModel());
```

REPLACE WITH (12 lines):

```csharp
                await _multiStoreService.InsertCostCenterAddressAsync(costCenter, address);
            }

            //activity log
            await _customerActivityService.InsertActivityAsync("EditCostCenter",
                string.Format(await _localizationService.GetResourceAsync(model.Address.Id > 0
                    ? "Plugins.CW.Core.ActivityLog.EditCostCenterAddress"
                    : "Plugins.CW.Core.ActivityLog.AddCostCenterAddress"), costCenter.Id, address.Id), costCenter);

            ViewBag.RefreshPage = true;

            return PartialView(new CWAddressModel());
```

#### 23. REPLACE — lines 2197–2203 (3 of 9 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 2197–2203, 7 lines):

```csharp
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _packNameService.RemovePackNameAddressAsync(packName, address))
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

        return new NullJsonResult();
```

REPLACE WITH (13 lines):

```csharp
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _packNameService.RemovePackNameAddressAsync(packName, address))
        {
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

            //activity log
            await _customerActivityService.InsertActivityAsync("EditPackName",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeletePackNameAddress"), packName.Name, address.Id), packName);
        }

        return new NullJsonResult();
```

#### 24. REPLACE — lines 2161–2166 (4 of 9 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 2161–2166, 6 lines):

```csharp
                await _packNameService.InsertPackNameAddressAsync(packName, address);
            }

            ViewBag.RefreshPage = true;

            return PartialView(new CWAddressModel());
```

REPLACE WITH (12 lines):

```csharp
                await _packNameService.InsertPackNameAddressAsync(packName, address);
            }

            //activity log
            await _customerActivityService.InsertActivityAsync("EditPackName",
                string.Format(await _localizationService.GetResourceAsync(model.Address.Id > 0
                    ? "Plugins.CW.Core.ActivityLog.EditPackNameAddress"
                    : "Plugins.CW.Core.ActivityLog.AddPackNameAddress"), packName.Name, address.Id), packName);

            ViewBag.RefreshPage = true;

            return PartialView(new CWAddressModel());
```

#### 25. REPLACE — lines 1157–1163 (5 of 9 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 1157–1163, 7 lines):

```csharp
            return Json(new List<object>());

        //a switched-off list takes no new packnames, so it is not offered
        var lists = (await _packNameService.GetPackNameListsByBranchAsync(branchId)).Where(list => list.Active).ToList();
        var selectedId = (lists.FirstOrDefault(list => list.IsDefault) ?? lists.FirstOrDefault())?.Id;

        var result = lists.Any() ? (from l in lists
```

REPLACE WITH (7 lines):

```csharp
            return Json(new List<object>());

        //a switched-off list takes no new packnames, so it is not offered
        var lists = (await _packNameService.GetPackNameListsByBranchIdAsync(branchId)).Where(list => list.Active).OrderBy(list => list.Name).ToList();
        var selectedId = (lists.FirstOrDefault(list => list.IsDefault) ?? lists.FirstOrDefault())?.Id;

        var result = lists.Any() ? (from l in lists
```

#### 26. REPLACE — lines 1103–1107 (6 of 9 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 1103–1107, 5 lines):

```csharp
    public virtual async Task<IActionResult> SetDefaultPackNameList(int listId, int branchId)
    {
        var packNameLists = await _packNameService.GetPackNameListsByBranchAsync(branchId);

        //The feature attribute gates on branchId; listId was never tied to it. An out-of-branch listId
```

REPLACE WITH (5 lines):

```csharp
    public virtual async Task<IActionResult> SetDefaultPackNameList(int listId, int branchId)
    {
        var packNameLists = await _packNameService.GetPackNameListsByBranchIdAsync(branchId);

        //The feature attribute gates on branchId; listId was never tied to it. An out-of-branch listId
```

#### 27. REPLACE — lines 911–915 (7 of 9 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 911–915, 5 lines):

```csharp
            return Json(new { success = false, error = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound") });

        await _branchService.SetDefaultBranchAddressAsync(branch, address);

        return Json(new { success = true });
```

REPLACE WITH (9 lines):

```csharp
            return Json(new { success = false, error = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound") });

        await _branchService.SetDefaultBranchAddressAsync(branch, address);

        //activity log
        await _customerActivityService.InsertActivityAsync("EditBranch",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.SetDefaultBranchAddress"), branch.Id, address.Id), branch);

        return Json(new { success = true });
```

#### 28. REPLACE — lines 893–899 (8 of 9 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 893–899, 7 lines):

```csharp
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _branchService.RemoveBranchAddressAsync(branch, address))
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

        return new NullJsonResult();
```

REPLACE WITH (13 lines):

```csharp
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _branchService.RemoveBranchAddressAsync(branch, address))
        {
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

            //activity log
            await _customerActivityService.InsertActivityAsync("EditBranch",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteBranchAddress"), branch.Id, address.Id), branch);
        }

        return new NullJsonResult();
```

#### 29. REPLACE — lines 866–871 (9 of 9 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 866–871, 6 lines):

```csharp
                await _branchService.InsertBranchAddressAsync(branch, address);
            }

            ViewBag.RefreshPage = true;

            return PartialView(new CWAddressModel());
```

REPLACE WITH (12 lines):

```csharp
                await _branchService.InsertBranchAddressAsync(branch, address);
            }

            //activity log
            await _customerActivityService.InsertActivityAsync("EditBranch",
                string.Format(await _localizationService.GetResourceAsync(model.Address.Id > 0
                    ? "Plugins.CW.Core.ActivityLog.EditBranchAddress"
                    : "Plugins.CW.Core.ActivityLog.AddBranchAddress"), branch.Id, address.Id), branch);

            ViewBag.RefreshPage = true;

            return PartialView(new CWAddressModel());
```

### BranchController.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

**3.20, the standalone *Branches* page:** address create, edit, delete and set-default are logged the same way.

`AddressDelete` used to delete the address record even when the branch mapping wasn't removed. It now uses the same `if` as Manage Store's delete. Because the address is already checked to belong to the branch, that `if` is always true, so the behaviour doesn't change.

#### 30. REPLACE — lines 518–521 (1 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

FIND (lines 518–521, 4 lines):

```csharp
        await _branchService.SetDefaultBranchAddressAsync(branch, address);

        return Json(new { success = true });
    }
```

REPLACE WITH (8 lines):

```csharp
        await _branchService.SetDefaultBranchAddressAsync(branch, address);

        //activity log
        await _customerActivityService.InsertActivityAsync("EditBranch",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.SetDefaultBranchAddress"), branch.Id, address.Id), branch);

        return Json(new { success = true });
    }
```

#### 31. REPLACE — lines 493–497 (2 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

FIND (lines 493–497, 5 lines):

```csharp
            await _addressService.UpdateAddressAsync(address);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Addresses.Updated"));

            return RedirectToAction("AddressEdit", new { addressId = model.Address.Id, branchId = model.EntityId });
```

REPLACE WITH (9 lines):

```csharp
            await _addressService.UpdateAddressAsync(address);

            //activity log
            await _customerActivityService.InsertActivityAsync("EditBranch",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditBranchAddress"), branch.Id, address.Id), branch);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Addresses.Updated"));

            return RedirectToAction("AddressEdit", new { addressId = model.Address.Id, branchId = model.EntityId });
```

#### 32. REPLACE — lines 432–436 (3 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

FIND (lines 432–436, 5 lines):

```csharp
            await _addressService.InsertAddressAsync(address);

            await _branchService.InsertBranchAddressAsync(branch, address);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Addresses.Added"));
```

REPLACE WITH (9 lines):

```csharp
            await _addressService.InsertAddressAsync(address);

            await _branchService.InsertBranchAddressAsync(branch, address);

            //activity log
            await _customerActivityService.InsertActivityAsync("EditBranch",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.AddBranchAddress"), branch.Id, address.Id), branch);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Addresses.Added"));
```

#### 33. REPLACE — lines 379–386 (4 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

FIND (lines 379–386, 8 lines):

```csharp
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        await _branchService.RemoveBranchAddressAsync(branch, address);

        //now delete the address record
        await _addressService.DeleteAddressAsync(address);

        return new NullJsonResult();
```

REPLACE WITH (13 lines):

```csharp
            return Content(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Address.NotFound"));

        if (await _branchService.RemoveBranchAddressAsync(branch, address))
        {
            //now delete the address record
            await _addressService.DeleteAddressAsync(address);

            //activity log
            await _customerActivityService.InsertActivityAsync("EditBranch",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.DeleteBranchAddress"), branch.Id, address.Id), branch);
        }

        return new NullJsonResult();
```

### Default.cshtml

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Shared/Components/PackNameOrderAdminDetails/Default.cshtml`

**3.4.** The hard-coded "Branch:", "Department:", "Cost Centre:" and "Shipping shared with: …" on the admin packname order card become resources. So do the *Shared* badge text and its tooltip in the same view.

The texts with packname names use `string.Format(T(key).Text, …)`, the pattern of the storefront card. `@T(key, args)` returns an `HtmlString`, which would print the names unencoded, and managers type those names. Given whole because six edits land in one view.

#### 34. WHOLE FILE — replace the entire content of the file with this

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Shared/Components/PackNameOrderAdminDetails/Default.cshtml`

(268 lines)

```cshtml
@model AdminPackNameOrderDetailsModel

@using Nop.Core.Domain.Tax;
@using Nop.Core.Domain.Catalog;

@if (!Model.HasPackNameItems)
{
    return;
}

@{
    var customer = await workContext.GetCurrentCustomerAsync();

    const string hidePackNameBlockAttributeName = "OrderPage.HidePackNameBlock";
    var hidePackNameBlock = await genericAttributeService.GetAttributeAsync<bool>(customer, hidePackNameBlockAttributeName);
}

@foreach (var group in Model.Groups)
{
    <nop-card asp-name="packname-order-@group.PackNameId" asp-icon="fas fa-user-tag" asp-title="@group.PackName" asp-hide-block-attribute-name="@hidePackNameBlockAttributeName" asp-hide="@hidePackNameBlock" asp-advanced="false">
        <div class="card-body">
            <div class="row">
                <div class="col-md-4">
                    <table class="adminData">
                        <tr>
                            <td class="adminTitle">@T("Plugins.CW.Core.Admin.Orders.Field.Branch"):</td>
                            <td class="adminData">@group.Branch (@group.BranchCode)</td>
                        </tr>
                        @if (!string.IsNullOrEmpty(group.Department))
                        {
                            <tr>
                                <td class="adminTitle">@T("Plugins.CW.Core.Admin.Orders.Field.Department"):</td>
                                <td class="adminData">@group.Department</td>
                            </tr>
                        }
                        @if (!string.IsNullOrEmpty(group.CostCenter))
                        {
                            <tr>
                                <td class="adminTitle">@T("Plugins.CW.Core.Admin.Orders.Field.CostCenter"):</td>
                                <td class="adminData">@group.CostCenter</td>
                            </tr>
                        }
                    </table>
                </div>
                <div class="col-md-4">
                    <table class="adminData">
                        <tr>
                            <td class="adminTitle">@T("Admin.Orders.Fields.ShippingMethod"):</td>
                            <td class="adminData">
                                @group.ShippingMethod
                                @if (group.IsSharedShipping)
                                {
                                    <span class="badge badge-info ml-1" title="@string.Format(T("Plugins.CW.Core.Admin.Orders.SharedShippingWith").Text, string.Join(", ", group.SharedWithPackNames))">
                                        <i class="fas fa-share-alt"></i> @T("Plugins.CW.Core.Admin.Orders.SharedShipping")
                                    </span>
                                }
                            </td>
                        </tr>
                        <tr>
                            <td class="adminTitle">@T("Admin.Orders.Fields.ShippingStatus"):</td>
                            <td class="adminData">@group.ShippingStatus</td>
                        </tr>
                        @if (!string.IsNullOrEmpty(group.ShippingExclTaxFormatted))
                        {
                            <tr>
                                <td class="adminTitle">@T("Plugins.CW.Core.Admin.Orders.Products.ShippingCost"):</td>
                                <td class="adminData">
                                    @if (Model.AllowCustomersToSelectTaxDisplayType)
                                    {
                                        <div>@Html.Raw(group.ShippingInclTaxFormatted)</div>
                                        <div>@Html.Raw(group.ShippingExclTaxFormatted)</div>
                                    }
                                    else
                                    {
                                        switch (Model.TaxDisplayType)
                                        {
                                            case TaxDisplayType.ExcludingTax:
                                            {
                                                @Html.Raw(group.ShippingExclTaxFormatted)
                                            }
                                                break;
                                            case TaxDisplayType.IncludingTax:
                                            {
                                                @Html.Raw(group.ShippingInclTaxFormatted)
                                            }
                                                break;
                                            default:
                                                break;
                                        }
                                    }
                                </td>
                            </tr>
                        }
                    </table>
                </div>
                <div class="col-md-4">
                    @if (!group.PickupInStore && !string.IsNullOrEmpty(group.AddressName))
                    {
                        <address>
                            <strong>@group.AddressName</strong><br />
                            @if (!string.IsNullOrEmpty(group.AddressLine1)) { @group.AddressLine1 <br /> }
                            @if (!string.IsNullOrEmpty(group.AddressLine2)) { @group.AddressLine2 <br /> }
                            @if (!string.IsNullOrEmpty(group.AddressCityStateZip)) { @group.AddressCityStateZip <br /> }
                            @if (!string.IsNullOrEmpty(group.AddressCountry)) { @group.AddressCountry }
                            @if (!string.IsNullOrEmpty(group.AddressPhone))
                            {
                                <br />@T("Plugins.CW.Core.Admin.Orders.Field.Phone")<text>:</text> @group.AddressPhone
                            }
                        </address>
                    }
                    @if (group.IsSharedShipping && group.SharedWithPackNames.Count > 0)
                    {
                        <div class="mt-2 text-muted small">
                            <i class="fas fa-share-alt"></i>
                            @string.Format(T("Plugins.CW.Core.Admin.Orders.SharedShippingWith").Text, string.Join(", ", group.SharedWithPackNames))
                        </div>
                    }
                </div>
            </div>

            <div class="table-responsive mt-3">
                <table class="table table-hover table-bordered">
                    <thead>
                        <tr>
                            <th>@T("Admin.Orders.Products.Picture")</th>
                            <th>@T("Admin.Orders.Products.ProductName")</th>
                            <th>@T("Admin.Orders.Products.Price")</th>
                            <th>@T("Admin.Orders.Products.Quantity")</th>
                            <th>@T("Admin.Orders.Products.Total")</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var item in group.Items)
                        {
                            <tr>
                                <td class="text-center preview">
                                    <img src="@item.PictureThumbnailUrl" alt="" title="" />
                                </td>
                                <td>
                                    <em><a asp-controller="Product" asp-action="Edit" asp-route-id="@item.ProductId">@item.ProductName</a></em>
                                    @if (!string.IsNullOrEmpty(item.AttributeInfo))
                                    {
                                        <p>@Html.Raw(item.AttributeInfo)</p>
                                    }
                                    @if (!string.IsNullOrEmpty(item.Sku))
                                    {
                                        <p><strong>@T("Admin.Orders.Products.SKU")</strong><text>:</text> @item.Sku</p>
                                    }
                                </td>
                                <td class="text-center">
                                    @if (Model.AllowCustomersToSelectTaxDisplayType)
                                    {
                                        <div>@Html.Raw(item.UnitPriceInclTax)</div>
                                        <div>@Html.Raw(item.UnitPriceExclTax)</div>
                                    }
                                    else
                                    {
                                        switch (Model.TaxDisplayType)
                                        {
                                            case TaxDisplayType.ExcludingTax:
                                                @Html.Raw(item.UnitPriceExclTax)
                                                break;
                                            case TaxDisplayType.IncludingTax:
                                                @Html.Raw(item.UnitPriceInclTax)
                                                break;
                                        }
                                    }
                                </td>
                                <td class="text-center">@item.Quantity</td>
                                <td class="text-center">
                                    @if (Model.AllowCustomersToSelectTaxDisplayType)
                                    {
                                        <div>@Html.Raw(item.SubTotalInclTax)</div>
                                        <div>@Html.Raw(item.SubTotalExclTax)</div>
                                    }
                                    else
                                    {
                                        switch (Model.TaxDisplayType)
                                        {
                                            case TaxDisplayType.ExcludingTax:
                                                @Html.Raw(item.SubTotalExclTax)
                                                break;
                                            case TaxDisplayType.IncludingTax:
                                                @Html.Raw(item.SubTotalInclTax)
                                                break;
                                        }
                                    }
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
            @if (group.HasSubsidy)
            {
                <div class="mt-3 px-2 py-2 border-top">
                    <div class="d-flex align-items-center mb-1">
                        <i class="fas fa-tag text-warning mr-2"></i>
                        <strong>@T("Plugins.CW.Core.Admin.Order.SubsidyApplied")</strong>
                    </div>
                    <table class="table table-sm table-borderless mb-0" style="width:auto; min-width:260px;">
                        @if (group.HasShippingSubsidy)
                        {
                            <tr>
                                <td class="text-muted py-0">@T("Plugins.CW.Core.Admin.Order.SubsidyForStock")</td>
                                <td class="text-right py-0">(@Html.Raw(group.SubsidyForStockFormatted))</td>
                            </tr>
                            <tr>
                                <td class="text-muted py-0">@T("Plugins.CW.Core.Admin.Order.SubsidyForShipping")</td>
                                <td class="text-right py-0">(@Html.Raw(group.SubsidyForShippingFormatted))</td>
                            </tr>
                        }
                        <tr>
                            <td class="font-weight-bold py-0">@T("Plugins.CW.Core.Admin.Order.SubsidyTotal")</td>
                            <td class="text-right font-weight-bold py-0">(@Html.Raw(group.SubsidyTotalFormatted))</td>
                        </tr>
                    </table>
                </div>
            }
        </div>
    </nop-card>
}

@if (Model.HasSettlement)
{
    var settlement = Model.SettlementTotals;

    <div class="card card-warning">
        <div class="card-header with-border">
            <h3 class="card-title">@T("Plugins.CW.Core.Admin.Order.SubsidySummary")</h3>
        </div>
        <div class="card-body">
            <div class="form-group row">
                <div class="col-md-4"><label>@T("Plugins.CW.Core.Admin.Order.SettleableTotal")</label></div>
                <div class="col-md-8">@Html.Raw(settlement.GrossTotal)</div>
            </div>
            @if (settlement.HasSubsidy)
            {
                @if (settlement.HasShippingSubsidy)
                {
                    <div class="form-group row">
                        <div class="col-md-4"><label>@T("Plugins.CW.Core.Admin.Order.SubsidyForStock")</label></div>
                        <div class="col-md-8">(@Html.Raw(settlement.SubsidyForStock))</div>
                    </div>
                    <div class="form-group row">
                        <div class="col-md-4"><label>@T("Plugins.CW.Core.Admin.Order.SubsidyForShipping")</label></div>
                        <div class="col-md-8">(@Html.Raw(settlement.SubsidyForShipping))</div>
                    </div>
                }
                <div class="form-group row">
                    <div class="col-md-4"><label>@T("Plugins.CW.Core.Admin.Order.SubsidyTotal")</label></div>
                    <div class="col-md-8"><strong>(@Html.Raw(settlement.SubsidyTotal))</strong></div>
                </div>
            }
            @if (settlement.HasCreditNotes)
            {
                <div class="form-group row">
                    <div class="col-md-4"><label>@T("Plugins.CW.Core.Admin.Order.CreditNotes")</label></div>
                    <div class="col-md-8"><strong>(@Html.Raw(settlement.CreditNoteTotal))</strong></div>
                </div>
            }
            <div class="form-group row">
                <div class="col-md-4"><label><strong>@T("Plugins.CW.Core.Admin.Order.PayableBalance")</strong></label></div>
                <div class="col-md-8"><strong>@Html.Raw(settlement.PayableBalance)</strong></div>
            </div>
        </div>
    </div>
}
```

### _Departments.DepartmentDetail.cshtml

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_Departments.DepartmentDetail.cshtml`

**3.3.** The delete button's `data-url` comes from `@Url.Action`, so renaming an action breaks the build instead of silently breaking the button (`CLAUDE.md` §9.14). The same one-line change is made in the four partials that follow.

#### 35. REPLACE — lines 48–52 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_Departments.DepartmentDetail.cshtml`

FIND (lines 48–52, 5 lines):

```cshtml
                                id="btn-department-delete"
                                data-id="@Model.Id"
                                data-url="/Admin/MultiStore/DeleteDepartment">
                            <i class="fas fa-trash" aria-hidden="true"></i>
                            @T("Admin.Common.Delete")
```

REPLACE WITH (5 lines):

```cshtml
                                id="btn-department-delete"
                                data-id="@Model.Id"
                                data-url="@Url.Action("DeleteDepartment", "MultiStore")">
                            <i class="fas fa-trash" aria-hidden="true"></i>
                            @T("Admin.Common.Delete")
```

### _Branches.CostCenterDetail.cshtml

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_Branches.CostCenterDetail.cshtml`

**3.3.**

#### 36. REPLACE — lines 52–56 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_Branches.CostCenterDetail.cshtml`

FIND (lines 52–56, 5 lines):

```cshtml
                                id="btn-focus-delete"
                                data-id="@Model.Id"
                                data-url="/Admin/MultiStore/DeleteCostCenter"
                                data-confirm="@T("Plugins.CW.Core.Admin.CostCenters.DeleteConfirmMessage")">
                            <i class="fas fa-trash" aria-hidden="true"></i>
```

REPLACE WITH (5 lines):

```cshtml
                                id="btn-focus-delete"
                                data-id="@Model.Id"
                                data-url="@Url.Action("DeleteCostCenter", "MultiStore")"
                                data-confirm="@T("Plugins.CW.Core.Admin.CostCenters.DeleteConfirmMessage")">
                            <i class="fas fa-trash" aria-hidden="true"></i>
```

### _Branches.PackNameDetail.cshtml

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_Branches.PackNameDetail.cshtml`

**3.3.**

#### 37. REPLACE — lines 50–54 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_Branches.PackNameDetail.cshtml`

FIND (lines 50–54, 5 lines):

```cshtml
                                id="btn-focus-delete"
                                data-id="@Model.Id"
                                data-url="/Admin/MultiStore/DeletePackName"
                                data-confirm="@T("Plugins.CW.Core.Admin.PackNames.DeleteConfirmMessage")">
                            <i class="fas fa-trash" aria-hidden="true"></i>
```

REPLACE WITH (5 lines):

```cshtml
                                id="btn-focus-delete"
                                data-id="@Model.Id"
                                data-url="@Url.Action("DeletePackName", "MultiStore")"
                                data-confirm="@T("Plugins.CW.Core.Admin.PackNames.DeleteConfirmMessage")">
                            <i class="fas fa-trash" aria-hidden="true"></i>
```

### _CostCenters.CostCenterDetail.cshtml

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_CostCenters.CostCenterDetail.cshtml`

**3.3.**

#### 38. REPLACE — lines 53–57 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_CostCenters.CostCenterDetail.cshtml`

FIND (lines 53–57, 5 lines):

```cshtml
                                id="btn-costcenter-delete"
                                data-id="@Model.Id"
                                data-url="/Admin/MultiStore/DeleteCostCenter">
                            <i class="fas fa-trash" aria-hidden="true"></i>
                            @T("Admin.Common.Delete")
```

REPLACE WITH (5 lines):

```cshtml
                                id="btn-costcenter-delete"
                                data-id="@Model.Id"
                                data-url="@Url.Action("DeleteCostCenter", "MultiStore")">
                            <i class="fas fa-trash" aria-hidden="true"></i>
                            @T("Admin.Common.Delete")
```

### _PackNames.PackNameDetail.cshtml

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_PackNames.PackNameDetail.cshtml`

**3.3.**

#### 39. REPLACE — lines 48–52 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/MultiStore/_PackNames.PackNameDetail.cshtml`

FIND (lines 48–52, 5 lines):

```cshtml
                                id="btn-packname-delete"
                                data-id="@Model.Id"
                                data-url="/Admin/MultiStore/DeletePackName"
                                data-confirm="@T("Plugins.CW.Core.Admin.PackNames.DeleteConfirmMessage")">
                            <i class="fas fa-trash" aria-hidden="true"></i>
```

REPLACE WITH (5 lines):

```cshtml
                                id="btn-packname-delete"
                                data-id="@Model.Id"
                                data-url="@Url.Action("DeletePackName", "MultiStore")"
                                data-confirm="@T("Plugins.CW.Core.Admin.PackNames.DeleteConfirmMessage")">
                            <i class="fas fa-trash" aria-hidden="true"></i>
```

### SubsidyVariationBuilder.cs

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Subsidies/SubsidyVariationBuilder.cs`

**3.15.** Four `Credit` columns used the builder default `AsDecimal()` = `DECIMAL(19, 5)`, while every other ledger money column is `(18, 4)`. They now declare `(18, 4)`.

This reaches **new installations only**. The project is pre-production, so there is no migration, and the dev database `testnop50` keeps `(19, 5)`, which truncates nothing. The same edit is made in the three history builders that follow.

#### 40. REPLACE — lines 17–21 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Subsidies/SubsidyVariationBuilder.cs`

FIND (lines 17–21, 5 lines):

```csharp
        table.WithColumn(nameof(SubsidyVariation.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyVariation.SubsidyPackNameId)).AsInt32().ForeignKey<SubsidyPackName>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyVariation.Credit)).AsDecimal().Nullable()
             .WithColumn(nameof(SubsidyVariation.DurationStart)).AsDate().Nullable()
             .WithColumn(nameof(SubsidyVariation.DurationEnd)).AsDate().Nullable()
```

REPLACE WITH (5 lines):

```csharp
        table.WithColumn(nameof(SubsidyVariation.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyVariation.SubsidyPackNameId)).AsInt32().ForeignKey<SubsidyPackName>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyVariation.Credit)).AsDecimal(18, 4).Nullable()
             .WithColumn(nameof(SubsidyVariation.DurationStart)).AsDate().Nullable()
             .WithColumn(nameof(SubsidyVariation.DurationEnd)).AsDate().Nullable()
```

### SubsidyVariationHistoryBuilder.cs

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Subsidies/SubsidyVariationHistoryBuilder.cs`

**3.15.**

#### 41. REPLACE — lines 17–21 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Subsidies/SubsidyVariationHistoryBuilder.cs`

FIND (lines 17–21, 5 lines):

```csharp
        table.WithColumn(nameof(SubsidyVariationHistory.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyVariationHistory.SubsidyVariationId)).AsInt32().ForeignKey<SubsidyVariation>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyVariationHistory.Credit)).AsDecimal().Nullable()
             .WithColumn(nameof(SubsidyVariationHistory.DurationStart)).AsDate().Nullable()
             .WithColumn(nameof(SubsidyVariationHistory.DurationEnd)).AsDate().Nullable()
```

REPLACE WITH (5 lines):

```csharp
        table.WithColumn(nameof(SubsidyVariationHistory.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyVariationHistory.SubsidyVariationId)).AsInt32().ForeignKey<SubsidyVariation>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyVariationHistory.Credit)).AsDecimal(18, 4).Nullable()
             .WithColumn(nameof(SubsidyVariationHistory.DurationStart)).AsDate().Nullable()
             .WithColumn(nameof(SubsidyVariationHistory.DurationEnd)).AsDate().Nullable()
```

### SubsidyTopupHistoryBuilder.cs

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Subsidies/SubsidyTopupHistoryBuilder.cs`

**3.15.**

#### 42. REPLACE — lines 17–21 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Subsidies/SubsidyTopupHistoryBuilder.cs`

FIND (lines 17–21, 5 lines):

```csharp
        table.WithColumn(nameof(SubsidyTopupHistory.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyTopupHistory.SubsidyTopupId)).AsInt32().ForeignKey<SubsidyTopup>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyTopupHistory.Credit)).AsDecimal().NotNullable()
             .WithColumn(nameof(SubsidyTopupHistory.Reason)).AsString().Nullable()
             .WithColumn(nameof(SubsidyTopupHistory.CreatorId)).AsInt32().NotNullable()
```

REPLACE WITH (5 lines):

```csharp
        table.WithColumn(nameof(SubsidyTopupHistory.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyTopupHistory.SubsidyTopupId)).AsInt32().ForeignKey<SubsidyTopup>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyTopupHistory.Credit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(SubsidyTopupHistory.Reason)).AsString().Nullable()
             .WithColumn(nameof(SubsidyTopupHistory.CreatorId)).AsInt32().NotNullable()
```

### ProductGroupTopupHistoryBuilder.cs

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Subsidies/ProductGroupTopupHistoryBuilder.cs`

**3.15.**

#### 43. REPLACE — lines 18–22 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Data/Mapping/Subsidies/ProductGroupTopupHistoryBuilder.cs`

FIND (lines 18–22, 5 lines):

```csharp
             .WithColumn(nameof(ProductGroupTopupHistory.ProductGroupTopupId)).AsInt32().ForeignKey<ProductGroupTopup>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.Quantity)).AsInt32().NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.Credit)).AsDecimal().NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.Reason)).AsString().Nullable()
             .WithColumn(nameof(ProductGroupTopupHistory.CreatorId)).AsInt32().NotNullable()
```

REPLACE WITH (5 lines):

```csharp
             .WithColumn(nameof(ProductGroupTopupHistory.ProductGroupTopupId)).AsInt32().ForeignKey<ProductGroupTopup>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.Quantity)).AsInt32().NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.Credit)).AsDecimal(18, 4).NotNullable()
             .WithColumn(nameof(ProductGroupTopupHistory.Reason)).AsString().Nullable()
             .WithColumn(nameof(ProductGroupTopupHistory.CreatorId)).AsInt32().NotNullable()
```

### CWCorePlugin.cs

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

The new resources: ten activity-log descriptions, next to their entity's existing ones, and the five order-card texts.

#### 44. REPLACE — lines 2176–2179 (1 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 2176–2179, 4 lines):

```csharp
            ["Plugins.CW.Core.ActivityLog.EditPackName"] = "Edited an packaging name ('{0}')",
            ["Plugins.CW.Core.ActivityLog.DeletePackName"] = "Deleted an packaging name ('{0}')",

            ["Plugins.CW.Core.ActivityLog.PackNameSelection.Started.Customer"] = "PackName selection by store owner (Email: {0}, ID = {1})",
```

REPLACE WITH (7 lines):

```csharp
            ["Plugins.CW.Core.ActivityLog.EditPackName"] = "Edited an packaging name ('{0}')",
            ["Plugins.CW.Core.ActivityLog.DeletePackName"] = "Deleted an packaging name ('{0}')",
            ["Plugins.CW.Core.ActivityLog.AddPackNameAddress"] = "Added an address to a packname ('{0}', address ID = {1})",
            ["Plugins.CW.Core.ActivityLog.EditPackNameAddress"] = "Edited an address of a packname ('{0}', address ID = {1})",
            ["Plugins.CW.Core.ActivityLog.DeletePackNameAddress"] = "Deleted an address of a packname ('{0}', address ID = {1})",

            ["Plugins.CW.Core.ActivityLog.PackNameSelection.Started.Customer"] = "PackName selection by store owner (Email: {0}, ID = {1})",
```

#### 45. REPLACE — lines 2167–2170 (2 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 2167–2170, 4 lines):

```csharp
            ["Plugins.CW.Core.ActivityLog.EditCostCenter"] = "Edited a cost centre (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.DeleteCostCenter"] = "Deleted a cost centre (ID = {0})",

            ["Plugins.CW.Core.ActivityLog.AddNewPackNameList"] = "Added a new packaging name list ('{0}')",
```

REPLACE WITH (7 lines):

```csharp
            ["Plugins.CW.Core.ActivityLog.EditCostCenter"] = "Edited a cost centre (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.DeleteCostCenter"] = "Deleted a cost centre (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.AddCostCenterAddress"] = "Added an address to a cost centre (cost centre ID = {0}, address ID = {1})",
            ["Plugins.CW.Core.ActivityLog.EditCostCenterAddress"] = "Edited an address of a cost centre (cost centre ID = {0}, address ID = {1})",
            ["Plugins.CW.Core.ActivityLog.DeleteCostCenterAddress"] = "Deleted an address of a cost centre (cost centre ID = {0}, address ID = {1})",

            ["Plugins.CW.Core.ActivityLog.AddNewPackNameList"] = "Added a new packaging name list ('{0}')",
```

#### 46. REPLACE — lines 2159–2162 (3 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 2159–2162, 4 lines):

```csharp
            ["Plugins.CW.Core.ActivityLog.EditBranch"] = "Edited a branch (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.DeleteBranch"] = "Deleted a branch (ID = {0})",

            ["Plugins.CW.Core.ActivityLog.AddNewDepartment"] = "Added a new department (ID = {0})",
```

REPLACE WITH (8 lines):

```csharp
            ["Plugins.CW.Core.ActivityLog.EditBranch"] = "Edited a branch (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.DeleteBranch"] = "Deleted a branch (ID = {0})",
            ["Plugins.CW.Core.ActivityLog.AddBranchAddress"] = "Added an address to a branch (branch ID = {0}, address ID = {1})",
            ["Plugins.CW.Core.ActivityLog.EditBranchAddress"] = "Edited an address of a branch (branch ID = {0}, address ID = {1})",
            ["Plugins.CW.Core.ActivityLog.DeleteBranchAddress"] = "Deleted an address of a branch (branch ID = {0}, address ID = {1})",
            ["Plugins.CW.Core.ActivityLog.SetDefaultBranchAddress"] = "Set the default address of a branch (branch ID = {0}, address ID = {1})",

            ["Plugins.CW.Core.ActivityLog.AddNewDepartment"] = "Added a new department (ID = {0})",
```

#### 47. REPLACE — lines 1528–1531 (4 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 1528–1531, 4 lines):

```csharp
            ["Plugins.CW.Core.Admin.Orders.Products.ShippingCost"] = "Shipping Cost",
            ["Plugins.CW.Core.Admin.Orders.Field.Phone"] = "Phone",

            ["Plugins.CW.Core.Admin.Orders.ShippingDestinations"] = "Shipping destinations",
```

REPLACE WITH (9 lines):

```csharp
            ["Plugins.CW.Core.Admin.Orders.Products.ShippingCost"] = "Shipping Cost",
            ["Plugins.CW.Core.Admin.Orders.Field.Phone"] = "Phone",
            ["Plugins.CW.Core.Admin.Orders.Field.Branch"] = "Branch",
            ["Plugins.CW.Core.Admin.Orders.Field.Department"] = "Department",
            ["Plugins.CW.Core.Admin.Orders.Field.CostCenter"] = "Cost Centre",
            ["Plugins.CW.Core.Admin.Orders.SharedShipping"] = "Shared",
            ["Plugins.CW.Core.Admin.Orders.SharedShippingWith"] = "Shipping shared with: {0}",

            ["Plugins.CW.Core.Admin.Orders.ShippingDestinations"] = "Shipping destinations",
```

### plugin.json

`Plugins/Nop.Plugin.CW.Core/plugin.json`

The DATA version goes up so the update installs the new resources.

#### 48. REPLACE — lines 3–7 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/plugin.json`

FIND (lines 3–7, 5 lines):

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_12_DATA_0.1",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```

REPLACE WITH (5 lines):

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_12_DATA_0.2",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```
## Tests (already in `Tests/CW.Tests`, by Claude)

These build once the spec is applied:

| File | Change | What it pins |
|---|---|---|
| `Core/Services/Events/EventConsumerContractTests.cs` (new) | 1 case | `EventConsumer` no longer consumes `EntityUpdatedEvent<ShoppingCartItem>` |
| `Core/Controllers/BranchAdminGuardsTests.cs` | +3 cases (14) | Deleting a branch address deletes it and logs `EditBranch` / `DeleteBranchAddress` against the branch. Setting the default logs `SetDefaultBranchAddress`. Another owner's address is neither deleted nor logged. The localisation mock now returns the key, as `string.Format` needs |
| `Core/Controllers/PackNameControllerTests.cs` | set-up | The manager's store comes from `IMultiStoreService.GetManagerStoreIdAsync`, not from `GetBranchByPackNameIdAsync`. The unused `Domain.Branches` using is removed |
| `Core/Services/MultiStores/ManagerStoreTests.cs` | +1 case (5) | A list whose branch is deleted gives no store; the other cases give the list's branch a live branch. The method is now public; the reflection call reaches it as before |

The builders (3.15) cannot be run in a unit test: they resolve table names through nop's engine, as `SchemaMigrationTests` notes.

## Build and checks

1. Build the solution. Then open, on the dev site, each view this spec touches: the five Manage Store detail panels and an admin packname order.
2. Run `dotnet test Tests/CW.Tests --filter "FullyQualifiedName~CW.Tests.Core"`: everything passes, `NopStartupTests` included. It checks that `EventConsumer`'s constructor still resolves.
3. Run the plugin update. `plugin.json` should be `2026_11_12_DATA_0.2`, and the fifteen resources should be installed.
4. The independent review result is in the last section.

## Test plan (dev site `https://localhost:59579`, database `testnop50`)

Side effects:
- T3 adds, edits, sets as default and deletes one test address on a test branch and on a cost centre.
- T5 saves one storefront packname edit with unchanged values.
- Nothing else is deleted.

| # | Item | Steps | Expected |
|---|---|---|---|
| T1 | 3.13 | Manage Store › a branch with two lists: the list cards; *Set default* on the other list and back. Admin packname form: the list picker. *Add packname* on the branch: its list dropdown (`GetListsByBranchId`) | Lists in name order, the default pre-selected, as before; set-default works |
| T2 | 3.3 | Delete button on a test department, a cost centre (from the branch and from the cost centres tab) and a packname (from the branch and from the packnames tab): read its `data-url`; cancel the confirmation | Each button has `/Admin/MultiStore/Delete…` from `Url.Action`; nothing is deleted |
| T3 | 3.20 | Manage Store: add an address to a branch, edit it, set it as default, delete it; the same for a cost centre (add, edit, delete). Standalone *Branches* page: add, edit, set default, delete. Then *Customers › Activity log* | One row per write, under *Edit a branch* / *Edit a cost centre*, with the new description and the ids |
| T4 | 3.20 | Packname address popup on Manage Store: add, edit, delete one address | Three rows under *Edit a packaging name* |
| T5 | 3.20, two routes | Impersonate a manager. The selector's *Add* / *Excel* buttons show as before. Edit one packname and save unchanged | Buttons as before; one *EditPackName* row |
| T6 | 3.4 | Admin › an order with packname lines: the packname card. On a shared destination, the *Shared* badge, its tooltip and the "Shipping shared with" line. Then rename a test packname on that order's destination to `<b>x</b>` (in admin), reload, and rename it back | The texts as before, now from resources. The name shows literally as `<b>x</b>`, not bold, in the line and the tooltip |
| T7 | 3.12 | Storefront: change a quantity in the packname checkout and in the flyout cart | Works as before; no error in the log |
| T8 | all | Open each page above once as a user | Nothing overflowing, no console error from the changed views |

## After the part

On approval:
1. Remove 3.3, 3.4, 3.9, 3.12, 3.13, 3.15 and 3.20 from `Subsidy_PackName_Open_Items.md`.
2. `MultiStore_Management_Reference.md` §7 (around line 468): a manager's store now comes from `IMultiStoreService.GetManagerStoreIdAsync` (list's store, refused on a deleted packname, list or branch). It is public and on the interface.
3. `subsidy-system.md` rule 1b: the four `Credit` columns are `(18, 4)` on a new installation; `testnop50` keeps `(19, 5)`.
4. Mark the rounds plan, and move this spec to `Deprecated/`.

Then R2-C.

## Independent review (2026-11-12)

A sub-agent that did not write this spec applied all the changes to a scratch copy and read every caller.

- **Fixed before it reached you:**
  - **3.4** would have printed packname names unencoded on the admin order card: `@T(key, args)` returns an `HtmlString`. It now uses `string.Format(T(key).Text, …)`, and T6 checks it.
  - **Two routes** would have let in a manager whose list's branch is deleted, which the branch route refused. The new method refuses that case too, and the branch cache clear is back.
  - The *Shared* badge and tooltip in the same view are now resources too.
- **Recorded:**
  - the sixth literal URL, which goes to R2-C;
  - the branch delete that leaves lists live, now Open Item 4.19;
  - the document updates, now numbered steps above;
  - the resource dictionary as a deliberate lesson #47 exception.
- **Checked and found correct:**
  - every FIND at its lines;
  - every member and signature used;
  - that nothing else needs a removed member;
  - the 3.13 order and fallbacks;
  - the activity types, placeholders and log timing;
  - that `(19, 5)` is used nowhere in SQL or tests;
  - the test mocks and reflection.

## Test run 1 (2026-11-12, dev site, plugin `2026_11_12_DATA_0.2`)

| # | Result | What was done |
|---|---|---|
| T1 | **Pass** | Branch 3's lists ("Test Branch 1.1 L" id 7, "Test Branch 1.3" id 9): `GetListsByBranchId` lists by name with the default pre-selected; *Set default* on 9 and back on 7 moves the selection (list 7 is now explicitly the default). Admin packname form's list picker: "Test 1*, Test 2" |
| T2 | **Pass** | The five delete buttons carry `/Admin/MultiStore/DeleteDepartment` (department 2), `DeleteCostCenter` (cost centre 2, both panels), `DeletePackName` (packname 1, both panels). Nothing deleted |
| T3 | **Pass** | Branch 3 on Manage Store: address 1056 added, edited, set default, default back to 22, deleted. Cost centre 2: address 1057 added, edited, deleted. Standalone *Branches* page: 1058 created, edited, set default, default back to 22, deleted. One activity row each, under *Edit a branch* / *Edit a cost centre*, with the new descriptions and ids. Branch 3's default is 22 again |
| T4 | **Pass** | Packname 1 (Steve Gates): address 1059 added, edited, deleted; three rows under *Edit an packaging name* |
| T5 | **Pass** | Impersonating Steve: the selector shows the Excel import link as before. Storefront *Edit packname* (Brenda Lindgren) saved unchanged: `success`, one *Edit an packaging name* row by steve_gates |
| T6 | **Pass** | Packname cards on orders 9 and 1011: "Branch:", "Department:", "Cost Centre:" from resources. A test order **1013** was placed (Check / Money order, Steve and Brenda to one address and method): both cards show the *Shared* badge with tooltip "Shipping shared with: …" and the line "Shipping shared with: …". The names on an order are its own snapshot, so renaming a packname cannot put `<b>x</b>` on an existing order's card; the encoding rests on `string.Format` returning a `string`, which Razor encodes |
| T7 | **Pass** | Steve's packname checkout: line 28 quantity 2, then back to 1; both saved |
| T8 | **Pass** | Pages above opened; no console error from the changed views (only Visual Studio's browser-link sockets) |

Left on the dev site: order **1013** (Pending, Check / Money order) with Steve's and Brenda's lines; list 7 is branch 3's explicit default.
