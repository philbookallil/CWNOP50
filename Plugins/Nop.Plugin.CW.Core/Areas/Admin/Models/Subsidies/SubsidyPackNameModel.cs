using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record SubsidyPackNameModel : BaseNopEntityModel
{
    #region Properties

    public int SubsidyId { get; set; }

    public int PackNameId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Name")]
    public string? Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.PrintName")]
    public string? PrintName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Branch")]
    public string? Branch { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Department")]
    public string? Department { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.CostCenter")]
    public string? CostCenter { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Active")]
    public bool Active { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender")]
    public string? Gender { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.EmployeeNumber")]
    public string? EmployeeNumber { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.POEntry")]
    public string? POEntry { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.IsCustomer")]
    public bool IsCustomer { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.UsedPercent")]
    public string? UsedPercent { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.SubsidyVariation")]
    public string SubsidyVariation { get; set; }

    #endregion
}
