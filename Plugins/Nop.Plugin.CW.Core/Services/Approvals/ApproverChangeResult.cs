namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// What came of a change to an approver
/// </summary>
public enum ApproverChangeResult
{
    /// <summary>
    /// The change was made
    /// </summary>
    Done,

    /// <summary>
    /// There is no such approver
    /// </summary>
    NotFound,

    /// <summary>
    /// The name or the e-mail address is missing or not valid
    /// </summary>
    Invalid,

    /// <summary>
    /// Another approver of the store has the e-mail address
    /// </summary>
    EmailTaken,

    /// <summary>
    /// An approver cannot be replaced with itself
    /// </summary>
    SameApprover,

    /// <summary>
    /// No rule names the approver, so there is nothing to put another in
    /// </summary>
    NothingToReplace,

    /// <summary>
    /// A rule in force names the approver, or an order waits for it
    /// </summary>
    InUse
}
