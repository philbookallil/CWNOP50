using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Controllers;

/// <summary>
/// The page an approver opens from the approval e-mail
/// </summary>
/// <remarks>
/// <para>
/// The approver usually has no account, so the link's token is the only credential, and the page is open even when the
/// store requires sign-in or is closed. Opening the link only shows the order: accepting and declining are POSTs with
/// an anti-forgery token, so a mail scanner that follows links cannot decide an order.
/// </para>
/// <para>
/// Not gated on the Approvals switch: an order already waiting is finished whatever the switch says now.
/// </para>
/// </remarks>
[AutoValidateAntiforgeryToken]
public class OrderApprovalController : BasePublicController
{
    #region Fields

    protected readonly IOrderApprovalModelFactory _orderApprovalModelFactory;
    protected readonly IOrderApprovalService _orderApprovalService;

    #endregion

    #region Ctor

    public OrderApprovalController(IOrderApprovalModelFactory orderApprovalModelFactory,
        IOrderApprovalService orderApprovalService)
    {
        _orderApprovalModelFactory = orderApprovalModelFactory;
        _orderApprovalService = orderApprovalService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The review page: the link's token is in its address, so the browser is told not to pass the address on to any
    /// page the approver goes to from here
    /// </summary>
    protected virtual IActionResult ReviewView(object model)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        return View("~/Plugins/CW.Core/Views/OrderApproval/Review.cshtml", model);
    }

    #endregion

    #region Methods

    //available even when navigation is not allowed
    [CheckAccessPublicStore(ignore: true)]
    //available even when a store is closed
    [CheckAccessClosedStore(ignore: true)]
    public virtual async Task<IActionResult> Review(string token)
    {
        var model = await _orderApprovalModelFactory.PrepareReviewModelAsync(token);

        return ReviewView(model);
    }

    [HttpPost]
    [CheckAccessPublicStore(ignore: true)]
    [CheckAccessClosedStore(ignore: true)]
    public virtual async Task<IActionResult> Accept(string token)
    {
        var result = await _orderApprovalService.AcceptByTokenAsync(token);
        var model = await _orderApprovalModelFactory.PrepareReviewModelAsync(token, result);

        return ReviewView(model);
    }

    [HttpPost]
    [CheckAccessPublicStore(ignore: true)]
    [CheckAccessClosedStore(ignore: true)]
    public virtual async Task<IActionResult> Decline(string token, string declineReason)
    {
        var result = await _orderApprovalService.DeclineByTokenAsync(token, declineReason);
        var model = await _orderApprovalModelFactory.PrepareReviewModelAsync(token, result);

        //a decline refused for its missing reason keeps what was typed (nothing) and the form open
        model.DeclineReason = result == OrderApprovalResult.ReasonRequired ? declineReason : null;

        return ReviewView(model);
    }

    #endregion
}
