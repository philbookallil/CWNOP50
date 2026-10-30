using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Areas.Admin.Controllers;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The return request edit page and the exchanges settings: every button of CW's edit form reaches an action that checks
/// the return-request permission, and the linked-actions popup saves an empty selection
/// </summary>
[TestFixture]
public class ReturnsAdminControllersTests
{
    /// <summary>
    /// The buttons of CW's edit form (Views/ReturnRequest/Edit.cshtml), which always posts to Returns/EditExchangeRequest
    /// </summary>
    private static readonly string[] EditFormButtons = ["save", "save-continue", "notify-customer", "notify-checkout", "impersonate", "undo-approval"];

    private static IEnumerable<MethodInfo> EditExchangeRequestActions() =>
        typeof(ReturnsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => (method.GetCustomAttribute<ActionNameAttribute>()?.Name ?? method.Name) == "EditExchangeRequest");

    private static string[] ButtonNames(MethodInfo action) =>
        action.GetCustomAttribute<FormValueRequiredAttribute>() is { } formValue
            ? (string[])typeof(FormValueRequiredAttribute).GetField("_submitButtonNames", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(formValue)!
            : [];

    [TestCaseSource(nameof(EditFormButtons))]
    public void Every_button_of_the_edit_form_reaches_an_action(string button)
    {
        EditExchangeRequestActions().Should().Contain(action => ButtonNames(action).Contains(button),
            $"the form posts \"{button}\" to Returns/EditExchangeRequest");
    }

    [Test]
    public void Every_action_behind_the_edit_form_checks_the_return_request_permission()
    {
        var actions = EditExchangeRequestActions().ToList();

        actions.Should().HaveCount(5);
        foreach (var action in actions)
            action.GetCustomAttributes<CheckPermissionAttribute>()
                .SelectMany(attribute => (List<string>)attribute.Arguments![1])
                .Should().Contain(StandardPermission.Orders.RETURN_REQUESTS_CREATE_EDIT_DELETE, $"{action.Name} changes a return request");
    }

    [Test]
    public async Task Clearing_every_linked_action_unlinks_them_all()
    {
        var exchangeService = new Mock<IExchangeService>();
        var controller = new ExchangesConfigController(Mock.Of<IExchangeSettingsModelFactory>(), exchangeService.Object,
            Mock.Of<IReturnRequestService>(), Mock.Of<IOrderService>(), Mock.Of<IReturnRequestOverrideModelFactory>());

        //a multi-select with nothing selected posts no value, which binds as null
        var result = await controller.AddLinkedActionsPopup(new ReasonRulesActionsModel { ReturnReasonId = 3, StoreId = 1, LinkedActionsIds = null! });

        result.Should().BeOfType<ViewResult>();
        exchangeService.Verify(service => service.LinkReasonToActionsAsync(3, 1, It.Is<IList<int>>(ids => ids.Count == 0)), Times.Once);
    }
}
