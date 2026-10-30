using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record SubsidyModel : BaseNopEntityModel
{
    #region Ctor

    public SubsidyModel()
    {
        SubsidyPackNameSearchModel = new SubsidyPackNameSearchModel();
        SubsidyProductGroupSearchModel = new SubsidyProductGroupSearchModel();
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
        Active = true;
    }

    #endregion

    #region Properties

    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Name")]
    public string? Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Description")]
    public string? Description { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Credit")]
    public decimal Credit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationStart")]
    [UIHint("DateNullable")]
    public DateTime? DurationStart { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.DurationEnd")]
    [UIHint("DateNullable")]
    public DateTime? DurationEnd { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.AnnualRecurrence")]
    public bool AnnualRecurrence { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.OrderOutsidePeriod")]
    public bool OrderOutsidePeriod { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.OnlyAllocatedProducts")]
    public bool OnlyAllocatedProducts { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.IncludeManagers")]
    public bool IncludeManagers { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.RestrictToAllocatedQty")]
    public bool RestrictToAllocatedQty { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.Active")]
    public bool Active { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.StartDate")]
    public string? StartDate { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.EndDate")]
    public string? EndDate { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.AllocatedStaff")]
    public int AllocatedStaff { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.TotalCredit")]
    public decimal TotalCredit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Subsidies.Subsidy.Field.UsedPercent")]
    public string? UsedPercent { get; set; }

    public bool IsNew { get; set; }

    public string? PrimaryStoreCurrencyCode { get; set; }

    public SubsidyPackNameSearchModel SubsidyPackNameSearchModel { get; set; }

    public SubsidyProductGroupSearchModel SubsidyProductGroupSearchModel { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }

    #endregion
}
