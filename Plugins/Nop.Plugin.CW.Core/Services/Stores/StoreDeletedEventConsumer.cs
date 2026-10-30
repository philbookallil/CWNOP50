using Nop.Core.Domain.Stores;
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Events;

namespace Nop.Plugin.CW.Core.Services.Stores;

/// <summary>
/// Soft-deletes everything this plugin keeps for a store when nop deletes the store.
/// </summary>
/// <remarks>
/// <para>
/// nop removes the store row and nothing else, which left the store's packname lists, packnames, branches,
/// departments, cost centres, subsidies and product groups pointing at a store that no longer exists. They
/// are soft-deleted, never removed, so their history stays readable: the orders placed for those packnames,
/// the top-ups and variations, the subsidy consumption.
/// </para>
/// <para>
/// Each table is read directly (every non-deleted row of the store, active or not) and each row is deleted
/// through its own service, so the deleted events those services publish still run their existing
/// handlers: a packname list's packnames are soft-deleted with it, a packname's subsidy assignments with
/// the packname, a subsidy's assignments and product-group allocations with the subsidy, and a product
/// group's allocations with the group.
/// </para>
/// <para>
/// Credit notes, return cases, the exchange and return rules and impersonation tokens also carry a store id
/// but have no soft-delete flag. They are left exactly as they are: the first two are the history this
/// exists to protect, and the rest are configuration and short-lived tokens for a store nobody can reach.
/// </para>
/// </remarks>
public class StoreDeletedEventConsumer : IConsumer<EntityDeletedEvent<Store>>
{
    #region Fields

    protected readonly IRepository<Branch> _branchRepository;
    protected readonly IRepository<CostCenter> _costCenterRepository;
    protected readonly IRepository<Department> _departmentRepository;
    protected readonly IRepository<PackNameList> _packNameListRepository;
    protected readonly IRepository<ProductGroup> _productGroupRepository;
    protected readonly IRepository<Subsidy> _subsidyRepository;
    protected readonly IBranchService _branchService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly IPackNameService _packNameService;
    protected readonly ISubsidyService _subsidyService;

    #endregion

    #region Ctor

    public StoreDeletedEventConsumer(IRepository<Branch> branchRepository,
        IRepository<CostCenter> costCenterRepository,
        IRepository<Department> departmentRepository,
        IRepository<PackNameList> packNameListRepository,
        IRepository<ProductGroup> productGroupRepository,
        IRepository<Subsidy> subsidyRepository,
        IBranchService branchService,
        IMultiStoreService multiStoreService,
        IPackNameService packNameService,
        ISubsidyService subsidyService)
    {
        _branchRepository = branchRepository;
        _costCenterRepository = costCenterRepository;
        _departmentRepository = departmentRepository;
        _packNameListRepository = packNameListRepository;
        _productGroupRepository = productGroupRepository;
        _subsidyRepository = subsidyRepository;
        _branchService = branchService;
        _multiStoreService = multiStoreService;
        _packNameService = packNameService;
        _subsidyService = subsidyService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handle the store deleted event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityDeletedEvent<Store> eventMessage)
    {
        if (eventMessage.Entity is null)
            return;

        var storeId = eventMessage.Entity.Id;

        //packname lists first: each takes its packnames with it, and each packname its subsidy assignments
        var packNameLists = await _packNameListRepository.GetAllAsync(q => q.Where(l => l.StoreId == storeId), includeDeleted: false);
        foreach (var packNameList in packNameLists)
            await _packNameService.DeletePackNameListAsync(packNameList);

        var subsidies = await _subsidyRepository.GetAllAsync(q => q.Where(s => s.StoreId == storeId), includeDeleted: false);
        foreach (var subsidy in subsidies)
            await _subsidyService.DeleteSubsidyAsync(subsidy);

        var productGroups = await _productGroupRepository.GetAllAsync(q => q.Where(pg => pg.StoreId == storeId), includeDeleted: false);
        foreach (var productGroup in productGroups)
            await _subsidyService.DeleteProductGroupAsync(productGroup);

        var branches = await _branchRepository.GetAllAsync(q => q.Where(b => b.StoreId == storeId), includeDeleted: false);
        foreach (var branch in branches)
            await _branchService.DeleteBranchAsync(branch);

        var departments = await _departmentRepository.GetAllAsync(q => q.Where(d => d.StoreId == storeId), includeDeleted: false);
        foreach (var department in departments)
            await _multiStoreService.DeleteDepartmentAsync(department);

        var costCenters = await _costCenterRepository.GetAllAsync(q => q.Where(c => c.StoreId == storeId), includeDeleted: false);
        foreach (var costCenter in costCenters)
            await _multiStoreService.DeleteCostCenterAsync(costCenter);
    }

    #endregion
}