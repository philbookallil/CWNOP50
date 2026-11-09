using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Services.Events;

namespace Nop.Plugin.CW.Core.Services.Exchanges;

/// <summary>
/// Removes the per-store rules and the reason-action links of a return reason or requested action deleted in nop's
/// settings
/// </summary>
/// <remarks>
/// Only a record no return uses gets this far (<see cref="Filters.ReturnReasonActionDeleteGuardFilter"/>). Without
/// this its rule rows would stay behind, pointing at nothing.
/// </remarks>
public class ReturnReasonActionDeletedEventConsumer : IConsumer<EntityDeletedEvent<ReturnRequestReason>>,
    IConsumer<EntityDeletedEvent<ReturnRequestAction>>
{
    #region Fields

    protected readonly IExchangeService _exchangeService;

    #endregion

    #region Ctor

    public ReturnReasonActionDeletedEventConsumer(IExchangeService exchangeService)
    {
        _exchangeService = exchangeService;
    }

    #endregion

    #region Methods

    public async Task HandleEventAsync(EntityDeletedEvent<ReturnRequestReason> eventMessage)
    {
        await _exchangeService.DeleteReturnReasonConfigurationAsync(eventMessage.Entity.Id);
    }

    public async Task HandleEventAsync(EntityDeletedEvent<ReturnRequestAction> eventMessage)
    {
        await _exchangeService.DeleteReturnActionConfigurationAsync(eventMessage.Entity.Id);
    }

    #endregion
}