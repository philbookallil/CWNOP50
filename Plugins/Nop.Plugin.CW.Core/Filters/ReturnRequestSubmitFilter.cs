using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core.Http;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Web.Controllers;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Global action filter on nop's own return request post (<c>ReturnRequestController</c>, the POST of
/// <c>/returnrequest/{orderId}</c>)
/// </summary>
/// <remarks>
/// With <i>Exchanges</i> on, a customer's return goes through CW's wizard, which checks it and records its exchange data;
/// nop's post takes a guest's withdrawal link only (the wizard is for the order's own signed-in customer). On every path
/// the quantities are those CW's return page offers, which counts shipped units only: nop's post caps each quantity at
/// the units ordered and not yet requested, shipped or not, so a post above CW's count is refused here rather than
/// reduced by nop.
/// </remarks>
public class ReturnRequestSubmitFilter : IAsyncActionFilter
{
    #region Fields

    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IExchangeService _exchangeService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IOrderService _orderService;

    #endregion

    #region Ctor

    public ReturnRequestSubmitFilter(ICWFeatureService cwFeatureService,
        IExchangeService exchangeService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        IOrderService orderService)
    {
        _cwFeatureService = cwFeatureService;
        _exchangeService = exchangeService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _orderService = orderService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Whether the request is nop's return request post
    /// </summary>
    protected virtual bool IsReturnRequestPost(ActionExecutingContext context) =>
        context.Controller is ReturnRequestController
        && context.ActionDescriptor is ControllerActionDescriptor { ActionName: "ReturnRequest" }
        && HttpMethods.IsPost(context.HttpContext.Request.Method);

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsReturnRequestPost(context)
            || !context.ActionArguments.TryGetValue("orderId", out var value) || value is not int orderId
            || await _orderService.GetOrderByIdAsync(orderId) is not { Deleted: false } order)
        {
            await next();
            return;
        }

        var token = (context.ActionArguments.TryGetValue("model", out var model) ? model as SubmitReturnRequestModel : null)?.WithdrawalToken;
        var backToPage = new RedirectToRouteResult(NopRouteNames.Standard.RETURN_REQUEST,
            string.IsNullOrEmpty(token) ? new { orderId } : new { orderId, token });

        //the wizard is the signed-in customer's way to return
        if (string.IsNullOrEmpty(token) && await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, order.StoreId))
        {
            context.Result = backToPage;
            return;
        }

        var form = context.HttpContext.Request.HasFormContentType ? context.HttpContext.Request.Form : null;
        var returnable = await _exchangeService.GetReturnableQuantitiesAsync(order);
        var tooMany = form?.Keys
            .Where(key => key.StartsWith("quantity", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(key.AsSpan("quantity".Length), out _))
            .Any(key =>
            {
                var orderItemId = int.Parse(key.AsSpan("quantity".Length));
                var quantity = int.TryParse(form[key], out var parsed) ? parsed : 0;

                return quantity < 0 || quantity > 0 && (!returnable.TryGetValue(orderItemId, out var available) || quantity > available);
            }) ?? false;

        if (tooMany)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.Exchanges.Wizard.QuantityNotAvailable"));
            context.Result = backToPage;
            return;
        }

        await next();
    }

    #endregion
}