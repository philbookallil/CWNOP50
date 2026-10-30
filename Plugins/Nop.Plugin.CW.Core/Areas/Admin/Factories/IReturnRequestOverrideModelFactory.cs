using Nop.Core.Domain.Orders;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Orders;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface IReturnRequestOverrideModelFactory : IReturnRequestModelFactory
{
    Task<ReturnRequestModel> PrepareReturnRequestModelAsync(ReturnRequest returnRequest, bool excludeProperties);
}
