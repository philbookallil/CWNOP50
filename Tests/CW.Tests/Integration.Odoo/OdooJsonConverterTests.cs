using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Nop.Plugin.Integration.Odoo.Models.Responses;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// Odoo's JSON-2 API answers <c>false</c> for an empty field of any type, a pair <c>[id, name]</c> for a
/// many-to-one, and some binary fields as an object. The converters read all of these; an unexpected shape
/// throws a <see cref="JsonException"/> that carries the JSON path, which is what the sync log reports.
/// </summary>
[TestFixture]
public class OdooJsonConverterTests
{
    #region Utilities

    private sealed class Probe
    {
        [JsonPropertyName("text")]
        [JsonConverter(typeof(StringOrFalseJsonConverter))]
        public string? Text { get; set; }

        [JsonPropertyName("image")]
        [JsonConverter(typeof(OdooBinaryJsonConverter))]
        public string? Image { get; set; }

        [JsonPropertyName("count")]
        [JsonConverter(typeof(IntOrFalseJsonConverter))]
        public int? Count { get; set; }

        [JsonPropertyName("price")]
        [JsonConverter(typeof(DoubleOrFalseJsonConverter))]
        public double? Price { get; set; }

        [JsonPropertyName("company")]
        public Many2One? Company { get; set; }
    }

    private static Probe Read(string json) => JsonSerializer.Deserialize<Probe>(json)!;

    #endregion

    #region Reading

    [Test]
    public void Values_are_read_as_given()
    {
        var probe = Read("""{ "text": "Polo", "image": "QUJD", "count": 3, "price": 12.5, "company": [7, "Head Office"] }""");

        probe.Text.Should().Be("Polo");
        probe.Image.Should().Be("QUJD");
        probe.Count.Should().Be(3);
        probe.Price.Should().Be(12.5);
        probe.Company!.Id.Should().Be(7);
        probe.Company.Name.Should().Be("Head Office");
    }

    [Test]
    public void False_reads_as_null_for_every_type()
    {
        var probe = Read("""{ "text": false, "image": false, "count": false, "price": false, "company": false }""");

        probe.Text.Should().BeNull();
        probe.Image.Should().BeNull();
        probe.Count.Should().BeNull();
        probe.Price.Should().BeNull();
        probe.Company.Should().BeNull();
    }

    [Test]
    public void A_binary_field_sent_as_an_object_reads_as_its_content()
    {
        Read("""{ "image": { "filename": "a.png", "content": "QUJD", "size": 3 } }""").Image.Should().Be("QUJD");
    }

    [Test]
    public void A_binary_object_without_content_reads_as_null()
    {
        Read("""{ "image": { "filename": "a.png" } }""").Image.Should().BeNull();
    }

    [Test]
    public void A_whole_number_price_reads_as_a_double()
    {
        Read("""{ "price": 20 }""").Price.Should().Be(20d);
    }

    [Test]
    public void An_unexpected_shape_throws_with_the_json_path()
    {
        var act = () => Read("""{ "text": 42 }""");

        act.Should().Throw<JsonException>().Which.Path.Should().Be("$.text");
    }

    [Test]
    public void A_many_to_one_that_is_not_a_pair_throws()
    {
        var act = () => Read("""{ "company": 7 }""");

        act.Should().Throw<JsonException>();
    }

    #endregion
}
