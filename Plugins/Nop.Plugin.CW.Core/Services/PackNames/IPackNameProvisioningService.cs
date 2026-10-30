using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Creates the packname lists and packnames a store asks for automatically: a default list for a branch, a packname for
/// each customer linked to a branch
/// </summary>
public interface IPackNameProvisioningService
{
    /// <summary>
    /// Returns the branch's default active packname list, creating one when the branch has no active list
    /// </summary>
    /// <param name="branch">Branch</param>
    /// <param name="nameFrom">Where the name of a created list comes from</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch's default active list, or its first active list when none is marked default
    /// </returns>
    Task<PackNameList> EnsureDefaultPackNameListAsync(Branch branch, PackNameListNameFrom nameFrom);

    /// <summary>
    /// Creates a packname in the list for every customer of the list's branch who has none
    /// </summary>
    /// <param name="packNameList">Packname list</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the number of packnames created
    /// </returns>
    Task<int> CreatePackNamesForCustomersWithoutOneAsync(PackNameList packNameList);

    /// <summary>
    /// Applies the store's automation to a branch that has just been created: when the store uses branches as packname
    /// lists, the branch gets its default list
    /// </summary>
    /// <param name="branch">The new branch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ProvisionNewBranchAsync(Branch branch);

    /// <summary>
    /// Applies the store's automation to a customer who has just been linked to a branch: when the store uses staff as
    /// packnames and the customer has none, one is created in the branch's default list, which is created when the
    /// branch has none
    /// </summary>
    /// <param name="customer">The customer</param>
    /// <param name="branchId">The branch the customer was linked to</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ProvisionCustomerBranchAsync(Customer customer, int branchId);

    /// <summary>
    /// Checks a change of a customer's branch against their packname, which follows them into the branch's default
    /// list: refused when the branch is cleared (a customer with a packname in a branch needs one), when the branch is
    /// in another store than the packname, or when the list already has a packname of the same name. Nothing is checked
    /// for a customer without a packname, one in a store-wide list, or while the PackName feature is off
    /// </summary>
    /// <param name="customer">The customer</param>
    /// <param name="branchId">The branch the customer is moving to; 0 when it is being cleared</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the reason the customer cannot move there, or null when they can
    /// </returns>
    Task<string> CheckCustomerBranchChangeAsync(Customer customer, int branchId);

    /// <summary>
    /// Applies a change of a customer's branch to their packname: a packname in another branch of the same store moves
    /// to the branch's default list (created when the branch has none), while the PackName feature is on; a customer
    /// without a packname gets one when the store gives every branch customer a packname. A packname in a store-wide
    /// list stays where it is
    /// </summary>
    /// <param name="customer">The customer</param>
    /// <param name="branchId">The customer's new branch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ApplyCustomerBranchAsync(Customer customer, int branchId);

    /// <summary>
    /// Keeps a packname's customer in the packname's branch: the customer's branch becomes the branch of the
    /// packname's list. Nothing changes for a deleted packname, one without a customer, or one in a list without a
    /// branch
    /// </summary>
    /// <param name="packName">The packname, as saved</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SetCustomerBranchFromPackNameAsync(PackName packName);

    /// <summary>
    /// Keeps the customers of a list's packnames in the list's branch, after the list moved to another branch
    /// </summary>
    /// <param name="packNameList">The list, as saved</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SetCustomerBranchesFromPackNameListAsync(PackNameList packNameList);
}