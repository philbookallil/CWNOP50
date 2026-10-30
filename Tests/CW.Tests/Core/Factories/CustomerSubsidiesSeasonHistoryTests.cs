using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// My Subsidies' history by season: under the figures, the current season's; earlier seasons apart, each a closed
/// statement. What stands for every season is in each one from the season it was made in.
/// </summary>
[TestFixture]
public class CustomerSubsidiesSeasonHistoryTests
{
    private static readonly IList<(DateTime Start, DateTime End)> Seasons =
    [
        (new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)),
        (new DateTime(2026, 1, 1), new DateTime(2026, 12, 31))
    ];

    private CustomerSubsidiesModelFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        var dateTimeHelper = new Mock<IDateTimeHelper>();
        dateTimeHelper.Setup(helper => helper.DefaultStoreTimeZone).Returns(TimeZoneInfo.Utc);
        dateTimeHelper.Setup(helper => helper.ConvertToUserTimeAsync(It.IsAny<DateTime>(), DateTimeKind.Utc)).ReturnsAsync((DateTime date, DateTimeKind _) => date);
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingCurrencyAsync()).ReturnsAsync(new Currency { Id = 1 });
        workContext.Setup(context => context.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 1 });
        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(service => service.ConvertFromPrimaryStoreCurrencyAsync(It.IsAny<decimal>(), It.IsAny<Currency>())).ReturnsAsync((decimal amount, Currency _) => amount);
        var priceFormatter = new Mock<IPriceFormatter>();
        priceFormatter.Setup(formatter => formatter.FormatPriceAsync(It.IsAny<decimal>(), true, It.IsAny<Currency>(), 1, true, false))
            .ReturnsAsync((decimal amount, bool _, Currency _, int _, bool _, bool _) => $"${amount:0.00}");
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        _factory = (CustomerSubsidiesModelFactory)RuntimeHelpers.GetUninitializedObject(typeof(CustomerSubsidiesModelFactory));
        foreach (var (name, value) in new (string, object)[]
                 {
                     ("_dateTimeHelper", dateTimeHelper.Object), ("_workContext", workContext.Object), ("_currencyService", currencyService.Object),
                     ("_priceFormatter", priceFormatter.Object), ("_localizationService", localizationService.Object)
                 })
            typeof(CustomerSubsidiesModelFactory).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_factory, value);
    }

    private static SubsidyHistory Row(HistoryType type, int year, int month, int day, int orderId = 0, decimal credit = 0, decimal topup = 0, int quantity = 0) =>
        new() { HistoryType = type, CreatedOn = new DateTime(year, month, day), OrderId = orderId, Credit = credit, CreditTopup = topup, Quantity = quantity, ProductGroupCredit = 0 };

    private IList<List<SubsidyHistory>> BySeason(params SubsidyHistory[] history) =>
        (IList<List<SubsidyHistory>>)typeof(CustomerSubsidiesModelFactory).GetMethod("HistoryBySeason", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_factory, [history.ToList(), Seasons])!;

    [Test]
    public void Top_ups_and_orders_stay_in_their_season_and_the_assignment_stands_in_every_one()
    {
        var assigned = Row(HistoryType.Subsidy, 2024, 12, 1, credit: 100);
        var oldOrder = Row(HistoryType.SubsidyUsed, 2025, 5, 1, orderId: 10, credit: 30);
        var oldTopup = Row(HistoryType.SubsidyTopUp, 2025, 6, 1, topup: 20);
        var newOrder = Row(HistoryType.SubsidyUsed, 2026, 2, 1, orderId: 11, credit: 40);

        var bySeason = BySeason(newOrder, oldTopup, oldOrder, assigned);

        bySeason[0].Should().Equal(oldTopup, oldOrder, assigned);
        bySeason[1].Should().Equal(newOrder, assigned);
    }

    [Test]
    public void A_variation_stands_from_its_season_on_only()
    {
        var variation = Row(HistoryType.SubsidyVariation, 2026, 3, 1, credit: 150);

        var bySeason = BySeason(variation);

        bySeason[0].Should().BeEmpty();
        bySeason[1].Should().Equal(variation);
    }

    [Test]
    public void A_refund_is_in_the_season_of_the_order_it_gives_back_to()
    {
        var order = Row(HistoryType.SubsidyUsed, 2025, 11, 20, orderId: 10, credit: 30);
        var refund = Row(HistoryType.Refund, 2026, 1, 10, orderId: 10, credit: 30);

        var bySeason = BySeason(refund, order);

        bySeason[0].Should().Equal(refund, order);
        bySeason[1].Should().BeEmpty();
    }

    [Test]
    public async Task An_earlier_season_sums_what_was_topped_up_and_used_less_what_returns_gave_back()
    {
        var history = new List<SubsidyHistory>
        {
            Row(HistoryType.Refund, 2025, 8, 1, orderId: 10, credit: 5),
            Row(HistoryType.SubsidyTopUp, 2025, 6, 1, topup: 20),
            Row(HistoryType.SubsidyUsed, 2025, 5, 1, orderId: 10, credit: 30),
            Row(HistoryType.Subsidy, 2024, 12, 1, credit: 100),
        };

        var season = await (Task<CustomerSubsidySeasonModel>)typeof(CustomerSubsidiesModelFactory)
            .GetMethod("PrepareSeasonModelAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_factory, [Seasons[0], history])!;

        season.ToppedUpFormatted.Should().Be("$20.00");
        season.UsedFormatted.Should().Be("$25.00");
        season.HasActivity.Should().BeTrue();
        season.History.Should().HaveCount(4);
    }
}
