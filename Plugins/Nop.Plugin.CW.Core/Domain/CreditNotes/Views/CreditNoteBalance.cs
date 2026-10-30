using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.CreditNotes.Views;

/// <summary>
/// Read-only view over CreditNote with a live-computed Remain balance (Amount minus every
/// CreditNoteHistory row belonging to a non-cancelled order). Backed by
/// vw_CORE_EX_CreditNoteRemain — see CWDatabaseService. Query-only: never insert/update against
/// this entity, write through IRepository&lt;CreditNote&gt; instead.
/// </summary>
public class CreditNoteBalance : BaseEntity
{
    public int CustomerId { get; set; }

    public int OrderId { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public long? Reference { get; set; }

    public string Name { get; set; }

    public decimal Amount { get; set; }

    public decimal Remain { get; set; }

    public string ReturnReason { get; set; }

    public int StoreId { get; set; }

    public int? ReturnRequestId { get; set; }

    public int? CreatedByCustomerId { get; set; }
}