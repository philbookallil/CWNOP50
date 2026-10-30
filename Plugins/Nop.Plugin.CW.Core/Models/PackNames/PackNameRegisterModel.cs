using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Models.Common;
using Nop.Web.Models.Customer;

namespace Nop.Plugin.CW.Core.Models.PackNames;

public partial record PackNameRegisterModel : BaseNopEntityModel
{
    #region Ctor

    public PackNameRegisterModel()
    {
        AvailableGenders = new List<SelectListItem>();
        AvailableBranches = new List<SelectListItem>();
        AvailableDepartments = new List<SelectListItem>();
        AvailableCostCenters = new List<SelectListItem>();
        Address = new AddressModel();
        Customer = new CustomerInfoModel();
        SubsidyIds = new List<int>();
        AvailableSubsidies = new List<SelectListItem>();
    }

    #endregion

    #region Properties

    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Branch")]
    public int BranchId { get; set; }
    public IList<SelectListItem> AvailableBranches { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Department")]
    public int? DepartmentId { get; set; }
    public IList<SelectListItem> AvailableDepartments { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.CostCenter")]
    public int? CostCenterId { get; set; }
    public IList<SelectListItem> AvailableCostCenters { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Group")]
    public bool Group { get; set; }

    public bool IsCustomer { get; set; }

    public bool EmployeeNumberEnabled { get; set; }
    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.EmployeeNumber")]
    public string EmployeeNumber { get; set; }

    public bool PrintNameEnabled { get; set; }
    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.PrintName")]
    public string PrintName { get; set; }

    public bool POEntryEnabled { get; set; }
    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.POEntry")]
    public string POEntry { get; set; }

    public bool CommentsEnabled { get; set; }
    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Comments")]
    public string Comments { get; set; }

    public bool GenderEnabled { get; set; }
    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Gender")]
    public string Gender { get; set; }
    public IList<SelectListItem> AvailableGenders { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Colour")]
    public string Colour { get; set; }

    public bool CanAddAddress { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.AddAddress")]
    public bool AddAddress { get; set; }

    public AddressModel Address { get; set; }

    public bool CanDelete { get; set; }

    public string BranchName { get; set; }

    public bool CanAddCustomer { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.AddCustomer")]
    public bool AddCustomer { get; set; }

    public CustomerInfoModel Customer { get; set; }

    /// <summary>
    /// True when the store lets managers pick the packname's subsidies on this form.
    /// </summary>
    public bool SubsidySelectionEnabled { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Subsidies")]
    public IList<int> SubsidyIds { get; set; }

    public IList<SelectListItem> AvailableSubsidies { get; set; }

    #endregion
}
