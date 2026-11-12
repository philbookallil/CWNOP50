using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Areas.Admin.Controllers;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Web.Areas.Admin.Models.Common;
using Nop.Web.Framework.Mvc.Filters;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The admin branch pages open and change only what belongs to the branch: a branch address is reached through its
/// branch, a deleted branch is not edited, the Manage Store branch panel opens only a live branch of its store, and the
/// branch search answers the two admin areas that use it
/// </summary>
[TestFixture]
public class BranchAdminGuardsTests
{
    private const int StoreId = 2;

    private readonly Branch _branch = new() { Id = 10, StoreId = StoreId, Active = true };
    private readonly Branch _deletedBranch = new() { Id = 11, StoreId = StoreId, Deleted = true };
    private readonly Branch _otherStoreBranch = new() { Id = 12, StoreId = 3 };

    private Mock<IBranchService> _branchService = null!;
    private Mock<IAddressService> _addressService = null!;
    private Mock<IBranchModelFactory> _branchModelFactory = null!;
    private Mock<ILocalizationService> _localizationService = null!;
    private Mock<ICustomerActivityService> _customerActivityService = null!;

    [SetUp]
    public void SetUp()
    {
        _branchService = new Mock<IBranchService>();
        _branchService.Setup(service => service.GetBranchByIdAsync(_branch.Id)).ReturnsAsync(_branch);
        _branchService.Setup(service => service.GetBranchByIdAsync(_deletedBranch.Id)).ReturnsAsync(_deletedBranch);
        _branchService.Setup(service => service.GetBranchByIdAsync(_otherStoreBranch.Id)).ReturnsAsync(_otherStoreBranch);

        //address 50 belongs to the branch; address 99 is someone else's (a customer's, say)
        _branchService.Setup(service => service.GetBranchAddressAsync(_branch.Id, 50)).ReturnsAsync(new Address { Id = 50 });

        _addressService = new Mock<IAddressService>();
        _addressService.Setup(service => service.GetAddressByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => new Address { Id = id });

        _localizationService = new Mock<ILocalizationService>();
        _localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);
        _customerActivityService = new Mock<ICustomerActivityService>();

        _branchModelFactory = new Mock<IBranchModelFactory>();
        _branchModelFactory.Setup(factory => factory.PrepareBranchModelAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new BranchModel());
    }

    private BranchController BranchPage()
    {
        var controller = TestObject.Create<BranchController>();
        TestObject.Set(controller, "_branchService", _branchService.Object);
        TestObject.Set(controller, "_addressService", _addressService.Object);
        TestObject.Set(controller, "_localizationService", _localizationService.Object);
        TestObject.Set(controller, "_customerActivityService", _customerActivityService.Object);
        return controller;
    }

    private MultiStoreController ManageStore()
    {
        var controller = TestObject.Create<MultiStoreController>();
        TestObject.Set(controller, "_branchService", _branchService.Object);
        TestObject.Set(controller, "_branchModelFactory", _branchModelFactory.Object);
        return controller;
    }

    private static CWAddressModel AddressPost(int branchId, int addressId) =>
        new() { EntityId = branchId, Address = new AddressModel { Id = addressId } };

    [Test]
    public void The_branch_search_answers_the_branch_page_and_Manage_Store()
    {
        var permissions = typeof(BranchController).GetMethod(nameof(BranchController.SearchBranches))!
            .GetCustomAttributes<CheckPermissionAttribute>()
            .SelectMany(attribute => (List<string>)attribute.Arguments![1])
            .ToList();

        permissions.Should().BeEquivalentTo([CWPermissionConfigManager.ADMIN_ACCESS_BRANCH, CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE],
            "its one caller is the packname panel of Manage Store, and the branch page owns it");
    }

    [Test]
    public async Task An_address_of_another_owner_is_not_opened_on_the_branch_page()
    {
        var result = await BranchPage().AddressEdit(99, _branch.Id);

        result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("Edit");
        _addressService.Verify(service => service.GetAddressByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task An_address_of_another_owner_is_not_saved_from_the_branch_page()
    {
        var result = await BranchPage().AddressEdit(AddressPost(_branch.Id, 99), new FormCollection(null));

        result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("Edit");
        _addressService.Verify(service => service.UpdateAddressAsync(It.IsAny<Address>()), Times.Never);
    }

    [Test]
    public async Task A_deleted_branch_is_not_edited_on_the_branch_page()
    {
        (await BranchPage().AddressEdit(50, _deletedBranch.Id)).Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("List");
        (await BranchPage().AddressEdit(AddressPost(_deletedBranch.Id, 50), new FormCollection(null))).Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("List");
        (await BranchPage().AddressCreate(_deletedBranch.Id)).Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("List");
        (await BranchPage().AddressCreate(AddressPost(_deletedBranch.Id, 0), new FormCollection(null))).Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("List");

        _addressService.Verify(service => service.UpdateAddressAsync(It.IsAny<Address>()), Times.Never);
        _addressService.Verify(service => service.InsertAddressAsync(It.IsAny<Address>()), Times.Never);
    }

    [Test]
    public async Task The_Manage_Store_panel_opens_a_live_branch_of_its_store()
    {
        var result = await ManageStore().BranchDetail(StoreId, _branch.Id, null);

        result.Should().BeOfType<PartialViewResult>();
        _branchModelFactory.Verify(factory => factory.PrepareBranchModelAsync(StoreId, _branch.Id, null), Times.Once);
    }

    [TestCase(404, null, Description = "a branch that does not exist")]
    [TestCase(11, null, Description = "a deleted branch")]
    [TestCase(12, null, Description = "a branch of another store")]
    [TestCase(null, 404, Description = "a new branch under a parent that does not exist")]
    [TestCase(null, 11, Description = "a new branch under a deleted parent")]
    [TestCase(null, 12, Description = "a new branch under a parent of another store")]
    public async Task The_Manage_Store_panel_does_not_open_anything_else(int? branchId, int? parentBranchId)
    {
        var result = await ManageStore().BranchDetail(StoreId, branchId, parentBranchId);

        result.Should().BeOfType<NotFoundResult>();
        _branchModelFactory.Verify(factory => factory.PrepareBranchModelAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }
    [Test]
    public async Task Deleting_a_branch_address_is_logged_against_the_branch()
    {
        _branchService.Setup(service => service.RemoveBranchAddressAsync(_branch, It.IsAny<Address>())).ReturnsAsync(true);

        await BranchPage().AddressDelete(50, _branch.Id);

        _addressService.Verify(service => service.DeleteAddressAsync(It.Is<Address>(address => address.Id == 50)), Times.Once);
        _customerActivityService.Verify(service => service.InsertActivityAsync("EditBranch",
            "Plugins.CW.Core.ActivityLog.DeleteBranchAddress", _branch), Times.Once);
    }

    [Test]
    public async Task Setting_the_default_branch_address_is_logged_against_the_branch()
    {
        await BranchPage().AddressSetDefault(_branch.Id, 50);

        _branchService.Verify(service => service.SetDefaultBranchAddressAsync(_branch, It.Is<Address>(address => address.Id == 50)), Times.Once);
        _customerActivityService.Verify(service => service.InsertActivityAsync("EditBranch",
            "Plugins.CW.Core.ActivityLog.SetDefaultBranchAddress", _branch), Times.Once);
    }

    [Test]
    public async Task Another_owners_address_is_neither_deleted_nor_logged()
    {
        await BranchPage().AddressDelete(99, _branch.Id);

        _addressService.Verify(service => service.DeleteAddressAsync(It.IsAny<Address>()), Times.Never);
        _customerActivityService.Verify(service => service.InsertActivityAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Nop.Core.BaseEntity>()), Times.Never);
    }
}
