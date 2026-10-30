using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Models.PackNameCheckout;
using Nop.Plugin.CW.Core.Validators.PackNameCheckout;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Web.Models.Common;
using NUnit.Framework;

namespace CW.Tests.Core.Validators;

/// <summary>
/// An address added at the packname checkout takes nop's address rules
/// </summary>
[TestFixture]
public class PackNameShippingAddressValidatorTests
{
    private static PackNameShippingAddressValidator Validator()
    {
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        return new PackNameShippingAddressValidator(localizationService.Object, new Mock<IStateProvinceService>().Object,
            new AddressSettings { StreetAddressEnabled = true, StreetAddressRequired = true }, new CustomerSettings());
    }

    [Test]
    public async Task An_empty_address_is_refused()
    {
        var result = await Validator().ValidateAsync(new PackNameShippingAddressModel { PackNameId = 9 });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(["Address.FirstName", "Address.LastName", "Address.Email", "Address.Address1"]);
    }

    [Test]
    public async Task A_complete_address_passes()
    {
        var model = new PackNameShippingAddressModel
        {
            PackNameId = 9,
            Address = new AddressModel { FirstName = "Ann", LastName = "Smith", Email = "ann@example.com", Address1 = "12 High Street" }
        };

        var result = await Validator().ValidateAsync(model);

        result.Errors.Should().BeEmpty();
    }
}
