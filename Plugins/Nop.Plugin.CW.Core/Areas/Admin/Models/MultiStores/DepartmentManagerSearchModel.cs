using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record DepartmentManagerSearchModel : BaseSearchModel
{
    #region Ctor

    public DepartmentManagerSearchModel()
    {
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
    }

    #endregion

    #region Properties

    public int DepartmentId { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }

    #endregion
}
