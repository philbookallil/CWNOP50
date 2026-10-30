using FluentValidation;
using Nop.Plugin.CW.Freight.Domain;
using Nop.Plugin.CW.Freight.Models;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Freight.Validators;

public partial class CWShippingMethodValidator : BaseNopValidator<CWShippingMethodModel>
{
    public CWShippingMethodValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Shipping method name required.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Shipping method description required.");
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0).WithMessage("Rate must be a positive number");

        //the store, role and country lists may be empty: no store means every store, no country restricts nothing
        SetDatabaseValidationRules<CWShippingMethod>();
    }
}
