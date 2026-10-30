using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Controllers;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Customers;
using Nop.Services.Security;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The admin credit-notes controller's answers to the customer page's Credit notes card
/// </summary>
[TestFixture]
public class CreditNotesControllerTests
{
    private Mock<ICreditNotesService> _creditNotesService = null!;
    private Mock<ICreditNotesModelFactory> _modelFactory = null!;
    private Mock<ICustomerService> _customerService = null!;
    private CreditNotesController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _creditNotesService = new Mock<ICreditNotesService>();
        _modelFactory = new Mock<ICreditNotesModelFactory>();
        _customerService = new Mock<ICustomerService>();

        var permissionService = new Mock<IPermissionService>();
        permissionService.Setup(service => service.AuthorizeAsync(CreditNotesPermissionConfigManager.ALLOW_MANUAL_CREDITNOTES)).ReturnsAsync(true);

        _controller = new CreditNotesController(_creditNotesService.Object, permissionService.Object, _modelFactory.Object,
            _customerService.Object, Mock.Of<ICWFeatureGuard>());
    }

    private static T Property<T>(IActionResult result, string name) =>
        (T)result.Should().BeOfType<JsonResult>().Subject.Value!.GetType().GetProperty(name)!.GetValue(((JsonResult)result).Value)!;

    [Test]
    public async Task The_available_balance_is_returned_as_a_price()
    {
        _modelFactory.Setup(factory => factory.PrepareCreditNotesCustomerSummaryModelAsync(4))
            .ReturnsAsync(new CreditNotesCustomerSummaryModel { CustomerId = 4, CreditNotesAvailableAmount = 125.5m, CreditNotesAvailableAmountFormated = "$125.50" });

        var result = await _controller.GetCreditNotesSummary(4);

        Property<string>(result, "amount").Should().Be("$125.50");
    }

    [Test]
    public async Task A_manual_credit_note_for_an_unknown_customer_is_refused_with_a_message()
    {
        _customerService.Setup(service => service.GetCustomerByIdAsync(99)).ReturnsAsync((Customer)null!);

        var result = await _controller.SaveCreditNote(new CreditNoteAddFormModel { CustomerId = 99, OrderId = 11, Amount = 10 });

        Property<bool>(result, "success").Should().BeFalse();
        Property<string>(result, "message").Should().NotBeNullOrEmpty();
        _creditNotesService.Verify(service => service.CreateManualCreditNoteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<Customer>()), Times.Never);
    }
}
