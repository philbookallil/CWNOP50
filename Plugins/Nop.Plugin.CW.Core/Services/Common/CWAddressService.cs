using Nop.Core.Domain.Common;
using Nop.Data;

namespace Nop.Plugin.CW.Core.Services.Common;

public class CWAddressService : ICWAddressService
{
    #region Fields

    protected readonly IRepository<Address> _addressRepository;

    #endregion

    #region Ctor

    public CWAddressService(IRepository<Address> addressRepository)
    {
        _addressRepository = addressRepository;
    }

    #endregion

    #region Utilities

    private static void CopyAddressValues(Address source, Address target)
    {
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.Email = source.Email;
        target.Company = source.Company;
        target.CountryId = source.CountryId;
        target.StateProvinceId = source.StateProvinceId;
        target.County = source.County;
        target.City = source.City;
        target.Address1 = source.Address1;
        target.Address2 = source.Address2;
        target.ZipPostalCode = source.ZipPostalCode;
        target.PhoneNumber = source.PhoneNumber;
        target.FaxNumber = source.FaxNumber;
        target.CustomAttributes = source.CustomAttributes;
    }

    #endregion

    #region Methods

    public async Task<int> InsertCheckoutAddressAsync(Address checkoutAddress)
    {
        var address = new Address();
        CopyAddressValues(checkoutAddress, address);

        //some validation
        if (address.CountryId == 0)
            address.CountryId = null;
        if (address.StateProvinceId == 0)
            address.StateProvinceId = null;

        await _addressRepository.InsertAsync(address);

        return address.Id;
    }

    #endregion
}
