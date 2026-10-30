using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Orders;

/// <summary>
/// What the settlement pipeline did for one order, written once when the order is placed: the amount it was
/// offered and how much of it each pool covered. Every screen, e-mail and document that shows an order's
/// subsidy or store credit reads this row rather than working the figures out again from the ledgers.
/// </summary>
public partial class OrderSettlement : BaseEntity
{
    public int OrderId { get; set; }

    /// <summary>The amount offered to the pipeline: goods at their line prices plus shipping, including tax.</summary>
    public decimal SettleableAmount { get; set; }

    /// <summary>Subsidy that paid for goods, including tax.</summary>
    public decimal SubsidyStockAmount { get; set; }

    /// <summary>Subsidy that paid for shipping, including tax.</summary>
    public decimal SubsidyShippingAmount { get; set; }

    /// <summary>Store credit (credit notes) applied, including tax.</summary>
    public decimal CreditNoteAmount { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}