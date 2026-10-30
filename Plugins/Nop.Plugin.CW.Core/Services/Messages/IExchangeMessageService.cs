using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Services.Messages
{
    public interface IExchangeMessageService
    {
        Task<string> GetExchangesReturnReceiptMessageAsync(ReturnRequest returnRequest);
    }
}