using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.CW.Core.Models.Exchanges;

/// <summary>
/// A page of candidate replacement products. Deliberately built from overview models -
/// attributes are not needed until a product has been chosen.
/// </summary>
public record ReplacementProductListModel : BaseNopModel
{
    #region Ctor

    public ReplacementProductListModel()
    {
        Products = new List<ProductOverviewModel>();
    }

    #endregion

    #region Properties

    /// <summary>The order item being replaced</summary>
    public int OrderItemId { get; set; }

    /// <summary>The returned product, excluded from the results</summary>
    public int ExcludeProductId { get; set; }

    public IList<ProductOverviewModel> Products { get; set; }

    public string Keywords { get; set; }

    public int PageIndex { get; set; }

    public int TotalPages { get; set; }

    public int TotalCount { get; set; }

    public bool HasPreviousPage => PageIndex > 0;

    public bool HasNextPage => PageIndex + 1 < TotalPages;

    #endregion
}