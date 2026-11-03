using Nop.Core.Configuration;

namespace Nop.Plugin.Integration.Odoo.Domain;

/// <summary>
/// Global settings for the Odoo Integration plugin.
/// Stored via NopCommerce's ISettingService and scoped globally (StoreId = 0).
/// </summary>
public class OdooSettings : ISettings
{
    /// <summary>
    /// The base URL of the Odoo instance, e.g. https://mycompany.odoo.com
    /// </summary>
    public string OdooUrl { get; set; } = string.Empty;

    /// <summary>
    /// The Odoo database name (for SaaS this is the subdomain, e.g. mycompany).
    /// Required as the X-Odoo-Database header when the instance hosts multiple databases.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// The Odoo login / username (email address) of the API user.
    /// Stored for reference only – the JSON-2 API authenticates via API key, not username+password.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// API key generated from the Odoo user profile
    /// (Settings → Technical → API Keys).
    /// Transmitted as the Authorization: bearer {ApiKey} HTTP header.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;


    public DateTime? LastSyncDateTime { get; set; }

}
