using Nop.Core;
using Nop.Core.Domain.Shipping;

namespace Nop.Plugin.CW.Core.Domain.Orders;

public class OrderShipping : BaseEntity
{
    /// <summary>
    /// Gets or sets the order identifier
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Gets or sets the packName identifier
    /// </summary>
    public int? PackNameId { get; set; }

    /// <summary>
    /// Gets or sets the shipping address identifier
    /// </summary>
    public int? ShippingAddressId { get; set; }

    /// <summary>
    /// Gets or sets the pickup address identifier
    /// </summary>
    public int? PickupAddressId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a customer chose "pick up in store" shipping option
    /// </summary>
    public bool PickupInStore { get; set; }

    /// <summary>
    /// Gets or sets the shipping status identifier
    /// </summary>
    public int ShippingStatusId { get; set; }

    /// <summary>
    /// Gets or sets the shipping method
    /// </summary>
    public string ShippingMethod { get; set; }

    /// <summary>
    /// Gets or sets the shipping rate computation method identifier or the pickup point provider identifier (if PickupInStore is true)
    /// </summary>
    public string ShippingRateComputationMethodSystemName { get; set; }

    /// <summary>
    /// Gets or sets the shipping (include tax)
    /// </summary>
    public decimal ShippingInclTax { get; set; }

    /// <summary>
    /// Gets or sets the shipping (exclude tax)
    /// </summary>
    public decimal ShippingExclTax { get; set; }

    #region Custom properties

    /// <summary>
    /// Gets or sets the shipping status
    /// </summary>
    public ShippingStatus ShippingStatus
    {
        get => (ShippingStatus)ShippingStatusId;
        set => ShippingStatusId = (int)value;
    }

    #endregion
}
