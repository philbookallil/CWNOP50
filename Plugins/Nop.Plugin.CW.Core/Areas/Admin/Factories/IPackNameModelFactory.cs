using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Services.PackNames.Import;
using PackNameImportModel = Nop.Plugin.CW.Core.Models.PackNames.PackNameImportModel;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface IPackNameModelFactory
{
    /// <summary>
    /// Prepare packaging name list model
    /// </summary>
    /// <param name="branchId">Branch identifier</param>
    /// <param name="listId">Packaging name list identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name list model
    /// </returns>
    Task<PackNameListModel> PreparePackNameListModelAsync(int branchId, int? listId);

    /// <summary>
    /// Prepare manager search model
    /// </summary>
    /// <param name="searchModel">Manager search model</param>
    /// <returns>Manager search model</returns>
    ManagerSearchModel PrepareManagerSearchModel(ManagerSearchModel searchModel, PackNameList packNameList);

    /// <summary>
    /// Prepare paged manager list model to add to the packNameList
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the packNameList</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager list model to add to the packNameList
    /// </returns>
    Task<PackNamesListModel> PrepareManagerListModelAsync(ManagerSearchModel searchModel);

    /// <summary>
    /// Prepare manager search model to add to the packNameList
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the packNameList</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager search model to add to the packNameList
    /// </returns>
    Task<AddPackNameListManagerSearchModel> PrepareAddManagerSearchModelAsync(AddPackNameListManagerSearchModel searchModel, PackNameList packNameList);

    /// <summary>
    /// Prepare paged manager list model to add to the packNameList
    /// </summary>
    /// <param name="searchModel">Manager search model to add to the packNameList</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the manager list model to add to the packNameList
    /// </returns>
    Task<PackNamesListModel> PrepareAddManagerListModelAsync(AddPackNameListManagerSearchModel searchModel, PackNameList packNameList);

    /// <summary>
    /// Prepare packName search model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <returns>PackName search model</returns>
    Task<PackNameSearchModel> PreparePackNameSearchModelAsync(PackNameSearchModel searchModel, PackNameList packNameList);

    /// <summary>
    /// Prepare packName search model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>PackName search model</returns>
    Task<PackNameSearchModel> PreparePackNameSearchModelAsync(PackNameSearchModel searchModel, int storeId);

    /// <summary>
    /// Prepare paged packName list model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName list model
    /// </returns>
    Task<PackNamesListModel> PreparePackNamesListModelAsync(PackNameSearchModel searchModel);

    /// <summary>
    /// Gets the packnames the grid's search matches, on every page, for the Excel export
    /// </summary>
    /// <param name="searchModel">PackName search model, with the store, the list and the filters the grid posts</param>
    /// <param name="selectedIds">The packnames ticked in the grid; empty for every packname the search matches</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packnames, ordered by name
    /// </returns>
    Task<IList<PackNameDTO>> PreparePackNamesForExportAsync(PackNameSearchModel searchModel, ICollection<int> selectedIds);

    /// <summary>
    /// Prepare packaging name model
    /// </summary>
    /// <param name="model">Packaging name model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="branchId">Branch identifier</param>
    /// <param name="listId">Packaging name list identifier</param>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name list model
    /// </returns>
    Task<PackNameModel> PreparePackNameModelAsync(PackNameModel model, int? storeId, int? branchId, int? listId, int? packNameId);

    /// <summary>
    /// Prepare paged packName address list model
    /// </summary>
    /// <param name="searchModel">PackName address search model</param>
    /// <param name="packName">PackName</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName address list model
    /// </returns>
    Task<CWAddressListModel> PreparePackNameAddressListModelAsync(CWAddressSearchModel searchModel, PackName packName);

    /// <summary>
    /// Prepare packaging name model
    /// </summary>
    /// <param name="model">Packaging name model</param>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name model
    /// </returns>
    Task<PackNameModel> PrepareCustomerPackNameModelAsync(PackNameModel model, int customerId);

    /// <summary>
    /// Populates the specified list with available gender options for selection in a user interface.
    /// </summary>
    /// <param name="listItems">The list to which gender options will be added. Must not be null.</param>
    /// <param name="selectedGender">The gender value that should be marked as selected in the resulting list. Can be null or empty if no selection
    /// is required.</param>
    /// <param name="addAllOption">A value indicating whether to include an 'All' option at the beginning of the list. Set to <see
    /// langword="true"/> to add the option; otherwise, <see langword="false"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PrepareAvailableGendersAsync(IList<SelectListItem> listItems, string selectedGender, bool addAllOption);

    /// <summary>
    /// Populates the specified list with available department options for selection in a user interface based on the provided store identifier.
    /// </summary>
    /// <param name="listItems">The list to which department options will be added. Must not be null.</param>
    /// <param name="storeId">The identifier of the store for which department options should be retrieved.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PrepareAvailableDepartmentsAsync(IList<SelectListItem> listItems, int storeId, bool activeOnly = false, int? keepId = null);

    /// <summary>
    /// Populates the specified list with available cost center options for selection in a user interface based on the provided store identifier.
    /// </summary>
    /// <param name="listItems">The list to which cost center options will be added. Must not be null.</param>
    /// <param name="storeId">The identifier of the store for which cost center options should be retrieved.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PrepareAvailableCostCentersAsync(IList<SelectListItem> listItems, int storeId, bool activeOnly = false, int? keepId = null);

    /// <summary>
    /// Populates the specified search model with available pack name type options for selection in the UI.
    /// </summary>
    /// <remarks>The method adds options for both, customer, and packname types to the model's available pack
    /// name types collection. The first option is selected by default. This method is typically used to prepare data
    /// for dropdown lists in views.</remarks>
    /// <param name="listItems">The list to populate with available pack name type options. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PrepareAvailablePackNameTypesAsync(IList<SelectListItem> listItems);

    /// <summary>
    /// Prepare paged customer model with no packname associated
    /// </summary>
    /// <param name="searchModel">Customer model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer model
    /// </returns>
    Task<ExtendedCustomerListModel> PrepareCustomersWithNoPackNameListModelAsync(PackNameSearchModel searchModel);

    /// <summary>
    /// Prepare customer model to associate to the packname
    /// </summary>
    /// <param name="searchModel">Customer model to associate to the packname</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer model to associate to the packname
    /// </returns>
    AssociateCustomerToPackNameSearchModel PrepareAssociateCustomerToPackNameSearchModel(
        AssociateCustomerToPackNameSearchModel searchModel);

    /// <summary>
    /// Prepare paged customer model to associate to the packname
    /// </summary>
    /// <param name="searchModel">Customer model to associate to the packname</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the customer model to associate to the packname
    /// </returns>
    Task<ExtendedCustomerListModel> PrepareAssociateCustomerToPackNameListModelAsync(
        AssociateCustomerToPackNameSearchModel searchModel);

    /// <summary>
    /// Prepare the model of an administrator's Excel import page for a store
    /// </summary>
    /// <param name="layout">The layout of the administrator's file</param>
    /// <param name="store">The store the packnames are imported into</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the import page model
    /// </returns>
    Task<PackNameImportModel> PreparePackNameImportModelAsync(PackNameImportLayout layout, Store store);
}