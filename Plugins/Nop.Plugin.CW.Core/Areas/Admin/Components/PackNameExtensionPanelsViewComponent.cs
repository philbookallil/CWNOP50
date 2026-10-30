using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Infrastructure;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

public partial class PackNameExtensionPanelsViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IEnumerable<IPackNamePanelProvider> _providers;
    protected readonly IPackNameService _packNameService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public PackNameExtensionPanelsViewComponent(IEnumerable<IPackNamePanelProvider> providers,
        IPackNameService packNameService,
        ICWFeatureService cwFeatureService)
    {
        _providers = providers;
        _packNameService = packNameService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(int packNameId, string prefix = null)
    {
        var packName = await _packNameService.GetPackNameByIdAsync(packNameId);
        var list = packName is null ? null : await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

        if (list == null || !await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, list.StoreId))
            return Content(string.Empty);

        var panels = new List<PackNamePanelModel>();

        foreach (var provider in _providers.OrderBy(p => p.Order))
        {
            var panel = await provider.GetPanelAsync(packNameId, prefix);
            if (panel != null)
                panels.Add(panel);
        }

        return View(panels);
    }
    #endregion
}
