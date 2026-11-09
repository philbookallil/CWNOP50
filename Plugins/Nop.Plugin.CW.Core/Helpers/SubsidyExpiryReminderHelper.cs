using Nop.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// Pure figures for the subsidy expiry reminders: whether a season is open and when it ends, which follow-up is due,
/// what is left to spend, and whether the follow-ups' lead times are in order. No clock and no database of its own.
/// </summary>
public static class SubsidyExpiryReminderHelper
{
    /// <summary>
    /// The longest lead time a follow-up may have: a year, in hours
    /// </summary>
    public const int MaximumLeadHours = 366 * 24;

    /// <summary>
    /// The season of an assignment open today, and its last day.
    /// </summary>
    /// <remarks>
    /// The same test the credit views make: the first period has begun and today falls inside the current season.
    /// It only picks the candidates; the credit view, which has no row for a season that is not open, decides.
    /// </remarks>
    /// <param name="durationStart">The period's effective start date</param>
    /// <param name="durationEnd">The period's effective end date</param>
    /// <param name="annualRecurrence">Whether the period repeats every year</param>
    /// <param name="today">The store's date</param>
    /// <param name="periodEnd">The last day of the open season; the last day of the season looked at when none is open</param>
    /// <returns>True when a season is open today</returns>
    public static bool TryGetOpenPeriodEnd(DateTime durationStart, DateTime durationEnd, bool annualRecurrence, DateTime today, out DateTime periodEnd)
    {
        var (seasonStart, seasonEnd) = CustomerSubsidyHelper.SeasonFor(durationStart, durationEnd, annualRecurrence, today);
        periodEnd = seasonEnd;

        return today.Date >= durationStart.Date && today.Date >= seasonStart && today.Date <= seasonEnd;
    }

    /// <summary>
    /// The follow-up to send now for a season, or null when none is due.
    /// </summary>
    /// <remarks>
    /// A follow-up is due from its lead time before the end until the end. Of those due, the latest (the highest number)
    /// is the one sent, and only when it comes after the last one sent: an assignment that reaches the window late gets
    /// the reminder for where it stands, not every earlier one at once. A follow-up that fell due before the start date
    /// is not sent at all, so switching the reminders on does not send the reminders that would have gone out before.
    /// </remarks>
    /// <param name="periodEndUtc">When the season ends</param>
    /// <param name="nowUtc">Now</param>
    /// <param name="leadHoursByFollowUp">The lead time in hours of each active follow-up, by number</param>
    /// <param name="lastFollowUpNumber">The last follow-up sent for the season; 0 when none</param>
    /// <param name="startDateUtc">The moment the reminders start from; null for no limit</param>
    /// <returns>The follow-up number, or null</returns>
    public static int? GetDueFollowUp(DateTime periodEndUtc, DateTime nowUtc, IReadOnlyDictionary<int, int> leadHoursByFollowUp, int lastFollowUpNumber, DateTime? startDateUtc = null)
    {
        ArgumentNullException.ThrowIfNull(leadHoursByFollowUp);

        if (nowUtc >= periodEndUtc)
            return null;

        int? due = null;

        foreach (var (number, leadHours) in leadHoursByFollowUp)
        {
            var dueUtc = periodEndUtc.AddHours(-leadHours);

            if (leadHours <= 0 || nowUtc < dueUtc || dueUtc < startDateUtc)
                continue;

            if (due is null || number > due)
                due = number;
        }

        return due > lastFollowUpNumber ? due : null;
    }

    /// <summary>
    /// What an assignment has left to spend in its season: credit (the subsidy's and every product group's, top-ups
    /// included) and items (every product group's, top-ups included). A bucket spent beyond its amount counts as empty.
    /// </summary>
    /// <param name="subsidyCredit">The assignment's row of the subsidy credit view</param>
    /// <param name="productGroupCredits">The assignment's rows of the product-group credit view</param>
    /// <returns>The credit and the items left</returns>
    public static (decimal Credit, int Units) RemainingToSpend(SubsidyPackNameCredit subsidyCredit, IEnumerable<ProductGroupPackNameCredit> productGroupCredits)
    {
        ArgumentNullException.ThrowIfNull(subsidyCredit);

        var credit = Math.Max(decimal.Zero, subsidyCredit.Credit - subsidyCredit.UsedCredit)
            + Math.Max(decimal.Zero, subsidyCredit.CreditTopup - subsidyCredit.UsedCreditTopup);
        var units = 0;

        foreach (var productGroup in productGroupCredits ?? [])
        {
            credit += Math.Max(decimal.Zero, productGroup.Credit - productGroup.UsedCredit)
                + Math.Max(decimal.Zero, productGroup.CreditTopup - productGroup.UsedCreditTopup);
            units += Math.Max(0, productGroup.Quantity - productGroup.UsedQuantity)
                + Math.Max(0, productGroup.QuantityTopup - productGroup.UsedQuantityTopup);
        }

        return (credit, units);
    }

    /// <summary>
    /// The days left in the season, today included: 1 on the last day, 7 a week before it.
    /// </summary>
    /// <remarks>
    /// The time left rounded up to whole days. The season ends at 23:59:59 of its last day, so any moment of a day leaves
    /// that day and the ones up to the last: the 7-day follow-up, sent in the first hour after it falls due, says 7.
    /// </remarks>
    /// <param name="periodEnd">The season's last day</param>
    /// <param name="today">The store's date</param>
    public static int DaysLeft(DateTime periodEnd, DateTime today)
    {
        return (periodEnd.Date - today.Date).Days + 1;
    }

    /// <summary>
    /// Whether a save of the card starts the reminders afresh: the start date becomes the moment of the save.
    /// </summary>
    /// <remarks>
    /// <para>Switching on starts afresh unless the admin typed a new date with it. Without that, switching back on after
    /// months off would keep the old date and send every follow-up that fell due while the reminders were off.</para>
    /// <para>While already on, an emptied date starts afresh too, except at a store scope that inherits the all-stores
    /// date (its date field is not posted then): that store keeps reading the all-stores date.</para>
    /// </remarks>
    /// <param name="enabled">Whether the save leaves the reminders on</param>
    /// <param name="wasEnabled">Whether they were on for the scope before the save</param>
    /// <param name="postedStartDateUtc">The start date posted; null when empty or not posted</param>
    /// <param name="storedStartDateUtc">The start date the scope read before the save</param>
    /// <param name="inheritsStartDate">Whether a store scope keeps the all-stores start date (no override)</param>
    public static bool StartsAfresh(bool enabled, bool wasEnabled, DateTime? postedStartDateUtc, DateTime? storedStartDateUtc, bool inheritsStartDate)
    {
        if (!enabled)
            return false;

        if (!wasEnabled)
            return postedStartDateUtc is null || postedStartDateUtc == storedStartDateUtc;

        return postedStartDateUtc is null && !inheritsStartDate;
    }

    /// <summary>
    /// A follow-up's lead time in hours; 0 when the amount is not positive or the period is not hours or days.
    /// </summary>
    /// <remarks>
    /// nop's <c>MessageDelayPeriod.ToHours</c> throws for an unknown period and overflows for a very large amount; this
    /// answers both with a figure the validator can refuse.
    /// </remarks>
    /// <param name="delayBeforeSend">The amount</param>
    /// <param name="delayPeriodId">The period, as <see cref="MessageDelayPeriod"/></param>
    public static int LeadHours(int delayBeforeSend, int delayPeriodId)
    {
        if (delayBeforeSend <= 0)
            return 0;

        long hours = (MessageDelayPeriod)delayPeriodId switch
        {
            MessageDelayPeriod.Hours => delayBeforeSend,
            MessageDelayPeriod.Days => delayBeforeSend * 24L,
            _ => 0
        };

        return (int)Math.Min(int.MaxValue, hours);
    }

    /// <summary>
    /// Whether every enabled follow-up is sent later than the enabled one before it: #1 the longest before the end,
    /// #3 the shortest. Disabled follow-ups are skipped.
    /// </summary>
    /// <param name="followUps">The follow-ups in number order, each with its lead time in hours</param>
    public static bool LeadTimesDecrease(IEnumerable<(bool Enabled, int LeadHours)> followUps)
    {
        int? previous = null;

        foreach (var (enabled, leadHours) in followUps ?? [])
        {
            if (!enabled)
                continue;

            if (previous.HasValue && leadHours >= previous.Value)
                return false;

            previous = leadHours;
        }

        return true;
    }
}