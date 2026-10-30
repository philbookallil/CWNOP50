using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record ProductGroupTopupModel : BaseNopEntityModel
{
    #region Properties

    public int SubsidyPackNameId { get; set; }

    public int ProductGroupId { get; set; }

    public string? ProductGroup { get; set; }

    public string? PackName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Quantity")]
    public int Quantity { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Credit")]
    public decimal Credit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Reason")]
    public string? Reason { get; set; }

    #endregion
}
