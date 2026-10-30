using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Services.Configuration;

namespace Nop.Plugin.CW.Core.Services.Branches;

public class BranchService : IBranchService
{
    #region Fields

    protected readonly IRepository<Branch> _branchRepository;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IRepository<Customer> _customerRepository;
    protected readonly IRepository<Address> _addressRepository;
    protected readonly IRepository<Store> _storeRepository;
    protected readonly IRepository<CustomerBranch> _customerBranchRepository;
    protected readonly IRepository<BranchAddressMapping> _branchAddressMappingRepository;
    protected readonly IShortTermCacheManager _shortTermCacheManager;
    protected readonly IRepository<PackNameList> _packNameListRepository;
    protected readonly IRepository<CostCenter> _costCenterRepository;
    protected readonly ISettingService _settingService;

    #endregion

    #region Ctor

    public BranchService(IRepository<Branch> branchRepository,
        IStaticCacheManager staticCacheManager,
        IRepository<Customer> customerRepository,
        IRepository<Address> addressRepository,
        IRepository<Store> storeRepository,
        IRepository<CustomerBranch> customerBranchRepository,
        IRepository<BranchAddressMapping> branchAddressMappingRepository,
        IShortTermCacheManager shortTermCacheManager,
        IRepository<PackNameList> packNameListRepository,
        IRepository<CostCenter> costCenterRepository,
        ISettingService settingService)
    {
        _branchRepository = branchRepository;
        _staticCacheManager = staticCacheManager;
        _customerRepository = customerRepository;
        _addressRepository = addressRepository;
        _storeRepository = storeRepository;
        _customerBranchRepository = customerBranchRepository;
        _branchAddressMappingRepository = branchAddressMappingRepository;
        _shortTermCacheManager = shortTermCacheManager;
        _packNameListRepository = packNameListRepository;
        _costCenterRepository = costCenterRepository;
        _settingService = settingService;
    }

    #endregion

    #region Utilities

    private static List<string> GetShippingMethodList(string shippingMethods)
    {
        if (string.IsNullOrWhiteSpace(shippingMethods))
            return new List<string>();

        return shippingMethods.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    #endregion

    #region Methods

    #region Stores

    public virtual async Task<IPagedList<(Store Store, int BranchCount)>> GetAllStoresWithBranchCountAsync(string name = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = _storeRepository.Table
        .Where(s => !s.Deleted && (string.IsNullOrEmpty(name) || s.Name.Contains(name)))
        .Select(s => new
        {
            Store = s,
            BranchCount = _branchRepository.Table.Count(b => b.StoreId == s.Id && !b.Deleted)
        })
        .OrderBy(x => x.Store.DisplayOrder).ThenBy(x => x.Store.Id);

        var pagedList = await query.ToPagedListAsync(pageIndex, pageSize);

        var result = pagedList.Select(x => (x.Store, x.BranchCount)).ToList();

        return new PagedList<(Store, int)>(result, pagedList.PageIndex, pagedList.PageSize, pagedList.TotalCount);
    }

    #endregion

    #region Branches

    /// <summary>
    /// Gets a branch
    /// </summary>
    /// <param name="branchId">Branch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task<Branch> GetBranchByIdAsync(int branchId)
    {
        return await _branchRepository.GetByIdAsync(branchId, cache => default);
    }

    /// <summary>
    /// Gets a branch by customer identifier
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task<Branch> GetBranchByCustomerIdAsync(int customerId)
    {
        if (customerId == 0)
            return null;

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(BranchServicesDefaults.BranchByCustomerIdCacheKey, customerId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var query = from cb in _customerBranchRepository.Table
                        join b in _branchRepository.Table on cb.BranchId equals b.Id
                        where cb.Id == customerId && !b.Deleted
                        select b;

            return await query.FirstOrDefaultAsync();
        });
    }

    /// <summary>
    /// Gets all branches
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branches
    /// </returns>
    public virtual async Task<IList<Branch>> GetAllBranchesByStoreIdAsync(int storeId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(BranchServicesDefaults.BranchesByStoreCacheKey, storeId);

        return await _staticCacheManager.GetAsync(key, async () =>
            await _branchRepository.GetAllAsync(
                q => q.Where(b => b.StoreId == storeId && !b.Deleted),
                includeDeleted: false
            )
        );
    }

    /// <summary>
    /// Gets branches by store identifier
    /// </summary>
    /// <param name="storeId">The store identifier</param>
    /// <param name="branchName">The branch name</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branches
    /// </returns>
    public virtual async Task<IPagedList<Branch>> GetBranchesByStoreIdAsync(int storeId, string branchName,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        if (storeId == 0)
            return new PagedList<Branch>([], pageIndex, pageSize);

        var query = from b in _branchRepository.Table
                    where b.StoreId == storeId && !b.Deleted &&
                            (string.IsNullOrEmpty(branchName) || b.Name.Contains(branchName))
                    orderby b.Name
                    select b;

        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets branches by store identifier
    /// </summary>
    /// <param name="storeId">The store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branches
    /// </returns>
    public virtual async Task<IPagedList<Branch>> GetBranchesByStoreIdAsync(int storeId,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var allBranches = await GetAllBranchesByStoreIdAsync(storeId);

        var sorted = SortBranchesForTree(
            allBranches.ToLookup(b => b.ParentBranchId ?? 0)
        ).ToList();

        return new PagedList<Branch>(sorted, pageIndex, pageSize);
    }

    /// <summary>
    /// Sort branches for tree representation
    /// </summary>
    /// <param name="branchesByParentId">Branches for sort</param>
    /// <param name="parentId">Parent branch identifier</param>
    /// <param name="ignoreBranchesWithoutExistingParent">A value indicating whether branches without parent branch in provided branch list (source) should be ignored</param>
    /// <returns>
    /// An enumerable containing the sorted branches
    /// </returns>
    public virtual IEnumerable<Branch> SortBranchesForTree(
        ILookup<int, Branch> branchesByParentId,
        int parentId = 0,
        bool ignoreBranchesWithoutExistingParent = false)
    {
        ArgumentNullException.ThrowIfNull(branchesByParentId);

        // Track all keys seen during traversal for orphan detection
        var allKeys = ignoreBranchesWithoutExistingParent
            ? null
            : branchesByParentId.Select(g => g.Key).ToHashSet();

        // Iterative depth-first traversal using an explicit stack
        // Stack holds (branchId, children) pairs to process
        var result = new List<Branch>();
        var visited = new HashSet<int>();

        // Seed with root-level branches (parentId = 0 or given parentId)
        var rootBranches = branchesByParentId[parentId]
            .OrderBy(b => b.Name)
            .ThenBy(b => b.Id)
            .ToList();

        // Use a stack of (ordered children list, current index) for iterative DFS
        var stack = new Stack<(List<Branch> siblings, int index)>();
        stack.Push((rootBranches, 0));

        while (stack.Count > 0)
        {
            var (siblings, index) = stack.Pop();

            if (index >= siblings.Count)
                continue;

            // Push the remainder of this sibling list back first
            stack.Push((siblings, index + 1));

            var branch = siblings[index];

            if (visited.Contains(branch.Id))
                continue; // prevent cycles

            visited.Add(branch.Id);
            allKeys?.Remove(branch.Id);
            result.Add(branch);

            // Push children so they're processed next (depth-first)
            var children = branchesByParentId[branch.Id]
                .OrderBy(b => b.Name)
                .ThenBy(b => b.Id)
                .ToList();

            if (children.Count > 0)
                stack.Push((children, 0));
        }

        if (!ignoreBranchesWithoutExistingParent && allKeys?.Count > 0)
        {
            // Append orphans — branches whose parent wasn't in the source set
            var orphans = allKeys
                .SelectMany(id => branchesByParentId[id])
                .Where(b => !visited.Contains(b.Id))
                .OrderBy(b => b.ParentBranchId ?? 0)
                .ThenBy(b => b.Name)
                .ThenBy(b => b.Id);

            result.AddRange(orphans);
        }

        return result;
    }

    /// <summary>
    /// Get formatted branch breadcrumb 
    /// Note: ACL and store mapping is ignored
    /// </summary>
    /// <param name="storeId">The store identifier</param>
    /// <param name="branch">Branch</param>
    /// <param name="allBranches">All branches</param>
    /// <param name="separator">Separator</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the formatted breadcrumb
    /// </returns>
    public virtual async Task<string> GetFormattedBreadCrumbAsync(int storeId, Branch branch, IList<Branch> allBranches,
        string separator = ">>")
    {
        ArgumentNullException.ThrowIfNull(allBranches);

        var result = string.Empty;

        var breadcrumb = await GetBranchBreadCrumbAsync(storeId, branch, allBranches);
        for (var i = 0; i <= breadcrumb.Count - 1; i++)
        {
            var branchName = breadcrumb[i].Name;
            result = string.IsNullOrEmpty(result) ? branchName : $"{result} {separator} {branchName}";
        }

        return result;
    }

    /// <summary>
    /// Get branch breadcrumb 
    /// </summary>
    /// <param name="storeId">The store identifier</param>
    /// <param name="branch">Branch</param>
    /// <param name="allBranches">All branches</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch breadcrumb 
    /// </returns>
    public virtual async Task<IList<Branch>> GetBranchBreadCrumbAsync(int storeId, Branch branch, IList<Branch> allBranches)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(allBranches);

        var key = _staticCacheManager.PrepareKeyForDefaultCache(BranchServicesDefaults.BranchBreadcrumbCacheKey, branch);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            var allBranchesById = allBranches
            .DistinctBy(b => b.Id)
            .ToDictionary(b => b.Id);

            //use a local variable, so we don't mutate the parameter captured by the closure
            var current = branch;
            var result = new List<Branch>();
            var seen = new HashSet<int>();

            while (current != null && !current.Deleted && seen.Add(current.Id))
            {
                result.Add(current);
                allBranchesById.TryGetValue(current.ParentBranchId ?? 0, out current);
            }

            result.Reverse();
            return result;
        });
    }

    /// <summary>
    /// Determines asynchronously whether the specified branch has any child branches, associated pack name lists, or
    /// cost centers.
    /// </summary>
    /// <param name="branchId">The unique identifier of the branch to check for child branches, pack name lists, or cost centers.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the branch
    /// has at least one child branch, pack name list, or cost center; otherwise, <see langword="false"/>.</returns>
    public virtual async Task<bool> BranchHasChildrenAsync(int branchId)
    {
        var hasChildrenOrPackNameListOrCostCenter = await (
            from id in
                (from b in _branchRepository.Table
                 where b.ParentBranchId == branchId && !b.Deleted
                 select b.Id)
                .Union(
                    from pnl in _packNameListRepository.Table
                    where pnl.BranchId == branchId && !pnl.Deleted
                    select pnl.Id
                )
                .Union(
                    from cc in _costCenterRepository.Table
                    where cc.BranchId == branchId && !cc.Deleted
                    select cc.Id
                )
            select id
        ).AnyAsync();

        return hasChildrenOrPackNameListOrCostCenter;
    }

    /// <summary>
    /// Asynchronously retrieves the number of staff members associated with the specified branch.
    /// </summary>
    /// <remarks>The result is cached to improve performance on subsequent calls for the same branch
    /// identifier.</remarks>
    /// <param name="branchId">The unique identifier of the branch for which to count staff members. Must be a non-zero value.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of staff members in the
    /// specified branch. Returns 0 if the branch identifier is 0.</returns>
    public virtual async Task<int> GetStaffCountAsync(int branchId)
    {
        if (branchId == 0)
            return 0;

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(BranchServicesDefaults.BranchStaffCountCacheKey, branchId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            return await (
                from cb in _customerBranchRepository.Table
                join c in _customerRepository.Table on cb.Id equals c.Id
                where cb.BranchId == branchId && !c.Deleted
                select cb
            ).CountAsync();
        });
    }

    public async Task InsertBranchAsync(Branch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        await _branchRepository.InsertAsync(branch);
    }

    public async Task UpdateBranchAsync(Branch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        branch.UpdatedOnUtc = DateTime.UtcNow;

        await _branchRepository.UpdateAsync(branch);
    }

    public async Task DeleteBranchAsync(Branch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        await _branchRepository.DeleteAsync(branch);
    }

    #endregion

    #region Branch address mapping

    /// <summary>
    /// Remove a branch-address mapping record
    /// </summary>
    /// <param name="branch">Branch</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task<bool> RemoveBranchAddressAsync(Branch branch, Address address)
    {
        ArgumentNullException.ThrowIfNull(branch);

        if (await _branchAddressMappingRepository.Table
                .FirstOrDefaultAsync(m => m.AddressId == address.Id && m.BranchId == branch.Id)
            is BranchAddressMapping mapping)
        {
            await _branchAddressMappingRepository.DeleteAsync(mapping);

            // Clear the default if it was this address
            if (branch.DefaultAddressId == address.Id)
                await SetDefaultBranchAddressAsync(branch, null);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Inserts a branch-address mapping record
    /// </summary>
    /// <param name="branch">Branch</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertBranchAddressAsync(Branch branch, Address address)
    {
        ArgumentNullException.ThrowIfNull(branch);

        ArgumentNullException.ThrowIfNull(address);

        if (await _branchAddressMappingRepository.Table
                .FirstOrDefaultAsync(m => m.AddressId == address.Id && m.BranchId == branch.Id)
            is null)
        {
            var mapping = new BranchAddressMapping
            {
                AddressId = address.Id,
                BranchId = branch.Id
            };

            await _branchAddressMappingRepository.InsertAsync(mapping);
        }
    }

    /// <summary>
    /// Gets a list of addresses mapped to branchId
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<IList<Address>> GetAddressesByBranchIdAsync(int branchId)
    {
        var query = from address in _addressRepository.Table
                    join bam in _branchAddressMappingRepository.Table on address.Id equals bam.AddressId
                    where bam.BranchId == branchId
                    orderby address.CreatedOnUtc
                    select address;

        return await _shortTermCacheManager.GetAsync(async () => await query.ToListAsync(), BranchServicesDefaults.BranchAddressesCacheKey, branchId);
    }

    /// <summary>
    /// Gets a address mapped to branch
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <param name="addressId">Address identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<Address> GetBranchAddressAsync(int branchId, int addressId)
    {
        if (branchId == 0 || addressId == 0)
            return null;

        var query = from address in _addressRepository.Table
                    join cam in _branchAddressMappingRepository.Table on address.Id equals cam.AddressId
                    where cam.BranchId == branchId && address.Id == addressId
                    select address;

        return await _shortTermCacheManager.GetAsync(async () => await query.FirstOrDefaultAsync(), BranchServicesDefaults.BranchAddressCacheKey, branchId, addressId);
    }

    /// <summary>
    /// Gets the main shipping address mapped to branch
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<Address> GetDefaultShipAddressByBranchIdAsync(int branchId)
    {
        if (branchId == 0)
            return null;

        var query = from branch in _branchRepository.Table
                    join bam in _branchAddressMappingRepository.Table on branch.Id equals bam.BranchId
                    join address in _addressRepository.Table on bam.AddressId equals address.Id
                    where branch.Id == branchId
                    orderby (branch.DefaultAddressId == bam.AddressId ? 0 : 1), address.CreatedOnUtc
                    select address;

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Sets or clears the default shipping address for a branch.
    /// Pass null address to clear the default.
    /// </summary>
    public virtual async Task SetDefaultBranchAddressAsync(Branch branch, Address? address)
    {
        ArgumentNullException.ThrowIfNull(branch);

        // If setting a default, verify the address is actually mapped to this branch
        if (address is not null && !await _branchAddressMappingRepository.Table.AnyAsync(m => m.BranchId == branch.Id && m.AddressId == address.Id))
        {
            return; // address not mapped to this branch
        }

        branch.DefaultAddressId = address?.Id;

        await UpdateBranchAsync(branch);
    }

    #endregion

    #region Shipping Methods

    public async Task<List<string>> GetShippingMethodsByBranchId(int branchId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(BranchServicesDefaults.DefaultShippingMethodsByBranchCacheKey, branchId);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            var query = from b in _branchRepository.Table
                        where b.Id == branchId && !b.Deleted
                        select b.ShippingMethod;

            var shippingMethods = await query.FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(shippingMethods))
                return GetShippingMethodList(shippingMethods);

            shippingMethods = await GetShippingMethodsByCardFileIdFromStoreSettings(branchId);

            return GetShippingMethodList(shippingMethods);
        });
    }

    public async Task<string> GetShippingMethodsByCardFileIdFromStoreSettings(int branchId)
    {
        var branch = await GetBranchByIdAsync(branchId);
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(branch.StoreId);

        return storeSettings?.ShippingMethod;
    }

    #endregion

    #endregion
}
