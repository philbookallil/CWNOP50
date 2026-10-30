using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Models.Subsidies;

namespace Nop.Plugin.CW.Core.Factories;

public interface ISubsidyCatalogModelFactory
{
    Task<SubsidyCatalogModel> PrepareSubsidyCatalogModelAsync(PackName packName, string viewMode);

    Task<SubsidyBarModel> PrepareSubsidyBarModelAsync(PackName packName, bool flyout);

    /// <summary>
    /// Prepare the full Change Topup panel model for the given PackName.
    /// </summary>
    Task<PackNameTopupModel> PreparePackNameTopupModelAsync(PackName packName);

    /// <summary>
    /// Return lightweight topup values for AJAX refresh after a save.
    /// </summary>
    Task<IList<TopupRefreshModel>> GetPackNameTopupsAsync(int packNameId);

    /// <summary>Prepare the subsidy-level topup input form model.</summary>
    Task<SubsidyTopupInputModel> PrepareSubsidyTopupInputModelAsync(SubsidyTopupInputModel model, int subsidyPackNameId);

    /// <summary>Prepare the product-group topup input form model.</summary>
    Task<ProductGroupTopupInputModel> PrepareProductGroupTopupInputModelAsync(ProductGroupTopupInputModel model, int subsidyPackNameId, int productGroupId);
}
