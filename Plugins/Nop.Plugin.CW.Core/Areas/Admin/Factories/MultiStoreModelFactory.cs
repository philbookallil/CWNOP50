using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class MultiStoreModelFactory : IMultiStoreModelFactory
{
    #region Fields

    protected readonly IMultiStoreService _multiStoreService;
    protected readonly IBranchService _branchService;
    protected readonly ICWSettingModelFactory _cwSettingModelFactory;
    protected readonly ICWAddressModelFactory _cwAddressModelFactory;

    #endregion

    #region Ctor

    public MultiStoreModelFactory(IMultiStoreService multiStoreService,
        IBranchService branchService,
        ICWSettingModelFactory cwSettingModelFactory,
        ICWAddressModelFactory cwAddressModelFactory)
    {
        _multiStoreService = multiStoreService;
        _branchService = branchService;
        _cwSettingModelFactory = cwSettingModelFactory;
        _cwAddressModelFactory = cwAddressModelFactory;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Prepare departments search model
    /// </summary>
    /// <param name="searchModel">MultiStore department search model</param>
    /// <param name="departmentId">Department identifier</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>MultiStore department search model</returns>
    protected virtual async Task<DepartmentManagerSearchModel> PrepareDepartmentManagerSearchModelAsync(DepartmentManagerSearchModel searchModel, int departmentId, int storeId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.DepartmentId = departmentId;

        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(searchModel.MultiStoreSettingsModel, storeId);

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    #endregion

    #region Methods

    #region Stores

    /// <summary>
    /// Prepare store search model
    /// </summary>
    /// <param name="searchModel">Store search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store search model
    /// </returns>
    public virtual async Task<MultiStoreSearchModel> PrepareMultiStoreSearchModelAsync(MultiStoreSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged store list model
    /// </summary>
    /// <param name="searchModel">Store search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store list model
    /// </returns>
    public virtual async Task<MultiStoreListModel> PrepareStoreListModelAsync(MultiStoreSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get customers
        var stores = await _branchService.GetAllStoresWithBranchCountAsync(name: searchModel.SearchStore,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare list model
        var model = await new MultiStoreListModel().PrepareToGridAsync(searchModel, stores, () =>
        {
            return stores.SelectAwait(async storeWithBranchCount =>
            {
                //fill in model values from the entity
                return new MultiStoreModel
                {
                    Id = storeWithBranchCount.Store.Id,
                    Name = storeWithBranchCount.Store.Name,
                    Url = storeWithBranchCount.Store.Url,
                    DisplayOrder = storeWithBranchCount.Store.DisplayOrder,
                    BranchesCount = storeWithBranchCount.BranchCount
                };
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare multiStore model
    /// </summary>
    /// <param name="model">MultiStore model</param>
    /// <param name="store">Store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the multiStore model
    /// </returns>
    public async Task<MultiStoreModel> PrepareMultiStoreModelAsync(MultiStoreModel model, Store store)
    {
        if (store != null)
        {
            //fill in model values from the entity
            if (model == null)
            {
                model = new MultiStoreModel
                {
                    Id = store.Id,
                    Name = store.Name,
                    CompanyName = store.CompanyName,
                    Url = store.Url,
                };
            }
        }

        return model;
    }

    #endregion

    #region Departments

    /// <summary>
    /// Prepare departments search model
    /// </summary>
    /// <param name="searchModel">MultiStore department search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>MultiStore department search model</returns>
    public virtual async Task<DepartmentSearchModel> PrepareDepartmentSearchModelAsync(DepartmentSearchModel searchModel, int storeId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.StoreId = storeId;

        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(searchModel.MultiStoreSettingsModel, storeId);

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged department list model
    /// </summary>
    /// <param name="searchModel">Department search model</param>
    /// <param name="store">Store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the department list model
    /// </returns>
    public virtual async Task<DepartmentListModel> PrepareDepartmentListModelAsync(DepartmentSearchModel searchModel, Store store)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        ArgumentNullException.ThrowIfNull(store);

        //get forums
        var departments = await _multiStoreService.GetDepartmentsByStoreIdAsync(storeId: store.Id, searchName: searchModel.SearchDepartmentName,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare list model
        var model = await new DepartmentListModel().PrepareToGridAsync(searchModel, departments, () =>
        {
            return departments.SelectAwait(async department =>
            {
                //fill in model values from the entity
                return new DepartmentModel
                {
                    Id = department.Id,
                    Name = department.Name,
                    Active = department.Active
                };
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare department model
    /// </summary>
    /// <param name="model">Department model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="departmentId">Department identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the department model
    /// </returns>
    public virtual async Task<DepartmentModel> PrepareDepartmentModelAsync(DepartmentModel model, int storeId, int? departmentId)
    {
        var department = departmentId.HasValue ? await _multiStoreService.GetDepartmentByIdAsync(departmentId.Value) : null;

        if (model == null)
        {
            // New packName form
            model = new DepartmentModel
            {
                Active = true,
                IsNew = true,
            };

            if (department != null)
            {
                model.Id = department.Id;
                model.Name = department.Name;
                model.Active = department.Active;
                model.IsNew = false;
            }
        }

        model.StoreId = storeId;

        if (department != null)
        {
            await PrepareDepartmentManagerSearchModelAsync(model.DepartmentManagerSearchModel, department.Id, storeId);
        }

        return model;
    }

    /// <summary>
    /// Prepare paged department manager list model
    /// </summary>
    /// <param name="searchModel">Manager search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager list model
    /// </returns>
    public virtual async Task<PackNamesListModel> PrepareDepartmentManagerListModelAsync(DepartmentManagerSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get managers
        var managers = await _multiStoreService.GetManagersByDepartmentIdAsync(searchModel.DepartmentId,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = new PackNamesListModel().PrepareToGrid(searchModel, managers, () =>
        {
            return managers.Select(manager =>
            {
                var packNameModel = new PackNameModel
                {
                    Id = manager.Id,
                    Name = manager.Name,
                    PrintName = manager.PrintName,
                    EmployeeNumber = manager.EmployeeNumber,
                    POEntry = manager.POEntry,
                    Active = manager.Active,
                    Gender = manager.Gender,
                    Group = manager.Group,
                    IsCustomer = manager.CustomerId.HasValue,
                    CustomerName = manager.Email,
                    Branch = manager.Branch,
                    DepartmentId = searchModel.DepartmentId
                };

                return packNameModel;
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare manager search model to add to the department
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the department</param>
    /// <param name="department">Department</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager search model to add to the department
    /// </returns>
    public async Task<AddDepartmentManagerSearchModel> PrepareAddDepartmentManagerSearchModelAsync(AddDepartmentManagerSearchModel searchModel, Department department)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.DepartmentId = department.Id;
        searchModel.Department = department.Name;

        //prepare page parameters
        searchModel.SetPopupGridPageSize();

        await _cwSettingModelFactory.PrepareMultiStoreBasicSettingsModelAsync(searchModel.MultiStoreSettingsModel, department.StoreId);

        return searchModel;
    }

    /// <summary>
    /// Prepare paged manager list model to add to the department
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the department</param>
    /// <param name="department">Department</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager list model to add to the department
    /// </returns>
    public virtual async Task<PackNamesListModel> PrepareAddDepartmentManagerListModelAsync(AddDepartmentManagerSearchModel searchModel, Department department)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get managers
        var managers = await _multiStoreService.GetManagersNotInDepartmentAsync(department.StoreId, searchModel.DepartmentId, searchModel.SearchManagerName,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare grid model
        var model = new PackNamesListModel().PrepareToGrid(searchModel, managers, () =>
        {
            return managers.Select(manager =>
            {
                var packNameModel = new PackNameModel
                {
                    Id = manager.Id,
                    Name = manager.Name,
                    PrintName = manager.PrintName,
                    EmployeeNumber = manager.EmployeeNumber,
                    POEntry = manager.POEntry,
                    Active = manager.Active,
                    Gender = manager.Gender,
                    Group = manager.Group,
                    IsCustomer = manager.CustomerId.HasValue,
                    CustomerName = manager.Email,
                    Branch = manager.Branch,
                };

                return packNameModel;
            });
        });

        return model;
    }

    #endregion

    #region Cost Centers

    /// <summary>
    /// Prepare cost center search model
    /// </summary>
    /// <param name="searchModel">MultiStore cost center search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>MultiStore cost center search model</returns>
    public virtual CostCenterSearchModel PrepareCostCenterSearchModel(CostCenterSearchModel searchModel, int? storeId, int? branchId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        searchModel.StoreId = storeId;
        searchModel.BranchId = branchId;

        //prepare page parameters
        searchModel.SetGridPageSize();

        return searchModel;
    }

    /// <summary>
    /// Prepare paged cost center list model
    /// </summary>
    /// <param name="searchModel">Cost center search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the cost center list model
    /// </returns>
    public virtual async Task<CostCenterListModel> PrepareCostCenterListModelAsync(CostCenterSearchModel searchModel, int? storeId, int? branchId)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get cost centers
        var costCenters = await _multiStoreService.GetCostCentersAsync(storeId: storeId, branchId: branchId, searchName: searchModel.SearchCostCenterName, searchBranchName: searchModel.SearchBranchName, searchInBranches: searchModel.SearchInBranches,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

        //prepare list model
        var model = await new CostCenterListModel().PrepareToGridAsync(searchModel, costCenters, () =>
        {
            return costCenters.SelectAwait(async costCenter =>
            {
                //fill in model values from the entity
                return new CostCenterModel
                {
                    Id = costCenter.Id,
                    Name = costCenter.Name,
                    Active = costCenter.Active,
                    Branch = costCenter.Branch
                };
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare cost center model
    /// </summary>
    /// <param name="model">Cost center model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="branchId">Branch identifier</param>
    /// <param name="costCenterId">Cost center identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the Cost center model
    /// </returns>
    public virtual async Task<CostCenterModel> PrepareCostCenterModelAsync(CostCenterModel model, int? storeId, int? branchId, int? costCenterId)
    {
        var branch = (Branch)null;

        if (branchId.HasValue)
        {
            branch = await _branchService.GetBranchByIdAsync(branchId.Value);
        }

        var costCenter = costCenterId.HasValue ? await _multiStoreService.GetCostCenterByIdAsync(costCenterId.Value) : null;

        if (model == null)
        {
            // New packName form
            model = new CostCenterModel
            {
                Active = true,
                IsNew = true,
            };

            if (costCenter != null)
            {
                model.Id = costCenter.Id;
                model.Name = costCenter.Name;
                model.Active = costCenter.Active;
                model.IsNew = false;

                branch ??= costCenter.BranchId.HasValue ? await _branchService.GetBranchByIdAsync(costCenter.BranchId.Value) : null;
            }
        }

        model.StoreId = storeId ?? branch?.StoreId ?? 0;
        model.BranchId = branch?.Id;
        model.Branch = branch?.Name;

        if (costCenter != null)
        {
            await _cwAddressModelFactory.PrepareCWAddressSearchModelAsync(model.CWAddressSearchModel, costCenter.Id);
        }

        return model;
    }

    /// <summary>
    /// Prepare paged cost center address list model
    /// </summary>
    /// <param name="searchModel">Cost center address search model</param>
    /// <param name="costCenter">CostCenter</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the cost center address list model
    /// </returns>
    public virtual async Task<CWAddressListModel> PrepareCostCenterAddressListModelAsync(CWAddressSearchModel searchModel, CostCenter costCenter)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        ArgumentNullException.ThrowIfNull(costCenter);

        var addressesModel = new List<CWAddressModel>();

        //get packName addresses
        var addresses = await _multiStoreService.GetAddressesByCostCenterIdAsync(costCenter.Id);

        foreach (var address in addresses)
            addressesModel.Add(await _cwAddressModelFactory.PrepareCWAddressModelAsync(address));

        var pagedAddressesModel = addressesModel.ToPagedList(searchModel);

        var model = await new CWAddressListModel().PrepareToGridAsync(searchModel, pagedAddressesModel, () =>
        {
            return pagedAddressesModel.SelectAwait(async addressModel => addressModel);
        });

        return model;
    }

    #endregion

    #endregion
}
