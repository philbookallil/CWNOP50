using Nop.Core;

namespace Nop.Plugin.Payments.NABGateway.Domain;

/// <summary>
/// Represents a single NAB Transact XML payment attempt (successful or failed).
/// One unified table backs both the "Payments" report (Approved == true) and the
/// "Failed Transactions" report (Approved == false).
/// </summary>
public class NABXMLTransaction : BaseEntity
{
    /// <summary>
    /// nopCommerce order GUID this attempt belongs to.
    /// </summary>
    public Guid OrderGuid { get; set; }

    /// <summary>
    /// nopCommerce customer identifier.
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Customer email captured at payment time.
    /// </summary>
    public string UserEmail { get; set; }

    /// <summary>
    /// Customer full name captured at payment time.
    /// </summary>
    public string UserFullName { get; set; }

    /// <summary>
    /// Whether the gateway approved the transaction. This is the discriminator the two reports filter on.
    /// </summary>
    public bool Approved { get; set; }

    /// <summary>
    /// Gateway message identifier (NABTransactMessage/MessageInfo/messageID).
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gateway transaction identifier (Txn/txnID).
    /// </summary>
    public string TxnId { get; set; }

    /// <summary>
    /// Gateway authorization identifier (Txn/authID).
    /// </summary>
    public string AuthId { get; set; }

    /// <summary>
    /// Gateway response code (Txn/responseCode), or the message-level status code when the txn block is absent.
    /// </summary>
    public string ResponseCode { get; set; }

    /// <summary>
    /// Human readable gateway response/reason (Txn/responseText or Status/statusDescription).
    /// </summary>
    public string ResponseText { get; set; }

    /// <summary>
    /// Gateway settlement date (Txn/settlementDate).
    /// </summary>
    public string SettlementDate { get; set; }

    /// <summary>
    /// Masked card number returned by the gateway (CreditCardInfo/pan).
    /// </summary>
    public string MaskedPan { get; set; }

    /// <summary>
    /// Card type returned by the gateway (CreditCardInfo/cardType or cardDescription).
    /// </summary>
    public string CardType { get; set; }

    /// <summary>
    /// Order amount in the store primary currency.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code used for the transaction.
    /// </summary>
    public string CurrencyCode { get; set; }

    /// <summary>
    /// Purchase order reference sent to the gateway (Txn/purchaseOrderNo).
    /// </summary>
    public string PurchaseOrderNo { get; set; }

    /// <summary>
    /// The full raw XML response returned by the gateway (shown in the report detail modal).
    /// </summary>
    public string RawResponseXml { get; set; }

    /// <summary>
    /// Creation date (UTC).
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }
}
