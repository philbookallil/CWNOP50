using Nop.Plugin.CW.Freight.Domain;
using Nop.Services.Shipping;

namespace Nop.Plugin.CW.Freight.Services;

/// <summary>
/// The CW shipping methods and their store, customer role and restricted-country mappings
/// </summary>
public interface IFreightService
{
    #region Shipping methods

    /// <summary>
    /// Gets the shipping methods offered in a store and to a country, by display order then name
    /// </summary>
    /// <param name="countryId">Country of the shipping address; 0 for no country filter</param>
    /// <param name="storeId">Store identifier; 0 for every store</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the methods</returns>
    Task<IList<CWShippingMethod>> GetAllCwShippingMethodsAsync(int countryId = 0, int storeId = 0);

    /// <summary>
    /// Gets a shipping method
    /// </summary>
    /// <param name="id">Shipping method identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the method, or null</returns>
    Task<CWShippingMethod> GetCwShippingMethodByIdAsync(int id);

    /// <summary>
    /// Inserts a shipping method
    /// </summary>
    /// <param name="shippingMethod">Shipping method</param>
    Task InsertCwShippingMethodAsync(CWShippingMethod shippingMethod);

    /// <summary>
    /// Updates a shipping method
    /// </summary>
    /// <param name="shippingMethod">Shipping method</param>
    Task UpdateCwShippingMethodAsync(CWShippingMethod shippingMethod);

    /// <summary>
    /// Deletes a shipping method and its mappings
    /// </summary>
    /// <param name="shippingMethod">Shipping method</param>
    Task DeleteCwShippingMethodAsync(CWShippingMethod shippingMethod);

    /// <summary>
    /// Gets the rate of a shipping method for a request
    /// </summary>
    /// <param name="shippingMethod">Shipping method</param>
    /// <param name="request">The shipping option request</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the rate</returns>
    Task<decimal> DetermineShippingMethodRateAsync(CWShippingMethod shippingMethod, GetShippingOptionRequest request);

    #endregion

    #region Store mappings

    /// <summary>
    /// Gets the store mappings of a shipping method
    /// </summary>
    /// <param name="shippingMethodId">Shipping method identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the mappings</returns>
    Task<IList<CWShippingMethodStoreMapping>> GetStoreMappingsForMethodAsync(int shippingMethodId);

    /// <summary>
    /// Inserts a store mapping
    /// </summary>
    /// <param name="mapping">Store mapping</param>
    Task InsertStoreMappingAsync(CWShippingMethodStoreMapping mapping);

    /// <summary>
    /// Deletes the store mappings of a shipping method
    /// </summary>
    /// <param name="shippingMethodId">Shipping method identifier</param>
    Task DeleteStoreMappingsForMethodAsync(int shippingMethodId);

    #endregion

    #region Customer role mappings

    /// <summary>
    /// Gets the customer role mappings of a shipping method
    /// </summary>
    /// <param name="shippingMethodId">Shipping method identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the mappings</returns>
    Task<IList<CWShippingMethodCustomerRoleMapping>> GetCustomerRoleMappingsForMethodAsync(int shippingMethodId);

    /// <summary>
    /// Inserts a customer role mapping
    /// </summary>
    /// <param name="mapping">Customer role mapping</param>
    Task InsertCustomerRoleMappingAsync(CWShippingMethodCustomerRoleMapping mapping);

    /// <summary>
    /// Deletes the customer role mappings of a shipping method
    /// </summary>
    /// <param name="shippingMethodId">Shipping method identifier</param>
    Task DeleteCustomerRoleMappingsForMethodAsync(int shippingMethodId);

    #endregion

    #region Country mappings

    /// <summary>
    /// Gets the restricted-country mappings of a shipping method
    /// </summary>
    /// <param name="shippingMethodId">Shipping method identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the mappings</returns>
    Task<IList<CWShippingMethodCountryMapping>> GetCountryMappingsForMethodAsync(int shippingMethodId);

    /// <summary>
    /// Inserts a restricted-country mapping
    /// </summary>
    /// <param name="mapping">Country mapping</param>
    Task InsertCountryMappingAsync(CWShippingMethodCountryMapping mapping);

    /// <summary>
    /// Deletes the restricted-country mappings of a shipping method
    /// </summary>
    /// <param name="shippingMethodId">Shipping method identifier</param>
    Task DeleteCountryMappingsForMethodAsync(int shippingMethodId);

    #endregion
}
