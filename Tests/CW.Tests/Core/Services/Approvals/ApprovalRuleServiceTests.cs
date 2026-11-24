using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Services.Approvals;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Approvals;

/// <summary>
/// The approval rule of a branch or department: one per branch or department, its approvers named by e-mail, switched
/// off rather than lost.
/// </summary>
[TestFixture]
public class ApprovalRuleServiceTests
{
    #region Fields

    private const int StoreId = 1;
    private const int BranchId = 9;

    private List<ApprovalRule> _rules = null!;
    private List<ApprovalRuleApprover> _mappings = null!;
    private List<Approver> _approvers = null!;
    private ApprovalRuleService _service = null!;

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
    private static IRepository<T> Repository<T>(List<T> rows) where T : BaseEntity, new()
    {
        var repository = new Mock<IRepository<T>>();

        repository.Setup(mock => mock.Table).Returns(() => rows.Select(Copy).ToList().AsQueryable());

        repository.Setup(mock => mock.UpdateAsync(It.IsAny<T>(), It.IsAny<bool>()))
            .Callback((T row, bool _) => rows[rows.FindIndex(item => item.Id == row.Id)] = Copy(row))
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
        _rules = [];
        _mappings = [];
        _approvers = [];

        _service = new ApprovalRuleService(Repository(_rules), Repository(_mappings), Repository(_approvers));
    }

    private Task SaveAsync(bool required, decimal? threshold, params string[] emails)
        => _service.SaveSetupAsync(StoreId, ApprovalScopeType.Branch, BranchId, new ApprovalRuleSetup(required, threshold, emails));

    private Task<ApprovalRuleSetup> GetAsync() => _service.GetSetupAsync(ApprovalScopeType.Branch, BranchId);

    #endregion

    #region Rules

    [Test]
    public async Task A_branch_without_a_rule_requires_nothing()
    {
        var setup = await GetAsync();

        setup.Required.Should().BeFalse();
        setup.Threshold.Should().BeNull();
        setup.Emails.Should().BeEmpty();
    }

    [Test]
    public async Task What_is_saved_is_shown_again()
    {
        await SaveAsync(true, 100m, "ann@x.com", "bob@x.com");

        var setup = await GetAsync();

        setup.Required.Should().BeTrue();
        setup.Threshold.Should().Be(100m);
        setup.Emails.Should().Equal("ann@x.com", "bob@x.com");
        _rules.Should().ContainSingle().Which.StoreId.Should().Be(StoreId);
    }

    [Test]
    public async Task Saving_again_changes_the_one_rule()
    {
        await SaveAsync(true, 100m, "ann@x.com", "bob@x.com");
        await SaveAsync(true, 250m, "bob@x.com", "cat@x.com");

        var setup = await GetAsync();

        _rules.Should().ContainSingle();
        setup.Threshold.Should().Be(250m);
        setup.Emails.Should().Equal("bob@x.com", "cat@x.com");
    }

    [Test]
    public async Task A_rule_no_longer_required_is_switched_off_and_keeps_what_it_had()
    {
        await SaveAsync(true, 100m, "ann@x.com");
        await SaveAsync(false, 100m, "ann@x.com");

        var setup = await GetAsync();

        _rules.Should().ContainSingle().Which.Active.Should().BeFalse();
        setup.Required.Should().BeFalse();
        setup.Threshold.Should().Be(100m);
        setup.Emails.Should().Equal("ann@x.com");
    }

    [Test]
    public async Task A_branch_that_never_required_approval_gets_no_rule()
    {
        await SaveAsync(false, null);

        _rules.Should().BeEmpty();
    }

    [Test]
    public async Task An_invalid_address_and_an_amount_out_of_range_are_left_out([Values(-5, 1e15)] double amount)
    {
        await SaveAsync(false, (decimal)amount, "not an address", "ann@x.com");

        var setup = await GetAsync();

        setup.Threshold.Should().BeNull();
        setup.Emails.Should().Equal("ann@x.com");
    }

    [Test]
    public async Task A_branch_and_a_department_with_the_same_identifier_have_their_own_rules()
    {
        await SaveAsync(true, 100m, "ann@x.com");
        await _service.SaveSetupAsync(StoreId, ApprovalScopeType.Department, BranchId, new ApprovalRuleSetup(true, 50m, ["bob@x.com"]));

        (await GetAsync()).Emails.Should().Equal("ann@x.com");
        (await _service.GetSetupAsync(ApprovalScopeType.Department, BranchId)).Emails.Should().Equal("bob@x.com");
    }

    [Test]
    public async Task A_deleted_branch_or_department_switches_its_rule_off_and_no_other()
    {
        await SaveAsync(true, 100m, "ann@x.com");
        await _service.SaveSetupAsync(StoreId, ApprovalScopeType.Department, BranchId, new ApprovalRuleSetup(true, 50m, ["bob@x.com"]));

        var consumer = new ApprovalRuleEventConsumer(_service);

        await consumer.HandleEventAsync(new EntityDeletedEvent<Branch>(new Branch { Id = BranchId }));

        (await GetAsync()).Required.Should().BeFalse();
        (await _service.GetSetupAsync(ApprovalScopeType.Department, BranchId)).Required.Should().BeTrue();

        await consumer.HandleEventAsync(new EntityDeletedEvent<Department>(new Department { Id = BranchId }));

        (await _service.GetSetupAsync(ApprovalScopeType.Department, BranchId)).Required.Should().BeFalse();
    }

    #endregion

    #region Approvers

    [Test]
    public async Task An_approver_is_created_once_per_store_whatever_the_case_of_the_address()
    {
        await SaveAsync(true, null, "ann@x.com");
        await _service.SaveSetupAsync(StoreId, ApprovalScopeType.Department, 4, new ApprovalRuleSetup(true, null, ["ANN@x.com"]));

        _approvers.Should().ContainSingle();
        _approvers[0].Name.Should().Be("ann@x.com");

        await _service.SaveSetupAsync(StoreId + 1, ApprovalScopeType.Branch, 20, new ApprovalRuleSetup(true, null, ["ann@x.com"]));

        _approvers.Should().HaveCount(2);
    }

    [Test]
    public async Task An_approver_switched_off_is_switched_back_on_when_named_again()
    {
        _approvers.Add(new Approver { Id = 5, StoreId = StoreId, Name = "Ann", Email = "ann@x.com", Active = false });

        await SaveAsync(true, null, "ann@x.com");

        _approvers.Should().ContainSingle().Which.Active.Should().BeTrue();
        _approvers[0].Name.Should().Be("Ann");
    }

    [Test]
    public async Task A_deleted_approver_is_not_used_again()
    {
        _approvers.Add(new Approver { Id = 5, StoreId = StoreId, Name = "Ann", Email = "ann@x.com", Active = true, Deleted = true });

        await SaveAsync(true, null, "ann@x.com");

        _approvers.Should().HaveCount(2);
        _mappings.Should().ContainSingle().Which.ApproverId.Should().NotBe(5);
    }

    [Test]
    public async Task An_approver_named_by_the_address_takes_the_first_name_given_and_keeps_it()
    {
        await _service.GetOrCreateApproverAsync(StoreId, "ann@x.com");

        (await _service.GetOrCreateApproverAsync(StoreId, "ann@x.com", "Ann")).Name.Should().Be("Ann");
        (await _service.GetOrCreateApproverAsync(StoreId, "ann@x.com", "Anna")).Name.Should().Be("Ann");
        (await _service.GetOrCreateApproverAsync(StoreId, "ann@x.com")).Name.Should().Be("Ann");
    }

    #endregion
}
