using Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface IPackNameOrderModelFactory
{
    Task<AdminPackNameOrderDetailsModel> PreparePackNameOrderDetailsModelAsync(int orderId);

    /// <summary>
    /// The distinct shipping destinations on an order, ready for the admin order screen. Empty for a
    /// non-packname order, which is also how the screen decides whether it is looking at one.
    /// </summary>
    Task<IList<OrderShippingDestination>> PrepareShippingDestinationsAsync(int orderId);
}