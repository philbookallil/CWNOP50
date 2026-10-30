namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// The subsidy ordering period, as arithmetic. Pure — no clock of its own, no database.
/// </summary>
/// <remarks>
/// This is the C# half of one rule whose other half is SQL: <c>[dbo].[subsidy_current_period]</c> and
/// <c>[dbo].[subsidy_topup_period_end]</c> in <see cref="Services.DataBase.CWDatabaseService"/>. The
/// two must agree. The SQL decides what a packname may spend; these decide which period the admin
/// screen adds a topup to. They have disagreed before — the admin dialog excluded a topup created during
/// the working day on the last day of a non-annual period, so <i>Add</i> inserted a second row while SQL
/// counted both.
///
/// A subsidy period is expressed in the STORE'S local time: a season ending on
/// 30 September ends when the 30th ends in the store's own zone. Topup rows carry UTC timestamps, so the
/// window returned here does the arithmetic locally and converts the boundaries back to UTC.
///
/// The end a period is measured from is the packname's EFFECTIVE end — see
/// <see cref="EffectiveDurationEnd"/> — which is what the SQL views bucket on too.
///
/// Change one half and change the other, and keep <c>SubsidyPeriodHelperTests</c> green.
/// </remarks>
public static class SubsidyPeriodHelper
{
    /// <summary>
    /// The same month and day in another year, clamped to the last day of the month when that day does not
    /// exist there — 29 February becomes 28 February in a non-leap year.
    /// </summary>
    /// <remarks>
    /// Constructing the date directly threw <see cref="ArgumentOutOfRangeException"/> from every page that
    /// read a packname's ordering state. The SQL side clamps the same way, with EOMONTH.
    /// </remarks>
    /// <param name="date">The date whose month and day are wanted</param>
    /// <param name="year">The year to place them in</param>
    /// <returns>Midnight on the anniversary, carrying <paramref name="date"/>'s <see cref="DateTimeKind"/></returns>
    public static DateTime Anniversary(DateTime date, int year)
    {
        var day = Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month));

        return new DateTime(year, date.Month, day, 0, 0, 0, date.Kind);
    }

    /// <summary>
    /// The last second of a day, as a zoneless wall-clock value.
    /// </summary>
    /// <remarks>
    /// A period's end is stored as a <c>date</c>, so reading it as an instant gives midnight — the moment
    /// the last day BEGINS. Every bound derived from it has to be the moment that day ENDS instead.
    /// The result is deliberately <see cref="DateTimeKind.Unspecified"/>: it is a local wall-clock time
    /// with no zone yet, and <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/> throws if
    /// handed a value already marked UTC.
    /// </remarks>
    /// <param name="date">The date whose day is wanted</param>
    /// <returns>23:59:59 on that day, <see cref="DateTimeKind.Unspecified"/></returns>
    public static DateTime EndOfLocalDay(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Unspecified);
    }

    /// <summary>
    /// A UTC instant as the store's wall clock reads it.
    /// </summary>
    /// <param name="utc">The instant</param>
    /// <param name="storeTimeZone">The store's time zone</param>
    /// <returns>The same instant in store-local terms</returns>
    public static DateTime ToStoreLocal(DateTime utc, TimeZoneInfo storeTimeZone)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), storeTimeZone);
    }

    /// <summary>
    /// A store wall-clock value as a UTC instant, so it can be compared with a stored CreatedOnUtc.
    /// </summary>
    /// <param name="storeLocal">The wall-clock value</param>
    /// <param name="storeTimeZone">The store's time zone</param>
    /// <returns>The corresponding UTC instant</returns>
    public static DateTime ToUtc(DateTime storeLocal, TimeZoneInfo storeTimeZone)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(storeLocal, DateTimeKind.Unspecified), storeTimeZone);
    }

    /// <summary>
    /// The end date a packname's period is measured from: the variation's when the store lets packnames own
    /// their dates and the variation sets one, otherwise the subsidy's.
    /// </summary>
    /// <remarks>
    /// The same <c>CASE WHEN vset.PackNamesOwnStartAndFinishDates = 1 THEN COALESCE(svar.DurationEnd, s.DurationEnd)
    /// ELSE s.DurationEnd END</c> the credit views apply. A variation row can exist while the setting is off
    /// (switched off later, or a row that varies only the credit), and in that state it must be ignored here
    /// as it is there, or the topup window and the SQL bucket disagree for exactly those packnames.
    /// </remarks>
    /// <param name="subsidyDurationEnd">The subsidy's own end date</param>
    /// <param name="variationDurationEnd">The variation's end date, if the packname has a variation with one</param>
    /// <param name="packNamesOwnStartAndFinishDates">The store setting that lets a variation carry dates</param>
    /// <returns>The end date to measure the period from</returns>
    public static DateTime EffectiveDurationEnd(DateTime subsidyDurationEnd, DateTime? variationDurationEnd, bool packNamesOwnStartAndFinishDates)
    {
        return packNamesOwnStartAndFinishDates && variationDurationEnd.HasValue
            ? variationDurationEnd.Value
            : subsidyDurationEnd;
    }

    /// <summary>
    /// The start date a packname's period is measured from: the variation's when the store lets packnames
    /// own their dates and the variation sets one, otherwise the subsidy's.
    /// </summary>
    /// <remarks>
    /// The start counterpart of <see cref="EffectiveDurationEnd"/>, and the same
    /// <c>CASE WHEN vset.PackNamesOwnStartAndFinishDates = 1 THEN COALESCE(svar.DurationStart, s.DurationStart)
    /// ELSE s.DurationStart END</c> the views apply.
    /// </remarks>
    /// <param name="subsidyDurationStart">The subsidy's own start date</param>
    /// <param name="variationDurationStart">The variation's start date, if the packname has a variation with one</param>
    /// <param name="packNamesOwnStartAndFinishDates">The store setting that lets a variation carry dates</param>
    /// <returns>The start date to measure the period from</returns>
    public static DateTime EffectiveDurationStart(DateTime subsidyDurationStart, DateTime? variationDurationStart, bool packNamesOwnStartAndFinishDates)
    {
        return packNamesOwnStartAndFinishDates && variationDurationStart.HasValue
            ? variationDurationStart.Value
            : subsidyDurationStart;
    }

    /// <summary>
    /// The next day after <paramref name="today"/> on which a period opens or closes: a period opens at the
    /// start of its first day and closes at the start of the day after its last. Null when neither will
    /// happen again.
    /// </summary>
    /// <remarks>
    /// Dates, not instants. <paramref name="today"/> is the STORE'S date, as the views' LocalToday is, and the
    /// result is the store-local midnight at which the change happens; convert it with <see cref="ToUtc"/>.
    /// An annual period is looked at on its anniversaries in this year and the next.
    /// </remarks>
    /// <param name="durationStart">The period's effective start date — see <see cref="EffectiveDurationStart"/></param>
    /// <param name="durationEnd">The period's effective end date — see <see cref="EffectiveDurationEnd"/></param>
    /// <param name="annualRecurrence">Whether the period repeats every year</param>
    /// <param name="today">The store's date</param>
    /// <returns>The store-local midnight of the next change, <see cref="DateTimeKind.Unspecified"/>; null when there is none</returns>
    public static DateTime? NextOrderingStatusChange(DateTime durationStart, DateTime durationEnd, bool annualRecurrence, DateTime today)
    {
        var dates = new List<DateTime> { durationStart.Date, durationEnd.Date.AddDays(1) };

        if (annualRecurrence)
        {
            foreach (var year in new[] { today.Year, today.Year + 1 })
            {
                dates.Add(Anniversary(durationStart, year).Date);
                dates.Add(Anniversary(durationEnd, year).Date.AddDays(1));
            }
        }

        var futureDates = dates.Where(date => date > today.Date).ToList();

        return futureDates.Count == 0
            ? null
            : DateTime.SpecifyKind(futureDates.Min(), DateTimeKind.Unspecified);
    }

    /// <summary>
    /// The range of <c>CreatedOnUtc</c> values whose topup rows belong to the ordering period current at
    /// <paramref name="nowUtc"/>. Bounds are UTC, because that is what the column holds.
    /// </summary>
    /// <remarks>
    /// The lower bound is EXCLUSIVE and the upper bound INCLUSIVE, so two consecutive periods meet exactly
    /// and a topup created in the seam belongs to precisely one of them.
    ///
    /// <b>An absent lower bound is null — never a sentinel date.</b> Until the CWTimestampsDateTime2
    /// migration the topup columns were <c>datetimeoffset</c>, every bound reached SQL Server
    /// through <c>new DateTimeOffset(DateTime)</c>, and <see cref="DateTime.MinValue"/> THREW on any server
    /// ahead of UTC. The columns are <c>datetime2</c> holding UTC now. Both real bounds below come from
    /// <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/>, which returns kind
    /// <see cref="DateTimeKind.Utc"/>, so they are exact on either column type.
    /// </remarks>
    /// <param name="nowUtc">The instant the question is asked</param>
    /// <param name="annualRecurrence">Whether the period repeats every year</param>
    /// <param name="durationEnd">The period's effective end date — see <see cref="EffectiveDurationEnd"/></param>
    /// <param name="storeTimeZone">The store's time zone — the frame the period is expressed in</param>
    /// <returns>The exclusive lower bound (null for a non-annual period, which has none) and the inclusive upper bound</returns>
    public static (DateTime? WindowStart, DateTime? WindowEnd) GetTopupSearchWindow(DateTime nowUtc, bool annualRecurrence, DateTime durationEnd, TimeZoneInfo storeTimeZone)
    {
        //A single fixed period. The bound is the END of the closing day IN THE STORE'S ZONE: a topup added
        //at 10:00 on the day the period closes belongs to the period closing that day, not to nothing.
        if (!annualRecurrence)
            return (null, ToUtc(EndOfLocalDay(durationEnd), storeTimeZone));

        //The YEAR is the store's, not UTC's. At 22:00 UTC on 31 December a UTC+10 store is already in January.
        var nowLocal = ToStoreLocal(nowUtc, storeTimeZone);
        var thisYearEndLocal = EndOfLocalDay(Anniversary(durationEnd, nowLocal.Year));

        //Past this year's close - the open period is next year's.
        if (thisYearEndLocal < nowLocal)
            return (ToUtc(thisYearEndLocal, storeTimeZone),
                    ToUtc(EndOfLocalDay(Anniversary(durationEnd, nowLocal.Year + 1)), storeTimeZone));

        return (ToUtc(EndOfLocalDay(Anniversary(durationEnd, nowLocal.Year - 1)), storeTimeZone),
                ToUtc(thisYearEndLocal, storeTimeZone));
    }
}