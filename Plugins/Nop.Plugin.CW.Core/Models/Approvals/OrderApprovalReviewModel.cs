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

    public string OrderNumber { get; set; }

    public string CustomerName { get; set; }

    public string CreatedOn { get; set; }

    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the stylesheet's name for the status: pending, approved, declined, expired or cancelled
    /// </summary>
    public string StatusClass { get; set; }

    public string DecidedBy { get; set; }

    public string DecisionComment { get; set; }

    /// <summary>
    /// Gets or sets what was ordered: one group per packname, and one without a name for the lines that are for none
    /// </summary>
    public IList<OrderApprovalReviewGroupModel> Groups { get; set; } = [];

    /// <summary>
    /// Gets the number of people ordered for; 0 for an order without packnames
    /// </summary>
    public int PackNameCount => Groups.Count(group => group.HasPackName);

    /// <summary>
    /// Gets a value indicating whether the page shows every person's products at once; an order for many shows a line
    /// per person, opened one by one
    /// </summary>
    public bool GroupsOpen => Groups.Count <= MaxOpenGroups;

    /// <summary>
    /// The most people whose products are all shown at once
    /// </summary>
    public const int MaxOpenGroups = 5;

    public int ItemCount { get; set; }

    public string Subtotal { get; set; }

    /// <summary>
    /// Gets or sets the delivery charge; null when there is none
    /// </summary>
    public string Delivery { get; set; }

    /// <summary>
    /// Gets or sets what else changes the total (discounts, gift cards, reward points, fees), signed; null when nothing does
    /// </summary>
    public string Other { get; set; }

    public string Subsidy { get; set; }

    public string StoreCredit { get; set; }

    public string Payable { get; set; }

    /// <summary>
    /// Gets or sets the reason the approver gives when declining
    /// </summary>
    public string DeclineReason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the decline form is open: a decline that did not go through
    /// </summary>
    public bool DeclineOpen { get; set; }
}

/// <summary>
/// What one packname was ordered, and where it is delivered
/// </summary>
public record OrderApprovalReviewGroupModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets the packname; null for the lines that are for none
    /// </summary>
    public string PackName { get; set; }

    public bool HasPackName => !string.IsNullOrEmpty(PackName);

    public string Colour { get; set; }

    /// <summary>
    /// Gets or sets the branch, department and cost centre, those it has
    /// </summary>
    public string Details { get; set; }

    /// <summary>
    /// Gets or sets how and where it is delivered; null when nothing says
    /// </summary>
    public string Delivery { get; set; }

    public int ItemCount { get; set; }

    public string Total { get; set; }

    public IList<OrderApprovalReviewLineModel> Lines { get; set; } = [];
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

    public int Quantity { get; set; }

    public string UnitPrice { get; set; }

    public string Total { get; set; }
}
