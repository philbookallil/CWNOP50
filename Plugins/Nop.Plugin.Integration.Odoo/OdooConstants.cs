namespace Nop.Plugin.Integration.Odoo;

/// <summary>
/// Shared constants for the Odoo Integration plugin.
/// </summary>
public static class OdooConstants
{
    /// <summary>Named HttpClient registered in DI.</summary>
    public const string HttpClientName = "OdooIntegrationClient";

    /// <summary>Admin controller name.</summary>
    public const string AdminController = "OdooAdmin";

    /// <summary>Folder of the admin controller's views.</summary>
    public const string ViewsPath = "~/Plugins/Integration.Odoo/Views/OdooAdmin/";

    /// <summary>Plugin system name matching plugin.json.</summary>
    public const string SystemName = "Integration.Odoo";

    /// <summary>Admin menu item of the Configure page.</summary>
    public const string SettingsMenuSystemName = "Odoo.Integration.Settings";

    /// <summary>Admin menu item of the Sync Log page.</summary>
    public const string SyncLogsMenuSystemName = "Odoo.Integration.SyncLogs";
}
