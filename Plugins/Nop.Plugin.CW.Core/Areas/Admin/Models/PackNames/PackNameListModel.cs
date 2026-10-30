using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

public record PackNameListModel : BaseNopEntityModel
{
    #region Ctor

    public PackNameListModel()
    {
        ManagerSearchModel = new ManagerSearchModel();

        MultiStoreSettingsModel = new MultiStoreSettingsModel();

        Active = true;
    }

    #endregion

    #region Properties

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Store")]
    public int? StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Store")]
    public string Store { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Branch")]
    public int? BranchId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Branch")]
    public string Branch { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.AllManagers")]
    public bool AllManagers { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.ParentManagers")]
    public bool ParentManagers { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.IsDefault")]
    public bool IsDefault { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Active")]
    public bool Active { get; set; }

    public bool IsNew { get; set; }

    public ManagerSearchModel ManagerSearchModel { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }

    #endregion
}
