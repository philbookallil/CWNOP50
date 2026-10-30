using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// Form model for adding credit to a subsidy-level topup.
/// </summary>
public record SubsidyTopupInputModel : BaseNopModel
{
    #region Properties

    public int SubsidyPackNameId { get; set; }

    public string SubsidyName { get; set; } = string.Empty;

    public string PackNameName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.CW.Core.Subsidy.Topup.Fields.Credit")]
    public decimal Credit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Subsidy.Topup.Fields.Reason")]
    public string? Reason { get; set; }

    #endregion
}
