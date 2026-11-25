using Nop.Plugin.CW.Core.Domain.Approvals;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;

/// <summary>
/// An approver a box suggests while an address is typed
/// </summary>
/// <param name="StoreId">The approver's store</param>
/// <param name="Name">The approver's name</param>
/// <param name="Email">The approver's e-mail address</param>
public record ApproverSuggestionModel(int StoreId, string Name, string Email)
{
    /// <summary>
    /// The suggestions of some approvers; one switched off is not suggested
    /// </summary>
    /// <param name="approvers">The approvers</param>
    public static IList<ApproverSuggestionModel> From(IEnumerable<Approver> approvers)
        => approvers
            .Where(approver => approver.Active)
            .Select(approver => new ApproverSuggestionModel(approver.StoreId, approver.Name, approver.Email))
            .ToList();
}
