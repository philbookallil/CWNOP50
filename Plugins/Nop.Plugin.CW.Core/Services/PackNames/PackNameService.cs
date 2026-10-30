using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Logging;

namespace Nop.Plugin.CW.Core.Services.PackNames;

public class PackNameService : IPackNameService
{
    #region Fields

    protected readonly IRepository<PackNameList> _packNameListRepository;
    protected readonly IRepository<PackNameListManagerMapping> _packNameListManagerRepository;
    protected readonly IRepository<PackName> _packNameRepository;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IRepository<Branch> _branchRepository;
    protected readonly IRepository<PackNameAddressMapping> _packNameAddressMappingRepository;
    protected readonly IRepository<Address> _addressRepository;
    protected readonly IRepository<PackNameShoppingCartItem> _packNameShoppingCartItemRepository;
    protected readonly IWorkContext _workContext;
    protected readonly IPackNameContext _packNameContext;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IRepository<CustomerAddressMapping> _customerAddressMappingRepository;
    protected readonly IRepository<Customer> _customerRepository;
    protected readonly IShortTermCacheManager _shortTermCacheManager;
    protected readonly IRepository<CostCenter> _costCenterRepository;
    protected readonly IRepository<Department> _departmentRepository;

    #endregion

    #region Ctor

    public PackNameService(IRepository<PackNameList> packNameListRepository,
        IRepository<PackNameListManagerMapping> packNameListManagerRepository,
        IRepository<PackName> packNameRepository,
        IStaticCacheManager staticCacheManager,
        IRepository<Branch> branchRepository,
        IRepository<PackNameAddressMapping> addressMappingRepository,
        IRepository<Address> addressRepository,
        IRepository<PackNameShoppingCartItem> packNameShoppingCartItemRepository,
        IWorkContext workContext,
        IPackNameContext packNameContext,
        ICustomerActivityService customerActivityService,
        ILocalizationService localizationService,
        IGenericAttributeService genericAttributeService,
        IRepository<CustomerAddressMapping> customerAddressMappingRepository,
        IRepository<Customer> customerRepository,
        IShortTermCacheManager shortTermCacheManager,
        IRepository<CostCenter> costCenterRepository,
        IRepository<Department> departmentRepository)
    {
        _packNameListRepository = packNameListRepository;
        _packNameListManagerRepository = packNameListManagerRepository;
        _packNameRepository = packNameRepository;
        _staticCacheManager = staticCacheManager;
        _branchRepository = branchRepository;
        _packNameAddressMappingRepository = addressMappingRepository;
        _addressRepository = addressRepository;
        _packNameShoppingCartItemRepository = packNameShoppingCartItemRepository;
        _workContext = workContext;
        _packNameContext = packNameContext;
        _customerActivityService = customerActivityService;
        _localizationService = localizationService;
        _genericAttributeService = genericAttributeService;
        _customerAddressMappingRepository = customerAddressMappingRepository;
        _customerRepository = customerRepository;
        _shortTermCacheManager = shortTermCacheManager;
        _costCenterRepository = costCenterRepository;
        _departmentRepository = departmentRepository;
    }

    #endregion

    #region Methods

    #region Duplicated from customer service

    /// <summary>
    /// Gets a address mapped to customer
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="addressId">Address identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<Address> GetCustomerAddressAsync(int customerId, int addressId)
    {
        if (customerId == 0 || addressId == 0)
            return null;

        var query = from address in _addressRepository.Table
                    join cam in _customerAddressMappingRepository.Table on address.Id equals cam.AddressId
                    where cam.CustomerId == customerId && address.Id == addressId
                    select address;

        return await _shortTermCacheManager.GetAsync(async () => await query.FirstOrDefaultAsync(), NopCustomerServicesDefaults.CustomerAddressCacheKey, customerId, addressId);
    }

    #endregion

    #region PackNameList

    /// <summary>
    /// Gets a packName list by indentifier
    /// </summary>
    /// <param name="packNameListId">PackName list indentifier</param>
    /// <returns>PackNameList</returns>
    public async Task<PackNameList> GetPackNameListByIdAsync(int packNameListId)
    {
        if (packNameListId == 0)
            return null;

        return await _packNameListRepository.GetByIdAsync(packNameListId, cache => default);
    }

    /// <summary>
    /// Gets packNameLists by identifier
    /// </summary>
    /// <param name="packNameListIds">PackNameList identifiers</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public virtual async Task<IList<PackNameList>> GetPackNameListsByIdsAsync(int[] packNameListIds)
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
    /// Gets packNameLists by branch identifier
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<IList<PackNameListDTO>> GetPackNameListDetailsByBranchAsync(int branchId)
    {
        if (branchId == 0)
            return [];

        var query = from pnl in _packNameListRepository.Table
                    join pn in _packNameRepository.Table on pnl.Id equals pn.PackNameListId into gPn
                    where pnl.BranchId == branchId && !pnl.Deleted
                    orderby pnl.Name
                    select new PackNameListDTO
                    {
                        Id = pnl.Id,
                        Name = pnl.Name,
                        IsDefault = pnl.IsDefault,
                        AllManagers = pnl.AllManagers,
                        ParentManagers = pnl.ParentManagers,
                        PackNamesCount = gPn.Count(x => !x.Deleted),
                        //a deleted manager keeps the list's manager rows, so only managers whose packname is not deleted
                        //are counted, as the managers grid lists them
                        ManagersCount = (from pnlm in _packNameListManagerRepository.Table
                                         join mpn in _packNameRepository.Table on pnlm.PackNameId equals mpn.Id
                                         where pnlm.PackNameListId == pnl.Id && !mpn.Deleted
                                         select pnlm.PackNameId).Count()
                    };

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets packNameLists by branch identifier
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<bool> ExistDefaultPackNameListAsync(int branchId)
    {
        if (branchId == 0)
            return false;

        var query = from pnl in _packNameListRepository.Table
                    where pnl.BranchId == branchId && pnl.IsDefault && !pnl.Deleted
                    select pnl;

        return await query.AnyAsync();
    }

    /// <summary>
    /// Gets packaging name list by store identifier
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<IList<PackNameList>> GetActivePackNameListsByStoreIdAsync(int storeId)
    {
        if (storeId == 0)
            return [];

        var query = from pnl in _packNameListRepository.Table
                    where pnl.StoreId == storeId && !pnl.Deleted && pnl.Active
                    orderby pnl.Id
                    select pnl;

        return await query.ToListAsync();
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
    {
        if (branchId == 0)
            return [];

        var query = from pnl in _packNameListRepository.Table
                    where pnl.BranchId == branchId && !pnl.Deleted
                    orderby pnl.Id
                    select pnl;

        return await query.ToListAsync();
    }

    /// <summary>
    /// Inserts a PackNameList
    /// </summary>
    /// <param name="PackNameList">packNameList</param>
    public async Task InsertPackNameListAsync(PackNameList packNameList)
    {
        ArgumentNullException.ThrowIfNull(packNameList);

        await _packNameListRepository.InsertAsync(packNameList);
    }

    /// <summary>
    /// Updates a PackNameList
    /// </summary>
    /// <param name="packNameList">PackNameList</param>
    public async Task UpdatePackNameListAsync(PackNameList packNameList)
    {
        ArgumentNullException.ThrowIfNull(packNameList);

        await _packNameListRepository.UpdateAsync(packNameList);
    }

    /// <summary>
    /// Deletes a PackNameList
    /// </summary>
    /// <param name="packNameList">PackNameList</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DeletePackNameListAsync(PackNameList packNameList)
    {
        //Because of the relationship between them, we have to remove the packnames before removing the list
        var packNames = await GetPackNamesByListIdAsync(packNameList.Id);

        if (packNames.Count != 0)
        {
            await _packNameRepository.DeleteAsync(packNames);
        }

        await _packNameListRepository.DeleteAsync(packNameList);
    }

    #endregion

    #region PackNameList - Managers

    /// <summary>
    /// Gets packNameList manager mapping
    /// </summary>
    /// <param name="packNameListId">PackNameList identifier</param>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mapping
    /// </returns>
    public async Task<PackNameListManagerMapping> GetPackNameListManagerByIdAsync(int packNameListId, int packNameId)
    {
        if (packNameListId == 0 || packNameId == 0)
            return null;

        var query = from pnlm in _packNameListManagerRepository.Table
                    where pnlm.PackNameListId == packNameListId && pnlm.PackNameId == packNameId
                    select pnlm;

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Find managers mapped to the packaging name list
    /// </summary>
    /// <param name="packNameListId">Packaging name list identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the managers
    /// </returns>
    public virtual async Task<List<PackNameListManagerMapping>> GetPackNameListManagersAsync(int packNameListId)
    {
        var query = from pc in _packNameListManagerRepository.Table
                    join pn in _packNameRepository.Table on pc.PackNameId equals pn.Id
                    where pc.PackNameListId == packNameListId && !pn.Deleted
                    select pc;

        return await query.ToListAsync();
    }

    /// <summary>
    /// Find managers mapped to the packaging name list
    /// </summary>
    /// <param name="packNameListId">Packaging name list identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the managers
    /// </returns>
    public virtual async Task<IPagedList<PackNameDTO>> GetManagersByPackNameListIdAsync(int packNameListId,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = from pc in _packNameListManagerRepository.Table
                    join pn in _packNameRepository.Table on pc.PackNameId equals pn.Id
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    join b in _branchRepository.Table on pnl.BranchId equals b.Id into gb
                    from b in gb.DefaultIfEmpty()
                    join user in _customerRepository.Table on pn.CustomerId equals user.Id into gUser
                    from user in gUser.DefaultIfEmpty()
                        //b == null: same left-join NULL trap as GetPackNamesAsync — a list with no branch
                        //had no managers at all, because NOT NULL is NULL in SQL.
                    where pc.PackNameListId == packNameListId && !pn.Deleted && !pnl.Deleted && (b == null || !b.Deleted)
                    orderby pn.Name
                    select new PackNameDTO
                    {
                        Id = pn.Id,
                        CustomerId = pn.CustomerId,
                        Name = pn.Name,
                        PrintName = pn.PrintName ?? string.Empty,
                        EmployeeNumber = pn.EmployeeNumber,
                        POEntry = pn.POEntry,
                        Group = pn.Group,
                        Gender = pn.Gender ?? string.Empty,
                        Active = pn.Active,
                        Branch = b != null ? b.BranchCode : string.Empty,
                        Email = user != null ? user.Email : string.Empty
                    };

        //paging
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Inserts a packaging name list manager mapping
    /// </summary>
    /// <param name="packNameListManager">>Packaging name list manager mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertPackNameListManagerAsync(PackNameListManagerMapping packNameListManager)
    {
        await _packNameListManagerRepository.InsertAsync(packNameListManager);
    }

    /// <summary>
    /// Deletes a packaging name list manager mapping
    /// </summary>
    /// <param name="packNameListManager">Packaging name list manager mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task DeletePackNameListManagerAsync(PackNameListManagerMapping packNameListManager)
    {
        await _packNameListManagerRepository.DeleteAsync(packNameListManager);
    }

    #endregion

    #region PackName

    /// <summary>
    /// Gets a packName by indentifier
    /// </summary>
    /// <param name="packNameId">PackName indentifier</param>
    /// <returns>PackName</returns>
    public async Task<PackName> GetPackNameByIdAsync(int packNameId)
    {
        if (packNameId == 0)
            return null;

        return await _packNameRepository.GetByIdAsync(packNameId, cache => default);
    }

    /// <summary>
    /// Gets a packName by customer indentifier
    /// </summary>
    /// <param name="customerId">Customer indentifier</param>
    /// <returns>PackName</returns>
    public async Task<PackName> GetPackNameByCustomerIdAsync(int customerId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.PackNameByCustomerCacheKey, customerId);

        var query = from pn in _packNameRepository.Table
                    where pn.CustomerId == customerId && !pn.Deleted
                    select pn;

        return await _staticCacheManager.GetAsync(key, () => query.FirstOrDefaultAsync());
    }

    /// <summary>
    /// Gets packNames by packaging name list indentifier
    /// </summary>
    /// <param name="packNameListId">Packaging name list indentifier</param>
    /// <returns>PackNames</returns>
    public async Task<List<PackName>> GetPackNamesByListIdAsync(int packNameListId)
    {
        var query = from pn in _packNameRepository.Table
                    where pn.PackNameListId == packNameListId && !pn.Deleted
                    select pn;

        return await query.ToListAsync();
    }

    /// <summary>
    /// Whether another packname of the list already has this name, ignoring case and surrounding spaces
    /// </summary>
    /// <param name="packNameListId">Packname list identifier</param>
    /// <param name="name">The name</param>
    /// <param name="ignorePackNameId">The packname being saved again, whose own name does not count; 0 on an insert</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true when the name is taken
    /// </returns>
    public async Task<bool> PackNameNameExistsAsync(int packNameListId, string name, int ignorePackNameId = 0)
    {
        if (packNameListId == 0 || string.IsNullOrWhiteSpace(name))
            return false;

        var lowered = name.Trim().ToLower();

        return await _packNameRepository.Table
            .AnyAsync(pn => pn.PackNameListId == packNameListId && !pn.Deleted && pn.Id != ignorePackNameId && pn.Name.Trim().ToLower() == lowered);
    }

    /// <summary>
    /// Gets a dictionary that maps packName identifiers to the total quantity of items in the current customer's
    /// shopping cart.
    /// </summary>
    /// <remarks>Results are retrieved from and stored in the short-term cache using
    /// PackNameServicesDefaults.CartCountsByCustomerCacheKey and the current customer's identifier; the counts are
    /// computed by summing the Quantity values grouped by PackNameId.</remarks>
    /// <returns>A task that yields a Dictionary<int, int> where each key is a pack name ID and each value is the total quantity
    /// for that pack for the current customer.</returns>
    public async Task<Dictionary<int, int>> GetCartCountsByCustomerAsync()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        return await _shortTermCacheManager.GetAsync(async () =>
             await (from pnci in _packNameShoppingCartItemRepository.Table
                    where pnci.CustomerId == customer.Id
                    group pnci by pnci.PackNameId into g
                    select new { PackNameId = g.Key, Count = g.Sum(x => x.Quantity) })
                      .ToDictionaryAsync(x => x.PackNameId, x => x.Count), PackNameServicesDefaults.CartCountsByCustomerCacheKey, customer.Id);
    }

    /// <summary>
    /// Gets the active packnames of a packaging name list, ordered by name
    /// </summary>
    /// <remarks>
    /// The list is cached and the same instance is handed to every caller, so it is read-only; a caller that needs more
    /// packnames in it builds its own list. It carries no cart quantities: those belong to one customer and are read with
    /// <see cref="GetCartCountsByCustomerAsync"/>.
    /// </remarks>
    /// <param name="packNameListId">Packaging name list indentifier</param>
    /// <returns>PackNames</returns>
    public async Task<IReadOnlyList<PackNameDTO>> GetActivePackNamesByListIdAsync(int packNameListId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            PackNameServicesDefaults.ActivePackNamesByListIdCacheKey, packNameListId);

        return await _staticCacheManager.GetAsync(key, async () =>
            await (from pn in _packNameRepository.Table
                   where pn.PackNameListId == packNameListId && !pn.Deleted && pn.Active
                   orderby pn.Name
                   select new PackNameDTO
                   {
                       Id = pn.Id,
                       Name = pn.Name,
                       Group = pn.Group,
                       Colour = pn.Colour,
                       CustomerId = pn.CustomerId,
                       EmployeeNumber = pn.EmployeeNumber
                   })
                  .ToListAsync()
        );
    }

    /// <summary>
    /// Gets the active packnames of the given departments, in one packaging name list or in any, ordered by name
    /// </summary>
    /// <remarks>
    /// The list is cached and the same instance is handed to every caller, so it is read-only; a caller that needs more
    /// packnames in it builds its own list. It carries no cart quantities: those belong to one customer and are read with
    /// <see cref="GetCartCountsByCustomerAsync"/>.
    /// </remarks>
    /// <param name="packNameListId">Packaging name list indentifier; 0 for every list</param>
    /// <param name="departmentIds">Department identifiers</param>
    /// <returns>PackNames</returns>
    public async Task<IReadOnlyList<PackNameDTO>> GetActivePackNamesByListIdForDepartmentsAsync(int packNameListId, List<int> departmentIds)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.ActivePackNamesByListIdForDepartmentsCacheKey, packNameListId, departmentIds);

        return await _staticCacheManager.GetAsync(key, async () =>
            await (from pn in _packNameRepository.Table
                   where (packNameListId == 0 || pn.PackNameListId == packNameListId) &&
                        pn.DepartmentId.HasValue && departmentIds.Contains(pn.DepartmentId.Value) &&
                        !pn.Deleted && pn.Active
                   orderby pn.Name
                   select new PackNameDTO
                   {
                       Id = pn.Id,
                       Name = pn.Name,
                       Group = pn.Group,
                       Colour = pn.Colour,
                       CustomerId = pn.CustomerId,
                       EmployeeNumber = pn.EmployeeNumber
                   })
                  .ToListAsync()
        );
    }

    /// <summary>
    /// Gets a manager's own packname when it is active
    /// </summary>
    /// <remarks>
    /// The packname is cached and the same instance is handed to every caller, so it is read-only. It carries no cart
    /// quantity: that belongs to one customer and is read with <see cref="GetCartCountsByCustomerAsync"/>.
    /// </remarks>
    /// <param name="managerId">The manager's packname identifier</param>
    /// <returns>The packname, or null when it is switched off or deleted</returns>
    public async Task<PackNameDTO> GetActivePackNameByManagerIdAsync(int managerId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.ActivePackNameByManagerIdCacheKey, managerId);

        return await _staticCacheManager.GetAsync(key, async () =>
            await (from pn in _packNameRepository.Table
                   where pn.Id == managerId && !pn.Deleted && pn.Active
                   select new PackNameDTO
                   {
                       Id = pn.Id,
                       Name = pn.Name,
                       Group = pn.Group,
                       Colour = pn.Colour,
                       CustomerId = pn.CustomerId,
                       EmployeeNumber = pn.EmployeeNumber
                   })
                  .FirstOrDefaultAsync()
        );
    }

    /// <summary>
    /// Gets packNames by identifier
    /// </summary>
    /// <param name="packNameIds">PackName identifiers</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNames
    /// </returns>
    public virtual async Task<IList<PackName>> GetPackNamesByIdsAsync(int[] packNameIds)
    {
        return await _packNameRepository.GetByIdsAsync(packNameIds);
    }

    /// <summary>
    /// Gets packNames
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="packNameListId">PackNameList identifier</param>
    /// <param name="packName"></param>
    /// <param name="email"></param>
    /// <param name="packNameType"></param>
    /// <param name="gender"></param>
    /// <param name="departmentId"></param>
    /// <param name="costCenterId"></param>
    /// <param name="manager"></param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    public async Task<IPagedList<PackNameDTO>> GetPackNamesAsync(int storeId, int packNameListId, string packName, string email, string packNameType, string gender,
        int departmentId, int costCenterId, bool? manager = null, string searchBranchName = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query =
            from pn in _packNameRepository.Table
            join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
            join b in _branchRepository.Table on pnl.BranchId equals b.Id into gb
            from b in gb.DefaultIfEmpty()
            join cc in _costCenterRepository.Table on pn.CostCenterId equals cc.Id into gcc
            from cc in gcc.DefaultIfEmpty()
            join dep in _departmentRepository.Table on pn.DepartmentId equals dep.Id into gdep
            from dep in gdep.DefaultIfEmpty()
            join user in _customerRepository.Table on pn.CustomerId equals user.Id into gUser
            from user in gUser.DefaultIfEmpty()
            where
                (packNameListId == 0 || pn.PackNameListId == packNameListId) &&
                (storeId == 0 || pnl.StoreId == storeId) &&
                //b IS A LEFT JOIN AND PackNameList.BranchId IS int?, so a list with no branch is
                //legitimate and b is null for it. `!b.Deleted` becomes SQL `NOT [b].[Deleted]`, which
                //is NULL for those rows and filters every one of their packnames out of the tab.
                //The intent is "not a deleted branch", not "has a branch".
                !pn.Deleted && !pnl.Deleted && (b == null || !b.Deleted) &&
                (
                    string.IsNullOrEmpty(packName) ||
                    pn.Name.Contains(packName) ||
                    (pn.PrintName ?? string.Empty).Contains(packName) ||
                    (pn.EmployeeNumber ?? string.Empty).Contains(packName) ||
                    (user != null && (user.FirstName.Contains(packName) || user.LastName.Contains(packName)))
                ) &&
                (string.IsNullOrEmpty(email) || (user != null && user.Email.Contains(email))) &&
                (string.IsNullOrEmpty(packNameType) ||
                    (packNameType != MultiStoreServicesDefaults.PackNameTypeCustomer || user != null) &&
                    (packNameType != MultiStoreServicesDefaults.PackNameTypePackname || user == null)
                ) &&
                (string.IsNullOrEmpty(gender) || pn.Gender == gender) &&
                (departmentId == 0 || pn.DepartmentId == departmentId) &&
                (costCenterId == 0 || pn.CostCenterId == costCenterId) &&
                (!manager.HasValue || pn.Manager == manager.Value) &&
                //THE BRANCH WAS ALREADY JOINED, only never filtered on. The PackNames tab has
                //rendered and posted SearchBranchName since 4.6 and the server dropped it silently,
                //so the filter looked broken rather than absent. `b` is the left join above and is
                //null for a list with no branch, which must not match a name search.
                (string.IsNullOrEmpty(searchBranchName) || (b != null && b.Name.Contains(searchBranchName)))
            orderby pn.Name
            select new PackNameDTO
            {
                Id = pn.Id,
                Name = pn.Name,
                PackNameListId = pn.PackNameListId,
                CostCenterId = pn.CostCenterId,
                DepartmentId = pn.DepartmentId,
                CustomerId = pn.CustomerId,
                EmployeeNumber = pn.EmployeeNumber,
                PrintName = pn.PrintName,
                POEntry = pn.POEntry,
                Gender = pn.Gender,
                Comments = pn.Comments,
                Group = pn.Group,
                Colour = pn.Colour,
                Manager = pn.Manager,
                Active = pn.Active,
                Branch = b != null ? b.Name : string.Empty,
                PackNameListName = pnl.Name,
                Email = user != null ? user.Email : string.Empty,
                CostCenter = cc != null ? cc.Name : string.Empty,
                Department = dep != null ? dep.Name : string.Empty,
            };

        //paging
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Gets packNames by department indentifier
    /// </summary>
    /// <param name="departmentId">Department indentifier</param>
    /// <returns>PackNames</returns>
    public async Task<List<PackName>> GetPackNamesByDepartmentIdAsync(int departmentId)
    {
        var query = from pn in _packNameRepository.Table
                    where pn.DepartmentId == departmentId && !pn.Deleted
                    select pn;

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets packNames by cost center indentifier
    /// </summary>
    /// <param name="costCenterId">Cost center indentifier</param>
    /// <returns>PackNames</returns>
    public async Task<List<PackName>> GetPackNamesByCostCenterIdAsync(int costCenterId)
    {
        var query = from pn in _packNameRepository.Table
                    where pn.CostCenterId == costCenterId && !pn.Deleted
                    select pn;

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets a branch by packaging name indentifier
    /// </summary>
    /// <param name="packNameId">Packaging name indentifier</param>
    /// <returns>Branch</returns>
    public async Task<Branch> GetBranchByPackNameIdAsync(int packNameId)
    {
        var query = from pn in _packNameRepository.Table
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    join b in _branchRepository.Table on pnl.BranchId equals b.Id
                    where pn.Id == packNameId && !b.Deleted
                    select b;

        return await query.FirstOrDefaultAsync();
    }

    public async Task SetPackNameAsync(int packNameId, int packNameListId, Customer customer, Branch branch)
    {
        //activity log
        await _customerActivityService.InsertActivityAsync(customer, "PackNameSelection.Started",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.PackNameSelection.Started.Customer"), customer.Email, customer.Id));
        await _customerActivityService.InsertActivityAsync(customer, "PackNameSelection.Started",
            string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.PackNameSelection.Started.Branch"), branch.BranchCode, packNameId), customer);

        await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameIdAttribute, packNameId);
        await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameListIdAttribute, packNameListId);

        _packNameContext.ClearPackNameContext();
    }

    public async Task UnsetPackNameAsync(Customer customer)
    {
        var currentPackName = await _packNameContext.GetCurrentPackNameAsync(customer);
        if (currentPackName != null && currentPackName.Id > 0)
        {
            //activity log
            await _customerActivityService.InsertActivityAsync(_workContext.OriginalCustomerIfImpersonated, "PackNameSelection.Finished",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.PackNameSelection.Finished.Customer"),
                    customer.Email, customer.Id), customer);

            await _customerActivityService.InsertActivityAsync("PackNameSelection.Finished",
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.PackNameSelection.Finished.Branch"),
                    (await _packNameContext.GetCurrentPackNameBranchAsync(customer))?.BranchCode, currentPackName.Id), customer);

            //clear packname context
            await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameIdAttribute, null);
            await _genericAttributeService.SaveAttributeAsync<int?>(customer, PackNameDefaults.SelectedPackNameListIdAttribute, null);
        }

        _packNameContext.ClearPackNameContext();
    }

    /// <summary>
    /// Retrieves a list of customers with a packName associated to the specified store identifier.
    /// </summary>
    /// <remarks>This method queries the database to retrieve customers linked to the specified
    /// store identifier.</remarks>
    /// <param name="storeId">The unique identifier of the store.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of customers associated
    /// with the specified store identifier.</returns>
    public virtual async Task<List<Customer>> GetCustomersFromStoreIdAsync(int storeId)
    {
        var query = from customer in _customerRepository.Table
                    join pn in _packNameRepository.Table on customer.Id equals pn.CustomerId
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    where !pnl.Deleted && !pn.Deleted && !customer.Deleted && pnl.StoreId == storeId
                    select customer;

        return await query.ToListAsync();
    }

    /// <summary>
    /// Inserts a PackName
    /// </summary>
    /// <param name="PackName">packName</param>
    public async Task InsertPackNameAsync(PackName packName)
    {
        ArgumentNullException.ThrowIfNull(packName);

        await _packNameRepository.InsertAsync(packName);

        //"No packname" is remembered for the request too, so a lookup made before this insert must not answer for
        //the rest of the request
        _packNameContext.ClearPackNameContext();
    }

    /// <summary>
    /// Updates a PackName
    /// </summary>
    /// <param name="packName">PackName</param>
    public async Task UpdatePackNameAsync(PackName packName)
    {
        ArgumentNullException.ThrowIfNull(packName);

        await _packNameRepository.UpdateAsync(packName);
    }

    /// <summary>
    /// Deletes a PackName
    /// </summary>
    /// <param name="packName">PackName</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DeletePackNameAsync(PackName packName)
    {
        await _packNameRepository.DeleteAsync(packName);
    }

    #endregion

    #region PackName - Addresses

    /// <summary>
    /// Remove a packName-address mapping record
    /// </summary>
    /// <param name="packName">PackName</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task<bool> RemovePackNameAddressAsync(PackName packName, Address address)
    {
        ArgumentNullException.ThrowIfNull(packName);

        if (await _packNameAddressMappingRepository.Table
            .FirstOrDefaultAsync(m => m.AddressId == address.Id && m.PackNameId == packName.Id)
            is PackNameAddressMapping mapping)
        {
            await _packNameAddressMappingRepository.DeleteAsync(mapping);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Inserts a packName-address mapping record
    /// </summary>
    /// <param name="packName">PackName</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertPackNameAddressAsync(PackName packName, Address address)
    {
        ArgumentNullException.ThrowIfNull(packName);

        ArgumentNullException.ThrowIfNull(address);

        if (await _packNameAddressMappingRepository.Table
            .FirstOrDefaultAsync(m => m.AddressId == address.Id && m.PackNameId == packName.Id)
            is null)
        {
            var mapping = new PackNameAddressMapping
            {
                AddressId = address.Id,
                PackNameId = packName.Id
            };

            await _packNameAddressMappingRepository.InsertAsync(mapping);
        }
    }

    /// <summary>
    /// Gets a list of addresses mapped to packName
    /// </summary>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<IList<Address>> GetAddressesByPackNameIdAsync(int packNameId)
    {
        var query = from address in _addressRepository.Table
                    join cam in _packNameAddressMappingRepository.Table on address.Id equals cam.AddressId
                    where cam.PackNameId == packNameId
                    select address;

        return await _shortTermCacheManager.GetAsync(async () => await query.ToListAsync(), PackNameServicesDefaults.PackNameAddressesCacheKey, packNameId);
    }

    /// <summary>
    /// Gets a address mapped to packName
    /// </summary>
    /// <param name="packNameId">PackName identifier</param>
    /// <param name="addressId">Address identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<Address> GetPackNameAddressAsync(int packNameId, int addressId)
    {
        if (packNameId == 0 || addressId == 0)
            return null;

        var query = from address in _addressRepository.Table
                    join cam in _packNameAddressMappingRepository.Table on address.Id equals cam.AddressId
                    where cam.PackNameId == packNameId && address.Id == addressId
                    select address;

        return await _shortTermCacheManager.GetAsync(async () => await query.FirstOrDefaultAsync(), PackNameServicesDefaults.PackNameAddressCacheKey, packNameId, addressId);
    }

    /// <summary>
    /// Gets a address mapped to packName or the customer mapped to the packName
    /// </summary>
    /// <param name="packNameId">PackName identifier</param>
    /// <param name="addressId">Address identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<Address> GetCustomerPackNameAddressAsync(int packNameId, int addressId)
    {
        if (packNameId == 0 || addressId == 0)
            return null;

        var packNameAddress = await GetPackNameAddressAsync(packNameId, addressId);

        //If not a packName address, let's check the customer addresses
        if (packNameAddress == null)
        {
            var customerId = (await GetPackNameByIdAsync(packNameId))?.CustomerId ?? 0;

            if (customerId > 0)
                return await GetCustomerAddressAsync(customerId, addressId);
        }

        return packNameAddress;
    }

    #endregion

    #endregion
}
