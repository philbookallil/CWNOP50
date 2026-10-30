using Nop.Plugin.CW.Core.Helpers;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// One assignment as the customer sees it.
/// </summary>
public record CustomerSubsidyModel : BaseNopModel
{
    public int SubsidyPackNameId { get; set; }

    public int SubsidyId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public CustomerSubsidyStatus Status { get; set; }

    public bool IsOpen => Status == CustomerSubsidyStatus.Open;

    /// <summary>
    /// The season's first and last day, formatted for the customer.
    /// </summary>
    public string SeasonStart { get; set; }

    public string SeasonEnd { get; set; }

    public bool AnnualRecurrence { get; set; }

    /// <summary>
    /// True when the subsidy allocates money (credit or top-up); false on a units-only subsidy.
    /// </summary>
    public bool HasCredit { get; set; }

    public string CreditFormatted { get; set; }

    public string TopupCreditFormatted { get; set; }

    public string TotalCreditFormatted { get; set; }

    public string UsedCreditFormatted { get; set; }

    public string RemainingCreditFormatted { get; set; }

    public int UsedCreditPercent { get; set; }

    /// <summary>
    /// True when at least one product group allocates units.
    /// </summary>
    public bool HasUnits { get; set; }

    public int TotalUnits { get; set; }

    public int UsedUnits { get; set; }

    public int RemainingUnits { get; set; }

    public int UsedUnitsPercent { get; set; }

    public IList<CustomerSubsidyProductGroupModel> ProductGroups { get; set; } = new List<CustomerSubsidyProductGroupModel>();

    /// <summary>
    /// The history of the season the figures above are for: what stands for it (the assignment, the allocations,
    /// changes) and its own top-ups and orders, newest first
    /// </summary>
    public IList<CustomerSubsidyHistoryModel> History { get; set; } = new List<CustomerSubsidyHistoryModel>();

    /// <summary>
    /// The seasons before it, newest first; empty for a one-off subsidy or the first season
    /// </summary>
    public IList<CustomerSubsidySeasonModel> EarlierSeasons { get; set; } = new List<CustomerSubsidySeasonModel>();
}

/// <summary>
/// One earlier season of an assignment, as a closed statement: its dates, what was topped up and used in it, and its history
/// </summary>
public record CustomerSubsidySeasonModel : BaseNopModel
{
    public string SeasonStart { get; set; }

    public string SeasonEnd { get; set; }

    /// <summary>The credit topped up in the season, formatted; empty when none</summary>
    public string ToppedUpFormatted { get; set; }

    /// <summary>The credit orders used in the season, less what returns gave back, formatted; empty when none</summary>
    public string UsedFormatted { get; set; }

    /// <summary>The product group units orders used in the season, less what returns gave back</summary>
    public int UsedUnits { get; set; }

    /// <summary>Whether anything was topped up or ordered in the season</summary>
    public bool HasActivity { get; set; }

    public IList<CustomerSubsidyHistoryModel> History { get; set; } = new List<CustomerSubsidyHistoryModel>();
}