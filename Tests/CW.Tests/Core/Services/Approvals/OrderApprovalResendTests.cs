using System.Reflection;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Approvals;

/// <summary>
/// Who a held order waits for, by their present names; changing them; and sending its links again: to those, not to the
/// ones it was changed from
/// </summary>
[TestFixture]
public class OrderApprovalResendTests
{
    private List<OrderApprovalRequest> _requests = null!;
    private List<Approver> _approvers = null!;
    private OrderApproval _approval = null!;
    private Mock<IRepository<OrderApprovalRequest>> _requestRepository = null!;
    private Mock<IOrderService> _orderService = null!;
    private Mock<IOrderApprovalWorkflowMessageService> _messageService = null!;
    private Order _order = null!;
    private bool _queues;
    private OrderApprovalService _service = null!;

    private static Mock<IRepository<T>> Repository<T>(List<T> rows) where T : BaseEntity
    {
        var repository = new Mock<IRepository<T>>();
        repository.Setup(mock => mock.Table).Returns(() => rows.AsQueryable());
        repository.Setup(mock => mock.InsertAsync(It.IsAny<T>(), It.IsAny<bool>())).Callback((T row, bool _) => rows.Add(row)).Returns(Task.CompletedTask);

        return repository;
    }

    [SetUp]
    public void SetUp()
    {
        _requests = [];
        _approvers = [new Approver { Id = 1, Name = "Ann", Email = "ann@example.com", Active = true }, new Approver { Id = 2, Name = "Bob", Email = "bob@example.com", Active = true }];

        var locker = new Mock<ILocker>();
        locker.Setup(mock => mock.PerformActionWithLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<Task>>()))
            .Returns(async (string _, TimeSpan _, Func<Task> action) =>
            {
                await action();
                return true;
            });

        _approval = new OrderApproval { Id = 5, OrderId = 1, Status = OrderApprovalStatus.Pending, LastReminderNumber = 1 };
        _requestRepository = Repository(_requests);
        _orderService = new Mock<IOrderService>();
        _orderService.Setup(mock => mock.GetOrderByIdAsync(1)).ReturnsAsync(() => _order);
        _order = new Order { Id = 1, StoreId = 1 };

        //what works when an e-mail is sent: the new link comes before the old ones stop
        _queues = true;
        _messageService = new Mock<IOrderApprovalWorkflowMessageService>();
        _messageService.Setup(mock => mock.SendApproverRequestAsync(It.IsAny<Order>(), It.IsAny<OrderApproval>(), It.IsAny<OrderApprovalRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(() => _queues ? new List<int> { 1 } : new List<int>());

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(mock => mock.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        _service = TestObject.Create<OrderApprovalService>();
        TestObject.Set(_service, "_locker", locker.Object);
        TestObject.Set(_service, "_orderService", _orderService.Object);
        TestObject.Set(_service, "_localizationService", localizationService.Object);
        TestObject.Set(_service, "_messageService", _messageService.Object);
        TestObject.Set(_service, "_logger", Mock.Of<ILogger>());
        TestObject.Set(_service, "_requestRepository", _requestRepository.Object);
        TestObject.Set(_service, "_approverRepository", Repository(_approvers).Object);
        TestObject.Set(_service, "_orderApprovalRepository", Repository(new List<OrderApproval> { _approval }).Object);

        var ruleService = new Mock<IApprovalRuleService>();
        ruleService.Setup(mock => mock.GetOrCreateApproverAsync(1, It.IsAny<string>(), null)).ReturnsAsync((int _, string email, string _) =>
        {
            var approver = _approvers.FirstOrDefault(item => item.Email == email);

            if (approver == null)
                _approvers.Add(approver = new Approver { Id = _approvers.Count + 1, Name = email, Email = email, Active = true });

            return approver;
        });
        TestObject.Set(_service, "_approvalRuleService", ruleService.Object);
    }

    private void Asked(int approverId, bool live)
        => _requests.Add(new OrderApprovalRequest { OrderApprovalId = 5, ApproverId = approverId, RevokedOnUtc = live ? null : DateTime.UtcNow });

    private async Task<(OrderApprovalResult Result, int[] Asked)> ResendAsync()
    {
        var before = _requests.ToList();
        var result = await _service.ResendAsync(1, new Customer { Email = "admin@example.com" });

        before.Should().OnlyContain(request => result != OrderApprovalResult.Done || request.RevokedOnUtc != null);

        return (result, _requests.Except(before).Select(request => request.ApproverId).Order().ToArray());
    }

    private void Sent(int approverId, string name, string email, bool live = true)
        => _requests.Add(new OrderApprovalRequest
        {
            Id = _requests.Count + 1,
            OrderApprovalId = 5,
            ApproverId = approverId,
            ApproverName = name,
            ApproverEmail = email,
            RevokedOnUtc = live ? null : DateTime.UtcNow
        });

    [Test]
    public async Task The_approvers_waited_for_have_their_present_names_each_once()
    {
        Sent(2, "Bobby", "bob@example.com");
        Sent(1, "ann@example.com", "ANN@example.com");
        Sent(1, "Old Ann", "ann@example.com");
        Sent(9, "Gone", "gone@example.com", live: false);

        (await _service.GetWaitingApproverNamesAsync(5)).Should().Equal("Ann", "Bob");
    }

    [Test]
    public async Task An_approver_whose_address_changed_or_who_is_gone_has_the_name_the_link_was_sent_with()
    {
        Sent(2, "Bobby", "old@example.com");
        Sent(9, "Zoe", "zoe@example.com");

        (await _service.GetWaitingApproverNamesAsync(5)).Should().Equal("Bobby", "Zoe");
    }

    [Test]
    public async Task Who_decides_with_a_link_has_the_present_name_and_the_address_it_was_sent_to()
    {
        Sent(1, "Old Ann", "ann@example.com");
        Sent(2, "Bobby", "old@example.com");

        (await DecidedByAsync(_requests[0])).Should().Be("Ann (ann@example.com)");
        (await DecidedByAsync(_requests[1])).Should().Be("Bobby (old@example.com)");
    }

    private async Task<OrderApprovalResult> ChangeAsync(string? emails, string? startedFrom = null)
        => await _service.ChangeApproversAsync(1, new Customer { Email = "admin@example.com" }, emails,
            startedFrom ?? string.Join(", ", await _service.GetWaitingEmailsAsync(5)));

    private void VerifySentTo(params string[] emails)
    {
        foreach (var email in emails)
            _messageService.Verify(mock => mock.SendApproverRequestAsync(_order, _approval, It.Is<OrderApprovalRequest>(request => request.ApproverEmail == email), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);

        _messageService.Verify(mock => mock.SendApproverRequestAsync(It.IsAny<Order>(), It.IsAny<OrderApproval>(), It.IsAny<OrderApprovalRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Exactly(emails.Length));
    }

    private string[] Live() => _requests.Where(request => request.RevokedOnUtc == null).Select(request => request.ApproverEmail).ToArray();

    [Test]
    public async Task One_approver_is_changed_and_the_others_keep_their_links()
    {
        Sent(1, "Ann", "ann@example.com");
        Sent(2, "Bob", "bob@example.com");

        (await ChangeAsync("ANN@example.com, new@example.com")).Should().Be(OrderApprovalResult.Done);

        _requests.Should().HaveCount(3, "Ann is not sent it again");
        Live().Should().Equal("ann@example.com", "new@example.com");
        VerifySentTo("new@example.com");
        _approval.LastReminderNumber.Should().Be(0, "somebody new was asked");
        _approval.RequestedOnUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        _orderService.Verify(mock => mock.InsertOrderNoteAsync(It.IsAny<OrderNote>()), Times.Once);
        _requestRepository.Verify(mock => mock.UpdateAsync(It.Is<IList<OrderApprovalRequest>>(removed => removed.Single().ApproverEmail == "bob@example.com"), It.IsAny<bool>()), Times.Once);
    }

    [Test]
    public async Task Every_approver_is_changed_for_a_list_of_others()
    {
        Sent(1, "Ann", "ann@example.com");
        Sent(1, "Ann", "ann@example.com");

        await ChangeAsync("bob@example.com; new@example.com\nbob@example.com");

        Live().Should().Equal("bob@example.com", "new@example.com");
    }

    [Test]
    public async Task An_approver_taken_out_loses_every_link_and_the_reminders_go_on()
    {
        Sent(1, "Ann", "ann@example.com");
        Sent(2, "Bob", "bob@example.com");
        Sent(2, "Bob", "bob@example.com");

        await ChangeAsync("ann@example.com");

        Live().Should().Equal("ann@example.com");
        _approval.LastReminderNumber.Should().Be(1);
    }

    [Test]
    public async Task An_approver_taken_out_before_and_put_back_gets_a_new_link()
    {
        Sent(1, "Ann", "ann@example.com");
        Sent(2, "Bob", "bob@example.com", live: false);

        await ChangeAsync("ann@example.com, bob@example.com");

        Live().Should().Equal("ann@example.com", "bob@example.com");
        _requests.Should().HaveCount(3);
    }

    [Test]
    public async Task The_same_approvers_change_nothing()
    {
        Sent(1, "Ann", "ann@example.com");

        (await ChangeAsync("ann@example.com")).Should().Be(OrderApprovalResult.Done);

        _requests.Should().ContainSingle();
        _orderService.Verify(mock => mock.InsertOrderNoteAsync(It.IsAny<OrderNote>()), Times.Never);
    }

    [Test]
    public async Task A_change_that_started_from_other_approvers_is_refused()
    {
        Sent(1, "Ann", "ann@example.com");

        (await ChangeAsync("ann@example.com, bob@example.com, new@example.com", startedFrom: "ann@example.com, bob@example.com"))
            .Should().Be(OrderApprovalResult.ChangedMeanwhile);

        Live().Should().Equal("ann@example.com");
        VerifySentTo();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task When_no_new_link_can_be_sent_the_old_ones_go_on(bool throws)
    {
        Sent(1, "Ann", "ann@example.com");
        _queues = false;

        if (throws)
            _messageService.Setup(mock => mock.SendApproverRequestAsync(It.IsAny<Order>(), It.IsAny<OrderApproval>(), It.IsAny<OrderApprovalRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException());

        (await ChangeAsync("bob@example.com")).Should().Be(OrderApprovalResult.Failed);
        (await _service.ResendAsync(1, new Customer { Email = "admin@example.com" })).Should().Be(OrderApprovalResult.Failed);

        Live().Should().Equal("ann@example.com");
        _approval.LastReminderNumber.Should().Be(1);
    }

    [Test]
    public async Task The_new_links_are_sent_before_the_old_ones_stop()
    {
        Sent(1, "Ann", "ann@example.com");
        var liveWhenSent = -1;
        _messageService.Setup(mock => mock.SendApproverRequestAsync(It.IsAny<Order>(), It.IsAny<OrderApproval>(), It.IsAny<OrderApprovalRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(() =>
            {
                liveWhenSent = Live().Length;
                return new List<int> { 1 };
            });

        await ChangeAsync("bob@example.com");

        liveWhenSent.Should().Be(2);
        Live().Should().Equal("bob@example.com");
    }

    [TestCase(OrderApprovalStatus.Approved, false, OrderApprovalResult.AlreadyDecided)]
    [TestCase(OrderApprovalStatus.Pending, true, OrderApprovalResult.AlreadyDecided)]
    public async Task A_decided_or_cancelled_order_is_not_changed(OrderApprovalStatus status, bool cancelled, OrderApprovalResult expected)
    {
        Sent(1, "Ann", "ann@example.com");
        _approval.Status = status;

        if (cancelled)
            _order.OrderStatus = OrderStatus.Cancelled;

        (await ChangeAsync("bob@example.com")).Should().Be(expected);

        VerifySentTo();
        _approval.Status.Should().Be(cancelled ? OrderApprovalStatus.Cancelled : status);
    }

    [Test]
    public async Task A_reminder_goes_to_the_addresses_waited_for()
    {
        Sent(1, "Ann", "old@example.com");
        Sent(2, "Bob", "bob@example.com");
        Sent(2, "Bob", "BOB@example.com");
        Sent(1, "Ann", "ann@example.com", live: false);

        await (Task)typeof(OrderApprovalService).GetMethod("SendReminderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_service, [1, 2])!;

        _requests.Skip(4).Select(request => request.ApproverEmail.ToLowerInvariant()).Should().BeEquivalentTo("old@example.com", "bob@example.com");
        _approval.LastReminderNumber.Should().Be(2);
    }

    [TestCase(null)]
    [TestCase(" , ")]
    [TestCase("ann@example.com, nobody")]
    public async Task An_order_is_never_left_without_approvers_or_with_a_bad_address(string? emails)
    {
        Sent(1, "Ann", "ann@example.com");

        (await ChangeAsync(emails)).Should().Be(OrderApprovalResult.InvalidApprover);

        Live().Should().Equal("ann@example.com");
    }

    [Test]
    public async Task The_box_starts_with_the_addresses_whose_links_work()
    {
        Sent(2, "Bob", "bob@example.com");
        Sent(1, "Ann", "ann@example.com");
        Sent(1, "Ann", "ANN@example.com");
        Sent(9, "Gone", "gone@example.com", live: false);

        (await _service.GetWaitingEmailsAsync(5)).Select(email => email.ToLowerInvariant()).Should().Equal("ann@example.com", "bob@example.com");
    }

    private Task<string> DecidedByAsync(OrderApprovalRequest request)
        => (Task<string>)typeof(OrderApprovalService).GetMethod("DecidedByAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_service, [request])!;

    [Test]
    public async Task An_approver_the_order_was_changed_from_is_not_asked_again()
    {
        Asked(1, live: false);
        Asked(2, live: true);

        var (result, asked) = await ResendAsync();

        result.Should().Be(OrderApprovalResult.Done);
        asked.Should().Equal(2);
    }

    [Test]
    public async Task When_no_link_works_whoever_was_asked_is_asked_again()
    {
        Asked(1, live: false);

        (await ResendAsync()).Asked.Should().Equal(1);
    }

    [Test]
    public async Task With_nobody_left_to_ask_the_admin_is_told()
    {
        Asked(1, live: true);
        _approvers[0].Deleted = true;

        (await ResendAsync()).Should().BeEquivalentTo((OrderApprovalResult.NobodyToAsk, Array.Empty<int>()));
    }
}
