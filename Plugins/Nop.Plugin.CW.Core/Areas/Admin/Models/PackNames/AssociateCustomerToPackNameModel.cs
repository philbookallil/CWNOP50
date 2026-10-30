using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

public partial record AssociateCustomerToPackNameModel : BaseNopModel
{
    #region Properties

    public int AssociatedToCustomerId { get; set; }

    #endregion
}
