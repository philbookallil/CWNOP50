using LinqToDB.Data;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Customers.DTOs;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.MultiStores.DTOs;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Configuration;
using Nop.Services.Localization;

namespace Nop.Plugin.CW.Core.Services.MultiStores;

public class MultiStoreService : IMultiStoreService
{
    #region Fields

    protected readonly IRepository<Department> _departmentRepository;
    protected readonly IRepository<DepartmentManagerMapping> _departmentManagerRepository;
    protected readonly IRepository<CostCenter> _costCenterRepository;
    protected readonly IRepository<PackName> _packNameRepository;
    protected readonly IRepository<PackNameList> _packNameListRepository;
    protected readonly IRepository<Branch> _branchRepository;
    protected readonly IRepository<Customer> _customerRepository;
    protected readonly IRepository<CustomerBranch> _customerBranchRepository;
    protected readonly IRepository<PackNameListManagerMapping> _packNameListManagerRepository;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IRepository<CostCenterAddressMapping> _costCenterAddressMappingRepository;
    protected readonly IRepository<Address> _addressRepository;
    protected readonly IShortTermCacheManager _shortTermCacheManager;
    protected readonly IPackNameService _packNameService;
    protected readonly ISettingService _settingService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INopDataProvider _dataProvider;
    protected readonly IBranchService _branchService;

    #endregion

    #region Ctor

    public MultiStoreService(IRepository<Department> departmentRepository,
        IRepository<DepartmentManagerMapping> departmentManagerRepository,
        IRepository<CostCenter> costCenterRepository,
        IRepository<PackName> packNameRepository,
        IRepository<PackNameList> packNameListRepository,
        IRepository<Branch> branchRepository,
        IRepository<Customer> customerRepository,
        IRepository<CustomerBranch> customerBranchRepository,
        IRepository<PackNameListManagerMapping> packNameListManagerRepository,
        IStaticCacheManager staticCacheManager,
        IRepository<CostCenterAddressMapping> costCenterAddressMappingRepository,
        IRepository<Address> addressRepository,
        IShortTermCacheManager shortTermCacheManager,
        IPackNameService packNameService,
        ISettingService settingService,
        ILocalizationService localizationService,
        INopDataProvider dataProvider,
        IBranchService branchService)
    {
        _departmentRepository = departmentRepository;
        _departmentManagerRepository = departmentManagerRepository;
        _costCenterRepository = costCenterRepository;
        _packNameRepository = packNameRepository;
        _packNameListRepository = packNameListRepository;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _customerBranchRepository = customerBranchRepository;
        _packNameListManagerRepository = packNameListManagerRepository;
        _staticCacheManager = staticCacheManager;
        _costCenterAddressMappingRepository = costCenterAddressMappingRepository;
        _addressRepository = addressRepository;
        _shortTermCacheManager = shortTermCacheManager;
        _packNameService = packNameService;
        _settingService = settingService;
        _localizationService = localizationService;
        _dataProvider = dataProvider;
        _branchService = branchService;
    }

    #endregion

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

    /// <summary>
    /// Whether a selector entry holds at least one packname this manager can see.
    /// </summary>
    /// <remarks>
    /// Follows the three routes of PackNameModelFactory.PreparePackNamesModelForManagerAsync with the same
    /// cached readers: a virtual department entry, a list the manager owns, and a list reached only through a
    /// department. If the roster changes how it resolves a list, this changes with it.
    /// </remarks>
    /// <param name="list">The selector entry</param>
    /// <param name="managerListId">The identifier of the manager's own list</param>
    /// <param name="ownedListIds">The identifiers of the lists the manager owns</param>
    /// <param name="departmentIds">The identifiers of the manager's departments</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true when the entry loads at least one packname.</returns>
    protected virtual async Task<bool> HasVisiblePackNamesAsync(PackNameList list, int managerListId, IList<int> ownedListIds, List<int> departmentIds)
    {
        //The manager's own list always stays: it is how they order for themselves.
        if (list.Id == managerListId)
            return true;

        //A virtual department entry: the negative id carries the department id.
        if (list.Id < 0)
            return (await _packNameService.GetActivePackNamesByListIdForDepartmentsAsync(0, new List<int> { -list.Id })).Count > 0;

        //A list the manager owns: every active packname in it is theirs to see.
        if (ownedListIds.Contains(list.Id))
            return (await _packNameService.GetActivePackNamesByListIdAsync(list.Id)).Count > 0;

        //A list reached only through a department: only that department's packnames are visible in it.
        return (await _packNameService.GetActivePackNamesByListIdForDepartmentsAsync(list.Id, departmentIds)).Count > 0;
    }

    #endregion

    #region Methods

    #region Departments

    /// <summary>
    /// Gets a department
    /// </summary>
    /// <param name="departmentId">Department identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task<Department> GetDepartmentByIdAsync(int departmentId)
    {
        return await _departmentRepository.GetByIdAsync(departmentId, cache => default);
    }

    /// <summary>
    /// Gets all departments
    /// </summary>
    /// <returns></returns>
    public virtual async Task<IList<Department>> GetAllDepartmentsAsync()
    {
        return await _departmentRepository.GetAllAsync(query =>
        {
            return query.Where(d => !d.Deleted).OrderBy(d => d.Name);
        }, cache => default);
    }

    /// <summary>
    /// Gets departments by store with searching and paging
    /// </summary>
    /// <param name="storeId"></param>
    /// <param name="searchName"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    public virtual async Task<IPagedList<Department>> GetDepartmentsByStoreIdAsync(int storeId, string searchName = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        return await _departmentRepository.GetAllPagedAsync(query =>
        {
            query = query.Where(d => d.StoreId == storeId && !d.Deleted).OrderBy(d => d.Name);

            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(d => d.Name.Contains(searchName));
            }

            return query;
        }, pageIndex, pageSize);
    }

    /// <summary>
    /// Inserts a department
    /// </summary>
    /// <param name="department">Department</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertDepartmentAsync(Department department)
    {
        await _departmentRepository.InsertAsync(department);
    }

    /// <summary>
    /// Updates a department
    /// </summary>
    /// <param name="department">Department</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task UpdateDepartmentAsync(Department department)
    {
        await _departmentRepository.UpdateAsync(department);
    }

    /// <summary>
    /// Deletes a department
    /// </summary>
    /// <param name="department">Department</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task DeleteDepartmentAsync(Department department)
    {
        await _departmentRepository.DeleteAsync(department);
    }

    /// <summary>
    /// Find managers mapped to the department
    /// </summary>
    /// <param name="departmentId">Department identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the managers
    /// </returns>
    public virtual async Task<IPagedList<PackNameDTO>> GetManagersByDepartmentIdAsync(int departmentId,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = from dm in _departmentManagerRepository.Table
                    join pn in _packNameRepository.Table on dm.PackNameId equals pn.Id
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    //INNER JOIN ON PURPOSE — see GetManagersNotInDepartmentAsync: a manager is
                    //scoped to a store through its branch, so a branchless list cannot supply one.
                    join b in _branchRepository.Table on pnl.BranchId equals b.Id
                    join dep in _departmentRepository.Table on dm.DepartmentId equals dep.Id
                    join user in _customerRepository.Table on pn.CustomerId equals user.Id into gUser
                    from user in gUser.DefaultIfEmpty()
                        //user != null ON PURPOSE: a manager needs a customer to log in and order. Stated
                        //rather than relying on `!user.Deleted` being NULL for a missing customer.
                    where dm.DepartmentId == departmentId && pn.Manager && !pn.Deleted && !pnl.Deleted && !b.Deleted && !dep.Deleted && user != null && !user.Deleted
                    orderby pn.Name
                    select new PackNameDTO
                    {
                        Id = pn.Id,
                        CustomerId = pn.CustomerId,
                        Name = pn.Name,
                        PrintName = pn.PrintName ?? string.Empty,
                        EmployeeNumber = pn.EmployeeNumber ?? string.Empty,
                        POEntry = pn.POEntry ?? string.Empty,
                        Group = pn.Group,
                        Gender = pn.Gender ?? string.Empty,
                        Active = pn.Active,
                        Branch = b.Name,
                        Email = user != null ? user.Email : string.Empty
                    };

        //paging
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Find departments mapped to a manager
    /// </summary>
    /// <param name="managerId">Manager identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the departments
    /// </returns>
    public virtual async Task<IList<Department>> GetDepartmentsByManagerIdAsync(int managerId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.DepartmentsByManagerCacheKey, managerId);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            //STORE-FILTERED, and the store is derived from the manager - so the cache key needs no new
            //parameter. A department in another store put that store's packnames into this manager's
            //OWNERSHIP set (this route bypasses the list SP entirely), made them editable and
            //top-uppable, and listed that store's employees in this store's selector.
            //The write side is closed where the mapping is saved; this is the read side.
            var managerStoreId = await GetManagerStoreIdAsync(managerId);

            if (managerStoreId == null)
                return new List<Department>();

            var query = from dm in _departmentManagerRepository.Table
                        join dep in _departmentRepository.Table on dm.DepartmentId equals dep.Id
                        join pn in _packNameRepository.Table on dm.PackNameId equals pn.Id
                        where dm.PackNameId == managerId && !pn.Deleted && pn.Manager && !dep.Deleted
                              && dep.StoreId == managerStoreId.Value
                        orderby dep.Name
                        select dep;

            return await query.ToListAsync();
        });
    }

    /// <summary>
    /// Find packname lists for packnames mapped to the departments
    /// </summary>
    /// <param name="departmentIds">Department identifiers</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packname lists
    /// </returns>
    public virtual async Task<IList<PackNameList>> GetPackNameListsByDepartmentIdsAsync(IList<int> departmentIds, int storeId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.PackNameListsByDepartmentIdsCacheKey, departmentIds, storeId);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            //STORE-FILTERED. Without this, a department mapping pointing at another store's department
            //(which nothing stopped being created) appended that store's packname lists to
            //this store's selector, and its employees were listed by name in the wrong store's UI.
            var query = from pn in _packNameRepository.Table
                        join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                        where pn.DepartmentId.HasValue && departmentIds.Contains(pn.DepartmentId.Value) &&
                        !pn.Deleted && pn.Active && !pnl.Deleted && pnl.Active && pnl.StoreId == storeId
                        select pnl;

            return await query.ToListAsync();
        });
    }

    /// <summary>
    /// Gets the managers from the store that have not been already mapped to the department
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="departmentId">Department identifier</param>
    /// <param name="name">Search name</param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    public async Task<IPagedList<PackNameDTO>> GetManagersNotInDepartmentAsync(int storeId, int departmentId, string name = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var mapped = from dm in _departmentManagerRepository.Table
                     where dm.DepartmentId == departmentId
                     select dm.PackNameId;

        var query = from pn in _packNameRepository.Table
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    //INNER JOIN ON PURPOSE: a manager is scoped to a store, and the branch is what
                    //carries the store (b.StoreId below). A packname whose list has no branch cannot
                    //be scoped, so it must not be selectable as a manager.
                    join b in _branchRepository.Table on pnl.BranchId equals b.Id
                    join user in _customerRepository.Table on pn.CustomerId equals user.Id into gUser
                    from user in gUser.DefaultIfEmpty()
                        //user != null ON PURPOSE: a manager orders on behalf of the packnames they own,
                        //so they have to be able to log in — which means a customer. The left join stays
                        //because the DTO reads the customer's name and e-mail below; the rule is stated
                        //here rather than left to `!user.Deleted` evaluating to NULL for a missing
                        //customer, which excluded the same rows by accident and read like the bug that
                        //`!b.Deleted` on a left-joined branch actually was (see PackNameService).
                    where !pn.Deleted && !pnl.Deleted && !b.Deleted && user != null && !user.Deleted && b.StoreId == storeId && !mapped.Contains(pn.Id) && pn.Manager
                    &&
                    (string.IsNullOrEmpty(name) ||
                    pn.Name.Contains(name) || (pn.PrintName ?? string.Empty).Contains(name) || (pn.EmployeeNumber ?? string.Empty).Contains(name) ||
                    (user != null && (user.FirstName.Contains(name) || user.LastName.Contains(name) || user.Email.Contains(name))))
                    orderby pn.Name
                    select new PackNameDTO
                    {
                        Id = pn.Id,
                        CustomerId = pn.CustomerId,
                        Name = pn.Name,
                        PrintName = pn.PrintName ?? string.Empty,
                        EmployeeNumber = pn.EmployeeNumber ?? string.Empty,
                        POEntry = pn.POEntry ?? string.Empty,
                        Group = pn.Group,
                        Gender = pn.Gender ?? string.Empty,
                        Active = pn.Active,
                        Branch = b.Name,
                        Email = user != null ? user.Email : string.Empty
                    };

        //paging
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Find managers mapped to the department
    /// </summary>
    /// <param name="departmentId">Department identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the managers
    /// </returns>
    public virtual async Task<List<DepartmentManagerMapping>> GetDepartmentManagersAsync(int departmentId)
    {
        var query = from dm in _departmentManagerRepository.Table
                    join pn in _packNameRepository.Table on dm.PackNameId equals pn.Id
                    where dm.DepartmentId == departmentId && !pn.Deleted
                    select dm;

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets department manager mapping
    /// </summary>
    /// <param name="departmentId">Department identifier</param>
    /// <param name="managerId">Manager identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mapping
    /// </returns>
    public async Task<DepartmentManagerMapping> GetDepartmentManagerByIdAsync(int departmentId, int managerId)
    {
        if (departmentId == 0 || managerId == 0)
            return null;

        var query = from dm in _departmentManagerRepository.Table
                    where dm.DepartmentId == departmentId && dm.PackNameId == managerId
                    select dm;

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Inserts a department manager mapping
    /// </summary>
    /// <param name="departmentManager">Department manager mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertDepartmentManagerAsync(DepartmentManagerMapping departmentManager)
    {
        await _departmentManagerRepository.InsertAsync(departmentManager);
    }

    /// <summary>
    /// Deletes a department manager mapping
    /// </summary>
    /// <param name="departmentManager">Department manager mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task DeleteDepartmentManagerAsync(DepartmentManagerMapping departmentManager)
    {
        await _departmentManagerRepository.DeleteAsync(departmentManager);
    }

    #endregion

    #region Cost Centers

    /// <summary>
    /// Gets a cost center
    /// </summary>
    /// <param name="costCenterId">Cost center identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task<CostCenter> GetCostCenterByIdAsync(int costCenterId)
    {
        return await _costCenterRepository.GetByIdAsync(costCenterId, cache => default);
    }

    /// <summary>
    /// Get cost centers by store and branch with searching and paging
    /// </summary>
    /// <param name="storeId"></param>
    /// <param name="branchId"></param>
    /// <param name="searchName"></param>
    /// <param name="searchBranchName"></param>
    /// <param name="searchInBranches"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    public virtual async Task<IPagedList<CostCenterDTO>> GetCostCentersAsync(int? storeId, int? branchId, string searchName = null, string searchBranchName = null, bool searchInBranches = false,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        //If branchId is specified, search in branches should be true otherwise the query will not return any result
        if (branchId.HasValue)
            searchInBranches = true;

        var query = from cc in _costCenterRepository.Table
                    join b in _branchRepository.Table on cc.BranchId equals b.Id into gb
                    from b in gb.DefaultIfEmpty()
                    where !cc.Deleted &&
                            (storeId == null || cc.StoreId == storeId) &&
                            (branchId == null || cc.BranchId == branchId) &&
                            (string.IsNullOrEmpty(searchName) || cc.Name.Contains(searchName)) &&
                            (
                                (searchInBranches && (string.IsNullOrEmpty(searchBranchName) || (b != null && b.Name.Contains(searchBranchName)))) ||
                                (!searchInBranches && b == null)
                            )
                    orderby cc.Name
                    select new CostCenterDTO
                    {
                        Id = cc.Id,
                        Name = cc.Name,
                        Active = cc.Active,
                        StoreId = cc.StoreId,
                        BranchId = cc.BranchId,
                        Branch = b != null ? b.Name : string.Empty
                    };

        //paging
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets cost centers for a store as select list items
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains every non-deleted cost centre in the store, with its Active flag.
    /// </returns>
    /// <remarks>
    /// ACTIVE IS RETURNED, NOT FILTERED. Active means "no new assignments", so
    /// the caller decides: an assignment picker drops inactive rows, a SEARCH filter must keep them or
    /// the packnames already sitting on an inactive cost centre become unfindable. Filtering here would
    /// take that choice away from both.
    /// </remarks>
    public async Task<IList<(int Id, string Name, bool Active)>> GetCostCenterSelectItemsAsync(int storeId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            MultiStoreServicesDefaults.CostCenterSelectItemsByStoreCacheKey, storeId);

        return await _staticCacheManager.GetAsync(key, async () =>
            await (from cc in _costCenterRepository.Table
                   where !cc.Deleted && cc.StoreId == storeId
                   orderby cc.Name
                   select new { cc.Id, cc.Name, cc.Active })
                  .Select(x => ValueTuple.Create(x.Id, x.Name, x.Active))
                  .ToListAsync()
        );
    }

    /// <summary>
    /// Inserts a cost center
    /// </summary>
    /// <param name="costCenter">Cost center</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertCostCenterAsync(CostCenter costCenter)
    {
        await _costCenterRepository.InsertAsync(costCenter);
    }

    /// <summary>
    /// Updates a cost center
    /// </summary>
    /// <param name="costCenter">Cost center</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task UpdateCostCenterAsync(CostCenter costCenter)
    {
        await _costCenterRepository.UpdateAsync(costCenter);
    }

    /// <summary>
    /// Deletes a cost center
    /// </summary>
    /// <param name="costCenter">Cost center</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task DeleteCostCenterAsync(CostCenter costCenter)
    {
        await _costCenterRepository.DeleteAsync(costCenter);
    }

    #endregion

    #region Cost Center - Addresses

    /// <summary>
    /// Gets a list of addresses mapped to a cost center
    /// </summary>
    /// <param name="costCenterId">Cost center identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<IList<Address>> GetAddressesByCostCenterIdAsync(int costCenterId)
    {
        var query = from address in _addressRepository.Table
                    join cam in _costCenterAddressMappingRepository.Table on address.Id equals cam.AddressId
                    where cam.CostCenterId == costCenterId
                    select address;

        return await _shortTermCacheManager.GetAsync(async () => await query.ToListAsync(), MultiStoreServicesDefaults.CostCenterAddressesCacheKey, costCenterId);
    }

    /// <summary>
    /// Gets a address mapped to a cost center
    /// </summary>
    /// <param name="costCenterId">Cost center identifier</param>
    /// <param name="addressId">Address identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<Address> GetCostCenterAddressAsync(int costCenterId, int addressId)
    {
        if (costCenterId == 0 || addressId == 0)
            return null;

        var query = from address in _addressRepository.Table
                    join cam in _costCenterAddressMappingRepository.Table on address.Id equals cam.AddressId
                    where cam.CostCenterId == costCenterId && address.Id == addressId
                    select address;

        return await _shortTermCacheManager.GetAsync(async () => await query.FirstOrDefaultAsync(), MultiStoreServicesDefaults.CostCenterAddressCacheKey, costCenterId, addressId);
    }

    /// <summary>
    /// Inserts a cost center-address mapping record
    /// </summary>
    /// <param name="costCenter">CostCenter</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertCostCenterAddressAsync(CostCenter costCenter, Address address)
    {
        ArgumentNullException.ThrowIfNull(costCenter);

        ArgumentNullException.ThrowIfNull(address);

        if (await _costCenterAddressMappingRepository.Table
            .FirstOrDefaultAsync(m => m.AddressId == address.Id && m.CostCenterId == costCenter.Id)
            is null)
        {
            var mapping = new CostCenterAddressMapping
            {
                AddressId = address.Id,
                CostCenterId = costCenter.Id
            };

            await _costCenterAddressMappingRepository.InsertAsync(mapping);
        }
    }

    /// <summary>
    /// Remove a cost center-address mapping record
    /// </summary>
    /// <param name="costCenter">CostCenter</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task<bool> RemoveCostCenterAddressAsync(CostCenter costCenter, Address address)
    {
        ArgumentNullException.ThrowIfNull(costCenter);

        if (await _costCenterAddressMappingRepository.Table
            .FirstOrDefaultAsync(m => m.AddressId == address.Id && m.CostCenterId == costCenter.Id)
            is CostCenterAddressMapping mapping)
        {
            await _costCenterAddressMappingRepository.DeleteAsync(mapping);
            return true;
        }

        return false;
    }

    #endregion

    #region PackNames

    public async Task<IPagedList<PackNameDTO>> GetPackNameManagersByStoreAsync(int storeId, int branchId, int packNameListId = 0, string name = null, bool fromOtherBrances = false,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        //Get branches in the store
        var branchQuery = from b in _branchRepository.Table
                          where b.StoreId == storeId && !b.Deleted
                          select b;

        if (branchId > 0 && !fromOtherBrances)
        {
            branchQuery = from b in branchQuery
                          where b.Id == branchId
                          select b;
        }

        var mapped = from pnlm in _packNameListManagerRepository.Table
                     where pnlm.PackNameListId == packNameListId
                     select pnlm.PackNameId;

        var query = from pn in _packNameRepository.Table
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    join b in branchQuery on pnl.BranchId equals b.Id
                    join user in _customerRepository.Table on pn.CustomerId equals user.Id into gUser
                    from user in gUser.DefaultIfEmpty()
                    where !pn.Deleted
                        && !pnl.Deleted
                        && !mapped.Contains(pn.Id) && pn.Manager
                        && (string.IsNullOrEmpty(name)
                            || pn.Name.Contains(name)
                            || (pn.PrintName ?? string.Empty).Contains(name)
                            || (pn.EmployeeNumber ?? string.Empty).Contains(name)
                            || (user != null && (user.FirstName.Contains(name) || user.LastName.Contains(name) || user.Email.Contains(name))))
                    orderby pn.Name
                    select new PackNameDTO
                    {
                        Id = pn.Id,
                        CustomerId = pn.CustomerId,
                        Name = pn.Name,
                        PrintName = pn.PrintName ?? string.Empty,
                        EmployeeNumber = pn.EmployeeNumber ?? string.Empty,
                        POEntry = pn.POEntry ?? string.Empty,
                        Group = pn.Group,
                        Gender = pn.Gender ?? string.Empty,
                        Active = pn.Active,
                        Branch = b.Name,
                        Email = user != null ? user.Email : string.Empty
                    };

        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets all PackNameList ids available to a manager across all access paths.
    /// </summary>
    /// <param name="managerId">Manager identifier</param>
    /// <param name="requireActiveList">
    /// True (default) answers VISIBILITY - inactive lists excluded. False answers OWNERSHIP - inactive
    /// lists included. The flag is part of the cache key: two different
    /// answers must not share one entry.
    /// </param>
    public async Task<List<int>> GetPackNameListIdsByManagerIdAsync(int managerId, bool requireActiveList = true)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            MultiStoreServicesDefaults.PackNameListIdsByManagerCacheKey, managerId, requireActiveList ? 1 : 0);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            var results = await _dataProvider.QueryAsync<int>(
            $"EXEC [dbo].[{CWCoreDefaults.GetPackNameListIdsByManagerSPName}] @ManagerId, @RequireActiveList",
            new DataParameter("@ManagerId", managerId),
            new DataParameter("@RequireActiveList", requireActiveList ? 1 : 0));

            return results.ToList();
        });
    }

    /// <summary>
    /// Gets all PackName lists available to a manager across all access paths.
    /// </summary>
    public async Task<List<PackNameList>> GetPackNameListsByManagerIdAsync(int managerId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            MultiStoreServicesDefaults.PackNameListsByManagerCacheKey, managerId);

        var cached = await _staticCacheManager.GetAsync(key, async () =>
        {
            var ids = await GetPackNameListIdsByManagerIdAsync(managerId);

            if (ids.Count == 0)
                return new List<PackNameList>();

            return (await _packNameListRepository.GetAllAsync(
                q => q.Where(pnl => ids.Contains(pnl.Id)).OrderBy(pnl => pnl.Name)
            )).ToList();
        });

        //A COPY, always. MemoryCacheManager stores the reference, and GetAvailablePackNameListsAsync
        //appends to whatever it is handed - the manager's own list and one
        //virtual "(Dept)" entry per department. Handing out the cached instance meant every selector
        //render grew it, so the dropdown accumulated duplicates until the entry expired, and other
        //consumers then saw virtual lists with negative ids and a department id in BranchId.
        //4.6 projected before returning for the same reason.
        return cached.ToList();
    }

    /// <summary>
    /// Gets branches visible to a manager — only branches where the manager
    /// has at least one accessible PackNameList. Does NOT return all descendants.
    /// Includes intermediate ancestor branches needed for a coherent tree display.
    /// </summary>
    public async Task<IList<Branch>> GetVisibleBranchesForManagerAsync(int managerId)
    {
        var accessibleLists = await GetPackNameListsByManagerIdAsync(managerId);
        if (accessibleLists.Count == 0)
            return [];

        var directlyVisibleBranchIds = accessibleLists
            .Where(l => l.BranchId.HasValue)
            .Select(l => l.BranchId.Value)
            .ToHashSet();

        if (directlyVisibleBranchIds.Count == 0)
            return [];

        var storeId = await GetManagerStoreIdAsync(managerId);
        if (storeId == null)
            return [];

        var allStoreBranches = await _branchService.GetAllBranchesByStoreIdAsync(storeId.Value);
        var branchDict = allStoreBranches.ToDictionary(b => b.Id);

        // Enrich with ancestors for sort scaffolding only
        var enrichedSetForSorting = new HashSet<int>(directlyVisibleBranchIds);
        foreach (var branchId in directlyVisibleBranchIds)
        {
            var current = branchDict.GetValueOrDefault(branchId);
            while (current?.ParentBranchId != null)
            {
                if (!enrichedSetForSorting.Add(current.ParentBranchId.Value))
                    break;
                current = branchDict.GetValueOrDefault(current.ParentBranchId.Value);
            }
        }

        var sorted = _branchService.SortBranchesForTree(
            allStoreBranches
                .Where(b => enrichedSetForSorting.Contains(b.Id))
                .ToLookup(b => b.ParentBranchId ?? 0)
        );

        // Return only the directly visible ones, preserving depth-first order
        return sorted
            .Where(b => directlyVisibleBranchIds.Contains(b.Id))
            .ToList();
    }

    /// <summary>
    ///  Gets all PackName IDs a manager can act on behalf of, across all access paths:
    ///  PackNames in any list the manager controls (branch/flag/direct-assignment paths)
    ///  PackNames assigned to any department the manager is mapped to
    /// </summary>
    /// <param name="managerId">Manager identifier</param>
    /// <returns></returns>
    /// <remarks>
    /// This took the manager's list ids and department ids as optional narrowing arguments. They
    /// were the same false economy as on <see cref="IsPackNameOwnedByManagerId"/>:
    /// the entry is cached under <c>managerId</c> alone, so a caller passing a narrowed subset got
    /// whatever the first caller had cached — the parameters changed the query but not the key.
    /// Both are resolved here now.
    /// </remarks>
    public async Task<List<int>> GetPackNameIdsByManagerId(int managerId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.PackNamesByManagerCacheKey, managerId);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            // All packnames in the lists the manager controls
            var packNameListIds = await GetPackNameListIdsByManagerIdAsync(managerId);

            var fromLists = packNameListIds.Count > 0
                ? await (from pn in _packNameRepository.Table
                         where !pn.Deleted && pn.Active && packNameListIds.Contains(pn.PackNameListId)
                         select pn.Id).Distinct().ToListAsync()
                : new List<int>();

            // All packnames assigned to departments this manager manages
            var departmentIds = (await GetDepartmentsByManagerIdAsync(managerId)).Select(d => d.Id).ToList();

            var fromDepartments = departmentIds.Count > 0
                ? await (from pn in _packNameRepository.Table
                         where !pn.Deleted && pn.Active
                               && pn.DepartmentId.HasValue
                               && departmentIds.Contains(pn.DepartmentId.Value)
                         select pn.Id).Distinct().ToListAsync()
                : new List<int>();

            return fromLists.Union(fromDepartments).Distinct().ToList();
        });
    }

    /// <summary>
    /// The ids of every packname a manager owns, through the lists they control or the departments
    /// they manage. One cache entry per manager.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately <b>not</b> <see cref="GetPackNameIdsByManagerId"/>: that one filters
    /// <c>pn.Active</c> because it feeds listings, and ownership is not the same question as
    /// visibility. A deactivated packname is still its manager's to edit, so the guard's predicate
    /// is deleted-only — exactly what the old per-pair query used.
    /// </para>
    /// <para>
    /// Replaces one cache entry per (manager, packname) PAIR, which every access guard was populating.
    /// With thousands of packnames and a guard on every maintenance action that
    /// was a key explosion, and two round trips on each miss.
    /// </para>
    /// </remarks>
    public async Task<HashSet<int>> GetOwnedPackNameIdsByManagerIdAsync(int managerId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            PackNameServicesDefaults.PackNameOwnedIdsByManagerCacheKey, managerId);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            //requireActiveList: FALSE. Ownership is not visibility: deactivating a list takes it out of
            //the ordering selector, it does not revoke the manager's right to administer the packnames
            //in it. Before this, all four SP paths filtered Active = 1, so
            //unticking Active on a list locked its manager out of every packname in it - including the
            //ones they would need to touch to put it right.
            var packNameListIds = await GetPackNameListIdsByManagerIdAsync(managerId, requireActiveList: false);
            var departmentIds = (await GetDepartmentsByManagerIdAsync(managerId)).Select(d => d.Id).ToList();

            if (packNameListIds.Count == 0 && departmentIds.Count == 0)
                return new HashSet<int>();

            //One query, both routes. Note: !Deleted only - see the remarks above.
            var ids = await (from pn in _packNameRepository.Table
                             where !pn.Deleted
                                   && (packNameListIds.Contains(pn.PackNameListId)
                                       || (pn.DepartmentId.HasValue && departmentIds.Contains(pn.DepartmentId.Value)))
                             select pn.Id).Distinct().ToListAsync();

            return ids.ToHashSet();
        });
    }

    /// <summary>
    /// Whether a manager has access to a packName either through packName lists or departments
    /// </summary>
    /// <param name="managerId">Manager identifier</param>
    /// <param name="packNameId">PackName identifier</param>
    /// <remarks>
    /// This once took the manager's list ids and department ids as optional narrowing arguments. They
    /// were a false economy: the result was cached under (managerId, packNameId) only, so a caller
    /// passing a narrowed subset got whatever the first caller had cached for that pair — the
    /// parameters changed the query but not the key, which is a correctness bug rather than an
    /// optimisation. The manager's owned set is now resolved once and cached under the manager
    /// alone, and the two parameters were removed because no caller ever passed them.
    /// </remarks>
    public async Task<bool> IsPackNameOwnedByManagerId(int managerId, int packNameId)
    {
        if (packNameId == 0)
            return false;

        var ownedIds = await GetOwnedPackNameIdsByManagerIdAsync(managerId);

        return ownedIds.Contains(packNameId);
    }

    /// <summary>
    /// Gets a paged list of customers for the specified store and optional branch, filtered by email, username, first
    /// name, and last name.
    /// </summary>
    /// <remarks>Excludes deleted customers, branches, and pack names. Filters use substring matching and
    /// results are obtained by joining customers to customer-branch and pack-name data; pagination is applied at the
    /// query level.</remarks>
    /// <param name="storeId">Identifier of the store; when zero returns an empty list.</param>
    /// <param name="branchId">Identifier of the branch within the store; when zero includes all branches of the store.</param>
    /// <param name="email">Optional substring filter for customer email.</param>
    /// <param name="username">Optional substring filter for customer username.</param>
    /// <param name="firstName">Optional substring filter for customer first name.</param>
    /// <param name="lastName">Optional substring filter for customer last name.</param>
    /// <param name="pageIndex">Zero-based index of the page to return.</param>
    /// <param name="pageSize">Maximum number of items to return per page.</param>
    /// <returns>A task that resolves to an IPagedList<CustomerDTO> containing matching customers ordered by first name then last
    /// name; each item includes branch name and the first matching pack name when available.</returns>
    public async Task<IPagedList<CustomerDTO>> GetCustomersByStoreAsync(int storeId, int branchId,
        string email = null, string username = null, string firstName = null, string lastName = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        if (storeId == 0)
            return new PagedList<CustomerDTO>([], pageIndex, pageSize);

        var query = _customerRepository.Table.Where(c => !c.Deleted);

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(c => c.Email.Contains(email));

        if (!string.IsNullOrWhiteSpace(username))
            query = query.Where(c => c.Username.Contains(username));

        if (!string.IsNullOrWhiteSpace(firstName))
            query = query.Where(c => c.FirstName.Contains(firstName));

        if (!string.IsNullOrWhiteSpace(lastName))
            query = query.Where(c => c.LastName.Contains(lastName));

        //Get branches in the store
        var branchQuery = from b in _branchRepository.Table
                          where b.StoreId == storeId && !b.Deleted
                          select b;

        if (branchId > 0)
        {
            branchQuery = from c in branchQuery
                          where c.Id == branchId
                          select c;
        }

        var packNamesQuery = from pn in _packNameRepository.Table
                             where !pn.Deleted
                             select pn;

        var queryDTO = from cus in query
                       join cb in _customerBranchRepository.Table on cus.Id equals cb.Id
                       join b in branchQuery on cb.BranchId equals b.Id
                       join pn in packNamesQuery on cus.Id equals pn.CustomerId into gpn
                       orderby cus.FirstName, cus.LastName
                       select new CustomerDTO
                       {
                           Id = cus.Id,
                           FirstName = cus.FirstName,
                           LastName = cus.LastName,
                           Email = cus.Email,
                           Active = cus.Active,
                           Branch = b.Name,
                           PackName = gpn.FirstOrDefault()
                       };

        return await queryDTO.ToPagedListAsync(pageIndex, pageSize);
    }

    public async Task<IPagedList<CustomerDTO>> GetCustomersWithoutPackNameByStoreAsync(int storeId, string email = null, string name = null,
    int pageIndex = 0, int pageSize = int.MaxValue)
    {
        if (storeId == 0)
            return new PagedList<CustomerDTO>([], pageIndex, pageSize);

        var query = _customerRepository.Table.Where(c => !c.Deleted);

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(c => c.Email.Contains(email));

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(c => $"{c.FirstName} {c.LastName}".Contains(name));

        //Get branches in the store
        var branchQuery = from b in _branchRepository.Table
                          where b.StoreId == storeId && !b.Deleted
                          select b;

        var packNamesQuery = from pn in _packNameRepository.Table
                             where !pn.Deleted
                             select pn;

        var queryDTO = from c in query
                       join cb in _customerBranchRepository.Table on c.Id equals cb.Id
                       join b in branchQuery on cb.BranchId equals b.Id
                       join pn in packNamesQuery on c.Id equals pn.CustomerId into gpn
                       where !gpn.Any()
                       orderby c.FirstName, c.LastName
                       select new CustomerDTO
                       {
                           Id = c.Id,
                           FirstName = c.FirstName,
                           LastName = c.LastName,
                           Email = c.Email,
                           Active = c.Active,
                           Branch = b.Name
                       };

        return await queryDTO.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets customers with no packName for the branch
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customers
    /// </returns>
    public async Task<IList<Customer>> GetCustomersWithoutPackNameByBranchAsync(int branchId)
    {
        var packNamesQuery = from pn in _packNameRepository.Table
                             where !pn.Deleted
                             select pn;

        var query = from cb in _customerBranchRepository.Table
                    join c in _customerRepository.Table on cb.Id equals c.Id
                    join pn in packNamesQuery on c.Id equals pn.CustomerId into gpn
                    where cb.BranchId == branchId && !gpn.Any() && !c.Deleted
                    select c;

        return await query.ToListAsync();
    }

    #endregion

    #region Public Store

    /// <summary>
    /// Gets the packName lists that a packName can use to order for himself and for the users he is manager of
    /// </summary>
    /// <remarks>
    /// Only entries that hold at least one packname the manager can see are offered: selecting an empty one
    /// would load an empty packname dropdown with no way forward. The manager's own list is always offered.
    /// The selector's add button adds into the selected list, so an empty list is not an add target: a new
    /// list gets its first packname from admin.
    /// </remarks>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of available packName lists.</returns>
    public async Task<List<PackNameList>> GetAvailablePackNameListsAsync(int customerId)
    {
        var packName = await _packNameService.GetPackNameByCustomerIdAsync(customerId);

        if (!(packName?.Manager ?? false))
            return new List<PackNameList>();

        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);
        var lists = await GetPackNameListsByManagerIdAsync(packName.Id);
        var departments = await GetDepartmentsByManagerIdAsync(packName.Id);

        if ((packNameList?.StoreId ?? 0) == 0)
            return new List<PackNameList>();

        var storeId = packNameList.StoreId;

        //The lists the manager owns, taken before the own list and the department lists are added: ownership
        //decides which roster an entry loads, and so how its emptiness is asked below.
        var ownedListIds = lists.Select(pnl => pnl.Id).ToList();
        var departmentIds = departments.Select(d => d.Id).ToList();

        //If the packName doesn't owned his packName list, we need to add it to be able for the packName to order for himself
        //(like packNames that are not manager are able to order for themselves
        if (!lists.Any(pnl => pnl.Id == packName.PackNameListId))
            lists.Add(packNameList);

        //Check whether the manager is mapped to departments
        if (departments.Any())
        {
            //Check the setting for showing the packnames in their own list or in a one big list for the department?
            var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

            //If we should show the packNames in their own branch, we need to find them and add the missing packname lists to the list
            if (storeSettings.ShowDepartmentPackNamesByBranch)
            {
                var depPackNameLists = await GetPackNameListsByDepartmentIdsAsync(departmentIds, storeId);
                foreach (var depPackNameList in depPackNameLists)
                    if (!lists.Any(pnl => pnl.Id == depPackNameList.Id))
                        lists.Add(depPackNameList);
            }
            else //Show one big list for each department. We add (dept) to the name because the department can have the same name as a list and could look like it is duplicated
            {
                foreach (var department in departments)
                {
                    lists.Add(new PackNameList
                    {
                        Id = -department.Id, //Use negative to be able to identify this list as a virtual list
                        StoreId = storeId,
                        BranchId = department.Id,
                        Name = $"{department.Name} ({await _localizationService.GetResourceAsync("Plugins.CW.Core.Department.Abbreviation")})",
                    });
                }
            }
        }

        //Offer nothing that would load nothing. Emptiness is asked of the same cached readers the roster uses,
        //so an entry is offered exactly when selecting it loads at least one packname.
        var offered = new List<PackNameList>();

        foreach (var list in lists)
            if (await HasVisiblePackNamesAsync(list, packName.PackNameListId, ownedListIds, departmentIds))
                offered.Add(list);

        return offered.OrderBy(pnl => pnl.Name).ToList();
    }

    #endregion

    #endregion
}
