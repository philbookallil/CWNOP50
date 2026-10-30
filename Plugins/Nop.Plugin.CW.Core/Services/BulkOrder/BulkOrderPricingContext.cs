using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.BulkOrder;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

/// <summary>
/// One product's prepared answers. Built by IBulkOrderPricingService; consulted per cell by the factory.
/// StateFor is SYNCHRONOUS on purpose: everything that needs a service call was resolved in PrepareAsync, and
/// an async per-cell method is exactly the shape that makes a large grid expensive.
/// </summary>
public class BulkOrderPricingContext
{
    #region Fields

    protected readonly IReadOnlyDictionary<int, decimal> _valueAdjustments;
    protected readonly IReadOnlyDictionary<(decimal? Overridden, decimal Charge), decimal> _prices;
    protected readonly IReadOnlyDictionary<int, BulkOrderCombinationStock> _stockByCombinationId;
    protected readonly IReadOnlyDictionary<(int Quantity, bool AllowOutOfStock), string> _stockMessages;
    protected readonly IReadOnlyDictionary<int, BulkOrderBundleLimit> _bundleLimits;

    protected readonly ManageInventoryMethod _inventoryMethod;
    protected readonly int _productStock;
    protected readonly bool _productBackorders;
    protected readonly bool _productDisabled;
    protected readonly bool _callForPrice;
    protected readonly bool _showPrices;
    protected readonly int _orderMaximumQuantity;

    #endregion

    #region Ctor

    public BulkOrderPricingContext(IReadOnlyDictionary<int, decimal> valueAdjustments,
        IReadOnlyDictionary<(decimal? Overridden, decimal Charge), decimal> prices,
        IReadOnlyDictionary<int, BulkOrderCombinationStock> stockByCombinationId,
        IReadOnlyDictionary<(int Quantity, bool AllowOutOfStock), string> stockMessages,
        IReadOnlyDictionary<int, BulkOrderBundleLimit> bundleLimits,
        ManageInventoryMethod inventoryMethod,
        int productStock,
        bool productBackorders,
        bool productDisabled,
        bool callForPrice,
        bool showPrices,
        int orderMaximumQuantity)
    {
        _valueAdjustments = valueAdjustments;
        _prices = prices;
        _stockByCombinationId = stockByCombinationId;
        _stockMessages = stockMessages;
        _bundleLimits = bundleLimits;
        _inventoryMethod = inventoryMethod;
        _productStock = productStock;
        _productBackorders = productBackorders;
        _productDisabled = productDisabled;
        _callForPrice = callForPrice;
        _showPrices = showPrices;
        _orderMaximumQuantity = orderMaximumQuantity;
    }

    #endregion

    #region Methods

    /// <summary>The sum of this cell's value adjustments. Arithmetic — every term was resolved already.</summary>
    public virtual decimal AdditionalChargeFor(BulkOrderCell cell)
    {
        var charge = decimal.Zero;

        foreach (var part in cell.Selection)
            foreach (var valueId in part.ValueIds)
                if (_valueAdjustments.TryGetValue(valueId, out var adjustment))
                    charge += adjustment;

        return charge;
    }

    /// <summary>Price, availability and the refusal rule, for one cell.</summary>
    public virtual BulkOrderCellState StateFor(BulkOrderCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        //the configuration does not exist. A dash, and no state to compute.
        if (!cell.Exists)
            return BulkOrderCellState.Unorderable;

        //the product-wide clause of the refusal rule. Resolved once in PrepareAsync: it cannot vary by cell.
        if (_productDisabled)
            return BulkOrderCellState.Unorderable;

        if (_callForPrice)
            return BulkOrderCellState.Unorderable with { CallForPrice = true };

        _stockByCombinationId.TryGetValue(cell.ProductAttributeCombinationId, out var stock);
        var hasStockRow = cell.ProductAttributeCombinationId > 0
                          && _stockByCombinationId.ContainsKey(cell.ProductAttributeCombinationId);

        var (inStock, maxAvailable, messageKey) = _inventoryMethod switch
        {
            //combination stock. A cell with no combination row cannot be reached here unless the product is
            //unrestricted, and then nop has no per-attribute stock to check either - so it is orderable.
            ManageInventoryMethod.ManageStockByAttributes when hasStockRow =>
                (stock.Quantity > 0 || stock.AllowOutOfStockOrders,
                 stock.AllowOutOfStockOrders ? (int?)null : Math.Max(stock.Quantity, 0),
                 ((int Quantity, bool AllowOutOfStock)?)(stock.Quantity, stock.AllowOutOfStockOrders)),

            ManageInventoryMethod.ManageStockByAttributes =>
                (true, (int?)null, null),

            //product-level stock. Backorders count as in stock.
            ManageInventoryMethod.ManageStock =>
                (_productStock > 0 || _productBackorders,
                 _productBackorders ? (int?)null : Math.Max(_productStock, 0),
                 ((int Quantity, bool AllowOutOfStock)?)(_productStock, _productBackorders)),

            //DontManageStock: no stock, no limit, no message
            _ => (true, (int?)null, null)
        };

        var message = messageKey.HasValue && _stockMessages.TryGetValue(messageKey.Value, out var text)
            ? text
            : string.Empty;

        //a bundle value's associated product caps the cell, or blocks it with ITS wording. Applied after
        //the parent's own stock, so the cell's max is the lower of the two - which is the order nop checks
        //them in (GetStandardWarningsAsync first, the bundled products last, :970-1000).
        foreach (var part in cell.Selection)
            foreach (var valueId in part.ValueIds)
            {
                if (!_bundleLimits.TryGetValue(valueId, out var limit))
                    continue;

                if (limit.Units <= 0)
                {
                    inStock = false;
                    message = limit.Message;
                }
                else
                {
                    maxAvailable = maxAvailable.HasValue ? Math.Min(maxAvailable.Value, limit.Units) : limit.Units;
                }
            }

        decimal? price = null;

        if (_showPrices)
        {
            var overridden = OverriddenPriceFor(cell);
            var charge = overridden.HasValue ? decimal.Zero : AdditionalChargeFor(cell);

            if (_prices.TryGetValue((overridden, charge), out var resolved))
                price = resolved;
        }

        //GetStandardWarningsAsync :410 refuses above OrderMaximumQuantity regardless of stock, so the box's
        //max is the lower of the two limits. When stock is unmanaged or backorders are on, maxAvailable is
        //null and this is the only limit there is - which is the case the box previously left unbounded.
        var cellMax = maxAvailable.HasValue
            ? Math.Min(maxAvailable.Value, _orderMaximumQuantity)
            : _orderMaximumQuantity;

        return new BulkOrderCellState(price, false, message, cellMax, !inStock);
    }

    /// <summary>
    /// The combination's OverriddenPrice, or null. Kept beside StateFor because the two must agree about which
    /// price path a cell is on: nop treats them as MUTUALLY EXCLUSIVE (ShoppingCartService.cs:1460-1467), so a
    /// cell with an overridden price contributes NO value adjustments at all.
    /// </summary>
    public virtual decimal? OverriddenPriceFor(BulkOrderCell cell)
        => _overriddenPrices.TryGetValue(cell.ProductAttributeCombinationId, out var overridden)
            ? overridden
            : null;

    #endregion

    /// <summary>Combination id -> OverriddenPrice, only for combinations that have one.</summary>
    public IReadOnlyDictionary<int, decimal> _overriddenPrices { get; init; } = new Dictionary<int, decimal>();
}