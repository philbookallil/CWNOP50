namespace Nop.Plugin.CW.Core.Domain.BulkOrder;

/// <summary>
/// What one cell costs and whether it can be ordered. Deliberately NOT part of
/// BulkOrderCell: the cell belongs to the customer-independent skeleton that is cached, and every field
/// here depends on the customer, their cart, their tax display and their currency.
/// </summary>
/// <param name="Price">
/// Taxed, in the PRIMARY STORE currency. The factory converts and formats — a service that returned a string
/// would be deciding a view concern, and services are kept out of models. Null when there is
/// nothing to show: prices hidden, or CallForPrice.
/// </param>
/// <param name="CallForPrice">The cell shows "Call for price" and takes no quantity.</param>
/// <param name="StockMessage">
/// nop's own wording, already localized (Products.Availability.*). EMPTY when the product has
/// DisplayStockAvailability off — which is the common case, not an error.
/// </param>
/// <param name="MaxAvailable">
/// The most this cell can supply, for the quantity input's max. the most this box will accept — the product does not
/// manage stock, or out-of-stock orders are allowed. It does NOT subtract what is already in the cart: nop's
/// own check compares the SUBMITTED quantity against stock (ShoppingCartService.cs:502), and how a bulk line
/// merges with an existing cart line is decided by the add path.
/// </param>
/// <param name="Disabled">
/// The refusal rule said no. The cell renders, but with no quantity box — distinct from a DASH, which means
/// the configuration does not exist at all.
/// </param>
public readonly record struct BulkOrderCellState(
    decimal? Price,
    bool CallForPrice,
    string StockMessage,
    int? MaxAvailable,
    bool Disabled)
{
    /// <summary>A cell nothing can be ordered from. Used for the product-wide refusals.</summary>
    public static BulkOrderCellState Unorderable { get; } =
        new(null, false, string.Empty, 0, true);
}