using System.Reflection;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Components;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Models.CreditNotes;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Components;

/// <summary>
/// The store credit an order used, on the customer's order page and the admin order page: in the currency and at the rate
/// the order was placed with, as every other total of the order, whatever currency the viewer has selected now.
/// </summary>
[TestFixture]
public class CreditNotesOrderAmountTests
{
    private static readonly Order Order = new() { Id = 7, CurrencyRate = 2m, CustomerCurrencyCode = "EUR" };
    private static readonly Currency PrimaryCurrency = new() { Id = 1, CurrencyCode = "AUD" };

    private Mock<IPriceFormatter> _priceFormatter = null!;
    private Mock<ICurrencyService> _currencyService = null!;
    private CreditNotesWidgetsViewComponent _component = null!;

    [SetUp]
    public void SetUp()
    {
        var creditNotesService = new Mock<ICreditNotesService>();
        creditNotesService.Setup(service => service.GetCreditNoteHistoriesByOrderIdAsync(7))
            .ReturnsAsync(new List<CreditNoteHistory> { new() { Amount = 6m }, new() { Amount = 4m } });
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(7)).ReturnsAsync(Order);
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 3 });
        //the viewer has another currency selected now: it must not be used
        workContext.Setup(context => context.GetWorkingCurrencyAsync()).ReturnsAsync(new Currency { Id = 9, CurrencyCode = "USD", Rate = 5m });
        _currencyService = new Mock<ICurrencyService>();
        _currencyService.Setup(service => service.ConvertCurrency(It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns((decimal amount, decimal rate) => amount * rate);
        _currencyService.Setup(service => service.GetCurrencyByIdAsync(1)).ReturnsAsync(PrimaryCurrency);
        _priceFormatter = new Mock<IPriceFormatter>();
        _priceFormatter.Setup(formatter => formatter.FormatPriceAsync(It.IsAny<decimal>(), true, "EUR", false, 3))
            .ReturnsAsync((decimal amount, bool _, string code, bool _, int _) => $"{code} {amount:0.00}");
        _priceFormatter.Setup(formatter => formatter.FormatOrderPriceAsync(It.IsAny<decimal>(), 2m, "EUR", It.IsAny<bool>(), PrimaryCurrency, 3, null, false))
            .ReturnsAsync((decimal amount, decimal _, string _, bool _, Currency primary, int _, bool? _, bool? _) => $"{primary.CurrencyCode} {amount:0.00}");

        _component = new CreditNotesWidgetsViewComponent(new Mock<IOrderTotalCalculationOverrideService>().Object, workContext.Object,
            new Mock<IStoreContext>().Object, _currencyService.Object, _priceFormatter.Object, creditNotesService.Object,
            new Mock<IShoppingCartService>().Object, orderService.Object, new CurrencySettings { PrimaryStoreCurrencyId = 1 }, new OrderSettings());
    }

    private async Task<CreditNotesOrderSummaryModel> SummaryAsync(bool adminPage)
    {
        var model = new CreditNotesOrderSummaryModel();
        var method = typeof(CreditNotesWidgetsViewComponent).GetMethod("PopulateCreditNotesSummaryFromOrderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(_component, [7, model, adminPage])!;

        return model;
    }

    [Test]
    public async Task The_customers_order_page_shows_the_credit_in_the_orders_currency_and_rate()
    {
        var model = await SummaryAsync(adminPage: false);

        model.NeedToShow.Should().BeTrue();
        model.Amount.Should().Be("EUR -20.00");
    }

    [Test]
    public async Task The_admin_order_page_shows_the_credit_as_nops_admin_totals_do()
    {
        var model = await SummaryAsync(adminPage: true);

        model.Amount.Should().Be("AUD 10.00");
    }
}
