using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.PackNames;

public class PackNameListManagerMapping : BaseEntity
{
    /// <summary>
    /// Gets or sets the packaging name list identifier
    /// </summary>
    public int PackNameListId { get; set; }

    /// <summary>
    /// Gets or sets the packName identifier
    /// </summary>
    public int PackNameId { get; set; }
}
