using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// The subsidy card of Promotions › Reminders
/// </summary>
public interface ISubsidyReminderModelFactory
{
    /// <summary>
    /// Prepares the card for the store scope the admin is editing
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the card's model
    /// </returns>
    Task<SubsidyExpiryRemindersModel> PrepareSubsidyExpiryRemindersModelAsync();
}