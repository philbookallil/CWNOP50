using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.NABGateway.Models;

/// <summary>
/// Represents the admin configuration screen for the NAB Transact XML payment plugin
/// </summary>
public record ConfigurationModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Fields.GatewayURL")]
    public string GatewayURL { get; set; }
    public bool GatewayURL_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Fields.MerchantId")]
    public string MerchantId { get; set; }
    public bool MerchantId_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Fields.Password")]
    public string Password { get; set; }
    public bool Password_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Fields.DebugMode")]
    public bool DebugMode { get; set; }
    public bool DebugMode_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Fields.IgnoreSslErrors")]
    public bool IgnoreSslErrors { get; set; }
    public bool IgnoreSslErrors_OverrideForStore { get; set; }
}
