using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Web.Factories;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The subsidy bar: its Change top-up button follows the top-up pages' own test, and its product groups carry the global
/// credit left after the cart, as the catalogue's first render gives it (the Exhausted badge reads it)
/// </summary>
[TestFixture]
public class SubsidyCatalogModelFactoryTests
{
    private Mock<IMultiStoreService> _multiStoreService = null!;
    private Mock<ISubsidyCatalogCartService> _cartService = null!;
    private SubsidyCatalogModelFactory _factory = null!;

    private readonly PackName _manager = new() { Id = 100, Manager = true, PackNameListId = 10 };
    private readonly PackName _employee = new() { Id = 200, PackNameListId = 10 };

    [SetUp]
    public void SetUp()
    {
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 7 });
        workContext.Setup(context => context.GetWorkingCurrencyAsync()).ReturnsAsync(new Currency());
        workContext.Setup(context => context.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 1 });

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 1 });

        var packNameService = new Mock<IPackNameService>();
        packNameService.Setup(service => service.GetPackNameByCustomerIdAsync(7)).ReturnsAsync(_manager);
        packNameService.Setup(service => service.GetPackNameListByIdAsync(10)).ReturnsAsync(new PackNameList { Id = 10, StoreId = 1, BranchId = 1 });

        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(1))
            .ReturnsAsync(new MultiStoreSettings { AllowEditSubsidyTopup = true, AllowManagersToAddPackNames = true });

        _multiStoreService = new Mock<IMultiStoreService>();

        //60 of global credit (50 plus a top-up of 10), and one product group with a product
        var credit = new PackNameCredit();
        var subsidyCredit = new SubsidyCredit { SubsidyId = 1, Credit = 50, CreditTopup = 10, AvailableCredit = 50, AvailableCreditTopup = 10 };
        subsidyCredit.ProductGroupCredits.Add(new ProductGroupCredit { ProductGroupId = 3, ProductIds = new List<int> { 1 } });
        credit.SubsidyCredits.Add(subsidyCredit);

        var creditService = new Mock<ISubsidyCreditService>();
        creditService.Setup(service => service.GetPackNameSubsidyCreditAsync(It.IsAny<int>())).ReturnsAsync(credit);

        var subsidyService = new Mock<ISubsidyService>();
        subsidyService.Setup(service => service.GetProductGroupsByIdsAsync(It.IsAny<IList<int>>())).ReturnsAsync(new List<ProductGroup> { new() { Id = 3, Name = "Shirts" } });

        _cartService = new Mock<ISubsidyCatalogCartService>();
        _cartService.Setup(service => service.GetPackNameSubsidyCartUsageAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new List<SubsidyCartUsageItem>());

        _factory = new SubsidyCatalogModelFactory(creditService.Object, Mock.Of<ISubsidyOrderingService>(), subsidyService.Object, _cartService.Object,
            storeContext.Object, Mock.Of<IProductModelFactory>(), workContext.Object, Mock.Of<IPriceFormatter>(), new CatalogSettings(),
            Mock.Of<ILocalizationService>(), packNameService.Object, settingService.Object, Mock.Of<IBranchService>(), _multiStoreService.Object,
            Mock.Of<ISubsidyReportingService>());
    }

    [Test]
    public async Task The_top_up_button_shows_for_a_packname_the_manager_controls()
    {
        _multiStoreService.Setup(service => service.IsPackNameOwnedByManagerId(_manager.Id, _employee.Id)).ReturnsAsync(true);

        var model = await _factory.PrepareSubsidyBarModelAsync(_employee, flyout: false);

        model.CanEditSubsidyTopup.Should().BeTrue();
    }

    [Test]
    public async Task The_top_up_button_is_hidden_for_a_packname_the_top_up_pages_would_refuse()
    {
        //the manager's own packname, outside every list and department he manages
        _multiStoreService.Setup(service => service.IsPackNameOwnedByManagerId(_manager.Id, _manager.Id)).ReturnsAsync(false);

        var model = await _factory.PrepareSubsidyBarModelAsync(_manager, flyout: false);

        model.CanEditSubsidyTopup.Should().BeFalse();
    }

    [Test]
    public async Task The_top_up_button_is_never_in_the_flyout()
    {
        _multiStoreService.Setup(service => service.IsPackNameOwnedByManagerId(_manager.Id, _employee.Id)).ReturnsAsync(true);

        var model = await _factory.PrepareSubsidyBarModelAsync(_employee, flyout: true);

        model.CanEditSubsidyTopup.Should().BeFalse();
    }

    [Test]
    public async Task The_bars_product_groups_carry_the_global_credit_left()
    {
        //the headers refreshed after a cart change are built from these
        var model = await _factory.PrepareSubsidyBarModelAsync(_employee, flyout: false);

        model.ProductGroups.Should().ContainSingle().Which.RemainGlobalCredit.Should().Be(60);
    }

    [Test]
    public async Task Global_credit_the_cart_uses_up_is_not_left_for_the_groups()
    {
        //the cart spends all 60 of the global credit: a group whose own units are used up is then exhausted
        _cartService.Setup(service => service.GetPackNameSubsidyCartUsageAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<SubsidyCartUsageItem> { new() { ProductGroupId = null, Credit = 50, CreditTopup = 10 } });

        var model = await _factory.PrepareSubsidyBarModelAsync(_employee, flyout: false);

        model.ProductGroups.Should().ContainSingle().Which.RemainGlobalCredit.Should().Be(0);
    }
}
