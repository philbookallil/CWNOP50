using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.NABGateway;

/// <summary>
/// Represents settings of the NAB Transact XML payment plugin
/// </summary>
public class NABXMLPaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets the base URL of the NAB Transact XML API
    /// </summary>
    public string GatewayURL { get; set; }

    /// <summary>
    /// Gets or sets the merchant identifier issued by NAB
    /// </summary>
    public string MerchantId { get; set; }

    /// <summary>
    /// Gets or sets the gateway password associated with the merchant identifier
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether debug logging is enabled. When on, the plugin writes the
    /// (card-masked) request payload, endpoint and raw gateway response to the nopCommerce log at Debug level.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether server SSL/TLS certificate validation errors should be ignored
    /// when calling the gateway. Intended for test/sandbox endpoints only — never enable in production.
    /// </summary>
    public bool IgnoreSslErrors { get; set; }
}
