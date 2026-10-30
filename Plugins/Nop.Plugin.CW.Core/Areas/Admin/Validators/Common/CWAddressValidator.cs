using FluentValidation;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Validators.Common;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Common;

public partial class CWAddressValidator : AbstractValidator<CWAddressModel>
{
    public CWAddressValidator(ILocalizationService localizationService,
        AddressSettings addressSettings,
        CustomerSettings customerSettings)
    {
        RuleFor(model => model.Address).SetValidator(new AddressValidator(addressSettings, customerSettings, localizationService));
    }
}