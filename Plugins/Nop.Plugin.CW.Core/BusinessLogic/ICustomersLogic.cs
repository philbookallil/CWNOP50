using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;

namespace Nop.Plugin.CW.Core.BusinessLogic;

/// <summary>
/// Orchestrates cross-store customer impersonation.
/// </summary>
public interface ICustomersLogic
{
    /// <summary>
    /// Requests an impersonation session on the given store.
    /// </summary>
    /// <param name="targetStore">The Store on which the impersonation session is requested</param>
    /// <param name="requestingUser">The user (customer) that is going to perform the impersonation action</param>
    /// <param name="impersonatedUserId">The Id of the user (customer) that is going to be impersonated</param>
    /// <param name="redirectTo">A relative URL to which the user should be redirected on the target Store</param>
    /// <returns>If the impersonation succeeds, returns the URL to which the current request should be redirected; otherwise an exception is thrown</returns>
    Task<string> RequestImpersonation(Store targetStore, Customer requestingUser, int impersonatedUserId, string redirectTo = null);

    Task ImpersonateCustomer(int customerId);

    /// <summary>
    /// Requests an Impersonation Token on the given Store and for the given owner (requestingUserId) and impersonated user (impersonatedUserId).
    /// </summary>
    Task<ImpersonateTokenAPIModel> RequestImpersonationToken(Store store, int requestingUserId, int impersonatedUserId);
}