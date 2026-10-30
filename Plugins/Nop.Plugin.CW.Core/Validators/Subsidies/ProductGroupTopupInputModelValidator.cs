using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Validators.Subsidies;

public class ProductGroupTopupInputModelValidator : BaseNopValidator<ProductGroupTopupInputModel>
{
    public ProductGroupTopupInputModelValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Reason.Required"));
        RuleFor(x => x.Reason).MaximumLength(SubsidyTopupValidator.MaximumReasonLength);

        RuleFor(x => x)
            .Must(x => x.Quantity > 0 || x.Credit > 0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Credit.Required"));

        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Credit).GreaterThanOrEqualTo(0);

        //the admin form's limits, and its messages
        RuleFor(x => x.Quantity)
            .LessThanOrEqualTo(ProductGroupTopupValidator.MaximumTopupQuantity)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Quantity.Maximum"));

        RuleFor(x => x.Credit)
            .LessThanOrEqualTo(SubsidyTopupValidator.MaximumTopupCredit)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Credit.Maximum"));
    }
}
