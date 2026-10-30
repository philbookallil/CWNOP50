using Nop.Plugin.CW.Core.Models.Orders;
using Nop.Web.Framework.Models;
using Nop.Web.Models.Checkout;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

/// <summary>
/// The page a packname order lands on once it is placed: nop's thank-you block, then the order's packnames
/// with their destinations and lines, then what the subsidy and store credit paid and what is left to pay.
/// </summary>
public record PackNameCheckoutCompletedModel : BaseNopModel
{
    public CheckoutCompletedModel Completed { get; set; } = new();

    public PackNameOrderDetailsModel PackNameDetails { get; set; } = new();

    /// <summary>
    /// The payment method's fee, formatted; empty when the order carries none.
    /// </summary>
    public string PaymentMethodAdditionalFee { get; set; }

    /// <summary>
    /// The order-total discount, formatted; empty when the order carries none.
    /// </summary>
    public string OrderTotalDiscount { get; set; }
}