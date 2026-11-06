using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection;
using Nop.Plugin.CW.ThemeCore.Services.Preview;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// A live-preview token authorises preview mode on one store's storefront, for a limited time, for one admin origin.
/// Anything else — another store, an expired, tampered or foreign token — is "no preview", never an exception.
/// </summary>
[TestFixture]
public class CwPreviewTokenServiceTests
{
    #region Utilities

    private const string AdminOrigin = "https://admin.example";

    private IDataProtectionProvider _provider = null!;
    private CwPreviewTokenService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _provider = new EphemeralDataProtectionProvider();
        _service = new CwPreviewTokenService(_provider);
    }

    #endregion

    #region Tests

    [Test]
    public void A_minted_token_validates_for_its_store_and_carries_its_origin()
    {
        var token = _service.Mint(4, AdminOrigin, TimeSpan.FromMinutes(5));

        _service.TryValidate(token, 4, out var payload).Should().BeTrue();
        payload.StoreId.Should().Be(4);
        payload.AdminOrigin.Should().Be(AdminOrigin);
        payload.ExpiresUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Test]
    public void The_token_is_url_safe()
    {
        var token = _service.Mint(4, AdminOrigin, TimeSpan.FromMinutes(5));

        token.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Test]
    public void A_token_for_another_store_is_refused()
    {
        var token = _service.Mint(4, AdminOrigin, TimeSpan.FromMinutes(5));

        _service.TryValidate(token, 5, out var payload).Should().BeFalse();
        payload.Should().BeNull();
    }

    [Test]
    public void An_expired_token_is_refused()
    {
        var token = _service.Mint(4, AdminOrigin, TimeSpan.FromSeconds(-1));

        _service.TryValidate(token, 4, out _).Should().BeFalse();
    }

    [Test]
    public void A_tampered_token_is_refused()
    {
        var token = _service.Mint(4, AdminOrigin, TimeSpan.FromMinutes(5));
        var tampered = token[..^4] + (token[^4] == 'A' ? "B" : "A") + token[^3..];

        _service.TryValidate(tampered, 4, out _).Should().BeFalse();
    }

    [Test]
    public void A_token_from_another_key_ring_is_refused()
    {
        var foreign = new CwPreviewTokenService(new EphemeralDataProtectionProvider())
            .Mint(4, AdminOrigin, TimeSpan.FromMinutes(5));

        _service.TryValidate(foreign, 4, out _).Should().BeFalse();
    }

    [Test]
    public void A_token_protected_for_another_purpose_is_refused()
    {
        var other = _provider.CreateProtector("Some.Other.Purpose")
            .Protect($"{{\"StoreId\":4,\"AdminOrigin\":\"{AdminOrigin}\",\"ExpiresUtc\":\"2999-01-01T00:00:00Z\"}}");

        _service.TryValidate(other, 4, out _).Should().BeFalse();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-a-token")]
    public void Missing_or_garbage_input_is_refused_without_throwing(string? token)
    {
        _service.TryValidate(token!, 4, out var payload).Should().BeFalse();
        payload.Should().BeNull();
    }

    [Test]
    public void A_validly_protected_value_that_is_not_a_payload_is_refused()
    {
        var notJson = _provider.CreateProtector("CW.ThemeCore.LivePreview.v1").Protect("hello");

        _service.TryValidate(notJson, 4, out _).Should().BeFalse();
    }

    #endregion
}
