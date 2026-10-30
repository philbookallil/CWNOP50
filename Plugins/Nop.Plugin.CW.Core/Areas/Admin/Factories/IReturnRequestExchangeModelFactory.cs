using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Web.Areas.Admin.Models.Orders;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface IReturnRequestExchangeModelFactory
{
    /// <summary>
    /// Populates the CW exchange detail on the admin return request edit screen
    /// </summary>
    /// <param name="model">The admin return request model to populate</param>
    /// <param name="requestData">nop's own return request model for this row</param>
    Task PrepareExchangeRequestModelAsync(CWReturnRequestModel model, ReturnRequestModel requestData);
}