using Nop.Core;
using Nop.Plugin.CW.ThemeCore.Services.Security;
using Nop.Plugin.CW.Theme.Standard.Settings;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Events;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.CW.Theme.Standard;

/// <summary>
/// Per-theme plugin for the CW.Standard theme: provides theme-specific admin settings.
/// Depends on CW.ThemeCore (Pattern C — inherits its base classes via a DLL reference).
/// </summary>
public class StandardThemePlugin : BasePlugin, IMiscPlugin, IConsumer<AdminMenuCreatedEvent>
{
    #region Fields

    protected readonly IAdminMenu _adminMenu;
    protected readonly ILocalizationService _localizationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public StandardThemePlugin(
        IAdminMenu adminMenu,
        ILocalizationService localizationService,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _adminMenu = adminMenu;
        _localizationService = localizationService;
        _settingService = settingService;
        _webHelper = webHelper;
    }

    #endregion

    #region Admin menu

    public async Task HandleEventAsync(AdminMenuCreatedEvent eventMessage)
    {
        var menu = new AdminMenuItem
        {
            SystemName = "CW.Theme.Standard.AdminMenu",
            Title = await _localizationService.GetResourceAsync("Plugins.CW.Theme.Standard.Admin.Menu"),
            Url = _adminMenu.GetMenuItemUrl("StandardThemeAdmin", "Configure"),
            IconClass = "far fa-circle",
            Visible = true,
            PermissionNames = new List<string> { CwThemeCorePermissionConfigManager.MANAGE }
        };

        //anchor under the CW.Core menu group if present, else at root
        var coreMenu = eventMessage.RootMenuItem.GetItemBySystemName("CW.Core");
        if (coreMenu is not null)
            coreMenu.ChildNodes.Add(menu);
        else
            eventMessage.RootMenuItem.ChildNodes.Add(menu);
    }

    #endregion

    #region Methods

    public override string GetConfigurationPageUrl()
        => $"{_webHelper.GetStoreLocation()}Admin/StandardThemeAdmin/Configure";

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new StandardThemeSettings
        {
            DarkNavBar = false,
            SquareCorners = false
        });

        await AddOrUpdateLocaleResourceAsync();

        await base.InstallAsync();
    }

    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        if (targetVersion.Contains("DATA"))
        {
            await AddOrUpdateLocaleResourceAsync();

            //LightNavBar became DarkNavBar: the opposite meaning, so its value does not carry over
            var renamed = (await _settingService.GetAllSettingsAsync())
                .Where(setting => setting.Name.Equals("standardthemesettings.lightnavbar", StringComparison.OrdinalIgnoreCase))
                .ToList();
            await _settingService.DeleteSettingsAsync(renamed);
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.CW.Theme.Standard.Fields.LightNavBar");
        }

        await base.UpdateAsync(currentVersion, targetVersion);
    }

    /// <summary>
    /// The plugin's resources, including the CW.Standard theme's own storefront text
    /// (the theme has no installer of its own)
    /// </summary>
    private async Task AddOrUpdateLocaleResourceAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.CW.Theme.Standard.Admin.Menu"] = "CW Standard theme",
            ["Plugins.CW.Theme.Standard.Configure.Title"] = "CW Standard theme settings",
            ["Plugins.CW.Theme.Standard.Fields.DarkNavBar"] = "Dark navigation bar",
            ["Plugins.CW.Theme.Standard.Fields.DarkNavBar.Hint"] = "On a desktop screen, show the navigation bar in the store's dark colour with white text, instead of the light default. Per store.",
            ["Plugins.CW.Theme.Standard.Fields.SquareCorners"] = "Square corners",
            ["Plugins.CW.Theme.Standard.Fields.SquareCorners.Hint"] = "Use square corners instead of the rounded default.",
            ["Plugins.CW.Theme.Standard.HeaderLinks.GoToCart"] = "Go to cart"
        });
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<StandardThemeSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.CW.Theme.Standard");

        await base.UninstallAsync();
    }

    #endregion
}
