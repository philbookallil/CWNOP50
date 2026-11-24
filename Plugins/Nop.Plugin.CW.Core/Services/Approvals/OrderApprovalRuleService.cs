using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Decides whether a placed order must wait for an approver
/// </summary>
public class OrderApprovalRuleService : IOrderApprovalRuleService
{
    #region Fields

    protected readonly IBranchService _branchService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IExchangeService _exchangeService;
    protected readonly ILogger _logger;
    protected readonly IOrderService _orderService;
    protected readonly IRepository<ApprovalRule> _approvalRuleRepository;
    protected readonly IRepository<ApprovalRuleApprover> _approvalRuleApproverRepository;
    protected readonly IRepository<Approver> _approverRepository;
    protected readonly IRepository<Department> _departmentRepository;
    protected readonly IRepository<PackName> _packNameRepository;
    protected readonly ISettingService _settingService;

    #endregion

    #region Ctor

    public OrderApprovalRuleService(IBranchService branchService,
        ICWFeatureService cwFeatureService,
        IExchangeService exchangeService,
        ILogger logger,
        IOrderService orderService,
        IRepository<ApprovalRule> approvalRuleRepository,
        IRepository<ApprovalRuleApprover> approvalRuleApproverRepository,
        IRepository<Approver> approverRepository,
        IRepository<Department> departmentRepository,
        IRepository<PackName> packNameRepository,
        ISettingService settingService)
    {
        _branchService = branchService;
        _cwFeatureService = cwFeatureService;
        _exchangeService = exchangeService;
        _logger = logger;
        _orderService = orderService;
        _approvalRuleRepository = approvalRuleRepository;
        _approvalRuleApproverRepository = approvalRuleApproverRepository;
        _approverRepository = approverRepository;
        _departmentRepository = departmentRepository;
        _packNameRepository = packNameRepository;
        _settingService = settingService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Whether an order leaves something to pay, with a payment method that waits for approval. An order with nothing
    /// left to pay can still name the method its customer had chosen before, so its total is asked first.
    /// </summary>
    /// <param name="order">The order</param>
    protected virtual async Task<bool> IsOnWaitingPaymentMethodAsync(Order order)
    {
        if (order.OrderTotal <= decimal.Zero || string.IsNullOrWhiteSpace(order.PaymentMethodSystemName))
            return false;

        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(order.StoreId);

        return settings.ApprovalPaymentMethods?.Any(name =>
            string.Equals(name?.Trim(), order.PaymentMethodSystemName.Trim(), StringComparison.InvariantCultureIgnoreCase)) == true;
    }

    /// <summary>
    /// The departments of a customer: those of their own packnames in the store. None while the store does not use
    /// packnames.
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="storeId">Store identifier</param>
    protected virtual async Task<int[]> GetDepartmentIdsAsync(int customerId, int storeId)
    {
        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, storeId))
            return [];

        return await (from packName in _packNameRepository.Table
                      join department in _departmentRepository.Table on packName.DepartmentId equals (int?)department.Id
                      where packName.CustomerId == customerId && !packName.Deleted
                            && department.StoreId == storeId && !department.Deleted
                      select department.Id)
            .Distinct()
            .ToArrayAsync();
    }

    /// <summary>
    /// Whether any item of an order replaces an item of an exchange
    /// </summary>
    /// <param name="order">The order</param>
    protected virtual async Task<bool> HasReplacementsAsync(Order order)
    {
        var orderItemIds = (await _orderService.GetOrderItemsAsync(order.Id)).Select(item => item.Id).ToArray();

        return (await _exchangeService.GetReturnRequestItemsForOrderItemsAsync(orderItemIds)).Any();
    }

    /// <summary>
    /// The active approvers of each rule; a rule whose approvers are all inactive or deleted is left out, and logged,
    /// because it would hold an order nobody is asked about
    /// </summary>
    /// <param name="rules">The rules that cover the order</param>
    protected virtual async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetApproverIdsByRuleAsync(IList<ApprovalRule> rules)
    {
        var ruleIds = rules.Select(rule => rule.Id).ToArray();

        var pairs = await (from mapping in _approvalRuleApproverRepository.Table
                           join approver in _approverRepository.Table on mapping.ApproverId equals approver.Id
                           where ruleIds.Contains(mapping.ApprovalRuleId) && approver.Active && !approver.Deleted
                           select new { mapping.ApprovalRuleId, mapping.ApproverId })
            .ToListAsync();

        var result = pairs
            .GroupBy(pair => pair.ApprovalRuleId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<int>)group.Select(pair => pair.ApproverId).Distinct().ToList());

        foreach (var rule in rules.Where(rule => !result.ContainsKey(rule.Id)))
            await _logger.WarningAsync($"[Approvals] Rule {rule.Id} ({rule.ScopeType} {rule.ScopeId}, store {rule.StoreId}) has no active approver and holds no order.");

        return result;
    }

    #endregion

    #region Methods

    public virtual async Task<OrderApprovalRequirement> EvaluateOrderAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Approvals, order.StoreId))
            return OrderApprovalRequirement.NotRequired;

        if (!await IsOnWaitingPaymentMethodAsync(order))
            return OrderApprovalRequirement.NotRequired;

        var branchId = (await _branchService.GetBranchByCustomerIdAsync(order.CustomerId))?.Id ?? 0;
        var departmentIds = await GetDepartmentIdsAsync(order.CustomerId, order.StoreId);

        var rules = await _approvalRuleRepository.Table
            .Where(rule => rule.StoreId == order.StoreId && rule.Active
                && ((rule.ScopeTypeId == (int)ApprovalScopeType.Branch && rule.ScopeId == branchId)
                    || (rule.ScopeTypeId == (int)ApprovalScopeType.Department && departmentIds.Contains(rule.ScopeId))))
            .ToListAsync();

        if (rules.Count == 0)
            return OrderApprovalRequirement.NotRequired;

        var requirement = OrderApprovalRuleHelper.Match(rules,
            await GetApproverIdsByRuleAsync(rules),
            branchId,
            departmentIds,
            order.OrderTotal);

        //an exchange's return is completed when its replacement is ordered: declining the order would put the replacement
        //back in the cart as an ordinary line, to be paid again, so such an order is never held, whatever else it has
        if (requirement.Required && await HasReplacementsAsync(order))
            return OrderApprovalRequirement.NotRequired;

        return requirement;
    }

    #endregion
}
