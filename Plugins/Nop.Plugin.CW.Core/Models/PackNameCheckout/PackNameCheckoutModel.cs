using Nop.Web.Framework.Models;
using Nop.Web.Models.Checkout;
using Nop.Web.Models.Common;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

public record PackNameCheckoutModel : BaseNopModel
{
    #region Properties

    // Cart
    public PackNameShoppingCartModel ShoppingCart { get; set; } = new();

    // Per-PackName shipping
    public List<PackNameShippingModel> ShippingOptions { get; set; } = new();

    public bool CanAddAddress { get; set; }

    public bool HasShippingWarnings { get; set; }

    // Billing (manager's address)
    public AddressModel BillingAddress { get; set; } = new();

    // Payment (single, for the whole order)
    public CheckoutPaymentMethodModel PaymentMethod { get; set; } = new();

    // Page flags
    public bool IsCheckoutEnabled { get; set; }
    public List<string> Warnings { get; set; } = new();

    // Captcha
    public bool DisplayCaptcha { get; set; }
    public bool IsReCaptchaV3 { get; set; }
    public string ReCaptchaPublicKey { get; set; }


    //public bool ShowNoShippingMethodsMessage { get; set; }

    //public bool DisableBillingAddressCheckoutStep { get; set; }

    #endregion
}
