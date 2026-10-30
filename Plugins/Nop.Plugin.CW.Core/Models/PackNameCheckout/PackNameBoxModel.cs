using Nop.Web.Framework.Models;
using Nop.Web.Models.Common;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

public record PackNameBoxModel : BaseNopModel
{
    #region Properties

    // The first cart item for this PackName (used for PackName metadata)
    public PackNameShoppingCartModel.PackNameShoppingCartItemModel Item { get; set; } = new();

    public PackNameShippingModel ShippingData { get; set; }

    public List<AddressModel> AvailableAddresses { get; set; } = new();

    public int BranchDefaultAddressId { get; set; }

    public bool CanAddAddress { get; set; }

    /// <summary>
    /// Formatted shipping subsidy amount for this packname, e.g. "−$8.50".
    /// Null when no shipping subsidy applies or IncludeShippingOnSubsidy is off.
    /// </summary>
    public string? ShippingSubsidyDisplay { get; set; }
    
    #endregion
}