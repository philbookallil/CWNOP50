using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Freight.Models;

public record CWShippingMethodModel : BaseNopEntityModel
{
    public CWShippingMethodModel()
    {
        AvailableStores = new List<SelectListItem>();
        AvailableCustomerRoles = new List<SelectListItem>();
        AvailableCountries = new List<SelectListItem>();
        SelectedStoreIds = new List<int>();
        SelectedCustomerRoleIds = new List<int>();
        RestrictedCountryIds = new List<int>();
    }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.Description")]
    public string Description { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.DisplayOrder")]
    public int DisplayOrder { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.Rate")]
    public decimal Rate { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.LowestOrderTotal")]
    public decimal LowestOrderTotal { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.HighestOrderTotal")]
    public decimal HighestOrderTotal { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.ApplyPerBranch")]
    public bool ApplyPerBranch { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.SelectedStoreIds")]
    public IList<int> SelectedStoreIds { get; set; }
    public IList<SelectListItem> AvailableStores { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.SelectedCustomerRoleIds")]
    public IList<int> SelectedCustomerRoleIds { get; set; }
    public IList<SelectListItem> AvailableCustomerRoles { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.RestrictedCountryIds")]
    public IList<int> RestrictedCountryIds { get; set; }
    public IList<SelectListItem> AvailableCountries { get; set; }
}
