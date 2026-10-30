using Nop.Core.Caching;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

public static partial class BulkOrderServicesDefaults
{
    #region Caching defaults

    /// <summary>
    /// Gets a key for caching the category ids that contain at least one product visible
    /// under a given bulk-order filter
    /// </summary>
    /// <remarks>
    /// {0} : filter type
    /// {1} : store id
    /// {2} : customer role ids
    /// {3} : order id
    /// {4} : customer id (only when the filter depends on the customer's own orders)
    /// {5} : allocated-products fingerprint (0 when unrestricted)
    /// </remarks>
    public static CacheKey VisibleCategoryIdsCacheKey => new("CW.bulkorder.visiblecategoryids.{0}-{1}-{2}-{3}-{4}-{5}");

    public static string VisibleCategoryIdsPrefix => "CW.bulkorder.visiblecategoryids.";

    /// <summary>
    /// Gets a key for caching the grid SKELETON of one product
    /// </summary>
    /// <remarks>
    /// {0} : product id
    /// {1} : language id  (the skeleton holds localized names, prompts and value names)
    /// {2} : whether attribute images are on (it decides whether value picture ids were resolved)
    ///
    /// The store is deliberately NOT in the key. Nothing the skeleton holds is store-scoped: the mappings,
    /// values, combinations and their localized strings are product-level, and every store-dependent answer
    /// (visibility, price, stock, permissions) is computed per request on top of it.
    /// RestrictsToExistingCombinations is read from the Product on each request and is not in here either,
    /// so flipping an inventory setting needs no invalidation.
    /// </remarks>
    public static CacheKey SkeletonCacheKey => new("CW.bulkorder.skeleton.{0}-{1}-{2}");

    /// <summary>Everything cached for ONE product. Format with the product id.</summary>
    public static string SkeletonByProductPrefix => "CW.bulkorder.skeleton.{0}-";

    /// <summary>Every skeleton, every product. For invalidations that cannot name a product.</summary>
    public static string SkeletonPrefix => "CW.bulkorder.skeleton.";

    #endregion
}