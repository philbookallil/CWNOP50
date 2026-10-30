using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Services.Exchanges;

/// <summary>
/// What is known about the exchange replacement the current logical call is writing.
/// </summary>
/// <param name="ReturnRequestItemId">The exchange line being fulfilled</param>
/// <param name="ReturnedOrderItemId">The order item being returned — what the customer actually paid for</param>
/// <param name="ExchangeType">Exact, Same Style or Different Style</param>
public sealed record ExchangeReplacementContext(int ReturnRequestItemId, int ReturnedOrderItemId, ExchangeType ExchangeType);

/// <summary>
/// Marks the narrow window in which the cart is being written on the customer's behalf to place an
/// exchange replacement, rather than by the customer choosing to buy something.
/// </summary>
/// <remarks>
/// <para>
/// Two things need to know they are inside that window. The catalogue's selling rules — a minimum order
/// quantity, a maximum, a fixed set such as 2, 4, 6 — decide what a customer may <b>buy</b>; a replacement
/// is not a purchase, so returning one of a pack of two has to be allowed even though one could never have
/// been ordered, or the add is refused and the whole authorisation rolls back. And an Exact replacement has
/// to be priced at what the customer paid, which on the auto-placed path cannot be looked up from the cart
/// row because there is no cart row — the items are detached templates with <c>Id == 0</c>.
/// </para>
/// <para>
/// That second job is why the scope carries a payload rather than a bool: it is the only way the price
/// consumer can identify a detached template.
/// </para>
/// <para>
/// <see cref="AsyncLocal{T}"/> rather than a field or a request item because the value has to survive the
/// awaits between the caller and the cart service without being visible to any other request. It is set
/// only inside the approval and auto-place workflows and always cleared by the <c>using</c>, so a
/// customer's own add-to-cart can never see it.
/// </para>
/// </remarks>
public static class ExchangeReplacementScope
{
    private static readonly AsyncLocal<ExchangeReplacementContext> _current = new();

    /// <summary>
    /// The replacement being written, or null when this call is ordinary shopping.
    /// </summary>
    public static ExchangeReplacementContext Current => _current.Value;

    /// <summary>
    /// Whether the current logical call is writing an exchange replacement on the customer's behalf.
    /// </summary>
    public static bool IsActive => _current.Value != null;

    /// <summary>
    /// Opens the scope for one exchange line. Dispose to close it — always with <c>using</c>, so an
    /// exception on the way through cannot leave it open for whatever this thread handles next.
    /// </summary>
    public static IDisposable Begin(ReturnRequestItem returnRequestItem)
    {
        ArgumentNullException.ThrowIfNull(returnRequestItem);

        var previous = _current.Value;

        _current.Value = new ExchangeReplacementContext(
            returnRequestItem.Id,
            returnRequestItem.ReturnedOrderItemId,
            returnRequestItem.ExchangeType);

        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly ExchangeReplacementContext _previous;

        public Scope(ExchangeReplacementContext previous)
        {
            _previous = previous;
        }

        //Restores rather than clears: nesting is not expected today, but restoring costs nothing and means
        //an inner scope can never blind an outer one.
        public void Dispose()
        {
            _current.Value = _previous;
        }
    }
}