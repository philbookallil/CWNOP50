using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tax;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Events;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Exchanges;

/// <summary>
/// Prices an Exact Exchange replacement at what the customer actually paid for the item being returned.
/// </summary>
/// <remarks>
/// <para>
/// An exchange replacement is an ordinary cart row, so without this it is priced live and drifts away from
/// the returned item: a tier price that applied at the ordered quantity but not at the replacement
/// quantity (validation U7 — $21.00 paid, $24.00 quoted back), a catalogue price change, a discount that
/// has since started or ended. The customer is swapping an item they already own, so the money must not
/// move.
/// </para>
/// <para>
/// <b>Exact only.</b> Same Style and Different Style re-price on purpose — the price difference the wizard
/// shows and the customer agrees to is the whole point of those two — so they are left alone.
/// </para>
/// <para>
/// nop publishes this event before it computes anything (ShoppingCartService.GetUnitPriceAsync) and
/// returns immediately when StopProcessing is set, which is why this needs no override of the cart
/// service and reaches every caller: the subtotal, the settlement quote and order placement alike.
/// </para>
/// <para>
/// The event carries the price in the primary store currency on the catalogue basis, which is what
/// ITaxService.GetProductPriceAsync converts afterwards — so the figure to hand back is the incl-tax one
/// when the store enters prices incl tax, and the excl-tax one otherwise. Handing back the wrong one
/// would apply tax twice, or not at all, on a taxed store.
/// </para>
/// Discounts are deliberately returned empty: whatever discount applied at the time is already inside the
/// price paid, and re-reporting it would let the caller subtract it a second time.
/// </remarks>
public class ExchangeReplacementPriceConsumer : IConsumer<GetShoppingCartItemUnitPriceEvent>
{
    #region Fields

    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IExchangeService _exchangeService;
    protected readonly IOrderService _orderService;
    protected readonly TaxSettings _taxSettings;

    #endregion

    #region Ctor

    public ExchangeReplacementPriceConsumer(ICWFeatureService cwFeatureService,
        IExchangeService exchangeService,
        IOrderService orderService,
        TaxSettings taxSettings)
    {
        _cwFeatureService = cwFeatureService;
        _exchangeService = exchangeService;
        _orderService = orderService;
        _taxSettings = taxSettings;
    }

    #endregion

    #region Methods

    public async Task HandleEventAsync(GetShoppingCartItemUnitPriceEvent eventMessage)
    {
        var cartItem = eventMessage?.ShoppingCartItem;

        //Cheapest exit first: this fires for every line of every cart on every totals calculation.
        if (cartItem == null || cartItem.ShoppingCartType != ShoppingCartType.ShoppingCart)
            return;

        var scope = ExchangeReplacementScope.Current;

        //Nothing to do unless this row either IS a replacement being written right now, or is a saved
        //replacement row sitting in the customer's cart. Cheap bail-out for ordinary shopping.
        if (scope == null && cartItem.Id == 0)
            return;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, cartItem.StoreId))
            return;

        int returnedOrderItemId;

        if (cartItem.Id == 0)
        {
            //An auto-placed exchange (PlaceReturnOrderAsync) prices DETACHED templates built by
            //BuildProductsListForReturnOrderAsync — Id 0, never persisted, so there is no link to follow.
            //The scope is the only thing that knows what they are.
            if (scope.ExchangeType != ExchangeType.Exact)
                return;

            returnedOrderItemId = scope.ReturnedOrderItemId;
        }
        else
        {
            //A saved row. Read the link rather than the scope: the customer checks the replacement out
            //later, in their own request, long after the scope has closed. The links of the whole cart are
            //read once per request, since this runs every time a line is priced.
            var cartReturnRequestItems = await _exchangeService.GetCartReturnRequestItemsAsync(cartItem.CustomerId);

            if (!cartReturnRequestItems.TryGetValue(cartItem.Id, out var returnRequestItem) || returnRequestItem.ExchangeType != ExchangeType.Exact)
                return;

            returnedOrderItemId = returnRequestItem.ReturnedOrderItemId;
        }

        var returnedOrderItem = await _orderService.GetOrderItemByIdAsync(returnedOrderItemId);

        if (returnedOrderItem == null)
            return;

        //The event carries the price on the CATALOGUE basis — the same basis ITaxService.GetProductPriceAsync
        //converts afterwards — so hand back the incl-tax figure when the store enters prices incl tax and
        //the excl-tax one otherwise. On a 10% GST store the wrong one is out by 10% in one direction or the
        //other. ReOrderAsync (OrderProcessingOverrideService:1288) picks its replay price the same way.
        eventMessage.UnitPrice = _taxSettings.PricesIncludeTax
            ? returnedOrderItem.UnitPriceInclTax
            : returnedOrderItem.UnitPriceExclTax;

        eventMessage.DiscountAmount = decimal.Zero;
        eventMessage.AppliedDiscounts = new();
        eventMessage.StopProcessing = true;
    }

    #endregion
}