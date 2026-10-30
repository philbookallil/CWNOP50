using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record CostCenterModel : BaseNopEntityModel
{
    #region Ctor

    public CostCenterModel()
    {
        CWAddressSearchModel = new CWAddressSearchModel();
    }

    #endregion

    #region Properties

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.StoreId")]
    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Branch")]
    public int? BranchId { get; set; }

    public string Branch { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Active")]
    public bool Active { get; set; }

    public bool IsNew { get; set; }

    public CWAddressSearchModel CWAddressSearchModel { get; set; }

    #endregion
}
