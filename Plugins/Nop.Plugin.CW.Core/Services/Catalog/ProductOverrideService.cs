using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.ArtificialIntelligence;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.ArtificialIntelligence;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Shipping.Date;
using Nop.Services.Stores;
using Nop.Services.Vendors;
using System.Data.SqlTypes;

namespace Nop.Plugin.CW.Core.Services.Catalog;

public class ProductOverrideService : ProductService
{
    #region Fields

    protected readonly IPackNameCatalogGate _catalogGate;
    protected readonly ICustomerOverrideService _customerOverrideService;

    #endregion

    #region Ctor

    public ProductOverrideService(
        ArtificialIntelligenceSettings artificialIntelligenceSettings,
        CatalogSettings catalogSettings,
        IAclService aclService,
        IAiRecommendationPluginManager aiRecommendationPluginManager,
        ICustomerService customerService,
        IDateRangeService dateRangeService,
        ILanguageService languageService,
        ILocalizationService localizationService,
        IMeasureService measureService,
        IProductAttributeParser productAttributeParser,
        IProductAttributeService productAttributeService,
        IRepository<Category> categoryRepository,
        IRepository<CrossSellProduct> crossSellProductRepository,
        IRepository<DiscountProductMapping> discountProductMappingRepository,
        IRepository<LocalizedProperty> localizedPropertyRepository,
        IRepository<Manufacturer> manufacturerRepository,
        IRepository<Product> productRepository,
        IRepository<Product3dObject> product3dObjectRepository,
        IRepository<ProductAttributeCombination> productAttributeCombinationRepository,
        IRepository<ProductAttributeMapping> productAttributeMappingRepository,
        IRepository<ProductCategory> productCategoryRepository,
        IRepository<ProductManufacturer> productManufacturerRepository,
        IRepository<ProductPicture> productPictureRepository,
        IRepository<ProductProductTagMapping> productTagMappingRepository,
        IRepository<ProductSpecificationAttribute> productSpecificationAttributeRepository,
        IRepository<ProductTag> productTagRepository,
        IRepository<ProductVideo> productVideoRepository,
        IRepository<ProductWarehouseInventory> productWarehouseInventoryRepository,
        IRepository<RelatedProduct> relatedProductRepository,
        IRepository<Shipment> shipmentRepository,
        IRepository<StockQuantityHistory> stockQuantityHistoryRepository,
        IRepository<TierPrice> tierPriceRepository,
        ISearchPluginManager searchPluginManager,
        IStaticCacheManager staticCacheManager,
        IVendorService vendorService,
        IStoreMappingService storeMappingService,
        IWorkContext workContext,
        LocalizationSettings localizationSettings,
        IPackNameCatalogGate catalogGate,
        ICustomerOverrideService customerOverrideService)
        : base(artificialIntelligenceSettings, catalogSettings, aclService, aiRecommendationPluginManager, customerService, dateRangeService,
               languageService, localizationService, measureService, productAttributeParser,
               productAttributeService, categoryRepository, crossSellProductRepository,
               discountProductMappingRepository, localizedPropertyRepository,
               manufacturerRepository, productRepository, product3dObjectRepository, productAttributeCombinationRepository,
               productAttributeMappingRepository, productCategoryRepository,
               productManufacturerRepository, productPictureRepository, productTagMappingRepository,
               productSpecificationAttributeRepository, productTagRepository, productVideoRepository,
               productWarehouseInventoryRepository, relatedProductRepository, shipmentRepository,
               stockQuantityHistoryRepository, tierPriceRepository, searchPluginManager,
               staticCacheManager, vendorService, storeMappingService, workContext,
               localizationSettings)
    {
        _catalogGate = catalogGate;
        _customerOverrideService = customerOverrideService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Returns the product IDs the current customer's catalogue packname may see, or null if no restriction
    /// applies - the same answer the product page, add-to-cart and the menus use (IPackNameCatalogGate). An exchange
    /// replacement picker's window is honoured, so it lists what that packname may receive.
    /// </summary>
    protected virtual async Task<IList<int>?> GetAllocatedProductIdsForCurrentCustomerAsync()
    {
        return await _catalogGate.GetVisibleProductIdsAsync(await _workContext.GetCurrentCustomerAsync());
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Gets a product tier prices for customer
    /// </summary>
    /// <remarks>
    /// A tier price is what the customer pays, so it follows the signed-in customer's roles even while the
    /// catalogue follows a packname's. nop filters on GetCustomerRoleIdsAsync, which in the packname context
    /// answers with the packname's roles.
    /// </remarks>
    /// <param name="product">Product</param>
    /// <param name="customer">Customer</param>
    /// <param name="store">Store</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task<IList<TierPrice>> GetTierPricesAsync(Product product, Customer customer, Store store)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(customer);

        return (await GetTierPricesByProductAsync(product.Id))
            .OrderBy(price => price.Quantity)
            .FilterByStore(store)
            .FilterByCustomerRole(await _customerOverrideService.GetSignedInCustomerRoleIdsAsync(customer))
            .FilterByDate()
            .RemoveDuplicatedQuantities()
            .ToList();
    }

    /// <summary>
    /// ⚠️ UPGRADE NOTE (NOP 5.00): The restricted-user branch is a copy of
    /// ProductService.SearchProductsAsync with one CW addition (marked inline).
    /// When upgrading NOP, diff the base method and merge any changes.
    /// </summary>
    public override async Task<IPagedList<Product>> SearchProductsAsync(
        int pageIndex = 0,
        int pageSize = int.MaxValue,
        IList<int> categoryIds = null,
        IList<int> manufacturerIds = null,
        int storeId = 0,
        int vendorId = 0,
        int warehouseId = 0,
        ProductType? productType = null,
        bool visibleIndividuallyOnly = false,
        bool excludeFeaturedProducts = false,
        decimal? priceMin = null,
        decimal? priceMax = null,
        int productTagId = 0,
        string keywords = null,
        bool searchDescriptions = false,
        bool searchManufacturerPartNumber = true,
        bool searchSku = true,
        bool searchProductTags = false,
        int languageId = 0,
        IList<SpecificationAttributeOption> filteredSpecOptions = null,
        ProductSortingEnum orderBy = ProductSortingEnum.Position,
        bool showHidden = false,
        bool? overridePublished = null)
    {
        //showHidden is the admin's product search: a packname's restriction belongs to the storefront, and an
        //admin whose own packname is restricted or closed must still see the whole catalogue.
        var allocatedIds = showHidden ? null : await GetAllocatedProductIdsForCurrentCustomerAsync();

        // Fast path: not restricted — delegate to base unchanged
        if (allocatedIds == null)
            return await base.SearchProductsAsync(pageIndex, pageSize, categoryIds,
                manufacturerIds, storeId, vendorId, warehouseId, productType,
                visibleIndividuallyOnly, excludeFeaturedProducts, priceMin, priceMax,
                productTagId, keywords, searchDescriptions, searchManufacturerPartNumber,
                searchSku, searchProductTags, languageId, filteredSpecOptions, orderBy,
                showHidden, overridePublished);

        // Restriction applies but no products allocated — return empty
        if (!allocatedIds.Any())
            return new PagedList<Product>(new List<Product>(), pageIndex, Math.Max(pageSize, 1));

        // Restricted path: same logic as base with one WHERE clause added.
        var allocatedSet = allocatedIds.ToHashSet();

        if (pageSize == int.MaxValue)
            pageSize = int.MaxValue - 1;

        //results of the search provider run
        List<int> providerResults = null;

        var productsQuery = _productRepository.Table;

        if (!showHidden)
            productsQuery = productsQuery.Where(p => p.Published);
        else if (overridePublished.HasValue)
            productsQuery = productsQuery.Where(p => p.Published == overridePublished.Value);

        var customer = await _workContext.GetCurrentCustomerAsync();

        if (!showHidden || storeId > 0)
            productsQuery = await _storeMappingService.ApplyStoreMapping(productsQuery, storeId);

        if (!showHidden)
            productsQuery = await _aclService.ApplyAcl(productsQuery, customer);

        // ── CW: restrict to allocated products at the SQL level ─────────────
        productsQuery = productsQuery.Where(p => allocatedSet.Contains(p.Id));
        // ── END CW ──────────────────────────────────────────────────────────

        productsQuery =
            from p in productsQuery
            where !p.Deleted &&
                  (!visibleIndividuallyOnly || p.VisibleIndividually) &&
                  (vendorId == 0 || p.VendorId == vendorId) &&
                  (
                      warehouseId == 0 ||
                      (
                          !p.UseMultipleWarehouses ? p.WarehouseId == warehouseId :
                              _productWarehouseInventoryRepository.Table.Any(pwi =>
                                  pwi.WarehouseId == warehouseId && pwi.ProductId == p.Id)
                      )
                  ) &&
                  (productType == null || p.ProductTypeId == (int)productType) &&
                  (showHidden ||
                   DateTime.UtcNow >= (p.AvailableStartDateTimeUtc ?? SqlDateTime.MinValue.Value) &&
                   DateTime.UtcNow <= (p.AvailableEndDateTimeUtc ?? SqlDateTime.MaxValue.Value)
                  ) &&
                  (priceMin == null || p.Price >= priceMin) &&
                  (priceMax == null || p.Price <= priceMax)
            select p;

        if (!string.IsNullOrEmpty(keywords))
        {
            //try to use AI-powered recommendation provider to search products
            if (!showHidden && await _aiRecommendationPluginManager.LoadPrimaryPluginAsync(customer, storeId) is { } aiRecommendationPlugin)
            {
                try
                {
                    var (results, totalItems) = await aiRecommendationPlugin
                        .SearchProductsAsync(keywords, categoryIds, manufacturerIds, productTagId, filteredSpecOptions, pageIndex, pageSize);

                    if (!results.Any())
                        throw new Exception("AI-powered recommendation provider. No products found by the specified parameters");

                    var sortedResult = await productsQuery
                        .Join(results, product => product.Id, id => id, (product, id) => product)
                        .OrderBy(orderBy, _localizedPropertyRepository, languageId > 0 ? languageId : (await _workContext.GetWorkingLanguageAsync()).Id, results)
                        .ToListAsync();

                    return new PagedList<Product>(sortedResult, pageIndex, pageSize, totalItems);
                }
                catch
                {
                    if (!_artificialIntelligenceSettings.UseStandardSearchWhenNoResults)
                    {
                        return await productsQuery
                            .OrderBy(orderBy, _localizedPropertyRepository, languageId > 0 ? languageId : (await _workContext.GetWorkingLanguageAsync()).Id)
                            .ToPagedListAsync(pageIndex, pageSize);
                    }
                }
            }

            var langs = await _languageService.GetAllLanguagesAsync(showHidden: true);

            //set a flag which will to points need to search in localized properties.
            //if showHidden doesn't set to true should be at least two published languages.
            var searchLocalizedValue = languageId > 0 && langs.Count >= 2 && (showHidden || langs.Count(l => l.Published) >= 2);

            var productsByKeywords = new List<int>().AsQueryable();
            var runStandardSearch = true;

            //try to use search provider for searching products by keywords
            if (!showHidden && await _searchPluginManager.LoadPrimaryPluginAsync(customer, storeId) is ISearchProvider searchProvider)
            {
                try
                {
                    providerResults = await searchProvider.SearchProductsAsync(keywords, searchLocalizedValue);
                    if (providerResults?.Any() != true)
                        throw new Exception("Search provider. No products found by the specified keywords");

                    productsByKeywords = providerResults.AsQueryable();
                    runStandardSearch = false;
                }
                catch
                {
                    runStandardSearch = _catalogSettings.UseStandardSearchWhenNoResults;
                }
            }

            //use standard search if search provider is not active or failed
            //also use standard search if showHidden is set to true, because in this case we should return all products (including hidden) and not only products that match the keywords
            if (runStandardSearch)
            {
                productsByKeywords =
                    from p in _productRepository.Table
                    where p.Name.Contains(keywords) ||
                          (searchDescriptions &&
                           (p.ShortDescription.Contains(keywords) || p.FullDescription.Contains(keywords))) ||
                          (searchManufacturerPartNumber && p.ManufacturerPartNumber == keywords) ||
                          (searchSku && p.Sku == keywords)
                    select p.Id;

                if (searchLocalizedValue)
                {
                    productsByKeywords = productsByKeywords.Union(
                        from lp in _localizedPropertyRepository.Table
                        let checkName = lp.LocaleKey == nameof(Product.Name) &&
                                        lp.LocaleValue.Contains(keywords)
                        let checkShortDesc = searchDescriptions &&
                                             lp.LocaleKey == nameof(Product.ShortDescription) &&
                                             lp.LocaleValue.Contains(keywords)
                        where lp.LocaleKeyGroup == nameof(Product) &&
                              lp.LanguageId == languageId &&
                              (checkName || checkShortDesc)
                        select lp.EntityId);
                }
            }

            if (searchSku)
            {
                productsByKeywords = productsByKeywords.Union(
                    from pac in _productAttributeCombinationRepository.Table
                    where pac.Sku == keywords
                    select pac.ProductId);
            }

            if (_catalogSettings.AllowCustomersToSearchWithCategoryName)
            {
                var categoryQuery = _categoryRepository.Table;

                if (!showHidden)
                    categoryQuery = categoryQuery.Where(p => p.Published);
                else if (overridePublished.HasValue)
                    categoryQuery = categoryQuery.Where(p => p.Published == overridePublished.Value);

                if (!showHidden || storeId > 0)
                    categoryQuery = await _storeMappingService.ApplyStoreMapping(categoryQuery, storeId);

                if (!showHidden)
                    categoryQuery = await _aclService.ApplyAcl(categoryQuery, customer);

                productsByKeywords = productsByKeywords.Union(
                    from pc in _productCategoryRepository.Table
                    join c in categoryQuery on pc.CategoryId equals c.Id
                    where c.Name.Contains(keywords) && !c.Deleted
                    select pc.ProductId);

                if (searchLocalizedValue)
                {
                    productsByKeywords = productsByKeywords.Union(
                        from pc in _productCategoryRepository.Table
                        join lp in _localizedPropertyRepository.Table on pc.CategoryId equals lp.EntityId
                        where lp.LocaleKeyGroup == nameof(Category) &&
                              lp.LocaleKey == nameof(Category.Name) &&
                              lp.LocaleValue.Contains(keywords) &&
                              lp.LanguageId == languageId
                        select pc.ProductId);
                }
            }

            //search by manufacturer name if admin allows
            if (_catalogSettings.AllowCustomersToSearchWithManufacturerName)
            {
                var manufacturerQuery = _manufacturerRepository.Table;

                if (!showHidden)
                    manufacturerQuery = manufacturerQuery.Where(p => p.Published);
                else if (overridePublished.HasValue)
                    manufacturerQuery = manufacturerQuery.Where(p => p.Published == overridePublished.Value);

                if (!showHidden || storeId > 0)
                    manufacturerQuery = await _storeMappingService.ApplyStoreMapping(manufacturerQuery, storeId);

                if (!showHidden)
                    manufacturerQuery = await _aclService.ApplyAcl(manufacturerQuery, customer);

                productsByKeywords = productsByKeywords.Union(
                    from pm in _productManufacturerRepository.Table
                    join m in manufacturerQuery on pm.ManufacturerId equals m.Id
                    where m.Name.Contains(keywords) && !m.Deleted
                    select pm.ProductId);

                if (searchLocalizedValue)
                {
                    productsByKeywords = productsByKeywords.Union(
                        from pm in _productManufacturerRepository.Table
                        join lp in _localizedPropertyRepository.Table on pm.ManufacturerId equals lp.EntityId
                        where lp.LocaleKeyGroup == nameof(Manufacturer) &&
                              lp.LocaleKey == nameof(Manufacturer.Name) &&
                              lp.LocaleValue.Contains(keywords) &&
                              lp.LanguageId == languageId
                        select pm.ProductId);
                }
            }

            if (searchProductTags)
            {
                productsByKeywords = productsByKeywords.Union(
                    from pptm in _productTagMappingRepository.Table
                    join pt in _productTagRepository.Table on pptm.ProductTagId equals pt.Id
                    where pt.Name.Contains(keywords)
                    select pptm.ProductId);

                if (searchLocalizedValue)
                {
                    productsByKeywords = productsByKeywords.Union(
                        from pptm in _productTagMappingRepository.Table
                        join lp in _localizedPropertyRepository.Table on pptm.ProductTagId equals lp.EntityId
                        where lp.LocaleKeyGroup == nameof(ProductTag) &&
                              lp.LocaleKey == nameof(ProductTag.Name) &&
                              lp.LocaleValue.Contains(keywords) &&
                              lp.LanguageId == languageId
                        select pptm.ProductId);
                }
            }

            productsQuery =
                from p in productsQuery
                join pbk in productsByKeywords on p.Id equals pbk
                select p;
        }

        if (categoryIds is not null)
        {
            categoryIds.Remove(0);

            if (categoryIds.Any())
            {
                var productCategoryQuery =
                    from pc in _productCategoryRepository.Table
                    where (!excludeFeaturedProducts || !pc.IsFeaturedProduct) &&
                          categoryIds.Contains(pc.CategoryId)
                    group pc.DisplayOrder by pc.ProductId into gr
                    select new { ProductId = gr.Key, DisplayOrder = gr.Min() };

                productsQuery =
                    from p in productsQuery
                    join pc in productCategoryQuery on p.Id equals pc.ProductId
                    orderby pc.DisplayOrder, p.Name
                    select p;
            }
        }

        if (manufacturerIds is not null)
        {
            manufacturerIds.Remove(0);

            if (manufacturerIds.Any())
            {
                var productManufacturerQuery =
                    from pm in _productManufacturerRepository.Table
                    where (!excludeFeaturedProducts || !pm.IsFeaturedProduct) &&
                          manufacturerIds.Contains(pm.ManufacturerId)
                    group pm by pm.ProductId into pm
                    select new { ProductId = pm.Key, DisplayOrder = pm.First().DisplayOrder };

                productsQuery =
                    from p in productsQuery
                    join pm in productManufacturerQuery on p.Id equals pm.ProductId
                    orderby pm.DisplayOrder, p.Name
                    select p;
            }
        }

        if (productTagId > 0)
        {
            productsQuery =
                from p in productsQuery
                join ptm in _productTagMappingRepository.Table on p.Id equals ptm.ProductId
                where ptm.ProductTagId == productTagId
                select p;
        }

        if (filteredSpecOptions?.Count > 0)
        {
            var specificationAttributeIds = filteredSpecOptions
                .Select(sao => sao.SpecificationAttributeId)
                .Distinct();

            foreach (var specificationAttributeId in specificationAttributeIds)
            {
                var optionIdsBySpecificationAttribute = filteredSpecOptions
                    .Where(o => o.SpecificationAttributeId == specificationAttributeId)
                    .Select(o => o.Id);

                var productSpecificationQuery =
                    from psa in _productSpecificationAttributeRepository.Table
                    where psa.AllowFiltering &&
                          optionIdsBySpecificationAttribute.Contains(psa.SpecificationAttributeOptionId)
                    select psa;

                productsQuery =
                    from p in productsQuery
                    where productSpecificationQuery.Any(pc => pc.ProductId == p.Id)
                    select p;
            }
        }

        return await productsQuery
            .OrderBy(orderBy, _localizedPropertyRepository, languageId > 0 ? languageId : (await _workContext.GetWorkingLanguageAsync()).Id, providerResults)
            .ToPagedListAsync(pageIndex, pageSize);
    }

    public override async Task<IList<Product>> GetCategoryFeaturedProductsAsync(
        int categoryId, int storeId = 0)
    {
        var products = await base.GetCategoryFeaturedProductsAsync(categoryId, storeId);
        var allocatedIds = await GetAllocatedProductIdsForCurrentCustomerAsync();
        if (allocatedIds == null) return products;
        var allocatedSet = allocatedIds.ToHashSet();
        return products.Where(p => allocatedSet.Contains(p.Id)).ToList();
    }

    public override async Task<IList<Product>> GetManufacturerFeaturedProductsAsync(
        int manufacturerId, int storeId = 0)
    {
        var products = await base.GetManufacturerFeaturedProductsAsync(manufacturerId, storeId);
        var allocatedIds = await GetAllocatedProductIdsForCurrentCustomerAsync();
        if (allocatedIds == null) return products;
        var allocatedSet = allocatedIds.ToHashSet();
        return products.Where(p => allocatedSet.Contains(p.Id)).ToList();
    }

    public override async Task<IList<Product>> GetAllProductsDisplayedOnHomepageAsync()
    {
        var products = await base.GetAllProductsDisplayedOnHomepageAsync();
        var allocatedIds = await GetAllocatedProductIdsForCurrentCustomerAsync();
        if (allocatedIds == null) return products;
        var allocatedSet = allocatedIds.ToHashSet();
        return products.Where(p => allocatedSet.Contains(p.Id)).ToList();
    }

    /// <summary>
    /// ⚠️ UPGRADE NOTE (NOP 5.00): The restricted-user branch is a copy of
    /// ProductService.GetProductsMarkedAsNewAsync with one CW addition (marked inline).
    /// When upgrading NOP, diff the base method and merge any changes.
    /// </summary>
    public override async Task<IPagedList<Product>> GetProductsMarkedAsNewAsync(int storeId = 0, int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var allocatedIds = await GetAllocatedProductIdsForCurrentCustomerAsync();

        // Fast path: not restricted — delegate to base unchanged
        if (allocatedIds == null)
            return await base.GetProductsMarkedAsNewAsync(storeId, pageIndex, pageSize);

        // Restriction applies but no products allocated — return empty
        if (!allocatedIds.Any())
            return new PagedList<Product>(new List<Product>(), pageIndex, Math.Max(pageSize, 1));

        // Restricted path: same logic as base with one WHERE clause added.
        var allocatedSet = allocatedIds.ToHashSet();

        var query = from p in _productRepository.Table
                    where p.Published && p.VisibleIndividually && p.MarkAsNew && !p.Deleted &&
                          DateTime.UtcNow >= (p.MarkAsNewStartDateTimeUtc ?? SqlDateTime.MinValue.Value) &&
                          DateTime.UtcNow <= (p.MarkAsNewEndDateTimeUtc ?? SqlDateTime.MaxValue.Value)
                    select p;

        //apply store mapping constraints
        query = await _storeMappingService.ApplyStoreMapping(query, storeId);

        //apply ACL constraints
        var customer = await _workContext.GetCurrentCustomerAsync();
        query = await _aclService.ApplyAcl(query, customer);

        // ── CW: restrict to allocated products at the SQL level ─────────────
        query = query.Where(p => allocatedSet.Contains(p.Id));
        // ── END CW ──────────────────────────────────────────────────────────

        query = query.OrderByDescending(p => p.CreatedOnUtc);

        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    #endregion
}
