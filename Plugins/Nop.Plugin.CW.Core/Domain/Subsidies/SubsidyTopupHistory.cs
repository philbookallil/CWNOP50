using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class SubsidyTopupHistory : BaseEntity
{
    public int SubsidyTopupId { get; set; }

    public decimal Credit { get; set; }

    public string Reason { get; set; }

    public int CreatorId { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
