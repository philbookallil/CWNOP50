using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class SubsidyTopup : BaseEntity
{
    public int SubsidyPackNameId { get; set; }

    public decimal Credit { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
