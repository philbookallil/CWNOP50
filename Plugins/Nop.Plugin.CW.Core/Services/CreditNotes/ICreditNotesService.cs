using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.CreditNotes;

namespace Nop.Plugin.CW.Core.Services.CreditNotes;

public interface ICreditNotesService
{
    /// <summary>
    /// Applies the customer's open credit notes to an order, up to <paramref name="settleableAmount"/>.
    /// </summary>
    /// <param name="settleableAmount">
    /// The amount still to settle, incl tax. Always supplied by the caller — an order's own OrderTotal is
    /// the payable balance after settlement, not the amount to settle with.
    /// </param>
    Task<(decimal AppliedTotal, decimal PendingOrderBalance)> ApplyCreditNotesForOrderAsync(Order order, decimal settleableAmount);

    Task<(IDictionary<int, decimal> Applied, decimal AppliedTotal)> CalculateAppliedCreditNotesAsync(int customerId, decimal orderTotal, bool bypassCache = false);

    /// <param name="createdByCustomerId">
    /// The identifier of the staff member/system actor who triggered this credit note (e.g. the admin
    /// processing the return). Null when there is no specific acting user.
    /// </param>
    Task CreateCreditNotesForOrderAsync(Order order, decimal returnForCredit, string returnReason, int? returnRequestId = null, int? createdByCustomerId = null);

    Task CreateManualCreditNoteAsync(int customerId, int orderId, decimal amount, string reason, Customer? createdByUser = null);

    Task<bool> CreditNoteHasHistoryAsync(int creditNoteId);

    Task<bool> CustomerHasCreditNotesBalanceAsync(int customerId);

    Task DeleteCreditNoteAsync(CreditNote creditNote);

    Task<CreditNote?> GetCreditNoteByIdAsync(int creditNoteId);

    Task<IList<CreditNoteHistory>> GetCreditNoteHistoriesByCreditNoteAsync(int creditNoteId);

    Task<IList<CreditNoteHistory>> GetCreditNoteHistoriesByCustomerIdAsync(int customerId);

    Task<IList<CreditNoteHistory>> GetCreditNoteHistoriesByOrderIdAsync(int orderId);

    Task<IList<CreditNote>> GetOpenCreditNotesByCustomerIdAsync(int customerId);

    Task<IPagedList<CreditNote>> SearchCreditNotesByCustomerIdAsync(int customerId, int pageIndex = 0, int pageSize = int.MaxValue);

    Task<IList<CreditNote>> GetCreditNotesByStoreAsync(int storeId);

    /// <summary>
    /// Gets a value indicating whether the customer has any credit note or credit note history,
    /// regardless of remaining balance.
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>A task whose result is true when any credit note record exists</returns>
    Task<bool> CustomerHasCreditNotesAsync(int customerId);

    Task UndoCreditNoteForReturnRequestAsync(int returnRequestId);

    /// <summary>
    /// The credit note raised for a return request, or null when none was — the ordinary outcome for a
    /// line that was paid for by subsidy, because the subsidy is given back in kind instead. Returns the most recent note when a case was undone and re-approved.
    /// </summary>
    Task<CreditNote?> GetCreditNoteByReturnRequestIdAsync(int returnRequestId);

    Task UpdateCreditNoteAsync(CreditNote creditNote);
}