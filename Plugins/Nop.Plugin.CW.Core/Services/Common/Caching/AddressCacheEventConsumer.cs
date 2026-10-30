using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.Common.Caching;

public partial class AddressCacheEventConsumer : CacheEventConsumer<Address>
{
    /// <summary>
    /// Clear cache by entity event type
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <param name="entityEventType">Entity event type</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task ClearCacheAsync(Address entity, EntityEventType entityEventType)
    {
        await RemoveByPrefixAsync(PackNameServicesDefaults.PackNameAddressesPrefix);
        await RemoveByPrefixAsync(MultiStoreServicesDefaults.CostCenterAddressesPrefix);
        //The admin order packname groups embed the shipping address, and an Address cannot know which
        //orders use it - so editing one left every already-read order showing the old address (H-5).
        await RemoveByPrefixAsync(OrderServicesDefaults.PackNameOrderGroupsAllPrefix);

        switch (entityEventType)
        {
            case EntityEventType.Update:
            case EntityEventType.Delete:
                await RemoveByPrefixAsync(BranchServicesDefaults.BranchAddressesPrefix);
                break;
        }
    }
}
