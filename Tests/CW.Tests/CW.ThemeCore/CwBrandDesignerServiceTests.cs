using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Logging;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;
using Nop.Services.Localization;
using Nop.Services.Logging;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// Who answers a Generate, and what the admin sees. The rule that matters: mock output is never presented as AI output,
/// so an AI failure is an error with the form left alone, never a quiet fallback.
/// </summary>
[TestFixture]
public class CwBrandDesignerServiceTests
{
    #region Utilities

    private Mock<ICustomerActivityService> _activity = null!;
    private Mock<ICwBrandAiClient> _client = null!;
    private Mock<ICwBrandAiLimiter> _limiter = null!;
    private Mock<ICwBrandAiSettingsService> _settings = null!;
    private CwBrandDesignerService _service = null!;

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

    private static BrandStyle Answer() => new()
    {
        PrimaryColor = "112233",
        PrimaryTintColor = "eeeeee",
        SuccessColor = "00aa00",
        DangerColor = "aa0000",
        WarningColor = "aaaa00",
        DarkColor = "111111",
        ButtonRadius = 999
    };

    private static BrandAiConnection Connection() => new()
    {
        Provider = BrandAiProviders.Find("Claude"),
        BaseUrl = "https://api.anthropic.com",
        Model = "claude-sonnet-5-5",
        ApiKey = "secret",
        TimeoutSeconds = 5,
        DailyLimit = 300
    };

    private void AiAnswers(BrandStyle style) => _client
        .Setup(x => x.GenerateAsync(It.IsAny<BrandAiConnection>(), It.IsAny<BrandAiRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new BrandAiAnswer { Style = style, Summary = "Done." });

    private void Connected() => _settings.Setup(x => x.GetConnectionAsync(It.IsAny<int>())).ReturnsAsync(Connection());

    [SetUp]
    public void SetUp()
    {
        _activity = new Mock<ICustomerActivityService>();
        _client = new Mock<ICwBrandAiClient>();
        _limiter = new Mock<ICwBrandAiLimiter>();
        _settings = new Mock<ICwBrandAiSettingsService>();

        var resolver = new Mock<ICwBrandAiClientResolver>();
        resolver.Setup(x => x.Get(It.IsAny<BrandAiClientKind>())).Returns(_client.Object);

        var localization = new Mock<ILocalizationService>();
        localization.Setup(x => x.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key.EndsWith("InvalidColours") ? "Bad: {0}" : key);

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(x => x.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 7 });

        _service = new CwBrandDesignerService(_activity.Object, resolver.Object, _limiter.Object, _settings.Object,
            localization.Object, new Mock<ILogger>().Object, new Mock<IStoreContext>().Object, workContext.Object);
    }

    #endregion

    #region Routing

    [Test]
    public async Task A_preset_name_never_calls_the_ai()
    {
        Connected();

        var result = await _service.GenerateAsync("Dark luxe", Current());

        result.Source.Should().Be(BrandResultSource.Preset);
        result.Style.PrimaryColor.Should().Be(BrandPresets.All.Single(p => p.Name == "Dark luxe").Palette.PrimaryColor);
        _client.VerifyNoOtherCalls();
    }

    [Test]
    public async Task No_connection_is_offline_and_labelled()
    {
        var result = await _service.GenerateAsync("a bit darker", Current());

        result.IsOffline.Should().BeTrue();
        result.Style.Should().NotBeNull();
        result.Provider.Should().BeNull();
        _client.VerifyNoOtherCalls();
    }

    [Test]
    public async Task A_connection_sends_the_prompt_to_the_ai()
    {
        Connected();
        AiAnswers(Answer());

        var result = await _service.GenerateAsync("  premium boutique  ", Current());

        result.Source.Should().Be(BrandResultSource.Ai);
        result.IsOffline.Should().BeFalse();
        result.Provider.Should().Be("Claude");
        result.Summary.Should().Be("Done.");
        result.Style.PrimaryColor.Should().Be("112233");
        _client.Verify(x => x.GenerateAsync(It.IsAny<BrandAiConnection>(),
            It.Is<BrandAiRequest>(r => r.Prompt == "premium boutique"), It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task A_failed_ai_call_is_an_error_and_the_mock_does_not_step_in()
    {
        Connected();
        _client.Setup(x => x.GenerateAsync(It.IsAny<BrandAiConnection>(), It.IsAny<BrandAiRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrandAiException(BrandAiMessages.Unavailable, "status 503"));

        var result = await _service.GenerateAsync("premium boutique", Current());

        result.Style.Should().BeNull();
        result.Error.Should().Be(BrandAiMessages.Unavailable);
        result.IsOffline.Should().BeFalse();
    }

    [Test]
    public async Task A_reached_limit_stops_the_call()
    {
        Connected();
        _limiter.Setup(x => x.TryCountAsync(It.IsAny<BrandAiConnection>(), 7)).ReturnsAsync(BrandAiMessages.DailyLimit);

        var result = await _service.GenerateAsync("premium boutique", Current());

        result.Error.Should().Be(BrandAiMessages.DailyLimit);
        result.Style.Should().BeNull();
        _client.VerifyNoOtherCalls();
    }

    [Test]
    public async Task An_empty_prompt_with_a_connection_spends_nothing()
    {
        Connected();

        var result = await _service.GenerateAsync("   ", Current());

        result.Error.Should().Be(BrandAiMessages.EmptyPrompt);
        _client.VerifyNoOtherCalls();
    }

    #endregion

    #region Validation

    [Test]
    public async Task A_bad_colour_keeps_its_current_value_with_a_note()
    {
        Connected();
        var answer = Answer();
        answer.DangerColor = "javascript:alert(1)";
        AiAnswers(answer);

        var result = await _service.GenerateAsync("premium boutique", Current());

        result.Style.DangerColor.Should().Be("c62828");
        result.Style.PrimaryColor.Should().Be("112233");
        result.Notes.Should().ContainSingle().Which.Should().Contain(nameof(BrandField.DangerColor));
    }

    [Test]
    public async Task A_hash_is_stripped()
    {
        Connected();
        var answer = Answer();
        answer.PrimaryColor = "#ABCDEF";
        AiAnswers(answer);

        (await _service.GenerateAsync("premium boutique", Current())).Style.PrimaryColor.Should().Be("abcdef");
    }

    [Test]
    public async Task No_usable_colour_is_an_error()
    {
        Connected();
        AiAnswers(new BrandStyle { PrimaryColor = "blue", ButtonRadius = 8 });

        var result = await _service.GenerateAsync("premium boutique", Current());

        result.Style.Should().BeNull();
        result.Error.Should().Be(BrandAiMessages.InvalidOutput);
    }

    [TestCase(-5, 0)]
    [TestCase(5000, 999)]
    [TestCase(12, 12)]
    public async Task The_radius_is_clamped(int radius, int expected)
    {
        Connected();
        var answer = Answer();
        answer.ButtonRadius = radius;
        AiAnswers(answer);

        (await _service.GenerateAsync("premium boutique", Current())).Style.ButtonRadius.Should().Be(expected);
    }

    #endregion

    #region Region filter

    [Test]
    public async Task A_region_takes_only_its_own_field_whatever_the_model_changed()
    {
        Connected();
        AiAnswers(Answer());

        var style = (await _service.GenerateAsync("make everything green", Current(), "danger")).Style;

        style.DangerColor.Should().Be("aa0000");
        style.PrimaryColor.Should().Be("3366cc");
        style.PrimaryTintColor.Should().Be("eef2fa");
        style.SuccessColor.Should().Be("2e7d32");
        style.WarningColor.Should().Be("ef6c00");
        style.DarkColor.Should().Be("1a2333");
        style.ButtonRadius.Should().Be(8);
    }

    [Test]
    public async Task The_button_region_takes_the_primary_colour_and_the_radius()
    {
        Connected();
        AiAnswers(Answer());

        var style = (await _service.GenerateAsync("pill shaped", Current(), "button")).Style;

        style.PrimaryColor.Should().Be("112233");
        style.ButtonRadius.Should().Be(999);
        style.DangerColor.Should().Be("c62828");
    }

    #endregion

    #region Audit and test

    [Test]
    public async Task A_generation_is_audited_without_the_key_and_with_a_short_prompt()
    {
        Connected();
        AiAnswers(Answer());
        string comment = null!;
        _activity.Setup(x => x.InsertActivityAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .Callback((string _, string text, Nop.Core.BaseEntity _) => comment = text)
            .ReturnsAsync(new ActivityLog());

        await _service.GenerateAsync(new string('p', 500), Current());

        comment.Should().Contain("Claude").And.Contain("ok").And.NotContain("secret");
        comment.Count(c => c == 'p').Should().BeLessThan(220);
    }

    [Test]
    public async Task Testing_with_no_connection_says_so()
    {
        (await _service.TestConnectionAsync()).Should().Be(BrandAiMessages.NoConnection);
    }

    [Test]
    public async Task A_working_connection_tests_clean()
    {
        Connected();

        (await _service.TestConnectionAsync()).Should().BeNull();
    }

    #endregion
}
