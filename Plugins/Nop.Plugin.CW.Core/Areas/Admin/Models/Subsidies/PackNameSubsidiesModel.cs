using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record PackNameSubsidiesModel : BaseNopEntityModel
{
    #region Ctor

    public PackNameSubsidiesModel()
    {
        Subsidies = new List<PackNameSubsidyModel>();
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
    }

    #endregion

    #region Properties

    public int StoreId { get; set; }

    public int PackNameId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Store")]
    public string Store { get; set; }

    public string Prefix { get; set; }

    public IList<PackNameSubsidyModel> Subsidies { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }

    #endregion
}
