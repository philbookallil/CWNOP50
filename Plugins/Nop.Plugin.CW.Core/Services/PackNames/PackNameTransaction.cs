using System.Transactions;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// The transaction packname registrations run in
/// </summary>
/// <remarks>
/// Read committed, not the Serializable a bare <see cref="TransactionScope"/> defaults to: Serializable keeps a range
/// lock on every row a lookup touched until the end, which on a long import blocks the store's other requests. A scope
/// opened inside another joins it, so a registration inside an import commits or rolls back with its batch. Work
/// inside a scope must run one call at a time: two connections open at once turn it into a distributed transaction,
/// which fails.
/// </remarks>
public static class PackNameTransaction
{
    /// <summary>
    /// Opens a read-committed scope that flows across awaits
    /// </summary>
    /// <param name="timeout">How long the scope may stay open before it is aborted</param>
    /// <returns>The scope; call Complete on it to commit</returns>
    public static TransactionScope Begin(TimeSpan timeout)
    {
        return new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = timeout },
            TransactionScopeAsyncFlowOption.Enabled);
    }
}