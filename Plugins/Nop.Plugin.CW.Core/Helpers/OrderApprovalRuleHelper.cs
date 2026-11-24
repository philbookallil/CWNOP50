using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// Decides from the rules of a store whether an order needs approval. Pure: no database, so the rules are tested alone.
/// </summary>
public static class OrderApprovalRuleHelper
{
    #region Utilities

    /// <summary>
    /// Whether a rule's scope covers the customer who placed the order
    /// </summary>
    /// <param name="rule">The rule</param>
    /// <param name="branchId">The customer's branch; null when there is none</param>
    /// <param name="departmentIds">The customer's departments</param>
    private static bool Covers(ApprovalRule rule, int? branchId, IReadOnlyCollection<int> departmentIds)
        => rule.ScopeType switch
        {
            ApprovalScopeType.Branch => branchId == rule.ScopeId,
            ApprovalScopeType.Department => departmentIds.Contains(rule.ScopeId),
            _ => false
        };

    /// <summary>
    /// Whether an amount is over a rule's threshold; a rule without one (null or 0) asks for every order
    /// </summary>
    /// <param name="threshold">The rule's threshold</param>
    /// <param name="amount">The amount measured</param>
    private static bool IsOverThreshold(decimal? threshold, decimal amount)
        => !threshold.HasValue || threshold.Value <= decimal.Zero || amount > threshold.Value;

    #endregion

    #region Methods

    /// <summary>
    /// Matches the rules against an order
    /// </summary>
    /// <param name="rules">The store's active rules</param>
    /// <param name="approverIdsByRule">Each rule's active approvers; a rule with none asks no one and is skipped</param>
    /// <param name="branchId">The branch of the customer who placed the order; null when there is none</param>
    /// <param name="departmentIds">The departments of the customer who placed the order</param>
    /// <param name="amount">The order total</param>
    /// <returns>The requirement: required when one rule both covers the customer and is over its threshold</returns>
    public static OrderApprovalRequirement Match(IEnumerable<ApprovalRule> rules,
        IReadOnlyDictionary<int, IReadOnlyList<int>> approverIdsByRule,
        int? branchId,
        IReadOnlyCollection<int> departmentIds,
        decimal amount)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(approverIdsByRule);
        ArgumentNullException.ThrowIfNull(departmentIds);

        var ruleIds = new List<int>();
        var approverIds = new List<int>();

        foreach (var rule in rules.Where(rule => rule.Active).OrderBy(rule => rule.Id))
        {
            if (!Covers(rule, branchId, departmentIds) || !IsOverThreshold(rule.Threshold, amount))
                continue;

            if (!approverIdsByRule.TryGetValue(rule.Id, out var ruleApproverIds) || ruleApproverIds.Count == 0)
                continue;

            ruleIds.Add(rule.Id);

            foreach (var approverId in ruleApproverIds)
                if (!approverIds.Contains(approverId))
                    approverIds.Add(approverId);
        }

        if (ruleIds.Count == 0)
            return OrderApprovalRequirement.NotRequired with { Amount = amount };

        return new OrderApprovalRequirement
        {
            Required = true,
            Amount = amount,
            ApproverIds = approverIds,
            RuleIds = ruleIds
        };
    }

    #endregion
}
