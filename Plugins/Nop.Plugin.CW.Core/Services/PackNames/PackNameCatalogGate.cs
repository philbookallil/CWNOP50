using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.Subsidies;
using System.Collections.Concurrent;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// What a packname's subsidies let it see (<see cref="PackNameCatalogGateHelper"/>), answered for the packname whose
/// catalogue is being checked: the one in <see cref="PackNameCatalogScope"/>, else the one the signed-in customer has
/// selected in the public store.
/// </summary>
/// <remarks>
/// <para>
/// One rule for every reader: the product lists, the product, category and manufacturer pages, the related,
/// cross-sell, recently-viewed and wishlist blocks, add-to-cart, the menu and bulk ordering. Before it the
/// restriction was applied to five product lists only, and every other path showed — and added — what the lists
/// hid.
/// </para>
/// <para>
/// The product answer comes from the subsidy ordering caches, which expire at the next ordering-period change; it is
/// widened to grouped-product relatives, and the category and manufacturer answers are derived from it, all cached by
/// its fingerprint. A change to a product, the product mappings or the category tree clears them.
/// </para>
/// </remarks>
public class PackNameCatalogGate : IPackNameCatalogGate
{
    #region Fields

    protected readonly IRepository<Category> _categoryRepository;
    protected readonly IRepository<Product> _productRepository;
    protected readonly IRepository<ProductCategory> _productCategoryRepository;
    protected readonly IRepository<ProductManufacturer> _productManufacturerRepository;
    protected readonly IStaticCacheManager _staticCacheManager;
    //Lazy: the customer service resolves its role set through this gate.
    protected readonly Lazy<ICustomerOverrideService> _customerService;
    protected readonly Lazy<ISubsidyOrderingService> _subsidyOrderingService;

    //The visible products of each packname worked out in this request (the gate is scoped). Concurrent, so the
    //memo stays safe if a lookup is ever run beside another one in the same request.
    protected readonly ConcurrentDictionary<(int PackNameId, ExchangeType? Replacement), IList<int>?> _visibleProductIds = new();

    #endregion

    #region Ctor

    public PackNameCatalogGate(IRepository<Category> categoryRepository,
        IRepository<Product> productRepository,
        IRepository<ProductCategory> productCategoryRepository,
        IRepository<ProductManufacturer> productManufacturerRepository,
        IStaticCacheManager staticCacheManager,
        Lazy<ICustomerOverrideService> customerService,
        Lazy<ISubsidyOrderingService> subsidyOrderingService)
    {
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _productManufacturerRepository = productManufacturerRepository;
        _staticCacheManager = staticCacheManager;
        _customerService = customerService;
        _subsidyOrderingService = subsidyOrderingService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The visible products with their grouped-product relatives (<see cref="PackNameCatalogGateHelper.WithGroupedRelatives"/>)
    /// </summary>
    /// <param name="productIds">The visible product ids</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the ids with the associated products of a visible parent and the parent of a visible associated product
    /// </returns>
    protected virtual async Task<IList<int>> WithGroupedRelativesAsync(IList<int> productIds)
    {
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.CatalogueVisibleProductIdsCacheKey,
            PackNameCatalogGateHelper.Fingerprint(productIds));

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var relations = await _productRepository.Table
                .Where(p => !p.Deleted && p.ParentGroupedProductId > 0
                            && (productIds.Contains(p.Id) || productIds.Contains(p.ParentGroupedProductId)))
                .Select(p => new { p.Id, p.ParentGroupedProductId })
                .ToListAsync();

            return PackNameCatalogGateHelper.WithGroupedRelatives(productIds, relations.Select(p => (p.Id, p.ParentGroupedProductId)));
        });
    }

    #endregion

    #region Methods

    /// <summary>
    /// Product ids a packname may see, or null when nothing restricts it
    /// </summary>
    /// <param name="packName">PackName</param>
    /// <param name="replacement">The exchange type when checking an exchange replacement; null for a purchase</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the visible product ids; an empty list when none; null when unrestricted
    /// </returns>
    public virtual async Task<IList<int>?> GetVisibleProductIdsAsync(PackName packName, ExchangeType? replacement)
    {
        ArgumentNullException.ThrowIfNull(packName);

        //Asked on every role lookup and every product check of the request: answered once per packname and purpose.
        if (_visibleProductIds.TryGetValue((packName.Id, replacement), out var known))
            return known;

        var exempt = replacement is ExchangeType.Exact or ExchangeType.SameStyle;

        var canOrder = exempt || replacement != null || await _subsidyOrderingService.Value.PackNameCanOrderAsync(packName.Id);
        var allocatedProductIds = exempt ? null : await _subsidyOrderingService.Value.GetOnlyAllocatedProductIdsAsync(packName.Id);

        var visible = PackNameCatalogGateHelper.VisibleProductIds(canOrder, allocatedProductIds, replacement);

        if (visible is { Count: > 0 })
            visible = await WithGroupedRelativesAsync(visible);

        _visibleProductIds[(packName.Id, replacement)] = visible;

        return visible;
    }

    /// <summary>
    /// Product ids the customer's catalogue packname may see, or null when nothing restricts it
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the visible product ids; an empty list when none; null when unrestricted
    /// </returns>
    public virtual async Task<IList<int>?> GetVisibleProductIdsAsync(Customer customer)
    {
        if (customer == null)
            return null;

        var packName = await _customerService.Value.GetCatalogPackNameAsync(customer);

        //The replacement belongs to the window; the selected packname is always a purchase.
        return packName == null
            ? null
            : await GetVisibleProductIdsAsync(packName, PackNameCatalogScope.Current == null ? null : PackNameCatalogScope.CurrentReplacement);
    }

    /// <summary>
    /// Category ids the customer's catalogue packname may see, or null when nothing restricts it
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the categories holding a visible product, with their ancestors; null when unrestricted
    /// </returns>
    public virtual async Task<ISet<int>?> GetVisibleCategoryIdsAsync(Customer customer)
    {
        var productIds = await GetVisibleProductIdsAsync(customer);

        if (productIds == null)
            return null;

        if (productIds.Count == 0)
            return new HashSet<int>();

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.CatalogueVisibleCategoryIdsCacheKey,
            PackNameCatalogGateHelper.Fingerprint(productIds));

        var categoryIds = await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var direct = await _productCategoryRepository.Table
                .Where(pc => productIds.Contains(pc.ProductId))
                .Select(pc => pc.CategoryId)
                .Distinct()
                .ToListAsync();

            var parentById = (await _categoryRepository.Table
                    .Select(c => new { c.Id, c.ParentCategoryId })
                    .ToListAsync())
                .ToDictionary(c => c.Id, c => c.ParentCategoryId);

            return PackNameCatalogGateHelper.WithAncestors(direct, parentById).ToList();
        });

        return categoryIds.ToHashSet();
    }

    /// <summary>
    /// Manufacturer ids the customer's catalogue packname may see, or null when nothing restricts it
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manufacturers of a visible product; null when unrestricted
    /// </returns>
    public virtual async Task<ISet<int>?> GetVisibleManufacturerIdsAsync(Customer customer)
    {
        var productIds = await GetVisibleProductIdsAsync(customer);

        if (productIds == null)
            return null;

        if (productIds.Count == 0)
            return new HashSet<int>();

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(PackNameServicesDefaults.CatalogueVisibleManufacturerIdsCacheKey,
            PackNameCatalogGateHelper.Fingerprint(productIds));

        var manufacturerIds = await _staticCacheManager.GetAsync(cacheKey, async () => await _productManufacturerRepository.Table
            .Where(pm => productIds.Contains(pm.ProductId))
            .Select(pm => pm.ManufacturerId)
            .Distinct()
            .ToListAsync());

        return manufacturerIds.ToHashSet();
    }

    #endregion
}