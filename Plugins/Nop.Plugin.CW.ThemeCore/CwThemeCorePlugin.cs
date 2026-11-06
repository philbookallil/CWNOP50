using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Plugin.CW.ThemeCore.Components;
using Nop.Plugin.CW.ThemeCore.Services;
using Nop.Plugin.CW.ThemeCore.Services.Security;
using Nop.Plugin.CW.ThemeCore.Settings;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Events;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.CW.ThemeCore;

/// <summary>
/// Central CW theme framework plugin. Renders per-store accent-colour custom properties
/// into the page head (Capability 1) and exposes an admin settings page.
/// </summary>
public class CwThemeCorePlugin : BasePlugin, IWidgetPlugin, IMiscPlugin, IConsumer<AdminMenuCreatedEvent>
{
    #region Fields

    protected readonly IAdminMenu _adminMenu;
    protected readonly ILocalizationService _localizationService;
    protected readonly ISettingService _settingService;
    protected readonly ICwThemeCssFileService _cssFileService;
    protected readonly IWebHelper _webHelper;
    protected readonly WidgetSettings _widgetSettings;

    #endregion

    #region Ctor

    public CwThemeCorePlugin(
        IAdminMenu adminMenu,
        ILocalizationService localizationService,
        ISettingService settingService,
        ICwThemeCssFileService cssFileService,
        IWebHelper webHelper,
        WidgetSettings widgetSettings)
    {
        _adminMenu = adminMenu;
        _localizationService = localizationService;
        _settingService = settingService;
        _cssFileService = cssFileService;
        _webHelper = webHelper;
        _widgetSettings = widgetSettings;
    }

    #endregion

    #region Widget

    /// <summary>
    /// Render into the core head zone so the :root{} block loads on any active theme.
    /// </summary>
    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string> { PublicWidgetZones.HeadHtmlTag });
    }

    public Type GetWidgetViewComponent(string widgetZone)
    {
        ArgumentNullException.ThrowIfNull(widgetZone);

        if (widgetZone.Equals(PublicWidgetZones.HeadHtmlTag))
            return typeof(CwThemeHeadViewComponent);

        return null;
    }

    /// <summary>
    /// Infrastructure widget — hidden from the admin widget list (managed automatically).
    /// </summary>
    public bool HideInWidgetList => true;

    #endregion

    #region Admin menu

    public async Task HandleEventAsync(AdminMenuCreatedEvent eventMessage)
    {
        var themeMenu = new AdminMenuItem
        {
            SystemName = CwThemeCoreDefaults.AdminMenuSystemName,
            Title = await _localizationService.GetResourceAsync("Plugins.CW.ThemeCore.Admin.Menu"),
            Url = _adminMenu.GetMenuItemUrl("CwThemeCore", "Configure"),
            IconClass = "far fa-dot-circle",
            Visible = true,
            PermissionNames = new List<string> { CwThemeCorePermissionConfigManager.MANAGE }
        };

        //anchor under the CW.Core menu group if present (soft dependency), else at root
        var coreMenu = eventMessage.RootMenuItem.GetItemBySystemName(CwThemeCoreDefaults.CwCoreMenuSystemName);
        if (coreMenu is not null)
            coreMenu.ChildNodes.Add(themeMenu);
        else
            eventMessage.RootMenuItem.ChildNodes.Add(themeMenu);
    }

    #endregion

    #region Methods

    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/CwThemeCore/Configure";
    }

    public override async Task InstallAsync()
    {
        //provisional default palette (pending design) — hex without '#'
        await _settingService.SaveSettingAsync(new CwThemeSettings
        {
            PrimaryColor = "00549f",
            PrimaryTintColor = "eaf4fc",
            SuccessColor = "28a745",
            DangerColor = "dc3545",
            WarningColor = "ffc107",
            DarkColor = "343a40",
            ButtonRadius = 8,
            LogoPictureId = 0,
            DisableAccentColors = false
        });

        //activate the widget so the head injection renders without manual toggling
        if (!_widgetSettings.ActiveWidgetSystemNames.Contains(CwThemeCoreDefaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Add(CwThemeCoreDefaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        await AddOrUpdateLocaleResourceAsync();

        await base.InstallAsync();
    }

    /// <summary>
    /// Update plugin
    /// </summary>
    /// <param name="currentVersion">Current version of plugin</param>
    /// <param name="targetVersion">New version of plugin</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        if (targetVersion.Contains("DATA"))
            await AddOrUpdateLocaleResourceAsync();

        await base.UpdateAsync(currentVersion, targetVersion);
    }

    private async Task AddOrUpdateLocaleResourceAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.CW.ThemeCore.Admin.Menu"] = "CW Theme",
            ["Plugins.CW.ThemeCore.Configure.Title"] = "CW Theme settings",
            ["Plugins.CW.ThemeCore.Configure.FormErrors"] = "The settings were not saved. Correct the fields marked below and save again.",
            ["Plugins.CW.ThemeCore.Configure.AccentColours"] = "Accent colours",
            ["Plugins.CW.ThemeCore.Configure.AccentColours.Hint"] = "These colours are exposed to all CW plugins and themes as CSS custom properties (--cw-*). Values are per store.",
            ["Plugins.CW.ThemeCore.Configure.Logo"] = "Logo",
            ["Plugins.CW.ThemeCore.Configure.Preview"] = "Live preview",
            ["Plugins.CW.ThemeCore.Fields.PrimaryColor"] = "Primary colour",
            ["Plugins.CW.ThemeCore.Fields.PrimaryColor.Hint"] = "The main brand/accent colour (CSS var --cw-primary).",
            ["Plugins.CW.ThemeCore.Fields.PrimaryTintColor"] = "Primary tint",
            ["Plugins.CW.ThemeCore.Fields.PrimaryTintColor.Hint"] = "A light tint of the primary colour, used for subtle backgrounds (--cw-primary-tint).",
            ["Plugins.CW.ThemeCore.Fields.SuccessColor"] = "Success colour",
            ["Plugins.CW.ThemeCore.Fields.SuccessColor.Hint"] = "Positive/success accent (--cw-success).",
            ["Plugins.CW.ThemeCore.Fields.DangerColor"] = "Danger colour",
            ["Plugins.CW.ThemeCore.Fields.DangerColor.Hint"] = "Error/danger accent (--cw-danger).",
            ["Plugins.CW.ThemeCore.Fields.WarningColor"] = "Warning colour",
            ["Plugins.CW.ThemeCore.Fields.WarningColor.Hint"] = "Warning accent (--cw-warning).",
            ["Plugins.CW.ThemeCore.Fields.DarkColor"] = "Dark colour",
            ["Plugins.CW.ThemeCore.Fields.DarkColor.Hint"] = "Dark ink/surface accent (--cw-dark).",
            ["Plugins.CW.ThemeCore.Fields.ButtonRadius"] = "Button radius (px)",
            ["Plugins.CW.ThemeCore.Fields.ButtonRadius.Hint"] = "Corner radius of buttons in pixels (--cw-btn-radius): about 2 is square, 8 rounded, 999 a full pill. The Brand designer sets this from prompts like \"square buttons\" or \"pill buttons\". 0 leaves it to the theme.",
            ["Plugins.CW.ThemeCore.Fields.ButtonRadius.Invalid"] = "Enter a whole number between 0 and 999.",
            ["Plugins.CW.ThemeCore.Fields.LogoPictureId"] = "Logo",
            ["Plugins.CW.ThemeCore.Fields.LogoPictureId.Hint"] = "The store logo used by CW themes.",
            ["Plugins.CW.ThemeCore.Fields.SecondLogoPictureId"] = "Second logo",
            ["Plugins.CW.ThemeCore.Fields.SecondLogoPictureId.Hint"] = "An optional co-brand logo shown on the right of the header, alongside the primary logo (per store). Leave empty for none.",
            ["Plugins.CW.ThemeCore.Fields.SecondLogoLinkUrl"] = "Second logo link",
            ["Plugins.CW.ThemeCore.Fields.SecondLogoLinkUrl.Hint"] = "Optional URL the second logo links to (e.g. a partner site). Leave blank to render it as a plain image.",
            ["Plugins.CW.ThemeCore.Fields.ReverseLogos"] = "Reverse logo positions",
            ["Plugins.CW.ThemeCore.Fields.ReverseLogos.Hint"] = "Swap the header logos left-to-right: the second (co-brand) logo shows on the left and the primary logo on the right. Per store.",
            ["Plugins.CW.ThemeCore.Fields.Color.Invalid"] = "Enter a valid hex colour (e.g. 00549f), without the '#'.",
            ["Plugins.CW.ThemeCore.Fields.DisableAccentColors"] = "Don't apply accent colours",
            ["Plugins.CW.ThemeCore.Fields.DisableAccentColors.Hint"] = "When enabled, CW.ThemeCore does not emit the accent-colour variables (--cw-*), leaving colour control entirely to the active theme. Custom CSS is unaffected.",
            ["Plugins.CW.ThemeCore.Configure.CustomCss"] = "Custom CSS",
            ["Plugins.CW.ThemeCore.Fields.CustomCss"] = "Custom CSS",
            ["Plugins.CW.ThemeCore.Fields.CustomCss.Hint"] = "Store-specific CSS applied to the storefront. Sanitized on save (dangerous constructs are always removed); served from a generated, cache-busted file.",
            ["Plugins.CW.ThemeCore.Fields.CustomCssUrlPolicy"] = "url() policy",
            ["Plugins.CW.ThemeCore.Fields.CustomCssUrlPolicy.Hint"] = "How url(...) in Custom CSS is treated: Allow (any), DataAndSameOriginOnly (data:/relative only), or Block (strip all). @import, expression(), javascript:/vbscript:, behavior: are always blocked.",
            ["Plugins.CW.ThemeCore.Configure.Layouts"] = "Layouts",
            ["Plugins.CW.ThemeCore.Configure.Layouts.Hint"] = "Choose the layout for each storefront section. Options come from the active theme; selections are per store.",
            ["Plugins.CW.ThemeCore.Configure.BrandDesigner"] = "Brand designer (preview)",
            ["Plugins.CW.ThemeCore.Configure.BrandDesigner.Hint"] = "Set up the look of your storefront here - pick a vibe, fine-tune the accent colours and button shape, choose a layout for each page, and set your logos. The preview shows your live storefront and updates instantly as you make changes; switch on \"Edit elements\" to click a part of the preview and restyle it, or describe a change and let AI suggest one. Click Save to apply everything to the store."
        });
    }

    public override async Task UninstallAsync()
    {
        if (_widgetSettings.ActiveWidgetSystemNames.Contains(CwThemeCoreDefaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove(CwThemeCoreDefaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        //remove generated Custom CSS files
        await _cssFileService.DeleteAllAsync();

        await _settingService.DeleteSettingAsync<CwThemeSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.CW.ThemeCore");

        await base.UninstallAsync();
    }

    #endregion
}
