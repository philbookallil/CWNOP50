using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Approvals;

/// <summary>
/// The page an approver opens from the e-mail: the order, and accept or decline while it waits
/// </summary>
public record OrderApprovalReviewModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets the token of the link, posted back with the decision
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link still decides the order
    /// </summary>
    public bool CanDecide { get; set; }

    /// <summary>
    /// Gets or sets what the page says above the order: why it cannot be decided, or what was just decided
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Message"/> reports an error
    /// </summary>
    public bool MessageIsError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the order is shown; not for an unknown link
    /// </summary>
    public bool ShowOrder { get; set; }

    public string ApproverName { get; set; }

    public string OrderNumber { get; set; }

    public string CustomerName { get; set; }

    public string CreatedOn { get; set; }

    public string Status { get; set; }

    public string DecidedBy { get; set; }

    public string DecisionComment { get; set; }

    public IList<OrderApprovalReviewLineModel> Lines { get; set; } = [];

    public string Gross { get; set; }

    public string Subsidy { get; set; }

    public string StoreCredit { get; set; }

    public string Payable { get; set; }

    /// <summary>
    /// Gets or sets the reason the approver gives when declining
    /// </summary>
    public string DeclineReason { get; set; }
}

/// <summary>
/// One line of the order an approver reviews
/// </summary>
public record OrderApprovalReviewLineModel : BaseNopModel
{
    public string ProductName { get; set; }

    /// <summary>
    /// Gets or sets the attributes as nop formats them for the order: HTML
    /// </summary>
    public string AttributeInfo { get; set; }

    public string PackName { get; set; }

    public int Quantity { get; set; }

    public string UnitPrice { get; set; }

    public string Total { get; set; }
}
