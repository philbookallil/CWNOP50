using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// Represents a product template attribute line from Odoo.
/// </summary>
public class ProductTemplateAttributeLineResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("product_tmpl_id")]
    public Many2One ProductTemplate { get; set; }

    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("attribute_id")]
    public Many2One Attribute { get; set; }

    [JsonPropertyName("value_ids")]
    public List<int> ValueIds { get; set; }

    [JsonPropertyName("value_count")]
    public int ValueCount { get; set; }

    [JsonPropertyName("product_template_value_ids")]
    public List<int> ProductTemplateValueIds { get; set; }

    [JsonPropertyName("create_uid")]
    public Many2One CreateUser { get; set; }

    [JsonPropertyName("create_date")]
    public string CreateDate { get; set; }

    [JsonPropertyName("write_uid")]
    public Many2One WriteUser { get; set; }

    [JsonPropertyName("write_date")]
    public string WriteDate { get; set; }
}