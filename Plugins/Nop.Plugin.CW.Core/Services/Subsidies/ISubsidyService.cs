using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

public interface ISubsidyService
{
    #region Subsidy

    /// <summary>
    /// Gets a subsidy by indentifier
    /// </summary>
    /// <param name="subsidyId">Subsidy indentifier</param>
    /// <returns>Subsidy</returns>
    Task<Subsidy> GetSubsidyByIdAsync(int subsidyId);

    /// <summary>
    /// Gets a subsidy by subsidy packName mapping identifier
    /// </summary>
    /// <param name="subsidyPackNameId"></param>
    /// <returns></returns>
    Task<Subsidy> GetSubsidyBySubsidyPackNameIdAsync(int subsidyPackNameId);

    /// <summary>
    /// Gets subsidies by a list of identifiers
    /// </summary>
    /// <param name="subsidyIds"></param>
    /// <returns></returns>
    Task<IList<Subsidy>> GetSubsidiesByIdsAsync(IList<int> subsidyIds);

    /// <summary>
    /// Gets subsidy store mapping collection
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="name">Subsidy name</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidies store mapping collection
    /// </returns>
    Task<IPagedList<SubsidyDTO>> GetSubsidiesByStoreIdAsync(int storeId, string? name = null,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets active subsidies by store
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidies store mapping collection
    /// </returns>
    Task<IList<Subsidy>> GetActiveSubsidiesByStoreIdAsync(int storeId);

    /// <summary>
    /// Asynchronously saves the specified subsidy to the data store, creating a new record or updating an existing
    /// one as appropriate.
    /// </summary>
    /// <param name="subsidy">The subsidy entity to save. Cannot be null. If the subsidy has an ID of 0, a new record is created;
    /// otherwise, the existing record is updated.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveSubsidyAsync(Subsidy subsidy);

    /// <summary>
    /// Inserts a subsidy
    /// </summary>
    /// <param name="Subsidy">subsidy</param>
    Task InsertSubsidyAsync(Subsidy subsidy);

    /// <summary>
    /// Updates a subsidy
    /// </summary>
    /// <param name="subsidy">Subsidy</param>
    Task UpdateSubsidyAsync(Subsidy subsidy);

    /// <summary>
    /// Deletes a subsidy
    /// </summary>
    /// <param name="subsidy">Subsidy</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteSubsidyAsync(Subsidy subsidy);

    #endregion

    #region Subsidy PackName Mapping

    /// <summary>
    /// Gets packNames by subsidy identifier
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    Task<IList<SubsidyPackName>> GetSubsidyPackNamesBySubsidyIdAsync(int subsidyId);

    /// <summary>
    /// Gets subsidy packNames by subsidy identifier
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    Task<IList<SubsidyPackName>> GetAllSubsidyPackNamesBySubsidyIdAsync(int subsidyId);

    /// <summary>
    /// Gets subsidy packName mappings by packName identifier
    /// </summary>
    /// <param name="packNameId">Packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packName mappings
    /// </returns>
    Task<IList<SubsidyPackName>> GetAllSubsidyPackNamesByPackNameIdAsync(int packNameId);

    /// <summary>
    /// Gets packNames
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    Task<IPagedList<SubsidyPackNameDTO>> GetPackNamesBySubsidyIdAsync(int subsidyId,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets packNames
    /// </summary>
    /// <param name="storeId"></param>
    /// <param name="branchName"></param>
    /// <param name="packName"></param>
    /// <param name="email"></param>
    /// <param name="packNameType"></param>
    /// <param name="gender"></param>
    /// <param name="withoutSubsidy"></param>
    /// <param name="departmentId"></param>
    /// <param name="costCenterId"></param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packNameLists
    /// </returns>
    Task<IPagedList<PackNameDTO>> GetPackNamesAsync(int storeId, string branchName, string packName, string email, string packNameType, string gender, bool withoutSubsidy, int departmentId, int costCenterId,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets subsidy packName mapping by identifier
    /// </summary>
    /// <param name="mappingId">Subsidy packName mapping identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mapping
    /// </returns>
    Task<SubsidyPackName> GetSubsidyPackNameByIdAsync(int mappingId);

    /// <summary>
    /// Inserts a subsidy packname mapping
    /// </summary>
    /// <param name="subsidyPackName">Subsidy packname mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertSubsidyPackNameAsync(SubsidyPackName subsidyPackName);

    /// <summary>
    /// Deletes a subsidy packName mapping
    /// </summary>
    /// <param name="subsidyPackName">Subsidy PackName mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteSubsidyPackNameAsync(SubsidyPackName subsidyPackName);

    #endregion

    #region Subsidy PackName Variation

    /// <summary>
    /// Gets a subsidy variation by indentifier
    /// </summary>
    /// <param name="id">Subsidy variation indentifier</param>
    /// <returns>Subsidy variation</returns>
    Task<SubsidyVariation> GetSubsidyVariationByIdAsync(int id);

    /// <summary>
    /// Gets the variation for a subsidy packName mapping
    /// </summary>
    /// <param name="subsidyPackNameId">Subsidy - Packaging name mapping identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the variation
    /// </returns>
    Task<SubsidyVariation> GetSubsidyVariationBySubsidyPacknameIdAsync(int subsidyPackNameId);

    /// <summary>
    /// Asynchronously retrieves all subsidy variations associated with the specified subsidy pack name identifiers.
    /// </summary>
    /// <param name="subsidyPackNameIds">A list of subsidy pack name identifiers for which to retrieve subsidy variations. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of subsidy variations
    /// matching the specified subsidy pack name identifiers. The list is empty if no matching subsidy variations
    /// are found.</returns>
    Task<IList<SubsidyVariation>> GetSubsidyVariationsBySubsidyPackNameIdsAsync(IList<int> subsidyPackNameIds);

    /// <summary>
    /// Inserts a subsidy variation
    /// </summary>
    /// <param name="subsidyVariation">Subsidy Variation</param>
    Task InsertSubsidyVariationAsync(SubsidyVariation subsidyVariation);

    /// <summary>
    /// Inserts a subsidy variation history
    /// </summary>
    /// <param name="subsidyVariationHistory">Subsidy Variation History</param>
    Task InsertSubsidyVariationHistoryAsync(SubsidyVariationHistory subsidyVariationHistory);

    /// <summary>
    /// Updates a subsidy variation
    /// </summary>
    /// <param name="subsidyVariation">Subsidy Variation</param>
    Task UpdateSubsidyVariationAsync(SubsidyVariation subsidyVariation);

    #endregion

    #region Product Group

    /// <summary>
    /// Gets a product group by indentifier
    /// </summary>
    /// <param name="productGroupId">Product group indentifier</param>
    /// <returns>Product Group</returns>
    Task<ProductGroup> GetProductGroupByIdAsync(int productGroupId);

    /// <summary>
    /// Gets a list of product groups by their identifiers
    /// </summary>
    /// <param name="productGroupIds">List of product group identifiers</param>
    /// <returns>List of Product Groups</returns>
    Task<IList<ProductGroup>> GetProductGroupsByIdsAsync(IList<int> productGroupIds);

    /// <summary>
    /// Gets product group store mapping collection
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <param name="name">Name</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidies store mapping collection
    /// </returns>
    Task<IPagedList<ProductGroupDTO>> GetProductGroupsByStoreIdAsync(int storeId, int subsidyId = 0, string? name = null,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Inserts a Product Group
    /// </summary>
    /// <param name="productGroup">Product group</param>
    Task InsertProductGroupAsync(ProductGroup productGroup);

    /// <summary>
    /// Updates a Product Group
    /// </summary>
    /// <param name="productGroup">Product group</param>
    Task UpdateProductGroupAsync(ProductGroup productGroup);

    /// <summary>
    /// Deletes a Product Group
    /// </summary>
    /// <param name="productGroup">Product group</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteProductGroupAsync(ProductGroup productGroup);

    /// <summary>
    /// Gets the list of icons for product groups
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    Task<List<string>> GetProductGroupIconsAsync();

    #endregion

    #region Product Group Product Mapping

    /// <summary>
    /// Gets all products by product group identifier
    /// </summary>
    /// <param name="productGroupId">Product Group identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product list
    /// </returns>
    Task<IList<ProductGroupProduct>> GetProductsByProductGroupIdAsync(int productGroupId);

    /// <summary>
    /// Gets  products by product group identifier
    /// </summary>
    /// <param name="productGroupId">Product Group identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product list
    /// </returns>
    Task<IPagedList<Product>> GetProductsByProductGroupIdAsync(int productGroupId,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets product group product mapping
    /// </summary>
    /// <param name="productGroupId">Product group identifier</param>
    /// <param name="productId">Product identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mapping
    /// </returns>
    Task<ProductGroupProduct> GetProductGroupProductByIdAsync(int productGroupId, int productId);

    /// <summary>
    /// Inserts a product group product mapping
    /// </summary>
    /// <param name="productGroupProduct">Product group product mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertProductGroupProductAsync(ProductGroupProduct productGroupProduct);

    /// <summary>
    /// Deletes a product group product mapping
    /// </summary>
    /// <param name="productGroupProduct">Product group product mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteProductGroupProductAsync(ProductGroupProduct productGroupProduct);

    #endregion

    #region Subsidy Product Group Mapping

    /// <summary>
    /// Gets product groups by subsidy identifier
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    Task<IList<SubsidyProductGroup>> GetSubsidyProductGroupsBySubsidyIdAsync(int subsidyId);

    /// <summary>
    /// Gets subsidy product groups by product group identifier
    /// </summary>
    /// <param name="productGroupId">Product Group identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    Task<IList<SubsidyProductGroup>> GetSubsidyProductGroupsByProductGroupIdAsync(int productGroupId);

    /// <summary>
    /// Gets product groups
    /// </summary>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list
    /// </returns>
    Task<IPagedList<SubsidyProductGroupDTO>> GetSubsidyProductGroupsAsync(int subsidyId,
        int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets subsidy product group mapping by identifier
    /// </summary>
    /// <param name="mappingId">Subsidy product group mapping identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mapping
    /// </returns>
    Task<SubsidyProductGroup> GetSubsidyProductGroupByIdAsync(int mappingId);

    /// <summary>
    /// Inserts a subsidy product group mapping
    /// </summary>
    /// <param name="subsidyProductGroup">Subsidy product group mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertSubsidyProductGroupAsync(SubsidyProductGroup subsidyProductGroup);

    /// <summary>
    /// Updates a subsidy product group mapping
    /// </summary>
    /// <param name="subsidyProductGroup">Subsidy product group mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task UpdateSubsidyProductGroupAsync(SubsidyProductGroup subsidyProductGroup);

    /// <summary>
    /// Deletes a subsidy product group mapping
    /// </summary>
    /// <param name="subsidyProductGroup">Subsidy product group mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteSubsidyProductGroupAsync(SubsidyProductGroup subsidyProductGroup);

    #endregion

    #region Subsidy Topup

    /// <summary>
    /// Adds credit to the topup for the current ordering period.
    /// Creates a new topup record when no record exists for the current period.
    /// When the period has no date constraint (null dates), the same record is reused indefinitely.
    /// </summary>
    Task<SubsidyTopup> SaveSubsidyTopupForPeriodAsync(int subsidyPackNameId, decimal creditToAdd, Subsidy subsidy, int creatorId, string reason);

    /// <summary>
    /// Whether a topup posted now for this assignment would land inside its current ordering period.
    /// </summary>
    /// <remarks>
    /// The two <c>Save*ForPeriodAsync</c> methods throw outside the period - that is the invariant, kept
    /// where the money is written. This is the question a screen asks first so it can refuse with a
    /// message instead of a 500: a closed non-annual season has no open period at all.
    /// </remarks>
    /// <param name="subsidyPackNameId">Subsidy - packaging name mapping identifier</param>
    /// <param name="subsidy">The assignment's subsidy</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result is true when a topup can be recorded now
    /// </returns>
    Task<bool> IsTopupPeriodOpenAsync(int subsidyPackNameId, Subsidy subsidy);

    #endregion

    #region Product Group Topup

    Task<ProductGroupTopup> SaveProductGroupTopupForPeriodAsync(int subsidyPackNameId, int productGroupId, int quantityToAdd, decimal creditToAdd, Subsidy subsidy, int creatorId, string reason);

    #endregion
}
