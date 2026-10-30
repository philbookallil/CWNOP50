using Nop.Web.Areas.Admin.Models.Orders;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;

/// <summary>nop's shipment row plus the packname destination it belongs to.</summary>
public partial record CWShipmentModel : ShipmentModel
{
    #region Ctor

    public CWShipmentModel()
    {
    }

    public CWShipmentModel(ShipmentModel model) : base(model)
    {
    }

    #endregion

    #region Properties

    public string Destination { get; set; }

    #endregion
}

