using Nop.Data;
using Nop.Plugin.CW.Freight.Domain;
using Nop.Services.Shipping;

namespace Nop.Plugin.CW.Freight.Services;

public class FreightService : IFreightService
{
    #region Fields

    protected readonly IRepository<CWShippingMethod> _shippingMethodRepository;
    protected readonly IRepository<CWShippingMethodStoreMapping> _storeMappingRepository;
    protected readonly IRepository<CWShippingMethodCustomerRoleMapping> _customerRoleMappingRepository;
    protected readonly IRepository<CWShippingMethodCountryMapping> _countryMappingRepository;

    #endregion

    #region Ctor

    public FreightService(
        IRepository<CWShippingMethod> shippingMethodRepository,
        IRepository<CWShippingMethodStoreMapping> storeMappingRepository,
        IRepository<CWShippingMethodCustomerRoleMapping> customerRoleMappingRepository,
        IRepository<CWShippingMethodCountryMapping> countryMappingRepository)
    {
        _shippingMethodRepository = shippingMethodRepository;
        _storeMappingRepository = storeMappingRepository;
        _customerRoleMappingRepository = customerRoleMappingRepository;
        _countryMappingRepository = countryMappingRepository;
    }

    #endregion

    #region Shipping methods

    /// <inheritdoc />
    public virtual async Task<IList<CWShippingMethod>> GetAllCwShippingMethodsAsync(int countryId = 0, int storeId = 0)
    {
        var methods = await _shippingMethodRepository.GetAllAsync(query =>
            query.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name));

        var storeMappings = storeId > 0
            ? await _storeMappingRepository.GetAllAsync(query => query)
            : new List<CWShippingMethodStoreMapping>();

        var countryMappings = countryId > 0
            ? await _countryMappingRepository.GetAllAsync(query => query.Where(m => m.CountryId == countryId))
            : new List<CWShippingMethodCountryMapping>();

        return FreightMethodFilter.Filter(methods, storeMappings, countryMappings, countryId, storeId);
    }

    /// <inheritdoc />
    public virtual async Task<CWShippingMethod> GetCwShippingMethodByIdAsync(int id)
    {
        return await _shippingMethodRepository.GetByIdAsync(id);
    }

    /// <inheritdoc />
    public virtual async Task InsertCwShippingMethodAsync(CWShippingMethod shippingMethod)
    {
        await _shippingMethodRepository.InsertAsync(shippingMethod);
    }

    /// <inheritdoc />
    public virtual async Task UpdateCwShippingMethodAsync(CWShippingMethod shippingMethod)
    {
        await _shippingMethodRepository.UpdateAsync(shippingMethod);
    }

    /// <inheritdoc />
    public virtual async Task DeleteCwShippingMethodAsync(CWShippingMethod shippingMethod)
    {
        await DeleteStoreMappingsForMethodAsync(shippingMethod.Id);
        await DeleteCustomerRoleMappingsForMethodAsync(shippingMethod.Id);
        await DeleteCountryMappingsForMethodAsync(shippingMethod.Id);

        await _shippingMethodRepository.DeleteAsync(shippingMethod);
    }

    /// <inheritdoc />
    public virtual Task<decimal> DetermineShippingMethodRateAsync(CWShippingMethod shippingMethod, GetShippingOptionRequest request)
    {
        //the stored rate as it is: the order-total band and ApplyPerBranch are not applied
        return Task.FromResult(shippingMethod.Rate);
    }

    #endregion

    #region Store mappings

    /// <inheritdoc />
    public virtual async Task<IList<CWShippingMethodStoreMapping>> GetStoreMappingsForMethodAsync(int shippingMethodId)
    {
        return await _storeMappingRepository.GetAllAsync(query =>
            query.Where(m => m.ShippingMethodId == shippingMethodId));
    }

    /// <inheritdoc />
    public virtual async Task InsertStoreMappingAsync(CWShippingMethodStoreMapping mapping)
    {
        await _storeMappingRepository.InsertAsync(mapping);
    }

    /// <inheritdoc />
    public virtual async Task DeleteStoreMappingsForMethodAsync(int shippingMethodId)
    {
        await _storeMappingRepository.DeleteAsync(m => m.ShippingMethodId == shippingMethodId);
    }

    #endregion

    #region Customer role mappings

    /// <inheritdoc />
    public virtual async Task<IList<CWShippingMethodCustomerRoleMapping>> GetCustomerRoleMappingsForMethodAsync(int shippingMethodId)
    {
        return await _customerRoleMappingRepository.GetAllAsync(query =>
            query.Where(m => m.ShippingMethodId == shippingMethodId));
    }

    /// <inheritdoc />
    public virtual async Task InsertCustomerRoleMappingAsync(CWShippingMethodCustomerRoleMapping mapping)
    {
        await _customerRoleMappingRepository.InsertAsync(mapping);
    }

    /// <inheritdoc />
    public virtual async Task DeleteCustomerRoleMappingsForMethodAsync(int shippingMethodId)
    {
        await _customerRoleMappingRepository.DeleteAsync(m => m.ShippingMethodId == shippingMethodId);
    }

    #endregion

    #region Country mappings

    /// <inheritdoc />
    public virtual async Task<IList<CWShippingMethodCountryMapping>> GetCountryMappingsForMethodAsync(int shippingMethodId)
    {
        return await _countryMappingRepository.GetAllAsync(query =>
            query.Where(m => m.ShippingMethodId == shippingMethodId));
    }

    /// <inheritdoc />
    public virtual async Task InsertCountryMappingAsync(CWShippingMethodCountryMapping mapping)
    {
        await _countryMappingRepository.InsertAsync(mapping);
    }

    /// <inheritdoc />
    public virtual async Task DeleteCountryMappingsForMethodAsync(int shippingMethodId)
    {
        await _countryMappingRepository.DeleteAsync(m => m.ShippingMethodId == shippingMethodId);
    }

    #endregion
}
