using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// Form model for adding quantity/credit to a product-group topup.
/// </summary>
public record ProductGroupTopupInputModel : BaseNopModel
{
    #region Properties

    public int SubsidyPackNameId { get; set; }

    public int ProductGroupId { get; set; }

    public string ProductGroupName { get; set; } = string.Empty;

    public string PackNameName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Quantity")]
    public int Quantity { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Credit")]
    public decimal Credit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Subsidy.ProductGroup.Topup.Fields.Reason")]
    public string? Reason { get; set; }

    #endregion
}
