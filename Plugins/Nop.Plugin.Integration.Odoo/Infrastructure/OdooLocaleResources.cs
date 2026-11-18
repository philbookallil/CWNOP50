namespace Nop.Plugin.Integration.Odoo.Infrastructure;

/// <summary>
/// The plugin's locale resources, installed by <see cref="OdooIntegrationPlugin"/> on install and on
/// a DATA version update.
/// </summary>
public static class OdooLocaleResources
{
    /// <summary>
    /// Prefix of every resource key the plugin owns; uninstall deletes by it.
    /// </summary>
    public const string Prefix = "Integration.Odoo";

    /// <summary>
    /// The prefix the resources were first installed under; the plugin update deletes what is left under it.
    /// </summary>
    public const string PreviousPrefix = "Plugins.Integration.Odoo";

    /// <summary>
    /// Key → English text.
    /// </summary>
    public static Dictionary<string, string> All => new()
    {
        // Admin menu
        [$"{Prefix}.Menu.Root"] = "Odoo",
        [$"{Prefix}.Menu.Settings"] = "Settings",
        [$"{Prefix}.Menu.Orders"] = "Orders",
        [$"{Prefix}.Menu.SyncLogs"] = "Sync Log",

        // Configure page
        [$"{Prefix}.Fields.OdooUrl"] = "Odoo URL",
        [$"{Prefix}.Fields.OdooUrl.Hint"] = "Enter the base URL of your Odoo instance, e.g. https://mycompany.odoo.com",
        [$"{Prefix}.Fields.DatabaseName"] = "Database Name",
        [$"{Prefix}.Fields.DatabaseName.Hint"] = "The Odoo database name. For SaaS this is the subdomain, e.g. mycompany",
        [$"{Prefix}.Fields.Username"] = "Username",
        [$"{Prefix}.Fields.Username.Hint"] = "The email address of the Odoo API user",
        [$"{Prefix}.Fields.ApiKey"] = "API Key",
        [$"{Prefix}.Fields.ApiKey.Hint"] = "Generate an API key in Odoo: Settings → Technical → API Keys",
        [$"{Prefix}.Fields.DeliveryProductCode"] = "Odoo delivery product",
        [$"{Prefix}.Fields.DeliveryProductCode.Hint"] = "Internal reference of the Odoo product used for shipping lines, e.g. Delivery_007.",
        [$"{Prefix}.Fields.ExchangeFeeProductCode"] = "Odoo exchange fee product",
        [$"{Prefix}.Fields.ExchangeFeeProductCode.Hint"] = "Internal reference of the Odoo service product used for exchange fee lines.",

        // Orders page
        [$"{Prefix}.Orders.List.Store"] = "Store",
        [$"{Prefix}.Orders.List.CreatedFrom"] = "Placed from",
        [$"{Prefix}.Orders.List.CreatedTo"] = "Placed to",
        [$"{Prefix}.Orders.List.Status"] = "Odoo sync",
        [$"{Prefix}.Orders.List.PaymentStatus"] = "Payment status",
        [$"{Prefix}.Orders.CancelConfirm"] = "The order's sales orders in Odoo will be cancelled. This cannot be undone from nop.",

        // Sync events grid
        [$"{Prefix}.LogEvent.List.EventType"] = "Event type",
        [$"{Prefix}.LogEvent.Fields.Name"] = "Name",
        [$"{Prefix}.LogEvent.Fields.EventType"] = "Event type",
        [$"{Prefix}.LogEvent.Fields.StartedOn"] = "Started on",
        [$"{Prefix}.LogEvent.Fields.FinishedOn"] = "Finished on",
        [$"{Prefix}.LogEvent.Fields.TraceCount"] = "Traces",
        [$"{Prefix}.LogEvent.Fields.HasErrors"] = "Errors",

        // Sync traces
        [$"{Prefix}.Log.List.CreatedOnFrom"] = "Created from",
        [$"{Prefix}.Log.List.CreatedOnTo"] = "Created to",
        [$"{Prefix}.Log.List.Message"] = "Message",
        [$"{Prefix}.Log.List.LogLevel"] = "Log level",
        [$"{Prefix}.Log.Fields.LogLevel"] = "Log level",
        [$"{Prefix}.Log.Fields.ShortMessage"] = "Short message",
        [$"{Prefix}.Log.Fields.FullMessage"] = "Full message",
        [$"{Prefix}.Log.Fields.EntityType"] = "Entity type",
        [$"{Prefix}.Log.Fields.OdooEntityId"] = "Odoo entity ID",
        [$"{Prefix}.Log.Fields.NopEntityId"] = "Nop entity ID",
        [$"{Prefix}.Log.Fields.CreatedOn"] = "Created on",
    };
}
