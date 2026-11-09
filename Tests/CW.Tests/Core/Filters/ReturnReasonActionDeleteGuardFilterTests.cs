using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Nop.Plugin.CW.Core.Areas.Admin.Controllers;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Controllers;
using NUnit.Framework;

namespace CW.Tests.Core.Filters;

/// <summary>
/// Deleting a return reason or action that a return records is refused; one no return uses is deleted as before
/// </summary>
[TestFixture]
public class ReturnReasonActionDeleteGuardFilterTests
{
    private Mock<IExchangeService> _exchangeService = null!;
    private Mock<INotificationService> _notificationService = null!;
    private ReturnReasonActionDeleteGuardFilter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        //reason 1 and action 3 are used by a return; reason 2 and action 4 are not
        _exchangeService = new Mock<IExchangeService>();
        _exchangeService.Setup(service => service.IsReturnReasonInUseAsync(1)).ReturnsAsync(true);
        _exchangeService.Setup(service => service.IsReturnReasonInUseAsync(2)).ReturnsAsync(false);
        _exchangeService.Setup(service => service.IsReturnActionInUseAsync(3)).ReturnsAsync(true);
        _exchangeService.Setup(service => service.IsReturnActionInUseAsync(4)).ReturnsAsync(false);

        _notificationService = new Mock<INotificationService>();

        _filter = new ReturnReasonActionDeleteGuardFilter(_exchangeService.Object, new Mock<ILocalizationService>().Object, _notificationService.Object);
    }

    private async Task<(IActionResult? result, bool reachedAction)> PostAsync<TController>(string method, int id) where TController : class
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;

        var actionDescriptor = new ControllerActionDescriptor
        {
            ActionName = method,
            //the first overload: the edit actions have a GET and a POST of the same name
            MethodInfo = typeof(ReturnRequestController).GetMethods().First(candidate => candidate.Name == method)
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
        var arguments = new Dictionary<string, object?> { ["id"] = id };
        var controller = TestObject.Create<TController>();
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
    public async Task A_reason_a_return_uses_is_not_deleted()
    {
        var (result, reachedAction) = await PostAsync<ReturnRequestController>(nameof(ReturnRequestController.ReturnRequestReasonDelete), 1);

        reachedAction.Should().BeFalse();
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ReturnRequestController.ReturnRequestReasonEdit));
        _notificationService.Verify(service => service.ErrorNotification(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Once);
    }

    [Test]
    public async Task A_reason_no_return_uses_is_deleted()
    {
        var (result, reachedAction) = await PostAsync<ReturnRequestController>(nameof(ReturnRequestController.ReturnRequestReasonDelete), 2);

        reachedAction.Should().BeTrue();
        result.Should().BeNull();
    }

    [Test]
    public async Task An_action_a_return_uses_is_not_deleted()
    {
        var (result, reachedAction) = await PostAsync<ReturnRequestController>(nameof(ReturnRequestController.ReturnRequestActionDelete), 3);

        reachedAction.Should().BeFalse();
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(ReturnRequestController.ReturnRequestActionEdit));
    }

    [Test]
    public async Task An_action_no_return_uses_is_deleted()
    {
        var (_, reachedAction) = await PostAsync<ReturnRequestController>(nameof(ReturnRequestController.ReturnRequestActionDelete), 4);

        reachedAction.Should().BeTrue();
    }

    [Test]
    public async Task The_same_delete_reached_through_CWs_returns_controller_is_guarded_too()
    {
        //ReturnsController derives from nop's ReturnRequestController, so the delete is routable under its name as well
        var (_, reachedAction) = await PostAsync<ReturnsController>(nameof(ReturnRequestController.ReturnRequestReasonDelete), 1);

        reachedAction.Should().BeFalse();
    }

    [Test]
    public async Task Other_return_settings_actions_are_not_checked()
    {
        var (_, reachedAction) = await PostAsync<ReturnRequestController>(nameof(ReturnRequestController.ReturnRequestReasonEdit), 1);

        reachedAction.Should().BeTrue();
        _exchangeService.Verify(service => service.IsReturnReasonInUseAsync(It.IsAny<int>()), Times.Never);
    }
}
