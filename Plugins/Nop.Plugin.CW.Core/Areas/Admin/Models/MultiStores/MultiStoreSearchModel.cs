using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record MultiStoreSearchModel : BaseSearchModel
{
    #region Ctor

    public MultiStoreSearchModel()
    {
    }

    #endregion

    #region Properties

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.SearchStore")]
    public string SearchStore { get; set; }

    #endregion
}
