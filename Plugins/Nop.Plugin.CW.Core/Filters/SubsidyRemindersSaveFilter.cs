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

        //nop shows the page again after a save; the card is drawn from what was just saved, not from what was posted, so
        //a start date the save filled in shows instead of the empty field that was posted
        foreach (var key in executedContext.ModelState.Keys.Where(key => key.StartsWith(FormPrefix + ".", StringComparison.Ordinal)).ToList())
            executedContext.ModelState.Remove(key);
    }

    #endregion
}