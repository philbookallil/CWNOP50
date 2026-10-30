using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Controllers;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames.Import;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Attributes;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The storefront packname actions: choosing a packname returns the manager to the page they were on, and the add form
/// opens on a list the manager owns
/// </summary>
[TestFixture]
public class PackNameControllerTests
{
    private const int StoreId = 2;

    private Mock<IPackNameService> _packNameService = null!;
    private Mock<IPackNameModelFactory> _modelFactory = null!;
    private Mock<IMultiStoreService> _multiStoreService = null!;
    private PackNameController _controller = null!;

    private readonly Customer _customer = new() { Id = 7 };
    private readonly PackName _manager = new() { Id = 100, Manager = true, PackNameListId = 10 };

    [SetUp]
    public void SetUp()
    {
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(_customer);

        _packNameService = new Mock<IPackNameService>();
        _packNameService.Setup(service => service.GetPackNameByCustomerIdAsync(_customer.Id)).ReturnsAsync(_manager);
        _packNameService.Setup(service => service.GetBranchByPackNameIdAsync(_manager.Id)).ReturnsAsync(new Branch { Id = 1, StoreId = StoreId });
        _packNameService.Setup(service => service.GetPackNameListByIdAsync(10)).ReturnsAsync(new PackNameList { Id = 10, StoreId = StoreId, BranchId = 1 });
        _packNameService.Setup(service => service.GetPackNameListByIdAsync(40)).ReturnsAsync(new PackNameList { Id = 40, StoreId = StoreId, BranchId = 4 });

        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(StoreId))
            .ReturnsAsync(new MultiStoreSettings { AllowManagersToAddPackNames = true });

        //the manager owns list 10; list 40 is one the selector offers through his departments
        _multiStoreService = new Mock<IMultiStoreService>();
        _multiStoreService.Setup(service => service.GetPackNameListIdsByManagerIdAsync(_manager.Id, It.IsAny<bool>())).ReturnsAsync(new List<int> { 10 });

        _modelFactory = new Mock<IPackNameModelFactory>();
        _modelFactory.Setup(factory => factory.PreparePackNameRegisterModelAsync(It.IsAny<PackNameRegisterModel>(), It.IsAny<PackName>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((PackNameRegisterModel model, PackName _, int _, int _, string _, string _) => model);

        _controller = new PackNameController(_modelFactory.Object, workContext.Object, _packNameService.Object, settingService.Object,
            Mock.Of<IBranchService>(), Mock.Of<ILocalizationService>(), Mock.Of<ICustomerOverrideService>(), _multiStoreService.Object,
            new CustomerSettings(), Mock.Of<IAttributeService<CustomerAttribute, CustomerAttributeValue>>(),
            Mock.Of<IAttributeParser<CustomerAttribute, CustomerAttributeValue>>(), Mock.Of<IAttributeParser<AddressAttribute, AddressAttributeValue>>(),
            Mock.Of<IPackNameContext>(), Mock.Of<ICustomerActivityService>(), Mock.Of<ISubsidyService>(), Mock.Of<ICWFeatureService>(),
            Mock.Of<IPackNameLandingService>(), Mock.Of<IPackNameRegistrationService>(), Mock.Of<IPackNameImportService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var url = new Mock<IUrlHelper>();
        url.Setup(helper => helper.IsLocalUrl(It.IsAny<string>())).Returns((string value) => value.StartsWith('/') && !value.StartsWith("//"));
        _controller.Url = url.Object;
    }

    [Test]
    public async Task Choosing_a_packname_returns_to_the_page_the_manager_was_on()
    {
        var result = await _controller.SelectPackName(0, 0, "/category/shirts");

        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be("/category/shirts");
        _packNameService.Verify(service => service.UnsetPackNameAsync(_customer), Times.Once);
    }

    [Test]
    public async Task A_return_address_on_another_site_goes_to_the_home_page()
    {
        var result = await _controller.SelectPackName(0, 0, "https://example.com/");

        result.Should().BeOfType<RedirectToRouteResult>().Which.RouteName.Should().Be("Homepage");
    }

    [Test]
    public async Task The_add_form_opens_on_a_list_the_manager_owns()
    {
        var result = await _controller.AddPackName(10);

        result.Should().BeOfType<PartialViewResult>().Which.ViewName.Should().Be("_AddPackName");
        _modelFactory.Verify(factory => factory.PreparePackNameRegisterModelAsync(It.IsAny<PackNameRegisterModel>(), _manager, StoreId, 1, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task On_a_list_reached_through_a_department_the_add_form_opens_on_the_managers_own_list()
    {
        var result = await _controller.AddPackName(40);

        result.Should().BeOfType<PartialViewResult>().Which.ViewName.Should().Be("_AddPackName");
        _modelFactory.Verify(factory => factory.PreparePackNameRegisterModelAsync(It.IsAny<PackNameRegisterModel>(), _manager, StoreId, 1, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _packNameService.Verify(service => service.GetPackNameListByIdAsync(40), Times.Never);
    }
}
