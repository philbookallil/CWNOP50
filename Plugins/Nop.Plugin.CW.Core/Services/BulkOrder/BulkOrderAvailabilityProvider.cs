using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.BulkOrder;
using Nop.Services.Catalog;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

/// <summary>
/// nop-only availability. No ETA, no per-warehouse breakdown and no live feed: those were Jim2-specific in
/// the previous implementation and there is no CW equivalent. The store-level AllowOutofStock flag, the
/// HandledNo* rules and the role gate are gone with them, so this class is deliberately
/// thin — that thinness is the decision, not an omission.
/// </summary>
public class BulkOrderAvailabilityProvider : IBulkOrderAvailabilityProvider
{
    #region Fields

    protected readonly IProductService _productService;

    #endregion

    #region Ctor

    public BulkOrderAvailabilityProvider(IProductService productService)
    {
        _productService = productService;
    }

    #endregion

    #region Methods

    public virtual async Task<BulkOrderAvailability> GetAsync(Product product,
        IList<ProductAttributeCombination> combinations)
    {
        ArgumentNullException.ThrowIfNull(product);

        //ONE call per product, not per cell. GetTotalStockQuantityAsync sums warehouse inventory, so calling
        //it per cell is how 4.6's grid became quadratic.
        var productStock = product.ManageInventoryMethod == ManageInventoryMethod.ManageStock
            ? await _productService.GetTotalStockQuantityAsync(product)
            : 0;

        var byCombination = new Dictionary<int, BulkOrderCombinationStock>();

        //read for every product that HAS combinations, not only ManageStockByAttributes ones: the caller
        //decides which method's numbers to believe, and building the dictionary costs nothing
        foreach (var combination in combinations ?? [])
            byCombination[combination.Id] =
                new BulkOrderCombinationStock(combination.StockQuantity, combination.AllowOutOfStockOrders);

        return new BulkOrderAvailability(productStock, byCombination);
    }

    #endregion
}