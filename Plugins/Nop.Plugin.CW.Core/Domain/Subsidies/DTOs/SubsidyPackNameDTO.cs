using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// One subsidy assignment as a screen shows it: the packname, the subsidy, the EFFECTIVE credit and dates
/// (the variation's where the store lets a packname own them, else the subsidy's), and the figures for the
/// current period. The period figures are zero when the assignment is not in its ordering period — the
/// row is still returned, because an admin must be able to see, top up and vary an assignment before its
/// season opens.
/// </summary>
public class SubsidyPackNameDTO : BaseEntity
{
    #region Properties

    public int StoreId { get; set; }

    /// <summary>The subsidy is active. The assignment's own Active flag is not part of this — see <see cref="AssignmentActive"/>.</summary>
    public bool Active { get; set; }

    /// <summary>The assignment row itself is active.</summary>
    public bool AssignmentActive { get; set; }

    #region PackName

    public int PackNameId { get; set; }

    public string? PackName { get; set; }

    public string? PrintName { get; set; }

    public string? Branch { get; set; }

    public string? Gender { get; set; }

    public string? Department { get; set; }

    public string? CostCenter { get; set; }

    public bool IsCustomer { get; set; }

    public string? EmployeeNumber { get; set; }

    public string? POEntry { get; set; }

    #endregion

    #region Subsidy

    public int SubsidyId { get; set; }

    public string? Subsidy { get; set; }

    public string? Description { get; set; }

    /// <summary>Effective start: the variation's when the store allows it and one is set, else the subsidy's.</summary>
    public DateTime DurationStart { get; set; }

    /// <summary>Effective end: the variation's when the store allows it and one is set, else the subsidy's.</summary>
    public DateTime DurationEnd { get; set; }

    public bool AnnualRecurrence { get; set; }

    public bool OrderOutsidePeriod { get; set; }

    public bool OnlyAllocatedProducts { get; set; }

    public bool IncludeManagers { get; set; }

    public bool RestrictToAllocatedQty { get; set; }

    /// <summary>Effective credit: the variation's when the store allows it and one is set, else the subsidy's.</summary>
    public decimal Credit { get; set; }

    /// <summary>Topups in the current period; zero out of period.</summary>
    public decimal TopupCredit { get; set; }

    /// <summary>Consumed from the allocation in the current period; zero out of period.</summary>
    public decimal UsedCredit { get; set; }

    /// <summary>Consumed from the topups in the current period; zero out of period.</summary>
    public decimal UsedCreditTopup { get; set; }

    public int ProductGroupsCount { get; set; }

    /// <summary>Whether the assignment is in its ordering period today, by the store's clock.</summary>
    public bool InOrderingPeriod { get; set; }

    public decimal? VariationCredit { get; set; }

    public DateTime? VariationDurationStart { get; set; }

    public DateTime? VariationDurationEnd { get; set; }

    #endregion

    #endregion
}