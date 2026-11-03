using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;


[JsonConverter(typeof(OdooMany2OneConverter))]
public class Many2One
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class OdooMany2OneConverter : JsonConverter<Many2One>
{
    public override Many2One Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.False)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected array for Many2One field.");

        reader.Read(); // move to first element (id)
        int id = reader.GetInt32();

        reader.Read(); // move to second element (name)
        string name = reader.GetString();

        reader.Read(); // move past EndArray
        return new Many2One { Id = id, Name = name };
    }

    public override void Write(Utf8JsonWriter writer, Many2One value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Id);
        writer.WriteStringValue(value.Name);
        writer.WriteEndArray();
    }
}
