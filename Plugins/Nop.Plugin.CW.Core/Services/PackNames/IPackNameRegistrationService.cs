using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Creates packnames the way a manager does, with the customer account, the delivery address, the subsidies and the
/// welcome e-mail that go with one. The storefront form and the Excel import both come here
/// </summary>
public interface IPackNameRegistrationService
{
    /// <summary>
    /// The list a manager's new packname goes into for a branch: an active list of the manager's own store, the
    /// branch's default list first; with LimitAssignedBranches only a list the manager owns
    /// </summary>
    /// <param name="manager">The manager's own packname</param>
    /// <param name="branch">The branch chosen for the new packname</param>
    /// <param name="storeSettings">The settings of the manager's store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the target list, or null when the manager may not add to that branch
    /// </returns>
    Task<PackNameList> ResolveTargetPackNameListAsync(PackName manager, Branch branch, MultiStoreSettings storeSettings);

    /// <summary>
    /// The list an administrator's new packname goes into for a branch: the branch's active default list, otherwise
    /// its first active list
    /// </summary>
    /// <param name="branch">The branch chosen for the new packname</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the target list, or null when the branch has no active list
    /// </returns>
    Task<PackNameList> ResolveTargetPackNameListAsync(Branch branch);

    /// <summary>
    /// Creates the packname, with its customer account, delivery address and subsidies when asked for, and queues the
    /// welcome e-mail when an account was created. Everything is written in one transaction: when the account cannot
    /// be registered nothing is created and the result carries the reasons
    /// </summary>
    /// <param name="registration">What to create</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what was created, or the errors
    /// </returns>
    Task<PackNameRegistrationResult> RegisterAsync(PackNameRegistration registration);
}