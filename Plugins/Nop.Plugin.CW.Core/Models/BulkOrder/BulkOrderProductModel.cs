using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.BulkOrder;

public record BulkOrderProductModel : BaseNopEntityModel
{
    public string Sku { get; set; }

    public string Name { get; set; }

    public string SeName { get; set; }

    public string ImageUrl { get; set; }

    public string ImageAlternateText { get; set; }

    public string Price { get; set; }

    /// <summary>
    /// "from X to Y" when the cells disagree and per-cell prices are not being shown. Null otherwise.
    /// </summary>
    public string PriceRange { get; set; }

    /// <summary>
    /// True when every cell costs the same — in which case Price holds that figure and no cell shows its own.
    /// This is why most grids show no
    /// price column at all: the overwhelming majority of products have no price-adjusting values.
    /// </summary>
    public bool SameAttributesPrice { get; set; }

    public int QuantityInCart { get; set; }

    /// <summary>
    /// Every product gets a grid. A product with no attributes gets a one-cell grid holding a single
    /// quantity box — the degenerate case, and the same code path as a 5-axis garment.
    /// </summary>
    public BulkOrderGridModel Grid { get; set; }
}