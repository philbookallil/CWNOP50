using Nop.Core;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class SubsidyPackName : BaseEntity, ISoftDeletedEntity
{
    public int PackNameId { get; set; }

    public int SubsidyId { get; set; }

    public int CreatorId { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public bool Active { get; set; }

    public bool Deleted { get; set; }
}
