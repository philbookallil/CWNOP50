using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Services.Messages;

/// <summary>
/// The e-mails a packname customer receives from the packname flows
/// </summary>
public interface IPackNameWorkflowMessageService
{
    /// <summary>
    /// Sends the welcome e-mail to a customer registered by a manager: their packname details, the subsidies
    /// assigned to it and a link to set their own password
    /// </summary>
    /// <param name="customer">The customer just registered</param>
    /// <param name="packName">The packname the customer was registered for</param>
    /// <param name="branchName">The branch the packname belongs to</param>
    /// <param name="subsidyNames">The names of the subsidies assigned to the packname; empty when none</param>
    /// <param name="storeId">The store the customer was registered in</param>
    /// <param name="languageId">Message language identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the queued e-mail identifiers
    /// </returns>
    Task<IList<int>> SendCustomerRegisteredByManagerMessageAsync(Customer customer, PackName packName, string branchName, IList<string> subsidyNames, int storeId, int languageId);
}