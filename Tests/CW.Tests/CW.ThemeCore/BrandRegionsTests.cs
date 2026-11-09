using AwesomeAssertions;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// The region map decides what a tweak on a selected preview element may change, for the offline generator and for
/// the AI answer alike. Every region the Configure page offers must be in it, or a selected element's tweak would
/// be read as a tweak of the whole style.
/// </summary>
[TestFixture]
public class BrandRegionsTests
{
    [TestCase("header", BrandField.PrimaryColor)]
    [TestCase("nav", BrandField.PrimaryColor)]
    [TestCase("link", BrandField.PrimaryColor)]
    [TestCase("hero", BrandField.PrimaryTintColor)]
    [TestCase("tint", BrandField.PrimaryTintColor)]
    [TestCase("danger", BrandField.DangerColor)]
    [TestCase("success", BrandField.SuccessColor)]
    [TestCase("warning", BrandField.WarningColor)]
    [TestCase("dark", BrandField.DarkColor)]
    [TestCase("footer", BrandField.DarkColor)]
    public void A_colour_region_changes_only_its_colour(string region, BrandField field)
    {
        BrandRegions.GetFields(region).Should().Equal(field);
        BrandRegions.GetColorField(region).Should().Be(field);
    }

    [Test]
    public void Buttons_change_the_primary_colour_and_the_radius()
    {
        BrandRegions.GetFields("button").Should().Equal(BrandField.PrimaryColor, BrandField.ButtonRadius);
        BrandRegions.GetColorField("button").Should().Be(BrandField.PrimaryColor);
    }

    [TestCase(" Button ")]
    [TestCase("BUTTON")]
    public void A_region_is_matched_whatever_its_case_and_spacing(string region)
    {
        BrandRegions.GetFields(region).Should().NotBeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("sidebar")]
    public void An_empty_or_unknown_region_has_no_fields(string? region)
    {
        BrandRegions.GetFields(region!).Should().BeNull();
        BrandRegions.GetColorField(region!).Should().BeNull();
    }

    [Test]
    public void Every_region_the_Configure_page_offers_is_mapped()
    {
        var page = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..",
            "Plugins", "Nop.Plugin.CW.ThemeCore", "Views", "Configure.cshtml"));
        var start = page.IndexOf("var regionConfig = {", StringComparison.Ordinal);
        var end = page.IndexOf("};", start, StringComparison.Ordinal);
        var regions = System.Text.RegularExpressions.Regex.Matches(page[start..end], @"^\s*(?:'(\w+)'|(\w+))\s*:", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
            .ToList();

        regions.Should().NotBeEmpty();
        regions.Where(r => BrandRegions.GetFields(r) is null).Should().BeEmpty();
    }
}
