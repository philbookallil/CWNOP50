using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record AddProductGroupToSubsidySearchModel : BaseSearchModel
{
    #region Properties

    public int SubsidyId { get; set; }

    public string? Subsidy { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.SearchProductGroupName")]
    public string? SearchName { get; set; }

    #endregion
}
