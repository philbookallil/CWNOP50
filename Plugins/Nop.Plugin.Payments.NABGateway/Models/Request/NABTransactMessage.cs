using System.Xml.Serialization;

namespace Nop.Plugin.Payments.NABGateway.Models.Request;

/// <summary>
/// Root request message sent to the NAB Transact XML API.
/// </summary>
[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "NABTransactMessage", Namespace = "", IsNullable = false)]
public class NABTransactMessage
{
    public NABTransactMessageMessageInfo MessageInfo { get; set; }

    public NABTransactMessageMerchantInfo MerchantInfo { get; set; }

    public string RequestType { get; set; }

    public NABTransactMessagePayment Payment { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessageMessageInfo
{
    public string messageID { get; set; }

    public string messageTimestamp { get; set; }

    public int timeoutValue { get; set; }

    public string apiVersion { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessageMerchantInfo
{
    public string merchantID { get; set; }

    public string password { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessagePayment
{
    public NABTransactMessagePaymentTxnList TxnList { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessagePaymentTxnList
{
    public NABTransactMessagePaymentTxnListTxn Txn { get; set; }

    [XmlAttribute]
    public int count { get; set; }
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

    public NABTransactMessagePaymentTxnListTxnCreditCardInfo CreditCardInfo { get; set; }

    [XmlAttribute]
    public string ID { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public class NABTransactMessagePaymentTxnListTxnCreditCardInfo
{
    public string cardNumber { get; set; }

    public string expiryDate { get; set; }

    public string cardHolderName { get; set; }

    public string cvv { get; set; }

    public string recurringflag { get; set; }
}
