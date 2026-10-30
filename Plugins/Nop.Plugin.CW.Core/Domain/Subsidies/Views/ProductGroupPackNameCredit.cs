using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.Views;

public partial class ProductGroupPackNameCredit : BaseEntity
{
    public int PackNameId { get; set; }

    public int SubsidyId { get; set; }

    public int ProductGroupId { get; set; }

    public string ProductGroupName { get; set; }

    public int DisplayOrder { get; set; }

    public decimal Credit { get; set; }

    public int Quantity { get; set; }

    public decimal CreditTopup { get; set; }

    public int QuantityTopup { get; set; }

    public decimal UsedCredit { get; set; }

    public decimal UsedCreditTopup { get; set; }

    public int UsedQuantity { get; set; }

    public int UsedQuantityTopup { get; set; }
}
