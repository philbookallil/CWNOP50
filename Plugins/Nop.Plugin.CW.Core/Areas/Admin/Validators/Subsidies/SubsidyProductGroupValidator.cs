using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;

/// <summary>
/// The inline-edit rules for a subsidy's product-group allocation.
/// </summary>
/// <remarks>
/// There was no validator for this model at all, while <c>SubsidyProductGroupUpdate</c> checked
/// <c>ModelState.IsValid</c> — which, with no validator and no <c>[Range]</c> on the model, enforced
/// type binding and nothing else. A negative quantity or credit went straight onto a live allocation
/// and into every assigned packname's available credit. Zero is allowed
/// here, unlike a top-up: an allocation of zero is how a group is listed but unfunded, and the add
/// popup creates one that way.
/// </remarks>
public partial class SubsidyProductGroupValidator : BaseNopValidator<SubsidyProductGroupModel>
{
    public SubsidyProductGroupValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Quantity.NotNegative"));

        RuleFor(x => x.Quantity)
            .LessThanOrEqualTo(ProductGroupTopupValidator.MaximumTopupQuantity)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Quantity.Maximum"));

        RuleFor(x => x.Credit)
            .GreaterThanOrEqualTo(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Credit.NotNegative"));

        RuleFor(x => x.Credit)
            .LessThanOrEqualTo(SubsidyTopupValidator.MaximumTopupCredit)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Credit.Maximum"));

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.DisplayOrder.NotNegative"));
    }
}