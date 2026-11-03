using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// Represents a product attribute from Odoo.
/// This model includes all attribute values, template values, and related product templates.
/// </summary>
public class ProductAttributeResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("create_variant")]
    public string CreateVariant { get; set; }

    [JsonPropertyName("display_type")]
    public string DisplayType { get; set; }

    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("value_ids")]
    public List<int> ValueIds { get; set; }

    [JsonPropertyName("template_value_ids")]
    public List<int> TemplateValueIds { get; set; }

    [JsonPropertyName("attribute_line_ids")]
    public List<int> AttributeLineIds { get; set; }

    [JsonPropertyName("product_tmpl_ids")]
    public List<int> ProductTemplateIds { get; set; }

    [JsonPropertyName("number_related_products")]
    public int NumberRelatedProducts { get; set; }

    [JsonPropertyName("create_uid")]
    public Many2One CreateUser { get; set; }

    [JsonPropertyName("create_date")]
    public string CreateDate { get; set; }

    [JsonPropertyName("write_uid")]
    public Many2One WriteUser { get; set; }

    [JsonPropertyName("write_date")]
    public string WriteDate { get; set; }

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; }

    [JsonPropertyName("preview_variants")]
    public string PreviewVariants { get; set; }

    [JsonPropertyName("is_thumbnail_visible")]
    public bool IsThumbnailVisible { get; set; }

    [JsonPropertyName("category_id")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string CategoryId { get; set; }
}