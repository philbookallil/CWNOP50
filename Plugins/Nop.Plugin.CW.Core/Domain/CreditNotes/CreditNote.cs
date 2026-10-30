using LinqToDB.Mapping;
using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.CreditNotes;

public class CreditNote : BaseEntity
{
    public int CustomerId { get; set; }

    public int OrderId { get; set; }

    public long? Reference { get; set; }

    /// <summary>
    /// Gets or sets the The Name of the Credit Note
    /// </summary>
    public string Name { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the The Reason of the Return
    /// </summary>
    public string ReturnReason { get; set; }

    public int StoreId { get; set; }

    public int? ReturnRequestId { get; set; }

    /// <summary>
    /// Gets or sets the customer identifier of who issued this credit note (the admin who created a
    /// manual credit note, or the acting user when the returns workflow generated it). Null when
    /// there is no specific acting user.
    /// </summary>
    public int? CreatedByCustomerId { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    #region Custom properties

    /// <summary>
    /// Gets or sets the remaining balance of this credit note.
    /// NOT a stored column — computed live by <c>vw_CORE_EX_CreditNoteRemain</c> as Amount minus
    /// every CreditNoteHistory row belonging to a non-cancelled order. <see cref="Services.CreditNotes.CreditNotesService"/>
    /// populates it after querying that view. Never gets written back via IRepository&lt;CreditNote&gt;.
    /// </summary>
    [NotColumn]
    public decimal Remain { get; set; }

    #endregion
}