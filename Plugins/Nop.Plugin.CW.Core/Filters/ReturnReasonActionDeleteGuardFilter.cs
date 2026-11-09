using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Controllers;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Refuses to delete a return reason or a requested action that a return records.
/// </summary>
/// <remarks>
/// <para>
/// A return line (<c>ReturnRequestItem</c>) and a return case keep the reason and the action by id only. nop deletes
/// the record with no check, so a return that used it would lose its fees (they are looked up through the reason and
/// action rules), its approval and its reports. Any return counts, whatever its status: a finished return is read
/// again by the reports and the case. Staff can rename a reason or an action instead.
/// </para>
/// <para>
/// <b>Matched on nop's controller type and method name.</b> <c>ReturnsController</c> derives from nop's
/// <c>ReturnRequestController</c>, so the same two actions are reachable under both controller names; the type test
/// covers both. Method names are pinned to nop by <c>ReturnReasonActionDeleteGuardFilterTests</c>.
/// </para>
/// <para>
/// <b>No feature-switch check.</b> Reasons and actions are shared by every store, and the returns that use them
/// stay whatever a store's Exchanges switch says.
/// </para>
/// <para>
/// When the delete goes ahead, <see cref="ReturnReasonActionDeletedEventConsumer"/> removes the record's rules and links.
/// </para>
/// </remarks>
public class ReturnReasonActionDeleteGuardFilter : IAsyncActionFilter
{
    #region Fields

    private const string ReasonDeleteMethod = nameof(ReturnRequestController.ReturnRequestReasonDelete);
    private const string ActionDeleteMethod = nameof(ReturnRequestController.ReturnRequestActionDelete);

    protected readonly IExchangeService _exchangeService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;

    #endregion

    #region Ctor

    public ReturnReasonActionDeleteGuardFilter(IExchangeService exchangeService,
        ILocalizationService localizationService,
        INotificationService notificationService)
    {
        _exchangeService = exchangeService;
        _localizationService = localizationService;
        _notificationService = notificationService;
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is not ReturnRequestController
            || context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor
            || actionDescriptor.MethodInfo.Name is not (ReasonDeleteMethod or ActionDeleteMethod))
        {
            await next();
            return;
        }

        //nop binds the record as the route's "id"; without one nop finds no record and deletes nothing
        if (!context.ActionArguments.TryGetValue("id", out var value) || value is not int id || id <= 0)
        {
            await next();
            return;
        }

        var isReason = actionDescriptor.MethodInfo.Name == ReasonDeleteMethod;

        var inUse = isReason
            ? await _exchangeService.IsReturnReasonInUseAsync(id)
            : await _exchangeService.IsReturnActionInUseAsync(id);

        if (!inUse)
        {
            await next();
            return;
        }

        _notificationService.ErrorNotification(await _localizationService.GetResourceAsync(isReason
            ? "Plugins.CW.Core.Admin.Returns.Reason.InUse"
            : "Plugins.CW.Core.Admin.Returns.Action.InUse"));

        //back to the record's own page, where the delete button was
        context.Result = new RedirectToActionResult(isReason ? "ReturnRequestReasonEdit" : "ReturnRequestActionEdit", "ReturnRequest", new { id });
    }

    #endregion
}