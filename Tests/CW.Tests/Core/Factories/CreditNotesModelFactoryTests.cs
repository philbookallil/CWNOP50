using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Orders;
using Nop.Services.Stores;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The admin credit-notes factory: the customer's available amount, the orders offered for a manual credit note, and the
/// per-store liability report. Prices are formatted as "$" plus the amount with two decimals.
/// </summary>
[TestFixture]
public class CreditNotesModelFactoryTests
{
    private Mock<ICreditNotesService> _creditNotesService = null!;
    private Mock<IOrderService> _orderService = null!;
    private Mock<IStoreService> _storeService = null!;
    private Mock<ICustomerService> _customerService = null!;
    private CreditNotesModelFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _creditNotesService = new Mock<ICreditNotesService>();
        _orderService = new Mock<IOrderService>();
        _storeService = new Mock<IStoreService>();
        _customerService = new Mock<ICustomerService>();

        var priceFormatter = new Mock<IPriceFormatter>();
        priceFormatter.Setup(formatter => formatter.FormatPriceAsync(It.IsAny<decimal>(), true, false))
            .ReturnsAsync((decimal price, bool _, bool _) => $"${price:0.00}");

        _factory = new CreditNotesModelFactory(_creditNotesService.Object,
            Mock.Of<IWorkContext>(),
            priceFormatter.Object,
            Mock.Of<IDateTimeHelper>(),
            _orderService.Object,
            _storeService.Object,
            _customerService.Object);
    }

    private static CreditNote Note(int customerId, decimal amount, decimal remain) =>
        new() { CustomerId = customerId, Amount = amount, Remain = remain };

    [Test]
    public async Task The_customer_summary_is_the_remainder_of_the_open_credit_notes()
    {
        _creditNotesService.Setup(service => service.GetOpenCreditNotesByCustomerIdAsync(4))
            .ReturnsAsync([Note(4, 100, 40), Note(4, 50, 50)]);

        var model = await _factory.PrepareCreditNotesCustomerSummaryModelAsync(4);

        model.CustomerId.Should().Be(4);
        model.CreditNotesAvailableAmount.Should().Be(90);
        model.CreditNotesAvailableAmountFormated.Should().Be("$90.00");
    }

    [Test]
    public async Task A_manual_credit_note_offers_the_customers_orders_by_number()
    {
        var orders = new PagedList<Order>([new Order { Id = 11, CustomOrderNumber = "1011" }, new Order { Id = 12, CustomOrderNumber = "1012" }], 0, int.MaxValue);
        _orderService.Setup(service => service.SearchOrdersAsync(It.IsAny<int>(), It.IsAny<int>(), 4, It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(orders);

        var model = await _factory.PrepareCreditNoteAddFormModelAsync(4);

        model.CustomerId.Should().Be(4);
        model.AvailableOrders.Select(item => (item.Value, item.Text)).Should().Equal(("11", "#1011"), ("12", "#1012"));
    }

    [Test]
    public async Task The_store_liability_counts_issued_and_consumed_notes_and_what_is_still_owed()
    {
        _storeService.Setup(service => service.GetAllStoresAsync()).ReturnsAsync([new Store { Id = 1, Name = "Main" }]);
        _creditNotesService.Setup(service => service.GetCreditNotesByStoreAsync(1))
            .ReturnsAsync([Note(4, 100, 0), Note(4, 100, 25), Note(5, 200, 200)]);

        var model = await _factory.PrepareStoreLiabilityListModelAsync(new StoreLiabilitySearchModel { Start = 0, Length = 10 });

        var row = model.Data.Should().ContainSingle().Subject;
        row.StoreId.Should().Be(1);
        row.StoreName.Should().Be("Main");
        row.CreditNotesIssued.Should().Be(3);
        row.CreditNotesConsumed.Should().Be(1);
        row.IssuedCreditNotesValue.Should().Be(400);
        row.LiabilityValue.Should().Be(225);
        row.LiabilityValueStr.Should().Be("$225.00");
        row.PercentageUsed.Should().Be(43.75m);
        row.PercentageUsedStr.Should().Be("43.75%");
    }

    [Test]
    public async Task A_store_with_no_credit_notes_has_nothing_used()
    {
        _storeService.Setup(service => service.GetAllStoresAsync()).ReturnsAsync([new Store { Id = 2, Name = "Empty" }]);
        _creditNotesService.Setup(service => service.GetCreditNotesByStoreAsync(2)).ReturnsAsync([]);

        var model = await _factory.PrepareStoreLiabilityListModelAsync(new StoreLiabilitySearchModel { Start = 0, Length = 10 });

        var row = model.Data.Should().ContainSingle().Subject;
        row.CreditNotesIssued.Should().Be(0);
        row.PercentageUsed.Should().Be(0);
        row.PercentageUsedStr.Should().Be("0%");
    }

    [Test]
    public async Task The_report_header_counts_the_credit_notes_with_a_balance_and_what_is_left_on_them_in_every_store()
    {
        _storeService.Setup(service => service.GetAllStoresAsync())
            .ReturnsAsync([new Store { Id = 1, Name = "Main" }, new Store { Id = 2, Name = "Second" }]);
        _creditNotesService.Setup(service => service.GetCreditNotesByStoreAsync(1)).ReturnsAsync([Note(4, 100, 0), Note(4, 100, 25)]);
        _creditNotesService.Setup(service => service.GetCreditNotesByStoreAsync(2)).ReturnsAsync([Note(5, 200, 200)]);

        var model = await _factory.PrepareCreditNotesReportModelAsync();

        model.CreditNotesWithBalance.Should().Be(2);
        model.TotalCreditNotesValue.Should().Be("$225.00", "the outstanding value is what is left on them");
    }

    [Test]
    public async Task The_store_customers_list_has_one_row_per_customer_with_what_is_left_on_their_notes()
    {
        _creditNotesService.Setup(service => service.GetCreditNotesByStoreAsync(1))
            .ReturnsAsync([Note(4, 100, 0), Note(4, 100, 25), Note(4, 50, 50), Note(5, 200, 200)]);
        _customerService.Setup(service => service.GetCustomerByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => new Customer { Id = id });
        _customerService.Setup(service => service.GetCustomerFullNameAsync(It.IsAny<Customer>())).ReturnsAsync((Customer customer) => $"Customer {customer.Id}");

        var model = await _factory.PrepareStoreCustomerCreditNotesListModelAsync(new StoreLiabilitySearchModel { StoreId = 1, Start = 0, Length = 10 });

        model.Data.Select(row => (row.CustomerName, row.NumberOfAvailableCreditNotes, row.CreditNotesValue, row.CreditNotesValueStr))
            .Should().Equal(("Customer 4", 2, 75m, "$75.00"), ("Customer 5", 1, 200m, "$200.00"));
    }
}
