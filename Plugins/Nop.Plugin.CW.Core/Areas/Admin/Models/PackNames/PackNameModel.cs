using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

public record PackNameModel : BaseNopEntityModel
{
    #region Ctor

    public PackNameModel()
    {
        AvailableBranches = new List<SelectListItem>();
        AvailableGenders = new List<SelectListItem>();
        AvailableLists = new List<SelectListItem>();
        AvailableDepartments = new List<SelectListItem>();
        AvailableCostCenters = new List<SelectListItem>();
        CWAddressSearchModel = new CWAddressSearchModel();
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
        Warnings = new List<string>();
        Active = true;
    }

    #endregion

    #region Properties

    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Branch")]
    public int BranchId { get; set; }
    public IList<SelectListItem> AvailableBranches { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Branch")]
    public string Branch { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList")]
    public int PackNameListId { get; set; }
    public IList<SelectListItem> AvailableLists { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.PackNameList")]
    public string PackNameList { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Colour")]
    public string Colour { get; set; }

    public string ForeColor { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender")]
    public string Gender { get; set; }
    public IList<SelectListItem> AvailableGenders { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.PrintName")]
    public string PrintName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.EmployeeNumber")]
    public string EmployeeNumber { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.POEntry")]
    public string POEntry { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Comments")]
    public string Comments { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Group")]
    public bool Group { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.IsCustomer")]
    public bool IsCustomer { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Customer")]
    public int? CustomerId { get; set; }

    public string CustomerName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Department")]
    public int? DepartmentId { get; set; }
    public IList<SelectListItem> AvailableDepartments { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Department")]
    public string Department { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.CostCenter")]
    public int? CostCenterId { get; set; }
    public IList<SelectListItem> AvailableCostCenters { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.CostCenter")]
    public string CostCenter { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Manager")]
    public bool Manager { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.PackName.Field.Active")]
    public bool Active { get; set; }

    public bool IsNew { get; set; }

    public IList<string> Warnings { get; set; }

    public CWAddressSearchModel CWAddressSearchModel { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }

    #endregion
}
