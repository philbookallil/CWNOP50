using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The scheduled task that sends the approval reminders and expires the approvals not answered in time
/// </summary>
/// <remarks>
/// Installed by the plugin's install and update, removed by its uninstall. nop finds it by
/// <see cref="OrderApprovalDefaults.TaskTypeFullName"/> and builds it with its dependencies.
/// </remarks>
public class ProcessOrderApprovalsTask : IScheduleTask
{
    #region Fields

    protected readonly IOrderApprovalService _orderApprovalService;

    #endregion

    #region Ctor

    public ProcessOrderApprovalsTask(IOrderApprovalService orderApprovalService)
    {
        _orderApprovalService = orderApprovalService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Executes the task
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task ExecuteAsync()
    {
        await _orderApprovalService.ProcessPendingApprovalsAsync();
    }

    #endregion
}
