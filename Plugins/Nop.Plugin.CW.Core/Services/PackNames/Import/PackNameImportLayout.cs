using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using System.Text.RegularExpressions;

namespace Nop.Plugin.CW.Core.Services.PackNames.Import;

/// <summary>
/// How a cell of an import column is filled
/// </summary>
public enum PackNameImportColumnKind
{
    /// <summary>
    /// Free text
    /// </summary>
    Text,

    /// <summary>
    /// One of the column's options, offered as a dropdown in the template and matched by name on import
    /// </summary>
    Dropdown,

    /// <summary>
    /// A date, as an Excel date or as text in the import's date format
    /// </summary>
    Date,

    /// <summary>
    /// A decimal number
    /// </summary>
    Decimal
}

/// <summary>
/// One column of the import file
/// </summary>
public class PackNameImportColumn
{
    /// <summary>
    /// What the column is, independent of its header
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// The header the template writes and the import looks for (case does not matter)
    /// </summary>
    public string Header { get; init; }

    public PackNameImportColumnKind Kind { get; init; } = PackNameImportColumnKind.Text;

    /// <summary>
    /// The options of a dropdown column: the text shown in Excel and the identifier it stands for
    /// </summary>
    public IList<(string Text, int Id)> Options { get; init; } = new List<(string Text, int Id)>();

    /// <summary>
    /// Whether a dropdown column may be left blank
    /// </summary>
    public bool AllowBlank { get; init; } = true;

    /// <summary>
    /// Whether the import refuses a file without this column
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// The attribute a custom customer or address attribute column stands for; null for the other columns
    /// </summary>
    public int? AttributeId { get; init; }

    /// <summary>
    /// The most characters the database field behind the column holds; null when it is not limited
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Whether a cell may name several of the column's options, comma separated (a checkbox list attribute)
    /// </summary>
    public bool SeveralOptions { get; init; }

    /// <summary>
    /// The identifier an option text stands for, or null when the text is not one of the options
    /// </summary>
    public int? OptionId(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();

        foreach (var (optionText, id) in Options)
        {
            if (string.Equals(optionText.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                return id;
        }

        return null;
    }
}

/// <summary>
/// One group of subsidy columns: the subsidy, and the packname's own credit and ordering period when the store allows them
/// </summary>
public class PackNameImportSubsidyGroup
{
    public PackNameImportColumn Subsidy { get; init; }

    public PackNameImportColumn Credit { get; init; }

    public PackNameImportColumn DurationStart { get; init; }

    public PackNameImportColumn DurationEnd { get; init; }
}

/// <summary>
/// The columns of an import file and the data they are checked against. Built once per request from the store's
/// settings and the importer's scope (a manager's, or an administrator's); the template writer and the importer both
/// read it
/// </summary>
public class PackNameImportLayout
{
    public int StoreId { get; init; }

    public MultiStoreSettings StoreSettings { get; init; }

    /// <summary>
    /// The manager the file is imported for; null when an administrator imports it
    /// </summary>
    public PackName Manager { get; init; }

    /// <summary>
    /// The customer recorded as the creator of what the file creates: the manager's account, or the administrator
    /// </summary>
    public int CreatedByCustomerId { get; init; }

    /// <summary>
    /// The branches the file may import into: the store's active branches, or for a manager under
    /// LimitAssignedBranches only those where the manager owns a packname list
    /// </summary>
    public IList<Branch> Branches { get; init; } = new List<Branch>();

    public IList<Country> Countries { get; init; } = new List<Country>();

    /// <summary>
    /// The states of every country, keyed by country identifier
    /// </summary>
    public IDictionary<int, IList<StateProvince>> StatesByCountry { get; init; } = new Dictionary<int, IList<StateProvince>>();

    /// <summary>
    /// The custom customer attributes a registration carries: those with a column, and read-only checkboxes, which
    /// take their pre-selected values
    /// </summary>
    public IList<CustomerAttribute> CustomerAttributes { get; init; } = new List<CustomerAttribute>();

    /// <summary>
    /// The custom address attributes a registration carries: those with a column, and read-only checkboxes, which
    /// take their pre-selected values
    /// </summary>
    public IList<AddressAttribute> AddressAttributes { get; init; } = new List<AddressAttribute>();

    /// <summary>
    /// Whether the file carries the customer account columns
    /// </summary>
    public bool CustomerColumns { get; init; }

    /// <summary>
    /// Whether the file carries the delivery address columns
    /// </summary>
    public bool AddressColumns { get; init; }

    /// <summary>
    /// The subsidy column groups; empty when the store offers no subsidy selection
    /// </summary>
    public IList<PackNameImportSubsidyGroup> SubsidyGroups { get; init; } = new List<PackNameImportSubsidyGroup>();

    /// <summary>
    /// Every column, in template order
    /// </summary>
    public IList<PackNameImportColumn> Columns { get; init; } = new List<PackNameImportColumn>();

    /// <summary>
    /// The column with this key, or null when the layout has none
    /// </summary>
    public PackNameImportColumn Column(string key) => Columns.FirstOrDefault(column => column.Key == key);
}

/// <summary>
/// The keys of the import columns
/// </summary>
public static class PackNameImportColumns
{
    public const string Branch = "Branch";
    public const string Department = "Department";
    public const string CostCenter = "CostCenter";
    public const string Name = "Name";
    public const string Group = "Group";
    public const string Colour = "Colour";
    public const string EmployeeNumber = "EmployeeNumber";
    public const string Gender = "Gender";
    public const string PrintName = "PrintName";
    public const string POEntry = "POEntry";
    public const string Comments = "Comments";

    public const string AddCustomer = "AddCustomer";
    public const string Email = "Email";
    public const string Username = "Username";
    public const string FirstName = "FirstName";
    public const string LastName = "LastName";
    public const string DateOfBirth = "DateOfBirth";
    public const string CustomerGender = "CustomerGender";
    public const string StreetAddress = "StreetAddress";
    public const string StreetAddress2 = "StreetAddress2";
    public const string ZipPostalCode = "ZipPostalCode";
    public const string City = "City";
    public const string County = "County";
    public const string Country = "Country";
    public const string StateProvince = "StateProvince";
    public const string Phone = "Phone";
    public const string Fax = "Fax";
    public const string CustomerAttributePrefix = "CustomerAttribute:";

    public const string AddAddress = "AddAddress";
    public const string AddressFirstName = "AddressFirstName";
    public const string AddressLastName = "AddressLastName";
    public const string AddressEmail = "AddressEmail";
    public const string AddressAddress1 = "AddressAddress1";
    public const string AddressAddress2 = "AddressAddress2";
    public const string AddressZipPostalCode = "AddressZipPostalCode";
    public const string AddressCity = "AddressCity";
    public const string AddressCounty = "AddressCounty";
    public const string AddressCountry = "AddressCountry";
    public const string AddressStateProvince = "AddressStateProvince";
    public const string AddressPhone = "AddressPhone";
    public const string AddressFax = "AddressFax";
    public const string AddressAttributePrefix = "AddressAttribute:";

    /// <summary>
    /// The start every key of the delivery address block shares (its Yes / No column, AddAddress, does not)
    /// </summary>
    public const string AddressBlockPrefix = "Address";

    public const string SubsidyPrefix = "Subsidy:";
    public const string SubsidyCreditPrefix = "SubsidyCredit:";
    public const string SubsidyStartPrefix = "SubsidyStart:";
    public const string SubsidyEndPrefix = "SubsidyEnd:";
}

/// <summary>
/// The headers of the import columns, as the template writes them and the import looks for them
/// </summary>
public static class PackNameImportHeaders
{
    public const string Branch = "Branch";
    public const string Name = "Name";
    public const string Group = "Group";
    public const string Department = "Department";
    public const string CostCenter = "Cost centre";
    public const string EmployeeNumber = "Employee number";
    public const string Gender = "Gender";
    public const string PrintName = "Print name";
    public const string POEntry = "PO entry";
    public const string Colour = "Colour";
    public const string Comments = "Comments";

    public const string SubsidyFormat = "Subsidy {0}";
    public const string SubsidyCreditFormat = "Subsidy {0} credit";
    public const string SubsidyStartFormat = "Subsidy {0} start";
    public const string SubsidyEndFormat = "Subsidy {0} end";

    public const string AddCustomer = "Add customer";
    public const string Email = "Email";
    public const string Username = "Username";
    public const string FirstName = "First name";
    public const string LastName = "Last name";
    public const string DateOfBirth = "Date of birth";
    public const string CustomerGender = "Customer gender";
    public const string StreetAddress = "Street address";
    public const string StreetAddress2 = "Street address 2";
    public const string ZipPostalCode = "Postcode";
    public const string City = "City";
    public const string County = "County";
    public const string Country = "Country code";
    public const string StateProvince = "State";
    public const string Phone = "Phone";
    public const string Fax = "Fax";
    public const string CustomerAttributePrefix = "Customer: ";

    public const string AddAddress = "Add address";
    public const string AddressFirstName = "Address first name";
    public const string AddressLastName = "Address last name";
    public const string AddressEmail = "Address email";
    public const string AddressAddress1 = "Address street";
    public const string AddressAddress2 = "Address street 2";
    public const string AddressZipPostalCode = "Address postcode";
    public const string AddressCity = "Address city";
    public const string AddressCounty = "Address county";
    public const string AddressCountry = "Address country code";
    public const string AddressStateProvince = "Address state";
    public const string AddressPhone = "Address phone";
    public const string AddressFax = "Address fax";
    public const string AddressAttributePrefix = "Address: ";

    /// <summary>
    /// The headers a store's settings can switch off; Branch and Name are always there
    /// </summary>
    private static readonly string[] _optional =
    [
        Group, Department, CostCenter, EmployeeNumber, Gender, PrintName, POEntry, Colour, Comments,
        AddCustomer, Email, Username, FirstName, LastName, DateOfBirth, CustomerGender, StreetAddress, StreetAddress2,
        ZipPostalCode, City, County, Country, StateProvince, Phone, Fax,
        AddAddress, AddressFirstName, AddressLastName, AddressEmail, AddressAddress1, AddressAddress2, AddressZipPostalCode,
        AddressCity, AddressCounty, AddressCountry, AddressStateProvince, AddressPhone, AddressFax
    ];

    /// <summary>
    /// Whether the header is one some template writes: a fixed header, a subsidy group's or a custom attribute's
    /// </summary>
    public static bool IsKnown(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return false;

        var trimmed = header.Trim();

        return _optional.Any(known => string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
            || Regex.IsMatch(trimmed, @"^Subsidy \d+( credit| start| end)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || trimmed.StartsWith(CustomerAttributePrefix, StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith(AddressAttributePrefix, StringComparison.OrdinalIgnoreCase);
    }
}