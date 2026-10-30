using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

public class SubsidyDTO : BaseEntity
{
    public int StoreId { get; set; }

    public string Name { get; set; }

    public decimal Credit { get; set; }

    public DateTime DurationStart { get; set; }

    public DateTime DurationEnd { get; set; }

    public bool Active { get; set; }

    public int PackNameCount { get; set; }
}
