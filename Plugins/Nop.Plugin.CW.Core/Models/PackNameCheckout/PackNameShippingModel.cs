using Nop.Web.Framework.Models;
using Nop.Web.Models.Checkout;
using Nop.Web.Models.Common;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

public record PackNameShippingModel : BaseNopModel
{
    #region Properties

    public int PackNameId { get; set; }

    public int BranchId { get; set; }

    public bool RequiresShipping { get; set; }

    public string SelectedShippingMethod { get; set; }

    public AddressModel Address { get; set; }  // currently selected address

    public CheckoutShippingMethodModel ShippingMethod { get; set; } = new();

    /// <summary>
    /// The names of the other packnames in the cart whose parcel goes to the same address by the same method; empty when
    /// this packname ships alone
    /// </summary>
    public IList<string> SharedWithPackNames { get; set; } = new List<string>();

    /// <summary>
    /// The formatted shipping charge of the shared destination, charged once for every packname in it; null when this
    /// packname ships alone
    /// </summary>
    public string SharedDestinationFee { get; set; }

    #endregion
}
