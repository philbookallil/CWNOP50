using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.PackNames;

public partial class PackNameListValidator : BaseNopValidator<PackNameListModel>
{
    public PackNameListValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Name.Required"));
        RuleFor(x => x.StoreId)
            .NotNull()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Store.Required"));
        RuleFor(x => x.StoreId)
            .NotEqual(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Store.Required"));
    }
}
