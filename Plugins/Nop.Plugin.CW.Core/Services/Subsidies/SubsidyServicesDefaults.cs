using Nop.Core.Caching;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

public static partial class SubsidyServicesDefaults
{
    #region Caching defaults

    /// <summary>
    /// Key for subsidies in store
    /// </summary>
    /// <remarks>
    /// {0} : Store id
    /// </remarks>
    public static CacheKey ActiveSubsidiesByStoreCacheKey => new("CW.subsidy.active.subsidies.bystore.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static string ActiveSubsidiesByStorePrefix => "CW.subsidy.active.subsidies.bystore.";

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    /// <remarks>
    /// {0} : subsidy id
    /// </remarks>
    public static CacheKey SubsidyPackNamesCacheKey => new("CW.subsidy.packnames.bysubsidy.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static string SubsidyPackNamesPrefix => "CW.subsidy.packnames.bysubsidy.";

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    /// <remarks>
    /// {0} : packName id
    /// </remarks>
    public static CacheKey SubsidiesByPackNameCacheKey => new("CW.subsidy.subsidies.bypackname.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static string SubsidiesByPackNamePrefix => "CW.subsidy.subsidies.bypackname.";

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    /// <remarks>
    /// {0} : packName id
    /// </remarks>
    public static CacheKey InOrderingSubsidiesByPackNameCacheKey => new("CW.subsidy.inordering.subsidies.bypackname.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static string InOrderingSubsidiesByPackNamePrefix => "CW.subsidy.inordering.subsidies.bypackname.";

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    /// <remarks>
    /// {0} : packName identifier
    /// </remarks>
    public static CacheKey OrderingBlockedPackNameCacheKey => new("CW.subsidy.orderingblocked.bypacknameid.{0}");

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    public static string OrderingBlockedPackNamePrefix => new("CW.subsidy.orderingblocked.bypacknameid.");

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    /// <remarks>
    /// {0} : packName identifier
    /// </remarks>
    public static CacheKey PackNameHasActiveProductGroupsCacheKey => new("CW.subsidy.hasactiveproductgroups.bypacknameid.{0}");

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    public static string PackNameHasActiveProductGroupsAllPrefix => new("CW.subsidy.hasactiveproductgroups.bypacknameid.");

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    /// <remarks>
    /// {0} : subsidy - packName identifier
    /// </remarks>
    public static CacheKey SubsidyVariationBySubsidyPackNameCacheKey => new("CW.subsidy.subsidyvariation.bysubsidypacknameid.{0}");

    /// <summary>
    /// Key for icons for product groups in subsidy
    /// </summary>
    public static CacheKey SubsidyProductGroupIconsCacheKey => new("CW.subsidy.productgroups.icons");

    /// <summary>
    /// Key for products in product group
    /// </summary>
    /// <remarks>
    /// {0} : product group id
    /// </remarks>
    public static CacheKey ProductsByProductGroupCacheKey => new("CW.subsidy.products.byproductgroup.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache.
    /// </summary>
    /// <remarks>
    /// Required: the key is keyed by PRODUCT GROUP id, and a deleted Product does not know which groups
    /// contain it, so it must clear the whole set (H-6). ProductGroupProductCacheEventConsumer clears
    /// by key because the mapping row does carry the group id.
    /// </remarks>
    public static string ProductsByProductGroupPrefix => "CW.subsidy.products.byproductgroup.";

    /// <summary>
    /// Key for product groups in subsidy
    /// </summary>
    /// <remarks>
    /// {0} : subsidy id
    /// </remarks>
    public static CacheKey SubsidyProductGroupsCacheKey => new("CW.subsidy.productgroups.bysubsidy.{0}");

    /// <summary>
    /// Key for the next moment any subsidy of a packname opens or closes its ordering period
    /// </summary>
    /// <remarks>
    /// {0} : Packname identifier
    /// </remarks>
    public static CacheKey PackNameNextSubsidyStatusChangeCacheKey => new("CW.subsidy.nextstatuschange.bypacknameid.{0}");

    /// <summary>
    /// Gets a key pattern to clear the next status change of every packname
    /// </summary>
    public static string PackNameNextSubsidyStatusChangePrefix => "CW.subsidy.nextstatuschange.bypacknameid.";

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    /// <remarks>
    /// {0} : packName identifier
    /// </remarks>
    public static CacheKey PackNameOrderingProductsCacheKey => new("CW.subsidy.orderingproducts.bypacknameid.{0}");

    /// <summary>
    /// Key for packnames in subsidy
    /// </summary>
    /// <remarks>
    /// {0} : packName identifier
    /// </remarks>
    public static string PackNameOrderingProductsPrefix => new("CW.subsidy.orderingproducts.bypacknameid.");

    #endregion
}
