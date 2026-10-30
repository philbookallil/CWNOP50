using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Domain.Orders.Views;

namespace Nop.Plugin.CW.Core.Domain.Orders.DTOs;

/// <summary>
/// Shared DTO consumed by both public and admin model factories.
/// </summary>
public class PackNameOrderGroupDTO
{
    public int PackNameId { get; set; }

    public string PackName { get; set; }

    public string Branch { get; set; }

    public string BranchCode { get; set; }

    public string Department { get; set; }

    public string CostCenter { get; set; }

    public IList<PackNameFullOrderItem> Items { get; set; } = new List<PackNameFullOrderItem>();

    public OrderShipping Shipping { get; set; }

    /// <summary>The Address entity loaded from ShippingAddressId. Null when PickupInStore.</summary>
    public Address ShippingAddress { get; set; }

    /// <summary>True when two or more packnames share the same ShippingAddressId + ShippingMethod.</summary>
    public bool IsSharedShipping { get; set; }

    /// <summary>PackName names (excluding this one) that share the same shipping.</summary>
    public IList<string> SharedWithPackNames { get; set; } = new List<string>();

    public string AddressName { get; set; }

    public string AddressLine1 { get; set; }

    public string AddressLine2 { get; set; }

    public string AddressCityStateZip { get; set; }

    public string AddressCountry { get; set; }

    public string AddressPhone { get; set; }
}