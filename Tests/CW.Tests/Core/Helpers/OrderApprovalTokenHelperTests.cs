using AwesomeAssertions;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The tokens of the approval links: random, safe in a URL path, and stored only as a hash
/// </summary>
[TestFixture]
public class OrderApprovalTokenHelperTests
{
    [Test]
    public void A_token_is_url_safe_and_carries_32_bytes()
    {
        var token = OrderApprovalTokenHelper.CreateToken();

        token.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
    }

    [Test]
    public void Two_tokens_differ()
        => OrderApprovalTokenHelper.CreateToken().Should().NotBe(OrderApprovalTokenHelper.CreateToken());

    [Test]
    public void The_hash_is_64_lowercase_hex_characters_and_repeatable()
    {
        var token = OrderApprovalTokenHelper.CreateToken();
        var hash = OrderApprovalTokenHelper.HashToken(token);

        hash.Should().MatchRegex("^[0-9a-f]{64}$");
        OrderApprovalTokenHelper.HashToken(token).Should().Be(hash);
    }

    [Test]
    public void The_hash_is_not_the_token()
    {
        var token = OrderApprovalTokenHelper.CreateToken();

        OrderApprovalTokenHelper.HashToken(token).Should().NotContain(token);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void An_empty_token_has_no_hash(string token)
        => OrderApprovalTokenHelper.HashToken(token).Should().BeNull();

    [Test]
    public void A_token_copied_with_spaces_around_it_still_matches()
    {
        var token = OrderApprovalTokenHelper.CreateToken();

        OrderApprovalTokenHelper.HashToken($" {token} ").Should().Be(OrderApprovalTokenHelper.HashToken(token));
    }
}
