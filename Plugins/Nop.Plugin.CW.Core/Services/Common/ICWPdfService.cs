using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Services.Common
{
    public interface ICWPdfService
    {
        Task PrintReturnExchangeRequestToPdfAsync(Stream stream, ReturnRequest returnRequest, int languageId = 0);

        Task PrintReturnExchangeRequestsToPdfAsync(Stream stream, IList<ReturnRequest> returnRequests, int languageId = 0);
    }
}