using Nop.Core;
using Nop.Data;
using Nop.Plugin.Payments.NABGateway.Domain;

namespace Nop.Plugin.Payments.NABGateway.Services;

/// <summary>
/// Default <see cref="INABXMLTransactionService"/> implementation backed by the plugin's repository.
/// </summary>
public class NABXMLTransactionService : INABXMLTransactionService
{
    #region Fields

    protected readonly IRepository<NABXMLTransaction> _transactionRepository;

    #endregion

    #region Ctor

    public NABXMLTransactionService(IRepository<NABXMLTransaction> transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    #endregion

    #region Methods

    public virtual async Task InsertTransactionAsync(NABXMLTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        await _transactionRepository.InsertAsync(transaction);
    }

    public virtual async Task<NABXMLTransaction> GetByIdAsync(int id)
    {
        return await _transactionRepository.GetByIdAsync(id);
    }

    public virtual async Task<IPagedList<NABXMLTransaction>> SearchTransactionsAsync(
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        string email = null,
        string userName = null,
        string authId = null,
        string txnId = null,
        bool? approved = null,
        int pageIndex = 0,
        int pageSize = int.MaxValue)
    {
        var query = _transactionRepository.Table;

        if (approved.HasValue)
            query = query.Where(t => t.Approved == approved.Value);

        if (startDateUtc.HasValue)
            query = query.Where(t => t.CreatedOnUtc >= startDateUtc.Value);

        if (endDateUtc.HasValue)
            query = query.Where(t => t.CreatedOnUtc <= endDateUtc.Value);

        if (!string.IsNullOrEmpty(email))
            query = query.Where(t => t.UserEmail.Contains(email));

        if (!string.IsNullOrEmpty(userName))
            query = query.Where(t => t.UserFullName.Contains(userName));

        if (!string.IsNullOrEmpty(authId))
            query = query.Where(t => t.AuthId.Contains(authId));

        if (!string.IsNullOrEmpty(txnId))
            query = query.Where(t => t.TxnId.Contains(txnId));

        query = query.OrderByDescending(t => t.CreatedOnUtc).ThenByDescending(t => t.Id);

        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    #endregion
}
