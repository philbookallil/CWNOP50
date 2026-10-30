using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using NUnit.Framework;

namespace CW.Tests.Core.Services.PackNames;

/// <summary>
/// Creating a packname the way a manager does: which list it goes into, the account that must exist before the
/// packname does and the address it bills to, the subsidies it starts with, and the welcome e-mail.
/// </summary>
[TestFixture]
public class PackNameRegistrationServiceTests
{
    private readonly PackName _manager = new() { Id = 4, Manager = true, CustomerId = 4, PackNameListId = 1 };
    private readonly Branch _managerBranch = new() { Id = 1, StoreId = 1, Name = "Head office" };
    private readonly Branch _branch = new() { Id = 5, StoreId = 1, Name = "Harbour", BranchCode = "HBR", Active = true };
    private readonly PackNameList _list = new() { Id = 7, BranchId = 5, StoreId = 1, IsDefault = true, Active = true };

    private Mock<IPackNameService> _packNames;
    private Mock<IMultiStoreService> _multiStore;
    private Mock<ICustomerOverrideService> _customers;
    private Mock<ICustomerRegistrationService> _registration;
    private Mock<ICustomerActivityService> _activity;
    private Mock<IAddressService> _addresses;
    private Mock<ISubsidyService> _subsidies;
    private Mock<ICWFeatureService> _features;
    private Mock<IPackNameWorkflowMessageService> _messages;
    private MultiStoreSettings _settings;
    private CustomerSettings _customerSettings;
    private AddressSettings _addressSettings;
    private List<PackName> _packNamesInList;
    private List<Subsidy> _activeSubsidies;

    private PackNameRegistrationService Build()
    {
        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(1)).ReturnsAsync(_settings);

        var dateTimeHelper = new Mock<IDateTimeHelper>();
        dateTimeHelper.Setup(helper => helper.DefaultStoreTimeZone).Returns(TimeZoneInfo.Utc);

        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        return new PackNameRegistrationService(_packNames.Object, _multiStore.Object, _customers.Object, _registration.Object,
            _activity.Object, _addresses.Object, _subsidies.Object, settingService.Object, _features.Object, dateTimeHelper.Object,
            new Mock<IEventPublisher>().Object, localization.Object, _messages.Object, _customerSettings, _addressSettings);
    }

    private PackNameRegistration Registration(PackNameCustomerRegistration customer = null, params PackNameSubsidyAssignment[] subsidies)
    {
        return new PackNameRegistration
        {
            StoreId = 1,
            Branch = _branch,
            PackNameList = _list,
            CreatedByCustomerId = 4,
            LanguageId = 1,
            Name = "Ann Lee",
            Gender = "F",
            Colour = "not a colour",
            Customer = customer,
            Subsidies = subsidies
        };
    }

    [SetUp]
    public void SetUp()
    {
        _settings = new MultiStoreSettings { AllowSubsidySelection = true, PackNamesOwnGlobalValue = true, PackNamesOwnStartAndFinishDates = true, DefaultPackNameCustomerRoleIds = new List<int>() };
        _customerSettings = new CustomerSettings { FirstNameEnabled = true, LastNameEnabled = true };
        _addressSettings = new AddressSettings();
        _packNamesInList = new List<PackName>();
        _activeSubsidies = new List<Subsidy> { new() { Id = 11, Name = "Winter", StoreId = 1 }, new() { Id = 12, Name = "Summer", StoreId = 1 } };

        _packNames = new Mock<IPackNameService>();
        _packNames.Setup(service => service.GetBranchByPackNameIdAsync(_manager.Id)).ReturnsAsync(_managerBranch);
        _packNames.Setup(service => service.GetPackNamesByListIdAsync(_list.Id)).ReturnsAsync(() => _packNamesInList);
        _packNames.Setup(service => service.InsertPackNameAsync(It.IsAny<PackName>())).Callback<PackName>(packName => packName.Id = 40).Returns(Task.CompletedTask);

        _multiStore = new Mock<IMultiStoreService>();
        _customers = new Mock<ICustomerOverrideService>();
        _customers.Setup(service => service.InsertCustomerAsync(It.IsAny<Customer>())).Callback<Customer>(customer => customer.Id = 1060).Returns(Task.CompletedTask);
        _customers.Setup(service => service.GetAllCustomerRolesAsync(true)).ReturnsAsync(new List<CustomerRole>());

        _registration = new Mock<ICustomerRegistrationService>();
        //nop's registration sets the e-mail and the username on the customer
        _registration.Setup(service => service.RegisterCustomerAsync(It.IsAny<CustomerRegistrationRequest>()))
            .Callback<CustomerRegistrationRequest>(request => { request.Customer.Email = request.Email; request.Customer.Username = request.Username; })
            .ReturnsAsync(new CustomerRegistrationResult());

        _activity = new Mock<ICustomerActivityService>();
        _addresses = new Mock<IAddressService>();
        var nextAddressId = 300;
        _addresses.Setup(service => service.InsertAddressAsync(It.IsAny<Address>())).Callback<Address>(address => address.Id = nextAddressId++).Returns(Task.CompletedTask);
        _addresses.Setup(service => service.CloneAddress(It.IsAny<Address>()))
            .Returns((Address address) => new Address { FirstName = address.FirstName, LastName = address.LastName, Email = address.Email, Company = address.Company, CountryId = address.CountryId });
        _addresses.Setup(service => service.IsAddressValidAsync(It.IsAny<Address>())).ReturnsAsync(false);

        _subsidies = new Mock<ISubsidyService>();
        _subsidies.Setup(service => service.GetActiveSubsidiesByStoreIdAsync(1)).ReturnsAsync(() => _activeSubsidies);
        _subsidies.Setup(service => service.InsertSubsidyPackNameAsync(It.IsAny<SubsidyPackName>())).Callback<SubsidyPackName>(mapping => mapping.Id = 90).Returns(Task.CompletedTask);
        _subsidies.Setup(service => service.InsertSubsidyVariationAsync(It.IsAny<SubsidyVariation>())).Callback<SubsidyVariation>(variation => variation.Id = 95).Returns(Task.CompletedTask);

        _features = new Mock<ICWFeatureService>();
        _features.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, 1)).ReturnsAsync(true);

        _messages = new Mock<IPackNameWorkflowMessageService>();
    }

    #region The list rule

    [Test]
    public async Task A_branch_of_another_store_gets_no_list()
    {
        var foreign = new Branch { Id = 9, StoreId = 2 };

        var list = await Build().ResolveTargetPackNameListAsync(_manager, foreign, _settings);

        list.Should().BeNull();
    }

    [Test]
    public async Task Without_the_limit_the_branch_default_list_comes_first()
    {
        _packNames.Setup(service => service.GetPackNameListsByBranchIdAsync(5)).ReturnsAsync(new List<PackNameList>
        {
            new() { Id = 6, BranchId = 5, IsDefault = false, Active = true },
            _list
        });

        var list = await Build().ResolveTargetPackNameListAsync(_manager, _branch, _settings);

        list.Should().BeSameAs(_list);
    }

    [Test]
    public async Task A_switched_off_list_takes_no_new_packnames()
    {
        var inactiveDefault = new PackNameList { Id = 6, BranchId = 5, IsDefault = true, Active = false };
        var active = new PackNameList { Id = 8, BranchId = 5, IsDefault = false, Active = true };

        _packNames.Setup(service => service.GetPackNameListsByBranchIdAsync(5)).ReturnsAsync(new List<PackNameList> { inactiveDefault, active });
        var withActive = await Build().ResolveTargetPackNameListAsync(_manager, _branch, _settings);

        _packNames.Setup(service => service.GetPackNameListsByBranchIdAsync(5)).ReturnsAsync(new List<PackNameList> { inactiveDefault });
        var withoutActive = await Build().ResolveTargetPackNameListAsync(_manager, _branch, _settings);

        withActive.Should().BeSameAs(active);
        withoutActive.Should().BeNull();
    }

    [Test]
    public async Task With_the_limit_only_a_list_the_manager_owns_on_that_branch_counts()
    {
        _settings.LimitAssignedBranches = true;
        _multiStore.Setup(service => service.GetPackNameListsByManagerIdAsync(_manager.Id)).ReturnsAsync(new List<PackNameList>
        {
            new() { Id = 1, BranchId = 1, IsDefault = true },
            new() { Id = 8, BranchId = 5, IsDefault = false }
        });

        var list = await Build().ResolveTargetPackNameListAsync(_manager, _branch, _settings);

        list.Id.Should().Be(8);
        _packNames.Verify(service => service.GetPackNameListsByBranchIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task An_administrator_s_list_is_the_branch_active_default_then_its_first_active_one()
    {
        var inactiveDefault = new PackNameList { Id = 6, BranchId = 5, IsDefault = true, Active = false };
        var active = new PackNameList { Id = 8, BranchId = 5, IsDefault = false, Active = true };

        _packNames.Setup(service => service.GetPackNameListsByBranchIdAsync(5)).ReturnsAsync(new List<PackNameList> { active, _list });
        var withDefault = await Build().ResolveTargetPackNameListAsync(_branch);

        _packNames.Setup(service => service.GetPackNameListsByBranchIdAsync(5)).ReturnsAsync(new List<PackNameList> { inactiveDefault, active });
        var withoutDefault = await Build().ResolveTargetPackNameListAsync(_branch);

        _packNames.Setup(service => service.GetPackNameListsByBranchIdAsync(5)).ReturnsAsync(new List<PackNameList> { inactiveDefault });
        var withoutActive = await Build().ResolveTargetPackNameListAsync(_branch);

        withDefault.Should().BeSameAs(_list);
        withoutDefault.Should().BeSameAs(active);
        withoutActive.Should().BeNull();
        (await Build().ResolveTargetPackNameListAsync(null)).Should().BeNull();
        _packNames.Verify(service => service.GetBranchByPackNameIdAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region Registering

    [Test]
    public async Task A_default_role_the_account_already_has_is_not_mapped_again()
    {
        _settings.DefaultPackNameCustomerRoleIds = new List<int> { 3, 5 };
        _customers.Setup(service => service.GetAllCustomerRolesAsync(true)).ReturnsAsync(new List<CustomerRole> { new() { Id = 3 }, new() { Id = 5 } });
        _customers.Setup(service => service.GetSignedInCustomerRoleIdsAsync(It.IsAny<Customer>(), true)).ReturnsAsync(new[] { 3 });

        await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test" }));

        _customers.Verify(service => service.AddCustomerRoleMappingAsync(It.Is<CustomerCustomerRoleMapping>(mapping => mapping.CustomerRoleId == 3)), Times.Never);
        _customers.Verify(service => service.AddCustomerRoleMappingAsync(It.Is<CustomerCustomerRoleMapping>(mapping => mapping.CustomerRoleId == 5)), Times.Once);
    }

    [Test]
    public async Task A_packname_without_a_gender_is_mixed_and_a_short_colour_is_written_in_full()
    {
        var result = await Build().RegisterAsync(Registration() with { Gender = null, Colour = "#f0a" });

        result.PackName.Gender.Should().Be("I");
        result.PackName.Colour.Should().Be("#ff00aa");
    }

    [Test]
    public async Task The_result_says_whether_the_welcome_e_mail_was_queued()
    {
        var withoutTemplate = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test" }));

        _messages.Setup(service => service.SendCustomerRegisteredByManagerMessageAsync(It.IsAny<Customer>(), It.IsAny<PackName>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<int> { 501 });
        var withTemplate = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "bob@cw.test" }));

        var withoutAccount = await Build().RegisterAsync(Registration());

        withoutTemplate.WelcomeEmailQueued.Should().BeFalse();
        withTemplate.WelcomeEmailQueued.Should().BeTrue();
        withoutAccount.WelcomeEmailQueued.Should().BeFalse();
    }

    [Test]
    public async Task A_packname_without_an_account_is_inserted_with_a_random_colour_and_no_e_mail()
    {
        var result = await Build().RegisterAsync(Registration());

        result.Success.Should().BeTrue();
        result.PackName.Id.Should().Be(40);
        result.PackName.Colour.Should().MatchRegex("^#[0-9A-F]{6}$");
        result.PackName.PackNameListId.Should().Be(7);
        result.Customer.Should().BeNull();
        _messages.Verify(service => service.SendCustomerRegisteredByManagerMessageAsync(It.IsAny<Customer>(), It.IsAny<PackName>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _activity.Verify(service => service.InsertActivityAsync("AddNewPackName", It.IsAny<string>(), It.Is<BaseEntity>(entity => entity.Id == 40)), Times.Once);
    }

    [Test]
    public async Task A_name_taken_in_the_list_since_the_checks_is_refused_before_anything_is_written()
    {
        _packNames.Setup(service => service.PackNameNameExistsAsync(_list.Id, "Ann Lee", 0)).ReturnsAsync(true);

        var result = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test" }));

        result.Success.Should().BeFalse();
        result.NameTaken.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be("Plugins.CW.Core.PackNames.PackName.Field.Name.Exists");
        _registration.Verify(service => service.RegisterCustomerAsync(It.IsAny<CustomerRegistrationRequest>()), Times.Never);
        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
    }

    [Test]
    public async Task A_refused_account_creates_nothing_and_carries_the_reason()
    {
        _registration.Setup(service => service.RegisterCustomerAsync(It.IsAny<CustomerRegistrationRequest>()))
            .ReturnsAsync(() => { var refused = new CustomerRegistrationResult(); refused.AddError("Email already exists"); return refused; });

        var result = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test" }));

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Email already exists");
        _packNames.Verify(service => service.InsertPackNameAsync(It.IsAny<PackName>()), Times.Never);
        _activity.Verify(service => service.InsertActivityAsync("AddNewPackName", It.IsAny<string>(), It.IsAny<BaseEntity>()), Times.Never);
        _messages.Verify(service => service.SendCustomerRegisteredByManagerMessageAsync(It.IsAny<Customer>(), It.IsAny<PackName>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        //the inserted customer goes with the transaction, which is not completed; it is not soft-deleted
        _customers.Verify(service => service.DeleteCustomerAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Test]
    public async Task An_account_is_linked_to_the_branch_and_the_packname_and_gets_the_welcome_e_mail()
    {
        var result = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test", FirstName = "Ann", LastName = "Lee" }));

        result.Success.Should().BeTrue();
        result.Customer.Id.Should().Be(1060);
        result.PackName.CustomerId.Should().Be(1060);
        _customers.Verify(service => service.InsertCustomerBranchAsync(It.Is<Nop.Plugin.CW.Core.Domain.Customers.CustomerBranch>(link => link.Id == 1060 && link.BranchId == 5)), Times.Once);
        _registration.Verify(service => service.RegisterCustomerAsync(It.Is<CustomerRegistrationRequest>(request => request.Email == "ann@cw.test" && request.Password.Length == 32)), Times.Once);
        _messages.Verify(service => service.SendCustomerRegisteredByManagerMessageAsync(It.Is<Customer>(customer => customer.Id == 1060), It.Is<PackName>(packName => packName.Id == 40), "Harbour", It.IsAny<IList<string>>(), 1, 1), Times.Once);
    }

    [Test]
    public async Task The_store_default_roles_go_on_the_account()
    {
        _settings.DefaultPackNameCustomerRoleIds = new List<int> { 5, 99 };
        _customers.Setup(service => service.GetAllCustomerRolesAsync(true)).ReturnsAsync(new List<CustomerRole> { new() { Id = 5 }, new() { Id = 6 } });

        await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test" }));

        _customers.Verify(service => service.AddCustomerRoleMappingAsync(It.Is<CustomerCustomerRoleMapping>(mapping => mapping.CustomerId == 1060 && mapping.CustomerRoleId == 5)), Times.Once);
        _customers.Verify(service => service.AddCustomerRoleMappingAsync(It.Is<CustomerCustomerRoleMapping>(mapping => mapping.CustomerRoleId == 99)), Times.Never);
    }

    [Test]
    public async Task The_account_gets_its_own_copy_of_the_packname_address_as_billing_and_shipping()
    {
        var address = new Address { FirstName = "Ann", LastName = "Lee", Email = "ann@cw.test", CountryId = 0 };
        var registration = Registration(new PackNameCustomerRegistration { Email = "ann@cw.test" }) with { Address = address };

        var result = await Build().RegisterAsync(registration);

        address.Id.Should().Be(300);
        address.CountryId.Should().BeNull();
        result.Customer.BillingAddressId.Should().Be(301);
        result.Customer.ShippingAddressId.Should().Be(301);
        _packNames.Verify(service => service.InsertPackNameAddressAsync(It.Is<PackName>(packName => packName.Id == 40), address), Times.Once);
        _customers.Verify(service => service.InsertCustomerAddressAsync(It.Is<Customer>(customer => customer.Id == 1060), It.Is<Address>(copy => copy.Id == 301 && copy.FirstName == "Ann")), Times.Once);
        _customers.Verify(service => service.InsertCustomerAddressAsync(It.IsAny<Customer>(), address), Times.Never);
        _addresses.Verify(service => service.InsertAddressAsync(It.IsAny<Address>()), Times.Exactly(2));
        _addresses.Verify(service => service.IsAddressValidAsync(It.IsAny<Address>()), Times.Never);
    }

    [Test]
    public async Task The_branch_name_is_the_company_of_the_account_and_of_an_address_that_has_one()
    {
        _customerSettings.CompanyEnabled = true;
        var withCompany = new Address { FirstName = "Ann" };

        _addressSettings.CompanyEnabled = true;
        var result = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test" }) with { Address = withCompany });

        var withoutCompany = new Address { FirstName = "Bob", Company = "typed in" };

        _addressSettings.CompanyEnabled = false;
        await Build().RegisterAsync(Registration() with { Address = withoutCompany });

        result.Customer.Company.Should().Be("Harbour");
        withCompany.Company.Should().Be("Harbour");
        withoutCompany.Company.Should().BeNull();
    }

    [Test]
    public async Task The_username_is_the_one_nop_registered()
    {
        var withoutUsernames = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test" }));

        _customerSettings.UsernamesEnabled = true;
        var withUsernames = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "bob@cw.test", Username = "bob.ray" }));

        withoutUsernames.Customer.Username.Should().Be("ann@cw.test");
        withUsernames.Customer.Username.Should().Be("bob.ray");
    }

    [Test]
    public async Task Without_a_packname_address_a_valid_profile_becomes_the_account_address()
    {
        _addresses.Setup(service => service.IsAddressValidAsync(It.IsAny<Address>())).ReturnsAsync(true);

        var result = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test", FirstName = "Ann", LastName = "Lee" }));

        result.Customer.BillingAddressId.Should().Be(300);
        _addresses.Verify(service => service.InsertAddressAsync(It.Is<Address>(address =>
            address.FirstName == "Ann" && address.LastName == "Lee" && address.Email == "ann@cw.test" && address.CountryId == null)), Times.Once);
        _customers.Verify(service => service.InsertCustomerAddressAsync(It.Is<Customer>(customer => customer.Id == 1060), It.Is<Address>(address => address.Id == 300)), Times.Once);
        _packNames.Verify(service => service.InsertPackNameAddressAsync(It.IsAny<PackName>(), It.IsAny<Address>()), Times.Never);
    }

    [Test]
    public async Task A_profile_that_is_not_a_valid_address_leaves_the_account_without_one()
    {
        var result = await Build().RegisterAsync(Registration(new PackNameCustomerRegistration { Email = "ann@cw.test" }));

        result.Success.Should().BeTrue();
        result.Customer.BillingAddressId.Should().BeNull();
        _addresses.Verify(service => service.InsertAddressAsync(It.IsAny<Address>()), Times.Never);
        _customers.Verify(service => service.InsertCustomerAddressAsync(It.IsAny<Customer>(), It.IsAny<Address>()), Times.Never);
    }

    [Test]
    public async Task A_packname_without_an_account_keeps_its_address_to_itself()
    {
        var address = new Address { FirstName = "Ann" };

        await Build().RegisterAsync(Registration() with { Address = address });

        _packNames.Verify(service => service.InsertPackNameAddressAsync(It.IsAny<PackName>(), address), Times.Once);
        _customers.Verify(service => service.InsertCustomerAddressAsync(It.IsAny<Customer>(), It.IsAny<Address>()), Times.Never);
    }

    [Test]
    public async Task The_account_text_fields_are_cut_to_1000_characters_as_nops_registration_cuts_them()
    {
        _customerSettings.UsernamesEnabled = true;
        _customerSettings.GenderEnabled = true;
        _customerSettings.StreetAddressEnabled = true;
        _customerSettings.StreetAddress2Enabled = true;
        _customerSettings.ZipPostalCodeEnabled = true;
        _customerSettings.CityEnabled = true;
        _customerSettings.CountyEnabled = true;
        _customerSettings.PhoneEnabled = true;
        _customerSettings.FaxEnabled = true;
        var tooLong = new string('x', 1200);

        CustomerRegistrationRequest request = null;
        _registration.Setup(service => service.RegisterCustomerAsync(It.IsAny<CustomerRegistrationRequest>()))
            .Callback<CustomerRegistrationRequest>(sent => { request = sent; sent.Customer.Email = sent.Email; sent.Customer.Username = sent.Username; })
            .ReturnsAsync(new CustomerRegistrationResult());

        Customer saved = null;
        _customers.Setup(service => service.UpdateCustomerAsync(It.IsAny<Customer>())).Callback<Customer>(customer => saved = customer).Returns(Task.CompletedTask);

        await Build().RegisterAsync(Registration(new PackNameCustomerRegistration
        {
            Email = tooLong, Username = tooLong, Gender = tooLong, FirstName = tooLong, LastName = tooLong,
            StreetAddress = tooLong, StreetAddress2 = tooLong, ZipPostalCode = tooLong, City = tooLong, County = tooLong, Phone = tooLong, Fax = tooLong
        }));

        request.Email.Should().HaveLength(1000);
        request.Username.Should().HaveLength(1000);
        new[] { saved.Gender, saved.FirstName, saved.LastName, saved.StreetAddress, saved.StreetAddress2, saved.ZipPostalCode, saved.City, saved.County, saved.Phone, saved.Fax }
            .Should().AllSatisfy(value => value.Should().HaveLength(1000));
    }

    #endregion

    #region Subsidies

    [Test]
    public async Task A_subsidy_named_twice_is_assigned_once_and_an_unknown_one_not_at_all()
    {
        var result = await Build().RegisterAsync(Registration(null,
            new PackNameSubsidyAssignment { SubsidyId = 11 },
            new PackNameSubsidyAssignment { SubsidyId = 11 },
            new PackNameSubsidyAssignment { SubsidyId = 77 }));

        result.Success.Should().BeTrue();
        _subsidies.Verify(service => service.InsertSubsidyPackNameAsync(It.Is<SubsidyPackName>(mapping => mapping.SubsidyId == 11 && mapping.PackNameId == 40 && mapping.CreatorId == 4)), Times.Once);
        _subsidies.Verify(service => service.InsertSubsidyPackNameAsync(It.IsAny<SubsidyPackName>()), Times.Once);
        _subsidies.Verify(service => service.InsertSubsidyVariationAsync(It.IsAny<SubsidyVariation>()), Times.Never);
    }

    [Test]
    public async Task Credit_and_dates_become_a_variation_with_its_history_row()
    {
        var start = new DateTime(2026, 11, 1);
        var end = new DateTime(2026, 12, 31);

        await Build().RegisterAsync(Registration(null, new PackNameSubsidyAssignment { SubsidyId = 12, Credit = 50, DurationStart = start, DurationEnd = end }));

        _subsidies.Verify(service => service.InsertSubsidyVariationAsync(It.Is<SubsidyVariation>(variation =>
            variation.SubsidyPackNameId == 90 && variation.Credit == 50 && variation.DurationStart == start && variation.DurationEnd == end)), Times.Once);
        _subsidies.Verify(service => service.InsertSubsidyVariationHistoryAsync(It.Is<SubsidyVariationHistory>(history =>
            history.SubsidyVariationId == 95 && history.Credit == 50 && history.CreatorId == 4)), Times.Once);
    }

    [Test]
    public async Task A_store_that_keeps_the_subsidy_values_gets_no_variation()
    {
        _settings.PackNamesOwnGlobalValue = false;
        _settings.PackNamesOwnStartAndFinishDates = false;

        await Build().RegisterAsync(Registration(null, new PackNameSubsidyAssignment { SubsidyId = 12, Credit = 50, DurationStart = new DateTime(2026, 11, 1), DurationEnd = new DateTime(2026, 12, 31) }));

        _subsidies.Verify(service => service.InsertSubsidyPackNameAsync(It.IsAny<SubsidyPackName>()), Times.Once);
        _subsidies.Verify(service => service.InsertSubsidyVariationAsync(It.IsAny<SubsidyVariation>()), Times.Never);
    }

    [Test]
    public async Task Without_subsidy_selection_or_with_the_feature_off_nothing_is_assigned()
    {
        _settings.AllowSubsidySelection = false;

        var withoutSelection = await Build().RegisterAsync(Registration(null, new PackNameSubsidyAssignment { SubsidyId = 11 }));

        _settings.AllowSubsidySelection = true;
        _features.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, 1)).ReturnsAsync(false);

        var withoutFeature = await Build().RegisterAsync(Registration(null, new PackNameSubsidyAssignment { SubsidyId = 11 }));

        withoutSelection.Success.Should().BeTrue();
        withoutFeature.Success.Should().BeTrue();
        _subsidies.Verify(service => service.InsertSubsidyPackNameAsync(It.IsAny<SubsidyPackName>()), Times.Never);
    }

    [Test]
    public async Task An_administrator_assigns_subsidies_the_store_does_not_let_its_managers_pick()
    {
        _settings.AllowSubsidySelection = false;

        var result = await Build().RegisterAsync(Registration(null, new PackNameSubsidyAssignment { SubsidyId = 11 }) with { ByAdministrator = true, CreatedByCustomerId = 1 });

        result.Success.Should().BeTrue();
        _subsidies.Verify(service => service.InsertSubsidyPackNameAsync(It.Is<SubsidyPackName>(mapping => mapping.SubsidyId == 11 && mapping.CreatorId == 1)), Times.Once);
    }

    [Test]
    public async Task With_the_feature_off_an_administrator_assigns_no_subsidy()
    {
        _features.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, 1)).ReturnsAsync(false);

        var result = await Build().RegisterAsync(Registration(null, new PackNameSubsidyAssignment { SubsidyId = 11 }) with { ByAdministrator = true });

        result.Success.Should().BeTrue();
        _subsidies.Verify(service => service.InsertSubsidyPackNameAsync(It.IsAny<SubsidyPackName>()), Times.Never);
    }

    #endregion
}
