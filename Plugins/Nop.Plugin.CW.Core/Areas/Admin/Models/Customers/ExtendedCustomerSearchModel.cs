using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Areas.Admin.Models.Customers;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;

public record ExtendedCustomerSearchModel : CustomerSearchModel
{
    #region Ctor

    public ExtendedCustomerSearchModel()
    {
        AvailableStores = [];
    }

    #endregion

    #region Properties

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Customers.List.SearchStoreId")]
    public int SearchStoreId { get; set; }

    public IList<SelectListItem> AvailableStores { get; set; }

    #endregion
}
