using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// Maps the fields returned by Odoo's custom Studio <c>x_store</c> model
/// (endpoint <c>/json/2/x_store/search_read</c>).
/// </summary>
public class OdooStoreResponse
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
    /// Change counter — used for incremental sync (skip when unchanged).
    /// </summary>
    [JsonPropertyName("x_studio_sync_revision")]
    public int XStudioSyncRevision { get; set; }

    /// <summary>
    /// Odoo's last-modified timestamp (UTC, "yyyy-MM-dd HH:mm:ss"), updated by the ORM on every write.
    /// Used with the revision for change detection, as an opaque token.
    /// </summary>
    [JsonPropertyName("write_date")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string WriteDate { get; set; }

    /// <summary>
    /// Store URL. May be false when not yet set.
    /// </summary>
    [JsonPropertyName("x_studio_store_url")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string XStudioStoreUrl { get; set; }

    /// <summary>
    /// Many-to-many ids of this store's departments (x_department).
    /// </summary>
    [JsonPropertyName("x_studio_departments")]
    public List<int> XStudioDepartments { get; set; } = new();

    /// <summary>
    /// Many-to-many ids of this store's companies (res.partner). Branch sync source.
    /// </summary>
    [JsonPropertyName("x_studio_companies")]
    public List<int> XStudioCompanies { get; set; } = new();
}
