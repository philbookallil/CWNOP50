using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record DepartmentSearchModel : BaseSearchModel
{
    #region Ctor

    public DepartmentSearchModel()
    {
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
    }

    #endregion

    #region Properties

    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Departments.SearchDepartmentName")]
    public string SearchDepartmentName { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }

    #endregion
}
