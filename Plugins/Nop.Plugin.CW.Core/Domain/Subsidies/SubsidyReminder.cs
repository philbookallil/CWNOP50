using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

/// <summary>
/// The last expiry reminder sent for one season of a subsidy assignment
/// </summary>
/// <remarks>
/// One row per assignment and season end. The next season of an annual subsidy, or a variation that moves the end date,
/// has another end, so its reminders start again from the first follow-up.
/// </remarks>
public class SubsidyReminder : BaseEntity
{
    /// <summary>
    /// Gets or sets the subsidy assignment identifier
    /// </summary>
    public int SubsidyPackNameId { get; set; }

    /// <summary>
    /// Gets or sets the last day of the season, on the store's calendar
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the number of the last follow-up sent for the season (1 to 3)
    /// </summary>
    public int LastFollowUpNumber { get; set; }

    /// <summary>
    /// Gets or sets when the last follow-up was sent
    /// </summary>
    public DateTime LastFollowUpOnUtc { get; set; }
}