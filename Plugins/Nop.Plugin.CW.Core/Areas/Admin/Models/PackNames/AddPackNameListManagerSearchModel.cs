using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

public record AddPackNameListManagerSearchModel : BaseSearchModel
{
    #region Ctor

    public AddPackNameListManagerSearchModel()
    {
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
    }

    #endregion

    #region Properties

    public string PackNameList { get; set; }

    public int PackNameListId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.Managers.SearchManagerName")]
    public string SearchManagerName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.Managers.SearchOtherBranches")]
    public bool SearchOtherBranches { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }

    #endregion
}
