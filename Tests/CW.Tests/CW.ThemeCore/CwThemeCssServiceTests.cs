using AwesomeAssertions;
using Moq;
using Nop.Plugin.CW.ThemeCore.Services;
using Nop.Plugin.CW.ThemeCore.Settings;
using Nop.Services.Configuration;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// The store's accent colours and button radius become one <c>:root{…}</c> block written into every page head. Only
/// hex values are written, so a setting value can never inject CSS into the page.
/// </summary>
[TestFixture]
public class CwThemeCssServiceTests
{
    #region Utilities

    private static Task<string> GenerateAsync(CwThemeSettings settings, int storeId = 1)
    {
        var settingService = new Mock<ISettingService>();
        settingService.Setup(s => s.LoadSettingAsync<CwThemeSettings>(storeId)).ReturnsAsync(settings);

        return new CwThemeCssService(settingService.Object).GenerateRootCssAsync(storeId);
    }

    private static CwThemeSettings AllColours() => new()
    {
        PrimaryColor = "00549f",
        PrimaryTintColor = "eaf4fc",
        SuccessColor = "28a745",
        DangerColor = "dc3545",
        WarningColor = "ffc107",
        DarkColor = "343a40",
        ButtonRadius = 8
    };

    #endregion

    #region Tests

    [Test]
    public async Task Every_valid_colour_and_the_radius_are_written_as_custom_properties()
    {
        var css = await GenerateAsync(AllColours());

        css.Should().Be(":root{--cw-primary:#00549f;--cw-primary-tint:#eaf4fc;--cw-success:#28a745;" +
                        "--cw-danger:#dc3545;--cw-warning:#ffc107;--cw-dark:#343a40;--cw-btn-radius:8px;}");
    }

    [Test]
    public async Task Colour_values_are_trimmed()
    {
        var settings = new CwThemeSettings { PrimaryColor = "  00549f ", ButtonRadius = 0 };

        (await GenerateAsync(settings)).Should().Be(":root{--cw-primary:#00549f;}");
    }

    [TestCase("fff;}body{display:none")]
    [TestCase("#00549f")]
    [TestCase("red")]
    [TestCase("12")]
    [TestCase("123456789")]
    [TestCase("00549g")]
    public async Task A_value_that_is_not_3_to_8_hex_digits_is_left_out(string value)
    {
        var settings = AllColours();
        settings.PrimaryColor = value;

        var css = await GenerateAsync(settings);

        css.Should().NotContain("--cw-primary:");
        css.Should().NotContain(value);
        css.Should().Contain("--cw-success:#28a745;");
    }

    [Test]
    public async Task Disabling_accent_colours_still_writes_the_button_radius()
    {
        var settings = AllColours();
        settings.DisableAccentColors = true;
        settings.ButtonRadius = 12;

        (await GenerateAsync(settings)).Should().Be(":root{--cw-btn-radius:12px;}");
    }

    [Test]
    public async Task A_zero_radius_falls_back_to_the_theme()
    {
        var settings = AllColours();
        settings.ButtonRadius = 0;

        (await GenerateAsync(settings)).Should().NotContain("--cw-btn-radius");
    }

    [Test]
    public async Task Nothing_valid_gives_an_empty_string_not_an_empty_block()
    {
        var settings = new CwThemeSettings { PrimaryColor = "nope", ButtonRadius = 0 };

        (await GenerateAsync(settings)).Should().BeEmpty();
    }

    [Test]
    public async Task The_store_given_is_the_store_whose_settings_are_read()
    {
        var settingService = new Mock<ISettingService>();
        settingService.Setup(s => s.LoadSettingAsync<CwThemeSettings>(3)).ReturnsAsync(AllColours());

        await new CwThemeCssService(settingService.Object).GenerateRootCssAsync(3);

        settingService.Verify(s => s.LoadSettingAsync<CwThemeSettings>(3), Times.Once);
    }

    #endregion
}
