using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;

/// <summary>
/// What the destination badge needs, on either screen that shows it.
/// </summary>
public partial record CWShipmentDestinationModel : BaseNopModel
{
    /// <summary>
    /// The destination, ready to display. On a legacy shipment that spans two destinations this is both
    /// of them joined — which is the honest answer, and the thing someone needs to see.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Set only on the Add Shipment screen, where it is echoed back into the form so the POST guard
    /// still knows the chosen destination when no quantities were entered. Null on Shipment Details,
    /// which is a read-only view with nothing to post.
    /// </summary>
    public int? DestinationKey { get; set; }
}