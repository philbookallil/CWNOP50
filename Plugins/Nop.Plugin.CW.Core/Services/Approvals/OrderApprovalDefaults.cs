using Nop.Plugin.CW.Core.Domain.Messages;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The fixed values of the order approvals
/// </summary>
public static class OrderApprovalDefaults
{
    /// <summary>
    /// The route of an approver's review page: the link in the approval e-mails
    /// </summary>
    public const string ReviewRouteName = "Plugin.CW.Core.OrderApproval.Review";

    /// <summary>
    /// The path of the review page, without the token
    /// </summary>
    public const string ReviewPath = "orderapproval";

    /// <summary>
    /// The route the review page posts an acceptance to
    /// </summary>
    public const string AcceptRouteName = "Plugin.CW.Core.OrderApproval.Accept";

    /// <summary>
    /// The route the review page posts a decline to
    /// </summary>
    public const string DeclineRouteName = "Plugin.CW.Core.OrderApproval.Decline";

    /// <summary>
    /// How many random bytes an approval link's token carries
    /// </summary>
    public const int TokenBytes = 32;

    /// <summary>
    /// The longest decline reason kept
    /// </summary>
    public const int DecisionCommentMaxLength = 2000;

    /// <summary>
    /// The lock that lets one change at a time act on an order's approval: {0} is the order id
    /// </summary>
    public const string DecisionLockKeyFormat = "CW.orderapproval.decide.{0}";

    /// <summary>
    /// How long a decision may hold its lock: it cancels the order and puts its lines back in the cart
    /// </summary>
    public static TimeSpan DecisionLockTime => TimeSpan.FromSeconds(60);

    /// <summary>
    /// The type name nop's scheduler finds the task by
    /// </summary>
    public static string TaskTypeFullName => typeof(ProcessOrderApprovalsTask).FullName;

    /// <summary>
    /// The task's name on System › Schedule tasks
    /// </summary>
    public const string TaskName = "Process order approvals";

    /// <summary>
    /// How often the task runs: hourly
    /// </summary>
    public const int TaskSeconds = 60 * 60;

    /// <summary>
    /// The e-mails the order approvals send, each installed once
    /// </summary>
    public static string[] MessageTemplates =>
    [
        CWMessageTemplateSystemNames.OrderApprovalApproverRequest,
        CWMessageTemplateSystemNames.OrderApprovalApproverReminder,
        CWMessageTemplateSystemNames.OrderApprovalAwaitingCustomer,
        CWMessageTemplateSystemNames.OrderApprovalApprovedCustomer,
        CWMessageTemplateSystemNames.OrderApprovalDeclinedCustomer,
        CWMessageTemplateSystemNames.OrderApprovalExpiredCustomer
    ];
}
