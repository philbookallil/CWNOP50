using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Plugin.CW.ThemeCore.Services.Preview;

namespace Nop.Plugin.CW.ThemeCore.Infrastructure;

/// <summary>
/// When a storefront request carries a valid live-preview token (<c>?cwpreview=…</c>), relax framing so the
/// store can be embedded in the Brand-Designer iframe — but ONLY by the specific admin origin the token
/// authorises, and ONLY for that request — and flag the request so the head component injects the bridge.
/// Every other request passes through untouched: no global framing change, no core edit.
/// </summary>
public class CwPreviewFramingMiddleware
{
    /// <summary>Request query key carrying the signed preview token.</summary>
    public const string QueryKey = "cwpreview";

    /// <summary><see cref="HttpContext.Items"/> key holding the validated admin origin (read by the head component).</summary>
    public const string AdminOriginItemKey = "CwPreview.AdminOrigin";

    /// <summary><see cref="HttpContext.Items"/> key holding the impersonated preview customer id; present only when
    /// this request really runs as the preview customer (read by <c>CwPreviewCheckoutFilter</c>).</summary>
    public const string PreviewCustomerItemKey = "CwPreview.CustomerId";

    private readonly RequestDelegate _next;

    public CwPreviewFramingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Query[QueryKey].ToString();
        if (!string.IsNullOrEmpty(token))
        {
            var tokenService = context.RequestServices.GetService<ICwPreviewTokenService>();
            var storeContext = context.RequestServices.GetService<IStoreContext>();

            if (tokenService is not null && storeContext is not null)
            {
                var store = await storeContext.GetCurrentStoreAsync();
                if (store is not null
                    && tokenService.TryValidate(token, store.Id, out var payload)
                    && IsSafeOrigin(payload.AdminOrigin))
                {
                    //a preview request is DISPLAY-ONLY: never let it mutate a cart or place an order.
                    //Covers the token-bearing request only — the page's own AJAX/posts carry no token and rely on
                    //the bridge, which stops clicks and form submits in the page; a script that posts on its own
                    //(form.submit(), AJAX) is not stopped — an accepted limitation
                    if (!HttpMethods.IsGet(context.Request.Method))
                    {
                        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                        return; // short-circuit — do not run the endpoint
                    }

                    //let the head component inject the bridge for this request
                    context.Items[AdminOriginItemKey] = payload.AdminOrigin;

                    //allow this store to be framed by the admin origin only, for this response only
                    var adminOrigin = payload.AdminOrigin;
                    context.Response.OnStarting(() =>
                    {
                        var headers = context.Response.Headers;
                        headers.Remove("X-Frame-Options");
                        headers["Content-Security-Policy"] = $"frame-ancestors 'self' {adminOrigin}";
                        return Task.CompletedTask;
                    });

                    //Phase 3: render Cart/Checkout as the store's hidden dummy "preview customer" so those pages
                    //show a populated cart through nop's normal pipeline. Scoped to cart/checkout paths and this
                    //validated request only — the customer cookie is deliberately left untouched (see below).
                    if (IsCartOrCheckout(context.Request.Path))
                    {
                        var previewCustomerService = context.RequestServices.GetService<ICwPreviewCustomerService>();
                        var workContext = context.RequestServices.GetService<IWorkContext>();
                        if (previewCustomerService is not null && workContext is not null)
                        {
                            var previewCustomer = await previewCustomerService.GetOrCreatePreviewCustomerAsync(store.Id);

                            //SetCurrentCustomerAsync also writes nop's guest cookie (.Nop.Customer) with the preview
                            //customer's GUID (WebWorkContext.SetCustomerCookie). Left in place, the browser would carry
                            //that identity beyond this request — after a logout the admin would browse as the preview
                            //customer, and the next login would migrate the dummy cart into their real one. The
                            //impersonation must last for this request only, so undo exactly the Set-Cookie entries
                            //that call added (cookies written earlier in this response are kept).
                            var setCookieBefore = context.Response.Headers.SetCookie;
                            await workContext.SetCurrentCustomerAsync(previewCustomer);
                            context.Response.Headers.SetCookie = setCookieBefore;

                            context.Items[PreviewCustomerItemKey] = previewCustomer.Id;
                        }
                    }
                }
            }
        }

        await _next(context);
    }

    /// <summary>Guard the value going into a response header: a bare http(s) origin, no CR/LF or CSP delimiters.</summary>
    internal static bool IsSafeOrigin(string origin)
        => !string.IsNullOrEmpty(origin)
           && (origin.StartsWith("https://") || origin.StartsWith("http://"))
           && origin.IndexOfAny(new[] { ' ', ';', ',', '\r', '\n' }) < 0
           && Uri.TryCreate(origin, UriKind.Absolute, out _);

    /// <summary>True when the request path is the Cart or one-page Checkout (allowing an optional /{lang} prefix).</summary>
    internal static bool IsCartOrCheckout(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is null)
            return false;

        foreach (var s in segments)
            if (s.Equals("cart", StringComparison.OrdinalIgnoreCase)
                || s.Equals("checkout", StringComparison.OrdinalIgnoreCase)
                || s.Equals("onepagecheckout", StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}
