using Nop.Core;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class SubsidyProductGroup : BaseEntity, ISoftDeletedEntity
{
    public int SubsidyId { get; set; }

    public int ProductGroupId { get; set; }

    public int Quantity { get; set; }

    public decimal Credit { get; set; }

    public int DisplayOrder { get; set; }

    public int CreatorId { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public bool Deleted { get; set; }
}
