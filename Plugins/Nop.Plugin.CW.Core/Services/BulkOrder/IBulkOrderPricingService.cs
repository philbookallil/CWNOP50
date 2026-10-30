using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.BulkOrder;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

/// <summary>
/// Prices and availability for one product's grid. Everything async happens once, in
/// PrepareAsync; the per-cell answer is then arithmetic and a dictionary hit.
/// </summary>
public interface IBulkOrderPricingService
{
    /// <summary>
    /// Resolves every price and stock figure the grid needs, in as few service calls as the data allows.
    /// </summary>
    /// <param name="tierQuantity">
    /// The quantity nop would price this product at (ShoppingCartService.cs:1490-1497): when
    /// GroupTierPricesForDistinctShoppingCartItems is on it is how many of THIS PRODUCT the cart holds across
    /// all its lines, else 1. Passed in rather than read here because the caller is looping products and this
    /// is one query each.
    /// NOT BulkOrderModelFactory.GetCartQuantitiesAsync's number. That one is packname-scoped, because the
    /// "N in cart" badge should show the packname the customer is looking at. nop's tier lookup is not
    /// packname-aware and counts the whole cart, so reusing the badge's number would price some carts at the
    /// wrong tier - cheaper or dearer than the checkout will actually charge.
    /// </param>
    Task<BulkOrderPricingContext> PrepareAsync(Product product,
        BulkOrderGrid grid,
        Customer customer,
        Store store,
        bool displayPrices,
        int tierQuantity);

    /// <summary>
    /// The tier quantity for every product the cart holds, in one read - the map PrepareAsync's
    /// tierQuantity comes out of. A product absent from it is priced at 1; an empty map means the store has
    /// GroupTierPricesForDistinctShoppingCartItems off and no cart was read at all.
    /// </summary>
    Task<IDictionary<int, int>> GetTierQuantitiesAsync(Customer customer);
}