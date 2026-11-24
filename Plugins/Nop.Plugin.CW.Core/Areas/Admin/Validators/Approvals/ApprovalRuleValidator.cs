using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Approvals;

/// <summary>
/// Checks the approval panel of a branch or a department while approval is required; a rule switched off keeps whatever
/// it had
/// </summary>
public partial class ApprovalRuleValidator : BaseNopValidator<ApprovalRuleModel>
{
    public ApprovalRuleValidator(ILocalizationService localizationService)
    {
        When(model => model.Available && model.Required, () =>
        {
            RuleFor(model => model.Threshold)
                .Must(OrderApprovalRuleHelper.IsValidThreshold)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ApprovalRule.Threshold.Invalid"));

            RuleFor(model => model.Emails)
                .Must(emails => OrderApprovalEmailHelper.Parse(emails).Count > 0)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ApprovalRule.Emails.Required"));

            RuleFor(model => model.Emails)
                .Must(emails => OrderApprovalEmailHelper.Parse(emails).All(OrderApprovalEmailHelper.IsValid))
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ApprovalRule.Emails.Invalid"));
        });
    }
}
