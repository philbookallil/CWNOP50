using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals.DTOs;
using Nop.Plugin.CW.Core.Services.Approvals;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Approvals;

/// <summary>
/// The list of approvers: what each approves for, its orders, and its changes
/// </summary>
[TestFixture]
public class ApproverServiceTests
{
    #region Fields

    private List<ApprovalRule> _rules = null!;
    private List<ApprovalRuleApprover> _mappings = null!;
    private List<Approver> _approvers = null!;
    private List<Order> _orders = null!;
    private List<OrderApproval> _approvals = null!;
    private List<OrderApprovalRequest> _requests = null!;
    private Action<BaseEntity>? _onUpdate;
    private ApproverService _service = null!;

    #endregion

    #region Utilities

    private static T Copy<T>(T row) where T : BaseEntity, new()
    {
        var copy = new T();

        foreach (var property in typeof(T).GetProperties().Where(property => property.CanWrite))
            property.SetValue(copy, property.GetValue(row));

        return copy;
    }

    /// <summary>
    /// Reads give copies, so only what is written with an update is seen again
    /// </summary>
    private IRepository<T> Repository<T>(List<T> rows) where T : BaseEntity, new()
    {
        var repository = new Mock<IRepository<T>>();

        repository.Setup(mock => mock.Table).Returns(() => rows.Select(Copy).ToList().AsQueryable());
        repository.Setup(mock => mock.UpdateAsync(It.IsAny<T>(), It.IsAny<bool>()))
            .Callback((T row, bool _) =>
            {
                rows[rows.FindIndex(item => item.Id == row.Id)] = Copy(row);
                _onUpdate?.Invoke(row);
            })
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.InsertAsync(It.IsAny<T>(), It.IsAny<bool>()))
            .Callback((T row, bool _) =>
            {
                row.Id = rows.Count == 0 ? 1 : rows.Max(item => item.Id) + 1;
                rows.Add(Copy(row));
            })
            .Returns(Task.CompletedTask);
        repository.Setup(mock => mock.DeleteAsync(It.IsAny<IList<T>>(), It.IsAny<bool>()))
            .Callback((IList<T> removed, bool _) => rows.RemoveAll(row => removed.Any(item => item.Id == row.Id)))
            .Returns(Task.CompletedTask);

        return repository.Object;
    }

    [SetUp]
    public void SetUp()
    {
        _rules = [new ApprovalRule { Id = 1, StoreId = 1, ScopeId = 5, Active = true }, new ApprovalRule { Id = 2, StoreId = 1, ScopeId = 6, Active = false }];
        _approvers =
        [
            new Approver { Id = 1, StoreId = 1, Name = "Ann", Email = "ann@example.com", Active = true },
            new Approver { Id = 2, StoreId = 1, Name = "Bob", Email = "bob@example.com", Active = true },
            new Approver { Id = 3, StoreId = 2, Name = "Cat", Email = "cat@example.com", Active = true },
            new Approver { Id = 4, StoreId = 1, Name = "Gone", Email = "gone@example.com", Deleted = true }
        ];
        _mappings = [];
        _orders = [];
        _approvals = [];
        _requests = [];
        _onUpdate = null;

        var rules = Repository(_rules);
        var mappings = Repository(_mappings);
        var approvers = Repository(_approvers);

        _service = new ApproverService(new ApprovalRuleService(rules, mappings, approvers), rules, mappings, approvers,
            Repository(_orders), Repository(_approvals), Repository(_requests));
    }

    private void Name(int ruleId, params int[] approverIds)
    {
        foreach (var approverId in approverIds)
            _mappings.Add(new ApprovalRuleApprover { Id = _mappings.Count + 1, ApprovalRuleId = ruleId, ApproverId = approverId });
    }

    private OrderApproval Ask(int approverId, OrderApprovalStatus status, int links = 1, bool revoked = false, bool orderDeleted = false)
    {
        var approval = new OrderApproval { Id = _approvals.Count + 1, OrderId = _orders.Count + 100, Status = status };
        _orders.Add(new Order { Id = approval.OrderId, Deleted = orderDeleted });
        _approvals.Add(approval);

        for (var link = 0; link < links; link++)
            _requests.Add(new OrderApprovalRequest { Id = _requests.Count + 1, OrderApprovalId = approval.Id, ApproverId = approverId, RevokedOnUtc = revoked ? DateTime.UtcNow : null });

        return approval;
    }

    private int[] NamedBy(int ruleId) => _mappings.Where(mapping => mapping.ApprovalRuleId == ruleId).Select(mapping => mapping.ApproverId).Order().ToArray();

    #endregion

    #region Search

    [Test]
    public async Task The_list_has_the_approvers_that_are_not_removed_by_name()
        => (await _service.SearchApproversAsync()).Select(approver => approver.Name).Should().Equal("Ann", "Bob", "Cat");

    [TestCase(2, null, "Cat")]
    [TestCase(0, "bob@", "Bob")]
    [TestCase(0, " An ", "Ann")]
    public async Task The_list_is_searched_by_store_name_and_address(int storeId, string? text, string expected)
        => (await _service.SearchApproversAsync(storeId, text)).Select(approver => approver.Name).Should().Equal(expected);

    [Test]
    public async Task An_order_waits_once_however_many_links_were_sent_and_only_while_one_works()
    {
        Ask(1, OrderApprovalStatus.Pending, links: 3);
        Ask(1, OrderApprovalStatus.Pending, revoked: true);
        Ask(1, OrderApprovalStatus.Pending, orderDeleted: true);
        Ask(1, OrderApprovalStatus.Approved).DecidedByApproverId = 1;
        Ask(1, OrderApprovalStatus.Declined).DecidedByApproverId = 1;
        Ask(1, OrderApprovalStatus.Declined).DecidedByApproverId = 1;
        Ask(1, OrderApprovalStatus.Approved).DecidedByCustomerId = 9;

        var counts = await _service.GetCountsAsync([1, 2]);

        counts[1].Should().Be(new ApproverCountsDTO(1, 1, 2));
        counts[2].Should().Be(new ApproverCountsDTO(0, 0, 0));
    }

    [Test]
    public async Task The_rules_of_an_approver_are_read_in_force_or_not()
    {
        Name(1, 1, 2);
        Name(2, 1);

        (await _service.GetRulesAsync([1])).Select(item => item.Rule.Id).Should().BeEquivalentTo([1, 2]);
    }

    #endregion

    #region Edit

    [Test]
    public async Task An_approver_takes_its_new_name_and_address()
    {
        (await _service.UpdateApproverAsync(1, " Anna ", " anna@example.com ")).Should().Be(ApproverChangeResult.Done);

        _approvers[0].Name.Should().Be("Anna");
        _approvers[0].Email.Should().Be("anna@example.com");
    }

    [TestCase("", "ann@example.com", ApproverChangeResult.Invalid)]
    [TestCase("Ann", "not an address", ApproverChangeResult.Invalid)]
    [TestCase("Ann", "BOB@example.com", ApproverChangeResult.EmailTaken)]
    [TestCase("Ann", "ann@example.com", ApproverChangeResult.Done)]
    [TestCase("Ann", "cat@example.com", ApproverChangeResult.Done, Description = "another store's")]
    [TestCase("Ann", "gone@example.com", ApproverChangeResult.Done, Description = "a removed approver's")]
    public async Task An_address_names_one_approver_of_a_store(string name, string email, ApproverChangeResult expected)
        => (await _service.UpdateApproverAsync(1, name, email)).Should().Be(expected);

    [Test]
    public async Task An_approver_named_by_its_address_keeps_being_when_only_the_address_changes()
    {
        _approvers[0].Name = "Ann@example.com";

        (await _service.UpdateApproverAsync(1, "Ann@example.com", "new@example.com")).Should().Be(ApproverChangeResult.Done);

        _approvers[0].Name.Should().Be("new@example.com");
    }

    [Test]
    public async Task A_removed_approver_is_not_changed()
    {
        (await _service.UpdateApproverAsync(4, "Back", "back@example.com")).Should().Be(ApproverChangeResult.NotFound);
        (await _service.ReplaceApproverAsync(4, "ann@example.com")).Should().Be(ApproverChangeResult.NotFound);
        (await _service.DeleteApproverAsync(4)).Should().Be(ApproverChangeResult.NotFound);
    }

    [Test]
    public async Task A_name_too_long_is_refused()
    {
        (await _service.UpdateApproverAsync(1, new string('a', 201), "ann@example.com")).Should().Be(ApproverChangeResult.Invalid);
        (await _service.ReplaceApproverAsync(1, "new@example.com", new string('a', 201))).Should().Be(ApproverChangeResult.Invalid);
    }

    #endregion

    #region Replace

    [Test]
    public async Task The_replacement_takes_every_rule_and_is_not_named_twice()
    {
        Name(1, 1, 2);
        Name(2, 1);

        (await _service.ReplaceApproverAsync(1, "bob@example.com")).Should().Be(ApproverChangeResult.Done);

        NamedBy(1).Should().Equal(2);
        NamedBy(2).Should().Equal(2);
    }

    [Test]
    public async Task An_approver_no_rule_names_has_nothing_to_replace_and_no_other_is_created()
    {
        (await _service.ReplaceApproverAsync(1, "new@example.com")).Should().Be(ApproverChangeResult.NothingToReplace);

        _approvers.Should().HaveCount(4);
    }

    [Test]
    public async Task A_replacement_the_store_does_not_have_is_created_in_the_same_store()
    {
        Name(1, 1);

        (await _service.ReplaceApproverAsync(1, "new@example.com", "New Person")).Should().Be(ApproverChangeResult.Done);

        var created = _approvers.Single(approver => approver.Email == "new@example.com");
        created.Name.Should().Be("New Person");
        created.StoreId.Should().Be(1);
        NamedBy(1).Should().Equal(created.Id);
    }

    [Test]
    public async Task Orders_already_waiting_keep_the_approver_they_asked()
    {
        Name(1, 1);
        Ask(1, OrderApprovalStatus.Pending);

        await _service.ReplaceApproverAsync(1, "bob@example.com");

        _requests.Should().ContainSingle().Which.ApproverId.Should().Be(1);
        _requests[0].RevokedOnUtc.Should().BeNull();
    }

    [TestCase("ANN@example.com", ApproverChangeResult.SameApprover)]
    [TestCase("", ApproverChangeResult.Invalid)]
    [TestCase("nobody", ApproverChangeResult.Invalid)]
    public async Task A_replacement_that_is_none_changes_nothing(string email, ApproverChangeResult expected)
    {
        Name(1, 1);

        (await _service.ReplaceApproverAsync(1, email)).Should().Be(expected);

        NamedBy(1).Should().Equal(1);
        _approvers.Should().HaveCount(4);
    }

    #endregion

    #region Remove

    [Test]
    public async Task An_approver_nothing_needs_is_removed_and_taken_out_of_the_rules_that_are_off()
    {
        Name(2, 1, 2);

        (await _service.DeleteApproverAsync(1)).Should().Be(ApproverChangeResult.Done);

        _approvers[0].Deleted.Should().BeTrue();
        NamedBy(2).Should().Equal(2);
    }

    [Test]
    public async Task An_approver_a_rule_in_force_names_is_not_removed()
    {
        Name(1, 1);

        (await _service.DeleteApproverAsync(1)).Should().Be(ApproverChangeResult.InUse);

        _approvers[0].Deleted.Should().BeFalse();
        NamedBy(1).Should().Equal(1);
    }

    [Test]
    public async Task An_approver_a_branch_names_while_it_is_removed_stays()
    {
        _onUpdate = row =>
        {
            if (row is Approver { Deleted: true })
                Name(1, 1);
        };

        (await _service.DeleteApproverAsync(1)).Should().Be(ApproverChangeResult.InUse);

        _approvers[0].Deleted.Should().BeFalse();
        NamedBy(1).Should().Equal(1);
    }

    [Test]
    public async Task An_approver_an_order_waits_for_is_not_removed()
    {
        Ask(1, OrderApprovalStatus.Pending);

        (await _service.DeleteApproverAsync(1)).Should().Be(ApproverChangeResult.InUse);
    }

    #endregion
}

/// <summary>
/// The list of the orders that need or needed approval
/// </summary>
[TestFixture]
public class OrderApprovalReportServiceTests
{
    private List<Customer> _customers = null!;
    private List<Order> _orders = null!;
    private List<OrderApproval> _approvals = null!;
    private List<OrderApprovalRequest> _requests = null!;
    private OrderApprovalReportService _service = null!;

    private static IRepository<T> Repository<T>(List<T> rows) where T : BaseEntity
    {
        var repository = new Mock<IRepository<T>>();
        repository.Setup(mock => mock.Table).Returns(() => rows.AsQueryable());

        return repository.Object;
    }

    [SetUp]
    public void SetUp()
    {
        _customers =
        [
            new Customer { Id = 1, FirstName = "Steve", LastName = "Gates", Email = "steve@example.com" },
            new Customer { Id = 2, FirstName = "Victoria", LastName = "Terces", Email = "victoria@example.com" }
        ];
        _orders = [];
        _approvals = [];
        _requests = [];

        Add(10, OrderApprovalStatus.Pending, customerId: 1, storeId: 1, approverId: 7);
        Add(11, OrderApprovalStatus.Approved, customerId: 2, storeId: 1, approverId: 7, revoked: true);
        Add(12, OrderApprovalStatus.Pending, customerId: 2, storeId: 2, approverId: 7, revoked: true);
        Add(13, OrderApprovalStatus.Pending, customerId: 1, storeId: 1, approverId: 8, deleted: true);

        _service = new OrderApprovalReportService(Repository(_customers), Repository(_orders), Repository(_approvals), Repository(_requests));
    }

    private void Add(int orderId, OrderApprovalStatus status, int customerId, int storeId, int approverId, bool revoked = false, bool deleted = false)
    {
        _orders.Add(new Order { Id = orderId, CustomOrderNumber = $"N{orderId}", CustomerId = customerId, StoreId = storeId, Deleted = deleted });
        _approvals.Add(new OrderApproval { Id = orderId + 100, OrderId = orderId, Status = status, CreatedOnUtc = new DateTime(2026, 11, orderId) });
        _requests.Add(new OrderApprovalRequest { Id = orderId, OrderApprovalId = orderId + 100, ApproverId = approverId, RevokedOnUtc = revoked ? DateTime.UtcNow : null });
    }

    private async Task<int[]> SearchAsync(OrderApprovalStatus? status = null, int storeId = 0, string? orderNumber = null, string? customer = null, int approverId = 0)
        => (await _service.SearchApprovalsAsync(status, storeId, orderNumber, customer, approverId)).Select(line => line.Order.Id).ToArray();

    [Test]
    public async Task The_list_is_newest_first_without_the_deleted_orders()
        => (await SearchAsync()).Should().Equal(12, 11, 10);

    [Test]
    public async Task The_list_is_filtered()
    {
        (await SearchAsync(OrderApprovalStatus.Pending)).Should().Equal(12, 10);
        (await SearchAsync(OrderApprovalStatus.Declined)).Should().BeEmpty();
        (await SearchAsync(storeId: 1)).Should().Equal(11, 10);
        (await SearchAsync(orderNumber: " N11 ")).Should().Equal(11);
        (await SearchAsync(customer: "steve gates")).Should().BeEmpty("the test database is case-sensitive");
        (await SearchAsync(customer: "Steve Ga")).Should().Equal(10);
        (await SearchAsync(customer: "victoria@")).Should().Equal(12, 11);
    }

    [Test]
    public async Task An_approver_finds_the_waiting_orders_with_a_working_link_and_every_decided_one()
        => (await SearchAsync(approverId: 7)).Should().Equal(11, 10);

    [Test]
    public async Task The_pages_of_a_store_that_never_used_approvals_are_not_shown()
    {
        (await _service.HasApprovalsAsync()).Should().BeTrue();

        _approvals.Clear();

        (await _service.HasApprovalsAsync()).Should().BeFalse();
    }
}
