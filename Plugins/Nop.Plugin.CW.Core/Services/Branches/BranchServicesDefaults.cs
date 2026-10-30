using Nop.Core.Caching;

namespace Nop.Plugin.CW.Core.Services.Branches;

public static partial class BranchServicesDefaults
{
    #region Caching defaults

    #region Branches

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : store ID
    /// </remarks>
    public static CacheKey BranchesByStoreCacheKey => new("CW.branch.bystore.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string BranchesByStorePrefix => "CW.branch.bystore.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : manager identifier
    /// </remarks>
    public static CacheKey ManagerStoreIdCacheKey => new("CW.manager.storeId.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string ManagerStoreIdCacheKeyAllPrefix => "CW.manager.storeId.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : branch identifier
    /// </remarks>
    public static CacheKey DefaultShippingMethodsByBranchCacheKey => new("CW.shippingmethods.bybranch.{0}");

    #endregion

    #region Addresses

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : branch identifier
    /// </remarks>
    public static CacheKey BranchAddressesCacheKey => new("CW.branch.addresses.{0}");

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : branch identifier
    /// {1} : address identifier
    /// </remarks>
    public static CacheKey BranchAddressCacheKey => new("CW.branch.addresses.{0}-{1}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string BranchAddressesPrefix => "CW.branch.addresses.";

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    /// <remarks>
    /// {0} : branch identifier
    /// </remarks>
    public static string BranchAddressesByBranchPrefix => "CW.branch.addresses.{0}";

    /// <summary>
    /// Key for caching of branch breadcrumb
    /// </summary>
    /// <remarks>
    /// {0} : branch id
    /// </remarks>
    public static CacheKey BranchBreadcrumbCacheKey => new("CW.branch.breadcrumb.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string BranchBreadcrumbPrefix => "CW.branch.breadcrumb.";

    /// <summary>
    /// Key for caching of branch staff count
    /// </summary>
    /// <remarks>
    /// {0} : branch id
    /// </remarks>
    public static CacheKey BranchStaffCountCacheKey => new("CW.branch.staffcount.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string BranchStaffCountPrefix => "CW.branch.staffcount.";

    /// <summary>
    /// Key for caching of branch staff count
    /// </summary>
    /// <remarks>
    /// {0} : customer id
    /// </remarks>
    public static CacheKey BranchByCustomerIdCacheKey => new("CW.branch.bycustomerid.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string BranchByCustomerIdPrefix => "CW.branch.bycustomerid.";

    #endregion

    #endregion
}
