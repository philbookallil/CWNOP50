using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class SubsidyOrderItem : BaseEntity
{
    public int OrderId { get; set; }

    public int OrderItemId { get; set; }

    public int SubsidyId { get; set; }

    public int? ProductGroupId { get; set; }

    public decimal Credit { get; set; }

    public decimal CreditTopup { get; set; }

    public int Quantity { get; set; }

    public int QuantityTopup { get; set; }

    public decimal ProductGroupCredit { get; set; }

    public decimal ProductGroupCreditTopup { get; set; }
}