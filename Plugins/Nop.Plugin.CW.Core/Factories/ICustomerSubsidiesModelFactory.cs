using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Models.Subsidies;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Builds the customer's subsidy page
/// </summary>
public interface ICustomerSubsidiesModelFactory
{
    /// <summary>
    /// Prepares the subsidy page for the packname the customer is ordering for
    /// </summary>
    /// <param name="packName">The packname being ordered for</param>
    /// <param name="customer">The signed-in customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the page model
    /// </returns>
    Task<CustomerSubsidiesModel> PrepareCustomerSubsidiesModelAsync(PackName packName, Customer customer);
}