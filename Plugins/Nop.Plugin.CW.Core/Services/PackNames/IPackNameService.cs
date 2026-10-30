using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;

namespace Nop.Plugin.CW.Core.Services.PackNames
{
    public interface IPackNameService
    {
        #region PackNameList

        /// <summary>
        /// Gets a packName list by indentifier
        /// </summary>
        /// <param name="packNameListId">PackName list indentifier</param>
        /// <returns>PackNameList</returns>
        Task<PackNameList> GetPackNameListByIdAsync(int packNameListId);

        /// <summary>
        /// Gets packNameLists by identifier
        /// </summary>
        /// <param name="packNameListIds">PackNameList identifiers</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the packNameLists
        /// </returns>
        Task<IList<PackNameList>> GetPackNameListsByIdsAsync(int[] packNameListIds);

        /// <summary>
        /// Gets packNameLists by branch identifier
        /// </summary>
        /// <param name="branchId">Branch identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
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
        /// Gets packNameLists by branch identifier
        /// </summary>
        /// <param name="branchId">Branch identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the packNameLists
        /// </returns>
        Task<bool> ExistDefaultPackNameListAsync(int branchId);

        /// <summary>
        /// Gets packaging name list by store identifier
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the packNameLists
        /// </returns>
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

        /// <summary>
        /// Inserts a PackNameList
        /// </summary>
        /// <param name="PackNameList">packNameList</param>
        Task InsertPackNameListAsync(PackNameList packNameList);

        /// <summary>
        /// Updates a PackNameList
        /// </summary>
        /// <param name="packNameList">PackNameList</param>
        Task UpdatePackNameListAsync(PackNameList packNameList);

        /// <summary>
        /// Deletes a PackNameList
        /// </summary>
        /// <param name="packNameList">PackNameList</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeletePackNameListAsync(PackNameList packNameList);

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
        Task<PackNameListManagerMapping> GetPackNameListManagerByIdAsync(int packNameListId, int packNameId);

        /// <summary>
        /// Find managers mapped to the packaging name list
        /// </summary>
        /// <param name="packNameListId">Packaging name list identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the managers
        /// </returns>
        Task<List<PackNameListManagerMapping>> GetPackNameListManagersAsync(int packNameListId);

        /// <summary>
        /// Find managers mapped to the packaging name list
        /// </summary>
        /// <param name="packNameListId">Packaging name list identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the managers
        /// </returns>
        Task<IPagedList<PackNameDTO>> GetManagersByPackNameListIdAsync(int packNameListId,
           int pageIndex = 0, int pageSize = int.MaxValue);

        /// <summary>
        /// Inserts a packaging name list manager mapping
        /// </summary>
        /// <param name="packNameListManager">>Packaging name list manager mapping</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InsertPackNameListManagerAsync(PackNameListManagerMapping packNameListManager);

        /// <summary>
        /// Deletes a packaging name list manager mapping
        /// </summary>
        /// <param name="packNameListManager">Packaging name list manager mapping</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeletePackNameListManagerAsync(PackNameListManagerMapping packNameListManager);

        #endregion

        #region PackName

        /// <summary>
        /// Gets a packName by indentifier
        /// </summary>
        /// <param name="packNameId">PackName indentifier</param>
        /// <returns>PackName</returns>
        Task<PackName> GetPackNameByIdAsync(int packNameId);

        /// <summary>
        /// Gets a packName by customer indentifier
        /// </summary>
        /// <param name="customerId">Customer indentifier</param>
        /// <returns>PackName</returns>
        Task<PackName> GetPackNameByCustomerIdAsync(int customerId);

        /// <summary>
        /// Gets packNames by packaging name list indentifier
        /// </summary>
        /// <param name="packNameListId">Packaging name list indentifier</param>
        /// <returns>PackNames</returns>
        Task<List<PackName>> GetPackNamesByListIdAsync(int packNameListId);

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
        Task<bool> PackNameNameExistsAsync(int packNameListId, string name, int ignorePackNameId = 0);

        /// <summary>
        /// Gets a dictionary that maps packName identifiers to the total quantity of items in the current customer's
        /// shopping cart.
        /// </summary>
        /// <remarks>Results are retrieved from and stored in the short-term cache using
        /// PackNameServicesDefaults.CartCountsByCustomerCacheKey and the current customer's identifier; the counts are
        /// computed by summing the Quantity values grouped by PackNameId.</remarks>
        /// <returns>A task that yields a Dictionary<int, int> where each key is a pack name ID and each value is the total quantity
        /// for that pack for the current customer.</returns>
        Task<Dictionary<int, int>> GetCartCountsByCustomerAsync();

        /// <summary>
        /// Gets the active packnames of a packaging name list, ordered by name
        /// </summary>
        /// <remarks>
        /// The list is cached and shared by every caller, so it is read-only, and it carries no cart quantities (see
        /// <see cref="GetCartCountsByCustomerAsync"/>).
        /// </remarks>
        /// <param name="packNameListId">Packaging name list indentifier</param>
        /// <returns>PackNames</returns>
        Task<IReadOnlyList<PackNameDTO>> GetActivePackNamesByListIdAsync(int packNameListId);

        /// <summary>
        /// Gets the active packnames of the given departments, in one packaging name list or in any, ordered by name
        /// </summary>
        /// <remarks>
        /// The list is cached and shared by every caller, so it is read-only, and it carries no cart quantities (see
        /// <see cref="GetCartCountsByCustomerAsync"/>).
        /// </remarks>
        /// <param name="packNameListId">Packaging name list indentifier; 0 for every list</param>
        /// <param name="departmentIds">Department identifiers</param>
        /// <returns>PackNames</returns>
        Task<IReadOnlyList<PackNameDTO>> GetActivePackNamesByListIdForDepartmentsAsync(int packNameListId, List<int> departmentIds);

        /// <summary>
        /// Gets a manager's own packname when it is active
        /// </summary>
        /// <remarks>
        /// The packname is cached and shared by every caller, so it is read-only, and it carries no cart quantity (see
        /// <see cref="GetCartCountsByCustomerAsync"/>).
        /// </remarks>
        /// <param name="managerId">The manager's packname identifier</param>
        /// <returns>The packname, or null when it is switched off or deleted</returns>
        Task<PackNameDTO> GetActivePackNameByManagerIdAsync(int managerId);

        /// <summary>
        /// Gets packNames by identifier
        /// </summary>
        /// <param name="packNameIds">PackName identifiers</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the packNames
        /// </returns>
        Task<IList<PackName>> GetPackNamesByIdsAsync(int[] packNameIds);

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
        /// <param name="searchBranchName">Branch name to filter by; the branch is already joined for the DTO's Branch column</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the packNameLists
        /// </returns>
        Task<IPagedList<PackNameDTO>> GetPackNamesAsync(int storeId, int packNameListId, string packName, string email, string packNameType, string gender,
            int departmentId, int costCenterId, bool? manager = null, string searchBranchName = null,
            int pageIndex = 0, int pageSize = int.MaxValue);

        /// <summary>
        /// Gets packNames by department indentifier
        /// </summary>
        /// <param name="departmentId">Department indentifier</param>
        /// <returns>PackNames</returns>
        Task<List<PackName>> GetPackNamesByDepartmentIdAsync(int departmentId);

        /// <summary>
        /// Gets packNames by cost center indentifier
        /// </summary>
        /// <param name="costCenterId">Cost center indentifier</param>
        /// <returns>PackNames</returns>
        Task<List<PackName>> GetPackNamesByCostCenterIdAsync(int costCenterId);

        /// <summary>
        /// Gets a branch by packaging name indentifier
        /// </summary>
        /// <param name="packNameId">Packaging name indentifier</param>
        /// <returns>Branch</returns>
        Task<Branch> GetBranchByPackNameIdAsync(int packNameId);

        Task SetPackNameAsync(int packNameId, int packNameListId, Customer customer, Branch branch);

        Task UnsetPackNameAsync(Customer customer);

        /// <summary>
        /// Retrieves a list of customers with a packName associated to the specified store identifier.
        /// </summary>
        /// <remarks>This method queries the database to retrieve customers linked to the specified
        /// store identifier.</remarks>
        /// <param name="storeId">The unique identifier of the store.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the list of customers associated
        /// with the specified store identifier.</returns>
        Task<List<Customer>> GetCustomersFromStoreIdAsync(int storeId);

        /// <summary>
        /// Inserts a PackName
        /// </summary>
        /// <param name="PackName">packName</param>
        Task InsertPackNameAsync(PackName packName);

        /// <summary>
        /// Updates a PackName
        /// </summary>
        /// <param name="packName">PackName</param>
        Task UpdatePackNameAsync(PackName packName);

        /// <summary>
        /// Deletes a PackName
        /// </summary>
        /// <param name="packName">PackName</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeletePackNameAsync(PackName packName);

        #endregion

        #region PackName - Addresses

        /// <summary>
        /// Remove a packName-address mapping record
        /// </summary>
        /// <param name="packName">PackName</param>
        /// <param name="address">Address</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<bool> RemovePackNameAddressAsync(PackName packName, Address address);

        /// <summary>
        /// Inserts a packName-address mapping record
        /// </summary>
        /// <param name="packName">PackName</param>
        /// <param name="address">Address</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InsertPackNameAddressAsync(PackName packName, Address address);

        /// <summary>
        /// Gets a list of addresses mapped to packName
        /// </summary>
        /// <param name="packNameId">PackName identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        Task<IList<Address>> GetAddressesByPackNameIdAsync(int packNameId);

        /// <summary>
        /// Gets a address mapped to packName
        /// </summary>
        /// <param name="packNameId">PackName identifier</param>
        /// <param name="addressId">Address identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        Task<Address> GetPackNameAddressAsync(int packNameId, int addressId);

        /// <summary>
        /// Gets a address mapped to packName or the customer mapped to the packName
        /// </summary>
        /// <param name="packNameId">PackName identifier</param>
        /// <param name="addressId">Address identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        Task<Address> GetCustomerPackNameAddressAsync(int packNameId, int addressId);

        #endregion
    }
}
