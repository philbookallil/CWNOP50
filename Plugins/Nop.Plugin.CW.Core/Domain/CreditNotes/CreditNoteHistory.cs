using LinqToDB.Mapping;
using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.CreditNotes;

/// <summary>
/// Represents a credit note history record
/// </summary>
public class CreditNoteHistory : BaseEntity
{
    /// <summary>
    /// Gets or sets the credit note identifier
    /// </summary>
    public int CreditNoteId { get; set; }

    /// <summary>
    /// Gets or sets the customer identifier
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the order identifier
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Gets or sets the amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the running remaining balance of the parent credit note immediately after this
    /// event. NOT a stored column — computed live by <c>vw_CORE_EX_CreditNoteHistoryRemain</c> as a
    /// running total (oldest-first window function).
    /// </summary>
    [NotColumn]
    public decimal Remain { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the credit note history entry was created in UTC
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }
}