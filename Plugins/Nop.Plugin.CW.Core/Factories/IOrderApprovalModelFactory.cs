using Nop.Plugin.CW.Core.Models.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Prepares the page an approver opens from the e-mail
/// </summary>
public interface IOrderApprovalModelFactory
{
    /// <summary>
    /// Prepares the review page of an approval link
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <param name="result">What was just done with the link, to say so on the page; null when it was only opened</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the model
    /// </returns>
    Task<OrderApprovalReviewModel> PrepareReviewModelAsync(string token, OrderApprovalResult? result = null);
}
