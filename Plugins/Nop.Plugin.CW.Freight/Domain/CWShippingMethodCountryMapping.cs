using Nop.Core;

namespace Nop.Plugin.CW.Freight.Domain;

/// <summary>
/// Maps a CW shipping method to a restricted country
/// </summary>
public class CWShippingMethodCountryMapping : BaseEntity
{
    public int ShippingMethodId { get; set; }
    public int CountryId { get; set; }
}
