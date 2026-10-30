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
}