using Nop.Plugin.CW.Core.Domain.Messages;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The fixed values of the subsidy expiry reminders
/// </summary>
public static class SubsidyReminderDefaults
{
    /// <summary>
    /// The follow-up templates, in follow-up order: #1 is sent the longest before the end
    /// </summary>
    public static string[] ExpiryFollowUpTemplates =>
    [
        CWMessageTemplateSystemNames.SubsidyExpiryFollowUp1,
        CWMessageTemplateSystemNames.SubsidyExpiryFollowUp2,
        CWMessageTemplateSystemNames.SubsidyExpiryFollowUp3
    ];

    /// <summary>
    /// The type name nop's scheduler finds the task by
    /// </summary>
    public static string ExpiryTaskTypeFullName => typeof(ProcessSubsidyExpiryRemindersTask).FullName;

    /// <summary>
    /// The task's name on System › Schedule tasks
    /// </summary>
    public const string ExpiryTaskName = "Process subsidy expiry reminders";

    /// <summary>
    /// How often the task runs: hourly, the finest lead time a follow-up can have
    /// </summary>
    public const int ExpiryTaskSeconds = 60 * 60;

    /// <summary>
    /// How many customers' reminders are read and sent together
    /// </summary>
    public const int ProcessingBatchSize = 100;
}