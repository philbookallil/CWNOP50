using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals.DTOs;
using Nop.Plugin.CW.Core.Helpers;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The approvers of the stores: who they are, what they approve for, and their changes
/// </summary>
public class ApproverService : IApproverService
{
    #region Fields

    protected readonly IApprovalRuleService _approvalRuleService;
    protected readonly IRepository<ApprovalRule> _approvalRuleRepository;
    protected readonly IRepository<ApprovalRuleApprover> _approvalRuleApproverRepository;
    protected readonly IRepository<Approver> _approverRepository;
    protected readonly IRepository<Order> _orderRepository;
    protected readonly IRepository<OrderApproval> _orderApprovalRepository;
    protected readonly IRepository<OrderApprovalRequest> _requestRepository;

    #endregion

    #region Ctor

    public ApproverService(IApprovalRuleService approvalRuleService,
        IRepository<ApprovalRule> approvalRuleRepository,
        IRepository<ApprovalRuleApprover> approvalRuleApproverRepository,
        IRepository<Approver> approverRepository,
        IRepository<Order> orderRepository,
        IRepository<OrderApproval> orderApprovalRepository,
        IRepository<OrderApprovalRequest> requestRepository)
    {
        _approvalRuleService = approvalRuleService;
        _approvalRuleRepository = approvalRuleRepository;
        _approvalRuleApproverRepository = approvalRuleApproverRepository;
        _approverRepository = approverRepository;
        _orderRepository = orderRepository;
        _orderApprovalRepository = orderApprovalRepository;
        _requestRepository = requestRepository;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// An approver that is not deleted; null otherwise
    /// </summary>
    protected virtual async Task<Approver> GetApproverAsync(int approverId)
    {
        if (approverId <= 0)
            return null;

        return await _approverRepository.Table.FirstOrDefaultAsync(approver => approver.Id == approverId && !approver.Deleted);
    }

    /// <summary>
    /// Whether another approver of the same store has an e-mail address
    /// </summary>
    protected virtual async Task<bool> IsEmailTakenAsync(Approver approver, string email)
    {
        var others = await _approverRepository.Table
            .Where(item => item.StoreId == approver.StoreId && item.Id != approver.Id && !item.Deleted)
            .ToListAsync();

        return others.Any(other => SameText(other.Email, email));
    }

    /// <summary>
    /// Whether a rule in force names an approver, or an order waits for it
    /// </summary>
    protected virtual async Task<bool> IsInUseAsync(int approverId)
        => (await GetRulesAsync([approverId])).Any(item => item.Rule.Active) || (await GetCountsAsync([approverId]))[approverId].Waiting > 0;

    protected static bool SameText(string first, string second)
        => string.Equals(first, second, StringComparison.InvariantCultureIgnoreCase);

    #endregion

    #region Queries

    public virtual async Task<IPagedList<Approver>> SearchApproversAsync(int storeId = 0, string text = null, int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = _approverRepository.Table.Where(approver => !approver.Deleted);

        if (storeId > 0)
            query = query.Where(approver => approver.StoreId == storeId);

        text = text?.Trim();

        if (!string.IsNullOrEmpty(text))
            query = query.Where(approver => approver.Name.Contains(text) || approver.Email.Contains(text));

        return await query.OrderBy(approver => approver.Name).ThenBy(approver => approver.Id).ToPagedListAsync(pageIndex, pageSize);
    }

    public virtual async Task<IList<ApproverRuleDTO>> GetRulesAsync(int[] approverIds)
    {
        if (approverIds == null || approverIds.Length == 0)
            return new List<ApproverRuleDTO>();

        return await (from mapping in _approvalRuleApproverRepository.Table
                      join rule in _approvalRuleRepository.Table on mapping.ApprovalRuleId equals rule.Id
                      where approverIds.Contains(mapping.ApproverId)
                      orderby rule.ScopeTypeId, rule.ScopeId
                      select new ApproverRuleDTO { ApproverId = mapping.ApproverId, Rule = rule })
            .ToListAsync();
    }

    public virtual async Task<IDictionary<int, ApproverCountsDTO>> GetCountsAsync(int[] approverIds)
    {
        if (approverIds == null || approverIds.Length == 0)
            return new Dictionary<int, ApproverCountsDTO>();

        //a reminder is a second link to the same order: an order counts once
        var waiting = (await (from request in _requestRepository.Table
                              join approval in _orderApprovalRepository.Table on request.OrderApprovalId equals approval.Id
                              join order in _orderRepository.Table on approval.OrderId equals order.Id
                              where approverIds.Contains(request.ApproverId) && request.RevokedOnUtc == null
                                    && approval.StatusId == (int)OrderApprovalStatus.Pending && !order.Deleted
                              select new { request.ApproverId, request.OrderApprovalId })
                .Distinct()
                .ToListAsync())
            .ToLookup(item => item.ApproverId);

        var decided = (await (from approval in _orderApprovalRepository.Table
                              where approval.DecidedByApproverId != null && approverIds.Contains(approval.DecidedByApproverId.Value)
                              group approval by new { ApproverId = approval.DecidedByApproverId.Value, approval.StatusId } into statuses
                              select new { statuses.Key.ApproverId, statuses.Key.StatusId, Count = statuses.Count() })
                .ToListAsync())
            .ToLookup(item => item.ApproverId);

        int decidedAs(int approverId, OrderApprovalStatus status) =>
            decided[approverId].Where(item => item.StatusId == (int)status).Sum(item => item.Count);

        return approverIds.Distinct().ToDictionary(id => id, id => new ApproverCountsDTO(waiting[id].Count(),
            decidedAs(id, OrderApprovalStatus.Approved),
            decidedAs(id, OrderApprovalStatus.Declined)));
    }

    #endregion

    #region Changes

    public virtual async Task<ApproverChangeResult> UpdateApproverAsync(int approverId, string name, string email)
    {
        name = name?.Trim();
        email = email?.Trim();

        var approver = await GetApproverAsync(approverId);
        if (approver == null)
            return ApproverChangeResult.NotFound;

        //an approver named by its address keeps being, and takes a name later
        var namedByAddress = SameText(approver.Name, approver.Email) && SameText(name, approver.Name);

        if (namedByAddress)
            name = email;

        if (string.IsNullOrEmpty(name) || !namedByAddress && name.Length > OrderApprovalEmailHelper.MaxNameLength || !OrderApprovalEmailHelper.IsValid(email))
            return ApproverChangeResult.Invalid;

        if (await IsEmailTakenAsync(approver, email))
            return ApproverChangeResult.EmailTaken;

        approver.Name = name;
        approver.Email = email;
        await _approverRepository.UpdateAsync(approver);

        return ApproverChangeResult.Done;
    }

    public virtual async Task<ApproverChangeResult> ReplaceApproverAsync(int approverId, string email, string name = null)
    {
        email = email?.Trim();
        name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        if (!OrderApprovalEmailHelper.IsValid(email) || name?.Length > OrderApprovalEmailHelper.MaxNameLength)
            return ApproverChangeResult.Invalid;

        var approver = await GetApproverAsync(approverId);
        if (approver == null)
            return ApproverChangeResult.NotFound;

        if (SameText(approver.Email, email))
            return ApproverChangeResult.SameApprover;

        //before the replacement is created: none is left unused
        if (!await _approvalRuleApproverRepository.Table.AnyAsync(mapping => mapping.ApproverId == approver.Id))
            return ApproverChangeResult.NothingToReplace;

        var replacement = await _approvalRuleService.GetOrCreateApproverAsync(approver.StoreId, email, name);

        var mappings = await _approvalRuleApproverRepository.Table
            .Where(mapping => mapping.ApproverId == approver.Id || mapping.ApproverId == replacement.Id)
            .ToListAsync();

        var replacementRuleIds = mappings
            .Where(mapping => mapping.ApproverId == replacement.Id)
            .Select(mapping => mapping.ApprovalRuleId)
            .ToHashSet();

        foreach (var mapping in mappings.Where(mapping => mapping.ApproverId == approver.Id))
        {
            //a rule that already names the replacement loses only this approver
            if (!replacementRuleIds.Add(mapping.ApprovalRuleId))
            {
                await _approvalRuleApproverRepository.DeleteAsync([mapping]);
                continue;
            }

            mapping.ApproverId = replacement.Id;
            await _approvalRuleApproverRepository.UpdateAsync(mapping);
        }

        return ApproverChangeResult.Done;
    }

    public virtual async Task<ApproverChangeResult> DeleteApproverAsync(int approverId)
    {
        var approver = await GetApproverAsync(approverId);
        if (approver == null)
            return ApproverChangeResult.NotFound;

        if (await IsInUseAsync(approverId))
            return ApproverChangeResult.InUse;

        approver.Deleted = true;
        await _approverRepository.UpdateAsync(approver);

        //a branch saved, or an order sent, with the approver at the same moment: whoever named it wants it asked
        if (await IsInUseAsync(approverId))
        {
            approver.Deleted = false;
            await _approverRepository.UpdateAsync(approver);

            return ApproverChangeResult.InUse;
        }

        //a rule switched off keeps its approvers for the day it is switched on again, but not a deleted one
        var mappings = await _approvalRuleApproverRepository.Table.Where(mapping => mapping.ApproverId == approverId).ToListAsync();
        if (mappings.Count > 0)
            await _approvalRuleApproverRepository.DeleteAsync(mappings);

        return ApproverChangeResult.Done;
    }

    #endregion
}
