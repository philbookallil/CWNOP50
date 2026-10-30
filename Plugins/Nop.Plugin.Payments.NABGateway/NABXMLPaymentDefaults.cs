namespace Nop.Plugin.Payments.NABGateway;

/// <summary>
/// Represents defaults for the NAB Transact XML payment plugin
/// </summary>
public static class NABXMLPaymentDefaults
{
    /// <summary>
    /// Plugin system name (kept in sync with plugin.json)
    /// </summary>
    public const string SystemName = "Payments.NABGateway";

    /// <summary>
    /// Folder name under ~/Plugins where the build output is published
    /// </summary>
    public const string PluginFolder = "Payments.NABGateway";

    /// <summary>
    /// Name of the public view component used during checkout
    /// </summary>
    public const string ViewComponentName = "NABXMLPayment";

    /// <summary>
    /// NAB Transact XML API version sent on every request
    /// </summary>
    public const string ApiVersion = "xml-4.2";

    /// <summary>
    /// Timeout (in seconds) communicated to the NAB gateway on every request
    /// </summary>
    public const int GatewayTimeoutSeconds = 60;

    /// <summary>
    /// Path appended to the configured gateway URL for payment requests
    /// </summary>
    public const string PaymentEndpointPath = "/xmlapi/payment";

    /// <summary>
    /// Name of the HttpClient registered for the NAB gateway
    /// </summary>
    public const string HttpClientName = "NABXMLPayment";

    /// <summary>
    /// Data Protection purpose (and memory-cache key prefix) for the card kept between checkout steps
    /// </summary>
    public const string CardDataProtectionPurpose = "Nop.Plugin.Payments.NABGateway.CardData";

    /// <summary>
    /// Minutes a card entered on the payment-info step is kept for the confirm step
    /// </summary>
    public const int CardDataLifetimeMinutes = 15;
}
