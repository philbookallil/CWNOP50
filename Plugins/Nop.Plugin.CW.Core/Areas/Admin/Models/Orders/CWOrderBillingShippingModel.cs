using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;

/// <summary>
/// nop's order model plus the packname order's shipping destinations, for the billing/shipping card.
/// </summary>
public partial record CWOrderBillingShippingModel : BaseNopModel
{
    #region Ctor

    public CWOrderBillingShippingModel(OrderModel order, IList<OrderShippingDestination> destinations)
    {
        Order = order;
        Destinations = destinations;
    }

    #endregion

    #region Properties

    public OrderModel Order { get; }

    public IList<OrderShippingDestination> Destinations { get; }

    #endregion
}