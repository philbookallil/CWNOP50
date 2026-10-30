using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record DepartmentModel : BaseNopEntityModel
{
    #region Ctor

    public DepartmentModel()
    {
        DepartmentManagerSearchModel = new DepartmentManagerSearchModel();
    }

    #endregion

    #region Properties

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Departments.Department.Field.StoreId")]
    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Departments.Department.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Departments.Department.Field.Active")]
    public bool Active { get; set; }

    public bool IsNew { get; set; }

    public DepartmentManagerSearchModel DepartmentManagerSearchModel { get; set; }

    #endregion
}
