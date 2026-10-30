using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Tax;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.ReturnRequests;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Factories
{
    public class ReturnRequestOverrideModelFactory : ReturnRequestModelFactory, IReturnRequestOverrideModelFactory
    {
        #region Fields

        protected readonly IExchangeService _exchangeService;
        protected readonly IReturnRequestItemModelFactory _returnRequestItemModelFactory;
        protected readonly IExchangesDisplayService _exchangesDisplayService;
        protected readonly ICWFeatureService _cwFeatureService;

        #endregion

        #region Ctor

        public ReturnRequestOverrideModelFactory(CaptchaSettings captchaSettings,
            ICurrencyService currencyService,
            IDateTimeHelper dateTimeHelper,
            IDownloadService downloadService,
            ILocalizationService localizationService,
            IOrderService orderService,
            IPriceFormatter priceFormatter,
            IProductService productService,
            IReturnRequestService returnRequestService,
            IStoreContext storeContext,
            INopUrlHelper nopUrlHelper,
            IUrlRecordService urlRecordService,
            IWorkContext workContext,
            ReturnRequestSettings returnRequestSettings,
            IExchangeService exchangeService,
            IReturnRequestItemModelFactory returnRequestItemModelFactory,
            IExchangesDisplayService exchangesDisplayService,
            ICWFeatureService cwFeatureService) : base(captchaSettings,
                currencyService,
                dateTimeHelper,
                downloadService,
                localizationService,
                orderService,
                priceFormatter,
                productService,
                returnRequestService,
                storeContext,
                nopUrlHelper,
                urlRecordService,
                workContext,
                returnRequestSettings)
        {
            _exchangeService = exchangeService;
            _returnRequestItemModelFactory = returnRequestItemModelFactory;
            _exchangesDisplayService = exchangesDisplayService;
            _cwFeatureService = cwFeatureService;
        }

        #endregion

        #region Overriddes

        public override async Task<SubmitReturnRequestModel> PrepareSubmitReturnRequestModelAsync(SubmitReturnRequestModel model, Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            ArgumentNullException.ThrowIfNull(model);

            //nop's page values (titles, settings, reasons, the returnable items through the override below)
            model = await base.PrepareSubmitReturnRequestModelAsync(model, order);

            // Work with the CW model so the views can bind ready-made dropdown lists
            var cwModel = model as CWSubmitReturnRequestModel ?? new CWSubmitReturnRequestModel(model);

            var store = await _storeContext.GetCurrentStoreAsync();

            //with Exchanges on, the reasons the store's rules offer (an inactive rule, or none, hides the reason)
            if (await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, store.Id))
            {
                var activeReasonIds = await _exchangeService.GetActiveReasonIdsAsync(store.Id);
                cwModel.AvailableReturnReasons = cwModel.AvailableReturnReasons.Where(reason => activeReasonIds.Contains(reason.Id)).ToList();
            }

            cwModel.ReturnReasonItems = cwModel.AvailableReturnReasons
                .Select(r => new SelectListItem { Text = r.Name, Value = r.Id.ToString() })
                .ToList();

            //the reason the page opens with (the first one when none is chosen yet), so its actions are listed from the start
            if (cwModel.AvailableReturnReasons.All(reason => reason.Id != cwModel.ReturnRequestReasonId))
                cwModel.ReturnRequestReasonId = cwModel.AvailableReturnReasons.FirstOrDefault()?.Id ?? 0;

            //return actions - same method the cascade calls, so both paths always agree
            cwModel.AvailableReturnActions = await (await _exchangeService.GetActiveActionsByReasonIdAsync(cwModel.ReturnRequestReasonId, store.Id))
                .SelectAwait(async returnAction => new SubmitReturnRequestModel.ReturnRequestActionModel
                {
                    Id = returnAction.Id,
                    Name = await _localizationService.GetLocalizedAsync(returnAction, x => x.Name)
                })
                .ToListAsync();

            cwModel.ReturnActionItems = cwModel.AvailableReturnActions
                .Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() })
                .ToList();

            return cwModel;
        }

        /// <summary>
        /// Prepares the order item models for return request by specified order.
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>
        /// The <see cref="Task"/> containing the <see cref="IList{SubmitReturnRequestModel.OrderItemModel}"/>
        /// </returns>
        protected override async Task<IList<SubmitReturnRequestModel.OrderItemModel>> PrepareSubmitReturnRequestOrderItemModelsAsync(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            var models = new List<SubmitReturnRequestModel.OrderItemModel>();

            //CW: the units the customer may return, as the wizard's server checks count them (shipped units only)
            var returnableQuantities = await _exchangeService.GetReturnableQuantitiesAsync(order);

            foreach (var orderItem in await _orderService.GetOrderItemsAsync(order.Id))
            {
                if (!returnableQuantities.TryGetValue(orderItem.Id, out var quantity))
                    continue;

                models.Add(await PrepareSubmitReturnRequestOrderItemModelAsync(order, orderItem, quantity));
            }

            return models;
        }

        /// <summary>
        /// Builds the customer's return request list, upgrading each entry to the CW model so the
        /// exchange type, replacement product and packname can be shown.
        /// </summary>
        public override async Task<CustomerReturnRequestsModel> PrepareCustomerReturnRequestsModelAsync()
        {
            var model = await base.PrepareCustomerReturnRequestsModelAsync();

            var items = new List<CustomerReturnRequestsModel.ReturnRequestModel>();

            foreach (var item in model.Items)
                items.Add(await _returnRequestItemModelFactory.PrepareCustomerExchangeRequestModelAsync(item));

            model.Items = items;

            return model;
        }

        #endregion

        #region Methods

        public async Task<SubmitReturnRequestModel.OrderItemModel> PrepareSubmitReturnRequestOrderItemModelAsync(Order order, OrderItem orderItem, int qtyForReturn)
        {
            var product = await _productService.GetProductByIdAsync(orderItem.ProductId);

            var model = new CWSubmitReturnRequestModel.CWOrderItemModel(new SubmitReturnRequestModel.OrderItemModel
            {
                Id = orderItem.Id,
                ProductId = product.Id,
                ProductName = await _localizationService.GetLocalizedAsync(product, x => x.Name),
                ProductSeName = await _urlRecordService.GetSeNameAsync(product),
                AttributeInfo = orderItem.AttributeDescription,
                Quantity = qtyForReturn,
            })
            {
                Packname = await _exchangesDisplayService.GetPacknameAsync(orderItem.Id),
                PictureUrl = await _exchangesDisplayService.GetProductThumbUrlAsync(orderItem.ProductId, orderItem.AttributesXml, CWCoreDefaults.ReturnItemThumbSize)
            };

            var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

            //unit price
            if (order.CustomerTaxDisplayType == TaxDisplayType.IncludingTax)
            {
                //including tax
                var unitPriceInclTaxInCustomerCurrency = _currencyService.ConvertCurrency(orderItem.UnitPriceInclTax, order.CurrencyRate);
                model.UnitPrice = await _priceFormatter.FormatPriceAsync(unitPriceInclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, true);
            }
            else
            {
                //excluding tax
                var unitPriceExclTaxInCustomerCurrency = _currencyService.ConvertCurrency(orderItem.UnitPriceExclTax, order.CurrencyRate);
                model.UnitPrice = await _priceFormatter.FormatPriceAsync(unitPriceExclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, languageId, false);
            }

            return model;
        }

        #endregion
    }
}
