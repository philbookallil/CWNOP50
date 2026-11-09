using Nop.Core.Configuration;

namespace Nop.Plugin.CW.Core.Domain.Settings;

/// <summary>
/// The subsidy card of Promotions › Reminders, per store
/// </summary>
/// <remarks>
/// When each follow-up is sent is stored on its message template (<c>DelayBeforeSend</c>, <c>DelayPeriod</c>), as nop
/// stores it for its own reminders; this class holds only the switch.
/// </remarks>
public class SubsidyReminderSettings : ISettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the customer of a packname is reminded before a subsidy's ordering period
    /// ends
    /// </summary>
    public bool ExpiryRemindersEnabled { get; set; }

    /// <summary>
    /// Gets or sets the moment the reminders start from: a follow-up that fell due before it is not sent. Filled with the
    /// moment of the save when the reminders are switched on without one, so switching on never sends a backlog.
    /// </summary>
    public DateTime? ExpiryRemindersStartDateUtc { get; set; }
}