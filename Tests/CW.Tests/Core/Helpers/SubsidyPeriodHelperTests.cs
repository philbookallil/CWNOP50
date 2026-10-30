using AwesomeAssertions;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The subsidy ordering period as arithmetic — the C# half of the rule whose other half is SQL.
/// </summary>
/// <remarks>
/// Four things are pinned here, and all four were live defects.
/// <para>
/// <b>A period is expressed in the STORE'S local time.</b> A campaign that ends on 30 September ends at
/// 23:59:59 local on the 30th, not at 23:59:59 UTC — which in UTC+10 is 09:59:59 on 1 October local. The
/// topup rows are stored with UTC timestamps, so the window this helper returns is in UTC: it does the
/// arithmetic locally and converts the boundaries back.
/// </para>
/// <para>
/// <b>The window's upper bound is the END of the closing day.</b> It used to be the DurationEnd column,
/// which is a <c>date</c> and therefore midnight. On the last day of a non-annual period <i>Add</i> searched
/// a window that had already closed, found nothing, and inserted a SECOND topup row instead of adding to the
/// existing one, while SQL counted both — so available and used credit were double-counted. Nothing
/// about this is visible in the dialog: its <i>Topup Credit</i> field is an amount to ADD and always opens at
/// zero by design, so the symptom is the row count and the running total, never the field.
/// </para>
/// <para>
/// <b>An anniversary clamps to the last day of the month.</b> Constructing the date directly threw
/// ArgumentOutOfRangeException for a 29 February subsidy in a non-leap year, from every page that reads a
/// packname's ordering state.
/// </para>
/// <para>
/// <b>The end that buckets a topup is the packname's EFFECTIVE end.</b> Where the store lets a packname carry
/// its own dates, a variation's end replaces the subsidy's; where it does not, the variation is ignored even
/// when a row exists. Windowing on the subsidy's end alone put a varied packname's topups into the wrong
/// period, and the SQL views bucket on the effective end.
/// </para>
/// If these change, the SQL functions change with them. The SQL decides what a packname may spend and
/// these decide which period the admin screen adds to; a disagreement between the two is exactly how the
/// duplicate-row defect happened.
/// </remarks>
[TestFixture]
public class SubsidyPeriodHelperTests
{
    #region Utilities

    /// <summary>
    /// A fixed +10:00 zone with no daylight saving. A custom zone rather than a real one so the tests give
    /// the same answer on any machine and in any year — a named zone's rules can be updated by the OS.
    /// </summary>
    private static readonly TimeZoneInfo StoreZone =
        TimeZoneInfo.CreateCustomTimeZone("CW.Tests.Plus10", TimeSpan.FromHours(10), "CW.Tests +10", "CW.Tests +10");

    /// <summary>A zone that is behind UTC, so an off-by-one in the wrong direction cannot pass both ways.</summary>
    private static readonly TimeZoneInfo BehindUtcZone =
        TimeZoneInfo.CreateCustomTimeZone("CW.Tests.Minus5", TimeSpan.FromHours(-5), "CW.Tests -5", "CW.Tests -5");

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        => new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    /// <summary>A wall-clock value with no zone attached — what "local" means to this helper.</summary>
    private static DateTime Local(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        => new(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

    /// <summary>A date column's value: midnight, no zone.</summary>
    private static DateTime PeriodDate(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    #endregion

    #region Anniversary

    [Test]
    public void Anniversary_OrdinaryDate_KeepsMonthAndDay()
    {
        SubsidyPeriodHelper.Anniversary(PeriodDate(2025, 9, 30), 2027)
            .Should().Be(PeriodDate(2027, 9, 30));
    }

    [Test]
    public void Anniversary_Feb29_InNonLeapYear_ClampsTo28()
    {
        SubsidyPeriodHelper.Anniversary(PeriodDate(2024, 2, 29), 2027)
            .Should().Be(PeriodDate(2027, 2, 28));
    }

    [Test]
    public void Anniversary_Feb29_InLeapYear_StaysOn29()
    {
        SubsidyPeriodHelper.Anniversary(PeriodDate(2024, 2, 29), 2028)
            .Should().Be(PeriodDate(2028, 2, 29));
    }

    [Test]
    public void Anniversary_LastDayOfA31DayMonth_IsNotClamped()
    {
        SubsidyPeriodHelper.Anniversary(PeriodDate(2025, 1, 31), 2026)
            .Should().Be(PeriodDate(2026, 1, 31));
    }

    [Test]
    public void Anniversary_DropsTheTimeOfDay()
    {
        SubsidyPeriodHelper.Anniversary(Local(2025, 7, 1, 13, 45, 12), 2026)
            .Should().Be(PeriodDate(2026, 7, 1));
    }

    #endregion

    #region EndOfLocalDay

    [Test]
    public void EndOfLocalDay_Midnight_IsTheLastSecondOfTheSameDay()
    {
        SubsidyPeriodHelper.EndOfLocalDay(PeriodDate(2026, 9, 30))
            .Should().Be(Local(2026, 9, 30, 23, 59, 59));
    }

    [Test]
    public void EndOfLocalDay_Midday_IsTheSameLastSecond()
    {
        SubsidyPeriodHelper.EndOfLocalDay(Local(2026, 9, 30, 12, 0, 0))
            .Should().Be(Local(2026, 9, 30, 23, 59, 59));
    }

    /// <summary>
    /// The result must carry no zone. TimeZoneInfo.ConvertTimeToUtc throws on a Utc-kinded input, so a
    /// stray Kind here would break every window conversion below.
    /// </summary>
    [Test]
    public void EndOfLocalDay_ResultIsZoneless()
    {
        SubsidyPeriodHelper.EndOfLocalDay(PeriodDate(2026, 9, 30))
            .Kind.Should().Be(DateTimeKind.Unspecified);
    }

    #endregion

    #region ToStoreLocal / ToUtc

    [Test]
    public void ToStoreLocal_ShiftsForwardByTheOffset()
    {
        SubsidyPeriodHelper.ToStoreLocal(Utc(2026, 6, 30, 22, 0, 0), StoreZone)
            .Should().Be(Local(2026, 7, 1, 8, 0, 0));
    }

    [Test]
    public void ToUtc_ShiftsBackByTheOffset()
    {
        SubsidyPeriodHelper.ToUtc(Local(2026, 9, 30, 23, 59, 59), StoreZone)
            .Should().Be(Utc(2026, 9, 30, 13, 59, 59));
    }

    [Test]
    public void ToStoreLocal_ThenToUtc_RoundTrips()
    {
        var original = Utc(2026, 3, 15, 4, 32, 11);

        SubsidyPeriodHelper.ToUtc(SubsidyPeriodHelper.ToStoreLocal(original, StoreZone), StoreZone)
            .Should().Be(original);
    }

    #endregion

    #region EffectiveDurationEnd

    [Test]
    public void EffectiveDurationEnd_StoreLetsPackNamesOwnDates_VariationEndWins()
    {
        SubsidyPeriodHelper.EffectiveDurationEnd(PeriodDate(2026, 9, 30), PeriodDate(2026, 11, 15), packNamesOwnStartAndFinishDates: true)
            .Should().Be(PeriodDate(2026, 11, 15));
    }

    [Test]
    public void EffectiveDurationEnd_StoreLetsPackNamesOwnDates_NoVariationEnd_FallsBackToTheSubsidy()
    {
        SubsidyPeriodHelper.EffectiveDurationEnd(PeriodDate(2026, 9, 30), null, packNamesOwnStartAndFinishDates: true)
            .Should().Be(PeriodDate(2026, 9, 30));
    }

    /// <summary>
    /// A variation row can exist while the store setting is off — the setting was switched off after the
    /// row was written, or the row only varies the credit. The SQL views ignore it in that state, and so
    /// must this, or the C# window and the SQL bucket disagree for exactly those packnames.
    /// </summary>
    [Test]
    public void EffectiveDurationEnd_StoreDoesNotLetPackNamesOwnDates_VariationIsIgnored()
    {
        SubsidyPeriodHelper.EffectiveDurationEnd(PeriodDate(2026, 9, 30), PeriodDate(2026, 11, 15), packNamesOwnStartAndFinishDates: false)
            .Should().Be(PeriodDate(2026, 9, 30));
    }

    #endregion

    #region EffectiveDurationStart

    [Test]
    public void EffectiveDurationStart_SettingOn_VariationSetsOne_UsesTheVariation()
    {
        SubsidyPeriodHelper.EffectiveDurationStart(PeriodDate(2026, 7, 1), PeriodDate(2026, 8, 1), packNamesOwnStartAndFinishDates: true)
            .Should().Be(PeriodDate(2026, 8, 1));
    }

    [Test]
    public void EffectiveDurationStart_SettingOff_IgnoresTheVariation()
    {
        SubsidyPeriodHelper.EffectiveDurationStart(PeriodDate(2026, 7, 1), PeriodDate(2026, 8, 1), packNamesOwnStartAndFinishDates: false)
            .Should().Be(PeriodDate(2026, 7, 1));
    }

    [Test]
    public void EffectiveDurationStart_SettingOn_NoVariationDate_UsesTheSubsidy()
    {
        SubsidyPeriodHelper.EffectiveDurationStart(PeriodDate(2026, 7, 1), null, packNamesOwnStartAndFinishDates: true)
            .Should().Be(PeriodDate(2026, 7, 1));
    }

    #endregion

    #region NextOrderingStatusChange

    /// <summary>Before the period: the next change is the day it opens.</summary>
    [Test]
    public void NextChange_BeforeThePeriod_IsTheStart()
    {
        SubsidyPeriodHelper.NextOrderingStatusChange(PeriodDate(2026, 7, 1), PeriodDate(2026, 9, 30), annualRecurrence: false, today: PeriodDate(2026, 6, 15))
            .Should().Be(PeriodDate(2026, 7, 1));
    }

    /// <summary>Inside the period, the start day included: the next change is the day after the last day.</summary>
    [TestCase(7, 1)]
    [TestCase(9, 30)]
    public void NextChange_InsideThePeriod_IsTheDayAfterTheEnd(int month, int day)
    {
        SubsidyPeriodHelper.NextOrderingStatusChange(PeriodDate(2026, 7, 1), PeriodDate(2026, 9, 30), annualRecurrence: false, today: PeriodDate(2026, month, day))
            .Should().Be(PeriodDate(2026, 10, 1));
    }

    [Test]
    public void NextChange_NonAnnual_AfterThePeriod_IsNone()
    {
        SubsidyPeriodHelper.NextOrderingStatusChange(PeriodDate(2026, 7, 1), PeriodDate(2026, 9, 30), annualRecurrence: false, today: PeriodDate(2026, 10, 1))
            .Should().BeNull();
    }

    [Test]
    public void NextChange_Annual_AfterThisYearsPeriod_IsNextYearsStart()
    {
        SubsidyPeriodHelper.NextOrderingStatusChange(PeriodDate(2024, 7, 1), PeriodDate(2024, 9, 30), annualRecurrence: true, today: PeriodDate(2026, 11, 5))
            .Should().Be(PeriodDate(2027, 7, 1));
    }

    [Test]
    public void NextChange_Annual_InsideThisYearsPeriod_IsTheDayAfterThisYearsEnd()
    {
        SubsidyPeriodHelper.NextOrderingStatusChange(PeriodDate(2024, 7, 1), PeriodDate(2024, 9, 30), annualRecurrence: true, today: PeriodDate(2026, 8, 10))
            .Should().Be(PeriodDate(2026, 10, 1));
    }

    [Test]
    public void NextChange_Annual_Feb29End_InNonLeapYear_ClosesOnMarch1()
    {
        SubsidyPeriodHelper.NextOrderingStatusChange(PeriodDate(2024, 1, 1), PeriodDate(2024, 2, 29), annualRecurrence: true, today: PeriodDate(2027, 2, 10))
            .Should().Be(PeriodDate(2027, 3, 1));
    }

    /// <summary>The result is a store wall-clock value, so ToUtc can place it: kind Unspecified.</summary>
    [Test]
    public void NextChange_IsUnspecifiedKind()
    {
        SubsidyPeriodHelper.NextOrderingStatusChange(PeriodDate(2026, 7, 1), PeriodDate(2026, 9, 30), annualRecurrence: false, today: PeriodDate(2026, 6, 15))!
            .Value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    #endregion

    #region GetTopupSearchWindow — non-annual

    /// <summary>
    /// An absent lower bound is null, never <see cref="DateTime.MinValue"/>. The sentinel reached SQL Server
    /// as a parameter for a <c>datetimeoffset</c> column, and coercing 0001-01-01 to a DateTimeOffset throws
    /// outright in any zone ahead of UTC — the admin *Product Groups* grid died with "Failed to convert
    /// parameter value from a DateTime to a DateTimeOffset" for every non-annual subsidy.
    /// </summary>
    [Test]
    public void TopupWindow_NonAnnual_LowerBound_IsNullNotTheDateTimeSentinel()
    {
        var (start, _) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 5, 1), annualRecurrence: false, durationEnd: PeriodDate(2026, 9, 30), StoreZone);

        start.Should().NotBe(DateTime.MinValue);
        start.Should().BeNull();
    }

    /// <summary>
    /// A period ending 30 September in a +10 store ends at 13:59:59 UTC on the 30th — the last second of
    /// the 30th LOCALLY. Reading the bound as UTC midnight (the old behaviour) cut the closing day short by
    /// a whole day; reading it as 23:59:59 UTC would extend it ten hours into 1 October local.
    /// </summary>
    [Test]
    public void TopupWindow_NonAnnual_EndsAtTheCloseOfTheLastLocalDay()
    {
        var (start, end) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 5, 1), annualRecurrence: false, durationEnd: PeriodDate(2026, 9, 30), StoreZone);

        start.Should().BeNull();
        end.Should().Be(Utc(2026, 9, 30, 13, 59, 59));
    }

    [Test]
    public void TopupWindow_NonAnnual_BehindUtcStore_EndsLaterInUtc()
    {
        var (_, end) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 5, 1), annualRecurrence: false, durationEnd: PeriodDate(2026, 9, 30), BehindUtcZone);

        end.Should().Be(Utc(2026, 10, 1, 4, 59, 59));
    }

    /// <summary>
    /// A topup added during the working day on the period's closing date must fall inside the window, or
    /// <i>Add</i> inserts a duplicate row instead of adding to the one already there.
    /// </summary>
    [Test]
    public void TopupWindow_NonAnnual_OnTheClosingDay_ContainsThatDaysTopup()
    {
        //10:00 local on 30 September in a +10 store.
        var topupCreatedOnUtc = Utc(2026, 9, 30, 0, 0, 0);
        var nowUtc = Utc(2026, 9, 30, 0, 5, 0);

        var (start, end) = SubsidyPeriodHelper.GetTopupSearchWindow(nowUtc, annualRecurrence: false, durationEnd: PeriodDate(2026, 9, 30), StoreZone);

        //A non-annual period has no lower bound at all, so every earlier topup belongs to it.
        start.Should().BeNull();
        topupCreatedOnUtc.Should().BeOnOrBefore(end!.Value);
    }

    /// <summary>
    /// The other side of the same boundary: 08:00 local on 1 October is 22:00 UTC on 30 September. Judged
    /// in UTC that instant is still "the 30th" and would be swept into the closed period; judged locally it
    /// is the next period's.
    /// </summary>
    [Test]
    public void TopupWindow_NonAnnual_MorningAfterTheClose_IsOutsideTheWindow()
    {
        var topupCreatedOnUtc = Utc(2026, 9, 30, 22, 0, 0);

        var (_, end) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 9, 30, 22, 5, 0), annualRecurrence: false, durationEnd: PeriodDate(2026, 9, 30), StoreZone);

        topupCreatedOnUtc.Should().BeAfter(end!.Value);
    }

    /// <summary>
    /// The varied case end to end: a packname whose own end is later than the subsidy's keeps its window
    /// open after the subsidy's close, because the end handed in is the EFFECTIVE one.
    /// </summary>
    [Test]
    public void TopupWindow_NonAnnual_VariedEnd_StaysOpenPastTheSubsidysClose()
    {
        var effectiveEnd = SubsidyPeriodHelper.EffectiveDurationEnd(PeriodDate(2026, 9, 30), PeriodDate(2026, 11, 15), packNamesOwnStartAndFinishDates: true);

        var (_, end) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 10, 10), annualRecurrence: false, durationEnd: effectiveEnd, StoreZone);

        end.Should().Be(Utc(2026, 11, 15, 13, 59, 59));
        Utc(2026, 10, 10).Should().BeOnOrBefore(end!.Value);
    }

    #endregion

    #region GetTopupSearchWindow — annual

    [Test]
    public void TopupWindow_Annual_BeforeThisYearsClose_IsLastYearsCloseToThisYears()
    {
        var (start, end) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 8, 10), annualRecurrence: true, durationEnd: PeriodDate(2025, 9, 30), StoreZone);

        start.Should().Be(Utc(2025, 9, 30, 13, 59, 59));
        end.Should().Be(Utc(2026, 9, 30, 13, 59, 59));
    }

    [Test]
    public void TopupWindow_Annual_AfterThisYearsClose_IsThisYearsCloseToNextYears()
    {
        var (start, end) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 11, 10), annualRecurrence: true, durationEnd: PeriodDate(2025, 9, 30), StoreZone);

        start.Should().Be(Utc(2026, 9, 30, 13, 59, 59));
        end.Should().Be(Utc(2027, 9, 30, 13, 59, 59));
    }

    /// <summary>
    /// The year is chosen from the LOCAL date, not the UTC one. At 22:00 UTC on 31 December a +10 store is
    /// already in January, and the window must be the new year's.
    /// </summary>
    [Test]
    public void TopupWindow_Annual_AcrossNewYearInTheStoresZone_UsesTheLocalYear()
    {
        //31 Dec 2026 22:00 UTC is 1 Jan 2027 08:00 in a +10 store.
        var (_, endLocalYear) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 12, 31, 22, 0, 0), annualRecurrence: true, durationEnd: PeriodDate(2025, 6, 30), StoreZone);

        endLocalYear.Should().Be(Utc(2027, 6, 30, 13, 59, 59));
    }

    [Test]
    public void TopupWindow_Annual_OnTheClosingDay_ContainsThatDaysTopup()
    {
        //10:00 local on 30 September in a +10 store.
        var topupCreatedOnUtc = Utc(2026, 9, 30, 0, 0, 0);

        var (start, end) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 9, 30, 0, 5, 0), annualRecurrence: true, durationEnd: PeriodDate(2025, 9, 30), StoreZone);

        topupCreatedOnUtc.Should().BeAfter(start!.Value);
        topupCreatedOnUtc.Should().BeOnOrBefore(end!.Value);
    }

    /// <summary>
    /// Consecutive occurrences must meet exactly — no overlap, no gap — or a topup created in the seam
    /// belongs to both periods or to neither.
    /// </summary>
    [Test]
    public void TopupWindow_Annual_ConsecutiveWindowsMeetExactly()
    {
        var durationEnd = PeriodDate(2025, 9, 30);

        var (_, thisEnd) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 8, 10), annualRecurrence: true, durationEnd: durationEnd, StoreZone);
        var (nextStart, _) = SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2026, 11, 10), annualRecurrence: true, durationEnd: durationEnd, StoreZone);

        nextStart.Should().Be(thisEnd!.Value);
    }

    [Test]
    public void TopupWindow_Annual_Feb29End_InNonLeapYear_DoesNotThrowAndClamps()
    {
        var act = () => SubsidyPeriodHelper.GetTopupSearchWindow(Utc(2027, 1, 10), annualRecurrence: true, durationEnd: PeriodDate(2024, 2, 29), StoreZone);

        act.Should().NotThrow();

        var (start, end) = act();

        start.Should().Be(Utc(2026, 2, 28, 13, 59, 59));
        end.Should().Be(Utc(2027, 2, 28, 13, 59, 59));
    }

    #endregion
}
