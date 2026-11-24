using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Approvals;

/// <summary>
/// Which placed orders wait for approval: on account, with something to pay, of a customer whose branch or department
/// has a rule, and without an exchange replacement.
/// </summary>
[TestFixture]
public class OrderApprovalRuleServiceTests
{
    #region Fields

    private const int StoreId = 1;
    private const int CustomerId = 100;
    private const int BranchId = 9;
    private const int DepartmentId = 4;
    private const string OnAccount = "Payments.CheckMoneyOrder";

    private Dictionary<CWFeature, bool> _features = null!;
    private Branch? _branch;
    private List<PackName> _packNames = null!;
    private List<Department> _departments = null!;
    private List<ApprovalRule> _rules = null!;
    private List<ApprovalRuleApprover> _mappings = null!;
    private List<Approver> _approvers = null!;
    private List<OrderItem> _orderItems = null!;
    private List<ReturnRequestItem> _returnRequestItems = null!;
    private OrderApprovalRuleService _service = null!;

    #endregion

    #region Utilities

    private static IRepository<T> Repository<T>(Func<IEnumerable<T>> items) where T : Nop.Core.BaseEntity
    {
        var repository = new Mock<IRepository<T>>();
        repository.Setup(mock => mock.Table).Returns(() => items().AsQueryable());

        return repository.Object;
    }

    private static Order NewOrder(decimal total = 250m, string? paymentMethod = OnAccount)
        => new() { Id = 1, StoreId = StoreId, CustomerId = CustomerId, OrderTotal = total, PaymentMethodSystemName = paymentMethod };

    private void AddRule(ApprovalScopeType scopeType, int scopeId, decimal? threshold = 100m, int approverId = 10, int storeId = StoreId)
    {
        var rule = new ApprovalRule { Id = _rules.Count + 1, StoreId = storeId, ScopeType = scopeType, ScopeId = scopeId, Threshold = threshold, Active = true };
        _rules.Add(rule);
        _mappings.Add(new ApprovalRuleApprover { ApprovalRuleId = rule.Id, ApproverId = approverId });
    }

    [SetUp]
    public void SetUp()
    {
        _features = new Dictionary<CWFeature, bool> { [CWFeature.Approvals] = true, [CWFeature.PackName] = true };
        _branch = new Branch { Id = BranchId, StoreId = StoreId };
        _packNames = [new PackName { Id = 55, CustomerId = CustomerId, DepartmentId = DepartmentId }];
        _departments = [new Department { Id = DepartmentId, StoreId = StoreId }];
        _rules = [];
        _mappings = [];
        _approvers = [new Approver { Id = 10, Active = true }, new Approver { Id = 11, Active = true }];
        _orderItems = [new OrderItem { Id = 70, OrderId = 1 }, new OrderItem { Id = 71, OrderId = 1 }];
        _returnRequestItems = [];

        var branchService = new Mock<IBranchService>();
        branchService.Setup(mock => mock.GetBranchByCustomerIdAsync(CustomerId)).ReturnsAsync(() => _branch);

        var featureService = new Mock<ICWFeatureService>();
        featureService.Setup(mock => mock.IsEnabledAsync(It.IsAny<CWFeature>(), StoreId))
            .ReturnsAsync((CWFeature feature, int? _) => _features.GetValueOrDefault(feature));

        var exchangeService = new Mock<IExchangeService>();
        exchangeService.Setup(mock => mock.GetReturnRequestItemsForOrderItemsAsync(It.IsAny<int[]>())).ReturnsAsync(() => _returnRequestItems);

        var orderService = new Mock<IOrderService>();
        orderService.Setup(mock => mock.GetOrderItemsAsync(1, It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<int>())).ReturnsAsync(() => _orderItems);

        var settingService = new Mock<ISettingService>();
        settingService.Setup(mock => mock.LoadSettingAsync<MultiStoreSettings>(StoreId)).ReturnsAsync(new MultiStoreSettings());

        _service = new OrderApprovalRuleService(branchService.Object,
            featureService.Object,
            exchangeService.Object,
            new Mock<ILogger>().Object,
            orderService.Object,
            Repository(() => _rules),
            Repository(() => _mappings),
            Repository(() => _approvers),
            Repository(() => _departments),
            Repository(() => _packNames),
            settingService.Object);
    }

    private async Task<bool> IsHeldAsync(Order order) => (await _service.EvaluateOrderAsync(order)).Required;

    #endregion

    #region Whose order

    [Test]
    public async Task The_branch_of_the_customer_who_placed_the_order_decides()
    {
        AddRule(ApprovalScopeType.Branch, BranchId);

        (await IsHeldAsync(NewOrder())).Should().BeTrue();

        _branch = new Branch { Id = BranchId + 1, StoreId = StoreId };
        (await IsHeldAsync(NewOrder())).Should().BeFalse();

        _branch = null;
        (await IsHeldAsync(NewOrder())).Should().BeFalse();
    }

    [Test]
    public async Task The_department_of_the_customers_own_packname_decides()
    {
        AddRule(ApprovalScopeType.Department, DepartmentId);

        (await IsHeldAsync(NewOrder())).Should().BeTrue();

        _packNames[0].CustomerId = CustomerId + 1;
        (await IsHeldAsync(NewOrder())).Should().BeFalse();
    }

    [Test]
    public async Task A_deleted_packname_or_department_and_another_stores_department_count_for_nothing()
    {
        AddRule(ApprovalScopeType.Department, DepartmentId);

        _packNames[0].Deleted = true;
        (await IsHeldAsync(NewOrder())).Should().BeFalse();

        _packNames[0].Deleted = false;
        _departments[0].Deleted = true;
        (await IsHeldAsync(NewOrder())).Should().BeFalse();

        _departments[0].Deleted = false;
        _departments[0].StoreId = StoreId + 1;
        (await IsHeldAsync(NewOrder())).Should().BeFalse();
    }

    [Test]
    public async Task With_packnames_off_the_department_is_not_read_and_the_branch_still_is()
    {
        _features[CWFeature.PackName] = false;

        AddRule(ApprovalScopeType.Department, DepartmentId);
        (await IsHeldAsync(NewOrder())).Should().BeFalse();

        AddRule(ApprovalScopeType.Branch, BranchId);
        (await IsHeldAsync(NewOrder())).Should().BeTrue();
    }

    [Test]
    public async Task The_branch_and_the_department_are_both_asked()
    {
        AddRule(ApprovalScopeType.Branch, BranchId, approverId: 10);
        AddRule(ApprovalScopeType.Department, DepartmentId, approverId: 11);

        (await _service.EvaluateOrderAsync(NewOrder())).ApproverIds.Should().Equal(10, 11);
    }

    #endregion

    #region Which order

    [Test]
    public async Task Nothing_is_held_while_the_switch_is_off()
    {
        AddRule(ApprovalScopeType.Branch, BranchId);
        _features[CWFeature.Approvals] = false;

        (await IsHeldAsync(NewOrder())).Should().BeFalse();
    }

    [TestCase(OnAccount, true)]
    [TestCase("payments.checkmoneyorder", true, TestName = "The method's name is compared without case")]
    [TestCase("Payments.NABGateway", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public async Task Only_a_listed_payment_method_waits(string? paymentMethod, bool held)
    {
        AddRule(ApprovalScopeType.Branch, BranchId);

        (await IsHeldAsync(NewOrder(paymentMethod: paymentMethod))).Should().Be(held);
    }

    [Test]
    public async Task An_order_with_nothing_left_to_pay_is_not_held_even_by_a_rule_for_every_order()
    {
        AddRule(ApprovalScopeType.Branch, BranchId, threshold: null);

        (await IsHeldAsync(NewOrder(total: 0m))).Should().BeFalse();
        (await IsHeldAsync(NewOrder(total: 0.01m))).Should().BeTrue();
    }

    [Test]
    public async Task The_order_total_is_what_the_threshold_is_compared_with()
    {
        AddRule(ApprovalScopeType.Branch, BranchId, threshold: 100m);

        (await IsHeldAsync(NewOrder(total: 100m))).Should().BeFalse();

        var requirement = await _service.EvaluateOrderAsync(NewOrder(total: 100.01m));
        requirement.Required.Should().BeTrue();
        requirement.Amount.Should().Be(100.01m);
    }

    [Test]
    public async Task Another_stores_rule_holds_nothing()
    {
        AddRule(ApprovalScopeType.Branch, BranchId, storeId: StoreId + 1);

        (await IsHeldAsync(NewOrder())).Should().BeFalse();
    }

    [Test]
    public async Task A_rule_whose_approvers_are_switched_off_or_deleted_holds_nothing()
    {
        AddRule(ApprovalScopeType.Branch, BranchId);

        _approvers[0].Active = false;
        (await IsHeldAsync(NewOrder())).Should().BeFalse();

        _approvers[0].Active = true;
        _approvers[0].Deleted = true;
        (await IsHeldAsync(NewOrder())).Should().BeFalse();
    }

    [Test]
    public async Task An_order_with_a_replacement_is_not_held_whatever_else_it_has()
    {
        AddRule(ApprovalScopeType.Branch, BranchId);

        (await IsHeldAsync(NewOrder())).Should().BeTrue();

        _returnRequestItems = [new ReturnRequestItem { OrderItemId = 70 }];
        (await IsHeldAsync(NewOrder())).Should().BeFalse();

        _returnRequestItems.Add(new ReturnRequestItem { OrderItemId = 71 });
        (await IsHeldAsync(NewOrder())).Should().BeFalse();
    }

    #endregion
}
