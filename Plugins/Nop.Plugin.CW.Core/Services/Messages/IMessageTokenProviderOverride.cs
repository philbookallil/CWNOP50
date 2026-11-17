using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Services.Messages;

namespace Nop.Plugin.CW.Core.Services.Messages;

public interface IMessageTokenProviderOverride
{
    /// <summary>
    /// Adds the exchange/return fee tokens for a return request.
    /// </summary>
    /// <param name="returnRequestItem">
    /// The return request item as the caller holds it, or null to load it. A caller that already has the
    /// item should pass it rather than have it re-read — the figures in the email must be the ones the
    /// caller priced against, not whatever a second read returns.
    /// </param>
    Task AddExchangeReturnTokensAsync(IList<Token> tokens, ReturnRequest returnRequest, decimal pendingAmount, ReturnRequestItem returnRequestItem = null);

    /// <summary>
    /// Adds the packname token of a returned item: the packname it was ordered for, empty when there is none.
    /// </summary>
    /// <param name="tokens">List of already added tokens</param>
    /// <param name="orderItem">The returned order item</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task AddReturnPackNameTokensAsync(IList<Token> tokens, OrderItem orderItem);

    /// <summary>
    /// Adds the tokens of a rejected or cancelled return: the decision note staff recorded
    /// </summary>
    /// <param name="tokens">List of already added tokens</param>
    /// <param name="decisionNote">The decision note; empty when there is none</param>
    void AddReturnRejectionTokens(IList<Token> tokens, string decisionNote);

    /// <summary>
    /// Adds the tokens of a return authorised without a replacement: the store credit raised and whether the subsidy
    /// was given back
    /// </summary>
    /// <param name="tokens">List of already added tokens</param>
    /// <param name="creditNoteAmount">The amount of the credit note raised; zero when none was</param>
    /// <param name="returnedToSubsidy">Whether what the subsidy paid for the item was given back</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task AddReturnCreditTokensAsync(IList<Token> tokens, decimal creditNoteAmount, bool returnedToSubsidy);
}