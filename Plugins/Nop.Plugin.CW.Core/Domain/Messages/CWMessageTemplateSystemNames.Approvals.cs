namespace Nop.Plugin.CW.Core.Domain.Messages;

public static partial class CWMessageTemplateSystemNames
{
    #region Order approval templates

    /// <summary>
    /// Sent to each approver of an order that needs approval: the order and the link to accept or decline it
    /// </summary>
    public const string OrderApprovalApproverRequest = "OrderApproval.ApproverNotification";

    /// <summary>
    /// Sent to the approvers of an order still waiting, at the store's reminder days, with a new link
    /// </summary>
    public const string OrderApprovalApproverReminder = "OrderApproval.ApproverReminder";

    /// <summary>
    /// Sent to the customer who placed an order that needs approval, naming the approvers asked
    /// </summary>
    public const string OrderApprovalAwaitingCustomer = "OrderApproval.AwaitingCustomerNotification";

    /// <summary>
    /// Sent to the customer when the order is accepted
    /// </summary>
    public const string OrderApprovalApprovedCustomer = "OrderApproval.ApprovedCustomerNotification";

    /// <summary>
    /// Sent to the customer when the order is declined: the reason, and the items back in the cart
    /// </summary>
    public const string OrderApprovalDeclinedCustomer = "OrderApproval.DeclinedCustomerNotification";

    /// <summary>
    /// Sent to the customer when nobody answered in time: the order was cancelled and the items are back in the cart
    /// </summary>
    public const string OrderApprovalExpiredCustomer = "OrderApproval.ExpiredCustomerNotification";

    #endregion
}
