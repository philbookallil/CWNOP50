using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// What the customer is told of an order's approval.
/// </summary>
[TestFixture]
public class OrderApprovalStatusModelTests
{
    #region Fields

    private const int OrderId = 1;

    private OrderApproval? _approval;
    private List<OrderApprovalRequest> _requests = null!;
    private OrderSettlement? _settlement;
    private OrderApprovalModelFactory _factory = null!;

    #endregion

    #region Utilities

    [SetUp]
    public void SetUp()
    {
        _approval = new OrderApproval { Id = 5, OrderId = OrderId, Status = OrderApprovalStatus.Pending };
        _requests = [];
        _settlement = null;

        var approvalService = new Mock<IOrderApprovalService>();
        approvalService.Setup(mock => mock.GetOrderApprovalByOrderIdAsync(OrderId)).ReturnsAsync(() => _approval);
        approvalService.Setup(mock => mock.GetRequestsAsync(5)).ReturnsAsync(() => _requests);

        var cwOrderService = new Mock<ICWOrderService>();
        cwOrderService.Setup(mock => mock.GetOrderSettlementAsync(OrderId)).ReturnsAsync(() => _settlement);

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(mock => mock.GetResourceAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => key.Split('.').Last() + " {0}");

        _factory = new OrderApprovalModelFactory(new Mock<IAddressService>().Object,
            new Mock<ICurrencyService>().Object,
            new Mock<ICustomerService>().Object,
            cwOrderService.Object,
            new Mock<IDateTimeHelper>().Object,
            localizationService.Object,
            approvalService.Object,
            new Mock<IOrderService>().Object,
            new Mock<IPackNameService>().Object,
            new Mock<IPriceFormatter>().Object,
            new Mock<IProductService>().Object,
            new Mock<IWorkContext>().Object);
    }

    private Task<OrderApprovalStatusModel> PrepareAsync() => _factory.PrepareStatusModelAsync(OrderId);

    #endregion

    #region Tests

    [Test]
    public async Task An_order_that_never_needed_approval_or_was_cancelled_while_it_waited_says_nothing()
    {
        _approval!.Status = OrderApprovalStatus.Cancelled;
        (await PrepareAsync()).Should().BeNull();

        _approval = null;
        (await PrepareAsync()).Should().BeNull();
    }

    [Test]
    public async Task An_order_that_waits_names_the_approvers_whose_links_work_each_once()
    {
        //latest first: Ann was still named by the address when the first link was sent
        _requests.Add(new OrderApprovalRequest { ApproverId = 2, ApproverName = "Bob" });
        _requests.Add(new OrderApprovalRequest { ApproverId = 1, ApproverName = "Ann" });
        _requests.Add(new OrderApprovalRequest { ApproverId = 1, ApproverName = "ann@x.com" });
        _requests.Add(new OrderApprovalRequest { ApproverId = 4, ApproverName = "ann" });
        _requests.Add(new OrderApprovalRequest { ApproverId = 3, ApproverName = "Cat", RevokedOnUtc = DateTime.UtcNow });

        var model = await PrepareAsync();

        model.StatusClass.Should().Be("pending");
        model.Message.Should().Be("WaitingBy Ann, Bob");
        model.ReservedMessage.Should().BeNull();
    }

    [Test]
    public async Task An_order_that_waits_for_nobody_says_only_that_it_waits()
        => (await PrepareAsync()).Message.Should().StartWith("Waiting ");

    [Test]
    public async Task An_order_subsidy_or_store_credit_paid_for_says_they_come_back()
    {
        _settlement = new OrderSettlement { OrderId = OrderId };

        (await PrepareAsync()).ReservedMessage.Should().StartWith("Reserved");
    }

    [TestCase(OrderApprovalStatus.Approved, "approved", "Approved Ann", null)]
    [TestCase(OrderApprovalStatus.Declined, "declined", "Declined Ann", "Too much")]
    [TestCase(OrderApprovalStatus.Expired, "expired", "Expired {0}", "Too much")]
    public async Task A_decided_order_says_how(OrderApprovalStatus status, string statusClass, string message, string? reason)
    {
        _approval!.Status = status;
        _approval.DecidedBy = "Ann";
        _approval.DecisionComment = reason;

        var model = await PrepareAsync();

        model.StatusClass.Should().Be(statusClass);
        model.Message.Should().Be(message);
        model.Reason.Should().Be(reason);
        model.ReservedMessage.Should().BeNull();
    }

    #endregion
}
