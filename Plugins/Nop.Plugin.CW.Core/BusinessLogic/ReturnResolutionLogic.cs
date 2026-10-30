using Nop.Core.Caching;
using Nop.Plugin.CW.Core.Domain.Exchanges.DTOs;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Services.Logging;

namespace Nop.Plugin.CW.Core.BusinessLogic;

/// <summary>
/// Records the outcome of a resolved return case, once.
/// </summary>
/// <remarks>
/// <para>
/// A case resolves when no line is left Pending. This does not execute anything — each line was executed
/// when the administrator authorised it. What happens here is the freeze: the fees are otherwise recomputed from live FeeRule rows on every read, so
/// a later change to a store's fee settings would retroactively alter a return the customer was already
/// quoted and has very likely already paid.
/// </para>
/// The figures frozen here are the figures already charged: every fee is priced over its own line from
/// data committed before that line executed, and a line's inputs are locked once it is authorised.
/// <para>
/// Both public entry points are serialised on the case. Everything they do is
/// check-then-act, and the checks are worthless unresolved against a concurrent writer.
/// </para>
/// </remarks>
public class ReturnResolutionLogic : IReturnResolutionLogic
{
    #region Fields

    protected readonly IReturnCaseService _returnCaseService;
    protected readonly IExchangeService _exchangeService;
    protected readonly ILocker _locker;
    protected readonly ILogger _logger;

    #endregion

    #region Ctor

    public ReturnResolutionLogic(IReturnCaseService returnCaseService,
        IExchangeService exchangeService,
        ILocker locker,
        ILogger logger)
    {
        _returnCaseService = returnCaseService;
        _exchangeService = exchangeService;
        _locker = locker;
        _logger = logger;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// One key for both halves — resolving a case and settling it must never overlap.
    /// </summary>
    protected static string ResolveLockKey(int returnCaseId) => $"cw_returncase_resolve_{returnCaseId}";

    /// <summary>
    /// Records the priced fees as ReturnCharge rows, one row per record.
    /// </summary>
    /// <remarks>
    /// Nothing is summed before it is recorded, so each row keeps the line it is collected with, the
    /// quantity it was priced over, the rule it came from and — for an ad-hoc staff fee — the name the
    /// customer sees.
    /// </remarks>
    protected async Task<decimal> WriteChargesAsync(ReturnResolution resolution, IList<ExchangeFeeAmount> fees)
    {
        var total = decimal.Zero;

        foreach (var fee in fees)
        {
            await _returnCaseService.InsertChargeAsync(new ReturnCharge
            {
                ReturnResolutionId = resolution.Id,
                ReturnCaseLineId = fee.ReturnCaseLineId,
                FeeType = fee.FeeType,
                Amount = fee.Amount,
                Quantity = fee.Quantity,
                SourceFeeRuleId = fee.SourceFeeRuleId,
                Description = fee.Description
            });

            total += fee.Amount;
        }

        return total;
    }

    /// <summary>
    /// Whether any accepted line is still waiting on the customer to pay for its replacement.
    /// </summary>
    protected async Task<bool> AnyLineAwaitingCustomerAsync(IList<ReturnCaseLine> lines)
    {
        foreach (var line in lines.Where(l => l.Disposition == ReturnLineDisposition.Accepted))
        {
            if (line.ReturnRequestId is not > 0)
                continue;

            var item = await _exchangeService.GetReturnRequestItemAsync(line.ReturnRequestId.Value);

            if (item is { NeedsCompletion: true, IsCompleted: false })
                return true;
        }

        return false;
    }

    /// <summary>
    /// The body of TryResolveCaseAsync, run under the case lock. Never call it directly.
    /// </summary>
    protected virtual async Task ResolveCaseCoreAsync(int returnCaseId, int? resolvedByCustomerId)
    {
        //already resolved — this is what ResolvedOnUtc exists for, and it is the whole of 3a's idempotency
        if (await _returnCaseService.GetResolutionByCaseIdAsync(returnCaseId) != null)
            return;

        var returnCase = await _returnCaseService.GetReturnCaseByIdAsync(returnCaseId);

        if (returnCase == null)
            return;

        var lines = await _returnCaseService.GetReturnCaseLinesAsync(returnCaseId);

        if (lines.Count == 0)
            return;

        //a case is recorded once, when nothing is left undecided
        if (lines.Any(line => line.Disposition == ReturnLineDisposition.Pending))
            return;

        //Priced per line, exactly as each line was priced when it executed. Rejected lines are excluded
        //inside, so they are free.
        var fees = await _exchangeService.CalculateExchangeFeeAmountsForCaseAsync(returnCaseId);

        var awaitingCustomer = await AnyLineAwaitingCustomerAsync(lines);

        var resolution = new ReturnResolution
        {
            ReturnCaseId = returnCaseId,
            ResolvedOnUtc = DateTime.UtcNow,
            ResolvedByCustomerId = resolvedByCustomerId,
            Status = ReturnResolutionStatus.Settled
        };

        try
        {
            await _returnCaseService.InsertResolutionAsync(resolution);
        }
        catch (Exception ex)
        {
            //The unique index on ReturnCaseId is what actually guarantees one resolution per case — the
            //case lock above is not enough, because MemoryCacheLocker.GetOrCreate is not atomic and lets
            //simultaneous callers all believe they acquired it (proven live: one case resolved 3x).
            //
            //Re-reading rather than matching a provider-specific duplicate-key number: if a resolution
            //exists now, a concurrent save won and this one stands down having written nothing. Charges
            //are written below, so returning here leaves none orphaned.
            if (await _returnCaseService.GetResolutionByCaseIdAsync(returnCaseId) != null)
            {
                await _logger.InformationAsync($"[Returns] Case {returnCaseId} was resolved by a concurrent save; this one recorded nothing.");
                return;
            }

            await _logger.ErrorAsync($"[Returns] Case {returnCaseId} could not be resolved.", ex);
            throw;
        }

        var chargesTotal = await WriteChargesAsync(resolution, fees);

        //Fees are cash-only, so the balance at resolution is the charges. Goods
        //value is settled by the pipeline at checkout and is not owed here.
        resolution.BalanceDue = awaitingCustomer ? chargesTotal : decimal.Zero;
        resolution.Status = awaitingCustomer ? ReturnResolutionStatus.Pending : ReturnResolutionStatus.Settled;

        await _returnCaseService.UpdateResolutionAsync(resolution);

        //LAST, deliberately. If anything above throws, the case is still Submitted while a resolution row
        //exists — which is a state you can find with a query. Marking the case first would hide it.
        returnCase.Status = ReturnCaseStatus.Resolved;
        returnCase.UpdatedOnUtc = DateTime.UtcNow;

        await _returnCaseService.UpdateReturnCaseAsync(returnCase);

        await _logger.InformationAsync($"[Returns] Case {returnCaseId} recorded: {lines.Count} line(s) decided, {fees.Count} charge(s) totalling {chargesTotal:0.00} frozen, status {resolution.Status}.");
    }

    /// <summary>
    /// The body of TrySettleResolutionAsync, run under the case lock. Never call it directly.
    /// </summary>
    protected virtual async Task SettleResolutionCoreAsync(int returnCaseId)
    {
        var resolution = await _returnCaseService.GetResolutionByCaseIdAsync(returnCaseId);

        //Nothing to settle: the case has not resolved yet, or it already settled. A case that resolves
        //AFTER the customer has paid is stamped Settled on the spot by TryResolveCaseAsync, so this is
        //only for the other order of events — resolved first, paid second, which is the normal one.
        if (resolution is not { Status: ReturnResolutionStatus.Pending })
            return;

        var lines = await _returnCaseService.GetReturnCaseLinesAsync(returnCaseId);

        //One line paid does not settle a case with two. Every accepted line has to be done.
        if (await AnyLineAwaitingCustomerAsync(lines))
            return;

        resolution.BalanceDue = decimal.Zero;
        resolution.Status = ReturnResolutionStatus.Settled;

        await _returnCaseService.UpdateResolutionAsync(resolution);

        await _logger.InformationAsync($"[Returns] Case {returnCaseId} settled: every accepted line has been paid for.");
    }

    /// <summary>
    /// The body of TryReopenCaseAsync, run under the case lock. Never call it directly.
    /// </summary>
    protected virtual async Task<int> ReopenCaseCoreAsync(int returnCaseId)
    {
        var resolution = await _returnCaseService.GetResolutionByCaseIdAsync(returnCaseId);

        if (resolution == null)
            return 0;

        var voidedCharges = (await _returnCaseService.GetChargesByResolutionIdAsync(resolution.Id)).Count;

        await _returnCaseService.ReopenCaseAsync(returnCaseId);

        return voidedCharges;
    }

    #endregion

    #region Methods

    public virtual async Task TryResolveCaseAsync(int returnCaseId, int? resolvedByCustomerId)
    {
        if (returnCaseId == 0)
            return;

        // Serialised on the case. Without this the guard inside is check-then-act: two administrators
        // saving SIBLING LINES of the same case at the same moment both read "not resolved" and both
        // insert, giving the case two resolutions and two full sets of frozen charges — the customer
        // billed twice for one return.
        //
        // Lock ordering: nothing else is acquired while this is held. The credit-note, subsidy-credit and
        // packname locks are all taken further IN, inside the approval workflow, never further out, so
        // there is no cycle with OrderProcessingOverrideService.RunWithPackNameLocksAsync.
        var resolved = await _locker.PerformActionWithLockAsync(
            ResolveLockKey(returnCaseId),
            TimeSpan.FromSeconds(30),
            () => ResolveCaseCoreAsync(returnCaseId, resolvedByCustomerId));

        if (!resolved)
            //nop's locker refuses rather than waits (MemoryCacheLocker returns false on a held key).
            //Whoever holds it is resolving this same case and reads the lines fresh inside, so the
            //outcome is theirs to record. Logged because their save could still fail: this method runs on
            //every line save and is a no-op until the last one, so any later save retries it.
            await _logger.WarningAsync($"[Returns] Case {returnCaseId} was already being resolved by another save; this one recorded nothing. The next save of any line of it will retry.");
    }

    public virtual async Task TrySettleResolutionAsync(int returnRequestId)
    {
        if (returnRequestId <= 0)
            return;

        var caseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequestId);

        if (caseLine == null)
            return;

        // Same key as TryResolveCaseAsync, deliberately. A customer checking out one line while an
        // administrator decides another would otherwise have this read AnyLineAwaitingCustomerAsync
        // against lines the resolver is still writing, and settle a case that is not finished.
        var settled = await _locker.PerformActionWithLockAsync(
            ResolveLockKey(caseLine.ReturnCaseId),
            TimeSpan.FromSeconds(30),
            () => SettleResolutionCoreAsync(caseLine.ReturnCaseId));

        if (!settled)
            await _logger.WarningAsync($"[Returns] Case {caseLine.ReturnCaseId} was busy resolving; settlement was not recorded on this pass. It is retried the next time a line of it is paid for or saved.");
    }

    public virtual async Task<int> TryReopenCaseAsync(int returnCaseId)
    {
        if (returnCaseId <= 0)
            return 0;

        var voidedCharges = 0;

        // Retried, unlike the other two locks, because giving up here is not safe. TryResolveCaseAsync
        // failing costs nothing — the next save of any line retries it — and ExecuteApprovedLineAsync
        // failing tells the administrator to click again. Failing HERE lets the caller carry on and price
        // the save against a frozen resolution that should have been voided, which is the exact defect
        // ReopenResolvedCaseIfDecisionChangesAsync exists to prevent; and on the undo path it leaves the
        // nop row Pending with the case line still Accepted.
        //
        // Whoever holds the lock is resolving or settling — a handful of queries — so three attempts over
        // ~300ms wins in practice. Still bounded: a genuinely stuck holder must not hang the request.
        const int attempts = 3;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            // Same key as TryResolveCaseAsync and TrySettleResolutionAsync. Writing a resolution and
            // voiding one are the same critical section seen from two directions.
            var reopened = await _locker.PerformActionWithLockAsync(
                ResolveLockKey(returnCaseId),
                TimeSpan.FromSeconds(30),
                async () =>
                {
                    voidedCharges = await ReopenCaseCoreAsync(returnCaseId);
                });

            if (reopened)
                return voidedCharges;

            if (attempt < attempts)
                await Task.Delay(150);
        }

        await _logger.ErrorAsync($"[Returns] Case {returnCaseId} stayed locked across {attempts} attempts, so its frozen outcome was NOT voided. Whatever this save prices may come from the previous resolution, and a line of it may be left inconsistent with its return request — check this case by hand.");

        return -1;
    }

    #endregion
}