using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// What a packname's subsidies let it see: products, and the categories and manufacturers that hold them.
/// </summary>
public interface IPackNameCatalogGate
{
    /// <summary>
    /// Product ids a packname may see, or null when nothing restricts it
    /// </summary>
    /// <param name="packName">PackName</param>
    /// <param name="replacement">The exchange type when checking an exchange replacement; null for a purchase</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the visible product ids; an empty list when none; null when unrestricted
    /// </returns>
    Task<IList<int>?> GetVisibleProductIdsAsync(PackName packName, ExchangeType? replacement);

    /// <summary>
    /// Product ids the customer's catalogue packname may see, or null when nothing restricts it
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the visible product ids; an empty list when none; null when unrestricted
    /// </returns>
    Task<IList<int>?> GetVisibleProductIdsAsync(Customer customer);

    /// <summary>
    /// Category ids the customer's catalogue packname may see, or null when nothing restricts it
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the categories holding a visible product, with their ancestors; null when unrestricted
    /// </returns>
    Task<ISet<int>?> GetVisibleCategoryIdsAsync(Customer customer);

    /// <summary>
    /// Manufacturer ids the customer's catalogue packname may see, or null when nothing restricts it
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manufacturers of a visible product; null when unrestricted
    /// </returns>
    Task<ISet<int>?> GetVisibleManufacturerIdsAsync(Customer customer);
}