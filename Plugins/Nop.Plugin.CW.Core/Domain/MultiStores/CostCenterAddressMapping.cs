using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.MultiStores;

public class CostCenterAddressMapping : BaseEntity
{
    /// <summary>
    /// Gets or sets the packname identifier
    /// </summary>
    public int CostCenterId { get; set; }

    /// <summary>
    /// Gets or sets the address identifier
    /// </summary>
    public int AddressId { get; set; }
}
