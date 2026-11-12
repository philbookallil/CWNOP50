# Round 2, part A — guards

**Date:** 2026-11-12. **Owner:** Eduardo. **Status:** applied, tested and approved 2026-11-12 (closed). **Round plan:** `NOP41_Gap_Rounds_Plan.md` (Round 2).

**What this part does.** It closes the six guard items of Round 2. Each was re-checked against the code on 2026-11-12, and Open Items has the re-check under each:

| Item | What changes | Changes |
|---|---|---|
| **4.13** | A manager's store comes from their list's `StoreId`. A deleted packname or list gives no store. The branch join, and the branch cache clear that went with it, are removed | 1–2 |
| **4.7** | Branch addresses are reached only through their branch: the standalone *Branches* page (edit GET + POST) and the Manage Store popups (branch and cost-centre GET). A deleted branch's addresses are not opened, created or saved (the delete and set-default actions are left as they are — see below). `SearchBranches` is checked for Branch **or** MultiStore | 3–10 |
| **4.5** | The Manage Store branch panel opens only a live branch (or, for a new child branch, a live parent) of its own store. Anything else gets a 404, which the panel shows as "Failed to load" | 11 |
| **4.12** | The customer page refuses a branch it could not have offered: missing, deleted, inactive or of another store. This applies on create, and on edit when the branch changes | 12–17 |
| **4.17** | When the packname subsidies card fails to load, it says so and offers *Retry* | 18 |
| **4.10** | **By-product checkout view:** each row shows its packname's line read-only (decided 2026-11-12). The editable box appears only in the by-packname view | 19–23 |
| — | DATA version bump (one new resource) | 24 |

**Decisions this spec rests on (2026-11-12, Eduardo):**
- 4.10 is read-only per row.
- No plugin code is edited by Claude.
- R2-C (the `alert()` / `@T` sweeps) is a part of its own.

Three choices were made here without asking, because each follows an existing rule:
- `SearchBranches` takes either permission, because its only caller is Manage Store.
- A refused branch on the customer page uses the picker's own rule (`MultiStore_Management_Reference.md` §7c).
- A failed subsidies card reuses the existing Manage Store "Failed to load / Retry" texts.

**Dead code this part removes.** The rule is lesson #56: a change that kills something removes it in the same step.
- Change 2: the branch consumer's manager-store cache clear.
- Change 21: the by-product view's toggle script and address-dialog div.
- Change 22: the position logic in `panelKey`.

Nothing else becomes dead.

**Not in this part** (found by the review; none needs a guard today because only a hand-made request reaches it):
- The Manage Store branch search still lists inactive branches, though the save refuses them with a message. Filtering them out would change what `GetBranchesByStoreIdAsync` returns for its paging.
- Deleting a branch address, or setting it as the default, still works for a deleted branch on both pages (`MultiStoreController.BranchAddressDelete` / `BranchAddressSetDefault`, `BranchController.AddressDelete` / `AddressSetDefault`). Each already checks that the address belongs to the branch.
- When the customer page refuses an inactive branch, the redisplayed picker still lists it, because it keeps the posted value (`keepId`). Only a hand-made value can be refused this way.
- The shipping warnings a by-product row now prints are the factory's own English strings (`PackNameCheckoutModelFactory.cs:170-196`), which were never localised.

## How to apply

Labels:
- **REPLACE:** the FIND block, at the lines given, becomes the REPLACE WITH block.
- **WHOLE FILE:** the file's entire content becomes the block.
- **NEW FILE:** create the file with the block.

Each change names its file under its heading. Within a file the blocks are listed **bottom to top**. The line numbers of each FIND are those of the file as it stands when that block is reached, so applying them in the order given never moves the next one. Every FIND and REPLACE starts and ends on a whole line.

Dependencies: none between changes, except that the project compiles only once all of them are applied (changes 12–17 add a constructor parameter, which nop's DI fills by itself).

## Changes
### MultiStoreService.cs

`Plugins/Nop.Plugin.CW.Core/Services/MultiStores/MultiStoreService.cs`

**4.13.** A manager's store comes from the list their packname is in (`PackNameList.StoreId`). The list already holds the store, so the branch join is no longer needed. A deleted packname or a deleted list now gives no store. The method gets the doc comment it was missing. Given as one block covering the whole method.

#### 1. REPLACE — lines 90–106 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/MultiStores/MultiStoreService.cs`

FIND (lines 90–106, 17 lines):

```csharp
    #region Utilities

    protected virtual async Task<int?> GetManagerStoreIdAsync(int managerId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            BranchServicesDefaults.ManagerStoreIdCacheKey, managerId);

        return await _staticCacheManager.GetAsync(key, async () =>
            await (
                from pn in _packNameRepository.Table
                join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                join b in _branchRepository.Table on pnl.BranchId equals b.Id
                where pn.Id == managerId
                select (int?)b.StoreId
            ).FirstOrDefaultAsync()
        );
    }
```

REPLACE WITH (24 lines):

```csharp
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
```

### BranchCacheEventConsumer.cs

`Plugins/Nop.Plugin.CW.Core/Services/Branches/Caching/BranchCacheEventConsumer.cs`

**4.13, dead line.** Once 4.13 is applied, a branch change can no longer change a manager's store, so clearing the manager-store cache on a branch change does nothing. The packname and list consumers still clear it.

#### 2. REPLACE — lines 39–43 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Services/Branches/Caching/BranchCacheEventConsumer.cs`

FIND (lines 39–43, 5 lines):

```csharp
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(BranchServicesDefaults.ManagerStoreIdCacheKeyAllPrefix);
        await RemoveAsync(BranchServicesDefaults.DefaultShippingMethodsByBranchCacheKey, entity.Id);
        await RemoveByPrefixAsync(BranchServicesDefaults.BranchByCustomerIdPrefix);
```

REPLACE WITH (4 lines):

```csharp
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListsByManagerCacheKeyAllPrefix);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKeyAllPrefix);
        await RemoveAsync(BranchServicesDefaults.DefaultShippingMethodsByBranchCacheKey, entity.Id);
        await RemoveByPrefixAsync(BranchServicesDefaults.BranchByCustomerIdPrefix);
```

### BranchController.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

**4.7.** The standalone *Branches* page.
- `SearchBranches` gets its permission check. It accepts Branch **or** MultiStore, because its only caller is the packname panel on Manage Store.
- Both `AddressEdit` actions now look the address up **through the branch**. The POST could otherwise overwrite any address by id, a customer's included.
- A deleted branch's addresses are not opened, created or saved.
- The Spanish comment becomes English.

#### 3. REPLACE — lines 470–478 (1 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

FIND (lines 470–478, 9 lines):

```csharp
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.EntityId);
        if (branch == null)
            return RedirectToAction("List");

        //try to get an address with the specified id
        var address = await _addressService.GetAddressByIdAsync(model.Address.Id);
        if (address == null)
            return RedirectToAction("Edit", new { id = branch.Id });
```

REPLACE WITH (9 lines):

```csharp
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.EntityId);
        if (branch == null || branch.Deleted)
            return RedirectToAction("List");

        //through the branch: by id alone the save below would overwrite any row of the shared Address table
        var address = await _branchService.GetBranchAddressAsync(branch.Id, model.Address.Id);
        if (address == null)
            return RedirectToAction("Edit", new { id = branch.Id });
```

#### 4. REPLACE — lines 450–458 (2 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

FIND (lines 450–458, 9 lines):

```csharp
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(branchId);
        if (branch == null)
            return RedirectToAction("List");

        //try to get an address with the specified id
        var address = await _addressService.GetAddressByIdAsync(addressId);
        if (address == null)
            return RedirectToAction("Edit", new { id = branch.Id });
```

REPLACE WITH (9 lines):

```csharp
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(branchId);
        if (branch == null || branch.Deleted)
            return RedirectToAction("List");

        //through the branch: by id alone any row of the shared Address table would load
        var address = await _branchService.GetBranchAddressAsync(branch.Id, addressId);
        if (address == null)
            return RedirectToAction("Edit", new { id = branch.Id });
```

#### 5. REPLACE — lines 405–411 (3 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

FIND (lines 405–411, 7 lines):

```csharp
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.EntityId);
        if (branch == null)
            return RedirectToAction("List");

        //custom address attributes
```

REPLACE WITH (7 lines):

```csharp
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.EntityId);
        if (branch == null || branch.Deleted)
            return RedirectToAction("List");

        //custom address attributes
```

#### 6. REPLACE — lines 390–396 (4 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

FIND (lines 390–396, 7 lines):

```csharp
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(branchId);
        if (branch == null)
            return RedirectToAction("List");

        //prepare model
```

REPLACE WITH (7 lines):

```csharp
    {
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(branchId);
        if (branch == null || branch.Deleted)
            return RedirectToAction("List");

        //prepare model
```

#### 7. REPLACE — lines 144–153 (5 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/BranchController.cs`

FIND (lines 144–153, 10 lines):

```csharp
    }

    [HttpGet]
    public async Task<IActionResult> SearchBranches(int storeId, string term, int page = 1)
    {
        const int pageSize = 20;

        // Filtra tu entidad según el término buscado
        var branches = await _branchService.GetBranchesByStoreIdAsync(
            storeId: storeId,
```

REPLACE WITH (11 lines):

```csharp
    }

    [HttpGet]
    [CheckPermission([CWPermissionConfigManager.ADMIN_ACCESS_BRANCH, CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE])]
    public async Task<IActionResult> SearchBranches(int storeId, string term, int page = 1)
    {
        const int pageSize = 20;

        //the store's branches whose name contains the term (deleted ones left out)
        var branches = await _branchService.GetBranchesByStoreIdAsync(
            storeId: storeId,
```

### MultiStoreController.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

**4.5.** `BranchDetail` opens only a live branch of the store, and only a live parent of the store for a new child branch. Anything else returns 404, which the panel shows as "Failed to load".

**4.7 (Manage Store side).** The address popups for a branch and a cost centre (GET) now load the address through its owner, the way the packname popup already does, and neither the branch popup's GET nor its save accepts a deleted branch.

#### 8. REPLACE — lines 2413–2418 (1 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 2413–2418, 6 lines):

```csharp
        if (costCenter == null)
            return AccessDeniedView();

        var address = addressId.HasValue ? await _addressService.GetAddressByIdAsync(addressId.Value) : null;

        //prepare model
```

REPLACE WITH (12 lines):

```csharp
        if (costCenter == null)
            return AccessDeniedView();

        //through the owner: by id alone any row of the shared Address table would load into the popup
        var address = addressId.HasValue
            ? await _multiStoreService.GetCostCenterAddressAsync(costCenter.Id, addressId.Value)
            : null;

        if (addressId.HasValue && address == null)
            return AccessDeniedView();

        //prepare model
```

#### 9. REPLACE — lines 799–804 (2 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 799–804, 6 lines):

```csharp
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.EntityId);
        if (branch == null)
            return AccessDeniedView();

        //THROUGH THE OWNER, never by id alone. GetBranchAddressAsync scopes the lookup to this
```

REPLACE WITH (6 lines):

```csharp
        //try to get a branch with the specified id
        var branch = await _branchService.GetBranchByIdAsync(model.EntityId);
        if (branch == null || branch.Deleted)
            return AccessDeniedView();

        //THROUGH THE OWNER, never by id alone. GetBranchAddressAsync scopes the lookup to this
```

#### 10. REPLACE — lines 781–788 (3 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 781–788, 8 lines):

```csharp
        var branch = await _branchService.GetBranchByIdAsync(branchId);

        if (branch == null)
            return AccessDeniedView();

        var address = addressId.HasValue ? await _addressService.GetAddressByIdAsync(addressId.Value) : null;

        //prepare model
```

REPLACE WITH (14 lines):

```csharp
        var branch = await _branchService.GetBranchByIdAsync(branchId);

        if (branch == null || branch.Deleted)
            return AccessDeniedView();

        //through the owner: by id alone any row of the shared Address table would load into the popup
        var address = addressId.HasValue
            ? await _branchService.GetBranchAddressAsync(branch.Id, addressId.Value)
            : null;

        if (addressId.HasValue && address == null)
            return AccessDeniedView();

        //prepare model
```

#### 11. REPLACE — lines 620–623 (4 of 4 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/MultiStoreController.cs`

FIND (lines 620–623, 4 lines):

```csharp
    public async Task<IActionResult> BranchDetail(int storeId, int? branchId, int? parentBranchId)
    {
        var model = await _branchModelFactory.PrepareBranchModelAsync(storeId, branchId, parentBranchId);
        return PartialView("_Branches.BranchDetail", model);
```

REPLACE WITH (14 lines):

```csharp
    public async Task<IActionResult> BranchDetail(int storeId, int? branchId, int? parentBranchId)
    {
        //the branch, and the parent of a new one, must be live branches of this store: the panel offers the store's
        //shipping methods and the save filters by them, so another store's branch would be edited against the wrong store
        var branch = branchId.HasValue ? await _branchService.GetBranchByIdAsync(branchId.Value) : null;
        if (branchId.HasValue && (branch == null || branch.Deleted || branch.StoreId != storeId))
            return NotFound();

        var parentBranch = parentBranchId.HasValue ? await _branchService.GetBranchByIdAsync(parentBranchId.Value) : null;
        if (parentBranchId.HasValue && (parentBranch == null || parentBranch.Deleted || parentBranch.StoreId != storeId))
            return NotFound();

        var model = await _branchModelFactory.PrepareBranchModelAsync(storeId, branchId, parentBranchId);
        return PartialView("_Branches.BranchDetail", model);
```

### ExtendedCustomerController.cs

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ExtendedCustomerController.cs`

**4.12.** The customer page saves only a branch it could have offered:
- the branch must be active and not deleted;
- it must be in the customer's store: the current store for a new customer, the registered store for an existing one, the same store the picker uses.

The controller takes `IBranchService`. On an edit the check runs only when the branch changes. The packname check runs after it, and only when the branch passed.

The constructor is given whole, together with the new helper that follows it.

#### 12. REPLACE — lines 620–625 (1 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ExtendedCustomerController.cs`

FIND (lines 620–625, 6 lines):

```csharp
        }

        //a customer's packname follows them to another branch: a change it cannot follow is refused
        if (model.BranchId != ((await _customerService.GetCustomerBranchByIdAsync(customer.Id))?.BranchId ?? 0))
        {
            var branchChangeError = await _packNameProvisioningService.CheckCustomerBranchChangeAsync(customer, model.BranchId);
```

REPLACE WITH (8 lines):

```csharp
        }

        //a new branch must be one the page offers (the branches of the customer's store), and the customer's packname
        //follows the customer into it: a change it cannot follow is refused
        if (model.BranchId != ((await _customerService.GetCustomerBranchByIdAsync(customer.Id))?.BranchId ?? 0)
            && await ValidateBranchAsync(model.BranchId, customer.RegisteredInStoreId))
        {
            var branchChangeError = await _packNameProvisioningService.CheckCustomerBranchChangeAsync(customer, model.BranchId);
```

#### 13. REPLACE — lines 389–392 (2 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ExtendedCustomerController.cs`

FIND (lines 389–392, 4 lines):

```csharp
        }

        //validate customer price lists
        var allCustomerPriceLists = await _priceListService.GetAllPriceListsAsync();
```

REPLACE WITH (7 lines):

```csharp
        }

        //the branch must be one the page offers: the page lists the current store's branches for a new customer
        await ValidateBranchAsync(model.BranchId, (await _storeContext.GetCurrentStoreAsync()).Id);

        //validate customer price lists
        var allCustomerPriceLists = await _priceListService.GetAllPriceListsAsync();
```

#### 14. REPLACE — lines 72–129 (3 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ExtendedCustomerController.cs`

FIND (lines 72–129, 58 lines):

```csharp
    public ExtendedCustomerController(CustomerSettings customerSettings,
        DateTimeSettings dateTimeSettings,
        GdprSettings gdprSettings,
        IAttributeParser<CustomerAttribute, CustomerAttributeValue> customerAttributeParser,
        IAttributeService<CustomerAttribute, CustomerAttributeValue> customerAttributeService,
        ICustomerActivityService customerActivityService,
        ICustomerOverrideModelFactory customerModelFactory,
        ICustomerRegistrationService customerRegistrationService,
        ICustomerOverrideService customerService,
        IEventPublisher eventPublisher,
        IGdprService gdprService,
        IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        INewsLetterSubscriptionService newsLetterSubscriptionService,
        INotificationService notificationService,
        IPermissionService permissionService,
        IPriceListService priceListService,
        IStoreContext storeContext,
        ITaxService taxService,
        IWorkContext workContext,
        IWorkflowMessageService workflowMessageService,
        OtpSettings otpSettings,
        TaxSettings taxSettings,
        IPackNameLandingService packNameLandingService,
        IPackNameProvisioningService packNameProvisioningService)
    {
        _customerSettings = customerSettings;
        _dateTimeSettings = dateTimeSettings;
        _gdprSettings = gdprSettings;
        _customerAttributeParser = customerAttributeParser;
        _customerAttributeService = customerAttributeService;
        _customerActivityService = customerActivityService;
        _customerModelFactory = customerModelFactory;
        _customerRegistrationService = customerRegistrationService;
        _customerService = customerService;
        _eventPublisher = eventPublisher;
        _gdprService = gdprService;
        _genericAttributeService = genericAttributeService;
        _localizationService = localizationService;
        _newsLetterSubscriptionService = newsLetterSubscriptionService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _priceListService = priceListService;
        _storeContext = storeContext;
        _taxService = taxService;
        _workContext = workContext;
        _workflowMessageService = workflowMessageService;
        _otpSettings = otpSettings;
        _taxSettings = taxSettings;
        _packNameLandingService = packNameLandingService;
        _packNameProvisioningService = packNameProvisioningService;
    }

    #endregion

    #region Utilities

    protected virtual async Task<string> ValidateCustomerRolesAsync(IList<CustomerRole> customerRoles, IList<CustomerRole> existingCustomerRoles)
```

REPLACE WITH (82 lines):

```csharp
    public ExtendedCustomerController(CustomerSettings customerSettings,
        DateTimeSettings dateTimeSettings,
        GdprSettings gdprSettings,
        IAttributeParser<CustomerAttribute, CustomerAttributeValue> customerAttributeParser,
        IAttributeService<CustomerAttribute, CustomerAttributeValue> customerAttributeService,
        ICustomerActivityService customerActivityService,
        ICustomerOverrideModelFactory customerModelFactory,
        ICustomerRegistrationService customerRegistrationService,
        ICustomerOverrideService customerService,
        IEventPublisher eventPublisher,
        IGdprService gdprService,
        IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        INewsLetterSubscriptionService newsLetterSubscriptionService,
        INotificationService notificationService,
        IPermissionService permissionService,
        IPriceListService priceListService,
        IStoreContext storeContext,
        ITaxService taxService,
        IWorkContext workContext,
        IWorkflowMessageService workflowMessageService,
        OtpSettings otpSettings,
        TaxSettings taxSettings,
        IPackNameLandingService packNameLandingService,
        IPackNameProvisioningService packNameProvisioningService,
        IBranchService branchService)
    {
        _customerSettings = customerSettings;
        _dateTimeSettings = dateTimeSettings;
        _gdprSettings = gdprSettings;
        _customerAttributeParser = customerAttributeParser;
        _customerAttributeService = customerAttributeService;
        _customerActivityService = customerActivityService;
        _customerModelFactory = customerModelFactory;
        _customerRegistrationService = customerRegistrationService;
        _customerService = customerService;
        _eventPublisher = eventPublisher;
        _gdprService = gdprService;
        _genericAttributeService = genericAttributeService;
        _localizationService = localizationService;
        _newsLetterSubscriptionService = newsLetterSubscriptionService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _priceListService = priceListService;
        _storeContext = storeContext;
        _taxService = taxService;
        _workContext = workContext;
        _workflowMessageService = workflowMessageService;
        _otpSettings = otpSettings;
        _taxSettings = taxSettings;
        _packNameLandingService = packNameLandingService;
        _packNameProvisioningService = packNameProvisioningService;
        _branchService = branchService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Adds a model error unless the branch is one the customer page offers: an active, not deleted branch of the store
    /// </summary>
    /// <param name="branchId">The posted branch identifier; 0 for no branch, which is always accepted</param>
    /// <param name="storeId">The store whose branches the page offers</param>
    /// <returns>A task that represents the asynchronous operation; the task result is true when the branch is accepted</returns>
    protected virtual async Task<bool> ValidateBranchAsync(int branchId, int storeId)
    {
        if (branchId == 0)
            return true;

        var branch = await _branchService.GetBranchByIdAsync(branchId);

        if (branch != null && !branch.Deleted && branch.Active && branch.StoreId == storeId)
            return true;

        ModelState.AddModelError(nameof(ExtendedCustomerModel.BranchId),
            await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.NotAvailable"));

        return false;
    }

    protected virtual async Task<string> ValidateCustomerRolesAsync(IList<CustomerRole> customerRoles, IList<CustomerRole> existingCustomerRoles)
```

#### 15. REPLACE — lines 64–68 (4 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ExtendedCustomerController.cs`

FIND (lines 64–68, 5 lines):

```csharp
    protected readonly IPackNameLandingService _packNameLandingService;
    protected readonly IPackNameProvisioningService _packNameProvisioningService;
    private static readonly char[] _separator = [','];

    #endregion
```

REPLACE WITH (6 lines):

```csharp
    protected readonly IPackNameLandingService _packNameLandingService;
    protected readonly IPackNameProvisioningService _packNameProvisioningService;
    protected readonly IBranchService _branchService;
    private static readonly char[] _separator = [','];

    #endregion
```

#### 16. REPLACE — lines 12–15 (5 of 5 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Controllers/ExtendedCustomerController.cs`

FIND (lines 12–15, 4 lines):

```csharp
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.PackNames;
```

REPLACE WITH (5 lines):

```csharp
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.PackNames;
```

### CWCorePlugin.cs

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

**4.12.** The message the customer page shows next to a refused branch.

#### 17. REPLACE — lines 1171–1174 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`

FIND (lines 1171–1174, 4 lines):

```csharp
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId"] = "Branch",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.SelectBranch"] = "Select Branch",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameNeedsBranch"] = "The customer's packname {0} is in a branch, so the customer needs a branch too. Delete the packname or unlink it from the customer first.",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameOtherStore"] = "The customer's packname {0} belongs to another store, so it cannot follow the customer to branch {1}.",
```

REPLACE WITH (5 lines):

```csharp
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId"] = "Branch",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.SelectBranch"] = "Select Branch",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.NotAvailable"] = "This branch cannot be chosen: choose an active branch of the customer's store.",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameNeedsBranch"] = "The customer's packname {0} is in a branch, so the customer needs a branch too. Delete the packname or unlink it from the customer first.",
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameOtherStore"] = "The customer's packname {0} belongs to another store, so it cannot follow the customer to branch {1}.",
```

### Default.cshtml

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Shared/Components/PackNameSubsidies/Default.cshtml`

**4.17.** When the subsidies card fails to load, it says so and offers *Retry*, instead of staying on "Loading...". It reuses the existing `Common.FailedToLoad` / `Common.Retry` / `Common.Loading` resources. The labels reach the script through the config div. Given whole: the change touches the config div and the script in three places.

#### 18. WHOLE FILE — replace the entire content of the file with this

`Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Shared/Components/PackNameSubsidies/Default.cshtml`

(112 lines)

```cshtml
@model PackNameSubsidiesPanelModel

<div class="card-body">
    <div id="@Model.Prefix-pn-subsidies-@Model.PackNameId" data-packname-id="@Model.PackNameId" class="packname-subsidies-container">
        <div class="subsidy-loading-message">@T("Plugins.CW.Core.Admin.StoreNav.Common.Loading")</div>
    </div>
</div>

@* EVERY RAZOR VALUE THE SCRIPT NEEDS ARRIVES THROUGH HERE, so the script is plain JavaScript. *@
<div class="pns-panel-config"
     data-container-id="@Model.Prefix-pn-subsidies-@Model.PackNameId"
     data-packname-id="@Model.PackNameId"
     data-prefix="@Model.Prefix"
     data-load-url="@Url.Action("PackNameSubsidies", "Subsidy")"
     data-edit-label="@T("Admin.Common.Edit")"
     data-loading-label="@T("Plugins.CW.Core.Admin.StoreNav.Common.Loading")"
     data-load-error="@T("Plugins.CW.Core.Admin.StoreNav.Common.FailedToLoad")"
     data-retry-label="@T("Plugins.CW.Core.Admin.StoreNav.Common.Retry")"
     style="display: none;"></div>

<script>
    //THE PANEL IS SELF-CONTAINED. It is hosted by the customer page as well as the store page, and
    //the customer page has no Admin.StoreNav, so the two DataTables render callbacks live in a
    //namespace of their own. The namespace is declared once however many panels a page renders.
    window.CW = window.CW || {};
    CW.PackNameSubsidies = CW.PackNameSubsidies || (function () {
        var editLabel = $('.pns-panel-config').last().data('editLabel');

        var render = {
            productGroupIcon: function (data, type, row) {
                return data
                    ? '<svg class="pg-icon" style="color:' + (row.Colour ? row.Colour : '#000000') + '" aria-hidden="true">'
                        + '<use href="/Plugins/CW.Core/Content/icons/pg-icons.svg#' + data + '"/></svg>'
                    : '';
            },

            //DATA ATTRIBUTES ONLY: the delegated handler below builds the popup URL. The refresh
            //button's id is the panel prefix plus the subsidy id, which is how the popup finds it.
            productGroupTopupEdit: function (data, type, row) {
                return '<button type="button" class="btn btn-default btn-pns-pg-topup"'
                    + ' data-subsidy-pack-name-id="' + row.SubsidyPackNameId + '"'
                    + ' data-product-group-id="' + row.ProductGroupId + '"'
                    + ' data-subsidy-id="' + row.SubsidyId + '">'
                    + '<i class="fas fa-pencil-alt"></i>' + editLabel + '</button>';
            }
        };

        return { render: render };
    })();

    (function () {
        var $cfg = $('.pns-panel-config').last();
        var $container = $('#' + $cfg.data('containerId'));
        var packNameId = $cfg.data('packnameId');
        var prefix = $cfg.data('prefix');
        var loadUrl = $cfg.data('loadUrl');
        var loadingLabel = $cfg.data('loadingLabel');
        var loadError = $cfg.data('loadError');
        var retryLabel = $cfg.data('retryLabel');

        //a failed load says so and offers a retry, rather than leaving the card on its loading message
        function loadPanel() {
            $.get(loadUrl, { packNameId: packNameId, prefix: prefix })
                .done(function (html) { $container.html(html); })
                .fail(function () {
                    $container.html($('<div class="subsidy-loading-message"></div>')
                        .text(loadError + ' ')
                        .append($('<a href="#" class="pns-retry"></a>').text(retryLabel)));
                });
        }

        //THE CONTAINER OUTLIVES THE PARTIAL: every reload swaps its innerHTML, so the handlers are
        //attached once, here, and removed first in case this view is rendered again for the same
        //packname.
        $container.off('.pns');

        // ── Load failed: try again ──
        $container.on('click.pns', '.pns-retry', function () {
            $container.html($('<div class="subsidy-loading-message"></div>').text(loadingLabel));
            loadPanel();
            return false;
        });

        // ── Topup info changed (popup closed): the whole panel is rebuilt ──
        $container.on('click.pns', '.pns-reload', function () {
            loadPanel();
            return false;
        });

        // ── Product-group topup changed (popup closed): the two grids of that subsidy ──
        $container.on('click.pns', '.pns-refresh', function () {
            $.each(String($(this).data('grids')).split(','), function (_, grid) {
                updateTable(grid);
            });
            return false;
        });

        // ── Grid: change a product group's topup (popup) ──
        $container.on('click.pns', '.btn-pns-pg-topup', function () {
            var $btn = $(this);
            var popupUrl = $container.find('.pns-config').data('topupPopupUrl');

            OpenWindow(popupUrl
                + '?subsidyPackNameId=' + $btn.data('subsidyPackNameId')
                + '&productGroupId=' + $btn.data('productGroupId')
                + '&btnRefreshId=' + prefix + '-btnRefreshPackNameProductGroups-' + $btn.data('subsidyId'), 600, 400, true);
            return false;
        });

        loadPanel();
    })();
</script>
```

### _PackNameBoxSummary.cshtml

`Plugins/Nop.Plugin.CW.Core/Views/PackNameCheckout/_PackNameBoxSummary.cshtml`

**4.10.** The packname line: name, branch, shipping method and address. It is taken out of `_PackNameBox` so the by-product view can show it read-only. Only the editable box adds the "N addresses available — see more" line.

#### 19. NEW FILE — create the file with this content

`Plugins/Nop.Plugin.CW.Core/Views/PackNameCheckout/_PackNameBoxSummary.cshtml`

(61 lines)

```cshtml
@model PackNameBoxModel

@using Nop.Plugin.CW.Core.Helpers

@*
    The packname, its branch, shipping method and address: the header of the editable box, and the whole of the
    read-only line the by-product view shows on each row. Only the editable box says how many addresses there are
    to choose from (ViewData "PackNameBoxEditable").
*@
@{
    var packNameIconClass = Model.Item.PackNameModel.Group ? "packname-group" : (Model.Item.PackNameModel.IsCustomer ? "packname-customer" : "packname-no-customer");
    var shippingAddress = Model.ShippingData.Address;
    var colour = PackNameColourHelper.Normalize(Model.Item.PackNameModel.Colour);
    var editable = ViewData["PackNameBoxEditable"] as bool? ?? false;
}

<div class="pack-name-header-info">
    <div class="pack-name-info">
        <div class="pack-name-name" style="color: @colour">
            @Html.PackNameTypeIcon(packNameIconClass, "pack-name-icon")
            <span>@Model.Item.PackNameModel.Name</span>
        </div>
        <div class="pack-name-branch">
            @Html.CWIcon("cw-icon-truck", "pack-name-branch-icon")
            <span>@Model.Item.PackNameModel.Branch</span>
        </div>
    </div>
    <div class="pack-name-shipping-method">
        @Html.CWIcon("cw-icon-truck", "pack-name-shipping-method-icon")
        <div class="pack-name-shipping-method-info">
            @if (!string.IsNullOrEmpty(Model.ShippingData.SelectedShippingMethod))
            {
                <span class="pack-name-shipping-method-name">@Model.ShippingData.SelectedShippingMethod</span>
            }
            else
            {
                <span class="pack-name-shipping-method-name">@T("Plugins.CW.Core.PackNameCheckout.ShippingMethod.NotSelected")</span>
            }
        </div>
    </div>
    <div class="pack-name-shipping-address">
        @Html.CWIcon("cw-icon-location", "pack-name-shipping-address-icon")
        <div class="pack-name-shipping-address-info">
            @if (shippingAddress != null && shippingAddress.Id > 0)
            {
                <span class="pack-name-shipping-address-name">@await Html.PartialAsync("_ShippingAddressName", shippingAddress)</span>
            }
            else
            {
                <span class="pack-name-shipping-address-name">@T("Plugins.CW.Core.PackNameCheckout.ShippingAddresses.NotSelected")</span>
            }
            @if (editable && Model.AvailableAddresses.Count > 1)
            {
                <div class="pack-name-shipping-addresses-available">
                    <span>@T("Plugins.CW.Core.PackNameCheckout.ShippingAddresses.Available", Model.AvailableAddresses.Count)</span>
                    <span class="pack-name-shipping-addresses-see-more">@T("Plugins.CW.Core.PackNameCheckout.ShippingAddresses.Available.SeeMore")</span>
                </div>
            }
        </div>
    </div>
</div>

```

### _PackNameBox.cshtml

`Plugins/Nop.Plugin.CW.Core/Views/PackNameCheckout/_PackNameBox.cshtml`

**4.10.** The box's header now renders `_PackNameBoxSummary`, with the view-data flag that turns on the addresses line. Nothing else changes: same classes, same body, same error div.

#### 20. WHOLE FILE — replace the entire content of the file with this

`Plugins/Nop.Plugin.CW.Core/Views/PackNameCheckout/_PackNameBox.cshtml`

(19 lines)

```cshtml
@model PackNameBoxModel

@{
    //the header shows how many addresses there are to choose from: this box is where the customer chooses
    ViewData["PackNameBoxEditable"] = true;
}

<div class="pack-name-panel pack-name-panel-@Model.Item.PackNameModel.Id with-body @(Model.ShippingData.ShippingMethod.Warnings.Count > 0 ? "with-errors" : string.Empty)" data-itemid="@Model.Item.Id">
    <div class="pack-name-panel-header collapsed">
        @await Html.PartialAsync("_PackNameBoxSummary", Model)
        <div class="toggle-icon">
            <div class="arrow"></div>
        </div>
    </div>
    <div class="pack-name-panel-body" style="display: none;">
        @await Html.PartialAsync("_PackNameBoxBody", Model)
    </div>
    <div id="pack-name-errors-@Model.Item.Id" class="pack-name-panel-errors message-error"></div>
</div>
```

### _CartByProduct.cshtml

`Plugins/Nop.Plugin.CW.Core/Views/PackNameCheckout/_CartByProduct.cshtml`

**4.10 (decided 2026-11-12).** Each row shows its packname's line read-only, plus any shipping warnings. The editable box, with its radios and handlers, is drawn only in the by-packname view (once per packname). That ends the shared radio names and the one-post-per-row changes.

The header-toggle script and the address-dialog div were only there for the box, and in this view they are now dead.

#### 21. REPLACE — lines 200–231 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Views/PackNameCheckout/_CartByProduct.cshtml`

FIND (lines 200–231, 32 lines):

```cshtml
                                </div>
                            }
                            @await Html.PartialAsync("_PackNameBox", row.Box)
                        </div>
                    }
                </div>
            </div>
        }
    }

    <div id="packname-address-dialog" style="display: none;"></div>
</div>
<script>
    $(document).ready(function () {
        $('.pack-name-panel-header').click(function () {
            let $container = $(this).parent();

            if (!$container.hasClass("with-errors")) {
                $container.find('.pack-name-panel-body').slideToggle();

                if ($(this).hasClass('collapsed')) {
                    $(this).addClass('expanded');
                    $(this).removeClass('collapsed');
                }
                else {
                    $(this).addClass('collapsed');
                    $(this).removeClass('expanded');
                }
            }
        });
    });
</script>
```

REPLACE WITH (26 lines):

```cshtml
                                </div>
                            }
                            @* read-only: a packname's shipping is chosen in the by-packname view, where its box appears once *@
                            <div class="pack-name-panel pack-name-panel-@row.Box.Item.PackNameModel.Id">
                                <div class="pack-name-panel-header">
                                    @await Html.PartialAsync("_PackNameBoxSummary", row.Box)
                                </div>
                                @if (row.Box.ShippingData.ShippingMethod.Warnings.Count > 0)
                                {
                                    <div class="pack-name-panel-errors message-error">
                                        <ul>
                                            @foreach (var warning in row.Box.ShippingData.ShippingMethod.Warnings)
                                            {
                                                <li>@warning</li>
                                            }
                                        </ul>
                                    </div>
                                }
                            </div>
                        </div>
                    }
                </div>
            </div>
        }
    }
</div>
```

### _OrderSummary.cshtml

`Plugins/Nop.Plugin.CW.Core/Views/PackNameCheckout/_OrderSummary.cshtml`

**4.10, follow-on.** `panelKey` counted a packname's position among repeated panels. Panels repeated only in the by-product view, and none of them open now, so a packname's class is the whole key. The comment is updated to match. One block, from `loadCartItems`'s comment to the end of `panelKey`.

#### 22. REPLACE — lines 95–137 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Views/PackNameCheckout/_OrderSummary.cshtml`

FIND (lines 95–137, 43 lines):

```cshtml
    });

    //keepExpanded: a shipping change redraws the whole cart in one request (it used to send one request per
    //packname panel), so the panels the customer had open are opened again once it is drawn. A panel is
    //remembered by its row and its packname: in the by-product view one packname's panel repeats on every row.
    function loadCartItems(keepExpanded) {
        let viewMode = getViewMode();
        let expandedPanels = [];

        if (keepExpanded) {
            $('#cartItemsContainer .pack-name-panel').each(function () {
                if ($(this).children('.pack-name-panel-header').hasClass('expanded')) {
                    expandedPanels.push(panelKey(this));
                }
            });
        }

        PackNameCheckout.startRequest();

        $("#cartItemsContainer").load('@Url.Action("PackNameCartItems", "PackNameCheckout")' + '?ViewMode=' + viewMode,
            function () {
                $('#cartItemsContainer .pack-name-panel').each(function () {
                    if (expandedPanels.indexOf(panelKey(this)) >= 0 && !$(this).hasClass('with-errors')) {
                        $(this).children('.pack-name-panel-header').removeClass('collapsed').addClass('expanded');
                        $(this).children('.pack-name-panel-body').show();
                    }
                });

                PackNameCheckout.endRequest();
            });
    }

    //The panel's packname class and its position among the panels of that packname.
    function panelKey(panel) {
        let packNameClass = ($(panel).attr('class') || '').split(/\s+/).filter(function (c) { return /^pack-name-panel-\d+$/.test(c); })[0] || '';
        if (!packNameClass) {
            return '';
        }
        let position = $('#cartItemsContainer .' + packNameClass).index(panel);
        return packNameClass + ':' + position;
    }

    function getViewMode() {
```

REPLACE WITH (38 lines):

```cshtml
    });

    //keepExpanded: a shipping change redraws the whole cart in one request, so the panels the customer had open are
    //opened again once it is drawn. A panel is remembered by its packname: only the by-packname view has panels that
    //open, one per packname.
    function loadCartItems(keepExpanded) {
        let viewMode = getViewMode();
        let expandedPanels = [];

        if (keepExpanded) {
            $('#cartItemsContainer .pack-name-panel').each(function () {
                if ($(this).children('.pack-name-panel-header').hasClass('expanded')) {
                    expandedPanels.push(panelKey(this));
                }
            });
        }

        PackNameCheckout.startRequest();

        $("#cartItemsContainer").load('@Url.Action("PackNameCartItems", "PackNameCheckout")' + '?ViewMode=' + viewMode,
            function () {
                $('#cartItemsContainer .pack-name-panel').each(function () {
                    if (expandedPanels.indexOf(panelKey(this)) >= 0 && !$(this).hasClass('with-errors')) {
                        $(this).children('.pack-name-panel-header').removeClass('collapsed').addClass('expanded');
                        $(this).children('.pack-name-panel-body').show();
                    }
                });

                PackNameCheckout.endRequest();
            });
    }

    //The panel's packname class.
    function panelKey(panel) {
        return ($(panel).attr('class') || '').split(/\s+/).filter(function (c) { return /^pack-name-panel-\d+$/.test(c); })[0] || '';
    }

    function getViewMode() {
```

### Nop.Plugin.CW.Core.csproj

`Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`

**4.10.** The new view is deployed. There is no glob, so a view without a csproj entry is never copied.

#### 23. REPLACE — lines 585–588 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`

FIND (lines 585–588, 4 lines):

```xml
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Views\PackNameCheckout\_PaymentMethodInfo.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
```

REPLACE WITH (7 lines):

```xml
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Views\PackNameCheckout\_PackNameBoxSummary.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Views\PackNameCheckout\_PaymentMethodInfo.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
```

### plugin.json

`Plugins/Nop.Plugin.CW.Core/plugin.json`

The DATA version goes up so the update installs the new resource (`CLAUDE.md` §6.8).

#### 24. REPLACE — lines 3–7 (1 of 1 in this file, applied bottom to top)

`Plugins/Nop.Plugin.CW.Core/plugin.json`

FIND (lines 3–7, 5 lines):

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_09_DATA_0.2",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```

REPLACE WITH (5 lines):

```json
  "FriendlyName": "CW Core Plugin",
  "SystemName": "CW.Core",
  "Version": "2026_11_12_DATA_0.1",
  "SupportedVersions": [ "5.00" ],
  "Author": "CW Team",
```
## Tests (already in `Tests/CW.Tests`, added by Claude)

They build only once the spec is applied (until then, some reach members by reflection that do not exist yet):

| File | Cases | What it pins |
|---|---|---|
| `Core/Controllers/BranchAdminGuardsTests.cs` | 11 | `SearchBranches` carries Branch + MultiStore; `AddressEdit` GET / POST never load or save another owner's address; a deleted branch is not edited (edit GET / POST, create GET / POST); `BranchDetail` opens a live branch of its store and returns 404 for a missing, deleted or other-store branch or parent |
| `Core/Controllers/CustomerBranchValidationTests.cs` | 6 | `ValidateBranchAsync`: no branch and an active branch of the store pass; missing, deleted, inactive and other-store are refused with an error on `BranchId` |
| `Core/Services/MultiStores/ManagerStoreTests.cs` | 4 | `GetManagerStoreIdAsync`: the list's store (with and without a branch); none for a deleted packname or a deleted list |

The views (4.10, 4.17) are covered by the site test plan below.

## Build and checks

1. Build the solution. The views compile on first request, so the test plan opens every view this spec touches.
2. `dotnet test Tests/CW.Tests --filter "FullyQualifiedName~CW.Tests.Core"`: everything passes. That includes `NopStartupTests`, which checks that `ExtendedCustomerController`'s new `IBranchService` is registered.
3. Run the plugin update (*Configuration › Local plugins*, or restart): `plugin.json` is `2026_11_12_DATA_0.1`, and the resource `Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.NotAvailable` is installed.
4. Independent review: done by a sub-agent before this reached you (see the last section).

## Test plan (dev site `https://localhost:59579`, database `testnop50`)

Side effects:
- T4 adds and then edits one branch address on a test branch.
- T6 changes one test customer's branch and puts it back.
- T12 switches PackName and Subsidy off for the test store and back on.
- Nothing is deleted.

| # | Item | Steps | Expected |
|---|---|---|---|
| T1 | 4.5 | Manage Store › Branches: open a branch, then *Add child branch*. In the browser, call `BranchDetail` with a branch id of another store, a deleted branch id, and an id that does not exist | The normal panels open. The three hand-made calls return 404, which the panel shows as "Failed to load" |
| T2 | 4.7 | Manage Store › packname panel: the branch search returns branches, as an administrator | Results as before |
| T3 | 4.7 | Branch address popup (GET) and cost-centre address popup (GET) with an `addressId` that belongs to a customer | Access denied, and the address is not shown. The popups for the owner's own addresses open as before |
| T4 | 4.7 | Standalone *Branches* page: add an address to a branch and edit it. Then open `Branch/AddressEdit?addressId={a customer's address}&branchId={branch}`, and post the edit form with that address id | Own address: added, edited, saved. Another owner's address: redirected to the branch page, and the address is unchanged in the database |
| T5 | 4.12 | Customer page › edit a customer with no packname. In the dev tools, set the branch `<option>` value to a deleted branch, then to an inactive branch, then to a branch of another store, and save each time. Finally choose an active branch normally and save | Each hand-made value is refused with the new message next to *Branch*, and nothing is saved. The normal choice is saved |
| T6 | 4.12 | The same on *Create customer* with a hand-made branch value. Then edit a customer **with** a packname, move them to an active branch of the same store, and move them back | Refused on create. With a packname: the packname moves, as before |
| T7 | 4.17 | Customer edit page of a packname customer: the subsidies card loads. Then, in the dev tools, block `Subsidy/PackNameSubsidies` and reload | Normal: the card loads. Blocked: "Failed to load. Retry". Unblock and click *Retry*: the card loads |
| T8 | 4.10 | Packname checkout with two packnames, one of them on two products. Switch to the by-product view | Each row shows its packname's name, branch, method and address as text, with no arrow, no radios and no expanding. Shipping warnings, if any, show under the row |
| T9 | 4.10 | Switch back to the by-packname view. Open a box, change the address and the method | The box opens, saves once per change (one request in the network tab), and stays open after the redraw, as before |
| T10 | 4.13 | As a department manager on the storefront: the packname selector lists the department's packnames, as before | No change |
| T11 | all | Open each page above once as a user | Nothing overflowing, links look like links, no console errors |
| T12 | feature off | Switch **PackName** off for the test store: the packname checkout (T8/T9) is not offered, and the Manage Store packname panel (T2) is closed. Switch **Subsidy** off: the customer page shows no subsidies card (T7). Switch both back on | As before this spec (the feature gates are unchanged) |

## After the part

On approval:
- Remove 4.5, 4.7, 4.10, 4.12, 4.13 and 4.17 from `Subsidy_PackName_Open_Items.md`.
- Mark the round plan.
- Note the read-only by-product view in `PackName_Order_Shipments.md` (checkout section).
- Move this spec to `Deprecated/`.

Then R2-B.

## Independent review (2026-11-12)

A sub-agent that did not write this spec applied all the changes to a scratch copy and read every caller.

- **Fixed before it reached you:**
  - The `CustomerBranchValidationTests` localisation mock returned `null`, which `AddModelError` refuses. It now returns the resource key.
  - Five places split one member or one view into several blocks. Each is now one block (changes 1, 14, 21, 22) or a whole file (change 18).
  - The branch address popup's save now refuses a deleted branch too (change 9).
  - Test T12 (features off) was added.
- **Recorded, not changed:** the four items under "Not in this part" above.
- **Checked and found correct:** every FIND at its lines; every member, type and overload the changes use; the 404 path of the branch panel; partial resolution and view data for `_PackNameBoxSummary`; every reader of `.pack-name-panel`; the csproj entry; the DATA bump; and the other two test files.

## Test run 1 (2026-11-12, dev site, build with the spec applied)

| # | Result | What was done |
|---|---|---|
| T1 | **Pass** | `BranchDetail`: own branch, new root and new child open (200). Another store's branch or parent, a missing id or parent, and the deleted ids 8, 12, 13, 15 return 404. On the page, a hash pointing at a deleted branch shows "Failed to load. Retry" |
| T2 | **Pass** | `SearchBranches` as administrator: store 1 returns its 11 branches; store 2 returns none (it has no branches) |
| T3 | **Pass** | Branch popup (GET): branch 3's own address 22 opens; address 1 (not the branch's) and a deleted branch are refused (redirect to access denied). Cost-centre popup (GET): cost centre 2's address 24 opens; addresses 22 and 25 (other owners) are refused; the blank "add" popup opens |
| T4 | **Pass** | *Branches* page: own address 22 opens and saves (unchanged values). Address 24 (a cost centre's) on GET redirects to the branch page; a forged POST of address 24 with a new street redirects and leaves 24 unchanged ("25 King William Street"). Deleted branch 13: address edit and create GET go to the list. A new address was **not** added (the create path changes only the deleted-branch check, covered above) |
| T5 | **Pass** | Brenda Lindgren (7): posting branch 10 (inactive), 13 (deleted) and 99999 (missing) each show the new message under *Branch* and save nothing (branch stays 1). Moving to branch 3 and back to 1 saves normally. Another store's branch could not be tried: store 2 has no branches (covered by the unit tests) |
| T6 | **Pass** | *Create customer* with branch 10 is refused with the message; no customer created. The packname move on a branch change is unchanged code (T5's move passed its packname check) |
| T7 | **Pass** | Steve Gates's subsidies card loads. A failed load (simulated by making `$.get` fail, which runs the same `.fail` handler) shows "Failed to load. Retry"; *Retry* shows "Loading..." and loads the card |
| T8 | **Pass** | Impersonating Steve, cart: Steve on two products, Brenda on one. By-product view: each row shows name, branch, method, address as text; no arrow, no radios, no element ids repeated; Brenda's row showed her shipping warning while her method was unavailable |
| T9 | **Pass** | By-packname view: Steve's box opens; changing the method posts once, the header follows, the box stays open after the redraw. Method set back to Express Delivery |
| T10 | **Pass** | Steve's packname selector lists his packnames as before |
| T11 | **Pass** | The pages above opened once as a user; the checkout views (screenshot) are laid out correctly. Console: no error from the changed views |
| T12 | **Not run** | Switching the features off was not done in this run; the gates themselves are unchanged |

Clean-up: the two cart lines added for T8 (Oversized Women T-Shirt for Brenda and Steve) were removed; the impersonation was ended.

**Found during the run, older than this spec:** `_Branches.BranchTree.cshtml:38` writes the selected path with `@Html.Raw(...)` inside a double-quoted attribute, so the JSON's quotes end the attribute early (`data-selected-ids="["`). On every page load with a branch selected, the tree script throws `selectedIds.forEach is not a function` and does not expand the path. Fix given in the chat (2026-11-12).

**Tree fix verified (2026-11-12):** after the change, reloading `Admin/MultiStore/Edit/1#branches/branch:1/branch:3` reads the path back as `["1","3"]` and the tree expands it. **R2-A approved by Eduardo the same day.**
