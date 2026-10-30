using FluentValidation;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Validators.Customers;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Customers;

/// <summary>
/// The rules of the admin customer create and edit forms: nop's own customer validator, applied to CW's customer model.
/// nop finds a validator by the exact type of the posted model, so nop's rules reach CW's form only through this class
/// </summary>
public partial class ExtendedCustomerValidator : AbstractValidator<ExtendedCustomerModel>
{
    public ExtendedCustomerValidator(CustomerSettings customerSettings,
        ICustomerService customerService,
        ILocalizationService localizationService,
        IStateProvinceService stateProvinceService)
    {
        Include(new CustomerValidator(customerSettings, customerService, localizationService, stateProvinceService));
    }
}
