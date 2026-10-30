using FluentValidation;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Tax;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using Nop.Web.Models.Customer;
using Nop.Web.Validators.Common;
using Nop.Web.Validators.Customer;

namespace Nop.Plugin.CW.Core.Validators.PackNames;

public partial class PackNameRegisterValidator : BaseNopValidator<PackNameRegisterModel>
{
    public PackNameRegisterValidator(ILocalizationService localizationService,
        IStateProvinceService stateProvinceService,
        CustomerSettings customerSettings,
        AddressSettings addressSettings,
        TaxSettings taxSettings)
    {
        RuleFor(x => x.BranchId).NotEqual(0).WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Branch.Required"));

        RuleFor(x => x.Name).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Name.Required"));

        //the gender rule needs the manager's store, which the posted form does not tell: the controller checks it

        When(x => x.AddAddress, () =>
        {
            RuleFor(model => model.Address).SetValidator(new AddressValidator(localizationService, stateProvinceService, addressSettings, customerSettings));
        });

        When(x => x.AddCustomer, () =>
        {
            RuleFor(model => model.Customer).SetValidator(new CustomerInfoValidator(localizationService, stateProvinceService, customerSettings, taxSettings));
            RuleFor(model => model.Customer).SetValidator(new AccountLengthValidator());

            //a new account's username, as nop's registration page checks it; the account validator checks it only
            //where customers may change theirs
            if (customerSettings.UsernamesEnabled && !customerSettings.AllowUsersToChangeUsernames)
            {
                RuleFor(model => model.Customer.Username).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Username.Required"));
                RuleFor(model => model.Customer.Username).IsUsername(customerSettings).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Username.NotValid"));
            }
        });

        //the lengths the database allows for the packname's text fields
        SetDatabaseValidationRules<PackName>();
    }

    /// <summary>
    /// The lengths the database allows for the new account's text fields, as the Excel import checks them. Private, so
    /// nop's validator scan does not register it for nop's own customer pages
    /// </summary>
    private sealed class AccountLengthValidator : BaseNopValidator<CustomerInfoModel>
    {
        public AccountLengthValidator()
        {
            SetDatabaseValidationRules<Customer>(PackNameServicesDefaults.ServerSuppliedCustomerFields);
        }
    }
}