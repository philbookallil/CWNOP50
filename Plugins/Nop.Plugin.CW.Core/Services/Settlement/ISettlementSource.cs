namespace Nop.Plugin.CW.Core.Services.Settlement;

/// <summary>
/// A pool the customer (or their PackName) is entitled to: consumed by orders, replenished by returns.
/// Credit Notes and Subsidy are the two implementations.
///
/// The contract is deliberately narrow. It agrees only on "how much of this amount can you cover, and
/// record that you did" — each source keeps its own, much richer API for everything else
/// Do not widen it to accommodate Subsidy's product-group model.
/// 
/// There is deliberately NO RestoreAsync member here. Restoration is not symmetric between the two
/// sources: the money a customer put into a returned item comes back as a NEWLY ISSUED credit note
/// (ExchangesLogic.ExecuteCreditNoteWorkflowAsync — the returned value minus the subsidy given back in
/// kind), which the replacement then consumes, so credit notes have nothing to give back. Only subsidy
/// restores, and it does so in kind via ISubsidyCreditService.RestoreSubsidyForReturnedItemAsync. An
/// interface member exists to make an obligation explicit; there is no obligation on the credit-note
/// side, so it would be an abstraction over one implementation.
/// </summary>
public interface ISettlementSource
{
    /// <summary>Stable name, from <see cref="SettlementSourceNames"/>.</summary>
    string Name { get; }

    /// <summary>
    /// Lower runs first, and therefore consumes first. This is the ordering rule expressed as data
    /// rather than as call order — changing it is a one-line change here, not a refactor.
    /// </summary>
    int Priority { get; }

    /// <summary>Whether this source is switched on for the context's store.</summary>
    Task<bool> IsEnabledAsync(SettlementContext context);

    /// <summary>
    /// How much of <paramref name="amount"/> this source would cover. Persists nothing.
    /// </summary>
    Task<SettlementLine> QuoteAsync(SettlementContext context, decimal amount);

    /// <summary>
    /// Covers what it can of <paramref name="amount"/> and persists the consumption.
    /// Requires <see cref="SettlementContext.Order"/>.
    /// </summary>
    Task<SettlementLine> ApplyAsync(SettlementContext context, decimal amount);
}