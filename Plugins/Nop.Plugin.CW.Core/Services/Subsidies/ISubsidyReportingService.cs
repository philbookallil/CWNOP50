using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// Read-side queries for the subsidy screens. Every assignment query here starts from the ASSIGNMENT table
/// and left-joins the period views, so an assignment outside its ordering period is still returned, with
/// zero period figures and <c>InOrderingPeriod = false</c>. Callers that only want the live season say so.
/// </summary>
public interface ISubsidyReportingService
{
    /// <summary>
    /// Gets one assignment with its effective credit and dates and its current-period figures
    /// </summary>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packname details, or null when the assignment does not exist
    /// </returns>
    Task<SubsidyPackNameDTO> GetSubsidyPackNameDetailsByIdAsync(int subsidyPackNameId);

    /// <summary>
    /// Gets subsidy and packName basic information by identifier
    /// </summary>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packname basic information
    /// </returns>
    Task<SubsidyPackNameDTO> GetSubsidyPackNameBasicInformationByIdAsync(int subsidyPackNameId);

    /// <summary>
    /// Gets every live assignment of a packname with its effective credit and dates and its current-period
    /// figures
    /// </summary>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <param name="inOrderingPeriodOnly">
    /// True to return only assignments whose ordering period is open today — the storefront's case, where
    /// a manager tops up the season being ordered in; false to return every live assignment — the admin
    /// panel's case, where a season is set up before it opens
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the assignments, ordered by subsidy name
    /// </returns>
    Task<IList<SubsidyPackNameDTO>> GetSubsidyDetailsByPackNameIdAsync(int packNameId, bool inOrderingPeriodOnly);

    /// <summary>
    /// Gets the product groups a subsidy allocates, with this assignment's current-period topups and usage
    /// per group (zero when the assignment is out of period)
    /// </summary>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <param name="activeOnly">Only the product groups that are active (the ones an order can use)</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product groups, in display order
    /// </returns>
    Task<IList<SubsidyProductGroupDTO>> GetSubsidyPackNameProductGroupDetailsAsync(int subsidyPackNameId, int subsidyId, bool activeOnly = false);

    /// <summary>
    /// Gets, for every in-period assignment of the given subsidies, what it has and what it has used —
    /// money and units together. Assignments out of period have no row.
    /// </summary>
    /// <param name="subsidyIds">Subsidy identifiers</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains one row per in-period assignment
    /// </returns>
    Task<IList<SubsidyPackNameUsageDTO>> GetSubsidyUsageBySubsidyIdsAsync(IList<int> subsidyIds);

    /// <summary>
    /// Gets subsidy history
    /// </summary>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the history rows, newest first
    /// </returns>
    Task<IPagedList<SubsidyHistory>> GetSubsidyPackNameHistoryAsync(int packNameId, int subsidyId,
        int pageIndex = 0, int pageSize = int.MaxValue);
}