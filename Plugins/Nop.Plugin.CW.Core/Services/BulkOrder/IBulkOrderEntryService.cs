using Nop.Core.Domain.Customers;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

/// <summary>
/// The two questions the entry points ask: may this customer use the bulk screen, and should signing in
/// take them straight to it.
/// </summary>
/// <remarks>
/// Two methods rather than one because they are genuinely different questions with one shared half. The
/// header link asks only whether the screen is usable — it must appear whether or not the store sends people
/// there after login. The redirects ask that AND whether the store opted in.
///
/// Defining the second in terms of the first is the point: three call sites (sign-in, Impersonate,
/// PlaceOrder) plus the link must agree about who qualifies, and a link that appears for a customer the
/// redirect would skip — or worse, a redirect to a page that then refuses them — is the failure mode this
/// exists to prevent.
/// </remarks>
public interface IBulkOrderEntryService
{
    /// <summary>
    /// Whether this customer may use the bulk ordering screen in the current store: the feature is on and
    /// the customer holds the access permission. What the header link is gated on.
    /// </summary>
    Task<bool> CanUseBulkOrderingAsync(Customer customer);

    /// <summary>
    /// The route name to send this customer to after they sign in or are impersonated, or <c>null</c> to
    /// leave the destination alone. Null is the instruction to change nothing.
    /// </summary>
    Task<string> ResolveLandingRouteAsync(Customer customer);
}