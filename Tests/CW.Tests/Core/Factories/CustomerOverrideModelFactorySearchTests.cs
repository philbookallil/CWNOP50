using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.Mapper;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Customers;
using Nop.Plugin.CW.Core.Infrastructure.Mapper;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Infrastructure.Mapper;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The admin customer list opens on registered customers, as nop's does: CW's search model keeps the role nop selects
/// </summary>
[TestFixture]
[NonParallelizable]
public class CustomerOverrideModelFactorySearchTests
{
    private IEngine? _previousEngine;

    [OneTimeSetUp]
    public void InitMapper()
    {
        //as nop does at start-up, when it finds the plugin's profile before its own: nop's admin rules then apply to
        //CW's maps too
        var profiles = new IOrderedMapperProfile[] { new CWMapperConfigurationProfile(), new AdminMapperConfiguration() };
        MapperConfiguration.Init(profiles.OrderBy(profile => profile.Order));
    }

    [SetUp]
    public void SetUp()
    {
        _previousEngine = EngineContext.Current;
        var engine = new Mock<IEngine>();
        engine.Setup(e => e.Resolve<AdminAreaSettings>(It.IsAny<Microsoft.Extensions.DependencyInjection.IServiceScope>()))
            .Returns(new AdminAreaSettings { DefaultGridPageSize = 15 });
        EngineContext.Replace(engine.Object);
    }

    [TearDown]
    public void TearDown() => EngineContext.Replace(_previousEngine!);

    /// <summary>
    /// Set the field on CW's factory and on nop's beneath it (CW's hides some of nop's with its own type)
    /// </summary>
    private static void SetField(object target, string name, object value)
    {
        var found = false;
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null)
                continue;

            field.SetValue(target, value);
            found = true;
        }

        if (!found)
            throw new MissingFieldException(name);
    }

    [Test]
    public async Task The_customer_list_opens_on_registered_customers()
    {
        var customerService = new Mock<ICustomerOverrideService>();
        customerService.Setup(service => service.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.RegisteredRoleName))
            .ReturnsAsync(new CustomerRole { Id = 3, SystemName = NopCustomerDefaults.RegisteredRoleName });
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        var factory = (CustomerOverrideModelFactory)RuntimeHelpers.GetUninitializedObject(typeof(CustomerOverrideModelFactory));
        SetField(factory, "_customerSettings", new CustomerSettings());
        SetField(factory, "_customerService", customerService.Object);
        SetField(factory, "_localizationService", localizationService.Object);
        SetField(factory, "_baseAdminModelFactory", new Mock<IBaseAdminModelFactory>().Object);

        var model = await factory.PrepareCustomerSearchModelAsync(new ExtendedCustomerSearchModel());

        model.SelectedCustomerRoleIds.Should().Equal(3);
    }
}
