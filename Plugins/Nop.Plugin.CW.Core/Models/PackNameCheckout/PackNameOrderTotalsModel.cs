using Nop.Web.Models.ShoppingCart;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

public partial record PackNameOrderTotalsModel : OrderTotalsModel
{
    #region Ctor

    public PackNameOrderTotalsModel()
    {
        ShippingTotals = new List<PackNameShippingTotalsModel>();
    }

    #endregion

    #region Properties

    public decimal PaymentMethodAdditionalFeeTaxValue { get; set; }

    public decimal TaxValue { get; set; }

    public IList<PackNameShippingTotalsModel> ShippingTotals { get; set; }

    //Subsidy values

    public string SubsidyTotal { get; set; }

    public string SubsidyForStock { get; set; }

    public string SubsidyForShipping { get; set; }

    public string PayableBalance { get; set; }

    /// <summary>Formatted credit-note amount applied, as a negative. Null when none applied.</summary>
    public string CreditNoteTotal { get; set; }

    /// <summary>Formatted exchange fees, as a positive. Null when the cart has no returned items.</summary>
    public string ExchangeFeesTotal { get; set; }

    /// <summary>
    /// The fee-by-fee breakdown behind <see cref="ExchangeFeesTotal"/> — redelivery, repackaging and each
    /// ad-hoc fee, already formatted. Empty when there is nothing to break down, which hides the expander.
    /// </summary>
    public IList<(string Concept, string Amount)> ExchangeFeeDetails { get; set; } = [];

    /// <summary>
    /// Whether the panel needs a separate "payable balance" line — true whenever something was deducted
    /// from the order total or added to it, so that Total and Payable are no longer the same figure.
    /// </summary>
    public bool ShowPayableBalance =>
        !string.IsNullOrEmpty(SubsidyTotal)
        || !string.IsNullOrEmpty(CreditNoteTotal)
        || !string.IsNullOrEmpty(ExchangeFeesTotal);

    /// <summary>
    /// Whether subsidy or store credit paid for part of the order — the panel then names the total before them and
    /// the customer's own discounts apart, as the order page and the invoice do.
    /// </summary>
    public bool HasSettlement =>
        !string.IsNullOrEmpty(SubsidyTotal)
        || !string.IsNullOrEmpty(CreditNoteTotal);

    #endregion
}
