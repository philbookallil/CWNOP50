using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.Filters;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Models.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Filters;

/// <summary>
/// The filter that turns nop's return request edit page into CW's: after nop's <c>ReturnRequest/Edit</c> GET has built
/// its model, the filter swaps it for a <see cref="CWReturnRequestModel"/> carrying the exchange data, which CW's fork of
/// the edit view renders and posts to <c>Returns/EditExchangeRequest</c>.
/// </summary>
[TestFixture]
public class ReturnRequestAdminFilterTests
{
    private Mock<IReturnRequestExchangeModelFactory> _exchangeModelFactory = null!;
    private ReturnRequestAdminFilter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        _exchangeModelFactory = new Mock<IReturnRequestExchangeModelFactory>();
        _filter = new ReturnRequestAdminFilter(_exchangeModelFactory.Object);
    }

    /// <summary>
    /// nop's controller without its services: the filter only reads its type and its ViewData
    /// </summary>
    private static Controller NopReturnRequestController() =>
        (Controller)RuntimeHelpers.GetUninitializedObject(typeof(ReturnRequestController));

    private async Task<Controller> RunAsync(Controller controller, string method, string actionName, object model)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor { ActionName = actionName });
        var executing = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller);

        await _filter.OnActionExecutionAsync(executing, () =>
        {
            //what nop's action does: it puts its model in ViewData and returns the view
            controller.ViewData.Model = model;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), controller));
        });

        return controller;
    }

    [Test]
    public async Task The_edit_page_gets_CWs_model_with_the_exchange_data()
    {
        var nopModel = new ReturnRequestModel { Id = 7, CustomNumber = "RR-7", Quantity = 2, ReturnRequestStatusId = 10 };

        var controller = await RunAsync(NopReturnRequestController(), HttpMethods.Get, "Edit", nopModel);

        var model = controller.ViewData.Model.Should().BeOfType<CWReturnRequestModel>().Subject;
        model.Id.Should().Be(7);
        model.CustomNumber.Should().Be("RR-7");
        model.Quantity.Should().Be(2);
        _exchangeModelFactory.Verify(factory => factory.PrepareExchangeRequestModelAsync(model, nopModel), Times.Once);
    }

    [Test]
    public async Task A_post_to_the_edit_action_is_left_alone()
    {
        var nopModel = new ReturnRequestModel { Id = 7 };

        var controller = await RunAsync(NopReturnRequestController(), HttpMethods.Post, "Edit", nopModel);

        controller.ViewData.Model.Should().BeSameAs(nopModel);
        _exchangeModelFactory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Another_action_of_the_controller_is_left_alone()
    {
        var nopModel = new ReturnRequestModel { Id = 7 };

        var controller = await RunAsync(NopReturnRequestController(), HttpMethods.Get, "List", nopModel);

        controller.ViewData.Model.Should().BeSameAs(nopModel);
        _exchangeModelFactory.VerifyNoOtherCalls();
    }

    [Test]
    public void CWs_model_copies_every_property_of_nops_model()
    {
        //nop's model gains a property in an upgrade: the copy must take it too, or the edit page loses it
        var nopModel = new ReturnRequestModel();
        var settable = typeof(ReturnRequestModel).GetProperties().Where(property => property.CanWrite && property.GetSetMethod() != null).ToList();
        foreach (var property in settable)
            property.SetValue(nopModel, SampleValue(property.PropertyType, property.Name));

        var copy = new CWReturnRequestModel(nopModel);

        foreach (var property in settable)
            property.GetValue(copy).Should().BeEquivalentTo(property.GetValue(nopModel), $"{property.Name} must be copied");
    }

    private static object? SampleValue(Type type, string name) => type switch
    {
        _ when type == typeof(string) => "value of " + name,
        _ when type == typeof(int) => name.Length + 100,
        _ when type == typeof(bool) => true,
        _ when type == typeof(DateTime) => new DateTime(2026, 1, 2, 3, 4, 5),
        _ when type == typeof(Guid) => Guid.Parse("11111111-2222-3333-4444-555555555555"),
        _ when type == typeof(Dictionary<string, string>) => new Dictionary<string, string> { ["key"] = name },
        _ => type.IsValueType ? Activator.CreateInstance(type) : null
    };
}
