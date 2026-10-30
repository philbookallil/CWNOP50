using Nop.Plugin.CW.Core.Domain.Returns;

namespace Nop.Plugin.CW.Core.Services.Returns;

/// <summary>
/// Data access and aggregate invariants for the Return Case model. Business rules (what may be
/// returned, which fees apply, what a return resolves to) live outside this service.
/// </summary>
public interface IReturnCaseService
{
    #region Cases

    /// <summary>
    /// Gets a return case by identifier
    /// </summary>
    /// <param name="returnCaseId">Return case identifier</param>
    /// <returns>A task whose result is the return case, or null when it does not exist</returns>
    Task<ReturnCase> GetReturnCaseByIdAsync(int returnCaseId);

    /// <summary>
    /// Gets the customer's open draft case for an order, if one exists. Used so that re-entering the
    /// return wizard resumes the existing draft instead of starting another.
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="orderId">Order identifier</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task whose result is the most recent draft case, or null when there is none</returns>
    Task<ReturnCase> GetDraftReturnCaseAsync(int customerId, int orderId, int storeId);

    /// <summary>
    /// Inserts a return case
    /// </summary>
    Task InsertReturnCaseAsync(ReturnCase returnCase);

    /// <summary>
    /// Updates a return case, stamping <see cref="ReturnCase.UpdatedOnUtc"/>
    /// </summary>
    Task UpdateReturnCaseAsync(ReturnCase returnCase);

    /// <summary>
    /// Moves a draft case to submitted, assigning its case number and submission timestamp.
    /// </summary>
    /// <param name="returnCase">The draft case to submit</param>
    /// <exception cref="Nop.Core.NopException">
    /// Thrown when the case is not a draft, has no reason or action, or has no lines.
    /// </exception>
    Task SubmitReturnCaseAsync(ReturnCase returnCase);

    #endregion

    #region Lines

    /// <summary>
    /// Gets every line belonging to a case, in creation order
    /// </summary>
    Task<IList<ReturnCaseLine>> GetReturnCaseLinesAsync(int returnCaseId);

    /// <summary>
    /// Gets the line mirrored to a given NOP return request
    /// </summary>
    /// <param name="returnRequestId">NOP return request identifier</param>
    /// <returns>A task whose result is the line, or null when the request predates the case model</returns>
    Task<ReturnCaseLine> GetReturnCaseLineByReturnRequestIdAsync(int returnRequestId);

    /// <summary>
    /// Inserts a return case line
    /// </summary>
    Task InsertReturnCaseLineAsync(ReturnCaseLine returnCaseLine);

    /// <summary>
    /// Updates a return case line
    /// </summary>
    Task UpdateReturnCaseLineAsync(ReturnCaseLine returnCaseLine);

    /// <summary>
    /// Records a staff decision on one returned item, stamping who decided and when.
    /// A rejected line attracts no fees — it is skipped when the case is priced.
    /// </summary>
    /// <param name="returnCaseLine">The line being decided</param>
    /// <param name="disposition">Accepted or rejected</param>
    /// <param name="reason">Staff note explaining the decision; expected when rejecting</param>
    /// <param name="staffCustomerId">The staff member making the decision</param>
    Task SetLineDispositionAsync(ReturnCaseLine returnCaseLine, ReturnLineDisposition disposition,
        string reason, int? staffCustomerId);

    #endregion

    #region Resolutions

    /// <summary>
    /// Gets the resolution recorded for a case, if it has been resolved
    /// </summary>
    /// <param name="returnCaseId">Return case identifier</param>
    /// <returns>A task whose result is the resolution, or null when the case is unresolved</returns>
    Task<ReturnResolution> GetResolutionByCaseIdAsync(int returnCaseId);

    /// <summary>
    /// Updates a resolution
    /// </summary>
    /// <param name="resolution"></param>
    /// <returns></returns>
    Task UpdateResolutionAsync(ReturnResolution resolution);

    /// <summary>
    /// Records the outcome of resolving a case. One per case — its existence is what makes resolving the
    /// same case again a no-op.
    /// </summary>
    Task InsertResolutionAsync(ReturnResolution resolution);

    /// <summary>
    /// Records one priced charge against a resolution.
    /// </summary>
    Task InsertChargeAsync(ReturnCharge charge);

    /// <summary>
    /// The charges priced at resolution time.
    /// </summary>
    Task<IList<ReturnCharge>> GetChargesByResolutionIdAsync(int returnResolutionId);

    /// <summary>
    /// Re-opens a resolved case: deletes its resolution and the charges frozen with it, and puts the case
    /// back to Submitted so it can be decided and re-priced.
    /// </summary>
    /// <remarks>
    /// The escape hatch for a decision that changed after the case resolved — which staff can reach,
    /// because a rejected return stays re-openable. Deliberately
    /// destructive: a frozen charge that no longer describes the case is worse than no charge, and the
    /// resolution is rewritten from the live rules the moment the last line is decided again.
    /// This does not undo a credit note or an exchange order already raised — that is
    /// <see cref="Nop.Plugin.CW.Core.BusinessLogic.IExchangesLogic.UndoReturnRequestApprovalAsync"/>'s job,
    /// and it is a separate decision by the administrator.
    /// </remarks>
    Task ReopenCaseAsync(int returnCaseId);

    #endregion
}