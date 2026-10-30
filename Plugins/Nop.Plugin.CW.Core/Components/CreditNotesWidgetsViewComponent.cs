using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Constants;
using Nop.Plugin.CW.Core.Models.CreditNotes;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Settlement;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;


namespace Nop.Plugin.CW.Core.Components;

[ViewComponent(Name = "CreditNotesWidgets")]
public class CreditNotesWidgetsViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IOrderTotalCalculationOverrideService _orderTotalCalculationService;
    protected readonly IWorkContext _workContext;
    protected readonly IStoreContext _storeContext;
    protected readonly ICurrencyService _currencyService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IShoppingCartService _shoppingCartService;
    protected readonly ICreditNotesService _creditNotesService;
    protected readonly IOrderService _orderService;
    protected readonly CurrencySettings _currencySettings;
    protected readonly OrderSettings _orderSettings;

    #endregion

    #region Ctor

    public CreditNotesWidgetsViewComponent(
        IOrderTotalCalculationOverrideService orderTotalCalculationService,
        IWorkContext workContext,
        IStoreContext storeContext,
        ICurrencyService currencyService,
        IPriceFormatter priceFormatter,
        ICreditNotesService creditNotesService,
        IShoppingCartService shoppingCartService,
        IOrderService orderService,
        CurrencySettings currencySettings,
        OrderSettings orderSettings)
    {
        _orderTotalCalculationService = orderTotalCalculationService;
        _workContext = workContext;
        _storeContext = storeContext;
        _currencyService = currencyService;
        _priceFormatter = priceFormatter;
        _creditNotesService = creditNotesService;
        _shoppingCartService = shoppingCartService;
        _orderService = orderService;
        _currencySettings = currencySettings;
        _orderSettings = orderSettings;
    }

    #endregion

    #region Utilities

    protected async Task PopulateCreditNotesSummaryFromCartAsync(CreditNotesOrderSummaryModel model)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        model.NeedToShow = await _creditNotesService.CustomerHasCreditNotesBalanceAsync(customer.Id);
        var store = await _storeContext.GetCurrentStoreAsync();
        if (model.NeedToShow)
        {
            if (customer.HasShoppingCartItems)
            {
                var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

                // One quote of the settlement pipeline: this is the same plan the cart total used, so
                // the widget can never disagree with the total shown beside it.
                var plan = await _orderTotalCalculationService.QuoteCartSettlementAsync(cart);

                decimal? creditNotesTotalBase = plan.SettleableAmount > decimal.Zero
                    ? plan.AmountFor(SettlementSourceNames.CREDIT_NOTES)
                    : null;

                //no amount yet: the view says it is calculated during checkout
                if (creditNotesTotalBase.HasValue)
                {
                    decimal creditNotesTotal = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(creditNotesTotalBase.Value, await _workContext.GetWorkingCurrencyAsync());
                    model.Amount = await _priceFormatter.FormatPriceAsync(-creditNotesTotal, true, false);
                }
            }
        }
    }

    /// <summary>
    /// The store credit an order used, in the currency and at the rate the order was placed with, as the totals beside it
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="model">The summary to fill</param>
    /// <param name="adminPage">
    /// The admin order page: the amount in the primary store currency (and the customer's, when the store shows it), with
    /// no minus, as nop's admin totals are written; otherwise the customer's page, in the order's currency with a minus
    /// </param>
    protected async Task PopulateCreditNotesSummaryFromOrderAsync(int orderId, CreditNotesOrderSummaryModel model, bool adminPage = false)
    {
        var histories = await _creditNotesService.GetCreditNoteHistoriesByOrderIdAsync(orderId);
        var totalCreditNotes = histories.Sum(r => r.Amount);
        var order = await _orderService.GetOrderByIdAsync(orderId);
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        model.NeedToShow = totalCreditNotes > 0;

        if (order == null)
        {
            model.Amount = await _priceFormatter.FormatPriceAsync(totalCreditNotes, true, false);
            return;
        }

        if (adminPage)
        {
            var primaryStoreCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
            model.Amount = await _priceFormatter.FormatOrderPriceAsync(totalCreditNotes, order.CurrencyRate, order.CustomerCurrencyCode,
                _orderSettings.DisplayCustomerCurrencyOnOrders, primaryStoreCurrency, languageId, null, false);

            return;
        }

        var amountInCustomerCurrency = _currencyService.ConvertCurrency(totalCreditNotes, order.CurrencyRate);
        model.Amount = await _priceFormatter.FormatPriceAsync(totalCreditNotes > 0 ? -amountInCustomerCurrency : amountInCustomerCurrency,
            true, order.CustomerCurrencyCode, false, languageId);
    }

    protected async Task<IViewComponentResult> CreditNotesAmountForCartAsync(object additionalData)
    {
        var model = new CreditNotesOrderSummaryModel();
        if (additionalData is int orderId)
        {
            await PopulateCreditNotesSummaryFromOrderAsync(orderId, model);
        }
        else
        {
            await PopulateCreditNotesSummaryFromCartAsync(model);
        }

        return View("CreditNotesOrderSummary", model);
    }

    protected async Task<IViewComponentResult> CreditNotesAmountForOrderAdminAsync(object additionalData)
    {
        var model = new CreditNotesOrderSummaryModel();
        if (additionalData is int orderId)
        {
            await PopulateCreditNotesSummaryFromOrderAsync(orderId, model, adminPage: true);
        }

        return View("CreditNotesOrderAdmin", model);
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        return widgetZone switch
        {
            //Public
            CWWidgetsZones.WIDGET_CREDITNOTES_FOR_CART => await CreditNotesAmountForCartAsync(additionalData),
            //Admin
            CWWidgetsZones.WIDGET_CREDITNOTES_ORDER_ADMIN => await CreditNotesAmountForOrderAdminAsync(additionalData),
            //Public
            CWWidgetsZones.WIDGET_CREDITNOTES_ORDER_CUSTOMER => await CreditNotesAmountForCartAsync(additionalData),
            _ => Content(string.Empty),
        };
    }

    #endregion
}