using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Integration.Odoo.Infrastructure;

/// <summary>
/// Attaches the plugin's admin sidebar menu (Odoo › Settings, Sync Log) when <c>AdminMenuCreatedEvent</c> fires.
/// </summary>
/// <remarks>
/// <see cref="BaseAdminMenuCreatedEventConsumer"/> checks that the plugin is installed before calling
/// <see cref="GetAdminMenuItemAsync"/>, so the menu goes with the plugin.
/// </remarks>
public class EventConsumer : BaseAdminMenuCreatedEventConsumer
{
    #region Fields

    protected readonly ILocalizationService _localizationService;

    #endregion

    #region Ctor

    public EventConsumer(
        IPluginManager<IPlugin> pluginManager,
        ILocalizationService localizationService)
        : base(pluginManager)
    {
        _localizationService = localizationService;
    }

    #endregion

    #region Overrides

    /// <inheritdoc />
    protected override async Task<AdminMenuItem> GetAdminMenuItemAsync(IPlugin plugin)
    {
        var settingsMenu = new AdminMenuItem
        {
            Title = await _localizationService.GetResourceAsync($"{OdooLocaleResources.Prefix}.Menu.Settings"),
            Url = $"/Admin/{OdooConstants.AdminController}/Configure",
            SystemName = OdooConstants.SettingsMenuSystemName,
            Visible = true,
            IconClass = "far fa-dot-circle",
        };
        var syncLogMenu = new AdminMenuItem
        {
            Title = await _localizationService.GetResourceAsync($"{OdooLocaleResources.Prefix}.Menu.SyncLogs"),
            Url = $"/Admin/{OdooConstants.AdminController}/SyncLogs",
            SystemName = OdooConstants.SyncLogsMenuSystemName,
            Visible = true,
            IconClass = "far fa-list-alt",
        };

        var menu = new AdminMenuItem
        {
            Visible = true,
            SystemName = plugin.PluginDescriptor.SystemName,
            Title = await _localizationService.GetResourceAsync($"{OdooLocaleResources.Prefix}.Menu.Root"),
            IconClass = "fa fa-exchange",
        };

        menu.ChildNodes.Add(settingsMenu);
        menu.ChildNodes.Add(syncLogMenu);

        return menu;
    }

    /// <inheritdoc />
    protected override string PluginSystemName => OdooConstants.SystemName;

    /// <inheritdoc />
    protected override MenuItemInsertType InsertType => MenuItemInsertType.TryBeforeThanAfter;

    /// <inheritdoc />
    protected override string BeforeMenuSystemName => "Catalog";

    #endregion
}
