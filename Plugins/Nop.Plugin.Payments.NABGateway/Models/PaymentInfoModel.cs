using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.NABGateway.Models;

/// <summary>
/// Represents the payment information captured on the checkout page
/// </summary>
public record PaymentInfoModel : BaseNopModel
{
    public PaymentInfoModel()
    {
        ExpireMonths = new List<SelectListItem>();
        ExpireYears = new List<SelectListItem>();
    }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Payment.CardholderName")]
    public string CardholderName { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Payment.CardNumber")]
    public string CardNumber { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Payment.ExpirationDate")]
    public string ExpireMonth { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Payment.ExpirationDate")]
    public string ExpireYear { get; set; }

    public IList<SelectListItem> ExpireMonths { get; set; }

    public IList<SelectListItem> ExpireYears { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Payment.CardCode")]
    public string CardCode { get; set; }
}
