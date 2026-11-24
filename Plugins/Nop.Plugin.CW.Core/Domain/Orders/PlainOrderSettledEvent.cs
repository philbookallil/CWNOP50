using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Domain.Orders;

/// <summary>
/// Published once a plain (non-packname) order is placed and its store credit settled, or its settlement failed: nop's
/// own placement publishes <c>OrderPlacedEvent</c> before CW settles the order, so whatever must read the settled order
/// waits for this
/// </summary>
public class PlainOrderSettledEvent
{
    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="order">The order</param>
    public PlainOrderSettledEvent(Order order)
    {
        Order = order;
    }

    /// <summary>
    /// Gets the order
    /// </summary>
    public Order Order { get; }
}
