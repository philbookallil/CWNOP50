using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Web.Factories;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The storefront packname edit form: it is filled from the packname's own store, and it always offers the branch the
/// packname is on
/// </summary>
[TestFixture]
public class PackNameModelFactoryTests
{
    private const int BrowsedStoreId = 1;
    private const int PackNameStoreId = 2;

    private Mock<ISettingService> _settingService = null!;
    private Mock<IMultiStoreService> _multiStoreService = null!;
    private Mock<IBranchService> _branchService = null!;
    private PackNameModelFactory _factory = null!;

    private readonly PackName _manager = new() { Id = 100, Manager = true, PackNameListId = 10 };
    private readonly PackName _packName = new() { Id = 200, PackNameListId = 30 };
    private readonly Branch _managersBranch = new() { Id = 10, Name = "Manager's branch", StoreId = PackNameStoreId, Active = true };
    private readonly Branch _packNamesBranch = new() { Id = 20, Name = "Department's branch", StoreId = PackNameStoreId, Active = true };

    [SetUp]
    public void SetUp()
    {
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 7 });

        var packNameService = new Mock<IPackNameService>();
        packNameService.Setup(service => service.GetPackNameByCustomerIdAsync(7)).ReturnsAsync(_manager);
        packNameService.Setup(service => service.GetPackNameListByIdAsync(30))
            .ReturnsAsync(new PackNameList { Id = 30, StoreId = PackNameStoreId, BranchId = _packNamesBranch.Id });

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = BrowsedStoreId });

        _settingService = new Mock<ISettingService>();
        _settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(It.IsAny<int>()))
            .ReturnsAsync(new MultiStoreSettings { LimitAssignedBranches = true });

        //the manager's own branches are those with his lists; the packname is reached through a department, in another
        _multiStoreService = new Mock<IMultiStoreService>();
        _multiStoreService.Setup(service => service.GetVisibleBranchesForManagerAsync(_manager.Id)).ReturnsAsync(new List<Branch> { _managersBranch });
        _multiStoreService.Setup(service => service.GetDepartmentsByStoreIdAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new PagedList<Department>(new List<Department>(), 0, int.MaxValue));
        _multiStoreService.Setup(service => service.GetCostCenterSelectItemsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<(int, string, bool)>());

        _branchService = new Mock<IBranchService>();
        _branchService.Setup(service => service.GetAllBranchesByStoreIdAsync(PackNameStoreId)).ReturnsAsync(new List<Branch> { _managersBranch, _packNamesBranch });
        _branchService.Setup(service => service.GetFormattedBreadCrumbAsync(It.IsAny<int>(), It.IsAny<Branch>(), It.IsAny<IList<Branch>>(), It.IsAny<string>()))
            .ReturnsAsync((int _, Branch branch, IList<Branch> _, string _) => branch.Name);

        var subsidyService = new Mock<ISubsidyService>();
        subsidyService.Setup(service => service.GetAllSubsidyPackNamesByPackNameIdAsync(It.IsAny<int>())).ReturnsAsync(new List<Nop.Plugin.CW.Core.Domain.Subsidies.SubsidyPackName>());

        _factory = new PackNameModelFactory(packNameService.Object, workContext.Object, _settingService.Object, _multiStoreService.Object,
            storeContext.Object, Mock.Of<ILocalizationService>(), _branchService.Object, new CustomerSettings(), Mock.Of<IAddressModelFactory>(),
            new AddressSettings(), Mock.Of<ICountryService>(), Mock.Of<IStateProvinceService>(), Mock.Of<ICustomerModelFactory>(),
            Mock.Of<ISubsidyOrderingService>(), subsidyService.Object, Mock.Of<ICWFeatureService>());
    }

    [Test]
    public async Task The_form_is_filled_from_the_packnames_store_not_the_one_browsed()
    {
        var model = await _factory.PreparePackNameEditModelAsync(null!, _packName);

        model.StoreId.Should().Be(PackNameStoreId);
        _settingService.Verify(service => service.LoadSettingAsync<MultiStoreSettings>(BrowsedStoreId), Times.Never);
        _branchService.Verify(service => service.GetAllBranchesByStoreIdAsync(BrowsedStoreId), Times.Never);
        _multiStoreService.Verify(service => service.GetDepartmentsByStoreIdAsync(PackNameStoreId, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }

    [Test]
    public async Task A_packname_in_a_branch_outside_the_managers_own_still_opens_on_its_branch()
    {
        var model = await _factory.PreparePackNameEditModelAsync(null!, _packName);

        model.AvailableBranches.Select(item => item.Value).Should().Contain("10").And.Contain("20");
        model.BranchId.Should().Be(_packNamesBranch.Id);
        model.BranchName.Should().Be(_packNamesBranch.Name);
    }
}
