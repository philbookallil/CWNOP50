using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

public class StringOrFalseJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // If the value is a string, return it
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        // If the value is false, return null
        if (reader.TokenType == JsonTokenType.False)
        {
            return null;
        }

        // Unexpected token type. No message on purpose: System.Text.Json only appends the JSON path
        // (e.g. "Path: $[3].description_sale") to a JsonException it builds itself, and without the
        // path the log cannot say which product and field Odoo returned in an unexpected shape.
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        // Write the string value or false if null
        if (value == null)
        {
            writer.WriteBooleanValue(false);
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

/// <summary>
/// Reads an Odoo binary field (e.g. <c>image_1920</c>) as its base64 content. Odoo returns either the
/// base64 string, <c>false</c> when empty, or — for some records — an object
/// <c>{ "filename": …, "content": "&lt;base64&gt;", "size": … }</c>; all three read as the base64 string
/// (or <c>null</c>), so consumers never see the difference.
/// </summary>
public class OdooBinaryJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.False:
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.StartObject:
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    return doc.RootElement.TryGetProperty("content", out var content)
                        && content.ValueKind == JsonValueKind.String
                            ? content.GetString()
                            : null;
                }

            default:
                // No message, so the exception carries the JSON path (see StringOrFalseJsonConverter).
                throw new JsonException();
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteBooleanValue(false);
        else
            writer.WriteStringValue(value);
    }
}

public class IntOrFalseJsonConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // If the value is a string, return it
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        // If the value is false, return null
        if (reader.TokenType == JsonTokenType.False)
        {
            return null;
        }

        // Unexpected token type — no message, so the exception carries the JSON path (see above).
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        // Write the string value or false if null
        if (value == null)
        {
            writer.WriteBooleanValue(false);
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}

public class DoubleOrFalseJsonConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // If the value is a string, return it
        if (reader.TokenType == JsonTokenType.Number)
        {

            if (reader.TryGetDouble(out double doubleValue))
            {
                return doubleValue;
            }

            if (reader.TryGetInt32(out int intValue))
            {
                return intValue;
            }
            if (reader.TryGetDecimal(out decimal decimalValue))
            {
                return (double)decimalValue;
            }

            // Handle cases where Odoo returns a float like 0.0 instead of 0
            return (double)0;

        }


        // If the value is false, return null
        if (reader.TokenType == JsonTokenType.False)
        {
            return null;
        }

        // Unexpected token type — no message, so the exception carries the JSON path (see above).
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        // Write the string value or false if null
        if (value == null)
        {
            writer.WriteBooleanValue(false);
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}