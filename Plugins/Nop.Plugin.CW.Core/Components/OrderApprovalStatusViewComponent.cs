using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Factories;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Checkout;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Components;

/// <summary>
/// Where an order's approval stands, on the order completed page and the customer's order page. Nothing for an order
/// that never needed approval.
/// </summary>
[ViewComponent(Name = "OrderApprovalStatus")]
public class OrderApprovalStatusViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IOrderApprovalModelFactory _orderApprovalModelFactory;

    #endregion

    #region Ctor

    public OrderApprovalStatusViewComponent(IOrderApprovalModelFactory orderApprovalModelFactory)
    {
        _orderApprovalModelFactory = orderApprovalModelFactory;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var orderId = additionalData switch
        {
            CheckoutCompletedModel completed => completed.OrderId,
            OrderDetailsModel { PrintMode: false } order => order.Id,
            _ => 0
        };

        var model = await _orderApprovalModelFactory.PrepareStatusModelAsync(orderId);

        return model == null ? Content(string.Empty) : await ViewAsync(model);
    }

    #endregion
}
