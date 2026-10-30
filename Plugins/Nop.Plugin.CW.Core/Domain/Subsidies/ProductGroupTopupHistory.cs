using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class ProductGroupTopupHistory : BaseEntity
{
    public int ProductGroupTopupId { get; set; }

    public int Quantity { get; set; }

    public decimal Credit { get; set; }

    public string Reason { get; set; }

    public int CreatorId { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
