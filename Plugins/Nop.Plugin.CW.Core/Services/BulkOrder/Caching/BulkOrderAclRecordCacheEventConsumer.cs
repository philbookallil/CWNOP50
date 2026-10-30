using Nop.Core.Domain.Security;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder.Caching;

/// <summary>
/// Same as the StoreMapping consumer: the key embeds customerRoleIds, but ApplyVisibilityAsync applies
/// the AclRecord rows themselves, so granting or revoking a role's access changes the answer without
/// changing the key. This is the path the department ACL picker writes on every save
/// (AclOverrideService.SaveAclDepartmentAsync), so it fires often.
/// </summary>
public partial class BulkOrderAclRecordCacheEventConsumer : CacheEventConsumer<AclRecord>
{
    protected override async Task ClearCacheAsync(AclRecord entity)
    {
        await RemoveByPrefixAsync(BulkOrderServicesDefaults.VisibleCategoryIdsPrefix);

        await base.ClearCacheAsync(entity);
    }
}