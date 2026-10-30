using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class ProductGroupTopup : BaseEntity
{
    public int SubsidyPackNameId { get; set; }

    public int ProductGroupId { get; set; }

    public int Quantity { get; set; }

    public decimal Credit { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
