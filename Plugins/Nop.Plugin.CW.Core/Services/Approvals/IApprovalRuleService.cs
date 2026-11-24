using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The approval rules of the branches and departments, and the approvers they name
/// </summary>
public interface IApprovalRuleService
{
    /// <summary>
    /// Gets the approval rule of a branch or a department
    /// </summary>
    /// <param name="scopeType">What the rule is attached to</param>
    /// <param name="scopeId">Branch or department identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the rule; not required, without a threshold or e-mails, when there is none
    /// </returns>
    Task<ApprovalRuleSetup> GetSetupAsync(ApprovalScopeType scopeType, int scopeId);

    /// <summary>
    /// Saves the approval rule of a branch or a department: creates or changes its one rule, and the approvers its
    /// e-mail addresses name. A rule no longer required is switched off and keeps its threshold and approvers. Orders
    /// already waiting keep the approvers they asked.
    /// </summary>
    /// <param name="storeId">The store of the branch or department</param>
    /// <param name="scopeType">What the rule is attached to</param>
    /// <param name="scopeId">Branch or department identifier</param>
    /// <param name="setup">The rule; an e-mail address that is not valid is left out</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SaveSetupAsync(int storeId, ApprovalScopeType scopeType, int scopeId, ApprovalRuleSetup setup);

    /// <summary>
    /// Switches off the rule of a branch or a department that was deleted
    /// </summary>
    /// <param name="scopeType">What the rule is attached to</param>
    /// <param name="scopeId">Branch or department identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeactivateAsync(ApprovalScopeType scopeType, int scopeId);

    /// <summary>
    /// Gets the store's approver with an e-mail address, or creates one. An approver found switched off is switched
    /// back on: whoever names it wants it asked.
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="email">E-mail address</param>
    /// <param name="name">The approver's name; null to name a new approver by the address</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the approver
    /// </returns>
    Task<Approver> GetOrCreateApproverAsync(int storeId, string email, string name = null);
}
