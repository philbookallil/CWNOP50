using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.BulkOrder;

public record BulkOrderGridModel : BaseNopModel
{
    public int ProductId { get; set; }

    /// <summary>Header for the row-label columns, in axis order. Empty when there is only one axis.</summary>
    public IList<string> RowAxisNames { get; set; } = [];

    /// <summary>The axis-1 name, used as the column-group heading.</summary>
    public string ColumnAxisName { get; set; }

    public IList<BulkOrderGridValueModel> Columns { get; set; } = [];

    public IList<BulkOrderGridRowModel> Rows { get; set; } = [];

    public IList<BulkOrderPinModel> Pins { get; set; } = [];

    /// <summary>True when the product has no attributes at all: one quantity box, no table.</summary>
    public bool IsSingleCell { get; set; }

    /// <summary>
    /// The one cell of a product with no axes. Set exactly when IsSingleCell is true, and it
    /// carries the same price, stock, max and disabled state as any cell in a table — which is the whole
    /// point: the rules must not stop applying because a product happens to have no attributes.
    /// </summary>
    public BulkOrderGridCellModel SingleCell { get; set; }

    /// <summary>Set when the pin selection matches no combination - the grid is empty and says why.</summary>
    public bool HasNoMatchingCombinations { get; set; }

    /// <summary>
    /// Set when no pin selection could ever produce a cell: the product only accepts existing attribute
    /// combinations and has none. Distinct from HasNoMatchingCombinations, which invites the customer to
    /// change the options above.
    /// </summary>
    public bool IsNotOrderable { get; set; }

    /// <summary>
    /// Localized explanation when the product cannot be ordered from this screen at all. Null
    /// when it can. Distinct from IsNotOrderable, which means nop would refuse the SALE - here the sale is
    /// fine, it just has to happen on the product page.
    /// </summary>
    public string UnsupportedReason { get; set; }

    /// <summary>SEO slug, so the unsupported notice can link to the page that does have the form.</summary>
    public string ProductSeName { get; set; }

    /// <summary>The product's name, in the customer's language: the card's add button is named for it</summary>
    public string ProductName { get; set; }

    /// <summary>
    /// Set when SOME cells need a required attribute this screen cannot take. The
    /// product is orderable, so this is a notice above the grid rather than an UnsupportedReason - and the
    /// name lives here rather than on every cell because a sentence that long inside a table cell widens its
    /// whole column.
    /// </summary>
    public string RequiredTextAttributeName { get; set; }

    /// <summary>
    /// The first REQUIRED pin holding nothing, for the notice above the grid.
    /// Null when every required pin has a value.
    /// </summary>
    public string RequiredChoiceAttributeName { get; set; }

    /// <summary>Names of attributes the cell budget moved off the axes, for the notice.</summary>
    public IList<string> DemotedAxisNames { get; set; } = [];

    public bool ExceedsBudget { get; set; }

    public bool HideQtyArrows { get; set; }

    /// <summary>
    /// Product.OrderMinimumQuantity. Drives the quantity box's min and the notice under the grid.
    /// </summary>
    /// <remarks>
    /// Product-level, so it is here rather than on every cell - a 400-cell product would otherwise carry 400
    /// copies of one number. nop refuses a line below this, so a box that let
    /// the customer step down to 1 on a product with a minimum of 6 is a control that invites a refusal.
    /// </remarks>
    public int MinQuantity { get; set; } = 1;

    /// <summary>
    /// Product.AllowedQuantities, parsed. Empty for almost every product, and empty means "any quantity".
    /// </summary>
    /// <remarks>
    /// When this has entries nop accepts NOTHING else and its own product
    /// page renders a dropdown rather than a number box. The grid does the same: a free number box cannot
    /// express a whitelist, so the customer would have to be refused to discover what the list is.
    /// </remarks>
    public IList<int> AllowedQuantities { get; set; } = new List<int>();

    public bool ShowAttributeImages { get; set; }

    /// <summary>
    /// Whether to render the per-cell price. Off when the setting is off, and ALSO off when every cell agrees
    /// with the product price — a column of the same number repeated 400 times is noise.
    /// </summary>
    public bool ShowCellPrices { get; set; }

    /// <summary>Whether to render the per-cell stock message.</summary>
    public bool ShowCellStock { get; set; }

    /// <summary>CSS size for a value image, e.g. "100px".</summary>
    public string AttributeImageSize { get; set; }

    /// <summary>CSS size for an image-squares swatch, e.g. "100px".</summary>
    public string AttributeImageSquaresSize { get; set; }
}

public record BulkOrderGridValueModel : BaseNopEntityModel
{
    public string Name { get; set; }

    public string ColorSquaresRgb { get; set; }

    /// <summary>Resolved swatch URL for an ImageSquares value. Null when the value has none.</summary>
    public string ImageSquaresUrl { get; set; }

    /// <summary>Resolved image URL for the value, shown when ShowAttributeImages is on. Null when none.</summary>
    public string ImageUrl { get; set; }
}

public record BulkOrderGridRowModel : BaseNopModel
{
    public IList<BulkOrderGridValueModel> Labels { get; set; } = [];

    public IList<BulkOrderGridCellModel> Cells { get; set; } = [];
}

public record BulkOrderGridCellModel : BaseNopModel
{
    public bool Exists { get; set; }

    public string Sku { get; set; }

    /// <summary>Formatted cell price, or null when there is nothing to show.</summary>
    public string Price { get; set; }

    /// <summary>nop's own availability wording. Empty when the product does not display stock.</summary>
    public string StockMessage { get; set; }

    /// <summary>True when the cell renders but takes no quantity. Distinct from !Exists, which is a dash.</summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// The cell takes no quantity because the product is CallForPrice. Distinct from Disabled-for-stock: the
    /// customer is being told to ask, not that they cannot have it. Both suppress the quantity box, so a view
    /// that only checked Disabled would render the same marker for two opposite meanings.
    /// </summary>
    public bool CallForPrice { get; set; }

    /// <summary>
    /// This cell needs a required free-text / date / file attribute the grid has no input for, because its
    /// condition is satisfied by this cell's axis value. Takes no quantity; its siblings do.
    /// </summary>
    public bool RequiresTextAttribute { get; set; }

    /// <summary>
    /// A REQUIRED pin that applies to this cell holds nothing yet. Takes no
    /// quantity and says "Choose above"; choosing the pin unblocks it.
    /// </summary>
    public bool RequiresPinChoice { get; set; }

    /// <summary>Max for the quantity input. Null means no limit.</summary>
    public int? MaxAvailable { get; set; }

    /// <summary>
    /// The cell's full selection as "mappingId:valueId,..." — written to a data attribute and posted
    /// back with the quantity when the cell is added to the cart.
    /// </summary>
    public string Selection { get; set; }

    /// <summary>
    /// Axis coordinates only, same wire shape as Selection. The grid reloader matches typed quantities on
    /// this, because it survives a pin change and Selection does not.
    /// </summary>
    public string AxisKey { get; set; }

    /// <summary>Accessible label, e.g. "Navy, Regular, size 12". Screen readers get nothing from a bare box.</summary>
    public string Label { get; set; }
}

public record BulkOrderPinModel : BaseNopEntityModel
{
    public string Name { get; set; }

    public string TextPrompt { get; set; }

    public bool IsRequired { get; set; }

    public int AttributeControlTypeId { get; set; }

    public IList<BulkOrderGridValueModel> Values { get; set; } = [];

    public IList<int> SelectedValueIds { get; set; } = [];

    public IList<int> DefaultValueIds { get; set; } = [];

    public bool WasDemoted { get; set; }

    /// <summary>This pin applies to part of the grid only.</summary>
    public bool IsConditional { get; set; }

    /// <summary>The single axis value it applies to, for the note beside the control.</summary>
    public string AppliesToValueName { get; set; }

    /// <summary>Rendered, disabled, naming what unlocks it. Submits nothing.</summary>
    public bool IsBlockedByCondition { get; set; }

    /// <summary>The attribute that unlocks it, for "Choose Embroidery first". Null unless blocked.</summary>
    public string DependsOnName { get; set; }
}

/// <summary>
/// One grid build: the model, plus the price span the product line needs from it. The span is returned rather
/// than recomputed by the caller because recomputing it means a second pass over every cell, and because the
/// two answers could then differ.
/// </summary>
/// <param name="Model">The grid model</param>
/// <param name="MinCellPrice">Lowest priced cell, or null when no cell has a price</param>
/// <param name="MaxCellPrice">Highest priced cell, or null when no cell has a price</param>
public record BulkOrderGridResult(BulkOrderGridModel Model, decimal? MinCellPrice, decimal? MaxCellPrice);