using System.Linq.Expressions;
using AwesomeAssertions;
using Moq;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;
using Nop.Plugin.CW.ThemeCore.Settings;
using Nop.Services.Configuration;
using Nop.Services.Security;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// The connection rules: which addresses a key may go to, when a connection counts as set, and that one provider's
/// key never ends up beside another provider's address.
/// </summary>
[TestFixture]
public class CwBrandAiSettingsServiceTests
{
    #region Utilities

    private Mock<ISettingService> _settingService = null!;
    private CwBrandAiSettingsService _service = null!;
    private CwThemeAiSettings _saved = null!;

    private static BrandAiSettingsInput Input(string provider = "Claude", string url = "https://api.anthropic.com", string key = "") => new()
    {
        Provider = provider,
        BaseUrl = url,
        Model = "some-model",
        ApiKey = key,
        TimeoutSeconds = 30,
        Effort = "medium",
        DailyLimit = 300
    };

    [SetUp]
    public void SetUp()
    {
        _saved = new CwThemeAiSettings();

        _settingService = new Mock<ISettingService>();
        _settingService.Setup(x => x.LoadSettingAsync<CwThemeAiSettings>(It.IsAny<int>())).ReturnsAsync(() => _saved);

        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(x => x.EncryptText(It.IsAny<string>(), It.IsAny<string>())).Returns((string text, string _) => $"enc:{text}");
        encryption.Setup(x => x.DecryptText(It.IsAny<string>(), It.IsAny<string>())).Returns((string text, string _) => text.Replace("enc:", ""));

        _service = new CwBrandAiSettingsService(encryption.Object, _settingService.Object);
    }

    #endregion

    #region Addresses

    [TestCase("https://api.anthropic.com", true)]
    [TestCase("https://generativelanguage.googleapis.com/v1beta/openai", true)]
    [TestCase("http://localhost:11434/v1", true)]
    [TestCase("http://127.0.0.1:11434/v1", true)]
    [TestCase("http://[::1]:11434/v1", true)]
    [TestCase("http://example.com/v1", false)]
    [TestCase("http://192.168.1.10:11434/v1", false)]
    [TestCase("ftp://example.com", false)]
    [TestCase("file:///c:/secret", false)]
    [TestCase("example.com", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void Only_https_or_this_machine_may_receive_a_key(string? url, bool allowed)
    {
        CwBrandAiSettingsService.IsAllowedAddress(url!).Should().Be(allowed);
    }

    [Test]
    public async Task A_refused_address_saves_nothing()
    {
        (await _service.SaveAsync(Input(url: "http://example.com"), 0)).Should().Be(BrandAiMessages.InvalidUrl);

        _settingService.Verify(x => x.ClearCacheAsync(), Times.Never);
    }

    #endregion

    #region Connection

    [Test]
    public async Task No_provider_is_no_connection()
    {
        (await _service.GetConnectionAsync(0)).Should().BeNull();
    }

    [Test]
    public async Task A_provider_that_needs_a_key_and_has_none_is_no_connection()
    {
        _saved = new CwThemeAiSettings { Provider = "Claude", BaseUrl = "https://api.anthropic.com", Model = "m" };

        (await _service.GetConnectionAsync(0)).Should().BeNull();
    }

    [Test]
    public async Task A_local_provider_needs_no_key()
    {
        _saved = new CwThemeAiSettings { Provider = "Ollama (local)", BaseUrl = "http://localhost:11434/v1", Model = "m" };

        var connection = await _service.GetConnectionAsync(0);

        connection.Should().NotBeNull();
        connection.ApiKey.Should().BeEmpty();
        connection.Provider.ClientKind.Should().Be(BrandAiClientKind.Generic);
    }

    [Test]
    public async Task The_key_arrives_decrypted_and_the_view_shows_only_its_end()
    {
        _saved = new CwThemeAiSettings { Provider = "Claude", BaseUrl = "https://api.anthropic.com", Model = "m", ApiKeyEncrypted = "enc:abcdefgh" };

        (await _service.GetConnectionAsync(0)).ApiKey.Should().Be("abcdefgh");
        (await _service.GetViewAsync(0)).KeyLastFour.Should().Be("efgh");
    }

    #endregion

    #region Key handling

    [Test]
    public async Task A_new_key_is_stored_encrypted()
    {
        (await _service.SaveAsync(Input(key: " new-key "), 0)).Should().BeNull();

        _saved.ApiKeyEncrypted.Should().Be("enc:new-key");
    }

    [Test]
    public async Task An_empty_key_keeps_the_saved_one_for_the_same_provider()
    {
        _saved = new CwThemeAiSettings { Provider = "Claude", BaseUrl = "https://api.anthropic.com", ApiKeyEncrypted = "enc:kept" };

        await _service.SaveAsync(Input(), 0);

        _saved.ApiKeyEncrypted.Should().Be("enc:kept");
    }

    [TestCase("Groq", "https://api.groq.com/openai/v1")]
    [TestCase("Claude", "https://evil.example.com")]
    public async Task Changing_the_provider_or_address_drops_the_old_key(string provider, string url)
    {
        _saved = new CwThemeAiSettings { Provider = "Claude", BaseUrl = "https://api.anthropic.com", ApiKeyEncrypted = "enc:kept" };

        await _service.SaveAsync(Input(provider, url), 0);

        _saved.ApiKeyEncrypted.Should().BeEmpty();
    }

    [Test]
    public async Task Remove_key_clears_it()
    {
        _saved = new CwThemeAiSettings { Provider = "Claude", BaseUrl = "https://api.anthropic.com", ApiKeyEncrypted = "enc:kept" };

        await _service.SaveAsync(Input() with { RemoveKey = true }, 0);

        _saved.ApiKeyEncrypted.Should().BeEmpty();
    }

    [Test]
    public async Task A_store_override_does_not_copy_the_global_key()
    {
        //a loaded setting falls back to the global value, which must not become the store's own
        _saved = new CwThemeAiSettings { Provider = "Claude", BaseUrl = "https://api.anthropic.com", ApiKeyEncrypted = "enc:global" };
        _settingService.Setup(x => x.GetSettingByKeyAsync("cwthemeaisettings.apikeyencrypted", string.Empty, 4, false)).ReturnsAsync(string.Empty);

        await _service.SaveAsync(Input() with { OverrideForStore = true }, 4);

        _saved.ApiKeyEncrypted.Should().BeEmpty();
    }

    [Test]
    public async Task A_store_without_an_override_changes_nothing_of_the_connection()
    {
        _saved = new CwThemeAiSettings { Provider = "Claude", BaseUrl = "https://api.anthropic.com", ApiKeyEncrypted = "enc:global" };

        (await _service.SaveAsync(Input("Groq", "not a url") with { OverrideForStore = false }, 4)).Should().BeNull();

        _saved.Provider.Should().Be("Claude");
        _saved.ApiKeyEncrypted.Should().Be("enc:global");
    }

    [Test]
    public async Task Effort_and_the_daily_limit_are_saved_only_at_all_stores()
    {
        await _service.SaveAsync(Input() with { OverrideForStore = true, Effort = "high", DailyLimit = 5 }, 4);

        _saved.Effort.Should().Be("medium");
        _saved.DailyLimit.Should().Be(300);
        _settingService.Verify(x => x.SaveSettingAsync(It.IsAny<CwThemeAiSettings>(), It.IsAny<Expression<Func<CwThemeAiSettings, string>>>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    #endregion
}
