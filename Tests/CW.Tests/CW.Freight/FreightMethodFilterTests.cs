using AwesomeAssertions;
using Nop.Plugin.CW.Freight.Domain;
using Nop.Plugin.CW.Freight.Services;
using NUnit.Framework;

namespace CW.Tests.CW.Freight;

/// <summary>
/// The freight plugin offers a method in a store when the method has no store mapping or is mapped to that store, and
/// hides it from a country it is restricted in. CW.Core's request (store id, no country) must see every method of the
/// store.
/// </summary>
[TestFixture]
public class FreightMethodFilterTests
{
    #region Utilities

    private static readonly List<CWShippingMethod> _methods =
    [
        new() { Id = 1, Name = "Courier", DisplayOrder = 1 },
        new() { Id = 2, Name = "Express", DisplayOrder = 2 },
        new() { Id = 3, Name = "Pickup", DisplayOrder = 3 }
    ];

    private static IList<int> Filter(IEnumerable<CWShippingMethodStoreMapping> storeMappings,
        IEnumerable<CWShippingMethodCountryMapping> countryMappings, int countryId, int storeId)
    {
        return FreightMethodFilter.Filter(_methods, storeMappings, countryMappings, countryId, storeId)
            .Select(m => m.Id).ToList();
    }

    private static CWShippingMethodStoreMapping Store(int methodId, int storeId) =>
        new() { ShippingMethodId = methodId, StoreId = storeId };

    private static CWShippingMethodCountryMapping Country(int methodId, int countryId) =>
        new() { ShippingMethodId = methodId, CountryId = countryId };

    #endregion

    #region Store

    [Test]
    public void Method_without_store_mappings_is_offered_in_every_store()
    {
        Filter([], [], 0, 5).Should().Equal(1, 2, 3);
    }

    [Test]
    public void Method_mapped_to_other_stores_is_not_offered()
    {
        Filter([Store(2, 7)], [], 0, 5).Should().Equal(1, 3);
    }

    [Test]
    public void Method_mapped_to_the_store_is_offered()
    {
        Filter([Store(2, 7), Store(2, 5)], [], 0, 5).Should().Equal(1, 2, 3);
    }

    [Test]
    public void Store_row_of_zero_means_every_store()
    {
        Filter([Store(2, 0)], [], 0, 5).Should().Equal(1, 2, 3);
    }

    [Test]
    public void No_store_filter_offers_every_method()
    {
        Filter([Store(2, 7)], [], 0, 0).Should().Equal(1, 2, 3);
    }

    #endregion

    #region Country

    [Test]
    public void Method_restricted_in_the_country_is_not_offered()
    {
        Filter([], [Country(1, 13)], 13, 5).Should().Equal(2, 3);
    }

    [Test]
    public void Restriction_in_another_country_does_not_hide_the_method()
    {
        Filter([], [Country(1, 14)], 13, 5).Should().Equal(1, 2, 3);
    }

    [Test]
    public void Request_without_an_address_ignores_country_restrictions()
    {
        //CW.Core's IPackNameShippingMethodService: store id, one empty item, no address
        Filter([Store(3, 7)], [Country(1, 13)], 0, 5).Should().Equal(1, 2);
    }

    #endregion
}
