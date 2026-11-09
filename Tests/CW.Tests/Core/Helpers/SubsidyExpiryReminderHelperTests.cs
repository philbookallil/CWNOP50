using AwesomeAssertions;
using Nop.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The rules of the subsidy expiry reminders: which season is open and when it ends, which follow-up is due, what is
/// left to spend, and the order of the follow-ups' lead times
/// </summary>
[TestFixture]
public class SubsidyExpiryReminderHelperTests
{
    private static readonly DateTime PeriodEndUtc = new(2026, 11, 30, 12, 59, 59, DateTimeKind.Utc);

    /// <summary>
    /// The default lead times: 30 days, 7 days and 1 day before the end
    /// </summary>
    private static readonly Dictionary<int, int> DefaultLeadHours = new() { [1] = 30 * 24, [2] = 7 * 24, [3] = 24 };

    #region Open season

    [Test]
    public void A_one_off_period_open_today_ends_on_its_end_date()
    {
        SubsidyExpiryReminderHelper.TryGetOpenPeriodEnd(new DateTime(2026, 1, 1), new DateTime(2026, 11, 30), false, new DateTime(2026, 11, 9), out var periodEnd)
            .Should().BeTrue();
        periodEnd.Should().Be(new DateTime(2026, 11, 30));
    }

    [Test]
    public void A_one_off_period_that_has_ended_is_not_open()
    {
        SubsidyExpiryReminderHelper.TryGetOpenPeriodEnd(new DateTime(2026, 1, 1), new DateTime(2026, 10, 31), false, new DateTime(2026, 11, 9), out _)
            .Should().BeFalse();
    }

    [Test]
    public void A_period_that_has_not_started_is_not_open()
    {
        SubsidyExpiryReminderHelper.TryGetOpenPeriodEnd(new DateTime(2026, 12, 1), new DateTime(2027, 2, 28), false, new DateTime(2026, 11, 9), out _)
            .Should().BeFalse();
    }

    [Test]
    public void An_annual_period_ends_on_this_years_anniversary()
    {
        SubsidyExpiryReminderHelper.TryGetOpenPeriodEnd(new DateTime(2025, 7, 1), new DateTime(2025, 11, 30), true, new DateTime(2026, 11, 9), out var periodEnd)
            .Should().BeTrue();
        periodEnd.Should().Be(new DateTime(2026, 11, 30));
    }

    [Test]
    public void An_annual_period_across_new_year_ends_next_year()
    {
        SubsidyExpiryReminderHelper.TryGetOpenPeriodEnd(new DateTime(2025, 11, 1), new DateTime(2026, 2, 28), true, new DateTime(2026, 12, 15), out var periodEnd)
            .Should().BeTrue();
        periodEnd.Should().Be(new DateTime(2027, 2, 28));
    }

    [Test]
    public void An_annual_period_between_seasons_is_not_open()
    {
        SubsidyExpiryReminderHelper.TryGetOpenPeriodEnd(new DateTime(2025, 3, 1), new DateTime(2025, 5, 31), true, new DateTime(2026, 11, 9), out _)
            .Should().BeFalse();
    }

    [Test]
    public void The_last_day_is_still_open()
    {
        SubsidyExpiryReminderHelper.TryGetOpenPeriodEnd(new DateTime(2026, 1, 1), new DateTime(2026, 11, 9), false, new DateTime(2026, 11, 9), out _)
            .Should().BeTrue();
    }

    #endregion

    #region Due follow-up

    [Test]
    public void Nothing_is_due_before_the_longest_lead_time()
    {
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddDays(-31), DefaultLeadHours, 0).Should().BeNull();
    }

    [Test]
    public void The_first_follow_up_is_due_inside_its_lead_time()
    {
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddDays(-29), DefaultLeadHours, 0).Should().Be(1);
    }

    [Test]
    public void A_follow_up_already_sent_is_not_sent_again()
    {
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddDays(-29), DefaultLeadHours, 1).Should().BeNull();
    }

    [Test]
    public void The_next_follow_up_is_due_inside_its_own_lead_time()
    {
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddDays(-6), DefaultLeadHours, 1).Should().Be(2);
    }

    [Test]
    public void A_season_reached_late_gets_only_the_latest_follow_up_due()
    {
        //assigned two hours from the end: only #3, not #1, #2 and #3 at once
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddHours(-2), DefaultLeadHours, 0).Should().Be(3);
    }

    [Test]
    public void Nothing_is_due_once_the_season_has_ended()
    {
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc, DefaultLeadHours, 0).Should().BeNull();
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddHours(1), DefaultLeadHours, 0).Should().BeNull();
    }

    [Test]
    public void A_follow_up_switched_off_is_skipped()
    {
        var leadHours = new Dictionary<int, int> { [1] = 30 * 24, [3] = 24 };

        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddDays(-6), leadHours, 1).Should().BeNull();
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddHours(-23), leadHours, 1).Should().Be(3);
    }

    [Test]
    public void A_follow_up_that_fell_due_before_the_start_date_is_not_sent()
    {
        //switched on five days before the end: #1 and #2 fell due before, so nothing until #3 is due
        var startDateUtc = PeriodEndUtc.AddDays(-5);

        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddDays(-4), DefaultLeadHours, 0, startDateUtc).Should().BeNull();
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddHours(-20), DefaultLeadHours, 0, startDateUtc).Should().Be(3);
    }

    [Test]
    public void A_start_date_before_every_follow_up_changes_nothing()
    {
        var startDateUtc = PeriodEndUtc.AddDays(-40);

        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddDays(-6), DefaultLeadHours, 0, startDateUtc).Should().Be(2);
    }

    [Test]
    public void No_follow_up_is_due_without_lead_times()
    {
        SubsidyExpiryReminderHelper.GetDueFollowUp(PeriodEndUtc, PeriodEndUtc.AddHours(-1), new Dictionary<int, int>(), 0).Should().BeNull();
    }

    #endregion

    #region Left to spend

    private static SubsidyPackNameCredit SubsidyCredit(decimal credit, decimal used, decimal topup = 0, decimal usedTopup = 0)
        => new() { Id = 5, Credit = credit, UsedCredit = used, CreditTopup = topup, UsedCreditTopup = usedTopup };

    private static ProductGroupPackNameCredit GroupCredit(int quantity, int usedQuantity, decimal credit = 0, decimal usedCredit = 0, int quantityTopup = 0, int usedQuantityTopup = 0)
        => new() { Id = 5, Quantity = quantity, UsedQuantity = usedQuantity, Credit = credit, UsedCredit = usedCredit, QuantityTopup = quantityTopup, UsedQuantityTopup = usedQuantityTopup };

    [Test]
    public void Credit_left_counts_the_subsidy_and_its_top_ups()
    {
        SubsidyExpiryReminderHelper.RemainingToSpend(SubsidyCredit(100, 40, 20, 5), []).Should().Be((75m, 0));
    }

    [Test]
    public void Product_groups_add_their_credit_and_items()
    {
        var groups = new[] { GroupCredit(3, 1, credit: 50, usedCredit: 10), GroupCredit(2, 0, quantityTopup: 1) };

        SubsidyExpiryReminderHelper.RemainingToSpend(SubsidyCredit(0, 0), groups).Should().Be((40m, 5));
    }

    [Test]
    public void A_bucket_spent_beyond_its_amount_counts_as_empty()
    {
        //an order can overspend a bucket the next one covers; the overspend does not reduce what another bucket holds
        var groups = new[] { GroupCredit(1, 3) };

        SubsidyExpiryReminderHelper.RemainingToSpend(SubsidyCredit(100, 120, 30, 0), groups).Should().Be((30m, 0));
    }

    [Test]
    public void Everything_spent_leaves_nothing()
    {
        SubsidyExpiryReminderHelper.RemainingToSpend(SubsidyCredit(100, 100), [GroupCredit(2, 2)]).Should().Be((0m, 0));
    }

    #endregion

    #region Days left and lead times

    [Test]
    public void Days_left_count_today()
    {
        SubsidyExpiryReminderHelper.DaysLeft(new DateTime(2026, 11, 30), new DateTime(2026, 11, 30)).Should().Be(1);
        SubsidyExpiryReminderHelper.DaysLeft(new DateTime(2026, 11, 30), new DateTime(2026, 11, 24)).Should().Be(7);
    }

    [TestCase(5, (int)MessageDelayPeriod.Hours, 5)]
    [TestCase(2, (int)MessageDelayPeriod.Days, 48)]
    [TestCase(0, (int)MessageDelayPeriod.Days, 0)]
    [TestCase(-3, (int)MessageDelayPeriod.Hours, 0)]
    [TestCase(4, 99, 0)]
    [TestCase(int.MaxValue, (int)MessageDelayPeriod.Days, int.MaxValue)]
    public void Lead_hours_answer_every_input_without_throwing(int delayBeforeSend, int delayPeriodId, int expected)
    {
        SubsidyExpiryReminderHelper.LeadHours(delayBeforeSend, delayPeriodId).Should().Be(expected);
    }

    [Test]
    public void Lead_times_must_shrink_from_the_first_follow_up_to_the_last()
    {
        SubsidyExpiryReminderHelper.LeadTimesDecrease([(true, 720), (true, 168), (true, 24)]).Should().BeTrue();
        SubsidyExpiryReminderHelper.LeadTimesDecrease([(true, 24), (true, 168), (true, 720)]).Should().BeFalse();
        SubsidyExpiryReminderHelper.LeadTimesDecrease([(true, 168), (true, 168)]).Should().BeFalse();
    }

    [Test]
    public void A_follow_up_switched_off_does_not_count_in_the_order()
    {
        SubsidyExpiryReminderHelper.LeadTimesDecrease([(true, 720), (false, 1000), (true, 24)]).Should().BeTrue();
        SubsidyExpiryReminderHelper.LeadTimesDecrease([(false, 0), (false, 0), (false, 0)]).Should().BeTrue();
    }

    #endregion

    #region Start date on save

    private static readonly DateTime OldStart = new(2026, 1, 10, 9, 0, 0);
    private static readonly DateTime TypedStart = new(2026, 11, 1, 0, 0, 0);

    [Test]
    public void Switching_on_for_the_first_time_starts_afresh()
        => SubsidyExpiryReminderHelper.StartsAfresh(true, false, null, null, false).Should().BeTrue();

    [Test]
    public void Switching_back_on_with_the_old_date_untouched_starts_afresh()
        => SubsidyExpiryReminderHelper.StartsAfresh(true, false, OldStart, OldStart, false).Should().BeTrue();

    [Test]
    public void Switching_on_with_a_date_typed_keeps_that_date()
        => SubsidyExpiryReminderHelper.StartsAfresh(true, false, TypedStart, OldStart, false).Should().BeFalse();

    [Test]
    public void Switching_on_at_a_store_scope_without_its_own_date_starts_afresh()
        => SubsidyExpiryReminderHelper.StartsAfresh(true, false, null, OldStart, true).Should().BeTrue();

    [Test]
    public void A_save_while_on_keeps_the_date()
        => SubsidyExpiryReminderHelper.StartsAfresh(true, true, OldStart, OldStart, false).Should().BeFalse();

    [Test]
    public void A_save_while_on_with_the_date_emptied_starts_afresh()
        => SubsidyExpiryReminderHelper.StartsAfresh(true, true, null, OldStart, false).Should().BeTrue();

    [Test]
    public void A_store_that_inherits_the_date_keeps_inheriting_while_on()
        => SubsidyExpiryReminderHelper.StartsAfresh(true, true, null, OldStart, true).Should().BeFalse();

    [Test]
    public void Switching_off_never_starts_afresh()
        => SubsidyExpiryReminderHelper.StartsAfresh(false, true, null, OldStart, false).Should().BeFalse();

    #endregion
}
