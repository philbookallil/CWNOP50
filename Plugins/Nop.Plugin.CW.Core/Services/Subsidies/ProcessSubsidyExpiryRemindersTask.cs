using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The scheduled task that sends the subsidy expiry reminders
/// </summary>
/// <remarks>
/// Installed by the plugin's install and update, removed by its uninstall. nop finds it by
/// <see cref="SubsidyReminderDefaults.ExpiryTaskTypeFullName"/> and builds it with its dependencies.
/// </remarks>
public class ProcessSubsidyExpiryRemindersTask : IScheduleTask
{
    #region Fields

    protected readonly ISubsidyReminderService _subsidyReminderService;

    #endregion

    #region Ctor

    public ProcessSubsidyExpiryRemindersTask(ISubsidyReminderService subsidyReminderService)
    {
        _subsidyReminderService = subsidyReminderService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Executes the task
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task ExecuteAsync()
    {
        await _subsidyReminderService.SendExpiryRemindersAsync();
    }

    #endregion
}