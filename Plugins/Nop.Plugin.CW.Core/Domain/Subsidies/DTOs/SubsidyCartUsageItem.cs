namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

public class SubsidyCartUsageItem
{
    /// <summary>Null = global subsidy-level credit; non-null = product-group-level usage.</summary>
    public int? ProductGroupId { get; set; }

    // Quantity buckets (product-group level only)
    public int Quantity { get; set; }       // consumed from pg qty base
    public int QuantityTopup { get; set; }  // consumed from pg qty topup

    // Product-group credit buckets (product-group level only)
    public decimal ProductGroupCredit { get; set; }       // consumed from pg credit base
    public decimal ProductGroupCreditTopup { get; set; }  // consumed from pg credit topup

    // Global credit buckets (ProductGroupId == null)
    public decimal Credit { get; set; }       // consumed from global subsidy credit base
    public decimal CreditTopup { get; set; }  // consumed from global subsidy credit topup
}
