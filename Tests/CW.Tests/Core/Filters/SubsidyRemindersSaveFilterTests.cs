using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Moq;
using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Models.Reminders;
using NUnit.Framework;

namespace CW.Tests.Core.Filters;

/// <summary>
/// The subsidy card of Promotions › Reminders is saved only by nop's save of that page, and only when the card was on
/// the page; every other request goes through untouched
/// </summary>
[TestFixture]
public class SubsidyRemindersSaveFilterTests
{
    private Mock<ISubsidyReminderService> _subsidyReminderService = null!;
    private SubsidyRemindersSaveFilter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        _subsidyReminderService = new Mock<ISubsidyReminderService>();

        _filter = new SubsidyRemindersSaveFilter(new Mock<ILocalizationService>().Object, new Mock<IStoreContext>().Object, _subsidyReminderService.Object);
    }

    private async Task<bool> RunAsync<TController>(string method, string httpMethod, Dictionary<string, StringValues>? form) where TController : class
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;

        if (form != null)
        {
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Request.Form = new FormCollection(form);
        }

        var actionDescriptor = new ControllerActionDescriptor
        {
            ActionName = method,
            MethodInfo = typeof(TController).GetMethods().First(candidate => candidate.Name == method)
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
        var controller = TestObject.Create<TController>();
        var executing = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller);
        var reachedAction = false;

        await _filter.OnActionExecutionAsync(executing, () =>
        {
            reachedAction = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), controller));
        });

        return reachedAction;
    }

    private void VerifyNothingSaved()
        => _subsidyReminderService.Verify(service => service.SaveExpiryReminderSettingsAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<bool>(), It.IsAny<IList<SubsidyExpiryFollowUpSetting>>()), Times.Never);

    [Test]
    public void The_card_field_is_the_store_scope_field_of_the_card()
    {
        SubsidyRemindersSaveFilter.CardField.Should().Be("SubsidyExpiry.ActiveStoreScopeConfiguration");
        SubsidyRemindersSaveFilter.FormPrefix.Should().Be(nameof(CWRemindersModel.SubsidyExpiry));
    }

    [Test]
    public void Nops_save_action_is_still_Index_taking_the_reminders_model()
    {
        //the filter matches nop's action by name: an upgrade that renames or reshapes it fails here
        typeof(ReminderController).GetMethods()
            .Should().Contain(method => method.Name == nameof(ReminderController.Index)
                && method.GetParameters().Length == 1
                && method.GetParameters()[0].ParameterType == typeof(RemindersModel));
    }

    [Test]
    public async Task Opening_the_page_is_not_touched()
    {
        var reachedAction = await RunAsync<ReminderController>(nameof(ReminderController.Index), HttpMethods.Get, null);

        reachedAction.Should().BeTrue();
        VerifyNothingSaved();
    }

    [Test]
    public async Task A_save_of_a_page_drawn_without_the_card_saves_nothing_of_it()
    {
        //subsidies off: the card was not drawn, so its switch must not be turned off by a form that did not show it
        var reachedAction = await RunAsync<ReminderController>(nameof(ReminderController.Index), HttpMethods.Post,
            new Dictionary<string, StringValues> { ["AbandonedCartEnabled"] = "true" });

        reachedAction.Should().BeTrue();
        VerifyNothingSaved();
    }

    [Test]
    public async Task Another_reminders_action_is_not_touched()
    {
        var reachedAction = await RunAsync<ReminderController>(nameof(ReminderController.ProcessPendingOrdersTaskWarning), HttpMethods.Post,
            new Dictionary<string, StringValues> { [SubsidyRemindersSaveFilter.CardField] = "0" });

        reachedAction.Should().BeTrue();
        VerifyNothingSaved();
    }

    [Test]
    public async Task Another_controllers_index_is_not_touched()
    {
        var reachedAction = await RunAsync<HomeController>(nameof(HomeController.Index), HttpMethods.Post,
            new Dictionary<string, StringValues> { [SubsidyRemindersSaveFilter.CardField] = "0" });

        reachedAction.Should().BeTrue();
        VerifyNothingSaved();
    }
}
