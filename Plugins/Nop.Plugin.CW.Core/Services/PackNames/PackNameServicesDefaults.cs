using Nop.Core.Caching;
using Nop.Web.Models.Common;
using Nop.Web.Models.Customer;

namespace Nop.Plugin.CW.Core.Services.PackNames;

public static partial class PackNameServicesDefaults
{
    #region Caching defaults

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : customer ID
    /// </remarks>
    public static CacheKey PackNameByCustomerCacheKey => new("CW.packname.bycustomer.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string PackNameByCustomerCacheKeyAllPrefix => "CW.packname.bycustomer.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : packaging name list identifier
    /// </remarks>
    public static CacheKey ActivePackNamesByListIdCacheKey => new("CW.packname.activepacknames.bylistid.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string ActivePackNamesByListIdCacheKeyAllPrefix => "CW.packname.activepacknames.bylistid.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : customer identifier
    /// </remarks>
    public static CacheKey CartCountsByCustomerCacheKey => new("CW.packname.cartcounts.bycustomer.{0}");

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : packaging name list identifier
    /// {1} : department identifiers
    /// </remarks>
    public static CacheKey ActivePackNamesByListIdForDepartmentsCacheKey => new("CW.packname.activepacknames.fordepartments.bylistid.{0}-{1}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string ActivePackNamesByListIdForDepartmentsCacheKeyAllPrefix => "CW.packname.activepacknames.fordepartments.bylistid.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : manager identifier
    /// </remarks>
    public static CacheKey ActivePackNameByManagerIdCacheKey => new("CW.packname.activepackname.bymanagerid.{0}");

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : packName identifier
    /// </remarks>
    public static CacheKey PackNameAddressesCacheKey => new("CW.packname.addresses.{0}");

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : packName identifier
    /// {1} : address identifier
    /// </remarks>
    public static CacheKey PackNameAddressCacheKey => new("CW.packname.addresses.{0}-{1}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string PackNameAddressesPrefix => "CW.packname.addresses.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : manager identifier
    /// </remarks>
    public static CacheKey DepartmentsByManagerCacheKey => new("CW.departments.bymanager.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string DepartmentsByManagerCacheKeyAllPrefix => "CW.departments.bymanager.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : manager identifier
    /// </remarks>
    public static CacheKey PackNamesByManagerCacheKey => new("CW.packnames.bymanager.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string PackNamesByManagerCacheKeyAllPrefix => "CW.packnames.bymanager.";

    /// <summary>
    /// Gets a key for caching
    /// </summary>
    /// <remarks>
    /// {0} : department ids, {1} : store id. The store is part of the key because the query filters on
    /// it — a parameter that changes the result and not the key hands one caller another's answer.
    /// </remarks>
    public static CacheKey PackNameListsByDepartmentIdsCacheKey => new("CW.packnamelists.bydepartment.ids.{0}-{1}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string PackNameListsByDepartmentIdsCacheKeyAllPrefix => "CW.packnamelists.bydepartment.ids.";

    /// <summary>
    /// Gets a key for the SET of packname ids a manager owns: {0} : manager id
    /// </summary>
    /// <remarks>
    /// One entry per manager. It replaced a per-(manager, packname) key: a key per pair is a key
    /// explosion at thousands of packnames, and every access guard calls it.
    /// This set carries the SAME predicate as the old per-pair query, deleted-only: it must NOT
    /// reuse the Active-filtered listing query, or an inactive packname stops counting as owned and
    /// its manager is refused access to it.
    /// </remarks>
    public static CacheKey PackNameOwnedIdsByManagerCacheKey => new("CW.packname.ownedids.bymanager.{0}");

    /// <summary>
    /// Gets a key pattern to clear cache
    /// </summary>
    public static string PackNameOwnedIdsByManagerCacheKeyAllPrefix => "CW.packname.ownedids.bymanager.";

    /// <summary>
    /// Gets a key for a restricted packname's visible products widened to their grouped-product relatives: {0} : fingerprint of its visible product ids
    /// </summary>
    public static CacheKey CatalogueVisibleProductIdsCacheKey => new("CW.catalogue.visible.products.{0}");

    /// <summary>
    /// Gets a key for the categories a restricted packname may see: {0} : fingerprint of its visible product ids
    /// </summary>
    public static CacheKey CatalogueVisibleCategoryIdsCacheKey => new("CW.catalogue.visible.categories.{0}");

    /// <summary>
    /// Gets a key for the manufacturers a restricted packname may see: {0} : fingerprint of its visible product ids
    /// </summary>
    public static CacheKey CatalogueVisibleManufacturerIdsCacheKey => new("CW.catalogue.visible.manufacturers.{0}");

    /// <summary>
    /// Gets a key pattern to clear the visible categories and manufacturers
    /// </summary>
    public static string CatalogueVisiblePrefix => "CW.catalogue.visible.";

    #endregion

    #region Registration

    /// <summary>
    /// The customer account fields a manager's registration does not take from the form or the file: the Company is
    /// the branch's name, and no VAT number is asked for. They are left out when the account's values are validated
    /// </summary>
    public static string[] ServerSuppliedCustomerFields => [nameof(CustomerInfoModel.Company), nameof(CustomerInfoModel.VatNumber)];

    /// <summary>
    /// The address fields a manager's registration does not take from the form or the file: the Company is the
    /// branch's name. They are left out when the address's values are validated
    /// </summary>
    public static string[] ServerSuppliedAddressFields => [nameof(AddressModel.Company)];

    #endregion
}
