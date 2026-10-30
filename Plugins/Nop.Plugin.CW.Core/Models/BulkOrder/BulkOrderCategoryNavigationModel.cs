using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.CW.Core.Models.BulkOrder;

public record BulkOrderCategoryNavigationModel : BaseNopModel
{
    #region Ctor

    public BulkOrderCategoryNavigationModel()
    {
        Categories = new List<CategorySimpleModel>();
    }

    #endregion

    #region Properties

    public int CategoryId { get; set; }

    public string FilterMode { get; set; }

    public int OrderId { get; set; }

    public int OrderBy { get; set; }

    public int PageSize { get; set; }

    public string ViewMode { get; set; }

    public IList<CategorySimpleModel> Categories { get; set; }

    #endregion
}