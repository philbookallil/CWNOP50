using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Settings;

namespace Nop.Plugin.CW.Core.Services.Settlement;

/// <summary>
/// Credit Notes as a settlement source. Priority 2 — the customer's own store credit is spent only on
/// what subsidy left unsettled.
/// </summary>
public class CreditNoteSettlementSource : ISettlementSource
{
    #region Fields

    protected readonly ICreditNotesService _creditNotesService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public CreditNoteSettlementSource(ICreditNotesService creditNotesService,
        ICWFeatureService cwFeatureService)
    {
        _creditNotesService = creditNotesService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Methods

    public string Name => SettlementSourceNames.CREDIT_NOTES;

    public int Priority => 2;

    /// <summary>
    /// Enabled when the feature is on for the store, OR when the customer already holds a balance.
    /// </summary>
    /// <remarks>
    /// The "or has data" arm is not defensive coding, it is the rule this project works to: a feature
    /// toggle never gates finishing or displaying work that already exists. A credit note is a liability
    /// the business already owes, and hiding it does not discharge it. Turning the feature off must stop
    /// new credit notes being issued; it must not force a customer to pay a balance they are not actually
    /// owed, nor strand an approved in-flight exchange.
    ///
    /// Order-scoped work resolves the feature from the order's store, never the current store.
    /// </remarks>
    public virtual async Task<bool> IsEnabledAsync(SettlementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var enabled = context.Order != null
            ? await _cwFeatureService.IsEnabledForOrderAsync(CWFeature.CreditNotes, context.Order)
            : await _cwFeatureService.IsEnabledAsync(CWFeature.CreditNotes, context.StoreId);

        if (enabled)
            return true;

        return await _creditNotesService.CustomerHasCreditNotesBalanceAsync(context.Customer.Id);
    }

    public virtual async Task<SettlementLine> QuoteAsync(SettlementContext context, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (_, appliedTotal) = await _creditNotesService.CalculateAppliedCreditNotesAsync(context.Customer.Id, amount);

        return new SettlementLine
        {
            SourceName = Name,
            Priority = Priority,
            Amount = appliedTotal,
        };
    }

    public virtual async Task<SettlementLine> ApplyAsync(SettlementContext context, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(context);

        var order = context.Order
            ?? throw new NopException($"The {Name} settlement source needs an order to apply against.");

        var (appliedTotal, _) = await _creditNotesService.ApplyCreditNotesForOrderAsync(order, amount);

        return new SettlementLine
        {
            SourceName = Name,
            Priority = Priority,
            Amount = appliedTotal,
        };
    }

    #endregion
}