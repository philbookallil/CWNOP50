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

    /// <summary>Admin menu item of the Orders page.</summary>
    public const string OrdersMenuSystemName = "Odoo.Integration.Orders";

    /// <summary>
    /// Prefix of the Odoo link kept in <c>ManufacturerPartNumber</c>: <c>ODOO_{template id}</c> on a product,
    /// <c>ODOO_{product.product id}</c> on a combination.
    /// </summary>
    public const string OdooLinkPrefix = "ODOO_";

    /// <summary>
    /// Generic attribute on a product whose Odoo template has exactly one variant: that variant's
    /// <c>product.product</c> id, which an order line needs. A product with variants keeps the id on each
    /// combination instead.
    /// </summary>
    public const string ProductVariantIdAttribute = "Odoo.ProductVariantId";
}
