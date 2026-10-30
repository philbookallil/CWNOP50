using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record AddProductToProductGroupSearchModel : BaseSearchModel
{
    #region Ctor

    public AddProductToProductGroupSearchModel()
    {
        AvailableCategories = new List<SelectListItem>();
        AvailableManufacturers = new List<SelectListItem>();
        AvailableProductTypes = new List<SelectListItem>();
    }

    #endregion

    #region Properties

    public int ProductGroupId { get; set; }

    public string? ProductGroup { get; set; }

    [NopResourceDisplayName("Admin.Catalog.Products.List.SearchProductName")]
    public string? SearchProductName { get; set; }

    [NopResourceDisplayName("Admin.Catalog.Products.List.SearchCategory")]
    public int SearchCategoryId { get; set; }

    [NopResourceDisplayName("Admin.Catalog.Products.List.SearchManufacturer")]
    public int SearchManufacturerId { get; set; }

    [NopResourceDisplayName("Admin.Catalog.Products.List.SearchProductType")]
    public int SearchProductTypeId { get; set; }

    public IList<SelectListItem> AvailableCategories { get; set; }

    public IList<SelectListItem> AvailableManufacturers { get; set; }

    public IList<SelectListItem> AvailableProductTypes { get; set; }

    #endregion
}
