using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// The subsidy card of Promotions › Reminders
/// </summary>
public class SubsidyReminderModelFactory : ISubsidyReminderModelFactory
{
    #region Fields

    protected readonly IMessageTemplateService _messageTemplateService;
    protected readonly IScheduleTaskService _scheduleTaskService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public SubsidyReminderModelFactory(IMessageTemplateService messageTemplateService,
        IScheduleTaskService scheduleTaskService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _messageTemplateService = messageTemplateService;
        _scheduleTaskService = scheduleTaskService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepares the card for the store scope the admin is editing
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the card's model
    /// </returns>
    public virtual async Task<SubsidyExpiryRemindersModel> PrepareSubsidyExpiryRemindersModelAsync()
    {
        var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var reminderSettings = await _settingService.LoadSettingAsync<SubsidyReminderSettings>(storeId);

        var model = new SubsidyExpiryRemindersModel
        {
            ActiveStoreScopeConfiguration = storeId,
            Enabled = reminderSettings.ExpiryRemindersEnabled,
            StartDateUtc = reminderSettings.ExpiryRemindersStartDateUtc
        };

        if (storeId > 0)
        {
            model.Enabled_OverrideForStore = await _settingService.SettingExistsAsync(reminderSettings, settings => settings.ExpiryRemindersEnabled, storeId);
            model.StartDateUtc_OverrideForStore = await _settingService.SettingExistsAsync(reminderSettings, settings => settings.ExpiryRemindersStartDateUtc, storeId);
        }

        var templateNames = SubsidyReminderDefaults.ExpiryFollowUpTemplates;

        for (var index = 0; index < templateNames.Length; index++)
        {
            //the first template of the name, the one nop's reminders read and write
            var template = (await _messageTemplateService.GetMessageTemplatesByNameAsync(templateNames[index], storeId)).FirstOrDefault();

            model.FollowUps.Add(new SubsidyExpiryFollowUpModel
            {
                Number = index + 1,
                Enabled = template?.IsActive ?? false,
                DelayBeforeSend = template?.DelayBeforeSend ?? 0,
                DelayPeriodId = template?.DelayPeriodId ?? 0
            });
        }

        var task = await _scheduleTaskService.GetTaskByTypeAsync(SubsidyReminderDefaults.ExpiryTaskTypeFullName);

        model.TaskFound = task != null;
        model.TaskEnabled = task?.Enabled ?? false;
        model.TaskName = task?.Name ?? SubsidyReminderDefaults.ExpiryTaskName;

        return model;
    }

    #endregion
}