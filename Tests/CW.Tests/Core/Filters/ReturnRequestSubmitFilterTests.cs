using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Moq;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Web.Controllers;
using Nop.Web.Models.Order;
using NUnit.Framework;

namespace CW.Tests.Core.Filters;

/// <summary>
/// nop's own return request post: with Exchanges on, only a guest's withdrawal link uses it (the signed-in customer goes
/// through the wizard), and on every path the quantities are those CW's return page offers
/// </summary>
[TestFixture]
public class ReturnRequestSubmitFilterTests
{
    private Mock<ICWFeatureService> _featureService = null!;
    private ReturnRequestSubmitFilter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        _featureService = new Mock<ICWFeatureService>();

        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(10)).ReturnsAsync(new Order { Id = 10, StoreId = 1 });

        //two units of item 100 can be returned
        var exchangeService = new Mock<IExchangeService>();
        exchangeService.Setup(service => service.GetReturnableQuantitiesAsync(It.IsAny<Order>())).ReturnsAsync(new Dictionary<int, int> { [100] = 2 });

        _filter = new ReturnRequestSubmitFilter(_featureService.Object, exchangeService.Object, new Mock<ILocalizationService>().Object,
            new Mock<INotificationService>().Object, orderService.Object);
    }

    private void ExchangesOn() =>
        _featureService.Setup(service => service.IsEnabledAsync(CWFeature.Exchanges, 1)).ReturnsAsync(true);

    private async Task<(IActionResult? result, bool reachedAction)> PostAsync(string? token, params (string key, string value)[] fields)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(fields.ToDictionary(field => field.key, field => new StringValues(field.value)));

        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor { ActionName = "ReturnRequest" });
        var arguments = new Dictionary<string, object?> { ["orderId"] = 10, ["model"] = new SubmitReturnRequestModel { WithdrawalToken = token } };
        var controller = TestObject.Create<ReturnRequestController>();
        var executing = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), arguments, controller);
        var reachedAction = false;

        await _filter.OnActionExecutionAsync(executing, () =>
        {
            reachedAction = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), controller));
        });

        return (executing.Result, reachedAction);
    }

    [Test]
    public async Task With_exchanges_on_the_signed_in_customer_is_sent_to_the_wizard()
    {
        ExchangesOn();

        var (result, reachedAction) = await PostAsync(null, ("quantity100", "1"));

        reachedAction.Should().BeFalse();
        result.Should().BeOfType<RedirectToRouteResult>();
    }

    [Test]
    public async Task With_exchanges_on_a_guests_withdrawal_link_uses_nops_form()
    {
        ExchangesOn();

        var (_, reachedAction) = await PostAsync("token", ("quantity100", "1"));

        reachedAction.Should().BeTrue();
    }

    [Test]
    public async Task More_units_than_can_be_returned_are_refused()
    {
        var (result, reachedAction) = await PostAsync(null, ("quantity100", "3"));

        reachedAction.Should().BeFalse();
        result.Should().BeOfType<RedirectToRouteResult>();
    }

    [Test]
    public async Task An_item_that_cannot_be_returned_is_refused()
    {
        var (_, reachedAction) = await PostAsync("token", ("quantity200", "1"));

        reachedAction.Should().BeFalse();
    }

    [Test]
    public async Task Returnable_units_go_to_nops_form()
    {
        var (_, reachedAction) = await PostAsync(null, ("quantity100", "2"), ("quantity200", "0"));

        reachedAction.Should().BeTrue();
    }
}
