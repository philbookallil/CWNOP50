using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;

/// <summary>
/// The admin variation form's rules. Every field but the reason is optional, so each rule only fires
/// when its field was supplied.
/// </summary>
/// <remarks>
/// A variation's dates are read as a pair: the effective-end rule falls back to the subsidy's own end
/// whenever the variation has no end, so a lone start would silently be a no-op and a lone end would
/// apply to a start the admin never saw. Credit is bounded the same way the top-up forms bound it,
/// because it lands in the same DECIMAL(18,4) column.
/// </remarks>
public partial class SubsidyVariationValidator : BaseNopValidator<SubsidyVariationModel>
{
    public SubsidyVariationValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Reason.Required"));

        RuleFor(x => x.DurationEnd)
            .Must((model, end) => end!.Value > model.DurationStart!.Value)
            .When(x => x.DurationStart.HasValue && x.DurationEnd.HasValue)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.DurationEnd.GreaterThanDurationStart"));

        RuleFor(x => x.DurationEnd)
            .Must((model, _) => model.DurationStart.HasValue == model.DurationEnd.HasValue)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Dates.BothOrNeither"));

        RuleFor(x => x.Credit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Credit.HasValue)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Credit.NotNegative"));

        RuleFor(x => x.Credit)
            .LessThanOrEqualTo(SubsidyTopupValidator.MaximumTopupCredit)
            .When(x => x.Credit.HasValue)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Credit.Maximum"));
    }
}