using AwesomeAssertions;
using Nop.Plugin.Integration.Odoo.Services;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// What the sync log and the Orders page show when Odoo refuses a call: Odoo's own message, in either error shape.
/// </summary>
[TestFixture]
public class OdooApiErrorTests
{
    [Test]
    public void The_access_check_names_the_fields_Odoo_does_not_list()
    {
        using var answer = System.Text.Json.JsonDocument.Parse("""{"name": {"type": "char"}, "active": {"type": "boolean"}}""");

        OdooApiService.MissingFields(new[] { "name", "is_published", "active" }, answer.RootElement)
            .Should().Equal("is_published");
    }

    [Test]
    public void A_JSON2_error_gives_its_top_level_message()
    {
        const string body = """
            {"name": "odoo.exceptions.ValidationError", "message": "Not enough stock for POLO (S)",
             "arguments": ["Not enough stock for POLO (S)"], "context": {}, "debug": "Traceback ..."}
            """;

        OdooApiService.ParseOdooError(body).Should().Be("Not enough stock for POLO (S)");
    }

    [Test]
    public void A_JSON_RPC_error_gives_the_nested_message()
    {
        OdooApiService.ParseOdooError("""{"error": {"message": "Odoo Server Error", "data": {"message": "Access denied"}}}""")
            .Should().Be("Odoo Server Error");

        OdooApiService.ParseOdooError("""{"error": {"data": {"message": "Access denied"}}}""")
            .Should().Be("Access denied");
    }

    [Test]
    public void A_body_that_is_not_an_error_gives_null()
    {
        OdooApiService.ParseOdooError("[1, 2]").Should().BeNull();
        OdooApiService.ParseOdooError("{}").Should().BeNull();
        OdooApiService.ParseOdooError("<html>Bad gateway</html>").Should().BeNull();
    }
}
