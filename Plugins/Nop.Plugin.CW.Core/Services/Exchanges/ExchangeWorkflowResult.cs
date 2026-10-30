using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Exchanges;

public class ExchangeWorkflowResult : PlaceOrderResult
{
    public bool NeedsCompletion { get; set; }
}
