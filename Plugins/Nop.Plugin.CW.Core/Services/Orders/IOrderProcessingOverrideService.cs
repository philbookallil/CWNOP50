using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Services.Settlement;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.CW.Core.Services.Orders;

public interface IOrderProcessingOverrideService : IOrderProcessingService
{
    Task<PlaceOrderResult> PlaceReturnOrderAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem);

    /// <summary>
    /// Quotes what would settle the exchange order for this return request item, without placing
    /// anything. Goods only — fees are added after settlement and are the caller's to test.
    /// </summary>
    /// <remarks>
    /// Walks the same pipeline, against the same packname cart and the same detached template, that
    /// <see cref="PlaceReturnOrderAsync"/> would settle. A plan with <c>BalanceDue == 0</c> is therefore a
    /// promise that placing the order will cost the customer nothing.
    /// </remarks>
    Task<SettlementPlan> QuoteReturnOrderSettlementAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem);

    /// <summary>
    /// Places the customer's packname order: every orderable packname line of the store, with a destination per
    /// packname and the subsidy and credit notes settled. The cart is read from the packname cart view.
    /// </summary>
    /// <param name="processPaymentRequest">Process payment request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the place order result
    /// </returns>
    Task<PlaceOrderResult> PlacePackNameOrderAsync(ProcessPaymentRequest processPaymentRequest);

    /// <summary>
    /// Gives the customer the billing address placement would resolve (when there is none) and, as shipping
    /// address, the first packname's destination — what a payment method taking the payment in its own pages
    /// reads from the customer.
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="packNameCart">The packname cart being paid</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task PrepareCustomerPaymentAddressesAsync(Customer customer, int storeId, IList<PackNameShoppingCartItem> packNameCart);
}