using ClosedXML.Excel;
using FluentValidation;
using Nop.Core;
using Nop.Core.Domain.Attributes;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Tax;
using Nop.Data.Mapping;
using Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Attributes;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Web.Framework.Validators;
using Nop.Web.Models.Common;
using Nop.Web.Models.Customer;
using Nop.Web.Validators.Common;
using Nop.Web.Validators.Customer;
using System.Globalization;

namespace Nop.Plugin.CW.Core.Services.PackNames.Import;

/// <summary>
/// The Excel import of packnames, for a store's managers on the storefront and for administrators in the admin area
/// </summary>
/// <remarks>
/// <para>
/// The file is read twice over: every row is checked first, against the file itself (duplicate names and e-mails)
/// and against the store (branches, departments, cost centres, subsidies, existing names and accounts); only when
/// no row fails are the packnames created, in batches of a few rows, each in its own transaction, so the store's other
/// pages never wait for the whole file. A failure while writing rolls back its batch and stops; the batches before it stay.
/// </para>
/// <para>
/// A row's customer account and delivery address are checked by nop's own validators, the ones the add-packname form
/// runs, so the file and the form follow the same rules; this class only turns the cells into the form's values
/// (options into identifiers, dates, yes / no) and adds what a file alone can get wrong (a duplicate in the file, an
/// option that does not exist, a value longer than its database field).
/// </para>
/// <para>
/// Columns are found by their header, so their order in the file does not matter and unknown columns are ignored.
/// The write phase never re-reads what it has just created: lists made for branches without one are kept in a local
/// dictionary, because a cache filled inside the transaction would survive its rollback.
/// </para>
/// </remarks>
public class PackNameImportService : IPackNameImportService
{
    #region Fields

    protected readonly IPackNameRegistrationService _packNameRegistrationService;
    protected readonly IPackNameProvisioningService _packNameProvisioningService;
    protected readonly IPackNameService _packNameService;
    protected readonly IBranchService _branchService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly ISubsidyService _subsidyService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly ISettingService _settingService;
    protected readonly ICountryService _countryService;
    protected readonly IStateProvinceService _stateProvinceService;
    protected readonly ICustomerService _customerService;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly IAttributeService<CustomerAttribute, CustomerAttributeValue> _customerAttributeService;
    protected readonly IAttributeService<AddressAttribute, AddressAttributeValue> _addressAttributeService;
    protected readonly IAttributeParser<CustomerAttribute, CustomerAttributeValue> _customerAttributeParser;
    protected readonly IAttributeParser<AddressAttribute, AddressAttributeValue> _addressAttributeParser;
    protected readonly ILocalizationService _localizationService;
    protected readonly ILogger _logger;
    protected readonly IWorkContext _workContext;
    protected readonly CustomerSettings _customerSettings;
    protected readonly AddressSettings _addressSettings;
    protected readonly TaxSettings _taxSettings;

    /// <summary>
    /// The packname gender codes, in the order the Gender column's options use as identifiers
    /// </summary>
    private static readonly string[] _genderCodes =
    [
        MultiStoreServicesDefaults.GenderMixed,
        MultiStoreServicesDefaults.GenderMale,
        MultiStoreServicesDefaults.GenderFemale,
        MultiStoreServicesDefaults.GenderNeutral
    ];

    /// <summary>
    /// The columns stored in a database field of limited length, and the entity property behind each
    /// </summary>
    private static readonly Dictionary<string, (Type Entity, string Property)> _storedColumns = new()
    {
        [PackNameImportColumns.Name] = (typeof(PackName), nameof(PackName.Name)),
        [PackNameImportColumns.PrintName] = (typeof(PackName), nameof(PackName.PrintName)),
        [PackNameImportColumns.EmployeeNumber] = (typeof(PackName), nameof(PackName.EmployeeNumber)),
        [PackNameImportColumns.POEntry] = (typeof(PackName), nameof(PackName.POEntry)),
        [PackNameImportColumns.Comments] = (typeof(PackName), nameof(PackName.Comments)),
        [PackNameImportColumns.Email] = (typeof(Customer), nameof(Customer.Email)),
        [PackNameImportColumns.Username] = (typeof(Customer), nameof(Customer.Username)),
        [PackNameImportColumns.FirstName] = (typeof(Customer), nameof(Customer.FirstName)),
        [PackNameImportColumns.LastName] = (typeof(Customer), nameof(Customer.LastName)),
        [PackNameImportColumns.StreetAddress] = (typeof(Customer), nameof(Customer.StreetAddress)),
        [PackNameImportColumns.StreetAddress2] = (typeof(Customer), nameof(Customer.StreetAddress2)),
        [PackNameImportColumns.ZipPostalCode] = (typeof(Customer), nameof(Customer.ZipPostalCode)),
        [PackNameImportColumns.City] = (typeof(Customer), nameof(Customer.City)),
        [PackNameImportColumns.County] = (typeof(Customer), nameof(Customer.County)),
        [PackNameImportColumns.Phone] = (typeof(Customer), nameof(Customer.Phone)),
        [PackNameImportColumns.Fax] = (typeof(Customer), nameof(Customer.Fax)),
        [PackNameImportColumns.AddressFirstName] = (typeof(Address), nameof(Address.FirstName)),
        [PackNameImportColumns.AddressLastName] = (typeof(Address), nameof(Address.LastName)),
        [PackNameImportColumns.AddressEmail] = (typeof(Address), nameof(Address.Email)),
        [PackNameImportColumns.AddressAddress1] = (typeof(Address), nameof(Address.Address1)),
        [PackNameImportColumns.AddressAddress2] = (typeof(Address), nameof(Address.Address2)),
        [PackNameImportColumns.AddressZipPostalCode] = (typeof(Address), nameof(Address.ZipPostalCode)),
        [PackNameImportColumns.AddressCity] = (typeof(Address), nameof(Address.City)),
        [PackNameImportColumns.AddressCounty] = (typeof(Address), nameof(Address.County)),
        [PackNameImportColumns.AddressPhone] = (typeof(Address), nameof(Address.PhoneNumber)),
        [PackNameImportColumns.AddressFax] = (typeof(Address), nameof(Address.FaxNumber))
    };

    private static readonly string[] _yesWords = ["yes", "y", "true", "1"];
    private static readonly string[] _noWords = ["no", "n", "false", "0"];

    #endregion

    #region Ctor

    public PackNameImportService(IPackNameRegistrationService packNameRegistrationService,
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
        TaxSettings taxSettings)
    {
        _packNameRegistrationService = packNameRegistrationService;
        _packNameProvisioningService = packNameProvisioningService;
        _packNameService = packNameService;
        _branchService = branchService;
        _multiStoreService = multiStoreService;
        _subsidyService = subsidyService;
        _cwFeatureService = cwFeatureService;
        _settingService = settingService;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _customerService = customerService;
        _customerActivityService = customerActivityService;
        _customerAttributeService = customerAttributeService;
        _addressAttributeService = addressAttributeService;
        _customerAttributeParser = customerAttributeParser;
        _addressAttributeParser = addressAttributeParser;
        _localizationService = localizationService;
        _logger = logger;
        _workContext = workContext;
        _customerSettings = customerSettings;
        _addressSettings = addressSettings;
        _taxSettings = taxSettings;
    }

    #endregion

    #region Nested classes

    /// <summary>
    /// One row of the file after validation: everything the write phase needs, resolved
    /// </summary>
    protected class ParsedRow
    {
        public int Row { get; init; }

        public Branch Branch { get; init; }

        /// <summary>
        /// The list the packname goes into; null when the branch has no list yet and one is created on import
        /// </summary>
        public PackNameList PackNameList { get; init; }

        public PackNameRegistration Registration { get; init; }
    }

    /// <summary>
    /// The sheet of an uploaded file with its columns located by header
    /// </summary>
    protected class ParsedFile
    {
        public XLWorkbook Workbook { get; init; }

        public IXLWorksheet Sheet { get; init; }

        /// <summary>
        /// The column number of each layout column found in the file, by column key
        /// </summary>
        public IDictionary<string, int> ColumnNumbers { get; init; } = new Dictionary<string, int>();

        /// <summary>
        /// The row numbers that hold data, blank rows left out
        /// </summary>
        public IList<int> DataRows { get; init; } = new List<int>();

        public int LastColumn { get; init; }

        /// <summary>
        /// The column of an error report's Errors header, when the file is a report uploaded again; 0 otherwise
        /// </summary>
        public int ErrorsColumn { get; init; }
    }

    /// <summary>
    /// Where the write phase is, so a failure can name it, and what its committed batches created
    /// </summary>
    protected class WriteProgress
    {
        /// <summary>
        /// The row being written
        /// </summary>
        public int Row { get; set; }

        /// <summary>
        /// The first row of the batch being written: when that batch fails, the first row not imported
        /// </summary>
        public int BatchFirstRow { get; set; }

        /// <summary>
        /// The lists the committed batches created for branches without an active list, by branch id
        /// </summary>
        public Dictionary<int, PackNameList> CreatedLists { get; } = new();

        /// <summary>
        /// How many packnames the committed batches put into each list, by list id
        /// </summary>
        public Dictionary<int, PackNameImportListCount> CountsByList { get; } = new();
    }

    #endregion

    #region Utilities - cells

    /// <summary>
    /// The text of a cell: blank for an empty cell, numbers and dates in the import's own formats
    /// </summary>
    protected virtual string CellText(IXLWorksheet sheet, int row, int column)
    {
        if (column <= 0)
            return string.Empty;

        var value = sheet.Cell(row, column).Value;

        if (value.IsBlank)
            return string.Empty;
        if (value.IsText)
            return value.GetText().Trim();
        if (value.IsNumber)
            return value.GetNumber().ToString(CultureInfo.InvariantCulture);
        if (value.IsBoolean)
            return value.GetBoolean() ? _yesWords[0] : _noWords[0];
        if (value.IsDateTime)
            return value.GetDateTime().ToString(PackNameImportDefaults.DateFormat, CultureInfo.InvariantCulture);

        return value.ToString().Trim();
    }

    /// <summary>
    /// The text of the file's cell for a layout column; blank when the file has no such column
    /// </summary>
    protected virtual string CellText(ParsedFile file, int row, string columnKey)
    {
        return file.ColumnNumbers.TryGetValue(columnKey, out var column) ? CellText(file.Sheet, row, column) : string.Empty;
    }

    /// <summary>
    /// The value a cell gives a form field: null for a blank cell, as the form posts an empty field
    /// </summary>
    protected virtual string FieldValue(ParsedFile file, int row, string columnKey)
    {
        var text = CellText(file, row, columnKey);

        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// A date cell: an Excel date, or text in the import's date format. Returns false when the cell holds something else
    /// </summary>
    protected virtual bool TryReadDate(ParsedFile file, int row, string columnKey, out DateTime? date)
    {
        date = null;

        if (!file.ColumnNumbers.TryGetValue(columnKey, out var column))
            return true;

        var value = file.Sheet.Cell(row, column).Value;

        if (value.IsBlank)
            return true;

        if (value.IsDateTime)
        {
            date = value.GetDateTime().Date;
            return true;
        }

        //a date Excel keeps as its serial number: pasted values, or a column formatted General
        if (value.IsNumber)
        {
            var serial = value.GetNumber();

            if (serial < 1 || serial >= 2958466)
                return false;

            date = DateTime.FromOADate(serial).Date;
            return true;
        }

        var text = CellText(file.Sheet, row, column);

        if (string.IsNullOrEmpty(text))
            return true;

        if (DateTime.TryParseExact(text, PackNameImportDefaults.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed.Date;
            return true;
        }

        return false;
    }

    /// <summary>
    /// A Yes / No cell. Blank counts as No; anything else that is not a yes or a no word returns null
    /// </summary>
    protected virtual bool? ReadYesNo(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var lower = text.Trim().ToLowerInvariant();

        if (_yesWords.Contains(lower))
            return true;
        if (_noWords.Contains(lower))
            return false;

        //the template offers the localized words
        return null;
    }

    protected virtual async Task<IList<(string Text, int Id)>> YesNoOptionsAsync()
    {
        return new List<(string Text, int Id)>
        {
            (await _localizationService.GetResourceAsync("Common.Yes"), 1),
            (await _localizationService.GetResourceAsync("Common.No"), 0)
        };
    }

    #endregion

    #region Utilities - layout

    /// <summary>
    /// Whether a custom attribute of this control type can be a column: lists and text boxes can, pickers that need
    /// a picture, a colour or a file cannot, and read-only checkboxes are not filled in by anyone
    /// </summary>
    protected virtual bool AttributeIsImportable(BaseAttribute attribute)
    {
        return attribute.AttributeControlType is AttributeControlType.DropdownList
            or AttributeControlType.RadioList
            or AttributeControlType.Checkboxes
            or AttributeControlType.TextBox
            or AttributeControlType.MultilineTextbox;
    }

    /// <summary>
    /// Whether a registration carries a custom attribute of this control type: the importable ones, and read-only
    /// checkboxes, which take their pre-selected values as on the form
    /// </summary>
    protected virtual bool AttributeIsCarried(BaseAttribute attribute)
    {
        return AttributeIsImportable(attribute) || attribute.AttributeControlType == AttributeControlType.ReadonlyCheckboxes;
    }

    /// <summary>
    /// Whether a custom attribute of this control type takes its value from a list of options
    /// </summary>
    protected virtual bool AttributeHasOptions(BaseAttribute attribute)
    {
        return attribute.AttributeControlType is AttributeControlType.DropdownList
            or AttributeControlType.RadioList
            or AttributeControlType.Checkboxes;
    }

    /// <summary>
    /// The columns of the custom customer or address attributes
    /// </summary>
    protected virtual async Task<IList<PackNameImportColumn>> AttributeColumnsAsync<TAttribute, TValue>(IList<TAttribute> attributes,
        IAttributeService<TAttribute, TValue> attributeService, string keyPrefix, string headerPrefix)
        where TAttribute : BaseAttribute
        where TValue : BaseAttributeValue
    {
        var columns = new List<PackNameImportColumn>();

        foreach (var attribute in attributes.Where(AttributeIsImportable))
        {
            var name = (await _localizationService.GetLocalizedAsync(attribute, x => x.Name))?.Trim();
            var options = new List<(string Text, int Id)>();

            if (AttributeHasOptions(attribute))
            {
                foreach (var value in await attributeService.GetAttributeValuesAsync(attribute.Id))
                    options.Add((await _localizationService.GetLocalizedAsync(value, x => x.Name), value.Id));
            }

            columns.Add(new PackNameImportColumn
            {
                Key = keyPrefix + attribute.Id,
                Header = headerPrefix + name,
                Kind = options.Count > 0 ? PackNameImportColumnKind.Dropdown : PackNameImportColumnKind.Text,
                Options = options,
                AllowBlank = !attribute.IsRequired,
                AttributeId = attribute.Id,
                SeveralOptions = attribute.AttributeControlType == AttributeControlType.Checkboxes
            });
        }

        return columns;
    }

    /// <summary>
    /// The text that names a branch in the file: its name, with the branch code added when another branch of the
    /// store has the same name
    /// </summary>
    protected virtual string BranchOptionText(Branch branch, IList<Branch> branches)
    {
        return branches.Count(other => string.Equals(other.Name, branch.Name, StringComparison.OrdinalIgnoreCase)) > 1
            ? $"{branch.Name} ({branch.BranchCode})"
            : branch.Name;
    }

    /// <summary>
    /// The texts that name the cost centres in the file: the name, with the branch added when another cost centre of
    /// the store has the same name, and the identifier as well when that still does not tell them apart
    /// </summary>
    protected virtual async Task<IList<(string Text, int Id)>> CostCenterOptionsAsync(IList<(int Id, string Name)> costCenters)
    {
        var options = new List<(string Text, int Id)>();

        foreach (var (id, name) in costCenters)
        {
            var text = name;

            if (costCenters.Count(other => string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)) > 1)
            {
                var costCenter = await _multiStoreService.GetCostCenterByIdAsync(id);
                var branch = costCenter?.BranchId > 0 ? await _branchService.GetBranchByIdAsync(costCenter.BranchId.Value) : null;

                text = branch != null
                    ? $"{name} ({branch.Name})"
                    : $"{name} ({await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.WholeStore")})";
            }

            options.Add((text, id));
        }

        return options
            .Select(option => options.Count(other => string.Equals(other.Text, option.Text, StringComparison.OrdinalIgnoreCase)) > 1
                ? ($"{option.Text} #{option.Id}", option.Id)
                : option)
            .ToList();
    }

    /// <summary>
    /// The most characters the database field of an entity property holds, from nop's mapping of the entity (the same
    /// source as nop's validators); null when the field is not limited
    /// </summary>
    protected virtual int? ResolveMaxLength(Type entityType, string propertyName)
    {
        var size = NopMappingSchema.GetEntityDescriptor(entityType)?.Fields.FirstOrDefault(field => field.Name == propertyName)?.Size;

        return size > 0 && size < int.MaxValue ? size : null;
    }

    #endregion

    #region Utilities - reading and validation

    /// <summary>
    /// Opens the file and locates the layout's columns by header. File-level problems go to the result
    /// </summary>
    protected virtual async Task<ParsedFile> ReadFileAsync(PackNameImportLayout layout, Stream file, PackNameImportResult result)
    {
        XLWorkbook workbook;

        try
        {
            //the workbook reader seeks and reads synchronously, so it reads a copy in memory, never the upload's own
            //stream; a file is at most a couple of megabytes
            var copy = new MemoryStream();
            await file.CopyToAsync(copy);
            copy.Position = 0;

            workbook = new XLWorkbook(copy);
        }
        catch (Exception)
        {
            result.FileErrors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.InvalidFile"));
            return null;
        }

        var sheet = workbook.Worksheets.FirstOrDefault(candidate => candidate.Name == PackNameImportDefaults.DataSheetName)
            ?? workbook.Worksheets.FirstOrDefault();

        if (sheet == null)
        {
            result.FileErrors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.InvalidFile"));
            return null;
        }

        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var columnNumbers = new Dictionary<string, int>();
        var duplicatedHeaders = new List<string>();
        var duplicatedColumns = new HashSet<int>();
        var errorsColumn = 0;

        for (var column = 1; column <= lastColumn; column++)
        {
            var header = CellText(sheet, 1, column);

            if (string.IsNullOrEmpty(header))
                continue;

            //an error report uploaded again: its Errors column is the report's, not data
            if (string.Equals(header, PackNameImportDefaults.ErrorsHeader, StringComparison.OrdinalIgnoreCase))
            {
                if (errorsColumn == 0)
                    errorsColumn = column;

                continue;
            }

            var layoutColumn = layout.Columns.FirstOrDefault(candidate => string.Equals(candidate.Header, header, StringComparison.OrdinalIgnoreCase));

            if (layoutColumn == null)
                continue;

            //a column copied: which of the two holds the values is not for the import to guess
            if (columnNumbers.ContainsKey(layoutColumn.Key))
            {
                duplicatedHeaders.Add(layoutColumn.Header);
                duplicatedColumns.Add(column);

                continue;
            }

            columnNumbers[layoutColumn.Key] = column;
        }

        var missing = layout.Columns
            .Where(column => column.Required && !columnNumbers.ContainsKey(column.Key))
            .Select(column => column.Header)
            .ToList();

        //a block's columns without its Yes / No column would be read as "No" on every row and their values dropped
        //without a word: the account block is recognised by its e-mail, the address block by any of its columns
        var addCustomerColumn = layout.Column(PackNameImportColumns.AddCustomer);

        if (addCustomerColumn != null && !columnNumbers.ContainsKey(addCustomerColumn.Key) && columnNumbers.ContainsKey(PackNameImportColumns.Email))
            missing.Add(addCustomerColumn.Header);

        var addAddressColumn = layout.Column(PackNameImportColumns.AddAddress);

        if (addAddressColumn != null && !columnNumbers.ContainsKey(addAddressColumn.Key)
            && columnNumbers.Keys.Any(key => key.StartsWith(PackNameImportColumns.AddressBlockPrefix, StringComparison.Ordinal)))
            missing.Add(addAddressColumn.Header);

        if (missing.Count > 0)
            result.FileErrors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.MissingColumns"), string.Join(", ", missing)));

        if (duplicatedHeaders.Count > 0)
            result.FileErrors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.DuplicatedColumns"), string.Join(", ", duplicatedHeaders.Distinct())));

        var dataRows = new List<int>();

        //only the rows that hold a value: a stray value far below the data would otherwise make every empty row up to
        //it be read
        foreach (var row in sheet.RowsUsed(XLCellsUsedOptions.Contents).Select(used => used.RowNumber()).Where(number => number >= 2))
        {
            var blank = columnNumbers.Values.All(column => string.IsNullOrEmpty(CellText(sheet, row, column)));

            if (!blank)
                dataRows.Add(row);

            //one row past the limit is enough to refuse the file
            if (dataRows.Count > PackNameImportDefaults.MaxRows)
                break;
        }

        //past the limit the file is refused and the count would only be the limit plus one
        result.RowCount = dataRows.Count <= PackNameImportDefaults.MaxRows ? dataRows.Count : 0;

        if (dataRows.Count == 0)
            result.FileErrors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.NoRows"));
        else if (dataRows.Count > PackNameImportDefaults.MaxRows)
            result.FileErrors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.TooManyRows"), PackNameImportDefaults.MaxRows));

        //a column some template writes but this store's no longer does (its setting was switched off since the
        //template was downloaded): ignoring it would import "Add customer = Yes" rows without their accounts
        var retired = new List<string>();

        for (var column = 1; column <= lastColumn; column++)
        {
            if (columnNumbers.Values.Contains(column) || duplicatedColumns.Contains(column))
                continue;

            var header = CellText(sheet, 1, column);

            if (!PackNameImportHeaders.IsKnown(header))
                continue;

            if (dataRows.Any(row => !string.IsNullOrEmpty(CellText(sheet, row, column))))
                retired.Add(header);
        }

        if (retired.Count > 0)
            result.FileErrors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.RetiredColumns"), string.Join(", ", retired)));

        return new ParsedFile
        {
            Workbook = workbook,
            Sheet = sheet,
            ColumnNumbers = columnNumbers,
            DataRows = dataRows,
            LastColumn = lastColumn,
            ErrorsColumn = errorsColumn
        };
    }

    /// <summary>
    /// A dropdown cell: the identifier its text stands for. Blank is allowed when the column is; any other text that
    /// is not an option adds the message and returns null
    /// </summary>
    protected virtual async Task<int?> ReadOptionAsync(ParsedFile file, int row, PackNameImportColumn column, string missingResource, string unknownResource, IList<string> errors)
    {
        if (column == null)
            return null;

        var text = CellText(file, row, column.Key);

        if (string.IsNullOrEmpty(text))
        {
            if (!column.AllowBlank && missingResource != null)
                errors.Add(await _localizationService.GetResourceAsync(missingResource));

            return null;
        }

        var id = column.OptionId(text);

        if (id == null)
            errors.Add(string.Format(await _localizationService.GetResourceAsync(unknownResource), text));

        return id;
    }

    /// <summary>
    /// A Yes / No cell of the file, with the message when it is neither
    /// </summary>
    protected virtual async Task<bool> ReadYesNoAsync(ParsedFile file, int row, PackNameImportColumn column, IList<string> errors)
    {
        if (column == null || !file.ColumnNumbers.ContainsKey(column.Key))
            return false;

        var text = CellText(file, row, column.Key);

        //the template's own words first (they are localized), then the plain yes / no words
        var optionId = column.OptionId(text);

        if (optionId.HasValue)
            return optionId.Value == 1;

        var value = ReadYesNo(text);

        if (value == null)
        {
            errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.YesNo.NotValid"), column.Header));
            return false;
        }

        return value.Value;
    }

    /// <summary>
    /// A country and state pair of the file: the country by its two-letter code, the state by its abbreviation or name
    /// within that country. Whether they are required is the validator's to say; here only a value that names nothing
    /// is reported, and <c>Unknown</c> tells the caller so
    /// </summary>
    protected virtual async Task<(int CountryId, int StateProvinceId, bool Unknown)> ReadCountryAndStateAsync(ParsedFile file, int row, PackNameImportLayout layout,
        string countryKey, string stateKey, bool countryEnabled, bool stateEnabled, IList<string> errors)
    {
        if (!countryEnabled)
            return (0, 0, false);

        var countryText = CellText(file, row, countryKey);

        if (string.IsNullOrEmpty(countryText))
            return (0, 0, false);

        var country = layout.Countries.FirstOrDefault(candidate => string.Equals(candidate.TwoLetterIsoCode, countryText, StringComparison.OrdinalIgnoreCase));

        if (country == null)
        {
            errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.CountryCode.DoesntExist"), countryText));
            return (0, 0, true);
        }

        if (!stateEnabled)
            return (country.Id, 0, false);

        var stateText = CellText(file, row, stateKey);

        if (string.IsNullOrEmpty(stateText))
            return (country.Id, 0, false);

        layout.StatesByCountry.TryGetValue(country.Id, out var states);

        var state = states?.FirstOrDefault(candidate => string.Equals(candidate.Abbreviation, stateText, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Name, stateText, StringComparison.OrdinalIgnoreCase));

        if (state == null)
        {
            errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.StateProvince.DoesntExist"), stateText, country.Name));
            return (country.Id, 0, true);
        }

        return (country.Id, state.Id, false);
    }

    /// <summary>
    /// Runs one of nop's validators over the values of a row and adds its messages, leaving out the properties named
    /// in <paramref name="skipped"/>: fields the registration fills itself, and fields whose cell already has its own
    /// message. A message two rules give for one field is added once
    /// </summary>
    /// <param name="messageFormat">How each message is written into the row's errors; {0} is the validator's message</param>
    protected virtual async Task AddValidatorErrorsAsync<TModel>(IValidator<TModel> validator, TModel model, ICollection<string> skipped, string messageFormat, IList<string> errors)
    {
        var validation = await validator.ValidateAsync(model);

        foreach (var message in validation.Errors.Where(failure => !skipped.Contains(failure.PropertyName)).Select(failure => failure.ErrorMessage).Distinct())
            errors.Add(string.Format(messageFormat, message));
    }

    /// <summary>
    /// The custom attributes of a row as nop's attributes XML, with every value checked against the attribute's options
    /// and nop's own warnings (required attributes among them) added to the errors. Read-only checkboxes take their
    /// pre-selected values, as the form gives them
    /// </summary>
    protected virtual async Task<string> ReadAttributesAsync<TAttribute, TValue>(ParsedFile file, int row, IList<TAttribute> attributes,
        IList<PackNameImportColumn> columns, IAttributeService<TAttribute, TValue> attributeService, IAttributeParser<TAttribute, TValue> parser, IList<string> errors)
        where TAttribute : BaseAttribute
        where TValue : BaseAttributeValue
    {
        var xml = string.Empty;

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeControlType == AttributeControlType.ReadonlyCheckboxes)
            {
                foreach (var value in (await attributeService.GetAttributeValuesAsync(attribute.Id)).Where(value => value.IsPreSelected))
                    xml = parser.AddAttribute(xml, attribute, value.Id.ToString());

                continue;
            }

            var column = columns.FirstOrDefault(candidate => candidate.AttributeId == attribute.Id);

            if (column == null)
                continue;

            var text = CellText(file, row, column.Key);

            if (string.IsNullOrEmpty(text))
                continue;

            if (column.Kind != PackNameImportColumnKind.Dropdown)
            {
                xml = parser.AddAttribute(xml, attribute, text);
                continue;
            }

            //a checkbox list takes several options, comma separated
            var parts = attribute.AttributeControlType == AttributeControlType.Checkboxes
                ? text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : new[] { text };

            foreach (var part in parts)
            {
                var id = column.OptionId(part);

                if (id == null)
                {
                    errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Attribute.ValueDoesntExist"), column.Header, part));
                    continue;
                }

                xml = parser.AddAttribute(xml, attribute, id.Value.ToString());
            }
        }

        foreach (var warning in await parser.GetAttributeWarningsAsync(xml))
            errors.Add(warning);

        return xml;
    }

    /// <summary>
    /// The customer account block of a row. The values are checked by nop's customer validator, as the form's are;
    /// what only a file can get wrong (an e-mail or username twice in the file, an account that already exists, a
    /// country or option that names nothing, a date that is not one) is checked here
    /// </summary>
    protected virtual async Task<PackNameCustomerRegistration> ReadCustomerAsync(ParsedFile file, int row, PackNameImportLayout layout, string packNameGender,
        IValidator<CustomerInfoModel> validator, IDictionary<string, int> emailsInFile, IDictionary<string, int> usernamesInFile, IList<string> errors)
    {
        //the fields the registration fills itself are not the file's to give
        var skipped = new HashSet<string>(PackNameServicesDefaults.ServerSuppliedCustomerFields);

        var email = CellText(file, row, PackNameImportColumns.Email);

        if (CommonHelper.IsValidEmail(email))
        {
            var emailKey = email.ToLowerInvariant();

            if (emailsInFile.TryGetValue(emailKey, out var firstRow))
                errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Email.Duplicated"), email, firstRow));
            else
            {
                emailsInFile[emailKey] = row;

                if (await _customerService.GetCustomerByEmailAsync(email) != null)
                    errors.Add(await _localizationService.GetResourceAsync("Account.Register.Errors.EmailAlreadyExists"));
            }
        }

        var username = email;

        if (_customerSettings.UsernamesEnabled)
        {
            //checked here as nop's registration checks it: the customer validator asks for a username only where
            //customers may change theirs
            skipped.Add(nameof(CustomerInfoModel.Username));
            username = CellText(file, row, PackNameImportColumns.Username);

            if (string.IsNullOrEmpty(username))
                errors.Add(await _localizationService.GetResourceAsync("Account.Fields.Username.Required"));
            else
            {
                var usernameKey = username.ToLowerInvariant();

                if (!UsernamePropertyValidator<PackNameImportService, string>.IsValid(username, _customerSettings))
                    errors.Add(await _localizationService.GetResourceAsync("Account.Fields.Username.NotValid"));
                else if (usernamesInFile.TryGetValue(usernameKey, out var firstRow))
                    errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Username.Duplicated"), username, firstRow));
                else
                {
                    usernamesInFile[usernameKey] = row;

                    if (await _customerService.GetCustomerByUsernameAsync(username) != null)
                        errors.Add(await _localizationService.GetResourceAsync("Account.Register.Errors.UsernameAlreadyExists"));
                }
            }
        }

        DateTime? dateOfBirth = null;

        if (_customerSettings.DateOfBirthEnabled && !TryReadDate(file, row, PackNameImportColumns.DateOfBirth, out dateOfBirth))
        {
            errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.DateOfBirth.NotValid"), CellText(file, row, PackNameImportColumns.DateOfBirth)));
            skipped.Add(nameof(CustomerInfoModel.DateOfBirthDay));
        }

        //the packname's gender stands for the customer's when the store asks for one on the packname; a mixed
        //packname gives a male customer, as the form does
        var gender = string.Empty;

        if (_customerSettings.GenderEnabled)
        {
            if (layout.StoreSettings.GenderEnabled)
                gender = packNameGender == MultiStoreServicesDefaults.GenderMixed ? MultiStoreServicesDefaults.GenderMale : packNameGender;
            else
            {
                var genderId = await ReadOptionAsync(file, row, layout.Column(PackNameImportColumns.CustomerGender), null,
                    "Plugins.CW.Core.PackNames.PackName.Field.Gender.DoesntExist", errors);

                gender = genderId.HasValue ? _genderCodes[genderId.Value] : string.Empty;
            }
        }

        var (countryId, stateProvinceId, unknownPlace) = await ReadCountryAndStateAsync(file, row, layout, PackNameImportColumns.Country, PackNameImportColumns.StateProvince,
            _customerSettings.CountryEnabled, _customerSettings.CountryEnabled && _customerSettings.StateProvinceEnabled, errors);

        if (unknownPlace)
        {
            skipped.Add(nameof(CustomerInfoModel.CountryId));
            skipped.Add(nameof(CustomerInfoModel.StateProvinceId));
        }

        var model = new CustomerInfoModel
        {
            Email = string.IsNullOrEmpty(email) ? null : email,
            Username = string.IsNullOrEmpty(username) ? null : username,
            Gender = gender,
            FirstName = FieldValue(file, row, PackNameImportColumns.FirstName),
            LastName = FieldValue(file, row, PackNameImportColumns.LastName),
            DateOfBirthDay = dateOfBirth?.Day,
            DateOfBirthMonth = dateOfBirth?.Month,
            DateOfBirthYear = dateOfBirth?.Year,
            StreetAddress = FieldValue(file, row, PackNameImportColumns.StreetAddress),
            StreetAddress2 = FieldValue(file, row, PackNameImportColumns.StreetAddress2),
            ZipPostalCode = FieldValue(file, row, PackNameImportColumns.ZipPostalCode),
            City = FieldValue(file, row, PackNameImportColumns.City),
            County = FieldValue(file, row, PackNameImportColumns.County),
            CountryId = countryId,
            StateProvinceId = stateProvinceId,
            Phone = FieldValue(file, row, PackNameImportColumns.Phone),
            Fax = FieldValue(file, row, PackNameImportColumns.Fax)
        };

        await AddValidatorErrorsAsync(validator, model, skipped, "{0}", errors);

        var attributesXml = await ReadAttributesAsync(file, row, layout.CustomerAttributes,
            layout.Columns.Where(column => column.Key.StartsWith(PackNameImportColumns.CustomerAttributePrefix)).ToList(), _customerAttributeService, _customerAttributeParser, errors);

        return new PackNameCustomerRegistration
        {
            Email = model.Email,
            Username = model.Username,
            Gender = model.Gender,
            FirstName = model.FirstName,
            LastName = model.LastName,
            DateOfBirth = dateOfBirth,
            StreetAddress = model.StreetAddress,
            StreetAddress2 = model.StreetAddress2,
            ZipPostalCode = model.ZipPostalCode,
            City = model.City,
            County = model.County,
            CountryId = model.CountryId,
            StateProvinceId = model.StateProvinceId,
            Phone = model.Phone,
            Fax = model.Fax,
            CustomAttributesXml = attributesXml
        };
    }

    /// <summary>
    /// The delivery address block of a row, checked by nop's address validator as the form's address is
    /// </summary>
    protected virtual async Task<Address> ReadAddressAsync(ParsedFile file, int row, PackNameImportLayout layout, IValidator<AddressModel> validator, IList<string> errors)
    {
        //the fields the registration fills itself are not the file's to give
        var skipped = new HashSet<string>(PackNameServicesDefaults.ServerSuppliedAddressFields);

        var (countryId, stateProvinceId, unknownPlace) = await ReadCountryAndStateAsync(file, row, layout, PackNameImportColumns.AddressCountry, PackNameImportColumns.AddressStateProvince,
            _addressSettings.CountryEnabled, _addressSettings.CountryEnabled && _addressSettings.StateProvinceEnabled, errors);

        if (unknownPlace)
        {
            skipped.Add(nameof(AddressModel.CountryId));
            skipped.Add(nameof(AddressModel.StateProvinceId));
        }

        var model = new AddressModel
        {
            FirstName = FieldValue(file, row, PackNameImportColumns.AddressFirstName),
            LastName = FieldValue(file, row, PackNameImportColumns.AddressLastName),
            Email = FieldValue(file, row, PackNameImportColumns.AddressEmail),
            CountryId = countryId,
            StateProvinceId = stateProvinceId,
            County = FieldValue(file, row, PackNameImportColumns.AddressCounty),
            City = FieldValue(file, row, PackNameImportColumns.AddressCity),
            Address1 = FieldValue(file, row, PackNameImportColumns.AddressAddress1),
            Address2 = FieldValue(file, row, PackNameImportColumns.AddressAddress2),
            ZipPostalCode = FieldValue(file, row, PackNameImportColumns.AddressZipPostalCode),
            PhoneNumber = FieldValue(file, row, PackNameImportColumns.AddressPhone),
            FaxNumber = FieldValue(file, row, PackNameImportColumns.AddressFax)
        };

        //nop's address messages share their text with the account's (street, city, phone...): the row says which block
        await AddValidatorErrorsAsync(validator, model, skipped,
            await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.AddressMessage"), errors);

        var attributesXml = await ReadAttributesAsync(file, row, layout.AddressAttributes,
            layout.Columns.Where(column => column.Key.StartsWith(PackNameImportColumns.AddressAttributePrefix)).ToList(), _addressAttributeService, _addressAttributeParser, errors);

        return new Address
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            CountryId = countryId,
            StateProvinceId = stateProvinceId,
            County = model.County,
            City = model.City,
            Address1 = model.Address1,
            Address2 = model.Address2,
            ZipPostalCode = model.ZipPostalCode,
            PhoneNumber = model.PhoneNumber,
            FaxNumber = model.FaxNumber,
            CustomAttributes = attributesXml
        };
    }

    /// <summary>
    /// The subsidy column groups of a row. A subsidy named in two groups counts once
    /// </summary>
    protected virtual async Task<IList<PackNameSubsidyAssignment>> ReadSubsidiesAsync(ParsedFile file, int row, PackNameImportLayout layout, IList<string> errors)
    {
        var assignments = new List<PackNameSubsidyAssignment>();
        var settings = layout.StoreSettings;

        foreach (var group in layout.SubsidyGroups)
        {
            var subsidyText = CellText(file, row, group.Subsidy.Key);
            var creditText = group.Credit != null ? CellText(file, row, group.Credit.Key) : string.Empty;
            var hasStart = group.DurationStart != null && !string.IsNullOrEmpty(CellText(file, row, group.DurationStart.Key));
            var hasEnd = group.DurationEnd != null && !string.IsNullOrEmpty(CellText(file, row, group.DurationEnd.Key));

            if (string.IsNullOrEmpty(subsidyText))
            {
                //credit or dates without a subsidy to attach them to
                if (!string.IsNullOrEmpty(creditText) || hasStart || hasEnd)
                    errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Subsidy.NameRequired"), group.Subsidy.Header));

                continue;
            }

            var subsidyId = group.Subsidy.OptionId(subsidyText);

            if (subsidyId == null)
            {
                errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Subsidy.DoesntExist"), subsidyText));
                continue;
            }

            //a subsidy named again counts once; values given with the repeat would be lost, so they are refused
            if (assignments.Any(assignment => assignment.SubsidyId == subsidyId.Value))
            {
                if (!string.IsNullOrEmpty(creditText) || hasStart || hasEnd)
                    errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Subsidy.NamedTwice"), subsidyText, group.Subsidy.Header));

                continue;
            }

            decimal? credit = null;

            if (!string.IsNullOrEmpty(creditText))
            {
                if (!decimal.TryParse(creditText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedCredit))
                    errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Subsidy.CreditNotValid"), group.Credit.Header, creditText));
                else if (parsedCredit < 0)
                    errors.Add($"{group.Subsidy.Header}: {await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Credit.NotNegative")}");
                else if (parsedCredit > SubsidyTopupValidator.MaximumTopupCredit)
                    errors.Add($"{group.Subsidy.Header}: " + (await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Credit.Maximum"))
                        .Replace("{ComparisonValue}", SubsidyTopupValidator.MaximumTopupCredit.ToString("N0", CultureInfo.CurrentCulture)));
                else
                    credit = parsedCredit;
            }

            DateTime? durationStart = null;
            DateTime? durationEnd = null;

            if (group.DurationStart != null && group.DurationEnd != null)
            {
                var startOk = TryReadDate(file, row, group.DurationStart.Key, out durationStart);
                var endOk = TryReadDate(file, row, group.DurationEnd.Key, out durationEnd);

                if (!startOk)
                    errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Date.NotValid"), group.DurationStart.Header, CellText(file, row, group.DurationStart.Key)));
                if (!endOk)
                    errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Date.NotValid"), group.DurationEnd.Header, CellText(file, row, group.DurationEnd.Key)));

                //the group is named: a row may have several
                if (startOk && endOk)
                {
                    if (durationStart.HasValue != durationEnd.HasValue)
                        errors.Add($"{group.Subsidy.Header}: {await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.Dates.BothOrNeither")}");
                    else if (durationStart.HasValue && durationEnd.Value <= durationStart.Value)
                        errors.Add($"{group.Subsidy.Header}: {await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Subsidies.Subsidy.Variation.Field.DurationEnd.GreaterThanDurationStart")}");
                }
            }

            assignments.Add(new PackNameSubsidyAssignment
            {
                SubsidyId = subsidyId.Value,
                Credit = settings.PackNamesOwnGlobalValue ? credit : null,
                DurationStart = settings.PackNamesOwnStartAndFinishDates ? durationStart : null,
                DurationEnd = settings.PackNamesOwnStartAndFinishDates ? durationEnd : null
            });
        }

        return assignments;
    }

    /// <summary>
    /// Checks every data row of the file and resolves what each one creates. Row problems go to the result
    /// </summary>
    protected virtual async Task<IList<ParsedRow>> ValidateRowsAsync(ParsedFile file, PackNameImportLayout layout, int languageId, PackNameImportResult result)
    {
        var rows = new List<ParsedRow>();
        var settings = layout.StoreSettings;

        var branchColumn = layout.Column(PackNameImportColumns.Branch);
        var departmentColumn = layout.Column(PackNameImportColumns.Department);
        var costCenterColumn = layout.Column(PackNameImportColumns.CostCenter);
        var groupColumn = layout.Column(PackNameImportColumns.Group);
        var genderColumn = layout.Column(PackNameImportColumns.Gender);
        var addCustomerColumn = layout.Column(PackNameImportColumns.AddCustomer);
        var addAddressColumn = layout.Column(PackNameImportColumns.AddAddress);

        //resolved once per branch: its target list (null = the branch has no active list and gets one on import), and the
        //names already in that list or in earlier rows of the file
        var listsByBranch = new Dictionary<int, PackNameList>();
        var namesByBranch = new Dictionary<int, Dictionary<string, int>>();
        var emailsInFile = new Dictionary<string, int>();
        var usernamesInFile = new Dictionary<string, int>();

        var limitedColumns = layout.Columns.Where(column => column.MaxLength > 0).ToList();

        //the validators the add-packname form runs on its customer account and address
        var customerValidator = new CustomerInfoValidator(_localizationService, _stateProvinceService, _customerSettings, _taxSettings);
        var addressValidator = new AddressValidator(_localizationService, _stateProvinceService, _addressSettings, _customerSettings);

        foreach (var row in file.DataRows)
        {
            var errors = new List<string>();

            foreach (var column in limitedColumns)
            {
                if (CellText(file, row, column.Key).Length > column.MaxLength)
                    errors.Add(string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.TooLong"), column.Header, column.MaxLength));
            }

            var name = CellText(file, row, PackNameImportColumns.Name);

            if (string.IsNullOrEmpty(name))
                errors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Name.Required"));

            Branch branch = null;
            PackNameList packNameList = null;

            var branchId = await ReadOptionAsync(file, row, branchColumn, "Plugins.CW.Core.PackNames.PackName.Field.Branch.Required",
                "Plugins.CW.Core.PackNames.PackName.Field.Branch.DoesntExist", errors);

            if (branchId.HasValue)
            {
                branch = layout.Branches.First(candidate => candidate.Id == branchId.Value);

                //null: the branch has no active list and gets one on import. Under LimitAssignedBranches the layout
                //offers a manager only branches with a list of his, so there it is never null; an administrator's row
                //goes into the branch's own default list
                if (!listsByBranch.TryGetValue(branch.Id, out packNameList))
                {
                    packNameList = layout.Manager != null
                        ? await _packNameRegistrationService.ResolveTargetPackNameListAsync(layout.Manager, branch, settings)
                        : await _packNameRegistrationService.ResolveTargetPackNameListAsync(branch);
                    listsByBranch[branch.Id] = packNameList;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    if (!namesByBranch.TryGetValue(branch.Id, out var names))
                    {
                        names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                        if (packNameList != null)
                        {
                            foreach (var existing in await _packNameService.GetPackNamesByListIdAsync(packNameList.Id))
                                names.TryAdd(existing.Name?.Trim() ?? string.Empty, 0);
                        }

                        namesByBranch[branch.Id] = names;
                    }

                    if (names.TryGetValue(name, out var firstRow))
                    {
                        errors.Add(firstRow == 0
                            ? string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Name.Exists"), name, branch.Name)
                            : string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Name.Duplicated"), name, firstRow, branch.Name));
                    }
                    else
                        names[name] = row;
                }
            }

            var departmentId = await ReadOptionAsync(file, row, departmentColumn, null, "Plugins.CW.Core.PackNames.PackName.Field.Department.DoesntExist", errors);
            var costCenterId = await ReadOptionAsync(file, row, costCenterColumn, null, "Plugins.CW.Core.PackNames.PackName.Field.CostCenter.DoesntExist", errors);
            var group = await ReadYesNoAsync(file, row, groupColumn, errors);

            var gender = MultiStoreServicesDefaults.GenderMixed;

            if (genderColumn != null)
            {
                var genderId = await ReadOptionAsync(file, row, genderColumn, "Plugins.CW.Core.PackNames.PackName.Field.Gender.Required",
                    "Plugins.CW.Core.PackNames.PackName.Field.Gender.DoesntExist", errors);

                if (genderId.HasValue)
                    gender = _genderCodes[genderId.Value];
            }

            PackNameCustomerRegistration customer = null;

            if (layout.CustomerColumns && await ReadYesNoAsync(file, row, addCustomerColumn, errors))
            {
                if (group)
                    errors.Add(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.PackName.Field.Group.NoCustomer"));
                else if (branch != null)
                    customer = await ReadCustomerAsync(file, row, layout, gender, customerValidator, emailsInFile, usernamesInFile, errors);
            }

            Address address = null;

            if (layout.AddressColumns && branch != null && await ReadYesNoAsync(file, row, addAddressColumn, errors))
                address = await ReadAddressAsync(file, row, layout, addressValidator, errors);

            var subsidies = await ReadSubsidiesAsync(file, row, layout, errors);

            if (errors.Count > 0)
            {
                result.RowErrors.Add(new PackNameImportRowError { Row = row, Messages = errors });
                continue;
            }

            rows.Add(new ParsedRow
            {
                Row = row,
                Branch = branch,
                PackNameList = packNameList,
                Registration = new PackNameRegistration
                {
                    StoreId = layout.StoreId,
                    Branch = branch,
                    PackNameList = packNameList,
                    CreatedByCustomerId = layout.CreatedByCustomerId,
                    ByAdministrator = layout.Manager == null,
                    LanguageId = languageId,
                    Name = name,
                    PrintName = CellText(file, row, PackNameImportColumns.PrintName),
                    Group = group,
                    Gender = gender,
                    Colour = CellText(file, row, PackNameImportColumns.Colour),
                    Comments = CellText(file, row, PackNameImportColumns.Comments),
                    EmployeeNumber = CellText(file, row, PackNameImportColumns.EmployeeNumber),
                    POEntry = CellText(file, row, PackNameImportColumns.POEntry),
                    DepartmentId = departmentId,
                    CostCenterId = costCenterId,
                    Customer = customer,
                    Address = address,
                    Subsidies = subsidies
                }
            });
        }

        return rows;
    }

    /// <summary>
    /// Creates the checked rows in batches of <see cref="PackNameImportDefaults.WriteBatchSize"/>, each in its own
    /// transaction, so the rows a batch locks are released within a second and the store's other pages do not wait for
    /// the whole file. A row refused while it is written rolls back its batch and stops the import; the batches before it
    /// stay, and the result counts only what was committed
    /// </summary>
    protected virtual async Task WriteRowsAsync(IList<ParsedRow> rows, PackNameImportLayout layout, PackNameImportResult result, WriteProgress progress)
    {
        var nameFrom = (PackNameListNameFrom)layout.StoreSettings.PackNameListNameFrom;

        foreach (var batch in rows.Chunk(PackNameImportDefaults.WriteBatchSize))
        {
            progress.BatchFirstRow = batch[0].Row;

            if (await WriteBatchAsync(batch, nameFrom, result, progress))
                continue;

            if (result.PackNamesCreated > 0)
                result.FirstRowNotImported = progress.BatchFirstRow;

            return;
        }
    }

    /// <summary>
    /// Creates one batch of rows in one transaction, and adds them to the result once it has committed
    /// </summary>
    /// <returns>Whether the batch was committed; false when one of its rows was refused and the batch rolled back</returns>
    protected virtual async Task<bool> WriteBatchAsync(ParsedRow[] batch, PackNameListNameFrom nameFrom, PackNameImportResult result, WriteProgress progress)
    {
        var createdLists = new Dictionary<int, PackNameList>();
        var written = new List<(ParsedRow Row, PackNameList List, PackNameRegistrationResult Registered)>();

        //everything inside runs in sequence on purpose: two connections open at once would promote the transaction to a
        //distributed one. The scope commits when it is disposed after Complete, so the batch is counted only after that
        using (var scope = PackNameTransaction.Begin(TimeSpan.FromMinutes(1)))
        {
            foreach (var row in batch)
            {
                progress.Row = row.Row;

                var packNameList = row.PackNameList;

                //a list created by an earlier batch is committed and reused; one created by this batch is reused too
                if (packNameList == null && !progress.CreatedLists.TryGetValue(row.Branch.Id, out packNameList)
                    && !createdLists.TryGetValue(row.Branch.Id, out packNameList))
                {
                    packNameList = await _packNameProvisioningService.EnsureDefaultPackNameListAsync(row.Branch, nameFrom);
                    createdLists[row.Branch.Id] = packNameList;
                }

                var registered = await _packNameRegistrationService.RegisterAsync(row.Registration with { PackNameList = packNameList });

                if (!registered.Success)
                {
                    //the scope is disposed without Complete, so this batch is rolled back; the batches before it stay
                    result.RowErrors.Add(new PackNameImportRowError { Row = row.Row, Messages = registered.Errors });
                    result.RefusedOnWrite = true;

                    return false;
                }

                written.Add((row, packNameList, registered));
            }

            scope.Complete();
        }

        foreach (var (branchId, packNameList) in createdLists)
            progress.CreatedLists[branchId] = packNameList;

        result.PackNameListsCreated += createdLists.Count;

        foreach (var (row, packNameList, registered) in written)
        {
            result.PackNamesCreated++;

            if (registered.Customer != null)
                result.CustomersCreated++;

            if (registered.WelcomeEmailQueued)
                result.WelcomeEmailsQueued++;

            if (!progress.CountsByList.TryGetValue(packNameList.Id, out var listCount))
                progress.CountsByList[packNameList.Id] = listCount = new PackNameImportListCount { Name = $"{row.Branch.Name} › {packNameList.Name}" };

            listCount.Count++;
        }

        return true;
    }

    /// <summary>
    /// Reports the lists the committed batches filled and, when any packname was created, logs the import under the
    /// manager's or the administrator's activity type. A failure to write the activity entry is logged: the packnames
    /// are created by then and the result says so
    /// </summary>
    protected virtual async Task FinishWriteAsync(PackNameImportLayout layout, PackNameImportResult result, WriteProgress progress)
    {
        foreach (var listCount in progress.CountsByList.Values.OrderBy(listCount => listCount.Name))
            result.Lists.Add(listCount);

        if (result.PackNamesCreated == 0)
            return;

        try
        {
            //the entry is the signed-in customer's: the manager on the storefront, the administrator in the admin area
            await _customerActivityService.InsertActivityAsync(layout.Manager != null ? PackNameImportDefaults.ManagerActivityLogType : PackNameImportDefaults.AdministratorActivityLogType,
                string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.ActivityLog.ImportPackNames"), result.PackNamesCreated));
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync($"Excel import of packnames: {result.PackNamesCreated} packname(s) were created but the activity entry could not be written",
                exception, await _workContext.GetCurrentCustomerAsync());
        }
    }

    #endregion

    #region Utilities - the layout of a file

    /// <summary>
    /// The columns of a file and the data its rows are checked against. For a manager: the store's active branches
    /// (under LimitAssignedBranches only those where he owns a list) and the columns the store lets its managers fill
    /// in. With no manager, for an administrator: every active branch of the store, and the account and subsidy
    /// columns whatever the managers may do
    /// </summary>
    /// <param name="manager">The manager's own packname; null for an administrator</param>
    /// <param name="createdByCustomerId">The customer recorded as the creator of what the file creates</param>
    /// <param name="storeId">The store the packnames are imported into</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the layout
    /// </returns>
    protected virtual async Task<PackNameImportLayout> BuildLayoutForImporterAsync(PackName manager, int createdByCustomerId, int storeId)
    {
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);
        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        //the limit is a manager's: an administrator is offered every active branch of the store
        var limited = manager != null && settings.LimitAssignedBranches;

        var branches = (limited
                ? await _multiStoreService.GetVisibleBranchesForManagerAsync(manager.Id)
                : await _branchService.GetAllBranchesByStoreIdAsync(storeId))
            .Where(branch => branch.Active && !branch.Deleted)
            .OrderBy(branch => branch.Name)
            .ToList();

        //the visible set also holds the parents of the manager's branches, shown so a tree reads well; under the limit
        //the manager adds only to a list he owns, so a branch without one is not offered
        if (limited)
        {
            var owned = new List<Branch>();

            foreach (var branch in branches)
            {
                if (await _packNameRegistrationService.ResolveTargetPackNameListAsync(manager, branch, settings) != null)
                    owned.Add(branch);
            }

            branches = owned;
        }

        var departments = (await _multiStoreService.GetDepartmentsByStoreIdAsync(storeId))
            .Where(department => department.Active)
            .OrderBy(department => department.Name)
            .ToList();

        var costCenters = (await _multiStoreService.GetCostCenterSelectItemsAsync(storeId))
            .Where(costCenter => costCenter.Active)
            .OrderBy(costCenter => costCenter.Name)
            .Select(costCenter => (costCenter.Id, costCenter.Name))
            .ToList();

        //picking subsidies and adding accounts are what the store lets its managers do; an administrator does both,
        //subsidies wherever the feature is on
        var subsidySelection = (manager == null || settings.AllowSubsidySelection) && await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, storeId);
        var subsidies = subsidySelection
            ? (await _subsidyService.GetActiveSubsidiesByStoreIdAsync(storeId)).OrderBy(subsidy => subsidy.Name).ToList()
            : new List<Subsidy>();

        var customerColumns = manager == null || settings.AllowToAddCustomerToPackName;
        var addressColumns = settings.AllowHomeDelivery;

        var countries = customerColumns || addressColumns
            ? await _countryService.GetAllCountriesAsync(languageId)
            : new List<Country>();

        var statesByCountry = new Dictionary<int, IList<StateProvince>>();

        if (countries.Count > 0)
        {
            foreach (var state in await _stateProvinceService.GetStateProvincesAsync())
            {
                if (!statesByCountry.TryGetValue(state.CountryId, out var states))
                    statesByCountry[state.CountryId] = states = new List<StateProvince>();

                states.Add(state);
            }
        }

        var customerAttributes = customerColumns
            ? (await _customerAttributeService.GetAllAttributesAsync()).Where(AttributeIsCarried).ToList()
            : new List<CustomerAttribute>();

        var addressAttributes = addressColumns
            ? (await _addressAttributeService.GetAllAttributesAsync()).Where(AttributeIsCarried).ToList()
            : new List<AddressAttribute>();

        var yesNo = await YesNoOptionsAsync();

        var genders = new List<(string Text, int Id)>
        {
            (await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Mixed"), 0),
            (await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Male"), 1),
            (await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Female"), 2)
        };

        if (_customerSettings.NeutralGenderEnabled)
            genders.Add((await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.PackNames.PackName.Field.Gender.Neutral"), 3));

        var countryOptions = countries.Select(country => (country.TwoLetterIsoCode, country.Id)).ToList();

        var columns = new List<PackNameImportColumn>
        {
            new()
            {
                Key = PackNameImportColumns.Branch,
                Header = PackNameImportHeaders.Branch,
                Kind = PackNameImportColumnKind.Dropdown,
                Options = branches.Select(branch => (BranchOptionText(branch, branches), branch.Id)).ToList(),
                AllowBlank = false,
                Required = true
            },
            new() { Key = PackNameImportColumns.Name, Header = PackNameImportHeaders.Name, Required = true },
            new() { Key = PackNameImportColumns.Group, Header = PackNameImportHeaders.Group, Kind = PackNameImportColumnKind.Dropdown, Options = yesNo }
        };

        if (departments.Count > 0)
            columns.Add(new()
            {
                Key = PackNameImportColumns.Department,
                Header = PackNameImportHeaders.Department,
                Kind = PackNameImportColumnKind.Dropdown,
                Options = departments.Select(department => (department.Name, department.Id)).ToList()
            });

        if (costCenters.Count > 0)
            columns.Add(new()
            {
                Key = PackNameImportColumns.CostCenter,
                Header = PackNameImportHeaders.CostCenter,
                Kind = PackNameImportColumnKind.Dropdown,
                Options = await CostCenterOptionsAsync(costCenters)
            });

        if (settings.EmployeeNumberEnabled)
            columns.Add(new() { Key = PackNameImportColumns.EmployeeNumber, Header = PackNameImportHeaders.EmployeeNumber });

        if (settings.GenderEnabled)
            columns.Add(new() { Key = PackNameImportColumns.Gender, Header = PackNameImportHeaders.Gender, Kind = PackNameImportColumnKind.Dropdown, Options = genders, AllowBlank = false });

        if (settings.PrintNameEnabled)
            columns.Add(new() { Key = PackNameImportColumns.PrintName, Header = PackNameImportHeaders.PrintName });

        if (settings.POEntryEnabled)
            columns.Add(new() { Key = PackNameImportColumns.POEntry, Header = PackNameImportHeaders.POEntry });

        columns.Add(new() { Key = PackNameImportColumns.Colour, Header = PackNameImportHeaders.Colour });

        if (settings.CommentsEnabled)
            columns.Add(new() { Key = PackNameImportColumns.Comments, Header = PackNameImportHeaders.Comments });

        var subsidyGroups = new List<PackNameImportSubsidyGroup>();

        if (subsidies.Count > 0)
        {
            //more groups than the store has subsidies to pick would only be empty columns
            var groupCount = Math.Min(Math.Clamp(settings.AllowSubsidyCountByExcel, 1, PackNameImportDefaults.MaxSubsidyGroups), subsidies.Count);
            //subsidies sharing a name are told apart by their identifier
            var subsidyOptions = subsidies
                .Select(subsidy => (subsidies.Count(other => string.Equals(other.Name, subsidy.Name, StringComparison.OrdinalIgnoreCase)) > 1
                    ? $"{subsidy.Name} #{subsidy.Id}"
                    : subsidy.Name, subsidy.Id))
                .ToList();

            for (var i = 1; i <= groupCount; i++)
            {
                var group = new PackNameImportSubsidyGroup
                {
                    Subsidy = new()
                    {
                        Key = PackNameImportColumns.SubsidyPrefix + i,
                        Header = string.Format(PackNameImportHeaders.SubsidyFormat, i),
                        Kind = PackNameImportColumnKind.Dropdown,
                        Options = subsidyOptions
                    },
                    Credit = settings.PackNamesOwnGlobalValue
                        ? new() { Key = PackNameImportColumns.SubsidyCreditPrefix + i, Header = string.Format(PackNameImportHeaders.SubsidyCreditFormat, i), Kind = PackNameImportColumnKind.Decimal }
                        : null,
                    DurationStart = settings.PackNamesOwnStartAndFinishDates
                        ? new() { Key = PackNameImportColumns.SubsidyStartPrefix + i, Header = string.Format(PackNameImportHeaders.SubsidyStartFormat, i), Kind = PackNameImportColumnKind.Date }
                        : null,
                    DurationEnd = settings.PackNamesOwnStartAndFinishDates
                        ? new() { Key = PackNameImportColumns.SubsidyEndPrefix + i, Header = string.Format(PackNameImportHeaders.SubsidyEndFormat, i), Kind = PackNameImportColumnKind.Date }
                        : null
                };

                subsidyGroups.Add(group);
                columns.Add(group.Subsidy);

                if (group.Credit != null)
                    columns.Add(group.Credit);
                if (group.DurationStart != null)
                    columns.Add(group.DurationStart);
                if (group.DurationEnd != null)
                    columns.Add(group.DurationEnd);
            }
        }

        if (customerColumns)
        {
            columns.Add(new() { Key = PackNameImportColumns.AddCustomer, Header = PackNameImportHeaders.AddCustomer, Kind = PackNameImportColumnKind.Dropdown, Options = yesNo });
            columns.Add(new() { Key = PackNameImportColumns.Email, Header = PackNameImportHeaders.Email });

            if (_customerSettings.UsernamesEnabled)
                columns.Add(new() { Key = PackNameImportColumns.Username, Header = PackNameImportHeaders.Username });
            if (_customerSettings.FirstNameEnabled)
                columns.Add(new() { Key = PackNameImportColumns.FirstName, Header = PackNameImportHeaders.FirstName });
            if (_customerSettings.LastNameEnabled)
                columns.Add(new() { Key = PackNameImportColumns.LastName, Header = PackNameImportHeaders.LastName });
            if (_customerSettings.DateOfBirthEnabled)
                columns.Add(new() { Key = PackNameImportColumns.DateOfBirth, Header = PackNameImportHeaders.DateOfBirth, Kind = PackNameImportColumnKind.Date });

            //the packname's gender serves as the customer's when the store asks for one on the packname; otherwise the
            //account's own, male or female as the form offers
            if (_customerSettings.GenderEnabled && !settings.GenderEnabled)
                columns.Add(new() { Key = PackNameImportColumns.CustomerGender, Header = PackNameImportHeaders.CustomerGender, Kind = PackNameImportColumnKind.Dropdown, Options = genders.Where(gender => gender.Id is 1 or 2).ToList() });

            if (_customerSettings.StreetAddressEnabled)
                columns.Add(new() { Key = PackNameImportColumns.StreetAddress, Header = PackNameImportHeaders.StreetAddress });
            if (_customerSettings.StreetAddress2Enabled)
                columns.Add(new() { Key = PackNameImportColumns.StreetAddress2, Header = PackNameImportHeaders.StreetAddress2 });
            if (_customerSettings.ZipPostalCodeEnabled)
                columns.Add(new() { Key = PackNameImportColumns.ZipPostalCode, Header = PackNameImportHeaders.ZipPostalCode });
            if (_customerSettings.CityEnabled)
                columns.Add(new() { Key = PackNameImportColumns.City, Header = PackNameImportHeaders.City });
            if (_customerSettings.CountyEnabled)
                columns.Add(new() { Key = PackNameImportColumns.County, Header = PackNameImportHeaders.County });
            if (_customerSettings.CountryEnabled)
                columns.Add(new() { Key = PackNameImportColumns.Country, Header = PackNameImportHeaders.Country, Kind = PackNameImportColumnKind.Dropdown, Options = countryOptions });
            if (_customerSettings.CountryEnabled && _customerSettings.StateProvinceEnabled)
                columns.Add(new() { Key = PackNameImportColumns.StateProvince, Header = PackNameImportHeaders.StateProvince });
            if (_customerSettings.PhoneEnabled)
                columns.Add(new() { Key = PackNameImportColumns.Phone, Header = PackNameImportHeaders.Phone });
            if (_customerSettings.FaxEnabled)
                columns.Add(new() { Key = PackNameImportColumns.Fax, Header = PackNameImportHeaders.Fax });

            foreach (var column in await AttributeColumnsAsync(customerAttributes, _customerAttributeService, PackNameImportColumns.CustomerAttributePrefix, PackNameImportHeaders.CustomerAttributePrefix))
                columns.Add(column);
        }

        if (addressColumns)
        {
            columns.Add(new() { Key = PackNameImportColumns.AddAddress, Header = PackNameImportHeaders.AddAddress, Kind = PackNameImportColumnKind.Dropdown, Options = yesNo });
            columns.Add(new() { Key = PackNameImportColumns.AddressFirstName, Header = PackNameImportHeaders.AddressFirstName });
            columns.Add(new() { Key = PackNameImportColumns.AddressLastName, Header = PackNameImportHeaders.AddressLastName });
            columns.Add(new() { Key = PackNameImportColumns.AddressEmail, Header = PackNameImportHeaders.AddressEmail });

            if (_addressSettings.StreetAddressEnabled)
                columns.Add(new() { Key = PackNameImportColumns.AddressAddress1, Header = PackNameImportHeaders.AddressAddress1 });
            if (_addressSettings.StreetAddress2Enabled)
                columns.Add(new() { Key = PackNameImportColumns.AddressAddress2, Header = PackNameImportHeaders.AddressAddress2 });
            if (_addressSettings.ZipPostalCodeEnabled)
                columns.Add(new() { Key = PackNameImportColumns.AddressZipPostalCode, Header = PackNameImportHeaders.AddressZipPostalCode });
            if (_addressSettings.CityEnabled)
                columns.Add(new() { Key = PackNameImportColumns.AddressCity, Header = PackNameImportHeaders.AddressCity });
            if (_addressSettings.CountyEnabled)
                columns.Add(new() { Key = PackNameImportColumns.AddressCounty, Header = PackNameImportHeaders.AddressCounty });
            if (_addressSettings.CountryEnabled)
                columns.Add(new() { Key = PackNameImportColumns.AddressCountry, Header = PackNameImportHeaders.AddressCountry, Kind = PackNameImportColumnKind.Dropdown, Options = countryOptions });
            if (_addressSettings.CountryEnabled && _addressSettings.StateProvinceEnabled)
                columns.Add(new() { Key = PackNameImportColumns.AddressStateProvince, Header = PackNameImportHeaders.AddressStateProvince });
            if (_addressSettings.PhoneEnabled)
                columns.Add(new() { Key = PackNameImportColumns.AddressPhone, Header = PackNameImportHeaders.AddressPhone });
            if (_addressSettings.FaxEnabled)
                columns.Add(new() { Key = PackNameImportColumns.AddressFax, Header = PackNameImportHeaders.AddressFax });

            foreach (var column in await AttributeColumnsAsync(addressAttributes, _addressAttributeService, PackNameImportColumns.AddressAttributePrefix, PackNameImportHeaders.AddressAttributePrefix))
                columns.Add(column);
        }

        foreach (var column in columns)
        {
            if (_storedColumns.TryGetValue(column.Key, out var stored))
                column.MaxLength = ResolveMaxLength(stored.Entity, stored.Property);
        }

        return new PackNameImportLayout
        {
            StoreId = storeId,
            StoreSettings = settings,
            Manager = manager,
            CreatedByCustomerId = createdByCustomerId,
            Branches = branches,
            Countries = countries,
            StatesByCountry = statesByCountry,
            CustomerAttributes = customerAttributes,
            AddressAttributes = addressAttributes,
            CustomerColumns = customerColumns,
            AddressColumns = addressColumns,
            SubsidyGroups = subsidyGroups,
            Columns = columns
        };
    }

    #endregion

    #region Methods

    public virtual Task<PackNameImportLayout> BuildLayoutAsync(PackName manager, int storeId)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return BuildLayoutForImporterAsync(manager, manager.CustomerId ?? 0, storeId);
    }

    public virtual Task<PackNameImportLayout> BuildAdministratorLayoutAsync(int storeId, Customer administrator)
    {
        ArgumentNullException.ThrowIfNull(administrator);

        return BuildLayoutForImporterAsync(null, administrator.Id, storeId);
    }

    public virtual async Task<string> CheckFileAsync(string fileName, long length)
    {
        if (string.IsNullOrEmpty(fileName) || length <= 0)
            return await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.NoFile");

        if (!string.Equals(Path.GetExtension(fileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.InvalidFile");

        if (length > PackNameImportDefaults.MaxFileBytes)
            return string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.FileTooLarge"), PackNameImportDefaults.MaxFileBytes / 1024 / 1024);

        return null;
    }

    public virtual Task<byte[]> GenerateTemplateAsync(PackNameImportLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(PackNameImportDefaults.DataSheetName);
        var options = workbook.Worksheets.Add(PackNameImportDefaults.OptionsSheetName);
        var lastDataRow = PackNameImportDefaults.MaxRows + 1;

        for (var index = 0; index < layout.Columns.Count; index++)
        {
            var column = layout.Columns[index];
            var columnNumber = index + 1;
            var header = sheet.Cell(1, columnNumber);

            header.Value = column.Header;
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");

            var cells = sheet.Range(2, columnNumber, lastDataRow, columnNumber);

            switch (column.Kind)
            {
                case PackNameImportColumnKind.Dropdown when column.Options.Count > 0:
                    for (var optionIndex = 0; optionIndex < column.Options.Count; optionIndex++)
                        options.Cell(optionIndex + 1, columnNumber).Value = column.Options[optionIndex].Text;

                    var validation = cells.CreateDataValidation();
                    validation.List(options.Range(1, columnNumber, column.Options.Count, columnNumber), true);
                    validation.IgnoreBlanks = column.AllowBlank;
                    validation.InCellDropdown = true;

                    //a checkbox list takes several options, comma separated, which the dropdown alone cannot give
                    if (column.SeveralOptions)
                        validation.ShowErrorMessage = false;
                    break;

                case PackNameImportColumnKind.Date:
                    cells.Style.DateFormat.Format = PackNameImportDefaults.ExcelDateFormat;
                    break;

                case PackNameImportColumnKind.Decimal:
                    cells.Style.NumberFormat.Format = "0.00";
                    break;

                default:
                    //text, so an employee number such as 007 keeps its zeros
                    cells.Style.NumberFormat.Format = "@";
                    break;
            }

            sheet.Column(columnNumber).Width = Math.Max(14, column.Header.Length + 4);
        }

        sheet.SheetView.FreezeRows(1);
        options.Visibility = XLWorksheetVisibility.VeryHidden;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return Task.FromResult(stream.ToArray());
    }

    public virtual async Task<PackNameImportResult> ImportAsync(PackNameImportLayout layout, Stream file, int languageId)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(file);

        var result = new PackNameImportResult();
        var progress = new WriteProgress();
        ParsedFile parsedFile = null;

        try
        {
            parsedFile = await ReadFileAsync(layout, file, result);

            if (parsedFile == null || result.FileErrors.Count > 0)
                return result;

            var rows = await ValidateRowsAsync(parsedFile, layout, languageId, result);

            if (!result.Success)
                return result;

            await WriteRowsAsync(rows, layout, result, progress);
        }
        catch (Exception exception)
        {
            //logged here, once the batch's transaction is gone: a log entry written inside it would be rolled back with it
            await _logger.ErrorAsync(progress.Row == 0
                    ? "Excel import of packnames failed while checking the file; nothing was imported"
                    : result.PackNamesCreated > 0
                        ? $"Excel import of packnames failed at row {progress.Row}; the rows before row {progress.BatchFirstRow} were imported"
                        : $"Excel import of packnames failed at row {progress.Row}; nothing was imported",
                exception, await _workContext.GetCurrentCustomerAsync());

            //what the checks found before the failure is not the whole answer, so it is not shown. What the committed
            //batches created stays in the store, so it is reported with the row the import stopped at
            result.RowErrors.Clear();
            result.FileErrors.Clear();
            result.RefusedOnWrite = true;
            result.FileErrors.Add(progress.Row > 0
                ? string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.Unexpected"), progress.Row)
                : await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNames.Import.Errors.UnexpectedCheck"));

            if (result.PackNamesCreated > 0)
                result.FirstRowNotImported = progress.BatchFirstRow;
        }
        finally
        {
            parsedFile?.Workbook.Dispose();
        }

        //once, after the write phase however it ended, and outside the try: a failure here is not a failed row
        if (progress.Row > 0)
            await FinishWriteAsync(layout, result, progress);

        return result;
    }

    public virtual async Task<byte[]> BuildErrorReportAsync(PackNameImportLayout layout, Stream file)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(file);

        var result = new PackNameImportResult();
        ParsedFile parsedFile = null;

        try
        {
            parsedFile = await ReadFileAsync(layout, file, result);

            if (parsedFile == null)
                return null;

            if (result.FileErrors.Count == 0)
                await ValidateRowsAsync(parsedFile, layout, 0, result);

            return BuildErrorReport(parsedFile, result);
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("The error report of an Excel import of packnames could not be built", exception, await _workContext.GetCurrentCustomerAsync());

            return null;
        }
        finally
        {
            parsedFile?.Workbook.Dispose();
        }
    }

    #endregion

    #region Utilities - error report

    /// <summary>
    /// The checked file with an Errors column that names each failing row's problems
    /// </summary>
    protected virtual byte[] BuildErrorReport(ParsedFile parsedFile, PackNameImportResult result)
    {
        //a report uploaded again gets its Errors column rewritten, not a second one
        var errorsColumn = parsedFile.ErrorsColumn > 0 ? parsedFile.ErrorsColumn : parsedFile.LastColumn + 1;

        if (parsedFile.ErrorsColumn > 0)
            parsedFile.Sheet.Column(errorsColumn).Clear(XLClearOptions.Contents);

        var header = parsedFile.Sheet.Cell(1, errorsColumn);

        header.Value = PackNameImportDefaults.ErrorsHeader;
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8CBAD");

        foreach (var rowError in result.RowErrors)
        {
            var cell = parsedFile.Sheet.Cell(rowError.Row, errorsColumn);

            cell.Value = string.Join(Environment.NewLine, rowError.Messages);
            cell.Style.Alignment.WrapText = true;
        }

        parsedFile.Sheet.Column(errorsColumn).Width = 60;

        using var stream = new MemoryStream();
        parsedFile.Workbook.SaveAs(stream);

        return stream.ToArray();
    }

    #endregion
}