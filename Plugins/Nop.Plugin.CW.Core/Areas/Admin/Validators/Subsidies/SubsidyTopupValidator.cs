using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;

/// <summary>
/// The admin top-up form's rules, which were once "Reason is not empty" and nothing else.
/// </summary>
/// <remarks>
/// Negative is refused: reducing a packname's credit is a variation, or the correction action with
/// its own permission and log keyword once that exists. Zero is refused
/// because it banks an audit row and a history entry for no movement. The upper bound exists because
/// Credit is DECIMAL(18,4): a larger value is an arithmetic overflow raised by SQL Server, which
/// surfaces as a 500 inside a popup with no error handler on the AJAX call.
/// </remarks>
public partial class SubsidyTopupValidator : BaseNopValidator<SubsidyTopupModel>
{
    /// <summary>
    /// The largest amount either top-up form accepts. Two orders of magnitude below the DECIMAL(18,4)
    /// ceiling, so no sum of plausible top-ups can reach it either.
    /// </summary>
    public const decimal MaximumTopupCredit = 1_000_000m;

    /// <summary>
    /// The longest reason a top-up takes: its history row stores at most 255 characters, and a longer one failed after
    /// the top-up itself was saved
    /// </summary>
    public const int MaximumReasonLength = 255;

    public SubsidyTopupValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Reason.Required"));
        RuleFor(x => x.Reason).MaximumLength(MaximumReasonLength);

        RuleFor(x => x.Credit)
            .GreaterThan(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Credit.GreaterThanZero"));

        RuleFor(x => x.Credit)
            .LessThanOrEqualTo(MaximumTopupCredit)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Credit.Maximum"));
    }
}