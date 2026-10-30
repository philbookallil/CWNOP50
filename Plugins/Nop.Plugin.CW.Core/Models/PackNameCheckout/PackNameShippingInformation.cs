using Nop.Core.Domain.Common;
using Nop.Core.Domain.Shipping;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

public class PackNameShippingInformation
{
    public int PackNameId { get; set; }

    public Address Address { get; set; }

    public ShippingOption ShippingOption { get; set; }
}
