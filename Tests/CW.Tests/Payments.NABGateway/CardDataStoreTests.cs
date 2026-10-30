using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Nop.Plugin.Payments.NABGateway;
using Nop.Plugin.Payments.NABGateway.Services;
using NUnit.Framework;

namespace CW.Tests.Payments.NABGateway;

/// <summary>
/// The in-memory store that holds a card between the payment-info and confirm steps: used once, per store and
/// customer, expiring, and never held as plain text.
/// </summary>
[TestFixture]
public class CardDataStoreTests
{
    #region Fields

    private static readonly NABXMLCardData _card = new("4111111111111111", "Test Shopper", 12, 2028, "123");

    private TestClock _clock = null!;
    private MemoryCache _memoryCache = null!;
    private NABXMLCardDataStore _store = null!;

    #endregion

    #region Utilities

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);
    }

    [SetUp]
    public void SetUp()
    {
        _clock = new TestClock();
        _memoryCache = new MemoryCache(new MemoryCacheOptions { Clock = _clock });
        _store = new NABXMLCardDataStore(_memoryCache, new EphemeralDataProtectionProvider());
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
    }

    #endregion

    #region Save and take

    [Test]
    public void Take_returns_the_card_that_was_saved()
    {
        _store.Save(1, 7, _card);

        _store.Take(1, 7).Should().Be(_card);
    }

    [Test]
    public void A_card_is_taken_once()
    {
        _store.Save(1, 7, _card);

        _store.Take(1, 7);

        _store.Take(1, 7).Should().BeNull();
    }

    [Test]
    public void Take_without_a_saved_card_returns_null()
    {
        _store.Take(1, 7).Should().BeNull();
    }

    [Test]
    public void A_new_card_replaces_the_previous_one()
    {
        var newCard = _card with { Number = "5555555555554444", Cvv = "456" };
        _store.Save(1, 7, _card);

        _store.Save(1, 7, newCard);

        _store.Take(1, 7).Should().Be(newCard);
    }

    [TestCase(2, 7, TestName = "Another store does not see the card")]
    [TestCase(1, 8, TestName = "Another customer does not see the card")]
    public void A_card_belongs_to_one_store_and_customer(int storeId, int customerId)
    {
        _store.Save(1, 7, _card);

        _store.Take(storeId, customerId).Should().BeNull();
        _store.Take(1, 7).Should().Be(_card);
    }

    [Test]
    public void Saving_no_card_throws()
    {
        var save = () => _store.Save(1, 7, null!);

        save.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Expiry

    [Test]
    public void A_card_is_still_there_before_it_expires()
    {
        _store.Save(1, 7, _card);

        _clock.UtcNow += TimeSpan.FromMinutes(NABXMLPaymentDefaults.CardDataLifetimeMinutes) - TimeSpan.FromSeconds(1);

        _store.Take(1, 7).Should().Be(_card);
    }

    [Test]
    public void A_card_is_gone_once_it_expires()
    {
        _store.Save(1, 7, _card);

        _clock.UtcNow += TimeSpan.FromMinutes(NABXMLPaymentDefaults.CardDataLifetimeMinutes) + TimeSpan.FromSeconds(1);

        _store.Take(1, 7).Should().BeNull();
    }

    #endregion

    #region Protection

    [Test]
    public void The_cache_never_holds_the_card_as_plain_text()
    {
        _store.Save(1, 7, _card);

        var key = $"{NABXMLPaymentDefaults.CardDataProtectionPurpose}.1.7";
        _memoryCache.TryGetValue(key, out byte[]? held).Should().BeTrue();

        var text = Encoding.UTF8.GetString(held!);
        text.Should().NotContain(_card.Number);
        text.Should().NotContain(_card.HolderName);
        text.Should().NotContain("\"Cvv\"");
    }

    #endregion
}
