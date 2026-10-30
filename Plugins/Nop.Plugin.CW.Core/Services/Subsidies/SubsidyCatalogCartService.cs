using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Localization;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Security;
using Nop.Services.Stores;
using System.Data.SqlTypes;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

public class SubsidyCatalogCartService : ISubsidyCatalogCartService
{
    #region Fields

    protected readonly IWorkContext _workContext;
    protected readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly IRepository<Product> _productRepository;
    protected readonly IStoreMappingService _storeMappingService;
    protected readonly IAclService _aclService;
    private readonly IRepository<LocalizedProperty> _localizedPropertyRepository;
    protected readonly IRepository<ProductSpecificationAttribute> _productSpecificationAttributeRepository;
    protected readonly IOrderTotalCalculationOverrideService _orderTotalCalculationService;

    #endregion

    #region Ctor

    public SubsidyCatalogCartService(IWorkContext workContext,
        IShoppingCartOverrideService shoppingCartService,
        IRepository<Product> productRepository,
        IStoreMappingService storeMappingService,
        IAclService aclService,
        IRepository<LocalizedProperty> localizedPropertyRepository,
        IRepository<ProductSpecificationAttribute> productSpecificationAttributeRepository,
        IOrderTotalCalculationOverrideService orderTotalCalculationService)
    {
        _workContext = workContext;
        _shoppingCartService = shoppingCartService;
        _productRepository = productRepository;
        _storeMappingService = storeMappingService;
        _aclService = aclService;
        _localizedPropertyRepository = localizedPropertyRepository;
        _productSpecificationAttributeRepository = productSpecificationAttributeRepository;
        _orderTotalCalculationService = orderTotalCalculationService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Per-product-group and global subsidy usage for the packname's current cart items.
    /// </summary>
    /// <remarks>
    /// Projects the settlement pipeline's own allocation — it does NOT simulate consumption itself.
    /// It used to, and the two disagreed: the simulation had no credit-note budget, so it showed a unit
    /// being consumed on a cart where settlement spent global credit instead.
    /// </remarks>
    public async Task<IList<SubsidyCartUsageItem>> GetPackNameSubsidyCartUsageAsync(int packNameId, int storeId)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        if (customer == null)
            return [];

        // The WHOLE cart, not just this packname's slice: the credit-note budget is shared across every
        // packname in the cart, so allocating one packname in isolation would hand it the full budget.
        var packNameCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, storeId);

        if (packNameCart.Count == 0)
            return [];

        var allocation = await _orderTotalCalculationService.GetCartSubsidyBreakdownAsync(packNameCart);

        if (!allocation.Items.Any())
            return [];

        // SubsidyOrderItem.Id carries the ShoppingCartItemId, so this is how a row is traced back to a
        // packname. Keep only the rows belonging to the packname this bar is drawn for.
        var cartItemIds = packNameCart
            .Where(x => x.PackNameId == packNameId)
            .Select(x => x.Id)
            .ToHashSet();

        return allocation.Items
            .Where(item => cartItemIds.Contains(item.Id))
            .Select(item => new SubsidyCartUsageItem
            {
                ProductGroupId = item.ProductGroupId,
                Quantity = item.Quantity,
                QuantityTopup = item.QuantityTopup,
                ProductGroupCredit = item.ProductGroupCredit,
                ProductGroupCreditTopup = item.ProductGroupCreditTopup,
                Credit = item.Credit,
                CreditTopup = item.CreditTopup,
            })
            .ToList();
    }

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
    public virtual async Task<IPagedList<Product>> SearchProductsAsync(
        int pageIndex = 0,
        int pageSize = int.MaxValue,
        IList<int> productIds = null,
        int storeId = 0,
        bool visibleIndividuallyOnly = false,
        bool excludeFeaturedProducts = false,
        IList<SpecificationAttributeOption> filteredSpecOptions = null,
        ProductSortingEnum orderBy = ProductSortingEnum.Position,
        bool showHidden = false)
    {
        //some databases don't support int.MaxValue
        if (pageSize == int.MaxValue)
            pageSize = int.MaxValue - 1;

        var productsQuery = _productRepository.Table;

        if (!showHidden)
            productsQuery = productsQuery.Where(p => p.Published);

        var customer = await _workContext.GetCurrentCustomerAsync();

        if (!showHidden || storeId > 0)
        {
            //apply store mapping constraints
            productsQuery = await _storeMappingService.ApplyStoreMapping(productsQuery, storeId);
        }

        if (!showHidden)
        {
            //apply ACL constraints
            productsQuery = await _aclService.ApplyAcl(productsQuery, customer);
        }

        productsQuery =
            from p in productsQuery
            where !p.Deleted &&
                  (!visibleIndividuallyOnly || p.VisibleIndividually) &&
                  (showHidden ||
                   DateTime.UtcNow >= (p.AvailableStartDateTimeUtc ?? SqlDateTime.MinValue.Value) &&
                   DateTime.UtcNow <= (p.AvailableEndDateTimeUtc ?? SqlDateTime.MaxValue.Value)
                  )
            select p;

        if (productIds?.Count > 0)
        {
            productsQuery =
                from p in productsQuery
                where productIds.Contains(p.Id)
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
                    where psa.AllowFiltering && optionIdsBySpecificationAttribute.Contains(psa.SpecificationAttributeOptionId)
                    select psa;

                productsQuery =
                    from p in productsQuery
                    where productSpecificationQuery.Any(pc => pc.ProductId == p.Id)
                    select p;
            }
        }

        return await productsQuery
            .OrderBy(orderBy, _localizedPropertyRepository, (await _workContext.GetWorkingLanguageAsync()).Id)
            .ToPagedListAsync(pageIndex, pageSize);
    }

    #endregion
}
