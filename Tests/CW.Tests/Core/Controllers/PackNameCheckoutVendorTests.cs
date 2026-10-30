using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Controllers;
using Nop.Plugin.CW.Core.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The vendor choice at the packname checkout: nop 5.00 can require one vendor per order, and offers the choice only on
/// its own cart page, which a packname customer never reaches.
/// </summary>
[TestFixture]
public class PackNameCheckoutVendorTests
{
    private static readonly Customer Customer = new() { Id = 5 };
    private Mock<IShoppingCartOverrideService> _shoppingCartService = null!;
    private ShoppingCartSettings _shoppingCartSettings = null!;
    private PackNameCheckoutController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(Customer);
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 1 });
        _shoppingCartService = new Mock<IShoppingCartOverrideService>();
        _shoppingCartSettings = new ShoppingCartSettings { VendorEnabled = true };

        _controller = (PackNameCheckoutController)RuntimeHelpers.GetUninitializedObject(typeof(PackNameCheckoutController));
        SetField("_workContext", workContext.Object);
        SetField("_storeContext", storeContext.Object);
        SetField("_shoppingCartService", _shoppingCartService.Object);
        SetField("_shoppingCartSettings", _shoppingCartSettings);
    }

    private void SetField(string name, object value) =>
        typeof(PackNameCheckoutController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_controller, value);

    private static bool Success(IActionResult result) =>
        (bool)result.Should().BeOfType<JsonResult>().Subject.Value!.GetType().GetProperty("success")!.GetValue(((JsonResult)result).Value)!;

    [Test]
    public async Task Choosing_a_vendor_checks_out_that_vendors_lines()
    {
        var result = await _controller.ChangeVendor(3);

        Success(result).Should().BeTrue();
        _shoppingCartService.Verify(service => service.SetShoppingCartVendorAsync(Customer, 3, 1), Times.Once);
    }

    [Test]
    public async Task With_vendors_off_for_the_cart_nothing_changes()
    {
        _shoppingCartSettings.VendorEnabled = false;

        var result = await _controller.ChangeVendor(3);

        Success(result).Should().BeFalse();
        _shoppingCartService.Verify(service => service.SetShoppingCartVendorAsync(It.IsAny<Customer>(), It.IsAny<int?>(), It.IsAny<int>()), Times.Never);
    }
}
