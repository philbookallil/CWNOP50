using Nop.Core;
using Nop.Plugin.Payments.NABGateway.Domain;

namespace Nop.Plugin.Payments.NABGateway.Services;

/// <summary>
/// Service for persisting and querying NAB Transact XML transactions.
/// </summary>
public interface INABXMLTransactionService
{
    /// <summary>
    /// Inserts a transaction record.
    /// </summary>
    Task InsertTransactionAsync(NABXMLTransaction transaction);

    /// <summary>
    /// Gets a transaction by its identifier.
    /// </summary>
    Task<NABXMLTransaction> GetByIdAsync(int id);

    /// <summary>
    /// Searches transactions, paged and ordered by newest first.
    /// </summary>
    /// <param name="approved">
    /// When set, restricts to approved (true) or non-approved (false) transactions.
    /// Pass <c>null</c> to return all.
    /// </param>
    Task<IPagedList<NABXMLTransaction>> SearchTransactionsAsync(
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        string email = null,
        string userName = null,
        string authId = null,
        string txnId = null,
        bool? approved = null,
        int pageIndex = 0,
        int pageSize = int.MaxValue);
}
