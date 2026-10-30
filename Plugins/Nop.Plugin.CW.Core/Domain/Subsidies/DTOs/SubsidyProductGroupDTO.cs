using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

public class SubsidyProductGroupDTO : BaseEntity
{
    public int ProductGroupId { get; set; }

    public int SubsidyId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string Icon { get; set; }

    public string Colour { get; set; }

    public int DisplayOrder { get; set; }

    public int Quantity { get; set; }

    public decimal Credit { get; set; }

    public int TopupQuantity { get; set; }

    public decimal TopupCredit { get; set; }

    public int UsedQuantity { get; set; }

    public int UsedQuantityTopup { get; set; }

    public decimal UsedCredit { get; set; }

    public decimal UsedCreditTopup { get; set; }
}
