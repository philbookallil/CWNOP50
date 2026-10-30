using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.Branches;

namespace Nop.Plugin.CW.Core.Services.Branches;

public interface IBranchService
{
    #region Stores

    Task<IPagedList<(Store Store, int BranchCount)>> GetAllStoresWithBranchCountAsync(string name = null,
    int pageIndex = 0, int pageSize = int.MaxValue);

    #endregion

    #region Branches

    /// <summary>
    /// Gets a branch
    /// </summary>
    /// <param name="branchId">Branch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<Branch> GetBranchByIdAsync(int branchId);

    /// <summary>
    /// Gets a branch by customer identifier
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<Branch> GetBranchByCustomerIdAsync(int customerId);

    Task<IList<Branch>> GetAllBranchesByStoreIdAsync(int storeId);

    Task<IPagedList<Branch>> GetBranchesByStoreIdAsync(int storeId, string branchName,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets branches by store identifier
    /// </summary>
    /// <param name="storeId">The store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branches
    /// </returns>
    Task<IPagedList<Branch>> GetBranchesByStoreIdAsync(int storeId,
         int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Sort branches for tree representation
    /// </summary>
    /// <param name="branchesByParentId">Branches for sort</param>
    /// <param name="parentId">Parent branch identifier</param>
    /// <param name="ignoreBranchesWithoutExistingParent">A value indicating whether branches without parent branch in provided branch list (source) should be ignored</param>
    /// <returns>
    /// An enumerable containing the sorted branches
    /// </returns>
    IEnumerable<Branch> SortBranchesForTree(
        ILookup<int, Branch> branchesByParentId,
        int parentId = 0,
        bool ignoreBranchesWithoutExistingParent = false);

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
    Task<string> GetFormattedBreadCrumbAsync(int storeId, Branch branch, IList<Branch> allBranches,
        string separator = ">>");

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
    Task<IList<Branch>> GetBranchBreadCrumbAsync(int storeId, Branch branch, IList<Branch> allBranches);

    /// <summary>
    /// Determines asynchronously whether the specified branch has any child branches, associated pack name lists, or
    /// cost centers.
    /// </summary>
    /// <param name="branchId">The unique identifier of the branch to check for child branches, pack name lists, or cost centers.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the branch
    /// has at least one child branch, pack name list, or cost center; otherwise, <see langword="false"/>.</returns>
    Task<bool> BranchHasChildrenAsync(int branchId);

    /// <summary>
    /// Asynchronously retrieves the number of staff members associated with the specified branch.
    /// </summary>
    /// <remarks>The result is cached to improve performance on subsequent calls for the same branch
    /// identifier.</remarks>
    /// <param name="branchId">The unique identifier of the branch for which to count staff members. Must be a non-zero value.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of staff members in the
    /// specified branch. Returns 0 if the branch identifier is 0.</returns>
    Task<int> GetStaffCountAsync(int branchId);

    Task InsertBranchAsync(Branch branch);

    Task UpdateBranchAsync(Branch branch);

    Task DeleteBranchAsync(Branch branch);

    #endregion

    #region Branch address mapping

    /// <summary>
    /// Remove a branch-address mapping record
    /// </summary>
    /// <param name="branch">Branch</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<bool> RemoveBranchAddressAsync(Branch branch, Address address);

    /// <summary>
    /// Inserts a branch-address mapping record
    /// </summary>
    /// <param name="branch">Branch</param>
    /// <param name="address">Address</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertBranchAddressAsync(Branch branch, Address address);

    /// <summary>
    /// Gets a list of addresses mapped to branchId
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    Task<IList<Address>> GetAddressesByBranchIdAsync(int branchId);

    /// <summary>
    /// Gets a address mapped to branch
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <param name="addressId">Address identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    Task<Address> GetBranchAddressAsync(int branchId, int addressId);

    /// <summary>
    /// Gets the main shipping address mapped to branch
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    Task<Address> GetDefaultShipAddressByBranchIdAsync(int branchId);

    /// <summary>
    /// Sets or clears the default shipping address for a branch.
    /// Pass null address to clear the default.
    /// </summary>
    Task SetDefaultBranchAddressAsync(Branch branch, Address? address);

    #endregion

    #region Shipping Methods

    Task<List<string>> GetShippingMethodsByBranchId(int branchId);

    Task<string> GetShippingMethodsByCardFileIdFromStoreSettings(int branchId);

    #endregion
}
