using FluentValidation;
using Nop.Data;
using Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.MultiStores;

public partial class CostCenterValidator : BaseNopValidator<CostCenterModel>
{
    public CostCenterValidator(ILocalizationService localizationService, IRepository<CostCenter> costCenterRepository)
    {
        RuleFor(x => x.StoreId)
            .NotEqual(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.StoreId.Required"));

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Name.Required"))
            .MustAsync(async (model, name, _) =>
            {
                if (string.IsNullOrWhiteSpace(name) || model.StoreId == 0)
                    return true;

                var exists = await costCenterRepository.Table
                    .AnyAsync(cc => cc.Name == name && cc.StoreId == model.StoreId && cc.Id != model.Id && !cc.Deleted);

                return !exists;
            })
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Name.Unique"));

        SetDatabaseValidationRules<CostCenter>();
    }
}
