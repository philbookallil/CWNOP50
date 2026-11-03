using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Integration.Odoo.Models;

/// <summary>
/// View model for the Odoo connector configuration page in the NopCommerce admin area.
/// </summary>
public record ConfigurationModel : BaseNopModel
{
    /// <summary>
    /// The base URL of the Odoo instance (e.g. https://mycompany.odoo.com).
    /// </summary>
    [NopResourceDisplayName("Plugins.Integration.Odoo.Fields.OdooUrl")]
    [Required(ErrorMessage = "Odoo URL is required")]
    [Url(ErrorMessage = "Please enter a valid URL")]
    public string OdooUrl { get; init; } = string.Empty;

    /// <summary>
    /// The Odoo database name. For SaaS this is the subdomain portion.
    /// </summary>
    [NopResourceDisplayName("Plugins.Integration.Odoo.Fields.DatabaseName")]
    [Required(ErrorMessage = "Database name is required")]
    public string DatabaseName { get; init; } = string.Empty;

    /// <summary>
    /// The Odoo user login / email used to identify the API user.
    /// </summary>
    [NopResourceDisplayName("Plugins.Integration.Odoo.Fields.Username")]
    [Required(ErrorMessage = "Username is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// The API key from Odoo Settings → Technical → API Keys.
    /// Stored encrypted. The UI shows a masked placeholder when already saved.
    /// </summary>
    [NopResourceDisplayName("Plugins.Integration.Odoo.Fields.ApiKey")]
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// True when an API key is already persisted (UI renders a "Change key" pattern).
    /// </summary>
    public bool HasApiKey { get; init; }


    public DateTime? LastSyncDateTime { get; set; }

    public string ConnectionStatus { get; set; }

    public string TestResultMessage { get; set; }
}
