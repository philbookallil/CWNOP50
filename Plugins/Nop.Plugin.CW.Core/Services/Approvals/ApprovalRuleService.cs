using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Helpers;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The approval rules of the branches and departments, and the approvers they name
/// </summary>
public class ApprovalRuleService : IApprovalRuleService
{
    #region Fields

    protected readonly IRepository<ApprovalRule> _approvalRuleRepository;
    protected readonly IRepository<ApprovalRuleApprover> _approvalRuleApproverRepository;
    protected readonly IRepository<Approver> _approverRepository;

    #endregion

    #region Ctor

    public ApprovalRuleService(IRepository<ApprovalRule> approvalRuleRepository,
        IRepository<ApprovalRuleApprover> approvalRuleApproverRepository,
        IRepository<Approver> approverRepository)
    {
        _approvalRuleRepository = approvalRuleRepository;
        _approvalRuleApproverRepository = approvalRuleApproverRepository;
        _approverRepository = approverRepository;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The one rule of a branch or a department; null when it has none
    /// </summary>
    protected virtual async Task<ApprovalRule> GetRuleAsync(ApprovalScopeType scopeType, int scopeId)
    {
        if (scopeId <= 0)
            return null;

        return await _approvalRuleRepository.Table
            .FirstOrDefaultAsync(rule => rule.ScopeTypeId == (int)scopeType && rule.ScopeId == scopeId);
    }

    /// <summary>
    /// Makes a rule name exactly these approvers
    /// </summary>
    protected virtual async Task SetApproversAsync(ApprovalRule rule, IReadOnlyCollection<int> approverIds)
    {
        var mappings = await _approvalRuleApproverRepository.Table
            .Where(mapping => mapping.ApprovalRuleId == rule.Id)
            .ToListAsync();

        var removed = mappings.Where(mapping => !approverIds.Contains(mapping.ApproverId)).ToList();
        if (removed.Count > 0)
            await _approvalRuleApproverRepository.DeleteAsync(removed);

        foreach (var approverId in approverIds.Where(id => mappings.All(mapping => mapping.ApproverId != id)))
            await _approvalRuleApproverRepository.InsertAsync(new ApprovalRuleApprover { ApprovalRuleId = rule.Id, ApproverId = approverId });
    }

    #endregion

    #region Methods

    public virtual async Task<ApprovalRuleSetup> GetSetupAsync(ApprovalScopeType scopeType, int scopeId)
    {
        var rule = await GetRuleAsync(scopeType, scopeId);

        if (rule == null)
            return new ApprovalRuleSetup(false, null, []);

        var emails = await (from mapping in _approvalRuleApproverRepository.Table
                            join approver in _approverRepository.Table on mapping.ApproverId equals approver.Id
                            where mapping.ApprovalRuleId == rule.Id && approver.Active && !approver.Deleted
                            orderby approver.Email
                            select approver.Email)
            .ToListAsync();

        return new ApprovalRuleSetup(rule.Active, rule.Threshold, emails);
    }

    public virtual async Task SaveSetupAsync(int storeId, ApprovalScopeType scopeType, int scopeId, ApprovalRuleSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(storeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scopeId);

        var rule = await GetRuleAsync(scopeType, scopeId);
        var emails = (setup.Emails ?? []).Where(OrderApprovalEmailHelper.IsValid).ToList();
        var threshold = OrderApprovalRuleHelper.IsValidThreshold(setup.Threshold) ? setup.Threshold : null;

        //nothing set up and nothing asked for
        if (rule == null && !setup.Required && emails.Count == 0 && !threshold.HasValue)
            return;

        //the approvers first: a rule is never in force without the ones it names
        var approverIds = new List<int>();

        foreach (var email in emails)
        {
            var approver = await GetOrCreateApproverAsync(storeId, email);

            if (!approverIds.Contains(approver.Id))
                approverIds.Add(approver.Id);
        }

        var now = DateTime.UtcNow;

        if (rule == null)
        {
            rule = new ApprovalRule
            {
                StoreId = storeId,
                ScopeType = scopeType,
                ScopeId = scopeId,
                Threshold = threshold,
                Active = setup.Required,
                CreatedOnUtc = now,
                UpdatedOnUtc = now
            };

            await _approvalRuleRepository.InsertAsync(rule);
        }
        else
        {
            rule.StoreId = storeId;
            rule.Threshold = threshold;
            rule.Active = setup.Required;
            rule.UpdatedOnUtc = now;

            await _approvalRuleRepository.UpdateAsync(rule);
        }

        await SetApproversAsync(rule, approverIds);
    }

    public virtual async Task DeactivateAsync(ApprovalScopeType scopeType, int scopeId)
    {
        var rule = await GetRuleAsync(scopeType, scopeId);

        if (rule == null || !rule.Active)
            return;

        rule.Active = false;
        rule.UpdatedOnUtc = DateTime.UtcNow;

        await _approvalRuleRepository.UpdateAsync(rule);
    }

    public virtual async Task<Approver> GetOrCreateApproverAsync(int storeId, string email, string name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        email = email.Trim();
        name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        var approver = (await _approverRepository.Table
                .Where(item => item.StoreId == storeId && !item.Deleted)
                .ToListAsync())
            .FirstOrDefault(item => string.Equals(item.Email, email, StringComparison.InvariantCultureIgnoreCase));

        if (approver == null)
        {
            approver = new Approver
            {
                StoreId = storeId,
                Name = name ?? email,
                Email = email,
                Active = true,
                CreatedOnUtc = DateTime.UtcNow
            };

            await _approverRepository.InsertAsync(approver);

            return approver;
        }

        //an approver named only by the address takes the name it is now given
        var takesName = name != null && string.Equals(approver.Name, approver.Email, StringComparison.InvariantCultureIgnoreCase);

        if (approver.Active && !takesName)
            return approver;

        approver.Active = true;

        if (takesName)
            approver.Name = name;

        await _approverRepository.UpdateAsync(approver);

        return approver;
    }

    #endregion
}
