using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals.DTOs;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// The approvers of the stores: who they are, what they approve for, and their changes
/// </summary>
public interface IApproverService
{
    /// <summary>
    /// Searches the approvers, by name
    /// </summary>
    /// <param name="storeId">Store identifier; 0 for every store</param>
    /// <param name="text">Part of the name or the e-mail address; empty for all</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the approvers
    /// </returns>
    Task<IPagedList<Approver>> SearchApproversAsync(int storeId = 0, string text = null, int pageIndex = 0, int pageSize = int.MaxValue);

    /// <summary>
    /// Gets the rules that name some approvers, in force or not
    /// </summary>
    /// <param name="approverIds">Approver identifiers</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains a row per approver and rule
    /// </returns>
    Task<IList<ApproverRuleDTO>> GetRulesAsync(int[] approverIds);

    /// <summary>
    /// Gets how many orders wait for some approvers, and how many they decided
    /// </summary>
    /// <param name="approverIds">Approver identifiers</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the counts of each approver
    /// </returns>
    Task<IDictionary<int, ApproverCountsDTO>> GetCountsAsync(int[] approverIds);

    /// <summary>
    /// Changes an approver's name and e-mail address. Orders already waiting keep the links already sent.
    /// </summary>
    /// <param name="approverId">Approver identifier</param>
    /// <param name="name">Name</param>
    /// <param name="email">E-mail address</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what came of it
    /// </returns>
    Task<ApproverChangeResult> UpdateApproverAsync(int approverId, string name, string email);

    /// <summary>
    /// Puts another approver in every rule that names this one. Orders already waiting keep the approvers they asked.
    /// </summary>
    /// <param name="approverId">The approver replaced</param>
    /// <param name="email">E-mail address of the approver who takes the place; created when the store has none with it</param>
    /// <param name="name">Name for a new approver; null to name it by the address</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what came of it
    /// </returns>
    Task<ApproverChangeResult> ReplaceApproverAsync(int approverId, string email, string name = null);

    /// <summary>
    /// Deletes an approver that no rule in force names and no order waits for
    /// </summary>
    /// <param name="approverId">Approver identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what came of it
    /// </returns>
    Task<ApproverChangeResult> DeleteApproverAsync(int approverId);
}
