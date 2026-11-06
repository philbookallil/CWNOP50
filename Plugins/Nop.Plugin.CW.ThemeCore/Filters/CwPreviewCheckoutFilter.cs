using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.ThemeCore.Infrastructure;
using Nop.Services.Orders;
using Nop.Web.Controllers;
using Nop.Web.Factories;

namespace Nop.Plugin.CW.ThemeCore.Filters;

/// <summary>
/// Live-preview Phase 3: guarantees the one-page Checkout renders inside the Brand-Designer preview regardless
/// of the store's checkout settings.
/// <para>
/// The preview customer is a <b>Guests</b>-role system account (so it stays out of the admin customer list).
/// Core <c>CheckoutController.OnePageCheckout</c> bounces (redirect/challenge) for several settings —
/// <c>CheckoutDisabled</c> (→ cart), <c>!OnePageCheckoutEnabled</c> (→ standard checkout),
/// <c>IsGuest &amp;&amp; !AnonymousCheckoutAllowed</c> (→ login). This global filter acts ONLY on that action, and
/// only when the framing middleware actually switched the request to the preview customer; if the real action
/// already produced a <see cref="ViewResult"/> it does nothing, otherwise (it bounced for ANY of the above
/// reasons) it swaps in the checkout view built from the preview customer's cart — <b>no redirect</b> (which
/// would drop the preview token), no core edit, no customer-service override, no CW.Core. Inert for all real
/// traffic and any non-empty-cart failure it doesn't own.
/// </para>
/// </summary>
public class CwPreviewCheckoutFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsPreviewCheckout(context))
        {
            await next();
            return;
        }

        var executed = await next();

        //the action threw — let the exception surface as it would without the preview
        if (executed.Exception is not null)
            return;

        //if the real action already rendered the checkout, leave it alone. Otherwise it bounced via a redirect or
        //challenge — for ANY reason (guest gate, one-page-checkout disabled → redirect to standard, checkout
        //disabled → redirect to cart) — and in the preview we render the one-page checkout view regardless, so the
        //checkout preview is robust to every store setting. (Empty cart is still respected below.)
        if (executed.Result is ViewResult)
            return;

        var services = context.HttpContext.RequestServices;
        var workContext = services.GetRequiredService<IWorkContext>();
        var storeContext = services.GetRequiredService<IStoreContext>();
        var shoppingCartService = services.GetRequiredService<IShoppingCartService>();
        var checkoutModelFactory = services.GetRequiredService<ICheckoutModelFactory>();

        var customer = await workContext.GetCurrentCustomerAsync();       // the impersonated preview customer
        var store = await storeContext.GetCurrentStoreAsync();
        var cart = await shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
        if (!cart.Any())
            return; // empty cart isn't our concern — leave the original result

        //render the one-page checkout view (mirrors CheckoutController.OnePageCheckout minus its gates;
        //re-diff against nop's action on every nopCommerce upgrade)
        var model = await checkoutModelFactory.PrepareOnePageCheckoutModelAsync(cart);
        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        var tempDataFactory = services.GetRequiredService<ITempDataDictionaryFactory>();

        executed.Result = new ViewResult
        {
            ViewName = "OnePageCheckout",
            ViewData = new ViewDataDictionary(metadataProvider, context.ModelState) { Model = model },
            TempData = tempDataFactory.GetTempData(context.HttpContext)
        };
    }

    /// <summary>
    /// The one-page checkout action on a request the framing middleware switched to the preview customer. Keyed on
    /// the impersonation flag, not merely a valid token: if the switch did not happen, the current customer is a
    /// real one and its checkout gates must stand.
    /// </summary>
    private static bool IsPreviewCheckout(ActionExecutingContext context)
        => context.HttpContext.Items.ContainsKey(CwPreviewFramingMiddleware.PreviewCustomerItemKey)
           && context.Controller is CheckoutController
           && string.Equals(context.RouteData.Values["action"] as string, "OnePageCheckout", StringComparison.OrdinalIgnoreCase);
}
