using Nop.Core.Domain.Customers;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Where a customer lands when the store has somewhere better than the homepage to put them
/// </summary>
public interface IPackNameLandingService
{
    /// <summary>
    /// Resolves the route a customer lands on after signing in or being impersonated: the subsidy catalogue
    /// when the store asks for it and the selected packname has product groups, otherwise bulk ordering when
    /// the store asks for that
    /// </summary>
    /// <param name="customer">The customer whose session it is</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the route name; null when the homepage is the right place
    /// </returns>
    Task<string> ResolveSignInLandingRouteAsync(Customer customer);

    /// <summary>
    /// Resolves the route a customer lands on after selecting a packname: the subsidy catalogue when the store
    /// asks for it and the packname has product groups
    /// </summary>
    /// <param name="customer">The customer whose session it is</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the route name; null when the page the customer came from is the right place
    /// </returns>
    Task<string> ResolveSelectionLandingRouteAsync(Customer customer);
}