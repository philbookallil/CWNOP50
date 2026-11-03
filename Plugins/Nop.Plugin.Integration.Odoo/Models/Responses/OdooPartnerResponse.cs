using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// Maps the fields returned by Odoo's res.partner model used for companies,
/// contacts, and delivery addresses.
/// </summary>
public class OdooPartnerResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// "company" = top-level company, "person" = individual contact.
    /// Delivery addresses have company_type = "person" but belong to a parent company.
    /// </summary>
    [JsonPropertyName("company_type")]
    public string CompanyType { get; set; }

    /// <summary>
    /// The type of address: "contact", "delivery", "invoice", "other", "private".
    /// Delivery branches will have type = "delivery".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Parent company partner ID. Delivery addresses reference their parent company here.
    /// </summary>
    [JsonPropertyName("parent_id")]
    public Many2One ParentId { get; set; }

    /// <summary>
    /// IDs of child contacts/addresses belonging to this partner.
    /// </summary>
    [JsonPropertyName("child_ids")]
    public List<int> ChildIds { get; set; } = new();

    [JsonPropertyName("street")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Street { get; set; }

    [JsonPropertyName("street2")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Street2 { get; set; }

    [JsonPropertyName("city")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string City { get; set; }

    [JsonPropertyName("zip")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Zip { get; set; }

    /// <summary>
    /// State/province as a Many2One [id, name] pair. False when not set.
    /// </summary>
    [JsonPropertyName("state_id")]
    public Many2One StateId { get; set; }

    /// <summary>
    /// Country as a Many2One [id, name] pair. False when not set.
    /// </summary>
    [JsonPropertyName("country_id")]
    public Many2One CountryId { get; set; }

    [JsonPropertyName("phone")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Phone { get; set; }

    [JsonPropertyName("email")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Email { get; set; }

    [JsonPropertyName("ref")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Ref { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("is_company")]
    public bool IsCompany { get; set; }

    [JsonPropertyName("properties")]
    public List<OdooPartnerProperty> Properties { get; set; } = new();
}

/// <summary>
/// Represents a single entry in the Odoo partner "properties" list.
/// Each property is a dynamic field definition with its current value.
/// </summary>
public class OdooPartnerProperty
{
    [JsonPropertyName("ai")]
    public bool Ai { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("string")]
    public string Label { get; set; }

    [JsonPropertyName("default")]
    public object Default { get; set; }

    [JsonPropertyName("ai_domain")]
    public List<object> AiDomain { get; set; } = new();

    /// <summary>
    /// Selection options as a list of [key, label] pairs.
    /// </summary>
    [JsonPropertyName("selection")]
    public List<List<string>> Selection { get; set; } = new();

    [JsonPropertyName("view_in_cards")]
    public bool ViewInCards { get; set; }

    [JsonPropertyName("value")]
    public object Value { get; set; }
}
