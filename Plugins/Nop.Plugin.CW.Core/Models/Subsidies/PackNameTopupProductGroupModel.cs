using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// One product-group row inside a subsidy in the Change Topup panel.
/// Id = ProductGroupId.
/// </summary>
public record PackNameTopupProductGroupModel : BaseNopEntityModel
{
    #region Properties

    /// <summary>
    /// The parent SubsidyPackNameId
    /// </summary>
    public int SubsidyPackNameId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string Colour { get; set; } = string.Empty;

    /// <summary>Base quantity allocated by the subsidy product group.</summary>
    public int Quantity { get; set; }

    /// <summary>Topup quantity already added for the current period.</summary>
    public int TopupQuantity { get; set; }

    /// <summary>Base credit allocated by the subsidy product group.</summary>
    public decimal Credit { get; set; }

    public string CreditFormatted { get; set; } = string.Empty;

    /// <summary>Topup credit already added for the current period.</summary>
    public decimal TopupCredit { get; set; }

    public string TopupCreditFormatted { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    #endregion
}
