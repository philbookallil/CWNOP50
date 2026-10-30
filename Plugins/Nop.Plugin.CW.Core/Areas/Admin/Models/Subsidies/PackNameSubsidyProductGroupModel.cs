using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record PackNameSubsidyProductGroupModel : BaseNopEntityModel
{
    #region Properties

    public int SubsidyId { get; set; }

    public int SubsidyPackNameId { get; set; }

    public int ProductGroupId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Name")]
    public string? Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Description")]
    public string? Description { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Quantity")]
    public int Quantity { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Credit")]
    public decimal Credit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Quantity")]
    public int TopupQuantity { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Topup.Field.Credit")]
    public decimal TopupCredit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.UsedQuantity")]
    public int UsedQuantity { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.UsedCredit")]
    public decimal UsedCredit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Icon")]
    public string? Icon { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Colour")]
    public string? Colour { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.DisplayOrder")]
    public int DisplayOrder { get; set; }

    #endregion
}
