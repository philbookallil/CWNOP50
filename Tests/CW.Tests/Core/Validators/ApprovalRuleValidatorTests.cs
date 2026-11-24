using AwesomeAssertions;
using Moq;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Areas.Admin.Validators.Approvals;
using Nop.Services.Localization;
using NUnit.Framework;

namespace CW.Tests.Core.Validators;

/// <summary>
/// The approval panel is checked while approval is required: an approver at least, valid addresses, no negative amount.
/// </summary>
[TestFixture]
public class ApprovalRuleValidatorTests
{
    private ApprovalRuleValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(mock => mock.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        _validator = new ApprovalRuleValidator(localizationService.Object);
    }

    private async Task<bool> IsValidAsync(bool required, decimal? threshold, string? emails, bool available = true)
        => (await _validator.ValidateAsync(new ApprovalRuleModel { Available = available, Required = required, Threshold = threshold, Emails = emails })).IsValid;

    [TestCase(null, "ann@x.com", true)]
    [TestCase(0, "ann@x.com", true)]
    [TestCase(100.5, "ann@x.com, bob@x.com", true)]
    [TestCase(-1, "ann@x.com", false)]
    [TestCase(1e15, "ann@x.com", false)]
    [TestCase(100, "", false)]
    [TestCase(100, null, false)]
    [TestCase(100, " , ", false)]
    [TestCase(100, "ann@x.com, bob", false)]
    public async Task Required(double? threshold, string? emails, bool valid)
        => (await IsValidAsync(true, threshold.HasValue ? (decimal)threshold.Value : null, emails)).Should().Be(valid);

    [Test]
    public async Task Not_required_or_not_on_the_form_is_not_checked()
    {
        (await IsValidAsync(false, -1m, "bob")).Should().BeTrue();
        (await IsValidAsync(true, -1m, "bob", available: false)).Should().BeTrue();
    }
}
