using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using NUnit.Framework;

namespace CW.Tests.Core.Services.PackNames;

/// <summary>
/// What a store gets automatically: a default list for a new branch when it uses branches as packname lists, a packname
/// for a customer linked to a branch when it uses staff as packnames — and nothing when the setting, the feature or the
/// branch says otherwise. A customer and their packname are kept in the same branch: the packname follows a customer who
/// changes branch, and the customer follows a packname moved to another branch's list.
/// </summary>
[TestFixture]
public class PackNameProvisioningServiceTests
{
    private readonly Branch _branch = new() { Id = 5, StoreId = 1, Name = "Harbour", BranchCode = "HBR" };
    private readonly Customer _customer = new() { Id = 1048, FirstName = "Ann", LastName = "Lee" };

    private Mock<IBranchService> _branches;
    private Mock<IPackNameService> _packNames;
    private Mock<IMultiStoreService> _multiStore;
    private Mock<ICWFeatureService> _features;
    private MultiStoreSettings _settings;
    private List<PackNameList> _listsOfBranch;
    private Mock<ICustomerOverrideService> _customers;
    private Mock<ICustomerActivityService> _activity;

    //another branch of the same store, and a branch of another store, each with one list
    private readonly Branch _otherBranch = new() { Id = 6, StoreId = 1, Name = "Quay", BranchCode = "QAY" };
    private readonly PackNameList _otherBranchList = new() { Id = 30, BranchId = 6, StoreId = 1, Active = true, IsDefault = true, Name = "Quay" };
    private readonly PackNameList _otherStoreList = new() { Id = 31, BranchId = 9, StoreId = 2, Active = true, IsDefault = true, Name = "Elsewhere" };

    private PackNameProvisioningService Build()
    {
        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(1)).ReturnsAsync(_settings);

        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key switch
        {
            "Plugins.CW.Core.PackNames.PackName.Field.Name.Exists" => "The name {0} already exists for the branch {1}",
            "Plugins.CW.Core.ActivityLog.EditPackName" => "edited {0}",
            "Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameNeedsBranch" => "The customer's packname {0} is in a branch, so the customer needs a branch too.",
            "Plugins.CW.Core.Admin.Customers.Customer.Fields.BranchId.PackNameOtherStore" => "The customer's packname {0} belongs to another store, so it cannot follow the customer to branch {1}.",
            _ => key
        });

        return new PackNameProvisioningService(_branches.Object, _packNames.Object, _multiStore.Object,
            settingService.Object, _features.Object, _customers.Object, _activity.Object, localization.Object);
    }

    [SetUp]
    public void SetUp()
    {
        _settings = new MultiStoreSettings { UseBranchesAsPackNameList = true, UseStaffAsPackName = true, PackNameListNameFrom = (int)PackNameListNameFrom.BranchCode };
        _listsOfBranch = new List<PackNameList>();

        _branches = new Mock<IBranchService>();
        _branches.Setup(service => service.GetBranchByIdAsync(_branch.Id)).ReturnsAsync(_branch);

        _packNames = new Mock<IPackNameService>();
        _packNames.Setup(service => service.GetPackNameListsByBranchIdAsync(_branch.Id)).ReturnsAsync(() => _listsOfBranch);
        _packNames.Setup(service => service.GetPackNameByCustomerIdAsync(_customer.Id)).ReturnsAsync((PackName)null);
        _packNames.Setup(service => service.InsertPackNameListAsync(It.IsAny<PackNameList>()))
            .Callback<PackNameList>(list => { list.Id = 70; _listsOfBranch.Add(list); })
            .Returns(Task.CompletedTask);

        _multiStore = new Mock<IMultiStoreService>();

        _features = new Mock<ICWFeatureService>();
        _features.Setup(service => service.IsEnabledAsync(CWFeature.PackName, 1)).ReturnsAsync(true);

        _packNames.Setup(service => service.GetPackNameListByIdAsync(_otherBranchList.Id)).ReturnsAsync(_otherBranchList);
        _packNames.Setup(service => service.GetPackNameListByIdAsync(_otherStoreList.Id)).ReturnsAsync(_otherStoreList);
        _packNames.Setup(service => service.GetPackNameListByIdAsync(It.Is<int>(id => id == 70 || id == 71)))
            .ReturnsAsync((int id) => _listsOfBranch.FirstOrDefault(list => list.Id == id));

        _customers = new Mock<ICustomerOverrideService>();
        _activity = new Mock<ICustomerActivityService>();
    }

    private PackName CustomerPackNameIn(PackNameList list)
    {
        var packName = new PackName { Id = 400, Name = "Ann Lee", PackNameListId = list.Id, CustomerId = _customer.Id };
        _packNames.Setup(service => service.GetPackNameByCustomerIdAsync(_customer.Id)).ReturnsAsync(packName);

        return packName;
    }

    private PackNameList DefaultListOfBranch()
    {
        var list = new PackNameList { Id = 71, BranchId = _branch.Id, StoreId = 1, Active = true, IsDefault = true, Name = "Harbour" };
        _listsOfBranch.Add(list);

        return list;
    }

    #region The packname follows the customer

    [Test]
    public async Task A_customer_moving_branch_takes_their_packname_into_the_new_branchs_default_list()
    {
        DefaultListOfBranch();
        var packName = CustomerPackNameIn(_otherBranchList);

        await Build().ApplyCustomerBranchAsync(_customer, _branch.Id);

        packName.PackNameListId.Should().Be(71);
        _packNames.Verify(service => service.UpdatePackNameAsync(packName), Times.Once);
        _activity.Verify(service => service.InsertActivityAsync("EditPackName", It.IsAny<string>(), packName), Times.Once);
    }

    [Test]
    public async Task A_branch_without_an_active_list_gets_its_default_list_for_the_packname()
    {
        var packName = CustomerPackNameIn(_otherBranchList);

        await Build().ApplyCustomerBranchAsync(_customer, _branch.Id);

        _packNames.Verify(service => service.InsertPackNameListAsync(It.Is<PackNameList>(list => list.BranchId == _branch.Id && list.Active)), Times.Once);
        packName.PackNameListId.Should().Be(70);
    }

    [Test]
    public async Task A_packname_already_in_the_customers_branch_stays_where_it_is()
    {
        var list = DefaultListOfBranch();
        CustomerPackNameIn(list);

        await Build().ApplyCustomerBranchAsync(_customer, _branch.Id);

        _packNames.Verify(service => service.UpdatePackNameAsync(It.IsAny<PackName>()), Times.Never);
        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task A_packname_in_a_store_wide_list_stays_where_it_is()
    {
        DefaultListOfBranch();
        var storeWide = new PackNameList { Id = 32, BranchId = null, StoreId = 1, Active = true, Name = "Everyone" };
        _packNames.Setup(service => service.GetPackNameListByIdAsync(storeWide.Id)).ReturnsAsync(storeWide);
        var packName = CustomerPackNameIn(storeWide);

        (await Build().CheckCustomerBranchChangeAsync(_customer, _branch.Id)).Should().BeNull();
        (await Build().CheckCustomerBranchChangeAsync(_customer, 0)).Should().BeNull();
        await Build().ApplyCustomerBranchAsync(_customer, _branch.Id);

        packName.PackNameListId.Should().Be(storeWide.Id);
        _packNames.Verify(service => service.UpdatePackNameAsync(It.IsAny<PackName>()), Times.Never);
        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task A_packname_in_another_store_is_not_moved_and_the_change_is_refused()
    {
        DefaultListOfBranch();
        _features.Setup(service => service.IsEnabledAsync(CWFeature.PackName, 2)).ReturnsAsync(true);
        var packName = CustomerPackNameIn(_otherStoreList);

        var error = await Build().CheckCustomerBranchChangeAsync(_customer, _branch.Id);
        await Build().ApplyCustomerBranchAsync(_customer, _branch.Id);

        error.Should().Be("The customer's packname Ann Lee belongs to another store, so it cannot follow the customer to branch Harbour.");
        packName.PackNameListId.Should().Be(_otherStoreList.Id);
        _packNames.Verify(service => service.UpdatePackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task A_customer_with_a_packname_in_a_branch_cannot_have_their_branch_cleared()
    {
        CustomerPackNameIn(_otherBranchList);

        var error = await Build().CheckCustomerBranchChangeAsync(_customer, 0);

        error.Should().Be("The customer's packname Ann Lee is in a branch, so the customer needs a branch too.");
    }

    [Test]
    public async Task With_the_feature_off_nothing_is_checked_and_nothing_moves()
    {
        var list = DefaultListOfBranch();
        var packName = CustomerPackNameIn(_otherBranchList);
        _packNames.Setup(service => service.PackNameNameExistsAsync(list.Id, "Ann Lee", 400)).ReturnsAsync(true);
        _features.Setup(service => service.IsEnabledAsync(CWFeature.PackName, 1)).ReturnsAsync(false);

        (await Build().CheckCustomerBranchChangeAsync(_customer, _branch.Id)).Should().BeNull();
        (await Build().CheckCustomerBranchChangeAsync(_customer, 0)).Should().BeNull();
        await Build().ApplyCustomerBranchAsync(_customer, _branch.Id);

        packName.PackNameListId.Should().Be(_otherBranchList.Id);
        _packNames.Verify(service => service.UpdatePackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task A_customer_without_a_packname_gets_one_in_the_new_branch()
    {
        DefaultListOfBranch();

        await Build().ApplyCustomerBranchAsync(_customer, _branch.Id);

        _packNames.Verify(service => service.InsertPackNameAsync(It.Is<PackName>(packName => packName.PackNameListId == 71)), Times.Once);
    }

    [Test]
    public async Task The_move_is_refused_when_the_branchs_default_list_has_a_packname_of_the_same_name()
    {
        var list = DefaultListOfBranch();
        CustomerPackNameIn(_otherBranchList);
        _packNames.Setup(service => service.PackNameNameExistsAsync(list.Id, "Ann Lee", 400)).ReturnsAsync(true);

        var error = await Build().CheckCustomerBranchChangeAsync(_customer, _branch.Id);

        error.Should().Be("The name Ann Lee already exists for the branch Harbour");
    }

    [Test]
    public async Task The_move_is_allowed_when_the_name_is_free_or_the_branch_has_no_active_list()
    {
        CustomerPackNameIn(_otherBranchList);

        (await Build().CheckCustomerBranchChangeAsync(_customer, _branch.Id)).Should().BeNull();

        DefaultListOfBranch();

        (await Build().CheckCustomerBranchChangeAsync(_customer, _branch.Id)).Should().BeNull();
    }

    #endregion

    #region The customer follows the packname

    [Test]
    public async Task A_packname_moved_to_another_branch_takes_its_customer_there()
    {
        var customerBranch = new CustomerBranch { Id = _customer.Id, BranchId = _branch.Id };
        _customers.Setup(service => service.GetCustomerBranchByIdAsync(_customer.Id)).ReturnsAsync(customerBranch);

        await Build().SetCustomerBranchFromPackNameAsync(new PackName { Id = 400, PackNameListId = _otherBranchList.Id, CustomerId = _customer.Id });

        customerBranch.BranchId.Should().Be(_otherBranch.Id);
        _customers.Verify(service => service.UpdateCustomerBranchAsync(customerBranch), Times.Once);
    }

    [Test]
    public async Task A_customer_with_no_branch_row_gets_one()
    {
        _customers.Setup(service => service.GetCustomerBranchByIdAsync(_customer.Id)).ReturnsAsync((CustomerBranch)null);

        await Build().SetCustomerBranchFromPackNameAsync(new PackName { Id = 400, PackNameListId = _otherBranchList.Id, CustomerId = _customer.Id });

        _customers.Verify(service => service.InsertCustomerBranchAsync(It.Is<CustomerBranch>(row => row.Id == _customer.Id && row.BranchId == _otherBranch.Id)), Times.Once);
    }

    [Test]
    public async Task Nothing_changes_for_a_packname_without_a_customer_or_a_customer_already_in_the_branch()
    {
        _customers.Setup(service => service.GetCustomerBranchByIdAsync(_customer.Id))
            .ReturnsAsync(new CustomerBranch { Id = _customer.Id, BranchId = _otherBranch.Id });

        await Build().SetCustomerBranchFromPackNameAsync(new PackName { Id = 401, PackNameListId = _otherBranchList.Id });
        await Build().SetCustomerBranchFromPackNameAsync(new PackName { Id = 400, PackNameListId = _otherBranchList.Id, CustomerId = _customer.Id });

        _customers.Verify(service => service.UpdateCustomerBranchAsync(It.IsAny<CustomerBranch>()), Times.Never);
        _customers.Verify(service => service.InsertCustomerBranchAsync(It.IsAny<CustomerBranch>()), Times.Never);
    }

    [Test]
    public async Task A_list_moved_to_another_branch_takes_its_packnames_customers_there()
    {
        var moved = new PackNameList { Id = 30, BranchId = _otherBranch.Id, StoreId = 1, Active = true };
        var customerBranch = new CustomerBranch { Id = _customer.Id, BranchId = _branch.Id };
        _customers.Setup(service => service.GetCustomerBranchByIdAsync(_customer.Id)).ReturnsAsync(customerBranch);
        _packNames.Setup(service => service.GetPackNamesByListIdAsync(moved.Id)).ReturnsAsync(new List<PackName>
        {
            new() { Id = 400, PackNameListId = moved.Id, CustomerId = _customer.Id },
            new() { Id = 401, PackNameListId = moved.Id }
        });

        await Build().SetCustomerBranchesFromPackNameListAsync(moved);

        customerBranch.BranchId.Should().Be(_otherBranch.Id);
        _customers.Verify(service => service.UpdateCustomerBranchAsync(customerBranch), Times.Once);
    }

    [Test]
    public async Task A_store_wide_list_leaves_its_customers_where_they_are()
    {
        await Build().SetCustomerBranchesFromPackNameListAsync(new PackNameList { Id = 32, BranchId = null, StoreId = 1 });

        _packNames.Verify(service => service.GetPackNamesByListIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task A_deleted_packname_does_not_move_its_customer()
    {
        await Build().SetCustomerBranchFromPackNameAsync(new PackName { Id = 400, PackNameListId = _otherBranchList.Id, CustomerId = _customer.Id, Deleted = true });

        _customers.Verify(service => service.GetCustomerBranchByIdAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    [Test]
    public async Task A_new_branch_gets_its_default_list_named_from_the_branch_code()
    {
        await Build().ProvisionNewBranchAsync(_branch);

        _packNames.Verify(service => service.InsertPackNameListAsync(It.Is<PackNameList>(list =>
            list.BranchId == 5 && list.StoreId == 1 && list.IsDefault && list.Active && list.Name == "HBR")), Times.Once);
    }

    [Test]
    public async Task The_list_takes_the_branch_name_when_the_store_says_so()
    {
        _settings.PackNameListNameFrom = (int)PackNameListNameFrom.Name;

        await Build().ProvisionNewBranchAsync(_branch);

        _packNames.Verify(service => service.InsertPackNameListAsync(It.Is<PackNameList>(list => list.Name == "Harbour")), Times.Once);
    }

    [Test]
    public async Task With_the_list_setting_off_a_new_branch_gets_nothing()
    {
        _settings.UseBranchesAsPackNameList = false;

        await Build().ProvisionNewBranchAsync(_branch);

        _packNames.Verify(service => service.InsertPackNameListAsync(It.IsAny<PackNameList>()), Times.Never);
    }

    [Test]
    public async Task A_branch_that_already_has_a_list_gets_no_second_one()
    {
        var existing = new PackNameList { Id = 7, BranchId = 5, IsDefault = false, Active = true };
        _listsOfBranch.Add(existing);

        var list = await Build().EnsureDefaultPackNameListAsync(_branch, PackNameListNameFrom.BranchCode);

        list.Should().BeSameAs(existing);
        _packNames.Verify(service => service.InsertPackNameListAsync(It.IsAny<PackNameList>()), Times.Never);
    }

    [Test]
    public async Task A_branch_whose_lists_are_all_switched_off_gets_a_new_active_one()
    {
        _listsOfBranch.Add(new PackNameList { Id = 7, BranchId = 5, IsDefault = true, Active = false });

        var list = await Build().EnsureDefaultPackNameListAsync(_branch, PackNameListNameFrom.BranchCode);

        list.Id.Should().NotBe(7);
        //the switched-off list stays the branch's one default
        _packNames.Verify(service => service.InsertPackNameListAsync(It.Is<PackNameList>(created => created.BranchId == 5 && created.Active && !created.IsDefault)), Times.Once);
    }

    [Test]
    public async Task A_linked_customer_gets_a_packname_in_the_default_list()
    {
        _listsOfBranch.Add(new PackNameList { Id = 6, BranchId = 5, IsDefault = false, Active = true });
        _listsOfBranch.Add(new PackNameList { Id = 7, BranchId = 5, IsDefault = true, Active = true });

        await Build().ProvisionCustomerBranchAsync(_customer, _branch.Id);

        _packNames.Verify(service => service.InsertPackNameAsync(It.Is<PackName>(packName =>
            packName.PackNameListId == 7 && packName.CustomerId == 1048 && packName.Name == "Ann Lee"
            && packName.PrintName == "Ann" && packName.Gender == "I" && packName.Active && !packName.Group
            && packName.Colour.StartsWith("#") && packName.Colour.Length == 7)), Times.Once);
    }

    [Test]
    public async Task A_linked_customer_on_a_branch_without_a_list_gets_the_list_too()
    {
        await Build().ProvisionCustomerBranchAsync(_customer, _branch.Id);

        _packNames.Verify(service => service.InsertPackNameListAsync(It.Is<PackNameList>(list => list.BranchId == 5 && list.IsDefault)), Times.Once);
        _packNames.Verify(service => service.InsertPackNameAsync(It.Is<PackName>(packName => packName.PackNameListId == 70)), Times.Once);
    }

    [Test]
    public async Task A_customer_who_already_has_a_packname_gets_no_second_one()
    {
        _packNames.Setup(service => service.GetPackNameByCustomerIdAsync(_customer.Id)).ReturnsAsync(new PackName { Id = 28 });

        await Build().ProvisionCustomerBranchAsync(_customer, _branch.Id);

        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
        _packNames.Verify(service => service.InsertPackNameListAsync(It.IsAny<PackNameList>()), Times.Never);
    }

    [Test]
    public async Task With_the_staff_setting_off_a_linked_customer_gets_nothing()
    {
        _settings.UseStaffAsPackName = false;

        await Build().ProvisionCustomerBranchAsync(_customer, _branch.Id);

        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
        _packNames.Verify(service => service.InsertPackNameListAsync(It.IsAny<PackNameList>()), Times.Never);
    }

    [Test]
    public async Task With_the_packname_feature_off_nothing_is_created()
    {
        _features.Setup(service => service.IsEnabledAsync(CWFeature.PackName, 1)).ReturnsAsync(false);

        await Build().ProvisionNewBranchAsync(_branch);
        await Build().ProvisionCustomerBranchAsync(_customer, _branch.Id);

        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
        _packNames.Verify(service => service.InsertPackNameListAsync(It.IsAny<PackNameList>()), Times.Never);
    }

    [Test]
    public async Task A_deleted_branch_provisions_nothing()
    {
        _branches.Setup(service => service.GetBranchByIdAsync(_branch.Id)).ReturnsAsync(new Branch { Id = 5, StoreId = 1, Deleted = true });

        await Build().ProvisionCustomerBranchAsync(_customer, _branch.Id);

        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task A_customer_without_a_name_gets_a_packname_named_after_the_e_mail()
    {
        var unnamed = new Customer { Id = 1050, Email = "p8.staff1@cw.test" };

        await Build().ProvisionCustomerBranchAsync(unnamed, _branch.Id);

        _packNames.Verify(service => service.InsertPackNameAsync(It.Is<PackName>(packName =>
            packName.CustomerId == 1050 && packName.Name == "p8.staff1@cw.test")), Times.Once);
    }

    [Test]
    public async Task The_generator_creates_one_packname_per_customer_without_one_and_counts_them()
    {
        var list = new PackNameList { Id = 7, BranchId = 5 };
        _multiStore.Setup(service => service.GetCustomersWithoutPackNameByBranchAsync(5))
            .ReturnsAsync(new List<Customer> { _customer, new() { Id = 1049, FirstName = "Bo", LastName = "Ray", Gender = "M" } });

        var created = await Build().CreatePackNamesForCustomersWithoutOneAsync(list);

        created.Should().Be(2);
        _packNames.Verify(service => service.InsertPackNameAsync(It.Is<PackName>(packName => packName.CustomerId == 1049 && packName.Gender == "M" && packName.PackNameListId == 7)), Times.Once);
        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Exactly(2));
    }
}
