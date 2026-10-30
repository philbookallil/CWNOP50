using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Localization;

namespace Nop.Plugin.CW.Core.Infrastructure;

public class SubsidyPackNamePanelProvider : IPackNamePanelProvider
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IPackNameService _packNameService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor
    public SubsidyPackNamePanelProvider(ILocalizationService localizationService,
        IPackNameService packNameService,
        ICWFeatureService cwFeatureService)
    {
        _localizationService = localizationService;
        _packNameService = packNameService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Methods

    public async Task<PackNamePanelModel> GetPanelAsync(int packNameId, string prefix = null)
    {
        var packName = await _packNameService.GetPackNameByIdAsync(packNameId);
        var list = packName is null ? null : await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

        if (list == null || !await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, list.StoreId))
            return null;

        return new PackNamePanelModel
        {
            Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackName.Subsidies"),
            ViewComponentName = "PackNameSubsidies",
            AdditionalData = new { packNameId, prefix }
        };
    }

    public int Order => 1;

    #endregion
}
