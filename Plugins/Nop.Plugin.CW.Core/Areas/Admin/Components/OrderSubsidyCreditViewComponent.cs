using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

/// <summary>
/// Renders the settlement rows in the admin order-details totals block: the total before subsidy and store
/// credit, and the subsidy that paid for goods and for shipping. Store credit and exchange fees have widgets
/// of their own beside it.
/// </summary>
/// <remarks>
/// This exists so that the view does not. _CWOrderDetails.Info.cshtml previously injected six services
/// and ran five awaits at the top of the file to produce one row. Services stay out of reach of a view
/// for exactly that reason: the work is invisible from the markup, runs on every render of the page, and
/// cannot be tested.
/// <para>
/// The settlement row is read first and the component returns empty when there is none, so an order that
/// settled nothing costs one cached read.
/// </para>
/// </remarks>
[ViewComponent(Name = "OrderSubsidyCredit")]
public class OrderSubsidyCreditViewComponent : NopViewComponent
{
    #region Fields

    protected readonly ICWOrderService _cwOrderService;
    protected readonly IOrderService _orderService;
    protected readonly ICurrencyService _currencyService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IWorkContext _workContext;
    protected readonly CurrencySettings _currencySettings;
    protected readonly OrderSettings _orderSettings;

    #endregion

    #region Ctor

    public OrderSubsidyCreditViewComponent(ICWOrderService cwOrderService,
        IOrderService orderService,
        ICurrencyService currencyService,
        IPriceFormatter priceFormatter,
        IWorkContext workContext,
        CurrencySettings currencySettings,
        OrderSettings orderSettings)
    {
        _cwOrderService = cwOrderService;
        _orderService = orderService;
        _currencyService = currencyService;
        _priceFormatter = priceFormatter;
        _workContext = workContext;
        _currencySettings = currencySettings;
        _orderSettings = orderSettings;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(int orderId = 0)
    {
        if (orderId <= 0)
            return Content(string.Empty);

        var settlement = await _cwOrderService.GetOrderSettlementAsync(orderId);

        //Nothing was settled, so there is no row — and none of the work below runs.
        if (settlement == null)
            return Content(string.Empty);

        var order = await _orderService.GetOrderByIdAsync(orderId);

        if (order == null)
            return Content(string.Empty);

        var primaryStoreCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        async Task<string> formatAsync(decimal amount) => await _priceFormatter.FormatOrderPriceAsync(
            amount,
            order.CurrencyRate,
            order.CustomerCurrencyCode,
            _orderSettings.DisplayCustomerCurrencyOnOrders,
            primaryStoreCurrency,
            languageId,
            null,
            false);

        var model = new OrderSubsidyCreditModel
        {
            FormattedGrossTotal = await formatAsync(settlement.SettleableAmount),
            FormattedSubsidyForStock = settlement.SubsidyStockAmount > decimal.Zero ? await formatAsync(settlement.SubsidyStockAmount) : null,
            FormattedSubsidyForShipping = settlement.SubsidyShippingAmount > decimal.Zero ? await formatAsync(settlement.SubsidyShippingAmount) : null,
        };

        return View(model);
    }

    #endregion
}