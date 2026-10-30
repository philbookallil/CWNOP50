using Nop.Core.Caching;

namespace Nop.Plugin.CW.Core.Services.Orders;

public static partial class OrderServicesDefaults
{
    #region Caching defaults

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : customer ID
    /// {1} : store ID
    /// {2} : packName ID
    /// </remarks>
    public static CacheKey PackNameCartCacheKey => new("CW.packname.shoppingcart.{0}-{1}-{2}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    /// <remarks>
    /// {0} : customer ID
    /// {1} : store ID
    /// </remarks>
    public static string PackNameCartCacheKeyPrefix => "CW.packname.shoppingcart.{0}-{1}";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : customer ID
    /// {1} : store ID
    /// {2} : packName ID
    /// </remarks>
    public static CacheKey PackNameShoppingCartItemsCacheKey => new("CW.packname.shoppingcartitems.{0}-{1}-{2}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    /// <remarks>
    /// {0} : customer ID
    /// {1} : store ID
    /// </remarks>
    public static string PackNameShoppingCartItemsCacheKeyPrefix => "CW.packname.shoppingcartitems.{0}-{1}";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : customer ID
    /// </remarks>
    public static CacheKey HiddenPackNameCartItemIdsCacheKey => new("CW.packname.hiddencartitemids.{0}");

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : order ID
    /// </remarks>
    public static CacheKey OrderShippingCacheKey => new("CW.ordershipping.all.{0}");

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : order ID
    /// </remarks>
    public static CacheKey PackNameOrderGroupsCacheKey => new("CW.order.packname.groups.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache.
    /// </summary>
    /// <remarks>
    /// Required, not redundant: the key is keyed by ORDER id, and the two entities that invalidate it
    /// cannot produce one. PackNameOrderItem carries OrderItemId, and an Address knows nothing about
    /// the order it is used on. Both must clear the whole set (H-5).
    /// </remarks>
    public static string PackNameOrderGroupsAllPrefix => "CW.order.packname.groups.";

    /// <summary>
    /// Key for the per-request settlement plan.
    /// {0} : customer id
    /// {1} : store id
    /// {2} : cart shape (item id + quantity)
    /// {3} : explicit settleable amount, or -1
    /// </summary>
    public static CacheKey CartSettlementPlanCacheKey => new("CW.cart.settlementplan.{0}-{1}-{2}-{3}");

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : order ID
    /// </remarks>
    public static CacheKey OrderSettlementCacheKey => new("CW.order.settlement.{0}");

    /// <summary>
    /// Key for the per-request map of a customer's cart lines to the return request items they replace
    /// </summary>
    /// <remarks>
    /// {0} : customer ID
    /// </remarks>
    public static CacheKey CartReturnRequestItemsCacheKey => new("CW.cart.returnrequestitems.{0}");

    /// <summary>
    /// Gets a key pattern to clear the per-request map of cart lines to return request items
    /// </summary>
    public static string CartReturnRequestItemsPrefix => "CW.cart.returnrequestitems.";

    /// <summary>
    /// Key for the per-request map of a customer's cart lines to their packnames
    /// </summary>
    /// <remarks>
    /// {0} : customer ID
    /// </remarks>
    public static CacheKey CartLinePackNamesCacheKey => new("CW.cart.linepacknames.{0}");

    /// <summary>
    /// Gets a key pattern to clear the per-request map of cart lines to packnames
    /// </summary>
    public static string CartLinePackNamesPrefix => "CW.cart.linepacknames.";

    /// <summary>
    /// Key for the per-request subtotal of a packname cart before discounts, including tax
    /// </summary>
    /// <remarks>
    /// {0} : customer ID
    /// {1} : store ID
    /// {2} : cart shape (item id + quantity)
    /// {3} : billing address ID
    /// {4} : shipping address ID
    /// </remarks>
    public static CacheKey PackNameCartSubTotalCacheKey => new("CW.cart.packnamesubtotal.{0}-{1}-{2}-{3}-{4}");

    /// <summary>
    /// Prefix of <see cref="PackNameCartSubTotalCacheKey"/>
    /// </summary>
    public static string PackNameCartSubTotalPrefix => "CW.cart.packnamesubtotal.";

    #endregion
}