using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Branches;

public class BranchAddressMapping : BaseEntity
{
    /// <summary>
    /// Gets or sets the branch identifier
    /// </summary>
    public int BranchId { get; set; }

    /// <summary>
    /// Gets or sets the address identifier
    /// </summary>
    public int AddressId { get; set; }
}