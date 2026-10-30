using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record SubsidyVariationModel : BaseNopEntityModel
{
    #region Properties

    public int SubsidyPackNameId { get; set; }

    public string? Subsidy { get; set; }

    public string? PackName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.DurationStart")]
    [UIHint("DateNullable")]
    public DateTime? DurationStart { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.DurationEnd")]
    [UIHint("DateNullable")]
    public DateTime? DurationEnd { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Credit")]
    [UIHint("DecimalNullable")]
    public decimal? Credit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Reason")]
    public string? Reason { get; set; }

    public bool PackNamesOwnGlobalValue { get; set; }

    public bool PackNamesOwnStartAndFinishDates { get; set; }

    #endregion
}
