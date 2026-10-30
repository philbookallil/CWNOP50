using System.ComponentModel;
using Nop.Services.Common.Pdf;

namespace Nop.Plugin.CW.Core.Services.Common.Pdf;

/// <summary>
/// nop's invoice totals plus the settlement rows of an order: what the order came to before subsidy and store
/// credit, what each covered, the exchange fees, and what the customer paid.
/// </summary>
public partial class CWInvoiceTotals : InvoiceTotals
{
    /// <summary>Goods and shipping at full price, before subsidy and store credit.</summary>
    [DisplayName("Plugins.CW.Core.Pdf.GrossTotal")]
    public string GrossTotal { get; set; }

    [DisplayName("Plugins.CW.Core.Pdf.SubsidyForStock")]
    public string SubsidyForStock { get; set; }

    [DisplayName("Plugins.CW.Core.Pdf.SubsidyForShipping")]
    public string SubsidyForShipping { get; set; }

    [DisplayName("Plugins.CW.Core.Pdf.CreditNotes")]
    public string CreditNotes { get; set; }

    /// <summary>The subtotal discount actually applied; nop's <see cref="InvoiceTotals.Discount"/> stays for nop's own invoice.</summary>
    [DisplayName("Plugins.CW.Core.Pdf.SubTotalDiscount")]
    public string SubTotalDiscount { get; set; }

    [DisplayName("Plugins.CW.Core.Pdf.OrderDiscount")]
    public string OrderDiscount { get; set; }

    [DisplayName("Plugins.CW.Core.Pdf.ExchangeFees")]
    public string ExchangeFees { get; set; }

    /// <summary>What the customer paid: the order total after every deduction and fee.</summary>
    [DisplayName("Plugins.CW.Core.Pdf.PayableBalance")]
    public string PayableBalance { get; set; }
}