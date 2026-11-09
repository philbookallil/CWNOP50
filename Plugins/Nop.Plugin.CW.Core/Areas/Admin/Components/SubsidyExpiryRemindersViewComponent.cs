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