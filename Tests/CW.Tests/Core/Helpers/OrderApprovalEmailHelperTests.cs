using AwesomeAssertions;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The approvers' e-mail addresses, as a form types them, and how an approver is named.
/// </summary>
[TestFixture]
public class OrderApprovalEmailHelperTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" , ; ")]
    public void Nothing_typed_gives_no_address(string? emails)
        => OrderApprovalEmailHelper.Parse(emails).Should().BeEmpty();

    [TestCase("ann@x.com,bob@x.com")]
    [TestCase(" ann@x.com ; bob@x.com ")]
    [TestCase("ann@x.com | bob@x.com,")]
    [TestCase("ann@x.com\r\nbob@x.com")]
    public void Addresses_are_separated_by_commas_semicolons_bars_or_lines(string emails)
        => OrderApprovalEmailHelper.Parse(emails).Should().Equal("ann@x.com", "bob@x.com");

    [Test]
    public void An_address_typed_twice_counts_once_whatever_its_case()
        => OrderApprovalEmailHelper.Parse("Ann@x.com, bob@x.com, ann@X.com").Should().Equal("Ann@x.com", "bob@x.com");

    [Test]
    public void Addresses_are_shown_separated_by_commas()
    {
        OrderApprovalEmailHelper.Format(["ann@x.com", "bob@x.com"]).Should().Be("ann@x.com, bob@x.com");
        OrderApprovalEmailHelper.Format([]).Should().BeEmpty();
        OrderApprovalEmailHelper.Format(null).Should().BeEmpty();
    }

    [TestCase("ann@x.com", true)]
    [TestCase("ann", false)]
    [TestCase("ann@", false)]
    [TestCase("ann @x.com", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void An_address_must_be_one_an_email_can_be_sent_to(string? email, bool valid)
        => OrderApprovalEmailHelper.IsValid(email).Should().Be(valid);

    [Test]
    public void An_address_longer_than_its_column_is_not_valid()
        => OrderApprovalEmailHelper.IsValid(new string('a', 60) + "@" + string.Join(".", Enumerable.Repeat(new string('b', 50), 4)) + ".com").Should().BeFalse();

    [TestCase("Ann", "ann@x.com", "Ann (ann@x.com)")]
    [TestCase("ann@x.com", "ann@x.com", "ann@x.com")]
    [TestCase("ANN@x.com", "ann@x.com", "ann@x.com")]
    [TestCase("", "ann@x.com", "ann@x.com")]
    public void An_approver_named_by_the_address_is_shown_once(string name, string email, string shown)
        => OrderApprovalEmailHelper.NameAndEmail(name, email).Should().Be(shown);
}
