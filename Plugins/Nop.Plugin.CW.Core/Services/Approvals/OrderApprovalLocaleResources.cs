namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The text of the order approvals, installed with the plugin's other resources
/// </summary>
public static class OrderApprovalLocaleResources
{
    /// <summary>
    /// Every resource of the order approvals, by key
    /// </summary>
    public static Dictionary<string, string> Resources => new()
    {
        //settings
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalsSection"] = "Order approvals",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalsEnable"] = "Enable order approvals",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalsEnable.Hint"] = "Orders placed on account by staff of a branch or department that requires approval wait for an approver, who accepts or declines them from an e-mail link. Orders already waiting are finished whether this is on or off.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalPaymentMethods"] = "Payment methods that wait for approval",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalPaymentMethods.Hint"] = "Only orders paid with these methods can be held. List methods that do not take the money at checkout (account, purchase order): declining an order cancels it and does not refund a payment. An order paid any other way, or with nothing left to pay, is never held.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalFirstReminderDays"] = "First reminder after (days)",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalFirstReminderDays.Hint"] = "Days after the request before the approvers are reminded. 0 sends no first reminder.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalSecondReminderDays"] = "Second reminder after (days)",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalSecondReminderDays.Hint"] = "Days after the request before the approvers are reminded again. 0 sends no second reminder.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalExpiryDays"] = "Expire after (days)",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalExpiryDays.Hint"] = "Days after the request before an unanswered order is cancelled and its items go back to the customer's cart. 0 never expires an order.",
        ["Plugins.CW.Core.Admin.MultiStores.Settings.ApprovalDays.NotNegative"] = "Enter 0 or a number of days.",

        //branch and department forms
        ["Plugins.CW.Core.Admin.ApprovalRule.Title"] = "Order approval",
        ["Plugins.CW.Core.Admin.ApprovalRule.Required"] = "Employees require authorisation?",
        ["Plugins.CW.Core.Admin.ApprovalRule.Required.Hint"] = "The orders its employees place on account wait for one of the authorising e-mails to accept them.",
        ["Plugins.CW.Core.Admin.ApprovalRule.Threshold"] = "Authorise over $",
        ["Plugins.CW.Core.Admin.ApprovalRule.Threshold.Hint"] = "The order total that once exceeded needs authorisation: what is left to pay after subsidy, store credit, discounts and gift cards. Empty or 0 means every order.",
        ["Plugins.CW.Core.Admin.ApprovalRule.Threshold.Invalid"] = "Enter 0 or an amount.",
        ["Plugins.CW.Core.Admin.ApprovalRule.Emails"] = "Authorising emails",
        ["Plugins.CW.Core.Admin.ApprovalRule.Emails.Hint"] = "The people who may authorise an order, separated by commas. Each gets the order by e-mail, and the first to answer decides. A change applies to new orders; orders already waiting keep the people they were sent to.",
        ["Plugins.CW.Core.Admin.ApprovalRule.Emails.Required"] = "Enter at least one e-mail address.",
        ["Plugins.CW.Core.Admin.ApprovalRule.Emails.Invalid"] = "Enter valid e-mail addresses, separated by commas.",

        //customer's order pages
        ["Plugins.CW.Core.OrderApproval.Customer.OrderStatus"] = "{0} - waiting for approval",
        ["Plugins.CW.Core.OrderApproval.Customer.Waiting"] = "This order is waiting for approval.",
        ["Plugins.CW.Core.OrderApproval.Customer.WaitingBy"] = "This order is waiting for approval by {0}.",
        ["Plugins.CW.Core.OrderApproval.Customer.Reserved"] = "Any subsidy or store credit it used comes back if it is not approved.",
        ["Plugins.CW.Core.OrderApproval.Customer.Approved"] = "This order was approved by {0}.",
        ["Plugins.CW.Core.OrderApproval.Customer.Declined"] = "This order was declined by {0}. Its available items went back to your cart.",
        ["Plugins.CW.Core.OrderApproval.Customer.Expired"] = "This order was not approved in time. Its available items went back to your cart.",

        //order notes
        ["Plugins.CW.Core.OrderApproval.Note.Held"] = "The order is waiting for approval by {0} (approval rules {1}).",
        ["Plugins.CW.Core.OrderApproval.Note.Approved"] = "The order was approved by {0}.",
        ["Plugins.CW.Core.OrderApproval.Note.Declined"] = "The order was declined by {0}: {1}",
        ["Plugins.CW.Core.OrderApproval.Note.Expired"] = "The order's approval expired ({0}): {1}",
        ["Plugins.CW.Core.OrderApproval.Note.ReOrderWarnings"] = "Some items could not go back to the customer's cart: {0}",
        ["Plugins.CW.Core.OrderApproval.Note.Cancelled"] = "The order was cancelled or deleted while it waited for approval; its approval links no longer work.",
        ["Plugins.CW.Core.OrderApproval.Note.Resent"] = "The approval links were sent again to {0} by {1}.",
        ["Plugins.CW.Core.OrderApproval.Note.ApproverChanged"] = "The approver was changed to {0} by {1}.",
        ["Plugins.CW.Core.OrderApproval.Note.EmailFailed"] = "The approval e-mail to {0} could not be queued (see the log). Send the links again from the Approval card.",
        ["Plugins.CW.Core.OrderApproval.Note.NotifyFailed"] = "The order is waiting for approval, but its approval e-mails could not be sent (see the log). Send the links again or change the approver from the Approval card.",
        ["Plugins.CW.Core.OrderApproval.Note.HoldFailed"] = "This order could not be checked for approval, or its approval could not be recorded (see the log): the order is NOT held. Check it before it is processed.",
        ["Plugins.CW.Core.OrderApproval.Note.ReOrderFailed"] = "The order was cancelled, but its items could not be put back in the customer's cart (see the log).",
        ["Plugins.CW.Core.OrderApproval.ReOrderHeld"] = "This order is waiting for approval and cannot be ordered again until it is decided.",

        //who decided
        ["Plugins.CW.Core.OrderApproval.DecidedBy.Admin"] = "{0} (admin, on the approvers' behalf)",
        ["Plugins.CW.Core.OrderApproval.DecidedBy.System"] = "The system",
        ["Plugins.CW.Core.OrderApproval.DecidedBy.OrderCancelled"] = "Order cancelled",
        ["Plugins.CW.Core.OrderApproval.ExpiredReason"] = "No approver answered within {0} days.",

        //statuses
        ["Plugins.CW.Core.OrderApproval.Status.Pending"] = "Waiting for approval",
        ["Plugins.CW.Core.OrderApproval.Status.Approved"] = "Approved",
        ["Plugins.CW.Core.OrderApproval.Status.Declined"] = "Declined",
        ["Plugins.CW.Core.OrderApproval.Status.Expired"] = "Expired",
        ["Plugins.CW.Core.OrderApproval.Status.Cancelled"] = "Cancelled",

        //approver's page
        ["Plugins.CW.Core.OrderApproval.PageTitle"] = "Order approval",
        ["Plugins.CW.Core.OrderApproval.OrderTitle"] = "Order {0}",
        ["Plugins.CW.Core.OrderApproval.PlacedBy"] = "Placed by {0} on {1}",
        ["Plugins.CW.Core.OrderApproval.DecidedBy"] = "Decided by:",
        ["Plugins.CW.Core.OrderApproval.DecisionComment"] = "Reason:",
        ["Plugins.CW.Core.OrderApproval.Column.Product"] = "Product",
        ["Plugins.CW.Core.OrderApproval.Column.Price"] = "Price",
        ["Plugins.CW.Core.OrderApproval.Column.Quantity"] = "Qty.",
        ["Plugins.CW.Core.OrderApproval.Column.Total"] = "Total",
        ["Plugins.CW.Core.OrderApproval.People"] = "People",
        ["Plugins.CW.Core.OrderApproval.Items"] = "Items",
        ["Plugins.CW.Core.OrderApproval.ItemsOne"] = "1 item",
        ["Plugins.CW.Core.OrderApproval.ItemsMany"] = "{0} items",
        ["Plugins.CW.Core.OrderApproval.ExpandAll"] = "Expand all",
        ["Plugins.CW.Core.OrderApproval.CollapseAll"] = "Collapse all",
        ["Plugins.CW.Core.OrderApproval.OtherProducts"] = "Other products",
        ["Plugins.CW.Core.OrderApproval.Totals.Subtotal"] = "Products",
        ["Plugins.CW.Core.OrderApproval.Totals.Delivery"] = "Delivery",
        ["Plugins.CW.Core.OrderApproval.Totals.Other"] = "Discounts and fees",
        ["Plugins.CW.Core.OrderApproval.Totals.Subsidy"] = "Subsidy",
        ["Plugins.CW.Core.OrderApproval.Totals.StoreCredit"] = "Store credit",
        ["Plugins.CW.Core.OrderApproval.Totals.Payable"] = "Payable",
        ["Plugins.CW.Core.OrderApproval.Accept"] = "Accept order",
        ["Plugins.CW.Core.OrderApproval.Decline"] = "Decline order",
        ["Plugins.CW.Core.OrderApproval.DeclineReason"] = "Reason (the customer will read this)",
        ["Plugins.CW.Core.OrderApproval.ConfirmDecline"] = "Confirm decline",
        ["Plugins.CW.Core.OrderApproval.Result.Accepted"] = "Thank you. The order is approved and will now be processed.",
        ["Plugins.CW.Core.OrderApproval.Result.Declined"] = "The order is declined. The customer has been told why, and the items are back in their cart.",
        ["Plugins.CW.Core.OrderApproval.Result.NotFound"] = "This approval link is not valid.",
        ["Plugins.CW.Core.OrderApproval.Result.AlreadyDecided"] = "This order has already been decided.",
        ["Plugins.CW.Core.OrderApproval.Result.LinkReplaced"] = "This link was replaced by a newer e-mail. Please use the link in the latest e-mail.",
        ["Plugins.CW.Core.OrderApproval.Result.Busy"] = "The order is being decided right now. Please reload this page in a moment.",
        ["Plugins.CW.Core.OrderApproval.Result.Failed"] = "The decision could not be completed. The order is still waiting; please try again later.",
        ["Plugins.CW.Core.OrderApproval.Result.ReasonRequired"] = "Please say why the order is declined.",

        //admin order page
        ["Plugins.CW.Core.Admin.OrderApproval.Title"] = "Approval",
        ["Plugins.CW.Core.Admin.OrderApproval.Status"] = "Status",
        ["Plugins.CW.Core.Admin.OrderApproval.MeasuredAmount"] = "Order total when placed",
        ["Plugins.CW.Core.Admin.OrderApproval.RequestedOn"] = "Requested on",
        ["Plugins.CW.Core.Admin.OrderApproval.Reminders"] = "Reminders sent",
        ["Plugins.CW.Core.Admin.OrderApproval.DecidedBy"] = "Decided by",
        ["Plugins.CW.Core.Admin.OrderApproval.DecisionComment"] = "Reason",
        ["Plugins.CW.Core.Admin.OrderApproval.Requests.Approver"] = "Approver",
        ["Plugins.CW.Core.Admin.OrderApproval.Requests.Email"] = "E-mail",
        ["Plugins.CW.Core.Admin.OrderApproval.Requests.SentOn"] = "Sent on",
        ["Plugins.CW.Core.Admin.OrderApproval.Requests.Live"] = "Link works",
        ["Plugins.CW.Core.Admin.OrderApproval.Reason"] = "Reason (to decline)",
        ["Plugins.CW.Core.Admin.OrderApproval.Approve"] = "Approve",
        ["Plugins.CW.Core.Admin.OrderApproval.Decline"] = "Decline",
        ["Plugins.CW.Core.Admin.OrderApproval.Resend"] = "Send the links again",
        ["Plugins.CW.Core.Admin.OrderApproval.ChangeApprover"] = "Change approver",
        ["Plugins.CW.Core.Admin.OrderApproval.ApproverName"] = "Name",
        ["Plugins.CW.Core.Admin.OrderApproval.ApproverEmail"] = "E-mail",
        ["Plugins.CW.Core.Admin.OrderApproval.ChangeApprover.Button"] = "Change and send",
        ["Plugins.CW.Core.Admin.OrderApproval.Confirm.Approve"] = "Approve this order on the approvers' behalf?",
        ["Plugins.CW.Core.Admin.OrderApproval.Confirm.Decline"] = "Decline this order on the approvers' behalf? It will be cancelled and its items will go back to the customer's cart.",
        ["Plugins.CW.Core.Admin.OrderApproval.Confirm.Resend"] = "Send the approvers new links? The links already sent will stop working.",
        ["Plugins.CW.Core.Admin.OrderApproval.Confirm.Change"] = "Replace the approvers with this one? The links already sent will stop working.",
        ["Plugins.CW.Core.Admin.OrderApproval.OrderHeld"] = "This order is waiting for approval. Approve or decline it on its Approval card first.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.Done"] = "Done.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.NotFound"] = "This order has no approval.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.AlreadyDecided"] = "This order's approval has already been decided.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.LinkReplaced"] = "The link was replaced.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.Busy"] = "The order's approval is being changed right now. Please reload the page and try again.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.ReasonRequired"] = "Please enter the reason for declining.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.InvalidApprover"] = "Please enter the approver's name and a valid e-mail address.",
        ["Plugins.CW.Core.Admin.OrderApproval.Result.Failed"] = "The action could not be completed; the order is still waiting. See the log for details."
    };
}
