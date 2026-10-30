using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Everything needed to create one packname the way a manager does: the packname itself, optionally the customer
/// account behind it, optionally a delivery address, and the subsidies it starts with
/// </summary>
public record PackNameRegistration
{
    /// <summary>
    /// The store the packname belongs to
    /// </summary>
    public int StoreId { get; init; }

    /// <summary>
    /// The branch the packname is created under; the customer, when one is created, is linked to it
    /// </summary>
    public Branch Branch { get; init; }

    /// <summary>
    /// The list the packname is created in
    /// </summary>
    public PackNameList PackNameList { get; init; }

    /// <summary>
    /// The customer identifier recorded as the creator of the subsidy assignments
    /// </summary>
    public int CreatedByCustomerId { get; init; }

    /// <summary>
    /// Whether an administrator registers the packname: what the store lets its managers do (pick subsidies) does not
    /// limit an administrator
    /// </summary>
    public bool ByAdministrator { get; init; }

    /// <summary>
    /// The language the welcome e-mail is sent in
    /// </summary>
    public int LanguageId { get; init; }

    public string Name { get; init; }

    public string PrintName { get; init; }

    public bool Group { get; init; }

    public string Gender { get; init; }

    /// <summary>
    /// A hex colour such as "#1A2B3C"; a random one is used when this is empty or not a hex colour
    /// </summary>
    public string Colour { get; init; }

    public string Comments { get; init; }

    public string EmployeeNumber { get; init; }

    public string POEntry { get; init; }

    public int? DepartmentId { get; init; }

    public int? CostCenterId { get; init; }

    /// <summary>
    /// The customer account to create for the packname; null for a packname without one
    /// </summary>
    public PackNameCustomerRegistration Customer { get; init; }

    /// <summary>
    /// A delivery address to attach to the packname, with its custom attributes already parsed; null for none
    /// </summary>
    public Address Address { get; init; }

    /// <summary>
    /// The subsidies to assign; a subsidy named twice is assigned once
    /// </summary>
    public IList<PackNameSubsidyAssignment> Subsidies { get; init; } = new List<PackNameSubsidyAssignment>();
}

/// <summary>
/// The customer account created for a packname
/// </summary>
public record PackNameCustomerRegistration
{
    public string Email { get; init; }

    public string Username { get; init; }

    public string Gender { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public DateTime? DateOfBirth { get; init; }

    public string StreetAddress { get; init; }

    public string StreetAddress2 { get; init; }

    public string ZipPostalCode { get; init; }

    public string City { get; init; }

    public string County { get; init; }

    public int CountryId { get; init; }

    public int StateProvinceId { get; init; }

    public string Phone { get; init; }

    public string Fax { get; init; }

    /// <summary>
    /// The custom customer attributes, already parsed and validated
    /// </summary>
    public string CustomAttributesXml { get; init; }
}

/// <summary>
/// A subsidy assigned to a new packname, with the packname's own credit and ordering period when the store allows them
/// </summary>
public record PackNameSubsidyAssignment
{
    public int SubsidyId { get; init; }

    /// <summary>
    /// The packname's own credit for the subsidy; null keeps the subsidy's
    /// </summary>
    public decimal? Credit { get; init; }

    /// <summary>
    /// The packname's own ordering period start; null keeps the subsidy's
    /// </summary>
    public DateTime? DurationStart { get; init; }

    /// <summary>
    /// The packname's own ordering period end; null keeps the subsidy's
    /// </summary>
    public DateTime? DurationEnd { get; init; }
}

/// <summary>
/// What registering a packname produced
/// </summary>
public record PackNameRegistrationResult
{
    /// <summary>
    /// The packname created; null when the registration failed
    /// </summary>
    public PackName PackName { get; init; }

    /// <summary>
    /// The customer created; null when no account was asked for or the registration failed
    /// </summary>
    public Nop.Core.Domain.Customers.Customer Customer { get; init; }

    /// <summary>
    /// Whether the account's welcome e-mail was queued; false without an account or when the store's welcome message
    /// is switched off
    /// </summary>
    public bool WelcomeEmailQueued { get; init; }

    /// <summary>
    /// Why the registration failed; empty on success
    /// </summary>
    public IList<string> Errors { get; init; } = new List<string>();

    /// <summary>
    /// Whether the registration was refused because the name was taken in the list since the caller checked it
    /// </summary>
    public bool NameTaken { get; init; }

    public bool Success => Errors.Count == 0 && PackName != null;
}