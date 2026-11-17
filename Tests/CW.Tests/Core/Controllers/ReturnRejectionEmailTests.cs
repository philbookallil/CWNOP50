using System.Reflection;
using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Areas.Admin.Controllers;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The customer is told once when their return is rejected or cancelled, with the decision note, and a failure to
/// queue the e-mail never fails the save
/// </summary>
[TestFixture]
public class ReturnRejectionEmailTests
{
    private Mock<IExchangesLogic> _exchangesLogic = null!;
    private ReturnsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _exchangesLogic = new Mock<IExchangesLogic>();
        _exchangesLogic.Setup(logic => logic.SendReturnRejectedNotificationAsync(It.IsAny<ReturnRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new List<int> { 1 });

        _controller = TestObject.Create<ReturnsController>();
        TestObject.Set(_controller, "_exchangesLogic", _exchangesLogic.Object);
        TestObject.Set(_controller, "_logger", Mock.Of<ILogger>());
    }

    private Task NotifyAsync(ReturnRequestStatus previous, ReturnRequestStatus now, string note = "Worn") =>
        TestObject.InvokeAsync(_controller, "NotifyCustomerIfRejectedAsync",
            new ReturnRequest { Id = 9, ReturnRequestStatus = now }, previous, note);

    [TestCase(ReturnRequestStatus.Pending, ReturnRequestStatus.RequestRejected)]
    [TestCase(ReturnRequestStatus.Received, ReturnRequestStatus.Cancelled)]
    public async Task A_save_that_rejects_or_cancels_the_return_tells_the_customer(ReturnRequestStatus previous, ReturnRequestStatus now)
    {
        await NotifyAsync(previous, now);

        _exchangesLogic.Verify(logic => logic.SendReturnRejectedNotificationAsync(It.Is<ReturnRequest>(request => request.Id == 9), "Worn"), Times.Once);
    }

    [TestCase(ReturnRequestStatus.RequestRejected, ReturnRequestStatus.RequestRejected)]
    [TestCase(ReturnRequestStatus.RequestRejected, ReturnRequestStatus.Cancelled)]
    [TestCase(ReturnRequestStatus.Pending, ReturnRequestStatus.ReturnAuthorized)]
    [TestCase(ReturnRequestStatus.Pending, ReturnRequestStatus.Received)]
    public async Task Any_other_save_sends_nothing(ReturnRequestStatus previous, ReturnRequestStatus now)
    {
        await NotifyAsync(previous, now);

        _exchangesLogic.Verify(logic => logic.SendReturnRejectedNotificationAsync(It.IsAny<ReturnRequest>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task An_e_mail_that_cannot_be_queued_does_not_fail_the_save()
    {
        _exchangesLogic.Setup(logic => logic.SendReturnRejectedNotificationAsync(It.IsAny<ReturnRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("no e-mail account"));

        var notify = () => NotifyAsync(ReturnRequestStatus.Pending, ReturnRequestStatus.RequestRejected);

        await notify.Should().NotThrowAsync();
    }

    private Task<bool> UnitsStillFreeAsync(int available, int returned = 2)
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderItemByIdAsync(100)).ReturnsAsync(new OrderItem { Id = 100, OrderId = 10, Quantity = 3 });
        var returnRequestService = new Mock<IReturnRequestService>();
        returnRequestService.Setup(service => service.GetReturnRequestAvailabilityAsync(10)).ReturnsAsync(new ReturnRequestAvailability
        {
            ReturnableOrderItems = [new ReturnableOrderItem { OrderItem = new OrderItem { Id = 100 }, AvailableQuantityForReturn = available }]
        });
        TestObject.Set(_controller, "_orderService", orderService.Object);
        TestObject.Set(_controller, "_returnRequestService", returnRequestService.Object);

        return TestObject.InvokeAsync<bool>(_controller, "AreReturnedUnitsStillFreeAsync",
            new ReturnRequest { Id = 9, OrderItemId = 100, Quantity = returned, ReturnRequestStatus = ReturnRequestStatus.RequestRejected });
    }

    /// <summary>A rejected return can be re-opened while nothing else has taken its units.</summary>
    [Test]
    public async Task A_rejected_return_whose_units_are_still_free_can_be_reopened()
    {
        (await UnitsStillFreeAsync(available: 3)).Should().BeTrue();
    }

    /// <summary>Once another return has taken the units, re-opening would give the customer their value twice.</summary>
    [Test]
    public async Task A_rejected_return_whose_units_were_returned_again_cannot_be_reopened()
    {
        (await UnitsStillFreeAsync(available: 1)).Should().BeFalse();
    }

    private static ReturnLineDisposition? LineState(ReturnRequestStatus status) =>
        (ReturnLineDisposition?)typeof(ReturnsController).GetMethod("MapStatusToLineState", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [(int)status]);

    /// <summary>Setting a decided return back to a step on the way re-opens its case line.</summary>
    [TestCase(ReturnRequestStatus.Pending, ReturnLineDisposition.Pending)]
    [TestCase(ReturnRequestStatus.Received, ReturnLineDisposition.Pending)]
    [TestCase(ReturnRequestStatus.ReturnAuthorized, ReturnLineDisposition.Accepted)]
    [TestCase(ReturnRequestStatus.RequestRejected, ReturnLineDisposition.Rejected)]
    [TestCase(ReturnRequestStatus.Cancelled, ReturnLineDisposition.Rejected)]
    public void Each_status_puts_the_case_line_in_its_state(ReturnRequestStatus status, ReturnLineDisposition expected)
    {
        LineState(status).Should().Be(expected);
    }

    /// <summary>Refunded and Repaired are labels: they leave the case line as it is.</summary>
    [TestCase(ReturnRequestStatus.ItemsRefunded)]
    [TestCase(ReturnRequestStatus.ItemsRepaired)]
    public void Refunded_and_repaired_leave_the_case_line_alone(ReturnRequestStatus status)
    {
        LineState(status).Should().BeNull();
    }
}
