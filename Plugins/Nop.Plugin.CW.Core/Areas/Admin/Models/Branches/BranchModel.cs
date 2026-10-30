using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;

public record BranchModel : BaseNopEntityModel
{
    #region Ctor

    public BranchModel()
    {
        AvailableStores = new List<SelectListItem>();
        AvailableBranches = new List<SelectListItem>();
        AvailableShippingMethods = new List<SelectListItem>();
        CWAddressSearchModel = new CWAddressSearchModel();
    }

    #endregion

    #region Properties

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.BranchCode")]
    public string BranchCode { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.CardCode")]
    public string CardCode { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.BranchNumber")]
    public string BranchNumber { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.StoreId")]
    public int StoreId { get; set; }
    public IList<SelectListItem> AvailableStores { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.StoreId")]
    public string StoreName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.PaymentTerms")]
    public string PaymentTerms { get; set; }

    private IList<string> _branchShippingMethods;

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.ShippingMethod")]
    public IList<string> BranchShippingMethods
    {
        get
        {
            _branchShippingMethods ??= string.IsNullOrWhiteSpace(ShippingMethod)
                    ? new List<string>()
                    : ShippingMethod.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            return _branchShippingMethods;
        }
        set
        {
            _branchShippingMethods = value ?? new List<string>();
            ShippingMethod = _branchShippingMethods.Count == 0
                ? null
                : string.Join(", ", _branchShippingMethods);
        }
    }
    public string ShippingMethod { get; set; }
    public IList<SelectListItem> AvailableShippingMethods { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.Active")]
    public bool Active { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.StaffCount")]
    public int StaffCount { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Branches.Branch.Field.ParentBranchId")]
    public int? ParentBranchId { get; set; }

    public IList<SelectListItem> AvailableBranches { get; set; }

    public string ParentBranchName { get; set; }  // display only

    public string Breadcrumb { get; set; }

    public bool IsNew { get; set; }

    public CWAddressSearchModel CWAddressSearchModel { get; set; }

    #endregion
}
