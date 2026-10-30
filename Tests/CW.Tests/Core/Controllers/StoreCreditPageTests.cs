using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Plugin.CW.Core.Controllers;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Models.CreditNotes;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// My Store Credit: the balance, the credit notes and the ledger, in the customer's currency, the ledger newest first
/// </summary>
[TestFixture]
public class StoreCreditPageTests
{
    private static readonly Currency Euro = new() { Id = 2, CurrencyCode = "EUR" };

    private Mock<ICreditNotesService> _creditNotesService = null!;
    private CreditNotesController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _creditNotesService = new Mock<ICreditNotesService>();
        _creditNotesService.Setup(service => service.GetOpenCreditNotesByCustomerIdAsync(5))
            .ReturnsAsync(new List<CreditNote> { new() { Id = 1, Name = "Return 1", Amount = 30m, Remain = 10m, CreatedOnUtc = new DateTime(2026, 1, 1) } });
        _creditNotesService.Setup(service => service.GetCreditNoteHistoriesByCustomerIdAsync(5)).ReturnsAsync(new List<CreditNoteHistory>
        {
            new() { Id = 1, CreditNoteId = 1, OrderId = 100, Amount = 5m, CreatedOnUtc = new DateTime(2026, 2, 1) },
            new() { Id = 2, CreditNoteId = 2, OrderId = 101, Amount = 7m, CreatedOnUtc = new DateTime(2026, 3, 1) },
            new() { Id = 3, CreditNoteId = 1, OrderId = 102, Amount = 15m, CreatedOnUtc = new DateTime(2026, 1, 15) },
        });
        _creditNotesService.Setup(service => service.SearchCreditNotesByCustomerIdAsync(5, 0, int.MaxValue))
            .ReturnsAsync(new PagedList<CreditNote>(new List<CreditNote> { new() { Id = 1, Name = "Return 1" }, new() { Id = 2, Name = "Return 2" } }, 0, int.MaxValue));

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingCurrencyAsync()).ReturnsAsync(Euro);
        //the customer's currency is worth twice the store's
        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(service => service.ConvertFromPrimaryStoreCurrencyAsync(It.IsAny<decimal>(), Euro))
            .ReturnsAsync((decimal amount, Currency _) => amount * 2);
        var priceFormatter = new Mock<IPriceFormatter>();
        priceFormatter.Setup(formatter => formatter.FormatPriceAsync(It.IsAny<decimal>(), true, false))
            .ReturnsAsync((decimal amount, bool _, bool _) => $"€{amount:0.00}");
        var dateTimeHelper = new Mock<IDateTimeHelper>();
        dateTimeHelper.Setup(helper => helper.ConvertToUserTimeAsync(It.IsAny<DateTime>(), DateTimeKind.Utc))
            .ReturnsAsync((DateTime date, DateTimeKind _) => date);

        _controller = (CreditNotesController)RuntimeHelpers.GetUninitializedObject(typeof(CreditNotesController));
        SetField("_creditNotesService", _creditNotesService.Object);
        SetField("_workContext", workContext.Object);
        SetField("_currencyService", currencyService.Object);
        SetField("_priceFormatter", priceFormatter.Object);
        SetField("_dateTimeHelper", dateTimeHelper.Object);
    }

    private void SetField(string name, object value) =>
        typeof(CreditNotesController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_controller, value);

    private async Task<StoreCreditsModel.StoreCreditSectionModel> PageAsync()
    {
        var method = typeof(CreditNotesController).GetMethod("PrepareMyStoreCreditModelAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (await (Task<StoreCreditsModel>)method.Invoke(_controller, [5])!).StoreCredit;
    }

    [Test]
    public async Task Amounts_are_in_the_customers_currency()
    {
        var section = await PageAsync();

        section.AvailableBalance.Should().Be("€20.00");
        section.IssuedCreditNotes.Single().Amount.Should().Be("€60.00");
        section.IssuedCreditNotes.Single().Remaining.Should().Be("€20.00");
        section.Ledger.Select(entry => entry.AmountApplied).Should().Contain("€30.00");
    }

    [Test]
    public async Task The_ledger_is_newest_first_and_names_each_credit_note_from_one_read()
    {
        var section = await PageAsync();

        section.Ledger.Select(entry => (entry.OrderId, entry.Name)).Should().Equal((101, "Return 2"), (100, "Return 1"), (102, "Return 1"));
        _creditNotesService.Verify(service => service.GetCreditNoteByIdAsync(It.IsAny<int>()), Times.Never);
        _creditNotesService.Verify(service => service.SearchCreditNotesByCustomerIdAsync(5, 0, int.MaxValue), Times.Once);
    }
}
