using Nop.Web.Models.Checkout;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

/// <summary>
/// nop's payment-method model plus the one fact the view cannot infer for itself: whether payment is
/// required at all.
/// </summary>
/// <remarks>
/// An empty <see cref="CheckoutPaymentMethodModel.PaymentMethods"/> is ambiguous. It means either
/// "settlement covered the cart, there is nothing to pay" — in which case the section must render
/// nothing — or "payment is required but no payment method is usable", which is a store
/// misconfiguration that must stay loud. Only the controller knows which, so it says so here.
/// </remarks>
public partial record PackNamePaymentMethodModel : CheckoutPaymentMethodModel
{
    #region Ctor

    public PackNamePaymentMethodModel()
    {
    }

    /// <summary>
    /// Copy-constructs from whatever nop's checkout factory produced, so every field it filled in
    /// survives. Records generate a protected copy constructor; this is what it is for.
    /// </summary>
    public PackNamePaymentMethodModel(CheckoutPaymentMethodModel model) : base(model)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    /// False when the settled payable balance is zero. Defaults to true so that any model reaching
    /// the view without passing through the controller still behaves exactly as it does today.
    /// </summary>
    public bool IsPaymentWorkflowRequired { get; set; } = true;

    #endregion
}