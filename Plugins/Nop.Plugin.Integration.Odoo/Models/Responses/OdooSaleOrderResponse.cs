using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

/// <summary>
/// The fields of an Odoo <c>sale.order</c> the order send reads back.
/// </summary>
public class OdooSaleOrderResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// The order reference, e.g. S00042.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Name { get; set; }

    /// <summary>
    /// draft, sent, sale or cancel.
    /// </summary>
    [JsonPropertyName("state")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string State { get; set; }

    /// <summary>
    /// The order total before tax.
    /// </summary>
    [JsonPropertyName("amount_untaxed")]
    public decimal AmountUntaxed { get; set; }
}
