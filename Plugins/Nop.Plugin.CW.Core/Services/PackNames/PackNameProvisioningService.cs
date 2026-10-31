using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Creates the packname lists and packnames a store asks for automatically. The two store settings are read here and
/// nowhere else: UseBranchesAsPackNameList gives a new branch its default list, UseStaffAsPackName gives a customer
/// linked to a branch a packname. The generate buttons of the store settings call the two lower methods directly.
/// </summary>
public class PackNameProvisioningService : IPackNameProvisioningService
{
    #region Fields

    protected readonly IBranchService _branchService;
    protected readonly IPackNameService _packNameService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly ISettingService _settingService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly ICustomerOverrideService _customerService;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ILogger _logger;

    #endregion

    #region Ctor

    public PackNameProvisioningService(IBranchService branchService,
        IPackNameService packNameService,
        IMultiStoreService multiStoreService,
        ISettingService settingService,
        ICWFeatureService cwFeatureService,
        ICustomerOverrideService customerService,
        ICustomerActivityService customerActivityService,
        ILocalizationService localizationService,
        ILogger logger)
    {
        _branchService = branchService;
        _packNameService = packNameService;
        _multiStoreService = multiStoreService;
        _settingService = settingService;
        _cwFeatureService = cwFeatureService;
        _customerService = customerService;
        _customerActivityService = customerActivityService;
        _localizationService = localizationService;
        _logger = logger;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Gets the list a branch's new packnames go into: its default active list, or its first active list when none is
    /// marked default
    /// </summary>
    /// <param name="branch">Branch</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list, or null when the branch has no active list
    /// </returns>
    protected virtual async Task<PackNameList> GetDefaultActivePackNameListAsync(Branch branch)
    {
        //a list that is switched off takes no new packnames
        var packNameLists = (await _packNameService.GetPackNameListsByBranchIdAsync(branch.Id)).Where(pnl => pnl.Active).ToList();

        return packNameLists.FirstOrDefault(pnl => pnl.IsDefault) ?? packNameLists.FirstOrDefault();
    }

    /// <summary>
    /// Gets the customer's packname and its list when the list belongs to a branch - the packname that has to stay in
    /// the customer's branch. A packname in a store-wide list (no branch) is in step whatever the customer's branch
    /// </summary>
    /// <param name="customer">The customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packname and its list, or nulls when the customer has none in a branch list
    /// </returns>
    protected virtual async Task<(PackName PackName, PackNameList PackNameList)> GetBranchPackNameAsync(Customer customer)
    {
        var packName = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);

        if (packName == null)
            return (null, null);

        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

        if (packNameList?.BranchId == null || packNameList.Deleted)
            return (null, null);

        return (packName, packNameList);
    }

    /// <summary>
    /// Builds the packname a customer gets when one is created for them: named after the customer (their e-mail when
    /// they have no name), printing the first name, the customer's gender or "I" when they have none, a random colour
    /// </summary>
    /// <param name="packNameList">The list the packname goes into</param>
    /// <param name="customer">The customer</param>
    /// <returns>The packname, not yet inserted</returns>
    protected virtual PackName BuildPackName(PackNameList packNameList, Customer customer)
    {
        var name = $"{customer.FirstName} {customer.LastName}".Trim();

        if (string.IsNullOrEmpty(name))
            name = !string.IsNullOrEmpty(customer.Email) ? customer.Email : customer.Username ?? string.Empty;

        return new PackName
        {
            PackNameListId = packNameList.Id,
            Name = name,
            PrintName = customer.FirstName,
            CustomerId = customer.Id,
            Group = false,
            Active = true,
            Gender = string.IsNullOrEmpty(customer.Gender) ? "I" : customer.Gender,
            Colour = $"#{Random.Shared.Next(0x1000000):X6}"
        };
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns the branch's default active packname list, creating one when the branch has no active list
    /// </summary>
    /// <param name="branch">Branch</param>
    /// <param name="nameFrom">Where the name of a created list comes from</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch's default active list, or its first active list when none is marked default
    /// </returns>
    public virtual async Task<PackNameList> EnsureDefaultPackNameListAsync(Branch branch, PackNameListNameFrom nameFrom)
    {
        ArgumentNullException.ThrowIfNull(branch);

        //a branch whose lists are all switched off gets a new one
        var packNameList = await GetDefaultActivePackNameListAsync(branch);

        if (packNameList != null)
            return packNameList;

        var allLists = await _packNameService.GetPackNameListsByBranchIdAsync(branch.Id);

        packNameList = new PackNameList
        {
            Name = nameFrom == PackNameListNameFrom.Name ? branch.Name : branch.BranchCode,
            BranchId = branch.Id,
            //a branch has one default list, switched off or not
            IsDefault = !allLists.Any(pnl => pnl.IsDefault),
            StoreId = branch.StoreId,
            CreatedOnUtc = DateTime.UtcNow,
            Active = true
        };

        await _packNameService.InsertPackNameListAsync(packNameList);

        return packNameList;
    }

    /// <summary>
    /// Creates a packname in the list for every customer of the list's branch who has none
    /// </summary>
    /// <param name="packNameList">Packname list</param>
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

    /// <summary>
    /// Applies the store's automation to a branch that has just been created: when the store uses branches as packname
    /// lists, the branch gets its default list
    /// </summary>
    /// <param name="branch">The new branch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task ProvisionNewBranchAsync(Branch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, branch.StoreId))
            return;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(branch.StoreId);

        if (!storeSettings.UseBranchesAsPackNameList)
            return;

        await EnsureDefaultPackNameListAsync(branch, (PackNameListNameFrom)storeSettings.PackNameListNameFrom);
    }

    /// <summary>
    /// Applies the store's automation to a customer who has just been linked to a branch: when the store uses staff as
    /// packnames and the customer has none, one is created in the branch's default list, which is created when the
    /// branch has none
    /// </summary>
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

    public virtual async Task<string> CheckCustomerBranchChangeAsync(Customer customer, int branchId)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var (packName, packNameList) = await GetBranchPackNameAsync(customer);

        //with the feature off the packname screens are closed: nothing moves, so nothing stops the customer's save
        if (packName == null || !await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, packNameList.StoreId))
            return null;

        if (branchId <= 0)
            return string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameNeedsBranch"), packName.Name);

        if (packNameList.BranchId == branchId)
            return null;

        var branch = await _branchService.GetBranchByIdAsync(branchId);

        if (branch == null || branch.Deleted)
            return null;

        //the packname's department, cost centre and subsidies belong to its store, so it cannot follow the customer out
        if (branch.StoreId != packNameList.StoreId)
            return string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameOtherStore"), packName.Name, branch.Name);

        //a branch without an active list gets a new, empty one, where the name is free
        var targetList = await GetDefaultActivePackNameListAsync(branch);

        if (targetList == null || !await _packNameService.PackNameNameExistsAsync(targetList.Id, packName.Name, packName.Id))
            return null;

        return string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Name.Exists"), packName.Name, branch.Name);
    }

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

    public virtual async Task SetCustomerBranchFromPackNameAsync(PackName packName)
    {
        ArgumentNullException.ThrowIfNull(packName);

        if (!packName.IsCustomer || packName.Deleted)
            return;

        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

        if (packNameList?.BranchId is not int branchId)
            return;

        //CustomerBranch is keyed by the customer id; a customer created outside the branch flow has no row yet
        var customerBranch = await _customerService.GetCustomerBranchByIdAsync(packName.CustomerId.Value);

        if (customerBranch == null)
        {
            await _customerService.InsertCustomerBranchAsync(new CustomerBranch
            {
                Id = packName.CustomerId.Value,
                BranchId = branchId,
                LastModifiedDateUtc = DateTime.UtcNow
            });

            return;
        }

        if (customerBranch.BranchId == branchId)
            return;

        customerBranch.BranchId = branchId;
        customerBranch.LastModifiedDateUtc = DateTime.UtcNow;
        await _customerService.UpdateCustomerBranchAsync(customerBranch);
    }

    public virtual async Task SetCustomerBranchesFromPackNameListAsync(PackNameList packNameList)
    {
        ArgumentNullException.ThrowIfNull(packNameList);

        if (packNameList.BranchId == null || packNameList.Deleted)
            return;

        foreach (var packName in await _packNameService.GetPackNamesByListIdAsync(packNameList.Id))
            await SetCustomerBranchFromPackNameAsync(packName);
    }

    #endregion
}