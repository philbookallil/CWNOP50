using System.Collections;
using System.Reflection;
using AwesomeAssertions;
using Nop.Core.Infrastructure.Mapper;
using Nop.Plugin.CW.Core.Infrastructure.Mapper;
using Nop.Web.Areas.Admin.Infrastructure.Mapper;
using NUnit.Framework;

namespace CW.Tests.Core.Infrastructure;

/// <summary>
/// Every CW map copies what it says it copies, with nop's admin mapping rules loaded after CW's profile.
/// </summary>
/// <remarks>
/// nop 5.00 maps with Mapster, and nop's admin profile applies generic rules (<c>ForAllMaps</c>) to every map that exists
/// when it runs, CW's included when nop happens to find CW's profile first: an ACL-supported model loses its selected
/// customer roles, a store-mapped one its stores, a discount-supported one its discounts, any model its custom
/// properties. 4.9's AutoMapper kept each profile's rules to itself. A CW map that relies on one of those members being
/// copied works or not depending on the load order; this test fails for it. The members listed here are the ones CW's
/// profile leaves out on purpose.
/// </remarks>
[TestFixture]
[NonParallelizable]
public class CWMapperGlobalRulesTests
{
    private static readonly Dictionary<string, string[]> LeftOutOnPurpose = new()
    {
        ["Customer->ExtendedCustomerModel"] =
        [
            "Email", "Company", "Phone", "ZipPostalCode", "Gender", "FirstName", "LastName", "DateOfBirth", "StreetAddress",
            "StreetAddress2", "City", "County", "CountryId", "StateProvinceId", "Fax", "TimeZoneId", "VatNumber"
        ],
        ["PackNameListModel->PackNameList"] = ["StoreId", "BranchId", "CreatedOnUtc"],
        ["PackNameModel->PackName"] = ["PackNameListId"],
        ["ProductGroupModel->ProductGroup"] = ["StoreId", "CreatedOnUtc"],
        ["SubsidyModel->Subsidy"] = ["StoreId", "CreatedOnUtc"],
        ["SubsidyTopupModel->SubsidyTopup"] = ["CreatedOnUtc"],
        ["SubsidyVariationModel->SubsidyVariation"] = ["CreatedOnUtc"],
        ["BranchModel->Branch"] = ["CreatedOnUtc", "UpdatedOnUtc", "DomainRegistrationFilter", "Deleted"],
    };

    [OneTimeSetUp]
    public void InitMapper()
    {
        //nop finds CW's profile first, so nop's admin rules reach CW's maps
        var profiles = new IOrderedMapperProfile[] { new CWMapperConfigurationProfile(), new AdminMapperConfiguration() };
        MapperConfiguration.Init(profiles.OrderBy(profile => profile.Order));
    }

    private static int _seed = 1000;

    /// <summary>
    /// A distinct value in every simple settable property, so a member left behind shows as a default
    /// </summary>
    private static object Filled(Type type)
    {
        var model = Activator.CreateInstance(type)!;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object? value = propertyType == typeof(string) ? $"value {++_seed}"
                : propertyType == typeof(int) ? ++_seed
                : propertyType == typeof(decimal) ? ++_seed + 0.5m
                : propertyType == typeof(bool) ? true
                : propertyType == typeof(DateTime) ? new DateTime(2020, 1, 1).AddDays(++_seed)
                : propertyType == typeof(IList<int>) || propertyType == typeof(List<int>) ? new List<int> { ++_seed }
                : propertyType.IsEnum && Enum.GetValues(propertyType).Length > 1 ? Enum.GetValues(propertyType).GetValue(1)
                : null;

            if (value != null)
                property.SetValue(model, value);
        }

        return model;
    }

    private static string Show(object? value) =>
        value is IEnumerable items and not string ? "[" + string.Join(",", items.Cast<object>()) + "]" : value?.ToString() ?? "null";

    public static IEnumerable<TestCaseData> CWMaps()
    {
        //every map CW's profile creates has a CW type on one side
        _ = new CWMapperConfigurationProfile();
        var cwAssembly = typeof(CWMapperConfigurationProfile).Assembly;

        return MapperConfiguration.RuleMap.Keys
            .Where(key => key.Source.Assembly == cwAssembly || key.Destination.Assembly == cwAssembly)
            .Select(key => new TestCaseData(key.Source, key.Destination).SetArgDisplayNames($"{key.Source.Name}->{key.Destination.Name}"));
    }

    [TestCaseSource(nameof(CWMaps))]
    public void A_CW_map_copies_every_member_it_does_not_leave_out_on_purpose(Type source, Type destination)
    {
        var model = Filled(source);

        var mapped = MapperConfiguration.Mapper.Map(model, source, destination);

        LeftOutOnPurpose.TryGetValue($"{source.Name}->{destination.Name}", out var leftOut);
        var notCopied = destination.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Select(property => (property, from: source.GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance)))
            .Where(pair => pair.from is { CanRead: true } && pair.from.GetValue(model) != null)
            .Where(pair => Show(pair.from!.GetValue(model)) != Show(pair.property.GetValue(mapped)))
            .Select(pair => pair.property.Name)
            .Except(leftOut ?? [])
            .ToList();

        notCopied.Should().BeEmpty();
    }
}
