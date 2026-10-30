using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// Provides shopping-cart-level subsidy usage for the subsidy catalog and bar.
/// These values are NOT stored in SubsidyOrderItem — they are calculated live
/// from the packname customer's current shopping cart.
/// </summary>
public interface ISubsidyCatalogCartService
{
    /// <summary>
    /// Simulates credit consumption for all current cart items of the given packname
    /// and returns a flat list of per-product-group and global usage records.
    /// Call this once per page render; results are aggregated by the model factory.
    /// </summary>
    Task<IList<SubsidyCartUsageItem>> GetPackNameSubsidyCartUsageAsync(int packNameId, int storeId);

    /// <summary>
    /// Search products
    /// </summary>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="productIds">Product identifiers</param>
    /// <param name="storeId">Store identifier; 0 to load all records</param>
    /// <param name="visibleIndividuallyOnly">A values indicating whether to load only products marked as "visible individually"; "false" to load all records; "true" to load "visible individually" only</param>
    /// <param name="excludeFeaturedProducts">A value indicating whether loaded products are marked as featured (relates only to categories and manufacturers); "false" (by default) to load all records; "true" to exclude featured products from results</param>
    /// <param name="filteredSpecOptions">Specification options list to filter products; null to load all records</param>
    /// <param name="orderBy">Order by</param>
    /// <param name="showHidden">A value indicating whether to show hidden records</param>
    /// null - process "Published" property according to "showHidden" parameter
    /// true - load only "Published" products
    /// false - load only "Unpublished" products
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the products
    /// </returns>
    Task<IPagedList<Product>> SearchProductsAsync(
        int pageIndex = 0,
        int pageSize = int.MaxValue,
        IList<int> productIds = null,
        int storeId = 0,
        bool visibleIndividuallyOnly = false,
        bool excludeFeaturedProducts = false,
        IList<SpecificationAttributeOption> filteredSpecOptions = null,
        ProductSortingEnum orderBy = ProductSortingEnum.Position,
        bool showHidden = false);
}
