using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record CostCenterSearchModel : BaseSearchModel
{
    #region Properties

    public int? StoreId { get; set; }

    public int? BranchId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CostCenters.SearchCostCenterName")]
    public string SearchCostCenterName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CostCenters.SearchInBranches")]
    public bool SearchInBranches { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CostCenters.SearchBranchName")]
    public string SearchBranchName { get; set; }

    #endregion
}
