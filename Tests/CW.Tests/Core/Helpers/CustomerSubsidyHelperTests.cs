using AwesomeAssertions;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The figures behind the customer's subsidy page: which season is being looked at, how it stands, and the
/// percentage its bar shows.
/// </summary>
[TestFixture]
public class CustomerSubsidyHelperTests
{
    private static readonly DateTime Start = new(2026, 1, 1);
    private static readonly DateTime End = new(2026, 12, 31);

    [Test]
    public void A_one_off_period_is_its_own_season_whatever_the_day()
    {
        CustomerSubsidyHelper.SeasonFor(Start, End, annualRecurrence: false, today: new DateTime(2028, 6, 1))
            .Should().Be((Start, End));
    }

    [Test]
    public void An_annual_period_shows_this_years_season_while_it_runs()
    {
        CustomerSubsidyHelper.SeasonFor(Start, End, annualRecurrence: true, today: new DateTime(2027, 10, 7))
            .Should().Be((new DateTime(2027, 1, 1), new DateTime(2027, 12, 31)));
    }

    [Test]
    public void An_annual_period_shows_next_years_season_once_this_one_has_ended()
    {
        var start = new DateTime(2026, 3, 1);
        var end = new DateTime(2026, 5, 31);

        CustomerSubsidyHelper.SeasonFor(start, end, annualRecurrence: true, today: new DateTime(2027, 9, 1))
            .Should().Be((new DateTime(2028, 3, 1), new DateTime(2028, 5, 31)));
    }

    [Test]
    public void An_annual_period_that_straddles_the_new_year_keeps_its_year_span()
    {
        var start = new DateTime(2026, 11, 1);
        var end = new DateTime(2027, 2, 28);

        CustomerSubsidyHelper.SeasonFor(start, end, annualRecurrence: true, today: new DateTime(2028, 1, 15))
            .Should().Be((new DateTime(2027, 11, 1), new DateTime(2028, 2, 28)));
    }

    [Test]
    public void A_season_that_has_not_started_is_not_started_and_one_that_has_is_closed()
    {
        var seasonStart = new DateTime(2027, 1, 1);

        CustomerSubsidyHelper.StatusFor(inOrderingPeriod: false, seasonStart, today: new DateTime(2026, 12, 31))
            .Should().Be(CustomerSubsidyStatus.NotStarted);

        CustomerSubsidyHelper.StatusFor(inOrderingPeriod: false, seasonStart, today: new DateTime(2027, 1, 1))
            .Should().Be(CustomerSubsidyStatus.Closed);
    }

    [Test]
    public void An_assignment_that_can_order_is_open_whatever_the_dates_say()
    {
        CustomerSubsidyHelper.StatusFor(inOrderingPeriod: true, new DateTime(2030, 1, 1), today: new DateTime(2026, 10, 7))
            .Should().Be(CustomerSubsidyStatus.Open);
    }

    [TestCase(0, 0, 0)]
    [TestCase(330, 0, 0)]
    [TestCase(330, 82.5, 25)]
    [TestCase(11, 2, 18)]
    [TestCase(50, 100, 100)]
    public void The_used_percentage_is_whole_and_between_0_and_100(decimal total, decimal used, int expected)
    {
        CustomerSubsidyHelper.UsedPercent(total, used).Should().Be(expected);
    }
    [Test]
    public void An_annual_period_lists_every_season_from_its_first_to_the_current_one()
    {
        CustomerSubsidyHelper.SeasonsUpTo(Start, End, annualRecurrence: true, today: new DateTime(2028, 3, 5))
            .Should().Equal((Start, End), (new DateTime(2027, 1, 1), new DateTime(2027, 12, 31)), (new DateTime(2028, 1, 1), new DateTime(2028, 12, 31)));
    }

    [Test]
    public void A_season_across_the_new_year_keeps_its_span_in_every_year()
    {
        CustomerSubsidyHelper.SeasonsUpTo(new DateTime(2025, 7, 1), new DateTime(2026, 6, 30), annualRecurrence: true, today: new DateTime(2026, 9, 1))
            .Should().Equal((new DateTime(2025, 7, 1), new DateTime(2026, 6, 30)), (new DateTime(2026, 7, 1), new DateTime(2027, 6, 30)));
    }

    [Test]
    public void A_one_off_period_has_one_season()
    {
        CustomerSubsidyHelper.SeasonsUpTo(Start, End, annualRecurrence: false, today: new DateTime(2028, 3, 5)).Should().Equal((Start, End));
    }

    [TestCase(2026, 6, 1, 0)]
    [TestCase(2026, 12, 31, 0)]
    [TestCase(2027, 1, 1, 1)]
    [TestCase(2030, 1, 1, 1)]
    public void A_movement_belongs_to_the_first_season_not_closed_before_its_date(int year, int month, int day, int expected)
    {
        var seasons = new List<(DateTime, DateTime)> { (Start, End), (new DateTime(2027, 1, 1), new DateTime(2027, 12, 31)) };

        CustomerSubsidyHelper.SeasonIndexFor(new DateTime(year, month, day), seasons).Should().Be(expected);
    }

    [Test]
    public void A_top_up_between_two_seasons_counts_towards_the_next()
    {
        var seasons = new List<(DateTime, DateTime)> { (new DateTime(2026, 3, 1), new DateTime(2026, 10, 31)), (new DateTime(2027, 3, 1), new DateTime(2027, 10, 31)) };

        CustomerSubsidyHelper.SeasonIndexFor(new DateTime(2026, 12, 15), seasons).Should().Be(1);
    }

    [TestCase(HistoryType.Subsidy, true)]
    [TestCase(HistoryType.ProductGroup, true)]
    [TestCase(HistoryType.SubsidyVariation, true)]
    [TestCase(HistoryType.SubsidyTopUp, false)]
    [TestCase(HistoryType.ProductGroupTopup, false)]
    [TestCase(HistoryType.SubsidyUsed, false)]
    [TestCase(HistoryType.ProductGroupUsed, false)]
    [TestCase(HistoryType.SubsidyUsedForShipping, false)]
    [TestCase(HistoryType.Refund, false)]
    public void What_stands_for_every_season_and_what_belongs_to_one(HistoryType type, bool standing)
    {
        CustomerSubsidyHelper.IsStanding(type).Should().Be(standing);
    }
}
