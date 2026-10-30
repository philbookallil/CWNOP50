using Nop.Plugin.CW.Freight.Domain;

namespace Nop.Plugin.CW.Freight.Services;

/// <summary>
/// Picks the shipping methods offered in a store and to a country
/// </summary>
internal static class FreightMethodFilter
{
    /// <summary>
    /// Filters the shipping methods by store and country, keeping their order
    /// </summary>
    /// <param name="methods">All shipping methods, in the order to offer them</param>
    /// <param name="storeMappings">All store mappings</param>
    /// <param name="countryMappings">All restricted-country mappings</param>
    /// <param name="countryId">Country of the shipping address; 0 for no country</param>
    /// <param name="storeId">Store identifier; 0 for every store</param>
    /// <returns>The methods offered</returns>
    /// <remarks>
    /// A method with no store mapping is offered in every store. A StoreId of 0 means every store too: 4.9 offered
    /// "All" as a store with value 0 and saved it as a row.
    /// </remarks>
    public static IList<CWShippingMethod> Filter(IEnumerable<CWShippingMethod> methods,
        IEnumerable<CWShippingMethodStoreMapping> storeMappings,
        IEnumerable<CWShippingMethodCountryMapping> countryMappings,
        int countryId,
        int storeId)
    {
        var result = methods;

        if (storeId > 0)
        {
            var storeRows = storeMappings.Where(m => m.StoreId != 0).ToList();
            var methodIdsForStore = storeRows.Where(m => m.StoreId == storeId).Select(m => m.ShippingMethodId).ToHashSet();
            var methodIdsWithStores = storeRows.Select(m => m.ShippingMethodId).ToHashSet();

            result = result.Where(m => !methodIdsWithStores.Contains(m.Id) || methodIdsForStore.Contains(m.Id));
        }

        if (countryId > 0)
        {
            var restrictedMethodIds = countryMappings
                .Where(m => m.CountryId == countryId)
                .Select(m => m.ShippingMethodId)
                .ToHashSet();

            result = result.Where(m => !restrictedMethodIds.Contains(m.Id));
        }

        return result.ToList();
    }
}
