using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;

public partial class SubsidyValidator : BaseNopValidator<SubsidyModel>
{
    public SubsidyValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Name.Required"));

        RuleFor(x => x.DurationStart).Must((x, context) =>
        {
            if (!x.DurationStart.HasValue)
                return false;

            return true;
        }).WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationStart.Required"));

        RuleFor(x => x.DurationEnd).Must((x, context) =>
        {
            if (!x.DurationEnd.HasValue)
                return false;

            return true;
        }).WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationEnd.Required"));

        RuleFor(x => x.DurationEnd)
            .GreaterThan(x => x.DurationStart.HasValue ? x.DurationStart.Value : DateTime.MinValue)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationEnd.GreaterThanDurationStart"));

        //Credit was never validated, and it is the subsidy-level allocation every assigned packname
        //reads. Zero is legitimate - a quantity-only subsidy - so the rules are non-negative and
        //bounded, not GreaterThan(0).
        RuleFor(x => x.Credit)
            .GreaterThanOrEqualTo(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Credit.NotNegative"));

        RuleFor(x => x.Credit)
            .LessThanOrEqualTo(SubsidyTopupValidator.MaximumTopupCredit)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Credit.Maximum"));
    }
}
