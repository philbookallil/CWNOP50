using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;

/// <summary>
/// The approval card of the admin order page
/// </summary>
public record OrderApprovalAdminModel : BaseNopModel
{
    public int OrderId { get; set; }

    public string Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the order is held
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the admin may decide, resend and change the approver
    /// </summary>
    public bool CanManage { get; set; }

    public string MeasuredAmount { get; set; }

    public string RequestedOn { get; set; }

    public int LastReminderNumber { get; set; }

    public string DecidedBy { get; set; }

    public string DecidedOn { get; set; }

    public string DecisionComment { get; set; }

    public IList<OrderApprovalRequestAdminModel> Requests { get; set; } = [];
}

/// <summary>
/// One approval link sent for an order
/// </summary>
public record OrderApprovalRequestAdminModel : BaseNopModel
{
    public string ApproverName { get; set; }

    public string ApproverEmail { get; set; }

    public string SentOn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link still works
    /// </summary>
    public bool IsLive { get; set; }
}
