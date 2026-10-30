using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class SubsidyOrderShipping : BaseEntity
{
    public int OrderId { get; set; }

    public int PackNameId { get; set; }

    public int SubsidyId { get; set; }

    public decimal Credit { get; set; }

    public decimal CreditTopup { get; set; }
}