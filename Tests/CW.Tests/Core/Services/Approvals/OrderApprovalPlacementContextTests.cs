using AwesomeAssertions;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Approvals;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Approvals;

/// <summary>
/// A held plain order's e-mails wait while its placement runs and go once, and the Approvals switch is a feature like
/// the others.
/// </summary>
[TestFixture]
public class OrderApprovalPlacementContextTests
{
    [Test]
    public void A_placement_runs_for_its_own_customer_until_it_ends()
    {
        var context = new OrderApprovalPlacementContext();

        context.BeginPlainPlacement(5);

        context.IsPlainPlacementRunning(5).Should().BeTrue();
        context.IsPlainPlacementRunning(6).Should().BeFalse();

        context.EndPlainPlacement(5);

        context.IsPlainPlacementRunning(5).Should().BeFalse();
    }

    [Test]
    public void A_plain_order_keeps_its_emails_until_it_is_settled_and_sends_them_once()
    {
        var context = new OrderApprovalPlacementContext();
        var requirement = new OrderApprovalRequirement { Required = true };

        context.SetPendingNotification(40, requirement);

        context.TakePendingNotification(41).Should().BeNull();
        context.TakePendingNotification(40).Should().BeSameAs(requirement);
        context.TakePendingNotification(40).Should().BeNull();
    }

    [Test]
    public void The_approvals_switch_reads_its_own_setting_and_needs_no_other_feature()
    {
        var features = CWFeatureSet.From(new MultiStoreSettings { ApprovalsEnable = true });

        features[CWFeature.Approvals].Should().BeTrue();
        features[CWFeature.PackName].Should().BeFalse();
        CWFeatureSet.From(new MultiStoreSettings { PackNameEnable = true })[CWFeature.Approvals].Should().BeFalse();
    }

    [Test]
    public void The_approval_defaults_hold_on_account_orders_remind_and_never_expire()
    {
        var settings = new MultiStoreSettings();

        settings.ApprovalsEnable.Should().BeFalse();
        settings.ApprovalPaymentMethods.Should().Equal("Payments.CheckMoneyOrder");
        settings.ApprovalFirstReminderDays.Should().Be(3);
        settings.ApprovalSecondReminderDays.Should().Be(7);
        settings.ApprovalExpiryDays.Should().Be(0);
    }
}
