using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// Maps the fields returned by Odoo's custom Studio <c>x_department</c> model
/// (endpoint <c>/json/2/x_department/search_read</c>).
/// </summary>
public class OdooDepartmentResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("x_name")]
    public string XName { get; set; }

    [JsonPropertyName("x_active")]
    public bool XActive { get; set; }

    [JsonPropertyName("x_studio_sequence")]
    public int XStudioSequence { get; set; }

    /// <summary>
    /// The parent store as a Many2One [id, name] pair (→ x_store). Authoritative parent link.
    /// False (→ null) when not set.
    /// </summary>
    [JsonPropertyName("x_studio_store")]
    public Many2One XStudioStore { get; set; }

    [JsonPropertyName("x_studio_sync_revision")]
    public int XStudioSyncRevision { get; set; }
}
