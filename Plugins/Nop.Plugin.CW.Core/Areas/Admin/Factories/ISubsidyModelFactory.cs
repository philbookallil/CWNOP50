using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Web.Areas.Admin.Models.Catalog;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface ISubsidyModelFactory
{
    #region Product Groups

    /// <summary>
    /// Prepare product group search model
    /// </summary>
    /// <param name="searchModel">Product group search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>Product group search model</returns>
    ProductGroupSearchModel PrepareProductGroupSearchModel(ProductGroupSearchModel searchModel, int storeId);

    /// <summary>
    /// Prepare paged product group list model
    /// </summary>
    /// <param name="searchModel">Product group search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store product group list model
    /// </returns>
    Task<ProductGroupListModel> PrepareProductGroupListModelAsync(ProductGroupSearchModel searchModel);

    /// <summary>
    /// Prepare product group model
    /// </summary>
    /// <param name="model">Product group model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="productGroupId">Product group identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group model
    /// </returns>
    Task<ProductGroupModel> PrepareProductGroupModelAsync(ProductGroupModel model, int storeId, int? productGroupId);

    #endregion

    #region Product Group - Products

    /// <summary>
    /// Prepare paged product list model
    /// </summary>
    /// <param name="searchModel">Product search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product list model
    /// </returns>
    Task<ProductListModel> PrepareProductListModelAsync(ProductGroupProductSearchModel searchModel);

    /// <summary>
    /// Prepare product search model to add to the product group
    /// </summary>
    /// <param name="searchModel">Product search model to add to the product group</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product search model to add to the product group
    /// </returns>
    Task<AddProductToProductGroupSearchModel> PrepareAddProductToProductGroupSearchModelAsync(AddProductToProductGroupSearchModel searchModel, int productGroupId);

    /// <summary>
    /// Prepare paged product list model to add to the product group
    /// </summary>
    /// <param name="searchModel">Product search model to add to the product group</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product list model to add to the product group
    /// </returns>
    Task<ProductListModel> PrepareAddProductListModelAsync(AddProductToProductGroupSearchModel searchModel);

    #endregion

    #region Subsidies

    /// <summary>
    /// Prepare subsidy search model
    /// </summary>
    /// <param name="searchModel">Subsidy search model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>Subsidy search model</returns>
    SubsidySearchModel PrepareSubsidySearchModel(SubsidySearchModel searchModel, int storeId);

    /// <summary>
    /// Prepare paged subsidy list model
    /// </summary>
    /// <param name="searchModel">Subsidy search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store subsidy list model
    /// </returns>
    Task<SubsidyListModel> PrepareSubsidyListModelAsync(SubsidySearchModel searchModel);

    /// <summary>
    /// Prepare subsidy model
    /// </summary>
    /// <param name="model">Subsidy model</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="subsidyId">Subsidy identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy model
    /// </returns>
    Task<SubsidyModel> PrepareSubsidyModelAsync(SubsidyModel model, int storeId, int? subsidyId);

    #endregion

    #region Subsidy - PackNames

    /// <summary>
    /// Prepare paged packName list model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName list model
    /// </returns>
    Task<SubsidyPackNameListModel> PrepareSubsidyPackNameListModelAsync(SubsidyPackNameSearchModel searchModel);

    /// <summary>
    /// Prepare packName search model to add to the subsidy
    /// </summary>
    /// <param name="searchModel">PackName search model to add to the subsidy</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName search model to add to the subsidy
    /// </returns>
    Task<AddPackNameToSubsidySearchModel> PrepareAddPackNameToSubsidySearchModelAsync(AddPackNameToSubsidySearchModel searchModel, int subsidyId);

    /// <summary>
    /// Prepare paged packName list model
    /// </summary>
    /// <param name="searchModel">PackName search model</param>
    /// <param name="subsidy">Subsidy</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packName list model
    /// </returns>
    Task<PackNamesListModel> PreparePackNameListModelAsync(AddPackNameToSubsidySearchModel searchModel, Domain.Subsidies.Subsidy subsidy);

    /// <summary>
    /// Prepare subsidy packname variation model
    /// </summary>
    /// <param name="model">Subsidy packname variation model</param>
    /// <param name="variation">Variation</param>
    /// <param name="SubsidyPackNameId">Subsidy packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy packname variation
    /// </returns>
    Task<SubsidyVariationModel> PrepareSubsidyVariationModelAsync(SubsidyVariationModel model, SubsidyVariation variation, int subsidyPackNameId);

    #endregion

    #region Subsidy - Product Groups

    /// <summary>
    /// Prepare paged subsidy product group list model
    /// </summary>
    /// <param name="searchModel">Product group search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list model
    /// </returns>
    Task<SubsidyProductGroupListModel> PrepareSubsidyProductGroupListModelAsync(SubsidyProductGroupSearchModel searchModel);

    /// <summary>
    /// Prepare product group search model to add to the subsidy
    /// </summary>
    /// <param name="searchModel">Product group search model to add to the subsidy</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group search model to add to the subsidy
    /// </returns>
    Task<AddProductGroupToSubsidySearchModel> PrepareAddProductGroupToSubsidySearchModelAsync(AddProductGroupToSubsidySearchModel searchModel, int subsidyId);

    /// <summary>
    /// Prepare paged product group list model
    /// </summary>
    /// <param name="searchModel">Product group search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list model
    /// </returns>
    Task<ProductGroupListModel> PrepareProductGroupListModelAsync(AddProductGroupToSubsidySearchModel searchModel, Domain.Subsidies.Subsidy subsidy);

    #endregion

    #region PackName - Subsidies

    /// <summary>
    /// Prepare packaging name details model
    /// </summary>
    /// <param name="model">Packaging name model</param>
    /// <param name="packName">Packaging name</param>
    /// <param name="prefix">Prefix used to ensure unique ids for elements in the view</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name model
    /// </returns>
    Task<PackNameSubsidiesModel> PreparePackNameSubsidiesModelAsync(PackNameSubsidiesModel model, PackName packName, string prefix);

    /// <summary>
    /// Prepare subsidy topup model
    /// </summary>
    /// <param name="model">Subsidy topup model</param>
    /// <param name="topup">Topup</param>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy topup model
    /// </returns>
    Task<SubsidyTopupModel> PrepareSubsidyTopupModelAsync(SubsidyTopupModel model, int subsidyPackNameId);

    /// <summary>
    /// Prepare product group topup model
    /// </summary>
    /// <param name="model">Product group topup model</param>
    /// <param name="topup">Topup</param>
    /// <param name="subsidyPackNameId">Subsidy packaging name identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the subsidy topup model
    /// </returns>
    Task<ProductGroupTopupModel> PrepareProductGroupTopupModelAsync(ProductGroupTopupModel model, int subsidyPackNameId, int productGroupId);

    /// <summary>
    /// Prepare paged packname subsidy product group list model
    /// </summary>
    /// <param name="searchModel">Subsidy packaging name product group search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the product group list model
    /// </returns>
    Task<PackNameSubsidyProductGroupListModel> PreparePackNameSubsidyProductGroupListModelAsync(PackNameSubsidyProductGroupSearchModel searchModel);

    /// <summary>
    /// Prepare paged packname subsidy history list model
    /// </summary>
    /// <param name="searchModel">Subsidy packaging name history search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the history list model
    /// </returns>
    Task<PackNameSubsidyHistoryListModel> PreparePackNameSubsidyHistoryListModelAsync(PackNameSubsidyProductGroupSearchModel searchModel);

    #endregion
}