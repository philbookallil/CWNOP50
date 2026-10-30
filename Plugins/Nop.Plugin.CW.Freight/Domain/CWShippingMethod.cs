using Nop.Core;

namespace Nop.Plugin.CW.Freight.Domain;

/// <summary>
/// Represents a CW shipping method with order-total-based rate bands
/// </summary>
public class CWShippingMethod : BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
    public int DisplayOrder { get; set; }
    public decimal Rate { get; set; }
    public decimal LowestOrderTotal { get; set; }
    public decimal HighestOrderTotal { get; set; }
    public bool ApplyPerBranch { get; set; }
}
