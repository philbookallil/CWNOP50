using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record ProductGroupModel : BaseNopEntityModel
{
    #region Ctor

    public ProductGroupModel()
    {
        ProductGroupProductSearchModel = new ProductGroupProductSearchModel();
        AvailableIcons = new List<SelectListItem>();
        Active = true;
    }

    #endregion

    #region Properties

    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Name")]
    public string? Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Description")]
    public string? Description { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Icon")]
    public string? Icon { get; set; }
    public IList<SelectListItem> AvailableIcons { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Colour")]
    public string? Colour { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.DisplayOrder")]
    public int DisplayOrder { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.Active")]
    public bool Active { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ProductGroups.ProductGroup.Field.ProductCount")]
    public int ProductCount { get; set; }

    public bool IsNew { get; set; }

    public ProductGroupProductSearchModel ProductGroupProductSearchModel { get; set; }

    #endregion
}
