using ClosedXML.Excel;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Tax;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames.Import;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Attributes;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using NUnit.Framework;
using System.Globalization;

namespace CW.Tests.Core.Services.PackNames;

/// <summary>
/// The Excel import: a template whose columns follow the settings, a file checked row by row against itself and the
/// store, and an import that writes nothing when any row fails its checks and commits the rest in batches.
/// </summary>
[TestFixture]
public class PackNameImportServiceTests
{
    private readonly PackName _manager = new() { Id = 4, Manager = true, CustomerId = 4, PackNameListId = 1 };
    private readonly Branch _harbour = new() { Id = 5, StoreId = 1, Name = "Harbour", BranchCode = "HBR", Active = true };
    private readonly Branch _city = new() { Id = 6, StoreId = 1, Name = "City", BranchCode = "CTY", Active = true };
    private readonly PackNameList _harbourList = new() { Id = 7, BranchId = 5, StoreId = 1, IsDefault = true, Name = "Harbour staff" };

    private Mock<IPackNameRegistrationService> _registration;
    private Mock<IPackNameProvisioningService> _provisioning;
    private Mock<IPackNameService> _packNames;
    private Mock<IBranchService> _branches;
    private Mock<IMultiStoreService> _multiStore;
    private Mock<ISubsidyService> _subsidies;
    private Mock<ICWFeatureService> _features;
    private Mock<ICustomerService> _customers;
    private Mock<ICustomerActivityService> _activity;
    private Mock<ILogger> _logger;
    private MultiStoreSettings _settings;
    private CustomerSettings _customerSettings;
    private AddressSettings _addressSettings;
    private TaxSettings _taxSettings;
    private List<CustomerAttribute> _customerAttributes;
    private List<CustomerAttributeValue> _customerAttributeValues;
    private List<Branch> _storeBranches;
    private List<Department> _departments;
    private List<Subsidy> _activeSubsidies;
    private List<PackName> _harbourPackNames;
    private List<StateProvince> _states;
    private List<(int Id, string Name, bool Active)> _costCenters;

    private PackNameImportService Build()
    {
        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(1)).ReturnsAsync(_settings);

        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key switch
        {
            "Common.Yes" => "Yes",
            "Common.No" => "No",
            "Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Mixed" => "Mixed",
            "Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Male" => "Male",
            "Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Female" => "Female",
            "Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Credit.Maximum" => "Credit.Maximum {ComparisonValue}",
            "Plugins.CW.Core.PackNames.Import.AddressMessage" => "Address: {0}",
            _ => key
        });

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 1 });

        var countries = new Mock<ICountryService>();
        countries.Setup(service => service.GetAllCountriesAsync(1, false)).ReturnsAsync(new List<Country> { new() { Id = 1, Name = "Australia", TwoLetterIsoCode = "AU" } });

        var states = new Mock<IStateProvinceService>();
        states.Setup(service => service.GetStateProvincesAsync(false)).ReturnsAsync(() => _states);
        states.Setup(service => service.GetStateProvincesByCountryIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync((int countryId, int languageId, bool showHidden) => _states.Where(state => state.CountryId == countryId).ToList());

        var customerAttributes = new Mock<IAttributeService<CustomerAttribute, CustomerAttributeValue>>();
        customerAttributes.Setup(service => service.GetAllAttributesAsync()).ReturnsAsync(() => _customerAttributes);
        customerAttributes.Setup(service => service.GetAttributeValuesAsync(It.IsAny<int>()))
            .ReturnsAsync((int attributeId) => _customerAttributeValues.Where(value => value.AttributeId == attributeId).ToList());

        var addressAttributes = new Mock<IAttributeService<AddressAttribute, AddressAttributeValue>>();
        addressAttributes.Setup(service => service.GetAllAttributesAsync()).ReturnsAsync(new List<AddressAttribute>());

        var customerParser = new Mock<IAttributeParser<CustomerAttribute, CustomerAttributeValue>>();
        customerParser.Setup(parser => parser.GetAttributeWarningsAsync(It.IsAny<string>())).ReturnsAsync(new List<string>());
        customerParser.Setup(parser => parser.AddAttribute(It.IsAny<string>(), It.IsAny<CustomerAttribute>(), It.IsAny<string>()))
            .Returns((string xml, CustomerAttribute attribute, string value) => $"{xml}[{attribute.Id}={value}]");

        var addressParser = new Mock<IAttributeParser<AddressAttribute, AddressAttributeValue>>();
        addressParser.Setup(parser => parser.GetAttributeWarningsAsync(It.IsAny<string>())).ReturnsAsync(new List<string>());

        return new TestablePackNameImportService(_registration.Object, _provisioning.Object, _packNames.Object, _branches.Object, _multiStore.Object,
            _subsidies.Object, _features.Object, settingService.Object, countries.Object, states.Object, _customers.Object, _activity.Object,
            customerAttributes.Object, addressAttributes.Object, customerParser.Object, addressParser.Object, localization.Object,
            _logger.Object, workContext.Object, _customerSettings, _addressSettings, _taxSettings);
    }

    /// <summary>
    /// A workbook with the given headers and rows, as the stream the service reads
    /// </summary>
    private static MemoryStream Workbook(string[] headers, params object[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("PackNames");

        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                var value = rows[row][column];

                if (value is DateTime date)
                    sheet.Cell(row + 2, column + 1).Value = date;
                else if (value is decimal number)
                    sheet.Cell(row + 2, column + 1).Value = number;
                else if (value is double serial)
                    sheet.Cell(row + 2, column + 1).Value = serial;
                else
                    sheet.Cell(row + 2, column + 1).Value = value?.ToString() ?? string.Empty;
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return stream;
    }

    private static readonly string[] _basicHeaders = ["Branch", "Name", "Group", "Gender", "Subsidy 1", "Subsidy 1 credit", "Subsidy 1 start", "Subsidy 1 end"];

    [SetUp]
    public void SetUp()
    {
        _settings = new MultiStoreSettings
        {
            AllowSubsidySelection = true,
            AllowSubsidyCountByExcel = 1,
            GenderEnabled = true,
            PackNamesOwnGlobalValue = true,
            PackNamesOwnStartAndFinishDates = true,
            PackNameListNameFrom = (int)PackNameListNameFrom.BranchCode
        };
        _customerSettings = new CustomerSettings { FirstNameEnabled = true, LastNameEnabled = true };
        _addressSettings = new AddressSettings();
        _taxSettings = new TaxSettings();
        _customerAttributes = new List<CustomerAttribute>();
        _customerAttributeValues = new List<CustomerAttributeValue>();
        _storeBranches = new List<Branch> { _harbour, _city };
        _departments = new List<Department>();
        _activeSubsidies = new List<Subsidy> { new() { Id = 11, Name = "Winter", StoreId = 1 } };
        _harbourPackNames = new List<PackName>();
        _states = new List<StateProvince>();
        _costCenters = new List<(int Id, string Name, bool Active)>();

        _registration = new Mock<IPackNameRegistrationService>();
        _registration.Setup(service => service.ResolveTargetPackNameListAsync(_manager, _harbour, _settings)).ReturnsAsync(_harbourList);
        _registration.Setup(service => service.ResolveTargetPackNameListAsync(_manager, _city, _settings)).ReturnsAsync((PackNameList)null);
        _registration.Setup(service => service.RegisterAsync(It.IsAny<PackNameRegistration>()))
            .ReturnsAsync((PackNameRegistration registration) => new PackNameRegistrationResult { PackName = new PackName { Id = 40, Name = registration.Name } });

        _provisioning = new Mock<IPackNameProvisioningService>();
        _provisioning.Setup(service => service.EnsureDefaultPackNameListAsync(_city, PackNameListNameFrom.BranchCode)).ReturnsAsync(new PackNameList { Id = 70, BranchId = 6, Name = "CTY" });

        _packNames = new Mock<IPackNameService>();
        _packNames.Setup(service => service.GetPackNamesByListIdAsync(_harbourList.Id)).ReturnsAsync(() => _harbourPackNames);

        _branches = new Mock<IBranchService>();
        _branches.Setup(service => service.GetAllBranchesByStoreIdAsync(1)).ReturnsAsync(() => _storeBranches);

        _multiStore = new Mock<IMultiStoreService>();
        _multiStore.Setup(service => service.GetDepartmentsByStoreIdAsync(1, null, 0, int.MaxValue)).ReturnsAsync(() => new PagedList<Department>(_departments, 0, int.MaxValue));
        _multiStore.Setup(service => service.GetCostCenterSelectItemsAsync(1)).ReturnsAsync(() => _costCenters);

        _subsidies = new Mock<ISubsidyService>();
        _subsidies.Setup(service => service.GetActiveSubsidiesByStoreIdAsync(1)).ReturnsAsync(() => _activeSubsidies);

        _features = new Mock<ICWFeatureService>();
        _features.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, 1)).ReturnsAsync(true);

        _customers = new Mock<ICustomerService>();
        _customers.Setup(service => service.GetCustomerByEmailAsync(It.IsAny<string>())).ReturnsAsync((Customer)null);

        _activity = new Mock<ICustomerActivityService>();
        _logger = new Mock<ILogger>();
    }

    #region Layout and template

    [Test]
    public async Task The_columns_follow_the_settings()
    {
        _settings.AllowToAddCustomerToPackName = false;
        _settings.AllowHomeDelivery = false;
        _settings.EmployeeNumberEnabled = true;
        _settings.AllowSubsidyCountByExcel = 25;

        var layout = await Build().BuildLayoutAsync(_manager, 1);

        layout.Columns.Select(column => column.Header).Should().Equal(
            "Branch", "Name", "Group", "Employee number", "Gender", "Colour",
            "Subsidy 1", "Subsidy 1 credit", "Subsidy 1 start", "Subsidy 1 end");
        layout.Column("Branch").Options.Select(option => option.Text).Should().Equal("City", "Harbour");
    }

    [Test]
    public async Task The_subsidy_groups_are_the_setting_capped_at_ten_and_at_the_active_subsidies()
    {
        _activeSubsidies.AddRange(Enumerable.Range(12, 14).Select(id => new Subsidy { Id = id, Name = $"S{id}", StoreId = 1 }));

        _settings.AllowSubsidyCountByExcel = 2;
        var two = await Build().BuildLayoutAsync(_manager, 1);

        _settings.AllowSubsidyCountByExcel = 25;
        var ten = await Build().BuildLayoutAsync(_manager, 1);

        _activeSubsidies.RemoveRange(3, _activeSubsidies.Count - 3);
        var three = await Build().BuildLayoutAsync(_manager, 1);

        two.SubsidyGroups.Should().HaveCount(2);
        ten.SubsidyGroups.Should().HaveCount(10);
        three.SubsidyGroups.Should().HaveCount(3);
    }

    [Test]
    public async Task Under_the_limit_only_branches_with_a_list_of_the_manager_are_offered()
    {
        _settings.LimitAssignedBranches = true;
        _multiStore.Setup(service => service.GetVisibleBranchesForManagerAsync(_manager.Id)).ReturnsAsync(new List<Branch> { _harbour, _city });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);
        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["City", "Ann Lee", "No", "Female"]), 1);

        layout.Column("Branch").Options.Select(option => option.Text).Should().Equal("Harbour");
        result.RowErrors.Should().ContainSingle().Which.Messages.Should().ContainSingle().Which.Should().Contain("Branch.DoesntExist");
        _provisioning.Verify(provisioning => provisioning.EnsureDefaultPackNameListAsync(It.IsAny<Branch>(), It.IsAny<PackNameListNameFrom>()), Times.Never);
    }

    [Test]
    public async Task Cost_centres_sharing_a_name_are_told_apart()
    {
        _costCenters.Add((1, "Admin", true));
        _costCenters.Add((2, "Admin", true));
        _costCenters.Add((3, "Stores", true));
        _multiStore.Setup(service => service.GetCostCenterByIdAsync(1)).ReturnsAsync(new CostCenter { Id = 1, BranchId = 5 });
        _multiStore.Setup(service => service.GetCostCenterByIdAsync(2)).ReturnsAsync(new CostCenter { Id = 2 });
        _branches.Setup(service => service.GetBranchByIdAsync(5)).ReturnsAsync(_harbour);

        var layout = await Build().BuildLayoutAsync(_manager, 1);

        layout.Column(PackNameImportColumns.CostCenter).Options.Should().Equal(
            ("Admin (Harbour)", 1), ("Admin (Plugins.CW.Core.PackNames.Import.WholeStore)", 2), ("Stores", 3));
    }

    [Test]
    public async Task Departments_customers_and_addresses_add_their_columns_and_subsidies_go_with_their_switches()
    {
        _departments.Add(new Department { Id = 3, Name = "Sales", Active = true });
        _settings.AllowToAddCustomerToPackName = true;
        _settings.AllowHomeDelivery = true;
        _settings.GenderEnabled = false;
        _settings.PackNamesOwnGlobalValue = false;
        _customerSettings.GenderEnabled = true;
        _customerSettings.CountryEnabled = true;

        var layout = await Build().BuildLayoutAsync(_manager, 1);
        var headers = layout.Columns.Select(column => column.Header).ToList();

        headers.Should().ContainInOrder("Department", "Subsidy 1", "Subsidy 1 start", "Add customer", "Email", "Customer gender", "Country code", "Add address", "Address first name");
        headers.Should().NotContain("Subsidy 1 credit").And.NotContain("Gender").And.NotContain("Cost centre");
        layout.Column(PackNameImportColumns.Country).Options.Should().ContainSingle().Which.Text.Should().Be("AU");
    }

    [Test]
    public async Task With_subsidies_off_or_no_selection_the_template_has_no_subsidy_columns()
    {
        _settings.AllowSubsidySelection = false;

        var withoutSelection = await Build().BuildLayoutAsync(_manager, 1);

        _settings.AllowSubsidySelection = true;
        _features.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, 1)).ReturnsAsync(false);

        var withoutFeature = await Build().BuildLayoutAsync(_manager, 1);

        withoutSelection.SubsidyGroups.Should().BeEmpty();
        withoutFeature.SubsidyGroups.Should().BeEmpty();
        withoutFeature.Columns.Select(column => column.Header).Should().NotContain(header => header.StartsWith("Subsidy"));
    }

    [Test]
    public async Task The_template_carries_every_header_and_a_hidden_options_sheet()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var bytes = await service.GenerateTemplateAsync(layout);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("PackNames");
        var headers = Enumerable.Range(1, layout.Columns.Count).Select(column => sheet.Cell(1, column).GetString()).ToList();

        headers.Should().Equal(layout.Columns.Select(column => column.Header));
        workbook.Worksheet("Options").Visibility.Should().Be(XLWorksheetVisibility.VeryHidden);
        workbook.Worksheet("Options").Cell(1, 1).GetString().Should().Be("City");
    }

    #endregion

    #region Refused files

    [Test]
    public async Task A_file_without_the_name_column_is_refused_whole()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(["Branch", "Gender"], ["Harbour", "Female"]), 1);

        result.Success.Should().BeFalse();
        result.FileErrors.Should().ContainSingle().Which.Should().Contain("MissingColumns");
        result.RowErrors.Should().BeEmpty();
        _registration.Verify(service => service.RegisterAsync(It.IsAny<PackNameRegistration>()), Times.Never);
    }

    [Test]
    public async Task Something_that_is_not_a_workbook_is_refused()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, new MemoryStream("not an xlsx"u8.ToArray()), 1);

        result.FileErrors.Should().ContainSingle().Which.Should().Contain("InvalidFile");
    }

    [Test]
    public async Task Row_problems_are_listed_per_row_and_nothing_is_imported()
    {
        _harbourPackNames.Add(new PackName { Id = 20, Name = "Bob Ray" });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var file = Workbook(_basicHeaders,
            ["Harbour", "Ann Lee", "No", "Female", "Winter", "abc", new DateTime(2026, 12, 31), new DateTime(2026, 11, 1)],
            ["Harbour", "ann lee", "No", "Female"],
            ["Harbour", "Bob Ray", "No", ""],
            ["Nowhere", "Cy Dee", "Maybe", "Male"],
            ["Harbour", "", "No", "Male", "", "50"]);

        var result = await service.ImportAsync(layout, file, 1);

        result.Success.Should().BeFalse();
        result.RowCount.Should().Be(5);
        result.RowErrors.Select(error => error.Row).Should().Equal(2, 3, 4, 5, 6);
        result.RowErrors[0].Messages.Should().Contain(message => message.Contains("Subsidy.CreditNotValid"))
            .And.Contain(message => message.Contains("GreaterThanDurationStart"));
        result.RowErrors[1].Messages.Should().ContainSingle().Which.Should().Contain("Name.Duplicated");
        result.RowErrors[2].Messages.Should().Contain(message => message.Contains("Name.Exists")).And.Contain(message => message.Contains("Gender.Required"));
        result.RowErrors[3].Messages.Should().Contain(message => message.Contains("Branch.DoesntExist")).And.Contain(message => message.Contains("YesNo.NotValid"));
        result.RowErrors[4].Messages.Should().Contain(message => message.Contains("Name.Required")).And.Contain(message => message.Contains("Subsidy.NameRequired"));
        _registration.Verify(service => service.RegisterAsync(It.IsAny<PackNameRegistration>()), Times.Never);
    }

    [Test]
    public async Task The_first_row_the_service_refuses_stops_the_import_with_nothing_counted()
    {
        _registration.Setup(service => service.RegisterAsync(It.Is<PackNameRegistration>(registration => registration.Name == "Bob Ray")))
            .ReturnsAsync(new PackNameRegistrationResult { Errors = new List<string> { "refused" } });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female"], ["Harbour", "Bob Ray", "No", "Male"]), 1);

        result.Success.Should().BeFalse();
        result.PackNamesCreated.Should().Be(0);
        result.RowErrors.Should().ContainSingle().Which.Row.Should().Be(3);
        result.RefusedOnWrite.Should().BeTrue();
        result.HasErrorReport.Should().BeFalse();
        result.FirstRowNotImported.Should().BeNull();
        _activity.Verify(service => service.InsertActivityAsync("PublicStore.ImportPackNames", It.IsAny<string>(), null), Times.Never);
    }

    /// <summary>
    /// Rows 2 to 26: the batches are rows 2–11, 12–21 and 22–26
    /// </summary>
    private static object[][] NumberedRows(string branch, int count)
    {
        return Enumerable.Range(1, count).Select(number => new object[] { branch, $"P{number}", "No", "Female" }).ToArray();
    }

    [Test]
    public async Task A_refusal_in_a_later_batch_keeps_the_batches_before_it()
    {
        var registered = new List<string>();
        _registration.Setup(service => service.RegisterAsync(It.IsAny<PackNameRegistration>()))
            .ReturnsAsync((PackNameRegistration registration) =>
            {
                registered.Add(registration.Name);

                return registration.Name == "P15"
                    ? new PackNameRegistrationResult { Errors = new List<string> { "refused" } }
                    : new PackNameRegistrationResult { PackName = new PackName { Id = 40, Name = registration.Name } };
            });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, NumberedRows("Harbour", 25)), 1);

        result.Success.Should().BeFalse();
        result.PackNamesCreated.Should().Be(10);
        result.FirstRowNotImported.Should().Be(12);
        result.RowErrors.Should().ContainSingle().Which.Row.Should().Be(16);
        result.RefusedOnWrite.Should().BeTrue();
        result.HasErrorReport.Should().BeFalse();
        result.Lists.Should().ContainSingle().Which.Should().BeEquivalentTo(new PackNameImportListCount { Name = "Harbour › Harbour staff", Count = 10 });
        registered.Should().HaveCount(15).And.NotContain("P16");
        _activity.Verify(activity => activity.InsertActivityAsync("PublicStore.ImportPackNames", It.IsAny<string>(), null), Times.Once);
    }

    [Test]
    public async Task An_exception_in_a_later_batch_reports_what_the_batches_before_it_created()
    {
        _registration.Setup(service => service.RegisterAsync(It.Is<PackNameRegistration>(registration => registration.Name == "P12")))
            .ThrowsAsync(new InvalidOperationException("database said no"));

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, NumberedRows("Harbour", 25)), 1);

        result.Success.Should().BeFalse();
        result.PackNamesCreated.Should().Be(10);
        result.FirstRowNotImported.Should().Be(12);
        result.FileErrors.Should().ContainSingle().Which.Should().Contain("Import.Errors.Unexpected");
        result.Lists.Should().ContainSingle().Which.Count.Should().Be(10);
        _logger.Verify(logger => logger.ErrorAsync(It.Is<string>(message => message.Contains("row 13") && message.Contains("before row 12")),
            It.Is<Exception>(exception => exception.Message == "database said no"), It.IsAny<Customer>()), Times.Once);
        _activity.Verify(activity => activity.InsertActivityAsync("PublicStore.ImportPackNames", It.IsAny<string>(), null), Times.Once);
    }

    [Test]
    public async Task A_list_created_in_one_batch_is_used_by_the_next()
    {
        var registrations = new List<PackNameRegistration>();
        _registration.Setup(service => service.RegisterAsync(Capture.In(registrations)))
            .ReturnsAsync((PackNameRegistration registration) => new PackNameRegistrationResult { PackName = new PackName { Id = 40, Name = registration.Name } });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, NumberedRows("City", 25)), 1);

        result.Success.Should().BeTrue();
        result.PackNamesCreated.Should().Be(25);
        result.PackNameListsCreated.Should().Be(1);
        result.FirstRowNotImported.Should().BeNull();
        registrations.Should().OnlyContain(registration => registration.PackNameList.Id == 70);
        result.Lists.Should().ContainSingle().Which.Should().BeEquivalentTo(new PackNameImportListCount { Name = "City › CTY", Count = 25 });
        _provisioning.Verify(provisioning => provisioning.EnsureDefaultPackNameListAsync(_city, PackNameListNameFrom.BranchCode), Times.Once);
        _activity.Verify(activity => activity.InsertActivityAsync("PublicStore.ImportPackNames", It.IsAny<string>(), null), Times.Once);
    }

    [Test]
    public async Task A_value_longer_than_its_database_field_is_a_row_error()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["Harbour", new string('a', 256), "No", "Female"], ["Harbour", new string('b', 255), "No", "Male"]), 1);

        layout.Column(PackNameImportColumns.Name).MaxLength.Should().Be(255);
        layout.Column(PackNameImportColumns.Branch).MaxLength.Should().BeNull();
        result.RowErrors.Should().ContainSingle().Which.Row.Should().Be(2);
        result.RowErrors[0].Messages.Should().ContainSingle().Which.Should().Contain("Import.Errors.TooLong");
    }

    [Test]
    public async Task An_exception_while_writing_imports_nothing_and_is_logged_after_the_rollback()
    {
        _registration.Setup(service => service.RegisterAsync(It.Is<PackNameRegistration>(registration => registration.Name == "Bob Ray")))
            .ThrowsAsync(new InvalidOperationException("database said no"));

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female"], ["Harbour", "Bob Ray", "No", "Male"]), 1);

        result.Success.Should().BeFalse();
        result.PackNamesCreated.Should().Be(0);
        result.Lists.Should().BeEmpty();
        result.HasErrorReport.Should().BeFalse();
        result.FileErrors.Should().ContainSingle().Which.Should().Contain("Import.Errors.Unexpected");
        result.FirstRowNotImported.Should().BeNull();
        _logger.Verify(logger => logger.ErrorAsync(It.Is<string>(message => message.Contains("row 3") && message.Contains("nothing was imported")), It.Is<Exception>(exception => exception.Message == "database said no"), It.IsAny<Customer>()), Times.Once);
        _activity.Verify(activity => activity.InsertActivityAsync("PublicStore.ImportPackNames", It.IsAny<string>(), null), Times.Never);
    }

    [Test]
    public async Task A_credit_with_a_comma_is_refused_rather_than_read_as_thousands()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female", "Winter", "50,5"]), 1);

        result.RowErrors.Should().ContainSingle().Which.Messages.Should().ContainSingle().Which.Should().Contain("Subsidy.CreditNotValid");
        _registration.Verify(registration => registration.RegisterAsync(It.IsAny<PackNameRegistration>()), Times.Never);
    }

    [Test]
    public async Task Values_in_a_column_the_store_no_longer_takes_refuse_the_file()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var withValue = await service.ImportAsync(layout, Workbook(["Branch", "Name", "Gender", "Employee number"], ["Harbour", "Ann Lee", "Female", "007"]), 1);
        var empty = await service.ImportAsync(layout, Workbook(["Branch", "Name", "Gender", "Employee number"], ["Harbour", "Bob Ray", "Male", ""]), 1);

        withValue.FileErrors.Should().ContainSingle().Which.Should().Contain("RetiredColumns");
        empty.Success.Should().BeTrue();
    }

    [Test]
    public async Task Account_phone_username_and_state_follow_the_customer_settings()
    {
        _settings.AllowToAddCustomerToPackName = true;
        _customerSettings.PhoneEnabled = true;
        _customerSettings.PhoneNumberValidationEnabled = true;
        _customerSettings.UsernamesEnabled = true;
        _customerSettings.UsernameValidationEnabled = true;
        _customerSettings.UsernameValidationRule = "abcdefghijklmnopqrstuvwxyz.";
        _customerSettings.CountryEnabled = true;
        _customerSettings.StateProvinceEnabled = true;
        _customerSettings.StateProvinceRequired = false;
        _states.Add(new StateProvince { Id = 9, CountryId = 1, Name = "Victoria", Abbreviation = "VIC" });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var headers = new[] { "Branch", "Name", "Gender", "Add customer", "Email", "Username", "Phone", "Country code", "State" };
        var result = await service.ImportAsync(layout, Workbook(headers,
            ["Harbour", "Ann Lee", "Female", "Yes", "ann@cw.test", "ann.lee", "+61 2 9374 4000", "AU", ""],
            ["Harbour", "Bob Ray", "Male", "Yes", "bob@cw.test", "Bob_Ray!", "call me", "AU", "VIC"]), 1);

        result.RowErrors.Should().ContainSingle().Which.Row.Should().Be(3);
        result.RowErrors[0].Messages.Should().BeEquivalentTo("Account.Fields.Username.NotValid", "Account.Fields.Phone.NotValid");
    }

    [Test]
    public async Task The_account_is_checked_by_the_form_validator_including_the_minimum_age()
    {
        _settings.AllowToAddCustomerToPackName = true;
        _customerSettings.DateOfBirthEnabled = true;
        _customerSettings.DateOfBirthRequired = true;
        _customerSettings.DateOfBirthMinimumAge = 18;

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var headers = new[] { "Branch", "Name", "Gender", "Add customer", "Email", "Date of birth" };
        var result = await service.ImportAsync(layout, Workbook(headers,
            ["Harbour", "Ann Lee", "Female", "Yes", "ann@cw.test", "1990-05-01"],
            ["Harbour", "Bob Ray", "Male", "Yes", "bob@cw.test", DateTime.Today.AddYears(-10)],
            ["Harbour", "Cy Dee", "Male", "Yes", "not an e-mail", ""]), 1);

        result.RowErrors.Select(error => error.Row).Should().Equal(3, 4);
        result.RowErrors[0].Messages.Should().ContainSingle().Which.Should().Be("Account.Fields.DateOfBirth.MinimumAge");
        result.RowErrors[1].Messages.Should().BeEquivalentTo("Common.WrongEmail", "Account.Fields.DateOfBirth.Required");
    }

    [Test]
    public async Task A_blank_cell_reads_as_an_empty_form_field_and_the_address_says_its_block()
    {
        _settings.AllowToAddCustomerToPackName = true;
        _settings.AllowHomeDelivery = true;
        _customerSettings.StreetAddressEnabled = true;
        _customerSettings.StreetAddressRequired = true;
        _addressSettings.StreetAddressEnabled = true;
        _addressSettings.StreetAddressRequired = true;

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var headers = new[] { "Branch", "Name", "Gender", "Add customer", "Email", "Street address", "Add address", "Address first name", "Address last name", "Address email", "Address street" };
        var result = await service.ImportAsync(layout, Workbook(headers,
            ["Harbour", "Ann Lee", "Female", "Yes", "", "", "Yes", "Ann", "Lee", "ann@cw.test", ""]), 1);

        result.RowErrors.Should().ContainSingle().Which.Messages.Should().BeEquivalentTo(
            "Account.Fields.Email.Required", "Account.Fields.StreetAddress.Required", "Address: Account.Fields.StreetAddress.Required");
    }

    [Test]
    public async Task Company_and_vat_number_required_by_the_store_do_not_block_a_row()
    {
        _settings.AllowToAddCustomerToPackName = true;
        _settings.AllowHomeDelivery = true;
        _customerSettings.CompanyEnabled = true;
        _customerSettings.CompanyRequired = true;
        _addressSettings.CompanyEnabled = true;
        _addressSettings.CompanyRequired = true;
        _taxSettings.EuVatEnabled = true;
        _taxSettings.EuVatRequired = true;

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var headers = new[] { "Branch", "Name", "Gender", "Add customer", "Email", "Add address", "Address first name", "Address last name", "Address email" };
        var result = await service.ImportAsync(layout, Workbook(headers,
            ["Harbour", "Ann Lee", "Female", "Yes", "ann@cw.test", "Yes", "Ann", "Lee", "ann@cw.test"]), 1);

        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task A_country_that_does_not_exist_is_reported_once()
    {
        _settings.AllowHomeDelivery = true;
        _addressSettings.CountryEnabled = true;

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var headers = new[] { "Branch", "Name", "Gender", "Add address", "Address first name", "Address last name", "Address email", "Address country code" };
        var result = await service.ImportAsync(layout, Workbook(headers,
            ["Harbour", "Ann Lee", "Female", "Yes", "Ann", "Lee", "ann@cw.test", "XX"],
            ["Harbour", "Bob Ray", "Male", "Yes", "Bob", "Ray", "bob@cw.test", ""]), 1);

        result.RowErrors.Select(error => error.Row).Should().Equal(2, 3);
        result.RowErrors[0].Messages.Should().ContainSingle().Which.Should().Contain("CountryCode.DoesntExist");
        result.RowErrors[1].Messages.Should().ContainSingle().Which.Should().Be("Address: Address.Fields.Country.Required");
    }

    [Test]
    public async Task A_read_only_checkbox_attribute_has_no_column_and_takes_its_preselected_values()
    {
        _settings.AllowToAddCustomerToPackName = true;
        _customerAttributes.Add(new CustomerAttribute { Id = 21, Name = "Terms", AttributeControlType = AttributeControlType.ReadonlyCheckboxes });
        _customerAttributeValues.Add(new CustomerAttributeValue { Id = 77, AttributeId = 21, IsPreSelected = true });
        _customerAttributeValues.Add(new CustomerAttributeValue { Id = 78, AttributeId = 21, IsPreSelected = false });

        var registrations = new List<PackNameRegistration>();
        _registration.Setup(service => service.RegisterAsync(Capture.In(registrations)))
            .ReturnsAsync((PackNameRegistration registration) => new PackNameRegistrationResult { PackName = new PackName { Id = 40 } });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(new[] { "Branch", "Name", "Gender", "Add customer", "Email" },
            ["Harbour", "Ann Lee", "Female", "Yes", "ann@cw.test"]), 1);

        layout.Columns.Should().NotContain(column => column.AttributeId == 21);
        result.Success.Should().BeTrue();
        registrations.Single().Customer.CustomAttributesXml.Should().Be("[21=77]");
    }

    [Test]
    public async Task A_retired_column_is_found_below_row_501_too()
    {
        var rows = Enumerable.Repeat(new object[] { "", "", "", "" }, 520).Append(new object[] { "Harbour", "Ann Lee", "Female", "007" }).ToArray();

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(["Branch", "Name", "Gender", "Employee number"], rows), 1);

        result.FileErrors.Should().ContainSingle().Which.Should().Contain("RetiredColumns");
    }

    [Test]
    public async Task A_column_given_twice_refuses_the_file_and_is_not_called_retired()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(["Branch", "Name", "Gender", "Name"], ["Harbour", "Ann Lee", "Female", "Ann Lee"]), 1);

        result.FileErrors.Should().ContainSingle().Which.Should().Contain("DuplicatedColumns");
        _registration.Verify(registration => registration.RegisterAsync(It.IsAny<PackNameRegistration>()), Times.Never);
    }

    [Test]
    public async Task Account_or_address_columns_without_their_yes_no_column_refuse_the_file()
    {
        _settings.AllowToAddCustomerToPackName = true;
        _settings.AllowHomeDelivery = true;

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var withoutAddCustomer = await service.ImportAsync(layout, Workbook(["Branch", "Name", "Gender", "Email"], ["Harbour", "Ann Lee", "Female", "ann@cw.test"]), 1);
        var withoutAddAddress = await service.ImportAsync(layout, Workbook(["Branch", "Name", "Gender", "Address first name"], ["Harbour", "Ann Lee", "Female", "Ann"]), 1);
        var withoutEither = await service.ImportAsync(layout, Workbook(["Branch", "Name", "Gender"], ["Harbour", "Ann Lee", "Female"]), 1);

        withoutAddCustomer.FileErrors.Should().ContainSingle().Which.Should().Contain("MissingColumns");
        withoutAddAddress.FileErrors.Should().ContainSingle().Which.Should().Contain("MissingColumns");
        withoutEither.Success.Should().BeTrue();
    }

    [Test]
    public async Task Rows_are_read_where_they_hold_values_however_far_down()
    {
        var rows = Enumerable.Repeat(new object[] { "", "", "", "" }, 2000).Append(new object[] { "Harbour", "Ann Lee", "No", "Female" }).ToArray();

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, rows), 1);

        result.Success.Should().BeTrue();
        result.RowCount.Should().Be(1);
        result.PackNamesCreated.Should().Be(1);
    }

    [Test]
    public async Task A_file_past_the_row_limit_reports_no_row_count()
    {
        var rows = Enumerable.Range(1, 501).Select(index => new object[] { "Harbour", $"Name {index}", "No", "Male" }).ToArray();

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, rows), 1);

        result.RowCount.Should().Be(0);
        result.FileErrors.Should().ContainSingle().Which.Should().Contain("TooManyRows");
    }

    [Test]
    public async Task A_credit_over_the_maximum_names_the_maximum()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        try
        {
            var service = Build();
            var layout = await service.BuildLayoutAsync(_manager, 1);

            var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female", "Winter", "2000000"]), 1);

            result.RowErrors.Should().ContainSingle().Which.Messages.Should().ContainSingle().Which.Should().Be("Subsidy 1: Credit.Maximum 1,000,000");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Test]
    public async Task An_exception_while_checking_the_file_is_logged_and_shows_no_partial_results()
    {
        _packNames.Setup(service => service.GetPackNamesByListIdAsync(_harbourList.Id)).ThrowsAsync(new InvalidOperationException("database said no"));

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["Nowhere", "Ann Lee", "No", "Female"], ["Harbour", "Bob Ray", "No", "Male"]), 1);

        result.Success.Should().BeFalse();
        result.RowErrors.Should().BeEmpty();
        result.HasErrorReport.Should().BeFalse();
        result.FileErrors.Should().ContainSingle().Which.Should().Be("Plugins.CW.Core.PackNames.Import.Errors.UnexpectedCheck");
        _logger.Verify(logger => logger.ErrorAsync(It.Is<string>(message => message.Contains("while checking")), It.IsAny<Exception>(), It.IsAny<Customer>()), Times.Once);
        _registration.Verify(registration => registration.RegisterAsync(It.IsAny<PackNameRegistration>()), Times.Never);
    }

    [Test]
    public async Task A_subsidy_named_again_with_its_own_values_is_refused()
    {
        _settings.AllowSubsidyCountByExcel = 2;
        _activeSubsidies.Add(new Subsidy { Id = 12, Name = "Summer", StoreId = 1 });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var headers = new[] { "Branch", "Name", "Group", "Gender", "Subsidy 1", "Subsidy 1 credit", "Subsidy 2", "Subsidy 2 credit" };
        var result = await service.ImportAsync(layout, Workbook(headers,
            ["Harbour", "Ann Lee", "No", "Female", "Winter", "10", "Winter", ""],
            ["Harbour", "Bob Ray", "No", "Male", "Winter", "10", "Winter", "20"]), 1);

        result.RowErrors.Should().ContainSingle().Which.Row.Should().Be(3);
        result.RowErrors[0].Messages.Should().ContainSingle().Which.Should().Contain("Subsidy.NamedTwice");
    }

    [Test]
    public async Task Subsidies_sharing_a_name_are_told_apart()
    {
        _activeSubsidies.Add(new Subsidy { Id = 12, Name = "Winter", StoreId = 1 });
        _activeSubsidies.Add(new Subsidy { Id = 13, Name = "Summer", StoreId = 1 });

        var layout = await Build().BuildLayoutAsync(_manager, 1);

        layout.SubsidyGroups[0].Subsidy.Options.Select(option => option.Text).Should().BeEquivalentTo("Winter #11", "Winter #12", "Summer");
    }

    [Test]
    public async Task The_account_gender_column_offers_what_the_form_offers()
    {
        _settings.AllowToAddCustomerToPackName = true;
        _settings.GenderEnabled = false;
        _customerSettings.GenderEnabled = true;
        _customerSettings.NeutralGenderEnabled = true;

        var layout = await Build().BuildLayoutAsync(_manager, 1);

        layout.Column(PackNameImportColumns.CustomerGender).Options.Select(option => option.Text).Should().Equal("Male", "Female");
    }

    [Test]
    public async Task An_exception_while_building_the_error_report_is_logged_and_gives_no_report()
    {
        _packNames.Setup(service => service.GetPackNamesByListIdAsync(_harbourList.Id)).ThrowsAsync(new InvalidOperationException("database said no"));

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var report = await service.BuildErrorReportAsync(layout, Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female"]));

        report.Should().BeNull();
        _logger.Verify(logger => logger.ErrorAsync(It.Is<string>(message => message.Contains("error report")), It.IsAny<Exception>(), It.IsAny<Customer>()), Times.Once);
    }

    [Test]
    public async Task A_date_kept_as_an_excel_serial_number_is_read()
    {
        var registrations = new List<PackNameRegistration>();
        _registration.Setup(service => service.RegisterAsync(Capture.In(registrations)))
            .ReturnsAsync((PackNameRegistration registration) => new PackNameRegistrationResult { PackName = new PackName { Id = 40 } });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders,
            ["Harbour", "Ann Lee", "No", "Female", "Winter", "", new DateTime(2026, 11, 1).ToOADate(), new DateTime(2026, 12, 31).ToOADate()]), 1);

        result.Success.Should().BeTrue();
        registrations.Single().Subsidies.Single().DurationStart.Should().Be(new DateTime(2026, 11, 1));
        registrations.Single().Subsidies.Single().DurationEnd.Should().Be(new DateTime(2026, 12, 31));
    }

    #endregion

    #region Imported files

    [Test]
    public async Task Valid_rows_are_registered_in_order_with_what_they_say()
    {
        var registrations = new List<PackNameRegistration>();
        _registration.Setup(service => service.RegisterAsync(Capture.In(registrations)))
            .ReturnsAsync((PackNameRegistration registration) => new PackNameRegistrationResult { PackName = new PackName { Id = 40, Name = registration.Name } });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var file = Workbook(_basicHeaders,
            ["Harbour", "Ann Lee", "Yes", "Mixed", "Winter", 50m, new DateTime(2026, 11, 1), "2026-12-31"],
            ["Harbour", "Bob Ray", "", "Male"]);

        var result = await service.ImportAsync(layout, file, 1);

        result.Success.Should().BeTrue();
        result.PackNamesCreated.Should().Be(2);
        result.PackNameListsCreated.Should().Be(0);
        registrations.Should().HaveCount(2);
        registrations[0].Name.Should().Be("Ann Lee");
        registrations[0].Group.Should().BeTrue();
        registrations[0].Gender.Should().Be("I");
        registrations[0].PackNameList.Should().BeSameAs(_harbourList);
        registrations[0].Branch.Should().BeSameAs(_harbour);
        registrations[0].LanguageId.Should().Be(1);
        registrations[0].CreatedByCustomerId.Should().Be(4);
        registrations[0].Subsidies.Should().ContainSingle().Which.Should().BeEquivalentTo(new PackNameSubsidyAssignment
        {
            SubsidyId = 11, Credit = 50, DurationStart = new DateTime(2026, 11, 1), DurationEnd = new DateTime(2026, 12, 31)
        });
        registrations[1].Group.Should().BeFalse();
        registrations[1].Gender.Should().Be("M");
        registrations[1].Subsidies.Should().BeEmpty();
        _activity.Verify(service => service.InsertActivityAsync("PublicStore.ImportPackNames", It.IsAny<string>(), null), Times.Once);
    }

    [Test]
    public async Task A_branch_without_any_list_gets_one_list_for_all_its_rows()
    {
        var registrations = new List<PackNameRegistration>();
        _registration.Setup(service => service.RegisterAsync(Capture.In(registrations)))
            .ReturnsAsync((PackNameRegistration registration) => new PackNameRegistrationResult { PackName = new PackName { Id = 40 } });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["City", "Ann Lee", "No", "Female"], ["City", "Bob Ray", "No", "Male"]), 1);

        result.Success.Should().BeTrue();
        result.PackNameListsCreated.Should().Be(1);
        registrations.Select(registration => registration.PackNameList.Id).Should().Equal(70, 70);
        _provisioning.Verify(service => service.EnsureDefaultPackNameListAsync(_city, PackNameListNameFrom.BranchCode), Times.Once);
    }

    [Test]
    public async Task The_result_counts_the_welcome_e_mails_queued()
    {
        _registration.Setup(service => service.RegisterAsync(It.IsAny<PackNameRegistration>()))
            .ReturnsAsync((PackNameRegistration registration) => new PackNameRegistrationResult
            {
                PackName = new PackName { Id = 40, Name = registration.Name },
                Customer = new Customer(),
                WelcomeEmailQueued = registration.Name == "Ann Lee"
            });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female"], ["Harbour", "Bob Ray", "No", "Male"]), 1);

        result.CustomersCreated.Should().Be(2);
        result.WelcomeEmailsQueued.Should().Be(1);
    }

    [Test]
    public async Task The_result_says_how_many_went_into_each_list()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, Workbook(_basicHeaders,
            ["Harbour", "Ann Lee", "No", "Female"], ["City", "Bob Ray", "No", "Male"], ["Harbour", "Cy Dee", "No", "Male"]), 1);

        result.Success.Should().BeTrue();
        result.Lists.Select(list => (list.Name, list.Count)).Should().Equal(("City › CTY", 1), ("Harbour › Harbour staff", 2));
    }

    [Test]
    public async Task The_error_report_is_the_file_with_an_errors_column()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var report = await service.BuildErrorReportAsync(layout, Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female"], ["Nowhere", "Bob Ray", "No", "Male"]));

        using var workbook = new XLWorkbook(new MemoryStream(report));
        var sheet = workbook.Worksheet(1);

        sheet.Cell(1, 9).GetString().Should().Be("Errors");
        sheet.Cell(2, 9).GetString().Should().BeEmpty();
        sheet.Cell(3, 9).GetString().Should().Contain("Branch.DoesntExist");
    }

    [Test]
    public async Task A_report_uploaded_again_keeps_one_errors_column()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var first = await service.BuildErrorReportAsync(layout, Workbook(_basicHeaders, ["Nowhere", "Ann Lee", "No", "Female"], ["Harbour", "Bob Ray", "Maybe", "Male"]));

        //the manager fixes the first row only and uploads the report
        using (var fixing = new XLWorkbook(new MemoryStream(first)))
        {
            fixing.Worksheet(1).Cell(2, 1).Value = "Harbour";
            using var fixedStream = new MemoryStream();
            fixing.SaveAs(fixedStream);
            first = fixedStream.ToArray();
        }

        var second = await service.BuildErrorReportAsync(layout, new MemoryStream(first));

        using var workbook = new XLWorkbook(new MemoryStream(second));
        var sheet = workbook.Worksheet(1);

        sheet.Cell(1, 10).GetString().Should().BeEmpty();
        sheet.Cell(2, 9).GetString().Should().BeEmpty();
        sheet.Cell(3, 9).GetString().Should().Contain("YesNo.NotValid");
    }

    #endregion

    #region An administrator's import

    private readonly Customer _administrator = new() { Id = 1 };

    [Test]
    public async Task An_administrator_is_offered_every_active_branch_whatever_the_limit()
    {
        _settings.LimitAssignedBranches = true;
        _storeBranches.Add(new Branch { Id = 8, StoreId = 1, Name = "Closed", Active = false });

        var layout = await Build().BuildAdministratorLayoutAsync(1, _administrator);

        layout.Manager.Should().BeNull();
        layout.CreatedByCustomerId.Should().Be(1);
        layout.Column("Branch").Options.Select(option => option.Text).Should().Equal("City", "Harbour");
        _multiStore.Verify(service => service.GetVisibleBranchesForManagerAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task An_administrator_gets_the_account_and_subsidy_columns_the_store_does_not_give_its_managers()
    {
        _settings.AllowToAddCustomerToPackName = false;
        _settings.AllowSubsidySelection = false;

        var service = Build();
        var administratorLayout = await service.BuildAdministratorLayoutAsync(1, _administrator);
        var managerLayout = await service.BuildLayoutAsync(_manager, 1);

        administratorLayout.Columns.Select(column => column.Header).Should().Contain("Subsidy 1").And.Contain("Add customer").And.Contain("Email");
        managerLayout.Columns.Select(column => column.Header).Should().NotContain("Subsidy 1").And.NotContain("Add customer").And.NotContain("Email");
    }

    [Test]
    public async Task With_the_subsidy_feature_off_an_administrator_gets_no_subsidy_columns()
    {
        _features.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, 1)).ReturnsAsync(false);

        var layout = await Build().BuildAdministratorLayoutAsync(1, _administrator);

        layout.SubsidyGroups.Should().BeEmpty();
        layout.Columns.Select(column => column.Header).Should().NotContain("Subsidy 1");
    }

    [Test]
    public async Task An_administrator_s_rows_go_into_the_branch_lists_with_the_administrator_as_their_creator()
    {
        var registrations = new List<PackNameRegistration>();
        _registration.Setup(service => service.ResolveTargetPackNameListAsync(_harbour)).ReturnsAsync(_harbourList);
        _registration.Setup(service => service.ResolveTargetPackNameListAsync(_city)).ReturnsAsync((PackNameList)null);
        _registration.Setup(service => service.RegisterAsync(Capture.In(registrations)))
            .ReturnsAsync((PackNameRegistration registration) => new PackNameRegistrationResult { PackName = new PackName { Id = 40, Name = registration.Name } });

        var service = Build();
        var layout = await service.BuildAdministratorLayoutAsync(1, _administrator);
        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female"], ["City", "Bob Ray", "No", "Male"]), 1);

        result.Success.Should().BeTrue();
        registrations.Select(registration => registration.PackNameList.Id).Should().Equal(7, 70);
        registrations.Should().OnlyContain(registration => registration.CreatedByCustomerId == 1 && registration.ByAdministrator);
        _registration.Verify(service => service.ResolveTargetPackNameListAsync(It.IsAny<PackName>(), It.IsAny<Branch>(), It.IsAny<MultiStoreSettings>()), Times.Never);
        _activity.Verify(activity => activity.InsertActivityAsync("ImportPackNames", It.IsAny<string>(), null), Times.Once);
        _activity.Verify(activity => activity.InsertActivityAsync("PublicStore.ImportPackNames", It.IsAny<string>(), null), Times.Never);
    }

    [Test]
    public async Task A_manager_s_rows_keep_the_manager_as_their_creator_and_the_storefront_activity_type()
    {
        var registrations = new List<PackNameRegistration>();
        _registration.Setup(service => service.RegisterAsync(Capture.In(registrations)))
            .ReturnsAsync((PackNameRegistration registration) => new PackNameRegistrationResult { PackName = new PackName { Id = 40, Name = registration.Name } });

        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);
        var result = await service.ImportAsync(layout, Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female"]), 1);

        result.Success.Should().BeTrue();
        layout.CreatedByCustomerId.Should().Be(4);
        registrations.Should().ContainSingle().Which.Should().Match<PackNameRegistration>(registration => registration.CreatedByCustomerId == 4 && !registration.ByAdministrator);
        _activity.Verify(activity => activity.InsertActivityAsync("PublicStore.ImportPackNames", It.IsAny<string>(), null), Times.Once);
        _activity.Verify(activity => activity.InsertActivityAsync("ImportPackNames", It.IsAny<string>(), null), Times.Never);
    }

    [Test]
    public async Task The_file_check_refuses_no_file_another_type_and_a_file_too_large()
    {
        var service = Build();

        (await service.CheckFileAsync(null, 0)).Should().Be("Plugins.CW.Core.PackNames.Import.NoFile");
        (await service.CheckFileAsync("staff.xlsx", 0)).Should().Be("Plugins.CW.Core.PackNames.Import.NoFile");
        (await service.CheckFileAsync("staff.csv", 100)).Should().Be("Plugins.CW.Core.PackNames.Import.Errors.InvalidFile");
        (await service.CheckFileAsync("staff.xlsx", PackNameImportDefaults.MaxFileBytes + 1)).Should().Be("Plugins.CW.Core.PackNames.Import.Errors.FileTooLarge");
        (await service.CheckFileAsync("Staff.XLSX", PackNameImportDefaults.MaxFileBytes)).Should().BeNull();
    }

    [Test]
    public async Task An_upload_that_cannot_seek_is_read_from_a_copy()
    {
        var service = Build();
        var layout = await service.BuildLayoutAsync(_manager, 1);

        var result = await service.ImportAsync(layout, new ForwardOnlyStream(Workbook(_basicHeaders, ["Harbour", "Ann Lee", "No", "Female"])), 1);

        result.Success.Should().BeTrue();
        result.PackNamesCreated.Should().Be(1);
    }

    #endregion
}

/// <summary>
/// A stream that can only be read forward, as a request body is
/// </summary>
internal class ForwardOnlyStream : Stream
{
    private readonly Stream _inner;

    public ForwardOnlyStream(Stream inner) => _inner = inner;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// The import with the database field lengths given here: nop reads them from its entity mapping, which needs the
/// running engine
/// </summary>
internal class TestablePackNameImportService : PackNameImportService
{
    public TestablePackNameImportService(IPackNameRegistrationService packNameRegistrationService,
        IPackNameProvisioningService packNameProvisioningService,
        IPackNameService packNameService,
        IBranchService branchService,
        IMultiStoreService multiStoreService,
        ISubsidyService subsidyService,
        ICWFeatureService cwFeatureService,
        ISettingService settingService,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        ICustomerService customerService,
        ICustomerActivityService customerActivityService,
        IAttributeService<CustomerAttribute, CustomerAttributeValue> customerAttributeService,
        IAttributeService<AddressAttribute, AddressAttributeValue> addressAttributeService,
        IAttributeParser<CustomerAttribute, CustomerAttributeValue> customerAttributeParser,
        IAttributeParser<AddressAttribute, AddressAttributeValue> addressAttributeParser,
        ILocalizationService localizationService,
        ILogger logger,
        IWorkContext workContext,
        CustomerSettings customerSettings,
        AddressSettings addressSettings,
        TaxSettings taxSettings) : base(packNameRegistrationService, packNameProvisioningService, packNameService,
            branchService, multiStoreService, subsidyService, cwFeatureService, settingService, countryService, stateProvinceService,
            customerService, customerActivityService, customerAttributeService, addressAttributeService, customerAttributeParser,
            addressAttributeParser, localizationService, logger, workContext, customerSettings, addressSettings, taxSettings)
    {
    }

    protected override int? ResolveMaxLength(Type entityType, string propertyName)
    {
        if (entityType == typeof(PackName) && propertyName != nameof(PackName.Comments))
            return 255;

        return entityType == typeof(Customer) ? 1000 : null;
    }
}
