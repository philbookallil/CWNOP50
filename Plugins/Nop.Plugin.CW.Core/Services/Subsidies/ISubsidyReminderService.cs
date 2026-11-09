using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The subsidy expiry reminders: sending them, and saving when they are sent
/// </summary>
public interface ISubsidyReminderService
{
    /// <summary>
    /// Sends the follow-ups due now, in every store with subsidies and the reminders switched on: one e-mail per
    /// customer, listing each of their packname's subsidies whose season is ending with something left to spend
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SendExpiryRemindersAsync();

    /// <summary>
    /// Saves the subsidy card of Promotions › Reminders: the switch for the store scope, and when the switch is on, each
    /// follow-up on its message template
    /// </summary>
    /// <param name="storeId">The store scope the admin is editing; 0 for all stores</param>
    /// <param name="enabled">Whether the reminders are sent</param>
    /// <param name="overrideForStore">Whether the store keeps its own value of the switch (store scope only)</param>
    /// <param name="startDateUtc">The moment the reminders start from; null to start from the save when switched on</param>
    /// <param name="startDateOverrideForStore">Whether the store keeps its own start date (store scope only)</param>
    /// <param name="followUps">The follow-ups in follow-up order; any beyond the third are ignored</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SaveExpiryReminderSettingsAsync(int storeId, bool enabled, bool overrideForStore, DateTime? startDateUtc, bool startDateOverrideForStore,
        IList<SubsidyExpiryFollowUpSetting> followUps);
}