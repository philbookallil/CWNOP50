using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record SubsidyTopupModel : BaseNopEntityModel
{
    #region Properties

    public int SubsidyPackNameId { get; set; }

    public string? Subsidy { get; set; }

    public string? PackName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Credit")]
    public decimal Credit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Topup.Field.Reason")]
    public string? Reason { get; set; }


    #endregion
}
