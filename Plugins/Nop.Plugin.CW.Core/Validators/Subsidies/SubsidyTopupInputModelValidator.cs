using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Validators.Subsidies;

public class SubsidyTopupInputModelValidator : BaseNopValidator<SubsidyTopupInputModel>
{
    public SubsidyTopupInputModelValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Subsidy.Topup.Fields.Reason.Required"));
        RuleFor(x => x.Reason).MaximumLength(SubsidyTopupValidator.MaximumReasonLength);

        RuleFor(x => x.Credit)
            .GreaterThan(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Subsidy.Topup.Fields.Credit.Required"));

        //the admin form's limits, and its messages
        RuleFor(x => x.Credit)
            .LessThanOrEqualTo(SubsidyTopupValidator.MaximumTopupCredit)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Credit.Maximum"));
    }
}
