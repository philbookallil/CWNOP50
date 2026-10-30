using Nop.Plugin.CW.Core.Domain.Subsidies.Views;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// How an assignment stands for the customer looking at it: open for ordering, not started yet, or closed.
/// </summary>
public enum CustomerSubsidyStatus
{
    Open = 1,
    NotStarted = 2,
    Closed = 3
}

/// <summary>
/// Pure figures for the customer's subsidy page: which season of a subsidy the customer is looking at today,
/// how that season stands, and the percentage a bar shows.
/// </summary>
public static class CustomerSubsidyHelper
{
    /// <summary>
    /// The season a customer is looking at today: for an annual subsidy the first anniversary season that has
    /// not ended yet (this year's while it runs, next year's once it has ended); for a one-off subsidy its only
    /// period.
    /// </summary>
    /// <param name="durationStart">The period's effective start date</param>
    /// <param name="durationEnd">The period's effective end date</param>
    /// <param name="annualRecurrence">Whether the period repeats every year</param>
    /// <param name="today">The store's date</param>
    /// <returns>The season's first and last day</returns>
    public static (DateTime Start, DateTime End) SeasonFor(DateTime durationStart, DateTime durationEnd, bool annualRecurrence, DateTime today)
    {
        var start = durationStart.Date;
        var end = durationEnd.Date;

        if (!annualRecurrence)
            return (start, end);

        var yearSpan = end.Year - start.Year;
        var season = (start, end);

        foreach (var year in new[] { today.Year - 1, today.Year, today.Year + 1 })
        {
            var seasonStart = SubsidyPeriodHelper.Anniversary(start, year).Date;
            var seasonEnd = SubsidyPeriodHelper.Anniversary(end, year + yearSpan).Date;

            season = (seasonStart, seasonEnd);

            if (today.Date <= seasonEnd)
                break;
        }

        return season;
    }

    /// <summary>
    /// How the season stands today.
    /// </summary>
    /// <param name="inOrderingPeriod">Whether the assignment can order today, as the reporting query says</param>
    /// <param name="seasonStart">The season's first day, from <see cref="SeasonFor"/></param>
    /// <param name="today">The store's date</param>
    public static CustomerSubsidyStatus StatusFor(bool inOrderingPeriod, DateTime seasonStart, DateTime today)
    {
        if (inOrderingPeriod)
            return CustomerSubsidyStatus.Open;

        return today.Date < seasonStart.Date
            ? CustomerSubsidyStatus.NotStarted
            : CustomerSubsidyStatus.Closed;
    }

    /// <summary>
    /// Every season of an assignment up to the one the customer is looking at today, oldest first: the anniversary
    /// seasons from the first one, ending with <see cref="SeasonFor"/>'s; for a one-off subsidy its only period.
    /// </summary>
    /// <param name="durationStart">The period's effective start date</param>
    /// <param name="durationEnd">The period's effective end date</param>
    /// <param name="annualRecurrence">Whether the period repeats every year</param>
    /// <param name="today">The store's date</param>
    /// <returns>Each season's first and last day, oldest first</returns>
    public static IList<(DateTime Start, DateTime End)> SeasonsUpTo(DateTime durationStart, DateTime durationEnd, bool annualRecurrence, DateTime today)
    {
        var current = SeasonFor(durationStart, durationEnd, annualRecurrence, today);

        if (!annualRecurrence)
            return [current];

        var start = durationStart.Date;
        var end = durationEnd.Date;
        var yearSpan = end.Year - start.Year;
        var seasons = new List<(DateTime Start, DateTime End)>();

        for (var year = start.Year; year <= current.Start.Year; year++)
        {
            var season = (Start: SubsidyPeriodHelper.Anniversary(start, year).Date, End: SubsidyPeriodHelper.Anniversary(end, year + yearSpan).Date);

            if (season.Start >= current.Start)
                break;

            seasons.Add(season);
        }

        seasons.Add(current);

        return seasons;
    }

    /// <summary>
    /// The season a dated movement belongs to: the first whose last day is on or after the movement's store date. A
    /// top-up made between two seasons counts towards the next one, as the subsidy's figures count it.
    /// </summary>
    /// <param name="storeDate">The movement's date on the store's calendar</param>
    /// <param name="seasons">The seasons, oldest first (<see cref="SeasonsUpTo"/>)</param>
    /// <returns>The season's index; the last season for a movement after all of them</returns>
    public static int SeasonIndexFor(DateTime storeDate, IList<(DateTime Start, DateTime End)> seasons)
    {
        for (var index = 0; index < seasons.Count; index++)
        {
            if (storeDate.Date <= seasons[index].End.Date)
                return index;
        }

        return seasons.Count - 1;
    }

    /// <summary>
    /// Whether a history movement stands for every season from the day it was made (the assignment, a product group's
    /// allocation, a change of credit or dates) rather than belonging to one season (a top-up, an order, a refund)
    /// </summary>
    public static bool IsStanding(HistoryType historyType)
    {
        return historyType is HistoryType.Subsidy or HistoryType.ProductGroup or HistoryType.SubsidyVariation;
    }

    /// <summary>
    /// The share of a total that has been used, as a whole percentage between 0 and 100; 0 when there is
    /// nothing to measure against.
    /// </summary>
    public static int UsedPercent(decimal total, decimal used)
    {
        if (total <= decimal.Zero)
            return 0;

        var percent = (int)Math.Round(used / total * 100, MidpointRounding.AwayFromZero);

        return Math.Clamp(percent, 0, 100);
    }
}