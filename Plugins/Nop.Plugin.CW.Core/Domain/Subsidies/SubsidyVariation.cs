using Nop.Core;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class SubsidyVariation : BaseEntity, ISoftDeletedEntity
{
    public int SubsidyPackNameId { get; set; }

    public decimal? Credit { get; set; }

    public DateTime? DurationStart { get; set; }

    public DateTime? DurationEnd { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public bool Deleted { get; set; }
}
