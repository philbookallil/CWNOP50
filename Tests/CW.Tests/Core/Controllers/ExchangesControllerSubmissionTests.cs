using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Controllers;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Products;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Web.Models.Order;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The returns wizard goes on only with what the server's checks allow, registers one attempt once, and says when an
/// exchange's options could not be changed
/// </summary>
[TestFixture]
public class ExchangesControllerSubmissionTests
{
    private ExchangesController _controller = null!;
    private Mock<IReturnSubmissionLogic> _submissionLogic = null!;
    private Mock<ILocker> _locker = null!;
    private Mock<IExchangesLogic> _exchangesLogic = null!;
    private Mock<IExchangeService> _exchangeService = null!;
    private Mock<IReturnRequestService> _returnRequestService = null!;
    private Mock<IExchangesModelFactory> _exchangesModelFactory = null!;

    [SetUp]
    public void SetUp()
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(10)).ReturnsAsync(new Order { Id = 10, CustomerId = 5 });

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 5 });

        var orderProcessingService = new Mock<IOrderProcessingService>();
        orderProcessingService.Setup(service => service.IsReturnRequestAllowedAsync(It.IsAny<Order>())).ReturnsAsync(true);

        var validator = new Mock<IValidator<ReturnWizardSubmission>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<ReturnWizardSubmission>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

        _submissionLogic = new Mock<IReturnSubmissionLogic>();
        _submissionLogic.Setup(logic => logic.CheckSubmissionAsync(It.IsAny<Order>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ReturnWizardSubmission>()))
            .ReturnsAsync(new List<string>());

        _exchangesModelFactory = new Mock<IExchangesModelFactory>();
        _exchangesModelFactory.Setup(factory => factory.RebuildGatheredInfoAsync(It.IsAny<ReturnRequestGatherInfoModel>(), It.IsAny<ReturnWizardSubmission>()))
            .ReturnsAsync((ReturnRequestGatherInfoModel info, ReturnWizardSubmission _) => info);

        var returnRequestModelFactory = new Mock<IReturnRequestOverrideModelFactory>();
        returnRequestModelFactory.Setup(factory => factory.PrepareSubmitReturnRequestModelAsync(It.IsAny<SubmitReturnRequestModel>(), It.IsAny<Order>()))
            .ReturnsAsync((SubmitReturnRequestModel model, Order _) => model);

        _locker = new Mock<ILocker>();
        _locker.Setup(locker => locker.PerformActionWithLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<Task>>()))
            .Returns(async (string _, TimeSpan _, Func<Task> action) => { await action(); return true; });

        _exchangesLogic = new Mock<IExchangesLogic>();
        _exchangeService = new Mock<IExchangeService>();
        _returnRequestService = new Mock<IReturnRequestService>();
        _returnRequestService.Setup(service => service.GetReturnRequestByIdAsync(300))
            .ReturnsAsync(new ReturnRequest { Id = 300, CustomerId = 5, ReturnRequestStatusId = (int)ReturnRequestStatus.Pending });

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Nop.Core.Domain.Stores.Store { Id = 1 });

        _controller = TestObject.Create<ExchangesController>();
        TestObject.Set(_controller, "_orderService", orderService.Object);
        TestObject.Set(_controller, "_workContext", workContext.Object);
        TestObject.Set(_controller, "_orderProcessingService", orderProcessingService.Object);
        TestObject.Set(_controller, "_wizardValidator", validator.Object);
        TestObject.Set(_controller, "_returnSubmissionLogic", _submissionLogic.Object);
        TestObject.Set(_controller, "_exchangesModelFactory", _exchangesModelFactory.Object);
        TestObject.Set(_controller, "_returnRequestModelFactory", returnRequestModelFactory.Object);
        TestObject.Set(_controller, "_locker", _locker.Object);
        TestObject.Set(_controller, "_exchangesLogic", _exchangesLogic.Object);
        TestObject.Set(_controller, "_exchangeService", _exchangeService.Object);
        TestObject.Set(_controller, "_returnRequestService", _returnRequestService.Object);
        TestObject.Set(_controller, "_localizationService", localizationService.Object);
        TestObject.Set(_controller, "_notificationService", new Mock<INotificationService>().Object);
        TestObject.Set(_controller, "_logger", new Mock<ILogger>().Object);
        TestObject.Set(_controller, "_storeContext", storeContext.Object);
    }

    private static IFormCollection Form(params (string key, string value)[] fields) =>
        new FormCollection(fields.ToDictionary(field => field.key, field => new StringValues(field.value)));

    private static IFormCollection ExchangeForm() =>
        Form(("exchange_mode", ((int)ExchangeReturnMode.CreditNote).ToString()), ("returned_item_qty_100", "1"));

    private void TheChecksRefuse() =>
        _submissionLogic.Setup(logic => logic.CheckSubmissionAsync(It.IsAny<Order>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ReturnWizardSubmission>()))
            .ReturnsAsync(new List<string> { "too many" });

    [Test]
    public async Task The_first_step_refused_by_the_checks_shows_the_return_page_again_and_opens_no_case()
    {
        TheChecksRefuse();

        var result = await _controller.ReturnRequestGatherInfo(10, new SubmitReturnRequestModel(), Form(("quantity100", "50")));

        result.Should().BeOfType<ViewResult>().Which.ViewName.Should().EndWith("ReturnRequest/ReturnRequest.cshtml");
        _controller.ModelState[string.Empty]!.Errors.Select(error => error.ErrorMessage).Should().Equal("too many");
        _submissionLogic.Verify(logic => logic.EnsureDraftReturnCaseAsync(It.IsAny<Order>(), It.IsAny<SubmitReturnRequestModel>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task The_summary_refused_by_the_checks_shows_the_items_step_again()
    {
        TheChecksRefuse();

        var result = await _controller.DisplayReturnRequestSummary(new ReturnRequestGatherInfoModel { OrderId = 10 }, ExchangeForm());

        result.Should().BeOfType<ViewResult>().Which.ViewName.Should().EndWith("Exchanges/ReturnRequestGatherInfo.cshtml");
        _exchangesModelFactory.Verify(factory => factory.PrepareSummaryFeesAsync(It.IsAny<ReturnRequestSummaryModel>()), Times.Never);
    }

    [Test]
    public async Task A_confirmation_refused_by_the_checks_registers_nothing()
    {
        TheChecksRefuse();

        await _controller.ConfirmReturnRequestSummary(new ReturnRequestSummaryModel { GatheredInfo = new ReturnRequestGatherInfoModel { OrderId = 10 } }, ExchangeForm());

        _submissionLogic.Verify(logic => logic.RegisterReturnRequestDataAsync(It.IsAny<ReturnRequestGatherInfoModel>(), It.IsAny<ExchangeReturnMode>(), It.IsAny<List<int>>()), Times.Never);
    }

    [Test]
    public async Task A_second_confirmation_of_the_order_while_another_runs_registers_nothing()
    {
        //the first post holds the attempt's lock
        _locker.Setup(locker => locker.PerformActionWithLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<Task>>())).ReturnsAsync(false);
        var groupId = Guid.NewGuid();

        var result = await _controller.ConfirmReturnRequestSummary(new ReturnRequestSummaryModel
        {
            GatheredInfo = new ReturnRequestGatherInfoModel { OrderId = 10, ReturnRequestGroupId = groupId }
        }, ExchangeForm());

        result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("CustomerReturnRequests");
        _locker.Verify(locker => locker.PerformActionWithLockAsync(It.Is<string>(key => key.EndsWith(".10")), It.IsAny<TimeSpan>(), It.IsAny<Func<Task>>()),
            "one confirmation of the order at a time, whichever attempt");
        _submissionLogic.Verify(logic => logic.RegisterReturnRequestDataAsync(It.IsAny<ReturnRequestGatherInfoModel>(), It.IsAny<ExchangeReturnMode>(), It.IsAny<List<int>>()), Times.Never);
    }

    [Test]
    public async Task A_confirmation_registers_the_attempt()
    {
        _submissionLogic.Setup(logic => logic.RegisterReturnRequestDataAsync(It.IsAny<ReturnRequestGatherInfoModel>(), ExchangeReturnMode.CreditNote, It.IsAny<List<int>>()))
            .Callback((ReturnRequestGatherInfoModel _, ExchangeReturnMode _, List<int> ids) => ids.Add(41))
            .ReturnsAsync(true);

        var result = await _controller.ConfirmReturnRequestSummary(new ReturnRequestSummaryModel { GatheredInfo = new ReturnRequestGatherInfoModel { OrderId = 10 } }, ExchangeForm());

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("CustomerReturnRequests");
        redirect.RouteValues!["returnRequests"].Should().Be("41");
    }

    [Test]
    public async Task An_exchange_whose_options_could_not_be_changed_says_so()
    {
        _exchangeService.Setup(service => service.GetReturnRequestItemAsync(300)).ReturnsAsync(new ReturnRequestItem { Id = 7 });
        _exchangesLogic.Setup(logic => logic.ModifyExchangedItemDetailsAsync(It.IsAny<ReturnRequestItem>(), It.IsAny<IEnumerable<SelectedProductAttribute>>()))
            .ReturnsAsync(false);

        var result = await _controller.EditExchangedItem(300, [new SelectedProductAttribute { AttributeId = 1, AttributeValueId = 2 }]);

        result.Value.Should().BeEquivalentTo(new { success = false, message = "Plugins.CW.Core.Exchanges.Wizard.Edit.NoExchangeItem" });
    }

    [Test]
    public async Task A_confirmation_posted_again_after_it_registered_gives_its_requests_back()
    {
        //the attempt is registered: its items no longer count as returnable, so the checks would refuse it
        TheChecksRefuse();
        var registeredId = 41;
        _exchangeService.Setup(service => service.IsReturnRequestAlreadyPerformed(100, It.IsAny<Guid>(), out registeredId)).Returns(true);
        _submissionLogic.Setup(logic => logic.RegisterReturnRequestDataAsync(It.IsAny<ReturnRequestGatherInfoModel>(), ExchangeReturnMode.CreditNote, It.IsAny<List<int>>()))
            .Callback((ReturnRequestGatherInfoModel _, ExchangeReturnMode _, List<int> ids) => ids.Add(41))
            .ReturnsAsync(true);

        var result = await _controller.ConfirmReturnRequestSummary(new ReturnRequestSummaryModel { GatheredInfo = new ReturnRequestGatherInfoModel { OrderId = 10 } }, ExchangeForm());

        result.Should().BeOfType<RedirectToActionResult>().Which.RouteValues!["returnRequests"].Should().Be("41");
    }

    [Test]
    public async Task The_actions_for_a_reason_are_named_in_the_customers_language()
    {
        _exchangeService.Setup(service => service.GetActiveActionsByReasonIdAsync(1, 1)).ReturnsAsync(new List<ReturnRequestAction> { new() { Id = 8, Name = "Replace" } });
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetLocalizedAsync(It.IsAny<ReturnRequestAction>(), It.IsAny<System.Linq.Expressions.Expression<Func<ReturnRequestAction, string>>>(), null, true, true))
            .ReturnsAsync("Remplacer");
        TestObject.Set(_controller, "_localizationService", localizationService.Object);

        var result = await _controller.GetActionsForReason(1, 1);

        ((IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>)result.Value!).Select(item => item.Text).Should().Equal("Remplacer");
    }
}
