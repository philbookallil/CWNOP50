using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record ProductGroupProductSearchModel : BaseSearchModel
{
    #region Properties

    public int ProductGroupId { get; set; }

    #endregion
}
