using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Marks the window in which a cart line is being checked for a packname other than the one selected.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue a packname customer sees follows the selected packname. A cart, though, can hold lines for
/// several packnames — a manager orders for each of them — and every line is validated again on the cart page
/// and when the order is placed. Checked against the selected packname, a product only another packname's
/// department may see would be refused as unpublished on that packname's own line. Inside this window the role
/// set is the line's packname's instead.
/// </para>
/// <para>
/// It also covers the admin area and other customers than the one signed in, where there is no selected
/// packname at all: an exchange approved in admin writes a replacement line for the order's packname and has to
/// see what that packname sees.
/// </para>
/// <para>
/// <see cref="AsyncLocal{T}"/> rather than a field, because the role set is resolved by two separately
/// registered instances of the customer service and the value has to survive the awaits between the cart
/// service and them without being visible to any other request. It is always closed by a <c>using</c>.
/// </para>
/// <para>
/// <b>Begin it in the method whose callees must see it.</b> An async method runs on a copy of the caller's
/// context, so a window opened inside an awaited helper is undone when that helper returns, and the caller
/// carries on without it. A helper may look the packname up; the caller opens the window.
/// </para>
/// <para>
/// A window opened for an exchange replacement carries its <see cref="ExchangeType"/>, because a replacement is
/// judged differently from a purchase: the same product is always allowed, another one only if allocated, and a
/// closed ordering period never blocks it.
/// </para>
/// </remarks>
public static class PackNameCatalogScope
{
    private sealed record Window(PackName PackName, ExchangeType? Replacement);

    private static readonly AsyncLocal<Window> _current = new();

    /// <summary>
    /// The packname whose catalogue the current logical call is checking, or null outside the window.
    /// </summary>
    public static PackName Current => _current.Value?.PackName;

    /// <summary>
    /// The exchange type when the window is for an exchange replacement; null for a purchase or outside the window.
    /// </summary>
    public static ExchangeType? CurrentReplacement => _current.Value?.Replacement;

    /// <summary>
    /// Opens the window for one packname. Dispose to close it — always with <c>using</c>, so an exception on
    /// the way through cannot leave it open.
    /// </summary>
    /// <param name="packName">The packname whose catalogue to check</param>
    /// <param name="replacement">The exchange type when checking an exchange replacement; null for a purchase</param>
    public static IDisposable Begin(PackName packName, ExchangeType? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(packName);

        var previous = _current.Value;
        _current.Value = new Window(packName, replacement);

        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Window _previous;

        public Scope(Window previous)
        {
            _previous = previous;
        }

        //Restores rather than clears: a line added inside a line's window (a required product) opens its own,
        //and closing it must not blind the outer one.
        public void Dispose()
        {
            _current.Value = _previous;
        }
    }
}