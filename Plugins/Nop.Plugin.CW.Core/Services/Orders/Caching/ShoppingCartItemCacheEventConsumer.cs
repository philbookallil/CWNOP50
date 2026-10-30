using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Orders.Caching;

public partial class ShoppingCartItemCacheEventConsumer : CacheEventConsumer<ShoppingCartItem>
{
    /// <summary>
    /// Clear cache data
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(ShoppingCartItem entity)
    {
        await RemoveAsync(PackNameServicesDefaults.CartCountsByCustomerCacheKey, entity.CustomerId);
        await RemoveByPrefixAsync(OrderServicesDefaults.PackNameCartCacheKeyPrefix, entity.CustomerId, entity.StoreId);
        await RemoveByPrefixAsync(OrderServicesDefaults.PackNameShoppingCartItemsCacheKeyPrefix, entity.CustomerId, entity.StoreId);
        await RemoveAsync(OrderServicesDefaults.HiddenPackNameCartItemIdsCacheKey, entity.CustomerId);
        await RemoveByPrefixAsync(OrderServicesDefaults.CartLinePackNamesPrefix);
        await RemoveByPrefixAsync(OrderServicesDefaults.CartReturnRequestItemsPrefix);

        await base.ClearCacheAsync(entity);
    }
}
