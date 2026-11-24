using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Approvals;

/// <summary>
/// An order that needs approval is held when it is placed and its e-mails are sent once, a plain order's only when it
/// is settled; a held order stays Pending until it is decided.
/// </summary>
[TestFixture]
public class OrderApprovalEventConsumerTests
{
    #region Fields

    private const int CustomerId = 100;

    private readonly OrderApprovalRequirement _required = new() { Required = true, ApproverIds = [10] };

    private Mock<ILogger> _logger = null!;
    private Mock<IOrderApprovalRuleService> _ruleService = null!;
    private Mock<IOrderApprovalService> _approvalService = null!;
    private Mock<IOrderService> _orderService = null!;
    private OrderApprovalPlacementContext _context = null!;
    private OrderApprovalEventConsumer _consumer = null!;
    private Order _order = null!;

    #endregion

    #region Utilities

    [SetUp]
    public void SetUp()
    {
        _order = new Order { Id = 1, CustomerId = CustomerId, OrderStatus = OrderStatus.Pending };

        _logger = new Mock<ILogger>();
        _ruleService = new Mock<IOrderApprovalRuleService>();
        _ruleService.Setup(mock => mock.EvaluateOrderAsync(_order)).ReturnsAsync(_required);

        _approvalService = new Mock<IOrderApprovalService>();
        _approvalService.Setup(mock => mock.HoldOrderAsync(_order, _required)).ReturnsAsync(true);

        _orderService = new Mock<IOrderService>();
        _context = new OrderApprovalPlacementContext();

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(mock => mock.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        _consumer = new OrderApprovalEventConsumer(localizationService.Object,
            _logger.Object,
            _ruleService.Object,
            _approvalService.Object,
            _orderService.Object,
            _context);
    }

    private Task PlaceAsync() => _consumer.HandleEventAsync(new OrderPlacedEvent(_order));

    private Task SettleAsync() => _consumer.HandleEventAsync(new PlainOrderSettledEvent(_order));

    private void VerifyNotified(Times times) => _approvalService.Verify(mock => mock.NotifyHoldAsync(_order, _required), times);

    private void VerifyNote(string note)
        => _orderService.Verify(mock => mock.InsertOrderNoteAsync(It.Is<OrderNote>(item => item.OrderId == _order.Id && item.Note == note && !item.DisplayToCustomer)), Times.Once);

    #endregion

    #region Placed

    [Test]
    public async Task An_order_that_needs_approval_is_held_and_its_emails_go_at_once()
    {
        await PlaceAsync();

        _approvalService.Verify(mock => mock.HoldOrderAsync(_order, _required), Times.Once);
        VerifyNotified(Times.Once());
    }

    [Test]
    public async Task An_order_that_needs_none_is_not_held()
    {
        _ruleService.Setup(mock => mock.EvaluateOrderAsync(_order)).ReturnsAsync(OrderApprovalRequirement.NotRequired);

        await PlaceAsync();

        _approvalService.Verify(mock => mock.HoldOrderAsync(It.IsAny<Order>(), It.IsAny<OrderApprovalRequirement>()), Times.Never);
        VerifyNotified(Times.Never());
    }

    [Test]
    public async Task An_order_already_held_is_not_notified_again()
    {
        _approvalService.Setup(mock => mock.HoldOrderAsync(_order, _required)).ReturnsAsync(false);

        await PlaceAsync();

        VerifyNotified(Times.Never());
    }

    [Test]
    public async Task A_plain_orders_emails_wait_until_it_is_settled_and_go_once()
    {
        _context.BeginPlainPlacement(CustomerId);
        await PlaceAsync();
        _context.EndPlainPlacement(CustomerId);

        VerifyNotified(Times.Never());

        await SettleAsync();
        await SettleAsync();

        VerifyNotified(Times.Once());
    }

    [Test]
    public async Task A_settled_order_that_was_not_held_sends_nothing()
    {
        await SettleAsync();

        VerifyNotified(Times.Never());
    }

    [Test]
    public async Task A_failed_check_leaves_the_order_unheld_and_says_so_on_the_order()
    {
        _ruleService.Setup(mock => mock.EvaluateOrderAsync(_order)).ThrowsAsync(new InvalidOperationException());

        await PlaceAsync();

        VerifyNote("Plugins.CW.Core.OrderApproval.Note.HoldFailed");
        VerifyNotified(Times.Never());
    }

    [Test]
    public async Task Emails_that_fail_leave_the_order_held_and_say_so_on_the_order()
    {
        _approvalService.Setup(mock => mock.NotifyHoldAsync(_order, _required)).ThrowsAsync(new InvalidOperationException());

        await PlaceAsync();

        VerifyNote("Plugins.CW.Core.OrderApproval.Note.NotifyFailed");
    }

    #endregion

    #region Status changes

    [TestCase(OrderStatus.Processing)]
    [TestCase(OrderStatus.Complete)]
    public async Task A_held_order_is_put_back_to_pending(OrderStatus newStatus)
    {
        _approvalService.Setup(mock => mock.IsOrderHeldAsync(_order.Id)).ReturnsAsync(true);
        _order.OrderStatus = newStatus;

        await _consumer.HandleEventAsync(new OrderStatusChangedEvent(_order, OrderStatus.Pending));

        _order.OrderStatus.Should().Be(OrderStatus.Pending);
        _orderService.Verify(mock => mock.UpdateOrderAsync(_order), Times.Once);
    }

    [Test]
    public async Task An_order_not_held_moves_on()
    {
        _order.OrderStatus = OrderStatus.Processing;

        await _consumer.HandleEventAsync(new OrderStatusChangedEvent(_order, OrderStatus.Pending));

        _order.OrderStatus.Should().Be(OrderStatus.Processing);
        _orderService.Verify(mock => mock.UpdateOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Test]
    public async Task A_held_order_that_is_cancelled_ends_its_approval()
    {
        _approvalService.Setup(mock => mock.IsOrderHeldAsync(_order.Id)).ReturnsAsync(true);
        _order.OrderStatus = OrderStatus.Cancelled;

        await _consumer.HandleEventAsync(new OrderStatusChangedEvent(_order, OrderStatus.Pending));

        _order.OrderStatus.Should().Be(OrderStatus.Cancelled);
        _approvalService.Verify(mock => mock.MarkCancelledAsync(_order.Id), Times.Once);
    }

    #endregion
}
