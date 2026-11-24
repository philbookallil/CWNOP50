using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Settings;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Prepares the approval panel of the branch and department forms
/// </summary>
public class ApprovalRuleModelFactory : IApprovalRuleModelFactory
{
    #region Fields

    protected readonly IApprovalRuleService _approvalRuleService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public ApprovalRuleModelFactory(IApprovalRuleService approvalRuleService, ICWFeatureService cwFeatureService)
    {
        _approvalRuleService = approvalRuleService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Methods

    public virtual async Task PrepareApprovalRuleModelAsync(ApprovalRuleModel model, int storeId, ApprovalScopeType scopeType, int scopeId, bool keepValues)
    {
        ArgumentNullException.ThrowIfNull(model);

        //a form posted without the panel has no values to keep: shown now, the panel needs the rule's own
        var hasPostedValues = keepValues && model.Available;

        model.Available = await _cwFeatureService.IsEnabledAsync(CWFeature.Approvals, storeId);

        if (!model.Available || hasPostedValues)
            return;

        var setup = await _approvalRuleService.GetSetupAsync(scopeType, scopeId);

        model.Required = setup.Required;
        model.Threshold = setup.Threshold;
        model.Emails = OrderApprovalEmailHelper.Format(setup.Emails);
    }

    public virtual async Task DropWhenOffAsync(ApprovalRuleModel model, int storeId, ModelStateDictionary modelState, string prefix)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        if (model?.Available != true || await _cwFeatureService.IsEnabledAsync(CWFeature.Approvals, storeId))
            return;

        model.Available = false;

        foreach (var key in modelState.FindKeysWithPrefix(prefix).Select(entry => entry.Key).ToList())
            modelState.Remove(key);
    }

    public virtual ApprovalRuleSetup ToSetup(ApprovalRuleModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ApprovalRuleSetup(model.Required, model.Threshold, OrderApprovalEmailHelper.Parse(model.Emails));
    }

    #endregion
}
