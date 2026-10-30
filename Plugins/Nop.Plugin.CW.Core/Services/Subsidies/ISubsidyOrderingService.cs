using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

public interface ISubsidyOrderingService
{
    /// <summary>
    /// Gets subsidy packName mappings for subsidies in ordering period (or can order outside) by packName identifier
    /// </summary>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packName mappings
    /// </returns>
    Task<IList<SubsidyPackName>> GetInOrderingSubsidyPackNamesByPackNameIdAsync(int packNameId);

    /// <summary>
    /// Gets the packname with subsidies allocated that can not order because they are not inside ordering period and can't order outside period
    /// </summary>
    /// <param name="packNameIds">The packnames to validate</param>
    /// <returns></returns>
    Task<List<int>> GetOrderingBlockedPackNamesAsync(List<int> packNameIds);

    /// <summary>
    /// Gets the ordering flag information for a packName
    /// </summary>
    /// <param name="packNameId">The packname identifier</param>
    /// <returns></returns>
    Task<SubsidyPackNameCanOrder> GetSubsidyPackNameCanOrderAsync(int packNameId);

    /// <summary>
    /// Gets whether the packname can not order because he is not inside ordering period and can't order outside period
    /// </summary>
    /// <param name="packNameId">The packname identifier</param>
    /// <returns></returns>
    Task<bool> PackNameCanOrderAsync(int packNameId);

    /// <summary>
    /// Gets products from product groups for in ordering subsidies by packname
    /// </summary>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    Task<IList<int>> GetSubsidyProductsByPackNameIdAsync(int packNameId);

    /// <summary>
    /// Returns the flat union of product IDs allocated to this PackName across all
    /// currently active subsidies (in-period or OrderOutsidePeriod), but only when ALL
    /// of those active subsidies restrict this packname to its allocated products
    /// (a manager only by a subsidy that includes managers).
    /// Returns null if no restriction applies: no active subsidies, or at least one
    /// active subsidy that does not restrict this packname.
    /// Returns an empty list if restriction applies but no product groups are assigned.
    /// </summary>
    Task<IList<int>?> GetOnlyAllocatedProductIdsAsync(int packNameId);

    /// <summary>
    /// Gets whether a packName has at least one product group in his active subsidies (active means in ordering period or can order outside period)
    /// </summary>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    Task<bool> PackNameHasActiveProductGroupsAsync(int packNameId);

    /// <summary>
    /// Gets the next moment any subsidy allocated to a packname changes its ordering status (its period
    /// opens or closes), as a UTC instant. Null when none will change again.
    /// </summary>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the UTC instant of the next change</returns>
    Task<DateTime?> GetPackNameNextSubsidyStatusChangeAsync(int packNameId);

    /// <summary>
    /// //Get the total minutes between Now and the next date the ordering status changes for the packname (based on its subsidies)
    /// </summary>
    /// <param name="packNameId">PackName identifier</param>
    /// <returns></returns>
    Task<int> GetPackNameCacheTimeInMinutesAsync(int packNameId);
}