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

    #endregion
}