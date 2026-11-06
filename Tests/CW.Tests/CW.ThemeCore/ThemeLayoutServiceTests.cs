using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Configuration;
using Nop.Plugin.CW.ThemeCore.Domain.Layout;
using Nop.Plugin.CW.ThemeCore.Services.Layout;
using Nop.Services.Configuration;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// Each storefront section offers the layouts the theme's manifest (<c>cw-theme.json</c>) lists. The selection is kept
/// per store with an all-stores default; an unknown or stale value falls back to the section's default, and the
/// selected css-class layouts become classes on the page.
/// </summary>
[TestFixture]
public class ThemeLayoutServiceTests
{
    #region Utilities

    private const string Theme = "CW.Standard";

    private Mock<ISettingService> _settingService = null!;
    private ThemeLayoutService _service = null!;

    private static ThemeLayoutManifest Manifest() => new()
    {
        ThemeSystemName = Theme,
        Sections =
        [
            new ThemeLayoutSection
            {
                Key = "home",
                Default = "standard",
                Layouts =
                [
                    new ThemeLayoutOption { Key = "standard", Type = "css-class", CssClass = "" },
                    new ThemeLayoutOption { Key = "boxed", Type = "css-class", CssClass = " cw-home--boxed " }
                ]
            },
            new ThemeLayoutSection
            {
                Key = "product",
                Default = "two-column",
                Layouts =
                [
                    new ThemeLayoutOption { Key = "two-column", Type = "css-class", CssClass = "" },
                    new ThemeLayoutOption { Key = "one-column", Type = "css-class", CssClass = "cw-product--one-col" },
                    new ThemeLayoutOption { Key = "gallery", Type = "partial", Partial = "_ProductGallery", CssClass = "not-a-class" }
                ]
            }
        ]
    };

    /// <summary>The value nop returns for a section key (null = no setting, so nop hands back the default)</summary>
    private void Stored(string sectionKey, string? value, int storeId = 1)
    {
        var key = $"cwtheme.layout.{Theme}.{sectionKey}".ToLowerInvariant();
        _settingService
            .Setup(s => s.GetSettingByKeyAsync(key, It.IsAny<string>(), storeId, true))
            .ReturnsAsync((string _, string defaultValue, int _, bool _) => value ?? defaultValue);
    }

    [SetUp]
    public void SetUp()
    {
        _settingService = new Mock<ISettingService>();
        _settingService
            .Setup(s => s.GetSettingByKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync((string _, string defaultValue, int _, bool _) => defaultValue);

        var manifestService = new Mock<IThemeLayoutManifestService>();
        manifestService.Setup(m => m.GetManifestAsync(Theme)).ReturnsAsync(Manifest());

        _service = new ThemeLayoutService(_settingService.Object, manifestService.Object);
    }

    #endregion

    #region Selected layout

    [Test]
    public async Task Without_a_setting_the_section_default_is_selected()
    {
        (await _service.GetSelectedLayoutKeyAsync(Theme, "home", 1)).Should().Be("standard");
    }

    [Test]
    public async Task A_stored_layout_is_selected()
    {
        Stored("home", "boxed");

        (await _service.GetSelectedLayoutKeyAsync(Theme, "home", 1)).Should().Be("boxed");
    }

    [Test]
    public async Task A_stored_layout_the_manifest_no_longer_offers_falls_back_to_the_default()
    {
        Stored("home", "removed-layout");

        (await _service.GetSelectedLayoutKeyAsync(Theme, "home", 1)).Should().Be("standard");
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task A_blank_stored_value_falls_back_to_the_default(string value)
    {
        Stored("home", value);

        (await _service.GetSelectedLayoutKeyAsync(Theme, "home", 1)).Should().Be("standard");
    }

    [Test]
    public async Task Section_keys_match_without_regard_to_case()
    {
        Stored("home", "boxed");

        (await _service.GetSelectedLayoutKeyAsync(Theme, "HOME", 1)).Should().Be("boxed");
    }

    [Test]
    public async Task A_section_the_manifest_does_not_have_gives_null()
    {
        (await _service.GetSelectedLayoutKeyAsync(Theme, "checkout", 1)).Should().BeNull();
    }

    [Test]
    public async Task The_setting_is_read_for_the_store_with_the_all_stores_value_as_fallback()
    {
        await _service.GetSelectedLayoutKeyAsync(Theme, "home", 7);

        _settingService.Verify(s => s.GetSettingByKeyAsync("cwtheme.layout.cw.standard.home", "standard", 7, true), Times.Once);
    }

    #endregion

    #region Page classes

    [Test]
    public async Task Default_layouts_with_no_class_give_no_page_classes()
    {
        (await _service.GetActivePageCssClassesAsync(Theme, 1)).Should().BeEmpty();
    }

    [Test]
    public async Task Selected_css_class_layouts_give_their_trimmed_classes()
    {
        Stored("home", "boxed");
        Stored("product", "one-column");

        (await _service.GetActivePageCssClassesAsync(Theme, 1))
            .Should().Equal("cw-home--boxed", "cw-product--one-col");
    }

    [Test]
    public async Task A_partial_layout_adds_no_page_class()
    {
        Stored("product", "gallery");

        (await _service.GetActivePageCssClassesAsync(Theme, 1)).Should().BeEmpty();
    }

    #endregion

    #region Saving

    [Test]
    public async Task Saving_for_all_stores_writes_store_zero()
    {
        await _service.SetSelectedLayoutKeyAsync(Theme, "home", "boxed", overrideForStore: false, storeId: 0);

        _settingService.Verify(s => s.SetSettingAsync("cwtheme.layout.cw.standard.home", "boxed", 0, false), Times.Once);
    }

    [Test]
    public async Task Saving_an_override_writes_the_store()
    {
        await _service.SetSelectedLayoutKeyAsync(Theme, "home", "boxed", overrideForStore: true, storeId: 2);

        _settingService.Verify(s => s.SetSettingAsync("cwtheme.layout.cw.standard.home", "boxed", 2, false), Times.Once);
    }

    [Test]
    public async Task Clearing_the_override_deletes_the_store_value_so_it_inherits()
    {
        var existing = new Setting { Name = "cwtheme.layout.cw.standard.home", Value = "boxed", StoreId = 2 };
        _settingService.Setup(s => s.GetSettingAsync("cwtheme.layout.cw.standard.home", 2, false)).ReturnsAsync(existing);

        await _service.SetSelectedLayoutKeyAsync(Theme, "home", "standard", overrideForStore: false, storeId: 2);

        _settingService.Verify(s => s.DeleteSettingAsync(existing), Times.Once);
        _settingService.Verify(s => s.SetSettingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Clearing_an_override_that_does_not_exist_changes_nothing()
    {
        await _service.SetSelectedLayoutKeyAsync(Theme, "home", "standard", overrideForStore: false, storeId: 2);

        _settingService.Verify(s => s.DeleteSettingAsync(It.IsAny<Setting>()), Times.Never);
    }

    [Test]
    public async Task Only_a_store_value_counts_as_an_override()
    {
        _settingService.Setup(s => s.GetSettingAsync("cwtheme.layout.cw.standard.home", 2, false))
            .ReturnsAsync(new Setting { StoreId = 2 });

        (await _service.IsOverriddenForStoreAsync(Theme, "home", 2)).Should().BeTrue();
        (await _service.IsOverriddenForStoreAsync(Theme, "home", 3)).Should().BeFalse();
        (await _service.IsOverriddenForStoreAsync(Theme, "home", 0)).Should().BeFalse();
    }

    #endregion
}
