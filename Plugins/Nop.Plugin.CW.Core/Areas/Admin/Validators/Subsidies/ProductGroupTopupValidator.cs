using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;

/// <summary>
/// The admin product-group top-up form's rules. See <see cref="SubsidyTopupValidator"/> for why each
/// one exists; the difference here is that a top-up may be quantity, credit or both, so neither field
/// is individually required and neither may be negative.
/// </summary>
public partial class ProductGroupTopupValidator : BaseNopValidator<ProductGroupTopupModel>
{
    /// <summary>
    /// The largest quantity either product-group top-up form accepts. Well inside Int32, so the sum of
    /// a period's rows cannot overflow one either - the old read-modify-write did
    /// <c>existing.Quantity += quantityToAdd</c> unchecked on an int.
    /// </summary>
    public const int MaximumTopupQuantity = 100_000;

    public ProductGroupTopupValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Reason.Required"));
        RuleFor(x => x.Reason).MaximumLength(SubsidyTopupValidator.MaximumReasonLength);

        RuleFor(x => x)
            .Must(x => x.Quantity > 0 || x.Credit > 0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.QuantityOrCreditRequired"));

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Quantity.NotNegative"));

        RuleFor(x => x.Quantity)
            .LessThanOrEqualTo(MaximumTopupQuantity)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Quantity.Maximum"));

        RuleFor(x => x.Credit)
            .GreaterThanOrEqualTo(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Credit.NotNegative"));

        RuleFor(x => x.Credit)
            .LessThanOrEqualTo(SubsidyTopupValidator.MaximumTopupCredit)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Credit.Maximum"));
    }
}