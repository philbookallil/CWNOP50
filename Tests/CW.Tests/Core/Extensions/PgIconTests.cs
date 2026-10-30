using AwesomeAssertions;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Moq;
using Nop.Plugin.CW.Core.Extensions;
using NUnit.Framework;

namespace CW.Tests.Core.Extensions;

/// <summary>
/// A product group's icon, as the catalogue, the subsidy bar, the cart and My Subsidies draw it. Its icon and colour are
/// typed in the admin product group form, so only a sprite symbol id and a hex colour reach the page.
/// </summary>
[TestFixture]
public class PgIconTests
{
    private static string Render(string? icon, string? colour) =>
        new Mock<IHtmlHelper>().Object.PgIcon(icon, "extra", colour) is HtmlString html ? html.Value ?? string.Empty : string.Empty;

    [Test]
    public void A_symbol_id_and_a_hex_colour_draw_the_icon_in_that_colour()
    {
        Render("pg-icon-Shirts", "#3a7bd5").Should()
            .Be("""<svg class="pg-icon extra" aria-hidden="true" style="color:#3a7bd5" ><use href="/Plugins/CW.Core/Content/icons/pg-icons.svg#pg-icon-Shirts"/></svg>""");
    }

    [Test]
    public void A_short_hex_colour_is_written_out()
    {
        Render("pg-icon-Shirts", "#f00").Should().Contain("""style="color:#ff0000" """);
    }

    [TestCase("red")]
    [TestCase("#123;background:url(x)")]
    [TestCase("\"><script>alert(1)</script>")]
    public void A_colour_that_is_not_hex_is_left_out(string colour)
    {
        var html = Render("pg-icon-Shirts", colour);

        html.Should().NotContain("style=").And.NotContain("<script").And.Contain("#pg-icon-Shirts");
    }

    [TestCase("pg-icon\"/><script>alert(1)</script>")]
    [TestCase("pg icon")]
    [TestCase("")]
    [TestCase(null)]
    public void An_icon_that_is_not_a_symbol_id_draws_nothing(string? icon)
    {
        Render(icon, "#3a7bd5").Should().BeEmpty();
    }
}
