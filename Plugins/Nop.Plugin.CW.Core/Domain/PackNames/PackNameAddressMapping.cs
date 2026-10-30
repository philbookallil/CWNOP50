using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.PackNames;

public class PackNameAddressMapping : BaseEntity
{
    /// <summary>
    /// Gets or sets the packname identifier
    /// </summary>
    public int PackNameId { get; set; }

    /// <summary>
    /// Gets or sets the address identifier
    /// </summary>
    public int AddressId { get; set; }
}
