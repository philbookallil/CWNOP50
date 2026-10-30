using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record AddPackNameToSubsidySearchModel : BaseSearchModel
{
    #region Ctor

    public AddPackNameToSubsidySearchModel()
    {
        AvailablePackNameTypes = new List<SelectListItem>();
        AvailableGenders = new List<SelectListItem>();
        AvailableDepartments = new List<SelectListItem>();
        AvailableCostCenters = new List<SelectListItem>();
        MultiStoreSettingsModel = new MultiStoreSettingsModel();
    }

    #endregion

    #region Properties

    public int SubsidyId { get; set; }

    public string? Subsidy { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.SearchBranchName")]
    public string? SearchBranchName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.SearchPackName")]
    public string? SearchPackName { get; set; }

    [DataType(DataType.EmailAddress)]
    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.SearchEmail")]
    public string? SearchEmail { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.SearchPackNameType")]
    public string? SearchPackNameType { get; set; }
    public IList<SelectListItem> AvailablePackNameTypes { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.SearchGender")]
    public string? SearchGender { get; set; }
    public IList<SelectListItem> AvailableGenders { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.SearchDepartmentId")]
    public int SearchDepartmentId { get; set; }
    public IList<SelectListItem> AvailableDepartments { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.SearchCostCenterId")]
    public int SearchCostCenterId { get; set; }
    public IList<SelectListItem> AvailableCostCenters { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNames.WithoutSubsidy")]
    public bool WithoutSubsidy { get; set; }

    public MultiStoreSettingsModel MultiStoreSettingsModel { get; set; }

    #endregion
}
