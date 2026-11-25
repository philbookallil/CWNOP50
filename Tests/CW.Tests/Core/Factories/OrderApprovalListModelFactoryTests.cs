using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals.DTOs;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Stores;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The admin lists of the order approvals, as the grids show them
/// </summary>
[TestFixture]
public class OrderApprovalListModelFactoryTests
{
    private OrderApproval _approval = null!;
    private Order _order = null!;
    private OrderSettlement? _settlement;
    private List<string> _waitingFor = null!;
    private List<ApproverRuleDTO> _rules = null!;
    private ApproverCountsDTO _counts;
    private OrderApprovalStatus? _searched;
    private Mock<IStoreService> _storeService = null!;
    private OrderApprovalListModelFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _approval = new OrderApproval { Id = 5, OrderId = 1, Status = OrderApprovalStatus.Pending };
        _order = new Order { Id = 1, CustomOrderNumber = "5034", StoreId = 1, CustomerId = 4, OrderTotal = 250m };
        _settlement = null;
        _waitingFor = [];
        _rules = [];
        _counts = default;
        _searched = null;

        var reportService = new Mock<IOrderApprovalReportService>();
        reportService.Setup(mock => mock.SearchApprovalsAsync(It.IsAny<OrderApprovalStatus?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback((OrderApprovalStatus? status, int _, string _, string _, int _, int _, int _) => _searched = status)
            .ReturnsAsync(() => new PagedList<OrderApprovalLineDTO>([new OrderApprovalLineDTO { Approval = _approval, Order = _order }], 0, 10));

        var approvalService = new Mock<IOrderApprovalService>();
        approvalService.Setup(mock => mock.GetWaitingApproverNamesAsync(5)).ReturnsAsync(() => _waitingFor);
        approvalService.Setup(mock => mock.GetWaitingEmailsAsync(5)).ReturnsAsync(["ann@example.com", "zoe@example.com"]);

        var approverService = new Mock<IApproverService>();
        approverService.Setup(mock => mock.SearchApproversAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(() => new PagedList<Approver>([new Approver { Id = 7, StoreId = 1, Name = "Ann", Email = "ann@example.com", Active = true }], 0, 10));
        approverService.Setup(mock => mock.GetRulesAsync(It.IsAny<int[]>())).ReturnsAsync(() => _rules);
        approverService.Setup(mock => mock.GetCountsAsync(It.IsAny<int[]>())).ReturnsAsync(() => new Dictionary<int, ApproverCountsDTO> { [7] = _counts });

        var branchService = new Mock<IBranchService>();
        branchService.Setup(mock => mock.GetBranchByIdAsync(1)).ReturnsAsync(new Branch { Id = 1, Name = "Branch 1" });
        branchService.Setup(mock => mock.GetBranchByIdAsync(2)).ReturnsAsync(new Branch { Id = 2, Name = "Gone", Deleted = true });
        branchService.Setup(mock => mock.GetBranchByCustomerIdAsync(4)).ReturnsAsync(new Branch { Id = 1, Name = "Branch 1" });

        var multiStoreService = new Mock<IMultiStoreService>();
        multiStoreService.Setup(mock => mock.GetDepartmentByIdAsync(3)).ReturnsAsync(new Department { Id = 3, Name = "Department 3" });

        var customerService = new Mock<ICustomerService>();
        customerService.Setup(mock => mock.GetCustomerByIdAsync(4)).ReturnsAsync(new Customer { Id = 4, Email = "steve@example.com" });
        customerService.Setup(mock => mock.GetCustomerFullNameAsync(It.IsAny<Customer>())).ReturnsAsync("Steve Gates");

        var cwOrderService = new Mock<ICWOrderService>();
        cwOrderService.Setup(mock => mock.GetOrderSettlementAsync(1)).ReturnsAsync(() => _settlement);

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(mock => mock.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key.Split('.')[^1] switch
        {
            "Over" => "{0} (over {1})",
            "Off" => "{0} (off)",
            "One" => "1 reminder sent",
            "Many" => "{0} reminders sent",
            var last => last
        });

        var priceFormatter = new Mock<IPriceFormatter>();
        priceFormatter.Setup(mock => mock.FormatPriceAsync(It.IsAny<decimal>(), true, It.IsAny<Currency>(), 1, true, false))
            .ReturnsAsync((decimal price, bool _, Currency _, int _, bool _, bool _) => $"${price:0.00}");

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(mock => mock.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 1 });

        _storeService = new Mock<IStoreService>();
        _storeService.Setup(mock => mock.GetAllStoresAsync()).ReturnsAsync([new Store { Id = 1, Name = "Main" }]);

        _factory = new OrderApprovalListModelFactory(new CurrencySettings { PrimaryStoreCurrencyId = 1 },
            approverService.Object,
            branchService.Object,
            Mock.Of<ICurrencyService>(),
            customerService.Object,
            cwOrderService.Object,
            Mock.Of<IDateTimeHelper>(),
            localizationService.Object,
            multiStoreService.Object,
            reportService.Object,
            approvalService.Object,
            priceFormatter.Object,
            _storeService.Object,
            workContext.Object);
    }

    private async Task<OrderApprovalListItemModel> RowAsync()
        => (await _factory.PrepareOrderApprovalListModelAsync(new OrderApprovalSearchModel { Start = 0, Length = 10 })).Data.Single();

    private async Task<ApproverModel> ApproverAsync()
        => (await _factory.PrepareApproverListModelAsync(new ApproverSearchModel { Start = 0, Length = 10 })).Data.Single();

    private void Rule(ApprovalScopeType scopeType, int scopeId, decimal? threshold = null, bool active = true)
        => _rules.Add(new ApproverRuleDTO { ApproverId = 7, Rule = new ApprovalRule { ScopeType = scopeType, ScopeId = scopeId, Threshold = threshold, Active = active } });

    [Test]
    public async Task An_order_shows_what_it_uses_and_what_is_left_to_pay()
    {
        _settlement = new OrderSettlement { SubsidyStockAmount = 43.50m, SubsidyShippingAmount = 4.98m, CreditNoteAmount = 60m };

        var row = await RowAsync();

        row.Id.Should().Be(1);
        row.OrderNumber.Should().Be("5034");
        row.Customer.Should().Be("Steve Gates");
        row.Branch.Should().Be("Branch 1");
        row.Payable.Should().Be("$250.00");
        row.Subsidy.Should().Be("$48.48");
        row.StoreCredit.Should().Be("$60.00");
        row.IsPending.Should().BeTrue();
    }

    [Test]
    public async Task An_order_that_uses_neither_shows_neither()
    {
        var row = await RowAsync();

        row.Subsidy.Should().BeEmpty();
        row.StoreCredit.Should().BeEmpty();
    }

    [TestCase(0, "Ann, Zoe")]
    [TestCase(1, "Ann, Zoe (1 reminder sent)")]
    [TestCase(2, "Ann, Zoe (2 reminders sent)")]
    public async Task A_waiting_order_names_those_it_waits_for_with_the_reminders(int reminders, string expected)
    {
        _approval.LastReminderNumber = reminders;
        _waitingFor.AddRange(["Ann", "Zoe"]);

        var row = await RowAsync();

        row.Approvers.Should().Be(expected);
        row.WaitingEmails.Should().Be("ann@example.com, zoe@example.com");
    }

    [Test]
    public async Task A_waiting_order_nobody_can_answer_says_so()
        => (await RowAsync()).Approvers.Should().Be("Nobody");

    [Test]
    public async Task A_decided_order_names_who_decided()
    {
        _approval.Status = OrderApprovalStatus.Declined;
        _approval.DecidedBy = "Ann (ann@example.com)";

        var row = await RowAsync();

        row.Approvers.Should().Be("Ann (ann@example.com)");
        row.WaitingEmails.Should().BeEmpty();
        row.IsPending.Should().BeFalse();
    }

    [TestCase(10, OrderApprovalStatus.Pending)]
    [TestCase(30, OrderApprovalStatus.Declined)]
    [TestCase(0, null)]
    [TestCase(99, null)]
    public async Task A_status_that_is_none_searches_all(int statusId, OrderApprovalStatus? expected)
    {
        await _factory.PrepareOrderApprovalListModelAsync(new OrderApprovalSearchModel { Start = 0, Length = 10, SearchStatusId = statusId });

        _searched.Should().Be(expected);
    }

    [Test]
    public async Task The_search_starts_with_the_waiting_orders_and_offers_every_status()
    {
        var model = await _factory.PrepareOrderApprovalSearchModelAsync(new OrderApprovalSearchModel());

        model.SearchStatusId.Should().Be(10);
        model.AvailableStatuses.Select(item => item.Value).Should().Equal("0", "10", "20", "30", "40", "50");
        model.AvailableApprovers.Select(item => item.Text).Should().Equal("All", "Ann (ann@example.com)");
        model.ShowStores.Should().BeFalse("there is one store");
    }

    [Test]
    public async Task The_grids_bring_the_approvers_their_boxes_suggest_and_the_rows_their_store()
    {
        var ann = new ApproverSuggestionModel(1, "Ann", "ann@example.com");

        (await _factory.PrepareOrderApprovalListModelAsync(new OrderApprovalSearchModel { Start = 0, Length = 10 })).Suggestions.Should().Equal(ann);
        (await _factory.PrepareApproverListModelAsync(new ApproverSearchModel { Start = 0, Length = 10 })).Suggestions.Should().Equal(ann);

        (await RowAsync()).StoreId.Should().Be(1);
        (await ApproverAsync()).StoreId.Should().Be(1);
    }

    [Test]
    public async Task The_store_is_shown_when_there_are_several()
    {
        _storeService.Setup(mock => mock.GetAllStoresAsync()).ReturnsAsync([new Store { Id = 1, Name = "Main" }, new Store { Id = 2, Name = "Second" }]);

        (await _factory.PrepareApproverSearchModelAsync(new ApproverSearchModel())).ShowStores.Should().BeTrue();
    }

    [Test]
    public async Task An_approver_shows_the_branches_and_departments_that_ask_it()
    {
        Rule(ApprovalScopeType.Branch, 1, threshold: 100m);
        Rule(ApprovalScopeType.Branch, 2);
        Rule(ApprovalScopeType.Department, 3, threshold: 50m, active: false);
        Rule((ApprovalScopeType)50, 3);
        _counts = new ApproverCountsDTO(2, 9, 5);

        var approver = await ApproverAsync();

        approver.ApprovesFor.Should().Be("Branch 1 (over $100.00), Department 3 (off)");
        approver.StoreName.Should().Be("Main");
        (approver.Waiting, approver.Approved, approver.Declined).Should().Be((2, 9, 5));
        approver.HasRules.Should().BeTrue();
        approver.CanDelete.Should().BeFalse();
    }

    [TestCase(null)]
    [TestCase(0)]
    public async Task A_rule_for_every_order_shows_no_amount(int? threshold)
    {
        Rule(ApprovalScopeType.Branch, 1, threshold);

        (await ApproverAsync()).ApprovesFor.Should().Be("Branch 1");
    }

    [Test]
    public async Task An_approver_of_rules_that_are_off_can_be_removed_unless_an_order_waits()
    {
        Rule(ApprovalScopeType.Branch, 1, active: false);

        (await ApproverAsync()).CanDelete.Should().BeTrue();

        _counts = new ApproverCountsDTO(1, 0, 0);

        (await ApproverAsync()).CanDelete.Should().BeFalse();
    }
}
