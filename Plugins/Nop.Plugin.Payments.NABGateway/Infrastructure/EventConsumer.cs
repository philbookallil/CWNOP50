using Nop.Plugin.Payments.NABGateway.Services.Security;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Payments.NABGateway.Infrastructure;

/// <summary>
/// Adds the NAB XML payment report items to the admin menu.
/// </summary>
public class EventConsumer : BaseAdminMenuCreatedEventConsumer, IConsumer<AdminMenuCreatedEvent>
{
    #region Fields

    private readonly IPermissionService _permissionService;
    private readonly ILocalizationService _localizationService;
    private readonly IAdminMenu _adminMenu;

    #endregion

    #region Ctor

    public EventConsumer(IPluginManager<IPlugin> pluginManager,
        IPermissionService permissionService,
        ILocalizationService localizationService,
        IAdminMenu adminMenu) : base(pluginManager)
    {
        _permissionService = permissionService;
        _localizationService = localizationService;
        _adminMenu = adminMenu;
    }

    #endregion

    #region Utilities

    protected override string PluginSystemName => NABXMLPaymentDefaults.SystemName;

    // Place the plugin's menu group just before the standard "Reports" menu.
    protected override string BeforeMenuSystemName => "Reports";

    protected override MenuItemInsertType InsertType => MenuItemInsertType.Before;

    protected override async Task<bool> CheckAccessAsync()
    {
        return await _permissionService.AuthorizeAsync(NABXMLPaymentPermissionConfigManager.ACCESS_REPORTS);
    }

    protected override async Task<AdminMenuItem> GetAdminMenuItemAsync(IPlugin plugin)
    {
        return new AdminMenuItem
        {
            SystemName = "Payments.NABGateway.Reports",
            Title = await _localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Reports.Menu"),
            IconClass = "fa fa-credit-card",
            Visible = true,
            ChildNodes = new List<AdminMenuItem>
            {
                new()
                {
                    Visible = true,
                    SystemName = "Payments.NABGateway.Reports.Payments",
                    Title = await _localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Reports.Payments.Menu"),
                    Url = _adminMenu.GetMenuItemUrl("NABXMLTransaction", "Payments"),
                    IconClass = "far fa-circle",
                    PermissionNames = new List<string> { NABXMLPaymentPermissionConfigManager.ACCESS_REPORTS }
                },
                new()
                {
                    Visible = true,
                    SystemName = "Payments.NABGateway.Reports.Failed",
                    Title = await _localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Reports.Failed.Menu"),
                    Url = _adminMenu.GetMenuItemUrl("NABXMLTransaction", "Failed"),
                    IconClass = "far fa-circle",
                    PermissionNames = new List<string> { NABXMLPaymentPermissionConfigManager.ACCESS_REPORTS }
                }
            }
        };
    }

    #endregion
}
