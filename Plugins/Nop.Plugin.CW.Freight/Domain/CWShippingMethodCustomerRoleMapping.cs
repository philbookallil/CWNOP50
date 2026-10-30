using Nop.Core;

namespace Nop.Plugin.CW.Freight.Domain;

/// <summary>
/// Maps a CW shipping method to a customer role
/// </summary>
public class CWShippingMethodCustomerRoleMapping : BaseEntity
{
    public int ShippingMethodId { get; set; }
    public int CustomerRoleId { get; set; }
}
