using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Messages;
using NUnit.Framework;

namespace CW.Tests.Core.BusinessLogic;

/// <summary>
/// The approval e-mail of a return is remembered on its line, and an undone approval withdraws it while the queue has
/// not sent it yet
/// </summary>
[TestFixture]
public class ReturnApprovalEmailTests
{
    private const int ReturnRequestId = 40;
    private const int QueuedEmailId = 900;

    private ExchangesLogic _logic = null!;
    private ReturnRequestItem _item = null!;
    private Mock<IExchangeService> _exchangeService = null!;
    private Mock<IQueuedEmailService> _queuedEmailService = null!;

    [SetUp]
    public void SetUp()
    {
        _item = new ReturnRequestItem { Id = 7, ReturnRequestId = ReturnRequestId };

        _exchangeService = new Mock<IExchangeService>();
        _exchangeService.Setup(service => service.GetReturnRequestItemAsync(ReturnRequestId)).ReturnsAsync(() => _item);
        _exchangeService.Setup(service => service.UpdateReturnRequestItemAsync(It.IsAny<ReturnRequestItem>())).Returns(Task.CompletedTask);

        _queuedEmailService = new Mock<IQueuedEmailService>();

        _logic = TestObject.Create<ExchangesLogic>();
        TestObject.Set(_logic, "_exchangeService", _exchangeService.Object);
        TestObject.Set(_logic, "_queuedEmailService", _queuedEmailService.Object);
    }

    [Test]
    public async Task The_queued_approval_email_is_remembered_on_the_line()
    {
        await TestObject.InvokeAsync(_logic, "RecordApprovalEmailAsync", _item, new List<int> { QueuedEmailId });

        _item.ApprovalQueuedEmailId.Should().Be(QueuedEmailId);
        _exchangeService.Verify(service => service.UpdateReturnRequestItemAsync(_item), Times.Once);
    }

    [Test]
    public async Task Nothing_is_remembered_when_no_email_was_queued()
    {
        //an inactive template queues nothing
        await TestObject.InvokeAsync(_logic, "RecordApprovalEmailAsync", _item, new List<int>());

        _item.ApprovalQueuedEmailId.Should().BeNull();
        _exchangeService.Verify(service => service.UpdateReturnRequestItemAsync(It.IsAny<ReturnRequestItem>()), Times.Never);
    }

    [Test]
    public async Task An_undo_deletes_the_approval_email_the_queue_has_not_sent()
    {
        var queuedEmail = new QueuedEmail { Id = QueuedEmailId, SentOnUtc = null };
        _item.ApprovalQueuedEmailId = QueuedEmailId;
        _queuedEmailService.Setup(service => service.GetQueuedEmailByIdAsync(QueuedEmailId)).ReturnsAsync(queuedEmail);

        await TestObject.InvokeAsync(_logic, "WithdrawApprovalEmailAsync", new ReturnRequest { Id = ReturnRequestId });

        _queuedEmailService.Verify(service => service.DeleteQueuedEmailAsync(queuedEmail), Times.Once);
        _item.ApprovalQueuedEmailId.Should().BeNull("a later approval records its own e-mail");
    }

    [Test]
    public async Task An_undo_keeps_an_approval_email_already_sent()
    {
        _item.ApprovalQueuedEmailId = QueuedEmailId;
        _queuedEmailService.Setup(service => service.GetQueuedEmailByIdAsync(QueuedEmailId))
            .ReturnsAsync(new QueuedEmail { Id = QueuedEmailId, SentOnUtc = DateTime.UtcNow });

        await TestObject.InvokeAsync(_logic, "WithdrawApprovalEmailAsync", new ReturnRequest { Id = ReturnRequestId });

        _queuedEmailService.Verify(service => service.DeleteQueuedEmailAsync(It.IsAny<QueuedEmail>()), Times.Never);
        _item.ApprovalQueuedEmailId.Should().BeNull();
    }

    [Test]
    public async Task An_undo_with_no_approval_email_touches_nothing()
    {
        await TestObject.InvokeAsync(_logic, "WithdrawApprovalEmailAsync", new ReturnRequest { Id = ReturnRequestId });

        _queuedEmailService.Verify(service => service.GetQueuedEmailByIdAsync(It.IsAny<int>()), Times.Never);
        _exchangeService.Verify(service => service.UpdateReturnRequestItemAsync(It.IsAny<ReturnRequestItem>()), Times.Never);
    }
}
