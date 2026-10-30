using Nop.Core.Domain.Orders;
using Nop.Web.Factories;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Factories
{
    public interface IReturnRequestOverrideModelFactory : IReturnRequestModelFactory
    {
        Task<SubmitReturnRequestModel.OrderItemModel> PrepareSubmitReturnRequestOrderItemModelAsync(Order order, OrderItem orderItem, int qtyForReturn);
    }
}
