using Nop.Core;

namespace Nop.Plugin.CW.Freight.Domain;

/// <summary>
/// Maps a CW shipping method to a store
/// </summary>
public class CWShippingMethodStoreMapping : BaseEntity
{
    public int ShippingMethodId { get; set; }
    public int StoreId { get; set; }
}
