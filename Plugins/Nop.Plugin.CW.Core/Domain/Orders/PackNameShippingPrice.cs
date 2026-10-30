using Nop.Core.Domain.Shipping;

namespace Nop.Plugin.CW.Core.Domain.Orders;

public class PackNameShippingPrice
{
    #region Ctor

    public PackNameShippingPrice()
    {
        PackNameIds = new List<int>();
    }

    #endregion

    #region Properties

    public List<int> PackNameIds { get; set; }

    public int AddressId { get; set; }

    public decimal? Price { get; set; }

    public decimal? PriceInclTaxt { get; set; }

    public decimal? PriceExclTaxt { get; set; }

    public ShippingOption ShippingOption { get; set; }

    #endregion
}
