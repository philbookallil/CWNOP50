using Nop.Core.Domain.Shipping;

namespace Nop.Plugin.CW.Core.Domain.PackNames;

/// <summary>
/// Holds the shipping assignment data for a single PackName within a checkout session.
/// Stored as part of <see cref="PackNameShippingAssignments"/> — one record per packname,
/// persisted as a single consolidated generic attribute on the customer.
/// </summary>
public class PackNameShippingAssignment
{
    /// <summary>The address Id selected for shipping. Null means no address is assigned yet.</summary>
    public int? AddressId { get; set; }

    /// <summary>The shipping method the customer has selected for this packname.</summary>
    public ShippingOption SelectedShippingOption { get; set; }

    /// <summary>All shipping methods available for this packname's address, as calculated by the shipping service.</summary>
    public List<ShippingOption> OfferedShippingOptions { get; set; } = new();

    /// <summary>
    /// What <see cref="OfferedShippingOptions"/> were rated for: the address and the packname's cart lines with their
    /// quantities. Null when they have not been rated, or were stored before this was recorded.
    /// </summary>
    public string RatedFor { get; set; }
}