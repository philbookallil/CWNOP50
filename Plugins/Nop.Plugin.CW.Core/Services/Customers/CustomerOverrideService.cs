using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tax;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Html;
using System.Collections.Concurrent;

namespace Nop.Plugin.CW.Core.Services.Customers;

public class CustomerOverrideService : CustomerService, ICustomerOverrideService
{
    #region Fields

    protected readonly IRepository<CustomerBranch> _customerBranchRepository;
    protected readonly IRepository<Branch> _branchRepository;
    protected readonly IRepository<Department> _departmentRepository;
    protected readonly IRepository<PackNameList> _packNameListRepository;
    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly ISettingService _settingService;
    //Lazy: the work context and the packname context both reach the customer service, so a direct
    //dependency on either would be a constructor cycle.
    protected readonly Lazy<IWorkContext> _workContext;
    protected readonly Lazy<IPackNameContext> _packNameContext;
    protected readonly Lazy<IPackNameCatalogGate> _catalogGate;

    //Concurrent, so the memo stays safe if a lookup is ever run beside another one in the same request.
    protected readonly ConcurrentDictionary<int, int[]> _defaultPackNameRoleIdsByStoreId = new();
    //Per logical call, not per instance: a field would hand a lookup running beside this one nop's answer
    //while this one is resolving. Static so the two registered instances of this service share it.
    private static readonly AsyncLocal<bool> _resolvingCatalogRoleIds = new();

    private static readonly PathString AdminPathSegment = new("/Admin");

    #endregion

    #region Ctor

    public CustomerOverrideService(CustomerSettings customerSettings,
        IGenericAttributeService genericAttributeService,
        IHtmlFormatter htmlFormatter,
        INopDataProvider dataProvider,
        IRepository<Address> customerAddressRepository,
        IRepository<BlogComment> blogCommentRepository,
        IRepository<Customer> customerRepository,
        IRepository<CustomerAddressMapping> customerAddressMappingRepository,
        IRepository<CustomerCustomerRoleMapping> customerCustomerRoleMappingRepository,
        IRepository<CustomerPassword> customerPasswordRepository,
        IRepository<CustomerRole> customerRoleRepository,
        IRepository<GenericAttribute> gaRepository,
        IRepository<Order> orderRepository,
        IRepository<PrivateMessage> privateMessageRepository,
        IRepository<ProductReview> productReviewRepository,
        IRepository<ProductReviewHelpfulness> productReviewHelpfulnessRepository,
        IRepository<ShoppingCartItem> shoppingCartRepository,
        IShortTermCacheManager shortTermCacheManager,
        IStaticCacheManager staticCacheManager,
        IStoreContext storeContext,
        ShoppingCartSettings shoppingCartSettings,
        TaxSettings taxSettings,
        IRepository<CustomerBranch> customerBranchRepository,
        IRepository<Branch> branchRepository,
        IRepository<Department> departmentRepository,
        IRepository<PackNameList> packNameListRepository,
        IHttpContextAccessor httpContextAccessor,
        ISettingService settingService,
        Lazy<IWorkContext> workContext,
        Lazy<IPackNameContext> packNameContext,
        Lazy<IPackNameCatalogGate> catalogGate) : base(customerSettings,
                                        genericAttributeService,
                                        htmlFormatter,
                                        dataProvider,
                                        customerAddressRepository,
                                        blogCommentRepository,
                                        customerRepository,
                                        customerAddressMappingRepository,
                                        customerCustomerRoleMappingRepository,
                                        customerPasswordRepository,
                                        customerRoleRepository,
                                        gaRepository,
                                        orderRepository,
                                        privateMessageRepository,
                                        productReviewRepository,
                                        productReviewHelpfulnessRepository,
                                        shoppingCartRepository,
                                        shortTermCacheManager,
                                        staticCacheManager,
                                        storeContext,
                                        shoppingCartSettings,
                                        taxSettings)
    {
        _customerBranchRepository = customerBranchRepository;
        _branchRepository = branchRepository;
        _departmentRepository = departmentRepository;
        _packNameListRepository = packNameListRepository;
        _httpContextAccessor = httpContextAccessor;
        _settingService = settingService;
        _workContext = workContext;
        _packNameContext = packNameContext;
        _catalogGate = catalogGate;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The packname the customer has selected in the public store, or null when the catalogue is nop's own
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the selected packname; null outside the packname context
    /// </returns>
    protected virtual async Task<PackName> GetSelectedPackNameAsync(Customer customer)
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        //No request is a background task, and the admin area works ON customers, not as them.
        if (request == null || request.Path.StartsWithSegments(AdminPathSegment, StringComparison.OrdinalIgnoreCase))
            return null;

        //The packname context belongs to the customer signed in. Asking it about anyone else would select a
        //packname for them and write it to their attributes.
        var currentCustomer = await _workContext.Value.GetCurrentCustomerAsync();
        if (currentCustomer == null || currentCustomer.Id != customer.Id)
            return null;

        //null when packnames are switched off for the store, for search engines and for a customer without one
        return await _packNameContext.Value.GetCurrentPackNameAsync(customer);
    }

    /// <summary>
    /// The packname's own role ids
    /// </summary>
    /// <remarks>
    /// A packname with an account has that account's roles. One without is seen as a newly registered
    /// packname customer: Registered plus the default packname roles of its store.
    /// </remarks>
    /// <param name="packName">PackName</param>
    /// <param name="showHidden">A value indicating whether to include inactive roles</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the role ids
    /// </returns>
    protected virtual async Task<int[]> GetPackNameRoleIdsAsync(PackName packName, bool showHidden)
    {
        if (packName.IsCustomer)
        {
            var packNameCustomer = await GetCustomerByIdAsync(packName.CustomerId.Value);

            if (packNameCustomer != null && !packNameCustomer.Deleted)
                return await base.GetCustomerRoleIdsAsync(packNameCustomer, showHidden);
        }

        var allRolesById = await GetAllCustomerRolesDictionaryAsync();
        var registeredRole = await GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.RegisteredRoleName);
        var defaultRoleIds = await GetDefaultPackNameRoleIdsAsync(packName.PackNameListId);

        return defaultRoleIds
            .Append(registeredRole?.Id ?? 0)
            .Where(id => allRolesById.TryGetValue(id, out var role) && (showHidden || role.Active))
            .ToArray();
    }

    /// <summary>
    /// The default packname role ids of the store the packname list belongs to
    /// </summary>
    /// <param name="packNameListId">PackName list identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the role ids
    /// </returns>
    protected virtual async Task<int[]> GetDefaultPackNameRoleIdsAsync(int packNameListId)
    {
        var packNameList = await _packNameListRepository.GetByIdAsync(packNameListId, cache => default);
        var storeId = packNameList?.StoreId ?? 0;

        //Read on every role lookup of the request, and settings are not cheap to bind, so once per store.
        if (!_defaultPackNameRoleIdsByStoreId.TryGetValue(storeId, out var roleIds))
        {
            var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);
            roleIds = storeSettings.DefaultPackNameCustomerRoleIds?.ToArray() ?? [];

            _defaultPackNameRoleIdsByStoreId[storeId] = roleIds;
        }

        return roleIds;
    }

    /// <summary>
    /// The role id of the packname's department, or null when it adds none
    /// </summary>
    /// <param name="departmentId">Department identifier</param>
    /// <param name="showHidden">A value indicating whether to include an inactive role</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the role id; null when the department is missing, deleted, inactive or has no role
    /// </returns>
    protected virtual async Task<int?> GetDepartmentRoleIdAsync(int? departmentId, bool showHidden)
    {
        if (!(departmentId > 0))
            return null;

        var department = await _departmentRepository.GetByIdAsync(departmentId, cache => default);

        if (department == null || department.Deleted || !department.Active || !(department.CustomerRoleId > 0))
            return null;

        var allRolesById = await GetAllCustomerRolesDictionaryAsync();

        return allRolesById.TryGetValue(department.CustomerRoleId.Value, out var role) && (showHidden || role.Active)
            ? role.Id
            : null;
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Gets all customer roles
    /// </summary>
    /// <param name="showHidden">A value indicating whether to show hidden records</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer roles
    /// </returns>
    public override async Task<IList<CustomerRole>> GetAllCustomerRolesAsync(bool showHidden = false)
    {
        var allRolesById = await GetAllCustomerRolesDictionaryAsync();
        var departmentCustomerRoleIds = await GetDepartmentCustomerRoleIdsAsync();

        return allRolesById.Values
            .Where(cr => (showHidden || cr.Active) && !departmentCustomerRoleIds.Contains(cr.Id))
            .ToList();
    }

    /// <summary>
    /// Get customer role identifiers
    /// </summary>
    /// <remarks>
    /// <para>
    /// While a customer orders for a packname in the public store, the catalogue follows the packname, not the
    /// person signed in: the packname's own roles plus its department's role. Every nop visibility consumer
    /// reads this method - ACL queries, the category, manufacturer, topic and tag lists, the menu, the sitemap -
    /// and keys its cache on the answer, so all of them follow the packname together.
    /// </para>
    /// <para>
    /// What the customer pays and may do still follows the customer signed in: those consumers read
    /// GetCustomerRolesAsync, which is not overridden, or GetSignedInCustomerRoleIdsAsync. nop's price caches
    /// are keyed on THIS method while computed from the signed-in roles, so the signed-in roles ride along as
    /// negative ids (see CustomerRoleSetHelper): they match nothing, but keep two customers who see the same
    /// packname roles apart in every cache key.
    /// </para>
    /// <para>
    /// What the packname's subsidies let it see is carried the same way: when they restrict it, one more id far
    /// below any role id stands for that restriction (PackNameCatalogGateHelper.RestrictionMarker), so cached
    /// category lists, menus and prices are not shared between packnames with the same roles and different
    /// allocations.
    /// </para>
    /// <para>
    /// Outside the packname context - the admin area, a background task, a customer other than the one signed
    /// in, packnames switched off for the store - the answer is nop's own. A cart line is checked against its
    /// own packname, whoever and wherever is asking, through PackNameCatalogScope.
    /// </para>
    /// </remarks>
    /// <param name="customer">Customer</param>
    /// <param name="showHidden">A value indicating whether to load hidden records</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer role identifiers
    /// </returns>
    public override async Task<int[]> GetCustomerRoleIdsAsync(Customer customer, bool showHidden = false)
    {
        var signedInRoleIds = await base.GetCustomerRoleIdsAsync(customer, showHidden);

        //Finding the packname goes through the work context and the packname context; anything they ask of
        //this method while it is being answered gets nop's answer rather than recursing.
        if (_resolvingCatalogRoleIds.Value)
            return signedInRoleIds;

        _resolvingCatalogRoleIds.Value = true;

        try
        {
            var packName = await GetCatalogPackNameAsync(customer);

            if (packName == null)
                return signedInRoleIds;

            var packNameRoleIds = await GetPackNameRoleIdsAsync(packName, showHidden);
            var departmentRoleId = await GetDepartmentRoleIdAsync(packName.DepartmentId, showHidden);

            var visibleProductIds = await _catalogGate.Value.GetVisibleProductIdsAsync(packName, PackNameCatalogScope.CurrentReplacement);

            return CustomerRoleSetHelper.ComposeCatalogRoleIds(packNameRoleIds, departmentRoleId, signedInRoleIds,
                PackNameCatalogGateHelper.RestrictionMarker(visibleProductIds));
        }
        finally
        {
            _resolvingCatalogRoleIds.Value = false;
        }
    }

    #endregion

    #region Customers

    /// <summary>
    /// Gets all customers
    /// </summary>
    /// <param name="createdFromUtc">Created date from (UTC); null to load all records</param>
    /// <param name="createdToUtc">Created date to (UTC); null to load all records</param>
    /// <param name="lastActivityFromUtc">Last activity date from (UTC); null to load all records</param>
    /// <param name="lastActivityToUtc">Last activity date to (UTC); null to load all records</param>
    /// <param name="affiliateId">Affiliate identifier</param>
    /// <param name="vendorId">Vendor identifier</param>
    /// <param name="customerRoleIds">A list of customer role identifiers to filter by (at least one match); pass null or empty list in order to load all customers; </param>
    /// <param name="email">Email; null to load all customers</param>
    /// <param name="username">Username; null to load all customers</param>
    /// <param name="firstName">First name; null to load all customers</param>
    /// <param name="lastName">Last name; null to load all customers</param>
    /// <param name="dayOfBirth">Day of birth; 0 to load all customers</param>
    /// <param name="monthOfBirth">Month of birth; 0 to load all customers</param>
    /// <param name="company">Company; null to load all customers</param>
    /// <param name="phone">Phone; null to load all customers</param>
    /// <param name="zipPostalCode">Phone; null to load all customers</param>
    /// <param name="ipAddress">IP address; null to load all customers</param>
    /// <param name="isActive">Customer is active; null to load all customers</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="getOnlyTotalCount">A value in indicating whether you want to load only total number of records. Set to "true" if you don't want to load data from database</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customers
    /// </returns>
    public virtual async Task<IPagedList<(Customer Customer, CustomerBranch CustomerBranch, string BranchName)>> GetAllCustomersDataAsync(
        DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
        DateTime? lastActivityFromUtc = null, DateTime? lastActivityToUtc = null,
        int affiliateId = 0, int vendorId = 0, int[] customerRoleIds = null,
        string email = null, string username = null, string firstName = null, string lastName = null,
        int dayOfBirth = 0, int monthOfBirth = 0,
        string company = null, string phone = null, string zipPostalCode = null, string ipAddress = null,
        bool? isActive = null, int storeId = 0, int pageIndex = 0, int pageSize = int.MaxValue, bool getOnlyTotalCount = false)
    {
        var query = _customerRepository.Table;

        if (createdFromUtc.HasValue)
            query = query.Where(c => createdFromUtc.Value <= c.CreatedOnUtc);
        if (createdToUtc.HasValue)
            query = query.Where(c => createdToUtc.Value >= c.CreatedOnUtc);
        if (lastActivityFromUtc.HasValue)
            query = query.Where(c => lastActivityFromUtc.Value <= c.LastActivityDateUtc);
        if (lastActivityToUtc.HasValue)
            query = query.Where(c => lastActivityToUtc.Value >= c.LastActivityDateUtc);
        if (affiliateId > 0)
            query = query.Where(c => affiliateId == c.AffiliateId);
        if (vendorId > 0)
            query = query.Where(c => vendorId == c.VendorId);
        if (isActive.HasValue)
            query = query.Where(c => c.Active == isActive.Value);

        query = query.Where(c => !c.Deleted);

        if (customerRoleIds != null && customerRoleIds.Length > 0)
        {
            query = query.Join(_customerCustomerRoleMappingRepository.Table, x => x.Id, y => y.CustomerId,
                    (x, y) => new { Customer = x, Mapping = y })
                .Where(z => customerRoleIds.Contains(z.Mapping.CustomerRoleId))
                .Select(z => z.Customer)
                .Distinct();
        }

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(c => c.Email.Contains(email));
        if (!string.IsNullOrWhiteSpace(username))
            query = query.Where(c => c.Username.Contains(username));
        if (!string.IsNullOrWhiteSpace(firstName))
            query = query.Where(c => c.FirstName.Contains(firstName));
        if (!string.IsNullOrWhiteSpace(lastName))
            query = query.Where(c => c.LastName.Contains(lastName));
        if (!string.IsNullOrWhiteSpace(company))
            query = query.Where(c => c.Company.Contains(company));
        if (!string.IsNullOrWhiteSpace(phone))
            query = query.Where(c => c.Phone.Contains(phone));
        if (!string.IsNullOrWhiteSpace(zipPostalCode))
            query = query.Where(c => c.ZipPostalCode.Contains(zipPostalCode));

        if (dayOfBirth > 0 && monthOfBirth > 0)
            query = query.Where(c => c.DateOfBirth.HasValue && c.DateOfBirth.Value.Day == dayOfBirth &&
                                     c.DateOfBirth.Value.Month == monthOfBirth);
        else if (dayOfBirth > 0)
            query = query.Where(c => c.DateOfBirth.HasValue && c.DateOfBirth.Value.Day == dayOfBirth);
        else if (monthOfBirth > 0)
            query = query.Where(c => c.DateOfBirth.HasValue && c.DateOfBirth.Value.Month == monthOfBirth);

        //search by IpAddress
        if (!string.IsNullOrWhiteSpace(ipAddress) && CommonHelper.IsValidIpAddress(ipAddress))
        {
            query = query.Where(w => w.LastIpAddress == ipAddress);
        }

        if (storeId > 0)
            query = query.Where(c => storeId == c.RegisteredInStoreId);

        // Join with CustomerBranch to get branch name in a single DB call
        var customerBranchQuery = from c in query
                                  join cb in _customerBranchRepository.Table on c.Id equals cb.Id into cbj
                                  from cb in cbj.DefaultIfEmpty()
                                  join b in _branchRepository.Table on cb.BranchId equals b.Id into bj
                                  from b in bj.DefaultIfEmpty()
                                  orderby c.CreatedOnUtc descending
                                  select new
                                  {
                                      Customer = c,
                                      CustomerBranch = cb,
                                      BranchName = b != null ? b.Name : string.Empty
                                  };

        var pagedList = await customerBranchQuery.ToPagedListAsync(pageIndex, pageSize, getOnlyTotalCount);

        var result = pagedList.Select(x => (x.Customer, x.CustomerBranch, x.BranchName)).ToList();

        return new PagedList<(Customer, CustomerBranch, string)>(result, pagedList.PageIndex, pagedList.PageSize, pagedList.TotalCount);
    }

    /// <summary>
    /// Returns the customer role name for a Department
    /// </summary>
    /// <param name="department">The department</param>
    /// <returns>The formatted customer role name for the department</returns>
    public string DepartmentCustomerRoleName(Department department) => $"{ExtendedCustomerRoleNames.DepartmentPrefix}{department.StoreId}_{department.Name}";

    /// <summary>
    /// Gets the role identifiers of the customer themselves, whatever packname they are ordering for
    /// </summary>
    /// <remarks>
    /// For what the customer pays and may use - tier prices, plugins limited to roles. GetCustomerRoleIdsAsync
    /// answers with the packname's roles in the packname context.
    /// </remarks>
    /// <param name="customer">Customer</param>
    /// <param name="showHidden">A value indicating whether to load hidden records</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer role identifiers
    /// </returns>
    public virtual async Task<int[]> GetSignedInCustomerRoleIdsAsync(Customer customer, bool showHidden = false)
    {
        return await base.GetCustomerRoleIdsAsync(customer, showHidden);
    }

    /// <summary>
    /// Gets the packname whose catalogue the customer is seeing
    /// </summary>
    /// <remarks>
    /// The packname of the open <see cref="PackNameCatalogScope"/> when there is one - a cart line, an exchange
    /// replacement - else the packname the customer has selected in the public store.
    /// </remarks>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packname; null outside the packname context
    /// </returns>
    public virtual async Task<PackName> GetCatalogPackNameAsync(Customer customer)
    {
        return PackNameCatalogScope.Current ?? await GetSelectedPackNameAsync(customer);
    }

    /// <summary>
    /// Asynchronously retrieves a set of unique customer role IDs associated with departments.
    /// </summary>
    /// <remarks>The result is cached to improve performance on subsequent calls. Only departments that are
    /// not marked as deleted and have a customer role assigned are included.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a set of unique customer role IDs
    /// for all departments. The set will be empty if no departments have associated customer roles.</returns>
    public virtual async Task<HashSet<int>> GetDepartmentCustomerRoleIdsAsync()
    {
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(ExtendedCustomerServiceDefaults.DepartmentCustomerRoleIdsCacheKey);
        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var query = from dep in _departmentRepository.Table
                        where dep.CustomerRoleId.HasValue && !dep.Deleted
                        select dep.CustomerRoleId.Value;

            var list = await query.Distinct().ToListAsync();
            return new HashSet<int>(list);
        });
    }

    /// <summary>
    /// Creates or updates the customer role associated with the specified department, ensuring the department has a
    /// corresponding customer role with the correct name.
    /// </summary>
    /// <remarks>If the department does not have an associated customer role, a new customer role is created
    /// and linked to the department. If a customer role already exists but its name does not match the expected value,
    /// the role is updated. This method is typically used to maintain consistency between departments and their
    /// customer roles.</remarks>
    /// <param name="department">The department for which to create or update the associated customer role. Cannot be null. The department's
    /// CustomerRoleId property will be set or updated as needed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task SaveDepartmentCustomerRole(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);

        var departmentCustomerRoleName = DepartmentCustomerRoleName(department);

        var customerRole = await GetCustomerRoleByIdAsync(department.CustomerRoleId ?? 0);

        if (customerRole == null)
        {
            //Create customer role for the department
            customerRole = new CustomerRole
            {
                Name = departmentCustomerRoleName,
                Active = true,
                IsSystemRole = false,
                SystemName = departmentCustomerRoleName,
            };

            await InsertCustomerRoleAsync(customerRole);

            department.CustomerRoleId = customerRole.Id;
        }
        else
        {
            if (departmentCustomerRoleName != customerRole.Name)
            {
                customerRole.Name = departmentCustomerRoleName;
                customerRole.SystemName = departmentCustomerRoleName;
                await UpdateCustomerRoleAsync(customerRole);
            }
        }
    }

    /// <summary>
    /// Gets customer for shopping cart
    /// </summary>
    /// <param name="shoppingCart">Shopping cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public virtual async Task<Customer> GetShoppingCartCustomerAsync(IList<PackNameShoppingCartItem> shoppingCart)
    {
        var customerId = shoppingCart.FirstOrDefault()?.CustomerId;

        return customerId.HasValue && customerId != 0 ? await GetCustomerByIdAsync(customerId.Value) : null;
    }

    #endregion

    #region Customer Branch

    /// <summary>
    /// Gets a customer
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains a customer
    /// </returns>
    public virtual async Task<CustomerBranch> GetCustomerBranchByIdAsync(int customerId)
    {
        return await _customerBranchRepository.GetByIdAsync(customerId, cache => default, useShortTermCache: true);
    }

    /// <summary>
    /// Insert a customer branch mapping
    /// </summary>
    /// <param name="customer">CustomerBranch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertCustomerBranchAsync(CustomerBranch customer)
    {
        await _customerBranchRepository.InsertAsync(customer);
    }

    /// <summary>
    /// Updates the customer branch mapping
    /// </summary>
    /// <param name="customer">CustomerBranch</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task UpdateCustomerBranchAsync(CustomerBranch customer)
    {
        await _customerBranchRepository.UpdateAsync(customer);
    }

    #endregion
}
