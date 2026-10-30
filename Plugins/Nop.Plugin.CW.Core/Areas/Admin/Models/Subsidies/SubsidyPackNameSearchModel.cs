using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record SubsidyPackNameSearchModel : BaseSearchModel
{
    #region Properties

    public int SubsidyId { get; set; }

    #endregion
}
