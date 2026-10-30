using Nop.Plugin.CW.Core.Models.Orders;

namespace Nop.Plugin.CW.Core.Factories;

public interface IPackNameOrderModelFactory
{
    Task<PackNameOrderDetailsModel> PreparePackNameOrderDetailsModelAsync(int orderId);
}
