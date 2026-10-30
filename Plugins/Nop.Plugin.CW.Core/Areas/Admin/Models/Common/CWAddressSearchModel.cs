using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Common;

public partial record CWAddressSearchModel : BaseSearchModel
{
    #region Properties

    public int EntityId { get; set; }

    #endregion
}
