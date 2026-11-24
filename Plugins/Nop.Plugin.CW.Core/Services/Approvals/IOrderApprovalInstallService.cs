namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Installs what the order approvals need besides their tables: the e-mails and the scheduled task
/// </summary>
public interface IOrderApprovalInstallService
{
    /// <summary>
    /// Installs each e-mail and the task once: a store that has edited an e-mail, or changed the task, keeps that
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InstallOrUpdateAsync();

    /// <summary>
    /// Removes the scheduled task, which the scheduler could not find the type of once the plugin is gone
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task UninstallAsync();
}
