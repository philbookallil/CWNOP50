using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Localization;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Orders;

/// <summary>
/// Adding to the cart for a packname whose subsidy ordering period has closed is refused with a warning naming the
/// packname. Cart warnings are shown as markup, and the name is typed by the customer, so it arrives encoded.
/// </summary>
[TestFixture]
public class AddToCartClosedPackNameWarningTests
{
    private ShoppingCartOverrideService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var packNameService = new Mock<IPackNameService>();
        packNameService.Setup(service => service.GetPackNameByIdAsync(9))
            .ReturnsAsync(new PackName { Id = 9, Name = "<b>Ann</b> & Co", Active = true });
        var subsidyOrderingService = new Mock<ISubsidyOrderingService>();
        subsidyOrderingService.Setup(service => service.PackNameCanOrderAsync(9)).ReturnsAsync(false);
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync("Plugins.CW.Core.PackName.CannotOrder"))
            .ReturnsAsync("Ordering is closed for {0}.");

        _service = (ShoppingCartOverrideService)RuntimeHelpers.GetUninitializedObject(typeof(ShoppingCartOverrideService));
        SetField("_packNameService", packNameService.Object);
        SetField("_subsidyOrderingService", subsidyOrderingService.Object);
        SetField("_localizationService", localizationService.Object);
    }

    /// <summary>
    /// Set a field declared on the service or on nop's service beneath it
    /// </summary>
    private void SetField(string name, object value)
    {
        for (var type = _service.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null)
                continue;

            field.SetValue(_service, value);
            return;
        }

        throw new MissingFieldException(name);
    }

    [Test]
    public async Task The_closed_packname_warning_carries_the_name_encoded()
    {
        var warnings = await _service.AddToCartAsync(new Customer { Id = 5 }, new Product { Id = 3 }, ShoppingCartType.ShoppingCart, 1, 9);

        warnings.Should().Equal("Ordering is closed for &lt;b&gt;Ann&lt;/b&gt; &amp; Co.");
    }
}
