using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Security;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

public partial class PackNameSubsidiesViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IPermissionService _permissionService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IPackNameService _packNameService;

    #endregion

    #region Ctor

    public PackNameSubsidiesViewComponent(IPermissionService permissionService,
        ICWFeatureService cwFeatureService,
        IPackNameService packNameService)
    {
        _permissionService = permissionService;
        _cwFeatureService = cwFeatureService;
        _packNameService = packNameService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Invoke view component
    /// </summary>
    /// <param name="packNameId">PackName identifier whose subsidies are rendered</param>
    /// <param name="prefix">Prefix for the generated element ids, so several instances on one page do not collide</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the view component result
    /// </returns>
    public async Task<IViewComponentResult> InvokeAsync(int packNameId, string prefix)
    {
        if (!await _permissionService.AuthorizeAsync(CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE))
            return Content(string.Empty);

        var packName = await _packNameService.GetPackNameByIdAsync(packNameId);
        var list = packName is null ? null : await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);

        if (list == null || !await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, list.StoreId))
            return Content(string.Empty);

        //prepare model
        var model = new PackNameSubsidiesPanelModel
        {
            PackNameId = packNameId,
            Prefix = prefix
        };

        return View(model);
    }

    #endregion
}
