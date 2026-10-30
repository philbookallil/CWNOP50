using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Returns;

/// <summary>
/// Represents one customer return submission: the returned items, the reason and action chosen,
/// and the case-level state. Individual items are held as <see cref="ReturnCaseLine"/> rows.
/// </summary>
public class ReturnCase : BaseEntity
{
    /// <summary>
    /// Gets or sets the store the return belongs to
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets the customer who raised the return
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the order the returned items came from
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable case number shown to customers and staff
    /// </summary>
    public string CaseNumber { get; set; }

    /// <summary>
    /// Gets or sets the return reason identifier (references NOP ReturnRequestReason).
    /// Null while the case is a Draft; required from Submitted onwards.
    /// </summary>
    public int? ReturnReasonId { get; set; }

    /// <summary>
    /// Gets or sets the requested action identifier (references NOP ReturnRequestAction).
    /// Null while the case is a Draft; required from Submitted onwards.
    /// </summary>
    public int? ReturnActionId { get; set; }

    /// <summary>
    /// Gets or sets the case status identifier
    /// </summary>
    public int StatusId { get; set; }

    /// <summary>
    /// Gets or sets the case status
    /// </summary>
    public ReturnCaseStatus Status
    {
        get => (ReturnCaseStatus)StatusId;
        set => StatusId = (int)value;
    }

    /// <summary>
    /// Gets or sets the comments entered by the customer
    /// </summary>
    public string CustomerComments { get; set; }

    /// <summary>
    /// Gets or sets the date and time the case was created (UTC)
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the date and time the case was last updated (UTC)
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the date and time the case was submitted by the customer (UTC).
    /// Null while the case is a Draft.
    /// </summary>
    public DateTime? SubmittedOnUtc { get; set; }
}