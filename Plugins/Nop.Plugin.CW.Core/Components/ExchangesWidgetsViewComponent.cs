using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Plugin.CW.Core.Constants;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components
{
    [ViewComponent(Name = "ExchangesWidgets")]
    public class ExchangesWidgetsViewComponent : NopViewComponent
    {
        #region Fields

        protected readonly IWorkContext _workContext;
        protected readonly IOrderService _orderService;
        protected readonly IStoreContext _storeContext;
        protected readonly IExchangesModelFactory _exchangesModelFactory;
        protected readonly ICWFeatureService _cwFeatureService;
        protected readonly IExchangesDisplayService _exchangesDisplayService;
        protected readonly ICurrencyService _currencyService;
        protected readonly IPriceFormatter _priceFormatter;
        protected readonly CurrencySettings _currencySettings;

        #endregion

        #region Ctor

        public ExchangesWidgetsViewComponent(IWorkContext workContext,
            IStoreContext storeContext,
            IExchangesModelFactory exchangesModelFactory,
            IOrderService orderService,
            ICWFeatureService cwFeatureService,
            IExchangesDisplayService exchangesDisplayService,
            ICurrencyService currencyService,
            IPriceFormatter priceFormatter,
            CurrencySettings currencySettings)
        {
            _workContext = workContext;
            _orderService = orderService;
            _storeContext = storeContext;
            _exchangesModelFactory = exchangesModelFactory;
            _cwFeatureService = cwFeatureService;
            _exchangesDisplayService = exchangesDisplayService;
            _currencyService = currencyService;
            _priceFormatter = priceFormatter;
            _currencySettings = currencySettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Formats every fee of the model for the page that shows it
        /// </summary>
        /// <param name="model">The fees</param>
        /// <param name="format">Formats an amount kept in the primary store currency</param>
        protected virtual async Task FormatAmountsAsync(ExchangesFeesModel model, Func<decimal, Task<string>> format)
        {
            foreach (var fee in model.Fees)
                fee.FeeAmountDisplay = await format(fee.FeeAmount);

            foreach (var fee in model.AdditionalFees)
                fee.FeeAmountDisplay = await format(fee.FeeAmount);
        }

        private async Task<IViewComponentResult> ExchangesFeesForCartComponentAsync()
        {
            //exchange fees on a live cart are a consumption path — block when the feature is off
            if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges))
                return Content(string.Empty);

            var model = await _exchangesModelFactory.PrepareExchangesFeesModelFromCartAsync(await _workContext.GetCurrentCustomerAsync(),
                await _storeContext.GetCurrentStoreAsync());

            //the cart is in the customer's currency
            await FormatAmountsAsync(model, _exchangesDisplayService.FormatPriceAsync);

            return await ViewAsync("ExchangesFeesForCartOrOrder", model);
        }

        private async Task<IViewComponentResult> ExchangesFeesForOrderAsync(object additionalData)
        {
            var model = new ExchangesFeesModel();
            if (additionalData is int orderId && await _orderService.GetOrderByIdAsync(orderId) is { } order)
            {
                model = await _exchangesModelFactory.PrepareExchangesFeesModelFromOrderAsync(order);

                //the customer's order page shows the order's currency, at the order's rate
                await FormatAmountsAsync(model, amount => _exchangesDisplayService.FormatOrderPriceAsync(amount, order));
            }

            return await ViewAsync("ExchangesFeesForCartOrOrder", model);
        }

        private async Task<IViewComponentResult> ExchangesFeesForOrderAdminAsync(object additionalData)
        {
            var model = new ExchangesFeesModel();
            if (additionalData is int orderId && await _orderService.GetOrderByIdAsync(orderId) is { } order)
            {
                model = await _exchangesModelFactory.PrepareExchangesFeesModelFromOrderAsync(order);

                //the admin order page shows the primary store currency, as nop's totals there
                var primaryStoreCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
                var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;
                await FormatAmountsAsync(model, amount => _priceFormatter.FormatPriceAsync(amount, true, primaryStoreCurrency, languageId, false));
            }

            return await ViewAsync("ExchangesFeesForOrderAdmin", model);
        }

        #endregion

        #region Methods

        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
        {
            return widgetZone switch
            {
                //Public
                CWWidgetsZones.WIDGET_EXCHANGES_FEES_FOR_CART => await ExchangesFeesForCartComponentAsync(),
                //Public
                CWWidgetsZones.WIDGET_EXCHANGES_FEES_FOR_ORDER => await ExchangesFeesForOrderAsync(additionalData),
                //Admin
                CWWidgetsZones.WIDGET_EXCHANGES_FEES_FOR_ORDER_ADMIN => await ExchangesFeesForOrderAdminAsync(additionalData),
                _ => Content(string.Empty),
            };
        }

        #endregion
    }
}