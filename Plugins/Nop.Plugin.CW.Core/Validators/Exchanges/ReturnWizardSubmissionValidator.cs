using FluentValidation;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Validators.Exchanges;

public class ReturnWizardSubmissionValidator : BaseNopValidator<ReturnWizardSubmission>
{
    public ReturnWizardSubmissionValidator(ILocalizationService localizationService)
    {
        RuleFor(submission => submission.Mode)
            .NotNull()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.ModeRequired"));

        RuleFor(submission => submission.Lines)
            .Must(lines => lines.Any(line => line.Quantity > 0))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.NoItemsSelected"));

        // Exchange-only rules. A credit note needs no type and no replacement.
        RuleForEach(submission => submission.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.ExchangeType)
                    .NotNull()
                    .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.ExchangeTypeRequired"))
                    .When(l => l.Quantity > 0);

                line.RuleFor(l => l.ReplacementProductId)
                    .NotNull()
                    .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.ReplacementRequired"))
                    .When(l => l.NeedsReplacement);
            })
            .When(submission => submission.Mode == ExchangeReturnMode.Exchange);
    }
}