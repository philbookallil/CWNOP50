using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Orders;

/// <summary>
/// The settlement block of the customer's order page: what the order came to before subsidy and store credit,
/// what each covered, and what the customer paid. Every amount is formatted in the order's currency.
/// </summary>
public record PackNameOrderSettlementModel : BaseNopModel
{
    /// <summary>Goods and shipping at full price, before subsidy and store credit.</summary>
    public string GrossTotal { get; set; }

    public string SubsidyForStock { get; set; }

    public string SubsidyForShipping { get; set; }

    /// <summary>Subsidy for goods and shipping together.</summary>
    public string SubsidyTotal { get; set; }

    public string CreditNoteTotal { get; set; }

    /// <summary>What the customer paid: the order total after every deduction and fee.</summary>
    public string PayableBalance { get; set; }

    public bool HasSubsidy { get; set; }

    public bool HasShippingSubsidy { get; set; }

    public bool HasCreditNotes { get; set; }
}