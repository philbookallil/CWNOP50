using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;

public record BranchSearchModel : BaseSearchModel
{
    #region Properties

    public int StoreId { get; set; }

    #endregion
}
