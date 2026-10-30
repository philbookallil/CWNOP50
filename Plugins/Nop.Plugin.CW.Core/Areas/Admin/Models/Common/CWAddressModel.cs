using Nop.Web.Areas.Admin.Models.Common;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Common;

public partial record CWAddressModel : BaseNopModel
{
    #region Ctor

    public CWAddressModel()
    {
        Address = new AddressModel();
    }

    #endregion

    #region Properties

    public int EntityId { get; set; }

    public string EntityName { get; set; }

    public AddressModel Address { get; set; }

    public bool IsEditable { get; set; }

    public string BranchCode { get; set; }

    /// <summary>
    /// Whether this address is the default shipping address for the branch.
    /// </summary>
    public bool IsDefault { get; set; }

    #endregion

    #region Inner Properties

    // Expose AddressModel properties directly for view access
    public int AddressId
    {
        get => Address.Id;
        set => Address.Id = value;
    }

    public string FirstName
    {
        get => Address.FirstName;
        set => Address.FirstName = value;
    }

    public string LastName
    {
        get => Address.LastName;
        set => Address.LastName = value;
    }

    public string Email
    {
        get => Address.Email;
        set => Address.Email = value;
    }

    public string Company
    {
        get => Address.Company;
        set => Address.Company = value;
    }

    public string CountryName
    {
        get => Address.CountryName;
        set => Address.CountryName = value;
    }

    public int? CountryId
    {
        get => Address.CountryId;
        set => Address.CountryId = value;
    }

    public string StateProvinceName
    {
        get => Address.StateProvinceName;
        set => Address.StateProvinceName = value;
    }

    public int? StateProvinceId
    {
        get => Address.StateProvinceId;
        set => Address.StateProvinceId = value;
    }

    public string City
    {
        get => Address.City;
        set => Address.City = value;
    }

    public string Address1
    {
        get => Address.Address1;
        set => Address.Address1 = value;
    }

    public string Address2
    {
        get => Address.Address2;
        set => Address.Address2 = value;
    }

    public string ZipPostalCode
    {
        get => Address.ZipPostalCode;
        set => Address.ZipPostalCode = value;
    }

    public string PhoneNumber
    {
        get => Address.PhoneNumber;
        set => Address.PhoneNumber = value;
    }

    public string FaxNumber
    {
        get => Address.FaxNumber;
        set => Address.FaxNumber = value;
    }

    public string AddressHtml
    {
        get => Address.AddressHtml;
        set => Address.AddressHtml = value;
    }

    #endregion
}
