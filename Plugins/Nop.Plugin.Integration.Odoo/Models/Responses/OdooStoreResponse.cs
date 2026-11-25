using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// Maps the fields returned by the eStore addon's <c>estore.store</c> model
/// (endpoint <c>/json/2/estore.store/search_read</c>).
/// </summary>
public class OdooStoreResponse
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
    /// Change counter — used for incremental sync (skip when unchanged).
    /// </summary>
    [JsonPropertyName("estore_sync_revision")]
    public int SyncRevision { get; set; }

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
    [JsonPropertyName("url")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Url { get; set; }

    /// <summary>
    /// Many-to-many ids of this store's departments (estore.department).
    /// </summary>
    [JsonPropertyName("department_ids")]
    public List<int> DepartmentIds { get; set; } = new();

    /// <summary>
    /// Ids of the customers linked directly to this store (res.partner), not the people under them. Branch sync source.
    /// </summary>
    [JsonPropertyName("partner_link_ids")]
    public List<int> CompanyIds { get; set; } = new();
}
