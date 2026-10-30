using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.BulkOrder;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using System.Data.SqlTypes;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

public class BulkOrderService : IBulkOrderService
{
    #region Fields

    protected readonly IAclService _aclService;
    protected readonly ICustomerService _customerService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly IOrderService _orderService;
    protected readonly IPackNameContext _packNameContext;
    protected readonly IRepository<Category> _categoryRepository;
    protected readonly IRepository<LocalizedProperty> _localizedPropertyRepository;
    protected readonly IRepository<Order> _orderRepository;
    protected readonly IRepository<OrderItem> _orderItemRepository;
    protected readonly IRepository<Product> _productRepository;
    protected readonly IRepository<ProductCategory> _productCategoryRepository;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IStoreMappingService _storeMappingService;
    protected readonly IPackNameCatalogGate _catalogGate;
    protected readonly IWorkContext _workContext;
    protected readonly ICategoryService _categoryService;
    protected readonly IShoppingCartOverrideService _shoppingCartService;

    #endregion

    #region Ctor

    public BulkOrderService(IAclService aclService,
        ICustomerService customerService,
        IDateTimeHelper dateTimeHelper,
        IOrderService orderService,
        IPackNameContext packNameContext,
        IRepository<Category> categoryRepository,
        IRepository<LocalizedProperty> localizedPropertyRepository,
        IRepository<Order> orderRepository,
        IRepository<OrderItem> orderItemRepository,
        IRepository<Product> productRepository,
        IRepository<ProductCategory> productCategoryRepository,
        IStaticCacheManager staticCacheManager,
        IStoreMappingService storeMappingService,
        IPackNameCatalogGate catalogGate,
        IWorkContext workContext,
        ICategoryService categoryService,
        IShoppingCartOverrideService shoppingCartService)
    {
        _aclService = aclService;
        _customerService = customerService;
        _dateTimeHelper = dateTimeHelper;
        _orderService = orderService;
        _packNameContext = packNameContext;
        _categoryRepository = categoryRepository;
        _localizedPropertyRepository = localizedPropertyRepository;
        _orderRepository = orderRepository;
        _orderItemRepository = orderItemRepository;
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _staticCacheManager = staticCacheManager;
        _storeMappingService = storeMappingService;
        _catalogGate = catalogGate;
        _workContext = workContext;
        _categoryService = categoryService;
        _shoppingCartService = shoppingCartService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The products the customer's catalogue packname may see, or null when unrestricted - the one rule the
    /// catalogue, the product page and add-to-cart use (IPackNameCatalogGate), so a packname whose ordering period
    /// is closed sees an empty grid here too.
    /// </summary>
    protected virtual async Task<IList<int>> GetAllocatedProductIdsAsync(Customer customer)
    {
        return await _catalogGate.GetVisibleProductIdsAsync(customer);
    }

    /// <summary>
    /// Applies every constraint the 4.9 catalogue applies: the base predicate, store mapping,
    /// customer-role ACL, and the packname subsidy allocation.
    /// </summary>
    protected virtual async Task<IQueryable<Product>> ApplyVisibilityAsync(IQueryable<Product> query, int storeId, Customer customer)
    {
        query = from p in query
                where !p.Deleted && p.Published && p.VisibleIndividually &&
                      DateTime.UtcNow >= (p.AvailableStartDateTimeUtc ?? SqlDateTime.MinValue.Value) &&
                      DateTime.UtcNow <= (p.AvailableEndDateTimeUtc ?? SqlDateTime.MaxValue.Value)
                select p;

        query = await _storeMappingService.ApplyStoreMapping(query, storeId);
        query = await _aclService.ApplyAcl(query, customer);

        var allocatedIds = await GetAllocatedProductIdsAsync(customer);
        if (allocatedIds != null)
            query = from p in query where allocatedIds.Contains(p.Id) select p;

        return query;
    }

    protected virtual async Task<IQueryable<Product>> ApplyCategoryAsync(IQueryable<Product> query, int categoryId, int storeId)
    {
        if (categoryId <= 0)
            return query;

        //the whole subtree, as nop does for a category page. The navigation renders an unbounded tree,
        //so 4.6's "category + direct children" silently hides third-level products.
        var categoryIds = new List<int> { categoryId };
        categoryIds.AddRange(await _categoryService.GetChildCategoryIdsAsync(categoryId, storeId));

        //Any() rather than join + Distinct: no SELECT DISTINCT across every Product column
        return from p in query
               where _productCategoryRepository.Table.Any(pc => pc.ProductId == p.Id && categoryIds.Contains(pc.CategoryId))
               select p;
    }

    protected virtual async Task<IQueryable<Product>> ApplySortingAsync(IQueryable<Product> query, BulkOrderSorting orderBy)
    {
        if (orderBy is BulkOrderSorting.NameAsc or BulkOrderSorting.NameDesc)
        {
            //sort on the localized name where one exists, falling back to Product.Name
            var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

            var named =
                from p in query
                join lp in _localizedPropertyRepository.Table
                    on new { p.Id, LanguageId = languageId, KeyGroup = nameof(Product), Key = nameof(Product.Name) }
                    equals new { Id = lp.EntityId, LanguageId = lp.LanguageId, KeyGroup = lp.LocaleKeyGroup, Key = lp.LocaleKey }
                    into lps
                from lp in lps.DefaultIfEmpty()
                select new { SortName = lp == null ? p.Name : lp.LocaleValue, Product = p };

            return orderBy == BulkOrderSorting.NameAsc
                ? from x in named orderby x.SortName select x.Product
                : from x in named orderby x.SortName descending select x.Product;
        }

        return orderBy switch
        {
            BulkOrderSorting.SkuAsc => query.OrderBy(p => p.Sku),
            BulkOrderSorting.SkuDesc => query.OrderByDescending(p => p.Sku),
            BulkOrderSorting.PriceAsc => query.OrderBy(p => p.Price),
            BulkOrderSorting.PriceDesc => query.OrderByDescending(p => p.Price),
            BulkOrderSorting.CreatedOn => query.OrderByDescending(p => p.CreatedOnUtc),
            _ => query.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id)
        };
    }

    /// <summary>The window for "recently ordered" — the last three months, in the customer's time zone.</summary>
    protected virtual async Task<DateTime> GetRecentOrderCutoffUtcAsync()
    {
        var nowUser = await _dateTimeHelper.ConvertToUserTimeAsync(DateTime.UtcNow, DateTimeKind.Utc);
        var timeZone = await _dateTimeHelper.GetCurrentTimeZoneAsync();

        return _dateTimeHelper.ConvertToUtcTime(nowUser.AddMonths(-3), timeZone);
    }

    /// <summary>
    /// Product ids of one past order - of THIS customer's past order.
    /// </summary>
    /// <remarks>
    /// The customer id is not decoration. OrderId arrives on the query string, so without this predicate
    /// "?FilterMode=LatestOrdered&amp;OrderId=12345" lists another customer's order contents, and the
    /// category tree beside it discloses what they bought. 4.6 had no such check.
    /// </remarks>
    protected virtual IQueryable<int> ProductIdsOfOrder(int orderId, int customerId)
    {
        return from oi in _orderItemRepository.Table
               join o in _orderRepository.Table on oi.OrderId equals o.Id
               where oi.OrderId == orderId && o.CustomerId == customerId && !o.Deleted &&
                     CountedOrderStatusIds.Contains(o.OrderStatusId)
               select oi.ProductId;
    }

    protected virtual IQueryable<int> ProductIdsOfRecentOrders(int customerId, int storeId, DateTime cutoffUtc)
    {
        return from oi in _orderItemRepository.Table
               join o in _orderRepository.Table on oi.OrderId equals o.Id
               where o.CustomerId == customerId && !o.Deleted &&
                     CountedOrderStatusIds.Contains(o.OrderStatusId) &&
                     (storeId == 0 || o.StoreId == storeId) &&
                     o.CreatedOnUtc >= cutoffUtc
               select oi.ProductId;
    }

    /// <summary>
    /// Order items that count towards "sold": the single definition the best-sellers list and the
    /// best-sellers category walk both build on, so a change to the rule cannot reach only one of them.
    /// </summary>
    protected virtual IQueryable<OrderItem> SoldOrderItems(int storeId)
    {
        return from oi in _orderItemRepository.Table
               join o in _orderRepository.Table on oi.OrderId equals o.Id
               where !o.Deleted && CountedOrderStatusIds.Contains(o.OrderStatusId) &&
                     (storeId == 0 || o.StoreId == storeId)
               select oi;
    }

    protected virtual async Task<IQueryable<Product>> ApplyLatestOrderedAsync(IQueryable<Product> products, Customer customer, int storeId, int orderId)
    {
        var productIds = orderId > 0
            ? ProductIdsOfOrder(orderId, customer.Id)
            : ProductIdsOfRecentOrders(customer.Id, storeId, await GetRecentOrderCutoffUtcAsync());

        return from p in products where productIds.Contains(p.Id) select p;
    }

    protected virtual IQueryable<Product> ApplyBestSellersExists(IQueryable<Product> products, int storeId)
    {
        var sold = SoldOrderItems(storeId).Select(oi => oi.ProductId);

        return from p in products where sold.Contains(p.Id) select p;
    }

    /// <summary>
    /// Order statuses that count as a real order.
    /// </summary>
    /// <remarks>
    /// A cancelled order is not a purchase: it must not make a product a best seller, put it in
    /// "recently ordered", or be offered in the order picker. nop's own BestSellersReportAsync defaults
    /// to every status.
    /// </remarks>
    protected static readonly int[] CountedOrderStatusIds =
    [
        (int)OrderStatus.Pending,
        (int)OrderStatus.Processing,
        (int)OrderStatus.Complete
    ];

    #endregion

    #region Methods

    public virtual async Task<IPagedList<Product>> SearchProductsAsync(BulkOrderQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var customer = await _workContext.GetCurrentCustomerAsync();

        var products = await ApplyVisibilityAsync(_productRepository.Table, query.StoreId, customer);
        products = await ApplyCategoryAsync(products, query.CategoryId, query.StoreId);

        switch (query.FilterType)
        {
            case BulkOrderFilterType.LatestOrdered:
                {
                    var productIds = query.OrderId > 0
                        ? ProductIdsOfOrder(query.OrderId, customer.Id)
                        : ProductIdsOfRecentOrders(customer.Id, query.StoreId, await GetRecentOrderCutoffUtcAsync());

                    products = from p in products where productIds.Contains(p.Id) select p;
                    products = await ApplySortingAsync(products, query.OrderBy);
                    break;
                }

            case BulkOrderFilterType.BestSellers:
                {
                    var report =
                        from oi in SoldOrderItems(query.StoreId)
                        group oi by oi.ProductId into g
                        select new { ProductId = g.Key, TotalQuantity = g.Sum(x => x.Quantity) };

                    //an explicit sort choice wins; Position keeps best-seller order
                    if (query.OrderBy == BulkOrderSorting.Position)
                    {
                        products = from r in report
                                   join p in products on r.ProductId equals p.Id
                                   orderby r.TotalQuantity descending, p.Id
                                   select p;
                    }
                    else
                    {
                        products = from r in report join p in products on r.ProductId equals p.Id select p;
                        products = await ApplySortingAsync(products, query.OrderBy);
                    }

                    break;
                }

            default:
                products = await ApplySortingAsync(products, query.OrderBy);
                break;
        }

        return await products.ToPagedListAsync(query.PageIndex, query.PageSize);
    }

    public virtual async Task<IList<int>> GetVisibleCategoryIdsAsync(BulkOrderFilterType filterType, int storeId, int orderId)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var customerRoleIds = await _customerService.GetCustomerRoleIdsAsync(customer);

        //The customer affects the answer whenever the filter reads orders - a selected order is read
        //through ProductIdsOfOrder(orderId, customerId), so it is customer-specific too. Leaving the
        //customer id out when orderId > 0 lets any two customers with the same roles share one entry,
        //and whoever loads it first decides what the other sees.
        var customerId = filterType == BulkOrderFilterType.LatestOrdered ? customer.Id : 0;

        //only that filter reads the order: any other keeps one entry whatever order the address names
        if (filterType != BulkOrderFilterType.LatestOrdered)
            orderId = 0;

        //two packnames with the same roles but different allocations must not share a cached tree
        var allocatedIds = await GetAllocatedProductIdsAsync(customer);

        //deterministic across processes and servers - System.HashCode is seeded per process, so with a
        //distributed cache two web servers would key the same allocation differently. Depends on the whole
        //set, so swapping one id inside it changes the key (count+min+max did not).
        //null (unrestricted) = 0; an empty allocation = 17, which is deliberately not the same thing.
        var allocationFingerprint = allocatedIds == null
            ? 0
            : allocatedIds.OrderBy(id => id).Aggregate(17, (hash, id) => unchecked(hash * 31 + id));

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(
            BulkOrderServicesDefaults.VisibleCategoryIdsCacheKey,
            filterType, storeId, customerRoleIds, orderId, customerId, allocationFingerprint);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var products = await ApplyVisibilityAsync(_productRepository.Table, storeId, customer);

            products = filterType switch
            {
                BulkOrderFilterType.LatestOrdered => await ApplyLatestOrderedAsync(products, customer, storeId, orderId),
                BulkOrderFilterType.BestSellers => ApplyBestSellersExists(products, storeId),
                _ => products
            };

            var categoryIds = await (from p in products
                                     join pc in _productCategoryRepository.Table on p.Id equals pc.ProductId
                                     select pc.CategoryId).Distinct().ToListAsync();

            //walk up to the roots so an ancestor with no direct products still renders
            var allCategories = await _categoryRepository.GetAllAsync(q => q, cache => default);
            var result = new HashSet<int>(categoryIds);
            var toProcess = new List<int>(categoryIds);

            while (toProcess.Count > 0)
            {
                var parentIds = allCategories
                    .Where(c => toProcess.Contains(c.Id) && c.ParentCategoryId > 0 && !result.Contains(c.ParentCategoryId))
                    .Select(c => c.ParentCategoryId)
                    .Distinct()
                    .ToList();

                foreach (var id in parentIds)
                    result.Add(id);

                toProcess = parentIds;
            }

            return result.ToList();
        });
    }

    public virtual async Task<IList<Order>> GetCustomerOrdersAsync(int storeId, int maxOrders = 0)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        //SearchOrdersAsync defaults to pageSize: int.MaxValue and orders newest first. A customer with
        //years of history would otherwise load every order they ever placed, on every page view, to fill
        //a dropdown - so the cap is applied in the query, not with a .Take() on the result
        return await _orderService.SearchOrdersAsync(storeId: storeId, customerId: customer.Id,
            osIds: CountedOrderStatusIds.ToList(),
            pageSize: maxOrders > 0 ? maxOrders : int.MaxValue);
    }

    public virtual async Task<Product> GetVisibleProductByIdAsync(int productId, int storeId)
    {
        if (productId <= 0)
            return null;

        var customer = await _workContext.GetCurrentCustomerAsync();
        var products = await ApplyVisibilityAsync(_productRepository.Table, storeId, customer);

        return await products.FirstOrDefaultAsync(product => product.Id == productId);
    }

    public virtual async Task<IDictionary<int, int>> GetCartQuantitiesAsync(Customer customer, int storeId)
    {
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        var cart = packName == null || packName.Id == 0
            ? await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, storeId)
            : await _shoppingCartService.GetPackNameCartAsync(customer, packName.Id, storeId);

        return cart
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
    }

    #endregion
}