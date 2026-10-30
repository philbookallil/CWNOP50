using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.PackNames;

public partial class PackNameValidator : BaseNopValidator<PackNameModel>
{
    public PackNameValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.BranchId)
            .NotEqual(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Branch.Required"));

        RuleFor(x => x.Name).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Name.Required"));

        //the lengths the database allows for the packname's text fields
        SetDatabaseValidationRules<PackName>();
    }
}