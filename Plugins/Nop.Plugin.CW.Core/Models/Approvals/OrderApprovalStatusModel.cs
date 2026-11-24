using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Approvals;

/// <summary>
/// Where an order's approval stands, as the customer who placed it reads it
/// </summary>
public record OrderApprovalStatusModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets the status, as a CSS class suffix: pending, approved, declined or expired
    /// </summary>
    public string StatusClass { get; set; }

    /// <summary>
    /// Gets or sets what the customer is told
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets why the order was declined or not approved in time; null otherwise
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets what is said of the subsidy and store credit of an order that waits; null when neither paid for it
    /// </summary>
    public string ReservedMessage { get; set; }
}
