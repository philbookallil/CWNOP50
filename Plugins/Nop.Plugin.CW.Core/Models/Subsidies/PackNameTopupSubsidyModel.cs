using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// One subsidy row in the Change Topup panel.
/// Id = SubsidyPackNameId (the mapping record, not the Subsidy itself).
/// </summary>
public record PackNameTopupSubsidyModel : BaseNopEntityModel
{
    #region Ctor

    public PackNameTopupSubsidyModel()
    {
        ProductGroups = new List<PackNameTopupProductGroupModel>();
    }

    #endregion

    #region Properties

    public int SubsidyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Base global credit allocated by the subsidy.</summary>
    public decimal Credit { get; set; }

    public string CreditFormatted { get; set; } = string.Empty;

    /// <summary>Additional topup credit already added for the current period.</summary>
    public decimal TopupCredit { get; set; }

    public string TopupCreditFormatted { get; set; } = string.Empty;

    public IList<PackNameTopupProductGroupModel> ProductGroups { get; set; }

    #endregion
}
