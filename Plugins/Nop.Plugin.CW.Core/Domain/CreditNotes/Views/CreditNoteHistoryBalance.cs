using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.CreditNotes.Views;

/// <summary>
/// Read-only view over CreditNoteHistory with a live-computed running Remain balance per event
/// (oldest-first window function running total). Backed by vw_CORE_EX_CreditNoteHistoryRemain —
/// see CWDatabaseService.
/// </summary>
public class CreditNoteHistoryBalance : BaseEntity
{
    public int CreditNoteId { get; set; }

    public int CustomerId { get; set; }

    public int OrderId { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public decimal Amount { get; set; }

    public decimal Remain { get; set; }
}