using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Web.Controllers;
using NUnit.Framework;

namespace CW.Tests.Core.Filters;

/// <summary>
/// The filter that keeps a packname customer out of nop's cart and checkout: a GET goes to the packname checkout, a POST
/// is refused, and nop's completed page for a packname order goes to the packname completed page.
/// </summary>
[TestFixture]
public class PackNameCheckoutRedirectFilterTests
{
    private Mock<IPackNameContext> _packNameContext = null!;
    private Mock<ICWFeatureService> _featureService = null!;
    private Mock<ICWOrderService> _orderService = null!;
    private PackNameCheckoutRedirectFilter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 5 });
        _packNameContext = new Mock<IPackNameContext>();
        _featureService = new Mock<ICWFeatureService>();
        _featureService.Setup(service => service.IsEnabledAsync(CWFeature.PackName, null)).ReturnsAsync(true);
        _orderService = new Mock<ICWOrderService>();

        _filter = new PackNameCheckoutRedirectFilter(workContext.Object, _packNameContext.Object, _featureService.Object, _orderService.Object);
    }

    private void APackNameIsSelected() =>
        _packNameContext.Setup(context => context.GetCurrentPackNameAsync(It.IsAny<Customer>())).ReturnsAsync(new PackName { Id = 9 });

    /// <summary>
    /// nop's controller without its services: the filter only reads its type
    /// </summary>
    private static Controller Nop<TController>() where TController : Controller =>
        (Controller)RuntimeHelpers.GetUninitializedObject(typeof(TController));

    /// <summary>
    /// Run the filter; the result is null when the request went on to nop's action
    /// </summary>
    private async Task<(IActionResult? result, bool reachedAction)> RunAsync(Controller controller, string method, string actionName,
        Dictionary<string, object?>? arguments = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor { ActionName = actionName });
        var executing = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), arguments ?? new Dictionary<string, object?>(), controller);
        var reachedAction = false;

        await _filter.OnActionExecutionAsync(executing, () =>
        {
            reachedAction = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), controller));
        });

        return (executing.Result, reachedAction);
    }

    [Test]
    public async Task A_packname_customer_opening_nops_cart_goes_to_the_packname_checkout()
    {
        APackNameIsSelected();

        var (result, reachedAction) = await RunAsync(Nop<ShoppingCartController>(), HttpMethods.Get, "Cart");

        reachedAction.Should().BeFalse();
        result.Should().BeOfType<RedirectToRouteResult>().Which.RouteName.Should().Be("PackNameCheckout");
    }

    [Test]
    public async Task A_packname_customer_posting_to_nops_cart_is_refused()
    {
        APackNameIsSelected();

        var (result, reachedAction) = await RunAsync(Nop<ShoppingCartController>(), HttpMethods.Post, "Cart");

        reachedAction.Should().BeFalse();
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [TestCase("Index")]
    [TestCase("OnePageCheckout")]
    [TestCase("ShippingMethod")]
    public async Task A_packname_customer_opening_any_page_of_nops_checkout_goes_to_the_packname_checkout(string action)
    {
        APackNameIsSelected();

        var (result, _) = await RunAsync(Nop<CheckoutController>(), HttpMethods.Get, action);

        result.Should().BeOfType<RedirectToRouteResult>().Which.RouteName.Should().Be("PackNameCheckout");
    }

    [Test]
    public async Task Adding_to_the_cart_is_left_to_nop()
    {
        APackNameIsSelected();

        var (_, reachedAction) = await RunAsync(Nop<ShoppingCartController>(), HttpMethods.Post, "AddProductToCart_Catalog");

        reachedAction.Should().BeTrue();
    }

    [Test]
    public async Task A_customer_with_no_packname_keeps_nops_cart()
    {
        var (_, reachedAction) = await RunAsync(Nop<ShoppingCartController>(), HttpMethods.Get, "Cart");

        reachedAction.Should().BeTrue();
    }

    [Test]
    public async Task With_the_packname_feature_off_nops_cart_is_left_alone()
    {
        APackNameIsSelected();
        _featureService.Setup(service => service.IsEnabledAsync(CWFeature.PackName, null)).ReturnsAsync(false);

        var (_, reachedAction) = await RunAsync(Nop<ShoppingCartController>(), HttpMethods.Get, "Cart");

        reachedAction.Should().BeTrue();
    }

    [Test]
    public async Task Nops_completed_page_for_a_packname_order_goes_to_the_packname_completed_page()
    {
        _orderService.Setup(service => service.IsPackNameOrderAsync(42)).ReturnsAsync(true);

        var (result, reachedAction) = await RunAsync(Nop<CheckoutController>(), HttpMethods.Get, "Completed",
            new Dictionary<string, object?> { ["orderId"] = 42 });

        reachedAction.Should().BeFalse();
        var redirect = result.Should().BeOfType<RedirectToRouteResult>().Subject;
        redirect.RouteName.Should().Be(CWCoreDefaults.PackNameCheckoutCompletedRouteName);
        redirect.RouteValues!["orderId"].Should().Be(42);
    }

    [Test]
    public async Task Nops_completed_page_for_a_plain_order_stays_nops_even_with_a_packname_selected()
    {
        APackNameIsSelected();
        _orderService.Setup(service => service.IsPackNameOrderAsync(43)).ReturnsAsync(false);

        var (_, reachedAction) = await RunAsync(Nop<CheckoutController>(), HttpMethods.Get, "Completed",
            new Dictionary<string, object?> { ["orderId"] = 43 });

        reachedAction.Should().BeTrue();
    }
}
