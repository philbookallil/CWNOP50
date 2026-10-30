using AwesomeAssertions;
using Nop.Plugin.Payments.NABGateway;
using Nop.Plugin.Payments.NABGateway.Services;
using NUnit.Framework;
using Request = Nop.Plugin.Payments.NABGateway.Models.Request;
using Response = Nop.Plugin.Payments.NABGateway.Models.Response;

namespace CW.Tests.Payments.NABGateway;

/// <summary>
/// The XML exchanged with NAB Transact: the request the plugin writes, the responses it reads, and the masking applied
/// before either reaches the debug log.
/// </summary>
[TestFixture]
public class GatewayXmlTests
{
    #region Fields

    private const string TEST_CARD = "4111111111111111";

    private const string APPROVED_RESPONSE =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<NABTransactMessage>" +
        "<MessageInfo><messageID>8af793f9af34bea0cf40f5fb5c630c</messageID>" +
        "<messageTimestamp>20262210120000000000+600</messageTimestamp><apiVersion>xml-4.2</apiVersion></MessageInfo>" +
        "<RequestType>Payment</RequestType>" +
        "<MerchantInfo><merchantID>ABC0001</merchantID></MerchantInfo>" +
        "<Status><statusCode>000</statusCode><statusDescription>Normal</statusDescription></Status>" +
        "<Payment><TxnList count=\"1\"><Txn ID=\"1\">" +
        "<txnType>0</txnType><txnSource>23</txnSource><amount>1000</amount><currency>AUD</currency>" +
        "<purchaseOrderNo>Shop-Jane-Smith</purchaseOrderNo><approved>Yes</approved><responseCode>00</responseCode>" +
        "<responseText>Approved</responseText><settlementDate>20261022</settlementDate>" +
        "<txnID>009844</txnID><authID>A12345</authID>" +
        "<CreditCardInfo><pan>444433...111</pan><expiryDate>12/28</expiryDate><cardType>6</cardType>" +
        "<cardDescription>Visa</cardDescription></CreditCardInfo>" +
        "</Txn></TxnList></Payment>" +
        "</NABTransactMessage>";

    private const string DECLINED_RESPONSE =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<NABTransactMessage>" +
        "<MessageInfo><messageID>9bf793f9af34bea0cf40f5fb5c630c</messageID><apiVersion>xml-4.2</apiVersion></MessageInfo>" +
        "<RequestType>Payment</RequestType>" +
        "<Status><statusCode>000</statusCode><statusDescription>Normal</statusDescription></Status>" +
        "<Payment><TxnList count=\"1\"><Txn ID=\"1\">" +
        "<amount>1000</amount><currency>AUD</currency><approved>No</approved><responseCode>51</responseCode>" +
        "<responseText>Insufficient Funds</responseText><txnID></txnID>" +
        "</Txn></TxnList></Payment>" +
        "</NABTransactMessage>";

    private const string GATEWAY_ERROR_RESPONSE =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<NABTransactMessage>" +
        "<MessageInfo><messageID>abc</messageID><apiVersion>xml-4.2</apiVersion></MessageInfo>" +
        "<RequestType>Payment</RequestType>" +
        "<Status><statusCode>504</statusCode><statusDescription>Invalid merchant ID</statusDescription></Status>" +
        "</NABTransactMessage>";

    #endregion

    #region Utilities

    private static Request.NABTransactMessage BuildRequest()
    {
        return new Request.NABTransactMessage
        {
            MessageInfo = new Request.NABTransactMessageMessageInfo
            {
                messageID = "0123456789abcdef0123456789abcdef",
                messageTimestamp = "20262210120000000000000",
                apiVersion = NABXMLPaymentDefaults.ApiVersion,
                timeoutValue = NABXMLPaymentDefaults.GatewayTimeoutSeconds
            },
            MerchantInfo = new Request.NABTransactMessageMerchantInfo
            {
                merchantID = "TEST0000",
                password = "placeholder"
            },
            RequestType = "Payment",
            Payment = new Request.NABTransactMessagePayment
            {
                TxnList = new Request.NABTransactMessagePaymentTxnList
                {
                    count = 1,
                    Txn = new Request.NABTransactMessagePaymentTxnListTxn
                    {
                        ID = "1",
                        txnType = "0",
                        txnSource = "23",
                        amount = "1000",
                        currency = "AUD",
                        purchaseOrderNo = "Shop-Jane-Smith",
                        CreditCardInfo = new Request.NABTransactMessagePaymentTxnListTxnCreditCardInfo
                        {
                            cardNumber = TEST_CARD,
                            expiryDate = "12/2028",
                            cardHolderName = "Test Shopper",
                            cvv = "123",
                            recurringflag = "no"
                        }
                    }
                }
            }
        };
    }

    #endregion

    #region Request

    [Test]
    public void Request_is_a_bare_NABTransactMessage_with_a_utf8_declaration()
    {
        var xml = NABXMLGatewayClient.Serialize(BuildRequest());

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?><NABTransactMessage>");
        xml.Should().NotContain("xmlns");
        xml.Should().EndWith("</NABTransactMessage>");
    }

    [Test]
    public void Request_sections_are_in_the_order_NAB_documents()
    {
        var xml = NABXMLGatewayClient.Serialize(BuildRequest());

        var positions = new[] { "<MessageInfo>", "<MerchantInfo>", "<RequestType>", "<Payment>" }
            .Select(tag => xml.IndexOf(tag, StringComparison.Ordinal))
            .ToList();

        positions.Should().NotContain(-1);
        positions.Should().BeInAscendingOrder();
    }

    [Test]
    public void Request_carries_the_transaction_fields_and_attributes()
    {
        var xml = NABXMLGatewayClient.Serialize(BuildRequest());

        xml.Should().Contain("<apiVersion>xml-4.2</apiVersion>");
        xml.Should().Contain($"<timeoutValue>{NABXMLPaymentDefaults.GatewayTimeoutSeconds}</timeoutValue>");
        xml.Should().Contain("<TxnList count=\"1\"><Txn ID=\"1\">");
        xml.Should().Contain("<txnSource>23</txnSource>");
        xml.Should().Contain("<amount>1000</amount>");
        xml.Should().Contain("<purchaseOrderNo>Shop-Jane-Smith</purchaseOrderNo>");
        xml.Should().Contain($"<cardNumber>{TEST_CARD}</cardNumber>");
        xml.Should().Contain("<expiryDate>12/2028</expiryDate>");
        xml.Should().Contain("<recurringflag>no</recurringflag>");
    }

    #endregion

    #region Response

    [Test]
    public void Approved_response_is_read()
    {
        var response = NABXMLGatewayClient.Deserialize<Response.NABTransactMessage>(APPROVED_RESPONSE);

        response.Should().NotBeNull();
        response.MessageInfo.messageID.Should().Be("8af793f9af34bea0cf40f5fb5c630c");
        response.Status.statusCode.Should().Be("000");

        var txn = response.Payment.TxnList.Txn;
        txn.approved.Should().Be("Yes");
        txn.responseCode.Should().Be("00");
        txn.responseText.Should().Be("Approved");
        txn.txnID.Should().Be("009844");
        txn.authID.Should().Be("A12345");
        txn.settlementDate.Should().Be("20261022");
        txn.purchaseOrderNo.Should().Be("Shop-Jane-Smith");
        txn.CreditCardInfo.pan.Should().Be("444433...111");
        txn.CreditCardInfo.cardDescription.Should().Be("Visa");
    }

    [Test]
    public void Declined_response_is_read()
    {
        var response = NABXMLGatewayClient.Deserialize<Response.NABTransactMessage>(DECLINED_RESPONSE);

        response.Status.statusCode.Should().Be("000");

        var txn = response.Payment.TxnList.Txn;
        txn.approved.Should().Be("No");
        txn.responseCode.Should().Be("51");
        txn.responseText.Should().Be("Insufficient Funds");
        txn.CreditCardInfo.Should().BeNull();
    }

    [Test]
    public void Gateway_error_response_has_a_status_and_no_payment()
    {
        var response = NABXMLGatewayClient.Deserialize<Response.NABTransactMessage>(GATEWAY_ERROR_RESPONSE);

        response.Status.statusCode.Should().Be("504");
        response.Status.statusDescription.Should().Be("Invalid merchant ID");
        response.Payment.Should().BeNull();
    }

    [TestCase(null, TestName = "No response gives null")]
    [TestCase("", TestName = "An empty response gives null")]
    [TestCase("   ", TestName = "A blank response gives null")]
    public void Missing_response_gives_null(string? xml)
    {
        NABXMLGatewayClient.Deserialize<Response.NABTransactMessage>(xml!).Should().BeNull();
    }

    [Test]
    public void Malformed_response_throws()
    {
        var read = () => NABXMLGatewayClient.Deserialize<Response.NABTransactMessage>("<html>Bad gateway</html>");

        read.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Log masking

    [Test]
    public void Request_logged_in_debug_mode_shows_only_the_last_four_digits_and_no_cvv()
    {
        var masked = NABXMLGatewayClient.MaskSensitiveData(NABXMLGatewayClient.Serialize(BuildRequest()));

        masked.Should().NotContain(TEST_CARD);
        masked.Should().Contain("<cardNumber>************1111</cardNumber>");
        masked.Should().Contain("<cvv>***</cvv>");
        masked.Should().NotContain("<cvv>123</cvv>");
    }

    [Test]
    public void Masking_ignores_the_case_of_the_tag()
    {
        var masked = NABXMLGatewayClient.MaskSensitiveData($"<CardNumber>{TEST_CARD}</CardNumber><CVV>123</CVV>");

        masked.Should().Be("<CardNumber>************1111</CardNumber><CVV>***</CVV>");
    }

    [Test]
    public void Masking_keeps_a_short_number_as_it_is_after_the_mask()
    {
        NABXMLGatewayClient.MaskSensitiveData("<cardNumber>12</cardNumber>")
            .Should().Be("<cardNumber>************12</cardNumber>");
    }

    [Test]
    public void Masking_leaves_a_response_without_card_data_unchanged()
    {
        NABXMLGatewayClient.MaskSensitiveData(APPROVED_RESPONSE).Should().Be(APPROVED_RESPONSE);
    }

    [TestCase(null, TestName = "No payload is returned as it is")]
    [TestCase("", TestName = "An empty payload is returned as it is")]
    public void Masking_returns_a_missing_payload_as_it_is(string? xml)
    {
        NABXMLGatewayClient.MaskSensitiveData(xml!).Should().Be(xml);
    }

    #endregion
}
