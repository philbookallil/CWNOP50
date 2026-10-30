using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Domain.MultiStores;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface IMultiStoreModelFactory
{
    #region Stores

    /// <summary>
    /// Prepare store branch search model
    /// </summary>
    /// <param name="searchModel">Branch search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the branch search model
    /// </returns>
    Task<MultiStoreSearchModel> PrepareMultiStoreSearchModelAsync(MultiStoreSearchModel searchModel);

    /// <summary>
    /// Prepare paged store list model
    /// </summary>
    /// <param name="searchModel">Store search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store list model
    /// </returns>
    Task<MultiStoreListModel> PrepareStoreListModelAsync(MultiStoreSearchModel searchModel);

    /// <summary>
    /// Prepare multiStore model
    /// </summary>
    /// <param name="model">MultiStore model</param>
    /// <param name="store">Store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the multiStore model
    /// </returns>
    Task<MultiStoreModel> PrepareMultiStoreModelAsync(MultiStoreModel model, Store store);

    #endregion

    #region Departments

    /// <summary>
    /// Prepare departments search model
    /// </summary>
    /// <param name="searchModel">MultiStore department search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>MultiStore department search model</returns>
    Task<DepartmentSearchModel> PrepareDepartmentSearchModelAsync(DepartmentSearchModel searchModel, int storeId);

    /// <summary>
    /// Prepare paged department list model
    /// </summary>
    /// <param name="searchModel">Department search model</param>
    /// <param name="store">Store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the department list model
    /// </returns>
    Task<DepartmentListModel> PrepareDepartmentListModelAsync(DepartmentSearchModel searchModel, Store store);

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
    Task<DepartmentModel> PrepareDepartmentModelAsync(DepartmentModel model, int storeId, int? departmentId);

    /// <summary>
    /// Prepare paged department manager list model
    /// </summary>
    /// <param name="searchModel">Manager search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager list model
    /// </returns>
    Task<PackNamesListModel> PrepareDepartmentManagerListModelAsync(DepartmentManagerSearchModel searchModel);

    /// <summary>
    /// Prepare manager search model to add to the department
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the department</param>
    /// <param name="department">Department</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager search model to add to the department
    /// </returns>
    Task<AddDepartmentManagerSearchModel> PrepareAddDepartmentManagerSearchModelAsync(AddDepartmentManagerSearchModel searchModel, Department department);

    /// <summary>
    /// Prepare paged manager list model to add to the department
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the department</param>
    /// <param name="department">Department</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager list model to add to the department
    /// </returns>
    Task<PackNamesListModel> PrepareAddDepartmentManagerListModelAsync(AddDepartmentManagerSearchModel searchModel, Department department);

    #endregion

    #region Cost Centers

    /// <summary>
    /// Prepare cost center search model
    /// </summary>
    /// <param name="searchModel">MultiStore cost center search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="branchId">Branch identifier</param>
    /// <returns>MultiStore cost center search model</returns>
    CostCenterSearchModel PrepareCostCenterSearchModel(CostCenterSearchModel searchModel, int? storeId, int? branchId);

    /// <summary>
    /// Prepare paged cost center list model
    /// </summary>
    /// <param name="searchModel">Cost center search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the cost center list model
    /// </returns>
    Task<CostCenterListModel> PrepareCostCenterListModelAsync(CostCenterSearchModel searchModel, int? storeId, int? branchId);

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
    Task<CostCenterModel> PrepareCostCenterModelAsync(CostCenterModel model, int? storeId, int? branchId, int? costCenterId);

    /// <summary>
    /// Prepare paged cost center address list model
    /// </summary>
    /// <param name="searchModel">Cost center address search model</param>
    /// <param name="costCenter">CostCenter</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the cost center address list model
    /// </returns>
    Task<CWAddressListModel> PrepareCostCenterAddressListModelAsync(CWAddressSearchModel searchModel, CostCenter costCenter);

    #endregion
}
