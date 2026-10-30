using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Security;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Security;

public class AclOverrideService : AclService, IAclOverrideService
{
    #region Fields

    protected new readonly ICustomerOverrideService _customerService;
    protected readonly IRepository<Department> _departmentRepository;
    protected readonly IStoreMappingService _storeMappingService;
    //Lazy: the gate resolves the customer's packname through the customer service, which this service is built on.
    protected readonly Lazy<IPackNameCatalogGate> _catalogGate;

    #endregion

    #region Ctor

    public AclOverrideService(CatalogSettings catalogSettings,
        ICustomerOverrideService customerService,
        INopDataProvider dataProvider,
        IRepository<AclRecord> aclRecordRepository,
        IStaticCacheManager staticCacheManager,
        Lazy<IWorkContext> workContext,
        IRepository<Department> departmentRepository,
        IStoreMappingService storeMappingService,
        Lazy<IPackNameCatalogGate> catalogGate) : base(catalogSettings,
                                                customerService,
                                                dataProvider,
                                                aclRecordRepository,
                                                staticCacheManager,
                                                workContext)
    {
        _customerService = customerService;
        _departmentRepository = departmentRepository;
        _storeMappingService = storeMappingService;
        _catalogGate = catalogGate;
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Apply ACL to the passed query
    /// </summary>
    /// <remarks>
    /// Products are also narrowed to those the packname's subsidies let it see (IPackNameCatalogGate), so a grouped
    /// product's page lists only the associated products it may add; categories and manufacturers to those holding
    /// such a product, so a restricted packname is not offered a category that would open empty. The role ids
    /// passed are the current customer's - every catalogue caller builds them that way - and they carry the
    /// restriction's marker, so the cached lists built from this query are keyed by it.
    /// </remarks>
    /// <typeparam name="TEntity">Type of entity that supports the ACL</typeparam>
    /// <param name="query">Query to filter</param>
    /// <param name="customerRoleIds">Identifiers of customer's roles</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the filtered query
    /// </returns>
    public override async Task<IQueryable<TEntity>> ApplyAcl<TEntity>(IQueryable<TEntity> query, int[] customerRoleIds)
    {
        query = await base.ApplyAcl(query, customerRoleIds);

        ISet<int> visibleIds;

        var customer = await _workContext.Value.GetCurrentCustomerAsync();

        if (typeof(TEntity) == typeof(Product))
            visibleIds = (await _catalogGate.Value.GetVisibleProductIdsAsync(customer))?.ToHashSet();
        else if (typeof(TEntity) == typeof(Category))
            visibleIds = await _catalogGate.Value.GetVisibleCategoryIdsAsync(customer);
        else if (typeof(TEntity) == typeof(Manufacturer))
            visibleIds = await _catalogGate.Value.GetVisibleManufacturerIdsAsync(customer);
        else
            return query;

        if (visibleIds == null)
            return query;

        var ids = visibleIds.ToArray();

        return query.Where(entity => ids.Contains(entity.Id));
    }

    /// <summary>
    /// Authorize ACL permission
    /// </summary>
    /// <remarks>
    /// A product, category or manufacturer the packname's subsidies do not let it see is refused like one its roles
    /// do not reach. This is the one check behind the product, category and manufacturer pages, the related,
    /// cross-sell, recently viewed and also-purchased blocks, the menu items and add-to-cart, so all of them follow
    /// the restriction the product lists apply. Checked after the roles, and for entities not subject to ACL too,
    /// which nop lets through before ever reaching the role check.
    /// </remarks>
    /// <typeparam name="TEntity">Type of entity that supports the ACL</typeparam>
    /// <param name="entity">Entity</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true - authorized; otherwise, false
    /// </returns>
    public override async Task<bool> AuthorizeAsync<TEntity>(TEntity entity, Customer customer)
    {
        if (!await base.AuthorizeAsync(entity, customer))
            return false;

        return entity switch
        {
            Product => await _catalogGate.Value.GetVisibleProductIdsAsync(customer) is not { } productIds || productIds.Contains(entity.Id),
            Category => await _catalogGate.Value.GetVisibleCategoryIdsAsync(customer) is not { } categoryIds || categoryIds.Contains(entity.Id),
            Manufacturer => await _catalogGate.Value.GetVisibleManufacturerIdsAsync(customer) is not { } manufacturerIds || manufacturerIds.Contains(entity.Id),
            _ => true
        };
    }

    /// <summary>
    /// Authorize ACL permission
    /// </summary>
    /// <remarks>
    /// nop answers this one from the customer's role objects - the only ACL check that does not read
    /// GetCustomerRoleIdsAsync. Reading the ids keeps the product, category, manufacturer and topic pages in step
    /// with the lists that link to them: a packname shown a product must also be let onto its page, and a
    /// product hidden from it must not open from a bookmarked link.
    /// </remarks>
    /// <param name="entityTypeName">Type name of entity that supports the ACL</param>
    /// <param name="entityId">Entity ID</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true - authorized; otherwise, false
    /// </returns>
    public override async Task<bool> AuthorizeAsync(string entityTypeName, int entityId, Customer customer)
    {
        if (string.IsNullOrEmpty(entityTypeName))
            return false;

        if (entityId <= 0)
            return false;

        if (customer == null)
            return false;

        if (_catalogSettings.IgnoreAcl)
            return true;

        var customerRoleIds = await _customerService.GetCustomerRoleIdsAsync(customer);

        return (await GetCustomerRoleIdsWithAccessAsync(entityId, entityTypeName)).Intersect(customerRoleIds).Any();
    }

    /// <summary>
    /// Save ACL mapping
    /// </summary>
    /// <typeparam name="TEntity">Type of entity</typeparam>
    /// <param name="entity">Entity</param>
    /// <param name="selectedCustomerRoleIds">Customer roles for mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task SaveAclAsync<TEntity>(TEntity entity, IList<int> selectedCustomerRoleIds)
    {
        if (entity == null)
            return;

        //departments are mapped using customer roles, so we need to check the customer roles from departments
        var departmentCustomerRoleIds = await _customerService.GetDepartmentCustomerRoleIdsAsync();

        var existingAclRecords = await GetAclRecordsAsync(entity);
        var existingDepartmentCustomerRoleIds = existingAclRecords.Where(acl => departmentCustomerRoleIds.Contains(acl.CustomerRoleId)).ToList();

        // Determine if entity should be subject to ACL
        var subjectToAcl = selectedCustomerRoleIds.Any() || existingDepartmentCustomerRoleIds.Any();

        if (entity.SubjectToAcl != subjectToAcl)
        {
            entity.SubjectToAcl = subjectToAcl;
            await _dataProvider.UpdateEntityAsync(entity);
        }

        var allCustomerRoles = await _customerService.GetAllCustomerRolesAsync(true);
        foreach (var customerRole in allCustomerRoles)
            if (selectedCustomerRoleIds.Contains(customerRole.Id))
            {
                //new role
                if (existingAclRecords.All(acl => acl.CustomerRoleId != customerRole.Id))
                    await InsertAclRecordAsync(entity, customerRole.Id);
            }
            else
            {
                //remove role
                var aclRecordToDelete = existingAclRecords.FirstOrDefault(acl => acl.CustomerRoleId == customerRole.Id);
                if (aclRecordToDelete != null)
                    await DeleteAclRecordAsync(aclRecordToDelete);
            }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Saves the department ACL for an entity.
    /// </summary>
    /// <typeparam name="TEntity">Type of entity</typeparam>
    /// <param name="entity">Entity</param>
    /// <param name="selectedDepartmentIds">Departments selected on the form</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    /// <remarks>
    /// <para>
    /// <b>The posted ids are filtered against the ENTITY'S stores, not trusted.</b> They arrive from
    /// <c>request.Form["SelectedDepartmentIds"]</c> (<c>EventConsumer.HandleEntityEventAsync</c>), so
    /// there is no model here and rule 3 applies: the store comes from the entity. A department belongs
    /// to one store and its customer role is minted for that store, so applying another store's
    /// department role restricts the entity to a role nobody in its own store holds - the product
    /// silently disappears from its own store's catalogue, with nothing on the form to say why, because
    /// the picker showed two identically-named options.
    /// </para>
    /// <para>
    /// <b>An entity with no store mappings is unrestricted</b>, so there is no store to filter against
    /// and every department is permissible. For those the protection is the store label on the
    /// picker, not this filter. Soft-deleted departments are excluded in both cases.
    /// </para>
    /// </remarks>
    public virtual async Task SaveAclDepartmentAsync<TEntity>(TEntity entity, IList<int> selectedDepartmentIds) where TEntity : BaseEntity, IAclSupported, IStoreMappingSupported
    {
        if (entity == null)
            return;

        selectedDepartmentIds ??= new List<int>();

        //Empty means "not limited to stores" - visible everywhere, so no store constrains the choice.
        var entityStoreIds = await _storeMappingService.GetStoresIdsWithAccessAsync(entity);

        // Get existing ACL records for the entity.
        // MOVED ABOVE THE FILTER BELOW, which needs it: an inactive department that ALREADY restricts
        // this entity has to survive the save. Dropping it would delete a live ACL record and silently
        // widen who can see the entity, from a POST that was not trying to change that department at all.
        var existingAclRecords = await GetAclRecordsAsync(entity);

        var candidateDepartments = await _departmentRepository.Table
            .Where(dep => selectedDepartmentIds.Contains(dep.Id)
                          && !dep.Deleted
                          && (entityStoreIds.Length == 0 || entityStoreIds.Contains(dep.StoreId)))
            .Select(dep => new { dep.Id, dep.Active, dep.CustomerRoleId })
            .ToListAsync();

        //ACTIVE, ON THE WRITE. Active means "no new assignments". The picker no longer offers
        //inactive departments; this is the half that makes it a rule
        //rather than a suggestion - rule 5, scoped on read means scoped on write. The Active test is
        //done in memory because the keep depends on existingAclRecords, which is not queryable.
        var permittedDepartmentIds = candidateDepartments
            .Where(dep => dep.Active
                          || (dep.CustomerRoleId.HasValue
                              && existingAclRecords.Any(acl => acl.CustomerRoleId == dep.CustomerRoleId.Value)))
            .Select(dep => dep.Id)
            .ToList();

        //departments are mapped using customer roles, so we need to check the customer roles from departments
        var departmentCustomerRoleIds = await _customerService.GetDepartmentCustomerRoleIdsAsync();
        // Get customer role IDs not related to departments
        var existingCustomerRoleIds = existingAclRecords.Where(acl => !departmentCustomerRoleIds.Contains(acl.CustomerRoleId)).ToList();

        // Determine if entity should be subject to ACL
        var subjectToAcl = permittedDepartmentIds.Any() || existingCustomerRoleIds.Any();

        if (entity.SubjectToAcl != subjectToAcl)
        {
            entity.SubjectToAcl = subjectToAcl;
            await _dataProvider.UpdateEntityAsync(entity);
        }

        // Get selected department customer role IDs
        var selectedDepartmentCustomerRoleIds = await _departmentRepository.Table
            .Where(dep => permittedDepartmentIds.Contains(dep.Id))
            .Select(dep => dep.CustomerRoleId)
            .ToListAsync();

        var selectedDepartmentCustomerRoleIdsSet = selectedDepartmentCustomerRoleIds.ToHashSet();

        foreach (var departmentCustomerRoleId in departmentCustomerRoleIds)
            if (selectedDepartmentCustomerRoleIdsSet.Contains(departmentCustomerRoleId))
            {
                //new department role
                if (!existingAclRecords.Any(acl => acl.CustomerRoleId == departmentCustomerRoleId))
                    await InsertAclRecordAsync(entity, departmentCustomerRoleId);
            }
            else
            {
                //remove department role
                var aclRecordToDelete = existingAclRecords.FirstOrDefault(acl => acl.CustomerRoleId == departmentCustomerRoleId);
                if (aclRecordToDelete != null)
                    await DeleteAclRecordAsync(aclRecordToDelete);
            }
    }

    #endregion
}
