using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

public partial record ManagerSearchModel : BaseSearchModel
{
    #region Properties

    public int PackNameListId { get; set; }

    #endregion
}
