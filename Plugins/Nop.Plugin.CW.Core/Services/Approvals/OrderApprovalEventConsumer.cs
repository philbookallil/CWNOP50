using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Holds an order that needs approval when it is placed, and keeps a held order from moving on until it is decided
/// </summary>
/// <remarks>
/// <para>
/// The hold is written on <c>OrderPlacedEvent</c>, which nop's placement and the packname placement publish before they
/// check the order's status: a payment method that answers Paid at once would otherwise move the order to Processing.
/// The e-mails go at once for a packname order, which is settled before the event; a plain order is settled after
/// nop's placement returns, so its e-mails wait for <c>PlainOrderSettledEvent</c>.
/// </para>
/// <para>
/// A held order is kept Pending through nop's own seam: <c>SetOrderStatusAsync</c> publishes
/// <c>OrderStatusChangedEvent</c> and stops when a handler has put the status back. That covers payment, shipping and
/// completion. nop's admin <c>ChangeOrderStatus</c> publishes the event too, but sets any status the admin picks,
/// Cancelled included, without nop's cancel (stock is not returned); <c>OrderApprovalAdminGuardFilter</c> refuses it
/// on a held order. The Cancel button cancels through nop's cancel, and ends the approval here.
/// </para>
/// </remarks>
public class OrderApprovalEventConsumer : IConsumer<OrderPlacedEvent>,
    IConsumer<PlainOrderSettledEvent>,
    IConsumer<OrderStatusChangedEvent>,
    IConsumer<EntityDeletedEvent<Order>>,
    IConsumer<AdditionalTokensAddedEvent>
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly ILogger _logger;
    protected readonly IOrderApprovalRuleService _orderApprovalRuleService;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IOrderService _orderService;
    protected readonly OrderApprovalPlacementContext _placementContext;

    #endregion

    #region Ctor

    public OrderApprovalEventConsumer(ILocalizationService localizationService,
        ILogger logger,
        IOrderApprovalRuleService orderApprovalRuleService,
        IOrderApprovalService orderApprovalService,
        IOrderService orderService,
        OrderApprovalPlacementContext placementContext)
    {
        _localizationService = localizationService;
        _logger = logger;
        _orderApprovalRuleService = orderApprovalRuleService;
        _orderApprovalService = orderApprovalService;
        _orderService = orderService;
        _placementContext = placementContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Adds a note to the order, for staff only
    /// </summary>
    protected virtual async Task AddOrderNoteAsync(Order order, string note)
    {
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Sends a held order's first e-mails; a failure is logged and noted, the order stays held (Resend or Change
    /// approver on its Approval card, or the first reminder, sends them again)
    /// </summary>
    protected virtual async Task NotifyAsync(Order order, OrderApprovalRequirement requirement)
    {
        try
        {
            await _orderApprovalService.NotifyHoldAsync(order, requirement);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync($"[Approvals] Order {order.Id} is held, and its approval e-mails could not be sent.", exception);
            await AddOrderNoteAsync(order, await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.NotifyFailed"));
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Holds the order when the rules of its store say it needs approval
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(OrderPlacedEvent eventMessage)
    {
        var order = eventMessage.Order;
        if (order == null)
            return;

        //the order is placed whatever happens here: a failure leaves it unheld, so staff are told on the order
        OrderApprovalRequirement requirement;
        bool held;

        try
        {
            requirement = await _orderApprovalRuleService.EvaluateOrderAsync(order);

            if (!requirement.Required)
                return;

            held = await _orderApprovalService.HoldOrderAsync(order, requirement);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync($"[Approvals] Order {order.Id} could not be checked against the approval rules, or its approval could not be recorded: the order is not held.", exception);
            await AddOrderNoteAsync(order, await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.HoldFailed"));
            return;
        }

        if (!held)
            return;

        if (_placementContext.IsPlainPlacementRunning(order.CustomerId))
            _placementContext.SetPendingNotification(order.Id, requirement);
        else
            await NotifyAsync(order, requirement);
    }

    /// <summary>
    /// Sends a held plain order's e-mails, now that its store credit is settled
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(PlainOrderSettledEvent eventMessage)
    {
        if (eventMessage.Order == null)
            return;

        var requirement = _placementContext.TakePendingNotification(eventMessage.Order.Id);

        if (requirement != null)
            await NotifyAsync(eventMessage.Order, requirement);
    }

    /// <summary>
    /// Puts a held order back to Pending when something tries to move it on, and ends its approval when it is cancelled
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(OrderStatusChangedEvent eventMessage)
    {
        var order = eventMessage.Order;

        if (order == null || eventMessage.PreviousOrderStatus != OrderStatus.Pending || order.OrderStatus == OrderStatus.Pending)
            return;

        if (!await _orderApprovalService.IsOrderHeldAsync(order.Id))
            return;

        if (order.OrderStatus == OrderStatus.Cancelled)
        {
            await _orderApprovalService.MarkCancelledAsync(order.Id);
            return;
        }

        order.OrderStatusId = (int)OrderStatus.Pending;
        await _orderService.UpdateOrderAsync(order);
    }

    /// <summary>
    /// Ends the approval of a deleted order
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityDeletedEvent<Order> eventMessage)
    {
        if (eventMessage.Entity != null)
            await _orderApprovalService.MarkCancelledAsync(eventMessage.Entity.Id);
    }

    /// <summary>
    /// Lists the approval tokens on the admin message template page
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task HandleEventAsync(AdditionalTokensAddedEvent eventMessage)
    {
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.OrderApprovalTokens))
        {
            eventMessage.AdditionalTokens.Add("%OrderApproval.ApproverName%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.Link%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.Approvers%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.Amount%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.Reason%");
            eventMessage.AdditionalTokens.Add("%OrderApproval.DecidedBy%");
        }

        return Task.CompletedTask;
    }

    #endregion
}
