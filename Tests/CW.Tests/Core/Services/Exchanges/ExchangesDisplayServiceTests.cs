using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Localization;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Exchanges;

/// <summary>
/// The returns pages' amounts and names: an amount kept in the store's currency is shown in the customer's (an order's
/// in the order's, at its rate), and fee, exchange type and return mode names come from the language's texts
/// </summary>
[TestFixture]
public class ExchangesDisplayServiceTests
{
    private static readonly Currency Euro = new() { Id = 2, CurrencyCode = "EUR" };

    private ExchangesDisplayService _service = null!;
    private Mock<ILocalizationService> _localizationService = null!;

    [SetUp]
    public void SetUp()
    {
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingCurrencyAsync()).ReturnsAsync(Euro);
        workContext.Setup(context => context.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 3 });

        //the customer's currency is worth twice the store's
        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(service => service.ConvertFromPrimaryStoreCurrencyAsync(It.IsAny<decimal>(), Euro))
            .ReturnsAsync((decimal amount, Currency _) => amount * 2);
        currencyService.Setup(service => service.ConvertCurrency(It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns((decimal amount, decimal rate) => amount * rate);

        var priceFormatter = new Mock<IPriceFormatter>();
        priceFormatter.Setup(formatter => formatter.FormatPriceAsync(It.IsAny<decimal>(), true, false))
            .ReturnsAsync((decimal amount, bool _, bool _) => $"€{amount:0.00}");
        priceFormatter.Setup(formatter => formatter.FormatPriceAsync(It.IsAny<decimal>(), true, It.IsAny<string>(), false, 3))
            .ReturnsAsync((decimal amount, bool _, string code, bool _, int _) => $"{code} {amount:0.00}");

        _localizationService = new Mock<ILocalizationService>();

        _service = TestObject.Create<ExchangesDisplayService>();
        TestObject.Set(_service, "_workContext", workContext.Object);
        TestObject.Set(_service, "_currencyService", currencyService.Object);
        TestObject.Set(_service, "_priceFormatter", priceFormatter.Object);
        TestObject.Set(_service, "_localizationService", _localizationService.Object);
    }

    [Test]
    public async Task An_amount_is_shown_in_the_customers_currency()
    {
        (await _service.FormatPriceAsync(7.5m)).Should().Be("€15.00");
    }

    [Test]
    public async Task A_negative_amount_shows_a_minus_sign()
    {
        //nop's formatting of a negative price follows the culture (brackets in some); the wizard always shows a minus
        (await _service.FormatPriceAsync(-7.5m)).Should().Be("-€15.00");
    }

    [Test]
    public async Task An_orders_amount_is_shown_in_the_orders_currency_at_its_rate()
    {
        var order = new Order { CurrencyRate = 1.5m, CustomerCurrencyCode = "GBP" };

        (await _service.FormatOrderPriceAsync(10m, order)).Should().Be("GBP 15.00");
    }

    [Test]
    public async Task Names_come_from_the_languages_texts()
    {
        _localizationService.Setup(service => service.GetLocalizedEnumAsync(FeeType.ReShippingFee, null)).ReturnsAsync("Frais de réexpédition");
        _localizationService.Setup(service => service.GetLocalizedEnumAsync(ExchangeType.SameStyle, null)).ReturnsAsync("Même modèle");
        _localizationService.Setup(service => service.GetLocalizedEnumAsync(ExchangeReturnMode.CreditNote, null)).ReturnsAsync("Avoir");

        (await _service.GetFeeTypeNameAsync(FeeType.ReShippingFee)).Should().Be("Frais de réexpédition");
        (await _service.GetExchangeTypeNameAsync(ExchangeType.SameStyle)).Should().Be("Même modèle");
        (await _service.GetExchangeModeNameAsync(ExchangeReturnMode.CreditNote)).Should().Be("Avoir");
    }
}
