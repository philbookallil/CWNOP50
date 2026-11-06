using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Configuration;
using Nop.Plugin.CW.ThemeCore.Areas.Admin.Models;
using Nop.Plugin.CW.ThemeCore.Services.Security;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.ThemeCore.Areas.Admin.Controllers;

/// <summary>
/// Reusable base for a per-theme settings admin page. Handles the store-scope override
/// boilerplate (load/save/override-flags) via nop's standard pattern, delegating the
/// theme-specific field wiring to abstract hooks. A per-theme plugin (Pattern C) derives
/// a thin controller that only implements the hooks + the view path.
/// </summary>
[Area(AreaNames.ADMIN)]
[AuthorizeAdmin]
[AutoValidateAntiforgeryToken]
public abstract class BaseCwThemeAdminController<TSettings, TModel> : BasePluginController
    where TSettings : class, ISettings, new()
    where TModel : BaseCwThemeSettingsModel, new()
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    protected BaseCwThemeAdminController(
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Template-method hooks (implement per theme)

    /// <summary>Explicit view path of the per-theme Configure page.</summary>
    protected abstract string GetConfigureViewPath();

    /// <summary>Copy theme-specific values from settings → model, and set *_OverrideForStore flags when storeScope &gt; 0.</summary>
    protected abstract Task PrepareModelAsync(TModel model, TSettings settings, int storeScope);

    /// <summary>Copy theme-specific values from model → settings.</summary>
    protected abstract void MapToSettings(TModel model, TSettings settings);

    /// <summary>Persist each theme-specific field with SaveSettingOverridablePerStoreAsync.</summary>
    protected abstract Task SaveOverridablePerStoreAsync(TModel model, TSettings settings, int storeScope);

    #endregion

    #region Actions

    [CheckPermission(CwThemeCorePermissionConfigManager.MANAGE)]
    public virtual async Task<IActionResult> Configure()
    {
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<TSettings>(storeScope);

        var model = new TModel { ActiveStoreScopeConfiguration = storeScope };
        await PrepareModelAsync(model, settings, storeScope);

        return View(GetConfigureViewPath(), model);
    }

    [HttpPost]
    [CheckPermission(CwThemeCorePermissionConfigManager.MANAGE)]
    public virtual async Task<IActionResult> Configure(TModel model)
    {
        if (!ModelState.IsValid)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Configure.FormErrors"));
            return await Configure();
        }

        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<TSettings>(storeScope);

        MapToSettings(model, settings);
        await SaveOverridablePerStoreAsync(model, settings, storeScope);

        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    #endregion
}
