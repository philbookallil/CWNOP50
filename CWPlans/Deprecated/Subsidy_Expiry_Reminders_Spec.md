# Subsidy expiry reminders — spec

**Status:** applied and closed 2026-11-10 (written 2026-11-09). Folded into `subsidy-system.md` §15; kept here as history. Tier 2 (new entity, new task, a copy of a nop view, many files).
**Code touch:** Eduardo applies every change below; Claude has written the unit tests (`Tests/CW.Tests/Core`, §5.1).
**Scope:** `Plugins/Nop.Plugin.CW.Core` only. No nop file changes (§2 Cardinal Rule): nop's Reminders page is copied
into the plugin and the copy adds one widget zone.

---

## 1. What it does

A fourth card on **Promotions › Reminders**, below nop's three: **Subsidy expiry reminders**, with the same layout as
nop's (a switch, overridable per store, and three follow-ups, each with an amount and Hours / Days). Each follow-up is
sent **that long before the subsidy's ordering period ends**. #1 is the earliest (default 30 days), #3 the latest
(default 1 day).

| Decision (Eduardo, 2026-11-09) | How the spec applies it |
|---|---|
| 1. Who is reminded | **Only a packname with its own customer account** (active, not deleted, with an e-mail). Managers are not reminded for now: a manager can have many packnames, and the same packname can have many managers. A manager digest can be added later as a separate card if the business asks for it (§8). |
| 2. Only when something is left to spend | A season is reminded only while its **credit (subsidy + product groups, top-ups included) or items** are not used up, as `vw_CORE_SUB_SubsidyPackNameCredit` / `vw_CORE_SUB_ProductGroupPackNameCredit` count them. The admin sees this in the switch's **tooltip** and in an **info callout** at the top of the card. |
| 3. One e-mail per customer | All the customer's seasons with a follow-up due in the same run go in **one e-mail**, each listed with its subsidy, packname, last day, days left and what is left. The wording is the template of the **latest** follow-up among them. |
| 4. `OrderOutsidePeriod` subsidies are reminded too | Nothing filters them out. Checked against the code and the docs, §1.1. |

### 1.1 Decision 4, checked against the code and the documents

**The code says what you said.** Outside the period, no subsidy money can be used, whatever `OrderOutsidePeriod` says:

- `vw_CORE_SUB_SubsidyPackNameCredit` and `vw_CORE_SUB_ProductGroupPackNameCredit` (`CWDatabaseService.cs`, the
  `WHERE` at the end of each) return a row **only while the store's today is inside the season**. Their consumption
  sub-queries also count only orders placed inside the season. Settlement reads credit from these views
  (`SubsidyCreditService.LoadPackNameCreditsAsync`), so outside the period there is nothing to settle.
- Neither credit view reads `OrderOutsidePeriod`. What does read it decides only ordering and visibility, never money:
  `vw_CORE_SUB_SubsidyPackNameCanOrder` (the `OR` arm of `canorder`: whether the packname **may place an order**),
  `SubsidyOrderingService` (which subsidies count as active for ordering), the subsidy catalogue (zero-credit entries,
  `AvailableCredit = 0`) and the subsidy save (it clears `RestrictToAllocatedQty`).

**The documents agree:** `subsidy-system.md` §2.2 ("Allow ordering when outside the period (but without subsidy
credit)"), §3.1 ("no subsidy credit is applied. The PackName pays full price"), §5.3 ("Orders placed outside the period
are at full price … It does not carry over any credit") and the "Important distinction" note in §4. I found no document
that says otherwise.

One thing to know, not a conflict: outside the period such a packname still sees **only its allocated products** when
the subsidy has `OnlyAllocatedProducts` (§5.3). So it pays itself, but only for those products.

## 2. How it works

```
Promotions › Reminders (CW.Core's copy of nop's page)
  └─ nop's three cards ── nop's form ── Save ──► ReminderController.Index (nop)
  └─ zone cw_admin_reminders_bottom              ▲
       └─ SubsidyExpiryRemindersViewComponent    │ SubsidyRemindersSaveFilter (global, order 1)
            (hidden while subsidies are off)     │   before: bind SubsidyExpiry.*, validate → ModelState
                                                 │   after (form valid): save switch + follow-ups

System › Schedule tasks: "Process subsidy expiry reminders" (hourly)
  └─ ProcessSubsidyExpiryRemindersTask → ISubsidyReminderService.SendExpiryRemindersAsync
       per store: Subsidy feature on? switch on? active follow-ups?
         candidates: live assignments whose packname has its own active account with an e-mail
         season open today and ending within the longest lead time (C#: SubsidyPeriodHelper + SeasonFor)
         follow-up due and not sent yet for this season (CORE_SUB_SubsidyReminder)
         credit view has the row (SQL decides "in period") and something is left to spend
         → one e-mail per customer → record the follow-up per assignment and season end
```

**Where things are stored**

| What | Where | Why |
|---|---|---|
| The switch | `SubsidyReminderSettings.ExpiryRemindersEnabled` (setting `subsidyremindersettings.expiryremindersenabled`), overridable per store | nop's `ReminderSettings` is a nop class: a plugin cannot add to it |
| Each follow-up's on/off and lead time | Its message template: `IsActive`, `DelayBeforeSend`, `DelayPeriod` | Exactly how nop stores its own follow-ups. The card saves them through nop's own `IReminderService.UpdateFollowUpAsync` |
| What was sent | `CORE_SUB_SubsidyReminder` (assignment, season end, last follow-up number, when) | Keyed on the season end, so the next season of an annual subsidy, or a variation that moves the end, starts again from #1 |

**Timing.** A season ends at 23:59:59 of its last day in the store's time zone (the same instant the top-up windows use,
`SubsidyPeriodHelper.EndOfLocalDay`). Follow-up *n* is due from *(end − lead time n)* until the end. The task runs
hourly, so a reminder goes out within an hour of becoming due. Of the follow-ups due, the latest is the one sent, and
only if it comes after the last one sent: a subsidy assigned two days before its end gets #3, not #1, #2 and #3 at once.

**E-mail.** Sent with `ignoreDelayBeforeSend: true`, as nop sends its own reminders. Without it, nop would also hold
the queued e-mail back by the same delay, because it reads `DelayBeforeSend` as "wait this long after queueing".
Tokens: `%Subsidy.ExpiryDetails%` (the list) and `%Subsidy.DaysLeft%` (the fewest days left among the seasons, for
the subject), plus nop's store and customer tokens.

**Feature switch (`Feature_Toggles_Reference.md` §1).** The task skips a store with Subsidy off (`ICWFeatureService`,
which already implies PackName). The card is not drawn when Subsidy is off for the store scope (for every store at the
all-stores scope). The filter acts only on a post that carries the card's own field, so a page without the card never
turns the switch off. Existing settings and templates are kept while the feature is off.

### 2.1 Two nop behaviours the card inherits

- **Lead times are not per store.** nop's `UpdateFollowUpAsync` updates the first template of the name. Our templates
  are not store-limited, so editing the lead times at store scope changes them for every store, exactly as nop's
  Pending orders card does today. Only the switch is really per store.
- **The template edit page shows "Delay before send"** for these templates. For them it means *before the end*, not
  *after queueing*. The template description says so.

### 2.2 Known, accepted

- **Time zone.** C# picks the candidates with the zone the SQL views use: the store's `DateTimeSettings` zone, then the
  instance's, then **UTC** when none is set (the views' fallback; nop's own C# helper would use the server's zone). If a
  store sets a zone the web server does not know, C# falls back to the instance zone. The SQL credit view keeps the last
  word on "in period".
- **A failure for one customer, or one store,** is logged (System › Log) and retried on the next run; the others still
  go. If the e-mail is queued but recording it fails, the next run sends it again.
- **The days in the subject count from today:** the season ends at 23:59:59, so #2 (7 days) usually goes out on the
  6th day before the end and reads "6 day(s) left".
- **Changing a lead time after a follow-up has been sent** does not resend it; the next follow-up follows the new times.

## 3. Files

22 new files and 33 changes to 14 existing files, all in `Plugins/Nop.Plugin.CW.Core`. Changes 1–22 are the new files;
23–55 the edits. The new view `Areas/Admin/Views/Reminder/Index.cshtml` is **a copy of a nop file**: add it to the
`CLAUDE.md` §10.3 upgrade list at closure (§7).

## 4. Changes

Labels: **ADD FILE** — a new file, whole; **REPLACE** — find the lines given (at the line numbers stated, counted in the file as it stands when that change is reached) and replace them with the block below. Within a file the changes are listed bottom to top, so no change moves the lines of the next one. No change depends on another to be applied in a different order: apply them as numbered, then build once.

### 4.1 New files — all under `Plugins/Nop.Plugin.CW.Core/`

#### 1. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Settings/SubsidyReminderSettings.cs`

The switch of the card, per store. (19 lines)

```csharp
using Nop.Core.Configuration;

namespace Nop.Plugin.CW.Core.Domain.Settings;

/// <summary>
/// The subsidy card of Promotions › Reminders, per store
/// </summary>
/// <remarks>
/// When each follow-up is sent is stored on its message template (<c>DelayBeforeSend</c>, <c>DelayPeriod</c>), as nop
/// stores it for its own reminders; this class holds only the switch.
/// </remarks>
public class SubsidyReminderSettings : ISettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the customer of a packname is reminded before a subsidy's ordering period
    /// ends
    /// </summary>
    public bool ExpiryRemindersEnabled { get; set; }
}
```

#### 2. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Subsidies/SubsidyReminder.cs`

The last follow-up sent per assignment and season end. (33 lines)

```csharp
using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

/// <summary>
/// The last expiry reminder sent for one season of a subsidy assignment
/// </summary>
/// <remarks>
/// One row per assignment and season end. The next season of an annual subsidy, or a variation that moves the end date,
/// has another end, so its reminders start again from the first follow-up.
/// </remarks>
public class SubsidyReminder : BaseEntity
{
    /// <summary>
    /// Gets or sets the subsidy assignment identifier
    /// </summary>
    public int SubsidyPackNameId { get; set; }

    /// <summary>
    /// Gets or sets the last day of the season, on the store's calendar
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the number of the last follow-up sent for the season (1 to 3)
    /// </summary>
    public int LastFollowUpNumber { get; set; }

    /// <summary>
    /// Gets or sets when the last follow-up was sent
    /// </summary>
    public DateTime LastFollowUpOnUtc { get; set; }
}
```

#### 3. ADD FILE `Plugins/Nop.Plugin.CW.Core/Data/Mapping/Subsidies/SubsidyReminderBuilder.cs`

Its table, `CORE_SUB_SubsidyReminder`. (19 lines)

```csharp
using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using System.Data;

namespace Nop.Plugin.CW.Core.Data.Mapping.Subsidies;

public class SubsidyReminderBuilder : NopEntityBuilder<SubsidyReminder>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SubsidyReminder.Id)).AsInt32().PrimaryKey().Identity()
             .WithColumn(nameof(SubsidyReminder.SubsidyPackNameId)).AsInt32().ForeignKey<SubsidyPackName>(onDelete: Rule.None).NotNullable()
             .WithColumn(nameof(SubsidyReminder.PeriodEnd)).AsDate().NotNullable()
             .WithColumn(nameof(SubsidyReminder.LastFollowUpNumber)).AsInt32().NotNullable()
             .WithColumn(nameof(SubsidyReminder.LastFollowUpOnUtc)).AsDateTime2().NotNullable();
    }
}
```

#### 4. ADD FILE `Plugins/Nop.Plugin.CW.Core/Data/Migrations/Subsidies/SubsidyReminderMigration.cs`

Creates the table on your database, which already has CW.Core installed. (42 lines)

```csharp
using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Domain.Subsidies;

namespace Nop.Plugin.CW.Core.Data.Migrations;

/// <summary>
/// Adds the subsidy expiry reminder table to a database installed before the table was part of the schema
/// </summary>
/// <remarks>
/// An installation creates the table from <see cref="SchemaMigration"/> and nop marks this migration as applied without
/// running it; a plugin update runs it, and it creates the table only when it is missing.
/// </remarks>
[NopMigration(MigrationVersions.SubsidyReminder, "CW.Core CORE_SUB_SubsidyReminder", MigrationProcessType.Update)]
public class SubsidyReminderMigration : Migration
{
    public override void Up()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(SubsidyReminder));

        if (Schema.Table(table).Exists())
            return;

        this.CreateTableIfNotExists<SubsidyReminder>();

        Create.Index(SchemaMigration.SubsidyReminderUniqueIndex)
            .OnTable(table)
            .OnColumn(nameof(SubsidyReminder.SubsidyPackNameId)).Ascending()
            .OnColumn(nameof(SubsidyReminder.PeriodEnd)).Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(SubsidyReminder));

        if (Schema.Table(table).Exists())
            Delete.Table(table);
    }
}
```

#### 5. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Subsidies/DTOs/SubsidyExpiryReminderItem.cs`

One season ending, as the service reads it and the e-mail lists it. (92 lines)

```csharp
namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// One subsidy assignment whose season is ending, as the expiry reminder reads it and as the e-mail lists it
/// </summary>
public class SubsidyExpiryReminderItem
{
    /// <summary>
    /// Gets or sets the subsidy assignment identifier
    /// </summary>
    public int SubsidyPackNameId { get; set; }

    /// <summary>
    /// Gets or sets the packname identifier
    /// </summary>
    public int PackNameId { get; set; }

    /// <summary>
    /// Gets or sets the packname name
    /// </summary>
    public string PackName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the packname's own customer account, the one reminded
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the subsidy identifier
    /// </summary>
    public int SubsidyId { get; set; }

    /// <summary>
    /// Gets or sets the subsidy name
    /// </summary>
    public string Subsidy { get; set; }

    /// <summary>
    /// Gets or sets the subsidy's own start date
    /// </summary>
    public DateTime DurationStart { get; set; }

    /// <summary>
    /// Gets or sets the subsidy's own end date
    /// </summary>
    public DateTime DurationEnd { get; set; }

    /// <summary>
    /// Gets or sets the start date of the assignment's variation, if it has one
    /// </summary>
    public DateTime? VariationDurationStart { get; set; }

    /// <summary>
    /// Gets or sets the end date of the assignment's variation, if it has one
    /// </summary>
    public DateTime? VariationDurationEnd { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the period repeats every year
    /// </summary>
    public bool AnnualRecurrence { get; set; }

    /// <summary>
    /// Gets or sets the last day of the season, on the store's calendar
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the moment the season ends: 23:59:59 of its last day in the store's time zone, as UTC
    /// </summary>
    public DateTime PeriodEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the days from the store's today to the last day (0 on the last day)
    /// </summary>
    public int DaysLeft { get; set; }

    /// <summary>
    /// Gets or sets the number of the follow-up due for this assignment (1 to 3)
    /// </summary>
    public int FollowUpNumber { get; set; }

    /// <summary>
    /// Gets or sets the credit left to spend in the season: the subsidy's and its product groups', top-ups included
    /// </summary>
    public decimal RemainingCredit { get; set; }

    /// <summary>
    /// Gets or sets the items left to order in the season, top-ups included
    /// </summary>
    public int RemainingUnits { get; set; }
}
```

#### 6. ADD FILE `Plugins/Nop.Plugin.CW.Core/Domain/Subsidies/DTOs/SubsidyExpiryFollowUpSetting.cs`

One follow-up as the admin saves it (the filter's input to the service). (11 lines)

```csharp
using Nop.Core.Domain.Messages;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// One follow-up of the subsidy expiry reminders as the admin saves it
/// </summary>
/// <param name="Enabled">Whether the follow-up is sent</param>
/// <param name="DelayBeforeSend">How long before the season ends it is sent, in <paramref name="DelayPeriod"/> units</param>
/// <param name="DelayPeriod">Hours or days</param>
public record SubsidyExpiryFollowUpSetting(bool Enabled, int DelayBeforeSend, MessageDelayPeriod DelayPeriod);
```

#### 7. ADD FILE `Plugins/Nop.Plugin.CW.Core/Helpers/SubsidyExpiryReminderHelper.cs`

The pure rules: open season and its end, due follow-up, left to spend, lead-time order. (154 lines)

```csharp
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
    /// the reminder for where it stands, not every earlier one at once.
    /// </remarks>
    /// <param name="periodEndUtc">When the season ends</param>
    /// <param name="nowUtc">Now</param>
    /// <param name="leadHoursByFollowUp">The lead time in hours of each active follow-up, by number</param>
    /// <param name="lastFollowUpNumber">The last follow-up sent for the season; 0 when none</param>
    /// <returns>The follow-up number, or null</returns>
    public static int? GetDueFollowUp(DateTime periodEndUtc, DateTime nowUtc, IReadOnlyDictionary<int, int> leadHoursByFollowUp, int lastFollowUpNumber)
    {
        ArgumentNullException.ThrowIfNull(leadHoursByFollowUp);

        if (nowUtc >= periodEndUtc)
            return null;

        int? due = null;

        foreach (var (number, leadHours) in leadHoursByFollowUp)
        {
            if (leadHours <= 0 || nowUtc < periodEndUtc.AddHours(-leadHours))
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
    /// The days from today to the season's last day: 0 on the last day itself
    /// </summary>
    /// <param name="periodEnd">The season's last day</param>
    /// <param name="today">The store's date</param>
    public static int DaysLeft(DateTime periodEnd, DateTime today)
    {
        return (periodEnd.Date - today.Date).Days;
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
```

#### 8. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Subsidies/SubsidyReminderDefaults.cs`

Template names, task type and name, interval, batch size. (39 lines)

```csharp
using Nop.Plugin.CW.Core.Domain.Messages;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The fixed values of the subsidy expiry reminders
/// </summary>
public static class SubsidyReminderDefaults
{
    /// <summary>
    /// The follow-up templates, in follow-up order: #1 is sent the longest before the end
    /// </summary>
    public static string[] ExpiryFollowUpTemplates =>
    [
        CWMessageTemplateSystemNames.SubsidyExpiryFollowUp1,
        CWMessageTemplateSystemNames.SubsidyExpiryFollowUp2,
        CWMessageTemplateSystemNames.SubsidyExpiryFollowUp3
    ];

    /// <summary>
    /// The type name nop's scheduler finds the task by
    /// </summary>
    public static string ExpiryTaskTypeFullName => typeof(ProcessSubsidyExpiryRemindersTask).FullName;

    /// <summary>
    /// The task's name on System › Schedule tasks
    /// </summary>
    public const string ExpiryTaskName = "Process subsidy expiry reminders";

    /// <summary>
    /// How often the task runs: hourly, the finest lead time a follow-up can have
    /// </summary>
    public const int ExpiryTaskSeconds = 60 * 60;

    /// <summary>
    /// How many customers' reminders are read and sent together
    /// </summary>
    public const int ProcessingBatchSize = 100;
}
```

#### 9. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Subsidies/ISubsidyReminderService.cs`

Send the reminders; save the card. (27 lines)

```csharp
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The subsidy expiry reminders: sending them, and saving when they are sent
/// </summary>
public interface ISubsidyReminderService
{
    /// <summary>
    /// Sends the follow-ups due now, in every store with subsidies and the reminders switched on: one e-mail per
    /// customer, listing each of their packname's subsidies whose season is ending with something left to spend
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SendExpiryRemindersAsync();

    /// <summary>
    /// Saves the subsidy card of Promotions › Reminders: the switch for the store scope, and when the switch is on, each
    /// follow-up on its message template
    /// </summary>
    /// <param name="storeId">The store scope the admin is editing; 0 for all stores</param>
    /// <param name="enabled">Whether the reminders are sent</param>
    /// <param name="overrideForStore">Whether the store keeps its own value of the switch (store scope only)</param>
    /// <param name="followUps">The follow-ups in follow-up order; any beyond the third are ignored</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SaveExpiryReminderSettingsAsync(int storeId, bool enabled, bool overrideForStore, IList<SubsidyExpiryFollowUpSetting> followUps);
}
```

#### 10. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Subsidies/SubsidyReminderService.cs`

The reminders themselves. (455 lines)

```csharp
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Reminders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The subsidy expiry reminders: which packname customers are reminded, when, and with what
/// </summary>
/// <remarks>
/// <para>
/// <b>Who.</b> The packname's own customer account, active and with an e-mail address. A packname without an account is
/// not reminded, and neither are the managers who order for it.
/// </para>
/// <para>
/// <b>When.</b> Each follow-up is sent its lead time before the season ends (23:59:59 of the last day, store time), the
/// lead time being the one nop's Reminders page stores on the follow-up's message template. A season is reminded only
/// while something is left to spend in it, credit or items, as the credit views count it. Subsidies with
/// <c>OrderOutsidePeriod</c> are reminded too: outside the season they may still order, but no subsidy pays for it.
/// </para>
/// <para>
/// <b>How.</b> One e-mail per customer and run, listing every season of theirs with a follow-up due, written with the
/// template of the latest follow-up among them. What was sent is kept per assignment and season end
/// (<see cref="SubsidyReminder"/>), so each follow-up goes once.
/// </para>
/// </remarks>
public class SubsidyReminderService : ISubsidyReminderService
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILogger _logger;
    protected readonly IMessageTemplateService _messageTemplateService;
    protected readonly IPackNameWorkflowMessageService _packNameWorkflowMessageService;
    protected readonly IReminderService _reminderService;
    protected readonly IRepository<Customer> _customerRepository;
    protected readonly IRepository<PackName> _packNameRepository;
    protected readonly IRepository<PackNameList> _packNameListRepository;
    protected readonly IRepository<ProductGroupPackNameCredit> _productGroupPackNameCreditRepository;
    protected readonly IRepository<Subsidy> _subsidyRepository;
    protected readonly IRepository<SubsidyPackName> _subsidyPackNameRepository;
    protected readonly IRepository<SubsidyPackNameCredit> _subsidyPackNameCreditRepository;
    protected readonly IRepository<SubsidyReminder> _subsidyReminderRepository;
    protected readonly IRepository<SubsidyVariation> _subsidyVariationRepository;
    protected readonly ISettingService _settingService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public SubsidyReminderService(ICustomerService customerService,
        ICWFeatureService cwFeatureService,
        IDateTimeHelper dateTimeHelper,
        ILogger logger,
        IMessageTemplateService messageTemplateService,
        IPackNameWorkflowMessageService packNameWorkflowMessageService,
        IReminderService reminderService,
        IRepository<Customer> customerRepository,
        IRepository<PackName> packNameRepository,
        IRepository<PackNameList> packNameListRepository,
        IRepository<ProductGroupPackNameCredit> productGroupPackNameCreditRepository,
        IRepository<Subsidy> subsidyRepository,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        IRepository<SubsidyPackNameCredit> subsidyPackNameCreditRepository,
        IRepository<SubsidyReminder> subsidyReminderRepository,
        IRepository<SubsidyVariation> subsidyVariationRepository,
        ISettingService settingService,
        IStoreService storeService)
    {
        _customerService = customerService;
        _cwFeatureService = cwFeatureService;
        _dateTimeHelper = dateTimeHelper;
        _logger = logger;
        _messageTemplateService = messageTemplateService;
        _packNameWorkflowMessageService = packNameWorkflowMessageService;
        _reminderService = reminderService;
        _customerRepository = customerRepository;
        _packNameRepository = packNameRepository;
        _packNameListRepository = packNameListRepository;
        _productGroupPackNameCreditRepository = productGroupPackNameCreditRepository;
        _subsidyRepository = subsidyRepository;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _subsidyPackNameCreditRepository = subsidyPackNameCreditRepository;
        _subsidyReminderRepository = subsidyReminderRepository;
        _subsidyVariationRepository = subsidyVariationRepository;
        _settingService = settingService;
        _storeService = storeService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The lead time in hours of each follow-up the store sends, by number; a follow-up whose template is inactive or
    /// has no lead time is left out
    /// </summary>
    /// <remarks>
    /// The first template of each name, the one nop's Reminders page reads and writes.
    /// </remarks>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the lead times</returns>
    protected virtual async Task<Dictionary<int, int>> GetFollowUpLeadHoursAsync(int storeId)
    {
        var leadHours = new Dictionary<int, int>();
        var templateNames = SubsidyReminderDefaults.ExpiryFollowUpTemplates;

        for (var index = 0; index < templateNames.Length; index++)
        {
            var template = (await _messageTemplateService.GetMessageTemplatesByNameAsync(templateNames[index], storeId)).FirstOrDefault();

            if (template is not { IsActive: true, DelayBeforeSend: not null })
                continue;

            var hours = SubsidyExpiryReminderHelper.LeadHours(template.DelayBeforeSend.Value, template.DelayPeriodId);

            if (hours > 0)
                leadHours[index + 1] = hours;
        }

        return leadHours;
    }

    /// <summary>
    /// The store's time zone, the one its subsidy periods are expressed in: the store's own setting, then the
    /// instance's, then UTC
    /// </summary>
    /// <remarks>
    /// UTC when no zone is set anywhere, as the SQL views fall back (<c>vw_CORE_SUB_StoreSubsidyVariationSetting</c>), so
    /// the season end worked out here is the one the credit views use. A zone this server does not know falls back to the
    /// instance zone, as nop's own helper does.
    /// </remarks>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the time zone</returns>
    protected virtual async Task<TimeZoneInfo> GetStoreTimeZoneAsync(int storeId)
    {
        var dateTimeSettings = await _settingService.LoadSettingAsync<DateTimeSettings>(storeId);

        if (string.IsNullOrWhiteSpace(dateTimeSettings.DefaultStoreTimeZoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(dateTimeSettings.DefaultStoreTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return _dateTimeHelper.DefaultStoreTimeZone;
        }
        catch (InvalidTimeZoneException)
        {
            return _dateTimeHelper.DefaultStoreTimeZone;
        }
    }

    /// <summary>
    /// Every live assignment of the store whose packname has its own active customer account with an e-mail address,
    /// with the dates its season is worked out from
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the assignments</returns>
    protected virtual async Task<IList<SubsidyExpiryReminderItem>> GetCandidateAssignmentsAsync(int storeId)
    {
        var query = from sp in _subsidyPackNameRepository.Table
                    join s in _subsidyRepository.Table on sp.SubsidyId equals s.Id
                    join pn in _packNameRepository.Table on sp.PackNameId equals pn.Id
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    join c in _customerRepository.Table on pn.CustomerId equals (int?)c.Id
                    join v in _subsidyVariationRepository.Table.Where(variation => !variation.Deleted) on sp.Id equals v.SubsidyPackNameId into variations
                    from v in variations.DefaultIfEmpty()
                    where !sp.Deleted && sp.Active
                        && !s.Deleted && s.Active && s.StoreId == storeId
                        && !pn.Deleted && pn.Active && !pnl.Deleted
                        && !c.Deleted && c.Active && c.Email != null && c.Email != string.Empty
                    select new SubsidyExpiryReminderItem
                    {
                        SubsidyPackNameId = sp.Id,
                        PackNameId = pn.Id,
                        PackName = pn.Name,
                        CustomerId = c.Id,
                        SubsidyId = s.Id,
                        Subsidy = s.Name,
                        DurationStart = s.DurationStart,
                        DurationEnd = s.DurationEnd,
                        VariationDurationStart = v != null ? v.DurationStart : null,
                        VariationDurationEnd = v != null ? v.DurationEnd : null,
                        AnnualRecurrence = s.AnnualRecurrence
                    };

        return await query.ToListAsync();
    }

    /// <summary>
    /// Sends the follow-ups due to one batch of customers of a store and records them
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="items">The customers' seasons inside the reminder window</param>
    /// <param name="leadHours">The store's follow-up lead times, by number</param>
    /// <param name="nowUtc">Now</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task SendBatchAsync(int storeId, IList<SubsidyExpiryReminderItem> items, IReadOnlyDictionary<int, int> leadHours, DateTime nowUtc)
    {
        var assignmentIds = items.Select(item => item.SubsidyPackNameId).Distinct().ToList();

        var records = await _subsidyReminderRepository.Table
            .Where(record => assignmentIds.Contains(record.SubsidyPackNameId))
            .ToListAsync();

        var due = new List<SubsidyExpiryReminderItem>();

        foreach (var item in items)
        {
            var lastFollowUpNumber = records
                .FirstOrDefault(record => record.SubsidyPackNameId == item.SubsidyPackNameId && record.PeriodEnd.Date == item.PeriodEnd.Date)
                ?.LastFollowUpNumber ?? 0;

            var followUpNumber = SubsidyExpiryReminderHelper.GetDueFollowUp(item.PeriodEndUtc, nowUtc, leadHours, lastFollowUpNumber);

            if (followUpNumber is null)
                continue;

            item.FollowUpNumber = followUpNumber.Value;
            due.Add(item);
        }

        if (due.Count == 0)
            return;

        var dueIds = due.Select(item => item.SubsidyPackNameId).Distinct().ToList();

        //what is left to spend, as the credit views count it; a view has no row for a season that is not open
        var subsidyCredits = await _subsidyPackNameCreditRepository.Table
            .Where(credit => dueIds.Contains(credit.Id))
            .ToListAsync();
        var productGroupCredits = await _productGroupPackNameCreditRepository.Table
            .Where(credit => dueIds.Contains(credit.Id))
            .ToListAsync();

        var toSend = new List<SubsidyExpiryReminderItem>();

        foreach (var item in due)
        {
            var subsidyCredit = subsidyCredits.FirstOrDefault(credit => credit.Id == item.SubsidyPackNameId);

            if (subsidyCredit is null)
                continue;

            var (remainingCredit, remainingUnits) = SubsidyExpiryReminderHelper.RemainingToSpend(subsidyCredit,
                productGroupCredits.Where(credit => credit.Id == item.SubsidyPackNameId));

            if (remainingCredit <= decimal.Zero && remainingUnits <= 0)
                continue;

            item.RemainingCredit = remainingCredit;
            item.RemainingUnits = remainingUnits;
            toSend.Add(item);
        }

        foreach (var customerItems in toSend.GroupBy(item => item.CustomerId))
        {
            var customer = await _customerService.GetCustomerByIdAsync(customerItems.Key);

            if (customer is null)
                continue;

            var reminderItems = customerItems
                .OrderBy(item => item.PeriodEnd)
                .ThenBy(item => item.PackName)
                .ThenBy(item => item.Subsidy)
                .ToList();

            //the latest follow-up among the customer's seasons decides the wording; each season is listed with its own date
            var templateName = SubsidyReminderDefaults.ExpiryFollowUpTemplates[reminderItems.Max(item => item.FollowUpNumber) - 1];

            IList<int> queuedEmailIds;

            try
            {
                queuedEmailIds = await _packNameWorkflowMessageService.SendSubsidyExpiryReminderAsync(customer, templateName, reminderItems, storeId);
            }
            catch (Exception exception)
            {
                //one customer's failure must not hold back the others; the next run tries again
                await _logger.ErrorAsync($"The subsidy expiry reminder to customer {customer.Id} could not be queued", exception, customer);
                continue;
            }

            if (queuedEmailIds.Count == 0)
                continue;

            foreach (var item in reminderItems)
            {
                var record = records.FirstOrDefault(sent => sent.SubsidyPackNameId == item.SubsidyPackNameId && sent.PeriodEnd.Date == item.PeriodEnd.Date);

                if (record is null)
                {
                    record = new SubsidyReminder
                    {
                        SubsidyPackNameId = item.SubsidyPackNameId,
                        PeriodEnd = item.PeriodEnd.Date,
                        LastFollowUpNumber = item.FollowUpNumber,
                        LastFollowUpOnUtc = nowUtc
                    };

                    await _subsidyReminderRepository.InsertAsync(record, false);
                    records.Add(record);
                }
                else
                {
                    record.LastFollowUpNumber = item.FollowUpNumber;
                    record.LastFollowUpOnUtc = nowUtc;

                    await _subsidyReminderRepository.UpdateAsync(record, false);
                }
            }
        }
    }

    /// <summary>
    /// Sends the follow-ups due now in one store
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="nowUtc">Now</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task SendStoreExpiryRemindersAsync(int storeId, DateTime nowUtc)
    {
        //a store with subsidies off sends no subsidy e-mail
        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, storeId))
            return;

        var reminderSettings = await _settingService.LoadSettingAsync<SubsidyReminderSettings>(storeId);

        if (!reminderSettings.ExpiryRemindersEnabled)
            return;

        var leadHours = await GetFollowUpLeadHoursAsync(storeId);

        if (leadHours.Count == 0)
            return;

        var maximumLeadHours = leadHours.Values.Max();
        var storeTimeZone = await GetStoreTimeZoneAsync(storeId);
        var today = SubsidyPeriodHelper.ToStoreLocal(nowUtc, storeTimeZone).Date;
        var packNamesOwnStartAndFinishDates = (await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId)).PackNamesOwnStartAndFinishDates;

        var inWindow = new List<SubsidyExpiryReminderItem>();

        foreach (var item in await GetCandidateAssignmentsAsync(storeId))
        {
            var durationStart = SubsidyPeriodHelper.EffectiveDurationStart(item.DurationStart, item.VariationDurationStart, packNamesOwnStartAndFinishDates);
            var durationEnd = SubsidyPeriodHelper.EffectiveDurationEnd(item.DurationEnd, item.VariationDurationEnd, packNamesOwnStartAndFinishDates);

            if (!SubsidyExpiryReminderHelper.TryGetOpenPeriodEnd(durationStart, durationEnd, item.AnnualRecurrence, today, out var periodEnd))
                continue;

            var periodEndUtc = SubsidyPeriodHelper.ToUtc(SubsidyPeriodHelper.EndOfLocalDay(periodEnd), storeTimeZone);

            //not ended yet, and inside the longest lead time
            if (periodEndUtc <= nowUtc || periodEndUtc.AddHours(-maximumLeadHours) > nowUtc)
                continue;

            item.PeriodEnd = periodEnd.Date;
            item.PeriodEndUtc = periodEndUtc;
            item.DaysLeft = SubsidyExpiryReminderHelper.DaysLeft(periodEnd, today);
            inWindow.Add(item);
        }

        //batched by customer, so a customer's seasons always travel in the same e-mail
        foreach (var customers in inWindow.GroupBy(item => item.CustomerId).Chunk(SubsidyReminderDefaults.ProcessingBatchSize))
            await SendBatchAsync(storeId, customers.SelectMany(customer => customer).ToList(), leadHours, nowUtc);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Sends the follow-ups due now, in every store with subsidies and the reminders switched on: one e-mail per
    /// customer, listing each of their packname's subsidies whose season is ending with something left to spend
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task SendExpiryRemindersAsync()
    {
        var nowUtc = DateTime.UtcNow;

        foreach (var store in await _storeService.GetAllStoresAsync())
        {
            try
            {
                await SendStoreExpiryRemindersAsync(store.Id, nowUtc);
            }
            catch (Exception exception)
            {
                //one store's failure must not hold back the others; the next run tries again
                await _logger.ErrorAsync($"The subsidy expiry reminders of store {store.Id} could not be sent", exception);
            }
        }
    }

    /// <summary>
    /// Saves the subsidy card of Promotions › Reminders: the switch for the store scope, and when the switch is on, each
    /// follow-up on its message template
    /// </summary>
    /// <remarks>
    /// As nop saves its own cards: the follow-ups are written only while the switch is on, through nop's
    /// <see cref="IReminderService.UpdateFollowUpAsync"/>, which keeps the lead time of a follow-up switched off.
    /// </remarks>
    /// <param name="storeId">The store scope the admin is editing; 0 for all stores</param>
    /// <param name="enabled">Whether the reminders are sent</param>
    /// <param name="overrideForStore">Whether the store keeps its own value of the switch (store scope only)</param>
    /// <param name="followUps">The follow-ups in follow-up order; any beyond the third are ignored</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task SaveExpiryReminderSettingsAsync(int storeId, bool enabled, bool overrideForStore, IList<SubsidyExpiryFollowUpSetting> followUps)
    {
        ArgumentNullException.ThrowIfNull(followUps);

        var reminderSettings = await _settingService.LoadSettingAsync<SubsidyReminderSettings>(storeId);
        reminderSettings.ExpiryRemindersEnabled = enabled;

        await _settingService.SaveSettingOverridablePerStoreAsync(reminderSettings, settings => settings.ExpiryRemindersEnabled, overrideForStore, storeId, false);

        if (enabled)
        {
            var templateNames = SubsidyReminderDefaults.ExpiryFollowUpTemplates;

            for (var index = 0; index < Math.Min(templateNames.Length, followUps.Count); index++)
            {
                var followUp = followUps[index];

                await _reminderService.UpdateFollowUpAsync(templateNames[index], storeId, followUp.Enabled, followUp.DelayBeforeSend, followUp.DelayPeriod);
            }
        }

        await _settingService.ClearCacheAsync();
    }

    #endregion
}
```

#### 11. ADD FILE `Plugins/Nop.Plugin.CW.Core/Services/Subsidies/ProcessSubsidyExpiryRemindersTask.cs`

The scheduled task (hourly). (41 lines)

```csharp
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The scheduled task that sends the subsidy expiry reminders
/// </summary>
/// <remarks>
/// Installed by the plugin's install and update, removed by its uninstall. nop finds it by
/// <see cref="SubsidyReminderDefaults.ExpiryTaskTypeFullName"/> and builds it with its dependencies.
/// </remarks>
public class ProcessSubsidyExpiryRemindersTask : IScheduleTask
{
    #region Fields

    protected readonly ISubsidyReminderService _subsidyReminderService;

    #endregion

    #region Ctor

    public ProcessSubsidyExpiryRemindersTask(ISubsidyReminderService subsidyReminderService)
    {
        _subsidyReminderService = subsidyReminderService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Executes the task
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task ExecuteAsync()
    {
        await _subsidyReminderService.SendExpiryRemindersAsync();
    }

    #endregion
}
```

#### 12. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/Subsidies/SubsidyExpiryRemindersModel.cs`

The card's model. (41 lines)

```csharp
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

/// <summary>
/// The subsidy card of Promotions › Reminders
/// </summary>
public partial record SubsidyExpiryRemindersModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets the store scope the card was prepared for; posted back, it also tells the save filter the card was on
    /// the page
    /// </summary>
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Enabled")]
    public bool Enabled { get; set; }

    public bool Enabled_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets the three follow-ups, in follow-up order
    /// </summary>
    public List<SubsidyExpiryFollowUpModel> FollowUps { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the task that sends the reminders is installed
    /// </summary>
    public bool TaskFound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the task that sends the reminders is enabled
    /// </summary>
    public bool TaskEnabled { get; set; }

    /// <summary>
    /// Gets or sets the task's name on System › Schedule tasks
    /// </summary>
    public string TaskName { get; set; }
}
```

#### 13. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/Subsidies/SubsidyExpiryFollowUpModel.cs`

One follow-up of the card. (23 lines)

```csharp
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

/// <summary>
/// One follow-up of the subsidy card of Promotions › Reminders
/// </summary>
public partial record SubsidyExpiryFollowUpModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets the follow-up number (1 to 3); shown in the label, never posted
    /// </summary>
    public int Number { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend")]
    public int DelayBeforeSend { get; set; }

    public int DelayPeriodId { get; set; }
}
```

#### 14. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/Subsidies/CWRemindersModel.cs`

The container that names the card's fields `SubsidyExpiry.*`. (16 lines)

```csharp
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

/// <summary>
/// The CW cards of Promotions › Reminders
/// </summary>
/// <remarks>
/// The card's fields are named through this model's property (<c>SubsidyExpiry.Enabled</c>, …) so that nop's own
/// fields on the same form keep their names, and so that nop's nested-setting tag helper, which reads the property
/// path rather than the HTML field prefix, finds the switch it toggles on.
/// </remarks>
public partial record CWRemindersModel : BaseNopModel
{
    public SubsidyExpiryRemindersModel SubsidyExpiry { get; set; } = new();
}
```

#### 15. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Validators/Subsidies/SubsidyExpiryRemindersValidator.cs`

The card's rules. (45 lines)

```csharp
using FluentValidation;
using Nop.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;

/// <summary>
/// The rules of the subsidy card of Promotions › Reminders.
/// </summary>
/// <remarks>
/// Checked only while the card's switch is on, as the follow-ups are saved only then. nop's own action validates only
/// its parameters, so the card is validated by <see cref="Filters.SubsidyRemindersSaveFilter"/>.
/// </remarks>
public partial class SubsidyExpiryRemindersValidator : BaseNopValidator<SubsidyExpiryRemindersModel>
{
    public SubsidyExpiryRemindersValidator(ILocalizationService localizationService)
    {
        RuleForEach(x => x.FollowUps).ChildRules(followUp =>
        {
            followUp.RuleFor(x => x.DelayBeforeSend)
                .GreaterThan(0)
                .When(x => x.Enabled)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend.MustBeGreaterThanZero"));

            followUp.RuleFor(x => x.DelayBeforeSend)
                .Must((model, delayBeforeSend) => SubsidyExpiryReminderHelper.LeadHours(delayBeforeSend, model.DelayPeriodId) <= SubsidyExpiryReminderHelper.MaximumLeadHours)
                .When(x => x.Enabled)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend.Maximum"));

            followUp.RuleFor(x => x.DelayPeriodId)
                .Must(delayPeriodId => Enum.IsDefined(typeof(MessageDelayPeriod), delayPeriodId))
                .When(x => x.Enabled)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayPeriod.Invalid"));
        }).When(x => x.Enabled);

        RuleFor(x => x.FollowUps)
            .Must(followUps => SubsidyExpiryReminderHelper.LeadTimesDecrease(followUps
                .Select(followUp => (followUp.Enabled, SubsidyExpiryReminderHelper.LeadHours(followUp.DelayBeforeSend, followUp.DelayPeriodId)))))
            .When(x => x.Enabled)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUps.Order"));
    }
}
```

#### 16. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/ISubsidyReminderModelFactory.cs`

The card's factory. (18 lines)

```csharp
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// The subsidy card of Promotions › Reminders
/// </summary>
public interface ISubsidyReminderModelFactory
{
    /// <summary>
    /// Prepares the card for the store scope the admin is editing
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the card's model
    /// </returns>
    Task<SubsidyExpiryRemindersModel> PrepareSubsidyExpiryRemindersModelAsync();
}
```

#### 17. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/SubsidyReminderModelFactory.cs`

The card's factory. (89 lines)

```csharp
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// The subsidy card of Promotions › Reminders
/// </summary>
public class SubsidyReminderModelFactory : ISubsidyReminderModelFactory
{
    #region Fields

    protected readonly IMessageTemplateService _messageTemplateService;
    protected readonly IScheduleTaskService _scheduleTaskService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public SubsidyReminderModelFactory(IMessageTemplateService messageTemplateService,
        IScheduleTaskService scheduleTaskService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _messageTemplateService = messageTemplateService;
        _scheduleTaskService = scheduleTaskService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepares the card for the store scope the admin is editing
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the card's model
    /// </returns>
    public virtual async Task<SubsidyExpiryRemindersModel> PrepareSubsidyExpiryRemindersModelAsync()
    {
        var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var reminderSettings = await _settingService.LoadSettingAsync<SubsidyReminderSettings>(storeId);

        var model = new SubsidyExpiryRemindersModel
        {
            ActiveStoreScopeConfiguration = storeId,
            Enabled = reminderSettings.ExpiryRemindersEnabled
        };

        if (storeId > 0)
            model.Enabled_OverrideForStore = await _settingService.SettingExistsAsync(reminderSettings, settings => settings.ExpiryRemindersEnabled, storeId);

        var templateNames = SubsidyReminderDefaults.ExpiryFollowUpTemplates;

        for (var index = 0; index < templateNames.Length; index++)
        {
            //the first template of the name, the one nop's reminders read and write
            var template = (await _messageTemplateService.GetMessageTemplatesByNameAsync(templateNames[index], storeId)).FirstOrDefault();

            model.FollowUps.Add(new SubsidyExpiryFollowUpModel
            {
                Number = index + 1,
                Enabled = template?.IsActive ?? false,
                DelayBeforeSend = template?.DelayBeforeSend ?? 0,
                DelayPeriodId = template?.DelayPeriodId ?? 0
            });
        }

        var task = await _scheduleTaskService.GetTaskByTypeAsync(SubsidyReminderDefaults.ExpiryTaskTypeFullName);

        model.TaskFound = task != null;
        model.TaskEnabled = task?.Enabled ?? false;
        model.TaskName = task?.Name ?? SubsidyReminderDefaults.ExpiryTaskName;

        return model;
    }

    #endregion
}
```

#### 18. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Components/SubsidyExpiryRemindersViewComponent.cs`

Draws the card in the new zone; nothing while subsidies are off. (63 lines)

```csharp
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

/// <summary>
/// The subsidy card of Promotions › Reminders: the switch and the three follow-ups of the subsidy expiry reminders.
/// </summary>
/// <remarks>
/// Drawn in the zone CW.Core's copy of nop's Reminders page adds, inside nop's form, so nop's Save button posts it too;
/// <see cref="Filters.SubsidyRemindersSaveFilter"/> validates and saves it. Nothing is drawn while subsidies are off for
/// the store scope (for every store at the all-stores scope).
/// </remarks>
[ViewComponent(Name = "SubsidyExpiryReminders")]
public class SubsidyExpiryRemindersViewComponent : NopViewComponent
{
    #region Fields

    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IStoreContext _storeContext;
    protected readonly ISubsidyReminderModelFactory _subsidyReminderModelFactory;

    #endregion

    #region Ctor

    public SubsidyExpiryRemindersViewComponent(ICWFeatureService cwFeatureService,
        IStoreContext storeContext,
        ISubsidyReminderModelFactory subsidyReminderModelFactory)
    {
        _cwFeatureService = cwFeatureService;
        _storeContext = storeContext;
        _subsidyReminderModelFactory = subsidyReminderModelFactory;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();

        var subsidyEnabled = storeId > 0
            ? await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, storeId)
            : await _cwFeatureService.IsEnabledForAnyStoreAsync(CWFeature.Subsidy);

        if (!subsidyEnabled)
            return Content(string.Empty);

        return View(new CWRemindersModel
        {
            SubsidyExpiry = await _subsidyReminderModelFactory.PrepareSubsidyExpiryRemindersModelAsync()
        });
    }

    #endregion
}
```

#### 19. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Shared/Components/SubsidyExpiryReminders/Default.cshtml`

The card. (76 lines)

```cshtml
@model CWRemindersModel
@using Nop.Core.Domain.Messages
@using Nop.Services

@{
    NopHtml.AddScriptParts(ResourceLocation.Footer, "~/Plugins/CW.Core/Content/Admin/Reminders/admin.subsidy.reminders.js");
}

<div class="card card-default" id="cw-subsidy-expiry-reminders">
    <div class="card-body">
        <input asp-for="SubsidyExpiry.ActiveStoreScopeConfiguration" type="hidden" />
        <div class="callout callout-info">
            @T("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Info")
        </div>
        @if (!Model.SubsidyExpiry.TaskFound)
        {
            <div class="callout callout-warning">
                @T("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Warning.TaskNotFound")
            </div>
        }
        else if (!Model.SubsidyExpiry.TaskEnabled)
        {
            <div class="callout callout-warning">
                @T("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Warning.TaskDisabled", Model.SubsidyExpiry.TaskName, Url.Action("List", "ScheduleTask", new { area = AreaNames.ADMIN }))
            </div>
        }
        <div class="form-group row">
            <div class="col-md-3">
                <nop-override-store-checkbox asp-for="SubsidyExpiry.Enabled_OverrideForStore" asp-input="SubsidyExpiry.Enabled" asp-store-scope="@Model.SubsidyExpiry.ActiveStoreScopeConfiguration" />
                <nop-label asp-for="SubsidyExpiry.Enabled" />
            </div>
            <div class="col-md-9">
                <nop-editor asp-for="SubsidyExpiry.Enabled" />
                <span asp-validation-for="SubsidyExpiry.Enabled"></span>
            </div>
        </div>
        <nop-nested-setting asp-for="SubsidyExpiry.Enabled">
            @for (var i = 0; i < Model.SubsidyExpiry.FollowUps.Count; i++)
            {
                var blockId = $"{Html.IdFor(m => m.SubsidyExpiry.FollowUps[i].Enabled)}-block";
                <div class="form-group row">
                    <div class="col-md-3">
                        <nop-label asp-for="SubsidyExpiry.FollowUps[i].Enabled" asp-resource-params="@(new string[] { Model.SubsidyExpiry.FollowUps[i].Number.ToString() })" />
                    </div>
                    <div class="col-md-9">
                        <div class="row">
                            <div class="col-sm-auto">
                                <input asp-for="SubsidyExpiry.FollowUps[i].Enabled" class="cw-subsidy-followup-enable" data-followup-block="@blockId" />
                            </div>
                            <div class="col-sm-8" id="@blockId">
                                <div class="row">
                                    <div class="col-3">
                                        <nop-editor asp-for="SubsidyExpiry.FollowUps[i].DelayBeforeSend" />
                                    </div>
                                    <div class="col-auto">
                                        <nop-select asp-for="SubsidyExpiry.FollowUps[i].DelayPeriodId" asp-items="@(await ((MessageDelayPeriod)Model.SubsidyExpiry.FollowUps[i].DelayPeriodId).ToSelectListAsync())" />
                                    </div>
                                    <div class="col-auto label-wrapper">
                                        <div title="@T("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend.Hint")" data-toggle="tooltip" class="ico-help"><i class="fas fa-circle-question"></i></div>
                                    </div>
                                </div>
                                <span asp-validation-for="SubsidyExpiry.FollowUps[i].DelayBeforeSend"></span>
                                <span asp-validation-for="SubsidyExpiry.FollowUps[i].DelayPeriodId"></span>
                            </div>
                        </div>
                    </div>
                </div>
            }
            <div class="form-group row">
                <div class="col-md-9 offset-md-3">
                    <span asp-validation-for="SubsidyExpiry.FollowUps"></span>
                </div>
            </div>
        </nop-nested-setting>
    </div>
</div>
```

#### 20. ADD FILE `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Reminder/Index.cshtml`

nop's Reminders page with one widget zone added (a copy of a nop file: re-diff on every nop upgrade). (126 lines)

```cshtml
@* nop's Areas/Admin/Views/Reminder/Index.cshtml (5.00) with one widget zone added for the CW cards. Re-diff on every nopCommerce upgrade. *@
@using Nop.Core.Domain.Messages
@using Nop.Services
@using Nop.Plugin.CW.Core.Constants
@model RemindersModel

@{
    //page title
    ViewBag.PageTitle = T("Admin.Promotions.Reminders").Text;
    //active menu item (system name)
    NopHtml.SetActiveMenuItemSystemName("Reminders");
}

<form asp-controller="Reminder" method="post">
    <div class="content-header clearfix">
        <h1 class="float-left">
            @T("Admin.Promotions.Reminders")
        </h1>
        <div class="float-right">
            <button type="submit" name="save" class="btn btn-primary">
                <i class="far fa-floppy-disk"></i>
                @T("Admin.Common.Save")
            </button>
        </div>
    </div>
    <section class="content">
        <div class="container-fluid">
            <div class="form-horizontal">
                @await Component.InvokeAsync(typeof(StoreScopeConfigurationViewComponent))
                <div class="card card-default">
                    <div class="card-body">
                        <div class="cards-group">
                            <div class="card card-default">
                                <div class="card-body">
                                    <div class="form-group row">
                                        <div class="col-md-3">
                                            <nop-label asp-for="AbandonedCartEnabled" />
                                        </div>
                                        <div class="col-md-9">
                                            <nop-editor asp-for="AbandonedCartEnabled" />
                                            <span asp-validation-for="AbandonedCartEnabled"></span>
                                        </div>
                                    </div>
                                    <nop-nested-setting asp-for="AbandonedCartEnabled">
                                        @{
                                            var abandonedCartFollowUpsViewData = new ViewDataDictionary(ViewData);
                                            abandonedCartFollowUpsViewData.TemplateInfo.HtmlFieldPrefix = nameof(Model.AbandonedCartFollowUps);
                                            @await Html.PartialAsync("_FollowUp", Model.AbandonedCartFollowUps, abandonedCartFollowUpsViewData)
                                        }
                                    </nop-nested-setting>
                                </div>
                            </div>

                            <div class="card card-default">
                                <div class="card-body">
                                    <div class="form-group row">
                                        <div class="col-md-3">
                                            <nop-override-store-checkbox asp-for="PendingOrdersEnabled_OverrideForStore" asp-input="PendingOrdersEnabled" asp-store-scope="@Model.ActiveStoreScopeConfiguration" />
                                            <nop-label asp-for="PendingOrdersEnabled" />
                                        </div>
                                        <div class="col-md-9">
                                            <nop-editor asp-for="PendingOrdersEnabled" />
                                            <span asp-validation-for="PendingOrdersEnabled"></span>
                                        </div>
                                    </div>
                                    <nop-nested-setting asp-for="PendingOrdersEnabled">
                                        @{
                                            var pendingOrdersFollowUpsUpsViewData = new ViewDataDictionary(ViewData);
                                            pendingOrdersFollowUpsUpsViewData.TemplateInfo.HtmlFieldPrefix = nameof(Model.PendingOrdersFollowUps);
                                            @await Html.PartialAsync("_FollowUp", Model.PendingOrdersFollowUps, pendingOrdersFollowUpsUpsViewData)
                                        }
                                    </nop-nested-setting>
                                </div>
                            </div>

                            <div class="card card-default">
                                <div class="card-body">
                                    <div class="form-group row">
                                        <div class="col-md-3">
                                            <nop-override-store-checkbox asp-for="IncompleteRegistrationEnabled_OverrideForStore" asp-input="IncompleteRegistrationEnabled" asp-store-scope="@Model.ActiveStoreScopeConfiguration" />
                                            <nop-label asp-for="IncompleteRegistrationEnabled" />
                                        </div>
                                        <div class="col-md-9">
                                            <nop-editor asp-for="IncompleteRegistrationEnabled" />
                                            <span asp-validation-for="IncompleteRegistrationEnabled"></span>
                                        </div>
                                    </div>
                                    <nop-nested-setting asp-for="IncompleteRegistrationEnabled">
                                        @{
                                            var incompleteRegistrationFollowUpsViewData = new ViewDataDictionary(ViewData);
                                            incompleteRegistrationFollowUpsViewData.TemplateInfo.HtmlFieldPrefix = nameof(Model.IncompleteRegistrationFollowUps);
                                            @await Html.PartialAsync("_FollowUp", Model.IncompleteRegistrationFollowUps, incompleteRegistrationFollowUpsViewData)
                                        }
                                    </nop-nested-setting>
                                </div>
                            </div>

                            @* CW.Core: the subsidy card (SubsidyExpiryRemindersViewComponent), inside nop's form so nop's Save posts it *@
                            @await Component.InvokeAsync(typeof(AdminWidgetViewComponent), new { widgetZone = CWWidgetsZones.ADMIN_REMINDERS_BOTTOM, additionalData = Model })
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </section>
</form>

<script asp-location="Footer">
    $(function() {
        $('#@Html.IdFor(m => m.AbandonedCartEnabled)').on('change', function() {
            warningValidation('@Url.Action("ProcessAbandonedCartsTaskWarning", "Reminder")', this.name, { enabled: this.checked });
        });
        $('#@Html.IdFor(m => m.AbandonedCartEnabled)').change();


        $('#@Html.IdFor(m => m.IncompleteRegistrationEnabled)').on('change', function() {
            warningValidation('@Url.Action("ProcessIncompleteRegistrationsTaskWarning", "Reminder")', this.name, { enabled: this.checked });
        });
        $('#@Html.IdFor(m => m.IncompleteRegistrationEnabled)').change();

        $('#@Html.IdFor(m => m.PendingOrdersEnabled)').on('change', function() {
            warningValidation('@Url.Action("ProcessPendingOrdersTaskWarning", "Reminder")', this.name, { enabled: this.checked });
        });
        $('#@Html.IdFor(m => m.PendingOrdersEnabled)').change();
    });
</script>
```

#### 21. ADD FILE `Plugins/Nop.Plugin.CW.Core/Filters/SubsidyRemindersSaveFilter.cs`

Validates and saves the card when nop's Save posts the page. (127 lines)

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Controllers;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Validates and saves the subsidy card of Promotions › Reminders when nop's Save posts the page.
/// </summary>
/// <remarks>
/// <para>
/// The card is drawn inside nop's form (<c>SubsidyExpiryRemindersViewComponent</c>), so it is posted to nop's
/// <c>ReminderController.Index</c>, whose model knows nothing of it. Before the action runs, the card's fields
/// (<c>SubsidyExpiry.*</c>) are bound and validated, and an error goes into the page's ModelState: nop then saves
/// nothing and shows the page again with the error under the field. After the action, when the form was valid, the
/// card is saved. A save is all or nothing, nop's cards and this one together.
/// </para>
/// <para>
/// Only a post that carries the card's store-scope field is touched: a page drawn without the card (subsidies off)
/// saves nothing of it, so its switch is never turned off by a form that did not show it.
/// </para>
/// <para>
/// <b>Matched on nop's controller type and method name.</b> The method name is pinned to nop by
/// <c>SubsidyRemindersSaveFilterTests</c>.
/// </para>
/// </remarks>
public class SubsidyRemindersSaveFilter : IAsyncActionFilter
{
    #region Fields

    /// <summary>
    /// The prefix of the card's fields
    /// </summary>
    public const string FormPrefix = nameof(CWRemindersModel.SubsidyExpiry);

    /// <summary>
    /// The field every post of the card carries
    /// </summary>
    public const string CardField = FormPrefix + "." + nameof(SubsidyExpiryRemindersModel.ActiveStoreScopeConfiguration);

    private const string IndexMethod = nameof(ReminderController.Index);

    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly ISubsidyReminderService _subsidyReminderService;

    #endregion

    #region Ctor

    public SubsidyRemindersSaveFilter(ILocalizationService localizationService,
        IStoreContext storeContext,
        ISubsidyReminderService subsidyReminderService)
    {
        _localizationService = localizationService;
        _storeContext = storeContext;
        _subsidyReminderService = subsidyReminderService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Whether the request is nop's save of the Reminders page with the card on it
    /// </summary>
    /// <param name="context">The action context</param>
    protected virtual bool IsCardSave(ActionExecutingContext context)
    {
        if (context.Controller is not ReminderController
            || context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor
            || actionDescriptor.MethodInfo.Name != IndexMethod)
            return false;

        var request = context.HttpContext.Request;

        return HttpMethods.IsPost(request.Method) && request.HasFormContentType && request.Form.ContainsKey(CardField);
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsCardSave(context))
        {
            await next();
            return;
        }

        var model = new SubsidyExpiryRemindersModel();
        await ((ControllerBase)context.Controller).TryUpdateModelAsync(model, FormPrefix);

        var validationResult = await new SubsidyExpiryRemindersValidator(_localizationService).ValidateAsync(model);

        foreach (var error in validationResult.Errors)
            context.ModelState.AddModelError($"{FormPrefix}.{error.PropertyName}", error.ErrorMessage);

        //nop saves its own cards only when the whole form is valid; the card follows the same answer
        var saveCard = context.ModelState.IsValid;

        var executedContext = await next();

        //a later filter that answered instead of the action means nop saved nothing; neither does the card
        if (!saveCard || executedContext.Canceled || executedContext.Exception != null)
            return;

        var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();

        await _subsidyReminderService.SaveExpiryReminderSettingsAsync(storeId, model.Enabled, model.Enabled_OverrideForStore,
            model.FollowUps
                .Select(followUp => new SubsidyExpiryFollowUpSetting(followUp.Enabled, followUp.DelayBeforeSend, (MessageDelayPeriod)followUp.DelayPeriodId))
                .ToList());
    }

    #endregion
}
```

#### 22. ADD FILE `Plugins/Nop.Plugin.CW.Core/Content/Admin/Reminders/admin.subsidy.reminders.js`

Shows a follow-up's lead time only while it is enabled. (26 lines)

```javascript
/*
 * The subsidy card of Promotions > Reminders: each follow-up's lead time shows only while that follow-up is enabled.
 * The card is drawn once per page and never refilled, so the handler is bound once, from the card itself.
 */
(function () {
    'use strict';

    function toggleBlock(checkbox) {
        $('#' + checkbox.getAttribute('data-followup-block')).toggle(checkbox.checked);
    }

    $(function () {
        var $card = $('#cw-subsidy-expiry-reminders');

        if (!$card.length)
            return;

        $card.off('.cwSubsidyReminders').on('change.cwSubsidyReminders', '.cw-subsidy-followup-enable', function () {
            toggleBlock(this);
        });

        $card.find('.cw-subsidy-followup-enable').each(function () {
            toggleBlock(this);
        });
    });
})();
```

### 4.2 Changes to existing files

#### 23. REPLACE — `Plugins/Nop.Plugin.CW.Core/Constants/CWWidgetsZones.cs`, lines 12–14

The new zone the copied Reminders page renders. 3 line(s) become 8.

FIND (lines 12–14):
```csharp

        public const string WIDGET_SIZES_CHART_PRODUCT = "size_charts_produt_detail";
    }
```

REPLACE WITH:
```csharp

        public const string WIDGET_SIZES_CHART_PRODUCT = "size_charts_produt_detail";

        /// <summary>
        /// Promotions › Reminders, inside nop's form after nop's cards; drawn by CW.Core's copy of nop's page
        /// </summary>
        public const string ADMIN_REMINDERS_BOTTOM = "cw_admin_reminders_bottom";
    }
```

#### 24. REPLACE — `Plugins/Nop.Plugin.CW.Core/Domain/Messages/CWMessageTemplateSystemNames.cs`, lines 22–25

The three follow-up templates. 4 line(s) become 23.

FIND (lines 22–25):
```csharp
    public const string CustomerRegistrationImport = "Customer.Subsidy.CustomerRegistrationImport";

    #endregion
}
```

REPLACE WITH:
```csharp
    public const string CustomerRegistrationImport = "Customer.Subsidy.CustomerRegistrationImport";

    #endregion

    #region Subsidy templates

    /// <summary>
    /// Subsidy expiry reminder, follow up #1: the earliest before a subsidy's season ends
    /// </summary>
    public const string SubsidyExpiryFollowUp1 = "Customer.Subsidy.ExpiryReminder.FollowUp1";

    /// <summary>
    /// Subsidy expiry reminder, follow up #2
    /// </summary>
    public const string SubsidyExpiryFollowUp2 = "Customer.Subsidy.ExpiryReminder.FollowUp2";

    /// <summary>
    /// Subsidy expiry reminder, follow up #3: the latest before a subsidy's season ends
    /// </summary>
    public const string SubsidyExpiryFollowUp3 = "Customer.Subsidy.ExpiryReminder.FollowUp3";

    #endregion
}
```

#### 25. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Messages/CWTokenGroupNames.cs`, lines 21–22

The token group the three templates offer in the template editor. 2 line(s) become 7.

FIND (lines 21–22):
```csharp
    public const string ReturnPackNameTokens = "Return packname tokens";
}
```

REPLACE WITH:
```csharp
    public const string ReturnPackNameTokens = "Return packname tokens";

    /// <summary>
    /// Represents the tokens of the subsidy expiry reminders: the seasons ending and the days left
    /// </summary>
    public const string SubsidyExpiryTokens = "Subsidy expiry tokens";
}
```

#### 26. REPLACE — `Plugins/Nop.Plugin.CW.Core/Data/Mapping/CWNameCompatibility.cs`, line 94

The table name of the new entity. 1 line(s) become 2.

FIND (line 94):
```csharp
        { typeof(SubsidyRestoreItem), "CORE_SUB_SubsidyRestoreItem" },
```

REPLACE WITH:
```csharp
        { typeof(SubsidyRestoreItem), "CORE_SUB_SubsidyRestoreItem" },
        { typeof(SubsidyReminder), "CORE_SUB_SubsidyReminder" },
```

#### 27. REPLACE — `Plugins/Nop.Plugin.CW.Core/Data/MigrationVersions.cs`, lines 16–17

The version of the update migration. 2 line(s) become 7.

FIND (lines 16–17):
```csharp
    public const string ReturnRequestItemApprovalQueuedEmailId = "2026/11/06 12:00:00";
}
```

REPLACE WITH:
```csharp
    public const string ReturnRequestItemApprovalQueuedEmailId = "2026/11/06 12:00:00";

    /// <summary>
    /// CORE_SUB_SubsidyReminder, on a database installed before the table was in the schema
    /// </summary>
    public const string SubsidyReminder = "2026/11/09 12:00:00";
}
```

#### 28. REPLACE — `Plugins/Nop.Plugin.CW.Core/Data/Migrations/SchemaMigration.cs`, lines 134–136

One reminder row per assignment and season end, on a new install. 3 line(s) become 9.

FIND (lines 134–136):
```csharp
        CreateLiveRowsUniqueIndex(typeof(SubsidyVariation), "UX_CORE_SUB_SubsidyVariation_SubsidyPackNameId",
            nameof(SubsidyVariation.SubsidyPackNameId));
    }
```

REPLACE WITH:
```csharp
        CreateLiveRowsUniqueIndex(typeof(SubsidyVariation), "UX_CORE_SUB_SubsidyVariation_SubsidyPackNameId",
            nameof(SubsidyVariation.SubsidyPackNameId));

        Create.Index(SubsidyReminderUniqueIndex)
            .OnTable(NameCompatibilityManager.GetTableName(typeof(SubsidyReminder)))
            .OnColumn(nameof(SubsidyReminder.SubsidyPackNameId)).Ascending()
            .OnColumn(nameof(SubsidyReminder.PeriodEnd)).Ascending()
            .WithOptions().Unique();
    }
```

#### 29. REPLACE — `Plugins/Nop.Plugin.CW.Core/Data/Migrations/SchemaMigration.cs`, lines 77–78

The index name the schema and the update migration share. 2 line(s) become 7.

FIND (lines 77–78):
```csharp
        typeof(SubsidyVariationHistory)
    ];
```

REPLACE WITH:
```csharp
        typeof(SubsidyVariationHistory)
    ];

    /// <summary>
    /// The unique index of the subsidy reminder table, created here and by <see cref="SubsidyReminderMigration"/>
    /// </summary>
    internal const string SubsidyReminderUniqueIndex = "UX_CORE_SUB_SubsidyReminder_SubsidyPackNameId_PeriodEnd";
```

#### 30. REPLACE — `Plugins/Nop.Plugin.CW.Core/Data/Migrations/SchemaMigration.cs`, line 73

After SubsidyPackName, which its foreign key points to. 1 line(s) become 2.

FIND (line 73):
```csharp
        typeof(SubsidyRestoreItem),
```

REPLACE WITH:
```csharp
        typeof(SubsidyRestoreItem),
        typeof(SubsidyReminder),
```

#### 31. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Messages/MessageTokenProviderOverride.cs`, lines 353–359

The tokens the template editor lists for the three templates. 7 line(s) become 16.

FIND (lines 353–359):
```csharp
            case CWMessageTemplateSystemNames.CustomerRegistrationImport:
                return new[]
                {
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.CustomerTokens,
                    CWTokenGroupNames.SubsidyTokens
                };
```

REPLACE WITH:
```csharp
            case CWMessageTemplateSystemNames.CustomerRegistrationImport:
                return new[]
                {
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.CustomerTokens,
                    CWTokenGroupNames.SubsidyTokens
                };
            case CWMessageTemplateSystemNames.SubsidyExpiryFollowUp1:
            case CWMessageTemplateSystemNames.SubsidyExpiryFollowUp2:
            case CWMessageTemplateSystemNames.SubsidyExpiryFollowUp3:
                return new[]
                {
                    TokenGroupNames.StoreTokens,
                    TokenGroupNames.CustomerTokens,
                    CWTokenGroupNames.SubsidyExpiryTokens
                };
```

#### 32. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/EventConsumer.cs`, lines 750–751

The two new tokens, in the new token group. 2 line(s) become 9.

FIND (lines 750–751):
```csharp
            eventMessage.AdditionalTokens.Add("%Subsidy.Details%");
        }
```

REPLACE WITH:
```csharp
            eventMessage.AdditionalTokens.Add("%Subsidy.Details%");
        }

        //The subsidy expiry reminders: the seasons ending, and the fewest days left among them
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.SubsidyExpiryTokens))
        {
            eventMessage.AdditionalTokens.Add("%Subsidy.ExpiryDetails%");
            eventMessage.AdditionalTokens.Add("%Subsidy.DaysLeft%");
        }
```

#### 33. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Messages/IPackNameWorkflowMessageService.cs`, lines 25–26

The new e-mail. 2 line(s) become 16.

FIND (lines 25–26):
```csharp
    Task<IList<int>> SendCustomerRegisteredByManagerMessageAsync(Customer customer, PackName packName, string branchName, IList<string> subsidyNames, int storeId, int languageId);
}
```

REPLACE WITH:
```csharp
    Task<IList<int>> SendCustomerRegisteredByManagerMessageAsync(Customer customer, PackName packName, string branchName, IList<string> subsidyNames, int storeId, int languageId);

    /// <summary>
    /// Sends a subsidy expiry reminder to the customer of a packname: every season listed, each with its last day, the
    /// days left and what is left to spend
    /// </summary>
    /// <param name="customer">The packname's own customer account</param>
    /// <param name="messageTemplateName">The follow-up template the reminder is written with</param>
    /// <param name="items">The seasons ending, in the order listed</param>
    /// <param name="storeId">The store the subsidies belong to</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the queued e-mail identifiers
    /// </returns>
    Task<IList<int>> SendSubsidyExpiryReminderAsync(Customer customer, string messageTemplateName, IList<SubsidyExpiryReminderItem> items, int storeId);
}
```

#### 34. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Messages/IPackNameWorkflowMessageService.cs`, line 2

For SubsidyExpiryReminderItem. 1 line(s) become 2.

FIND (line 2):
```csharp
using Nop.Plugin.CW.Core.Domain.PackNames;
```

REPLACE WITH:
```csharp
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
```

#### 35. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`, lines 212–216

The new e-mail, after the welcome e-mail. 5 line(s) become 48.

FIND (lines 212–216):
```csharp
        }).ToListAsync();
    }

    #endregion
}
```

REPLACE WITH:
```csharp
        }).ToListAsync();
    }

    public virtual async Task<IList<int>> SendSubsidyExpiryReminderAsync(Customer customer, string messageTemplateName, IList<SubsidyExpiryReminderItem> items, int storeId)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentException.ThrowIfNullOrEmpty(messageTemplateName);
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0 || string.IsNullOrEmpty(customer.Email))
            return new List<int>();

        var store = await _storeService.GetStoreByIdAsync(storeId) ?? await _storeContext.GetCurrentStoreAsync();
        var languageId = await EnsureLanguageIsActiveAsync(customer.LanguageId ?? 0, store.Id);

        var messageTemplates = await GetActiveMessageTemplatesAsync(messageTemplateName, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        //tokens
        var commonTokens = new List<Token>();
        await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, customer);

        commonTokens.Add(new Token("Subsidy.DaysLeft", items.Min(item => item.DaysLeft)));
        commonTokens.Add(new Token("Subsidy.ExpiryDetails", await PrepareSubsidyExpiryDetailsAsync(items, languageId), true));

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            //email account
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            //event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            var toEmail = customer.Email;
            var toName = await _customerService.GetCustomerFullNameAsync(customer);

            //queued to go now: the template's delay says how long before the season ends the reminder is due, which the
            //reminder service has already applied
            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName, ignoreDelayBeforeSend: true);
        }).ToListAsync();
    }

    #endregion
}
```

#### 36. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`, lines 148–151

The details token, after the welcome e-mail's. 4 line(s) become 46.

FIND (lines 148–151):
```csharp
        return $"{WebUtility.HtmlEncode(title)}<br />{string.Join("<br />", lines)}<br />";
    }

    #endregion
```

REPLACE WITH:
```csharp
        return $"{WebUtility.HtmlEncode(title)}<br />{string.Join("<br />", lines)}<br />";
    }

    /// <summary>
    /// The seasons an expiry reminder lists: a title, then one line per season with its subsidy and packname, its last
    /// day, the days left and what is left to spend.
    /// </summary>
    /// <param name="items">The seasons, in the order listed</param>
    /// <param name="languageId">Message language identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the HTML, encoded</returns>
    protected virtual async Task<string> PrepareSubsidyExpiryDetailsAsync(IList<SubsidyExpiryReminderItem> items, int languageId)
    {
        var lineFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Expiry.Line", languageId);
        var daysLeftFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Expiry.DaysLeft", languageId);
        var lastDay = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Expiry.LastDay", languageId);
        var creditFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Credit", languageId);
        var unitsFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Units", languageId);

        //credit is held in the primary store currency, the customer's e-mail states it in that currency
        var primaryCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);

        var lines = new List<string>();

        foreach (var item in items)
        {
            var parts = new List<string>
            {
                string.Format(lineFormat, WebUtility.HtmlEncode(item.Subsidy), WebUtility.HtmlEncode(item.PackName), item.PeriodEnd.ToShortDateString()),
                item.DaysLeft > 0 ? string.Format(daysLeftFormat, item.DaysLeft) : WebUtility.HtmlEncode(lastDay)
            };

            if (item.RemainingCredit > decimal.Zero)
                parts.Add(string.Format(creditFormat, WebUtility.HtmlEncode(await _priceFormatter.FormatPriceAsync(item.RemainingCredit, true, primaryCurrency))));

            if (item.RemainingUnits > 0)
                parts.Add(string.Format(unitsFormat, item.RemainingUnits));

            lines.Add(string.Join(", ", parts));
        }

        var title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Expiry.Title", languageId);

        return $"{WebUtility.HtmlEncode(title)}<br />{string.Join("<br />", lines)}<br />";
    }

    #endregion
```

#### 37. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`, line 8

For SubsidyExpiryReminderItem. 1 line(s) become 2.

FIND (line 8):
```csharp
using Nop.Plugin.CW.Core.Domain.PackNames;
```

REPLACE WITH:
```csharp
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
```

#### 38. REPLACE — `Plugins/Nop.Plugin.CW.Core/Infrastructure/NopStartup.cs`, line 212

The filter that saves the card. 1 line(s) become 2.

FIND (line 212):
```csharp
            options.Filters.Add<ReturnReasonActionDeleteGuardFilter>(1);
```

REPLACE WITH:
```csharp
            options.Filters.Add<ReturnReasonActionDeleteGuardFilter>(1);
            options.Filters.Add<SubsidyRemindersSaveFilter>(1);
```

#### 39. REPLACE — `Plugins/Nop.Plugin.CW.Core/Infrastructure/NopStartup.cs`, line 167

The card's factory. 1 line(s) become 2.

FIND (line 167):
```csharp
        services.AddScoped<ISubsidyModelFactory, SubsidyModelFactory>();
```

REPLACE WITH:
```csharp
        services.AddScoped<ISubsidyModelFactory, SubsidyModelFactory>();
        services.AddScoped<ISubsidyReminderModelFactory, SubsidyReminderModelFactory>();
```

#### 40. REPLACE — `Plugins/Nop.Plugin.CW.Core/Infrastructure/NopStartup.cs`, line 90

The reminder service. The scheduled task is not registered: nop builds it itself. 1 line(s) become 2.

FIND (line 90):
```csharp
        services.AddScoped<ISubsidyReportingService, SubsidyReportingService>();
```

REPLACE WITH:
```csharp
        services.AddScoped<ISubsidyReportingService, SubsidyReportingService>();
        services.AddScoped<ISubsidyReminderService, SubsidyReminderService>();
```

#### 41. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, lines 2170–2177

Uninstall removes the task. 8 line(s) become 13.

FIND (lines 2170–2177):
```csharp
        //settings
        if (_widgetSettings.ActiveWidgetSystemNames.Contains(CWCoreDefaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove(CWCoreDefaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        await base.UninstallAsync();
```

REPLACE WITH:
```csharp
        //settings
        if (_widgetSettings.ActiveWidgetSystemNames.Contains(CWCoreDefaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove(CWCoreDefaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        //the scheduler would fail on every run to find the type of a task left behind
        var subsidyExpiryTask = await _scheduleTaskService.GetTaskByTypeAsync(SubsidyReminderDefaults.ExpiryTaskTypeFullName);
        if (subsidyExpiryTask != null)
            await _scheduleTaskService.DeleteTaskAsync(subsidyExpiryTask);

        await base.UninstallAsync();
```

#### 42. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, line 1754

The new resources. Installed by the plugin update because the version carries DATA. 1 line(s) become 18.

FIND (line 1754):
```csharp
            ["Plugins.CW.Core.Messages.Subsidy.Details.Units"] = "{0} items",
```

REPLACE WITH:
```csharp
            ["Plugins.CW.Core.Messages.Subsidy.Details.Units"] = "{0} items",
            ["Plugins.CW.Core.Messages.Subsidy.Expiry.Title"] = "Subsidies ending soon:",
            ["Plugins.CW.Core.Messages.Subsidy.Expiry.Line"] = "{0} for {1} ends on {2}",
            ["Plugins.CW.Core.Messages.Subsidy.Expiry.DaysLeft"] = "{0} day(s) left",
            ["Plugins.CW.Core.Messages.Subsidy.Expiry.LastDay"] = "today is the last day",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Enabled"] = "Subsidy expiry reminders enabled",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Enabled.Hint"] = "Check to e-mail the customer of a packname before the ordering period of one of its subsidies ends. Only a packname with its own customer account is reminded, and only while something is left to spend in the period: subsidy credit or items.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Info"] = "Subsidy expiry reminders go to the customer account of each packname before a subsidy's ordering period ends. A packname without its own account is not reminded. A reminder is sent only while something is left to spend in the period (subsidy credit or items), and a customer with several subsidies ending gets one e-mail listing them all. Subsidies that allow ordering outside the period are reminded too: after the end the customer may still order, but the subsidy no longer pays.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.Enabled"] = "Follow up #{0}",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.Enabled.Hint"] = "Check to send this reminder.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend"] = "Before the end",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend.Hint"] = "How long before the subsidy's ordering period ends this reminder is sent. Follow up #1 is the earliest and #3 the latest.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend.MustBeGreaterThanZero"] = "The time before the end must be greater than 0.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend.Maximum"] = "The time before the end cannot be more than a year.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayPeriod.Invalid"] = "Choose hours or days.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUps.Order"] = "Each follow up must be sent closer to the end than the one before it: #1 the longest before the end, #3 the shortest.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Warning.TaskDisabled"] = "The \"{0}\" scheduled task is disabled: no reminder is sent until it is <a href=\"{1}\" target=\"_blank\">enabled</a>.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Warning.TaskNotFound"] = "The scheduled task that sends the subsidy expiry reminders is missing. Update the CW Core plugin to add it again.",
```

#### 43. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, lines 343–346

The templates and the task, installed once each. 4 line(s) become 72.

FIND (lines 343–346):
```csharp
        await _localizationService.AddOrUpdateLocaleResourceAsync(
            $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.CustomerRegistrationImport}",
            "Sent to a customer a manager registers for a packname: the packname details, the subsidies assigned and a link to set the password.");
    }
```

REPLACE WITH:
```csharp
        await _localizationService.AddOrUpdateLocaleResourceAsync(
            $"Admin.ContentManagement.MessageTemplates.Description.{CWMessageTemplateSystemNames.CustomerRegistrationImport}",
            "Sent to a customer a manager registers for a packname: the packname details, the subsidies assigned and a link to set the password.");
    }

    /// <summary>
    /// Installs the three subsidy expiry reminders, each once: a store that has edited one keeps its wording and its lead
    /// time.
    /// </summary>
    /// <remarks>
    /// When each is sent is its <c>DelayBeforeSend</c> and <c>DelayPeriod</c>, before the season ends; Promotions ›
    /// Reminders edits them. The reminders stay off until switched on there.
    /// </remarks>
    protected async Task InstallSubsidyExpiryReminderTemplatesAsync()
    {
        var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
            ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

        const string body = "Hello %Customer.FullName%,<br /><br />" +
                            "You still have subsidy to spend at <a href=\"%Store.URL%\">%Store.Name%</a>, and it ends soon:<br />" +
                            "%Subsidy.ExpiryDetails%" +
                            "<br />What is not used by the end date cannot be used afterwards. Please sign in and place your order before then.<br /><br />" +
                            "Thank you,<br />%Store.Name%<br />";

        var templates = new[]
        {
            (Name: CWMessageTemplateSystemNames.SubsidyExpiryFollowUp1, Subject: "Your subsidy at %Store.Name% ends soon", DelayBeforeSend: 30, DelayPeriod: MessageDelayPeriod.Days, Number: 1),
            (Name: CWMessageTemplateSystemNames.SubsidyExpiryFollowUp2, Subject: "Your subsidy at %Store.Name% ends in %Subsidy.DaysLeft% day(s)", DelayBeforeSend: 7, DelayPeriod: MessageDelayPeriod.Days, Number: 2),
            (Name: CWMessageTemplateSystemNames.SubsidyExpiryFollowUp3, Subject: "Last reminder: your subsidy at %Store.Name% is about to end", DelayBeforeSend: 1, DelayPeriod: MessageDelayPeriod.Days, Number: 3)
        };

        foreach (var template in templates)
        {
            if ((await _messageTemplateService.GetMessageTemplatesByNameAsync(template.Name)).Any())
                continue;

            await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
            {
                Name = template.Name,
                Subject = template.Subject,
                Body = body,
                IsActive = true,
                EmailAccountId = emailAccount?.Id ?? 0,
                DelayBeforeSend = template.DelayBeforeSend,
                DelayPeriod = template.DelayPeriod
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(
                $"Admin.ContentManagement.MessageTemplates.Description.{template.Name}",
                $"Subsidy expiry reminder, follow up #{template.Number}: sent to the customer of a packname before a subsidy's ordering period ends, while something is left to spend. Switched on, and timed, on Promotions › Reminders.");
        }
    }

    /// <summary>
    /// Installs the scheduled task that sends the subsidy expiry reminders, once: a store that has disabled it or
    /// changed its interval keeps that.
    /// </summary>
    protected async Task InstallSubsidyExpiryReminderTaskAsync()
    {
        if (await _scheduleTaskService.GetTaskByTypeAsync(SubsidyReminderDefaults.ExpiryTaskTypeFullName) != null)
            return;

        await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
        {
            Name = SubsidyReminderDefaults.ExpiryTaskName,
            Seconds = SubsidyReminderDefaults.ExpiryTaskSeconds,
            Type = SubsidyReminderDefaults.ExpiryTaskTypeFullName,
            Enabled = true,
            LastEnabledUtc = DateTime.UtcNow,
            StopOnError = false
        });
    }
```

#### 44. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, line 92

Install and every update add the templates and the task when missing. 1 line(s) become 3.

FIND (line 92):
```csharp
        await InstallCustomerRegistrationTemplateAsync();
```

REPLACE WITH:
```csharp
        await InstallCustomerRegistrationTemplateAsync();
        await InstallSubsidyExpiryReminderTemplatesAsync();
        await InstallSubsidyExpiryReminderTaskAsync();
```

#### 45. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, lines 68–81

The constructor takes nop's schedule task service. 14 line(s) become 16.

FIND (lines 68–81):
```csharp
        ICWDatabaseService cwDatabaseService,
        IMessageTemplateService messageTemplateService,
        IEmailAccountService emailAccountService,
        EmailAccountSettings emailAccountSettings)
    {
        _settingService = settingService;
        _widgetSettings = widgetSettings;
        _localizationService = localizationService;
        _activityLogTypeRepository = activityLogTypeRepository;
        _cwDatabaseService = cwDatabaseService;
        _messageTemplateService = messageTemplateService;
        _emailAccountService = emailAccountService;
        _emailAccountSettings = emailAccountSettings;
    }
```

REPLACE WITH:
```csharp
        ICWDatabaseService cwDatabaseService,
        IMessageTemplateService messageTemplateService,
        IEmailAccountService emailAccountService,
        EmailAccountSettings emailAccountSettings,
        IScheduleTaskService scheduleTaskService)
    {
        _settingService = settingService;
        _widgetSettings = widgetSettings;
        _localizationService = localizationService;
        _activityLogTypeRepository = activityLogTypeRepository;
        _cwDatabaseService = cwDatabaseService;
        _messageTemplateService = messageTemplateService;
        _emailAccountService = emailAccountService;
        _emailAccountSettings = emailAccountSettings;
        _scheduleTaskService = scheduleTaskService;
    }
```

#### 46. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, line 57

Its field. 1 line(s) become 2.

FIND (line 57):
```csharp
    private readonly EmailAccountSettings _emailAccountSettings;
```

REPLACE WITH:
```csharp
    private readonly EmailAccountSettings _emailAccountSettings;
    private readonly IScheduleTaskService _scheduleTaskService;
```

#### 47. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, lines 47–48

The card in the new zone (a comma is added to the line above). 2 line(s) become 3.

FIND (lines 47–48):
```csharp
        [AdminWidgetZones.CustomerDetailsButtons] = typeof(OrderListBadgesViewComponent)
    };
```

REPLACE WITH:
```csharp
        [AdminWidgetZones.CustomerDetailsButtons] = typeof(OrderListBadgesViewComponent),
        [CWWidgetsZones.ADMIN_REMINDERS_BOTTOM] = typeof(SubsidyExpiryRemindersViewComponent)
    };
```

#### 48. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, lines 15–16

For IScheduleTaskService. 2 line(s) become 3.

FIND (lines 15–16):
```csharp
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;
```

REPLACE WITH:
```csharp
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Web.Framework.Infrastructure;
```

#### 49. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, lines 10–11

For SubsidyReminderDefaults. 2 line(s) become 3.

FIND (lines 10–11):
```csharp
using Nop.Plugin.CW.Core.Services.DataBase;
using Nop.Services.Cms;
```

REPLACE WITH:
```csharp
using Nop.Plugin.CW.Core.Services.DataBase;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Cms;
```

#### 50. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, lines 3–4

For ScheduleTask. 2 line(s) become 3.

FIND (lines 3–4):
```csharp
using Nop.Core.Domain.Messages;
using Nop.Data;
```

REPLACE WITH:
```csharp
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Data;
```

#### 51. REPLACE — `Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`, lines 331–333

The card's view. 3 line(s) become 6.

FIND (lines 331–333):
```xml
    <None Update="Areas\Admin\Views\Shared\Components\PackNameSubsidies\Default.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
```

REPLACE WITH:
```xml
    <None Update="Areas\Admin\Views\Shared\Components\PackNameSubsidies\Default.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Areas\Admin\Views\Shared\Components\SubsidyExpiryReminders\Default.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
```

#### 52. REPLACE — `Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`, lines 262–264

The copy of nop's Reminders page. 3 line(s) become 6.

FIND (lines 262–264):
```xml
    <None Update="Areas\Admin\Views\Product\_CreateOrUpdate.Info.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
```

REPLACE WITH:
```xml
    <None Update="Areas\Admin\Views\Product\_CreateOrUpdate.Info.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
    <None Update="Areas\Admin\Views\Reminder\Index.cshtml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
```

#### 53. REPLACE — `Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`, lines 39–41

The card's script. 3 line(s) become 6.

FIND (lines 39–41):
```xml
    <Content Include="Content\Admin\Orders\admin.order.badges.js">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
```

REPLACE WITH:
```xml
    <Content Include="Content\Admin\Orders\admin.order.badges.js">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="Content\Admin\Reminders\admin.subsidy.reminders.js">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
```

#### 54. REPLACE — `Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`, line 26

As the other scripts: not a None item. 1 line(s) become 2.

FIND (line 26):
```xml
    <None Remove="Content\Admin\Orders\admin.order.badges.js" />
```

REPLACE WITH:
```xml
    <None Remove="Content\Admin\Orders\admin.order.badges.js" />
    <None Remove="Content\Admin\Reminders\admin.subsidy.reminders.js" />
```

#### 55. REPLACE — `Plugins/Nop.Plugin.CW.Core/plugin.json`, line 5

Runs the update: the migration, the resources, the templates and the task. 1 line(s) become 1.

FIND (line 5):
```json
  "Version": "2026_11_09_DATA_0.1",
```

REPLACE WITH:
```json
  "Version": "2026_11_09_DATA_0.2",
```

## 5. Build and checks

1. Build the solution (`NopCommerce.sln`), then run the CW.Core tests:
   `dotnet test Tests/CW.Tests -p:SolutionDir=<repo root> --filter "FullyQualifiedName~CW.Tests.Core"`.
   Until this spec is applied the test project does not compile: the new tests below use the new types.
2. **Registration and cycle checks** are in the tests: `NopStartupTests` (the filter's, the component's, the
   service's and the factory's CW dependencies are registered; the filter is in the global filter list) and
   `CWCorePluginWidgetTests` (the zone is drawn by the card). `SchemaMigrationTests` checks the new table is in the
   create list and has a builder.
3. **Razor views compile on first request.** Open Promotions › Reminders after the update, at all-stores scope and at
   a store scope.
4. **Plugin update.** The version becomes `2026_11_09_DATA_0.2`. Run *Configuration › Local plugins › CW Core ›
   Update* (or restart if your site applies updates on start). The update runs the migration (the table), installs the
   resources, the three templates and the task. Check:
   `SELECT Name, IsActive, DelayBeforeSend, DelayPeriodId FROM MessageTemplate WHERE Name LIKE 'Customer.Subsidy.ExpiryReminder%'`
   (three rows), `SELECT Name, Seconds, Enabled FROM ScheduleTask WHERE Type LIKE '%ProcessSubsidyExpiryRemindersTask'`
   (one row) and `SELECT OBJECT_ID('CORE_SUB_SubsidyReminder')` (not null).

### 5.1 Unit tests (written by Claude, in the repository)

| File | Cases | What |
|---|---|---|
| `Tests/CW.Tests/Core/Helpers/SubsidyExpiryReminderHelperTests.cs` (new) | 28 | Open season and its end (one-off, annual, across New Year, between seasons, last day); due follow-up (window, already sent, late reach sends only the latest, ended, a follow-up off); left to spend (top-ups, product groups, overspent bucket); days left; lead hours never throw; lead-time order |
| `Tests/CW.Tests/Core/Validators/SubsidyExpiryRemindersValidatorTests.cs` (new) | 8 | Defaults pass; hours vs days; out of order; disabled follow-up skipped; zero lead time; over a year; invalid period; nothing checked while off |
| `Tests/CW.Tests/Core/Filters/SubsidyRemindersSaveFilterTests.cs` (new) | 6 | Field names; nop's `Index(RemindersModel)` still exists; GET, a post without the card, another action and another controller are not touched |
| `Tests/CW.Tests/Core/Infrastructure/CWCorePluginWidgetTests.cs` (edited) | +1 | The new zone is drawn by `SubsidyExpiryRemindersViewComponent` |
| `Tests/CW.Tests/Core/Infrastructure/NopStartupTests.cs` (edited) | — | `SubsidyRemindersSaveFilter` added to the global filter list it checks |

## 6. Test plan (dev site)

Side effects: settings rows, message template edits, rows in `CORE_SUB_SubsidyReminder`, queued e-mails. Nothing is
deleted. To run the task on demand: System › Schedule tasks › *Process subsidy expiry reminders* › **Run now**.

| # | Step | Expected |
|---|---|---|
| T1 | Open Promotions › Reminders, all-stores scope | Fourth card with the info callout, the switch (tooltip on the label) and three follow-ups: 30 / 7 / 1 Days |
| T2 | Untick a follow-up's box | Its amount and period hide; tick again, they show |
| T3 | Switch on, Save | Success message; card shows on. `Setting` row `subsidyremindersettings.expiryremindersenabled = True`, store 0 |
| T4 | Set #1 = 1 day, #3 = 30 days, Save | Error under the follow-ups ("Each follow up must be sent closer…"); **nop's cards are not saved either** (change one of nop's values in the same post and check it did not change) |
| T5 | Set #2 = 0 with #2 ticked, Save | Error under #2's amount; posted values stay on screen |
| T6 | Store scope: tick *override for this store* next to the switch, switch off, Save | Store row saved; all-stores value unchanged |
| T7 | Switch the store's Subsidy feature off (Manage Store › Store Settings) | Card not drawn at that store's scope; Save of the page leaves `subsidyremindersettings.*` unchanged. Switch Subsidy back on (feature-off case, `Feature_Toggles_Reference.md` §1) |
| T8 | Disable the task in Schedule tasks, reopen the page | Warning callout with a link to Schedule tasks. Enable it again |
| T9 | Prepare data: a packname **with** an account and e-mail, assigned a subsidy whose season ends in 5 days with credit left; another packname **without** an account on the same subsidy; a third packname with an account whose credit and items are used up | — |
| T10 | Run the task | One e-mail queued (System › Message queue) to the first packname's customer, written with **#2**'s template (5 days is inside 7, not inside 1), listing the subsidy, packname, end date, "5 day(s) left" and the credit; none for the other two. One `CORE_SUB_SubsidyReminder` row, `LastFollowUpNumber = 2` |
| T11 | Run the task again | Nothing new queued |
| T12 | Give the first packname's customer a second subsidy ending in 20 days; run | One e-mail with #1's template listing **only** the 20-day season: the 5-day season already had #2 and its #3 is not due yet |
| T13 | Set #2 to 10 days and #3 to 6 days, Save, run | The 5-day season gets #3: one e-mail, `LastFollowUpNumber = 3` |
| T14 | Subsidy with `OrderOutsidePeriod` on, ending in 5 days, credit left, its packname with an account | Reminded like any other (decision 4) |
| T15 | Switch the card off, run | Nothing queued |
| T16 | Store with Subsidy off (T7 state), run | Nothing queued for that store |
| T17 | Variation (store with *PackNames own start and finish dates* on) moving the end 3 days later | The season end changes, so reminders start again for the new end |
| T18 | Uninstall check is not run (it would remove CW.Core's data); read the code of change 41 instead | Task deleted on uninstall |

## 7. To do at closure (Claude, documents only)

- `CLAUDE.md`: §10.3 (or §10.4's note) — `Areas/Admin/Views/Reminder/Index.cshtml` is a copy of nop's, re-diff on
  upgrade; §13.4 (registrations, filter), §13.7 (component), §14.4 (`SubsidyReminder` → `CORE_SUB_SubsidyReminder`),
  §15.x (`SubsidyReminderSettings`), §19 (the new admin zone).
- `CWPlans/subsidy-system.md`: a new section "Expiry reminders" (this spec's §1–§2, without the change list).
- `CWPlans/CWCore_Port_Reference.md`: add `Reminder/Index` to the forked views list of the upgrade checklist.
- `CWPlans/Feature_Toggles_Reference.md` §1: the card, the filter and the task as Subsidy readers.
- Move this spec to `CWPlans/Deprecated/` once the above is folded in (§0.4).

## 8. Open points (not in this spec)

- **Managers.** If the business wants managers told about their packnames without accounts, the clean shape is a
  second switch on the same card and one digest per manager listing their packnames (the owned set,
  `MultiStore_Management_Reference.md` §7.1). Not built now (decision 1).
- **Per-store lead times.** Would need store-limited copies of the three templates; nop's own cards do not do it either.

## 9. Corrections after apply (2026-11-09)

Found validating the applied code. Apply in this order (bottom to top in the one service file), then build once.

### 56. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Subsidies/SubsidyReminderService.cs`, lines 173–208 (whole method `GetCandidateAssignmentsAsync`, with its doc comment)

A packname whose list has no live branch cannot order (`ShoppingCartOverrideService.AddToCartAsync` refuses it with
`PackName.NotAvailableForOrdering`, and `vw_CORE_PN_PackNameShoppingCartItem` hides its lines), so it must not be told to
spend its subsidy. Adds one join and one condition. 36 lines become 38.

FIND (lines 173–208):
```csharp
    /// <summary>
    /// Every live assignment of the store whose packname has its own active customer account with an e-mail address,
    /// with the dates its season is worked out from
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the assignments</returns>
    protected virtual async Task<IList<SubsidyExpiryReminderItem>> GetCandidateAssignmentsAsync(int storeId)
    {
        var query = from sp in _subsidyPackNameRepository.Table
                    join s in _subsidyRepository.Table on sp.SubsidyId equals s.Id
                    join pn in _packNameRepository.Table on sp.PackNameId equals pn.Id
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    join c in _customerRepository.Table on pn.CustomerId equals (int?)c.Id
                    join v in _subsidyVariationRepository.Table.Where(variation => !variation.Deleted) on sp.Id equals v.SubsidyPackNameId into variations
                    from v in variations.DefaultIfEmpty()
                    where !sp.Deleted && sp.Active
                        && !s.Deleted && s.Active && s.StoreId == storeId
                        && !pn.Deleted && pn.Active && !pnl.Deleted
                        && !c.Deleted && c.Active && c.Email != null && c.Email != string.Empty
                    select new SubsidyExpiryReminderItem
                    {
                        SubsidyPackNameId = sp.Id,
                        PackNameId = pn.Id,
                        PackName = pn.Name,
                        CustomerId = c.Id,
                        SubsidyId = s.Id,
                        Subsidy = s.Name,
                        DurationStart = s.DurationStart,
                        DurationEnd = s.DurationEnd,
                        VariationDurationStart = v != null ? v.DurationStart : null,
                        VariationDurationEnd = v != null ? v.DurationEnd : null,
                        AnnualRecurrence = s.AnnualRecurrence
                    };

        return await query.ToListAsync();
    }
```

REPLACE WITH:
```csharp
    /// <summary>
    /// Every live assignment of the store whose packname has its own active customer account with an e-mail address
    /// and can order (its list on a live branch, as the packname cart requires), with the dates its season is worked
    /// out from
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the assignments</returns>
    protected virtual async Task<IList<SubsidyExpiryReminderItem>> GetCandidateAssignmentsAsync(int storeId)
    {
        var query = from sp in _subsidyPackNameRepository.Table
                    join s in _subsidyRepository.Table on sp.SubsidyId equals s.Id
                    join pn in _packNameRepository.Table on sp.PackNameId equals pn.Id
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    join b in _branchRepository.Table on pnl.BranchId equals (int?)b.Id
                    join c in _customerRepository.Table on pn.CustomerId equals (int?)c.Id
                    join v in _subsidyVariationRepository.Table.Where(variation => !variation.Deleted) on sp.Id equals v.SubsidyPackNameId into variations
                    from v in variations.DefaultIfEmpty()
                    where !sp.Deleted && sp.Active
                        && !s.Deleted && s.Active && s.StoreId == storeId
                        && !pn.Deleted && pn.Active && !pnl.Deleted && !b.Deleted
                        && !c.Deleted && c.Active && c.Email != null && c.Email != string.Empty
                    select new SubsidyExpiryReminderItem
                    {
                        SubsidyPackNameId = sp.Id,
                        PackNameId = pn.Id,
                        PackName = pn.Name,
                        CustomerId = c.Id,
                        SubsidyId = s.Id,
                        Subsidy = s.Name,
                        DurationStart = s.DurationStart,
                        DurationEnd = s.DurationEnd,
                        VariationDurationStart = v != null ? v.DurationStart : null,
                        VariationDurationEnd = v != null ? v.DurationEnd : null,
                        AnnualRecurrence = s.AnnualRecurrence
                    };

        return await query.ToListAsync();
    }
```

### 57. REPLACE — same file, lines 68–105 (whole constructor)

Takes the branch repository. 38 lines become 40.

FIND (lines 68–105):
```csharp
    public SubsidyReminderService(ICustomerService customerService,
        ICWFeatureService cwFeatureService,
        IDateTimeHelper dateTimeHelper,
        ILogger logger,
        IMessageTemplateService messageTemplateService,
        IPackNameWorkflowMessageService packNameWorkflowMessageService,
        IReminderService reminderService,
        IRepository<Customer> customerRepository,
        IRepository<PackName> packNameRepository,
        IRepository<PackNameList> packNameListRepository,
        IRepository<ProductGroupPackNameCredit> productGroupPackNameCreditRepository,
        IRepository<Subsidy> subsidyRepository,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        IRepository<SubsidyPackNameCredit> subsidyPackNameCreditRepository,
        IRepository<SubsidyReminder> subsidyReminderRepository,
        IRepository<SubsidyVariation> subsidyVariationRepository,
        ISettingService settingService,
        IStoreService storeService)
    {
        _customerService = customerService;
        _cwFeatureService = cwFeatureService;
        _dateTimeHelper = dateTimeHelper;
        _logger = logger;
        _messageTemplateService = messageTemplateService;
        _packNameWorkflowMessageService = packNameWorkflowMessageService;
        _reminderService = reminderService;
        _customerRepository = customerRepository;
        _packNameRepository = packNameRepository;
        _packNameListRepository = packNameListRepository;
        _productGroupPackNameCreditRepository = productGroupPackNameCreditRepository;
        _subsidyRepository = subsidyRepository;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _subsidyPackNameCreditRepository = subsidyPackNameCreditRepository;
        _subsidyReminderRepository = subsidyReminderRepository;
        _subsidyVariationRepository = subsidyVariationRepository;
        _settingService = settingService;
        _storeService = storeService;
    }
```

REPLACE WITH:
```csharp
    public SubsidyReminderService(ICustomerService customerService,
        ICWFeatureService cwFeatureService,
        IDateTimeHelper dateTimeHelper,
        ILogger logger,
        IMessageTemplateService messageTemplateService,
        IPackNameWorkflowMessageService packNameWorkflowMessageService,
        IReminderService reminderService,
        IRepository<Branch> branchRepository,
        IRepository<Customer> customerRepository,
        IRepository<PackName> packNameRepository,
        IRepository<PackNameList> packNameListRepository,
        IRepository<ProductGroupPackNameCredit> productGroupPackNameCreditRepository,
        IRepository<Subsidy> subsidyRepository,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        IRepository<SubsidyPackNameCredit> subsidyPackNameCreditRepository,
        IRepository<SubsidyReminder> subsidyReminderRepository,
        IRepository<SubsidyVariation> subsidyVariationRepository,
        ISettingService settingService,
        IStoreService storeService)
    {
        _customerService = customerService;
        _cwFeatureService = cwFeatureService;
        _dateTimeHelper = dateTimeHelper;
        _logger = logger;
        _messageTemplateService = messageTemplateService;
        _packNameWorkflowMessageService = packNameWorkflowMessageService;
        _reminderService = reminderService;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _packNameRepository = packNameRepository;
        _packNameListRepository = packNameListRepository;
        _productGroupPackNameCreditRepository = productGroupPackNameCreditRepository;
        _subsidyRepository = subsidyRepository;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _subsidyPackNameCreditRepository = subsidyPackNameCreditRepository;
        _subsidyReminderRepository = subsidyReminderRepository;
        _subsidyVariationRepository = subsidyVariationRepository;
        _settingService = settingService;
        _storeService = storeService;
    }
```

### 58. REPLACE — same file, line 52

FIND (line 52):
```csharp
    protected readonly IRepository<Customer> _customerRepository;
```

REPLACE WITH:
```csharp
    protected readonly IRepository<Branch> _branchRepository;
    protected readonly IRepository<Customer> _customerRepository;
```

### 59. REPLACE — same file, line 3

FIND (line 3):
```csharp
using Nop.Plugin.CW.Core.Domain.PackNames;
```

REPLACE WITH:
```csharp
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
```

### 60. REPLACE — `Plugins/Nop.Plugin.CW.Core/Nop.Plugin.CW.Core.csproj`, line 26 (optional, consistency)

The `None Remove` line of change 54 is not in the file. The script still deploys (its `Content` item copies it); the
line only keeps it like the other scripts.

FIND (line 26):
```xml
    <None Remove="Content\Admin\Orders\admin.order.badges.js" />
```

REPLACE WITH:
```xml
    <None Remove="Content\Admin\Orders\admin.order.badges.js" />
    <None Remove="Content\Admin\Reminders\admin.subsidy.reminders.js" />
```

Test plan addition — **T19:** a packname with an account, credit left and a season ending in 5 days, whose list's branch
is deleted: run the task, nothing is queued for it.

## 10. Start date and days left (2026-11-09)

Two changes Eduardo asked for, before the plugin update was first run (so no migration change and no new version: the
start date is a setting, and `2026_11_09_DATA_0.2` installs the new texts).

**Start date.** A new setting, `SubsidyReminderSettings.ExpiryRemindersStartDateUtc` (overridable per store, shown on
the card as **Start date**, UTC, nop's `DateTimeNullable` editor as on nop's discounts). A follow-up that **fell due**
before it is not sent. Saving the card with the switch on and no date fills it with the moment of the save, so
switching the reminders on never sends a backlog: a season ending in 5 days on the day they are switched on gets only #3,
the day before its end. Set an earlier date to catch up on purpose.

**Days left.** `%Subsidy.DaysLeft%` and the list now count **today included**: the time left rounded up to whole days.
The 7-day follow-up says "7 day(s) left"; the last day says "today is the last day" (1 day).

Labels as before. Changes 61–68 are whole files: replace each file's whole content. 69–71 are REPLACE blocks.

### 61. WHOLE FILE — `Plugins/Nop.Plugin.CW.Core/Domain/Settings/SubsidyReminderSettings.cs`

Adds the start date. (25 lines)

```csharp
using Nop.Core.Configuration;

namespace Nop.Plugin.CW.Core.Domain.Settings;

/// <summary>
/// The subsidy card of Promotions › Reminders, per store
/// </summary>
/// <remarks>
/// When each follow-up is sent is stored on its message template (<c>DelayBeforeSend</c>, <c>DelayPeriod</c>), as nop
/// stores it for its own reminders; this class holds only the switch.
/// </remarks>
public class SubsidyReminderSettings : ISettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the customer of a packname is reminded before a subsidy's ordering period
    /// ends
    /// </summary>
    public bool ExpiryRemindersEnabled { get; set; }

    /// <summary>
    /// Gets or sets the moment the reminders start from: a follow-up that fell due before it is not sent. Filled with the
    /// moment of the save when the reminders are switched on without one, so switching on never sends a backlog.
    /// </summary>
    public DateTime? ExpiryRemindersStartDateUtc { get; set; }
}
```

### 62. WHOLE FILE — `Plugins/Nop.Plugin.CW.Core/Helpers/SubsidyExpiryReminderHelper.cs`

`GetDueFollowUp` takes the start date; `DaysLeft` counts today (rounds the time left up to whole days). (162 lines)

```csharp
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
```

### 63. WHOLE FILE — `Plugins/Nop.Plugin.CW.Core/Services/Subsidies/ISubsidyReminderService.cs`

`SaveExpiryReminderSettingsAsync` takes the start date and its store override. (30 lines)

```csharp
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The subsidy expiry reminders: sending them, and saving when they are sent
/// </summary>
public interface ISubsidyReminderService
{
    /// <summary>
    /// Sends the follow-ups due now, in every store with subsidies and the reminders switched on: one e-mail per
    /// customer, listing each of their packname's subsidies whose season is ending with something left to spend
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SendExpiryRemindersAsync();

    /// <summary>
    /// Saves the subsidy card of Promotions › Reminders: the switch for the store scope, and when the switch is on, each
    /// follow-up on its message template
    /// </summary>
    /// <param name="storeId">The store scope the admin is editing; 0 for all stores</param>
    /// <param name="enabled">Whether the reminders are sent</param>
    /// <param name="overrideForStore">Whether the store keeps its own value of the switch (store scope only)</param>
    /// <param name="startDateUtc">The moment the reminders start from; null to start from the save when switched on</param>
    /// <param name="startDateOverrideForStore">Whether the store keeps its own start date (store scope only)</param>
    /// <param name="followUps">The follow-ups in follow-up order; any beyond the third are ignored</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SaveExpiryReminderSettingsAsync(int storeId, bool enabled, bool overrideForStore, DateTime? startDateUtc, bool startDateOverrideForStore,
        IList<SubsidyExpiryFollowUpSetting> followUps);
}
```

### 64. WHOLE FILE — `Plugins/Nop.Plugin.CW.Core/Services/Subsidies/SubsidyReminderService.cs`

**Includes corrections 56–59** (the live-branch join): if you have not applied them yet, skip them; if you have, this file is still the right one. Adds the start date to the due rule and to the save. (468 lines)

```csharp
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Reminders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The subsidy expiry reminders: which packname customers are reminded, when, and with what
/// </summary>
/// <remarks>
/// <para>
/// <b>Who.</b> The packname's own customer account, active and with an e-mail address. A packname without an account is
/// not reminded, and neither are the managers who order for it.
/// </para>
/// <para>
/// <b>When.</b> Each follow-up is sent its lead time before the season ends (23:59:59 of the last day, store time), the
/// lead time being the one nop's Reminders page stores on the follow-up's message template. A season is reminded only
/// while something is left to spend in it, credit or items, as the credit views count it. Subsidies with
/// <c>OrderOutsidePeriod</c> are reminded too: outside the season they may still order, but no subsidy pays for it.
/// </para>
/// <para>
/// <b>How.</b> One e-mail per customer and run, listing every season of theirs with a follow-up due, written with the
/// template of the latest follow-up among them. What was sent is kept per assignment and season end
/// (<see cref="SubsidyReminder"/>), so each follow-up goes once.
/// </para>
/// </remarks>
public class SubsidyReminderService : ISubsidyReminderService
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILogger _logger;
    protected readonly IMessageTemplateService _messageTemplateService;
    protected readonly IPackNameWorkflowMessageService _packNameWorkflowMessageService;
    protected readonly IReminderService _reminderService;
    protected readonly IRepository<Branch> _branchRepository;
    protected readonly IRepository<Customer> _customerRepository;
    protected readonly IRepository<PackName> _packNameRepository;
    protected readonly IRepository<PackNameList> _packNameListRepository;
    protected readonly IRepository<ProductGroupPackNameCredit> _productGroupPackNameCreditRepository;
    protected readonly IRepository<Subsidy> _subsidyRepository;
    protected readonly IRepository<SubsidyPackName> _subsidyPackNameRepository;
    protected readonly IRepository<SubsidyPackNameCredit> _subsidyPackNameCreditRepository;
    protected readonly IRepository<SubsidyReminder> _subsidyReminderRepository;
    protected readonly IRepository<SubsidyVariation> _subsidyVariationRepository;
    protected readonly ISettingService _settingService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public SubsidyReminderService(ICustomerService customerService,
        ICWFeatureService cwFeatureService,
        IDateTimeHelper dateTimeHelper,
        ILogger logger,
        IMessageTemplateService messageTemplateService,
        IPackNameWorkflowMessageService packNameWorkflowMessageService,
        IReminderService reminderService,
        IRepository<Branch> branchRepository,
        IRepository<Customer> customerRepository,
        IRepository<PackName> packNameRepository,
        IRepository<PackNameList> packNameListRepository,
        IRepository<ProductGroupPackNameCredit> productGroupPackNameCreditRepository,
        IRepository<Subsidy> subsidyRepository,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        IRepository<SubsidyPackNameCredit> subsidyPackNameCreditRepository,
        IRepository<SubsidyReminder> subsidyReminderRepository,
        IRepository<SubsidyVariation> subsidyVariationRepository,
        ISettingService settingService,
        IStoreService storeService)
    {
        _customerService = customerService;
        _cwFeatureService = cwFeatureService;
        _dateTimeHelper = dateTimeHelper;
        _logger = logger;
        _messageTemplateService = messageTemplateService;
        _packNameWorkflowMessageService = packNameWorkflowMessageService;
        _reminderService = reminderService;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _packNameRepository = packNameRepository;
        _packNameListRepository = packNameListRepository;
        _productGroupPackNameCreditRepository = productGroupPackNameCreditRepository;
        _subsidyRepository = subsidyRepository;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _subsidyPackNameCreditRepository = subsidyPackNameCreditRepository;
        _subsidyReminderRepository = subsidyReminderRepository;
        _subsidyVariationRepository = subsidyVariationRepository;
        _settingService = settingService;
        _storeService = storeService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The lead time in hours of each follow-up the store sends, by number; a follow-up whose template is inactive or
    /// has no lead time is left out
    /// </summary>
    /// <remarks>
    /// The first template of each name, the one nop's Reminders page reads and writes.
    /// </remarks>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the lead times</returns>
    protected virtual async Task<Dictionary<int, int>> GetFollowUpLeadHoursAsync(int storeId)
    {
        var leadHours = new Dictionary<int, int>();
        var templateNames = SubsidyReminderDefaults.ExpiryFollowUpTemplates;

        for (var index = 0; index < templateNames.Length; index++)
        {
            var template = (await _messageTemplateService.GetMessageTemplatesByNameAsync(templateNames[index], storeId)).FirstOrDefault();

            if (template is not { IsActive: true, DelayBeforeSend: not null })
                continue;

            var hours = SubsidyExpiryReminderHelper.LeadHours(template.DelayBeforeSend.Value, template.DelayPeriodId);

            if (hours > 0)
                leadHours[index + 1] = hours;
        }

        return leadHours;
    }

    /// <summary>
    /// The store's time zone, the one its subsidy periods are expressed in: the store's own setting, then the
    /// instance's, then UTC
    /// </summary>
    /// <remarks>
    /// UTC when no zone is set anywhere, as the SQL views fall back (<c>vw_CORE_SUB_StoreSubsidyVariationSetting</c>), so
    /// the season end worked out here is the one the credit views use. A zone this server does not know falls back to the
    /// instance zone, as nop's own helper does.
    /// </remarks>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the time zone</returns>
    protected virtual async Task<TimeZoneInfo> GetStoreTimeZoneAsync(int storeId)
    {
        var dateTimeSettings = await _settingService.LoadSettingAsync<DateTimeSettings>(storeId);

        if (string.IsNullOrWhiteSpace(dateTimeSettings.DefaultStoreTimeZoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(dateTimeSettings.DefaultStoreTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return _dateTimeHelper.DefaultStoreTimeZone;
        }
        catch (InvalidTimeZoneException)
        {
            return _dateTimeHelper.DefaultStoreTimeZone;
        }
    }

    /// <summary>
    /// Every live assignment of the store whose packname has its own active customer account with an e-mail address
    /// and can order (its list on a live branch, as the packname cart requires), with the dates its season is worked
    /// out from
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the assignments</returns>
    protected virtual async Task<IList<SubsidyExpiryReminderItem>> GetCandidateAssignmentsAsync(int storeId)
    {
        var query = from sp in _subsidyPackNameRepository.Table
                    join s in _subsidyRepository.Table on sp.SubsidyId equals s.Id
                    join pn in _packNameRepository.Table on sp.PackNameId equals pn.Id
                    join pnl in _packNameListRepository.Table on pn.PackNameListId equals pnl.Id
                    join b in _branchRepository.Table on pnl.BranchId equals (int?)b.Id
                    join c in _customerRepository.Table on pn.CustomerId equals (int?)c.Id
                    join v in _subsidyVariationRepository.Table.Where(variation => !variation.Deleted) on sp.Id equals v.SubsidyPackNameId into variations
                    from v in variations.DefaultIfEmpty()
                    where !sp.Deleted && sp.Active
                        && !s.Deleted && s.Active && s.StoreId == storeId
                        && !pn.Deleted && pn.Active && !pnl.Deleted && !b.Deleted
                        && !c.Deleted && c.Active && c.Email != null && c.Email != string.Empty
                    select new SubsidyExpiryReminderItem
                    {
                        SubsidyPackNameId = sp.Id,
                        PackNameId = pn.Id,
                        PackName = pn.Name,
                        CustomerId = c.Id,
                        SubsidyId = s.Id,
                        Subsidy = s.Name,
                        DurationStart = s.DurationStart,
                        DurationEnd = s.DurationEnd,
                        VariationDurationStart = v != null ? v.DurationStart : null,
                        VariationDurationEnd = v != null ? v.DurationEnd : null,
                        AnnualRecurrence = s.AnnualRecurrence
                    };

        return await query.ToListAsync();
    }

    /// <summary>
    /// Sends the follow-ups due to one batch of customers of a store and records them
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="items">The customers' seasons inside the reminder window</param>
    /// <param name="leadHours">The store's follow-up lead times, by number</param>
    /// <param name="startDateUtc">The moment the store's reminders start from; null for no limit</param>
    /// <param name="nowUtc">Now</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task SendBatchAsync(int storeId, IList<SubsidyExpiryReminderItem> items, IReadOnlyDictionary<int, int> leadHours, DateTime? startDateUtc, DateTime nowUtc)
    {
        var assignmentIds = items.Select(item => item.SubsidyPackNameId).Distinct().ToList();

        var records = await _subsidyReminderRepository.Table
            .Where(record => assignmentIds.Contains(record.SubsidyPackNameId))
            .ToListAsync();

        var due = new List<SubsidyExpiryReminderItem>();

        foreach (var item in items)
        {
            var lastFollowUpNumber = records
                .FirstOrDefault(record => record.SubsidyPackNameId == item.SubsidyPackNameId && record.PeriodEnd.Date == item.PeriodEnd.Date)
                ?.LastFollowUpNumber ?? 0;

            var followUpNumber = SubsidyExpiryReminderHelper.GetDueFollowUp(item.PeriodEndUtc, nowUtc, leadHours, lastFollowUpNumber, startDateUtc);

            if (followUpNumber is null)
                continue;

            item.FollowUpNumber = followUpNumber.Value;
            due.Add(item);
        }

        if (due.Count == 0)
            return;

        var dueIds = due.Select(item => item.SubsidyPackNameId).Distinct().ToList();

        //what is left to spend, as the credit views count it; a view has no row for a season that is not open
        var subsidyCredits = await _subsidyPackNameCreditRepository.Table
            .Where(credit => dueIds.Contains(credit.Id))
            .ToListAsync();
        var productGroupCredits = await _productGroupPackNameCreditRepository.Table
            .Where(credit => dueIds.Contains(credit.Id))
            .ToListAsync();

        var toSend = new List<SubsidyExpiryReminderItem>();

        foreach (var item in due)
        {
            var subsidyCredit = subsidyCredits.FirstOrDefault(credit => credit.Id == item.SubsidyPackNameId);

            if (subsidyCredit is null)
                continue;

            var (remainingCredit, remainingUnits) = SubsidyExpiryReminderHelper.RemainingToSpend(subsidyCredit,
                productGroupCredits.Where(credit => credit.Id == item.SubsidyPackNameId));

            if (remainingCredit <= decimal.Zero && remainingUnits <= 0)
                continue;

            item.RemainingCredit = remainingCredit;
            item.RemainingUnits = remainingUnits;
            toSend.Add(item);
        }

        foreach (var customerItems in toSend.GroupBy(item => item.CustomerId))
        {
            var customer = await _customerService.GetCustomerByIdAsync(customerItems.Key);

            if (customer is null)
                continue;

            var reminderItems = customerItems
                .OrderBy(item => item.PeriodEnd)
                .ThenBy(item => item.PackName)
                .ThenBy(item => item.Subsidy)
                .ToList();

            //the latest follow-up among the customer's seasons decides the wording; each season is listed with its own date
            var templateName = SubsidyReminderDefaults.ExpiryFollowUpTemplates[reminderItems.Max(item => item.FollowUpNumber) - 1];

            IList<int> queuedEmailIds;

            try
            {
                queuedEmailIds = await _packNameWorkflowMessageService.SendSubsidyExpiryReminderAsync(customer, templateName, reminderItems, storeId);
            }
            catch (Exception exception)
            {
                //one customer's failure must not hold back the others; the next run tries again
                await _logger.ErrorAsync($"The subsidy expiry reminder to customer {customer.Id} could not be queued", exception, customer);
                continue;
            }

            if (queuedEmailIds.Count == 0)
                continue;

            foreach (var item in reminderItems)
            {
                var record = records.FirstOrDefault(sent => sent.SubsidyPackNameId == item.SubsidyPackNameId && sent.PeriodEnd.Date == item.PeriodEnd.Date);

                if (record is null)
                {
                    record = new SubsidyReminder
                    {
                        SubsidyPackNameId = item.SubsidyPackNameId,
                        PeriodEnd = item.PeriodEnd.Date,
                        LastFollowUpNumber = item.FollowUpNumber,
                        LastFollowUpOnUtc = nowUtc
                    };

                    await _subsidyReminderRepository.InsertAsync(record, false);
                    records.Add(record);
                }
                else
                {
                    record.LastFollowUpNumber = item.FollowUpNumber;
                    record.LastFollowUpOnUtc = nowUtc;

                    await _subsidyReminderRepository.UpdateAsync(record, false);
                }
            }
        }
    }

    /// <summary>
    /// Sends the follow-ups due now in one store
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="nowUtc">Now</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task SendStoreExpiryRemindersAsync(int storeId, DateTime nowUtc)
    {
        //a store with subsidies off sends no subsidy e-mail
        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, storeId))
            return;

        var reminderSettings = await _settingService.LoadSettingAsync<SubsidyReminderSettings>(storeId);

        if (!reminderSettings.ExpiryRemindersEnabled)
            return;

        var leadHours = await GetFollowUpLeadHoursAsync(storeId);

        if (leadHours.Count == 0)
            return;

        var maximumLeadHours = leadHours.Values.Max();
        var storeTimeZone = await GetStoreTimeZoneAsync(storeId);
        var today = SubsidyPeriodHelper.ToStoreLocal(nowUtc, storeTimeZone).Date;
        var packNamesOwnStartAndFinishDates = (await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId)).PackNamesOwnStartAndFinishDates;

        var inWindow = new List<SubsidyExpiryReminderItem>();

        foreach (var item in await GetCandidateAssignmentsAsync(storeId))
        {
            var durationStart = SubsidyPeriodHelper.EffectiveDurationStart(item.DurationStart, item.VariationDurationStart, packNamesOwnStartAndFinishDates);
            var durationEnd = SubsidyPeriodHelper.EffectiveDurationEnd(item.DurationEnd, item.VariationDurationEnd, packNamesOwnStartAndFinishDates);

            if (!SubsidyExpiryReminderHelper.TryGetOpenPeriodEnd(durationStart, durationEnd, item.AnnualRecurrence, today, out var periodEnd))
                continue;

            var periodEndUtc = SubsidyPeriodHelper.ToUtc(SubsidyPeriodHelper.EndOfLocalDay(periodEnd), storeTimeZone);

            //not ended yet, and inside the longest lead time
            if (periodEndUtc <= nowUtc || periodEndUtc.AddHours(-maximumLeadHours) > nowUtc)
                continue;

            item.PeriodEnd = periodEnd.Date;
            item.PeriodEndUtc = periodEndUtc;
            item.DaysLeft = SubsidyExpiryReminderHelper.DaysLeft(periodEnd, today);
            inWindow.Add(item);
        }

        //batched by customer, so a customer's seasons always travel in the same e-mail
        foreach (var customers in inWindow.GroupBy(item => item.CustomerId).Chunk(SubsidyReminderDefaults.ProcessingBatchSize))
            await SendBatchAsync(storeId, customers.SelectMany(customer => customer).ToList(), leadHours, reminderSettings.ExpiryRemindersStartDateUtc, nowUtc);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Sends the follow-ups due now, in every store with subsidies and the reminders switched on: one e-mail per
    /// customer, listing each of their packname's subsidies whose season is ending with something left to spend
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task SendExpiryRemindersAsync()
    {
        var nowUtc = DateTime.UtcNow;

        foreach (var store in await _storeService.GetAllStoresAsync())
        {
            try
            {
                await SendStoreExpiryRemindersAsync(store.Id, nowUtc);
            }
            catch (Exception exception)
            {
                //one store's failure must not hold back the others; the next run tries again
                await _logger.ErrorAsync($"The subsidy expiry reminders of store {store.Id} could not be sent", exception);
            }
        }
    }

    /// <summary>
    /// Saves the subsidy card of Promotions › Reminders: the switch for the store scope, and when the switch is on, each
    /// follow-up on its message template
    /// </summary>
    /// <remarks>
    /// As nop saves its own cards: the follow-ups are written only while the switch is on, through nop's
    /// <see cref="IReminderService.UpdateFollowUpAsync"/>, which keeps the lead time of a follow-up switched off. A
    /// switch saved on without a start date starts from the moment of the save, so no backlog is sent.
    /// </remarks>
    /// <param name="storeId">The store scope the admin is editing; 0 for all stores</param>
    /// <param name="enabled">Whether the reminders are sent</param>
    /// <param name="overrideForStore">Whether the store keeps its own value of the switch (store scope only)</param>
    /// <param name="startDateUtc">The moment the reminders start from; null to start from the save when switched on</param>
    /// <param name="startDateOverrideForStore">Whether the store keeps its own start date (store scope only)</param>
    /// <param name="followUps">The follow-ups in follow-up order; any beyond the third are ignored</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task SaveExpiryReminderSettingsAsync(int storeId, bool enabled, bool overrideForStore, DateTime? startDateUtc, bool startDateOverrideForStore,
        IList<SubsidyExpiryFollowUpSetting> followUps)
    {
        ArgumentNullException.ThrowIfNull(followUps);

        var reminderSettings = await _settingService.LoadSettingAsync<SubsidyReminderSettings>(storeId);
        reminderSettings.ExpiryRemindersEnabled = enabled;
        reminderSettings.ExpiryRemindersStartDateUtc = startDateUtc ?? (enabled ? DateTime.UtcNow : (DateTime?)null);

        await _settingService.SaveSettingOverridablePerStoreAsync(reminderSettings, settings => settings.ExpiryRemindersEnabled, overrideForStore, storeId, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(reminderSettings, settings => settings.ExpiryRemindersStartDateUtc, startDateOverrideForStore, storeId, false);

        if (enabled)
        {
            var templateNames = SubsidyReminderDefaults.ExpiryFollowUpTemplates;

            for (var index = 0; index < Math.Min(templateNames.Length, followUps.Count); index++)
            {
                var followUp = followUps[index];

                await _reminderService.UpdateFollowUpAsync(templateNames[index], storeId, followUp.Enabled, followUp.DelayBeforeSend, followUp.DelayPeriod);
            }
        }

        await _settingService.ClearCacheAsync();
    }

    #endregion
}
```

### 65. WHOLE FILE — `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Models/Subsidies/SubsidyExpiryRemindersModel.cs`

The start date field and its store override. (48 lines)

```csharp
using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

/// <summary>
/// The subsidy card of Promotions › Reminders
/// </summary>
public partial record SubsidyExpiryRemindersModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets the store scope the card was prepared for; posted back, it also tells the save filter the card was on
    /// the page
    /// </summary>
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Enabled")]
    public bool Enabled { get; set; }

    public bool Enabled_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.StartDate")]
    [UIHint("DateTimeNullable")]
    public DateTime? StartDateUtc { get; set; }

    public bool StartDateUtc_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets the three follow-ups, in follow-up order
    /// </summary>
    public List<SubsidyExpiryFollowUpModel> FollowUps { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the task that sends the reminders is installed
    /// </summary>
    public bool TaskFound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the task that sends the reminders is enabled
    /// </summary>
    public bool TaskEnabled { get; set; }

    /// <summary>
    /// Gets or sets the task's name on System › Schedule tasks
    /// </summary>
    public string TaskName { get; set; }
}
```

### 66. WHOLE FILE — `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Factories/SubsidyReminderModelFactory.cs`

Prepares the start date. (93 lines)

```csharp
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// The subsidy card of Promotions › Reminders
/// </summary>
public class SubsidyReminderModelFactory : ISubsidyReminderModelFactory
{
    #region Fields

    protected readonly IMessageTemplateService _messageTemplateService;
    protected readonly IScheduleTaskService _scheduleTaskService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public SubsidyReminderModelFactory(IMessageTemplateService messageTemplateService,
        IScheduleTaskService scheduleTaskService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _messageTemplateService = messageTemplateService;
        _scheduleTaskService = scheduleTaskService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepares the card for the store scope the admin is editing
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the card's model
    /// </returns>
    public virtual async Task<SubsidyExpiryRemindersModel> PrepareSubsidyExpiryRemindersModelAsync()
    {
        var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var reminderSettings = await _settingService.LoadSettingAsync<SubsidyReminderSettings>(storeId);

        var model = new SubsidyExpiryRemindersModel
        {
            ActiveStoreScopeConfiguration = storeId,
            Enabled = reminderSettings.ExpiryRemindersEnabled,
            StartDateUtc = reminderSettings.ExpiryRemindersStartDateUtc
        };

        if (storeId > 0)
        {
            model.Enabled_OverrideForStore = await _settingService.SettingExistsAsync(reminderSettings, settings => settings.ExpiryRemindersEnabled, storeId);
            model.StartDateUtc_OverrideForStore = await _settingService.SettingExistsAsync(reminderSettings, settings => settings.ExpiryRemindersStartDateUtc, storeId);
        }

        var templateNames = SubsidyReminderDefaults.ExpiryFollowUpTemplates;

        for (var index = 0; index < templateNames.Length; index++)
        {
            //the first template of the name, the one nop's reminders read and write
            var template = (await _messageTemplateService.GetMessageTemplatesByNameAsync(templateNames[index], storeId)).FirstOrDefault();

            model.FollowUps.Add(new SubsidyExpiryFollowUpModel
            {
                Number = index + 1,
                Enabled = template?.IsActive ?? false,
                DelayBeforeSend = template?.DelayBeforeSend ?? 0,
                DelayPeriodId = template?.DelayPeriodId ?? 0
            });
        }

        var task = await _scheduleTaskService.GetTaskByTypeAsync(SubsidyReminderDefaults.ExpiryTaskTypeFullName);

        model.TaskFound = task != null;
        model.TaskEnabled = task?.Enabled ?? false;
        model.TaskName = task?.Name ?? SubsidyReminderDefaults.ExpiryTaskName;

        return model;
    }

    #endregion
}
```

### 67. WHOLE FILE — `Plugins/Nop.Plugin.CW.Core/Filters/SubsidyRemindersSaveFilter.cs`

Passes the start date to the save. (128 lines)

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Controllers;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Validates and saves the subsidy card of Promotions › Reminders when nop's Save posts the page.
/// </summary>
/// <remarks>
/// <para>
/// The card is drawn inside nop's form (<c>SubsidyExpiryRemindersViewComponent</c>), so it is posted to nop's
/// <c>ReminderController.Index</c>, whose model knows nothing of it. Before the action runs, the card's fields
/// (<c>SubsidyExpiry.*</c>) are bound and validated, and an error goes into the page's ModelState: nop then saves
/// nothing and shows the page again with the error under the field. After the action, when the form was valid, the
/// card is saved. A save is all or nothing, nop's cards and this one together.
/// </para>
/// <para>
/// Only a post that carries the card's store-scope field is touched: a page drawn without the card (subsidies off)
/// saves nothing of it, so its switch is never turned off by a form that did not show it.
/// </para>
/// <para>
/// <b>Matched on nop's controller type and method name.</b> The method name is pinned to nop by
/// <c>SubsidyRemindersSaveFilterTests</c>.
/// </para>
/// </remarks>
public class SubsidyRemindersSaveFilter : IAsyncActionFilter
{
    #region Fields

    /// <summary>
    /// The prefix of the card's fields
    /// </summary>
    public const string FormPrefix = nameof(CWRemindersModel.SubsidyExpiry);

    /// <summary>
    /// The field every post of the card carries
    /// </summary>
    public const string CardField = FormPrefix + "." + nameof(SubsidyExpiryRemindersModel.ActiveStoreScopeConfiguration);

    private const string IndexMethod = nameof(ReminderController.Index);

    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly ISubsidyReminderService _subsidyReminderService;

    #endregion

    #region Ctor

    public SubsidyRemindersSaveFilter(ILocalizationService localizationService,
        IStoreContext storeContext,
        ISubsidyReminderService subsidyReminderService)
    {
        _localizationService = localizationService;
        _storeContext = storeContext;
        _subsidyReminderService = subsidyReminderService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Whether the request is nop's save of the Reminders page with the card on it
    /// </summary>
    /// <param name="context">The action context</param>
    protected virtual bool IsCardSave(ActionExecutingContext context)
    {
        if (context.Controller is not ReminderController
            || context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor
            || actionDescriptor.MethodInfo.Name != IndexMethod)
            return false;

        var request = context.HttpContext.Request;

        return HttpMethods.IsPost(request.Method) && request.HasFormContentType && request.Form.ContainsKey(CardField);
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsCardSave(context))
        {
            await next();
            return;
        }

        var model = new SubsidyExpiryRemindersModel();
        await ((ControllerBase)context.Controller).TryUpdateModelAsync(model, FormPrefix);

        var validationResult = await new SubsidyExpiryRemindersValidator(_localizationService).ValidateAsync(model);

        foreach (var error in validationResult.Errors)
            context.ModelState.AddModelError($"{FormPrefix}.{error.PropertyName}", error.ErrorMessage);

        //nop saves its own cards only when the whole form is valid; the card follows the same answer
        var saveCard = context.ModelState.IsValid;

        var executedContext = await next();

        //a later filter that answered instead of the action means nop saved nothing; neither does the card
        if (!saveCard || executedContext.Canceled || executedContext.Exception != null)
            return;

        var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();

        await _subsidyReminderService.SaveExpiryReminderSettingsAsync(storeId, model.Enabled, model.Enabled_OverrideForStore,
            model.StartDateUtc, model.StartDateUtc_OverrideForStore,
            model.FollowUps
                .Select(followUp => new SubsidyExpiryFollowUpSetting(followUp.Enabled, followUp.DelayBeforeSend, (MessageDelayPeriod)followUp.DelayPeriodId))
                .ToList());
    }

    #endregion
}
```

### 68. WHOLE FILE — `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Views/Shared/Components/SubsidyExpiryReminders/Default.cshtml`

The start date row, first inside the switch's nested block. (87 lines)

```cshtml
@model CWRemindersModel

@using Nop.Core.Domain.Messages
@using Nop.Services

@{
    NopHtml.AddScriptParts(ResourceLocation.Footer, "~/Plugins/CW.Core/Content/Admin/Reminders/admin.subsidy.reminders.js");
}

<div class="card card-default" id="cw-subsidy-expiry-reminders">
    <div class="card-body">
        <input asp-for="SubsidyExpiry.ActiveStoreScopeConfiguration" type="hidden" />
        <div class="callout callout-info">
            @T("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Info")
        </div>
        @if (!Model.SubsidyExpiry.TaskFound)
        {
            <div class="callout callout-warning">
                @T("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Warning.TaskNotFound")
            </div>
        }
        else if (!Model.SubsidyExpiry.TaskEnabled)
        {
            <div class="callout callout-warning">
                @T("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Warning.TaskDisabled", Model.SubsidyExpiry.TaskName, Url.Action("List", "ScheduleTask", new { area = AreaNames.ADMIN }))
            </div>
        }
        <div class="form-group row">
            <div class="col-md-3">
                <nop-override-store-checkbox asp-for="SubsidyExpiry.Enabled_OverrideForStore" asp-input="SubsidyExpiry.Enabled" asp-store-scope="@Model.SubsidyExpiry.ActiveStoreScopeConfiguration" />
                <nop-label asp-for="SubsidyExpiry.Enabled" />
            </div>
            <div class="col-md-9">
                <nop-editor asp-for="SubsidyExpiry.Enabled" />
                <span asp-validation-for="SubsidyExpiry.Enabled"></span>
            </div>
        </div>
        <nop-nested-setting asp-for="SubsidyExpiry.Enabled">
            <div class="form-group row">
                <div class="col-md-3">
                    <nop-override-store-checkbox asp-for="SubsidyExpiry.StartDateUtc_OverrideForStore" asp-input="SubsidyExpiry.StartDateUtc" asp-store-scope="@Model.SubsidyExpiry.ActiveStoreScopeConfiguration" />
                    <nop-label asp-for="SubsidyExpiry.StartDateUtc" />
                </div>
                <div class="col-md-9">
                    <nop-editor asp-for="SubsidyExpiry.StartDateUtc" />
                    <span asp-validation-for="SubsidyExpiry.StartDateUtc"></span>
                </div>
            </div>
            @for (var i = 0; i < Model.SubsidyExpiry.FollowUps.Count; i++)
            {
                var blockId = $"{Html.IdFor(m => m.SubsidyExpiry.FollowUps[i].Enabled)}-block";
                <div class="form-group row">
                    <div class="col-md-3">
                        <nop-label asp-for="SubsidyExpiry.FollowUps[i].Enabled" asp-resource-params="@(new string[] { Model.SubsidyExpiry.FollowUps[i].Number.ToString() })" />
                    </div>
                    <div class="col-md-9">
                        <div class="row">
                            <div class="col-sm-auto">
                                <input asp-for="SubsidyExpiry.FollowUps[i].Enabled" class="cw-subsidy-followup-enable" data-followup-block="@blockId" />
                            </div>
                            <div class="col-sm-8" id="@blockId">
                                <div class="row">
                                    <div class="col-3">
                                        <nop-editor asp-for="SubsidyExpiry.FollowUps[i].DelayBeforeSend" />
                                    </div>
                                    <div class="col-auto">
                                        <nop-select asp-for="SubsidyExpiry.FollowUps[i].DelayPeriodId" asp-items="@(await ((MessageDelayPeriod)Model.SubsidyExpiry.FollowUps[i].DelayPeriodId).ToSelectListAsync())" />
                                    </div>
                                    <div class="col-auto label-wrapper">
                                        <div title="@T("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend.Hint")" data-toggle="tooltip" class="ico-help"><i class="fas fa-circle-question"></i></div>
                                    </div>
                                </div>
                                <span asp-validation-for="SubsidyExpiry.FollowUps[i].DelayBeforeSend"></span>
                                <span asp-validation-for="SubsidyExpiry.FollowUps[i].DelayPeriodId"></span>
                            </div>
                        </div>
                    </div>
                </div>
            }
            <div class="form-group row">
                <div class="col-md-9 offset-md-3">
                    <span asp-validation-for="SubsidyExpiry.FollowUps"></span>
                </div>
            </div>
        </nop-nested-setting>
    </div>
</div>
```

### 69. REPLACE — `Plugins/Nop.Plugin.CW.Core/CWCorePlugin.cs`, line 1838

The card's info text mentions the start date, and the start date's label and hint follow it. 1 line becomes 3.

FIND (line 1838):
```csharp
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Info"] = "Subsidy expiry reminders go to the customer account of each packname before a subsidy's ordering period ends. A packname without its own account is not reminded. A reminder is sent only while something is left to spend in the period (subsidy credit or items), and a customer with several subsidies ending gets one e-mail listing them all. Subsidies that allow ordering outside the period are reminded too: after the end the customer may still order, but the subsidy no longer pays.",
```

REPLACE WITH:
```csharp
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Info"] = "Subsidy expiry reminders go to the customer account of each packname before a subsidy's ordering period ends. A packname without its own account is not reminded. A reminder is sent only while something is left to spend in the period (subsidy credit or items), and a customer with several subsidies ending gets one e-mail listing them all. Subsidies that allow ordering outside the period are reminded too: after the end the customer may still order, but the subsidy no longer pays. A follow up that fell due before the start date is not sent, so switching the reminders on does not send a backlog.",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.StartDate"] = "Start date",
            ["Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.StartDate.Hint"] = "The moment the reminders start from, in Coordinated Universal Time (UTC). A follow up that fell due before it is not sent. Leave it empty to start from the moment you switch the reminders on and save.",
```

### 70. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`, line 177

Days left now counts today, so the last day is 1. 1 line stays 1.

FIND (line 177):
```csharp
                item.DaysLeft > 0 ? string.Format(daysLeftFormat, item.DaysLeft) : WebUtility.HtmlEncode(lastDay)
```

REPLACE WITH:
```csharp
                item.DaysLeft > 1 ? string.Format(daysLeftFormat, item.DaysLeft) : WebUtility.HtmlEncode(lastDay)
```

### 71. REPLACE — `Plugins/Nop.Plugin.CW.Core/Domain/Subsidies/DTOs/SubsidyExpiryReminderItem.cs`, line 74

The comment of the changed figure. 1 line stays 1.

FIND (line 74):
```csharp
    /// Gets or sets the days from the store's today to the last day (0 on the last day)
```

REPLACE WITH:
```csharp
    /// Gets or sets the days left in the season, today included (1 on the last day)
```

**Test plan changes.** T10: the 5-day season is listed as "6 day(s) left" (today included). New **T20:** switch the card
off, clear the start date, switch on and save: the date fills with now; a season ending in 5 days gets nothing until the
day before its end, then #3. **T21:** set the start date 40 days back and run: the same season gets #2 at once (the
latest follow-up due; #3 falls due the day before the end), as before this change.

## 11. The start date on save (2026-11-09)

Found validating changes 61–71. Two cases sent a backlog anyway:

- **Switching back on.** Once a start date was saved, switching the reminders off and back on months later kept the old
  date, so every follow-up that fell due while they were off was sent at once.
- **Switching on at a store scope** with the start date not overridden: its field is disabled and not posted, the date
  filled in was saved with "override" off, so nop deleted it and the store read the all-stores date, empty or old.

The rule now (`SubsidyExpiryReminderHelper.StartsAfresh`, unit-tested): the start date becomes the moment of the save
when the reminders are **switched on** (unless a new date was typed with it), or when they are on and the date is
**emptied** (unless a store scope inherits the all-stores date). The new date is kept at the scope being edited.

Two changes in two files; no other change depends on them.

### 72. REPLACE — `Plugins/Nop.Plugin.CW.Core/Helpers/SubsidyExpiryReminderHelper.cs`, lines 111–114

Adds `StartsAfresh` after `DaysLeft`. 4 lines become 29.

FIND (lines 111–114):
```csharp
    public static int DaysLeft(DateTime periodEnd, DateTime today)
    {
        return (periodEnd.Date - today.Date).Days + 1;
    }
```

REPLACE WITH:
```csharp
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
```

### 73. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Subsidies/SubsidyReminderService.cs`, lines 424–465 (whole method `SaveExpiryReminderSettingsAsync`, with its doc comment)

Applies the rule. 42 lines become 52.

FIND (lines 424–465):
```csharp
    /// <summary>
    /// Saves the subsidy card of Promotions › Reminders: the switch for the store scope, and when the switch is on, each
    /// follow-up on its message template
    /// </summary>
    /// <remarks>
    /// As nop saves its own cards: the follow-ups are written only while the switch is on, through nop's
    /// <see cref="IReminderService.UpdateFollowUpAsync"/>, which keeps the lead time of a follow-up switched off. A
    /// switch saved on without a start date starts from the moment of the save, so no backlog is sent.
    /// </remarks>
    /// <param name="storeId">The store scope the admin is editing; 0 for all stores</param>
    /// <param name="enabled">Whether the reminders are sent</param>
    /// <param name="overrideForStore">Whether the store keeps its own value of the switch (store scope only)</param>
    /// <param name="startDateUtc">The moment the reminders start from; null to start from the save when switched on</param>
    /// <param name="startDateOverrideForStore">Whether the store keeps its own start date (store scope only)</param>
    /// <param name="followUps">The follow-ups in follow-up order; any beyond the third are ignored</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task SaveExpiryReminderSettingsAsync(int storeId, bool enabled, bool overrideForStore, DateTime? startDateUtc, bool startDateOverrideForStore,
        IList<SubsidyExpiryFollowUpSetting> followUps)
    {
        ArgumentNullException.ThrowIfNull(followUps);

        var reminderSettings = await _settingService.LoadSettingAsync<SubsidyReminderSettings>(storeId);
        reminderSettings.ExpiryRemindersEnabled = enabled;
        reminderSettings.ExpiryRemindersStartDateUtc = startDateUtc ?? (enabled ? DateTime.UtcNow : (DateTime?)null);

        await _settingService.SaveSettingOverridablePerStoreAsync(reminderSettings, settings => settings.ExpiryRemindersEnabled, overrideForStore, storeId, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(reminderSettings, settings => settings.ExpiryRemindersStartDateUtc, startDateOverrideForStore, storeId, false);

        if (enabled)
        {
            var templateNames = SubsidyReminderDefaults.ExpiryFollowUpTemplates;

            for (var index = 0; index < Math.Min(templateNames.Length, followUps.Count); index++)
            {
                var followUp = followUps[index];

                await _reminderService.UpdateFollowUpAsync(templateNames[index], storeId, followUp.Enabled, followUp.DelayBeforeSend, followUp.DelayPeriod);
            }
        }

        await _settingService.ClearCacheAsync();
    }
```

REPLACE WITH:
```csharp
    /// <summary>
    /// Saves the subsidy card of Promotions › Reminders: the switch for the store scope, and when the switch is on, each
    /// follow-up on its message template
    /// </summary>
    /// <remarks>
    /// As nop saves its own cards: the follow-ups are written only while the switch is on, through nop's
    /// <see cref="IReminderService.UpdateFollowUpAsync"/>, which keeps the lead time of a follow-up switched off.
    /// When the save starts the reminders afresh (<see cref="SubsidyExpiryReminderHelper.StartsAfresh"/>) the start date
    /// becomes the moment of the save, kept at the scope being edited, so no backlog is sent.
    /// </remarks>
    /// <param name="storeId">The store scope the admin is editing; 0 for all stores</param>
    /// <param name="enabled">Whether the reminders are sent</param>
    /// <param name="overrideForStore">Whether the store keeps its own value of the switch (store scope only)</param>
    /// <param name="startDateUtc">The moment the reminders start from; null to start from the save when switched on</param>
    /// <param name="startDateOverrideForStore">Whether the store keeps its own start date (store scope only)</param>
    /// <param name="followUps">The follow-ups in follow-up order; any beyond the third are ignored</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task SaveExpiryReminderSettingsAsync(int storeId, bool enabled, bool overrideForStore, DateTime? startDateUtc, bool startDateOverrideForStore,
        IList<SubsidyExpiryFollowUpSetting> followUps)
    {
        ArgumentNullException.ThrowIfNull(followUps);

        //what the scope reads before this save, the all-stores values included where the store has none of its own
        var reminderSettings = await _settingService.LoadSettingAsync<SubsidyReminderSettings>(storeId);

        if (SubsidyExpiryReminderHelper.StartsAfresh(enabled, reminderSettings.ExpiryRemindersEnabled, startDateUtc,
            reminderSettings.ExpiryRemindersStartDateUtc, inheritsStartDate: storeId > 0 && !startDateOverrideForStore))
        {
            startDateUtc = DateTime.UtcNow;
            startDateOverrideForStore = true;
        }

        reminderSettings.ExpiryRemindersEnabled = enabled;
        reminderSettings.ExpiryRemindersStartDateUtc = startDateUtc;

        await _settingService.SaveSettingOverridablePerStoreAsync(reminderSettings, settings => settings.ExpiryRemindersEnabled, overrideForStore, storeId, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(reminderSettings, settings => settings.ExpiryRemindersStartDateUtc, startDateOverrideForStore, storeId, false);

        if (enabled)
        {
            var templateNames = SubsidyReminderDefaults.ExpiryFollowUpTemplates;

            for (var index = 0; index < Math.Min(templateNames.Length, followUps.Count); index++)
            {
                var followUp = followUps[index];

                await _reminderService.UpdateFollowUpAsync(templateNames[index], storeId, followUp.Enabled, followUp.DelayBeforeSend, followUp.DelayPeriod);
            }
        }

        await _settingService.ClearCacheAsync();
    }
```

**Unit tests** (written in `SubsidyExpiryReminderHelperTests.cs`): eight cases for `StartsAfresh`.

**Test plan additions.** **T22:** switch off, save; switch on without touching the date, save: the date becomes now.
**T23:** at a store scope with the date not overridden, switch the store on (switch overridden), save: the store gets its
own date (now), and its "override" box is ticked on reload. **T24:** at that store, already on, save without changes: the
date stays.

## 12. Improvements from the dev-site run (2026-11-09)

Three things the first site run found (tracker "Subsidy expiry reminders — test run 2026-11-09"). None changes what is
sent or saved; no migration, no new resource, no plugin version bump. Apply bottom to top within each file.

1. **One error per mistake.** A ticked follow-up with 0 showed its own error and the order error. The order check now
   leaves out a follow-up whose lead time already fails its own rules (changes 74, 75).
2. **The card after a successful save.** nop's `Index` POST returns `View(model)`, so the tag helpers draw the card's
   fields from the posted values (ModelState) rather than from what was saved: a start date the save filled in showed
   as the empty field that was posted until the page was reloaded. After a save the card's entries are removed from
   ModelState, so the card is drawn from what was saved (change 76). After a refused save they stay, so the admin sees
   what was typed.
3. **"incl tax" in the e-mails.** `FormatPriceAsync(price, true, currency)` adds nop's tax suffix whenever
   `TaxSettings.DisplayTaxSuffix` is on, and reads the tax display type and language of the *work context* (the
   scheduled task's or the admin's), not the customer's. A credit is an allowance, not a price: both e-mails now format
   it with no suffix, in the message's language (changes 77–80). This also changes the welcome e-mail
   (`%Subsidy.Details%`).

### 74. REPLACE — `Plugins/Nop.Plugin.CW.Core/Areas/Admin/Validators/Subsidies/SubsidyExpiryRemindersValidator.cs`, lines 39–45 (the order rule to the end of the file)

FIND:

```csharp
        RuleFor(x => x.FollowUps)
            .Must(followUps => SubsidyExpiryReminderHelper.LeadTimesDecrease(followUps
                .Select(followUp => (followUp.Enabled, SubsidyExpiryReminderHelper.LeadHours(followUp.DelayBeforeSend, followUp.DelayPeriodId)))))
            .When(x => x.Enabled)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUps.Order"));
    }
}
```

REPLACE WITH:

```csharp
        RuleFor(x => x.FollowUps)
            .Must(followUps => SubsidyExpiryReminderHelper.LeadTimesDecrease(followUps
                .Select(followUp => (followUp.Enabled && HasValidLeadTime(followUp), SubsidyExpiryReminderHelper.LeadHours(followUp.DelayBeforeSend, followUp.DelayPeriodId)))))
            .When(x => x.Enabled)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUps.Order"));
    }

    /// <summary>
    /// Whether a follow-up's lead time passes its own rules. One that does not already has its error under the field and
    /// is left out of the order check, so the page shows one error for it, not two.
    /// </summary>
    /// <param name="followUp">The follow-up</param>
    private static bool HasValidLeadTime(SubsidyExpiryFollowUpModel followUp)
    {
        if (followUp.DelayBeforeSend <= 0 || !Enum.IsDefined(typeof(MessageDelayPeriod), followUp.DelayPeriodId))
            return false;

        return SubsidyExpiryReminderHelper.LeadHours(followUp.DelayBeforeSend, followUp.DelayPeriodId) <= SubsidyExpiryReminderHelper.MaximumLeadHours;
    }
}
```

### 75. INSERT — `Tests/CW.Tests/Core/Validators/SubsidyExpiryRemindersValidatorTests.cs`, after line 71 (after `An_enabled_follow_up_needs_a_lead_time`)

```csharp

    [Test]
    public async Task A_follow_up_without_a_lead_time_shows_one_error_not_two()
    {
        //#2 at 0 would also put #3 (1 day) after it; its own error is enough
        var result = await Validator().ValidateAsync(Card(true, FollowUp(1, true, 30), FollowUp(2, true, 0), FollowUp(3, true, 1)));

        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be("FollowUps[1].DelayBeforeSend");
    }
```

### 76. INSERT — `Plugins/Nop.Plugin.CW.Core/Filters/SubsidyRemindersSaveFilter.cs`, after line 124 (after the `SaveExpiryReminderSettingsAsync(...)` call, before the closing `}` of `OnActionExecutionAsync`)

```csharp

        //nop shows the page again after a save; the card is drawn from what was just saved, not from what was posted, so
        //a start date the save filled in shows instead of the empty field that was posted
        foreach (var key in executedContext.ModelState.Keys.Where(key => key.StartsWith(FormPrefix + ".", StringComparison.Ordinal)).ToList())
            executedContext.ModelState.Remove(key);
```

### 77. REPLACE — `Plugins/Nop.Plugin.CW.Core/Services/Messages/PackNameWorkflowMessageService.cs`, line 181

FIND:

```csharp
                parts.Add(string.Format(creditFormat, WebUtility.HtmlEncode(await _priceFormatter.FormatPriceAsync(item.RemainingCredit, true, primaryCurrency))));
```

REPLACE WITH:

```csharp
                parts.Add(string.Format(creditFormat, WebUtility.HtmlEncode(await _priceFormatter.FormatPriceAsync(item.RemainingCredit, true, primaryCurrency, languageId, true, false))));
```

### 78. REPLACE — same file, line 167

FIND:

```csharp
        //credit is held in the primary store currency, the customer's e-mail states it in that currency
```

REPLACE WITH:

```csharp
        //credit is held in the primary store currency, the customer's e-mail states it in that currency, in the message's
        //language and without nop's tax suffix (an allowance, not a price)
```

### 79. REPLACE — same file, line 143

FIND:

```csharp
                credit > decimal.Zero ? string.Format(creditFormat, WebUtility.HtmlEncode(await _priceFormatter.FormatPriceAsync(credit, true, primaryCurrency))) : null,
```

REPLACE WITH:

```csharp
                credit > decimal.Zero ? string.Format(creditFormat, WebUtility.HtmlEncode(await _priceFormatter.FormatPriceAsync(credit, true, primaryCurrency, languageId, true, false))) : null,
```

### 80. REPLACE — same file, line 127

Same FIND and REPLACE as change 78.

**Test plan additions.** **T25:** tick #2 with 0, save: one error, under #2. **T26:** switch on with the start date
empty, save: the date shows at once, without a reload. **T27:** run the task with tax suffix display on: the e-mail reads
"$100.00 credit". **T28:** register a packname from the storefront with a subsidy: the welcome e-mail reads the same.
