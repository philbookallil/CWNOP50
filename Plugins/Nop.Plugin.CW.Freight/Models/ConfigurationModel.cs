using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Freight.Models;

public record ConfigurationModel : BaseSearchModel
{
    public ConfigurationModel()
    {
        AvailableStores = new List<SelectListItem>();
    }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.LimitMethodsToCreated")]
    public bool LimitMethodsToCreated { get; set; }

    [NopResourceDisplayName("Plugins.CW.Freight.Fields.SearchStoreId")]
    public int SearchStoreId { get; set; }

    public IList<SelectListItem> AvailableStores { get; set; }
}
