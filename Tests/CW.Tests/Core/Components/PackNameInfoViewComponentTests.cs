using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Components;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using NUnit.Framework;

namespace CW.Tests.Core.Components;

/// <summary>
/// The info strip's subsidy button: it opens the subsidy flyout, so it shows only while the store has subsidies on
/// </summary>
[TestFixture]
public class PackNameInfoViewComponentTests
{
    private Mock<ICWFeatureService> _featureService = null!;
    private PackNameInfoViewComponent _component = null!;

    [SetUp]
    public void SetUp()
    {
        var customer = new Customer { Id = 7 };
        var packName = new PackName { Id = 200, Name = "Steve Gates", PackNameListId = 10 };

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(customer);

        var packNameContext = new Mock<IPackNameContext>();
        packNameContext.Setup(context => context.GetCurrentPackNameAsync(customer)).ReturnsAsync(packName);

        //a subsidy in its ordering period, and ordering closed so the shipping address is not asked
        var orderingService = new Mock<ISubsidyOrderingService>();
        orderingService.Setup(service => service.GetInOrderingSubsidyPackNamesByPackNameIdAsync(packName.Id))
            .ReturnsAsync(new List<SubsidyPackName> { new() { Id = 1, SubsidyId = 1, PackNameId = packName.Id } });

        _featureService = new Mock<ICWFeatureService>();

        _component = new PackNameInfoViewComponent(packNameContext.Object, workContext.Object, Mock.Of<IPackNameService>(),
            Mock.Of<IBranchService>(), Mock.Of<IMultiStoreService>(), orderingService.Object, Mock.Of<IShoppingCartOverrideService>(),
            Mock.Of<ISettingService>(), _featureService.Object);
    }

    private async Task<PackNameInfoModel> InvokeAsync() =>
        (PackNameInfoModel)(await _component.InvokeAsync("content_before", null!)).Should().BeOfType<ViewViewComponentResult>().Subject.ViewData!.Model!;

    [Test]
    public async Task With_subsidies_on_the_strip_offers_the_subsidy_button()
    {
        _featureService.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, null)).ReturnsAsync(true);

        (await InvokeAsync()).HasSubsidies.Should().BeTrue();
    }

    [Test]
    public async Task With_subsidies_off_the_strip_has_no_subsidy_button()
    {
        _featureService.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, null)).ReturnsAsync(false);

        (await InvokeAsync()).HasSubsidies.Should().BeFalse();
    }
}
