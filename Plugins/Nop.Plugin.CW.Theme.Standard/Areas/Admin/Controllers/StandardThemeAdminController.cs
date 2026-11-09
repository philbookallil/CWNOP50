using Nop.Core;
using Nop.Plugin.CW.ThemeCore.Areas.Admin.Controllers;
using Nop.Plugin.CW.Theme.Standard.Areas.Admin.Models;
using Nop.Plugin.CW.Theme.Standard.Settings;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;

namespace Nop.Plugin.CW.Theme.Standard.Areas.Admin.Controllers;

/// <summary>
/// Thin per-theme settings controller. All the store-scope override boilerplate + the
/// Configure GET/POST live in the inherited BaseCwThemeAdminController (from CW.ThemeCore.dll,
/// Pattern C). This class only wires the theme-specific fields.
/// </summary>
public class StandardThemeAdminController : BaseCwThemeAdminController<StandardThemeSettings, StandardThemeSettingsModel>
{
    public StandardThemeAdminController(
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISettingService settingService,
        IStoreContext storeContext)
        : base(localizationService, notificationService, settingService, storeContext)
    {
    }

    protected override string GetConfigureViewPath()
        => "~/Plugins/CW.Theme.Standard/Views/Configure.cshtml";

    protected override async Task PrepareModelAsync(StandardThemeSettingsModel model, StandardThemeSettings settings, int storeScope)
    {
        model.DarkNavBar = settings.DarkNavBar;
        model.SquareCorners = settings.SquareCorners;

        if (storeScope > 0)
        {
            model.DarkNavBar_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.DarkNavBar, storeScope);
            model.SquareCorners_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.SquareCorners, storeScope);
        }
    }

    protected override void MapToSettings(StandardThemeSettingsModel model, StandardThemeSettings settings)
    {
        settings.DarkNavBar = model.DarkNavBar;
        settings.SquareCorners = model.SquareCorners;
    }

    protected override async Task SaveOverridablePerStoreAsync(StandardThemeSettingsModel model, StandardThemeSettings settings, int storeScope)
    {
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.DarkNavBar, model.DarkNavBar_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.SquareCorners, model.SquareCorners_OverrideForStore, storeScope, false);
    }
}
