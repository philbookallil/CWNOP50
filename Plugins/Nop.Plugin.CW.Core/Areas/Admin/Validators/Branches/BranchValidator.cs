using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Branches;

public partial class BranchValidator : BaseNopValidator<BranchModel>
{
    public BranchValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.StoreId)
            .NotEqual(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Field.StoreId.Required"));

        RuleFor(x => x.BranchCode)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Field.BranchCode.Required"));

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Branches.Branch.Field.Name.Required"));

        SetDatabaseValidationRules<Branch>();
    }
}
