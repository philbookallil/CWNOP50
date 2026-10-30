namespace Nop.Plugin.CW.Core.BusinessLogic;

public interface IReturnResolutionLogic
{
    /// <summary>
    /// Records a return case's outcome once every line has been decided: prices the fees and freezes them
    /// as charges.
    /// </summary>
    /// <remarks>
    /// Safe to call after any line decision. It is a no-op until every line is decided, and a no-op
    /// forever after, so the caller does not have to know which save was the last one.
    /// <para>
    /// It executes nothing. Each line's credit note, exchange order or cart entry happened when the
    /// administrator authorised that line — see <c>IExchangesLogic.ExecuteApprovedLineAsync</c>. What this
    /// adds is the freeze: fees are otherwise recomputed from live FeeRule rows on every read, so a later
    /// change to a store's fee settings would retroactively alter a return the customer has already paid.
    /// </para>
    /// </remarks>
    Task TryResolveCaseAsync(int returnCaseId, int? resolvedByCustomerId);

    /// <summary>
    /// Settles a case's resolution once the customer has paid for every accepted line of it.
    /// </summary>
    /// <remarks>
    /// The other half of TryResolveCaseAsync. A case usually resolves while its replacements are still in
    /// the customer's cart, so it is stamped Pending with the charges outstanding; this is what moves it to
    /// Settled when they pay. Without it ReturnResolutionStatus.Settled is unreachable for any return that
    /// carries a fee, and BalanceDue is a permanent record of money already collected.
    /// <para>
    /// Safe to call for any return request. It is a no-op when there is no case, no resolution, a
    /// resolution that already settled, or another accepted line still awaiting the customer.
    /// </para>
    /// </remarks>
    Task TrySettleResolutionAsync(int returnRequestId);

    /// <summary>
    /// Voids a case's frozen outcome so it can be decided again, under the same lock that writes one.
    /// </summary>
    /// <remarks>
    /// A resolution is written by TryResolveCaseAsync under the case lock; voiding it has to take the
    /// same lock or the two race. The damaging order is a void landing between InsertResolutionAsync and
    /// WriteChargesAsync, which leaves charges pointing at a resolution that has been removed.
    /// <para>
    /// A no-op when the case has not resolved. Returns the number of frozen charges that were voided, so
    /// the caller can log what the customer no longer owes; -1 means the lock was not acquired and
    /// nothing was voided.
    /// </para>
    /// </remarks>
    Task<int> TryReopenCaseAsync(int returnCaseId);
}