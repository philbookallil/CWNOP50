using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record SubsidySearchModel : BaseSearchModel
{
    #region Properties

    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.SearchSubsidyName")]
    public string SearchSubsidyName { get; set; }

    #endregion
}
