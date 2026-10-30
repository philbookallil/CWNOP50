using FluentValidation;
using Nop.Data;
using Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.MultiStores;

public partial class DepartmentValidator : BaseNopValidator<DepartmentModel>
{
    public DepartmentValidator(ILocalizationService localizationService, IRepository<Department> departmentRepository)
    {
        RuleFor(x => x.StoreId)
            .NotEqual(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Departments.Department.Field.StoreId.Required"));

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Departments.Department.Field.Name.Required"))
            .MustAsync(async (model, name, _) =>
            {
                if (string.IsNullOrWhiteSpace(name) || model.StoreId == 0)
                    return true;

                var exists = await departmentRepository.Table
                    .AnyAsync(d => d.Name == name && d.StoreId == model.StoreId && d.Id != model.Id && !d.Deleted);

                return !exists;
            })
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Departments.Department.Field.Name.Unique"));

        SetDatabaseValidationRules<Department>();
    }
}
