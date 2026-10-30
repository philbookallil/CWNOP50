using FluentValidation;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;

public partial class ProductGroupValidator : BaseNopValidator<ProductGroupModel>
{
    public ProductGroupValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Name.Required"));
    }
}
