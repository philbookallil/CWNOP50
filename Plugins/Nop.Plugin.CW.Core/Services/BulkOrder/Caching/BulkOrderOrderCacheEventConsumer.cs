using Nop.Core.Domain.Orders;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

/// <summary>
/// Clears the bulk-order category tree when an order changes.
/// </summary>
/// <remarks>
/// The Recently ordered and Best sellers trees are derived from orders, so without this a customer
/// who just checked out does not see the categories they ordered from until the cache expires.
/// The clear is by prefix, so one order rebuilds the tree for every filter/store/role/customer
/// combination on next use - accepted deliberately: the key puts the customer id fifth, so there is
/// no per-customer prefix, and the rebuild is one grouped query run lazily.
/// </remarks>
public partial class BulkOrderOrderCacheEventConsumer : CacheEventConsumer<Order>
{
    protected override async Task ClearCacheAsync(Order entity)
    {
        await RemoveByPrefixAsync(BulkOrderServicesDefaults.VisibleCategoryIdsPrefix);

        await base.ClearCacheAsync(entity);
    }
}