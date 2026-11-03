using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Infrastructure;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Nop.Plugin.Integration.Odoo;

/// <summary>
/// Entry point for the Odoo Integration plugin.
/// </summary>
public class OdooIntegrationPlugin : BasePlugin
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public OdooIntegrationPlugin(
        ISettingService settingService,
        ILocalizationService localizationService,
        IWebHelper webHelper)
    {
        _settingService = settingService;
        _localizationService = localizationService;
        _webHelper = webHelper;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public override string GetConfigurationPageUrl()
        => $"{_webHelper.GetStoreLocation()}Admin/{OdooConstants.AdminController}/Configure";

    /// <inheritdoc />
    public override async Task InstallAsync()
    {
        // Persist default (empty) settings so the settings page has a record to load
        await _settingService.SaveSettingAsync(new OdooSettings(), storeId: 0);

        await _localizationService.AddOrUpdateLocaleResourceAsync(OdooLocaleResources.All);

        await base.InstallAsync();
    }

    /// <summary>
    /// Update plugin; a version containing <c>DATA</c> reinstalls the locale resources.
    /// </summary>
    /// <param name="currentVersion">Current version of plugin</param>
    /// <param name="targetVersion">New version of plugin</param>
    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        if (targetVersion.Contains("DATA"))
            await _localizationService.AddOrUpdateLocaleResourceAsync(OdooLocaleResources.All);

        await base.UpdateAsync(currentVersion, targetVersion);
    }

    /// <inheritdoc />
    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<OdooSettings>();

        await _localizationService.DeleteLocaleResourcesAsync(OdooLocaleResources.Prefix);

        await base.UninstallAsync();
    }

    #endregion
}
