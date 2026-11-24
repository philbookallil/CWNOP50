using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Settings;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The approval panel of the branch and department forms: a form without it never changes the rule.
/// </summary>
[TestFixture]
public class ApprovalRuleModelFactoryTests
{
    #region Fields

    private const int StoreId = 1;
    private const int BranchId = 9;

    private bool _featureOn;
    private ApprovalRuleModelFactory _factory = null!;

    #endregion

    #region Utilities

    [SetUp]
    public void SetUp()
    {
        _featureOn = true;

        var ruleService = new Mock<IApprovalRuleService>();
        ruleService.Setup(mock => mock.GetSetupAsync(ApprovalScopeType.Branch, BranchId))
            .ReturnsAsync(new ApprovalRuleSetup(true, 100m, ["ann@x.com", "bob@x.com"]));

        var featureService = new Mock<ICWFeatureService>();
        featureService.Setup(mock => mock.IsEnabledAsync(CWFeature.Approvals, StoreId)).ReturnsAsync(() => _featureOn);

        _factory = new ApprovalRuleModelFactory(ruleService.Object, featureService.Object);
    }

    private Task PrepareAsync(ApprovalRuleModel model, bool keepValues)
        => _factory.PrepareApprovalRuleModelAsync(model, StoreId, ApprovalScopeType.Branch, BranchId, keepValues);

    #endregion

    #region Tests

    [Test]
    public async Task A_form_shows_the_rule()
    {
        var model = new ApprovalRuleModel();
        await PrepareAsync(model, keepValues: false);

        model.Available.Should().BeTrue();
        model.Required.Should().BeTrue();
        model.Threshold.Should().Be(100m);
        model.Emails.Should().Be("ann@x.com, bob@x.com");
    }

    [Test]
    public async Task A_refused_post_keeps_what_was_typed()
    {
        var model = new ApprovalRuleModel { Available = true, Required = true, Threshold = -1m, Emails = "bob" };
        await PrepareAsync(model, keepValues: true);

        model.Available.Should().BeTrue();
        model.Threshold.Should().Be(-1m);
        model.Emails.Should().Be("bob");
    }

    [Test]
    public async Task A_refused_post_without_the_panel_shows_the_rule_not_an_empty_panel()
    {
        var model = new ApprovalRuleModel();
        await PrepareAsync(model, keepValues: true);

        model.Available.Should().BeTrue();
        model.Required.Should().BeTrue();
        model.Emails.Should().Be("ann@x.com, bob@x.com");
    }

    [Test]
    public async Task The_panel_is_not_shown_while_approvals_is_off([Values] bool keepValues, [Values] bool posted)
    {
        _featureOn = false;

        var model = new ApprovalRuleModel { Available = posted };
        await PrepareAsync(model, keepValues);

        model.Available.Should().BeFalse();
    }

    [Test]
    public async Task A_panel_posted_after_approvals_is_switched_off_is_dropped_with_its_errors_only([Values] bool featureOn)
    {
        _featureOn = featureOn;

        var model = new ApprovalRuleModel { Available = true, Required = true };
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("ApprovalRule.Emails", "wrong");
        modelState.AddModelError("Name", "wrong");

        await _factory.DropWhenOffAsync(model, StoreId, modelState, "ApprovalRule");

        model.Available.Should().Be(featureOn);
        modelState.ContainsKey("ApprovalRule.Emails").Should().Be(featureOn);
        modelState.ContainsKey("Name").Should().BeTrue();
    }

    [Test]
    public void A_posted_panel_is_the_rule_it_asks_for()
    {
        var setup = _factory.ToSetup(new ApprovalRuleModel { Required = true, Threshold = 50m, Emails = "ann@x.com; bob@x.com" });

        setup.Required.Should().BeTrue();
        setup.Threshold.Should().Be(50m);
        setup.Emails.Should().Equal("ann@x.com", "bob@x.com");
    }

    #endregion
}
