using Nop.Core;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.PackNames;

public class PackNameList : BaseEntity, ISoftDeletedEntity
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the store identifier
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets the branch identifier
    /// </summary>
    public int? BranchId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the packname list is active
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all managers can see this packname list
    /// </summary>
    public bool AllManagers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all managers from the parent branches can see this packname list
    /// </summary>
    public bool ParentManagers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is the default list for the branch
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the date and time of packnamelist creation
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the packname list is deleted
    /// </summary>
    public bool Deleted { get; set; }
}
