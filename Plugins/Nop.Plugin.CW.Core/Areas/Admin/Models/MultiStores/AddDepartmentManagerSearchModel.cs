using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record AddDepartmentManagerSearchModel : BaseSearchModel
{
    #region Ctor

    public AddDepartmentManagerSearchModel()
    {
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
    }

    #endregion

    #region Properties

    public string Department { get; set; }

    public int DepartmentId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Departments.Department.Managers.SearchManagerName")]
    public string SearchManagerName { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }

    #endregion
}
