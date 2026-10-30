using Nop.Core.Caching;

namespace Nop.Plugin.CW.Core.Services.MultiStores;

public static partial class MultiStoreServicesDefaults
{
    #region Gender

    public static string GenderMixed => "I";

    public static string GenderMale => "M";

    public static string GenderFemale => "F";

    public static string GenderNeutral => "N";

    #endregion

    #region PackName Type

    public static string PackNameTypeCustomer => "C";

    public static string PackNameTypePackname => "P";

    #endregion

    #region Caching defaults

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : cost center identifier
    /// </remarks>
    public static CacheKey CostCenterAddressesCacheKey => new("CW.costcenter.addresses.{0}");

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : cost center identifier
    /// {1} : address identifier
    /// </remarks>
    public static CacheKey CostCenterAddressCacheKey => new("CW.costcenter.addresses.{0}-{1}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string CostCenterAddressesPrefix => "CW.costcenter.addresses.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : store identifier
    /// </remarks>
    public static CacheKey CostCenterSelectItemsByStoreCacheKey => new("CW.costcenter.selectitems.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string CostCenterSelectItemsByStorePrefix => "CW.costcenter.selectitems.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : manager id, {1} : whether inactive lists were excluded (1) or included (0).
    /// The second parameter is NOT optional: ownership asks the SP with inactive lists INCLUDED
    /// (deactivating a list hides it from ordering, it does not revoke the manager's right to
    /// administer its packnames), while every visibility caller asks with
    /// them excluded. One key for both answers would serve whichever ran first.
    /// </remarks>
    public static CacheKey PackNameListIdsByManagerCacheKey => new("CW.packnamelistids.bymanager.{0}-{1}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string PackNameListIdsByManagerCacheKeyAllPrefix => "CW.packnamelistids.bymanager.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : manager identifier
    /// </remarks>
    public static CacheKey PackNameListsByManagerCacheKey => new("CW.packnamelists.bymanager.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string PackNameListsByManagerCacheKeyAllPrefix => "CW.packnamelists.bymanager.";

    #endregion
}
