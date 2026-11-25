using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The approvals of held orders
/// </summary>
/// <remarks>
/// <para>
/// A held order is a real order: its subsidy, store credit and stock were taken at placement. Accepting it moves it to
/// Processing. Declining or expiring it cancels it through nop's <c>CancelOrderAsync</c>, which gives the stock, reward
/// points and gift cards back; the subsidy and store credit come back because the views leave cancelled orders out. Its
/// lines then go back to the customer's cart through <c>ReOrderAsync</c>, with their packnames.
/// </para>
/// <para>
/// Every change to one approval runs under one lock per order, and each re-reads the approval inside it: the first
/// decision wins, and a resend or a change of approver cannot cross a decision.
/// </para>
/// </remarks>
public class OrderApprovalService : IOrderApprovalService
{
    #region Fields

    protected readonly IApprovalRuleService _approvalRuleService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ILocker _locker;
    protected readonly ILogger _logger;
    protected readonly IOrderApprovalRuleService _orderApprovalRuleService;
    protected readonly IOrderApprovalWorkflowMessageService _messageService;
    protected readonly IOrderProcessingOverrideService _orderProcessingService;
    protected readonly IOrderService _orderService;
    protected readonly IRepository<Approver> _approverRepository;
    protected readonly IRepository<OrderApproval> _orderApprovalRepository;
    protected readonly IRepository<OrderApprovalRequest> _requestRepository;
    protected readonly ISettingService _settingService;
    protected readonly ISubsidyCreditService _subsidyCreditService;

    #endregion

    #region Ctor

    public OrderApprovalService(IApprovalRuleService approvalRuleService,
        ILocalizationService localizationService,
        ILocker locker,
        ILogger logger,
        IOrderApprovalRuleService orderApprovalRuleService,
        IOrderApprovalWorkflowMessageService messageService,
        IOrderProcessingOverrideService orderProcessingService,
        IOrderService orderService,
        IRepository<Approver> approverRepository,
        IRepository<OrderApproval> orderApprovalRepository,
        IRepository<OrderApprovalRequest> requestRepository,
        ISettingService settingService,
        ISubsidyCreditService subsidyCreditService)
    {
        _approvalRuleService = approvalRuleService;
        _localizationService = localizationService;
        _locker = locker;
        _logger = logger;
        _orderApprovalRuleService = orderApprovalRuleService;
        _messageService = messageService;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _approverRepository = approverRepository;
        _orderApprovalRepository = orderApprovalRepository;
        _requestRepository = requestRepository;
        _settingService = settingService;
        _subsidyCreditService = subsidyCreditService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Adds a note to the order, for staff only
    /// </summary>
    protected virtual async Task AddOrderNoteAsync(Order order, string note)
    {
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Stops every live link of an approval
    /// </summary>
    protected virtual async Task RevokeRequestsAsync(OrderApproval approval)
        => await RevokeRequestsAsync(await GetLiveRequestsAsync(approval.Id));

    /// <summary>
    /// Stops some links
    /// </summary>
    protected virtual async Task RevokeRequestsAsync(IList<OrderApprovalRequest> requests)
    {
        if (requests.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var request in requests)
            request.RevokedOnUtc = now;

        await _requestRepository.UpdateAsync(requests);
    }

    /// <summary>
    /// The links of an approval that work, newest first
    /// </summary>
    protected virtual async Task<IList<OrderApprovalRequest>> GetLiveRequestsAsync(int orderApprovalId)
        => (await GetRequestsAsync(orderApprovalId)).Where(request => request.RevokedOnUtc == null).ToList();

    /// <summary>
    /// Sends each approver a new link to the order. A link whose e-mail could not be queued is logged and noted on the
    /// order; the others still go.
    /// </summary>
    /// <returns>The links, and whether the e-mail of any was queued: only the e-mails carry them</returns>
    protected virtual async Task<(IList<OrderApprovalRequest> Requests, bool AnyQueued)> SendRequestsAsync(Order order, OrderApproval approval,
        IList<Approver> approvers, bool reminder)
    {
        var requests = new List<OrderApprovalRequest>();
        var anyQueued = false;

        foreach (var approver in approvers)
        {
            var token = OrderApprovalTokenHelper.CreateToken();

            var request = new OrderApprovalRequest
            {
                OrderApprovalId = approval.Id,
                ApproverId = approver.Id,
                ApproverName = approver.Name,
                ApproverEmail = approver.Email,
                TokenHash = OrderApprovalTokenHelper.HashToken(token),
                SentOnUtc = DateTime.UtcNow
            };

            await _requestRepository.InsertAsync(request);
            requests.Add(request);

            var queued = false;
            Exception failure = null;

            try
            {
                //none without an error: the store has no active template
                queued = (await _messageService.SendApproverRequestAsync(order, approval, request, token, reminder))?.Count > 0;
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (queued)
            {
                anyQueued = true;
                continue;
            }

            await _logger.ErrorAsync($"[Approvals] Order {order.Id}: the approval e-mail to {approver.Email} could not be queued.", failure);
            await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.EmailFailed"), approver.Email));
        }

        return (requests, anyQueued);
    }

    /// <summary>
    /// The approvers' names of some links, comma separated
    /// </summary>
    protected static string Names(IEnumerable<OrderApprovalRequest> requests) => string.Join(", ", requests.Select(request => request.ApproverName));

    /// <summary>
    /// Sends a customer e-mail of the approvals; one that could not be queued is logged, the decision stands
    /// </summary>
    protected virtual async Task SendCustomerNotificationAsync(Order order, OrderApproval approval, string messageTemplateName, string approverNames)
    {
        try
        {
            await _messageService.SendCustomerNotificationAsync(order, approval, messageTemplateName, approverNames);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync($"[Approvals] Order {order.Id}: the customer e-mail {messageTemplateName} could not be queued.", exception);
        }
    }

    /// <summary>
    /// Puts a decision that could not be carried out back to pending
    /// </summary>
    protected virtual async Task UndoDecisionAsync(OrderApproval approval)
    {
        approval.Status = OrderApprovalStatus.Pending;
        approval.DecidedBy = null;
        approval.DecidedByApproverId = null;
        approval.DecidedByCustomerId = null;
        approval.DecidedOnUtc = null;
        approval.DecisionComment = null;
        await _orderApprovalRepository.UpdateAsync(approval);
    }

    /// <summary>
    /// The approvers the rules asked, by name
    /// </summary>
    protected virtual async Task<IList<Approver>> GetApproversAsync(OrderApprovalRequirement requirement)
    {
        var approverIds = requirement.ApproverIds.ToArray();

        return await _approverRepository.Table
            .Where(approver => approverIds.Contains(approver.Id) && approver.Active && !approver.Deleted)
            .OrderBy(approver => approver.Name)
            .ToListAsync();
    }

    /// <summary>
    /// The approvers an approval's links went to, live or not, that can still be asked. A held order whose first links
    /// were never sent (the site stopped, or sending failed) asks the approvers its rules name now.
    /// </summary>
    protected virtual async Task<IList<Approver>> GetRequestedApproversAsync(Order order, OrderApproval approval, bool liveLinksOnly)
    {
        if (!await _requestRepository.Table.AnyAsync(request => request.OrderApprovalId == approval.Id))
            return await GetApproversAsync(await _orderApprovalRuleService.EvaluateOrderAsync(order));

        var approverIds = await _requestRepository.Table
            .Where(request => request.OrderApprovalId == approval.Id && (!liveLinksOnly || request.RevokedOnUtc == null))
            .Select(request => request.ApproverId)
            .Distinct()
            .ToListAsync();

        return await _approverRepository.Table
            .Where(approver => approverIds.Contains(approver.Id) && !approver.Deleted)
            .OrderBy(approver => approver.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Whom to remind: each address with a link that works, at that address, as it is the one waited for. An approver
    /// removed since is not reminded. A held order whose first links were never sent asks the approvers its rules name now.
    /// </summary>
    protected virtual async Task<IList<Approver>> GetReminderTargetsAsync(Order order, OrderApproval approval)
    {
        var live = (await GetLiveRequestsAsync(approval.Id)).DistinctBy(request => request.ApproverEmail, StringComparer.InvariantCultureIgnoreCase).ToList();

        if (live.Count == 0)
            return await GetRequestedApproversAsync(order, approval, liveLinksOnly: true);

        var approverIds = live.Select(request => request.ApproverId).ToArray();

        var approvers = (await _approverRepository.Table.Where(approver => approverIds.Contains(approver.Id) && !approver.Deleted).ToListAsync())
            .ToDictionary(approver => approver.Id);

        return live
            .Where(request => approvers.ContainsKey(request.ApproverId))
            .Select(request => new Approver
            {
                Id = request.ApproverId,
                Name = PresentName(request, approvers[request.ApproverId]),
                Email = request.ApproverEmail
            })
            .OrderBy(approver => approver.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The name of the approver a link was sent to: the present one while the link's address is still the approver's,
    /// otherwise the name it was sent with
    /// </summary>
    protected static string PresentName(OrderApprovalRequest request, Approver approver)
        => approver != null && string.Equals(approver.Email, request.ApproverEmail, StringComparison.InvariantCultureIgnoreCase)
            ? approver.Name
            : request.ApproverName;

    /// <summary>
    /// Who decides with a link, as shown to staff and the customer
    /// </summary>
    protected virtual async Task<string> DecidedByAsync(OrderApprovalRequest request)
    {
        var approver = await _approverRepository.Table.FirstOrDefaultAsync(item => item.Id == request.ApproverId);

        return OrderApprovalEmailHelper.NameAndEmail(PresentName(request, approver), request.ApproverEmail);
    }

    /// <summary>
    /// Runs a change to one approval under the order's lock
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="action">The change; it re-reads what it acts on</param>
    protected virtual async Task<OrderApprovalResult> RunLockedAsync(int orderId, Func<Task<OrderApprovalResult>> action)
    {
        var result = OrderApprovalResult.Busy;

        var ran = await _locker.PerformActionWithLockAsync(
            string.Format(OrderApprovalDefaults.DecisionLockKeyFormat, orderId),
            OrderApprovalDefaults.DecisionLockTime,
            async () =>
            {
                result = await action();
            });

        if (!ran)
            await _logger.WarningAsync($"[Approvals] Order {orderId}: another change to its approval was running; nothing was done.");

        return ran ? result : OrderApprovalResult.Busy;
    }

    /// <summary>
    /// Decides a pending approval: accepted, declined or expired
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="outcome">Approved, Declined or Expired</param>
    /// <param name="decidedBy">Who decided, as shown to staff and the customer</param>
    /// <param name="approverId">The approver who decided, if one did</param>
    /// <param name="adminCustomerId">The admin who decided, if one did</param>
    /// <param name="comment">The reason of a decline or an expiry</param>
    /// <param name="requestId">The link used, which must still be live</param>
    protected virtual Task<OrderApprovalResult> DecideAsync(int orderId, OrderApprovalStatus outcome, string decidedBy,
        int? approverId, int? adminCustomerId, string comment, int? requestId)
    {
        return RunLockedAsync(orderId, async () =>
        {
            var approval = await GetOrderApprovalByOrderIdAsync(orderId);

            if (approval == null)
                return OrderApprovalResult.NotFound;

            if (approval.Status != OrderApprovalStatus.Pending)
                return OrderApprovalResult.AlreadyDecided;

            if (requestId.HasValue && (await _requestRepository.GetByIdAsync(requestId.Value))?.RevokedOnUtc != null)
                return OrderApprovalResult.LinkReplaced;

            var order = await _orderService.GetOrderByIdAsync(orderId);

            if (order == null || order.Deleted || order.OrderStatus == OrderStatus.Cancelled)
            {
                await EndAsCancelledAsync(approval);
                return OrderApprovalResult.AlreadyDecided;
            }

            //the approval is decided before the order moves, so the status-change guard sees an order no longer held
            approval.Status = outcome;
            approval.DecidedBy = decidedBy;
            approval.DecidedByApproverId = approverId;
            approval.DecidedByCustomerId = adminCustomerId;
            approval.DecidedOnUtc = DateTime.UtcNow;
            approval.DecisionComment = comment;
            await _orderApprovalRepository.UpdateAsync(approval);

            if (outcome == OrderApprovalStatus.Approved)
            {
                try
                {
                    await _orderProcessingService.ReleaseApprovedOrderAsync(order);
                }
                catch (Exception exception)
                {
                    await _logger.ErrorAsync($"[Approvals] Order {orderId}: releasing the approved order failed.", exception);

                    //still Pending: nothing moved, so the order can be decided again
                    if (order.OrderStatus == OrderStatus.Pending)
                    {
                        await UndoDecisionAsync(approval);
                        return OrderApprovalResult.Failed;
                    }
                }

                await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.Approved"), decidedBy));
            }
            else
            {
                try
                {
                    if (_orderProcessingService.CanCancelOrder(order))
                        await _orderProcessingService.CancelOrderAsync(order, false);
                }
                catch (Exception exception)
                {
                    await _logger.ErrorAsync($"[Approvals] Order {orderId}: cancelling the {outcome} order failed.", exception);

                    //not cancelled: nothing moved, so the order can be decided again
                    if (order.OrderStatus != OrderStatus.Cancelled)
                    {
                        await UndoDecisionAsync(approval);
                        return OrderApprovalResult.Failed;
                    }
                }

                await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync(outcome == OrderApprovalStatus.Expired
                    ? "Plugins.CW.Core.OrderApproval.Note.Expired"
                    : "Plugins.CW.Core.OrderApproval.Note.Declined"), decidedBy, comment));

                //the lines go back after the cancel, which gave their subsidy back; the credit this request read before
                //it is stale. The order is cancelled whatever happens here, so a failure is noted, not undone.
                _subsidyCreditService.ClearPackNameCredits();

                try
                {
                    var warnings = await _orderProcessingService.ReOrderAsync(order);

                    if (warnings.Any())
                        await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.ReOrderWarnings"), string.Join("; ", warnings)));
                }
                catch (Exception exception)
                {
                    await _logger.ErrorAsync($"[Approvals] Order {orderId}: putting the lines back in the cart failed.", exception);
                    await AddOrderNoteAsync(order, await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.ReOrderFailed"));
                }
            }

            await RevokeRequestsAsync(approval);

            var templateName = outcome switch
            {
                OrderApprovalStatus.Approved => CWMessageTemplateSystemNames.OrderApprovalApprovedCustomer,
                OrderApprovalStatus.Expired => CWMessageTemplateSystemNames.OrderApprovalExpiredCustomer,
                _ => CWMessageTemplateSystemNames.OrderApprovalDeclinedCustomer
            };

            await SendCustomerNotificationAsync(order, approval, templateName, null);

            return OrderApprovalResult.Done;
        });
    }

    /// <summary>
    /// Ends a pending approval whose order is gone or cancelled
    /// </summary>
    protected virtual async Task EndAsCancelledAsync(OrderApproval approval)
    {
        approval.Status = OrderApprovalStatus.Cancelled;
        approval.DecidedBy = await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.DecidedBy.OrderCancelled");
        approval.DecidedOnUtc = DateTime.UtcNow;
        await _orderApprovalRepository.UpdateAsync(approval);

        await RevokeRequestsAsync(approval);
    }

    /// <summary>
    /// A decline reason as kept: trimmed and cut to the longest kept; null when empty
    /// </summary>
    protected static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        reason = reason.Trim();

        return reason.Length > OrderApprovalDefaults.DecisionCommentMaxLength
            ? reason[..OrderApprovalDefaults.DecisionCommentMaxLength]
            : reason;
    }

    /// <summary>
    /// How an admin deciding is named
    /// </summary>
    protected virtual async Task<string> AdminNameAsync(Customer admin)
        => string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.DecidedBy.Admin"), admin.Email);

    /// <summary>
    /// Sends the reminder due: a new link to each approver whose link is still live; the old links keep working
    /// </summary>
    protected virtual Task SendReminderAsync(int orderId, int reminderNumber)
    {
        return RunLockedAsync(orderId, async () =>
        {
            var approval = await GetOrderApprovalByOrderIdAsync(orderId);
            if (approval == null || approval.Status != OrderApprovalStatus.Pending || approval.LastReminderNumber >= reminderNumber)
                return OrderApprovalResult.AlreadyDecided;

            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
                return OrderApprovalResult.NotFound;

            var approvers = await GetReminderTargetsAsync(order, approval);

            //the reminder stays due: an admin has to change the approver or decide
            if (approvers.Count == 0)
            {
                await _logger.WarningAsync($"[Approvals] Order {orderId} is waiting for approval and has nobody to remind. Change its approver or decide it on its Approval card.");
                return OrderApprovalResult.NobodyToAsk;
            }

            approval.LastReminderNumber = reminderNumber;
            await _orderApprovalRepository.UpdateAsync(approval);

            await SendRequestsAsync(order, approval, approvers, reminder: true);

            return OrderApprovalResult.Done;
        });
    }

    #endregion

    #region Queries

    public virtual async Task<OrderApproval> GetOrderApprovalByOrderIdAsync(int orderId)
    {
        if (orderId <= 0)
            return null;

        return await _orderApprovalRepository.Table.FirstOrDefaultAsync(approval => approval.OrderId == orderId);
    }

    public virtual async Task<bool> IsOrderHeldAsync(int orderId)
    {
        if (orderId <= 0)
            return false;

        return await _orderApprovalRepository.Table
            .AnyAsync(approval => approval.OrderId == orderId && approval.StatusId == (int)OrderApprovalStatus.Pending);
    }

    public virtual async Task<IList<int>> GetHeldOrderIdsAsync(int[] orderIds)
    {
        if (orderIds == null || orderIds.Length == 0)
            return new List<int>();

        return await _orderApprovalRepository.Table
            .Where(approval => orderIds.Contains(approval.OrderId) && approval.StatusId == (int)OrderApprovalStatus.Pending)
            .Select(approval => approval.OrderId)
            .ToListAsync();
    }

    public virtual async Task<IList<OrderApprovalRequest>> GetRequestsAsync(int orderApprovalId)
    {
        return await _requestRepository.Table
            .Where(request => request.OrderApprovalId == orderApprovalId)
            .OrderByDescending(request => request.SentOnUtc)
            .ThenByDescending(request => request.Id)
            .ToListAsync();
    }

    public virtual async Task<IList<string>> GetWaitingApproverNamesAsync(int orderApprovalId)
    {
        //each approver once, by the latest link
        var requests = (await GetLiveRequestsAsync(orderApprovalId))
            .DistinctBy(request => request.ApproverId)
            .ToList();

        var approverIds = requests.Select(request => request.ApproverId).ToArray();

        var approvers = (await _approverRepository.Table.Where(approver => approverIds.Contains(approver.Id)).ToListAsync())
            .ToDictionary(approver => approver.Id);

        return requests
            .Select(request => PresentName(request, approvers.GetValueOrDefault(request.ApproverId)))
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public virtual async Task<IList<string>> GetWaitingEmailsAsync(int orderApprovalId)
    {
        return (await GetLiveRequestsAsync(orderApprovalId))
            .Select(request => request.ApproverEmail)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .OrderBy(email => email, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public virtual async Task<(OrderApproval approval, OrderApprovalRequest request)> GetByTokenAsync(string token)
    {
        var hash = OrderApprovalTokenHelper.HashToken(token);

        if (hash == null)
            return (null, null);

        var request = await _requestRepository.Table.FirstOrDefaultAsync(item => item.TokenHash == hash);

        if (request == null)
            return (null, null);

        return (await _orderApprovalRepository.GetByIdAsync(request.OrderApprovalId), request);
    }

    #endregion

    #region Placement

    public virtual async Task<bool> HoldOrderAsync(Order order, OrderApprovalRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(requirement);

        if (!requirement.Required)
            return false;

        if (await GetOrderApprovalByOrderIdAsync(order.Id) != null)
            return false;

        var now = DateTime.UtcNow;

        //the row is what holds the order, so it is written before anything is sent
        await _orderApprovalRepository.InsertAsync(new OrderApproval
        {
            OrderId = order.Id,
            Status = OrderApprovalStatus.Pending,
            MeasuredAmount = requirement.Amount,
            RequestedOnUtc = now,
            LastReminderNumber = 0,
            CreatedOnUtc = now
        });

        return true;
    }

    public virtual async Task NotifyHoldAsync(Order order, OrderApprovalRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(requirement);

        var approval = await GetOrderApprovalByOrderIdAsync(order.Id);
        if (approval == null || approval.Status != OrderApprovalStatus.Pending)
            return;

        var approvers = await GetApproversAsync(requirement);

        var (requests, _) = await SendRequestsAsync(order, approval, approvers, reminder: false);
        var approverNames = Names(requests);

        await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.Held"),
            approverNames, string.Join(", ", requirement.RuleIds)));

        await SendCustomerNotificationAsync(order, approval, CWMessageTemplateSystemNames.OrderApprovalAwaitingCustomer, approverNames);
    }

    public virtual async Task MarkCancelledAsync(int orderId)
    {
        var approval = await GetOrderApprovalByOrderIdAsync(orderId);

        if (approval == null || approval.Status != OrderApprovalStatus.Pending)
            return;

        await EndAsCancelledAsync(approval);

        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order != null)
            await AddOrderNoteAsync(order, await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.Cancelled"));
    }

    #endregion

    #region Decisions

    public virtual async Task<OrderApprovalResult> AcceptByTokenAsync(string token)
    {
        var (approval, request) = await GetByTokenAsync(token);

        if (approval == null || request == null)
            return OrderApprovalResult.NotFound;

        return await DecideAsync(approval.OrderId, OrderApprovalStatus.Approved,
            await DecidedByAsync(request), request.ApproverId, null, null, request.Id);
    }

    public virtual async Task<OrderApprovalResult> DeclineByTokenAsync(string token, string reason)
    {
        reason = NormalizeReason(reason);

        if (reason == null)
            return OrderApprovalResult.ReasonRequired;

        var (approval, request) = await GetByTokenAsync(token);

        if (approval == null || request == null)
            return OrderApprovalResult.NotFound;

        return await DecideAsync(approval.OrderId, OrderApprovalStatus.Declined,
            await DecidedByAsync(request), request.ApproverId, null, reason, request.Id);
    }

    public virtual async Task<OrderApprovalResult> AcceptByAdminAsync(int orderId, Customer admin)
    {
        ArgumentNullException.ThrowIfNull(admin);

        return await DecideAsync(orderId, OrderApprovalStatus.Approved, await AdminNameAsync(admin), null, admin.Id, null, null);
    }

    public virtual async Task<OrderApprovalResult> DeclineByAdminAsync(int orderId, Customer admin, string reason)
    {
        ArgumentNullException.ThrowIfNull(admin);

        reason = NormalizeReason(reason);

        if (reason == null)
            return OrderApprovalResult.ReasonRequired;

        return await DecideAsync(orderId, OrderApprovalStatus.Declined, await AdminNameAsync(admin), null, admin.Id, reason, null);
    }

    public virtual Task<OrderApprovalResult> ResendAsync(int orderId, Customer admin)
    {
        ArgumentNullException.ThrowIfNull(admin);

        return RunLockedAsync(orderId, async () =>
        {
            var approval = await GetOrderApprovalByOrderIdAsync(orderId);
            if (approval == null)
                return OrderApprovalResult.NotFound;

            if (approval.Status != OrderApprovalStatus.Pending)
                return OrderApprovalResult.AlreadyDecided;

            var order = await _orderService.GetOrderByIdAsync(orderId);

            if (order == null || order.Deleted || order.OrderStatus == OrderStatus.Cancelled)
            {
                await EndAsCancelledAsync(approval);
                return OrderApprovalResult.AlreadyDecided;
            }

            //those it waits for now: an approver it was changed from is not asked again
            var approvers = await GetRequestedApproversAsync(order, approval, liveLinksOnly: true);

            //no link works (a change or a resend stopped half way): whoever was ever asked
            if (approvers.Count == 0)
                approvers = await GetRequestedApproversAsync(order, approval, liveLinksOnly: false);

            if (approvers.Count == 0)
                return OrderApprovalResult.NobodyToAsk;

            var live = await GetLiveRequestsAsync(approval.Id);
            var (requests, anyQueued) = await SendRequestsAsync(order, approval, approvers, reminder: false);

            //nobody got a new link: the old ones go on working
            if (!anyQueued)
            {
                await RevokeRequestsAsync(requests);
                return OrderApprovalResult.Failed;
            }

            await RevokeRequestsAsync(live);

            approval.RequestedOnUtc = DateTime.UtcNow;
            approval.LastReminderNumber = 0;
            await _orderApprovalRepository.UpdateAsync(approval);

            await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.Resent"), Names(requests), admin.Email));

            return OrderApprovalResult.Done;
        });
    }

    public virtual async Task<OrderApprovalResult> ChangeApproversAsync(int orderId, Customer admin, string emails, string startedFrom)
    {
        ArgumentNullException.ThrowIfNull(admin);

        var wanted = OrderApprovalEmailHelper.Parse(emails);

        if (wanted.Count == 0 || !wanted.All(OrderApprovalEmailHelper.IsValid))
            return OrderApprovalResult.InvalidApprover;

        return await RunLockedAsync(orderId, async () =>
        {
            var approval = await GetOrderApprovalByOrderIdAsync(orderId);
            if (approval == null)
                return OrderApprovalResult.NotFound;

            if (approval.Status != OrderApprovalStatus.Pending)
                return OrderApprovalResult.AlreadyDecided;

            var order = await _orderService.GetOrderByIdAsync(orderId);

            if (order == null || order.Deleted || order.OrderStatus == OrderStatus.Cancelled)
            {
                await EndAsCancelledAsync(approval);
                return OrderApprovalResult.AlreadyDecided;
            }

            var live = await GetLiveRequestsAsync(approval.Id);

            if (!OrderApprovalEmailHelper.Parse(startedFrom)
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase)
                    .SetEquals(live.Select(request => request.ApproverEmail)))
                return OrderApprovalResult.ChangedMeanwhile;

            var added = wanted.Except(live.Select(request => request.ApproverEmail), StringComparer.InvariantCultureIgnoreCase).ToList();
            var removed = live.Where(request => !wanted.Contains(request.ApproverEmail, StringComparer.InvariantCultureIgnoreCase)).ToList();

            if (added.Count == 0 && removed.Count == 0)
                return OrderApprovalResult.Done;

            //the new links before the old ones stop: there is always somebody who can answer
            if (added.Count > 0)
            {
                var approvers = new List<Approver>();

                foreach (var email in added)
                    approvers.Add(await _approvalRuleService.GetOrCreateApproverAsync(order.StoreId, email));

                var (requests, anyQueued) = await SendRequestsAsync(order, approval, approvers, reminder: false);

                //none of the new ones got a link: nothing is changed
                if (!anyQueued)
                {
                    await RevokeRequestsAsync(requests);
                    return OrderApprovalResult.Failed;
                }

                approval.RequestedOnUtc = DateTime.UtcNow;
                approval.LastReminderNumber = 0;
                await _orderApprovalRepository.UpdateAsync(approval);
            }

            await RevokeRequestsAsync(removed);

            await AddOrderNoteAsync(order, string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Note.ApproverChanged"),
                string.Join(", ", await GetWaitingApproverNamesAsync(approval.Id)), admin.Email));

            return OrderApprovalResult.Done;
        });
    }

    #endregion

    #region Task

    public virtual async Task ProcessPendingApprovalsAsync()
    {
        var pending = await _orderApprovalRepository.Table
            .Where(approval => approval.StatusId == (int)OrderApprovalStatus.Pending)
            .OrderBy(approval => approval.Id)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var approval in pending)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(approval.OrderId);

                if (order == null || order.Deleted || order.OrderStatus == OrderStatus.Cancelled)
                {
                    await MarkCancelledAsync(approval.OrderId);
                    continue;
                }

                //an approval already waiting is finished whatever the switch says now
                var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(order.StoreId);
                var daysWaiting = (now - approval.RequestedOnUtc).TotalDays;

                if (settings.ApprovalExpiryDays > 0 && daysWaiting >= settings.ApprovalExpiryDays)
                {
                    await DecideAsync(order.Id, OrderApprovalStatus.Expired,
                        await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.DecidedBy.System"),
                        null, null,
                        string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.ExpiredReason"), settings.ApprovalExpiryDays),
                        null);
                    continue;
                }

                if (settings.ApprovalSecondReminderDays > 0 && approval.LastReminderNumber < 2 && daysWaiting >= settings.ApprovalSecondReminderDays)
                {
                    await SendReminderAsync(order.Id, 2);
                    continue;
                }

                if (settings.ApprovalFirstReminderDays > 0 && approval.LastReminderNumber < 1 && daysWaiting >= settings.ApprovalFirstReminderDays)
                    await SendReminderAsync(order.Id, 1);
            }
            catch (Exception exception)
            {
                await _logger.ErrorAsync($"[Approvals] Order {approval.OrderId}: the pending approval could not be processed.", exception);
            }
        }
    }

    #endregion
}
