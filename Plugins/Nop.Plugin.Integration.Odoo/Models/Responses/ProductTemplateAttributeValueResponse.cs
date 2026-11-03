using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// Represents a product template attribute value from Odoo.
/// This model represents the combination of product attribute values and their template-specific properties.
/// </summary>
public class ProductTemplateAttributeValueResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("ptav_active")]
    public bool PtavActive { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("product_attribute_value_id")]
    public Many2One ProductAttributeValue { get; set; }

    [JsonPropertyName("attribute_line_id")]
    public Many2One AttributeLine { get; set; }

    [JsonPropertyName("price_extra")]
    public double PriceExtra { get; set; }

    [JsonPropertyName("currency_id")]
    public Many2One Currency { get; set; }

    [JsonPropertyName("exclude_for")]
    public List<int> ExcludeFor { get; set; }

    [JsonPropertyName("product_tmpl_id")]
    public Many2One ProductTemplate { get; set; }

    [JsonPropertyName("attribute_id")]
    public Many2One Attribute { get; set; }

    [JsonPropertyName("ptav_product_variant_ids")]
    public List<int> PtavProductVariantIds { get; set; }

    [JsonPropertyName("html_color")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string HtmlColor { get; set; }

    [JsonPropertyName("is_custom")]
    public bool IsCustom { get; set; }

    [JsonPropertyName("display_type")]
    public string DisplayType { get; set; }

    [JsonPropertyName("color")]
    public int Color { get; set; }

    [JsonPropertyName("image")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Image { get; set; }

    [JsonPropertyName("create_uid")]
    public Many2One CreateUser { get; set; }

    [JsonPropertyName("create_date")]
    public string CreateDate { get; set; }

    [JsonPropertyName("write_uid")]
    public Many2One WriteUser { get; set; }

    [JsonPropertyName("write_date")]
    public string WriteDate { get; set; }
}