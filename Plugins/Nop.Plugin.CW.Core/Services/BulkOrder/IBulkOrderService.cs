using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.BulkOrder;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

public interface IBulkOrderService
{
    /// <summary>
    /// Searches products for the bulk ordering page, applying every visibility constraint
    /// that the catalogue applies
    /// </summary>
    Task<IPagedList<Product>> SearchProductsAsync(BulkOrderQuery query);

    /// <summary>
    /// Gets the ids of every category containing at least one product visible under the
    /// given filter, plus all their ancestors. Cached.
    /// </summary>
    Task<IList<int>> GetVisibleCategoryIdsAsync(BulkOrderFilterType filterType, int storeId, int orderId);

    /// <summary>
    /// Gets the customer's orders for the "latest ordered" order picker, newest first
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="maxOrders">Maximum orders to return; 0 for all</param>
    Task<IList<Order>> GetCustomerOrdersAsync(int storeId, int maxOrders = 0);

    /// <summary>
    /// Gets one product, but only if it passes every visibility constraint the list applies
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>The product, or null when the customer may not see it</returns>
    Task<Product> GetVisibleProductByIdAsync(int productId, int storeId);

    /// <summary>
    /// Gets the units of each product currently in the customer's cart, keyed by product id
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>Product id to units; products with nothing in the cart are absent</returns>
    /// <remarks>
    /// Packname-scoped when a packname is in context, which is what the list's "n in cart" figure has always
    /// meant. It lives on the service rather than on the factory because two callers now need the same
    /// number - the factory renders it, and the add path returns the refreshed value. Two
    /// copies of this grouping is how the card and the response start disagreeing.
    /// </remarks>
    Task<IDictionary<int, int>> GetCartQuantitiesAsync(Customer customer, int storeId);
}