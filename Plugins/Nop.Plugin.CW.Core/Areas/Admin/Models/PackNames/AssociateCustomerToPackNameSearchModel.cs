using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

public partial record AssociateCustomerToPackNameSearchModel : BaseSearchModel
{
    #region Ctor

    public AssociateCustomerToPackNameSearchModel()
    {
        AssociateCustomerToPackNameModel = new AssociateCustomerToPackNameModel();
    }

    #endregion

    #region Properties

    public int BranchId { get; set; }

    public int CurrentCustomerId { get; set; }

    [NopResourceDisplayName("Admin.Customers.Customers.List.SearchEmail")]
    public string SearchEmail { get; set; }

    [NopResourceDisplayName("Admin.Customers.Customers.List.SearchUsername")]
    public string SearchUsername { get; set; }

    public bool UsernamesEnabled { get; set; }

    [NopResourceDisplayName("Admin.Customers.Customers.List.SearchFirstName")]
    public string SearchFirstName { get; set; }
    public bool FirstNameEnabled { get; set; }

    [NopResourceDisplayName("Admin.Customers.Customers.List.SearchLastName")]
    public string SearchLastName { get; set; }
    public bool LastNameEnabled { get; set; }

    public AssociateCustomerToPackNameModel AssociateCustomerToPackNameModel { get; set; }

    #endregion
}
