namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// One subsidy assignment whose season is ending, as the expiry reminder reads it and as the e-mail lists it
/// </summary>
public class SubsidyExpiryReminderItem
{
    /// <summary>
    /// Gets or sets the subsidy assignment identifier
    /// </summary>
    public int SubsidyPackNameId { get; set; }

    /// <summary>
    /// Gets or sets the packname identifier
    /// </summary>
    public int PackNameId { get; set; }

    /// <summary>
    /// Gets or sets the packname name
    /// </summary>
    public string PackName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the packname's own customer account, the one reminded
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the subsidy identifier
    /// </summary>
    public int SubsidyId { get; set; }

    /// <summary>
    /// Gets or sets the subsidy name
    /// </summary>
    public string Subsidy { get; set; }

    /// <summary>
    /// Gets or sets the subsidy's own start date
    /// </summary>
    public DateTime DurationStart { get; set; }

    /// <summary>
    /// Gets or sets the subsidy's own end date
    /// </summary>
    public DateTime DurationEnd { get; set; }

    /// <summary>
    /// Gets or sets the start date of the assignment's variation, if it has one
    /// </summary>
    public DateTime? VariationDurationStart { get; set; }

    /// <summary>
    /// Gets or sets the end date of the assignment's variation, if it has one
    /// </summary>
    public DateTime? VariationDurationEnd { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the period repeats every year
    /// </summary>
    public bool AnnualRecurrence { get; set; }

    /// <summary>
    /// Gets or sets the last day of the season, on the store's calendar
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the moment the season ends: 23:59:59 of its last day in the store's time zone, as UTC
    /// </summary>
    public DateTime PeriodEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the days left in the season, today included (1 on the last day)
    /// </summary>
    public int DaysLeft { get; set; }

    /// <summary>
    /// Gets or sets the number of the follow-up due for this assignment (1 to 3)
    /// </summary>
    public int FollowUpNumber { get; set; }

    /// <summary>
    /// Gets or sets the credit left to spend in the season: the subsidy's and its product groups', top-ups included
    /// </summary>
    public decimal RemainingCredit { get; set; }

    /// <summary>
    /// Gets or sets the items left to order in the season, top-ups included
    /// </summary>
    public int RemainingUnits { get; set; }
}