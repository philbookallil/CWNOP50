using System.Xml.Serialization;

namespace Nop.Plugin.Payments.NABGateway.Models.Response;

/// <summary>
/// Root response message returned by the NAB Transact XML API.
/// </summary>
[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "NABTransactMessage", Namespace = "", IsNullable = false)]
public class NABTransactMessage
{
    public NABTransactMessageMessageInfo MessageInfo { get; set; }

    public NABTransactMessageMerchantInfo MerchantInfo { get; set; }

    public string RequestType { get; set; }

    public NABTransactMessageStatus Status { get; set; }

    public NABTransactMessagePayment Payment { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessageMessageInfo
{
    public string messageID { get; set; }

    public string messageTimestamp { get; set; }

    public string apiVersion { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessageMerchantInfo
{
    public string merchantID { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessageStatus
{
    public string statusCode { get; set; }

    public string statusDescription { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessagePayment
{
    public NABTransactMessagePaymentTxnList TxnList { get; set; }

    [XmlText]
    public string[] Text { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessagePaymentTxnList
{
    public NABTransactMessagePaymentTxnListTxn Txn { get; set; }

    [XmlAttribute]
    public string count { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessagePaymentTxnListTxn
{
    public string txnType { get; set; }

    public string txnSource { get; set; }

    public string amount { get; set; }

    public string currency { get; set; }

    public string purchaseOrderNo { get; set; }

    public string approved { get; set; }

    public string responseCode { get; set; }

    public string responseText { get; set; }

    public string settlementDate { get; set; }

    public string txnID { get; set; }

    public string authID { get; set; }

    public NABTransactMessagePaymentTxnListTxnCreditCardInfo CreditCardInfo { get; set; }

    [XmlAttribute]
    public string ID { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessagePaymentTxnListTxnCreditCardInfo
{
    public string pan { get; set; }

    public string expiryDate { get; set; }

    public string cardType { get; set; }

    public string cardDescription { get; set; }
}
