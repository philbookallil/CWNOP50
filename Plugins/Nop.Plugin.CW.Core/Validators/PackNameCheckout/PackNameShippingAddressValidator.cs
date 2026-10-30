using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Models.PackNameCheckout;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using Nop.Web.Validators.Common;

namespace Nop.Plugin.CW.Core.Validators.PackNameCheckout;

/// <summary>
/// An address added at the packname checkout takes nop's address rules, as an address added in My account does
/// </summary>
public partial class PackNameShippingAddressValidator : BaseNopValidator<PackNameShippingAddressModel>
{
    public PackNameShippingAddressValidator(ILocalizationService localizationService,
        IStateProvinceService stateProvinceService,
        AddressSettings addressSettings,
        CustomerSettings customerSettings)
    {
        RuleFor(model => model.Address).SetValidator(new AddressValidator(localizationService, stateProvinceService, addressSettings, customerSettings));
    }
}