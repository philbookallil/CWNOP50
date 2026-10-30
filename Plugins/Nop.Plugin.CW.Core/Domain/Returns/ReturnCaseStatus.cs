namespace Nop.Plugin.CW.Core.Domain.Returns;

/// <summary>
/// Represents the lifecycle of a return case (the customer's submission as a whole).
/// Per-item accept/reject is tracked separately by <see cref="ReturnLineDisposition"/>.
/// </summary>
public enum ReturnCaseStatus
{
    /// <summary>
    /// Being filled in by the customer; not yet submitted. Reason and action may not be set yet.
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Submitted by the customer and awaiting review.
    /// </summary>
    Submitted = 2,

    /// <summary>
    /// Reviewed and acted upon. A resolution exists.
    /// </summary>
    Resolved = 3,

    /// <summary>
    /// Abandoned before submission, or cancelled afterwards by an administrator.
    /// </summary>
    Cancelled = 4
}