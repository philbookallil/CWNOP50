namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>One product's grid: what to render, with no view concerns and no customer-specific data.</summary>
public record BulkOrderGrid
{
    public int ProductId { get; init; }

    /// <summary>Axis 1 supplies the columns; axes 2..n supply the row label columns.</summary>
    public IList<BulkOrderAxis> Axes { get; init; } = [];

    /// <summary>Pinned attributes, in render order. May be empty.</summary>
    public IList<BulkOrderPin> Pins { get; init; } = [];

    /// <summary>
    /// Column headers. Every axis-1 value when the product is unrestricted; only the values carried by a
    /// matching combination when it is restricted.
    /// </summary>
    public IList<BulkOrderAxisValue> Columns { get; init; } = [];

    public IList<BulkOrderRow> Rows { get; init; } = [];

    /// <summary>
    /// True when nop itself would refuse a selection that has no combination row - that is, when the
    /// product is ManageStockByAttributes AND AllowAddingOnlyExistingAttributeCombinations
    /// (ShoppingCartService.cs:496-517). False for every other product, including products that have
    /// combinations: there the combination is information, not a gate.
    /// </summary>
    public bool RestrictsToExistingCombinations { get; init; }

    /// <summary>
    /// Set when the product needs data at add-to-cart that this screen cannot collect. Lives
    /// on the SKELETON, beside RestrictsToExistingCombinations, for the same reason: it is derived from
    /// product columns alone, so it is customer-independent and safe to cache - and the skeleton's
    /// invalidation on Product changes already covers it.
    /// </summary>
    public BulkOrderUnsupportedReason UnsupportedReason { get; init; }

    /// <summary>
    /// The attribute name behind UnsupportedReason, when naming it helps. Only RequiredTextAttribute sets it:
    /// the other reasons are properties of the whole product and name themselves. Null otherwise.
    /// </summary>
    public string UnsupportedDetail { get; init; }


    /// <summary>
    /// The attribute whose value the <see cref="BulkOrderCell.RequiresTextAttribute"/> cells need, for the
    /// notice above the grid. Null when no cell is blocked.
    /// </summary>
    /// <remarks>
    /// NOT UnsupportedDetail. That one pairs with UnsupportedReason and means the whole product is refused;
    /// this one means part of the grid is. They are mutually exclusive today only because the product-level
    /// refusal returns early — which is a reason to keep them apart, not to merge them.
    /// </remarks>
    public string RequiredTextAttributeName { get; init; }

    /// <summary>
    /// The first REQUIRED pin holding nothing, for the notice above the grid. Null
    /// when every required pin has a value. The cells it blocks carry <see cref="BulkOrderCell.RequiresPinChoice"/>.
    /// </summary>
    public string RequiredChoiceAttributeName { get; init; }

    /// <summary>
    /// True when the product has at least one combination row. With RestrictsToExistingCombinations, this
    /// separates "your pins matched nothing, try others" from "nothing can ever match" - a restricted
    /// product with no combinations at all is one nop refuses every selection on.
    /// </summary>
    public bool HasCombinations { get; init; }

    /// <summary>Attributes demoted to pins by the cell budget, in demotion order. Empty when none.</summary>
    public IList<string> DemotedAxisNames { get; init; } = [];

    /// <summary>True when one axis alone exceeds the budget and the grid renders anyway.</summary>
    public bool ExceedsBudget { get; init; }
}

/// <summary>An attribute laid out on the grid.</summary>
public record BulkOrderAxis
{
    public int ProductAttributeMappingId { get; init; }

    public string Name { get; init; }

    public IList<BulkOrderAxisValue> Values { get; init; } = [];
}

public record BulkOrderAxisValue
{
    public int Id { get; init; }

    public string Name { get; init; }

    /// <summary>ColorSquaresRgb, for a colour-squares attribute. Null otherwise.</summary>
    public string ColorSquaresRgb { get; init; }

    /// <summary>ProductAttributeValue.ImageSquaresPictureId — the swatch of an ImageSquares attribute. 0 when none.</summary>
    public int ImageSquaresPictureId { get; init; }

    /// <summary>
    /// The value's own image, from the <b>ProductAttributeValuePicture</b> mapping. 0 when none.
    /// </summary>
    /// <remarks>
    /// NOT ProductAttributeValue.PictureId: that field is [Obsolete] and [EditorBrowsable(Never)] since nop
    /// 4.70 and returns null on any store created since - it compiles, runs, and silently renders nothing.
    /// </remarks>
    public int ValuePictureId { get; init; }

    public int DisplayOrder { get; init; }
}

/// <summary>A pinned attribute: one value applies to every line of this card.</summary>
public record BulkOrderPin
{
    public int ProductAttributeMappingId { get; init; }

    public string Name { get; init; }

    public string TextPrompt { get; init; }

    public bool IsRequired { get; init; }

    /// <summary>nop's AttributeControlType id, so the view can pick the right control.</summary>
    public int AttributeControlTypeId { get; init; }

    /// <summary>Empty for a non-combinable pin (text, date, file).</summary>
    public IList<BulkOrderAxisValue> Values { get; init; } = [];

    /// <summary>The values currently selected. Empty when none. More than one only for checkboxes.</summary>
    public IList<int> SelectedValueIds { get; init; } = [];

    /// <summary>What this pin resolves to with no customer selection. The client sends only pins that differ.</summary>
    public IList<int> DefaultValueIds { get; init; } = [];

    /// <summary>True when the cell budget demoted this attribute from an axis rather than classification.</summary>
    public bool WasDemoted { get; init; }

    /// <summary>
    /// This pin's condition depends on an attribute that is on an AXIS, so it applies to
    /// part of the grid and not the rest. The control is shown, and its value reaches only the cells whose
    /// axis value satisfies the condition.
    /// </summary>
    public bool IsConditional { get; init; }

    /// <summary>
    /// The single axis value this pin applies to, for the note beside the control. Null unless IsConditional.
    /// ONE value, not a list: when the depended-on attribute is an axis, each cell contributes exactly one of
    /// its values, and nop compares the required set by count first, so only a one-value condition can ever be
    /// met — and only by the one value it names. A name rather than an id because the only consumer is a
    /// sentence the customer reads (dropping the value silently on the other lines is not
    /// acceptable).
    /// </summary>
    public string AppliesToValueName { get; init; }

    /// <summary>
    /// This attribute's condition is currently UNMET, so nop would not offer it —
    /// but the control is rendered anyway, disabled, naming the attribute that unlocks it.
    /// </summary>
    /// <remarks>
    /// nop hides its own conditional fields with an inline <c>display: none</c> and leaves them in the DOM
    /// (<c>Views/Product/_ProductAttributes.cshtml:19,33</c>), then strips them server-side at
    /// <c>ParseProductAttributes:309-317</c>. We keep the same data handling — the value travels and the
    /// server ignores it — and diverge on presentation: on nop's product page the attributes are a short
    /// vertical list where a field appearing is noticeable, while here the strip is one row on one card of
    /// fifteen, and a control silently leaving it is not noticed at all.
    /// Keeping the control in the DOM is also what makes the selection survive: untick, re-tick, and the pin
    /// comes back at what the customer chose rather than at its default.
    /// A pin in this state contributes NOTHING to any cell key — that is the part that must not
    /// change, and <c>BuildAsync</c> enforces it by excluding these from <c>allPins</c>.
    /// </remarks>
    public bool IsBlockedByCondition { get; init; }

    /// <summary>
    /// The name of the attribute this one depends on, for "Choose Embroidery first". Null unless
    /// <see cref="IsBlockedByCondition"/>.
    /// </summary>
    /// <remarks>
    /// The attribute, not the value that unlocks it: a condition can require a SET, and an empty required
    /// list is meaningful — admin writes it to mean "nothing selected". Naming the attribute is true
    /// in all three shapes and needs one string rather than three.
    /// </remarks>
    public string DependsOnName { get; init; }
}

/// <summary>One grid row: the axis 2..n value tuple, plus its cells.</summary>
public record BulkOrderRow
{
    /// <summary>One entry per axis 2..n, in axis order. Empty when there is only one axis.</summary>
    public IList<BulkOrderAxisValue> Labels { get; init; } = [];

    public IList<BulkOrderCell> Cells { get; init; } = [];
}

/// <summary>
/// One cell. A cell is a dash - not enterable, not submittable - only when nop would refuse it:
/// no combination matched AND the product restricts to existing combinations.
/// </summary>
public record BulkOrderCell
{
    /// <summary>
    /// The axis-1 value this cell sits under. NULL only for the degenerate case — a product with
    /// no axes has one cell and no coordinates to name it by.
    /// </summary>
    public BulkOrderAxisValue Column { get; init; }

    /// <summary>0 when no combination matched this cell. Carries the Sku when one did.</summary>
    public int ProductAttributeCombinationId { get; init; }

    public bool Exists => ProductAttributeCombinationId > 0 || AllowedWithoutCombination;

    /// <summary>
    /// True when the product does not restrict to existing combinations, so a lookup miss is not a
    /// reason to close the cell. This is the common case: on the measured catalogue every
    /// product with combinations is unrestricted.
    /// </summary>
    public bool AllowedWithoutCombination { get; init; }

    /// <summary>
    /// The full attribute selection this cell represents: axis values plus pinned values.
    /// Turned into AttributesXml when the cell is added to the cart, and rendered as data attributes.
    /// </summary>
    public IList<CombinationKeyPart> Selection { get; init; } = [];

    /// <summary>
    /// The AXIS values only, no pins. This is what identifies a cell to the customer: "M / Navy" is the
    /// same cell before and after a pin change, while Selection legitimately changes with it. Quantity
    /// carry-over across a pin change matches on this and never on Selection.
    /// </summary>
    public IList<CombinationKeyPart> AxisSelection { get; init; } = [];

    public string Sku { get; init; }


    /// <summary>
    /// True when a REQUIRED attribute that takes free text, a date or a file applies to THIS cell —
    /// its condition is satisfied by this cell's axis value. nop refuses such
    /// a line for want of a value the grid has no input for
    /// (ShoppingCartService.GetShoppingCartItemAttributeWarningsAsync:869), so the cell renders its reason
    /// instead of a quantity box.
    /// </summary>
    /// <remarks>
    /// Its sibling cells, whose axis value does NOT satisfy the condition, are ordinary: nop strips the
    /// attribute before the required check (:857) and never asks them for a value. Blocking the whole product
    /// instead refused cells the store sells — on the measured catalogue, all 101 products carrying a
    /// conditional required text attribute have at least one axis value that does not satisfy it.
    /// </remarks>
    public bool RequiresTextAttribute { get; init; }

    /// <summary>
    /// True when a REQUIRED pin that applies to this cell holds nothing yet. The
    /// cell renders blocked with "Choose above" and takes no quantity; choosing the pin unblocks it. Unlike
    /// RequiresTextAttribute this is recoverable on this screen, which is why it is not a refusal.
    /// </summary>
    public bool RequiresPinChoice { get; init; }
}