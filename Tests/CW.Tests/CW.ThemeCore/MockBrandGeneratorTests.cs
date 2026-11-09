using AwesomeAssertions;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// The Brand Designer's generator turns a plain-English prompt into a style: a preset chip by name, else the preset whose
/// keywords match best, else the current style or a colour derived from the prompt, then the prompt's tweaks (shape,
/// intensity, lightness, warmth), optionally scoped to the preview region the admin selected. Whatever the prompt, every
/// colour it returns is a six-digit hex the settings page and <c>:root</c> block accept.
/// </summary>
[TestFixture]
public class MockBrandGeneratorTests
{
    #region Utilities

    private readonly MockBrandGenerator _generator = new();

    private static BrandStyle Current() => new()
    {
        PrimaryColor = "3366cc",
        PrimaryTintColor = "eef2fa",
        SuccessColor = "2e7d32",
        DangerColor = "c62828",
        WarningColor = "ef6c00",
        DarkColor = "1a2333",
        ButtonRadius = 8
    };

    private static IEnumerable<string> Colours(BrandStyle s) =>
        [s.PrimaryColor, s.PrimaryTintColor, s.SuccessColor, s.DangerColor, s.WarningColor, s.DarkColor];

    private static (double H, double S, double L) Hsl(string hex)
    {
        var r = Convert.ToInt32(hex[..2], 16) / 255.0;
        var g = Convert.ToInt32(hex[2..4], 16) / 255.0;
        var b = Convert.ToInt32(hex[4..6], 16) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var d = max - min;
        if (d < 1e-9)
            return (0, 0, l);

        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        var h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60, s, l);
    }

    private static double HueDistance(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    private Task<BrandStyle> GenerateAsync(string prompt, BrandStyle? current = null, string? region = null)
        => Task.FromResult(_generator.Generate(prompt, current ?? Current(), region!));

    #endregion

    #region Presets

    [Test]
    public void Every_preset_has_a_name_keywords_and_six_hex_colours()
    {
        foreach (var preset in BrandPresets.All)
        {
            preset.Name.Should().NotBeNullOrWhiteSpace();
            preset.Keywords.Should().NotBeEmpty();
            Colours(BrandStyle.FromPalette(preset.Palette)).Should().AllSatisfy(c => c.Should().MatchRegex("^[0-9a-f]{6}$"));
        }
    }

    [Test]
    public async Task A_preset_name_returns_that_preset_unchanged_by_modifier_words_in_it()
    {
        var preset = BrandPresets.All.Single(p => p.Name == "Bold & techy");

        var style = await GenerateAsync("BOLD & TECHY");

        style.PrimaryColor.Should().Be(preset.Palette.PrimaryColor);
        style.ButtonRadius.Should().Be(preset.ButtonRadius);
    }

    [Test]
    public async Task Keywords_pick_the_best_matching_preset()
    {
        var preset = BrandPresets.All.Single(p => p.Name == "Earthy organic");

        var style = await GenerateAsync("an eco shop for organic, sustainable plant care");

        style.PrimaryColor.Should().Be(preset.Palette.PrimaryColor);
    }

    #endregion

    #region Base style

    [Test]
    public async Task Without_a_preset_match_the_current_style_is_the_base_and_is_not_changed_in_place()
    {
        var current = Current();

        var style = await GenerateAsync("something unrelated", current);

        style.Should().NotBeSameAs(current);
        style.PrimaryColor.Should().Be("3366cc");
        current.PrimaryColor.Should().Be("3366cc");
    }

    [Test]
    public async Task Without_usable_current_colours_the_same_prompt_always_gives_the_same_style()
    {
        var empty = new BrandStyle();

        var first = await GenerateAsync("zzqx", empty);
        var second = await GenerateAsync("zzqx", empty);

        first.PrimaryColor.Should().Be(second.PrimaryColor);
        Colours(first).Should().AllSatisfy(c => c.Should().MatchRegex("^[0-9a-f]{6}$"));
    }

    [Test]
    public void Random_always_gives_valid_colours()
    {
        for (var i = 0; i < 50; i++)
            Colours(_generator.Random()).Should().AllSatisfy(c => c.Should().MatchRegex("^[0-9a-f]{6}$"));
    }

    #endregion

    #region Tweaks

    [TestCase("pill buttons please", 999)]
    [TestCase("make it square", 2)]
    [TestCase("softly rounded corners", 12)]
    public async Task Shape_words_set_the_button_radius(string prompt, int radius)
    {
        (await GenerateAsync(prompt)).ButtonRadius.Should().Be(radius);
    }

    //the tweak prompts below avoid preset keywords: a keyword anywhere in the prompt switches the base to that preset
    //first

    [Test]
    public async Task Deeper_lowers_the_primary_lightness_and_keeps_the_tint_in_its_hue()
    {
        var style = await GenerateAsync("a bit deeper");

        Hsl(style.PrimaryColor).L.Should().BeLessThan(Hsl("3366cc").L);
        HueDistance(Hsl(style.PrimaryTintColor).H, Hsl(style.PrimaryColor).H).Should().BeLessThan(6);
    }

    [Test]
    public async Task Muted_lowers_the_saturation()
    {
        var style = await GenerateAsync("more muted");

        Hsl(style.PrimaryColor).S.Should().BeLessThan(Hsl("3366cc").S);
    }

    [Test]
    public async Task A_colour_word_targets_that_token_only()
    {
        var style = await GenerateAsync("deeper red");

        style.DangerColor.Should().NotBe("c62828");
        style.PrimaryColor.Should().Be("3366cc");
    }

    [Test]
    public async Task Cosier_moves_the_brand_hue_toward_orange_and_takes_the_tint_and_dark_along()
    {
        var style = await GenerateAsync("cosier please");

        var hue = Hsl(style.PrimaryColor).H;
        HueDistance(hue, 32).Should().BeLessThan(HueDistance(Hsl("3366cc").H, 32));
        HueDistance(Hsl(style.PrimaryTintColor).H, hue).Should().BeLessThan(6);
        HueDistance(Hsl(style.DarkColor).H, hue).Should().BeLessThan(6);
    }

    [TestCase("a bit darker")]
    [TestCase("make it warmer")]
    public async Task A_tweak_word_does_not_read_as_the_preset_keyword_inside_it(string prompt)
    {
        var style = await GenerateAsync(prompt);

        style.SuccessColor.Should().Be(Current().SuccessColor);
    }

    [Test]
    public async Task Extreme_tweaks_still_give_valid_colours()
    {
        var style = await GenerateAsync("darker darker deeper moodier muted pastel lighter warmer intense", Current());

        Colours(style).Should().AllSatisfy(c => c.Should().MatchRegex("^[0-9a-f]{6}$"));
    }

    #endregion

    #region Scoped to a region

    [Test]
    public async Task A_scoped_tweak_changes_only_the_selected_token()
    {
        var style = await GenerateAsync("darker", region: "footer");

        style.DarkColor.Should().NotBe("1a2333");
        style.PrimaryColor.Should().Be("3366cc");
    }

    [Test]
    public async Task A_scoped_tweak_never_switches_to_a_preset()
    {
        var style = await GenerateAsync("tech", region: "header");

        style.PrimaryColor.Should().Be("3366cc");
    }

    [Test]
    public async Task Shape_words_reshape_buttons_only_when_no_other_region_is_selected()
    {
        (await GenerateAsync("square", region: "footer")).ButtonRadius.Should().Be(8);
        (await GenerateAsync("square", region: "button")).ButtonRadius.Should().Be(2);
    }

    [Test]
    public async Task An_unknown_region_behaves_as_no_region()
    {
        var style = await GenerateAsync("tech", region: "sidebar");

        style.PrimaryColor.Should().Be(BrandPresets.All.Single(p => p.Name == "Bold & techy").Palette.PrimaryColor);
    }

    #endregion
}
