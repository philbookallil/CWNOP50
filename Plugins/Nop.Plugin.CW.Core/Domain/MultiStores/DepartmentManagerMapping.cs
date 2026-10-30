using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.MultiStores;

public class DepartmentManagerMapping : BaseEntity
{
    /// <summary>
    /// Gets or sets the packname identifier
    /// </summary>
    public int PackNameId { get; set; }

    /// <summary>
    /// Gets or sets the department identifier
    /// </summary>
    public int DepartmentId { get; set; }
}
