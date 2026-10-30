using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Web.Controllers;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Global action filter that keeps a packname customer out of nop's own cart and checkout, and sends a
/// packname order's completed page to the packname one.
/// </summary>
/// <remarks>
/// <para>
/// A packname customer's order has to be placed by <c>PackNameCheckoutController</c>: that is what
/// writes <c>PackNameOrderItem</c>, the per-packname <c>OrderShipping</c> destinations, and the
/// subsidy settlement. An order placed through nop's checkout carries every packname's items as one
/// ordinary cart, with one shipping address, no destinations and <b>no subsidy consumed</b> — and it
/// is invisible to every packname screen and report.
/// </para>
/// <para>
/// <b>The rule is controller-level, not a list of actions.</b> This filter once named
/// two actions — <c>ShoppingCartController.Cart</c> and <c>CheckoutController.Index</c> — and nop
/// 4.90's <c>CheckoutController</c> exposes eighteen more, the whole one-page checkout among them.
/// Enumerating them would leave the next nop patch's new action open by default, which is the wrong
/// default for this. So every action on <c>CheckoutController</c> is intercepted.
/// </para>
/// <para>
/// <b><c>Completed</c> is re-routed, not refused.</b> A payment method that takes the payment in its own
/// pages sends the customer to nop's completed page with the order id, so that route stays live for a
/// packname customer whatever the packname checkout does. When the order it names is a packname order the
/// customer is sent to the packname completed page instead; a plain order's completed page is nop's.
/// </para>
/// <para>
/// <b>GET redirects; POST is refused with 403.</b> The one-page checkout posts by AJAX and reads
/// JSON, so a redirect result is rendered into the page as markup and the customer sees nothing
/// happen at all — a silent failure is worse than the hole. A 403 is visible in the network tab and
/// cannot be mistaken for success (assert on the outcome, not on the result type).
/// </para>
/// <para>
/// This closes the REACHABILITY of nop's checkout. Making a mixed cart place correctly is separate
/// work; this filter makes it place nothing.
/// </para>
/// </remarks>
public class PackNameCheckoutRedirectFilter : CoreActionFilter, IAsyncActionFilter
{
    #region Fields

    protected readonly IWorkContext _workContext;
    protected readonly IPackNameContext _packNameContext;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly ICWOrderService _cwOrderService;

    #endregion

    #region Ctor

    public PackNameCheckoutRedirectFilter(
        IWorkContext workContext,
        IPackNameContext packNameContext,
        ICWFeatureService cwFeatureService,
        ICWOrderService cwOrderService)
    {
        _workContext = workContext;
        _packNameContext = packNameContext;
        _cwFeatureService = cwFeatureService;
        _cwOrderService = cwOrderService;
    }

    #endregion

    #region Utilities

    private bool IsShoppingCartMethod(object controller, ControllerActionDescriptor actionDescriptor)
    {
        //ANY method, not just GET. nop fixes action = "Cart" in the route and eight methods answer to
        //that name: FormValueRequired is an action constraint, so a POST with updatecart= reaches
        //UpdateCart and mutates packname lines through nop's own non-packname path, and a POST with no
        //recognised key reaches the unconstrained Cart() and renders nop's cart view. Adding to cart is
        //unaffected - AddProductToCart_Catalog / _Details / CheckoutAttributeChange have their own
        //action names.
        return IsController<ShoppingCartController>(controller)
                && IsActionName("Cart", actionDescriptor);
    }

    private bool IsCheckoutCompleted(object controller, ControllerActionDescriptor actionDescriptor)
    {
        return IsController<CheckoutController>(controller)
                && IsActionName("Completed", actionDescriptor);
    }

    private bool IsCheckoutMethod(object controller, ControllerActionDescriptor actionDescriptor)
    {
        return IsController<CheckoutController>(controller)
                && !IsCheckoutCompleted(controller, actionDescriptor);
    }

    /// <summary>
    /// The order id nop's completed page was asked for, when the request carries one.
    /// </summary>
    private static int? GetCompletedOrderId(ActionExecutingContext context)
    {
        return context.ActionArguments.TryGetValue("orderId", out var value) && value is int orderId && orderId > 0
            ? orderId
            : null;
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName))
        {
            await next();
            return;
        }

        if (context.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
        {
            var controller = context.Controller;
            var httpContext = context.HttpContext;

            //A packname order's completed page is the packname one, whoever is looking and whatever packname
            //is selected: the order decides, and the packname page checks that it is the customer's own.
            if (IsCheckoutCompleted(controller, controllerActionDescriptor) && IsGet(httpContext))
            {
                var orderId = GetCompletedOrderId(context);

                if (orderId.HasValue && await _cwOrderService.IsPackNameOrderAsync(orderId.Value))
                {
                    context.Result = new RedirectToRouteResult(CWCoreDefaults.PackNameCheckoutCompletedRouteName, new { orderId = orderId.Value });

                    return;
                }

                await next();
                return;
            }

            var isCart = IsShoppingCartMethod(controller, controllerActionDescriptor);
            var isCheckout = IsCheckoutMethod(controller, controllerActionDescriptor);

            if (isCart || isCheckout)
            {
                var packName = await _packNameContext.GetCurrentPackNameAsync(await _workContext.GetCurrentCustomerAsync());

                if (packName != null)
                {
                    //A redirect result posted back to an AJAX caller is rendered as markup and the
                    //customer sees nothing happen, so a POST is refused outright instead.
                    context.Result = IsGet(httpContext)
                        ? new RedirectToRouteResult("PackNameCheckout", null)
                        : new StatusCodeResult(StatusCodes.Status403Forbidden);

                    return;
                }
            }
        }

        await next();
    }

    #endregion
}