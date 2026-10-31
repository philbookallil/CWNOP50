# CW.Core move to the final 5.00 — the deferred fixes

Written 2026-10-31. This covers the three fixes deferred from the beta port (beta strategy §5.3), now that the move
has passed phase 7. All three defects behave the same way in 4.9.

| # | Deferred fix | Decision (Eduardo, 2026-10-31) |
|---|---|---|
| 1 | A packname that moves to another branch keeps a cost centre tied to its old branch | **Dropped.** The fix rested on a wrong premise: nothing limits a branch-tied cost centre to its branch. The admin and storefront pickers offer every cost centre of the store (`MultiStoreService.GetCostCenterSelectItemsAsync`), and the saves check only the store. Clearing it on a move would let the admin pick it again on the same form. A cost centre's branch stays a grouping in the admin tree, as in 4.9. Ask the manager if branch restriction is wanted later |
| 2 | The admin packname pages save the colour as typed | Normalise the colour on every admin save (changes 9–13) |
| 3 | Provisioning can create a packname whose name the list already has | **Skip and report.** No packname is created for that customer. The customer page shows a warning. *Generate packnames* skips the customer and logs a warning (changes 1–8) |

Reviewed 2026-10-31 (independent review). Every FIND block and anchor matches the original files, and the line numbers
hold when the changes are applied bottom to top. Nothing else calls the two changed methods or stubs them in a mock,
and the service constructor is built by hand only in its own tests. The new and existing tests pass as traced. No other
admin path writes `Colour` from a form. The review also confirmed fix 1's premise: the cost-centre pickers and saves
check only the store. Its two minor points are taken: the logger mock moved to `SetUp` (14b), and the helper's remark
was updated (13d).

**Labels.** **REPLACE** — find the FIND block (whole lines, at the lines given) and put the REPLACE block in its place.
**INSERT AFTER** — put the block after the line given. Within a file, the changes are given bottom to top, so the
line numbers of the ones still to apply stay right. All C# files are CRLF.

---

## Fix 3 — provisioning skips a name the list already has

Every other insert path checks the name first (`PackNameRegistrationService`, the admin saves, the customer branch
check). Provisioning builds the name from the customer (`BuildPackName`) and inserted it unchecked.

The service now asks `PackNameNameExistsAsync` first:

- `ProvisionCustomerBranchAsync` returns the reason it created nothing, or null. `ApplyCustomerBranchAsync` passes that
  reason on.
- The customer page's *Create* and *Edit* show it as a warning. The customer is still saved, linked to the branch, with
  no packname. The admin can rename the other packname and save again, or create the packname from the store page.
- `CreatePackNamesForCustomersWithoutOneAsync` (*Generate packnames*, run in the background) skips the customer. It
  writes one warning per skipped customer to *System → Log* and counts only the packnames it created.
  - Two customers of the same name in one branch: the first gets the packname, the second is skipped. The check reads
    the database after each insert, so the second sees the first.

### 1. REPLACE — `ApplyCustomerBranchAsync` returns provisioning's reason

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Services\PackNames\PackNameProvisioningService.cs` — lines 282–315

Depends on: change 2 (the method it returns), change 6 (the interface). 34 lines become 34.

FIND:
```csharp
    public virtual async Task ApplyCustomerBranchAsync(Customer customer, int branchId)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var branch = await _branchService.GetBranchByIdAsync(branchId);

        if (branch == null || branch.Deleted)
            return;

        var (packName, packNameList) = await GetBranchPackNameAsync(customer);

        //a customer without a packname may get one (provisioning reads the store's settings and switches; it does nothing
        //for a customer who already has a packname, in a store-wide list for instance)
        if (packName == null)
        {
            await ProvisionCustomerBranchAsync(customer, branchId);
            return;
        }

        if (packNameList.BranchId == branch.Id || packNameList.StoreId != branch.StoreId)
            return;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, branch.StoreId))
            return;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(branch.StoreId);
        var targetList = await EnsureDefaultPackNameListAsync(branch, (PackNameListNameFrom)storeSettings.PackNameListNameFrom);

        packName.PackNameListId = targetList.Id;
        await _packNameService.UpdatePackNameAsync(packName);

        await _customerActivityService.InsertActivityAsync("EditPackName",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditPackName"), packName.Name), packName);
    }
```

REPLACE WITH:
```csharp
    public virtual async Task<string> ApplyCustomerBranchAsync(Customer customer, int branchId)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var branch = await _branchService.GetBranchByIdAsync(branchId);

        if (branch == null || branch.Deleted)
            return null;

        var (packName, packNameList) = await GetBranchPackNameAsync(customer);

        //a customer without a packname may get one (provisioning reads the store's settings and switches; it does nothing
        //for a customer who already has a packname, in a store-wide list for instance)
        if (packName == null)
            return await ProvisionCustomerBranchAsync(customer, branchId);

        if (packNameList.BranchId == branch.Id || packNameList.StoreId != branch.StoreId)
            return null;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, branch.StoreId))
            return null;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(branch.StoreId);
        var targetList = await EnsureDefaultPackNameListAsync(branch, (PackNameListNameFrom)storeSettings.PackNameListNameFrom);

        packName.PackNameListId = targetList.Id;
        await _packNameService.UpdatePackNameAsync(packName);

        await _customerActivityService.InsertActivityAsync("EditPackName",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.EditPackName"), packName.Name), packName);

        return null;
    }
```

### 2. REPLACE — `ProvisionCustomerBranchAsync` checks the name and returns why it created nothing

Same file — lines 220–246

Depends on: change 6 (the interface), change 8 (the resource). 27 lines become 40.

FIND:
```csharp
    /// <param name="customer">The customer</param>
    /// <param name="branchId">The branch the customer was linked to</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task ProvisionCustomerBranchAsync(Customer customer, int branchId)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var branch = await _branchService.GetBranchByIdAsync(branchId);

        if (branch == null || branch.Deleted)
            return;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, branch.StoreId))
            return;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(branch.StoreId);

        if (!storeSettings.UseStaffAsPackName)
            return;

        if (await _packNameService.GetPackNameByCustomerIdAsync(customer.Id) != null)
            return;

        var packNameList = await EnsureDefaultPackNameListAsync(branch, (PackNameListNameFrom)storeSettings.PackNameListNameFrom);

        await _packNameService.InsertPackNameAsync(BuildPackName(packNameList, customer));
    }
```

REPLACE WITH:
```csharp
    /// <param name="customer">The customer</param>
    /// <param name="branchId">The branch the customer was linked to</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains why no packname was created when the store asks for one but the list already has a
    /// packname of the customer's name; otherwise null
    /// </returns>
    public virtual async Task<string> ProvisionCustomerBranchAsync(Customer customer, int branchId)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var branch = await _branchService.GetBranchByIdAsync(branchId);

        if (branch == null || branch.Deleted)
            return null;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, branch.StoreId))
            return null;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(branch.StoreId);

        if (!storeSettings.UseStaffAsPackName)
            return null;

        if (await _packNameService.GetPackNameByCustomerIdAsync(customer.Id) != null)
            return null;

        var packNameList = await EnsureDefaultPackNameListAsync(branch, (PackNameListNameFrom)storeSettings.PackNameListNameFrom);
        var packName = BuildPackName(packNameList, customer);

        //every other insert path refuses a name the list already has; provisioning skips the customer and says why
        if (await _packNameService.PackNameNameExistsAsync(packNameList.Id, packName.Name))
            return string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameNotCreated"),
                packName.Name, packNameList.Name);

        await _packNameService.InsertPackNameAsync(packName);

        return null;
    }
```

### 3. REPLACE — *Generate packnames* skips a name the list already has

Same file — lines 174–192

Depends on: change 4 (the logger). 19 lines become 31.

FIND:
```csharp
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the number of packnames created
    /// </returns>
    public virtual async Task<int> CreatePackNamesForCustomersWithoutOneAsync(PackNameList packNameList)
    {
        ArgumentNullException.ThrowIfNull(packNameList);

        //a list without a branch has no customers to draw from
        if (!packNameList.BranchId.HasValue)
            return 0;

        var customers = await _multiStoreService.GetCustomersWithoutPackNameByBranchAsync(packNameList.BranchId.Value);

        foreach (var customer in customers)
            await _packNameService.InsertPackNameAsync(BuildPackName(packNameList, customer));

        return customers.Count;
    }
```

REPLACE WITH:
```csharp
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the number of packnames created; a customer whose name the list already has gets none,
    /// with a warning in the log
    /// </returns>
    public virtual async Task<int> CreatePackNamesForCustomersWithoutOneAsync(PackNameList packNameList)
    {
        ArgumentNullException.ThrowIfNull(packNameList);

        //a list without a branch has no customers to draw from
        if (!packNameList.BranchId.HasValue)
            return 0;

        var customers = await _multiStoreService.GetCustomersWithoutPackNameByBranchAsync(packNameList.BranchId.Value);
        var created = 0;

        foreach (var customer in customers)
        {
            var packName = BuildPackName(packNameList, customer);

            //read after each insert, so two customers of one name in the branch give one packname, not two
            if (await _packNameService.PackNameNameExistsAsync(packNameList.Id, packName.Name))
            {
                await _logger.WarningAsync($"[PackNames] Generate packnames: no packname created for customer {customer.Id}: the list {packNameList.Name} (id {packNameList.Id}) already has a packname named {packName.Name}.");
                continue;
            }

            await _packNameService.InsertPackNameAsync(packName);
            created++;
        }

        return created;
    }
```

### 4. REPLACE — the service takes nop's logger

Same file — lines 32, 45 and 54 (three blocks, bottom to top)

Depends on: nothing. `Nop.Services.Logging` is already imported (line 12). DI resolves the new parameter. The unit
tests build the service by hand (change 14).

FIND (line 54):
```csharp
        _localizationService = localizationService;
```
REPLACE WITH:
```csharp
        _localizationService = localizationService;
        _logger = logger;
```

FIND (line 45):
```csharp
        ILocalizationService localizationService)
```
REPLACE WITH:
```csharp
        ILocalizationService localizationService,
        ILogger logger)
```

FIND (line 32):
```csharp
    protected readonly ILocalizationService _localizationService;
```
REPLACE WITH:
```csharp
    protected readonly ILocalizationService _localizationService;
    protected readonly ILogger _logger;
```

### 5. REPLACE — the interface: `ApplyCustomerBranchAsync`

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Services\PackNames\IPackNameProvisioningService.cs` — lines 72–75

Depends on: nothing. 4 lines become 7.

FIND:
```csharp
    /// <param name="customer">The customer</param>
    /// <param name="branchId">The customer's new branch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ApplyCustomerBranchAsync(Customer customer, int branchId);
```

REPLACE WITH:
```csharp
    /// <param name="customer">The customer</param>
    /// <param name="branchId">The customer's new branch</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains why the customer got no packname (see ProvisionCustomerBranchAsync), or null
    /// </returns>
    Task<string> ApplyCustomerBranchAsync(Customer customer, int branchId);
```

### 6. REPLACE — the interface: `ProvisionCustomerBranchAsync`

Same file — lines 47–50

Depends on: nothing. 4 lines become 8.

FIND:
```csharp
    /// <param name="customer">The customer</param>
    /// <param name="branchId">The branch the customer was linked to</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ProvisionCustomerBranchAsync(Customer customer, int branchId);
```

REPLACE WITH:
```csharp
    /// <param name="customer">The customer</param>
    /// <param name="branchId">The branch the customer was linked to</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains why no packname was created when the store asks for one but the list already has a
    /// packname of the customer's name; otherwise null
    /// </returns>
    Task<string> ProvisionCustomerBranchAsync(Customer customer, int branchId);
```

### 7. REPLACE — the customer page shows the reason (two blocks, bottom to top)

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Areas\Admin\Controllers\ExtendedCustomerController.cs`

Depends on: changes 1, 2. A warning is a notification, not a model error: the customer is saved, only the packname
is missing.

**7a — *Edit*, lines 767–770.** 4 lines become 9.

FIND:
```csharp
                    //a new branch takes the customer's packname along, or gives the customer one when the store gives
                    //every branch customer a packname
                    if (customerBranch.BranchId.HasValue && customerBranch.BranchId != previousBranchId)
                        await _packNameProvisioningService.ApplyCustomerBranchAsync(customer, customerBranch.BranchId.Value);
```

REPLACE WITH:
```csharp
                    //a new branch takes the customer's packname along, or gives the customer one when the store gives
                    //every branch customer a packname
                    if (customerBranch.BranchId.HasValue && customerBranch.BranchId != previousBranchId)
                    {
                        var packNameWarning = await _packNameProvisioningService.ApplyCustomerBranchAsync(customer, customerBranch.BranchId.Value);

                        if (!string.IsNullOrEmpty(packNameWarning))
                            _notificationService.WarningNotification(packNameWarning);
                    }
```

**7b — *Create*, lines 462–464.** 3 lines become 8.

FIND:
```csharp
                //the store may ask for a packname for every customer linked to one of its branches
                if (customerBranch.BranchId.HasValue)
                    await _packNameProvisioningService.ProvisionCustomerBranchAsync(customer, customerBranch.BranchId.Value);
```

REPLACE WITH:
```csharp
                //the store may ask for a packname for every customer linked to one of its branches
                if (customerBranch.BranchId.HasValue)
                {
                    var packNameWarning = await _packNameProvisioningService.ProvisionCustomerBranchAsync(customer, customerBranch.BranchId.Value);

                    if (!string.IsNullOrEmpty(packNameWarning))
                        _notificationService.WarningNotification(packNameWarning);
                }
```

### 8. INSERT AFTER — the warning's text

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\CWCorePlugin.cs` — after line 948

Depends on: change 15 (the version, so the update installs it). `{0}` is the name, `{1}` the list.

INSERT AFTER line 948 (`["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameOtherStore"] = …`):
```csharp
            ["Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameNotCreated"] = "The customer was saved without a packname: the list {1} already has a packname named {0}. Rename one of them, or create the customer's packname on the store page.",
```

---

## Fix 2 — the admin packname saves keep only a hex colour

`PackNameColourHelper.Normalize` already guards the storefront saves and the import. It returns a six-digit hex colour,
writes out a three-digit one, and returns null for anything else. The five admin saves in `MultiStoreController` now
use it:

- **A new packname** with no valid colour gets a random one, as `PackNameRegistrationService` and provisioning do.
- **An edit** keeps the packname's colour when the posted one is not valid. `ToEntity` has already overwritten it, so
  the colour is read before mapping, as the list id already is.

Colours already stored as free text stay until the packname is next saved.

### 9. INSERT AFTER — *Create packname* popup (customer without one)

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Areas\Admin\Controllers\MultiStoreController.cs` — after line 1873

INSERT AFTER line 1873 (`            packName.CostCenterId = null;`, the one after line 1872's `if`):
```csharp

            //the storefront writes the colour into HTML: only a hex colour is kept, and a new packname without one gets a random colour
            packName.Colour = PackNameColourHelper.Normalize(packName.Colour) ?? $"#{Random.Shared.Next(0x1000000):X6}";
```

### 10. INSERT AFTER — `SavePackName`, edit: normalise

Same file — after line 1565 (`                    packName.CostCenterId = null;`, after line 1564's `if`)

```csharp

                //only a hex colour is kept; a posted colour that is not one leaves the packname's colour as it was
                packName.Colour = PackNameColourHelper.Normalize(packName.Colour) ?? colourBeforeMapping;
```

### 11. INSERT AFTER — `SavePackName`, edit: the colour before mapping

Same file — after line 1547 (`                var packNameListIdBeforeMapping = packName.PackNameListId;`)

```csharp
                var colourBeforeMapping = packName.Colour;
```

### 12. INSERT AFTER — `SavePackName`, new packname

Same file — after line 1519 (`                    packName.CostCenterId = null;`, after line 1518's `if`)

```csharp

                //the storefront writes the colour into HTML: only a hex colour is kept, and a new packname without one gets a random colour
                packName.Colour = PackNameColourHelper.Normalize(packName.Colour) ?? $"#{Random.Shared.Next(0x1000000):X6}";
```

### 13. INSERT AFTER — `SaveBranchPackName`, the same three (bottom to top)

Same file:

**13a** — after line 1417 (`                    packName.CostCenterId = null;`, after line 1416's `if`):
```csharp

                //only a hex colour is kept; a posted colour that is not one leaves the packname's colour as it was
                packName.Colour = PackNameColourHelper.Normalize(packName.Colour) ?? colourBeforeMapping;
```

**13b** — after line 1399 (`                var packNameListIdBeforeMapping = packName.PackNameListId;`):
```csharp
                var colourBeforeMapping = packName.Colour;
```

**13c** — after line 1373 (`                    packName.CostCenterId = null;`, after line 1372's `if`):
```csharp

                //the storefront writes the colour into HTML: only a hex colour is kept, and a new packname without one gets a random colour
                packName.Colour = PackNameColourHelper.Normalize(packName.Colour) ?? $"#{Random.Shared.Next(0x1000000):X6}";
```

`Nop.Plugin.CW.Core.Helpers` is already imported (line 21).

### 13d. REPLACE — the helper's remark no longer says the admin saves free text

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\Helpers\PackNameColourHelper.cs` — line 40

FIND:
```csharp
    /// light. A colour that is not a hex colour (the admin pages save it as typed) gets black.
```

REPLACE WITH:
```csharp
    /// light. A colour that is not a hex colour (one saved as free text before the admin saves normalised it) gets black.
```

---

## Tests and version

### 14. REPLACE and INSERT — `PackNameProvisioningServiceTests` (bottom to top)

`C:\Users\edpab\source\repos\CW50\Tests\CW.Tests\Core\Services\PackNames\PackNameProvisioningServiceTests.cs`

**14a — three tests at the end, REPLACE lines 459–460.**

FIND:
```csharp
    }
}
```

REPLACE WITH:
```csharp
    }

    [Test]
    public async Task A_linked_customer_whose_name_the_list_has_gets_no_packname_and_the_reason()
    {
        DefaultListOfBranch();
        _packNames.Setup(service => service.PackNameNameExistsAsync(71, "Ann Lee", 0)).ReturnsAsync(true);

        var reason = await Build().ProvisionCustomerBranchAsync(_customer, _branch.Id);

        reason.Should().Be("Ann Lee is in Harbour");
        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task A_customer_moving_to_a_branch_whose_list_has_the_name_gets_the_reason()
    {
        DefaultListOfBranch();
        _packNames.Setup(service => service.PackNameNameExistsAsync(71, "Ann Lee", 0)).ReturnsAsync(true);

        var reason = await Build().ApplyCustomerBranchAsync(_customer, _branch.Id);

        reason.Should().Be("Ann Lee is in Harbour");
        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task The_generator_skips_a_customer_whose_name_the_list_has_and_does_not_count_them()
    {
        var list = new PackNameList { Id = 7, BranchId = 5, Name = "Harbour" };
        _multiStore.Setup(service => service.GetCustomersWithoutPackNameByBranchAsync(5))
            .ReturnsAsync(new List<Customer> { _customer, new() { Id = 1049, FirstName = "Bo", LastName = "Ray" } });
        _packNames.Setup(service => service.PackNameNameExistsAsync(7, "Ann Lee", 0)).ReturnsAsync(true);

        var created = await Build().CreatePackNamesForCustomersWithoutOneAsync(list);

        created.Should().Be(1);
        _packNames.Verify(service => service.InsertPackNameAsync(It.Is<PackName>(packName => packName.CustomerId == 1049)), Times.Once);
        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Once);
        _logger.Verify(logger => logger.WarningAsync(It.Is<string>(message => message.Contains("1048")), null, null), Times.Once);
    }
}
```

**14b — the logger mock, INSERT AFTER line 92** (`        _activity = new Mock<ICustomerActivityService>();`, in
`SetUp`, so a test that builds the service twice still checks one logger):
```csharp
        _logger = new Mock<ILogger>();
```

**14b′ — the logger passed to the service, REPLACE lines 61–62** (in `Build()`).

FIND:
```csharp
        return new PackNameProvisioningService(_branches.Object, _packNames.Object, _multiStore.Object,
            settingService.Object, _features.Object, _customers.Object, _activity.Object, localization.Object);
```

REPLACE WITH:
```csharp
        return new PackNameProvisioningService(_branches.Object, _packNames.Object, _multiStore.Object,
            settingService.Object, _features.Object, _customers.Object, _activity.Object, localization.Object, _logger.Object);
```

**14c — the warning's text in the localization mock, INSERT AFTER line 57**
(`"Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameOtherStore" => …`):
```csharp
            "Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameNotCreated" => "{0} is in {1}",
```

**14d — the logger field, INSERT AFTER line 39** (`    private Mock<ICustomerActivityService> _activity;`):
```csharp
    private Mock<ILogger> _logger;
```

`Nop.Services.Logging` is already imported (line 15). nop 5.00's `ILogger.WarningAsync(string message, Exception
exception = null, Customer customer = null)` is matched with the two nulls.

### 15. REPLACE — plugin version, so the update installs the new resource

`C:\Users\edpab\source\repos\CW50\Plugins\Nop.Plugin.CW.Core\plugin.json` — line 5

FIND:
```json
  "Version": "2026_10_31_DATA_0.1",
```

REPLACE WITH:
```json
  "Version": "2026_10_31_DATA_0.2",
```

---

## Checks after applying

1. Rebuild: 0 errors. `dotnet test Tests\CW.Tests --filter "FullyQualifiedName~CW.Tests.Core"`: 634 passing (631 + the
   three new ones).
2. Restart the site. *Configuration → Local plugins* shows `2026_10_31_DATA_0.2`.
3. Claude checks on `testnop50`:
   - **D1** customer page, with *Use staff as packname* on for store 1: a new customer named like an existing packname
     of the target branch's default list, linked to that branch. Expected: the customer is saved, with the warning and
     no packname. Then the same customer with a unique name: a packname is created, with no warning.
   - **D2** *Generate packnames* with one such customer: the duplicate is skipped and one warning is in *System → Log*.
     The others get their packnames.
   - **D3** store page, edit a packname with colour `red`: the saved colour is unchanged. With `#0a0`: saved as
     `#00aa00`. A new packname with an empty colour: a random `#RRGGBB`.
4. Afterwards, any test customers and packnames that were created are deactivated rather than deleted. *Use staff as
   packname* goes back to its current value.

## Results (2026-10-31)

Applied by Eduardo. CW.Tests all green, and the site restarted on `2026_10_31_DATA_0.2`.

- **D1 pass.**
  - A new customer *Victoria Terces*, linked to Test Branch 1.1: saved (customer 21), with the warning "The customer
    was saved without a packname: the list Test Branch 1.1 L already has a packname named Victoria Terces…" and no
    packname.
  - The same customer renamed *Victoria Terces Two* and moved to Test Branch 1.2: saved with no warning, and packname
    36 created in Test Branch 1.2's list.
- **D2 pass.** *Generate packnames* for store 1 skipped customer 20 (also *Victoria Terces*, Test Branch 1.1). The log
  has one warning: "[PackNames] Generate packnames: no packname created for customer 20: the list Test Branch 1.1 L
  (id 7) already has a packname named Victoria Terces." No other branch customer was without a packname. The three
  settings the button writes are unchanged.
- **D3 pass.**
  - Packname 3 (`SavePackName`): `red` left `#fec93e`, `#0a0` saved as `#00aa00`, then the original colour was put
    back.
  - A new packname with an empty colour (`SaveBranchPackName`, packname 35): got `#94735B`. On edit, `red` kept that
    colour.
- **Left on `testnop50`, deactivated:** customers 20 and 21, packnames 35 and 36. Customer 20 was first created as a
  Guest by mistake and was then set to Registered. The log has no errors since the restart.

