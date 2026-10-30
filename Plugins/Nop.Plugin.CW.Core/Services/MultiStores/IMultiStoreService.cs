using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Customers.DTOs;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.MultiStores.DTOs;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;

namespace Nop.Plugin.CW.Core.Services.MultiStores;

public interface IMultiStoreService
{
    #region Departments

    /// <summary>
    /// Gets a department
    /// </summary>
    /// <param name="departmentId">Department identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<Department> GetDepartmentByIdAsync(int departmentId);

    /// <summary>
    /// Gets all departments
    /// </summary>
    /// <returns></returns>
    Task<IList<Department>> GetAllDepartmentsAsync();

    /// <summary>
    /// Gets departments by store with searching and paging
    /// </summary>
    /// <param name="storeId"></param>
    /// <param name="searchName"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    Task<IPagedList<Department>> GetDepartmentsByStoreIdAsync(int storeId, string searchName = null,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Inserts a department
    /// </summary>
    /// <param name="department">Department</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertDepartmentAsync(Department department);

    /// <summary>
    /// Updates a department
    /// </summary>
    /// <param name="department">Department</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task UpdateDepartmentAsync(Department department);

    /// <summary>
    /// Deletes a department
    /// </summary>
    /// <param name="department">Department</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteDepartmentAsync(Department department);

    /// <summary>
    /// Find managers mapped to the department
    /// </summary>
    /// <param name="departmentId">Department identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the managers
    /// </returns>
    Task<IPagedList<PackNameDTO>> GetManagersByDepartmentIdAsync(int departmentId,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Find departments mapped to a manager
    /// </summary>
    /// <param name="managerId">Manager identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the departments
    /// </returns>
    Task<IList<Department>> GetDepartmentsByManagerIdAsync(int managerId);

    /// <summary>
    /// Gets the managers from the store that have not been already mapped to the department
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="departmentId">Department identifier</param>
    /// <param name="name">Search name</param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    Task<IPagedList<PackNameDTO>> GetManagersNotInDepartmentAsync(int storeId, int departmentId, string name = null,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Find managers mapped to the department
    /// </summary>
    /// <param name="departmentId">Department identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the managers
    /// </returns>
    Task<List<DepartmentManagerMapping>> GetDepartmentManagersAsync(int departmentId);

    /// <summary>
    /// Gets department manager mapping
    /// </summary>
    /// <param name="departmentId">Department identifier</param>
    /// <param name="managerId">Manager identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mapping
    /// </returns>
    Task<DepartmentManagerMapping> GetDepartmentManagerByIdAsync(int departmentId, int managerId);

    /// <summary>
    /// Inserts a department manager mapping
    /// </summary>
    /// <param name="departmentManager">Department manager mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertDepartmentManagerAsync(DepartmentManagerMapping departmentManager);

    /// <summary>
    /// Deletes a department manager mapping
    /// </summary>
    /// <param name="departmentManager">Department manager mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteDepartmentManagerAsync(DepartmentManagerMapping departmentManager);

    #endregion

    #region Cost Centers

    /// <summary>
    /// Gets a cost center
    /// </summary>
    /// <param name="costCenterId">Cost center identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<CostCenter> GetCostCenterByIdAsync(int costCenterId);

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
    Task<IPagedList<CostCenterDTO>> GetCostCentersAsync(int? storeId, int? branchId, string searchName = null, string searchBranchName = null, bool searchInBranches = false,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets cost centers for a store as select list items
    /// </summary>
    /// <param name="storeId"></param>
    /// <returns></returns>
    Task<IList<(int Id, string Name, bool Active)>> GetCostCenterSelectItemsAsync(int storeId);

    /// <summary>
    /// Inserts a cost center
    /// </summary>
    /// <param name="costCenter">Cost center</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertCostCenterAsync(CostCenter costCenter);

    /// <summary>
    /// Updates a cost center
    /// </summary>
    /// <param name="costCenter">Cost center</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task UpdateCostCenterAsync(CostCenter costCenter);

    /// <summary>
    /// Deletes a cost center
    /// </summary>
    /// <param name="costCenter">Cost center</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteCostCenterAsync(CostCenter costCenter);

    #endregion

    #region Cost Centers - Addresses Mapping

    /// <summary>
    /// Gets a list of addresses mapped to a cost center
    /// </summary>
    /// <param name="costCenterId">Cost center identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    Task<IList<Address>> GetAddressesByCostCenterIdAsync(int costCenterId);

    /// <summary>
    /// Gets a address mapped to a cost center
    /// </summary>
    /// <param name="costCenterId">Cost center identifier</param>
    /// <param name="addressId">Address identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    Task<Address> GetCostCenterAddressAsync(int costCenterId, int addressId);

    /// <summary>
    /// Inserts a cost center-address mapping record
    /// </summary>
    /// <param name="costCenter">CostCenter</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertCostCenterAddressAsync(CostCenter costCenter, Address address);

    /// <summary>
    /// Remove a cost center-address mapping record
    /// </summary>
    /// <param name="costCenter">Cost center</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<bool> RemoveCostCenterAddressAsync(CostCenter costCenter, Address address);

    #endregion

    #region PackNames

    Task<IPagedList<PackNameDTO>> GetPackNameManagersByStoreAsync(int storeId, int branchId, int packNameListId = 0, string name = null, bool fromOtherBrances = false,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets all PackNameList ids available to a manager across all access paths.
    /// </summary>
    /// <param name="managerId">Manager identifier</param>
    /// <param name="requireActiveList">
    /// True (default) answers VISIBILITY - which lists this manager may order from, inactive lists
    /// excluded. False answers OWNERSHIP - which lists' packnames are theirs to administer, inactive
    /// lists included.
    /// </param>
    Task<List<int>> GetPackNameListIdsByManagerIdAsync(int managerId, bool requireActiveList = true);

    Task<List<PackNameList>> GetPackNameListsByManagerIdAsync(int managerId);

    /// <summary>
    /// Gets branches visible to a manager — only branches where the manager
    /// has at least one accessible PackNameList. Does NOT return all descendants.
    /// Includes intermediate ancestor branches needed for a coherent tree display.
    /// </summary>
    Task<IList<Branch>> GetVisibleBranchesForManagerAsync(int managerId);

    /// <summary>
    ///  Gets all PackName IDs a manager can act on behalf of, across all access paths:
    ///  PackNames in any list the manager controls (branch/flag/direct-assignment paths)
    ///  PackNames assigned to any department the manager is mapped to
    /// </summary>
    /// <param name="managerId">Manager identifier</param>
    /// <returns></returns>
    Task<List<int>> GetPackNameIdsByManagerId(int managerId);

    /// <summary>
    /// The ids of every packname a manager owns, through their lists or their departments.
    /// </summary>
    /// <param name="managerId">Manager identifier</param>
    Task<HashSet<int>> GetOwnedPackNameIdsByManagerIdAsync(int managerId);

    /// <summary>
    /// Whether a manager has access to a packName either through packName lists or departments 
    /// </summary>
    /// <param name="managerId">Manager identifier</param>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns></returns>
    Task<bool> IsPackNameOwnedByManagerId(int managerId, int packNameId);

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
    Task<IPagedList<CustomerDTO>> GetCustomersByStoreAsync(int storeId, int branchId,
        string email = null, string username = null, string firstName = null, string lastName = null,
        int pageIndex = 0, int pageSize = int.MaxValue);

    Task<IPagedList<CustomerDTO>> GetCustomersWithoutPackNameByStoreAsync(int storeId, string email = null, string name = null,
    int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets customers with no packName for the branch
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customers
    /// </returns>
    Task<IList<Customer>> GetCustomersWithoutPackNameByBranchAsync(int branchId);

    #endregion

    #region Public Store

    /// <summary>
    /// Gets the packName lists that a packName can use to order for himself and for the users he is manager of
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of available packName lists.</returns>
    Task<List<PackNameList>> GetAvailablePackNameListsAsync(int customerId);

    #endregion
}
