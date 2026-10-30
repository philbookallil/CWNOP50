using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Plugin.CW.Core.Factories;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// My Subsidies shows its money in the customer's currency: amounts are stored in the primary store currency
/// </summary>
[TestFixture]
public class CustomerSubsidiesModelFactoryTests
{
    [Test]
    public async Task An_amount_is_converted_to_the_customers_currency_before_it_is_formatted()
    {
        var euro = new Currency { Id = 2, CurrencyCode = "EUR" };
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingCurrencyAsync()).ReturnsAsync(euro);
        workContext.Setup(context => context.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 1 });
        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(service => service.ConvertFromPrimaryStoreCurrencyAsync(100m, euro)).ReturnsAsync(60m);
        var priceFormatter = new Mock<IPriceFormatter>();
        priceFormatter.Setup(formatter => formatter.FormatPriceAsync(It.IsAny<decimal>(), true, euro, 1, true, false))
            .ReturnsAsync((decimal amount, bool _, Currency currency, int _, bool _, bool _) => $"{currency.CurrencyCode} {amount:0.00}");

        var factory = (CustomerSubsidiesModelFactory)RuntimeHelpers.GetUninitializedObject(typeof(CustomerSubsidiesModelFactory));
        foreach (var (name, value) in new (string, object)[] { ("_workContext", workContext.Object), ("_currencyService", currencyService.Object), ("_priceFormatter", priceFormatter.Object) })
            typeof(CustomerSubsidiesModelFactory).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(factory, value);

        var method = typeof(CustomerSubsidiesModelFactory).GetMethod("FormatAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        (await (Task<string>)method.Invoke(factory, [100m])!).Should().Be("EUR 60.00");
    }
}
