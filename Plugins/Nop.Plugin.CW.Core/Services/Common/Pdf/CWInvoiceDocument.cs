using Nop.Services.Common.Pdf;
using PdfRpt.Core.Contracts;

namespace Nop.Plugin.CW.Core.Services.Common.Pdf;

/// <summary>
/// An invoice that can name more than one delivery address.
/// </summary>
/// <remarks>
/// <para>
/// nop's <see cref="InvoiceDocument"/> holds a single <see cref="InvoiceDocument.ShippingAddress"/>,
/// because a nop order has a single delivery address. A packname order has several, and the one on
/// <c>Order.ShippingAddressId</c> is only the first packname's, stored as a placeholder by
/// <c>OrderProcessingOverrideService</c>. An unmodified invoice therefore names one branch and hides
/// the rest.
/// </para>
/// <para>
/// Only <see cref="CreateAdressesInfo"/> is overridden — nop's spelling of the name is kept, since it
/// is the base member. It is <c>protected virtual</c> and reached through
/// <c>CreateInvoiceHeader</c> from inside <c>Generate</c>, so neither of those has to be touched, and
/// every address block is still rendered by nop's own <c>BuildAddressTable</c>. No rendering code is
/// copied, so nothing here drifts when nop's address layout changes.
/// </para>
/// </remarks>
public class CWInvoiceDocument : InvoiceDocument
{
    #region Utilities

    /// <summary>
    /// Build the billing / shipping header block, stacking one address per delivery destination.
    /// </summary>
    protected override PdfGrid CreateAdressesInfo()
    {
        var destinations = ShippingDestinations?.Where(destination => destination != null).ToList() ?? [];

        //One destination, or none: nop's own layout already says everything there is to say, and
        //ShippingAddress carries that destination's address. Handing back to base keeps every
        //ordinary order — and every single-destination packname order — byte-identical to today.
        if (destinations.Count < 2)
            return base.CreateAdressesInfo();

        //Shipping is not required on this order, so nop prints no shipping block at all and neither
        //should we. ShippingMethod is the field nop itself gates on at InvoiceDocument.cs line 18,
        //and GetShippingAddressAsync only ever sets it when shipping is required.
        if (!destinations.Any(destination => !string.IsNullOrEmpty(destination.ShippingMethod)))
            return base.CreateAdressesInfo();

        var addressesTable = PdfDocumentHelper.BuildPdfGrid(numColumns: 2, DocumentRunDirection);

        var billingInfo = PdfDocumentHelper.BuildPdfPCell(
            BuildAddressTable<InvoiceDocument>(source => BillingAddress, BillingAddress),
            DocumentRunDirection);

        addressesTable.AddCell(billingInfo);

        //A single-column grid inside the shipping cell, one full address block per destination. Each
        //block brings nop's own caption and horizontal rule with it, which is what separates them.
        var shippingColumn = PdfDocumentHelper.BuildPdfGrid(numColumns: 1, DocumentRunDirection);

        foreach (var destination in destinations)
        {
            var destinationCell = PdfDocumentHelper.BuildPdfPCell(
                BuildAddressTable<InvoiceDocument>(source => ShippingAddress, destination),
                DocumentRunDirection);

            //SpacingAfter on a PdfGrid only applies when the grid is added to the document directly,
            //not when it is nested in a cell, so the gap between blocks has to be cell padding.
            destinationCell.PaddingBottom = 8;

            shippingColumn.AddCell(destinationCell);
        }

        addressesTable.AddCell(PdfDocumentHelper.BuildPdfPCell(shippingColumn, DocumentRunDirection));

        return addressesTable;
    }

    /// <summary>
    /// Build the totals block. With settlement totals the rows read in the order the money moved: the goods
    /// and shipping at full price, what subsidy and store credit covered, the customer's own discounts and
    /// fees, and what was paid. Without them nop's own block is used unchanged.
    /// </summary>
    /// <remarks>
    /// nop's row list is reproduced here because <c>InvoiceDocument.CreateSummary</c> places its order total
    /// last and offers no seam to add rows before it.
    /// </remarks>
    protected override PdfGrid CreateSummary()
    {
        if (Totals is not CWInvoiceTotals totals)
            return base.CreateSummary();

        var summaryData = PdfDocumentHelper.BuildPdfGrid(numColumns: 1, DocumentRunDirection);

        if (!string.IsNullOrEmpty(totals.SubTotal))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.SubTotal, totals.SubTotal));
        if (!string.IsNullOrEmpty(totals.Shipping))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.Shipping, totals.Shipping));
        if (!string.IsNullOrEmpty(totals.Tax))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.Tax, totals.Tax));

        foreach (var rate in totals.TaxRates)
            summaryData.AddCell(BuildPdfPCell(rate));

        if (!string.IsNullOrEmpty(totals.GrossTotal))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.GrossTotal, totals.GrossTotal));
        if (!string.IsNullOrEmpty(totals.SubsidyForStock))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.SubsidyForStock, totals.SubsidyForStock));
        if (!string.IsNullOrEmpty(totals.SubsidyForShipping))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.SubsidyForShipping, totals.SubsidyForShipping));
        if (!string.IsNullOrEmpty(totals.CreditNotes))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.CreditNotes, totals.CreditNotes));
        if (!string.IsNullOrEmpty(totals.SubTotalDiscount))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.SubTotalDiscount, totals.SubTotalDiscount));
        if (!string.IsNullOrEmpty(totals.PaymentMethodAdditionalFee))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.PaymentMethodAdditionalFee, totals.PaymentMethodAdditionalFee));
        if (!string.IsNullOrEmpty(totals.OrderDiscount))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.OrderDiscount, totals.OrderDiscount));

        foreach (var card in totals.GiftCards)
            summaryData.AddCell(BuildPdfPCell(card));

        if (!string.IsNullOrEmpty(totals.RewardPoints))
            summaryData.AddCell(BuildTextCell<InvoiceTotals>(t => t.RewardPoints, totals.RewardPoints));
        if (!string.IsNullOrEmpty(totals.ExchangeFees))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.ExchangeFees, totals.ExchangeFees));
        if (!string.IsNullOrEmpty(totals.PayableBalance))
            summaryData.AddCell(BuildTextCell<CWInvoiceTotals>(t => t.PayableBalance, totals.PayableBalance));

        return summaryData;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Every delivery address on the order, in destination order, each already carrying its packname
    /// list as a custom value. <see cref="InvoiceDocument.ShippingAddress"/> is the first of them, so
    /// that the base class stays correct if this list is empty or holds one entry.
    /// </summary>
    public IList<AddressItem> ShippingDestinations { get; init; }

    #endregion
}