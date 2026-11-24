using Nop.Core.Domain.Messages;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Installs the e-mails and the scheduled task of the order approvals
/// </summary>
public class OrderApprovalInstallService : IOrderApprovalInstallService
{
    #region Fields

    protected readonly EmailAccountSettings _emailAccountSettings;
    protected readonly IEmailAccountService _emailAccountService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IMessageTemplateService _messageTemplateService;
    protected readonly IScheduleTaskService _scheduleTaskService;

    #endregion

    #region Ctor

    public OrderApprovalInstallService(EmailAccountSettings emailAccountSettings,
        IEmailAccountService emailAccountService,
        ILocalizationService localizationService,
        IMessageTemplateService messageTemplateService,
        IScheduleTaskService scheduleTaskService)
    {
        _emailAccountSettings = emailAccountSettings;
        _emailAccountService = emailAccountService;
        _localizationService = localizationService;
        _messageTemplateService = messageTemplateService;
        _scheduleTaskService = scheduleTaskService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The e-mails as installed: name, subject, body and the description the admin template list shows
    /// </summary>
    protected static (string Name, string Subject, string Body, string Description)[] Templates =>
    [
        (CWMessageTemplateSystemNames.OrderApprovalApproverRequest,
            "%Store.Name%: order %Order.OrderNumber% needs your approval",
            "Hello %OrderApproval.ApproverName%,<br /><br />" +
            "%Order.CustomerFullName% placed order %Order.OrderNumber% at %Store.Name% on %Order.CreatedOn%, and it needs your approval.<br />" +
            "Order total: %OrderApproval.Amount%<br /><br />" +
            "%Order.Product(s)%<br />" +
            "<a href=\"%OrderApproval.Link%\">Review the order and accept or decline it</a><br /><br />" +
            "The order is not processed until it is accepted.<br />",
            "Sent to each approver of an order that needs approval: the order, and a link to accept or decline it."),
        (CWMessageTemplateSystemNames.OrderApprovalApproverReminder,
            "Reminder - %Store.Name%: order %Order.OrderNumber% is waiting for your approval",
            "Hello %OrderApproval.ApproverName%,<br /><br />" +
            "Order %Order.OrderNumber%, placed by %Order.CustomerFullName% on %Order.CreatedOn%, is still waiting for your approval.<br />" +
            "Order total: %OrderApproval.Amount%<br /><br />" +
            "%Order.Product(s)%<br />" +
            "<a href=\"%OrderApproval.Link%\">Review the order and accept or decline it</a><br />",
            "Sent to the approvers of an order still waiting for approval, at the reminder days of the store's settings."),
        (CWMessageTemplateSystemNames.OrderApprovalAwaitingCustomer,
            "%Store.Name%: order %Order.OrderNumber% is waiting for approval",
            "Hello %Order.CustomerFullName%,<br /><br />" +
            "Thank you for your order %Order.OrderNumber%. It needs approval before it is processed, and has been sent to %OrderApproval.Approvers%.<br />" +
            "We will let you know when they decide.<br /><br />" +
            "%Order.Product(s)%<br />",
            "Sent to the customer who placed an order that needs approval, naming the approvers asked."),
        (CWMessageTemplateSystemNames.OrderApprovalApprovedCustomer,
            "%Store.Name%: order %Order.OrderNumber% is approved",
            "Hello %Order.CustomerFullName%,<br /><br />" +
            "Your order %Order.OrderNumber% was approved by %OrderApproval.DecidedBy% and is now being processed.<br /><br />" +
            "%Order.Product(s)%<br />",
            "Sent to the customer when an order waiting for approval is accepted."),
        (CWMessageTemplateSystemNames.OrderApprovalDeclinedCustomer,
            "%Store.Name%: order %Order.OrderNumber% was declined",
            "Hello %Order.CustomerFullName%,<br /><br />" +
            "Your order %Order.OrderNumber% was declined by %OrderApproval.DecidedBy%:<br />" +
            "<i>%OrderApproval.Reason%</i><br /><br />" +
            "The order was cancelled and its items are back in your cart, so you can change it and order again.<br /><br />" +
            "%Order.Product(s)%<br />",
            "Sent to the customer when an order waiting for approval is declined: the reason, and the items back in the cart."),
        (CWMessageTemplateSystemNames.OrderApprovalExpiredCustomer,
            "%Store.Name%: order %Order.OrderNumber% was not approved in time",
            "Hello %Order.CustomerFullName%,<br /><br />" +
            "Your order %Order.OrderNumber% was not approved in time: %OrderApproval.Reason%<br /><br />" +
            "The order was cancelled and its items are back in your cart, so you can order again.<br /><br />" +
            "%Order.Product(s)%<br />",
            "Sent to the customer when an order waiting for approval is not answered within the expiry days of the store's settings.")
    ];

    #endregion

    #region Methods

    public virtual async Task InstallOrUpdateAsync()
    {
        var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
            ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

        foreach (var template in Templates)
        {
            if ((await _messageTemplateService.GetMessageTemplatesByNameAsync(template.Name)).Any())
                continue;

            await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
            {
                Name = template.Name,
                Subject = template.Subject,
                Body = template.Body,
                IsActive = true,
                EmailAccountId = emailAccount?.Id ?? 0
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(
                $"Admin.ContentManagement.MessageTemplates.Description.{template.Name}", template.Description);
        }

        if (await _scheduleTaskService.GetTaskByTypeAsync(OrderApprovalDefaults.TaskTypeFullName) != null)
            return;

        await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
        {
            Name = OrderApprovalDefaults.TaskName,
            Seconds = OrderApprovalDefaults.TaskSeconds,
            Type = OrderApprovalDefaults.TaskTypeFullName,
            Enabled = true,
            LastEnabledUtc = DateTime.UtcNow,
            StopOnError = false
        });
    }

    public virtual async Task UninstallAsync()
    {
        var task = await _scheduleTaskService.GetTaskByTypeAsync(OrderApprovalDefaults.TaskTypeFullName);

        if (task != null)
            await _scheduleTaskService.DeleteTaskAsync(task);
    }

    #endregion
}
