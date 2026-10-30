using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record ProductGroupSearchModel : BaseSearchModel
{
    #region Properties

    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.SearchProductGroupName")]
    public string SearchProductGroupName { get; set; }

    #endregion
}
