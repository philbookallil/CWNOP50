using AwesomeAssertions;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Approvals;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// Whether an order needs approval, from the rules of its store: which rules cover the customer who placed it, which
/// thresholds it is over, and whom to ask.
/// </summary>
/// <remarks>
/// A rule covers an order through its customer: their branch, or a department of their own packname. The amount is
/// compared strictly: "over $X". Every matching rule's approvers are asked, each once.
/// </remarks>
[TestFixture]
public class OrderApprovalRuleHelperTests
{
    #region Utilities

    private static ApprovalRule Rule(int id, ApprovalScopeType scopeType, int scopeId, decimal? threshold = null, bool active = true)
        => new()
        {
            Id = id,
            ScopeType = scopeType,
            ScopeId = scopeId,
            Threshold = threshold,
            Active = active
        };

    private static OrderApprovalRequirement Match(IEnumerable<ApprovalRule> rules,
        int? branchId = null,
        int[]? departmentIds = null,
        decimal amount = 50m,
        Dictionary<int, IReadOnlyList<int>>? approvers = null)
    {
        var ruleList = rules.ToList();
        approvers ??= ruleList.ToDictionary(rule => rule.Id, rule => (IReadOnlyList<int>)[rule.Id * 10]);

        return OrderApprovalRuleHelper.Match(ruleList, approvers, branchId, departmentIds ?? [], amount);
    }

    #endregion

    #region Scopes

    [Test]
    public void A_branch_rule_covers_the_orders_of_the_branch_and_no_other()
    {
        Match([Rule(1, ApprovalScopeType.Branch, 2)], branchId: 2).Required.Should().BeTrue();
        Match([Rule(1, ApprovalScopeType.Branch, 2)], branchId: 3).Required.Should().BeFalse();
    }

    [Test]
    public void A_department_rule_covers_the_orders_of_the_department_and_no_other()
    {
        Match([Rule(1, ApprovalScopeType.Department, 4)], departmentIds: [4]).Required.Should().BeTrue();
        Match([Rule(1, ApprovalScopeType.Department, 4)], departmentIds: [5]).Required.Should().BeFalse();
    }

    [Test]
    public void One_of_several_departments_is_enough()
        => Match([Rule(1, ApprovalScopeType.Department, 4)], departmentIds: [3, 4]).Required.Should().BeTrue();

    [Test]
    public void A_branch_and_a_department_with_the_same_identifier_are_not_mixed_up()
    {
        Match([Rule(1, ApprovalScopeType.Branch, 4)], branchId: 9, departmentIds: [4]).Required.Should().BeFalse();
        Match([Rule(1, ApprovalScopeType.Department, 9)], branchId: 9, departmentIds: [4]).Required.Should().BeFalse();
    }

    [Test]
    public void A_customer_without_a_branch_or_a_department_is_covered_by_nothing()
        => Match([Rule(1, ApprovalScopeType.Branch, 1), Rule(2, ApprovalScopeType.Department, 1)]).Required.Should().BeFalse();

    [Test]
    public void A_rule_of_an_unknown_scope_covers_nothing()
        => Match([Rule(1, (ApprovalScopeType)50, 9)], branchId: 9, departmentIds: [9]).Required.Should().BeFalse();

    #endregion

    #region Thresholds

    [TestCase(null, 0.01, true, TestName = "No threshold asks for every order")]
    [TestCase(0, 0.01, true, TestName = "A zero threshold asks for every order")]
    [TestCase(100, 100, false, TestName = "An order of exactly the threshold is not over it")]
    [TestCase(100, 100.01, true, TestName = "An order over the threshold needs approval")]
    [TestCase(100, 99.99, false, TestName = "An order under the threshold does not")]
    public void Threshold(double? threshold, double amount, bool required)
        => Match([Rule(1, ApprovalScopeType.Branch, 9, threshold.HasValue ? (decimal)threshold.Value : null)], branchId: 9, amount: (decimal)amount)
            .Required.Should().Be(required);

    [Test]
    public void A_rule_under_its_threshold_does_not_stop_another_rule_asking()
    {
        var result = Match([Rule(1, ApprovalScopeType.Branch, 9, 500m), Rule(2, ApprovalScopeType.Department, 4, 100m)],
            branchId: 9, departmentIds: [4], amount: 200m);

        result.Required.Should().BeTrue();
        result.RuleIds.Should().Equal(2);
    }

    #endregion

    #region Approvers

    [Test]
    public void The_branch_and_the_department_are_both_asked_each_approver_once()
    {
        var approvers = new Dictionary<int, IReadOnlyList<int>> { [1] = [10, 11], [2] = [11, 12] };

        var result = Match([Rule(1, ApprovalScopeType.Branch, 9), Rule(2, ApprovalScopeType.Department, 4)],
            branchId: 9, departmentIds: [4], approvers: approvers);

        result.ApproverIds.Should().Equal(10, 11, 12);
        result.RuleIds.Should().Equal(1, 2);
    }

    [Test]
    public void A_rule_without_an_active_approver_holds_nothing()
    {
        var approvers = new Dictionary<int, IReadOnlyList<int>> { [1] = [] };

        Match([Rule(1, ApprovalScopeType.Branch, 9)], branchId: 9, approvers: approvers).Required.Should().BeFalse();
    }

    [Test]
    public void An_inactive_rule_is_ignored()
        => Match([Rule(1, ApprovalScopeType.Branch, 9, active: false)], branchId: 9).Required.Should().BeFalse();

    [Test]
    public void The_amount_measured_is_kept_whether_or_not_approval_is_needed()
    {
        Match([], branchId: 9, amount: 42m).Amount.Should().Be(42m);
        Match([Rule(1, ApprovalScopeType.Branch, 9)], branchId: 9, amount: 42m).Amount.Should().Be(42m);
    }

    #endregion
}
