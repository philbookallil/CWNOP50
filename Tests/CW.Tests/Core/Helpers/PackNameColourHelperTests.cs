using AwesomeAssertions;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The colour a packname is shown in: only a hex colour is kept, because the storefront selector writes it into HTML; and
/// the text colour that reads on it
/// </summary>
[TestFixture]
public class PackNameColourHelperTests
{
    [TestCase("#1A2B3C", "#1A2B3C")]
    [TestCase("  #a0b1c2 ", "#a0b1c2")]
    [TestCase("#F0a", "#FF00aa")]
    public void A_hex_colour_is_kept(string entered, string kept)
    {
        PackNameColourHelper.Normalize(entered).Should().Be(kept);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("red")]
    [TestCase("#12345")]
    [TestCase("#1234567")]
    [TestCase("#GGGGGG")]
    [TestCase("#123456\"><img src=x onerror=alert(1)>")]
    [TestCase("#123456;background:url(x)")]
    public void Anything_else_is_refused(string? entered)
    {
        PackNameColourHelper.Normalize(entered!).Should().BeNull();
    }

    [TestCase("#FFFFFF", "#000")]
    [TestCase("#E8D44D", "#000")]
    [TestCase("#F05A28", "#000")]
    [TestCase("#000000", "#FFF")]
    [TestCase("#1F3A93", "#FFF")]
    [TestCase("#333333", "#FFF")]
    [TestCase("#fff", "#000")]
    public void Text_on_a_packname_colour_is_black_on_light_and_white_on_dark(string colour, string text)
    {
        PackNameColourHelper.ForegroundFor(colour).Should().Be(text);
    }

    [TestCase(null)]
    [TestCase("red")]
    [TestCase("#12;background:url(x)")]
    public void Text_on_a_colour_that_is_not_hex_is_black(string? colour)
    {
        //the admin pages save a packname's colour as typed; it used to be parsed with ColorTranslator, which throws
        PackNameColourHelper.ForegroundFor(colour!).Should().Be("#000");
    }
}
