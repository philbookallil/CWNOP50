namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// What happened to a decision, a resend or a change of approver
/// </summary>
public enum OrderApprovalResult
{
    /// <summary>
    /// Done
    /// </summary>
    Done = 0,

    /// <summary>
    /// No approval matches the link or the order
    /// </summary>
    NotFound = 10,

    /// <summary>
    /// The approval was already decided, expired or cancelled
    /// </summary>
    AlreadyDecided = 20,

    /// <summary>
    /// The link was replaced by a resend or a change of approver
    /// </summary>
    LinkReplaced = 30,

    /// <summary>
    /// Another change to the same approval was running; nothing was done
    /// </summary>
    Busy = 40,

    /// <summary>
    /// A decline needs a reason
    /// </summary>
    ReasonRequired = 50,

    /// <summary>
    /// The approver's name or e-mail address is missing or not valid
    /// </summary>
    InvalidApprover = 60,

    /// <summary>
    /// The order could not be cancelled or released; the approval is still pending and the error is logged
    /// </summary>
    Failed = 70
}
