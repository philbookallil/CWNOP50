using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// Maps the fields returned by the eStore addon's <c>estore.department</c> model
/// (endpoint <c>/json/2/estore.department/search_read</c>).
/// </summary>
public class OdooDepartmentResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    /// <summary>
    /// The parent store as a Many2One [id, name] pair (→ estore.store). Authoritative parent link.
    /// False (→ null) when not set.
    /// </summary>
    [JsonPropertyName("store_id")]
    public Many2One Store { get; set; }

    [JsonPropertyName("estore_sync_revision")]
    public int SyncRevision { get; set; }
}
