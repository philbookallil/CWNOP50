using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class SubsidyVariationHistory : BaseEntity
{
    public int SubsidyVariationId { get; set; }

    public decimal? Credit { get; set; }

    public DateTime? DurationStart { get; set; }

    public DateTime? DurationEnd { get; set; }

    public string Reason { get; set; }

    public int CreatorId { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
