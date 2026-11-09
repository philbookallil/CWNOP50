using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Orders;
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

    /// <summary>
    /// The requested actions staff may choose for a return line with this reason: those the return's store offers for
    /// the reason, and the line's saved action while its saved reason is chosen. The first item is "select an action"
    /// (value 0)
    /// </summary>
    /// <param name="returnRequest">The return; its store's rules apply</param>
    /// <param name="reasonId">The reason chosen on the page</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the dropdown items</returns>
    Task<IList<SelectListItem>> PrepareAvailableReturnActionsAsync(ReturnRequest returnRequest, int reasonId);
}