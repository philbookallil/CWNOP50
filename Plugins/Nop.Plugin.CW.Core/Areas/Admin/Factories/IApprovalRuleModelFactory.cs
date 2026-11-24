using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Prepares the approval panel of the branch and department forms
/// </summary>
public interface IApprovalRuleModelFactory
{
    /// <summary>
    /// Prepares the panel: shown while Approvals is on for the store
    /// </summary>
    /// <param name="model">The panel</param>
    /// <param name="storeId">The store of the branch or department</param>
    /// <param name="scopeType">What the rule is attached to</param>
    /// <param name="scopeId">Branch or department identifier; 0 for a new one</param>
    /// <param name="keepValues">Whether the panel holds a refused post, whose values are shown again; a post without the panel has none</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task PrepareApprovalRuleModelAsync(ApprovalRuleModel model, int storeId, ApprovalScopeType scopeType, int scopeId, bool keepValues);

    /// <summary>
    /// Drops a posted panel, and what was wrong with it, when Approvals is off for the store
    /// </summary>
    /// <param name="model">The posted panel</param>
    /// <param name="storeId">The store of the branch or department</param>
    /// <param name="modelState">The post's state</param>
    /// <param name="prefix">The form's property that holds the panel</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DropWhenOffAsync(ApprovalRuleModel model, int storeId, ModelStateDictionary modelState, string prefix);

    /// <summary>
    /// The rule a posted panel asks for
    /// </summary>
    /// <param name="model">The posted panel</param>
    ApprovalRuleSetup ToSetup(ApprovalRuleModel model);
}
