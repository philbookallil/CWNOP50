namespace Nop.Plugin.CW.Core.Domain.Orders.DTOs;

/// <summary>
/// A distinct shipping destination on a packname order: one address + method, and every packname and
/// order item that shares it.
/// </summary>
/// <remarks>
/// nop's Shipment is order-scoped and holds no address — it assumes one destination per order. A
/// packname order has several, so this is the unit shipments are actually organised by: one shipment,
/// one destination, one tracking number that means something.
/// </remarks>
public class OrderShippingDestination
{
    /// <summary>
    /// Stable identifier for this destination within the order: the lowest PackNameId sharing it.
    /// Used in URLs and posted back, so it must not depend on ordering or list position.
    /// </summary>
    public int Key { get; set; }

    public int? ShippingAddressId { get; set; }

    public bool PickupInStore { get; set; }

    public string ShippingMethod { get; set; }

    public IList<string> PackNames { get; set; } = new List<string>();

    /// <summary>OrderItem ids belonging to this destination.</summary>
    public IList<int> OrderItemIds { get; set; } = new List<int>();

    /// <summary>PackName ids belonging to this destination — what gets its shipping status updated.</summary>
    public IList<int> PackNameIds { get; set; } = new List<int>();

    /// <summary>
    /// True when at least one of this destination's order items still has quantity that is not in any
    /// shipment.
    /// </summary>
    public bool HasItemsToShip { get; set; }

    /// <summary>
    /// The shipping status shared by this destination's packnames, already localised.
    /// </summary>
    /// <remarks>
    /// AdvancePackNameShippingAsync moves every packname in a destination together, so they agree. If
    /// they ever do not — hand-edited data, or a destination regrouped after placement — the *least*
    /// advanced one is shown, because "is this destination finished" is answered by its slowest part.
    /// </remarks>
    public string ShippingStatus { get; set; }

    /// <summary>
    /// One line, already localised and never empty: the address, or "Pickup in store", or "no address
    /// recorded". Resolved in CWOrderService so every surface shows the same words.
    /// </summary>
    public string AddressSummary { get; set; }

    /// <summary>What staff read on a button, a column and a badge. One place, so all three agree.</summary>
    public string Label
    {
        get
        {
            var how = string.IsNullOrWhiteSpace(ShippingMethod) ? null : $" · {ShippingMethod}";
            var who = PackNames.Count > 0 ? $" ({string.Join(", ", PackNames)})" : string.Empty;

            return $"{AddressSummary}{how}{who}";
        }
    }
}