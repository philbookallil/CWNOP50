using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record PackNameSubsidyProductGroupSearchModel : BaseSearchModel
{
    #region Properties

    public int SubsidyPackNameId { get; set; }

    #endregion
}
